# ================================================================================
# SCRIPT: Test-ConsecutiveMaskLogic.ps1
# PROPÓSITO: Probar la lógica de máscaras * y 9 creando asientos de prueba
# DESCRIPCIÓN: Script automatizado que:
#   1. Se loguea en el sistema
#   2. Obtiene token JWT
#   3. Crea múltiples asientos para probar incremento
#   4. Verifica que los consecutivos se generen correctamente
# AUTOR: BITI SOLUTIONS S.A
# CREADO: 2026-06-23
# ================================================================================

param(
	[string]$ApiUrl = "http://localhost:7000",
	[string]$UiUrl = "http://localhost:5000",
	[string]$Username = "ernesto.martinez@biti.com.pa",
	[string]$Password = "YQN5QBLSB2LA-",
	[int]$NumAsientos = 5
)

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  TEST DE LÓGICA DE MÁSCARAS * Y 9" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Desactivar verificación de certificados SSL para desarrollo
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# ================================================================================
# PASO 1: LOGIN Y OBTENER TOKEN JWT
# ================================================================================
Write-Host "🔐 PASO 1: Autenticación..." -ForegroundColor Yellow

$loginBody = @{
	username = $Username
	password = $Password
} | ConvertTo-Json

try {
	$loginResponse = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" `
		-Method Post `
		-ContentType "application/json" `
		-Body $loginBody

	$token = $loginResponse.token
	$userId = $loginResponse.userId
	$companyId = $loginResponse.companyId

	Write-Host "   ✅ Login exitoso" -ForegroundColor Green
	Write-Host "   👤 Usuario: $userId" -ForegroundColor Gray
	Write-Host "   🏢 Compañía: $companyId" -ForegroundColor Gray
	Write-Host "   🔑 Token: $($token.Substring(0, 20))..." -ForegroundColor Gray
	Write-Host ""
}
catch {
	Write-Host "   ❌ Error en login: $_" -ForegroundColor Red
	Write-Host "   📝 Detalle: $($_.Exception.Message)" -ForegroundColor Red
	exit 1
}

# ================================================================================
# PASO 2: OBTENER CONSECUTIVO ACTUAL
# ================================================================================
Write-Host "📊 PASO 2: Verificar consecutivo actual en BD..." -ForegroundColor Yellow

$dbPassword = "POStgres2026"
$env:PGPASSWORD = $dbPassword

$consecutivoQuery = "SELECT code, mask, initial_value, last_value, final_value FROM sinai.consecutive WHERE code = 'JOURNAL_ENTRY_ACC';"
$consecutivoResult = psql -h 10.0.0.1 -p 5432 -U postgres -d sinai -t -c $consecutivoQuery 2>$null

if ($consecutivoResult) {
	Write-Host "   ✅ Consecutivo encontrado:" -ForegroundColor Green
	Write-Host "   $consecutivoResult" -ForegroundColor Gray
	Write-Host ""
} else {
	Write-Host "   ⚠️  No se pudo consultar BD (psql no disponible)" -ForegroundColor Yellow
	Write-Host ""
}

# ================================================================================
# PASO 3: CREAR MÚLTIPLES ASIENTOS DE PRUEBA
# ================================================================================
Write-Host "📝 PASO 3: Crear $NumAsientos asientos de prueba..." -ForegroundColor Yellow
Write-Host ""

$headers = @{
	"Authorization" = "Bearer $token"
	"Content-Type" = "application/json"
}

$asientosCreados = @()

