// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/transportUnits.js
// PROPÓSITO: Lógica cliente para Gestión de Unidades de Transporte (Fleet Management)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-14
// ================================================================================

'use strict';

// ── Namespace ─────────────────────────────────────────────────
const FL = {
    page:        1,
    pageSize:    20,
    search:      '',
    type:        '',
    status:      '',
    units:       [],
    currentUnit: null,
};

// ── API helpers ───────────────────────────────────────────────

async function flFetch(path, options = {}) {
    const url     = (window.FL_API || '').replace(/\/$/, '') + path;
    const headers = {
        'Content-Type':  'application/json',
        'Authorization': `Bearer ${window.FL_TOKEN || ''}`,
        ...(options.headers || {}),
    };
    const res = await fetch(url, { ...options, headers });
    if (!res.ok) {
        let msg = `HTTP ${res.status}`;
        try { const body = await res.json(); msg = body.error || body.message || msg; } catch {}
        throw new Error(msg);
    }
    return res.status === 204 ? null : res.json();
}

// ── Utilities ─────────────────────────────────────────────────

function flFmt(val, decimals = 0) {
    if (val == null || val === '') return '—';
    return Number(val).toLocaleString('es-CR', { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
}

function flDate(str) {
    if (!str) return '—';
    try { return new Date(str).toLocaleDateString('es-CR'); } catch { return str; }
}

function flDaysUntil(dateStr) {
    if (!dateStr) return null;
    const d = new Date(dateStr);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return Math.round((d - today) / 86400000);
}

function statusBadge(status) {
    const map = {
        Active:       ['status-active',       'bi-check-circle', 'Activo'],
        Maintenance:  ['status-maintenance',  'bi-wrench',       'Mantenimiento'],
        OutOfService: ['status-outofservice', 'bi-x-circle',     'Fuera de Servicio'],
        Retired:      ['status-retired',      'bi-archive',      'Retirado'],
    };
    const [cls, icon, label] = map[status] || ['status-retired', 'bi-question', status];
    return `<span class="status-badge ${cls}"><i class="bi ${icon}"></i>${label}</span>`;
}

function typeBadge(type) {
    const map = {
        Truck:      ['🚛', 'Camión'],
        Van:        ['🚐', 'Van'],
        Car:        ['🚗', 'Auto'],
        Pickup:     ['🛻', 'Pickup'],
        Motorcycle: ['🏍', 'Moto'],
        Forklift:   ['🏗', 'Montacargas'],
        Trailer:    ['🚜', 'Remolque'],
        Other:      ['⬜', 'Otro'],
    };
    const [emoji, label] = map[type] || ['⬜', type];
    return `<span class="type-badge">${emoji} ${label}</span>`;
}

function maintTypeName(type) {
    const map = {
        OilChange: '🛢 Aceite', TireChange: '🔵 Llantas', TireRotation: '🔄 Rot. Llantas',
        BrakeService: '🛑 Frenos', FilterChange: '🔧 Filtros', BatteryReplacement: '🔋 Batería',
        Inspection: '🔍 Inspección', Revision: '⚙ Revisión', Repair: '🔨 Reparación',
        Wash: '💧 Lavado', Insurance: '📋 Seguro', Other: '📌 Otro',
    };
    return map[type] || type;
}

function inspectionAlert(dateStr) {
    if (!dateStr) return '';
    const days = flDaysUntil(dateStr);
    if (days === null) return '';
    if (days < 0)   return `<span class="fl-alert-pill fl-alert-danger"><i class="bi bi-exclamation-triangle"></i>Vencida hace ${Math.abs(days)} días</span>`;
    if (days <= 30) return `<span class="fl-alert-pill fl-alert-warning"><i class="bi bi-clock"></i>Vence en ${days} días</span>`;
    return '';
}

function showFlAlert(msg, type = 'success') {
    const div = document.createElement('div');
    div.className = `alert alert-${type} alert-dismissible fade show`;
    div.style.cssText = 'position:fixed;top:70px;right:1.5rem;z-index:9999;min-width:300px;max-width:450px;font-size:.88rem;';
    div.innerHTML = `${msg}<button type="button" class="btn-close" data-bs-dismiss="alert"></button>`;
    document.body.appendChild(div);
    setTimeout(() => { try { new bootstrap.Alert(div).close(); } catch { div.remove(); } }, 4000);
}

// ── KPI Counters ──────────────────────────────────────────────

function updateKpis(items, total) {
    const active  = items.filter(u => u.status === 'Active').length;
    const maint   = items.filter(u => u.status === 'Maintenance').length;
    const alerts  = items.filter(u => {
        const insp = flDaysUntil(u.nextInspectionDate);
        const ins  = flDaysUntil(u.insuranceExpiredDate);
        return (insp !== null && insp <= 30) || (ins !== null && ins <= 30);
    }).length;
    document.getElementById('kpiTotal').textContent       = total;
    document.getElementById('kpiActive').textContent      = active;
    document.getElementById('kpiMaintenance').textContent = maint;
    document.getElementById('kpiAlerts').textContent      = alerts;
}

// ── Load units ────────────────────────────────────────────────

async function loadUnits(page = 1) {
    FL.page = page;
    const tbody = document.getElementById('flTableBody');
    tbody.innerHTML = '<tr><td colspan="11" class="fl-empty"><span class="spinner-border spinner-border-sm me-2"></span>Cargando…</td></tr>';

    try {
        const params = new URLSearchParams({
            page: FL.page, pageSize: FL.pageSize,
            ...(FL.search ? { search: FL.search } : {}),
            ...(FL.type   ? { type:   FL.type }   : {}),
            ...(FL.status ? { status: FL.status }  : {}),
        });
        const data = await flFetch(`/api/transportunit?${params}`);
        FL.units = data.items || [];
        renderTable(FL.units);
        renderPagination(data.total, data.page, data.pageSize);
        updateKpis(FL.units, data.total);
    } catch (e) {
        tbody.innerHTML = `<tr><td colspan="11" class="fl-empty"><i class="bi bi-exclamation-triangle text-danger"></i>${e.message}</td></tr>`;
    }
}

function renderTable(units) {
    const tbody = document.getElementById('flTableBody');
    if (!units.length) {
        tbody.innerHTML = '<tr><td colspan="11" class="fl-empty"><i class="bi bi-truck"></i>No se encontraron unidades de transporte</td></tr>';
        return;
    }
    tbody.innerHTML = units.map(u => {
        const inspDay = flDaysUntil(u.nextInspectionDate);
        const insDay  = flDaysUntil(u.insuranceExpiredDate);
        const alerts = [
            (inspDay !== null && inspDay <= 30 && inspDay >= 0) ? `<i class="bi bi-clock text-warning" title="Revisión en ${inspDay}d"></i>` : '',
            (inspDay !== null && inspDay < 0)  ? `<i class="bi bi-exclamation-triangle text-danger" title="Revisión vencida"></i>` : '',
            (insDay  !== null && insDay <= 30 && insDay >= 0) ? `<i class="bi bi-shield-exclamation text-warning" title="Seguro en ${insDay}d"></i>` : '',
            (insDay  !== null && insDay < 0)   ? `<i class="bi bi-shield-x text-danger" title="Seguro vencido"></i>` : '',
        ].join(' ');

        return `<tr data-id="${u.id}">
            <td><span class="fw-semibold" style="color:#a78bfa;">${u.code}</span></td>
            <td><span class="fw-bold text-light">${u.plateNumber}</span></td>
            <td>
                <div class="fw-semibold text-light">${u.name}</div>
                ${alerts ? `<div class="mt-1" style="font-size:.75rem;">${alerts}</div>` : ''}
            </td>
            <td>${typeBadge(u.unitType)}</td>
            <td>${u.brandName || '—'}${u.modelName ? ` <span style="color:var(--fl-muted);">${u.modelName}</span>` : ''}</td>
            <td class="text-center">${u.year || '—'}</td>
            <td class="text-end">${u.maxLoadKg ? flFmt(u.maxLoadKg, 0) + ' kg' : '—'}</td>
            <td class="text-end"><span style="color:#06b6d4;">${flFmt(u.currentOdometerKm, 0)} km</span></td>
            <td class="text-center">
                ${u.nextInspectionDate
                    ? `<span style="font-size:.8rem;${inspDay !== null && inspDay < 0 ? 'color:#f87171;' : inspDay !== null && inspDay <= 30 ? 'color:#fbbf24;' : 'color:#94a3b8;'}">${flDate(u.nextInspectionDate)}</span>`
                    : '<span style="color:#4b5563;">—</span>'}
            </td>
            <td>${statusBadge(u.status)}</td>
            <td class="text-center" style="white-space:nowrap;">
                <button class="btn btn-sm btn-outline-primary btn-action" data-action="edit" data-id="${u.id}" title="Editar" style="padding:.2rem .5rem;font-size:.75rem;">
                    <i class="bi bi-pencil"></i>
                </button>
                <button class="btn btn-sm btn-outline-warning btn-action ms-1" data-action="maint" data-id="${u.id}" title="Mantenimiento" style="padding:.2rem .5rem;font-size:.75rem;">
                    <i class="bi bi-wrench"></i>
                </button>
                <button class="btn btn-sm btn-outline-danger btn-action ms-1" data-action="delete" data-id="${u.id}" title="Dar de Baja" style="padding:.2rem .5rem;font-size:.75rem;">
                    <i class="bi bi-trash"></i>
                </button>
            </td>
        </tr>`;
    }).join('');

    tbody.querySelectorAll('tr[data-id]').forEach(row => {
        row.addEventListener('click', e => {
            if (e.target.closest('.btn-action')) return;
            openUnitModal(+row.dataset.id);
        });
    });

    tbody.querySelectorAll('.btn-action').forEach(btn => {
        btn.addEventListener('click', e => {
            e.stopPropagation();
            const id = +btn.dataset.id;
            if (btn.dataset.action === 'edit')   openUnitModal(id);
            if (btn.dataset.action === 'maint')  openUnitModal(id, 'maint');
            if (btn.dataset.action === 'delete') openDeleteModal(id);
        });
    });
}

function renderPagination(total, page, pageSize) {
    const totalPages = Math.max(1, Math.ceil(total / pageSize));
    const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
    const to   = Math.min(page * pageSize, total);
    document.getElementById('flPaginInfo').textContent = total === 0
        ? 'Sin resultados'
        : `Mostrando ${from}–${to} de ${total} unidades`;

    const ul = document.getElementById('flPagination');
    ul.innerHTML = '';
    if (totalPages <= 1) return;

    const addPage = (label, p, disabled = false, active = false) => {
        const li = document.createElement('li');
        li.className = `page-item${disabled ? ' disabled' : ''}${active ? ' active' : ''}`;
        li.innerHTML = `<button class="page-link">${label}</button>`;
        if (!disabled && !active) li.querySelector('button').addEventListener('click', () => loadUnits(p));
        ul.appendChild(li);
    };

    addPage('«', 1, page === 1);
    addPage('‹', page - 1, page === 1);
    const start = Math.max(1, page - 2);
    const end   = Math.min(totalPages, page + 2);
    for (let p = start; p <= end; p++) addPage(p, p, false, p === page);
    addPage('›', page + 1, page === totalPages);
    addPage('»', totalPages, page === totalPages);
}

// ── Open / populate unit modal ────────────────────────────────

async function openUnitModal(id = null, activeTab = 'info') {
    const title        = document.getElementById('unitModalTitle');
    const maintNavItem = document.getElementById('tabMaintenanceNavItem');
    resetUnitForm();

    if (id) {
        title.innerHTML = '<i class="bi bi-truck me-2 text-info"></i>Editar Unidad de Transporte';
        maintNavItem.style.display = '';
        try {
            const u = await flFetch(`/api/transportunit/${id}`);
                FL.currentUnit = u;
                await populateUnitForm(u);
                renderMaintenanceTable(u.maintenance || []);
            if (activeTab === 'maint') {
                const tabBtn = document.querySelector('[data-bs-target="#tabMaintenanceLog"]');
                if (tabBtn) new bootstrap.Tab(tabBtn).show();
            }
        } catch (e) {
            showFlAlert(`Error cargando unidad: ${e.message}`, 'danger');
            return;
        }
    } else {
        title.innerHTML = '<i class="bi bi-plus-circle me-2 text-warning"></i>Nueva Unidad de Transporte';
        maintNavItem.style.display = 'none';
        FL.currentUnit = null;
    }

    new bootstrap.Modal(document.getElementById('unitModal')).show();
}

function resetUnitForm() {
    ['fldUnitId','fldCode','fldPlate','fldName','fldColor',
     'fldYear','fldVin','fldEngine','fldNotes',
     'fldMaxLoad','fldMaxVol','fldPallets','fldCargoL','fldCargoW','fldCargoH',
     'fldOdometer','fldOdometerDate','fldInspection',
     'fldInsuranceExpiry','fldPolicy'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = '';
    });
    const drvSel = document.getElementById('fldDriver');
    if (drvSel) drvSel.value = '';
    const insSel = document.getElementById('fldInsurer');
    if (insSel) insSel.value = '';
    // Default to first available option in catalog-driven selects
    const typeSel   = document.getElementById('fldType');
    const statusSel = document.getElementById('fldStatus');
    if (typeSel)   typeSel.selectedIndex   = typeSel.options.length > 1 ? 1 : 0;
    if (statusSel) statusSel.selectedIndex = statusSel.options.length > 1 ? 1 : 0;
    document.getElementById('fldFuel').value  = '';
    document.getElementById('fldBrand').value = '';
    loadModelsByBrand(null, null);
    document.getElementById('volCalc').textContent = '';
    document.getElementById('inspectionAlert').innerHTML = '';
    document.getElementById('insuranceAlert').innerHTML  = '';
    const firstTab = document.querySelector('[data-bs-target="#tabIdentification"]');
    if (firstTab) new bootstrap.Tab(firstTab).show();
}

async function populateUnitForm(u) {
    const set = (id, val) => { const el = document.getElementById(id); if (el) el.value = val ?? ''; };
    set('fldUnitId',       u.id);
    set('fldCode',         u.code);
    set('fldPlate',        u.plateNumber);
    set('fldName',         u.name);
    set('fldType',         u.unitType);
    set('fldStatus',       u.status);
    set('fldColor',        u.colorHex);
    set('fldYear',         u.year);
    set('fldFuel',         u.fuelType);
    set('fldVin',          u.vinNumber);
    set('fldEngine',       u.engineNumber);
    const drvSel = document.getElementById('fldDriver');
    if (drvSel) drvSel.value = u.idDriver ?? '';
    set('fldNotes',        u.notes);
    set('fldMaxLoad',      u.maxLoadKg);
    set('fldMaxVol',       u.maxVolumeM3);
    set('fldPallets',      u.palletCapacity);
    set('fldCargoL',       u.cargoLengthM);
    set('fldCargoW',       u.cargoWidthM);
    set('fldCargoH',       u.cargoHeightM);
    set('fldOdometer',     u.currentOdometerKm);
    set('fldOdometerDate', u.lastOdometerDate?.substring(0, 10));
    set('fldInspection',   u.nextInspectionDate?.substring(0, 10));
    set('fldInsuranceExpiry', u.insuranceExpiredDate?.substring(0, 10));
    set('fldPolicy',       u.insurancePolicyNumber);
    // Brand + dependent model
    set('fldBrand', u.idVehicleBrand);
    await loadModelsByBrand(u.idVehicleBrand, u.idVehicleModel);
    // Insurer
    const insSel = document.getElementById('fldInsurer');
    if (insSel) insSel.value = u.idInsurer ?? '';
    updateVolCalc();
    updateDateAlerts();
}

function buildUnitDto() {
    const gv = id => document.getElementById(id)?.value?.trim() || null;
    const gn = id => { const v = document.getElementById(id)?.value; return v !== '' && v != null ? +v : null; };
    const brandSel  = document.getElementById('fldBrand');
    const modelSel  = document.getElementById('fldModel');
    const insurSel  = document.getElementById('fldInsurer');
    return {
        code:                  gv('fldCode')         || '',
        plateNumber:           gv('fldPlate')        || '',
        name:                  gv('fldName')         || '',
        unitType:              document.getElementById('fldType').value,
        status:                document.getElementById('fldStatus').value,
        colorHex:              gv('fldColor'),
        idVehicleBrand:        brandSel?.value ? +brandSel.value : null,
        brandName:             brandSel?.selectedOptions[0]?.textContent?.trim() || null,
        idVehicleModel:        modelSel?.value ? +modelSel.value : null,
        modelName:             modelSel?.selectedOptions[0]?.textContent?.trim() || null,
        year:                  gn('fldYear'),
        fuelType:              document.getElementById('fldFuel').value || null,
        vinNumber:             gv('fldVin'),
        engineNumber:          gv('fldEngine'),
        idDriver:              gn('fldDriver') || null,
        assignedDriverName:    document.getElementById('fldDriver')?.selectedOptions[0]?.textContent?.trim() || null,
        notes:                 gv('fldNotes'),
        maxLoadKg:             gn('fldMaxLoad'),
        maxVolumeM3:           gn('fldMaxVol'),
        palletCapacity:        gn('fldPallets'),
        cargoLengthM:          gn('fldCargoL'),
        cargoWidthM:           gn('fldCargoW'),
        cargoHeightM:          gn('fldCargoH'),
        currentOdometerKm:     gn('fldOdometer'),
        lastOdometerDate:      gv('fldOdometerDate')    ? new Date(gv('fldOdometerDate')).toISOString()    : null,
        nextInspectionDate:    gv('fldInspection')      ? new Date(gv('fldInspection')).toISOString()      : null,
        insuranceExpiredDate:  gv('fldInsuranceExpiry') ? new Date(gv('fldInsuranceExpiry')).toISOString() : null,
        idInsurer:             insurSel?.value ? +insurSel.value : null,
        insurerName:           insurSel?.selectedOptions[0]?.textContent?.trim() || null,
        insurancePolicyNumber: gv('fldPolicy'),
    };
}

// ── Validation ────────────────────────────────────────────────

function validateUnitForm(dto) {
    // Each rule: [condition, message, tabTarget]
    // tabTarget = data-bs-target value of the tab that contains the field
    const rules = [
        // Identification tab
        [!dto.code,              'El Código es obligatorio.',                      '#tabIdentification'],
        [!dto.plateNumber,       'La Placa es obligatoria.',                       '#tabIdentification'],
        [!dto.name,              'El Nombre de la Unidad es obligatorio.',         '#tabIdentification'],
        [!dto.unitType,          'El Tipo es obligatorio.',                        '#tabIdentification'],
        [!dto.status,            'El Estado es obligatorio.',                      '#tabIdentification'],
        [!dto.idVehicleBrand,    'La Marca es obligatoria.',                       '#tabIdentification'],
        [!dto.idVehicleModel,    'El Modelo es obligatorio.',                      '#tabIdentification'],
        [!dto.year,              'El Año es obligatorio.',                         '#tabIdentification'],
        [!dto.fuelType,          'El Tipo de Combustible es obligatorio.',         '#tabIdentification'],
        [!dto.vinNumber,         'El Número de Chasis / VIN es obligatorio.',      '#tabIdentification'],
        [!dto.engineNumber,      'El Número de Motor es obligatorio.',             '#tabIdentification'],
        [!dto.idDriver,          'El Conductor Asignado es obligatorio.',          '#tabIdentification'],
        // Operation tab
        [dto.currentOdometerKm === null, 'El Kilometraje Actual es obligatorio.', '#tabOperation'],
        [!dto.lastOdometerDate,  'La Fecha Última Lectura es obligatoria.',        '#tabOperation'],
    ];

    for (const [fail, msg, tabTarget] of rules) {
        if (fail) {
            // Switch to the tab that contains the failing field
            const tabBtn = document.querySelector(`[data-bs-target="${tabTarget}"]`);
            if (tabBtn) new bootstrap.Tab(tabBtn).show();
            showFlAlert(msg, 'warning');
            return false;
        }
    }
    return true;
}

// ── Save unit ─────────────────────────────────────────────────

async function saveUnit() {
    const btn = document.getElementById('btnSaveUnit');
    const id  = document.getElementById('fldUnitId').value;
    const dto = buildUnitDto();

    if (!validateUnitForm(dto)) return;

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Guardando…';

    try {
        if (id) {
            await flFetch(`/api/transportunit/${id}`, { method: 'PUT', body: JSON.stringify(dto) });
            showFlAlert('Unidad de transporte actualizada correctamente.', 'success');
        } else {
            await flFetch('/api/transportunit', { method: 'POST', body: JSON.stringify(dto) });
            showFlAlert('Unidad de transporte creada correctamente.', 'success');
        }
        bootstrap.Modal.getInstance(document.getElementById('unitModal')).hide();
        loadUnits(FL.page);
    } catch (e) {
        showFlAlert(e.message, 'danger');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-floppy me-1"></i>Guardar';
    }
}

// ── Delete unit ───────────────────────────────────────────────

function openDeleteModal(id) {
    const u = FL.units.find(x => x.id === id);
    document.getElementById('deleteUnitId').value    = id;
    document.getElementById('deleteUnitName').textContent = u ? `${u.plateNumber} — ${u.name}` : `#${id}`;
    new bootstrap.Modal(document.getElementById('deleteUnitModal')).show();
}

async function deleteUnit() {
    const id  = document.getElementById('deleteUnitId').value;
    const btn = document.getElementById('btnConfirmDeleteUnit');
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Procesando…';
    try {
        await flFetch(`/api/transportunit/${id}`, { method: 'DELETE' });
        bootstrap.Modal.getInstance(document.getElementById('deleteUnitModal')).hide();
        showFlAlert('Unidad dada de baja correctamente.', 'success');
        loadUnits(FL.page);
    } catch (e) {
        showFlAlert(e.message, 'danger');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-trash me-1"></i>Dar de Baja';
    }
}

// ── Maintenance ───────────────────────────────────────────────

function renderMaintenanceTable(records) {
    const tbody = document.getElementById('maintTableBody');
    if (!records.length) {
        tbody.innerHTML = '<tr><td colspan="8" class="text-center" style="padding:1.5rem;color:var(--fl-muted);">Sin registros de mantenimiento</td></tr>';
        return;
    }
    tbody.innerHTML = records.map(m => `
        <tr>
            <td>${flDate(m.maintenanceDate)}</td>
            <td><span class="maint-type-badge">${maintTypeName(m.maintenanceType)}</span></td>
            <td style="max-width:200px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" title="${m.description}">${m.description}</td>
            <td class="text-end">${m.odometerAtServiceKm != null ? flFmt(m.odometerAtServiceKm, 0) + ' km' : '—'}</td>
            <td class="text-center" style="font-size:.8rem;">
                ${m.nextServiceDate ? flDate(m.nextServiceDate) : ''}
                ${m.nextServiceKm   ? `<br><span style="color:#06b6d4;">${flFmt(m.nextServiceKm,0)} km</span>` : ''}
                ${!m.nextServiceDate && !m.nextServiceKm ? '—' : ''}
            </td>
            <td class="text-end">${m.cost != null ? `${m.currency || 'USD'} ${flFmt(m.cost, 2)}` : '—'}</td>
            <td style="font-size:.8rem;color:var(--fl-muted);">${m.vendorName || '—'}</td>
            <td class="text-center" style="white-space:nowrap;">
                <button class="btn btn-sm btn-outline-primary" style="padding:.15rem .4rem;font-size:.72rem;" onclick="openMaintModal(${m.idTransportUnit}, ${m.id})" title="Editar">
                    <i class="bi bi-pencil"></i>
                </button>
                <button class="btn btn-sm btn-outline-danger ms-1" style="padding:.15rem .4rem;font-size:.72rem;" onclick="deleteMaintenance(${m.idTransportUnit}, ${m.id})" title="Eliminar">
                    <i class="bi bi-trash"></i>
                </button>
            </td>
        </tr>`).join('');
}

function openMaintModal(unitId, maintId = null) {
    resetMaintForm();
    document.getElementById('fldMaintUnitId').value = unitId;
    document.getElementById('fldMaintDate').value   = new Date().toISOString().substring(0, 10);

    if (maintId) {
        const u = FL.currentUnit;
        const m = (u?.maintenance || []).find(x => x.id === maintId);
        if (m) {
            document.getElementById('fldMaintId').value           = m.id;
            document.getElementById('fldMaintType').value         = m.maintenanceType;
            document.getElementById('fldMaintDate').value         = m.maintenanceDate?.substring(0, 10) || '';
            document.getElementById('fldMaintKm').value           = m.odometerAtServiceKm ?? '';
            document.getElementById('fldMaintDesc').value         = m.description || '';
            document.getElementById('fldMaintNextDate').value     = m.nextServiceDate?.substring(0, 10) || '';
            document.getElementById('fldMaintNextKm').value       = m.nextServiceKm ?? '';
            document.getElementById('fldMaintCost').value         = m.cost ?? '';
            document.getElementById('fldMaintCurrency').value     = m.currency || 'USD';
            document.getElementById('fldMaintVendor').value     = m.vendorName || '';
            document.getElementById('fldMaintInvoice').value      = m.invoiceNumber || '';
            document.getElementById('fldMaintOutOfService').checked = !!m.vehicleOutOfService;
            document.getElementById('fldMaintNotes').value        = m.notes || '';
        }
        document.getElementById('maintModalTitle').innerHTML = '<i class="bi bi-wrench me-2 text-warning"></i>Editar Mantenimiento';
    } else {
        if (FL.currentUnit?.currentOdometerKm) {
            document.getElementById('fldMaintKm').value = FL.currentUnit.currentOdometerKm;
        }
        document.getElementById('maintModalTitle').innerHTML = '<i class="bi bi-wrench me-2 text-warning"></i>Registrar Mantenimiento';
    }
    new bootstrap.Modal(document.getElementById('maintModal')).show();
}

function resetMaintForm() {
    ['fldMaintId','fldMaintKm','fldMaintDesc','fldMaintNextDate','fldMaintNextKm',
     'fldMaintCost','fldMaintVendor','fldMaintInvoice','fldMaintNotes'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = '';
    });
    document.getElementById('fldMaintType').value     = 'OilChange';
    document.getElementById('fldMaintCurrency').value = 'USD';
    document.getElementById('fldMaintOutOfService').checked = false;
}

