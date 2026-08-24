// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/vendors.js
// PROPÓSITO: Mantenimiento de vendors (proveedores) y sus actividades económicas.
// CONSUME: /api/Vendor  (VendorController en CMS.API)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

let VD_STATE = { page: 1, pageSize: 25, total: 0 };
let VD_CURRENT_ID = null;

async function vdFetch(path, options = {}) {
    const url = (window.VD_API || '') + path;
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${window.VD_TOKEN || ''}`,
        ...(options.headers || {}),
    };
    const res = await fetch(url, { ...options, headers });
    if (res.status === 204) return null;
    const text = await res.text();
    const data = text ? JSON.parse(text) : null;
    if (!res.ok) {
        const msg = (data && data.message) || `HTTP ${res.status}`;
        throw new Error(msg);
    }
    return data;
}

function vdEsc(s) {
    return (s ?? '').toString()
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

async function loadVendors() {
    const tbody = document.getElementById('vdResults');
    tbody.innerHTML = '<tr><td colspan="8" class="text-center text-muted p-3">Cargando...</td></tr>';
    try {
        const params = new URLSearchParams();
        const term = document.getElementById('vdSearch').value.trim();
        if (term) params.append('searchTerm', term);
        if (document.getElementById('vdIncludeInactive').checked) params.append('includeInactive', 'true');
        params.append('page', VD_STATE.page);
        params.append('pageSize', VD_STATE.pageSize);

        const result = await vdFetch(`/api/Vendor?${params}`);
        VD_STATE.total = result.total;

        const items = result.items || [];
        if (items.length === 0) {
            tbody.innerHTML = '<tr><td colspan="8" class="text-center text-muted p-3">No se encontraron vendors.</td></tr>';
        } else {
            tbody.innerHTML = items.map(v => `
                <tr>
                    <td>${vdEsc(v.code)}</td>
                    <td>${vdEsc(v.name)}${v.commercialName ? `<br/><small class="text-muted">${vdEsc(v.commercialName)}</small>` : ''}</td>
                    <td>${vdEsc(v.identification) || '<span class="text-muted">—</span>'}</td>
                    <td>${vdEsc(v.vendorType) || '—'}</td>
                    <td>${vdEsc(v.email) || '<span class="text-muted">—</span>'}</td>
                    <td>${vdEsc(v.phone) || '<span class="text-muted">—</span>'}</td>
                    <td class="text-center">${v.isActive
                        ? '<span class="badge bg-success">Activo</span>'
                        : '<span class="badge bg-secondary">Inactivo</span>'}</td>
                    <td class="text-center" style="white-space:nowrap;">
                        <button class="btn btn-sm btn-outline-primary" onclick="openVendorModal(${v.id})" title="Editar"><i class="bi bi-pencil"></i></button>
                        <button class="btn btn-sm btn-outline-danger ms-1" onclick="deactivateVendor(${v.id})" title="Desactivar"><i class="bi bi-trash"></i></button>
                    </td>
                </tr>`).join('');
        }

        const from = VD_STATE.total === 0 ? 0 : (VD_STATE.page - 1) * VD_STATE.pageSize + 1;
        const to = Math.min(VD_STATE.page * VD_STATE.pageSize, VD_STATE.total);
        const totalPages = Math.max(1, Math.ceil(VD_STATE.total / VD_STATE.pageSize));
        document.getElementById('vdPager').innerHTML = `
            <span>Mostrando ${from}-${to} de ${VD_STATE.total}</span>
            <span>
                <button class="btn btn-sm btn-outline-light" ${VD_STATE.page <= 1 ? 'disabled' : ''} onclick="vdGoPage(${VD_STATE.page - 1})">&laquo;</button>
                <span class="mx-2">Página ${VD_STATE.page} / ${totalPages}</span>
                <button class="btn btn-sm btn-outline-light" ${VD_STATE.page >= totalPages ? 'disabled' : ''} onclick="vdGoPage(${VD_STATE.page + 1})">&raquo;</button>
            </span>`;
    } catch (e) {
        tbody.innerHTML = `<tr><td colspan="8" class="text-center text-danger p-3">${vdEsc(e.message)}</td></tr>`;
    }
}

function vdGoPage(p) {
    VD_STATE.page = p;
    loadVendors();
}

function resetVendorForm() {
    ['fldVendorId', 'fldCode', 'fldName', 'fldCommercialName', 'fldIdentification',
     'fldEconomicActivity', 'fldEmail', 'fldPhoneCode', 'fldPhone',
     'fldCreditDays', 'fldCreditLimit', 'fldNotes', 'fldNewActivityCode', 'fldNewActivityDesc']
        .forEach(id => { const el = document.getElementById(id); if (el) el.value = ''; });
    document.getElementById('fldIdentificationType').value = '';
    document.getElementById('fldVendorType').value = 'Both';
    document.getElementById('fldCurrency').value = 'CRC';
    document.getElementById('fldIsActive').checked = true;
    document.getElementById('activitiesBody').innerHTML =
        '<tr><td colspan="4" class="text-center text-muted p-2">Sin actividades registradas.</td></tr>';
}

async function openVendorModal(id = null) {
    resetVendorForm();
    VD_CURRENT_ID = id;

    const showActivities = !!id;
    document.getElementById('activitiesSection').classList.toggle('d-none', !showActivities);
    document.getElementById('activitiesHint').classList.toggle('d-none', showActivities);
    if (showActivities) await loadActivityCatalog();

    if (id) {
        document.getElementById('vendorModalTitle').innerHTML = '<i class="bi bi-pencil me-2"></i>Editar Vendor';
        try {
            const v = await vdFetch(`/api/Vendor/${id}`);
            document.getElementById('fldVendorId').value = v.id;
            document.getElementById('fldCode').value = v.code || '';
            document.getElementById('fldName').value = v.name || '';
            document.getElementById('fldCommercialName').value = v.commercialName || '';
            document.getElementById('fldIdentificationType').value = v.idElectronicDocumentIdentificationType || '';
            document.getElementById('fldIdentification').value = v.identification || '';
            document.getElementById('fldEconomicActivity').value = v.economicActivity || '';
            document.getElementById('fldVendorType').value = v.vendorType || 'Both';
            document.getElementById('fldEmail').value = v.email || '';
            document.getElementById('fldPhoneCode').value = v.phoneCode || '';
            document.getElementById('fldPhone').value = v.phone || '';
            document.getElementById('fldCurrency').value = v.currency || 'CRC';
            document.getElementById('fldCreditDays').value = v.creditDays ?? '';
            document.getElementById('fldCreditLimit').value = v.creditLimit ?? '';
            document.getElementById('fldNotes').value = v.notes || '';
            document.getElementById('fldIsActive').checked = !!v.isActive;
            renderActivities(v.economicActivities || []);
        } catch (e) {
            alert('Error al cargar vendor: ' + e.message);
            return;
        }
    } else {
        document.getElementById('vendorModalTitle').innerHTML = '<i class="bi bi-bag-check me-2"></i>Nuevo Vendor';
    }

    new bootstrap.Modal(document.getElementById('vendorModal')).show();
}

function buildVendorDto() {
    const gv = id => { const v = document.getElementById(id).value.trim(); return v === '' ? null : v; };
    const gn = id => { const v = document.getElementById(id).value; return v === '' ? null : +v; };
    return {
        id: +document.getElementById('fldVendorId').value || 0,
        code: gv('fldCode'),
        name: gv('fldName'),
        commercialName: gv('fldCommercialName'),
        idElectronicDocumentIdentificationType: gn('fldIdentificationType'),
        identification: gv('fldIdentification'),
        economicActivity: gv('fldEconomicActivity'),
        vendorType: gv('fldVendorType'),
        email: gv('fldEmail'),
        phoneCode: gv('fldPhoneCode'),
        phone: gv('fldPhone'),
        currency: gv('fldCurrency'),
        creditDays: gn('fldCreditDays'),
        creditLimit: gn('fldCreditLimit'),
        notes: gv('fldNotes'),
        isActive: document.getElementById('fldIsActive').checked
    };
}

async function saveVendor() {
    const dto = buildVendorDto();
    if (!dto.code) { alert('El código es obligatorio.'); return; }
    if (!dto.name) { alert('El nombre es obligatorio.'); return; }

    // Validar el número de identificación según el tipo (Hacienda CR), si se indicó tipo o número.
    if (dto.idElectronicDocumentIdentificationType || dto.identification) {
        const idError = window.IdentificationValidator
            && window.IdentificationValidator.validate(dto.idElectronicDocumentIdentificationType, dto.identification);
        if (idError) { alert(idError); return; }
    }

    const btn = document.getElementById('btnSaveVendor');
    btn.disabled = true;
    try {
        if (dto.id) {
            await vdFetch(`/api/Vendor/${dto.id}`, { method: 'PUT', body: JSON.stringify(dto) });
        } else {
            const created = await vdFetch('/api/Vendor', { method: 'POST', body: JSON.stringify(dto) });
            VD_CURRENT_ID = created.id;
            document.getElementById('fldVendorId').value = created.id;
            document.getElementById('activitiesSection').classList.remove('d-none');
            document.getElementById('activitiesHint').classList.add('d-none');
            document.getElementById('vendorModalTitle').innerHTML = '<i class="bi bi-pencil me-2"></i>Editar Vendor';
        }
        await loadVendors();
        if (dto.id) {
            bootstrap.Modal.getInstance(document.getElementById('vendorModal'))?.hide();
        }
    } catch (e) {
        alert('Error al guardar: ' + e.message);
    } finally {
        btn.disabled = false;
    }
}

async function deactivateVendor(id) {
    if (!confirm('¿Desactivar este vendor?')) return;
    try {
        await vdFetch(`/api/Vendor/${id}`, { method: 'DELETE' });
        await loadVendors();
    } catch (e) {
        alert('Error al desactivar: ' + e.message);
    }
}

// -------- Actividades económicas --------

function renderActivities(activities) {
    const body = document.getElementById('activitiesBody');
    if (!activities || activities.length === 0) {
        body.innerHTML = '<tr><td colspan="4" class="text-center text-muted p-2">Sin actividades registradas.</td></tr>';
        return;
    }
    body.innerHTML = activities.map(a => `
        <tr>
            <td>${vdEsc(a.economicActivityCode)}</td>
            <td>${vdEsc(a.description) || '<span class="text-muted">—</span>'}</td>
            <td class="text-center">${a.isDefault
                ? '<span class="badge bg-primary">Predeterminada</span>'
                : `<button class="btn btn-sm btn-outline-light" onclick="setDefaultActivity(${a.id})">Marcar</button>`}</td>
            <td class="text-center">
                <button class="btn btn-sm btn-outline-danger" onclick="deleteActivity(${a.id})" title="Eliminar"><i class="bi bi-trash"></i></button>
            </td>
        </tr>`).join('');
}

async function reloadActivities() {
    if (!VD_CURRENT_ID) return;
    const list = await vdFetch(`/api/Vendor/${VD_CURRENT_ID}/economic-activities`);
    renderActivities(list || []);
}

// Carga el catálogo central de actividades económicas en el selector (una sola vez).
let VD_ACTIVITY_CATALOG_LOADED = false;
async function loadActivityCatalog() {
    if (VD_ACTIVITY_CATALOG_LOADED) return;
    const sel = document.getElementById('fldNewActivity');
    if (!sel) return;
    try {
        const list = await vdFetch('/api/electronicdocumenteconomicactivity/active');
        sel.innerHTML = '<option value="">Seleccione una actividad económica...</option>' +
            (list || []).map(a => `<option value="${a.id}">${vdEsc(a.code)} — ${vdEsc(a.description)}</option>`).join('');
        VD_ACTIVITY_CATALOG_LOADED = true;
    } catch (e) {
        console.error('Error al cargar catálogo de actividades', e);
    }
}

async function addActivity() {
    if (!VD_CURRENT_ID) { alert('Guarde el vendor primero.'); return; }
    const sel = document.getElementById('fldNewActivity');
    const idActivity = parseInt(sel.value, 10);
    if (!idActivity) { alert('Seleccione una actividad económica.'); return; }
    try {
        await vdFetch(`/api/Vendor/${VD_CURRENT_ID}/economic-activities`, {
            method: 'POST',
            body: JSON.stringify({ idElectronicDocumentEconomicActivity: idActivity })
        });
        sel.value = '';
        await reloadActivities();
    } catch (e) {
        alert('Error al agregar actividad: ' + e.message);
    }
}

async function setDefaultActivity(activityId) {
    try {
        await vdFetch(`/api/Vendor/${VD_CURRENT_ID}/economic-activities/${activityId}/default`, { method: 'PUT' });
        await reloadActivities();
    } catch (e) {
        alert('Error: ' + e.message);
    }
}

async function deleteActivity(activityId) {
    if (!confirm('¿Eliminar esta actividad económica?')) return;
    try {
        await vdFetch(`/api/Vendor/${VD_CURRENT_ID}/economic-activities/${activityId}`, { method: 'DELETE' });
        await reloadActivities();
    } catch (e) {
        alert('Error: ' + e.message);
    }
}
