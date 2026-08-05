# ================================================================================
# SCRIPT: TestEmission_Final.ps1
# PROPÓSITO: Prueba final de emisión de factura electrónica con customer model
# DESCRIPCIÓN: Emite una factura usando customer 1 como emisor y customer 2 como receptor
# AUTOR: EAMR, BITI SOLUTIONS S.A
# CREADO: 2026
# ================================================================================

$ErrorActionPreference = "Stop"

# Configuración
$baseUrl = "https://localhost:7082"
$apiUrl = "https://localhost:7082/api"

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "PRUEBA FINAL DE EMISIÓN - CUSTOMER MODEL" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host ""

# Deshabilitar validación SSL para desarrollo
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
add-type @"
	using System.Net;
	using System.Security.Cryptography.X509Certificates;
	public class TrustAllCertsPolicy : ICertificatePolicy {
		public bool CheckValidationResult(
			ServicePoint srvPoint, X509Certificate certificate,
			WebRequest request, int certificateProblem) {
			return true;
		}
	}
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy

# Paso 1: Login
Write-Host "1. Login..." -ForegroundColor Yellow
$loginPayload = @{
	email = "ernesto.martinez@biti-solutions.com"
	password = "EAMR2601"
	companyId = 4
} | ConvertTo-Json

try {
	$loginResponse = Invoke-RestMethod -Uri "$apiUrl/auth/login" -Method Post `
		-ContentType "application/json" -Body $loginPayload -SessionVariable session

	$token = $loginResponse.token
	Write-Host "   ✓ Login exitoso" -ForegroundColor Green
	Write-Host "   Token: $($token.Substring(0, 50))..." -ForegroundColor Gray
} catch {
	Write-Host "   ✗ Error en login: $($_.Exception.Message)" -ForegroundColor Red
	exit 1
}

# Headers con token
$headers = @{
	"Authorization" = "Bearer $token"
	"Content-Type" = "application/json"
}

# Paso 2: Verificar emisor (customer 1)
Write-Host ""
Write-Host "2. Verificar emisor (customer 1)..." -ForegroundColor Yellow
try {
	$emisor = Invoke-RestMethod -Uri "$apiUrl/Customer/1" -Method Get -Headers $headers
	Write-Host "   ✓ Emisor encontrado:" -ForegroundColor Green
	Write-Host "     - ID: $($emisor.id)" -ForegroundColor Gray
	Write-Host "     - Nombre: $($emisor.name)" -ForegroundColor Gray
	Write-Host "     - Identificación: $($emisor.identification)" -ForegroundColor Gray
	Write-Host "     - Es emisor: $($emisor.isIssuer)" -ForegroundColor Gray
	Write-Host "     - Ambiente activo: $($emisor.activeEnvironment)" -ForegroundColor Gray
} catch {
	Write-Host "   ✗ Error al obtener emisor: $($_.Exception.Message)" -ForegroundColor Red
	exit 1
}

# Paso 3: Verificar receptor (customer 2)
Write-Host ""
Write-Host "3. Verificar receptor (customer 2)..." -ForegroundColor Yellow
try {
	$receptor = Invoke-RestMethod -Uri "$apiUrl/Customer/2" -Method Get -Headers $headers
	Write-Host "   ✓ Receptor encontrado:" -ForegroundColor Green
	Write-Host "     - ID: $($receptor.id)" -ForegroundColor Gray
	Write-Host "     - Nombre: $($receptor.name)" -ForegroundColor Gray
	Write-Host "     - Identificación: $($receptor.identification)" -ForegroundColor Gray
} catch {
	Write-Host "   ✗ Error al obtener receptor: $($_.Exception.Message)" -ForegroundColor Red
	exit 1
}

# Paso 4: Emitir factura
Write-Host ""
Write-Host "4. Emitir factura electrónica..." -ForegroundColor Yellow

$emissionPayload = @{
	issuerId = 1
	receptorId = 2
	documentType = "01"  # Factura electrónica
	saleCondition = "01"  # Contado
	currency = "CRC"
	exchangeRate = 1.0
	receptorEmail = "ernesto.martinez@biti-solutions.com"
	lines = @(
		@{
			lineNumber = 1
			code = "ITEM-001"
			description = "Servicio de consultoría TI"
			quantity = 1.0
			unit = "Srv"
			unitPrice = 50000.0
			subtotal = 50000.0
			discount = 0.0
			discountReason = $null
			taxType = "01"  # IVA
			taxRate = 13.0
			taxAmount = 6500.0
			total = 56500.0
		}
	)
	observations = "Factura de prueba - Sistema CMS"
} | ConvertTo-Json -Depth 10

Write-Host "   Payload:" -ForegroundColor Gray
Write-Host $emissionPayload -ForegroundColor DarkGray

try {
	$emitResponse = Invoke-RestMethod -Uri "$apiUrl/electronicinvoice/emit" -Method Post `
		-Headers $headers -Body $emissionPayload

	Write-Host ""
	Write-Host "   ✓ EMISIÓN EXITOSA" -ForegroundColor Green
	Write-Host "   ================================================" -ForegroundColor Cyan
	Write-Host "   Clave numérica: $($emitResponse.documentKey)" -ForegroundColor White
	Write-Host "   Estado inicial: $($emitResponse.status)" -ForegroundColor White
	Write-Host "   Consecutivo: $($emitResponse.consecutivo)" -ForegroundColor White
	Write-Host "   ================================================" -ForegroundColor Cyan

	$claveNumérica = $emitResponse.documentKey

} catch {
	Write-Host "   ✗ Error en emisión: $($_.Exception.Message)" -ForegroundColor Red
	if ($_.ErrorDetails) {
		Write-Host "   Detalles: $($_.ErrorDetails.Message)" -ForegroundColor Red
	}
	exit 1
}

# Paso 5: Esperar y consultar estado en Hacienda
Write-Host ""
Write-Host "5. Esperando respuesta de Hacienda (15 segundos)..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

Write-Host ""
Write-Host "6. Consultar estado en Hacienda..." -ForegroundColor Yellow
try {
	$statusResponse = Invoke-RestMethod -Uri "$apiUrl/electronicinvoice/status/$claveNumérica" `
		-Method Get -Headers $headers

	Write-Host ""
	Write-Host "   ================================================" -ForegroundColor Cyan
	Write-Host "   RESPUESTA DE HACIENDA" -ForegroundColor White
	Write-Host "   ================================================" -ForegroundColor Cyan
	Write-Host "   Estado: $($statusResponse.status)" -ForegroundColor $(if($statusResponse.status -eq 'aceptado') {'Green'} else {'Yellow'})
	Write-Host "   Mensaje: $($statusResponse.message)" -ForegroundColor White

	if ($statusResponse.status -eq 'aceptado') {
		Write-Host ""
		Write-Host "   🎉 FACTURA ACEPTADA POR HACIENDA 🎉" -ForegroundColor Green
		Write-Host ""
	} else {
		Write-Host ""
		Write-Host "   ⚠️  Estado pendiente o rechazado" -ForegroundColor Yellow
		Write-Host ""
	}

} catch {
	Write-Host "   ✗ Error al consultar estado: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "PRUEBA COMPLETADA" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan
