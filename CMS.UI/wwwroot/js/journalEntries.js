// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/journalEntries.js
// PROPÓSITO: Lógica de cliente para mantenimiento de Asientos de Diario
// DESCRIPCIÓN: CRUD completo, maestro-detalle, validación de cuadre, contabilización
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-XX
// ================================================================================

const JE = (() => {
    'use strict';

    let allEntries = [];
    let currentEntry = null;
    let currentLines = [];
    let accounts = [];
    let costCenters = [];
    let editingLineIndex = -1;

    // ===== INICIALIZACIÓN =====

    function init() {
        loadAccounts();
        loadCostCenters();
        load();
        bindFilters();
        setDefaultDates();
    }

    function bindFilters() {
        ['filterSearch', 'filterStatus', 'filterType', 'filterDateFrom', 'filterDateTo'].forEach(id => {
            const el = document.getElementById(id);
            if (el) {
                el.addEventListener('change', applyFilters);
                if (id === 'filterSearch') el.addEventListener('keyup', applyFilters);
            }
        });
    }

    function setDefaultDates() {
        const today = new Date().toISOString().split('T')[0];
        const firstDay = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0];
        document.getElementById('filterDateFrom').value = firstDay;
        document.getElementById('filterDateTo').value = today;
        document.getElementById('entryDate').value = today;
        document.getElementById('postingDate').value = today;
    }

    // ===== CARGAR CATÁLOGOS =====

    async function loadAccounts() {
        try {
            const response = await fetch(`${COA_API}?isActive=true&isDetail=true`, {
                headers: { 'Authorization': `Bearer ${JE_TOKEN}` }
            });
            if (!response.ok) throw new Error('Error loading accounts');
            accounts = await response.json();
            populateAccountDropdowns();
        } catch (error) {
            console.error('Error loading accounts:', error);
            showToast('Error al cargar cuentas contables', 'error');
        }
    }

    async function loadCostCenters() {
        try {
            const response = await fetch(`${CC_API}?isActive=true&isPostingAllowed=true`, {
                headers: { 'Authorization': `Bearer ${JE_TOKEN}` }
            });
            if (!response.ok) throw new Error('Error loading cost centers');
            costCenters = await response.json();
            populateCostCenterDropdowns();
        } catch (error) {
            console.error('Error loading cost centers:', error);
            showToast('Error al cargar centros de costo', 'error');
        }
    }

    function populateAccountDropdowns() {
        const select = document.getElementById('lineAccount');
        if (!select) return;

        select.innerHTML = '<option value="">Seleccione cuenta...</option>';
        accounts.forEach(acc => {
            const opt = document.createElement('option');
            opt.value = acc.idChartOfAccounts;
            opt.textContent = `${acc.code} - ${acc.name}`;
            opt.dataset.code = acc.code;
            opt.dataset.name = acc.name;
            select.appendChild(opt);
        });
    }

    function populateCostCenterDropdowns() {
        const select = document.getElementById('lineCostCenter');
        if (!select) return;

        select.innerHTML = '<option value="">Ninguno</option>';
        costCenters.forEach(cc => {
            const opt = document.createElement('option');
            opt.value = cc.code;
            opt.textContent = `${cc.code} - ${cc.name}`;
            opt.dataset.name = cc.name;
            select.appendChild(opt);
        });
    }

    // ===== CARGAR ASIENTOS =====

    async function load() {
        try {
            const idJournalEntryStatus = document.getElementById('filterStatus')?.value || '';
            const type = document.getElementById('filterType')?.value || '';
            const dateFrom = document.getElementById('filterDateFrom')?.value || '';
            const dateTo = document.getElementById('filterDateTo')?.value || '';
            const search = document.getElementById('filterSearch')?.value || '';

            let url = `${JE_API}?`;
            if (idJournalEntryStatus) url += `idJournalEntryStatus=${encodeURIComponent(idJournalEntryStatus)}&`;
            if (type) url += `entryType=${encodeURIComponent(type)}&`;
            if (dateFrom) url += `dateFrom=${encodeURIComponent(dateFrom)}&`;
            if (dateTo) url += `dateTo=${encodeURIComponent(dateTo)}&`;
            if (search) url += `search=${encodeURIComponent(search)}&`;

            const response = await fetch(url, {
                headers: { 'Authorization': `Bearer ${JE_TOKEN}` }
            });

            if (!response.ok) throw new Error('Error loading entries');
            allEntries = await response.json();
            renderTable(allEntries);
        } catch (error) {
            console.error('Error loading journal entries:', error);
            showToast('Error al cargar asientos de diario', 'error');
        }
    }

    function applyFilters() {
        load();
    }

    function clearFilters() {
        document.getElementById('filterSearch').value = '';
        document.getElementById('filterStatus').value = '2'; // 2 = Posted
        document.getElementById('filterType').value = '';
        setDefaultDates();
        load();
    }

    // ===== RENDERIZAR TABLA =====

    function renderTable(items) {
        const tbody = document.getElementById('journalEntriesTable');
        if (!tbody) return;

        if (!items || items.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="9" class="text-center text-muted py-5">
                        <i class="bi bi-inbox display-4 d-block mb-2"></i>
                        No se encontraron asientos de diario
                    </td>
                </tr>`;
            return;
        }

        tbody.innerHTML = items.map(e => {
            const statusClass = `status-${e.status.toLowerCase()}`;
            const statusBadge = `badge badge-${e.status.toLowerCase()}`;
            const typeLabel = getTypeLabel(e.entryType);

            return `
                <tr>
                    <td><strong class="text-info">${escapeHtml(e.entryNumber)}</strong></td>
                    <td>${formatDate(e.postingDate)}</td>
                    <td><span class="badge bg-secondary">${escapeHtml(e.period)}</span></td>
                    <td>
                        <div>${escapeHtml(e.description)}</div>
                        ${e.reference ? `<small class="text-muted">Ref: ${escapeHtml(e.reference)}</small>` : ''}
                    </td>
                    <td><span class="badge bg-info">${escapeHtml(typeLabel)}</span></td>
                    <td class="text-end debit-cell">${formatCurrency(e.debitTotal)}</td>
                    <td class="text-end credit-cell">${formatCurrency(e.creditTotal)}</td>
                    <td><span class="${statusBadge}">${getStatusLabel(e.status)}</span></td>
                    <td>
                        <div class="btn-group btn-group-sm">
                            <button class="btn btn-info" onclick="JE.view(${e.idJournalEntry})" title="Ver">
                                <i class="bi bi-eye"></i>
                            </button>
                            ${e.status === 'Draft' ? `
                                <button class="btn btn-warning" onclick="JE.openEdit(${e.idJournalEntry})" title="Editar">
                                    <i class="bi bi-pencil"></i>
                                </button>
                                <button class="btn btn-success" onclick="JE.confirmPost(${e.idJournalEntry}, '${escapeHtml(e.entryNumber)}')" title="Contabilizar">
                                    <i class="bi bi-check-circle"></i>
                                </button>
                                <button class="btn btn-danger" onclick="JE.confirmDelete(${e.idJournalEntry}, '${escapeHtml(e.entryNumber)}')" title="Eliminar">
                                    <i class="bi bi-trash"></i>
                                </button>
                            ` : ''}
                            ${e.status === 'Posted' && !e.isReversing ? `
                                <button class="btn btn-danger" onclick="JE.openReverse(${e.idJournalEntry})" title="Revertir">
                                    <i class="bi bi-arrow-counterclockwise"></i>
                                </button>
                            ` : ''}
                        </div>
                    </td>
                </tr>
            `;
        }).join('');
    }

    // ===== CRUD - NUEVO =====

    async function openNew() {
        currentEntry = null;
        currentLines = [];
        editingLineIndex = -1;

        document.getElementById('idJournalEntry').value = '0';
        document.getElementById('modalTitle').innerHTML = '<i class="bi bi-plus-lg me-2"></i>Nuevo Asiento de Diario';

        // ⭐ Entry Number se genera automáticamente en el backend usando consecutivos jerárquicos
        // El número se asigna al momento de guardar según el menú actual (CURRENT_MENU_ID)
        document.getElementById('entryNumber').value = '[Se generará automáticamente]';
        document.getElementById('entryNumber').style.fontStyle = 'italic';
        document.getElementById('entryNumber').style.color = '#9ca3af';

        const today = new Date();
        const period = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`;
        document.getElementById('period').value = period;
        document.getElementById('fiscalYear').value = today.getFullYear();

        // Resetear campos
        document.getElementById('entryType').value = 'Manual';
        document.getElementById('description').value = '';
        document.getElementById('reference').value = '';
        document.getElementById('currencyCode').value = 'CRC';
        document.getElementById('exchangeRate').value = '1.00';
        document.getElementById('requiresApproval').checked = false;
        document.getElementById('sourceModule').value = '';
        document.getElementById('sourceDocumentType').value = '';
        document.getElementById('sourceDocumentId').value = '';
        document.getElementById('sourceDocumentNumber').value = '';

        renderLines();
        updateTotals();

        const modal = new bootstrap.Modal(document.getElementById('journalEntryModal'));
        modal.show();

        // Activar primera tab
        document.querySelector('[href="#tabHeader"]').click();
    }

    // ===== CRUD - EDITAR =====

    async function openEdit(id) {
        try {
            const response = await fetch(`${JE_API}/${id}`, {
                headers: { 'Authorization': `Bearer ${JE_TOKEN}` }
            });

            if (!response.ok) throw new Error('Error loading entry');
            currentEntry = await response.json();
            currentLines = currentEntry.lines || [];

            document.getElementById('idJournalEntry').value = currentEntry.idJournalEntry;
            document.getElementById('modalTitle').innerHTML = `<i class="bi bi-pencil me-2"></i>Editar Asiento: ${escapeHtml(currentEntry.entryNumber)}`;

            // Restaurar estilo normal del entry_number cuando se edita un asiento existente
            const entryNumberInput = document.getElementById('entryNumber');
            entryNumberInput.value = currentEntry.entryNumber;
            entryNumberInput.style.fontStyle = 'normal';
            entryNumberInput.style.color = '';

            document.getElementById('entryType').value = currentEntry.entryType || 'Manual';
            document.getElementById('entryDate').value = currentEntry.entryDate || '';
            document.getElementById('postingDate').value = currentEntry.postingDate || '';
            document.getElementById('description').value = currentEntry.description || '';
            document.getElementById('reference').value = currentEntry.reference || '';
            document.getElementById('period').value = currentEntry.period || '';
            document.getElementById('fiscalYear').value = currentEntry.fiscalYear || new Date().getFullYear();
            document.getElementById('currencyCode').value = currentEntry.currencyCode || 'CRC';
            document.getElementById('exchangeRate').value = currentEntry.exchangeRate || 1.0;
            document.getElementById('requiresApproval').checked = currentEntry.requiresApproval || false;
            document.getElementById('sourceModule').value = currentEntry.sourceModule || '';
            document.getElementById('sourceDocumentType').value = currentEntry.sourceDocumentType || '';
            document.getElementById('sourceDocumentId').value = currentEntry.sourceDocumentId || '';
            document.getElementById('sourceDocumentNumber').value = currentEntry.sourceDocumentNumber || '';

            renderLines();
            updateTotals();

            const modal = new bootstrap.Modal(document.getElementById('journalEntryModal'));
            modal.show();
        } catch (error) {
            console.error('Error loading entry:', error);
            showToast('Error al cargar el asiento', 'error');
        }
    }

    // ===== CRUD - VER =====

    async function view(id) {
        await openEdit(id);
        // Deshabilitar campos si es necesario
    }

    // ===== CRUD - GUARDAR =====

    async function save(andPost = false) {
        try {
            const id = parseInt(document.getElementById('idJournalEntry').value);

            const data = {
                idJournalEntry: id,
                idMenu: CURRENT_MENU_ID, // ⭐ ID del menú actual para consecutivo jerárquico
                entryNumber: document.getElementById('entryNumber').value,
                entryType: document.getElementById('entryType').value,
                description: document.getElementById('description').value,
                reference: document.getElementById('reference').value,
                entryDate: document.getElementById('entryDate').value,
                postingDate: document.getElementById('postingDate').value,
                period: document.getElementById('period').value,
                fiscalYear: parseInt(document.getElementById('fiscalYear').value),
                currencyCode: document.getElementById('currencyCode').value,
                exchangeRate: parseFloat(document.getElementById('exchangeRate').value),
                requiresApproval: document.getElementById('requiresApproval').checked,
                sourceModule: document.getElementById('sourceModule').value || null,
                sourceDocumentType: document.getElementById('sourceDocumentType').value || null,
                sourceDocumentId: document.getElementById('sourceDocumentId').value ? parseInt(document.getElementById('sourceDocumentId').value) : null,
                sourceDocumentNumber: document.getElementById('sourceDocumentNumber').value || null,
                lines: currentLines
            };

            // Validar
            if (!data.description) {
                showToast('La descripción es requerida', 'error');
                return;
            }

            if (!data.lines || data.lines.length < 2) {
                showToast('El asiento debe tener al menos 2 líneas', 'error');
                return;
            }

            // Validar cuadre
            const totalDebit = data.lines.reduce((sum, l) => sum + l.debitAmount, 0);
            const totalCredit = data.lines.reduce((sum, l) => sum + l.creditAmount, 0);
            const diff = Math.abs(totalDebit - totalCredit);
            if (diff > 0.01) {
                showToast(`El asiento no está cuadrado. Diferencia: ${diff.toFixed(2)}`, 'error');
                return;
            }

            const url = id > 0 ? `${JE_API}/${id}` : JE_API;
            const method = id > 0 ? 'PUT' : 'POST';

            const response = await fetch(url, {
                method: method,
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${JE_TOKEN}`
                },
                body: JSON.stringify(data)
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Error saving entry');
            }

            const saved = await response.json();

            // Si se solicitó contabilizar
            if (andPost) {
                await postEntry(saved.idJournalEntry);
            }

            showToast(`Asiento ${id > 0 ? 'actualizado' : 'creado'} correctamente`, 'success');
            bootstrap.Modal.getInstance(document.getElementById('journalEntryModal')).hide();
            load();
        } catch (error) {
            console.error('Error saving entry:', error);
            showToast(error.message || 'Error al guardar el asiento', 'error');
        }
    }

    async function saveAndPost() {
        await save(true);
    }

    // ===== CRUD - ELIMINAR =====

    function confirmDelete(id, entryNumber) {
        if (!confirm(`¿Está seguro que desea eliminar el asiento "${entryNumber}"?\n\nEsta acción no se puede deshacer.`)) {
            return;
        }
        deleteEntry(id);
    }

    async function deleteEntry(id) {
        try {
            const response = await fetch(`${JE_API}/${id}`, {
                method: 'DELETE',
                headers: { 'Authorization': `Bearer ${JE_TOKEN}` }
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Error deleting entry');
            }

            showToast('Asiento eliminado correctamente', 'success');
            load();
        } catch (error) {
            console.error('Error deleting entry:', error);
            showToast(error.message || 'Error al eliminar el asiento', 'error');
        }
    }

    // ===== OPERACIONES CONTABLES =====

    function confirmPost(id, entryNumber) {
        if (!confirm(`¿Está seguro que desea CONTABILIZAR el asiento "${entryNumber}"?\n\nUna vez contabilizado no podrá editarse.`)) {
            return;
        }
        postEntry(id);
    }

    async function postEntry(id) {
        try {
            const response = await fetch(`${JE_API}/${id}/post`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${JE_TOKEN}` }
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Error posting entry');
            }

            showToast('Asiento contabilizado correctamente', 'success');
            load();
        } catch (error) {
            console.error('Error posting entry:', error);
            showToast(error.message || 'Error al contabilizar el asiento', 'error');
        }
    }

    function openReverse(id) {
        const reversalDate = prompt('Ingrese la fecha de reversión (YYYY-MM-DD):');
        if (!reversalDate) return;

        const reason = prompt('Ingrese el motivo de la reversión:');
        if (!reason) return;

        reverseEntry(id, reversalDate, reason);
    }

    async function reverseEntry(id, reversalDate, reason) {
        try {
            const response = await fetch(`${JE_API}/${id}/reverse`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${JE_TOKEN}`
                },
                body: JSON.stringify({ 
                    reversalDate, 
                    idCancelReason: 1, // Por ahora usar razón por defecto - TODO: obtener de un selector
                    idMenu: CURRENT_MENU_ID // ⭐ Enviar el menú actual para resolver consecutivo
                })
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Error reversing entry');
            }

            showToast('Asiento revertido correctamente', 'success');
            load();
        } catch (error) {
            console.error('Error reversing entry:', error);
            showToast(error.message || 'Error al revertir el asiento', 'error');
        }
    }

    // ===== LÍNEAS - AGREGAR =====

    function addLine() {
        editingLineIndex = -1;
        document.getElementById('lineIndex').value = '-1';

        document.getElementById('lineAccount').value = '';
        document.getElementById('lineDescription').value = '';
        document.getElementById('lineDebit').value = '0';
        document.getElementById('lineCredit').value = '0';
        document.getElementById('lineCostCenter').value = '';
        document.getElementById('lineReference').value = '';

        const modal = new bootstrap.Modal(document.getElementById('lineModal'));
        modal.show();
    }

    // ===== LÍNEAS - EDITAR =====

    function editLine(index) {
        const line = currentLines[index];
        if (!line) return;

        editingLineIndex = index;
        document.getElementById('lineIndex').value = index;

        document.getElementById('lineAccount').value = line.idChartOfAccounts;
        document.getElementById('lineDescription').value = line.lineDescription || '';
        document.getElementById('lineDebit').value = line.debitAmount || 0;
        document.getElementById('lineCredit').value = line.creditAmount || 0;
        document.getElementById('lineCostCenter').value = line.costCenterCode || '';
        document.getElementById('lineReference').value = line.reference || '';

        const modal = new bootstrap.Modal(document.getElementById('lineModal'));
        modal.show();
    }

    // ===== LÍNEAS - GUARDAR =====

    function saveLine() {
        const accountId = parseInt(document.getElementById('lineAccount').value);
        const description = document.getElementById('lineDescription').value;
        const debit = parseFloat(document.getElementById('lineDebit').value) || 0;
        const credit = parseFloat(document.getElementById('lineCredit').value) || 0;
        const ccCode = document.getElementById('lineCostCenter').value;
        const reference = document.getElementById('lineReference').value;

        if (!accountId) {
            showToast('Debe seleccionar una cuenta', 'error');
            return;
        }

        if (!description) {
            showToast('La descripción es requerida', 'error');
            return;
        }

        if (debit === 0 && credit === 0) {
            showToast('Debe ingresar débito o crédito', 'error');
            return;
        }

        if (debit > 0 && credit > 0) {
            showToast('No puede ingresar débito y crédito simultáneamente', 'error');
            return;
        }

        // Obtener datos de cuenta
        const accountOpt = document.querySelector(`#lineAccount option[value="${accountId}"]`);
        const accountCode = accountOpt?.dataset.code || '';
        const accountName = accountOpt?.dataset.name || '';

        // Obtener datos de centro de costo
        let ccName = '';
        if (ccCode) {
            const ccOpt = document.querySelector(`#lineCostCenter option[value="${ccCode}"]`);
            ccName = ccOpt?.dataset.name || '';
        }

        const line = {
            idJournalEntry: 0,
            idJournalEntryLine: 0,
            idChartOfAccounts: accountId,
            accountCode: accountCode,
            accountName: accountName,
            lineDescription: description,
            debitAmount: debit,
            creditAmount: credit,
            currencyCode: document.getElementById('currencyCode').value || 'CRC',
            exchangeRate: parseFloat(document.getElementById('exchangeRate').value) || 1.0,
            costCenterCode: ccCode || null,
            costCenterName: ccName || null,
            reference: reference || null
        };

        const index = parseInt(document.getElementById('lineIndex').value);
        if (index >= 0) {
            currentLines[index] = line;
        } else {
            currentLines.push(line);
        }

        renderLines();
        updateTotals();

        bootstrap.Modal.getInstance(document.getElementById('lineModal')).hide();
        showToast('Línea guardada', 'success');
    }

    // ===== LÍNEAS - ELIMINAR =====

    function deleteLine(index) {
        if (!confirm('¿Eliminar esta línea?')) return;
        currentLines.splice(index, 1);
        renderLines();
        updateTotals();
    }

    // ===== LÍNEAS - RENDERIZAR =====

    function renderLines() {
        const tbody = document.getElementById('linesTable');
        if (!tbody) return;

        document.getElementById('linesCount').textContent = currentLines.length;

        if (!currentLines || currentLines.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="7" class="text-center text-muted py-4">
                        <i class="bi bi-inbox display-4 d-block mb-2"></i>
                        No hay líneas. Haz clic en "Agregar Línea" para comenzar.
                    </td>
                </tr>`;
            return;
        }

        tbody.innerHTML = currentLines.map((line, index) => `
            <tr class="line-row" ondblclick="JE.editLine(${index})">
                <td class="text-muted">${index + 1}</td>
                <td>
                    <strong>${escapeHtml(line.accountCode || '')}</strong><br>
                    <small class="text-muted">${escapeHtml(line.accountName || '')}</small>
                </td>
                <td>${escapeHtml(line.lineDescription || '')}</td>
                <td class="text-end debit-cell">
                    ${line.debitAmount > 0 ? formatCurrency(line.debitAmount) : '-'}
                </td>
                <td class="text-end credit-cell">
                    ${line.creditAmount > 0 ? formatCurrency(line.creditAmount) : '-'}
                </td>
                <td>
                    ${line.costCenterCode ? `<small>${escapeHtml(line.costCenterCode)} - ${escapeHtml(line.costCenterName || '')}</small>` : '-'}
                </td>
                <td>
                    <div class="btn-group btn-group-sm">
                        <button class="btn btn-warning btn-sm" onclick="JE.editLine(${index})" title="Editar">
                            <i class="bi bi-pencil"></i>
                        </button>
                        <button class="btn btn-danger btn-sm" onclick="JE.deleteLine(${index})" title="Eliminar">
                            <i class="bi bi-trash"></i>
                        </button>
                    </div>
                </td>
            </tr>
        `).join('');
    }

    // ===== LÍNEAS - TOTALES =====

    function updateTotals() {
        const totalDebit = currentLines.reduce((sum, l) => sum + (l.debitAmount || 0), 0);
        const totalCredit = currentLines.reduce((sum, l) => sum + (l.creditAmount || 0), 0);
        const difference = Math.abs(totalDebit - totalCredit);

        document.getElementById('totalDebit').textContent = formatCurrency(totalDebit);
        document.getElementById('totalCredit').textContent = formatCurrency(totalCredit);

        const diffEl = document.getElementById('difference');
        const statusEl = document.getElementById('balanceStatus');

        if (difference < 0.01) {
            diffEl.textContent = '0.00';
            diffEl.className = 'balance-indicator balance-ok';
            statusEl.textContent = 'Cuadrado ✓';
            statusEl.className = 'text-muted';
        } else {
            diffEl.textContent = formatCurrency(difference);
            diffEl.className = 'balance-indicator balance-error';
            statusEl.textContent = 'Descuadrado ✗';
            statusEl.className = 'text-danger';
        }
    }

    // ===== UTILIDADES =====

    function formatCurrency(value) {
        if (value === null || value === undefined) return '0.00';
        return parseFloat(value).toLocaleString('es-CR', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function formatDate(dateStr) {
        if (!dateStr) return '';
        const date = new Date(dateStr + 'T00:00:00');
        return date.toLocaleDateString('es-CR', { year: 'numeric', month: '2-digit', day: '2-digit' });
    }

    function escapeHtml(text) {
        if (!text) return '';
        const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
        return text.toString().replace(/[&<>"']/g, m => map[m]);
    }

    function getTypeLabel(type) {
        const types = {
            'Manual': 'Manual',
            'Automatic': 'Automático',
            'Reversal': 'Reversión',
            'Adjustment': 'Ajuste',
            'Closing': 'Cierre',
            'Opening': 'Apertura'
        };
        return types[type] || type;
    }

    function getStatusLabel(status) {
        const statuses = {
            'Draft': 'Borrador',
            'Posted': 'Contabilizado',
            'Reversed': 'Revertido',
            'Cancelled': 'Cancelado'
        };
        return statuses[status] || status;
    }

    function showToast(message, type = 'info') {
        console.log(`[${type.toUpperCase()}] ${message}`);
        alert(message);
    }

    // ===== API PÚBLICA =====

    return {
        init,
        load,
        applyFilters,
        clearFilters,
        openNew,
        openEdit,
        view,
        save,
        saveAndPost,
        confirmDelete,
        confirmPost,
        openReverse,
        addLine,
        editLine,
        saveLine,
        deleteLine
    };

})();

// Auto-inicializar
document.addEventListener('DOMContentLoaded', () => JE.init());
