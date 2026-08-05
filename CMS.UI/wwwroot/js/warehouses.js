// ================================================================================
// ARCHIVO: CMS.UI/wwwroot/js/warehouses.js
// PROPÓSITO: Lógica cliente para la pantalla /Warehouse/Warehouses
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-03
// ================================================================================

'use strict';

// ===== ESTADO GLOBAL =====
const WH = {
    apiBase: '',
    token: '',
    currentPage: 1,
    pageSize: 15,
    totalPages: 0,
    filters: { search: '', warehouseType: '', warehouseLevel: '', isActive: 'true' },
    editingId: null,
    viewMode: 'grid',   // 'grid' | 'list' | 'tree'
    treeData: [],
};

// ===== TIPOS Y ETIQUETAS =====
// Los tipos se cargan dinámicamente desde /api/warehousetype al iniciar
const WAREHOUSE_TYPES = {};

const WAREHOUSE_LEVELS = {
    0: { label: 'Bodega',   icon: 'bi-building-fill',   badge: 'bg-primary' },
    1: { label: 'Zona',     icon: 'bi-grid-3x3-gap',    badge: 'bg-info' },
    2: { label: 'Pasillo',  icon: 'bi-distribute-vertical', badge: 'bg-secondary' },
    3: { label: 'Rack',     icon: 'bi-server',           badge: 'bg-warning text-dark' },
    4: { label: 'Bin',      icon: 'bi-geo-alt',          badge: 'bg-dark' },
};

// ===== INIT =====
document.addEventListener('DOMContentLoaded', () => {
    loadWarehouseTypes().then(() => loadWarehouses());
    initSearch();

    // Toggle transport unit select when warehouse type changes
    document.getElementById('fWarehouseType')?.addEventListener('change', function () {
        toggleTransportUnit(this.value);
    });
});

// Habilita/deshabilita el selector de unidad de transporte según el tipo de bodega
function toggleTransportUnit(typeValue) {
    const sel  = document.getElementById('fTransportUnit');
    const wrap = document.getElementById('fTransportUnitWrap');
    if (!sel) return;
    const isTransit = typeValue === 'Transit';
    sel.disabled = !isTransit;
    if (!isTransit) {
        sel.value = '';
        sel.classList.remove('is-invalid');
    } else {
        // Recargar siempre para reflejar disponibilidad actualizada
        loadTransportUnitOptions();
    }
}

// Carga unidades de transporte activas para el selector
// Solo muestra unidades NO asignadas a otra bodega (availableOnly=true).
// currentWarehouseId: si se está editando, la unidad de esa bodega sí aparece en la lista.
async function loadTransportUnitOptions(selectedId) {
    try {
        const currentWarehouseId = WH.editingId ?? null;
        let url = '/api/transportunit?isActive=true&pageSize=200&availableOnly=true';
        if (currentWarehouseId) url += `&currentWarehouseId=${currentWarehouseId}`;
        const res = await apiFetch(url);
        if (!res.ok) return;
        const data = await res.json();
        const items = data.items || [];
        const select = document.getElementById('fTransportUnit');
        if (!select) return;
        const current = selectedId ?? select.value;
        select.innerHTML = '<option value="">— Seleccione una unidad —</option>';
        for (const u of items) {
            const label = `${u.plateNumber} — ${u.name}`;
            select.appendChild(new Option(label, u.id));
        }
        if (current) select.value = current;
    } catch { /* silencioso */ }
}

// Carga tipos de bodega desde la BD central y rellena los selects de filtro y formulario
async function loadWarehouseTypes() {
    try {
        const res = await apiFetch('/api/warehousetype?isActive=true');
        if (!res.ok) return;
        const types = await res.json();
        for (const t of types) {
            WAREHOUSE_TYPES[t.code] = {
                label: t.name,
                icon:  t.icon  || 'bi-building',
                color: t.color || '#64748b',
            };
        }
        // Rellenar select de filtro
        const filterSel = document.getElementById('filterType');
        if (filterSel) {
            filterSel.innerHTML = '<option value="">Todos</option>';
            for (const t of types) {
                filterSel.appendChild(new Option(t.name, t.code));
            }
        }
        // Rellenar select del formulario
        const formSel = document.getElementById('fWarehouseType');
        if (formSel) {
            const current = formSel.value;
            formSel.innerHTML = '';
            for (const t of types) {
                formSel.appendChild(new Option(t.name, t.code));
            }
            if (current) formSel.value = current;
            if (!formSel.value && types.length > 0) formSel.value = types[0].code;
        }
    } catch { /* silencioso: los selects quedan con sus opciones estáticas */ }
}

function initSearch() {
    const searchInput = document.getElementById('searchInput');
    let debounceTimer;
    searchInput?.addEventListener('input', () => {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            WH.filters.search = searchInput.value.trim();
            WH.currentPage = 1;
            loadWarehouses();
        }, 400);
    });
}

