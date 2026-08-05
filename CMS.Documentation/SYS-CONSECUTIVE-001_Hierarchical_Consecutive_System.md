# Sistema de Consecutivos Jerárquicos

## 📋 Resumen

Se implementó un sistema de consecutivos automáticos con lógica de herencia jerárquica basada en la estructura de menús del CMS. El sistema permite:

1. **Herencia por Menú**: Un consecutivo configurado en un menú padre aplica automáticamente a todos sus submenús
2. **Personalización por Submenú**: Un submenú puede tener su propio consecutivo específico
3. **Generación Thread-Safe**: Usa transacciones con nivel de aislamiento `Serializable` para evitar duplicados
4. **Auditoría Completa**: Registra `last_value`, `last_user`, `last_date` en cada generación

## 🗄️ Estructura de Datos

### Tablas Centrales (admin.*)
- `admin.entity_type` - Catálogo de tipos de entidad (DOC, JOU, etc.)
- `admin.entity_document` - Catálogo de documentos (Journal Entry, Check, etc.)
- `admin.menu` - Jerarquía de menús del sistema

### Tabla Operacional (sinai.*)
- `sinai.consecutive` - Configuración de consecutivos por compañía

```sql
CREATE TABLE sinai.consecutive (
	id_consecutive       SERIAL PRIMARY KEY,
	code                 VARCHAR(30) NOT NULL UNIQUE,
	description          VARCHAR(200),
	id_entity_type       INTEGER NOT NULL,      -- Referencia a admin.entity_type
	id_entity_document   INTEGER NOT NULL,      -- Referencia a admin.entity_document
	id_menu              INTEGER NOT NULL,      -- Referencia a admin.menu (jerárquico)
	mask                 VARCHAR(50) NOT NULL,
	length               INTEGER NOT NULL DEFAULT 15,
	initial_value        VARCHAR(50) NOT NULL,
	final_value          VARCHAR(50) NOT NULL,
	last_value           VARCHAR(50),
	last_user            INTEGER,
	last_date            TIMESTAMP,
	is_active            BOOLEAN NOT NULL DEFAULT TRUE,
	-- ... campos de auditoría ...
);
```

## 🔍 Lógica de Búsqueda Jerárquica

Cuando se genera un consecutivo para un menú, el sistema:

1. Busca un consecutivo donde `id_menu = menuActual` y `id_entity_document = tipoDocumento`
2. Si **no encuentra**, consulta el `id_parent` del menú actual en `admin.menu`
3. Repite la búsqueda con el menú padre
4. Continúa ascendiendo hasta encontrar un consecutivo o llegar a la raíz (`id_parent = 0`)
5. Si no encuentra ninguno, lanza excepción pidiendo configurar uno

### Ejemplo Visual

```
admin.menu:
  8: Warehouse & Distribution (id_parent=0)
	├─ 86: Warehouses (id_parent=8)
	└─ 96: Stock Transfers (id_parent=8)

sinai.consecutive:
  JOURNAL_ENTRY_WAD: id_menu=8, mask=***999999999999

Flujo:
  1. Usuario crea Journal Entry desde "Stock Transfers" (id_menu=96)
  2. Sistema busca consecutivo con id_menu=96 → NO encuentra
  3. Sistema sube a menú padre (id_menu=8) → Encuentra JOURNAL_ENTRY_WAD
  4. Genera número: WAD000000000001
```

## 🔧 Implementación Backend

### 1. Servicio de Consecutivos (`CMS.Data/Services/ConsecutiveService.cs`)

```csharp
public class ConsecutiveService : IConsecutiveService
{
	// Generación thread-safe con transacción Serializable
	public async Task<string> GenerateNextNumberAsync(
		int companyId,
		int menuId,
		int entityDocumentId,
		int userId)
	{
		// 1. Búsqueda jerárquica
		var consecutive = await FindConsecutiveHierarchicalAsync(...);

		// 2. Calcular siguiente valor
		int nextValue = CalculateNextValue(consecutive);

		// 3. Validar límite
		ValidateFinalValue(consecutive, nextValue);

		// 4. Aplicar máscara
		string generatedNumber = ApplyMask(consecutive.MASK, nextValue);

		// 5. Actualizar BD
		consecutive.LAST_VALUE = ...;
		consecutive.LAST_USER = userId;
		consecutive.LAST_DATE = DateTime.UtcNow;

		await companyDb.SaveChangesAsync();
		return generatedNumber;
	}

	private async Task<Consecutive?> FindConsecutiveHierarchicalAsync(...)
	{
		var currentMenuId = menuId;
		var visited = new HashSet<int>();

		while (currentMenuId > 0 && !visited.Contains(currentMenuId))
		{
			visited.Add(currentMenuId);

			// Buscar en menú actual
			var consecutive = await companyDb.Consecutives
				.FirstOrDefaultAsync(c => c.ID_MENU == currentMenuId && ...);

			if (consecutive != null)
				return consecutive;

			// Subir al padre
			var menu = await _centralDb.Menus.FindAsync(currentMenuId);
			if (menu == null || menu.ID_PARENT == 0)
				break;

			currentMenuId = menu.ID_PARENT;
		}

		return null;
	}
}
```

