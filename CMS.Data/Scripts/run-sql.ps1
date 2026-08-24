<#
================================================================================
 SCRIPT: run-sql.ps1
 PROPÓSITO: Ejecutar scripts .sql contra las bases de datos del CMS (cms / sinai)
			sin que la terminal se cuelgue (pager desactivado, sin prompts).
 USO:
   .\run-sql.ps1 -File 018_add_customer_type_menu.sql            # BD por defecto: cms
   .\run-sql.ps1 -File 016_customer.sql -Database sinai          # BD de compañía
   .\run-sql.ps1 -Query "SELECT * FROM admin.customer_type;"     # consulta inline

 REQUISITOS:
   - Variable de entorno CMS_DB_PASSWORD con la contraseña del usuario cmssystem.
	 Configurar UNA sola vez:  setx CMS_DB_PASSWORD "TU_PASSWORD"
	 (cerrar y reabrir la terminal después de setx)

 AUTOR: EAMR, BITI SOLUTIONS S.A
================================================================================
#>
param(
	[string]$File,
	[string]$Query,
	[string]$Database = "cms",
	[string]$DbHost   = "10.0.0.1",
	[int]   $Port     = 5432,
	[string]$User     = "cmssystem",
	[string]$PsqlPath = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PsqlPath)) {
	Write-Error "No se encontró psql en '$PsqlPath'. Ajusta el parámetro -PsqlPath."
	exit 1
}

if (-not $env:CMS_DB_PASSWORD) {
	Write-Error "Falta la variable de entorno CMS_DB_PASSWORD. Configúrala con: setx CMS_DB_PASSWORD `"TU_PASSWORD`" y reabre la terminal."
	exit 1
}

# Desactivar cualquier pager interactivo (causa principal de bloqueos)
$env:PGPASSWORD      = $env:CMS_DB_PASSWORD
$env:PGCLIENTENCODING = "UTF8"
$env:PSQL_PAGER      = ""
$env:PAGER           = ""

$commonArgs = @(
	"-h", $DbHost,
	"-p", $Port,
	"-U", $User,
	"-d", $Database,
	"-P", "pager=off",
	"-v", "ON_ERROR_STOP=1",
	"--no-psqlrc"
)

if ($File) {
	if (-not (Test-Path $File)) {
		# intentar ruta relativa al directorio del script
		$candidate = Join-Path $PSScriptRoot $File
		if (Test-Path $candidate) { $File = $candidate }
		else { Write-Error "No se encontró el archivo SQL: $File"; exit 1 }
	}
	Write-Host "Ejecutando archivo '$File' en BD '$Database'..." -ForegroundColor Cyan
	& $PsqlPath @commonArgs -f $File
}
elseif ($Query) {
	Write-Host "Ejecutando consulta en BD '$Database'..." -ForegroundColor Cyan
	& $PsqlPath @commonArgs -c $Query
}
else {
	Write-Error "Debes indicar -File <ruta.sql> o -Query `"SELECT ...`""
	exit 1
}

exit $LASTEXITCODE
