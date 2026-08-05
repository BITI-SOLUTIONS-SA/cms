// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/journalEntryStatus.js
// PROPÓSITO: Lógica cliente para mantenimiento de admin.journal_entry_status
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

'use strict';

const JES = (() => {
    const API   = () => window.JES_API   || '';
    const TOKEN = () => window.JES_TOKEN || '';

    let _deleteId   = null;
    let _deleteCode = null;

    // ============================================================
    // FETCH HELPER
    // ============================================================

    async function jesFetch(path, options = {}) {
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
        const el = document.getElementById('jesAlert');
        if (!el) return;
        el.className = `alert alert-${type}`;
        el.innerHTML = `<i class="bi bi-${type === 'success' ? 'check-circle' : 'exclamation-triangle'} me-2"></i>${msg}`;
        el.classList.remove('d-none');
        setTimeout(() => el.classList.add('d-none'), 5000);
    }

    function showModalAlert(msg, type = 'danger') {
        const el = document.getElementById('jesModalAlert');
        if (!el) return;
        el.className = `alert alert-${type} mt-2 mb-0`;
        el.innerHTML = `<i class="bi bi-exclamation-triangle me-2"></i>${msg}`;
        el.classList.remove('d-none');
    }

    function clearModalAlert() {
        const el = document.getElementById('jesModalAlert');
        if (el) el.classList.add('d-none');
    }

    // ============================================================
    // CARGAR Y RENDERIZAR
    // ============================================================

    async function load() {
        const tbody = document.getElementById('bodyJES');
        tbody.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-3"><i class="bi bi-hourglass-split me-1"></i>Cargando…</td></tr>';
        try {
            const items = await jesFetch('/api/journal-entry-status');
            renderTable(items);
        } catch (e) {
            tbody.innerHTML = `<tr><td colspan="7" class="text-center text-danger py-3">${e.message}</td></tr>`;
        }
    }

    function renderTable(items) {
        const tbody = document.getElementById('bodyJES');
        if (!items || !items.length) {
            tbody.innerHTML = '<tr><td colspan="7" class="text-center text-light py-3">No hay estados de asiento registrados.</td></tr>';
            return;
        }
        tbody.innerHTML = items.map(t => `
            <tr>
                <td class="text-center text-light" style="font-size:.8rem;">${t.sortOrder}</td>
                <td>
                    ${t.color
                        ? `<span class="badge bg-${escHtml(t.color)}">${escHtml(t.code)}</span>`
                        : `<code class="jes-code">${escHtml(t.code)}</code>`}
                </td>
                <td class="text-light">${escHtml(t.description || '—')}</td>
                <td class="text-center">
                    ${t.icon
                        ? `<i class="bi ${escHtml(t.icon)} text-info" title="${escHtml(t.icon)}"></i>
                           <small class="text-muted ms-1" style="font-size:.7rem;">${escHtml(t.icon)}</small>`
                        : '<span class="text-light">—</span>'}
                </td>
                <td class="text-center">
                    ${t.color
                        ? `<span class="badge bg-${escHtml(t.color)}">${escHtml(t.color)}</span>`
                        : '<span class="text-muted">—</span>'}
                </td>
                <td class="text-center">
                    ${t.isActive
                        ? '<i class="bi bi-check-circle-fill text-success"></i>'
                        : '<i class="bi bi-x-circle-fill text-danger"></i>'}
                </td>
                <td class="text-end">
                    <button class="btn btn-sm btn-outline-info me-1" onclick="JES.openEdit(${t.id})" title="Editar">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger" onclick="JES.openDelete(${t.id}, '${escHtml(t.code)}')" title="Desactivar">
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
        document.getElementById('jesId').value       = '';
        document.getElementById('jesCode').value     = '';
        document.getElementById('jesDesc').value     = '';
        document.getElementById('jesIcon').value     = '';
        document.getElementById('jesColor').value    = '';
        document.getElementById('jesOrder').value    = '10';
        document.getElementById('jesActive').checked = true;
        document.getElementById('jesIconPreview').className = 'bi bi-question-circle text-info';
        document.getElementById('jesModalTitle').innerHTML  =
            '<i class="bi bi-signpost-split me-2 text-info"></i>Nuevo Estado de Asiento';
        clearModalAlert();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('jesModal')).show();
    }

    // ============================================================
    // MODAL EDITAR
    // ============================================================

    async function openEdit(id) {
        try {
            const item = await jesFetch(`/api/journal-entry-status/${id}`);
            document.getElementById('jesId').value       = item.id;
            document.getElementById('jesCode').value     = item.code;
            document.getElementById('jesDesc').value     = item.description || '';
            document.getElementById('jesIcon').value     = item.icon || '';
            document.getElementById('jesColor').value    = item.color || '';
            document.getElementById('jesOrder').value    = item.sortOrder;
            document.getElementById('jesActive').checked = item.isActive;
            previewIcon(item.icon || '');
            document.getElementById('jesModalTitle').innerHTML =
                '<i class="bi bi-pencil me-2 text-warning"></i>Editar Estado de Asiento';
            clearModalAlert();
            bootstrap.Modal.getOrCreateInstance(document.getElementById('jesModal')).show();
        } catch (e) {
            showAlert(e.message, 'danger');
        }
    }

    // ============================================================
    // GUARDAR
    // ============================================================

    async function save() {
        const id          = document.getElementById('jesId').value;
        const code        = document.getElementById('jesCode').value.trim();
        const description = document.getElementById('jesDesc').value.trim() || null;
        const icon        = document.getElementById('jesIcon').value.trim() || null;
        const color       = document.getElementById('jesColor').value || null;
        const sortOrder   = parseInt(document.getElementById('jesOrder').value, 10) || 0;
        const isActive    = document.getElementById('jesActive').checked;

        if (!code) { showModalAlert('El código es requerido.'); return; }

        const payload = { code, description, icon, color, sortOrder, isActive };

        try {
            if (id) {
                await jesFetch(`/api/journal-entry-status/${id}`, { method: 'PUT', body: payload });
                bootstrap.Modal.getOrCreateInstance(document.getElementById('jesModal')).hide();
                showAlert('Estado de asiento actualizado correctamente.');
            } else {
                await jesFetch('/api/journal-entry-status', { method: 'POST', body: payload });
                bootstrap.Modal.getOrCreateInstance(document.getElementById('jesModal')).hide();
                showAlert('Estado de asiento creado correctamente.');
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
        document.getElementById('jesDeleteCode').textContent = code;
        bootstrap.Modal.getOrCreateInstance(document.getElementById('jesDeleteModal')).show();
    }

    async function confirmDelete() {
        if (!_deleteId) return;
        try {
            await jesFetch(`/api/journal-entry-status/${_deleteId}`, { method: 'DELETE' });
            bootstrap.Modal.getOrCreateInstance(document.getElementById('jesDeleteModal')).hide();
            showAlert(`Estado '${_deleteCode}' desactivado.`);
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
        const el = document.getElementById('jesIconPreview');
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
