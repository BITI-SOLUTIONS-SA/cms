# ================================================================================
# SCRIPT: Test-JournalEntryCreation.ps1
# PROPÓSITO: Crear asientos de prueba simulando el flujo de la UI
# DESCRIPCIÓN: Usa las sesiones de la UI para crear asientos y verificar
#              la generación de consecutivos con máscara * y 9
# ================================================================================

param(
	[int]$NumAsientos = 5
)

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  TEST DE CONSECUTIVOS - CREACIÓN DE ASIENTOS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ================================================================================
# PASO 1: VERIFICAR ESTADO INICIAL
# ================================================================================
Write-Host "📊 PASO 1: Estado inicial del consecutivo..." -ForegroundColor Yellow
Write-Host ""

$env:PGPASSWORD = "POStgres2026"
$initialQuery = @"
SELECT 
	code,
	mask,
	initial_value,
	last_value,
	final_value,
	length,
	last_date
FROM sinai.consecutive 
WHERE code = 'JOURNAL_ENTRY_ACC'
ORDER BY id_consecutive;
"@

Write-Host "   🗄️  Consultando BD..." -ForegroundColor Gray
$initialResult = psql -h 10.0.0.1 -p 5432 -U postgres -d sinai -c $initialQuery 2>&1

if ($LASTEXITCODE -eq 0) {
	Write-Host ""
	Write-Host $initialResult
	Write-Host ""
} else {
	Write-Host "   Advertencia: No se pudo consultar la BD" -ForegroundColor Yellow
	Write-Host "   $initialResult" -ForegroundColor Gray
	Write-Host ""
}

# ================================================================================
# PASO 2: INSTRUCCIONES PARA CREAR ASIENTOS DESDE LA UI
# ================================================================================
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "  INSTRUCCIONES PARA CREAR ASIENTOS" -ForegroundColor Yellow
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Por favor, realiza los siguientes pasos en el navegador:" -ForegroundColor White
Write-Host ""
Write-Host "1. Abre el navegador en:" -ForegroundColor Cyan
Write-Host "   https://localhost:5001" -ForegroundColor White
Write-Host ""
Write-Host "2. Inicia sesion con:" -ForegroundColor Cyan
Write-Host "   Usuario: ernesto.martinez@biti.com.pa" -ForegroundColor White
Write-Host "   Contrasena: YQN5QBLSB2LA-" -ForegroundColor White
Write-Host ""
Write-Host "3. Navega a: Accounting > Journal Entries" -ForegroundColor Cyan
Write-Host ""
Write-Host "4. Crea $NumAsientos asientos nuevos con estos datos:" -ForegroundColor Cyan
Write-Host ""
Write-Host "   Asiento de prueba:" -ForegroundColor Yellow
Write-Host "   ────────────────────" -ForegroundColor Gray
Write-Host "   - Entry Type:   Standard" -ForegroundColor White
Write-Host "   - Entry Date:   Hoy" -ForegroundColor White
Write-Host "   - Posting Date: Hoy" -ForegroundColor White
Write-Host "   - Reference:    TEST-MASK-1, TEST-MASK-2, etc." -ForegroundColor White
Write-Host "   - Currency:     CRC" -ForegroundColor White
Write-Host ""
Write-Host "   Lineas del asiento:" -ForegroundColor Yellow
Write-Host "   ────────────────────" -ForegroundColor Gray
Write-Host "   Linea 1:" -ForegroundColor White
Write-Host "   - Account:     (selecciona cualquier cuenta)" -ForegroundColor White
Write-Host "   - Description: Prueba de mascara * y 9 - Debito" -ForegroundColor White
Write-Host "   - Debit:       1000.00" -ForegroundColor White
Write-Host "   - Credit:      0.00" -ForegroundColor White
Write-Host ""
Write-Host "   Linea 2:" -ForegroundColor White
Write-Host "   - Account:     (selecciona otra cuenta)" -ForegroundColor White
Write-Host "   - Description: Prueba de mascara * y 9 - Credito" -ForegroundColor White
Write-Host "   - Debit:       0.00" -ForegroundColor White
Write-Host "   - Credit:      1000.00" -ForegroundColor White
Write-Host ""
Write-Host "5. Haz clic en 'Save' para cada asiento" -ForegroundColor Cyan
Write-Host ""
Write-Host "6. Observa el campo 'Entry Number' generado automaticamente" -ForegroundColor Cyan
Write-Host ""
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Presiona ENTER cuando hayas creado los $NumAsientos asientos..." -ForegroundColor Yellow
Read-Host