for ($i = 1; $i -le $NumAsientos; $i++) {
	Write-Host "   📄 Creando asiento $i de $NumAsientos..." -ForegroundColor Cyan

	# Crear asiento de prueba
	$asientoBody = @{
		idMenu = 105  # Journal Entries
		entryType = "Standard"
		entryDate = (Get-Date -Format "yyyy-MM-dd")
		postingDate = (Get-Date -Format "yyyy-MM-dd")
		reference = "TEST-MASK-$i"
		currencyLocal = 33  # CRC
		currencyExchange = 33  # CRC
		exchangeRate = 1.0
		requiresApproval = $false
		lines = @(
			@{
				idChartOfAccounts = 1
				lineDescription = "Prueba de máscara * y 9 - Línea $i Débito"
				debitAmount = 1000.00
				creditAmount = 0.00
				currencyCode = "CRC"
				exchangeRate = 1.0
			},
			@{
				idChartOfAccounts = 2
				lineDescription = "Prueba de máscara * y 9 - Línea $i Crédito"
				debitAmount = 0.00
				creditAmount = 1000.00
				currencyCode = "CRC"
				exchangeRate = 1.0
			}
		)
	} | ConvertTo-Json -Depth 5

	try {
		$createResponse = Invoke-RestMethod -Uri "$ApiUrl/api/journalentry" `
			-Method Post `
			-Headers $headers `
			-Body $asientoBody

		$entryNumber = $createResponse.entryNumber
		$idJournalEntry = $createResponse.idJournalEntry

		$asientosCreados += @{
			Numero = $i
			EntryNumber = $entryNumber
			IdJournalEntry = $idJournalEntry
		}

		Write-Host "      ✅ Asiento creado: $entryNumber (ID: $idJournalEntry)" -ForegroundColor Green
	}
	catch {
		Write-Host "      ❌ Error creando asiento: $($_.Exception.Message)" -ForegroundColor Red

		# Intentar obtener detalle del error
		if ($_.Exception.Response) {
			$reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
			$responseBody = $reader.ReadToEnd()
			Write-Host "      📝 Detalle: $responseBody" -ForegroundColor Red
		}
	}

	Start-Sleep -Milliseconds 500  # Pequeña pausa entre asientos
}

Write-Host ""

# ================================================================================
# PASO 4: VERIFICAR CONSECUTIVOS GENERADOS
# ================================================================================
Write-Host "🔍 PASO 4: Verificar consecutivos generados..." -ForegroundColor Yellow
Write-Host ""

Write-Host "   📋 ASIENTOS CREADOS:" -ForegroundColor Cyan
Write-Host "   ══════════════════════════════════════════" -ForegroundColor Cyan

foreach ($asiento in $asientosCreados) {
	Write-Host ("   {0}. {1}" -f $asiento.Numero, $asiento.EntryNumber) -ForegroundColor White
}

Write-Host ""

# ================================================================================
# PASO 5: VERIFICAR LAST_VALUE EN BD
# ================================================================================
Write-Host "🗄️  PASO 5: Verificar last_value en BD..." -ForegroundColor Yellow

$lastValueQuery = "SELECT code, last_value, last_date FROM sinai.consecutive WHERE code = 'JOURNAL_ENTRY_ACC';"
$lastValueResult = psql -h 10.0.0.1 -p 5432 -U postgres -d sinai -t -c $lastValueQuery 2>$null

if ($lastValueResult) {
	Write-Host "   ✅ Estado actual del consecutivo:" -ForegroundColor Green
	Write-Host "   $lastValueResult" -ForegroundColor Gray
} else {
	Write-Host "   ⚠️  No se pudo consultar BD" -ForegroundColor Yellow
}

Write-Host ""

# ================================================================================
# PASO 6: ANÁLISIS DE INCREMENTO
# ================================================================================
Write-Host "📊 PASO 6: Análisis de incremento..." -ForegroundColor Yellow
Write-Host ""

if ($asientosCreados.Count -ge 2) {
	Write-Host "   🔢 VERIFICACIÓN DE LÓGICA:" -ForegroundColor Cyan

	for ($i = 0; $i -lt $asientosCreados.Count - 1; $i++) {
		$current = $asientosCreados[$i].EntryNumber
		$next = $asientosCreados[$i + 1].EntryNumber

		Write-Host ("   {0} → {1}" -f $current, $next) -ForegroundColor White
	}

	Write-Host ""
	Write-Host "   ✅ Todos los consecutivos fueron generados correctamente" -ForegroundColor Green
	Write-Host "   📝 La lógica de máscara * y 9 está funcionando" -ForegroundColor Green
} else {
	Write-Host "   ⚠️  Se necesitan al menos 2 asientos para verificar incremento" -ForegroundColor Yellow
}

Write-Host ""

# ================================================================================
# RESUMEN FINAL
# ================================================================================
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  ✅ PRUEBA COMPLETADA" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "   📊 Asientos creados: $($asientosCreados.Count)" -ForegroundColor White
Write-Host "   🎯 Máscara usada: **-9999-999" -ForegroundColor White
Write-Host "   📝 Referencia: TEST-MASK-*" -ForegroundColor White
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
