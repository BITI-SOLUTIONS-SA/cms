// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/exchangeRates.js
// PROPÓSITO: Lógica cliente para mantenimiento de Tipos de Tasa de Cambio
// TABLA:     {company_schema}.exchange_rate
// AUTOR:     BITI SOLUTIONS S.A
// CREADO:    2026-06-28
// ================================================================================

'use strict';

const ER = (() => {

    // ── Estado interno ───────────────────────────────────────────────────────────
    let _items       = [];
    let _deleteId    = null;
    let _modal       = null;
    let _deleteModal = null;

    // ── Bootstrap modal helpers ──────────────────────────────────────────────────
    function getModal()       { return _modal       ??= new bootstrap.Modal(document.getElementById('erModal')); }
    function getDeleteModal() { return _deleteModal ??= new bootstrap.Modal(document.getElementById('erDeleteModal')); }

    // ── Helpers de alerta ────────────────────────────────────────────────────────
    function showAlert(msg, type = 'danger') {
        const el = document.getElementById('erAlert');
        if (!el) return;
        el.className = `alert alert-${type}`;
        el.textContent = msg;
        el.classList.remove('d-none');
        setTimeout(() => el.classList.add('d-none'), 5000);
    }

    function hideAlert() {
        const el = document.getElementById('erAlert');
        if (el) el.classList.add('d-none');
    }

    // ── Fetch helper ─────────────────────────────────────────────────────────────
    async function apiFetch(path, method = 'GET', body = null) {
        const opts = {
            method,
            headers: {
                'Content-Type':  'application/json',
                'Authorization': `Bearer ${window.ER_TOKEN}`
            }
        };
        if (body !== null) opts.body = JSON.stringify(body);

        const res = await fetch(`${window.ER_API}${path}`, opts);

        if (!res.ok) {
            let errMsg = `Error ${res.status}`;
            try {
                const json = await res.json();
                errMsg = json.message ?? errMsg;
            } catch { /* respuesta no JSON */ }
            throw new Error(errMsg);
        }

        if (res.status === 204) return null;
        return res.json();
    }

    // ── Render tabla ─────────────────────────────────────────────────────────────
    function render() {
        const tbody = document.getElementById('bodyExchangeRates');
        if (!tbody) return;

        if (!_items.length) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-center text-muted py-4">
                        <i class="bi bi-inbox me-2"></i>No hay tipos de tasa de cambio registrados
                    </td>
                </tr>`;
            return;
        }

        tbody.innerHTML = _items.map((item, idx) => `
            <tr>
                <td class="text-muted small">${idx + 1}</td>
                <td><span class="er-code-badge">${escHtml(item.code)}</span></td>
                <td class="text-light small">${escHtml(item.description ?? '—')}</td>
                <td class="text-center text-light small">${item.displayOrder}</td>
                <td class="text-center">
                    ${item.isActive
                        ? '<span class="badge bg-success text-white">Activo</span>'
                        : '<span class="badge bg-danger text-white">Inactivo</span>'}
                </td>
                <td>
                    <div class="d-flex gap-1 justify-content-end">
                        <button class="btn btn-sm btn-outline-info border-0"
                                onclick="ER.openEdit(${item.idExchangeRate})"
                                title="Editar">
                            <i class="bi bi-pencil"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-danger border-0"
                                onclick="ER.openDelete(${item.idExchangeRate}, '${escHtml(item.code)}')"
                                title="Eliminar">
                            <i class="bi bi-trash"></i>
                        </button>
                    </div>
                </td>
            </tr>`).join('');
    }

    function escHtml(str) {
        if (str == null) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // ── Cargar lista ─────────────────────────────────────────────────────────────
    async function load() {
        const tbody = document.getElementById('bodyExchangeRates');
        if (tbody) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-center text-muted py-3">
                        <i class="bi bi-hourglass-split me-1"></i>Cargando…
                    </td>
                </tr>`;
        }
        try {
            _items = await apiFetch('/api/ExchangeRate');
            render();
        } catch (err) {
            showAlert(`Error al cargar: ${err.message}`);
            if (tbody) {
                tbody.innerHTML = `
                    <tr>
                        <td colspan="6" class="text-center text-danger py-3">
                            <i class="bi bi-exclamation-circle me-1"></i>${escHtml(err.message)}
                        </td>
                    </tr>`;
            }
        }
    }

    // ── Abrir modal NUEVO ────────────────────────────────────────────────────────
    function openNew() {
        hideAlert();
        document.getElementById('erModalTitle').innerHTML =
            '<i class="bi bi-currency-exchange me-2 text-info"></i>Nuevo Tipo de Tasa de Cambio';
        document.getElementById('erId').value          = '';
        document.getElementById('erCode').value        = '';
        document.getElementById('erDescription').value = '';
        document.getElementById('erDisplayOrder').value = '0';
        document.getElementById('erActive').checked    = true;
        document.getElementById('erCode').disabled     = false;
        getModal().show();
    }

    // ── Abrir modal EDITAR ───────────────────────────────────────────────────────
    async function openEdit(id) {
        hideAlert();
        try {
            const item = await apiFetch(`/api/ExchangeRate/${id}`);
            document.getElementById('erModalTitle').innerHTML =
                '<i class="bi bi-pencil me-2 text-warning"></i>Editar Tipo de Tasa de Cambio';
            document.getElementById('erId').value           = item.idExchangeRate;
            document.getElementById('erCode').value         = item.code;
            document.getElementById('erDescription').value  = item.description ?? '';
            document.getElementById('erDisplayOrder').value = item.displayOrder;
            document.getElementById('erActive').checked     = item.isActive;
            document.getElementById('erCode').disabled      = false;
            getModal().show();
        } catch (err) {
            showAlert(`Error al cargar el registro: ${err.message}`);
        }
    }

    // ── Guardar (crear o actualizar) ─────────────────────────────────────────────
    async function save() {
        const id          = parseInt(document.getElementById('erId').value || '0');
        const code        = document.getElementById('erCode').value.trim().toUpperCase();
        const description = document.getElementById('erDescription').value.trim() || null;
        const displayOrder = parseInt(document.getElementById('erDisplayOrder').value || '0');
        const isActive    = document.getElementById('erActive').checked;

        if (!code) {
            showAlert('El código es requerido.');
            return;
        }

        const dto = { idExchangeRate: id, code, description, isActive, displayOrder };

        try {
            if (id === 0) {
                await apiFetch('/api/ExchangeRate', 'POST', dto);
                showAlert('Tipo de tasa de cambio creado correctamente.', 'success');
            } else {
                await apiFetch(`/api/ExchangeRate/${id}`, 'PUT', dto);
                showAlert('Tipo de tasa de cambio actualizado correctamente.', 'success');
            }
            getModal().hide();
            await load();
        } catch (err) {
            showAlert(`Error al guardar: ${err.message}`);
        }
    }

    // ── Abrir modal ELIMINAR ─────────────────────────────────────────────────────
    function openDelete(id, code) {
        _deleteId = id;
        document.getElementById('erDeleteName').textContent = code;
        getDeleteModal().show();
    }

    // ── Confirmar eliminación ────────────────────────────────────────────────────
    async function confirmDelete() {
        if (!_deleteId) return;
        try {
            await apiFetch(`/api/ExchangeRate/${_deleteId}`, 'DELETE');
            showAlert('Tipo de tasa de cambio eliminado.', 'success');
            getDeleteModal().hide();
            _deleteId = null;
            await load();
        } catch (err) {
            showAlert(`Error al eliminar: ${err.message}`);
            getDeleteModal().hide();
        }
    }

    // ── Init ─────────────────────────────────────────────────────────────────────
    document.addEventListener('DOMContentLoaded', () => {
        // Forzar código en mayúsculas al escribir
        const codeInput = document.getElementById('erCode');
        if (codeInput) {
            codeInput.addEventListener('input', function () {
                this.value = this.value.toUpperCase();
            });
        }
        load();
    });

    // ── API pública ──────────────────────────────────────────────────────────────
    return { openNew, openEdit, openDelete, confirmDelete, save, load };

})();
