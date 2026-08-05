# ================================================================================
# INSTRUCCIONES: Configurar Credenciales de PostgreSQL para Copilot
# ================================================================================

## 📋 Problema

Copilot necesita acceso a las credenciales de PostgreSQL para poder:
- Ejecutar consultas de diagnóstico
- Verificar el estado de la base de datos
- Probar scripts SQL
- Obtener información del sistema

## ✅ Solución Permanente

### Paso 1: Configurar el archivo de credenciales

1. Abre el archivo `.copilot/.env.database`
2. Reemplaza `TU_PASSWORD_AQUI` con la contraseña real del usuario `cmssystem`
3. Si tu servidor PostgreSQL NO está en `localhost`, actualiza `DB_HOST`

**Ejemplo de configuración correcta:**

```ini
DB_HOST=localhost
DB_PORT=5432
DB_NAME=cms
DB_USER=cmssystem
DB_PASSWORD=eamr123    # ← Reemplaza con tu contraseña real

DB_COMPANY_NAME=sinai
DB_COMPANY_USER=cmssystem
DB_COMPANY_PASSWORD=eamr123    # ← Reemplaza con tu contraseña real
```

### Paso 2: Probar la conexión

Ejecuta el script de prueba desde PowerShell:

```powershell
.\.copilot\test-db-connection.ps1
```

Deberías ver:
```
✅ Conexión exitosa a BD Central (cms)
✅ Conexión exitosa a BD de Compañía (sinai)
```

### Paso 3: Informar a Copilot

Una vez configurado, dime:
- "✅ Credenciales configuradas en `.copilot/.env.database`"

A partir de ese momento, cuando necesite ejecutar comandos de PostgreSQL, leeré las credenciales desde ese archivo.

## 🔒 Seguridad

- El archivo `.copilot/.env.database` está en `.gitignore` y NO se subirá al repositorio
- Es local a tu máquina
- Solo tú y Copilot tendrán acceso a las credenciales

## 🛠️ Cómo funciona

Cuando necesite ejecutar un comando PostgreSQL, haré:

```powershell
# Leer credenciales desde .copilot/.env.database
$env:PGPASSWORD = [contraseña del archivo]

# Ejecutar comando
psql -h [host] -U [usuario] -d [base_de_datos] -c "[consulta SQL]"
```

## 📝 Alternativa: Proporcionar credenciales manualmente

Si prefieres NO guardar las credenciales en un archivo, también puedes:

1. Decirme las credenciales en cada sesión:
   - "La contraseña de cmssystem es: XXXXX"

2. Ejecutar los comandos manualmente y copiarme los resultados

Pero la opción del archivo `.env.database` es más conveniente para sesiones futuras.

## ❓ ¿Qué prefieres?

**Opción A (Recomendada):** Configurar `.copilot/.env.database` con tus credenciales
**Opción B:** Darme las credenciales manualmente en cada sesión
**Opción C:** Ejecutar los comandos tú mismo y copiarme los resultados

¿Cuál opción prefieres?