### 2. Integración con Journal Entry Service

```csharp
public class JournalEntryService : IJournalEntryService
{
	private readonly IConsecutiveService _consecutiveService;
	private const int ENTITY_DOCUMENT_ID_JOURNAL_ENTRY = 1;

	public async Task<JournalEntry> CreateJournalEntryAsync(
		int companyId,
		JournalEntry entry,
		int idMenu,        // ⭐ Nuevo parámetro
		int userId,
		string currentUser)
	{
		// Generar entry_number automáticamente
		if (string.IsNullOrWhiteSpace(entry.EntryNumber))
		{
			entry.EntryNumber = await _consecutiveService.GenerateNextNumberAsync(
				companyId,
				idMenu,
				ENTITY_DOCUMENT_ID_JOURNAL_ENTRY,
				userId);
		}

		// ... resto de la lógica ...
	}
}
```

### 3. API Controller (`JournalEntryController.cs`)

```csharp
[HttpPost]
public async Task<ActionResult<JournalEntryDto>> CreateJournalEntry(JournalEntryDto dto)
{
	if (dto.IdMenu <= 0)
	{
		return BadRequest("El campo IdMenu es requerido para generar el consecutivo.");
	}

	var created = await _journalEntryService.CreateJournalEntryAsync(
		companyId,
		entry,
		dto.IdMenu,    // ⭐ Pasar id_menu desde el DTO
		userId,
		currentUser);

	return CreatedAtAction(...);
}

// DTO actualizado
public class JournalEntryDto
{
	// ... campos existentes ...
	public int IdMenu { get; set; }  // ⭐ Nuevo campo
	public List<JournalEntryLineDto> Lines { get; set; }
}
```

## 🎨 Frontend (Pendiente)

El frontend deberá:

1. Detectar el `id_menu` actual desde donde se crea el Journal Entry
2. Incluir `idMenu` en el JSON al llamar `POST /api/JournalEntry`
3. El campo `entry_number` debe ser **readonly** y generado automáticamente por el backend

```javascript
// Ejemplo en CMS.UI/wwwroot/js/journalEntries.js
const createData = {
	idMenu: currentMenuId,  // ⭐ Obtener del contexto de navegación
	entryType: 'Manual',
	entryDate: ...,
	postingDate: ...,
	lines: [...]
};

const response = await fetch('/api/JournalEntry', {
	method: 'POST',
	headers: { 'Content-Type': 'application/json' },
	body: JSON.stringify(createData)
});
```

## 📊 Tokens de Máscara Soportados

| Token    | Descripción                    | Ejemplo          |
|----------|--------------------------------|------------------|
| `***`    | Máscara especial (todo número) | `WAD000000000001`|
| `{YYYY}` | Año completo                   | `2025`           |
| `{YY}`   | Año corto                      | `25`             |
| `{MM}`   | Mes                            | `01`             |
| `{DD}`   | Día                            | `22`             |
| `{####}` | Número consecutivo con padding | `0001`           |

**Ejemplo de máscaras:**
- `***999999999999` → `WAD000000000001`, `WAD000000000002`, ...
- `JE-{YYYY}-{MM}-{####}` → `JE-2025-01-0001`, `JE-2025-01-0002`, ...
- `INV-{YY}{MM}-{######}` → `INV-2501-000001`, `INV-2501-000002`, ...

## 🛠️ Registro del Servicio

En `CMS.API/Program.cs`:

```csharp
builder.Services.AddScoped<CMS.Data.Services.Interfaces.IConsecutiveService,
							CMS.Data.Services.ConsecutiveService>();
```

## ✅ Estado Actual

