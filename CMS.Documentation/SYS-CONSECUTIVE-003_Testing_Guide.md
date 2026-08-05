# 🧪 Guía de Pruebas: Sistema de Consecutivos Jerárquicos

## ✅ Pre-requisitos

Antes de probar, verificar:

1. ✅ Build exitoso
2. ✅ Base de datos `sinai` con tabla `consecutive`
3. ✅ Base de datos `cms` con tablas `admin.entity_type`, `admin.entity_document`, `admin.menu`
4. ✅ Usuario admin con permisos asignados
5. ✅ Al menos un consecutivo configurado (JOURNAL_ENTRY_WAD)

## 📋 Plan de Pruebas

### 1️⃣ Verificar Datos Base

```sql
-- Conectar a BD central
psql -h 10.0.0.1 -p 5432 -U postgres -d cms

-- Verificar entity types
SELECT id_entity_type, code, name FROM admin.entity_type ORDER BY id_entity_type;

-- Verificar entity documents
SELECT id_entity_document, id_entity_type, code, name FROM admin.entity_document ORDER BY id_entity_document;

-- Verificar menús (especialmente Warehouse y Journal Entries)
SELECT id_menu, id_parent, name, url FROM admin.menu 
WHERE id_menu IN (8, 105) OR id_parent = 8
ORDER BY id_menu;

-- Conectar a BD de compañía
psql -h 10.0.0.1 -p 5432 -U postgres -d sinai

-- Verificar consecutivo existente
SELECT 
	c.id_consecutive,
	c.code,
	c.description,
	c.id_menu,
	m.name AS menu_name,
	c.mask,
	c.initial_value,
	c.last_value,
	c.is_active
FROM sinai.consecutive c
LEFT JOIN admin.menu m ON c.id_menu = m.id_menu;
```

**Resultado esperado:**
- Entity types: 8 registros (DOC, DOP, JOU, EMP, SUP, CUS, INV, USE)
- Entity documents: 16 registros (Journal Entry, Check, Invoice, etc.)
- Menu 8: "Warehouse & Distribution" con id_parent=0
- Menu 105: "Journal Entries" con id_parent=19
- Consecutive: JOURNAL_ENTRY_WAD con id_menu=8, mask=`***999999999999`

---

### 2️⃣ Probar Pantallas de Mantenimiento UI

#### A) Entity Types (`/Admin/EntityTypes`)

1. Iniciar sesión como admin
2. Ir a: **Administration → Entity Types**
3. Verificar que aparezcan 8 entity types
4. Probar búsqueda: escribir "JOU"
5. Clic en editar el tipo "Journal"
6. Cambiar descripción (ej: "Journal - Updated")
7. Guardar
8. Verificar que se actualiza en la lista

**Resultado esperado:**
- ✅ Lista carga correctamente
- ✅ Búsqueda funciona
- ✅ Edición guarda cambios
- ✅ Toast de éxito aparece

#### B) Entity Documents (`/Admin/EntityDocuments`)

1. Ir a: **Administration → Entity Documents**
2. Verificar que aparezcan 16 documentos
3. Filtrar por tipo: seleccionar "Document"
4. Verificar que solo aparezcan documentos de ese tipo
5. Probar búsqueda: escribir "Journal"
6. Clic en editar "Journal Entry"
7. Cambiar descripción
8. Guardar

**Resultado esperado:**
- ✅ Lista carga correctamente
- ✅ Filtro por tipo funciona
- ✅ Búsqueda funciona
- ✅ Edición guarda cambios

#### C) Consecutives (`/Settings/Consecutives`)

1. Ir a: **Settings → Consecutives**
2. Verificar que aparezca el consecutivo JOURNAL_ENTRY_WAD
3. Ver columnas: Code, Description, Entity Type, Entity Document, Menu, Mask, Initial Value, Last Value
4. Clic en editar JOURNAL_ENTRY_WAD
5. Verificar que carguen los selectores:
   - Entity Type: debe seleccionar "Journal"
   - Entity Document: debe seleccionar "Journal Entry"
   - Menu: debe seleccionar "Warehouse & Distribution"
6. Cambiar descripción (ej: "Journal Entry - Warehouse & Distribution - TEST")
7. Guardar
8. Verificar que se actualiza en la lista

**Resultado esperado:**
- ✅ Lista carga correctamente
- ✅ Selectores cargan datos de admin.* correctamente
- ✅ Edición guarda cambios
- ✅ Toast de éxito aparece

---

### 3️⃣ Probar Generación Automática de Entry Number

