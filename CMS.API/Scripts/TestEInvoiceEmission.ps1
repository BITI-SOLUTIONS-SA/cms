# ================================================================================
# SCRIPT: TestEInvoiceEmission.ps1
# PROPOSITO: Prueba end-to-end de emision de factura electronica CR v4.4
# AUTOR: EAMR, BITI SOLUTIONS S.A
# CREADO: 2026
# ================================================================================

param(
	[string]$ApiBaseUrl = "http://localhost:7000",
	[string]$Username = "eamr",
	[string]$Password = "eamr1024",
	[int]$CompanyId = 4,
	[int]$IssuerId = 1,
	[int]$ReceptorId = 2
)

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "PRUEBA DE EMISION DE FACTURA CR v4.4" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 1. Autenticacion
Write-Host "[1/5] Autenticando usuario..." -ForegroundColor Green
$loginBody = @{
	username = $Username
	password = $Password
} | ConvertTo-Json

try {
	$loginResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/api/Auth/login" -Method Post -Body $loginBody -ContentType "application/json"
	$token = $loginResponse.token
	Write-Host "  OK Token obtenido" -ForegroundColor Green
} catch {
	Write-Host "  ERROR en autenticacion: $_" -ForegroundColor Red
	exit 1
}

$headers = @{
	"Authorization" = "Bearer $token"
	"Content-Type" = "application/json"
}

# 2. Verificar Customer Emisor
Write-Host "[2/5] Verificando customer emisor (ID=$IssuerId)..." -ForegroundColor Green
try {
	$issuer = Invoke-RestMethod -Uri "$ApiBaseUrl/api/Customer/$IssuerId" -Method Get -Headers $headers
	Write-Host "  OK Emisor: $($issuer.name)" -ForegroundColor Green
	Write-Host "    - Codigo: $($issuer.code)" -ForegroundColor Gray
	Write-Host "    - Identificacion: $($issuer.identification)" -ForegroundColor Gray
	Write-Host "    - Es Emisor: $($issuer.isIssuer)" -ForegroundColor Gray
	Write-Host "    - Ambiente: $($issuer.activeEnvironment)" -ForegroundColor Gray
} catch {
	Write-Host "  ERROR obteniendo emisor: $_" -ForegroundColor Red
	exit 1
}

# 3. Verificar Customer Receptor
Write-Host "[3/5] Verificando customer receptor (ID=$ReceptorId)..." -ForegroundColor Green
try {
	$receptor = Invoke-RestMethod -Uri "$ApiBaseUrl/api/Customer/$ReceptorId" -Method Get -Headers $headers
	Write-Host "  OK Receptor: $($receptor.name)" -ForegroundColor Green
	Write-Host "    - Codigo: $($receptor.code)" -ForegroundColor Gray
	Write-Host "    - Identificacion: $($receptor.identification)" -ForegroundColor Gray
} catch {
	Write-Host "  ERROR obteniendo receptor: $_" -ForegroundColor Red
	exit 1
}

# 4. Emitir Factura Electronica
Write-Host "[4/5] Emitiendo factura electronica..." -ForegroundColor Green

$emitBody = @{
	companyId = $CompanyId
	issuerId = $IssuerId
	receptorId = $ReceptorId
	documentType = "01"
	branch = "001"
	terminal = "00001"
	saleCondition = "01"
	paymentMethod = "01"
	currency = "CRC"
	exchangeRate = 1
	lines = @(
		@{
			cabysCode = "2118401010109"
			detail = "Articulo de prueba - Emision con Customer nuevo"
			quantity = 1
			unitMeasure = "Unid"
			unitPrice = 10000
			taxRate = 13
		}
	)
	references = @()
	userId = 1
} | ConvertTo-Json -Depth 10

try {
	Write-Host "  -> Enviando comprobante a Hacienda..." -ForegroundColor Yellow
	$emitResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/api/ElectronicDocument/emit" -Method Post -Body $emitBody -Headers $headers

	Write-Host "  OK Documento creado exitosamente" -ForegroundColor Green
	Write-Host "    - ID Documento: $($emitResponse.documentId)" -ForegroundColor Gray
	Write-Host "    - Clave: $($emitResponse.clave)" -ForegroundColor Gray
	Write-Host "    - Consecutivo: $($emitResponse.consecutive)" -ForegroundColor Gray
	Write-Host "    - Estado: $($emitResponse.status)" -ForegroundColor Gray
	Write-Host "    - Enviado a Hacienda: $($emitResponse.sentToHacienda)" -ForegroundColor Gray
	Write-Host "    - Mensaje: $($emitResponse.message)" -ForegroundColor Gray

	$documentId = $emitResponse.documentId
	$clave = $emitResponse.clave

} catch {
	Write-Host "  ERROR emitiendo factura: $_" -ForegroundColor Red
	exit 1
}

# 5. Esperar procesamiento y verificar estado en Hacienda
Write-Host "[5/5] Verificando respuesta de Hacienda..." -ForegroundColor Green
Write-Host "  -> Esperando procesamiento (30 segundos)..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

try {
	$statusResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/api/ElectronicDocument/$documentId" -Method Get -Headers $headers

	Write-Host ""
	Write-Host "=====================================" -ForegroundColor Cyan
	Write-Host "RESULTADO FINAL" -ForegroundColor Cyan
	Write-Host "=====================================" -ForegroundColor Cyan
	Write-Host "Estado del Documento: $($statusResponse.status)" -ForegroundColor $(if ($statusResponse.status -eq "Aceptado") { "Green" } else { "Yellow" })
	Write-Host "Estado Hacienda: $($statusResponse.haciendaStatus)" -ForegroundColor $(if ($statusResponse.haciendaStatus -eq "aceptado") { "Green" } else { "Yellow" })
	Write-Host "Clave: $clave" -ForegroundColor Gray
	Write-Host "Consecutivo: $($statusResponse.consecutive)" -ForegroundColor Gray

	if ($statusResponse.haciendaDetail) {
		Write-Host "Detalle Hacienda: $($statusResponse.haciendaDetail)" -ForegroundColor Gray
	}

	Write-Host ""

	if ($statusResponse.haciendaStatus -eq "aceptado" -or $statusResponse.status -eq "Aceptado") {
		Write-Host "FACTURA ACEPTADA POR HACIENDA" -ForegroundColor Green
		Write-Host ""
		Write-Host "La migracion a Customer y CustomerBillingCredential fue EXITOSA." -ForegroundColor Green
		Write-Host "El sistema esta listo para eliminar las tablas legacy." -ForegroundColor Green
		exit 0
	} else {
		Write-Host "ADVERTENCIA: La factura NO fue aceptada" -ForegroundColor Yellow
		Write-Host "Estado: $($statusResponse.status)" -ForegroundColor Yellow
		Write-Host "Revisar logs para mas detalles." -ForegroundColor Yellow
		exit 1
	}

} catch {
	Write-Host "  ERROR verificando estado: $_" -ForegroundColor Red
	exit 1
}
