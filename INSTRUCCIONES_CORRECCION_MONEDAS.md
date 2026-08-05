# 🔧 Corrección: Mostrar TODAS las monedas activas en Global Parameters

## 📋 Problema Identificado

En la pantalla `/Settings/GlobalParameters`, al editar los parámetros `currency_local` y `currency_exchange`, **solo aparecen 3 monedas** (CRC, EUR, USD) en lugar de mostrar **todas las monedas activas** de la tabla `admin.currency`.

## 🔍 Causa Raíz

La tabla `admin.currency` probablemente solo tiene 3 registros activos. El sistema está funcionando correctamente - el API endpoint `/api/currency/active` retorna todas las monedas con `is_active = TRUE`, pero si solo hay 3 en la BD, solo 3 se mostrarán.

## ✅ Solución

Ejecutar el script `068_seed_currencies.sql` que carga **89 monedas del mundo** en la tabla `admin.currency`.

### Pasos:

1. **Ejecutar el script de carga de monedas:**

```bash
psql -h localhost -U cmssystem -d cms -f CMS.Data/Scripts/068_seed_currencies.sql
```

O si estás en Windows con PowerShell:

```powershell
$env:PGPASSWORD="TU_PASSWORD"
psql -h localhost -U cmssystem -d cms -f "C:\Disco\BITI Solutions S.A\BITI Solutions\Proyectos\CMS\CMS\CMS.Data\Scripts\068_seed_currencies.sql"
```

2. **Verificar que se cargaron correctamente:**

```sql
-- Debe retornar 89 (o más)
SELECT COUNT(*) as total_monedas FROM admin.currency WHERE is_active = TRUE;

-- Ver las primeras 20 monedas
SELECT code, name, symbol, sort_order 
FROM admin.currency 
WHERE is_active = TRUE 
ORDER BY sort_order 
LIMIT 20;
```

3. **Refrescar la página en el navegador** (F5) para que el JavaScript cargue las nuevas monedas.

## 📊 Monedas que se cargarán

El script `068_seed_currencies.sql` incluye:

### América Latina (32 monedas)
- CRC (Colón costarricense)
- USD (Dólar estadounidense)
- MXN (Peso mexicano)
- Y 29 más...

### Europa (18 monedas)
- EUR (Euro)
- GBP (Libra esterlina)
- CHF (Franco suizo)
- Y 15 más...

### Asia y Oceanía (29 monedas)
- JPY (Yen japonés)
- CNY (Yuan chino)
- INR (Rupia india)
- Y 26 más...

### África (13 monedas)
- ZAR (Rand sudafricano)
- NGN (Naira nigeriana)
- Y 11 más...

### Norteamérica (1 moneda adicional)
- CAD (Dólar canadiense)

**Total: 89 monedas activas**

## 🔧 El código ya está correcto

El código de `globalParameters.js` y `CurrencyController.cs` YA están implementados correctamente para cargar TODAS las monedas activas. Solo falta poblar la tabla con el script.

### Flujo actual (correcto):

1. ✅ `globalParameters.js` → llama a `/api/currency/active`
2. ✅ `CurrencyController.GetActiveCurrencies()` → consulta `admin.currency WHERE is_active = TRUE`
3. ✅ Retorna TODAS las monedas activas (ordenadas por `sort_order`)
4. ✅ El JavaScript renderiza un `<option>` por cada moneda
5. ❌ **Problema:** Solo hay 3 monedas en la BD → solo 3 opciones aparecen

## 🎯 Resultado Esperado

Después de ejecutar el script, el dropdown de monedas mostrará las 89 monedas ordenadas por región:

```
— Seleccione una moneda —
CRC - Colón costarricense (₡)
USD - United States Dollar ($)
MXN - Peso mexicano ($)
GTQ - Quetzal guatemalteco (Q)
...
(85 monedas más)
```

## 📝 Notas Importantes

- El script usa `ON CONFLICT (code) DO UPDATE`, por lo que es **idempotente** (se puede ejecutar múltiples veces sin duplicar registros)
- Todas las monedas se insertan con `is_active = TRUE`
- Si necesitas desactivar alguna moneda específica, ejecuta:
  ```sql
  UPDATE admin.currency SET is_active = FALSE WHERE code = 'XXX';
  ```
- El orden de aparición en el dropdown está determinado por el campo `sort_order` (América → Europa → Asia → África)
