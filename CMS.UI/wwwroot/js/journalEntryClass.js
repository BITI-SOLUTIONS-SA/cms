// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/journalEntryClass.js
// PROPÓSITO: Lógica cliente para mantenimiento de admin.journal_entry_class
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

'use strict';

const JEC = (() => {
    const API   = () => window.JEC_API   || '';
    const TOKEN = () => window.JEC_TOKEN || '';

    let _deleteId   = null;
    let _deleteCode = null;

    // ============================================================
    // FETCH HELPER
    // ============================================================

    async function jecFetch(path, options = {}) {
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
        const el = document.getElementById('jecAlert');
        if (!el) return;
        el.className = `alert alert-${type}`;
        el.innerHTML = `<i class="bi bi-${type === 'success' ? 'check-circle' : 'exclamation-triangle'} me-2"></i>${msg}`;
        el.classList.remove('d-none');
        setTimeout(() => el.classList.add('d-none'), 5000);
    }

    function showModalAlert(msg, type = 'danger') {
        const el = document.getElementById('jecModalAlert');
        if (!el) return;
        el.className = `alert alert-${type} mt-2 mb-0`;
        el.innerHTML = `<i class="bi bi-exclamation-triangle me-2"></i>${msg}`;
        el.classList.remove('d-none');
    }

    function clearModalAlert() {
        const el = document.getElementById('jecModalAlert');
        if (el) el.classList.add('d-none');
    }

    // ============================================================
    // CARGAR Y RENDERIZAR
    // ============================================================

    async function load() {
        const tbody = document.getElementById('bodyJEC');
        tbody.innerHTML = '<tr><td colspan="6" class="text-center text-muted py-3"><i class="bi bi-hourglass-split me-1"></i>Cargando…</td></tr>';
        try {
            const items = await jecFetch('/api/journal-entry-class');
            renderTable(items);
        } catch (e) {
            tbody.innerHTML = `<tr><td colspan="6" class="text-center text-danger py-3">${e.message}</td></tr>`;
        }
    }

    function renderTable(items) {
        const tbody = document.getElementById('bodyJEC');
        if (!items || !items.length) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-center text-light py-3">No hay clases de asiento registradas.</td></tr>';
            return;
        }
        tbody.innerHTML = items.map(t => `
            <tr>
                <td class="text-center text-light" style="font-size:.8rem;">${t.sortOrder}</td>
                <td><code class="jec-code">${escHtml(t.code)}</code></td>
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
                    <button class="btn btn-sm btn-outline-info me-1" onclick="JEC.openEdit(${t.id})" title="Editar">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger" onclick="JEC.openDelete(${t.id}, '${escHtml(t.code)}')" title="Desactivar">
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
        document.getElementById('jecId').value       = '';
        document.getElementById('jecCode').value     = '';
        document.getElementById('jecDesc').value     = '';
        document.getElementById('jecIcon').value     = '';
        document.getElementById('jecOrder').value    = '10';
        document.getElementById('jecActive').checked = true;
        document.getElementById('jecIconPreview').className = 'bi bi-question-circle text-info';
        document.getElementById('jecModalTitle').innerHTML  =
            '<i class="bi bi-bookmark-check me-2 text-info"></i>Nueva Clase de Asiento';
        clearModalAlert();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('jecModal')).show();
    }

    // ============================================================
    // MODAL EDITAR
    // ============================================================

    async function openEdit(id) {
        try {
            const item = await jecFetch(`/api/journal-entry-class/${id}`);
            document.getElementById('jecId').value       = item.id;
            document.getElementById('jecCode').value     = item.code;
            document.getElementById('jecDesc').value     = item.description || '';
            document.getElementById('jecIcon').value     = item.icon || '';
            document.getElementById('jecOrder').value    = item.sortOrder;
            document.getElementById('jecActive').checked = item.isActive;
            previewIcon(item.icon || '');
            document.getElementById('jecModalTitle').innerHTML =
                '<i class="bi bi-pencil me-2 text-warning"></i>Editar Clase de Asiento';
            clearModalAlert();
            bootstrap.Modal.getOrCreateInstance(document.getElementById('jecModal')).show();
        } catch (e) {
            showAlert(e.message, 'danger');
        }
    }

    // ============================================================
    // GUARDAR
    // ============================================================

    async function save() {
        const id          = document.getElementById('jecId').value;
        const code        = document.getElementById('jecCode').value.trim().toUpperCase();
        const description = document.getElementById('jecDesc').value.trim() || null;
        const icon        = document.getElementById('jecIcon').value.trim() || null;
        const sortOrder   = parseInt(document.getElementById('jecOrder').value, 10) || 0;
        const isActive    = document.getElementById('jecActive').checked;

        if (!code) { showModalAlert('El código es requerido.'); return; }

        const payload = { code, description, icon, sortOrder, isActive };

        try {
            if (id) {
                await jecFetch(`/api/journal-entry-class/${id}`, { method: 'PUT', body: payload });
                bootstrap.Modal.getOrCreateInstance(document.getElementById('jecModal')).hide();
                showAlert('Clase de asiento actualizada correctamente.');
            } else {
                await jecFetch('/api/journal-entry-class', { method: 'POST', body: payload });
                bootstrap.Modal.getOrCreateInstance(document.getElementById('jecModal')).hide();
                showAlert('Clase de asiento creada correctamente.');
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
        document.getElementById('jecDeleteCode').textContent = code;
        bootstrap.Modal.getOrCreateInstance(document.getElementById('jecDeleteModal')).show();
    }

    async function confirmDelete() {
        if (!_deleteId) return;
        try {
            await jecFetch(`/api/journal-entry-class/${_deleteId}`, { method: 'DELETE' });
            bootstrap.Modal.getOrCreateInstance(document.getElementById('jecDeleteModal')).hide();
            showAlert(`Clase de asiento '${_deleteCode}' desactivada.`);
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
        const el = document.getElementById('jecIconPreview');
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
