# ================================================================================
# SCRIPT DE DIAGNÓSTICO: Problema de Monedas en Global Parameters
# ================================================================================
# Este script diagnostica por qué solo aparecen 3 monedas en el selector
# ================================================================================

param(
	[string]$ConfigFile = ".copilot\.env.database"
)

Write-Host "=== DIAGNÓSTICO: MONEDAS EN GLOBAL PARAMETERS ===" -ForegroundColor Cyan
Write-Host ""

# Verificar archivo de configuración
if (-not (Test-Path $ConfigFile)) {
	Write-Host "⚠️ No se encontró $ConfigFile" -ForegroundColor Yellow
	Write-Host "   Por favor, proporciona las credenciales manualmente:" -ForegroundColor Yellow
	$dbHost = Read-Host "   Host PostgreSQL (localhost)"
	if ([string]::IsNullOrWhiteSpace($dbHost)) { $dbHost = "10.0.0.1" }

	$dbPort = Read-Host "   Puerto (5432)"
	if ([string]::IsNullOrWhiteSpace($dbPort)) { $dbPort = "5432" }

	$dbUser = Read-Host "   Usuario (cmssystem)"
	if ([string]::IsNullOrWhiteSpace($dbUser)) { $dbUser = "postgres" }

	$dbName = Read-Host "   Base de datos (cms)"
	if ([string]::IsNullOrWhiteSpace($dbName)) { $dbName = "cms" }

	$dbPassword = Read-Host "   Contraseña" -AsSecureString
	$env:PGPASSWORD = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($dbPassword))
} else {
	# Leer desde archivo
	$config = @{}
	Get-Content $ConfigFile | ForEach-Object {
		if ($_ -match '^\s*([^#][^=]+)=(.+)$') {
			$key = $matches[1].Trim()
			$value = $matches[2].Trim()
			$config[$key] = $value
		}
	}

	$dbHost = $config['DB_HOST']
	$dbPort = $config['DB_PORT']
	$dbUser = $config['DB_USER']
	$dbName = $config['DB_NAME']
	$env:PGPASSWORD = $config['DB_PASSWORD']
}

Write-Host "🔍 Conectando a PostgreSQL..." -ForegroundColor Gray
Write-Host ""

# 1. Contar monedas activas en la BD
Write-Host "📊 PASO 1: Verificar monedas en admin.currency" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Gray

psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -c "
SELECT 
	COUNT(*) FILTER (WHERE is_active = TRUE) as activas,
	COUNT(*) FILTER (WHERE is_active = FALSE) as inactivas,
	COUNT(*) as total
FROM admin.currency;
"

if ($LASTEXITCODE -ne 0) {
	Write-Host "❌ Error conectando a la base de datos" -ForegroundColor Red
	exit 1
}

Write-Host ""
Write-Host "📋 Primeras 10 monedas activas:" -ForegroundColor Cyan
psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -c "
SELECT 
	id_currency,
	code,
	name,
	symbol,
	sort_order,
	is_active
FROM admin.currency
WHERE is_active = TRUE
ORDER BY sort_order
LIMIT 10;
"

Write-Host ""
Write-Host "📊 PASO 2: Verificar endpoint de API" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Gray

Write-Host "⚠️ Para verificar el API, necesitas ejecutar manualmente:" -ForegroundColor Yellow
Write-Host ""
Write-Host "   1. Asegúrate que CMS.API esté corriendo (dotnet run en CMS.API)" -ForegroundColor White
Write-Host "   2. Abre el navegador en modo desarrollador (F12)" -ForegroundColor White
Write-Host "   3. Ve a la pestaña 'Network' / 'Red'" -ForegroundColor White
Write-Host "   4. Carga la página: http://localhost:5001/Settings/GlobalParameters" -ForegroundColor White
Write-Host "   5. Busca la petición: GET /api/currency/active" -ForegroundColor White
Write-Host "   6. Verifica cuántos registros retorna en la respuesta JSON" -ForegroundColor White
Write-Host ""

Write-Host "📊 PASO 3: Verificar logs del navegador" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Gray

Write-Host "En la consola del navegador (F12 > Console), busca:" -ForegroundColor Yellow
Write-Host "   '💱 Cargando monedas desde: ...'" -ForegroundColor White
Write-Host "   '✅ Monedas cargadas: X'" -ForegroundColor White
Write-Host ""
Write-Host "Si ves '✅ Monedas cargadas: 3', entonces el API solo está retornando 3 registros." -ForegroundColor White
Write-Host "Si ves un número mayor, entonces el problema está en el renderizado del select." -ForegroundColor White
Write-Host ""

Write-Host "📊 PASO 4: SQL para depurar" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Gray

Write-Host "Ejecuta esta consulta para ver TODAS las monedas activas:" -ForegroundColor Yellow
Write-Host ""
Write-Host "SELECT id_currency, code, name, symbol, is_active, sort_order" -ForegroundColor Gray
Write-Host "FROM admin.currency" -ForegroundColor Gray
Write-Host "WHERE is_active = TRUE" -ForegroundColor Gray
Write-Host "ORDER BY sort_order;" -ForegroundColor Gray
Write-Host ""

Write-Host "=== FIN DEL DIAGNÓSTICO ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "💡 SIGUIENTES PASOS:" -ForegroundColor Green
Write-Host "   1. Revisa el número de monedas activas en la consulta de arriba" -ForegroundColor White
Write-Host "   2. Abre el navegador y verifica los logs de la consola" -ForegroundColor White
Write-Host "   3. Verifica la respuesta del endpoint /api/currency/active" -ForegroundColor White
Write-Host "   4. Copia y pega los resultados aquí para que pueda ayudarte" -ForegroundColor White