# ================================================================================
# PASO 3: VERIFICAR ASIENTOS CREADOS
# ================================================================================
Write-Host ""
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "  VERIFICACION DE RESULTADOS" -ForegroundColor Yellow
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "PASO 3: Verificar asientos creados..." -ForegroundColor Yellow
Write-Host ""

$asientosQuery = @"
SELECT 
	id_journal_entry,
	entry_number,
	entry_date,
	reference,
	status,
	createdate
FROM sinai.journal_entry
WHERE reference LIKE 'TEST-MASK-%'
ORDER BY id_journal_entry DESC
LIMIT 10;
"@

Write-Host "   Consultando asientos creados..." -ForegroundColor Gray
$asientosResult = psql -h 10.0.0.1 -p 5432 -U postgres -d sinai -c $asientosQuery 2>&1

if ($LASTEXITCODE -eq 0) {
	Write-Host ""
	Write-Host $asientosResult
	Write-Host ""
} else {
	Write-Host "   Advertencia: No se pudo consultar los asientos" -ForegroundColor Yellow
	Write-Host "   $asientosResult" -ForegroundColor Gray
	Write-Host ""
}

# ================================================================================
# PASO 4: VERIFICAR ESTADO FINAL DEL CONSECUTIVO
# ================================================================================
Write-Host "PASO 4: Estado final del consecutivo..." -ForegroundColor Yellow
Write-Host ""

$finalQuery = @"
SELECT 
	code,
	mask,
	last_value AS current_last_value,
	final_value,
	length,
	last_date,
	last_user
FROM sinai.consecutive 
WHERE code = 'JOURNAL_ENTRY_ACC';
"@

Write-Host "   Consultando estado final..." -ForegroundColor Gray
$finalResult = psql -h 10.0.0.1 -p 5432 -U postgres -d sinai -c $finalQuery 2>&1

if ($LASTEXITCODE -eq 0) {
	Write-Host ""
	Write-Host $finalResult
	Write-Host ""
} else {
	Write-Host "   Advertencia: No se pudo consultar el estado final" -ForegroundColor Yellow
	Write-Host "   $finalResult" -ForegroundColor Gray
	Write-Host ""
}

# ================================================================================
# PASO 5: ANALISIS DE INCREMENTO
# ================================================================================
Write-Host "PASO 5: Analisis de incremento..." -ForegroundColor Yellow
Write-Host ""

$incrementQuery = @"
SELECT 
	entry_number,
	reference,
	entry_date,
	createdate
FROM sinai.journal_entry
WHERE reference LIKE 'TEST-MASK-%'
ORDER BY id_journal_entry ASC;
"@

Write-Host "   Secuencia de entry_number generados:" -ForegroundColor Gray
$incrementResult = psql -h 10.0.0.1 -p 5432 -U postgres -d sinai -t -c $incrementQuery 2>&1

if ($LASTEXITCODE -eq 0) {
	Write-Host ""
	$lines = $incrementResult -split "`n" | Where-Object { $_.Trim() -ne "" }
	foreach ($line in $lines) {
		$parts = $line -split '\|'
		if ($parts.Count -ge 2) {
			$entryNum = $parts[0].Trim()
			$ref = $parts[1].Trim()
			Write-Host "   -> $entryNum  ($ref)" -ForegroundColor White
		}
	}
	Write-Host ""
} else {
	Write-Host "   Advertencia: No se pudo analizar el incremento" -ForegroundColor Yellow
	Write-Host ""
}

# ================================================================================
# RESUMEN
# ================================================================================
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "  VERIFICACION COMPLETADA" -ForegroundColor Green
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "   -> Mascara configurada: **-9999-999" -ForegroundColor White
Write-Host "   -> Formato esperado:    JE-0001-001, JE-0001-002, ..." -ForegroundColor White
Write-Host "   -> Incremento:          +1 con carry de derecha a izquierda" -ForegroundColor White
Write-Host ""
Write-Host "   -> Los entry_number deben seguir la logica:" -ForegroundColor Green
Write-Host "      - * = alfanumerico (JE, JF, ..., ZZ)" -ForegroundColor Gray
Write-Host "      - 9 = numerico (0-9)" -ForegroundColor Gray
Write-Host "      - - = literal (no cambia)" -ForegroundColor Gray
Write-Host "      - Incremento: +1 con carry" -ForegroundColor Gray
Write-Host ""
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host ""
