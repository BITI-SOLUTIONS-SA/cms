// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/inventoryMovements.js
// PROPÓSITO: Lógica cliente para la pantalla de Inventory Movements
// DESCRIPCIÓN: Gestiona la visualización, creación y flujo completo de movimientos
//              de inventario incluyendo el modo de tránsito (vehículo → bodegas).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-13
// ================================================================================

'use strict';

// ============================================================
// CONFIGURACIÓN GLOBAL
// ============================================================

const INV = {
    api:       window.INV_API   || '',
    token:     window.INV_TOKEN || '',
    page:      1,
    pageSize:  10,
    totalPages: 1,
    // Filtros activos
    filters:   { search: '', type: '', status: '', dateFrom: '', dateTo: '' },
    // Datos cacheados
    warehouses:    [],
    movementTypes: [],   // tipos cargados desde admin.inventory_transaction_type
    transactionStatuses: [], // estados cargados desde admin.inventory_transaction_status
    items:      [],
    // Estado del modal
    editingId:  null,
    lines:      [],        // líneas editables [{...}]
    _groups:    [],        // grupos de destino en modo tránsito [{destId, sealDest, departureTime, arrivalTime, odometerOut}]
    _vehicleOdometerKm: null, // km actual del vehículo de tránsito seleccionado
    // Movimiento actual en detalle
    currentTxn: null,
    cancelTargetId: null,
    // Parámetros globales (se recargan en tiempo real desde la BD cada vez que se necesitan)
    allowsReturnsInTransit: true,  // Default true, se recarga en openReceive()
};

// ============================================================
// HELPERS DE TIPO DE MOVIMIENTO (usando catálogo admin.inventory_transaction_type)
// Siempre leen de INV.movementTypes — NO hay mapa estático hardcodeado.
// ============================================================

/**
 * Devuelve { code, label, icon, css } para un id_inventory_transaction_type.
 */
function txnTypeInfo(id) {
    const t = INV.movementTypes.find(m => m.id == id);
    if (!t) return { code: '', label: 'Desconocido', icon: '📦', css: '' };
    return { code: t.code, label: t.name, icon: t.emoji || '📦', css: t.cssClass || '' };
}

/**
 * Retorna true si el id_inventory_transaction_type dado corresponde al code indicado.
 */
function txnTypeIs(id, code) {
    return txnTypeInfo(id).code === code;
}

/**
 * Retorna el id_inventory_transaction_type que tiene el code indicado, o null.
 */
function txnTypeIdByCode(code) {
    const t = INV.movementTypes.find(m => m.code === code);
    return t ? t.id : null;
}

// ============================================================
// HELPERS DE ESTADO (usando catálogo admin.inventory_transaction_status)
// Siempre leen de INV.transactionStatuses — NO hay mapa estático hardcodeado.
// ============================================================

/**
 * Devuelve { code, label, icon, css } para un idInventoryTransactionStatus.
 */
function txnStatusInfo(id) {
    const s = INV.transactionStatuses.find(x => x.id == id);
    if (!s) return { code: '', label: 'Desconocido', icon: '❓', css: 'st-Draft' };
    return { code: s.code, label: s.name, icon: s.emoji || '❓', css: s.cssClass || '' };
}

/**
 * Retorna true si el idInventoryTransactionStatus dado corresponde al code indicado.
 */
function txnStatusIs(id, code) {
    return txnStatusInfo(id).code === code;
}

/**
 * Retorna el idInventoryTransactionStatus que tiene el code indicado, o null.
 */
function txnStatusIdByCode(code) {
    const s = INV.transactionStatuses.find(x => x.code === code);
    return s ? s.id : null;
}

// ============================================================
// FETCH HELPER
// ============================================================