// ===== API HELPERS =====
async function apiFetch(path, options = {}) {
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${WH.token}`,
        ...(options.headers || {}),
    };
    const res = await fetch(`${WH.apiBase}${path}`, { ...options, headers });
    return res;
}

// ===== CARGA PRINCIPAL =====
async function loadWarehouses() {
    setLoading(true);
    try {
        const params = new URLSearchParams({
            page: WH.currentPage,
            pageSize: WH.pageSize,
        });
        if (WH.filters.search)        params.append('search', WH.filters.search);
        if (WH.filters.warehouseType) params.append('warehouseType', WH.filters.warehouseType);
        if (WH.filters.warehouseLevel !== '') params.append('warehouseLevel', WH.filters.warehouseLevel);
        if (WH.filters.isActive !== '') params.append('isActive', WH.filters.isActive);

        const res = await apiFetch(`/api/warehouse?${params}`);
        if (!res.ok) throw new Error(await res.text());

        const data = await res.json();
        WH.totalPages = data.totalPages;
        renderWarehouses(data.items, data.totalCount);
        renderPagination(data.totalCount);
    } catch (err) {
        showAlert('danger', `Error al cargar bodegas: ${err.message}`);
    } finally {
        setLoading(false);
    }
}

async function loadTree() {
    setLoading(true);
    try {
        const res = await apiFetch('/api/warehouse/tree?activeOnly=false');
        if (!res.ok) throw new Error(await res.text());
        WH.treeData = await res.json();
        renderTree(WH.treeData);
    } catch (err) {
        showAlert('danger', `Error al cargar árbol: ${err.message}`);
    } finally {
        setLoading(false);
    }
}

// ===== RENDER GRID =====
function renderWarehouses(items, total) {
    const container = document.getElementById('warehouseContainer');
    const emptyState = document.getElementById('emptyState');
    document.getElementById('totalCount').textContent = total;

    if (!items || items.length === 0) {
        container.innerHTML = '';
        emptyState.classList.remove('d-none');
        return;
    }
    emptyState.classList.add('d-none');

    if (WH.viewMode === 'list') {
        renderList(items, container);
    } else {
        renderGrid(items, container);
    }
}

function renderGrid(items, container) {
    container.innerHTML = `<div class="row g-3">${items.map(buildCard).join('')}</div>`;
}

function buildCard(w) {
    const type = WAREHOUSE_TYPES[w.warehouseType] || { label: w.warehouseType, icon: 'bi-building', color: '#64748b' };
    const level = WAREHOUSE_LEVELS[w.warehouseLevel] || WAREHOUSE_LEVELS[0];
    const activeBadge = w.isActive
        ? '<span class="badge bg-success bg-opacity-20 text-success border border-success border-opacity-25">Activa</span>'
        : '<span class="badge bg-danger bg-opacity-20 text-danger border border-danger border-opacity-25">Inactiva</span>';
    const defaultBadge = w.isDefault
        ? '<span class="badge bg-warning bg-opacity-20 text-warning border border-warning border-opacity-25 ms-1"><i class="bi bi-star-fill me-1"></i>Default</span>'
        : '';
    const managedBadge = w.isManaged
        ? '<span class="badge bg-info bg-opacity-20 text-info border border-info border-opacity-25 ms-1"><i class="bi bi-cpu me-1"></i>WMS</span>'
        : '';

    const flags = [];
    if (w.requiresLotTracking) flags.push('<span title="Control de lotes"><i class="bi bi-upc-scan text-info"></i></span>');
    if (w.requiresExpiryDate)  flags.push('<span title="Fecha vencimiento"><i class="bi bi-calendar-x text-warning"></i></span>');
    if (w.requiresLocation)    flags.push('<span title="Ubicación requerida"><i class="bi bi-geo-alt text-success"></i></span>');
    if (w.allowsNegativeStock) flags.push('<span title="Permite stock negativo"><i class="bi bi-dash-circle text-danger"></i></span>');

    return `
    <div class="col-md-6 col-xl-4">
      <div class="card bg-dark-card border-0 h-100 warehouse-card" onclick="openDetail(${w.id})" style="cursor:pointer; border-left: 3px solid ${type.color} !important; transition: transform .15s, box-shadow .15s;">
        <div class="card-body p-3">
          <div class="d-flex align-items-start justify-content-between mb-2">
            <div class="d-flex align-items-center gap-2">
              <div class="wh-icon-circle" style="background: ${type.color}22; color: ${type.color}; width:40px; height:40px; border-radius:10px; display:flex; align-items:center; justify-content:center; font-size:1.2rem;">
                <i class="bi ${type.icon}"></i>
              </div>
              <div>
                <div class="fw-semibold text-white" style="font-size:.95rem;">${escHtml(w.name)}</div>
                <code class="text-muted" style="font-size:.75rem;">${escHtml(w.code)}</code>
              </div>
            </div>
            <div class="dropdown" onclick="event.stopPropagation()">
              <button class="btn btn-sm btn-outline-secondary border-0" data-bs-toggle="dropdown" style="padding:2px 6px;">
                <i class="bi bi-three-dots-vertical"></i>
              </button>
              <ul class="dropdown-menu dropdown-menu-dark dropdown-menu-end">
                <li><a class="dropdown-item" href="#" onclick="openEdit(${w.id}); return false;"><i class="bi bi-pencil me-2"></i>Editar</a></li>
                <li><a class="dropdown-item" href="#" onclick="openChildren(${w.id}, '${escHtml(w.name)}'); return false;"><i class="bi bi-diagram-2 me-2"></i>Ver sub-ubicaciones</a></li>
                <li><hr class="dropdown-divider"></li>
                ${w.isActive
                    ? `<li><a class="dropdown-item text-warning" href="#" onclick="toggleStatus(${w.id}, false); return false;"><i class="bi bi-pause-circle me-2"></i>Desactivar</a></li>`
                    : `<li><a class="dropdown-item text-success" href="#" onclick="toggleStatus(${w.id}, true); return false;"><i class="bi bi-play-circle me-2"></i>Activar</a></li>`}
              </ul>
            </div>
          </div>
          ${w.description ? `<p class="text-light small mb-2" style="line-height:1.3; opacity:.75;">${escHtml(w.description)}</p>` : ''}
          <div class="d-flex flex-wrap gap-1 mb-2">
            ${activeBadge}${defaultBadge}${managedBadge}
            <span class="badge ${level.badge} bg-opacity-20 border border-opacity-25"><i class="bi ${level.icon} me-1"></i>${level.label}</span>
            <span class="badge" style="background:${type.color}22; color:${type.color}; border:1px solid ${type.color}44;"><i class="bi ${type.icon} me-1"></i>${type.label}</span>
          </div>
          <div class="d-flex align-items-center justify-content-between">
            <div class="d-flex gap-2" style="font-size:1rem;">
              ${flags.join('')}
            </div>
            ${w.city ? `<small class="text-light" style="opacity:.85;"><i class="bi bi-geo-alt me-1"></i>${escHtml(w.city)}</small>` : ''}
          </div>
          ${w.responsibleName ? `<div class="mt-2 pt-2 border-top border-secondary border-opacity-25"><small class="text-light" style="opacity:.85;"><i class="bi bi-person me-1"></i>${escHtml(w.responsibleName)}</small></div>` : ''}
        </div>
      </div>
    </div>`;
}

// ===== RENDER LIST =====
function renderList(items, container) {
    const rows = items.map(w => {
        const type = WAREHOUSE_TYPES[w.warehouseType] || { label: w.warehouseType, icon: 'bi-building', color: '#64748b' };
        const level = WAREHOUSE_LEVELS[w.warehouseLevel] || WAREHOUSE_LEVELS[0];
        return `
        <tr style="cursor:pointer;" onclick="openDetail(${w.id})">
          <td><code class="text-info">${escHtml(w.code)}</code></td>
          <td class="text-white fw-semibold">${escHtml(w.name)}</td>
          <td><span class="badge" style="background:${type.color}22; color:${type.color};"><i class="bi ${type.icon} me-1"></i>${type.label}</span></td>
          <td><span class="badge ${level.badge} bg-opacity-20">${level.label}</span></td>
          <td class="text-light small">${w.city || '-'}</td>
          <td class="text-light small">${w.responsibleName || '-'}</td>
          <td>${w.isActive ? '<span class="badge bg-success bg-opacity-20 text-success">Activa</span>' : '<span class="badge bg-danger bg-opacity-20 text-danger">Inactiva</span>'}</td>
          <td onclick="event.stopPropagation()">
            <div class="d-flex gap-1">
              <button class="btn btn-sm btn-outline-info border-0" onclick="openEdit(${w.id})" title="Editar"><i class="bi bi-pencil"></i></button>
              ${w.isActive
                ? `<button class="btn btn-sm btn-outline-warning border-0" onclick="toggleStatus(${w.id},false)" title="Desactivar"><i class="bi bi-pause-circle"></i></button>`
                : `<button class="btn btn-sm btn-outline-success border-0" onclick="toggleStatus(${w.id},true)" title="Activar"><i class="bi bi-play-circle"></i></button>`}
            </div>
          </td>
        </tr>`;
    }).join('');

    container.innerHTML = `
    <div class="table-responsive">
      <table class="table table-dark table-hover table-sm align-middle mb-0" style="font-size:.875rem;">
        <thead>
          <tr style="color:#94a3b8; border-bottom:1px solid rgba(255,255,255,.1);">
            <th>Código</th><th>Nombre</th><th>Tipo</th><th>Nivel</th><th>Ciudad</th><th>Responsable</th><th>Estado</th><th></th>
          </tr>
        </thead>
        <tbody>${rows}</tbody>
      </table>
    </div>`;
}

// ===== RENDER TREE =====
function renderTree(nodes) {
    const container = document.getElementById('warehouseContainer');
    document.getElementById('emptyState').classList.add('d-none');
    document.getElementById('totalCount').textContent = countTreeNodes(nodes);

    container.innerHTML = `<div class="wh-tree p-2">${nodes.map(n => buildTreeNode(n, 0)).join('')}</div>`;
}

function buildTreeNode(node, depth) {
    const type = WAREHOUSE_TYPES[node.warehouseType] || { label: node.warehouseType, icon: 'bi-building', color: '#64748b' };
    const children = node.children?.length
        ? `<div class="wh-tree-children" style="margin-left:${depth < 2 ? 24 : 16}px; border-left:1px dashed rgba(255,255,255,.1); padding-left:8px;">${node.children.map(c => buildTreeNode(c, depth + 1)).join('')}</div>`
        : '';

    return `
    <div class="wh-tree-node mb-1">
      <div class="d-flex align-items-center gap-2 p-2 rounded" style="background:rgba(255,255,255,.04); cursor:pointer;" onclick="openDetail(${node.id})">
        ${node.children?.length ? `<i class="bi bi-chevron-down text-muted" style="font-size:.7rem;"></i>` : `<i class="bi bi-dot text-muted"></i>`}
        <div class="wh-icon-circle" style="background:${type.color}22; color:${type.color}; width:28px; height:28px; border-radius:6px; display:flex; align-items:center; justify-content:center; font-size:.85rem; flex-shrink:0;">
          <i class="bi ${type.icon}"></i>
        </div>
        <div class="flex-grow-1">
          <span class="text-white fw-semibold" style="font-size:.875rem;">${escHtml(node.name)}</span>
          <code class="text-muted ms-2" style="font-size:.72rem;">${escHtml(node.code)}</code>
        </div>
        ${node.isDefault ? '<i class="bi bi-star-fill text-warning ms-1" style="font-size:.7rem;" title="Default"></i>' : ''}
        ${!node.isActive ? '<span class="badge bg-danger bg-opacity-20 text-danger ms-1" style="font-size:.65rem;">Inactiva</span>' : ''}
        <button class="btn btn-sm btn-outline-secondary border-0 ms-auto" style="padding:1px 5px; font-size:.75rem;" onclick="event.stopPropagation(); openEdit(${node.id})">
          <i class="bi bi-pencil"></i>
        </button>
      </div>
      ${children}
    </div>`;
}

function countTreeNodes(nodes) {
    let c = nodes.length;
    for (const n of nodes) if (n.children?.length) c += countTreeNodes(n.children);
    return c;
}

// ===== PAGINACIÓN =====
function renderPagination(total) {
    const pag = document.getElementById('pagination');
    if (WH.totalPages <= 1) { pag.innerHTML = ''; return; }

    let html = '';
    const prev = WH.currentPage === 1;
    const next = WH.currentPage === WH.totalPages;
    html += `<li class="page-item ${prev ? 'disabled' : ''}"><a class="page-link" href="#" onclick="goPage(${WH.currentPage - 1}); return false;"><i class="bi bi-chevron-left"></i></a></li>`;

    for (let i = 1; i <= WH.totalPages; i++) {
        if (WH.totalPages > 7 && (i > 2 && i < WH.currentPage - 1)) { if (i === 3) html += `<li class="page-item disabled"><span class="page-link">…</span></li>`; continue; }
        if (WH.totalPages > 7 && (i < WH.totalPages - 1 && i > WH.currentPage + 1)) { if (i === WH.currentPage + 2) html += `<li class="page-item disabled"><span class="page-link">…</span></li>`; continue; }
        html += `<li class="page-item ${i === WH.currentPage ? 'active' : ''}"><a class="page-link" href="#" onclick="goPage(${i}); return false;">${i}</a></li>`;
    }

    html += `<li class="page-item ${next ? 'disabled' : ''}"><a class="page-link" href="#" onclick="goPage(${WH.currentPage + 1}); return false;"><i class="bi bi-chevron-right"></i></a></li>`;
    pag.innerHTML = html;
}

function goPage(p) {
    if (p < 1 || p > WH.totalPages) return;
    WH.currentPage = p;
    loadWarehouses();
}

// ===== FILTROS =====
function applyFilters() {
    WH.filters.warehouseType = document.getElementById('filterType').value;
    WH.filters.warehouseLevel = document.getElementById('filterLevel').value;
    WH.filters.isActive = document.getElementById('filterStatus').value;
    WH.currentPage = 1;
    if (WH.viewMode === 'tree') { loadTree(); } else { loadWarehouses(); }
}

function clearFilters() {
    document.getElementById('searchInput').value = '';
    document.getElementById('filterType').value = '';
    document.getElementById('filterLevel').value = '';
    document.getElementById('filterStatus').value = 'true';
    WH.filters = { search: '', warehouseType: '', warehouseLevel: '', isActive: 'true' };
    WH.currentPage = 1;
    if (WH.viewMode === 'tree') { loadTree(); } else { loadWarehouses(); }
}

// ===== SWITCH VIEW MODE =====
function setViewMode(mode) {
    WH.viewMode = mode;
    document.querySelectorAll('.btn-view-mode').forEach(b => b.classList.remove('active'));
    document.querySelector(`[data-view="${mode}"]`)?.classList.add('active');

    const pagSection = document.getElementById('paginationSection');
    if (mode === 'tree') {
        pagSection.classList.add('d-none');
        loadTree();
    } else {
        pagSection.classList.remove('d-none');
        loadWarehouses();
    }
}

// ===== DETAIL / MODAL =====
async function openDetail(id) {
    try {
        const res = await apiFetch(`/api/warehouse/${id}`);
        if (!res.ok) throw new Error(await res.text());
        const w = await res.json();
        populateDetailModal(w);
        const modal = new bootstrap.Modal(document.getElementById('detailModal'));
        modal.show();
    } catch (err) {
        showAlert('danger', `Error: ${err.message}`);
    }
}

function populateDetailModal(w) {
    const type = WAREHOUSE_TYPES[w.warehouseType] || { label: w.warehouseType, icon: 'bi-building', color: '#64748b' };
    const level = WAREHOUSE_LEVELS[w.warehouseLevel] || WAREHOUSE_LEVELS[0];

    document.getElementById('detailCode').textContent = w.code;
    document.getElementById('detailName').textContent = w.name;
    document.getElementById('detailDescription').textContent = w.description || '—';
    document.getElementById('detailType').innerHTML = `<i class="bi ${type.icon} me-1" style="color:${type.color}"></i>${type.label}`;
    document.getElementById('detailLevel').textContent = level.label;
    document.getElementById('detailStatus').innerHTML = w.isActive
        ? '<span class="badge bg-success">Activa</span>'
        : '<span class="badge bg-danger">Inactiva</span>';
    document.getElementById('detailDefault').innerHTML = w.isDefault ? '<span class="badge bg-warning text-dark">Sí</span>' : 'No';
    document.getElementById('detailManaged').innerHTML = w.isManaged ? '<span class="badge bg-info">WMS activo</span>' : 'No';
    document.getElementById('detailNegStock').textContent = w.allowsNegativeStock ? 'Sí' : 'No';
    document.getElementById('detailLotTracking').textContent = w.requiresLotTracking ? 'Sí' : 'No';
    document.getElementById('detailExpiry').textContent = w.requiresExpiryDate ? 'Sí' : 'No';
    document.getElementById('detailRequiresLoc').textContent = w.requiresLocation ? 'Sí' : 'No';
    document.getElementById('detailCapacity').textContent = w.maxCapacity ? `${w.maxCapacity} ${w.capacityUnit || ''}` : '—';
    document.getElementById('detailAddress').textContent = w.locationAddress || '—';
    document.getElementById('detailCity').textContent = w.locationCity || '—';
    document.getElementById('detailCountry').textContent = w.locationCountryCode || '—';
    document.getElementById('detailResponsible').innerHTML = w.responsibleUserId
        ? `<span class="text-light">${escHtml(w.responsibleName || '(sin nombre)')}</span>
           <small class="text-muted ms-2">#${w.responsibleUserId}</small>`
        : '—';
    document.getElementById('detailPhone').textContent = w.responsiblePhone || '—';
    document.getElementById('detailEmail').textContent = w.responsibleEmail || '—';
    document.getElementById('detailNotes').textContent = w.notes || '—';
    document.getElementById('detailCreatedBy').textContent = `${w.createdBy || '—'} — ${w.createdAt ? new Date(w.createdAt).toLocaleString('es-CR') : ''}`;
    document.getElementById('detailUpdatedBy').textContent = w.updatedBy ? `${w.updatedBy} — ${new Date(w.updatedAt).toLocaleString('es-CR')}` : '—';
    document.getElementById('detailChildren').textContent = w.stats ? `${w.stats.totalChildren} directos / ${w.stats.totalDescendants} total` : '—';

    // GPS link
    const gpsEl = document.getElementById('detailGps');
    if (w.locationGpsLatitude && w.locationGpsLongitude) {
        gpsEl.innerHTML = `<a href="https://maps.google.com/?q=${w.locationGpsLatitude},${w.locationGpsLongitude}" target="_blank" class="text-info"><i class="bi bi-map me-1"></i>${w.locationGpsLatitude}, ${w.locationGpsLongitude}</a>`;
    } else {
        gpsEl.textContent = '—';
    }

    // botón editar en modal
    document.getElementById('detailEditBtn').onclick = () => {
        bootstrap.Modal.getInstance(document.getElementById('detailModal'))?.hide();
        openEdit(w.id);
    };
}

