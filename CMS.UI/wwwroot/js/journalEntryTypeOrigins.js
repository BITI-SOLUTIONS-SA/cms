// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/journalEntryTypeOrigins.js
// PROPÓSITO: Lógica cliente para mantenimiento de admin.journal_entry_type_origin
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

'use strict';

const JETO = (() => {
    const API   = () => window.JETO_API   || '';
    const TOKEN = () => window.JETO_TOKEN || '';

    let _deleteId   = null;
    let _deleteCode = null;

    // ============================================================
    // FETCH HELPER
    // ============================================================

    async function jetoFetch(path, options = {}) {
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
    // ALERT
    // ============================================================

    function showAlert(msg, type = 'success') {
        const el = document.getElementById('jetoAlert');
        if (!el) return;
        el.className = `alert alert-${type}`;
        el.innerHTML = `<i class="bi bi-${type === 'success' ? 'check-circle' : 'exclamation-triangle'} me-2"></i>${msg}`;
        el.classList.remove('d-none');
        setTimeout(() => el.classList.add('d-none'), 5000);
    }

    // Muestra error dentro del modal (visible aunque el modal esté abierto)
    function showModalAlert(msg, type = 'danger') {
        const el = document.getElementById('jetoModalAlert');
        if (!el) return;
        el.className = `alert alert-${type} mt-2 mb-0`;
        el.innerHTML = `<i class="bi bi-exclamation-triangle me-2"></i>${msg}`;
        el.classList.remove('d-none');
    }

    function clearModalAlert() {
        const el = document.getElementById('jetoModalAlert');
        if (el) el.classList.add('d-none');
    }

    // ============================================================
    // CARGAR Y RENDERIZAR
    // ============================================================

    async function load() {
        const tbody = document.getElementById('bodyOrigins');
        tbody.innerHTML = '<tr><td colspan="6" class="text-center text-muted py-3"><i class="bi bi-hourglass-split me-1"></i>Cargando…</td></tr>';
        try {
            const items = await jetoFetch('/api/journal-entry-type-origin');
            renderTable(items);
        } catch (e) {
            tbody.innerHTML = `<tr><td colspan="6" class="text-center text-danger py-3">${e.message}</td></tr>`;
        }
    }

    function renderTable(items) {
        const tbody = document.getElementById('bodyOrigins');
        if (!items || !items.length) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-center text-light py-3">No hay tipos de origen registrados.</td></tr>';
            return;
        }
        tbody.innerHTML = items.map(t => `
            <tr>
                <td class="text-center text-light" style="font-size:.8rem;">${t.sortOrder}</td>
                <td>
                    <code class="origin-code">${escHtml(t.code)}</code>
                </td>
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
                    <button class="btn btn-sm btn-outline-info me-1" onclick="JETO.openEdit(${t.id})" title="Editar">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger" onclick="JETO.openDelete(${t.id}, '${escHtml(t.code)}')" title="Desactivar">
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
        document.getElementById('jetoId').value        = '';
        document.getElementById('jetoCode').value      = '';
        document.getElementById('jetoDesc').value      = '';
        document.getElementById('jetoIcon').value      = '';
        document.getElementById('jetoOrder').value     = '10';
        document.getElementById('jetoActive').checked  = true;
        document.getElementById('jetoIconPreview').className = 'bi bi-question-circle text-info';
        document.getElementById('jetoModalTitle').innerHTML  =
            '<i class="bi bi-diagram-3 me-2 text-info"></i>Nuevo Tipo de Origen';
        document.getElementById('jetoCode').disabled = false;
        clearModalAlert();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('jetoModal')).show();
    }

    // ============================================================
    // MODAL EDITAR
    // ============================================================

    async function openEdit(id) {
        try {
            const item = await jetoFetch(`/api/journal-entry-type-origin/${id}`);
            document.getElementById('jetoId').value        = item.id;
            document.getElementById('jetoCode').value      = item.code;
            document.getElementById('jetoDesc').value      = item.description || '';
            document.getElementById('jetoIcon').value      = item.icon || '';
            document.getElementById('jetoOrder').value     = item.sortOrder;
            document.getElementById('jetoActive').checked  = item.isActive;
            document.getElementById('jetoCode').disabled   = false;
            previewIcon(item.icon || '');
            document.getElementById('jetoModalTitle').innerHTML =
                '<i class="bi bi-pencil me-2 text-warning"></i>Editar Tipo de Origen';
            clearModalAlert();
            bootstrap.Modal.getOrCreateInstance(document.getElementById('jetoModal')).show();
        } catch (e) {
            showAlert(e.message, 'danger');
        }
    }

    // ============================================================
    // GUARDAR (crear o actualizar)
    // ============================================================

    async function save() {
        const id          = document.getElementById('jetoId').value;
        const code        = document.getElementById('jetoCode').value.trim();
        const description = document.getElementById('jetoDesc').value.trim() || null;
        const icon        = document.getElementById('jetoIcon').value.trim() || null;
        const sortOrder   = parseInt(document.getElementById('jetoOrder').value, 10) || 0;
        const isActive    = document.getElementById('jetoActive').checked;

        if (!code) { showModalAlert('El código es requerido.'); return; }

        const payload = { code, description, icon, sortOrder, isActive };

        try {
            if (id) {
                await jetoFetch(`/api/journal-entry-type-origin/${id}`, { method: 'PUT', body: payload });
                bootstrap.Modal.getOrCreateInstance(document.getElementById('jetoModal')).hide();
                showAlert('Tipo de origen actualizado correctamente.');
            } else {
                await jetoFetch('/api/journal-entry-type-origin', { method: 'POST', body: payload });
                bootstrap.Modal.getOrCreateInstance(document.getElementById('jetoModal')).hide();
                showAlert('Tipo de origen creado correctamente.');
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
        document.getElementById('jetoDeleteCode').textContent = code;
        bootstrap.Modal.getOrCreateInstance(document.getElementById('jetoDeleteModal')).show();
    }

    async function confirmDelete() {
        if (!_deleteId) return;
        try {
            await jetoFetch(`/api/journal-entry-type-origin/${_deleteId}`, { method: 'DELETE' });
            bootstrap.Modal.getOrCreateInstance(document.getElementById('jetoDeleteModal')).hide();
            showAlert(`Tipo de origen '${_deleteCode}' desactivado.`);
            await load();
        } catch (e) {
            showAlert(e.message, 'danger');
        } finally {
            _deleteId   = null;
            _deleteCode = null;
        }
    }

    // ============================================================
    // PREVIEW ICONO
    // ============================================================

    function previewIcon(value) {
        const el = document.getElementById('jetoIconPreview');
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