#### Escenario 1: Crear Journal Entry desde Accounting (Menu 105)

1. Ir a: **Accounting → Journal Entries**
2. Clic en "Nuevo Asiento"
3. Verificar campo "Número de Asiento":
   - Debe mostrar: `[Se generará automáticamente]`
   - Debe estar en readonly
   - Debe estar en cursiva y color gris
4. Llenar campos:
   - Descripción: "Prueba consecutivo - Accounting"
   - Fecha de asiento: hoy
   - Fecha contable: hoy
5. Agregar al menos 2 líneas que cuadren:
   - Línea 1: Cuenta (ej: 1-1-001), Débito: 1000.00
   - Línea 2: Cuenta (ej: 2-1-001), Crédito: 1000.00
6. Clic en "Guardar"
7. Verificar toast de éxito
8. Cerrar modal
9. **Verificar en la lista el Entry Number generado**

**Resultado esperado (si existe consecutivo para menu 105):**
- ✅ Entry Number generado: `JE-2025-01-0001` (o similar según máscara)
- ✅ Asiento guardado correctamente
- ✅ Aparece en la lista con el número generado

**Resultado esperado (si NO existe consecutivo para menu 105):**
- ❌ Error: "No se encontró consecutivo para el menú 105..."
- → **Acción**: Crear consecutivo para menu 105 (ver script 122)

#### Escenario 2: Verificar Herencia desde Submenu Warehouse

**Pre-requisito:** Asegurarse de que NO existe consecutivo específico para el submenu que se va a probar (ej: menu 96 - Stock Transfers)

```sql
-- Verificar que NO exista consecutivo para submenu
SELECT * FROM sinai.consecutive WHERE id_menu = 96; -- Debe estar vacío
```

1. ⚠️ **NOTA**: Esta prueba es conceptual porque actualmente Journal Entries solo se crean desde `/Accounting/JournalEntries`
2. Para probar realmente la herencia, se necesitaría crear Journal Entries desde otros módulos (ej: Warehouse)
3. **Prueba alternativa en consola del navegador:**

```javascript
// Abrir DevTools (F12) en la página Journal Entries
// Cambiar temporalmente el CURRENT_MENU_ID
console.log('Menu actual:', CURRENT_MENU_ID);
// Forzar un menu hijo del Warehouse para simular
window.TEST_MENU_ID = 96; // Stock Transfers (hijo de Warehouse id=8)
```

4. Luego crear un asiento y verificar en la BD que se use el consecutivo del padre (id_menu=8)

**Resultado esperado:**
- ✅ Sistema busca consecutivo con id_menu=96
- ✅ No encuentra, sube al padre id_menu=8
- ✅ Encuentra JOURNAL_ENTRY_WAD
- ✅ Genera número: WAD000000000002 (siguiente del anterior)

---

### 4️⃣ Verificar Actualización de Consecutivo en BD

Después de crear un Journal Entry exitoso:

```sql
-- Conectar a BD de compañía
psql -h 10.0.0.1 -p 5432 -U postgres -d sinai

-- Ver estado del consecutivo
SELECT 
	code,
	last_value,
	last_user,
	last_date,
	updated_by,
	record_date
FROM sinai.consecutive
WHERE code = 'JOURNAL_ENTRY_WAD';
```

**Resultado esperado:**
- ✅ `last_value` actualizado (ej: "000000000001" → "000000000002")
- ✅ `last_user` = ID del usuario que creó el asiento
- ✅ `last_date` = timestamp de la creación
- ✅ `updated_by` = "ConsecutiveService"
- ✅ `record_date` = timestamp actualizado

---

### 5️⃣ Probar Límites y Validaciones

#### A) Agotar Consecutivo

**⚠️ CUIDADO:** Esto es destructivo, solo en ambiente de pruebas.

1. En BD, cambiar `final_value` del consecutivo a un valor muy bajo:

```sql
UPDATE sinai.consecutive 
SET final_value = 'WAD000000000003'
WHERE code = 'JOURNAL_ENTRY_WAD';
```

2. Crear Journal Entries hasta llegar al límite
3. Intentar crear uno más

**Resultado esperado:**
- ❌ Error: "Consecutivo agotado: El siguiente valor (4) excede el límite (3)..."
- → Usuario debe ir a Settings/Consecutives y actualizar `final_value`

#### B) Consecutivo Inactivo

```sql
UPDATE sinai.consecutive 
SET is_active = FALSE
WHERE code = 'JOURNAL_ENTRY_WAD';
```