// ===== OPEN CHILDREN FILTER =====
function openChildren(parentId, parentName) {
    document.getElementById('searchInput').value = '';
    WH.filters = { search: '', warehouseType: '', warehouseLevel: '', isActive: '' };
    document.getElementById('filterStatus').value = '';
    // Note: filter by parent not in quick filters, use a breadcrumb display
    showAlert('info', `Mostrando sub-ubicaciones de "${parentName}" (funcionalidad de drill-down próximamente)`, 3000);
}

// ===== FORM CREATE/EDIT =====
function openCreate() {
    WH.editingId = null;
    resetForm();
    document.getElementById('formModalTitle').textContent = 'Nueva Bodega';
    document.getElementById('formModalSubtitle').textContent = 'Complete los datos para registrar una nueva bodega o ubicación.';
    const modal = new bootstrap.Modal(document.getElementById('formModal'));
    modal.show();
}

async function openEdit(id) {
    try {
        const res = await apiFetch(`/api/warehouse/${id}`);
        if (!res.ok) throw new Error(await res.text());
        const w = await res.json();
        WH.editingId = id;
        // Cargar opciones primero para que los selects tengan las opciones antes de pre-seleccionar
        await Promise.all([
            loadUserOptions(w.responsibleUserId ?? null),
            loadLocationOptions(w.idLocation ?? null, id),
            loadParentOptions()
        ]);
        populateForm(w);
        document.getElementById('formModalTitle').textContent = 'Editar Bodega';
        document.getElementById('formModalSubtitle').textContent = `Modificando: ${w.code} — ${w.name}`;
        const modal = new bootstrap.Modal(document.getElementById('formModal'));
        modal.show();
    } catch (err) {
        showAlert('danger', `Error: ${err.message}`);
    }
}