async function saveMaintenance() {
    const unitId  = +document.getElementById('fldMaintUnitId').value;
    const maintId = document.getElementById('fldMaintId').value;
    const btn     = document.getElementById('btnSaveMaint');

    const gv = id => document.getElementById(id)?.value?.trim() || null;
    const gn = id => { const v = document.getElementById(id)?.value; return v !== '' && v != null ? +v : null; };

    const dto = {
        maintenanceType:     document.getElementById('fldMaintType').value,
        description:         gv('fldMaintDesc') || '',
        maintenanceDate:     gv('fldMaintDate') || new Date().toISOString().substring(0, 10),
        odometerAtServiceKm: gn('fldMaintKm'),
        nextServiceDate:     gv('fldMaintNextDate'),
        nextServiceKm:       gn('fldMaintNextKm'),
        cost:                gn('fldMaintCost'),
        currency:            document.getElementById('fldMaintCurrency').value,
        vendorName:          gv('fldMaintVendor'),
        invoiceNumber:       gv('fldMaintInvoice'),
        vehicleOutOfService: document.getElementById('fldMaintOutOfService').checked,
        notes:               gv('fldMaintNotes'),
    };

    if (!dto.description) { showFlAlert('La descripción es obligatoria.', 'warning'); return; }

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Guardando…';

    try {
        if (maintId) {
            await flFetch(`/api/transportunit/${unitId}/maintenance/${maintId}`, { method: 'PUT', body: JSON.stringify(dto) });
        } else {
            await flFetch(`/api/transportunit/${unitId}/maintenance`, { method: 'POST', body: JSON.stringify(dto) });
        }
        bootstrap.Modal.getInstance(document.getElementById('maintModal')).hide();
        showFlAlert('Mantenimiento guardado correctamente.', 'success');
        const updated = await flFetch(`/api/transportunit/${unitId}`);
        FL.currentUnit = updated;
        renderMaintenanceTable(updated.maintenance || []);
        loadUnits(FL.page);
    } catch (e) {
        showFlAlert(e.message, 'danger');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-floppy me-1"></i>Guardar Mantenimiento';
    }
}

