# Simple test script for consecutive logic
param([int]$NumAsientos = 5)

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  TEST DE CONSECUTIVOS - JOURNAL ENTRIES" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# Show initial state
Write-Host "PASO 1: Estado inicial del consecutivo..." -ForegroundColor Yellow
Write-Host ""
$env:PGPASSWORD = "POStgres2026"
psql -h 10.0.0.1 -p 5432 -U postgres -d sinai -c "SELECT code, mask, last_value, final_value FROM sinai.consecutive WHERE code = 'JOURNAL_ENTRY_ACC';"
Write-Host ""

# Instructions
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  INSTRUCCIONES" -ForegroundColor Yellow
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Abre el navegador en: https://localhost:5001" -ForegroundColor White
Write-Host ""
Write-Host "Inicia sesion con:" -ForegroundColor White
Write-Host "  Usuario: ernesto.martinez@biti.com.pa" -ForegroundColor Gray
Write-Host "  Password: YQN5QBLSB2LA-" -ForegroundColor Gray
Write-Host ""
Write-Host "Navega a: Accounting > Journal Entries" -ForegroundColor White
Write-Host ""
Write-Host "Crea $NumAsientos asientos con:" -ForegroundColor White
Write-Host "  - Entry Type: Standard" -ForegroundColor Gray
Write-Host "  - Entry Date: Hoy" -ForegroundColor Gray
Write-Host "  - Reference: TEST-MASK-1, TEST-MASK-2, etc." -ForegroundColor Gray
Write-Host "  - Linea 1: Cuenta cualquiera, Debit 1000, Credit 0" -ForegroundColor Gray
Write-Host "  - Linea 2: Cuenta cualquiera, Debit 0, Credit 1000" -ForegroundColor Gray
Write-Host ""
Write-Host "Observa el Entry Number generado automaticamente" -ForegroundColor White
Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Presiona ENTER cuando hayas creado los asientos..." -ForegroundColor Yellow
Read-Host

# Verify results
Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  VERIFICACION" -ForegroundColor Yellow
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "PASO 2: Asientos creados..." -ForegroundColor Yellow
Write-Host ""
psql -h 10.0.0.1 -p 5432 -U postgres -d sinai -c "SELECT id_journal_entry, entry_number, reference FROM sinai.journal_entry WHERE reference LIKE 'TEST-MASK-%' ORDER BY id_journal_entry;"
Write-Host ""

Write-Host "PASO 3: Estado final del consecutivo..." -ForegroundColor Yellow
Write-Host ""
psql -h 10.0.0.1 -p 5432 -U postgres -d sinai -c "SELECT code, mask, last_value, last_date FROM sinai.consecutive WHERE code = 'JOURNAL_ENTRY_ACC';"
Write-Host ""

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  RESULTADOS" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Mascara: **-9999-999" -ForegroundColor White
Write-Host "Formato esperado: JE-0001-001, JE-0001-002, ..." -ForegroundColor White
Write-Host "Incremento: +1 con carry" -ForegroundColor White
Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""