function populateForm(w) {
    document.getElementById('fCode').value = w.code;
    document.getElementById('fName').value = w.name;
    document.getElementById('fDescription').value = w.description || '';
    document.getElementById('fWarehouseType').value = w.warehouseType;
    document.getElementById('fWarehouseLevel').value = w.warehouseLevel;
    document.getElementById('fParentWarehouse').value = w.idParentWarehouse || '';
    document.getElementById('fIsDefault').checked = w.isDefault;
    document.getElementById('fIsManaged').checked = w.isManaged;
    document.getElementById('fAllowsNegativeStock').checked = w.allowsNegativeStock;
    document.getElementById('fRequiresLocation').checked = w.requiresLocation;
    document.getElementById('fRequiresLotTracking').checked = w.requiresLotTracking;
    document.getElementById('fRequiresExpiryDate').checked = w.requiresExpiryDate;
    document.getElementById('fMaxCapacity').value = w.maxCapacity || '';
    document.getElementById('fCapacityUnit').value = w.capacityUnit || '';
    // Localización: pre-seleccionar en el select
    const locSelect = document.getElementById('fIdLocation');
    if (locSelect) {
        locSelect.value = w.idLocation || '';
        if (w.idLocation && !locSelect.querySelector(`option[value="${w.idLocation}"]`)) {
            const cityPart = w.locationCity ? ` — ${w.locationCity}` : '';
            const opt = new Option(`LOC #${w.idLocation}${cityPart}`, w.idLocation);
            locSelect.add(opt);
            locSelect.value = w.idLocation;
        }
    }
    // Responsable: seleccionar en el dropdown de usuarios
    const respSelect = document.getElementById('fResponsibleUserId');
    if (respSelect) {
        respSelect.value = w.responsibleUserId || '';
        // Si ya tiene usuario cargado, mostrar la opción resuelta
        if (w.responsibleUserId && w.responsibleName) {
            let opt = respSelect.querySelector(`option[value="${w.responsibleUserId}"]`);
            if (!opt) {
                opt = new Option(`${w.responsibleName} (ID: ${w.responsibleUserId})`, w.responsibleUserId);
                respSelect.add(opt);
            }
            respSelect.value = w.responsibleUserId;
        }
    }
    document.getElementById('fNotes').value = w.notes || '';
    document.getElementById('fIsActive').checked = w.isActive;
    // Transport unit (Transit warehouses only)
    toggleTransportUnit(w.warehouseType);
    if (w.warehouseType === 'Transit' && w.idTransportUnit) {
        loadTransportUnitOptions(w.idTransportUnit).then(() => {
            document.getElementById('fTransportUnit').value = w.idTransportUnit;
        });
    }
    clearFormErrors();
}

