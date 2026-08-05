// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/typeAccountings.js
// PROPÓSITO: Lógica cliente para mantenimiento de admin.type_accounting
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

'use strict';

const TA = (() => {
    const API   = () => window.TA_API   || '';
    const TOKEN = () => window.TA_TOKEN || '';

    let _deleteId   = null;
    let _deleteCode = null;

    // ============================================================
    // FETCH HELPER
    // ============================================================

    async function taFetch(path, options = {}) {
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
        const el = document.getElementById('taAlert');
        if (!el) return;
        el.className = `alert alert-${type}`;
        el.innerHTML = `<i class="bi bi-${type === 'success' ? 'check-circle' : 'exclamation-triangle'} me-2"></i>${msg}`;
        el.classList.remove('d-none');
        setTimeout(() => el.classList.add('d-none'), 5000);
    }

    function showModalAlert(msg, type = 'danger') {
        const el = document.getElementById('taModalAlert');
        if (!el) return;
        el.className = `alert alert-${type} mt-2 mb-0`;
        el.innerHTML = `<i class="bi bi-exclamation-triangle me-2"></i>${msg}`;
        el.classList.remove('d-none');
    }

    function clearModalAlert() {
        const el = document.getElementById('taModalAlert');
        if (el) el.classList.add('d-none');
    }

    // ============================================================
    // CARGAR Y RENDERIZAR
    // ============================================================

    async function load() {
        const tbody = document.getElementById('bodyTA');
        tbody.innerHTML = '<tr><td colspan="6" class="text-center text-muted py-3"><i class="bi bi-hourglass-split me-1"></i>Cargando…</td></tr>';
        try {
            const items = await taFetch('/api/type-accounting');
            renderTable(items);
        } catch (e) {
            tbody.innerHTML = `<tr><td colspan="6" class="text-center text-danger py-3">${e.message}</td></tr>`;
        }
    }

    function renderTable(items) {
        const tbody = document.getElementById('bodyTA');
        if (!items || !items.length) {
            tbody.innerHTML = '<tr><td colspan="6" class="text-center text-light py-3">No hay tipos de contabilidad registrados.</td></tr>';
            return;
        }
        tbody.innerHTML = items.map(t => `
            <tr>
                <td class="text-center text-light" style="font-size:.8rem;">${t.sortOrder}</td>
                <td><code class="ta-code">${escHtml(t.code)}</code></td>
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
                    <button class="btn btn-sm btn-outline-info me-1" onclick="TA.openEdit(${t.id})" title="Editar">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger" onclick="TA.openDelete(${t.id}, '${escHtml(t.code)}')" title="Desactivar">
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
        document.getElementById('taId').value       = '';
        document.getElementById('taCode').value     = '';
        document.getElementById('taDesc').value     = '';
        document.getElementById('taIcon').value     = '';
        document.getElementById('taOrder').value    = '10';
        document.getElementById('taActive').checked = true;
        document.getElementById('taIconPreview').className = 'bi bi-question-circle text-info';
        document.getElementById('taModalTitle').innerHTML  =
            '<i class="bi bi-journal-text me-2 text-info"></i>Nuevo Tipo de Contabilidad';
        clearModalAlert();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('taModal')).show();
    }

    // ============================================================
    // MODAL EDITAR
    // ============================================================

    async function openEdit(id) {
        try {
            const item = await taFetch(`/api/type-accounting/${id}`);
            document.getElementById('taId').value       = item.id;
            document.getElementById('taCode').value     = item.code;
            document.getElementById('taDesc').value     = item.description || '';
            document.getElementById('taIcon').value     = item.icon || '';
            document.getElementById('taOrder').value    = item.sortOrder;
            document.getElementById('taActive').checked = item.isActive;
            previewIcon(item.icon || '');
            document.getElementById('taModalTitle').innerHTML =
                '<i class="bi bi-pencil me-2 text-warning"></i>Editar Tipo de Contabilidad';
            clearModalAlert();
            bootstrap.Modal.getOrCreateInstance(document.getElementById('taModal')).show();
        } catch (e) {
            showAlert(e.message, 'danger');
        }
    }

    // ============================================================
    // GUARDAR
    // ============================================================

    async function save() {
        const id          = document.getElementById('taId').value;
        const code        = document.getElementById('taCode').value.trim();
        const description = document.getElementById('taDesc').value.trim() || null;
        const icon        = document.getElementById('taIcon').value.trim() || null;
        const sortOrder   = parseInt(document.getElementById('taOrder').value, 10) || 0;
        const isActive    = document.getElementById('taActive').checked;

        if (!code) { showModalAlert('El código es requerido.'); return; }

        const payload = { code, description, icon, sortOrder, isActive };

        try {
            if (id) {
                await taFetch(`/api/type-accounting/${id}`, { method: 'PUT', body: payload });
                bootstrap.Modal.getOrCreateInstance(document.getElementById('taModal')).hide();
                showAlert('Tipo de contabilidad actualizado correctamente.');
            } else {
                await taFetch('/api/type-accounting', { method: 'POST', body: payload });
                bootstrap.Modal.getOrCreateInstance(document.getElementById('taModal')).hide();
                showAlert('Tipo de contabilidad creado correctamente.');
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
        document.getElementById('taDeleteCode').textContent = code;
        bootstrap.Modal.getOrCreateInstance(document.getElementById('taDeleteModal')).show();
    }

    async function confirmDelete() {
        if (!_deleteId) return;
        try {
            await taFetch(`/api/type-accounting/${_deleteId}`, { method: 'DELETE' });
            bootstrap.Modal.getOrCreateInstance(document.getElementById('taDeleteModal')).hide();
            showAlert(`Tipo de contabilidad '${_deleteCode}' desactivado.`);
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
        const el = document.getElementById('taIconPreview');
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