async function invFetch(path, options = {}) {
    const url = `${INV.api}${path}`;
    const opts = {
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${INV.token}`,
        },
        ...options,
    };
    if (opts.body && typeof opts.body !== 'string') opts.body = JSON.stringify(opts.body);
    const res = await fetch(url, opts);
    if (!res.ok) {
        let msg = `HTTP ${res.status}`;
        try { const d = await res.json(); msg = d.error || d.title || msg; } catch {}
        throw new Error(msg);
    }
    return res.json();
}

// ============================================================
// HTML ESCAPE HELPER
// ============================================================

function _escapeHtml(str) {
    return String(str ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

// ============================================================
// INIT
// ============================================================

document.addEventListener('DOMContentLoaded', async () => {
    await Promise.all([
        loadWarehouses(), 
        loadMovementTypes(), 
        loadTransactionStatuses(),
        loadGlobalParameters()
    ]);
    loadMovements();
    bindEvents();
});

// ============================================================
// CARGAR PARÁMETROS GLOBALES
// ============================================================

async function loadGlobalParameters() {
    try {
        // Menu ID 8 = "Warehouse & Distribution" (admin.menu.id_menu)
        const response = await invFetch('/api/globalparameters/8/allows_returns_received_in_transit/value');
        INV.allowsReturnsInTransit = response.value === true;
        console.log('✅ Global Parameter loaded: allowsReturnsInTransit =', INV.allowsReturnsInTransit);
    } catch (e) {
        console.warn('⚠️ No se pudo cargar el parámetro allows_returns_received_in_transit, usando valor por defecto (true):', e);
        INV.allowsReturnsInTransit = true;
    }
}

// ============================================================
// CARGAR BODEGAS (caché)
// ============================================================

async function loadWarehouses() {
    try {
        const data = await invFetch('/api/warehouse?isActive=true&pageSize=200');
        INV.warehouses = data.items || data || [];
    } catch (e) {
        console.warn('No se pudieron cargar bodegas:', e);
    }
}

// ============================================================
// CARGAR TIPOS DE MOVIMIENTO (caché + select dinámico)
// ============================================================

async function loadMovementTypes() {
    try {
        // ✅ Filtrar solo los tipos que deben mostrarse en Inventory Movements
        const types = await invFetch('/api/inventory-transaction-type?isActive=true&showInInventoryMovements=true');
        INV.movementTypes = types || [];

        // Poblar el select del modal (fldType)
        populateMovementTypeSelect('fldType');

        // Poblar el select de filtro en la lista (fType)
        populateMovementTypeSelect('fType', true);
    } catch (e) {
        console.warn('No se pudieron cargar tipos de movimiento:', e);
    }
}

function populateMovementTypeSelect(selectId, includeAll = false) {
    const sel = document.getElementById(selectId);
    if (!sel) return;
    const prev = sel.value;
    const allOpt = includeAll ? '<option value="">— Todos los tipos —</option>' : '';
    sel.innerHTML = allOpt + INV.movementTypes
        .map(t => `<option value="${t.id}">${t.emoji ? t.emoji + ' ' : ''}${t.name}</option>`)
        .join('');
    // Restaurar selección previa si sigue siendo válida
    if (prev && INV.movementTypes.some(t => t.id == prev)) sel.value = prev;
}

// ============================================================
// CARGAR ESTADOS DE TRANSACCIÓN (caché + select dinámico)
// ============================================================

async function loadTransactionStatuses() {
    try {
        const statuses = await invFetch('/api/inventory-transaction-status?isActive=true');
        INV.transactionStatuses = statuses || [];
        populateStatusSelect('fStatus', true);
    } catch (e) {
        console.warn('No se pudieron cargar estados de transacción:', e);
    }
}

function populateStatusSelect(selectId, includeAll = false) {
    const sel = document.getElementById(selectId);
    if (!sel) return;
    const prev = sel.value;
    const allOpt = includeAll ? '<option value="">Todos los estados</option>' : '';
    sel.innerHTML = allOpt + INV.transactionStatuses
        .filter(s => s.isActive !== false)
        .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0))
        .map(s => `<option value="${s.id}">${s.emoji ? s.emoji + ' ' : ''}${s.name}</option>`)
        .join('');
    if (prev && INV.transactionStatuses.some(s => s.id == prev)) sel.value = prev;
}

// ============================================================
// ============================================================

async function loadMovements(page = 1) {
    INV.page = page;
    const tb = document.getElementById('invTableBody');
    tb.innerHTML = '<tr><td colspan="8" class="text-center py-4" style="color:#cbd5e1;">Cargando…</td></tr>';

    try {
        const params = new URLSearchParams({
            page,
            pageSize: INV.pageSize,
        });
        if (INV.filters.search)   params.set('search',      INV.filters.search);
        if (INV.filters.type)     params.set('idInventoryTransactionType', INV.filters.type);
        if (INV.filters.status)   params.set('idInventoryTransactionStatus', INV.filters.status);
        if (INV.filters.dateFrom) params.set('dateFrom',     INV.filters.dateFrom);
        if (INV.filters.dateTo)   params.set('dateTo',       INV.filters.dateTo);

        const data = await invFetch(`/api/inventorytransaction?${params}`);
        const items = data.items || [];
        INV.totalPages = data.totalPages || 1;

        renderTable(items);
        renderPagination(data.totalCount || 0, data.page, data.totalPages);
        updateKpis(data.totalCount || 0, data.statusCounts || {});
    } catch (e) {
        tb.innerHTML = `<tr><td colspan="8" class="text-center py-4 text-danger">${e.message}</td></tr>`;
    }
}

// ============================================================
// RENDER TABLA
// ============================================================

function renderTable(items) {
    const tb = document.getElementById('invTableBody');
    if (!items.length) {
        tb.innerHTML = '<tr><td colspan="8" class="text-center py-4" style="color:#94a3b8;">No se encontraron movimientos.</td></tr>';
        return;
    }

    tb.innerHTML = items.map(t => {
        const typeM = txnTypeInfo(t.idInventoryTransactionType);
        const statM = txnStatusInfo(t.idInventoryTransactionStatus);
        const originName = warehouseName(t.idWarehouseOrigin);
        const destName   = t.idWarehouseDest ? warehouseName(t.idWarehouseDest) : '—';

        // Indicador de progreso de recepción para TransitTransfer
        let progressBadge = '';
        if (t.isTransitTransfer && t.totalGroups > 0) {
            const receivedGroups = t.receivedGroups || 0;
            const totalGroups = t.totalGroups || 0;
            const percentage = totalGroups > 0 ? Math.round((receivedGroups / totalGroups) * 100) : 0;
            const progressColor = receivedGroups === totalGroups
                ? '#22c55e' // verde completo
                : (receivedGroups > 0 ? '#f59e0b' : '#64748b'); // naranja parcial, gris ninguno
            progressBadge = `<br><small style="color:${progressColor};font-size:.7rem;font-weight:600;">
                <i class="bi bi-geo-alt-fill me-1"></i>${receivedGroups}/${totalGroups} Recibidas (${percentage}%)
            </small>`;
        }

        return `<tr onclick="openDetail(${t.id})" title="Ver detalle">
            <td>
                <span class="fw-semibold text-white">${t.transactionNumber}</span>
                ${t.isTransitTransfer ? '<span class="ms-1 badge-type tp-TransitTransfer" style="font-size:.65rem;padding:.1rem .3rem;border-radius:4px;"><i class="bi bi-truck"></i></span>' : ''}
                ${progressBadge}
            </td>
            <td><span class="badge-type ${typeM.css}">${typeM.icon} ${typeM.label}</span></td>
            <td><span class="badge-status ${statM.css}">${statM.icon} ${statM.label}</span></td>
            <td style="color:#cbd5e1;">${t.transactionDate || '—'}</td>
            <td class="text-light">${originName}</td>
            <td class="text-light">${destName}</td>
            <td class="hide-mobile" style="color:#94a3b8;">${t.reference || '—'}</td>
            <td onclick="event.stopPropagation()">
                ${actionButtons(t)}
            </td>
        </tr>`;
    }).join('');
}

function actionButtons(t) {
    const btns = [];
    if (txnStatusIs(t.idInventoryTransactionStatus, 'Draft')) {
        btns.push(`<button class="btn btn-sm btn-outline-warning me-1 btn-inv" onclick="openEdit(${t.id})" title="Editar"><i class="bi bi-pencil"></i></button>`);
        btns.push(`<button class="btn btn-sm btn-outline-info btn-inv" onclick="confirmTxn(${t.id})" title="Confirmar"><i class="bi bi-check2-circle"></i></button>`);
    }
    if (txnStatusIs(t.idInventoryTransactionStatus, 'InTransit') || txnStatusIs(t.idInventoryTransactionStatus, 'PartiallyReceived')) {
        btns.push(`<button class="btn btn-sm btn-outline-success btn-inv" onclick="openReceive(${t.id})" title="Recibir"><i class="bi bi-box-arrow-in-down"></i></button>`);
    }
    if (txnStatusIs(t.idInventoryTransactionStatus, 'Confirmed')) {
        btns.push(`<button class="btn btn-sm btn-outline-success btn-inv" onclick="completeTxn(${t.id})" title="Completar"><i class="bi bi-check-all"></i></button>`);
    }
    if (txnStatusIs(t.idInventoryTransactionStatus, 'Completed') || txnStatusIs(t.idInventoryTransactionStatus, 'Cancelled')) {
        btns.push(`<button class="btn btn-sm btn-outline-info btn-inv" onclick="openDetail(${t.id})" title="Ver detalle"><i class="bi bi-eye"></i></button>`);
    }
    if (!txnStatusIs(t.idInventoryTransactionStatus, 'Completed') && !txnStatusIs(t.idInventoryTransactionStatus, 'Cancelled')) {
        btns.push(`<button class="btn btn-sm btn-outline-danger btn-inv" onclick="openCancel(${t.id})" title="Cancelar"><i class="bi bi-x-circle"></i></button>`);
    }
    return btns.join('');
}

// ============================================================
// KPIs
// ============================================================

/**
 * Actualiza los chips de KPI con conteos globales del servidor.
 * @param {number} total - Total global de registros
 * @param {Object} statusCounts - Diccionario { statusId: count }
 */
function updateKpis(total, statusCounts = {}) {
    document.getElementById('kpiTotal').textContent = total;

    // Buscar los IDs de estado desde el catálogo cargado
    const draftId     = INV.transactionStatuses.find(s => s.code === 'Draft')?.id || 0;
    const transitId   = INV.transactionStatuses.find(s => s.code === 'InTransit')?.id || 0;
    const partialId   = INV.transactionStatuses.find(s => s.code === 'PartiallyReceived')?.id || 0;
    const completedId = INV.transactionStatuses.find(s => s.code === 'Completed')?.id || 0;
    const confirmedId = INV.transactionStatuses.find(s => s.code === 'Confirmed')?.id || 0;
    const cancelledId = INV.transactionStatuses.find(s => s.code === 'Cancelled')?.id || 0;

    // Usar los conteos del servidor (statusCounts es { statusId: count })
    document.getElementById('kpiDraft').textContent     = statusCounts[draftId] || 0;
    document.getElementById('kpiTransit').textContent   = statusCounts[transitId] || 0;
    document.getElementById('kpiPartial').textContent   = statusCounts[partialId] || 0;
    document.getElementById('kpiCompleted').textContent = statusCounts[completedId] || 0;
    document.getElementById('kpiConfirmed').textContent = statusCounts[confirmedId] || 0;
    document.getElementById('kpiCancelled').textContent = statusCounts[cancelledId] || 0;
}

// ============================================================
// PAGINACIÓN
// ============================================================

function renderPagination(total, page, totalPages) {
    document.getElementById('pageInfo').textContent =
        `Mostrando página ${page} de ${totalPages} (${total} registros)`;

    const ul = document.getElementById('pagination');
    ul.innerHTML = '';

    const addPage = (label, p, disabled = false, active = false) => {
        const li = document.createElement('li');
        li.className = `page-item${disabled ? ' disabled' : ''}${active ? ' active' : ''}`;
        li.innerHTML = `<a class="page-link" href="#">${label}</a>`;
        if (!disabled) li.addEventListener('click', e => { e.preventDefault(); loadMovements(p); });
        ul.appendChild(li);
    };

    addPage('‹', page - 1, page <= 1);
    const start = Math.max(1, page - 2), end = Math.min(totalPages, page + 2);
    for (let p = start; p <= end; p++) addPage(p, p, false, p === page);
    addPage('›', page + 1, page >= totalPages);
}

// ============================================================
// ABRIR MODAL NUEVO
// ============================================================

async function openCreate() {
    INV.editingId = null;
    INV.lines = [];

    document.getElementById('invModalTitle').innerHTML =
        '<i class="bi bi-plus-circle me-2 text-warning"></i>Nuevo Movimiento';
    document.getElementById('modalAlert').classList.add('d-none');

    // Número automático
    try {
        const { transactionNumber } = await invFetch('/api/inventorytransaction/next-number');
        document.getElementById('fldNumber').value = transactionNumber;
    } catch { document.getElementById('fldNumber').value = ''; }

    const today = new Date().toISOString().slice(0, 10);
    document.getElementById('fldDate').value    = today;
    document.getElementById('fldDate').min      = today;
    // Seleccionar el primer tipo disponible o el que tenga el code='Transfer' si se puede
    const defaultType = INV.movementTypes.find(t => t.code === 'Transfer') || INV.movementTypes[0];
    document.getElementById('fldType').value    = defaultType ? defaultType.id : '';
    document.getElementById('fldOrigin').value  = '';
    document.getElementById('fldDest').value    = '';
    document.getElementById('fldReference').value = '';
    document.getElementById('fldNotes').value   = '';
    INV._vehicleOdometerKm = null;

    // Limpiar campos de encabezado de tránsito
    const fldSealNew = document.getElementById('fldSecuritySeal');
    const fldDeptNew = document.getElementById('fldDepartureTime');
    const fldOdoNew  = document.getElementById('fldOdometerOut');
    const fldOdoErrNew  = document.getElementById('fldOdoErr');
    const fldOdoHintNew = document.getElementById('fldOdoHint');
    if (fldSealNew) fldSealNew.value = '';
    if (fldDeptNew) fldDeptNew.value = '';
    if (fldOdoNew)  { fldOdoNew.value = ''; fldOdoNew.style.borderColor = ''; }
    if (fldOdoErrNew) fldOdoErrNew.style.display = 'none';
    if (fldOdoHintNew) fldOdoHintNew.textContent = 'Km al salir hacia la bodega de tránsito';

    INV._groups = [{ destId: null, sealDest: '', departureTime: '', arrivalTime: '', odometerOut: '', notes: '' }];
    populateWarehouseSelects();
    updateTransitUI();
    renderLines();

    document.getElementById('btnSaveConfirm').classList.remove('d-none');
    bootstrap.Modal.getOrCreateInstance(document.getElementById('invModal')).show();
}

// ============================================================
// ABRIR MODAL EDITAR
// ============================================================

async function openEdit(id) {
    INV.editingId = id;
    INV.lines = [];

    document.getElementById('invModalTitle').innerHTML =
        '<i class="bi bi-pencil me-2 text-warning"></i>Editar Movimiento';
    document.getElementById('modalAlert').classList.add('d-none');

    try {
        const txn = await invFetch(`/api/inventorytransaction/${id}`);
        const lines = txn.lines || await invFetch(`/api/inventorytransaction/${id}/lines`);

        document.getElementById('fldNumber').value    = txn.transactionNumber;
        document.getElementById('fldDate').value      = txn.transactionDate;
        document.getElementById('fldDate').min        = '';
        document.getElementById('fldType').value      = txn.idInventoryTransactionType;
        document.getElementById('fldReference').value = txn.reference || '';
        document.getElementById('fldNotes').value     = txn.notes || '';
        document.getElementById('sealError').classList.add('d-none');
        INV._vehicleOdometerKm = null;

        // Guardar IDs a restaurar ANTES de que updateTransitUI() reconstruya los selects
        const savedDestId   = txn.idWarehouseDest || '';
        const savedOriginId = txn.idWarehouseOrigin || '';

        INV.lines = (lines || []).map(l => ({
            id:                                      l.id || 0,
            idItem:                                  l.idItem,
            itemCode:                                l.itemCode,
            itemName:                                l.itemName,
            qtyRequested:                            l.qtyRequested,
            qtyReturned:                             l.qtyReturned || 0,
            idInventoryTransactionWarehouseTransit:  l.idInventoryTransactionWarehouseTransit || null,
            idUnitOfMeasure:                         l.idUnitOfMeasure || null,
            unitOfMeasureCode:                       l.unitOfMeasureCode || '',
            unitCost:                                l.unitCost || null,
            lotNumber:                               l.lotNumber || '',
            notes:                                   l.notes || '',
        }));

        // Reconstruir grupos desde los transit groups cargados del API
        const isTransitMode = txnTypeIs(txn.idInventoryTransactionType, 'TransitTransfer');
        if (isTransitMode) {
            let serverGroups = [];
            try {
                serverGroups = await invFetch(`/api/inventorytransaction/${id}/transit-groups`);
            } catch (_) { serverGroups = []; }

            if (serverGroups.length > 0) {
                INV._groups = serverGroups.map(g => ({
                    id:            g.id,
                    destId:        g.idWarehouseDestLine || null,
                    sealDest:      g.destSecuritySeal   || '',
                    departureTime: g.departureTime      || '',
                    arrivalTime:   g.arrivalTime        || '',
                    odometerOut:   g.odometerOut        != null ? g.odometerOut : '',
                    notes:         g.notes              || '',
                }));
            } else {
                INV._groups = [{ id: null, destId: null, sealDest: '', departureTime: '', arrivalTime: '', odometerOut: '', notes: '' }];
            }

            // Derivar idWarehouseDestLine en cada línea a partir del grupo de tránsito al que pertenece.
            // El campo idInventoryTransactionWarehouseTransit coincide con el id del grupo (INV._groups[].id).
            // Sin este paso, renderTransitGroups no puede asignar líneas a grupos y todos quedan vacíos,
            // especialmente visible al reordenar grupos con las flechas arriba/abajo.
            INV.lines.forEach(l => {
                if (l.idInventoryTransactionWarehouseTransit) {
                    const grp = INV._groups.find(g => g.id === l.idInventoryTransactionWarehouseTransit);
                    if (grp) l.idWarehouseDestLine = grp.destId;
                }
            });
        } else {
            INV._groups = [{ id: null, destId: null, sealDest: '', departureTime: '', arrivalTime: '', odometerOut: '', notes: '' }];
        }

        populateWarehouseSelects();
        document.getElementById('fldOrigin').value = savedOriginId;

        // updateTransitUI reconstruye fldDest — por eso restauramos después
        updateTransitUI();

        // Restaurar Bodega Tránsito (fldDest) después de que updateTransitUI lo reconstruyó
        if (savedDestId) document.getElementById('fldDest').value = savedDestId;

        // Restaurar campos de encabezado de tránsito
        if (isTransitMode) {
            const fldSeal = document.getElementById('fldSecuritySeal');
            const fldDept = document.getElementById('fldDepartureTime');
            const fldOdo  = document.getElementById('fldOdometerOut');
            if (fldSeal) fldSeal.value = txn.securitySeal   || '';
            if (fldDept) fldDept.value = txn.departureTime  || '';
            if (fldOdo)  fldOdo.value  = txn.odometerOut != null ? txn.odometerOut : '';

            // Cargar km del vehículo para validación en tiempo real
            if (savedDestId) {
                const wh = INV.warehouses.find(w => w.id === parseInt(savedDestId));
                if (wh && wh.idTransportUnit) {
                    invFetch(`/api/transportunit/${wh.idTransportUnit}`)
                        .then(unit => {
                            INV._vehicleOdometerKm = unit.currentOdometerKm ?? null;
                            const fldOdoHint = document.getElementById('fldOdoHint');
                            if (fldOdoHint && INV._vehicleOdometerKm !== null)
                                fldOdoHint.textContent = `Km actual del vehículo: ${INV._vehicleOdometerKm.toLocaleString('es-CR')} km`;
                        })
                        .catch(() => {});
                }
            }
        }

        renderLines();

        document.getElementById('btnSaveConfirm').classList.remove('d-none');
        bootstrap.Modal.getOrCreateInstance(document.getElementById('invModal')).show();
    } catch (e) {
        showAlert(e.message, 'danger');
    }
}

// ============================================================
// POBLAR SELECTS DE BODEGAS
// ============================================================

function populateWarehouseSelects() {
    const typeId = document.getElementById('fldType').value;
    const isTransit = txnTypeIs(typeId, 'TransitTransfer');
    const isAdjustmentIn = txnTypeIs(typeId, 'AdjustmentIn');
    const isAdjustmentOut = txnTypeIs(typeId, 'AdjustmentOut');

    // VALIDACIÓN: Solo mostrar bodegas Transit si es Ajuste de Inventario (+) o (-)
    // Para todos los demás tipos, excluir bodegas Transit
    let originWhs;
    if (isAdjustmentIn || isAdjustmentOut) {
        // Ajustes de inventario: mostrar TODAS las bodegas (incluir Transit)
        originWhs = INV.warehouses;
    } else {
        // Todos los demás tipos (incluido Transfer y TransitTransfer): excluir bodegas Transit
        originWhs = INV.warehouses.filter(w => w.warehouseType !== 'Transit');
    }

    const originOpts = originWhs.map(w =>
        `<option value="${w.id}">${w.code} — ${w.name}</option>`
    ).join('');

    const elOrigin = document.getElementById('fldOrigin');
    const curOrigin = elOrigin.value;
    elOrigin.innerHTML = '<option value="">— seleccione —</option>' + originOpts;
    if (curOrigin) elOrigin.value = curOrigin;
}

// ============================================================
// UI DINÁMICA SEGÚN TIPO DE MOVIMIENTO
// ============================================================

function updateTransitUI() {
    const typeId    = document.getElementById('fldType').value;
    const typeCode  = txnTypeInfo(typeId).code;

    const isTransit = typeCode === 'TransitTransfer';
    const needsDest = ['Transfer', 'TransitTransfer', 'CustomerReturn'].includes(typeCode);
    const banner    = document.getElementById('transitBanner');
    const destGroup = document.getElementById('destGroup');
    const destLabel = document.getElementById('destLabel');
    const destHint  = document.getElementById('destHint');
    const finalDestGroup = document.getElementById('finalDestGroup');

    banner.classList.toggle('d-none', !isTransit);
    destGroup.classList.toggle('d-none', !needsDest);

    // Mostrar/ocultar campos de encabezado de tránsito (Sello, Hora Salida, Km Salida)
    const transitHeaderFields = document.getElementById('transitHeaderFields');
    if (transitHeaderFields) transitHeaderFields.classList.toggle('d-none', !isTransit);

    // Limpiar campos de encabezado al cambiar a un tipo no-tránsito
    if (!isTransit) {
        const fldSeal = document.getElementById('fldSecuritySeal');
        const fldDept = document.getElementById('fldDepartureTime');
        const fldOdo  = document.getElementById('fldOdometerOut');
        if (fldSeal) fldSeal.value = '';
        if (fldDept) fldDept.value = '';
        if (fldOdo)  fldOdo.value  = '';
        INV._vehicleOdometerKm = null;
        const fldOdoErr = document.getElementById('fldOdoErr');
        if (fldOdoErr) fldOdoErr.style.display = 'none';
        // Clear busy warning and re-enable save buttons
        const busyWarn = document.getElementById('transitBusyWarn');
        if (busyWarn) busyWarn.classList.add('d-none');
        ['btnSaveMovement', 'btnSaveConfirm'].forEach(id => {
            const btn = document.getElementById(id);
            if (btn) btn.disabled = false;
        });
    }

    // En modo tránsito el selector global de destino se reemplaza por grupos inline
    finalDestGroup.classList.add('d-none');
    // El botón global Agregar Artículo solo aplica en modo no-tránsito
    const btnAdd = document.getElementById('btnAddLine');
    if (btnAdd) btnAdd.classList.toggle('d-none', isTransit);

    // Repoblar origen (excluir transit si es TransitTransfer)
    populateWarehouseSelects();

    if (isTransit) {
        // Solo bodegas de tránsito en el select de destino (vehículo intermediario)
        const opts = INV.warehouses
            .filter(w => w.warehouseType === 'Transit')
            .map(w => `<option value="${w.id}">🚛 ${w.code} — ${w.name}</option>`)
            .join('');

        document.getElementById('fldDest').innerHTML =
            '<option value="">— seleccione vehículo/tránsito —</option>' + opts;

        destLabel.textContent = 'Bodega Tránsito (Vehículo) *';
        destHint.textContent  = 'La bodega tipo Transit actúa como vehículo intermediario.';
    } else {
        // Para tipos no-transit: aplicar filtrado según el tipo de movimiento
        const originId = parseInt(document.getElementById('fldOrigin').value) || 0;
        const isAdjustmentIn = typeCode === 'AdjustmentIn';
        const isAdjustmentOut = typeCode === 'AdjustmentOut';

        // VALIDACIÓN: Solo excluir bodegas Transit si NO es un ajuste de inventario
        let destWhs;
        if (isAdjustmentIn || isAdjustmentOut) {
            // Ajustes de inventario: mostrar todas las bodegas excepto la misma que el origen
            destWhs = INV.warehouses.filter(w => w.id !== originId);
        } else {
            // Otros tipos: excluir bodegas Transit y la misma que el origen
            destWhs = INV.warehouses.filter(w => w.id !== originId && w.warehouseType !== 'Transit');
        }

        const destOpts = destWhs.map(w =>
            `<option value="${w.id}">${w.code} — ${w.name}</option>`
        ).join('');
        document.getElementById('fldDest').innerHTML =
            '<option value="">— seleccione —</option>' + destOpts;

        destLabel.textContent = 'Bodega Destino';
        destHint.textContent  = '';
    }

    // Re-renderizar líneas
    renderLines();
}

// ============================================================
// HELPERS DE GRUPOS (modo tránsito)
// ============================================================

// Retorna array de IDs de bodegas destino ya usadas en los grupos actuales
function getUsedDestIds() {
    return INV._groups
        .map(g => g.destId)
        .filter(id => id && id > 0);
}

// Opciones disponibles para un selector de grupo (excluye origen, tránsito y otros grupos)
function buildDestOpts(excludeGroupIdx) {
    const originId = parseInt(document.getElementById('fldOrigin').value) || 0;
    const otherDests = INV._groups
        .filter((_, i) => i !== excludeGroupIdx)
        .map(g => g.destId)
        .filter(id => id && id > 0);

    return INV.warehouses.filter(w =>
        w.warehouseType !== 'Transit' &&
        w.id !== originId &&
        !otherDests.includes(w.id)
    );
}

// El usuario cambia el selector de destino de un grupo
function changeGroupDest(groupIdx) {
    const sel = document.getElementById(`grpDest${groupIdx}`);
    if (!sel) return;
    const newDestId = parseInt(sel.value) || null;
    const oldDestId = INV._groups[groupIdx].destId;
    INV._groups[groupIdx].destId = newDestId;
    // Actualizar todas las líneas que pertenecían a este grupo
    INV.lines.forEach(l => {
        if (l.idWarehouseDestLine === oldDestId) l.idWarehouseDestLine = newDestId;
    });
    renderLines();
}

// ============================================================
// RENDER LÍNEAS EDITABLES
// ============================================================

function renderLines() {
    const container = document.getElementById('linesContainer');
    const empty     = document.getElementById('linesEmpty');
    const typeId    = document.getElementById('fldType').value;
    const isTransit = txnTypeIs(typeId, 'TransitTransfer');

    // Ocultar/mostrar el botón global
    const btnAddLine    = document.getElementById('btnAddLine');
    const addLineHeader = document.getElementById('addLineHeader');
    const finalDestGroup = document.getElementById('finalDestGroup');
    if (btnAddLine)    btnAddLine.style.display    = isTransit ? 'none' : '';
    if (addLineHeader) addLineHeader.style.display = isTransit ? 'none' : '';
    if (finalDestGroup) finalDestGroup.style.display = 'none'; // siempre oculto — grupos inline

    if (isTransit) {
        if (empty) empty.style.display = 'none';
        // In transit mode we want the groups to expand naturally (no inner vertical scroll)
        // so override the lines container scrolling to allow the modal to scroll instead.
        container.style.maxHeight = 'none';
        container.style.overflowY = 'visible';
        renderTransitGroups(container);
        return;
    }

    if (!INV.lines.length) {
        container.innerHTML = '';
        if (empty) empty.style.display = '';
        return;
    }
    if (empty) empty.style.display = 'none';

    // Restore scrolling behavior for non-transit mode
    container.style.maxHeight = '';
    container.style.overflowY = '';

    container.innerHTML = INV.lines.map((l, idx) => _buildLineHtml(l, idx, false)).join('');
    _bindLineEvents(container);
}

// Construye el HTML de una línea (usada tanto en modo normal como en grupos transit)
function _buildLineHtml(l, idx, isTransit) {
    // En modo transit, agregamos columna de Devolución
    if (isTransit) {
        return `<div class="line-row transit" id="lineRow${idx}">
            <div class="row g-2">
                <div class="col-12 col-md-3">
                    <label class="form-label" style="font-size:.72rem;">Artículo *</label>
                    <div class="input-group input-group-sm" style="position:relative;">
                        <input type="text" class="form-control line-item-search" data-idx="${idx}"
                               value="${l.itemCode ? l.itemCode + ' — ' + l.itemName : ''}"
                               placeholder="Código o nombre…" autocomplete="off">
                        <input type="hidden" class="line-item-id"   data-idx="${idx}" value="${l.idItem || ''}">
                        <input type="hidden" class="line-item-code" data-idx="${idx}" value="${l.itemCode || ''}">
                        <input type="hidden" class="line-item-name" data-idx="${idx}" value="${l.itemName || ''}">
                    </div>
                    <div class="item-suggestions" id="sugg${idx}" style="display:none;position:absolute;z-index:9999;width:calc(100% - 1.5rem);"></div>
                </div>
                <div class="col-3 col-md-1">
                    <label class="form-label" style="font-size:.72rem;">Cant. *</label>
                    <input type="number" class="form-control form-control-sm line-qty" data-idx="${idx}"
                           value="${l.qtyRequested || ''}" min="0" step="0.0001">
                </div>
                <div class="col-3 col-md-1">
                    <label class="form-label" style="font-size:.72rem;color:#fbbf24;">Devoluc.</label>
                    <input type="number" class="form-control form-control-sm line-return" data-idx="${idx}"
                           value="${l.qtyReturned || 0}" min="0" step="0.0001"
                           style="background:#0d1117;color:#fbbf24;border-color:#fbbf24;">
                </div>
                <div class="col-3 col-md-2">
                    <label class="form-label" style="font-size:.72rem;">Costo Unit.</label>
                    <input type="number" class="form-control form-control-sm line-cost" data-idx="${idx}"
                           value="${l.unitCost || ''}" min="0" step="0.01">
                </div>
                <div class="col-3 col-md-2">
                    <label class="form-label" style="font-size:.72rem;">Lote</label>
                    <input type="text" class="form-control form-control-sm line-lot" data-idx="${idx}"
                           value="${l.lotNumber || ''}">
                </div>
                <div class="col-12 col-md-3 d-flex align-items-end">
                    <button type="button" class="btn btn-sm btn-outline-danger w-100 btn-remove-line"
                            data-idx="${idx}"><i class="bi bi-trash3"></i></button>
                </div>
            </div>
        </div>`;
    }

    // Modo no-transit: HTML original sin columna de devolución
    return `<div class="line-row" id="lineRow${idx}">
        <div class="row g-2">
            <div class="col-12 col-md-4">
                <label class="form-label" style="font-size:.72rem;">Artículo *</label>
                <div class="input-group input-group-sm" style="position:relative;">
                    <input type="text" class="form-control line-item-search" data-idx="${idx}"
                           value="${l.itemCode ? l.itemCode + ' — ' + l.itemName : ''}"
                           placeholder="Código o nombre…" autocomplete="off">
                    <input type="hidden" class="line-item-id"   data-idx="${idx}" value="${l.idItem || ''}">
                    <input type="hidden" class="line-item-code" data-idx="${idx}" value="${l.itemCode || ''}">
                    <input type="hidden" class="line-item-name" data-idx="${idx}" value="${l.itemName || ''}">
                </div>
                <div class="item-suggestions" id="sugg${idx}" style="display:none;position:absolute;z-index:9999;width:calc(100% - 1.5rem);"></div>
            </div>
            <div class="col-4 col-md-2">
                <label class="form-label" style="font-size:.72rem;">Cantidad *</label>
                <input type="number" class="form-control form-control-sm line-qty" data-idx="${idx}"
                       value="${l.qtyRequested || ''}" min="0.0001" step="0.0001">
            </div>
            <div class="col-4 col-md-2">
                <label class="form-label" style="font-size:.72rem;">Costo Unit.</label>
                <input type="number" class="form-control form-control-sm line-cost" data-idx="${idx}"
                       value="${l.unitCost || ''}" min="0" step="0.01">
            </div>
            <div class="col-4 col-md-2">
                <label class="form-label" style="font-size:.72rem;">Lote</label>
                <input type="text" class="form-control form-control-sm line-lot" data-idx="${idx}"
                       value="${l.lotNumber || ''}">
            </div>
            <div class="col-12 col-md-2 d-flex align-items-end">
                <button type="button" class="btn btn-sm btn-outline-danger w-100 btn-remove-line"
                        data-idx="${idx}"><i class="bi bi-trash3"></i></button>
            </div>
        </div>
    </div>`;
}

// Bind todos los eventos de líneas dentro de un container dado
function _bindLineEvents(container) {
    container.querySelectorAll('.line-item-search').forEach(inp => {
        inp.addEventListener('input', onItemSearch);
        inp.addEventListener('keydown', e => {
            if (e.key !== 'Enter') return;
            e.preventDefault();
            const idx  = +inp.dataset.idx;
            const sugg = document.getElementById(`sugg${idx}`);
            const first = sugg && sugg.querySelector('.sugg-item');
            if (first) {
                const { id, code, name } = first.dataset;
                _applyItemSuggestion(idx, inp, id, code, name);
            }
        });
        inp.addEventListener('blur', () => setTimeout(() => {
            const idx   = +inp.dataset.idx;
            const term  = inp.value.trim().toLowerCase();
            const cache = _itemSuggestionsCache[idx] || [];
            if (!INV.lines[idx]) return;
            if (!INV.lines[idx].idItem && cache.length) {
                const exact = cache.find(it =>
                    it.code.toLowerCase() === term ||
                    `${it.code} — ${it.name}`.toLowerCase() === term
                );
                const match = exact || (cache.length === 1 ? cache[0] : null);
                if (match) {
                    _applyItemSuggestion(idx, inp, match.id, match.code, match.name, match.costPrice);
                    return;
                }
            }
            const sugg = document.getElementById(`sugg${idx}`);
            if (sugg) sugg.style.display = 'none';
        }, 200));
    });

    container.querySelectorAll('.line-qty').forEach(inp =>
        inp.addEventListener('change', e => {
            const idx = +e.target.dataset.idx;
            INV.lines[idx].qtyRequested = parseFloat(e.target.value) || 0;
        }));

    container.querySelectorAll('.line-cost').forEach(inp =>
        inp.addEventListener('change', e => {
            const idx = +e.target.dataset.idx;
            INV.lines[idx].unitCost = parseFloat(e.target.value) || null;
        }));

    container.querySelectorAll('.line-lot').forEach(inp =>
        inp.addEventListener('change', e => {
            const idx = +e.target.dataset.idx;
            INV.lines[idx].lotNumber = e.target.value;
        }));

    container.querySelectorAll('.line-return').forEach(inp =>
        inp.addEventListener('change', e => {
            const idx = +e.target.dataset.idx;
            INV.lines[idx].qtyReturned = parseFloat(e.target.value) || 0;
        }));

    container.querySelectorAll('.btn-remove-line').forEach(btn =>
        btn.addEventListener('click', e => {
            const idx = +e.currentTarget.dataset.idx;
            const groupDestId = INV.lines[idx] ? INV.lines[idx].idWarehouseDestLine : null;
            INV.lines.splice(idx, 1);
            // Si ese grupo queda sin líneas, quitar el grupo también
            if (txnTypeIs(document.getElementById('fldType').value, 'TransitTransfer')) {
                const groupIdx = INV._groups.findIndex(g => g.destId === groupDestId);
                if (groupIdx >= 0 && !INV.lines.some(l => l.idWarehouseDestLine === groupDestId)) {
                    // Solo eliminar el grupo si hay más de uno
                    if (INV._groups.length > 1) INV._groups.splice(groupIdx, 1);
                }
            }
            renderLines();
        }));
}

// ============================================================
// REORDENAR GRUPOS DE DESTINO (modo TransitTransfer)
// ============================================================

/**
 * Mueve el grupo en posición gIdx una posición hacia arriba (dir=-1) o abajo (dir=1).
 * También reordena las líneas para que mantengan coherencia con el nuevo orden.
 */
function moveGroup(gIdx, dir) {
    const targetIdx = gIdx + dir;
    if (targetIdx < 0 || targetIdx >= INV._groups.length) return;
    // Remove the group and insert at the new position to preserve order
    const [moved] = INV._groups.splice(gIdx, 1);
    INV._groups.splice(targetIdx, 0, moved);

    // Reorder INV.lines so that groups' lines appear in the same order as INV._groups
    const groupOrder = INV._groups.map(g => g.destId ?? 0);
    const newLines = [];
    const used = new Set();
    for (const destId of groupOrder) {
        const grpLines = INV.lines.filter(l => (l.idWarehouseDestLine ?? 0) === destId);
        for (const ln of grpLines) { newLines.push(ln); used.add(ln); }
    }
    // Append any lines not mapped to the groups (safety)
    for (const ln of INV.lines) if (!used.has(ln)) newLines.push(ln);
    INV.lines = newLines;

    renderLines();
}

// ============================================================
// RENDER GRUPOS DE DESTINO (modo TransitTransfer)
// ============================================================

/**
 * Devuelve el índice del primer grupo de destino que NO está completamente recibido.
 * Ese grupo es el "activo" — sus campos (sello, hora, km) son obligatorios.
 * Los grupos anteriores (ya recibidos) y los posteriores (futuros) no se exigen.
 */
function _getActiveTransitGroupIdx() {
    for (let i = 0; i < INV._groups.length; i++) {
        const g = INV._groups[i];
        if (!g.destId) return i; // grupo sin bodega asignada → activo
        const groupLines = INV.lines.filter(l => l.idWarehouseDestLine === g.destId);
        if (!groupLines.length) return i; // sin líneas → activo
    }
    return 0; // fallback: primer grupo
}

function renderTransitGroups(container) {
    // Calcular todos los destIds usados por otros grupos (para exclusión)
    const originId = parseInt(document.getElementById('fldOrigin').value) || 0;

    // Índice del grupo activo: primer grupo no completamente recibido
    const activeGroupIdx = _getActiveTransitGroupIdx();

    let html = '';

    INV._groups.forEach((group, gIdx) => {
        const isFirstGroup  = gIdx === 0;
        const isActiveGroup = gIdx === activeGroupIdx;
        const groupLines = INV.lines.filter(l => l.idWarehouseDestLine === group.destId);
        // Índices reales dentro de INV.lines para este grupo
        const groupLineIdxs = INV.lines
            .map((l, i) => ({ l, i }))
            .filter(({ l }) => l.idWarehouseDestLine === group.destId)
            .map(({ i }) => i);

        // Opciones para este selector: excluir origen, tránsito, y destinos usados en otros grupos
        const otherDests = INV._groups
            .filter((_, i) => i !== gIdx)
            .map(g => g.destId)
            .filter(id => id && id > 0);

        const availableWhs = INV.warehouses.filter(w =>
            w.warehouseType !== 'Transit' &&
            w.id !== originId &&
            !otherDests.includes(w.id)
        );

        const destOpts = availableWhs.map(w =>
            `<option value="${w.id}" ${group.destId === w.id ? 'selected' : ''}>${w.code} — ${w.name}</option>`
        ).join('');

        // Badges de bodegas usadas en el movimiento (todos los grupos)
        const usedBadges = INV._groups
            .filter(g => g.destId)
            .map(g => {
                const w = INV.warehouses.find(x => x.id === g.destId);
                const isCurrentGroup = g.destId === group.destId;
                return w
                    ? `<span class="badge" style="background:${isCurrentGroup ? '#2d1b6e' : '#1d3557'};color:${isCurrentGroup ? '#c4b5fd' : '#93c5fd'};font-size:.72rem;">${w.code}</span>`
                    : '';
            }).join(' ');

        const linesHtml = groupLineIdxs.map(idx => _buildLineHtml(INV.lines[idx], idx, true)).join('');

        const canRemoveGroup = INV._groups.length > 1;
        const canMoveUp      = gIdx > 0;
        const canMoveDown    = gIdx < INV._groups.length - 1;

        html += `<div class="transit-group-block mb-3" id="grpBlock${gIdx}"
                      style="background:#111827;border:1px solid var(--inv-orange);border-radius:10px;padding:1rem;">
            <div class="row g-2 align-items-end mb-2">
                <div class="col-12 col-md-5">
                    <label class="form-label text-warning fw-semibold" style="font-size:.8rem;">
                        <i class="bi bi-geo-alt me-1"></i>Bodega Destino Final * <span class="text-muted" style="font-size:.7rem;font-weight:400;">— Parada ${gIdx + 1} de ${INV._groups.length}</span>
                    </label>
                    <select id="grpDest${gIdx}" class="form-select grp-dest-sel" data-gidx="${gIdx}">
                        <option value="">— seleccione bodega destino —</option>
                        ${destOpts}
                    </select>
                    <small style="color:#cbd5e1!important;">Las bodegas ya usadas en este movimiento no aparecen.</small>
                </div>
                <div class="col-12 col-md-3">
                    <div class="d-flex flex-wrap gap-1 align-items-center mt-2">
                        <small class="text-muted" style="font-size:.72rem;">Bodegas usadas en este movimiento:</small>
                        <span>${usedBadges || '<span class="text-muted fst-italic" style="font-size:.72rem;">—</span>'}</span>
                    </div>
                </div>
                <div class="col-12 col-md-4 d-flex align-items-end gap-1 justify-content-end">
                    <button type="button" class="btn btn-sm btn-outline-secondary btn-move-group-up" data-gidx="${gIdx}"
                            title="Subir bodega" ${canMoveUp ? '' : 'disabled'}>
                        <i class="bi bi-arrow-up"></i>
                    </button>
                    <button type="button" class="btn btn-sm btn-outline-secondary btn-move-group-down" data-gidx="${gIdx}"
                            title="Bajar bodega" ${canMoveDown ? '' : 'disabled'}>
                        <i class="bi bi-arrow-down"></i>
                    </button>
                    ${canRemoveGroup ? `<button type="button" class="btn btn-sm btn-outline-danger btn-remove-group" data-gidx="${gIdx}">
                        <i class="bi bi-x-circle me-1"></i>Quitar
                    </button>` : ''}
                </div>
            </div>
            <!-- Artículos de este grupo -->
            <div class="d-flex justify-content-between align-items-center mb-2">
                <span class="fw-semibold text-light" style="font-size:.85rem;"><i class="bi bi-list-ul me-1"></i>Artículos</span>
                <button type="button" class="btn btn-sm btn-outline-warning btn-add-group-line" data-gidx="${gIdx}">
                    <i class="bi bi-plus me-1"></i>Agregar Artículo
                </button>
            </div>
            <div id="grpLines${gIdx}">
                ${linesHtml || '<div class="text-center py-2" style="font-size:.82rem;color:#94a3b8;"><i class="bi bi-inbox me-1"></i>Sin artículos. Haga clic en "Agregar Artículo".</div>'}
            </div>
            <!-- Observaciones por bodega destino -->
            <div class="row g-2 mt-2">
                <div class="col-12">
                    <label class="form-label" style="font-size:.78rem;color:#94a3b8;">
                        <i class="bi bi-chat-left-text me-1 text-info"></i>Observaciones para esta bodega destino
                    </label>
                    <textarea id="grpNotes${gIdx}" class="form-control grp-notes" data-gidx="${gIdx}" rows="2"
                              placeholder="Observaciones del despacho hacia esta bodega…"
                              style="background:#0d1117;color:#e2e8f0;border-color:#2a3a5c;resize:vertical;font-size:.85rem;">${_escapeHtml(group.notes || '')}</textarea>
                    <small style="color:#cbd5e1!important;">Opcional — se guardan junto al movimiento y se mostrarán al confirmar la recepción.</small>
                </div>
            </div>
        </div>`;
    });

    // Botón para agregar nueva bodega destino
    html += `<div class="text-end mt-2">
        <button type="button" class="btn btn-sm btn-outline-info" id="btnAddDestGroup">
            <i class="bi bi-plus-circle me-1"></i>Agregar otra Bodega Destino
        </button>
    </div>`;

    container.innerHTML = html;

    // Bind: cambio de selector de destino por grupo
    container.querySelectorAll('.grp-dest-sel').forEach(sel => {
        sel.addEventListener('change', () => changeGroupDest(+sel.dataset.gidx));
    });

    // Bind: observaciones por grupo
    container.querySelectorAll('.grp-notes').forEach(ta => {
        ta.addEventListener('input', () => {
            const gIdx = +ta.dataset.gidx;
            if (INV._groups[gIdx] !== undefined) INV._groups[gIdx].notes = ta.value;
        });
    });

    // Bind: agregar artículo a un grupo específico
    container.querySelectorAll('.btn-add-group-line').forEach(btn => {
        btn.addEventListener('click', () => {
            const gIdx = +btn.dataset.gidx;
            const destId = INV._groups[gIdx] ? INV._groups[gIdx].destId : null;
            INV.lines.push({
                idItem: 0, itemCode: '', itemName: '', qtyRequested: 1,
                idWarehouseDestLine: destId,
                idUnitOfMeasure: null, unitOfMeasureCode: '',
                unitCost: null, lotNumber: '', notes: '',
                qtyReturned: 0,
            });
            renderLines();
            // Scroll al fondo del modal
            const modalBody = document.querySelector('#invModal .modal-body');
            if (modalBody) setTimeout(() => modalBody.scrollTo({ top: modalBody.scrollHeight, behavior: 'smooth' }), 50);
        });
    });

    // Bind: quitar grupo
    container.querySelectorAll('.btn-remove-group').forEach(btn => {
        btn.addEventListener('click', () => {
            const gIdx = +btn.dataset.gidx;
            if (INV._groups.length <= 1) return;
            const removedDest = INV._groups[gIdx].destId;
            // Quitar las líneas de este grupo
            INV.lines = INV.lines.filter(l => l.idWarehouseDestLine !== removedDest);
            INV._groups.splice(gIdx, 1);
            renderLines();
        });
    });

    // Bind: mover grupo hacia arriba
    container.querySelectorAll('.btn-move-group-up').forEach(btn => {
        btn.addEventListener('click', () => {
            const gIdx = +btn.dataset.gidx;
            moveGroup(gIdx, -1);
        });
    });

    // Bind: mover grupo hacia abajo
    container.querySelectorAll('.btn-move-group-down').forEach(btn => {
        btn.addEventListener('click', () => {
            const gIdx = +btn.dataset.gidx;
            moveGroup(gIdx, 1);
        });
    });

    // Bind: agregar nueva bodega destino
    const btnAddDestGroup = container.querySelector('#btnAddDestGroup');
    if (btnAddDestGroup) {
        btnAddDestGroup.addEventListener('click', () => {
            const hasUnselected = INV._groups.some(g => !g.destId);
            if (hasUnselected) {
                showModalAlert('Seleccione una bodega destino en los grupos existentes antes de agregar uno nuevo.', 'info');
                return;
            }
            const available = buildDestOpts(-1);
            if (available.length === 0) {
                showModalAlert('No hay más bodegas destino disponibles. Todas las bodegas ya han sido asignadas en este movimiento.', 'info');
                return;
            }
            INV._groups.push({ destId: null, sealDest: '', departureTime: '', arrivalTime: '', odometerOut: '', notes: '' });
            renderLines();
            const modalBody = document.querySelector('#invModal .modal-body');
            if (modalBody) setTimeout(() => modalBody.scrollTo({ top: modalBody.scrollHeight, behavior: 'smooth' }), 50);
        });
    }

    // Bind eventos de líneas dentro de los grupos
    _bindLineEvents(container);
}

// ============================================================
// BÚSQUEDA DE ARTÍCULOS (autocompletado)
// ============================================================

let _itemSearchTimeout = null;

// Caché de sugerencias por índice de línea
const _itemSuggestionsCache = {};

function _applyItemSuggestion(idx, inp, id, code, name, costPrice) {
    INV.lines[idx].idItem   = parseInt(id);
    INV.lines[idx].itemCode = code;
    INV.lines[idx].itemName = name;
    if (costPrice != null && costPrice !== '') {
        INV.lines[idx].unitCost = parseFloat(costPrice) || null;
        // Actualizar el input de costo en el DOM si ya está renderizado
        const costEl = document.querySelector(`.line-cost[data-idx="${idx}"]`);
        if (costEl && INV.lines[idx].unitCost != null) costEl.value = INV.lines[idx].unitCost;
    }
    inp.value = `${code} — ${name}`;
    const sugg = document.getElementById(`sugg${idx}`);
    if (sugg) sugg.style.display = 'none';
}

async function onItemSearch(e) {
    const inp  = e.target;
    const idx  = +inp.dataset.idx;
    const term = inp.value.trim();

    // Limpiar idItem al modificar el campo
    INV.lines[idx].idItem   = 0;
    INV.lines[idx].itemCode = '';
    INV.lines[idx].itemName = '';
    _itemSuggestionsCache[idx] = [];

    clearTimeout(_itemSearchTimeout);
    if (term.length < 2) {
        const s = document.getElementById(`sugg${idx}`);
        if (s) s.style.display = 'none';
        return;
    }

    _itemSearchTimeout = setTimeout(async () => {
        try {
            const data = await invFetch(`/api/item?search=${encodeURIComponent(term)}&pageSize=8&isActive=true`);
            const items = data.items || data || [];
            _itemSuggestionsCache[idx] = items;
            renderItemSuggestions(idx, items, inp);
            // Auto-seleccionar si hay resultado único exacto por código
            if (items.length === 1) {
                const it = items[0];
                if (it.code.toLowerCase() === term.toLowerCase() || term.toLowerCase() === `${it.code} — ${it.name}`.toLowerCase()) {
                    _applyItemSuggestion(idx, inp, it.id, it.code, it.name, it.costPrice);
                }
            }
        } catch {}
    }, 300);
}

function renderItemSuggestions(idx, items, inp) {
    let sugg = document.getElementById(`sugg${idx}`);
    if (!sugg) return;

    if (!items.length) { sugg.style.display = 'none'; return; }

    sugg.style.cssText = `
        display:block; position:absolute; z-index:9999;
        background:#1a2340; border:1px solid #2a3a5c; border-radius:8px;
        max-height:200px; overflow-y:auto; width:100%;
    `;

    sugg.innerHTML = items.map(it =>
        `<div class="sugg-item p-2" style="cursor:pointer;border-bottom:1px solid #2a3a5c;font-size:.82rem;"
              data-id="${it.id}" data-code="${it.code}" data-name="${it.name}" data-cost="${it.costPrice ?? ''}">
            <strong>${it.code}</strong> — ${it.name}
        </div>`
    ).join('');

    sugg.querySelectorAll('.sugg-item').forEach(el => {
        el.addEventListener('mousedown', ev => {
            ev.preventDefault();
            const { id, code, name, cost } = ev.currentTarget.dataset;
            _applyItemSuggestion(idx, inp, id, code, name, cost);
        });
        el.addEventListener('mouseenter', () => el.style.background = '#1e2d4a');
        el.addEventListener('mouseleave', () => el.style.background = 'transparent');
    });
}

// ============================================================
// GUARDAR MOVIMIENTO
// ============================================================

async function saveMovement(autoConfirm = false) {
    const number    = document.getElementById('fldNumber').value.trim();
    const date      = document.getElementById('fldDate').value;
    const typeId    = parseInt(document.getElementById('fldType').value) || 0;
    const originId  = parseInt(document.getElementById('fldOrigin').value) || 0;
    const destId    = parseInt(document.getElementById('fldDest').value)   || null;
    const reference = document.getElementById('fldReference').value.trim() || null;
    const notes     = document.getElementById('fldNotes').value.trim()     || null;

    const isTransit = txnTypeIs(typeId, 'TransitTransfer');
    const securitySeal = isTransit
        ? (document.getElementById('fldSecuritySeal').value.trim() || null)
        : null;

    // Campos de tramo Origen→Tránsito
    const departureTime  = isTransit ? (document.getElementById('fldDepartureTime').value || null) : null;
    const odometerRaw    = document.getElementById('fldOdometerOut').value.trim();
    const odometerOut    = isTransit ? (odometerRaw !== '' ? parseFloat(odometerRaw) : null) : null;

    if (!originId) return showModalAlert('Seleccione la Bodega Origen.', 'warning');
    if (isTransit && !destId) return showModalAlert('Seleccione la Bodega de Tránsito (Vehículo).', 'warning');
    if (isTransit) {
        // Validaciones de encabezado solo obligatorias al Confirmar (autoConfirm)
        if (autoConfirm) {
            if (!securitySeal)
                return showModalAlert('El Sello de Seguridad es obligatorio para confirmar el movimiento.', 'warning');
            if (!departureTime)
                return showModalAlert('La Hora de Salida es obligatoria para confirmar el movimiento.', 'warning');
            if (odometerOut === null)
                return showModalAlert('El Km de Salida es obligatorio para confirmar el movimiento.', 'warning');

            // Validar unicidad del sello en toda la BD (headers + lines de cualquier movimiento)
            if (securitySeal) {
                try {
                    const excludeId = INV.editingId || undefined;
                    const sealUrl = `/api/inventorytransaction/check-any-seal?seal=${encodeURIComponent(securitySeal)}`
                        + (excludeId ? `&excludeId=${excludeId}` : '');
                    const { exists: sealDup } = await invFetch(sealUrl);
                    if (sealDup)
                        return showModalAlert(`El Sello de Seguridad "${securitySeal}" ya está en uso en otro movimiento. Ingrese un sello único.`, 'warning');
                } catch { /* si falla el check, el servidor lo rechazará */ }
            }
        }
        // Validar Km Salida >= km del vehículo si se ingresó un valor
        if (odometerOut !== null && INV._vehicleOdometerKm !== null && odometerOut < INV._vehicleOdometerKm)
            return showModalAlert(
                `El Km de Salida (${odometerOut.toLocaleString('es-CR')} km) no puede ser menor al kilometraje actual del vehículo (${INV._vehicleOdometerKm.toLocaleString('es-CR')} km).`,
                'warning'
            );

        // Validar que cada grupo tenga bodega destino asignada
        const missingDest = INV._groups.some(g => !g.destId);
        if (missingDest) return showModalAlert('Seleccione la Bodega Destino Final en todos los grupos de artículos.', 'warning');

        // Validar que ningún grupo esté vacío de artículos
        for (const [gIdx, group] of INV._groups.entries()) {
            const hasLines = INV.lines.some(l => l.idWarehouseDestLine === group.destId);
            if (!hasLines) {
                const w = INV.warehouses.find(x => x.id === group.destId);
                const wName = w ? `${w.code} — ${w.name}` : `Grupo ${gIdx + 1}`;
                return showModalAlert(
                    `La bodega destino "${wName}" no tiene artículos asignados. Agregue artículos o elimine la bodega del movimiento.`,
                    'warning'
                );
            }
            // Los campos de sello, hora y km de los grupos NO son obligatorios en crear/confirmar
        }
    }
    if (!INV.lines.length) return showModalAlert('Agregue al menos un artículo.', 'warning');

    // Verificar unicidad del sello de seguridad principal
    if (isTransit && securitySeal) {
        try {
            const excludeId = INV.editingId || undefined;
            const sealUrl   = `/api/inventorytransaction/check-seal?seal=${encodeURIComponent(securitySeal)}`
                + (excludeId ? `&excludeId=${excludeId}` : '');
            const { exists } = await invFetch(sealUrl);
            if (exists) {
                document.getElementById('sealError').classList.remove('d-none');
                return showModalAlert('El Sello de Seguridad ya está en uso en otra transacción.', 'danger');
            }
            document.getElementById('sealError').classList.add('d-none');
        } catch {}
    }

    // Recolectar valores de líneas desde el DOM y propagar campos de grupo
    const container = document.getElementById('linesContainer');
    INV.lines.forEach((l, idx) => {
        const qtyEl    = container.querySelector(`.line-qty[data-idx="${idx}"]`);
        const costEl   = container.querySelector(`.line-cost[data-idx="${idx}"]`);
        const lotEl    = container.querySelector(`.line-lot[data-idx="${idx}"]`);
        const returnEl = container.querySelector(`.line-return[data-idx="${idx}"]`);
        if (qtyEl)    l.qtyRequested = parseFloat(qtyEl.value)    || 0;
        if (costEl)   l.unitCost     = parseFloat(costEl.value)   || null;
        if (lotEl)    l.lotNumber    = lotEl.value || null;
        if (returnEl) l.qtyReturned  = parseFloat(returnEl.value) || 0;
    });

    for (const l of INV.lines) {
        if (!l.idItem) return showModalAlert('Una línea no tiene artículo asignado.', 'warning');

        if (isTransit) {
            // Para traslados vía tránsito: permitir si Cantidad O Devolución > 0
            const qty = l.qtyRequested || 0;
            const ret = l.qtyReturned || 0;

            if (qty <= 0 && ret <= 0) {
                return showModalAlert(`La línea "${l.itemCode}" debe tener al menos una cantidad mayor a 0 (Cantidad o Devolución).`, 'warning');
            }
        } else {
            // Para movimientos normales: Cantidad debe ser > 0
            if (!l.qtyRequested || l.qtyRequested <= 0) {
                return showModalAlert(`La cantidad de "${l.itemCode}" debe ser mayor a 0.`, 'warning');
            }
        }
    }

    // Recopilar notas por grupo de bodega destino (solo TransitTransfer)
    const groupNotesByDestId = {};
    if (isTransit) {
        INV._groups.forEach(g => {
            if (g.destId) groupNotesByDestId[g.destId] = g.notes || null;
        });
    }

    const payload = {
        transactionNumber: number || undefined,
        idInventoryTransactionType: typeId,
        idMenu: window.INV_MENU_ID || 8,  // ⚠️ ID del menú de origen (fallback a id_menu=8 si no está definido)
        idWarehouseOrigin: originId,
        idWarehouseDest:   destId,
        reference,
        notes,
        securitySeal,
        departureTime,
        odometerOut,
        transactionDate:      date,
        isTransitTransfer:    isTransit,
        groupNotesByDestId,
        lines: INV.lines.map(l => ({
            idItem:              l.idItem,
            itemCode:            l.itemCode,
            itemName:            l.itemName,
            qtyRequested:        l.qtyRequested,
            qtyReturned:         l.qtyReturned || 0,
            idWarehouseDestLine: l.idWarehouseDestLine || null,
            idUnitOfMeasure:     l.idUnitOfMeasure || null,
            unitOfMeasureCode:   l.unitOfMeasureCode || null,
            unitCost:            l.unitCost || null,
            lotNumber:           l.lotNumber || null,
        })),
    };

    try {
        let txn;
        if (INV.editingId) {
            await invFetch(`/api/inventorytransaction/${INV.editingId}`, { method: 'PUT', body: payload });
            await invFetch(`/api/inventorytransaction/${INV.editingId}/lines`, {
                method: 'PUT',
                body: { lines: payload.lines, groupNotesByDestId: payload.groupNotesByDestId }
            });
            txn = { id: INV.editingId };
        } else {
            txn = await invFetch('/api/inventorytransaction', { method: 'POST', body: payload });
        }

        if (autoConfirm && txn.id) {
            await invFetch(`/api/inventorytransaction/${txn.id}/confirm`, { method: 'PATCH' });
        }

        bootstrap.Modal.getInstance(document.getElementById('invModal')).hide();
        showAlert('Movimiento guardado correctamente.', 'success');
        loadMovements(INV.page);
    } catch (e) {
        showModalAlert(e.message, 'danger');
    }
}

// ============================================================
// RECIBIR MOVIMIENTO — pantalla bodega por bodega
// ============================================================

async function openReceive(id) {
    INV.currentTxn = null;
    const body   = document.getElementById('receiveModalBody');
    const footer = document.getElementById('receiveModalFooter');
    body.innerHTML   = '<div class="text-center py-4" style="color:#cbd5e1;">Cargando…</div>';
    footer.innerHTML = '<button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cerrar</button>';
    document.getElementById('receiveModalTitle').innerHTML =
        '<i class="bi bi-box-arrow-in-down me-2 text-success"></i>Recibir Movimiento';

    bootstrap.Modal.getOrCreateInstance(document.getElementById('receiveModal')).show();

    // ⚠️ IMPORTANTE: Recargar el parámetro global CADA VEZ que se abre el modal
    // para asegurar que siempre tenga el valor más reciente desde la BD
    await loadGlobalParameters();

    try {
        const txn   = await invFetch(`/api/inventorytransaction/${id}`);
        const lines = txn.lines || await invFetch(`/api/inventorytransaction/${id}/lines`);
        txn.lines   = lines;
        if (txn.isTransitTransfer)
            txn.transitGroups = await invFetch(`/api/inventorytransaction/${id}/transit-groups`);
        INV.currentTxn = txn;

        document.getElementById('receiveModalTitle').innerHTML =
            `<i class="bi bi-box-arrow-in-down me-2 text-success"></i>${txn.transactionNumber} — ${statusBadge(txn.idInventoryTransactionStatus)}`;

        body.innerHTML = renderReceiveBody(txn);
        renderReceiveFooter(txn, footer);
        _bindReceiveBodyEvents(body);

    } catch (e) {
        body.innerHTML = `<div class="text-danger text-center py-4">${e.message}</div>`;
    }
}

// ============================================================
// FIRMA DIGITAL (Signature Pad — canvas nativo)
// ============================================================

function _initSignaturePad(canvas, gIdx) {
    // Ajustar resolución real del canvas al tamaño CSS — debe ejecutarse cuando el DOM ya tiene layout
    function _resizeCanvas() {
        const rect = canvas.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) return false;
        const dpr = window.devicePixelRatio || 1;
        canvas.width  = Math.round(rect.width  * dpr);
        canvas.height = Math.round(rect.height * dpr);
        const ctx = canvas.getContext('2d');
        ctx.resetTransform();
        ctx.scale(dpr, dpr);
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, rect.width, rect.height);   // ← usar dimensiones CSS, no canvas px
        return true;
    }

    // Diferir la inicialización hasta que el modal termine de abrirse y el canvas tenga layout
    function _tryInit(attempts) {
        if (_resizeCanvas()) {
            _attachEvents();
        } else if (attempts > 0) {
            setTimeout(() => _tryInit(attempts - 1), 60);
        }
    }

    function _attachEvents() {
        const ctx = canvas.getContext('2d');
        ctx.strokeStyle = '#1e1b4b';
        ctx.lineWidth   = 2.5;
        ctx.lineCap     = 'round';
        ctx.lineJoin    = 'round';

        let drawing = false;

        function getPos(e) {
            const rect = canvas.getBoundingClientRect();
            const src  = e.touches ? e.touches[0] : e;
            return {
                x: src.clientX - rect.left,
                y: src.clientY - rect.top,
            };
        }

        function startDraw(e) {
            e.preventDefault();
            drawing = true;
            const { x, y } = getPos(e);
            ctx.beginPath();
            ctx.moveTo(x, y);
        }

        function draw(e) {
            if (!drawing) return;
            e.preventDefault();
            const { x, y } = getPos(e);
            ctx.lineTo(x, y);
            ctx.stroke();
        }

        function stopDraw(e) {
            if (!drawing) return;
            e.preventDefault();
            drawing = false;
            ctx.beginPath();
            const errEl = document.getElementById(`rcvSignErr${gIdx}`);
            if (errEl) errEl.style.display = 'none';
        }

        // Mouse
        canvas.addEventListener('mousedown',  startDraw);
        canvas.addEventListener('mousemove',  draw);
        canvas.addEventListener('mouseup',    stopDraw);
        canvas.addEventListener('mouseleave', stopDraw);
        // Touch
        canvas.addEventListener('touchstart', startDraw, { passive: false });
        canvas.addEventListener('touchmove',  draw,      { passive: false });
        canvas.addEventListener('touchend',   stopDraw,  { passive: false });

        // Botón limpiar
        const clearBtn = document.getElementById(`rcvSignClear${gIdx}`);
        if (clearBtn) {
            clearBtn.addEventListener('click', () => {
                const rect = canvas.getBoundingClientRect();
                ctx.fillStyle = '#ffffff';
                ctx.fillRect(0, 0, rect.width, rect.height);
            });
        }
    }

    requestAnimationFrame(() => _tryInit(10));
}

/**
 * Devuelve el dataURL PNG de la firma, o null si el canvas está vacío.
 * Usa escaneo de pixels (ImageData) para detectar si hay algún trazo,
 * lo cual es robusto incluso si el canvas fue inicializado con DPR deferred.
 */
function _getSignatureDataUrl(gIdx) {
    const canvas = document.getElementById(`rcvSignCanvas${gIdx}`);
    if (!canvas) return null;

    // Si el canvas aún no tiene dimensiones reales (init aún no terminó), forzar resize
    if (canvas.width <= 300 || canvas.height <= 150) {
        const rect = canvas.getBoundingClientRect();
        if (rect.width > 0 && rect.height > 0) {
            const dpr = window.devicePixelRatio || 1;
            canvas.width  = Math.round(rect.width  * dpr);
            canvas.height = Math.round(rect.height * dpr);
            const ctx = canvas.getContext('2d');
            ctx.resetTransform();
            ctx.scale(dpr, dpr);
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, rect.width, rect.height);
        }
    }

    if (canvas.width === 0 || canvas.height === 0) return null;

    // Escanear pixels: si todos son 255,255,255 (blanco puro) → canvas vacío
    const ctx = canvas.getContext('2d');
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    const data = imageData.data;
    for (let i = 0; i < data.length; i += 4) {
        // Si algún pixel NO es blanco puro → hay trazo
        if (data[i] < 250 || data[i + 1] < 250 || data[i + 2] < 250) {
            return canvas.toDataURL('image/png');
        }
    }
    return null; // completamente en blanco
}

function _bindReceiveBodyEvents(container) {
    // Inicializar canvas de firma si existe
    container.querySelectorAll('canvas[id^="rcvSignCanvas"]').forEach(canvas => {
        const gIdx = canvas.id.replace('rcvSignCanvas', '');
        _initSignaturePad(canvas, gIdx);
    });

    // Per-line checkbox → set qty to dispatched
    container.querySelectorAll('.rcv-line-chk').forEach(chk => {
        chk.addEventListener('change', () => {
            const lineId    = chk.dataset.lineId;
            const dispatched = parseFloat(chk.dataset.dispatched) || 0;
            const qtyInput   = document.getElementById(`rcv-qty-${lineId}`);
            if (qtyInput) {
                qtyInput.value = chk.checked ? dispatched : '';
                qtyInput.style.borderColor = chk.checked ? '#22c55e' : '#2a3a5c';
            }
        });
    });

    // Mark-all checkbox → check all line checkboxes and fill qtys
    container.querySelectorAll('.rcv-mark-all-chk').forEach(allChk => {
        allChk.addEventListener('change', () => {
            const groupChks = container.querySelectorAll('.rcv-line-chk');
            groupChks.forEach(chk => {
                if (chk.checked !== allChk.checked) {
                    chk.checked = allChk.checked;
                    chk.dispatchEvent(new Event('change'));
                }
            });
        });
    });

    // qty input manual edit → uncheck checkbox if value differs from dispatched
    container.querySelectorAll('.rcv-qty-input').forEach(inp => {
        inp.addEventListener('input', () => {
            const dispatched = parseFloat(inp.dataset.dispatched) || 0;
            const lineId = inp.id.replace('rcv-qty-', '');
            const chk = container.querySelector(`.rcv-line-chk[data-line-id="${lineId}"]`);
            if (chk) {
                const newVal = parseFloat(inp.value);
                chk.checked = !isNaN(newVal) && newVal === dispatched;
            }
        });
    });

    // Km Salida: validate > previous km AND > header odometer on input
    container.querySelectorAll('[id^="rcvOdoOut"]').forEach(odoInput => {
        const gIdx = odoInput.id.replace('rcvOdoOut', '');
        const errEl = document.getElementById(`rcvOdoOutErr${gIdx}`);
        odoInput.addEventListener('input', () => {
            const prevKm    = parseFloat(odoInput.dataset.prevKm)      || 0;
            const headerKm  = parseFloat(odoInput.dataset.headerOdometer) || 0;
            const minKm     = Math.max(prevKm, headerKm);
            const newKm     = parseFloat(odoInput.value);
            const isInvalid = !isNaN(newKm) && minKm > 0 && newKm <= minKm;
            if (errEl) {
                errEl.textContent = isInvalid
                    ? (newKm <= prevKm && prevKm > 0
                        ? `Debe ser mayor al Km anterior (${prevKm})`
                        : `Debe ser mayor al Km de Salida del encabezado (${headerKm})`)
                    : '';
                errEl.style.display = isInvalid ? '' : 'none';
            }
            odoInput.style.borderColor = isInvalid ? '#ef4444' : '';
        });
    });

    // Hora Llegada: validate > previous group's departure (or header departure for first group)
    container.querySelectorAll('[id^="rcvArrTime"]').forEach(arrInput => {
        const gIdx  = arrInput.id.replace('rcvArrTime', '');
        const errEl = document.getElementById(`rcvArrTimeErr${gIdx}`);
        arrInput.addEventListener('input', () => {
            const prevDept  = arrInput.dataset.prevDeparture || '';
            const arrVal    = arrInput.value;
            const isInvalid = prevDept && arrVal && arrVal <= prevDept;
            if (errEl) {
                errEl.textContent = isInvalid ? `Debe ser mayor a la Hora de Salida de referencia (${prevDept})` : '';
                errEl.style.display = isInvalid ? '' : 'none';
            }
            arrInput.style.borderColor = isInvalid ? '#ef4444' : '';
            // Also re-check departure time (departure must be > arrival)
            const deptInput = document.getElementById(`rcvDeptTime${gIdx}`);
            if (deptInput) deptInput.dispatchEvent(new Event('input'));
        });
    });

    // Hora Salida: validate > Hora Llegada on input
    container.querySelectorAll('[id^="rcvDeptTime"]').forEach(deptInput => {
        const gIdx  = deptInput.id.replace('rcvDeptTime', '');
        const errEl = document.getElementById(`rcvDeptTimeErr${gIdx}`);
        deptInput.addEventListener('input', () => {
            const arrInput = document.getElementById(`rcvArrTime${gIdx}`);
            const arrVal   = arrInput?.value || '';
            const deptVal  = deptInput.value;
            const isInvalid = arrVal && deptVal && deptVal <= arrVal;
            if (errEl) {
                errEl.textContent = isInvalid ? `Debe ser mayor a la Hora de Llegada (${arrVal})` : '';
                errEl.style.display = isInvalid ? '' : 'none';
            }
            deptInput.style.borderColor = isInvalid ? '#ef4444' : '';
        });
    });

    // Sello Destino: real-time uniqueness check
    let _sealCheckTimer = null;
    container.querySelectorAll('[id^="rcvSeal"]').forEach(sealInput => {
        // Skip error/ok indicator elements that start with rcvSealErr / rcvSealOk
        if (!sealInput.tagName || sealInput.tagName.toLowerCase() !== 'input') return;
        const gIdx  = sealInput.id.replace('rcvSeal', '');
        if (isNaN(parseInt(gIdx))) return;
        const errEl = document.getElementById(`rcvSealErr${gIdx}`);
        const okEl  = document.getElementById(`rcvSealOk${gIdx}`);

        sealInput.addEventListener('input', () => {
            clearTimeout(_sealCheckTimer);
            const seal = sealInput.value.trim();
            if (errEl) errEl.style.display = 'none';
            if (okEl)  okEl.style.display  = 'none';
            sealInput.style.borderColor = '';
            if (!seal) return;

            _sealCheckTimer = setTimeout(async () => {
                try {
                    const url = `/api/inventorytransaction/check-any-seal?seal=${encodeURIComponent(seal)}`;
                    const { exists } = await invFetch(url);
                    if (errEl) errEl.style.display = exists ? '' : 'none';
                    if (okEl)  okEl.style.display  = exists ? 'none' : '';
                    sealInput.style.borderColor = exists ? '#ef4444' : '#22c55e';
                } catch {}
            }, 450);
        });
    });
}

function _getReceiveDestGroups(txn) {
    // Si ya tenemos grupos cargados desde /transit-groups, usarlos directamente.
    // Cada grupo ya contiene sus líneas embebidas (campo lines) devuelto por el endpoint.
    if (txn.transitGroups && txn.transitGroups.length > 0) {
        const lines = txn.lines || [];
        return txn.transitGroups.map(g => ({
            ...g,
            destId: g.idWarehouseDestLine || 0,
            lines:  g.lines ?? lines.filter(l => l.idInventoryTransactionWarehouseTransit === g.id),
        }));
    }
    // Fallback: agrupar líneas por idInventoryTransactionWarehouseTransit (antes del primer load)
    const lines = txn.lines || [];
    const ordered = [];
    const seen = new Set();
    for (const l of lines) {
        const key = l.idInventoryTransactionWarehouseTransit || 0;
        if (!seen.has(key)) { seen.add(key); ordered.push(key); }
    }
    return ordered.map(key => ({
        id: key,
        destId: 0,
        idWarehouseDestLine: null,
        lineStatus: 'Pending',
        destSecuritySeal: null,
        departureTime: null,
        arrivalTime: null,
        odometerOut: null,
        signature: null,
        lines: lines.filter(l => (l.idInventoryTransactionWarehouseTransit || 0) === key),
    }));
}

function _activeReceiveGroupIdx(groups) {
    // El primer grupo cuyo lineStatus no esté en estado final (Received / Cancelled)
    return groups.findIndex(g => g.lineStatus !== 'Received' && g.lineStatus !== 'Cancelled');
}

function renderReceiveBody(txn) {
    const typeM = txnTypeInfo(txn.idInventoryTransactionType);

    const infoRow = (label, val) =>
        `<div class="col-6 col-md-3 mb-2">
            <div style="font-size:.72rem;color:#94a3b8;font-weight:600;letter-spacing:.03em;">${label}</div>
            <div style="color:#e2e8f0;">${val || '—'}</div>
        </div>`;

    const headerBlock = `
        <div class="row mb-3">
            ${infoRow('Número', `<strong>${txn.transactionNumber}</strong>`)}
            ${infoRow('Tipo', `<span class="badge-type ${typeM.css}">${typeM.icon} ${typeM.label}</span>`)}
            ${infoRow('Estado', statusBadge(txn.idInventoryTransactionStatus))}
            ${infoRow('Fecha', txn.transactionDate)}
            ${infoRow('Origen', warehouseName(txn.idWarehouseOrigin))}
            ${infoRow('Vehículo / Tránsito', txn.idWarehouseDest ? warehouseName(txn.idWarehouseDest) : '—')}
            ${infoRow('Referencia', txn.reference)}
            ${infoRow('Sello Seguridad', txn.securitySeal || '—')}
            ${txn.isTransitTransfer ? infoRow('<i class="bi bi-clock me-1"></i>Hora Salida (enc.)', txn.departureTime || '—') : ''}
            ${txn.isTransitTransfer ? infoRow('<i class="bi bi-speedometer me-1"></i>Km Salida (enc.)', txn.odometerOut != null ? txn.odometerOut.toLocaleString('es-CR') + ' km' : '—') : ''}
        </div>
        <hr style="border-color:#2a3a5c;">`;

    // Para TransitTransfer: grupos bodega por bodega
    if (txn.isTransitTransfer) {
        const groups = _getReceiveDestGroups(txn);
        const activeIdx = _activeReceiveGroupIdx(groups);

        let groupsHtml = '';
        groups.forEach((g, gIdx) => {
            const isActive   = gIdx === activeIdx;
            const isReceived = g.lineStatus === 'Received' || g.lineStatus === 'Cancelled';
            const isPending  = !isReceived && !isActive;
            const isLastDest = gIdx === groups.length - 1;

            const usedBadges = groups.filter(x => x.idWarehouseDestLine).map(x => {
                const w = INV.warehouses.find(wh => wh.id === x.idWarehouseDestLine);
                const isCur = x.idWarehouseDestLine === g.idWarehouseDestLine;
                return w ? `<span class="badge" style="background:${isCur ? '#2d1b6e' : '#1d3557'};color:${isCur ? '#c4b5fd' : '#93c5fd'};font-size:.72rem;">${w.code}</span>` : '';
            }).join(' ');

            // prevKm: desde el grupo anterior ya recibido o desde el encabezado
            let prevKm = txn.odometerOut || 0;
            if (gIdx > 0) {
                const prevGroup = groups[gIdx - 1];
                if (prevGroup?.odometerOut) prevKm = prevGroup.odometerOut;
            }

            // Hora de salida de referencia para validar Hora Llegada
            const prevGroupDeparture = gIdx === 0
                ? (txn.departureTime || '')
                : (groups[gIdx - 1]?.departureTime || txn.departureTime || '');
            const prevGroupDepartureLabel = gIdx === 0
                ? 'encabezado'
                : warehouseName(groups[gIdx - 1]?.idWarehouseDestLine);

            // Artículos en tabla
            const artRows = g.lines.map(l => {
                const alreadyDone = !isActive || isReceived;

                if (alreadyDone) {
                    return `<tr style="opacity:${isReceived ? '.6' : '1'}">
                        <td style="width:36px;"></td>
                        <td><strong>${l.itemCode}</strong><br><small style="color:#64748b!important;">${l.itemName}</small></td>
                        <td class="text-center">${l.qtyRequested}</td>
                        <td class="text-center">${l.qtyDispatched || 0}</td>
                        <td class="text-center text-success">${l.qtyReceived || 0}</td>
                        <td class="text-center text-warning">${l.qtyReturned || 0}</td>
                        <td class="text-center">${isReceived ? '✅ Received' : '⏳ Pending'}</td>
                    </tr>`;
                }

                const dispatched = l.qtyDispatched || l.qtyRequested || 0;
                return `<tr id="rcv-row-${l.id}" data-is-new="false">
                    <td class="text-center" style="width:36px;vertical-align:middle;">
                        <input type="checkbox" class="form-check-input rcv-line-chk" data-line-id="${l.id}"
                               data-dispatched="${dispatched}" style="width:1.1rem;height:1.1rem;cursor:pointer;"
                               title="Marcar como recibido con total despachado">
                    </td>
                    <td><strong>${l.itemCode}</strong><br><small style="color:#64748b!important;">${l.itemName}</small></td>
                    <td class="text-center">${l.qtyRequested}</td>
                    <td class="text-center">${dispatched}</td>
                    <td class="text-center" style="min-width:90px;">
                        <input type="number" id="rcv-qty-${l.id}" class="form-control form-control-sm rcv-qty-input text-center"
                               value="${dispatched}" min="0" step="any" data-dispatched="${dispatched}"
                               style="background:#0d1117;color:#4ade80;border-color:#2a3a5c;width:90px;margin:auto;">
                    </td>
                    <td class="text-center" style="min-width:90px;">
                        <input type="number" id="rcv-return-${l.id}" class="form-control form-control-sm rcv-return-input text-center"
                               value="${l.qtyReturned || 0}" min="0" step="any"
                               style="background:#0d1117;color:#fbbf24;border-color:#2a3a5c;width:90px;margin:auto;"
                               placeholder="0"
                               ${!INV.allowsReturnsInTransit ? 'disabled readonly' : ''}>
                    </td>
                    <td class="text-center">⏳ Pending</td>
                </tr>`;
            }).join('');

            let borderColor = isActive ? 'var(--inv-green)' : (isReceived ? '#22c55e55' : '#2a3a5c');
            let headerColor = isActive ? '#22c55e' : (isReceived ? '#4ade80' : '#94a3b8');
            let statusTag   = isActive
                ? `<span class="badge bg-success ms-2" style="font-size:.72rem;">⬅ ACTIVO — RECIBIR</span>`
                : (isReceived
                    ? `<span class="badge" style="background:#14532d;color:#4ade80;font-size:.72rem;">✅ Recibido</span>`
                    : `<span class="badge" style="background:#1e3a5f;color:#60a5fa;font-size:.72rem;">⏳ Pendiente</span>`);

            // Campos de recepción (solo en grupo activo)
            const receiveFields = isActive ? `
                <div class="row g-2 mb-2 mt-1" id="rcv-fields-${gIdx}">
                    <div class="col-12 col-md-4">
                        <label class="form-label" style="color:#94a3b8;font-size:.78rem;">
                            <i class="bi bi-shield-lock me-1 text-warning"></i>Sello Destino${isLastDest ? '' : ' *'}
                        </label>
                        <input type="text" id="rcvSeal${gIdx}" class="form-control form-control-sm"
                               value="${g.destSecuritySeal || ''}" maxlength="50" autocomplete="off"
                               placeholder="Ingrese el sello de seguridad…"
                               data-txn-id="${txn.id}">
                        <small id="rcvSealErr${gIdx}" style="color:#f87171!important;display:none;">
                            <i class="bi bi-exclamation-triangle me-1"></i>Este sello ya está en uso, debe ser único.
                        </small>
                        <small id="rcvSealOk${gIdx}" style="color:#4ade80!important;display:none;">
                            <i class="bi bi-check-circle me-1"></i>Sello disponible.
                        </small>
                        <small style="color:#cbd5e1!important;">${isLastDest ? 'Opcional en la última bodega destino' : 'Obligatorio y único en todo el sistema'}</small>
                    </div>
                    <div class="col-6 col-md-2">
                        <label class="form-label" style="color:#94a3b8;font-size:.78rem;">
                            <i class="bi bi-clock-fill me-1 text-info"></i>Hora Llegada *
                        </label>
                        <input type="time" id="rcvArrTime${gIdx}" class="form-control form-control-sm" step="60"
                               placeholder="--:--" required
                               data-prev-departure="${prevGroupDeparture}">
                        <small id="rcvArrTimeErr${gIdx}" style="color:#f87171!important;display:none;">Debe ser mayor a la Hora de Salida del ${prevGroupDepartureLabel}</small>
                        <small style="color:#cbd5e1!important;">Obligatorio${prevGroupDeparture ? ` (ref: ${prevGroupDeparture} — ${prevGroupDepartureLabel})` : ''}</small>
                    </div>
                    <div class="col-6 col-md-2">
                        <label class="form-label" style="color:#94a3b8;font-size:.78rem;">
                            <i class="bi bi-clock me-1 text-warning"></i>Hora Salida *
                        </label>
                        <input type="time" id="rcvDeptTime${gIdx}" class="form-control form-control-sm"
                               value="${g.departureTime || ''}" step="60" required
                               placeholder="--:--">
                        <small id="rcvDeptTimeErr${gIdx}" style="color:#f87171!important;display:none;">Debe ser mayor a la Hora de Llegada</small>
                        <small style="color:#cbd5e1!important;">Obligatorio (Debe ser mayor a la Hora de Llegada)</small>
                    </div>
                    <div class="col-6 col-md-2">
                        <label class="form-label" style="color:#94a3b8;font-size:.78rem;">
                            <i class="bi bi-speedometer me-1 text-warning"></i>Km Salida *
                        </label>
                        <input type="number" id="rcvOdoOut${gIdx}" class="form-control form-control-sm"
                               placeholder="0" min="0" step="1" required
                               data-prev-km="${prevKm}"
                               data-header-odometer="${txn.odometerOut || 0}">
                        <small id="rcvOdoOutErr${gIdx}" style="color:#f87171!important;display:none;">Debe ser mayor al Km anterior</small>
                        <small class="rcvOdoHelp${gIdx}" style="color:#cbd5e1!important;">Km al salir hacia siguiente destino${prevKm > 0 ? ` (anterior: ${prevKm})` : ''}${txn.odometerOut ? ` / (encabezado: ${txn.odometerOut})` : ''}</small>
                    </div>
                </div>` : '';

                        // Observaciones del despacho + campo para observaciones al recibir
                        const prevNotes = g.notes || '';
                        const notesField = isActive ? `
                            <div class="row g-2 mb-2 mt-2">
                                <div class="col-12">
                                    <label class="form-label" style="color:#94a3b8;font-size:.78rem;">
                                        <i class="bi bi-chat-left-text me-1 text-info"></i>Observaciones
                                    </label>
                                    ${prevNotes ? `<div style="background:#0d1117;border:1px solid #2a3a5c;border-radius:6px;padding:.5rem .75rem;color:#94a3b8;font-size:.82rem;white-space:pre-wrap;margin-bottom:.4rem;">
                                        <small style="color:#4ade80!important;font-weight:600;display:block;margin-bottom:.25rem;"><i class="bi bi-lock-fill me-1"></i>Observaciones del despacho (solo lectura):</small>${_escapeHtml(prevNotes)}</div>` : ''}
                                    <textarea id="rcvNotes${gIdx}" class="form-control form-control-sm" rows="2"
                                              placeholder="Observaciones al recibir esta bodega… (se concatenarán a las anteriores)"
                                              style="background:#0d1117;color:#e2e8f0;border-color:#2a3a5c;resize:vertical;font-size:.85rem;"></textarea>
                                    <small style="color:#cbd5e1!important;">${prevNotes ? 'Se agregarán después de las observaciones del despacho.' : 'Opcional — observaciones al recibir esta bodega.'}</small>
                                </div>
                            </div>` : (prevNotes ? `
                            <div class="row g-2 mb-2 mt-2">
                                <div class="col-12">
                                    <label class="form-label" style="color:#94a3b8;font-size:.78rem;">
                                        <i class="bi bi-chat-left-text me-1 text-info"></i>Observaciones
                                    </label>
                                    <div style="background:#0d1117;border:1px solid #2a3a5c;border-radius:6px;padding:.5rem .75rem;color:#cbd5e1;font-size:.82rem;white-space:pre-wrap;">${_escapeHtml(prevNotes)}</div>
                                </div>
                            </div>` : '');

                        // Firma digital del receptor (solo en grupo activo, se coloca al final)
                        const signatureField = isActive ? `
                            <div class="row g-2 mb-2 mt-3">
                                <div class="col-12">
                                    <label class="form-label fw-semibold" style="color:#fbbf24;font-size:.8rem;">
                                        <i class="bi bi-pen me-1 text-warning"></i>Firma del Receptor *
                                        <span style="color:#94a3b8;font-size:.72rem;font-weight:normal;">— Firme con el dedo o lápiz táctil</span>
                                    </label>
                                    <div style="border:2px solid #fbbf24;border-radius:8px;background:#fff;position:relative;touch-action:none;user-select:none;">
                                        <canvas id="rcvSignCanvas${gIdx}" height="160"
                                                style="display:block;width:100%;height:160px;border-radius:6px;cursor:crosshair;touch-action:none;">
                                        </canvas>
                                        <button type="button" id="rcvSignClear${gIdx}"
                                                style="position:absolute;top:6px;right:8px;background:rgba(30,30,40,.75);border:1px solid #475569;color:#94a3b8;border-radius:5px;font-size:.72rem;padding:2px 8px;cursor:pointer;">
                                            <i class="bi bi-eraser me-1"></i>Limpiar
                                        </button>
                                    </div>
                                    <small id="rcvSignErr${gIdx}" style="color:#f87171!important;display:none;">
                                        <i class="bi bi-exclamation-triangle me-1"></i>La firma es obligatoria para confirmar la recepción.
                                    </small>
                                </div>
                            </div>` : '';

            // Firma guardada — mostrar si el grupo ya fue recibido y tiene firma
            const savedSignatureBlock = isReceived && g.signature ? `
                <div class="row g-2 mb-2 mt-1">
                    <div class="col-12">
                        <div style="color:#4ade80;font-size:.78rem;font-weight:600;margin-bottom:4px;">
                            <i class="bi bi-pen-fill me-1"></i>Firma del Receptor
                        </div>
                        <div style="border:2px solid #22c55e55;border-radius:8px;background:#fff;display:inline-block;padding:4px;">
                            <img src="${g.signature}" alt="Firma del receptor"
                                 style="display:block;max-width:100%;max-height:120px;border-radius:4px;"
                                 title="Firma capturada al confirmar recepción">
                        </div>
                    </div>
                </div>` : '';

            // Checkbox marcar todas (solo en grupo activo)
            const markAllField = isActive ? `
                <div class="row g-2 mb-2 mt-1">
                    <div class="col-12 col-md-5">
                        <div class="form-check" title="Marcar todas las líneas como recibidas con el total despachado">
                            <input class="form-check-input rcv-mark-all-chk" type="checkbox"
                                   id="rcvMarkAll${gIdx}" data-group="${gIdx}"
                                   style="width:1.1rem;height:1.1rem;cursor:pointer;">
                            <label class="form-check-label" for="rcvMarkAll${gIdx}"
                                   style="color:#cbd5e1;font-size:.78rem;cursor:pointer;">Marcar todas como recibidas (total despachado)</label>
                        </div>
                    </div>
                </div>` : '';

            groupsHtml += `
            <div class="transit-group-block mb-3"
                 style="background:#111827;border:1px solid ${borderColor};border-radius:10px;padding:1rem;${isActive ? '' : 'display:none;'}"
                 data-group-idx="${gIdx}" data-is-active="${isActive}" data-is-received="${isReceived}">
                <!-- Encabezado del grupo -->
                <div class="row g-2 align-items-end mb-2">
                    <div class="col-12 col-md-6">
                        <label class="form-label fw-semibold" style="color:${headerColor};font-size:.8rem;">
                            <i class="bi bi-geo-alt me-1"></i>Bodega Destino Final ${statusTag}
                        </label>
                        <div class="form-control form-control-sm" style="background:#0d1117;color:#e2e8f0;pointer-events:none;">
                            ${warehouseName(g.idWarehouseDestLine)}
                        </div>
                        <small style="color:#cbd5e1!important;">Las bodegas ya usadas en este movimiento no aparecen.</small>
                    </div>
                    <div class="col-12 col-md-6">
                        <div class="d-flex flex-wrap gap-1 align-items-center mt-2">
                            <small class="text-muted" style="font-size:.72rem;">Bodegas usadas en este movimiento:</small>
                            <span>${usedBadges || '—'}</span>
                        </div>
                    </div>
                </div>
                ${markAllField}
                <div class="d-flex justify-content-between align-items-center mb-2 mt-2">
                    <span class="fw-semibold text-light" style="font-size:.85rem;"><i class="bi bi-list-ul me-1"></i>Artículos</span>
                    ${isActive && INV.allowsReturnsInTransit ? `<button type="button" class="btn btn-sm btn-outline-warning" onclick="openAddReturnItem(${gIdx})" title="Agregar artículo de devolución">
                        <i class="bi bi-plus-circle me-1"></i>Agregar Devolución
                    </button>` : ''}
                </div>
                <div class="table-responsive">
                    <table class="table table-sm" style="color:var(--inv-text);">
                        <thead style="background:#0d1117;">
                            <tr>
                                <th style="width:36px;"></th>
                                <th>Artículo</th>
                                <th class="text-center">Solicitado</th>
                                <th class="text-center">Despachado</th>
                                <th class="text-center">Recibido</th>
                                <th class="text-center" style="color:#fbbf24;">Devolución</th>
                                <th class="text-center">Estado</th>
                            </tr>
                        </thead>
                        <tbody>${artRows}</tbody>
                    </table>
                </div>
                ${receiveFields}
                ${notesField}
                ${signatureField}
                ${savedSignatureBlock}
            </div>`;
        });

        return `${headerBlock}<div id="rcv-groups-container">${groupsHtml}</div>`;
    }

    // Para movimientos no-TransitTransfer: recepción simple
    const allLines = txn.lines || [];
    const canReceiveLines = allLines.filter(l => l.lineStatus !== 'Received' && l.lineStatus !== 'Cancelled');

    const simpleRows = allLines.map(l => {
        const ls = { Pending: '⏳', InTransit: '🚛', Received: '✅', Cancelled: '❌' }[l.lineStatus] || '';
        const alreadyDone = l.lineStatus === 'Received' || l.lineStatus === 'Cancelled';
        const dispatched = l.qtyDispatched || l.qtyRequested || 0;

        if (alreadyDone) {
            return `<tr style="opacity:.6">
                    <td style="width:36px;"></td>
                    <td><strong>${l.itemCode}</strong><br><small style="color:#64748b!important;">${l.itemName}</small></td>
                <td class="text-center">${l.qtyRequested}</td>
                <td class="text-center">${dispatched}</td>
                <td class="text-center text-success">${l.qtyReceived || 0}</td>
                <td class="text-center">${ls} ${l.lineStatus}</td>
            </tr>`;
        }

        return `<tr id="rcv-row-${l.id}">
            <td class="text-center" style="width:36px;vertical-align:middle;">
                <input type="checkbox" class="form-check-input rcv-line-chk" data-line-id="${l.id}"
                       data-dispatched="${dispatched}" style="width:1.1rem;height:1.1rem;cursor:pointer;">
            </td>
            <td><strong>${l.itemCode}</strong><br><small style="color:#64748b!important;">${l.itemName}</small></td>
            <td class="text-center">${l.qtyRequested}</td>
            <td class="text-center">${dispatched}</td>
            <td class="text-center" style="min-width:90px;">
                <input type="number" id="rcv-qty-${l.id}" class="form-control form-control-sm rcv-qty-input text-center"
                       value="${dispatched}" min="0" step="any" data-dispatched="${dispatched}"
                       style="background:#0d1117;color:#4ade80;border-color:#2a3a5c;width:90px;margin:auto;">
            </td>
            <td class="text-center">${ls} ${l.lineStatus}</td>
        </tr>`;
    }).join('');

    return `${headerBlock}
        <div class="row g-2 mb-3">
            <div class="col-6 col-md-3">
                <label class="form-label" style="color:#94a3b8;font-size:.78rem;">
                    <i class="bi bi-clock-fill me-1 text-info"></i>Hora Llegada *
                </label>
                <input type="time" id="rcvSimpleArrTime" class="form-control form-control-sm" step="60" required>
                <small style="color:#cbd5e1!important;">Obligatorio</small>
            </div>
            <div class="col-6 col-md-3">
                <label class="form-label" style="color:#94a3b8;font-size:.78rem;">
                    <i class="bi bi-speedometer me-1 text-warning"></i>Km Salida *
                </label>
                <input type="number" id="rcvSimpleOdoOut" class="form-control form-control-sm"
                       placeholder="0" min="0" step="1" required>
                <small style="color:#cbd5e1!important;">Obligatorio</small>
            </div>
        </div>
        <div class="table-responsive">
            <div class="d-flex justify-content-between align-items-center mb-2">
                <span class="fw-semibold text-light" style="font-size:.85rem;"><i class="bi bi-list-ul me-1"></i>Artículos</span>
                <div class="form-check mb-0" title="Marcar todas las líneas como recibidas con el total despachado">
                    <input class="form-check-input rcv-mark-all-chk" type="checkbox" id="rcvSimpleMarkAll"
                           style="width:1.1rem;height:1.1rem;cursor:pointer;">
                    <label class="form-check-label" for="rcvSimpleMarkAll"
                           style="color:#cbd5e1;font-size:.78rem;cursor:pointer;">Marcar todas recibidas</label>
                </div>
            </div>
            <table class="table table-sm" style="color:var(--inv-text);">
                <thead style="background:#0d1117;">
                    <tr>
                        <th style="width:36px;"></th>
                        <th>Artículo</th>
                        <th class="text-center">Solicitado</th>
                        <th class="text-center">Despachado</th>
                        <th class="text-center">Recibido</th>
                        <th class="text-center">Estado</th>
                    </tr>
                </thead>
                <tbody>${simpleRows}</tbody>
            </table>
        </div>`;
}

function renderReceiveFooter(txn, footer) {
    const btns = ['<button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cerrar</button>'];

    const canReceive = txnStatusIs(txn.idInventoryTransactionStatus, 'InTransit') || txnStatusIs(txn.idInventoryTransactionStatus, 'PartiallyReceived');
    if (canReceive) {
        const groups = txn.isTransitTransfer ? _getReceiveDestGroups(txn) : null;
        const activeIdx = groups ? _activeReceiveGroupIdx(groups) : -1;

        if (txn.isTransitTransfer && activeIdx >= 0) {
            const nextGroup = groups[activeIdx + 1];
            btns.push(`<button type="button" class="btn btn-success" id="btnSubmitReceive"
                        onclick="submitReceive(${txn.id}, ${activeIdx}, ${nextGroup ? nextGroup.destId : 'null'})">
                        <i class="bi bi-box-arrow-in-down me-1"></i>Confirmar Recepción
                       </button>`);
        } else if (!txn.isTransitTransfer) {
            btns.push(`<button type="button" class="btn btn-success" id="btnSubmitReceive"
                        onclick="submitReceiveSimple(${txn.id})">
                        <i class="bi bi-box-arrow-in-down me-1"></i>Confirmar Recepción
                       </button>`);
        }
    }

    if (!txnStatusIs(txn.idInventoryTransactionStatus, 'Completed') && !txnStatusIs(txn.idInventoryTransactionStatus, 'Cancelled')) {
        btns.push(`<button type="button" class="btn btn-outline-danger" onclick="openCancelFromReceive(${txn.id})">
                    <i class="bi bi-x-circle me-1"></i>Cancelar
                   </button>`);
    }

    footer.innerHTML = btns.join('');
}

async function submitReceive(txnId, activeGroupIdx, nextWarehouseId) {
    // Obtain active group lines first
    const txn = INV.currentTxn;
    const groups = _getReceiveDestGroups(txn);
    const activeGroup = groups[activeGroupIdx];
    if (!activeGroup) return;

    const pendingLines = activeGroup.lines || [];
    if (!pendingLines.length) {
        showInfoDialog('No hay líneas pendientes de recepción en esta bodega.', 'info');
        return;
    }

    // ============================================================
    // VALIDACIÓN 1: ARTÍCULOS RECIBIDOS
    // ============================================================

    // Validate that ALL pending lines are checked
    const uncheckedLines = pendingLines.filter(l => {
        const chk = document.querySelector(`.rcv-line-chk[data-line-id="${l.id}"]`);
        return !chk || !chk.checked;
    });

    if (uncheckedLines.length > 0) {
        const names = uncheckedLines.map(l => l.itemCode).join(', ');
        showInfoDialog(`Todos los artículos deben estar marcados como recibidos antes de confirmar la recepción. Pendientes sin marcar: ${names}`, 'warning');
        return;
    }

    const checkedLineIds = pendingLines.map(l => l.id);

    // ============================================================
    // VALIDACIÓN 1B: CANTIDADES (Recibido O Devolución > 0)
    // ============================================================
    // Igual que en Nuevo Movimiento: al menos uno de los dos campos debe ser > 0
    for (const lineId of checkedLineIds) {
        const qtyInput = document.getElementById(`rcv-qty-${lineId}`);
        const returnInput = document.getElementById(`rcv-return-${lineId}`);
        const qty = qtyInput ? parseFloat(qtyInput.value) : 0;
        const qtyReturned = returnInput ? parseFloat(returnInput.value) : 0;

        // Validar que al menos uno sea > 0
        if ((!qty || qty <= 0) && (!qtyReturned || qtyReturned <= 0)) {
            const line = pendingLines.find(l => l.id === lineId);
            const name = line ? `${line.itemCode} — ${line.itemName}` : `Línea #${lineId}`;
            showInfoDialog(`La cantidad recibida O la cantidad de devolución de "${name}" debe ser mayor a 0.`, 'warning');
            qtyInput?.focus();
            return;
        }
    }

    // ============================================================
    // VALIDACIÓN 2: DEVOLUCIONES (si hay líneas nuevas)
    // ============================================================

    const newReturnLines = [];
    const groupBlock = document.querySelector(`.transit-group-block[data-group-idx="${activeGroupIdx}"][data-is-active="true"]`);
    if (groupBlock) {
        const newRows = groupBlock.querySelectorAll('tr[data-is-new="true"]');
        for (const row of newRows) {
            const tempId = row.dataset.tempId || row.id.replace('rcv-row-', '');
            const itemId = parseInt(row.dataset.itemId);
            const returnInput = document.getElementById(`rcv-return-${tempId}`);
            const qtyReturned = returnInput ? parseFloat(returnInput.value) : 0;

            // Validar que se haya seleccionado un artículo
            if (!itemId || itemId <= 0) {
                showInfoDialog('Debe seleccionar un artículo válido de la lista o eliminar la línea.', 'warning');
                const searchInput = document.getElementById(`return-item-search-${tempId}`);
                if (searchInput) searchInput.focus();
                return;
            }

            // Validar que la devolución sea mayor a 0 para líneas nuevas
            if (!qtyReturned || qtyReturned <= 0) {
                const searchInput = document.getElementById(`return-item-search-${tempId}`);
                const itemText = searchInput ? searchInput.value : 'Artículo';
                showInfoDialog(`La cantidad de devolución del artículo "${itemText}" debe ser mayor a 0 o debe eliminar la línea.`, 'warning');
                returnInput?.focus();
                return;
            }

            newReturnLines.push({
                itemId: itemId,
                qtyReturned: qtyReturned
            });
        }
    }

    // ============================================================
    // VALIDACIÓN 3: SELLO DE SEGURIDAD
    // ============================================================

    const currentSealEl = document.getElementById(`rcvSeal${activeGroupIdx}`);
    const currentSeal   = currentSealEl?.value?.trim() || '';
    const isLastGroup   = !nextWarehouseId; // nextWarehouseId null/falsy → último destino

    if (!isLastGroup && !currentSeal) {
        showInfoDialog('El Sello Destino es obligatorio para confirmar la recepción (excepto en la última bodega destino).', 'warning');
        currentSealEl?.focus();
        return;
    }

    // Real-time indicator already checked, but do a final server-side uniqueness check (only if seal was entered)
    if (currentSeal) {
        try {
            const url = `/api/inventorytransaction/check-any-seal?seal=${encodeURIComponent(currentSeal)}`;
            const { exists } = await invFetch(url);
            if (exists) {
                showInfoDialog(`El sello '${currentSeal}' ya está en uso. Debe ingresar un sello único.`, 'warning');
                currentSealEl?.focus();
                const errEl = document.getElementById(`rcvSealErr${activeGroupIdx}`);
                if (errEl) errEl.style.display = '';
                currentSealEl.style.borderColor = '#ef4444';
                return;
            }
        } catch {}
    }

    // ============================================================
    // VALIDACIÓN 4: HORA DE LLEGADA
    // ============================================================

    const arrTimeEl  = document.getElementById(`rcvArrTime${activeGroupIdx}`);
    const arrTime    = arrTimeEl?.value?.trim();
    const prevDeparture = arrTimeEl?.dataset?.prevDeparture || '';

    if (!arrTime) {
        showInfoDialog('La Hora de Llegada es obligatoria para confirmar la recepción.', 'warning');
        arrTimeEl?.focus();
        return;
    }

    // Hora Llegada must be > previous departure (header for first group, prev group's departure for others)
    if (prevDeparture && arrTime <= prevDeparture) {
        showInfoDialog(`La Hora de Llegada (${arrTime}) debe ser mayor a la Hora de Salida de referencia (${prevDeparture}).`, 'warning');
        arrTimeEl?.focus();
        return;
    }

    // ============================================================
    // VALIDACIÓN 5: HORA DE SALIDA
    // ============================================================

    const deptTimeEl = document.getElementById(`rcvDeptTime${activeGroupIdx}`);
    const deptTime   = deptTimeEl?.value?.trim();

    if (!deptTime) {
        showInfoDialog('La Hora de Salida es obligatoria para confirmar la recepción.', 'warning');
        deptTimeEl?.focus();
        return;
    }

    // Hora Salida must be > Hora Llegada
    if (arrTime && deptTime <= arrTime) {
        showInfoDialog(`La Hora de Salida (${deptTime}) debe ser mayor a la Hora de Llegada (${arrTime}).`, 'warning');
        deptTimeEl?.focus();
        return;
    }

    // ============================================================
    // VALIDACIÓN 6: KM DE SALIDA
    // ============================================================

    const odoOutEl   = document.getElementById(`rcvOdoOut${activeGroupIdx}`);
    const odoOut     = odoOutEl?.value?.trim();
    const headerOdometer = parseFloat(odoOutEl?.dataset?.headerOdometer) || 0;
    const prevKm         = parseFloat(odoOutEl?.dataset?.prevKm) || 0;

    if (!odoOut || odoOut === '') {
        showInfoDialog('El Km de Salida es obligatorio para confirmar la recepción.', 'warning');
        odoOutEl?.focus();
        return;
    }

    const odoOutNum = parseFloat(odoOut);
    if (isNaN(odoOutNum) || odoOutNum < 0) {
        showInfoDialog('El Km de Salida debe ser un número válido mayor o igual a cero.', 'warning');
        odoOutEl?.focus();
        return;
    }

    // Km must be > header odometer
    if (headerOdometer > 0 && odoOutNum <= headerOdometer) {
        showInfoDialog(`El Km de Salida (${odoOutNum.toLocaleString('es-CR')}) debe ser mayor al Km de Salida del encabezado (${headerOdometer.toLocaleString('es-CR')}).`, 'warning');
        odoOutEl?.focus();
        return;
    }

    // Km must also be > previous group km
    if (prevKm > 0 && odoOutNum <= prevKm) {
        showInfoDialog(`El Km de Salida (${odoOutNum.toLocaleString('es-CR')}) debe ser mayor al kilometraje anterior (${prevKm.toLocaleString('es-CR')}).`, 'warning');
        odoOutEl?.focus();
        return;
    }

    // ============================================================
    // VALIDACIÓN 7: FIRMA (SIEMPRE AL FINAL)
    // ============================================================

    const signatureDataUrl = _getSignatureDataUrl(activeGroupIdx);
    if (!signatureDataUrl) {
        const errEl = document.getElementById(`rcvSignErr${activeGroupIdx}`);
        if (errEl) errEl.style.display = '';
        showInfoDialog('La firma del receptor es obligatoria para confirmar la recepción.', 'warning');
        document.getElementById(`rcvSignCanvas${activeGroupIdx}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        return;
    }

    // ============================================================
    // RECOPILACIÓN DE DATOS Y ENVÍO
    // ============================================================

    // Observaciones al recibir (textarea rcvNotes{activeGroupIdx})
    const rcvNotesEl = document.getElementById(`rcvNotes${activeGroupIdx}`);
    const rcvNotesValue = rcvNotesEl?.value?.trim() || null;

    // Recopilar cantidades recibidas y devoluciones de líneas existentes
    const lineIds  = checkedLineIds;
    const lineQtys = checkedLineIds.map(lineId => {
        const l = pendingLines.find(x => x.id === lineId);
        const qtyInput = document.getElementById(`rcv-qty-${lineId}`);
        const returnInput = document.getElementById(`rcv-return-${lineId}`);
        const qty = qtyInput ? parseFloat(qtyInput.value) : null;
        const qtyReturned = returnInput ? (parseFloat(returnInput.value) || 0) : 0;
        return { 
            lineId, 
            qty: (qty > 0 ? qty : (l?.qtyDispatched || l?.qtyRequested || 0)),
            qtyReturned: qtyReturned
        };
    });

    const btn = document.getElementById('btnSubmitReceive');
    if (btn) { btn.disabled = true; btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Procesando…'; }

    try {
        await invFetch(`/api/inventorytransaction/${txnId}/receive`, {
            method: 'PATCH',
            body: {
                lineIds,
                lineQtys,
                newReturnLines,
                arrivalTime:      arrTime,
                departureTime:    deptTime,
                odometerOut:      odoOutNum,
                destSeal:         currentSeal,
                nextWarehouseId:  nextWarehouseId || null,
                signature:        signatureDataUrl,
                transitGroupId:   activeGroup.id || null,
                notes:            rcvNotesValue,
            },
        });
        bootstrap.Modal.getInstance(document.getElementById('receiveModal')).hide();
        showAlert('Recepción registrada correctamente.', 'success');
        loadMovements(INV.page);
    } catch (e) {
        if (btn) { btn.disabled = false; btn.innerHTML = '<i class="bi bi-box-arrow-in-down me-1"></i>Confirmar Recepción'; }
        showInfoDialog(e.message, 'danger');
    }
}

async function submitReceiveSimple(txnId) {
    const arrTime = document.getElementById('rcvSimpleArrTime')?.value?.trim();
    const odoOut  = document.getElementById('rcvSimpleOdoOut')?.value?.trim();

    if (!arrTime) {
        showInfoDialog('La Hora de Llegada es obligatoria para confirmar la recepción.', 'warning');
        document.getElementById('rcvSimpleArrTime')?.focus();
        return;
    }
    if (!odoOut || parseFloat(odoOut) < 0) {
        showInfoDialog('El Km de Salida es obligatorio para confirmar la recepción.', 'warning');
        return;
    }

    const txn = INV.currentTxn;
    const pendingLines = (txn.lines || []).filter(l => l.lineStatus !== 'Received' && l.lineStatus !== 'Cancelled');

    if (!pendingLines.length) {
        showInfoDialog('No hay líneas pendientes de recepción.', 'info');
        return;
    }

    const lineIds  = pendingLines.map(l => l.id);
    const lineQtys = pendingLines.map(l => {
        const qtyInput = document.getElementById(`rcv-qty-${l.id}`);
        const qty = qtyInput ? parseFloat(qtyInput.value) : null;
        return { lineId: l.id, qty: (qty > 0 ? qty : (l.qtyDispatched || l.qtyRequested || 0)) };
    });

    const btn = document.getElementById('btnSubmitReceive');
    if (btn) { btn.disabled = true; btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Procesando…'; }

    try {
        await invFetch(`/api/inventorytransaction/${txnId}/receive`, {
            method: 'PATCH',
            body: {
                lineIds,
                lineQtys,
                arrivalTime:  arrTime,
                odometerOut:  parseFloat(odoOut),
            },
        });
        bootstrap.Modal.getInstance(document.getElementById('receiveModal')).hide();
        showAlert('Recepción registrada correctamente.', 'success');
        loadMovements(INV.page);
    } catch (e) {
        if (btn) { btn.disabled = false; btn.innerHTML = '<i class="bi bi-box-arrow-in-down me-1"></i>Confirmar Recepción'; }
        showInfoDialog(e.message, 'danger');
    }
}

function openCancelFromReceive(id) {
    const rcvModal = bootstrap.Modal.getInstance(document.getElementById('receiveModal'));
    if (rcvModal) rcvModal.hide();
    openCancel(id);
}

// ============================================================
// AGREGAR ARTÍCULO DE DEVOLUCIÓN
// ============================================================

function openAddReturnItem(groupIdx) {
    // Buscar la tabla del grupo activo
    const groupBlock = document.querySelector(`.transit-group-block[data-group-idx="${groupIdx}"][data-is-active="true"]`);
    if (!groupBlock) return;

    const tbody = groupBlock.querySelector('tbody');
    if (!tbody) return;

    // Generar ID temporal para la nueva línea
    const tempId = `new-${Date.now()}`;
    const rowId = `rcv-row-${tempId}`;

    // Crear nueva fila con campo de búsqueda autocompletado
    const newRow = document.createElement('tr');
    newRow.id = rowId;
    newRow.dataset.isNew = 'true';
    newRow.dataset.itemId = '0';
    newRow.dataset.tempId = tempId;

    newRow.innerHTML = `
        <td class="text-center" style="width:36px;vertical-align:middle;">
            <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeReturnItemRow('${tempId}')" title="Eliminar">
                <i class="bi bi-x-lg"></i>
            </button>
        </td>
        <td style="position:relative;">
            <input type="text" 
                   id="return-item-search-${tempId}" 
                   class="form-control form-control-sm return-item-search" 
                   placeholder="Código o nombre..."
                   data-temp-id="${tempId}"
                   style="background:#0d1117;color:#e2e8f0;border-color:#2a3a5c;">
            <div id="return-sugg-${tempId}" style="display:none;"></div>
        </td>
        <td class="text-center">
            <input type="number" class="form-control form-control-sm text-center" value="0" readonly 
                   style="background:#0d1117;color:#6b7280;border-color:#2a3a5c;width:90px;margin:auto;">
        </td>
        <td class="text-center">
            <input type="number" class="form-control form-control-sm text-center" value="0" readonly 
                   style="background:#0d1117;color:#6b7280;border-color:#2a3a5c;width:90px;margin:auto;">
        </td>
        <td class="text-center">
            <input type="number" class="form-control form-control-sm text-center" value="0" readonly 
                   style="background:#0d1117;color:#6b7280;border-color:#2a3a5c;width:90px;margin:auto;">
        </td>
        <td class="text-center" style="min-width:90px;">
            <input type="number" 
                   id="rcv-return-${tempId}" 
                   class="form-control form-control-sm rcv-return-input text-center"
                   value="0" min="0" step="any" required
                   style="background:#0d1117;color:#fbbf24;border-color:#fbbf24;width:90px;margin:auto;"
                   placeholder="Cantidad">
        </td>
        <td class="text-center">
            <span class="badge" style="background:#1e3a5f;color:#fbbf24;">➕ Devolución</span>
        </td>
    `;

    tbody.appendChild(newRow);

    // Bind eventos de autocompletado
    const searchInput = document.getElementById(`return-item-search-${tempId}`);
    let searchTimeout;
    let suggestionsCache = [];

    searchInput.addEventListener('input', async (e) => {
        const term = e.target.value.trim();

        if (term.length < 2) {
            const sugg = document.getElementById(`return-sugg-${tempId}`);
            if (sugg) sugg.style.display = 'none';
            return;
        }

        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(async () => {
            try {
                const data = await invFetch(`/api/item?search=${encodeURIComponent(term)}&pageSize=8&isActive=true`);
                const items = data.items || data || [];
                suggestionsCache = items;
                renderReturnItemSuggestions(tempId, items, searchInput);

                // Auto-seleccionar si hay resultado único exacto por código
                if (items.length === 1) {
                    const it = items[0];
                    if (it.code.toLowerCase() === term.toLowerCase() || 
                        term.toLowerCase() === `${it.code} — ${it.name}`.toLowerCase()) {
                        applyReturnItemSelection(tempId, it.id, it.code, it.name);
                    }
                }
            } catch (err) {
                console.error('Error searching items:', err);
            }
        }, 300);
    });

    searchInput.addEventListener('keydown', (e) => {
        if (e.key !== 'Enter') return;
        e.preventDefault();
        const sugg = document.getElementById(`return-sugg-${tempId}`);
        const first = sugg && sugg.querySelector('.sugg-item');
        if (first) {
            const { id, code, name } = first.dataset;
            applyReturnItemSelection(tempId, id, code, name);
        }
    });

    searchInput.addEventListener('blur', () => {
        setTimeout(() => {
            const term = searchInput.value.trim().toLowerCase();
            const row = document.getElementById(rowId);
            if (!row || row.dataset.itemId !== '0') return;

            if (suggestionsCache.length) {
                const exact = suggestionsCache.find(it =>
                    it.code.toLowerCase() === term ||
                    `${it.code} — ${it.name}`.toLowerCase() === term
                );
                const match = exact || (suggestionsCache.length === 1 ? suggestionsCache[0] : null);
                if (match) {
                    applyReturnItemSelection(tempId, match.id, match.code, match.name);
                    return;
                }
            }

            const sugg = document.getElementById(`return-sugg-${tempId}`);
            if (sugg) sugg.style.display = 'none';
        }, 200);
    });

    // Focus en el campo de búsqueda
    setTimeout(() => searchInput.focus(), 50);
    showAlert('Busque y seleccione el artículo a devolver.', 'info');
}

function renderReturnItemSuggestions(tempId, items, inp) {
    let sugg = document.getElementById(`return-sugg-${tempId}`);
    if (!sugg) return;

    if (!items.length) { 
        sugg.style.display = 'none'; 
        return; 
    }

    sugg.style.cssText = `
        display:block; position:absolute; z-index:9999;
        background:#1a2340; border:1px solid #2a3a5c; border-radius:8px;
        max-height:200px; overflow-y:auto; width:100%;
        box-shadow:0 4px 12px rgba(0,0,0,0.4);
    `;

    sugg.innerHTML = items.map(it =>
        `<div class="sugg-item p-2" style="cursor:pointer;border-bottom:1px solid #2a3a5c;font-size:.82rem;color:#e2e8f0;"
              data-id="${it.id}" data-code="${it.code}" data-name="${it.name}">
            <strong>${it.code}</strong> — ${it.name}
        </div>`
    ).join('');

    sugg.querySelectorAll('.sugg-item').forEach(el => {
        el.addEventListener('mousedown', ev => {
            ev.preventDefault();
            const { id, code, name } = ev.currentTarget.dataset;
            applyReturnItemSelection(tempId, id, code, name);
        });
        el.addEventListener('mouseenter', () => el.style.background = '#1e2d4a');
        el.addEventListener('mouseleave', () => el.style.background = 'transparent');
    });
}

function applyReturnItemSelection(tempId, itemId, code, name) {
    const row = document.getElementById(`rcv-row-${tempId}`);
    if (!row) return;

    row.dataset.itemId = itemId;

    const searchInput = document.getElementById(`return-item-search-${tempId}`);
    if (searchInput) {
        searchInput.value = `${code} — ${name}`;
        searchInput.style.color = '#4ade80'; // Verde para indicar selección válida
    }

    const sugg = document.getElementById(`return-sugg-${tempId}`);
    if (sugg) sugg.style.display = 'none';

    // Focus en el campo de devolución
    const returnInput = document.getElementById(`rcv-return-${tempId}`);
    if (returnInput) {
        returnInput.focus();
        returnInput.select();
    }
}

function removeReturnItemRow(tempId) {
    const row = document.getElementById(`rcv-row-${tempId}`);
    if (row) {
        row.remove();
        showAlert('Artículo eliminado.', 'info');
    }
}

// ============================================================
// DETALLE / RECEPCIÓN
// ============================================================

async function openDetail(id) {
    INV.currentTxn = null;
    const body   = document.getElementById('detailBody');
    const footer = document.getElementById('detailFooter');
    body.innerHTML = '<div class="text-center py-4" style="color:#cbd5e1;">Cargando…</div>';
    footer.innerHTML = '<button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cerrar</button>';
    document.getElementById('detailTitle').innerHTML =
        '<i class="bi bi-file-earmark-text me-2"></i>Detalle del Movimiento';

    bootstrap.Modal.getOrCreateInstance(document.getElementById('detailModal')).show();

    try {
        const txn = await invFetch(`/api/inventorytransaction/${id}`);
        INV.currentTxn = txn;

        const lines = txn.lines || await invFetch(`/api/inventorytransaction/${id}/lines`);
        txn.lines = lines;
        if (txn.isTransitTransfer)
            txn.transitGroups = await invFetch(`/api/inventorytransaction/${id}/transit-groups`);

        document.getElementById('detailTitle').innerHTML =
            `<i class="bi bi-file-earmark-text me-2"></i>${txn.transactionNumber} — ${statusBadge(txn.idInventoryTransactionStatus)}`;

        body.innerHTML = renderDetailBody(txn);
        renderDetailFooter(txn, footer);

    } catch (e) {
        body.innerHTML = `<div class="text-danger text-center py-4">${e.message}</div>`;
    }
}

function renderDetailBody(txn) {
    const typeM = txnTypeInfo(txn.idInventoryTransactionType);

    const infoRow = (label, val) =>
        `<div class="col-6 col-md-3 mb-2">
            <div style="font-size:.72rem;color:#94a3b8;font-weight:600;letter-spacing:.03em;">${label}</div>
            <div style="color:#e2e8f0;">${val || '—'}</div>
        </div>`;

    const header = `
        <div class="row mb-3">
            ${infoRow('Número', `<strong>${txn.transactionNumber}</strong>`)}
            ${infoRow('Tipo', `<span class="badge-type ${typeM.css}">${typeM.icon} ${typeM.label}</span>`)}
            ${infoRow('Estado', statusBadge(txn.idInventoryTransactionStatus))}
            ${infoRow('Fecha', txn.transactionDate)}
            ${infoRow('Origen', warehouseName(txn.idWarehouseOrigin))}
            ${infoRow('Destino', txn.idWarehouseDest ? warehouseName(txn.idWarehouseDest) : '—')}
            ${infoRow('Referencia', txn.reference)}
            ${infoRow('Notas', txn.notes)}
            ${txn.isTransitTransfer ? infoRow('<i class="bi bi-shield-lock me-1 text-warning"></i>Sello Seguridad', txn.securitySeal || '—') : ''}
            ${txn.isTransitTransfer ? infoRow('<i class="bi bi-clock me-1 text-warning"></i>Hora Salida', txn.departureTime || '—') : ''}
            ${txn.isTransitTransfer ? infoRow('<i class="bi bi-speedometer me-1 text-warning"></i>Km Salida', txn.odometerOut != null ? txn.odometerOut.toLocaleString('es-CR') + ' km' : '—') : ''}
        </div>
        <hr style="border-color:#2a3a5c;">`;

    const lineStatM = (s) => ({
        Pending:   { css: 'bg-secondary',        icon: '⏳' },
        InTransit: { css: 'bg-warning text-dark', icon: '🚛' },
        Received:  { css: 'bg-success',           icon: '✅' },
        Rejected:  { css: 'bg-danger',            icon: '❌' },
        Cancelled: { css: 'bg-secondary',         icon: '❌' },
    }[s] || { css: 'bg-secondary', icon: '' });

    // TransitTransfer: agrupar por bodega destino, mostrar firma por grupo
    if (txn.isTransitTransfer && ((txn.transitGroups || []).length > 0 || (txn.lines || []).length > 0)) {
        const groups = _getReceiveDestGroups(txn);
        const groupsHtml = groups.map(g => {
            const isReceived = g.lineStatus === 'Received' || g.lineStatus === 'Cancelled';
            const borderColor = isReceived ? '#22c55e55' : '#2a3a5c';
            const headerColor = isReceived ? '#4ade80' : '#94a3b8';
            const statusTag = isReceived
                ? `<span class="badge" style="background:#14532d;color:#4ade80;font-size:.72rem;">✅ Recibido</span>`
                : `<span class="badge" style="background:#1e3a5f;color:#60a5fa;font-size:.72rem;">⏳ Pendiente</span>`;

            const infoItems = [
                g.destSecuritySeal ? `<span style="font-size:.75rem;color:#cbd5e1;"><i class="bi bi-shield-lock me-1 text-warning"></i>Sello: <strong>${g.destSecuritySeal}</strong></span>` : '',
                g.arrivalTime      ? `<span style="font-size:.75rem;color:#cbd5e1;"><i class="bi bi-clock-fill me-1 text-info"></i>Llegada: <strong>${g.arrivalTime}</strong></span>` : '',
                g.departureTime    ? `<span style="font-size:.75rem;color:#cbd5e1;"><i class="bi bi-clock me-1 text-warning"></i>Salida: <strong>${g.departureTime}</strong></span>` : '',
                g.odometerOut != null ? `<span style="font-size:.75rem;color:#cbd5e1;"><i class="bi bi-speedometer me-1 text-warning"></i>Km: <strong>${g.odometerOut}</strong></span>` : '',
            ].filter(Boolean).join('&nbsp;&nbsp;');

            const notesHtml = g.notes ? `
                <div style="margin-top:8px;padding:.5rem .75rem;background:#0d1117;border:1px solid #2a3a5c;border-radius:6px;">
                    <div style="font-size:.72rem;color:#94a3b8;font-weight:600;margin-bottom:.25rem;">
                        <i class="bi bi-chat-left-text me-1 text-info"></i>Observaciones
                    </div>
                    <div style="font-size:.83rem;color:#cbd5e1;white-space:pre-wrap;">${_escapeHtml(g.notes)}</div>
                </div>` : '';

            const signatureHtml = g.signature ? `
                <div style="margin-top:8px;">
                    <div style="color:#4ade80;font-size:.75rem;font-weight:600;margin-bottom:4px;">
                        <i class="bi bi-pen-fill me-1"></i>Firma del Receptor
                    </div>
                    <div style="border:2px solid #22c55e55;border-radius:8px;background:#fff;display:inline-block;padding:4px;">
                        <img src="${g.signature}" alt="Firma"
                             style="display:block;max-width:300px;max-height:110px;border-radius:4px;"
                             title="Firma capturada al confirmar recepción">
                    </div>
                </div>` : '';

            const rowsHtml = (g.lines || []).map(l => {
                const qtyReturned = l.qtyReturned || 0;
                const returnColor = qtyReturned > 0 ? '#fbbf24' : '#94a3b8';
                return `<tr>
                    <td><strong>${l.itemCode}</strong><br><small class="text-muted">${l.itemName}</small></td>
                    <td class="text-center">${l.qtyRequested}</td>
                    <td class="text-center">${l.qtyDispatched || 0}</td>
                    <td class="text-center text-success">${l.qtyReceived || 0}</td>
                    <td class="text-center" style="color:${returnColor};">${qtyReturned}</td>
                    <td><span class="badge bg-secondary">${isReceived ? '✅' : '⏳'} ${g.lineStatus}</span></td>
                </tr>`;
            }).join('');

            return `
            <div class="mb-3" style="border:1px solid ${borderColor};border-radius:10px;padding:.85rem;background:#111827;">
                <div class="d-flex align-items-center gap-2 mb-2 flex-wrap">
                    <span style="color:${headerColor};font-size:.82rem;font-weight:600;">
                        <i class="bi bi-geo-alt me-1"></i>${warehouseName(g.idWarehouseDestLine)}
                    </span>
                    ${statusTag}
                </div>
                ${infoItems ? `<div class="d-flex flex-wrap gap-3 mb-2">${infoItems}</div>` : ''}
                ${notesHtml}
                ${signatureHtml}
                <div class="table-responsive mt-2">
                    <table class="table table-sm mb-0" style="color:var(--inv-text);">
                        <thead style="background:#0d1117;">
                            <tr>
                                <th>Artículo</th>
                                <th class="text-center">Solicitado</th>
                                <th class="text-center">Despachado</th>
                                <th class="text-center">Recibido</th>
                                <th class="text-center">Devolución</th>
                                <th>Estado</th>
                            </tr>
                        </thead>
                        <tbody>${rowsHtml}</tbody>
                    </table>
                </div>
            </div>`;
        }).join('');

        return `${header}${groupsHtml}`;
    }

    // Movimiento no-TransitTransfer: tabla simple
    const linesHtml = (txn.lines || []).map(l => {
        const m = lineStatM(l.lineStatus);
        const destName = l.idWarehouseDest ? warehouseName(l.idWarehouseDest) : '—';
        return `<tr>
            <td><strong>${l.itemCode}</strong><br><small class="text-muted">${l.itemName}</small></td>
            <td class="text-center">${l.qtyRequested}</td>
            <td class="text-center">${l.qtyDispatched || 0}</td>
            <td class="text-center text-success">${l.qtyReceived || 0}</td>
            <td>${destName}</td>
            <td><span class="badge ${m.css}">${m.icon} ${l.lineStatus}</span></td>
        </tr>`;
    }).join('');

    return `
        ${header}
        <div class="table-responsive">
            <table class="table table-sm" style="color:var(--inv-text);">
                <thead style="background:#111827;">
                    <tr>
                        <th>Artículo</th>
                        <th class="text-center">Solicitado</th>
                        <th class="text-center">Despachado</th>
                        <th class="text-center">Recibido</th>
                        <th>Bodega Destino</th>
                        <th>Estado Línea</th>
                    </tr>
                </thead>
                <tbody>${linesHtml}</tbody>
            </table>
        </div>`;
}

function renderDetailFooter(txn, footer) {
    const btns = ['<button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cerrar</button>'];

    if (txnStatusIs(txn.idInventoryTransactionStatus, 'InTransit') || txnStatusIs(txn.idInventoryTransactionStatus, 'PartiallyReceived')) {
        btns.push(`<button type="button" class="btn btn-success"
                   onclick="bootstrap.Modal.getInstance(document.getElementById('detailModal')).hide(); openReceive(${txn.id});">
                   <i class="bi bi-box-arrow-in-down me-1"></i>Recibir Movimiento
                 </button>`);
    }

    if (txnStatusIs(txn.idInventoryTransactionStatus, 'Confirmed')) {
        btns.push(`<button type="button" class="btn btn-success" onclick="completeTxn(${txn.id})">
                    <i class="bi bi-check-all me-1"></i>Completar Movimiento
                   </button>`);
    }

    if (!txnStatusIs(txn.idInventoryTransactionStatus, 'Completed') && !txnStatusIs(txn.idInventoryTransactionStatus, 'Cancelled')) {
        btns.push(`<button type="button" class="btn btn-outline-danger" onclick="openCancel(${txn.id})">
                    <i class="bi bi-x-circle me-1"></i>Cancelar
                   </button>`);
    }

    footer.innerHTML = btns.join('');
}

// ============================================================
// CONFIRMAR / COMPLETAR
// ============================================================

async function confirmTxn(id) {
    if (!confirm('¿Confirmar este movimiento? Esto despachará la mercancía desde la bodega origen.')) return;
    try {
        await invFetch(`/api/inventorytransaction/${id}/confirm`, { method: 'PATCH' });
        showAlert('Movimiento confirmado. Mercancía despachada.', 'success');
        loadMovements(INV.page);
    } catch (e) {
        showAlert(e.message, 'danger');
    }
}

async function completeTxn(id) {
    if (!confirm('¿Completar este movimiento? Se actualizarán los saldos de inventario.')) return;
    try {
        await invFetch(`/api/inventorytransaction/${id}/complete`, { method: 'PATCH' });
        const modal = bootstrap.Modal.getInstance(document.getElementById('detailModal'));
        if (modal) modal.hide();
        showAlert('Movimiento completado. Inventario actualizado.', 'success');
        loadMovements(INV.page);
    } catch (e) {
        showAlert(e.message, 'danger');
    }
}

// ============================================================
// CANCELAR
// ============================================================

function openCancel(id) {
    INV.cancelTargetId = id;
    document.getElementById('cancelReason').value = '';
    const detailModal = bootstrap.Modal.getInstance(document.getElementById('detailModal'));
    if (detailModal) detailModal.hide();
    bootstrap.Modal.getOrCreateInstance(document.getElementById('cancelModal')).show();
}

async function executeCancelTxn() {
    const reason = document.getElementById('cancelReason').value.trim();
    if (!reason) {
        alert('Ingrese el motivo de cancelación.');
        return;
    }
    try {
        await invFetch(`/api/inventorytransaction/${INV.cancelTargetId}/cancel`, {
            method: 'PATCH',
            body: { reason },
        });
        bootstrap.Modal.getInstance(document.getElementById('cancelModal')).hide();
        showAlert('Movimiento cancelado.', 'warning');
        loadMovements(INV.page);
    } catch (e) {
        showAlert(e.message, 'danger');
    }
}

// ============================================================
// BIND EVENTS
// ============================================================

function bindEvents() {
    // Nuevo movimiento
    document.getElementById('btnNewMovement').addEventListener('click', openCreate);

    // Guardar
    document.getElementById('btnSaveMovement').addEventListener('click', () => saveMovement(false));
    document.getElementById('btnSaveConfirm').addEventListener('click',  () => saveMovement(true));

    // Agregar línea (solo modo no-tránsito; en tránsito cada grupo tiene su propio botón)
    document.getElementById('btnAddLine').addEventListener('click', () => {
        INV.lines.push({
            idItem: 0, itemCode: '', itemName: '', qtyRequested: 1,
            idWarehouseDestLine: null,
            idUnitOfMeasure: null, unitOfMeasureCode: '',
            unitCost: null, lotNumber: '', notes: '',
        });
        renderLines();
        const modalBody = document.querySelector('#invModal .modal-body');
        if (modalBody) setTimeout(() => modalBody.scrollTo({ top: modalBody.scrollHeight, behavior: 'smooth' }), 50);
    });

    // Tipo de movimiento cambia → actualizar UI
    document.getElementById('fldType').addEventListener('change', () => {
        const typeId = document.getElementById('fldType').value;
        const isTransitMode = txnTypeIs(typeId, 'TransitTransfer');
        if (!isTransitMode && INV._groups.length > 1) {
            showInfoDialog('Debe eliminar los grupos adicionales antes de cambiar a un tipo que no sea Tránsito.');
            // Revertir al ID que corresponde a TransitTransfer
            const transitId = txnTypeIdByCode('TransitTransfer');
            if (transitId !== null) document.getElementById('fldType').value = transitId;
            return;
        }
        updateTransitUI();
    });

    // Impedir seleccionar fecha menor a hoy en fldDate (tanto en crear como en editar)
    document.getElementById('fldDate').addEventListener('input', () => {
        const today  = new Date().toISOString().slice(0, 10);
        const dateEl = document.getElementById('fldDate');
        if (dateEl.value && dateEl.value < today) {
            dateEl.value = today;
            showInfoDialog('La fecha del movimiento no puede ser anterior al día de hoy.', 'warning');
        }
    });

    // Cambio de origen → actualizar opciones de destino
    document.getElementById('fldOrigin').addEventListener('change', () => {
        updateTransitUI();
    });

    // Cambio de bodega tránsito → cargar km actual del vehículo, pre-rellenar fldOdometerOut
    document.getElementById('fldDest').addEventListener('change', async () => {
        INV._vehicleOdometerKm = null;
        const destId = parseInt(document.getElementById('fldDest').value) || null;
        const fldOdo     = document.getElementById('fldOdometerOut');
        const fldOdoErr  = document.getElementById('fldOdoErr');
        const fldOdoHint = document.getElementById('fldOdoHint');
        const busyWarn   = document.getElementById('transitBusyWarn');
        const busyText   = document.getElementById('transitBusyWarnText');

        // Helper: enable/disable save buttons based on busy state
        const setSaveBusy = (busy) => {
            ['btnSaveMovement', 'btnSaveConfirm'].forEach(id => {
                const btn = document.getElementById(id);
                if (btn) btn.disabled = busy;
            });
        };

        // Clear busy warning
        if (busyWarn) busyWarn.classList.add('d-none');
        setSaveBusy(false);

        // Limpiar si se deselecciona o no es TransitTransfer
        if (!destId || !txnTypeIs(document.getElementById('fldType').value, 'TransitTransfer')) {
            if (fldOdo) fldOdo.value = '';
            if (fldOdoErr) fldOdoErr.style.display = 'none';
            return;
        }

        // Check if transit warehouse is busy
        try {
            const excludeId = INV.editingId || undefined;
            const busyUrl = `/api/inventorytransaction/transit-warehouse/${destId}/busy`
                + (excludeId ? `?excludeId=${excludeId}` : '');
            const { isBusy, transactionNumber } = await invFetch(busyUrl);
            if (isBusy) {
                if (busyWarn) busyWarn.classList.remove('d-none');
                if (busyText) busyText.textContent =
                    `Esta bodega de tránsito ya tiene el movimiento activo ${transactionNumber}. ` +
                    'Todos los movimientos deben estar en estado Completado antes de crear uno nuevo.';
                setSaveBusy(true);
                return; // Skip odometer fetch — warehouse is blocked
            }
        } catch { /* no crítico — si falla el check, no bloqueamos */ }

        // Load odometer for the selected transit warehouse
        try {
            const wh = INV.warehouses.find(w => w.id === destId);
            if (wh && wh.idTransportUnit) {
                const unit = await invFetch(`/api/transportunit/${wh.idTransportUnit}`);
                INV._vehicleOdometerKm = unit.currentOdometerKm ?? null;
                // Pre-rellenar con el km actual del vehículo si el campo está vacío
                if (fldOdo && (fldOdo.value === '' || fldOdo.value === '0')) {
                    fldOdo.value = INV._vehicleOdometerKm ?? '';
                }
                if (fldOdoHint && INV._vehicleOdometerKm !== null) {
                    fldOdoHint.textContent = `Km actual del vehículo: ${INV._vehicleOdometerKm.toLocaleString('es-CR')} km`;
                }
            }
        } catch { /* no crítico */ }
    });

    // Validación en tiempo real del Km Salida del encabezado
    document.getElementById('fldOdometerOut').addEventListener('input', () => {
        const fldOdo    = document.getElementById('fldOdometerOut');
        const fldOdoErr = document.getElementById('fldOdoErr');
        if (!fldOdoErr) return;
        const val = parseFloat(fldOdo.value);
        const isInvalid = INV._vehicleOdometerKm !== null
            && !isNaN(val)
            && val < INV._vehicleOdometerKm;
        fldOdoErr.style.display   = isInvalid ? '' : 'none';
        fldOdo.style.borderColor  = isInvalid ? '#ef4444' : '';
    });

    // fldFinalDest queda oculto (reemplazado por grupos inline) — sin listener necesario

    // Validación del sello de seguridad en tiempo real (encabezado — header + lines)
    let sealTimer = null;
    document.getElementById('fldSecuritySeal').addEventListener('input', () => {
        clearTimeout(sealTimer);
        const seal = document.getElementById('fldSecuritySeal').value.trim();
        document.getElementById('sealError').classList.add('d-none');
        if (!seal) return;
        sealTimer = setTimeout(async () => {
            try {
                const excludeId = INV.editingId || undefined;
                const url = `/api/inventorytransaction/check-any-seal?seal=${encodeURIComponent(seal)}`
                    + (excludeId ? `&excludeId=${excludeId}` : '');
                const { exists } = await invFetch(url);
                document.getElementById('sealError').classList.toggle('d-none', !exists);
            } catch {}
        }, 500);
    });

    // Filtros
    let filterTimer;
    const triggerFilter = () => {
        clearTimeout(filterTimer);
        filterTimer = setTimeout(() => {
            INV.filters.search   = document.getElementById('fSearch').value.trim();
            INV.filters.type     = document.getElementById('fType').value;
            INV.filters.status   = document.getElementById('fStatus').value;
            INV.filters.dateFrom = document.getElementById('fDateFrom').value;
            INV.filters.dateTo   = document.getElementById('fDateTo').value;
            loadMovements(1);
        }, 400);
    };

    ['fSearch', 'fType', 'fStatus', 'fDateFrom', 'fDateTo'].forEach(id =>
        document.getElementById(id).addEventListener('change', triggerFilter));
    document.getElementById('fSearch').addEventListener('input', triggerFilter);

    document.getElementById('btnClearFilters').addEventListener('click', () => {
        ['fSearch', 'fDateFrom', 'fDateTo'].forEach(id => document.getElementById(id).value = '');
        ['fType', 'fStatus'].forEach(id => document.getElementById(id).value = '');
        INV.filters = { search: '', type: '', status: '', dateFrom: '', dateTo: '' };
        loadMovements(1);
    });

    // KPI chips como filtros (data-filter-status es el code en minúsculas, buscamos el ID)
    document.getElementById('kpiRow').querySelectorAll('.kpi-chip').forEach(chip => {
        chip.addEventListener('click', () => {
            const code = chip.dataset.filterStatus || '';
            const statusId = code
                ? (INV.transactionStatuses.find(s => s.code.toLowerCase() === code)?.id?.toString() || '')
                : '';
            document.getElementById('fStatus').value = statusId;
            INV.filters.status = statusId;
            document.querySelectorAll('.kpi-chip').forEach(c => c.classList.remove('active'));
            chip.classList.add('active');
            loadMovements(1);
        });
    });

    // Confirmar cancelación
    document.getElementById('btnConfirmCancel').addEventListener('click', executeCancelTxn);
}

// ============================================================
// HELPERS
// ============================================================

function warehouseName(id) {
    if (!id) return '—';
    const w = INV.warehouses.find(w => w.id === id || w.id === +id);
    return w ? `${w.code} — ${w.name}` : `#${id}`;
}

function statusBadge(idStatus) {
    const m = txnStatusInfo(idStatus);
    return `<span class="badge-status ${m.css}">${m.icon} ${m.label}</span>`;
}

function showAlert(msg, type = 'info') {
    const el = document.getElementById('invAlert');
    el.className = `inv-alert alert alert-${type}`;
    el.textContent = msg;
    el.classList.remove('d-none');
    setTimeout(() => el.classList.add('d-none'), 5000);
}

function showInfoDialog(msg, type = 'warning') {
    const existing = document.getElementById('_infoDialogOverlay');
    if (existing) existing.remove();

    const iconMap = {
        info:    { icon: 'bi-info-circle',          color: '#38bdf8', border: '#0ea5e9', title: 'Información' },
        warning: { icon: 'bi-exclamation-triangle', color: '#fbbf24', border: '#f59e0b', title: 'Atención'    },
        danger:  { icon: 'bi-x-circle',             color: '#f87171', border: '#ef4444', title: 'Error'       },
        success: { icon: 'bi-check-circle',         color: '#4ade80', border: '#22c55e', title: 'Éxito'       },
    };
    const m = iconMap[type] || iconMap.warning;

    const overlay = document.createElement('div');
    overlay.id = '_infoDialogOverlay';
    overlay.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,.6);z-index:9999;display:flex;align-items:center;justify-content:center;';

    const box = document.createElement('div');
    box.style.cssText = `background:#1e2d4a;border:1px solid ${m.border};border-radius:12px;padding:1.75rem 2rem;max-width:420px;width:90%;box-shadow:0 8px 32px rgba(0,0,0,.6);text-align:center;`;

    const iconEl = document.createElement('i');
    iconEl.className = `bi ${m.icon}`;
    iconEl.style.cssText = `font-size:2.2rem;color:${m.color};display:block;margin-bottom:.85rem;`;

    const titleEl = document.createElement('h6');
    titleEl.style.cssText = 'color:#ffffff;font-weight:700;margin-bottom:.6rem;font-size:1rem;';
    titleEl.textContent = m.title;

    const msgEl = document.createElement('p');
    msgEl.style.cssText = 'color:#cbd5e1;font-size:.9rem;margin-bottom:1.4rem;line-height:1.5;';
    msgEl.textContent = msg;

    const btn = document.createElement('button');
    btn.className = 'btn btn-primary btn-sm px-4';
    btn.innerHTML = '<i class="bi bi-check me-1"></i>Aceptar';
    btn.addEventListener('click', () => overlay.remove());

    box.appendChild(iconEl);
    box.appendChild(titleEl);
    box.appendChild(msgEl);
    box.appendChild(btn);
    overlay.appendChild(box);
    document.body.appendChild(overlay);
    setTimeout(() => btn.focus(), 50);
    overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
}

function showModalAlert(msg, type = 'warning') {
    showInfoDialog(msg, type);
}
