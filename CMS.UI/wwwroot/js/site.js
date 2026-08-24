//document.addEventListener("click", function (e) {
//    const toggle = e.target.closest(".nav-group-toggle");
//    if (!toggle) return;

//    const group = toggle.closest(".nav-group");
//    group.classList.toggle("open");
//});
// ============================================================
// MENÚ OVERLAY (drawer tipo modal)
// El menú está oculto por defecto y se despliega ENCIMA del
// contenido. Se oculta al seleccionar una opción, al pulsar el
// botón de cerrar, al hacer clic en el backdrop o con Escape.
// ============================================================
function toggleSidebar() {
    document.querySelector('.layout-wrapper').classList.toggle('sidebar-open');
}

function openSidebar() {
    document.querySelector('.layout-wrapper').classList.add('sidebar-open');
}

function closeSidebar() {
    document.querySelector('.layout-wrapper').classList.remove('sidebar-open');
}

// Cerrar con la tecla Escape (igual que un modal)
document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        closeSidebar();
    }
});

// ============================================================
// COMPORTAMIENTO DE ACORDEÓN PARA EL MENÚ
// Solo permite un submenú abierto a la vez
// ============================================================
document.addEventListener("DOMContentLoaded", function () {

    console.log("✅ JavaScript cargado correctamente");

    // ============================================================
    // CERRAR EL MENÚ OVERLAY AL SELECCIONAR UNA OPCIÓN NAVEGABLE
    // Los enlaces reales tienen href a una URL y NO son toggles de
    // grupo. Al hacer clic en uno, el menú se oculta completamente.
    // ============================================================
    document.querySelectorAll('.sidebar-nav a.nav-link:not(.nav-group-toggle)').forEach(link => {
        link.addEventListener('click', function () {
            const href = this.getAttribute('href') || '';
            if (href && href !== 'javascript:void(0)' && !href.startsWith('#')) {
                closeSidebar();
            }
        });
    });

    // ============================================================
    // TOGGLE GRUPOS NIVEL 1 (menús principales con hijos)
    // ============================================================
    const toggles = document.querySelectorAll(".nav-group-toggle:not(.nav-subgroup-toggle)");

    console.log(`📋 Encontrados ${toggles.length} grupos de menú`);

    toggles.forEach((toggle, index) => {
        toggle.addEventListener("click", function (e) {
            e.preventDefault();
            e.stopPropagation();

            console.log(`🖱️ Clic en grupo ${index + 1}`);

            const parentGroup = this.closest(".nav-group:not(.nav-subgroup)");
            const wasOpen = parentGroup.classList.contains("open");

            console.log(`Estado anterior: ${wasOpen ? 'ABIERTO' : 'CERRADO'}`);

            // Cerrar todos los grupos de nivel 1 excepto el actual
            document.querySelectorAll(".nav-group:not(.nav-subgroup)").forEach(group => {
                if (group !== parentGroup) {
                    group.classList.remove("open");
                    console.log("🔒 Cerrando otro grupo");
                }
            });

            if (!wasOpen) {
                parentGroup.classList.add("open");
                console.log("🔓 Abriendo este grupo");

                setTimeout(() => {
                    const sidebarNav = document.querySelector('.sidebar-nav');
                    if (sidebarNav) {
                        const groupRect = parentGroup.getBoundingClientRect();
                        const sidebarRect = sidebarNav.getBoundingClientRect();
                        const subMenu = parentGroup.querySelector('.nav-group-items');

                        const totalHeight = groupRect.height + (subMenu ? subMenu.scrollHeight : 0);
                        const availableSpace = sidebarRect.bottom - groupRect.top;

                        if (totalHeight > availableSpace) {
                            const scrollTarget = parentGroup.offsetTop - 80;
                            sidebarNav.scrollTo({ top: scrollTarget, behavior: 'smooth' });
                            console.log(`📜 Auto-scroll: menú padre a posición ${scrollTarget}px`);
                        }
                    }
                }, 100);
            } else {
                parentGroup.classList.remove("open");
                console.log("🔒 Cerrando este grupo");
            }
        });
    });

    // ============================================================
    // TOGGLE SUBGRUPOS NIVEL 2 (ej: General Accounting dentro de Administration)
    // ============================================================
    const subToggles = document.querySelectorAll(".nav-subgroup-toggle");

    subToggles.forEach((toggle, index) => {
        toggle.addEventListener("click", function (e) {
            e.preventDefault();
            e.stopPropagation(); // evitar que el click suba al grupo padre

            const subGroup = this.closest(".nav-subgroup");
            const wasOpen  = subGroup.classList.contains("open");

            // Cerrar otros subgrupos del mismo padre
            const parentItems = subGroup.closest(".nav-group-items");
            if (parentItems) {
                parentItems.querySelectorAll(".nav-subgroup").forEach(sg => {
                    if (sg !== subGroup) sg.classList.remove("open");
                });
            }

            if (!wasOpen) {
                subGroup.classList.add("open");
                console.log(`🔓 Abriendo subgrupo ${index + 1}`);
            } else {
                subGroup.classList.remove("open");
                console.log(`🔒 Cerrando subgrupo ${index + 1}`);
            }
        });
    });

    // ⭐ AUTO-SCROLL EN CARGA INICIAL: Si hay un menú activo expandido, hacer scroll ⭐
    setTimeout(() => {
        const activeItem = document.querySelector('.nav-group.open .nav-link.active');
        if (activeItem) {
            const sidebarNav = document.querySelector('.sidebar-nav');
            if (sidebarNav) {
                activeItem.scrollIntoView({ behavior: 'smooth', block: 'center' });
                console.log("📜 Auto-scroll inicial al menú activo");
            }
        }
    }, 100);
});