# Diagnóstico y Solución - Error "admin.customer does not exist"

## Problema

Error: `42P01: relation "admin.customer" does not exist`

## Causa Raíz

El `CompanyDbContext` está usando el schema "admin" cuando debería usar "sinai". Esto ocurre porque:

1. El `companyId` en el JWT podría ser incorrecto (1 en lugar de 4)
2. O el factory está recibiendo el ID correcto pero el company no tiene connection string configurado

## Verificación Inmediata

Ejecutar en la consola de logs cuando accedes a `/Customers/Customers`:

Buscar líneas con:
- `🔍 DEBUG GetCompanyId - Claim value:`
- `🔍 CompanyId parseado:`
- `🔗 Compañía {CompanyId} ({Schema})`

## Solución 1: Si el companyId es 1 (incorrecto)

El JWT está usando la compañía admin en lugar de sinai. Necesitas:

1. Hacer logout completo
2. Volver a login seleccionando la compañía correcta
3. Verificar que la sesión tenga el companyId = 4

## Solución 2: Si el companyId es 4 pero aún falla

El problema está en que el `company.COMPANY_SCHEMA` es NULL o vacío.

Ejecutar:
```sql
SELECT id_company, company_schema, connection_string_development 
FROM admin.company WHERE id_company = 4;
```

Si `company_schema` es NULL, actualizar:
```sql
UPDATE admin.company 
SET company_schema = 'sinai' 
WHERE id_company = 4;
```

## Solución 3: Fallback manual

Si nada funciona, forzar el schema en CustomerService.cs línea 46:

```csharp
// TEMPORAL: Forzar schema
await using var db = await _factory.CreateDbContextAsync(4); // Forzar companyId = 4
```

## Logs Esperados (Correctos)

```
🔍 CompanyId parseado: 4
🔗 Compañía 4 (sinai): IS_PRODUCTION=True, AppEnvironment=Development, UsandoCS=Development
Creando CompanyDbContext para schema: sinai
```

## Logs Incorrectos

```
🔍 CompanyId parseado: 1
🔗 Compañía 1 (admin): ...
```

---

**SIGUIENTE PASO**: Por favor ejecuta la app, accede a Customers, y copia TODOS los logs que veas en la consola de Visual Studio (Output → Debug).