async function deleteMaintenance(unitId, maintId) {
    if (!confirm('¿Eliminar este registro de mantenimiento?')) return;
    try {
        await flFetch(`/api/transportunit/${unitId}/maintenance/${maintId}`, { method: 'DELETE' });
        showFlAlert('Registro eliminado.', 'success');
        const updated = await flFetch(`/api/transportunit/${unitId}`);
        FL.currentUnit = updated;
        renderMaintenanceTable(updated.maintenance || []);
        loadUnits(FL.page);
    } catch (e) {
        showFlAlert(e.message, 'danger');
    }
}

// ── Dynamic helpers ───────────────────────────────────────────

function updateVolCalc() {
    const l = +document.getElementById('fldCargoL').value || 0;
    const w = +document.getElementById('fldCargoW').value || 0;
    const h = +document.getElementById('fldCargoH').value || 0;
    const div = document.getElementById('volCalc');
    if (l > 0 && w > 0 && h > 0) {
        const vol = l * w * h;
        div.innerHTML = `<span style="color:#06b6d4;"><i class="bi bi-calculator me-1"></i>Vol. calculado: <strong>${vol.toFixed(2)} m³</strong></span>`;
    } else {
        div.textContent = '';
    }
}

function updateDateAlerts() {
    document.getElementById('inspectionAlert').innerHTML = inspectionAlert(document.getElementById('fldInspection').value);
    document.getElementById('insuranceAlert').innerHTML  = inspectionAlert(document.getElementById('fldInsuranceExpiry').value);
}