2. Intentar crear Journal Entry

**Resultado esperado:**
- ❌ Error: "No se encontró consecutivo..." (porque está inactivo)

---

### 6️⃣ Probar Concurrencia (Thread Safety)

**Requiere herramienta de carga:**

1. Usar Postman / JMeter / Artillery para simular múltiples requests simultáneos
2. Configurar 10 requests paralelos POST `/api/JournalEntry`
3. Verificar que todos obtengan entry_numbers únicos y consecutivos

**Resultado esperado:**
- ✅ No hay números duplicados
- ✅ Números generados son consecutivos (pueden tener gaps si alguno falla)
- ✅ `last_value` en BD refleja el último número generado exitosamente

---

### 7️⃣ Probar Reversión de Journal Entry

1. Crear un Journal Entry exitoso (guarda su entry_number)
2. Contabilizarlo (Status = Posted)
3. Ir a acciones → "Reverse Entry"
4. Ingresar fecha de reversión y motivo
5. Confirmar reversión
6. Verificar que se crea un nuevo Journal Entry con:
   - Tipo: "Reversal"
   - Líneas invertidas (débitos ↔ créditos)
   - **Entry Number diferente** (ej: JE-2025-01-0002 si la reversión usa menu 105)

**Resultado esperado:**
- ✅ Reversión crea nuevo asiento automáticamente
- ✅ El nuevo asiento tiene su propio entry_number generado
- ✅ Asiento original cambia a Status = "Reversed"
- ✅ El consecutivo se incrementa

---

## 🐛 Problemas Comunes y Soluciones

### Error: "No se encontró consecutivo para el menú X"

**Causa:** No existe consecutivo configurado para ese menú ni sus padres.

**Solución:**
1. Identificar el menú padre principal
2. Crear consecutivo usando script 122 o la UI
3. Ejemplo:
```sql
INSERT INTO sinai.consecutive (code, id_menu, id_entity_document, mask, ...) 
VALUES ('JOURNAL_ENTRY_ACCOUNTING', 105, 1, 'JE-{YYYY}-{MM}-{####}', ...);
```

### Error: "El campo IdMenu es requerido"

**Causa:** El frontend no está enviando `idMenu` en el JSON.

**Solución:**
1. Verificar que `CURRENT_MENU_ID` esté definido en la vista .cshtml
2. Verificar que `journalEntries.js` incluya `idMenu: CURRENT_MENU_ID` en el objeto `data`

### Entry Number no se genera (queda en blanco)

**Causa:** Error en el servicio de consecutivos o no se está llamando.

**Solución:**
1. Revisar logs de CMS.API para errores
2. Verificar que `IConsecutiveService` esté registrado en Program.cs
3. Verificar que `JournalEntryService` esté inyectando `IConsecutiveService`

### Entry Number duplicado (muy raro)

**Causa:** Problema de concurrencia si el nivel de aislamiento no es correcto.

**Solución:**
1. Verificar que `ConsecutiveService` use `IsolationLevel.Serializable`
2. Revisar si hay múltiples instancias de la API corriendo sin load balancer

---

## 📊 Checklist Final

Antes de considerar el sistema completo:

- [ ] Build exitoso sin warnings críticos
- [ ] Todas las tablas de BD creadas y con seed data
- [ ] Pantallas UI cargan y guardan correctamente
- [ ] Generación de entry_number funciona para menu 105
- [ ] Consecutivo se actualiza en BD después de cada generación
- [ ] Validación de límite funciona
- [ ] Reversiones generan nuevo entry_number
- [ ] Documentación completa en CMS.Documentation
- [ ] Script de ejemplos (122) disponible para otros módulos

---

## 🚀 Próximos Pasos Después de Pruebas

1. **Crear consecutivos para módulos en uso:**
   - Ejecutar script 122 ajustando los `id_menu` correctos
   - Probar cada uno desde su módulo respectivo

2. **Agregar al manual de usuario:**
   - Cómo configurar un consecutivo nuevo
   - Significado de los tokens de máscara
   - Qué hacer cuando se agota un consecutivo

3. **Monitoreo:**
   - Agregar alertas cuando un consecutivo esté cerca del límite
   - Dashboard con uso de consecutivos

4. **Optimizaciones futuras:**
   - Caché de jerarquía de menús en memoria
   - Pool de números pre-generados para alta concurrencia

---

**Creado por**: BITI SOLUTIONS S.A  
**Fecha**: 2025-01-22  
**Versión**: 1.0