function resetForm() {
    document.getElementById('warehouseForm').reset();
    document.getElementById('fIsActive').checked = true;
    document.getElementById('fWarehouseType').value = 'Physical';
    document.getElementById('fWarehouseLevel').value = '0';
    toggleTransportUnit('Physical');
    clearFormErrors();
    loadParentOptions();
    loadUserOptions();
    loadLocationOptions();
}

// Carga usuarios de la compañía activa para el selector de responsable
async function loadUserOptions(selectedId) {
    try {
        const res = await apiFetch('/api/user/for-company');
        if (!res.ok) return;
        const users = await res.json();
        const select = document.getElementById('fResponsibleUserId');
        if (!select) return;
        const current = selectedId ?? select.value;
        select.innerHTML = '<option value="">— Sin responsable asignado —</option>';
        for (const u of users) {
            const label = `${u.displayName || u.username || ''}${u.email ? ' — ' + u.email : ''}`;
            const opt = new Option(label, u.id);
            select.appendChild(opt);
        }
        if (current) select.value = current;
    } catch { /* silencioso: el campo queda opcional */ }
}

// Carga localizaciones tipo WAREHOUSE para el selector de ubicación
// selectedId: id de la ubicación actualmente asignada (para preseleccionar)
// currentWarehouseId: id de la bodega en edición (para incluir su ubicación actual en la lista)
async function loadLocationOptions(selectedId, currentWarehouseId = null) {
    try {
        // Buscar el locationTypeId de WAREHOUSE primero
        const typesRes = await apiFetch('/api/locationtype?isActive=true');
        if (!typesRes.ok) return;
        const types = await typesRes.json();
        const warehouseType = types.find(t => t.code?.toUpperCase() === 'WAREHOUSE');
        if (!warehouseType) return;

        // availableOnly=true: solo libres + la actualmente asignada (currentLocationId)
        let url = `/api/location/by-type/${warehouseType.id}?isActive=true&availableOnly=true`;
        if (selectedId) url += `&currentLocationId=${selectedId}`;

        const res = await apiFetch(url);
        if (!res.ok) return;
        const locations = await res.json();
        const select = document.getElementById('fIdLocation');
        if (!select) return;
        const current = selectedId ?? select.value;
        select.innerHTML = '<option value="">— Sin localización asignada —</option>';
        for (const l of locations) {
            const label = l.address || l.address2 || `Localización #${l.id}`;
            select.appendChild(new Option(label, l.id));
        }
        if (current) select.value = current;
    } catch { /* silencioso */ }
}