// ── Debounce search ───────────────────────────────────────────

let _searchTimer = null;
function onSearchInput() {
    clearTimeout(_searchTimer);
    _searchTimer = setTimeout(() => { FL.search = document.getElementById('flSearch').value.trim(); loadUnits(1); }, 350);
}

// ── Load driver catalog ──────────────────────────────────────
async function loadDriverCatalog() {
    try {
        const drivers = await flFetch('/api/employees/drivers');
        const sel = document.getElementById('fldDriver');
        if (!sel) return;
        const cur = sel.value;
        sel.innerHTML = '<option value="">— Sin conductor asignado —</option>' +
            (drivers || []).map(d => `<option value="${d.id}">${d.fullName} (${d.code})</option>`).join('');
        if (cur) sel.value = cur;
    } catch { /* no crítico */ }
}

// ── Fleet catalog loaders ────────────────────────────────────

function fillSelect(selId, items, valueFn, textFn, emptyLabel) {
    const sel = document.getElementById(selId);
    if (!sel) return;
    const cur = sel.value;
    sel.innerHTML = `<option value="">${emptyLabel}</option>` +
        items.map(i => `<option value="${valueFn(i)}">${textFn(i)}</option>`).join('');
    if (cur) sel.value = cur;
}

async function loadFleetCatalogs() {
    try {
        const [types, statuses, fuels, brands] = await Promise.all([
            flFetch('/api/fleet-catalog/unit-types?isActive=true'),
            flFetch('/api/fleet-catalog/unit-statuses?isActive=true'),
            flFetch('/api/fleet-catalog/fuel-types?isActive=true'),
            flFetch('/api/fleet-catalog/brands?isActive=true'),
        ]);

        // Modal selects
        fillSelect('fldType',   types,    t => t.code, t => t.name, '— Seleccione tipo —');
        fillSelect('fldStatus', statuses, s => s.code, s => s.name, '— Seleccione estado —');
        fillSelect('fldFuel',   fuels,    f => f.code, f => f.name, '— Seleccione combustible —');
        fillSelect('fldBrand',  brands,   b => b.id,   b => b.name, '— Seleccione marca —');

        // Toolbar filters
        fillSelect('flFilterType',   types,    t => t.code, t => t.name, 'Todos los tipos');
        fillSelect('flFilterStatus', statuses, s => s.code, s => s.name, 'Todos los estados');

        // Restore toolbar filter values that may already be set
        if (FL.type)   document.getElementById('flFilterType').value   = FL.type;
        if (FL.status) document.getElementById('flFilterStatus').value = FL.status;
    } catch (e) {
        console.warn('Error cargando catálogos fleet:', e.message);
    }
}

