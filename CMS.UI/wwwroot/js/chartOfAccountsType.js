// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/chartOfAccountsType.js
// PROPÓSITO: Lógica cliente para mantenimiento de admin.chart_of_accounts_type
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

'use strict';

const CAT = (() => {
    const API   = () => window.CAT_API   || '';
    const TOKEN = () => window.CAT_TOKEN || '';

    let _deleteId   = null;
    let _deleteCode = null;

    // ============================================================
    // FETCH HELPER
    // ============================================================

    async function catFetch(path, options = {}) {
        const url  = `${API()}${path}`;
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
    // ALERTS
    // ============================================================

    function showAlert(msg, type = 'success') {
        const el = document.getElementById('catAlert');
        if (!el) return;
        el.className = `alert alert-${type}`;
        el.innerHTML = `<i class="bi bi-${type === 'success' ? 'check-circle' : 'exclamation-triangle'} me-2"></i>${msg}`;
        el.classList.remove('d-none');
        setTimeout(() => el.classList.add('d-none'), 5000);
    }

    function showModalAlert(msg, type = 'danger') {
        const el = document.getElementById('catModalAlert');
        if (!el) return;
        el.className = `alert alert-${type} mt-2 mb-0`;
        el.innerHTML = `<i class="bi bi-exclamation-triangle me-2"></i>${msg}`;
        el.classList.remove('d-none');
    }

    function clearModalAlert() {
        const el = document.getElementById('catModalAlert');
        if (el) el.classList.add('d-none');
    }

    // ============================================================
    // CARGAR Y RENDERIZAR
    // ============================================================

    async function load() {
        const tbody = document.getElementById('bodyCAT');
        tbody.innerHTML = '<tr><td colspan="6" class="text-center text-muted py-3"><i class="bi bi-hourglass-split me-1"></i>Cargando…</td></tr>';
        try {
            const items = await catFetch('/api/chart-of-accounts-type');
            renderTable(items);
        } catch (e) {
            tbody.innerHTML = `<tr><td colspan="6" class="text-center text-danger py-3">${e.message}</td></tr>`;
        }
    }

    function renderTable(items) {
        const tbody = document.getElementById('bodyCAT');
        if (!items || !items.length) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-center text-light py-3">No hay tipos de cuenta registrados.</td></tr>';
            return;
        }
        tbody.innerHTML = items.map(t => `
            <tr>
                <td class="text-center text-light" style="font-size:.8rem;">${t.sortOrder}</td>
                <td><code class="cat-code">${escHtml(t.code)}</code></td>
                <td class="text-light">${escHtml(t.description || '—')}</td>
                <td class="text-center">
                    ${t.icon
                        ? `<i class="bi ${escHtml(t.icon)} text-info" title="${escHtml(t.icon)}"></i>
                           <small class="text-muted ms-1" style="font-size:.7rem;">${escHtml(t.icon)}</small>`
                        : '<span class="text-light">—</span>'}
                </td>
                <td class="text-center">
                    ${t.isActive
                        ? '<i class="bi bi-check-circle-fill text-success"></i>'
                        : '<i class="bi bi-x-circle-fill text-danger"></i>'}
                </td>
                <td class="text-end">
                    <button class="btn btn-sm btn-outline-info me-1" onclick="CAT.openEdit(${t.id})" title="Editar">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger" onclick="CAT.openDelete(${t.id}, '${escHtml(t.code)}')" title="Desactivar">
                        <i class="bi bi-trash"></i>
                    </button>
                </td>
            </tr>
        `).join('');
    }

    // ============================================================
    // MODAL CREAR
    // ============================================================

    function openNew() {
        document.getElementById('catId').value       = '';
        document.getElementById('catCode').value     = '';
        document.getElementById('catDesc').value     = '';
        document.getElementById('catIcon').value     = '';
        document.getElementById('catOrder').value    = '10';
        document.getElementById('catActive').checked = true;
        document.getElementById('catIconPreview').className = 'bi bi-question-circle text-info';
        document.getElementById('catModalTitle').innerHTML  =
            '<i class="bi bi-tags me-2 text-info"></i>Nuevo Tipo de Cuenta Contable';
        clearModalAlert();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('catModal')).show();
    }

    // ============================================================
    // MODAL EDITAR
    // ============================================================

    async function openEdit(id) {
        try {
            const item = await catFetch(`/api/chart-of-accounts-type/${id}`);
            document.getElementById('catId').value       = item.id;
            document.getElementById('catCode').value     = item.code;
            document.getElementById('catDesc').value     = item.description || '';
            document.getElementById('catIcon').value     = item.icon || '';
            document.getElementById('catOrder').value    = item.sortOrder;
            document.getElementById('catActive').checked = item.isActive;
            previewIcon(item.icon || '');
            document.getElementById('catModalTitle').innerHTML =
                '<i class="bi bi-pencil me-2 text-warning"></i>Editar Tipo de Cuenta Contable';
            clearModalAlert();
            bootstrap.Modal.getOrCreateInstance(document.getElementById('catModal')).show();
        } catch (e) {
            showAlert(e.message, 'danger');
        }
    }

    // ============================================================
    // GUARDAR
    // ============================================================

    async function save() {
        const id          = document.getElementById('catId').value;
        const code        = document.getElementById('catCode').value.trim();
        const description = document.getElementById('catDesc').value.trim() || null;
        const icon        = document.getElementById('catIcon').value.trim() || null;
        const sortOrder   = parseInt(document.getElementById('catOrder').value, 10) || 0;
        const isActive    = document.getElementById('catActive').checked;

        if (!code) { showModalAlert('El código es requerido.'); return; }

        const payload = { code, description, icon, sortOrder, isActive };

        try {
            if (id) {
                await catFetch(`/api/chart-of-accounts-type/${id}`, { method: 'PUT', body: payload });
                bootstrap.Modal.getOrCreateInstance(document.getElementById('catModal')).hide();
                showAlert('Tipo de cuenta actualizado correctamente.');
            } else {
                await catFetch('/api/chart-of-accounts-type', { method: 'POST', body: payload });
                bootstrap.Modal.getOrCreateInstance(document.getElementById('catModal')).hide();
                showAlert('Tipo de cuenta creado correctamente.');
            }
            await load();
        } catch (e) {
            showModalAlert(e.message);
        }
    }

    // ============================================================
    // ELIMINAR (lógico)
    // ============================================================

    function openDelete(id, code) {
        _deleteId   = id;
        _deleteCode = code;
        document.getElementById('catDeleteCode').textContent = code;
        bootstrap.Modal.getOrCreateInstance(document.getElementById('catDeleteModal')).show();
    }

    async function confirmDelete() {
        if (!_deleteId) return;
        try {
            await catFetch(`/api/chart-of-accounts-type/${_deleteId}`, { method: 'DELETE' });
            bootstrap.Modal.getOrCreateInstance(document.getElementById('catDeleteModal')).hide();
            showAlert(`Tipo de cuenta '${_deleteCode}' desactivado.`);
            await load();
        } catch (e) {
            showAlert(e.message, 'danger');
        } finally {
            _deleteId = _deleteCode = null;
        }
    }

    // ============================================================
    // PREVIEW ICONO
    // ============================================================

    function previewIcon(value) {
        const el = document.getElementById('catIconPreview');
        if (!el) return;
        el.className = value.trim() ? `bi ${value.trim()} text-info` : 'bi bi-question-circle text-info';
    }

    // ============================================================
    // HELPERS
    // ============================================================

    function escHtml(str) {
        if (str == null) return '';
        return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    // ============================================================
    // INIT
    // ============================================================

    document.addEventListener('DOMContentLoaded', load);

    return { load, openNew, openEdit, save, openDelete, confirmDelete, previewIcon };
})();