function clearFormErrors() {
    document.querySelectorAll('.is-invalid').forEach(el => el.classList.remove('is-invalid'));
    document.querySelectorAll('.invalid-feedback').forEach(el => el.textContent = '');
    const ma = document.getElementById('modalAlert');
    if (ma) ma.classList.add('d-none');
}

async function loadParentOptions() {
    try {
        const res = await apiFetch('/api/warehouse/tree?activeOnly=true');
        if (!res.ok) return;
        const tree = await res.json();
        const select = document.getElementById('fParentWarehouse');
        const current = select.value;
        select.innerHTML = '<option value="">— Sin padre (nivel raíz) —</option>';
        populateParentSelect(select, tree, 0);
        if (current) select.value = current;
    } catch { /* silencioso */ }
}

function populateParentSelect(select, nodes, depth) {
    for (const n of nodes) {
        const opt = document.createElement('option');
        opt.value = n.id;
        opt.textContent = `${'  '.repeat(depth)}${depth > 0 ? '└ ' : ''}${n.code} — ${n.name}`;
        select.appendChild(opt);
        if (n.children?.length) populateParentSelect(select, n.children, depth + 1);
    }
}

// ===== VALIDACIÓN CÓDIGO EN TIEMPO REAL =====
let codeCheckTimer;
document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('fCode')?.addEventListener('input', function() {
        clearTimeout(codeCheckTimer);
        const val = this.value.trim();
        if (!val) return;
        codeCheckTimer = setTimeout(async () => {
            const excludeParam = WH.editingId ? `&excludeId=${WH.editingId}` : '';
            const res = await apiFetch(`/api/warehouse/check-code?code=${encodeURIComponent(val.toUpperCase())}${excludeParam}`);
            if (res.ok) {
                const data = await res.json();
                const feedback = document.getElementById('fCodeFeedback');
                if (data.exists) {
                    this.classList.add('is-invalid');
                    feedback.textContent = 'Este código ya existe en la compañía.';
                } else {
                    this.classList.remove('is-invalid');
                    this.classList.add('is-valid');
                    feedback.textContent = '';
                }
            }
        }, 500);
    });
});