async function loadModelsByBrand(brandId, currentModelId) {
    const sel = document.getElementById('fldModel');
    if (!sel) return;
    if (!brandId) {
        sel.innerHTML = '<option value="">— Seleccione marca primero —</option>';
        return;
    }
    sel.innerHTML = '<option value="">Cargando…</option>';
    try {
        const models = await flFetch(`/api/fleet-catalog/models?isActive=true&brandId=${brandId}`);
        sel.innerHTML = '<option value="">— Seleccione modelo —</option>' +
            (models || []).map(m => `<option value="${m.id}">${m.name}</option>`).join('');
        if (currentModelId) sel.value = currentModelId;
    } catch {
        sel.innerHTML = '<option value="">— Error cargando modelos —</option>';
    }
}

async function loadInsurerCatalog() {
    try {
        const data = await flFetch('/api/insurers?isActive=true&pageSize=200');
        const items = data.items || [];
        fillSelect('fldInsurer', items, i => i.id, i => i.name, '— Sin aseguradora —');
    } catch { /* no crítico */ }
}

// ── Bootstrap initialization ──────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('flSearch').addEventListener('input', onSearchInput);
    document.getElementById('flFilterType').addEventListener('change', e => { FL.type = e.target.value; loadUnits(1); });
    document.getElementById('flFilterStatus').addEventListener('change', e => { FL.status = e.target.value; loadUnits(1); });
    document.getElementById('btnRefreshUnits').addEventListener('click', () => loadUnits(FL.page));
    document.getElementById('btnNewUnit').addEventListener('click', () => openUnitModal());

    document.getElementById('btnSaveUnit').addEventListener('click', saveUnit);
    document.getElementById('btnConfirmDeleteUnit').addEventListener('click', deleteUnit);
    document.getElementById('btnSaveMaint').addEventListener('click', saveMaintenance);
    document.getElementById('btnAddMaintenance').addEventListener('click', () => {
        const id = +document.getElementById('fldUnitId').value;
        if (id) openMaintModal(id);
    });

    ['fldCargoL','fldCargoW','fldCargoH'].forEach(id =>
        document.getElementById(id).addEventListener('input', updateVolCalc));

    ['fldInspection','fldInsuranceExpiry'].forEach(id =>
        document.getElementById(id).addEventListener('change', updateDateAlerts));

    // Brand → Model dependency
    document.getElementById('fldBrand').addEventListener('change', e => {
        loadModelsByBrand(e.target.value || null, null);
    });

    // Load all catalogs on page load
    loadFleetCatalogs();
    loadDriverCatalog();
    loadInsurerCatalog();
    loadUnits(1);
});
