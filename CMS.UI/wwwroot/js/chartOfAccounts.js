// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/chartOfAccounts.js
// PROPÓSITO: Lógica cliente para mantenimiento del Plan de Cuentas
// TABLA:     {company_schema}.chart_of_accounts  (BD de compañía)
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-20
// ================================================================================

'use strict';

const COA = (() => {
    const API   = () => window.COA_API   || '';
    const TOKEN = () => window.COA_TOKEN || '';

    let _currentPage = 1;
    let _pageSize    = 50;
    let _totalCount  = 0;
    let _filters     = {};
    let _accountTypes = []; // catálogo admin.chart_of_accounts_type

    // ============================================================
    // FETCH HELPER
    // ============================================================

    async function coaFetch(path, options = {}) {
        const url = `${API()}${path}`;
        const opts = {
            headers: {
                'Content-Type':  'application/json',
                'Authorization': `Bearer ${TOKEN()}`,
            },
            ...options,
        };
        if (opts.body && typeof opts.body !== 'string') opts.body = JSON.stringify(opts.body);
        const res = await fetch(url, opts);
        if (!res.ok) {
            let msg = `HTTP ${res.status}`;
            try { const d = await res.json(); msg = d.message || d.error || d.title || msg; } catch {}
            throw new Error(msg);
        }
        if (res.status === 204) return null;
        return res.json();
    }

    // ============================================================
    // ALERT
    // ============================================================

    function showAlert(msg, type = 'success') {
        const el = document.getElementById('coaAlert');
        if (!el) return;
        el.className = `alert alert-${type}`;
        el.innerHTML = `<i class="bi bi-${type === 'success' ? 'check-circle' : 'exclamation-triangle'} me-2"></i>${msg}`;
        el.classList.remove('d-none');
        setTimeout(() => el.classList.add('d-none'), 5000);
    }

    // ============================================================
    // CARGAR Y RENDERIZAR
    // ============================================================

    async function load(page = 1) {
        _currentPage = page;
        const tbody = document.getElementById('bodyAccounts');
        tbody.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-3"><i class="bi bi-hourglass-split me-1"></i>Cargando…</td></tr>';

        try {
            const params = new URLSearchParams({
                page: _currentPage,
                pageSize: _pageSize,
                ..._filters
            });

            const data = await coaFetch(`/api/chart-of-accounts?${params}`);
            _totalCount = data.totalCount || 0;
            renderTable(data.items || []);
            renderPagination(data.totalPages || 1);
            document.getElementById('lblTotal').textContent = `Total: ${_totalCount} cuenta${_totalCount !== 1 ? 's' : ''}`;
        } catch (e) {
            tbody.innerHTML = `<tr><td colspan="8" class="text-center text-danger py-3">${e.message}</td></tr>`;
        }
    }

    function renderTable(items) {
        const tbody = document.getElementById('bodyAccounts');
        if (!items || !items.length) {
            tbody.innerHTML = '<tr><td colspan="8" class="text-center text-light py-3">No hay cuentas registradas.</td></tr>';
            return;
        }
        tbody.innerHTML = items.map((a, i) => {
            const levelClass = `account-level-${Math.min(a.accountLevel || 1, 6)}`;
            const typeBadge = getAccountTypeBadge(a.idChartOfAccountsType);
            const statusBadge = a.isActive
                ? '<span class="badge bg-success">Activa</span>'
                : '<span class="badge bg-secondary">Inactiva</span>';
            const blockIcon = a.isBlocked
                ? '<i class="bi bi-lock-fill text-warning ms-1" title="Bloqueada"></i>'
                : '';

            return `
                <tr>
                    <td class="text-light" style="font-size:.8rem;">${(_currentPage - 1) * _pageSize + i + 1}</td>
                    <td>
                        <code style="color:#7dd3fc;font-size:.85rem;">${escapeHtml(a.code)}</code>
                    </td>
                    <td class="${levelClass} text-light">
                        ${a.isHeader ? '<i class="bi bi-folder me-1 text-warning"></i>' : ''}
                        ${escapeHtml(a.name)}
                        ${blockIcon}
                    </td>
                    <td>
                        ${typeBadge}
                    </td>
                    <td class="text-center text-light" style="font-size:.85rem;">${a.accountLevel}</td>
                    <td class="text-center">
                        ${a.isDetail
                            ? '<i class="bi bi-check-circle-fill text-success" title="Cuenta de detalle"></i>'
                            : '<i class="bi bi-dash text-muted" title="Cuenta de encabezado"></i>'}
                    </td>
                    <td class="text-center">
                        ${statusBadge}
                    </td>
                    <td class="text-end">
                        <button class="btn btn-sm btn-outline-info me-1"
                                onclick="COA.openView(${a.idChartOfAccounts})" title="Ver">
                            <i class="bi bi-eye"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-warning me-1"
                                onclick="COA.openEdit(${a.idChartOfAccounts})" title="Editar">
                            <i class="bi bi-pencil"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-danger"
                                onclick="COA.confirmDelete(${a.idChartOfAccounts}, '${escapeHtml(a.code)}')" title="Eliminar">
                            <i class="bi bi-trash"></i>
                        </button>
                    </td>
                </tr>`;
        }).join('');
    }

    function getAccountTypeBadge(idChartOfAccountsType) {
        const colors = [undefined, 'primary', 'danger', 'warning', 'success', 'info', 'secondary'];
        const t = _accountTypes.find(x => x.id === idChartOfAccountsType);
        if (!t) return '<span class="badge badge-account-type bg-dark">—</span>';
        const color = colors[idChartOfAccountsType] || 'secondary';
        return `<span class="badge badge-account-type bg-${color}">${t.description}</span>`;
    }

    function renderPagination(totalPages) {
        const nav = document.getElementById('pagination');
        if (totalPages <= 1) {
            nav.innerHTML = '';
            return;
        }

        let html = '<ul class="pagination pagination-sm mb-0">';

        // Previous
        html += `<li class="page-item ${_currentPage === 1 ? 'disabled' : ''}">
                    <a class="page-link" href="#" onclick="COA.load(${_currentPage - 1}); return false;">&laquo;</a>
                 </li>`;

        // Pages
        const maxVisible = 5;
        let start = Math.max(1, _currentPage - Math.floor(maxVisible / 2));
        let end = Math.min(totalPages, start + maxVisible - 1);
        if (end - start + 1 < maxVisible) start = Math.max(1, end - maxVisible + 1);

        for (let p = start; p <= end; p++) {
            html += `<li class="page-item ${p === _currentPage ? 'active' : ''}">
                        <a class="page-link" href="#" onclick="COA.load(${p}); return false;">${p}</a>
                     </li>`;
        }

        // Next
        html += `<li class="page-item ${_currentPage === totalPages ? 'disabled' : ''}">
                    <a class="page-link" href="#" onclick="COA.load(${_currentPage + 1}); return false;">&raquo;</a>
                 </li>`;
        html += '</ul>';

        nav.innerHTML = html;
    }

    // ============================================================
    // FILTROS
    // ============================================================

    function applyFilters() {
        _filters = {};

        const search = document.getElementById('txtSearch')?.value?.trim();
        if (search) _filters.search = search;

        const accountType = document.getElementById('selAccountType')?.value;
        if (accountType) _filters.idChartOfAccountsType = accountType;

        const isDetail = document.getElementById('selIsDetail')?.value;
        if (isDetail) _filters.isDetail = isDetail;

        const isActive = document.getElementById('selIsActive')?.value;
        if (isActive) _filters.isActive = isActive;

        load(1);
    }

    function clearFilters() {
        document.getElementById('txtSearch').value = '';
        document.getElementById('selAccountType').value = '';
        document.getElementById('selIsDetail').value = '';
        document.getElementById('selIsActive').value = 'true';
        _filters = {};
        load(1);
    }

    // ============================================================
    // MODAL NUEVO
    // ============================================================

    async function openNew() {
        document.getElementById('coaModalTitle').innerHTML =
            '<i class="bi bi-plus-circle me-2 text-success"></i>Nueva Cuenta Contable';
        clearForm();
        await loadParentAccounts();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('coaModal')).show();
    }

    // ============================================================
    // MODAL VER
    // ============================================================

    async function openView(id) {
        try {
            const account = await coaFetch(`/api/chart-of-accounts/${id}`);
            const t = _accountTypes.find(x => x.id === account.idChartOfAccountsType);
            const typeName = t ? t.description : account.idChartOfAccountsType;
            alert(`Cuenta: ${account.code} - ${account.name}\n\nTipo: ${typeName}\nNivel: ${account.accountLevel}\nDetalle: ${account.isDetail ? 'Sí' : 'No'}\nActiva: ${account.isActive ? 'Sí' : 'No'}`);
        } catch (e) {
            showAlert(e.message, 'danger');
        }
    }

    // ============================================================
    // MODAL EDITAR
    // ============================================================

    async function openEdit(id) {
        try {
            const account = await coaFetch(`/api/chart-of-accounts/${id}`);
            document.getElementById('coaModalTitle').innerHTML =
                `<i class="bi bi-pencil me-2 text-warning"></i>Editar Cuenta — ${account.code}`;

            // General
            document.getElementById('coaId').value = account.idChartOfAccounts;
            document.getElementById('coaCode').value = account.code;
            document.getElementById('coaName').value = account.name;
            document.getElementById('coaDescription').value = account.description || '';
            document.getElementById('coaAlias').value = account.alias || '';
            await loadParentAccounts(account.idChartOfAccounts);
            document.getElementById('coaParent').value = account.idParentAccount || '';
            document.getElementById('coaLevel').value = account.accountLevel;
            document.getElementById('coaAccountType').value = account.idChartOfAccountsType || '';
            document.getElementById('coaIsHeader').checked = account.isHeader;
            document.getElementById('coaIsDetail').checked = account.isDetail;

            // Clasificación
            document.getElementById('coaAccountClass').value = account.accountClass || '';
            document.getElementById('coaNormalBalance').value = account.normalBalance;
            document.getElementById('coaCashFlowCategory').value = account.cashFlowCategory || '';
            document.getElementById('coaCurrencyCode').value = account.currencyCode;
            document.getElementById('coaMultiCurrency').checked = account.allowsMultiCurrency;
            document.getElementById('coaIsReceivable').checked = account.isReceivable;
            document.getElementById('coaIsPayable').checked = account.isPayable;
            document.getElementById('coaIsReconciliation').checked = account.isReconciliation;

            // Controles
            document.getElementById('coaAcceptsManualEntry').checked = account.acceptsManualEntry;
            document.getElementById('coaAcceptsAutoEntry').checked = account.acceptsAutoEntry;
            document.getElementById('coaRequiresCostCenter').checked = account.requiresCostCenter;
            document.getElementById('coaRequiresProject').checked = account.requiresProject;
            document.getElementById('coaRequiresPartner').checked = account.requiresPartner;
            document.getElementById('coaTaxCode').value = account.taxCode || '';
            document.getElementById('coaIsTaxRelevant').checked = account.isTaxRelevant;
            document.getElementById('coaIsActive').checked = account.isActive;
            document.getElementById('coaIsBlocked').checked = account.isBlocked;
            document.getElementById('coaBlockReason').value = account.blockReason || '';
            document.getElementById('divBlockReason').style.display = account.isBlocked ? 'block' : 'none';

            // Reportes
            document.getElementById('coaFinancialStatement').value = account.financialStatement || '';
            document.getElementById('coaSortOrder').value = account.sortOrder;
            document.getElementById('coaReportLineItem').value = account.reportLineItem || '';
            document.getElementById('coaEffectiveDate').value = account.effectiveDate ? formatDateForInput(account.effectiveDate) : '';
            document.getElementById('coaExpirationDate').value = account.expirationDate ? formatDateForInput(account.expirationDate) : '';
            document.getElementById('coaNotes').value = account.notes || '';

            bootstrap.Modal.getOrCreateInstance(document.getElementById('coaModal')).show();
        } catch (e) {
            showAlert(e.message, 'danger');
        }
    }

    // ============================================================
    // GUARDAR (crear o actualizar)
    // ============================================================

    async function save() {
        const id   = document.getElementById('coaId').value;
        const code = document.getElementById('coaCode').value.trim();
        const name = document.getElementById('coaName').value.trim();
        const type = document.getElementById('coaAccountType').value;

        if (!code) { showAlert('El código de cuenta es obligatorio.', 'danger'); return; }
        if (!name) { showAlert('El nombre de cuenta es obligatorio.', 'danger'); return; }
        if (!type) { showAlert('El tipo de cuenta es obligatorio.', 'danger'); return; }

        const isHeader = document.getElementById('coaIsHeader').checked;
        const isDetail = document.getElementById('coaIsDetail').checked;

        if (!isHeader && !isDetail) {
            showAlert('La cuenta debe ser de encabezado o de detalle.', 'danger');
            return;
        }
        if (isHeader && isDetail) {
            showAlert('La cuenta no puede ser de encabezado y detalle al mismo tiempo.', 'danger');
            return;
        }

        const payload = {
            code: code,
            name: name,
            description: document.getElementById('coaDescription').value.trim() || null,
            alias: document.getElementById('coaAlias').value.trim() || null,
            idParentAccount: document.getElementById('coaParent').value ? parseInt(document.getElementById('coaParent').value) : null,
            accountLevel: parseInt(document.getElementById('coaLevel').value) || 1,
            isHeader,
            isDetail,
            idChartOfAccountsType: parseInt(type),
            accountClass: document.getElementById('coaAccountClass').value || null,
            normalBalance: document.getElementById('coaNormalBalance').value,
            isDebitBalance: document.getElementById('coaNormalBalance').value === 'Debit',
            acceptsManualEntry: document.getElementById('coaAcceptsManualEntry').checked,
            acceptsAutoEntry: document.getElementById('coaAcceptsAutoEntry').checked,
            requiresCostCenter: document.getElementById('coaRequiresCostCenter').checked,
            requiresProject: document.getElementById('coaRequiresProject').checked,
            requiresPartner: document.getElementById('coaRequiresPartner').checked,
            currencyCode: document.getElementById('coaCurrencyCode').value.trim() || 'CRC',
            allowsMultiCurrency: document.getElementById('coaMultiCurrency').checked,
            isReconciliation: document.getElementById('coaIsReconciliation').checked,
            taxCode: document.getElementById('coaTaxCode').value.trim() || null,
            isTaxRelevant: document.getElementById('coaIsTaxRelevant').checked,
            isReceivable: document.getElementById('coaIsReceivable').checked,
            isPayable: document.getElementById('coaIsPayable').checked,
            cashFlowCategory: document.getElementById('coaCashFlowCategory').value || null,
            financialStatement: document.getElementById('coaFinancialStatement').value || null,
            reportLineItem: document.getElementById('coaReportLineItem').value.trim() || null,
            sortOrder: parseInt(document.getElementById('coaSortOrder').value) || 0,
            effectiveDate: document.getElementById('coaEffectiveDate').value || null,
            expirationDate: document.getElementById('coaExpirationDate').value || null,
            isActive: document.getElementById('coaIsActive').checked,
            isBlocked: document.getElementById('coaIsBlocked').checked,
            blockReason: document.getElementById('coaBlockReason').value.trim() || null,
            notes: document.getElementById('coaNotes').value.trim() || null,
        };

        const isNew  = !id;
        const method = isNew ? 'POST' : 'PUT';
        const path   = isNew ? '/api/chart-of-accounts' : `/api/chart-of-accounts/${id}`;

        try {
            await coaFetch(path, { method, body: payload });
            bootstrap.Modal.getInstance(document.getElementById('coaModal')).hide();
            showAlert(isNew ? 'Cuenta creada correctamente.' : 'Cuenta actualizada correctamente.', 'success');
            load(_currentPage);
        } catch (e) {
            showAlert(e.message, 'danger');
        }
    }

    // ============================================================
    // ELIMINAR
    // ============================================================

    function confirmDelete(id, code) {
        if (!confirm(`¿Está seguro de eliminar la cuenta ${code}?\n\nEsta acción no se puede deshacer y solo se permite si la cuenta no tiene transacciones ni subcuentas.`)) return;
        deleteAccount(id);
    }

    async function deleteAccount(id) {
        try {
            await coaFetch(`/api/chart-of-accounts/${id}`, { method: 'DELETE' });
            showAlert('Cuenta eliminada correctamente.', 'success');
            load(_currentPage);
        } catch (e) {
            showAlert(e.message, 'danger');
        }
    }

    // ============================================================
    // HELPERS
    // ============================================================

    async function loadParentAccounts(excludeId = null) {
        try {
            const data = await coaFetch('/api/chart-of-accounts?isDetail=false&pageSize=1000');
            const select = document.getElementById('coaParent');
            select.innerHTML = '<option value="">Sin padre (cuenta raíz)</option>';

            (data.items || []).forEach(a => {
                if (excludeId && a.idChartOfAccounts === excludeId) return;
                select.innerHTML += `<option value="${a.idChartOfAccounts}">${a.code} - ${a.name}</option>`;
            });
        } catch (e) {
            console.error('Error al cargar cuentas padre:', e);
        }
    }

    function clearForm() {
        document.getElementById('coaId').value = '';
        document.getElementById('coaCode').value = '';
        document.getElementById('coaName').value = '';
        document.getElementById('coaDescription').value = '';
        document.getElementById('coaAlias').value = '';
        document.getElementById('coaParent').value = '';
        document.getElementById('coaLevel').value = '1';
        document.getElementById('coaAccountType').value = '';
        document.getElementById('coaIsHeader').checked = false;
        document.getElementById('coaIsDetail').checked = true;
        document.getElementById('coaAccountClass').value = '';
        document.getElementById('coaNormalBalance').value = 'Debit';
        document.getElementById('coaCashFlowCategory').value = '';
        document.getElementById('coaCurrencyCode').value = 'CRC';
        document.getElementById('coaMultiCurrency').checked = false;
        document.getElementById('coaIsReceivable').checked = false;
        document.getElementById('coaIsPayable').checked = false;
        document.getElementById('coaIsReconciliation').checked = false;
        document.getElementById('coaAcceptsManualEntry').checked = true;
        document.getElementById('coaAcceptsAutoEntry').checked = true;
        document.getElementById('coaRequiresCostCenter').checked = false;
        document.getElementById('coaRequiresProject').checked = false;
        document.getElementById('coaRequiresPartner').checked = false;
        document.getElementById('coaTaxCode').value = '';
        document.getElementById('coaIsTaxRelevant').checked = false;
        document.getElementById('coaIsActive').checked = true;
        document.getElementById('coaIsBlocked').checked = false;
        document.getElementById('coaBlockReason').value = '';
        document.getElementById('divBlockReason').style.display = 'none';
        document.getElementById('coaFinancialStatement').value = '';
        document.getElementById('coaSortOrder').value = '0';
        document.getElementById('coaReportLineItem').value = '';
        document.getElementById('coaEffectiveDate').value = '';
        document.getElementById('coaExpirationDate').value = '';
        document.getElementById('coaNotes').value = '';
    }

    function escapeHtml(text) {
        const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
        return (text || '').replace(/[&<>"']/g, m => map[m]);
    }

    function formatDateForInput(dateStr) {
        // Input: "2025-01-20" (DateOnly from API)
        return dateStr;
    }

    // ============================================================
    // CATÁLOGO DE TIPOS DE CUENTA
    // ============================================================

    async function loadAccountTypes() {
        try {
            const data = await coaFetch('/api/chart-of-accounts-type');
            _accountTypes = (data || []).filter(t => t.isActive);

            const opts = _accountTypes
                .map(t => `<option value="${t.id}">${t.description}</option>`)
                .join('');

            const selFilter = document.getElementById('selAccountType');
            const selModal  = document.getElementById('coaAccountType');
            if (selFilter) selFilter.innerHTML = '<option value="">Todos</option>' + opts;
            if (selModal)  selModal.innerHTML  = '<option value="">Seleccionar...</option>' + opts;
        } catch (e) {
            console.error('Error al cargar tipos de cuenta:', e);
        }
    }

    // ============================================================
    // INIT
    // ============================================================

    async function init() {
        await loadAccountTypes();
        load(1);
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    return {
        init,
        load,
        applyFilters,
        clearFilters,
        openNew,
        openView,
        openEdit,
        save,
        confirmDelete,
    };
})();

// Auto-init
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', COA.init);
} else {
    COA.init();
}