// ===== GUARDAR FORM =====
async function saveWarehouse() {
    clearFormErrors();
    const btn = document.getElementById('saveBtnModal');
    const isEdit = !!WH.editingId;

    const body = {
        code: document.getElementById('fCode').value.trim().toUpperCase(),
        name: document.getElementById('fName').value.trim(),
        description: document.getElementById('fDescription').value.trim() || null,
        warehouseType: document.getElementById('fWarehouseType').value,
        warehouseLevel: parseInt(document.getElementById('fWarehouseLevel').value),
        idParentWarehouse: document.getElementById('fParentWarehouse').value ? parseInt(document.getElementById('fParentWarehouse').value) : null,
        isDefault: document.getElementById('fIsDefault').checked,
        isManaged: document.getElementById('fIsManaged').checked,
        allowsNegativeStock: document.getElementById('fAllowsNegativeStock').checked,
        requiresLocation: document.getElementById('fRequiresLocation').checked,
        requiresLotTracking: document.getElementById('fRequiresLotTracking').checked,
        requiresExpiryDate: document.getElementById('fRequiresExpiryDate').checked,
        maxCapacity: document.getElementById('fMaxCapacity').value ? parseFloat(document.getElementById('fMaxCapacity').value) : null,
        capacityUnit: document.getElementById('fCapacityUnit').value.trim() || null,
        idLocation: document.getElementById('fIdLocation').value ? parseInt(document.getElementById('fIdLocation').value) : null,
        responsibleUserId: document.getElementById('fResponsibleUserId').value ? parseInt(document.getElementById('fResponsibleUserId').value) : null,
        notes: document.getElementById('fNotes').value.trim() || null,
        isActive: document.getElementById('fIsActive').checked,
        idTransportUnit: document.getElementById('fTransportUnit').value ? parseInt(document.getElementById('fTransportUnit').value) : null,
    };

    // Validación básica
    let hasError = false;
    if (!body.code) { setFieldError('fCode', 'El código es requerido.'); hasError = true; }
    if (!body.name) { setFieldError('fName', 'El nombre es requerido.'); hasError = true; }
    if (!body.idLocation) { setFieldError('fIdLocation', 'La localización es obligatoria.'); hasError = true; }
    if (body.warehouseType === 'Transit' && !body.idTransportUnit) { setFieldError('fTransportUnit', 'La unidad de transporte es obligatoria para bodegas en tránsito.'); hasError = true; }
    if (hasError) return;

    setBtnLoading(btn, true);
    try {
        const url = isEdit ? `/api/warehouse/${WH.editingId}` : '/api/warehouse';
        const method = isEdit ? 'PUT' : 'POST';

        const res = await apiFetch(url, {
            method,
            body: JSON.stringify(body),
        });

        if (res.status === 409) {
            const err = await res.json();
            setFieldError('fCode', err.error || 'Código duplicado.');
            return;
        }

        if (!res.ok) {
            const err = await res.json().catch(() => ({ error: 'Error desconocido al guardar la bodega.' }));
            const base = err.error || err.message || `Error ${res.status} al guardar.`;
            const detail = err.detail ? `<br><small class="text-white-50">${err.detail}</small>` : '';
            showModalAlert('danger', base + detail);
            return;
        }

        bootstrap.Modal.getInstance(document.getElementById('formModal'))?.hide();
        showAlert('success', isEdit ? 'Bodega actualizada exitosamente.' : 'Bodega creada exitosamente.');

        if (WH.viewMode === 'tree') { loadTree(); } else { loadWarehouses(); }
    } catch (err) {
        showAlert('danger', `Error: ${err.message}`);
    } finally {
        setBtnLoading(btn, false);
    }
}