### Completado ✅
- [x] Tablas `admin.entity_type`, `admin.entity_document`, `sinai.consecutive` creadas
- [x] Entidades EF Core para `Consecutive`, `EntityType`, `EntityDocument`
- [x] Servicio `ConsecutiveService` con búsqueda jerárquica y generación thread-safe
- [x] Integración en `JournalEntryService` para `entry_number` automático
- [x] API Controllers actualizados (`ConsecutiveController`, `JournalEntryController`)
- [x] DTOs actualizados con campo `IdMenu`
- [x] Pantallas UI de mantenimiento:
  - `/Admin/EntityTypes` (admin.entity_type)
  - `/Admin/EntityDocuments` (admin.entity_document)
  - `/Settings/Consecutives` (sinai.consecutive)
- [x] Permisos y menús creados y asignados a admin
- [x] Build exitoso

### Pendiente 🔄
- [ ] Actualizar `journalEntries.js` para enviar `idMenu` desde el frontend
- [ ] Crear consecutivos adicionales para otros módulos (Sales, Purchasing, etc.)
- [ ] Agregar pruebas unitarias para `ConsecutiveService`
- [ ] Documentar en manual de usuario la configuración de consecutivos

## 🚀 Ejemplo de Configuración

### Crear Consecutivo para Warehouse

```sql
-- Consecutivo para Journal Entries originados en Warehouse & Distribution
INSERT INTO sinai.consecutive (
	code, description,
	id_entity_type, id_entity_document, id_menu,
	mask, length, initial_value, final_value,
	is_active, createdate, record_date, created_by, updated_by, rowpointer
) VALUES (
	'JOURNAL_ENTRY_WAD',
	'Asientos de diario - Warehouse & Distribution',
	3,    -- Journal (admin.entity_type)
	1,    -- Journal Entry (admin.entity_document)
	8,    -- Warehouse & Distribution (admin.menu)
	'***999999999999',
	15,
	'WAD000000000001',
	'WAD999999999999',
	TRUE, now(), now(), current_user, current_user, gen_random_uuid()
);
```

### Verificar Jerarquía de Menús

```sql
-- Ver estructura de menús Warehouse
SELECT 
	m.id_menu,
	m.id_parent,
	LPAD('', (level-1)*3, '  ') || m.name AS name_indented,
	m.url
FROM (
	WITH RECURSIVE menu_tree AS (
		SELECT id_menu, id_parent, name, url, 1 AS level
		FROM admin.menu
		WHERE id_menu = 8

		UNION ALL

		SELECT m.id_menu, m.id_parent, m.name, m.url, mt.level + 1
		FROM admin.menu m
		INNER JOIN menu_tree mt ON m.id_parent = mt.id_menu
	)
	SELECT * FROM menu_tree
) m
ORDER BY m.level, m.id_menu;
```

## 📝 Notas Técnicas

1. **Thread Safety**: La generación usa `IsolationLevel.Serializable` para evitar números duplicados en concurrencia
2. **Performance**: La búsqueda jerárquica tiene protección contra ciclos infinitos (`HashSet<int> visited`)
3. **Cross-DB Referencias**: Los campos `id_entity_type`, `id_entity_document`, `id_menu` son **lógicos** (no FK real) porque las tablas están en diferentes bases de datos
4. **Reversiones**: Las reversiones de Journal Entry usan `id_menu=105` (Journal Entries) por defecto
5. **Escalabilidad**: Para sistemas multi-compañía grandes, considerar caché de la jerarquía de menús en memoria

## 🎯 Casos de Uso

### Caso 1: Entrada de Inventario desde Warehouses (id_menu=86)
- Sistema busca consecutivo con `id_menu=86` → No existe
- Sube a padre `id_menu=8` → Encuentra `JOURNAL_ENTRY_WAD`
- Genera: `WAD000000000001`

### Caso 2: Transferencia de Stock (id_menu=96)
- Sistema busca consecutivo con `id_menu=96` → No existe
- Sube a padre `id_menu=8` → Encuentra `JOURNAL_ENTRY_WAD`
- Genera: `WAD000000000002` (siguiente valor)

### Caso 3: Journal Entry Manual (id_menu=105)
- Sistema busca consecutivo con `id_menu=105` → Debe existir uno específico
- Si no existe: Error "No se encontró consecutivo"
- **Acción**: Crear consecutivo para `id_menu=105` con máscara `JE-{YYYY}-{MM}-{####}`

---

**Autor**: BITI SOLUTIONS S.A  
**Fecha**: 2025-01-22  
**Versión**: 1.0
