# Test PostgreSQL Connection
param([string]$ConfigFile = ".copilot\.env.database")

Write-Host "=== PRUEBA DE CONEXION A POSTGRESQL ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $ConfigFile)) {
	Write-Host "[ERROR] No se encontro el archivo $ConfigFile" -ForegroundColor Red
	exit 1
}

Write-Host "[INFO] Leyendo configuracion desde: $ConfigFile" -ForegroundColor Gray
$config = @{}
Get-Content $ConfigFile | ForEach-Object {
	if ($_ -match '^\s*([^#][^=]+)=(.+)$') {
		$key = $matches[1].Trim()
		$value = $matches[2].Trim()
		$config[$key] = $value
	}
}

if ($config['DB_PASSWORD'] -eq 'TU_PASSWORD_AQUI') {
	Write-Host "[ERROR] Debes configurar DB_PASSWORD en $ConfigFile" -ForegroundColor Red
	exit 1
}

$env:PGPASSWORD = $config['DB_PASSWORD']

Write-Host "[TEST] Probando conexion a BD Central (cms)..." -ForegroundColor Cyan
Write-Host "  Host: $($config['DB_HOST'])" -ForegroundColor Gray
Write-Host "  Puerto: $($config['DB_PORT'])" -ForegroundColor Gray
Write-Host "  Base de datos: $($config['DB_NAME'])" -ForegroundColor Gray
Write-Host "  Usuario: $($config['DB_USER'])" -ForegroundColor Gray
Write-Host ""

$sqlQuery = "SELECT current_database() as db, current_user as usuario, version() as version;"
psql -h $config['DB_HOST'] -p $config['DB_PORT'] -U $config['DB_USER'] -d $config['DB_NAME'] -c $sqlQuery 2>&1 | Out-String | Write-Host

if ($LASTEXITCODE -eq 0) {
	Write-Host "[OK] Conexion exitosa a BD Central (cms)" -ForegroundColor Green
	Write-Host ""

	Write-Host "[INFO] Informacion del Sistema:" -ForegroundColor Cyan

	$sqlInfo = @"
SELECT 'BD Central' as tipo, COUNT(*) as total_usuarios FROM admin.user;
SELECT 'Companias Activas' as tipo, COUNT(*) as total FROM admin.company WHERE is_active = TRUE;
SELECT 'Monedas Activas' as tipo, COUNT(*) as total FROM admin.currency WHERE is_active = TRUE;
"@

	psql -h $config['DB_HOST'] -p $config['DB_PORT'] -U $config['DB_USER'] -d $config['DB_NAME'] -c $sqlInfo 2>&1 | Out-String | Write-Host
} else {
	Write-Host "[ERROR] Error conectando a BD Central (cms)" -ForegroundColor Red
	exit 1
}

Write-Host ""
Write-Host "[TEST] Probando conexion a BD de Compania ($($config['DB_COMPANY_NAME']))..." -ForegroundColor Cyan

$sqlCompany = "SELECT current_database() as db, current_user as usuario;"
psql -h $config['DB_HOST'] -p $config['DB_PORT'] -U $config['DB_COMPANY_USER'] -d $config['DB_COMPANY_NAME'] -c $sqlCompany 2>&1 | Out-String | Write-Host

if ($LASTEXITCODE -eq 0) {
	Write-Host "[OK] Conexion exitosa a BD de Compania ($($config['DB_COMPANY_NAME']))" -ForegroundColor Green
	Write-Host ""

	Write-Host "[INFO] Parametros Globales:" -ForegroundColor Cyan
	$sqlParams = "SELECT code, parameter_name, data_type, is_active FROM $($config['DB_COMPANY_NAME']).global_parameter ORDER BY sort_order LIMIT 10;"
	psql -h $config['DB_HOST'] -p $config['DB_PORT'] -U $config['DB_COMPANY_USER'] -d $config['DB_COMPANY_NAME'] -c $sqlParams 2>&1 | Out-String | Write-Host
} else {
	Write-Host "[ERROR] Error conectando a BD de Compania" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== FIN DE PRUEBA ===" -ForegroundColor Cyan