// ===== TOGGLE STATUS =====
async function toggleStatus(id, activate) {
    const action = activate ? 'activate' : 'deactivate';
    const label = activate ? 'activar' : 'desactivar';
    if (!confirm(`¿Desea ${label} esta bodega?`)) return;

    try {
        const res = await apiFetch(`/api/warehouse/${id}/${action}`, { method: 'PATCH' });
        if (!res.ok) throw new Error((await res.json()).error);
        showAlert('success', `Bodega ${activate ? 'activada' : 'desactivada'} exitosamente.`);
        if (WH.viewMode === 'tree') { loadTree(); } else { loadWarehouses(); }
    } catch (err) {
        showAlert('danger', `Error: ${err.message}`);
    }
}

// ===== HELPERS =====
function setLoading(on) {
    const spinner = document.getElementById('loadingSpinner');
    const container = document.getElementById('warehouseContainer');
    if (on) { spinner?.classList.remove('d-none'); container && (container.style.opacity = '.4'); }
    else { spinner?.classList.add('d-none'); container && (container.style.opacity = '1'); }
}

function setFieldError(id, msg) {
    const el = document.getElementById(id);
    el?.classList.add('is-invalid');
    const fb = document.getElementById(id + 'Feedback');
    if (fb) fb.textContent = msg;
}

function setBtnLoading(btn, on) {
    if (!btn) return;
    if (on) { btn.dataset.original = btn.innerHTML; btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Guardando…'; btn.disabled = true; }
    else { btn.innerHTML = btn.dataset.original || 'Guardar'; btn.disabled = false; }
}

let alertTimer;
function showAlert(type, msg, duration = 5000) {
    clearTimeout(alertTimer);
    const icons = { success: 'bi-check-circle-fill', danger: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill', warning: 'bi-exclamation-circle-fill' };
    const cssClass = { success: 'alert-success-tech', danger: 'alert-danger-tech', info: 'alert-info text-white', warning: 'alert-warning text-dark' };
    const el = document.getElementById('pageAlert');
    if (!el) return;
    el.className = `alert ${cssClass[type] || 'alert-info'} d-flex align-items-center`;
    el.innerHTML = `<i class="bi ${icons[type] || 'bi-info-circle-fill'} me-2"></i><div>${msg}</div><button type="button" class="btn-close btn-close-white ms-auto" onclick="this.closest('.alert').classList.add('d-none')"></button>`;
    el.classList.remove('d-none');
    alertTimer = setTimeout(() => el.classList.add('d-none'), duration);
}

function showModalAlert(type, msg) {
    const icons = { success: 'bi-check-circle-fill', danger: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill', warning: 'bi-exclamation-circle-fill' };
    const bgClass = { success: 'alert-success', danger: 'alert-danger', info: 'alert-info', warning: 'alert-warning' };
    const el = document.getElementById('modalAlert');
    if (!el) { showAlert(type, msg); return; }
    el.className = `alert ${bgClass[type] || 'alert-danger'} d-flex align-items-center py-2`;
    el.innerHTML = `<i class="bi ${icons[type] || 'bi-exclamation-triangle-fill'} me-2"></i><div>${msg}</div>`;
    el.classList.remove('d-none');
}

function escHtml(str) {
    if (!str) return '';
    return str.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
