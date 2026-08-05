// ================================================================================
// ARCHIVO: CMS.API/Controllers/MenuController.cs
// PROPÓSITO: Controlador REST para gestión de menús
// DESCRIPCIÓN: REQUIERE JWT, devuelve menús filtrados por permisos del token
// AUTOR: EAMR, BITI SOLUTIONS S.A
// ACTUALIZADO: 2026-02-11
// ================================================================================

using CMS.Data;
using CMS.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    /// <summary>
    /// Controlador REST para la gestión de menús de navegación.
    /// REQUIERE autenticación JWT y devuelve menús filtrados por permisos.
    /// </summary>
    [Authorize]  // ⭐ REQUIERE JWT
    [ApiController]
    [Route("api/[controller]")]
    public class MenuController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<MenuController> _logger;

        public MenuController(AppDbContext db, ILogger<MenuController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene los menús disponibles para el usuario autenticado.
        /// Filtra según los permisos incluidos en el JWT.
        /// </summary>
        /// <returns>
        /// JSON:
        /// {
        ///   "success": true,
        ///   "count": 10,
        ///   "data": [ array de MenuDto en camelCase ]
        /// }
        /// </returns>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                // =====================================================================
                // 1. EXTRAER INFO DEL TOKEN JWT
                // =====================================================================
                var userIdClaim = User.FindFirst("userId")?.Value;
                var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    _logger.LogWarning("❌ Token sin userId claim");
                    return Unauthorized(new { success = false, message = "Token inválido" });
                }

                // Obtener permisos del token (ya vienen en el JWT como claims)
                var permissions = User.FindAll("permission")
                                     .Select(c => c.Value)
                                     .ToHashSet(StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation("👤 Usuario: {UserName} (ID: {UserId}), Permisos: {Count}",
                    userName, userIdClaim, permissions.Count);

                // =====================================================================
                // 2. CARGAR TODOS LOS MENÚS ACTIVOS
                // =====================================================================
                var allMenus = await _db.Menus
                    .Where(m => m.IS_ACTIVE)
                    .OrderBy(m => m.ID_PARENT)
                    .ThenBy(m => m.ORDER)
                    .ToListAsync();

                _logger.LogInformation("📋 Menús activos: {Count}", allMenus.Count);

                // =====================================================================
                // 3. FILTRAR MENÚS SEGÚN PERMISOS DEL TOKEN
                // =====================================================================

                // Paso 3a: ítems que pasan el filtro de permisos directamente
                var directlyAllowed = allMenus
                    .Where(m => string.IsNullOrEmpty(m.PERMISSION_KEY) ||
                                permissions.Contains(m.PERMISSION_KEY))
                    .ToHashSet();

                // Paso 3b: "bubble-up" — si un ítem es accesible, todos sus ancestros
                // deben incluirse aunque tengan un PERMISSION_KEY que no sea un permiso
                // real (p.ej. menús contenedor como "General Accounting")
                var allowedIds = directlyAllowed.Select(m => m.ID_MENU).ToHashSet();

                var allMenusById = allMenus.ToDictionary(m => m.ID_MENU);

                foreach (var menu in directlyAllowed.ToList())
                {
                    var parentId = menu.ID_PARENT;
                    while (parentId != 0 && allMenusById.TryGetValue(parentId, out var parent))
                    {
                        if (allowedIds.Add(parent.ID_MENU))
                            directlyAllowed.Add(parent);
                        parentId = parent.ID_PARENT;
                    }
                }

                var filteredMenus = allMenus
                    .Where(m => allowedIds.Contains(m.ID_MENU))
                    .ToList();

                _logger.LogInformation("🔒 Menús filtrados: {Filtered}/{Total}",
                    filteredMenus.Count, allMenus.Count);

                // =====================================================================
                // 4. CONVERTIR A DTOs (camelCase)
                // =====================================================================
                var menuDtos = filteredMenus.Select(m => new MenuDto
                {
                    IdMenu = m.ID_MENU,
                    IdParent = m.ID_PARENT,
                    Name = m.NAME,
                    Url = m.URL,
                    Icon = m.ICON,
                    Order = m.ORDER,
                    PermissionKey = m.PERMISSION_KEY,
                    IsActive = m.IS_ACTIVE
                }).ToList();

                // =====================================================================
                // 5. RESPUESTA
                // =====================================================================
                return Ok(new
                {
                    success = true,
                    count = menuDtos.Count,
                    data = menuDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo menús");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor"
                });
            }
        }

        // =====================================================================
        // ADMIN CRUD ENDPOINTS
        // =====================================================================

        /// <summary>
        /// Obtiene TODOS los menús (para administración)
        /// GET: api/menu/admin
        /// </summary>
        [Authorize]  // Solo requiere JWT válido, permisos se validan en UI
        [HttpGet("admin")]
        public async Task<IActionResult> GetAllAdmin()
        {
            try
            {
                var menus = await _db.Menus
                    .OrderBy(m => m.ID_PARENT)
                    .ThenBy(m => m.ORDER)
                    .Select(m => new MenuDto
                    {
                        IdMenu = m.ID_MENU,
                        IdParent = m.ID_PARENT,
                        Name = m.NAME,
                        Url = m.URL,
                        Icon = m.ICON,
                        Order = m.ORDER,
                        PermissionKey = m.PERMISSION_KEY,
                        IsActive = m.IS_ACTIVE
                    })
                    .ToListAsync();

                return Ok(new { success = true, count = menus.Count, data = menus });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo menús para admin");
                return StatusCode(500, new { success = false, message = "Error interno" });
            }
        }

        /// <summary>
        /// Obtiene un menú por ID
        /// GET: api/menu/admin/{id}
        /// </summary>
        [Authorize]  // Solo requiere JWT válido
        [HttpGet("admin/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var menu = await _db.Menus
                    .Where(m => m.ID_MENU == id)
                    .Select(m => new MenuDto
                    {
                        IdMenu = m.ID_MENU,
                        IdParent = m.ID_PARENT,
                        Name = m.NAME,
                        Url = m.URL,
                        Icon = m.ICON,
                        Order = m.ORDER,
                        PermissionKey = m.PERMISSION_KEY,
                        IsActive = m.IS_ACTIVE
                    })
                    .FirstOrDefaultAsync();

                if (menu == null)
                    return NotFound(new { message = "Menú no encontrado" });

                return Ok(menu);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo menú {Id}", id);
                return StatusCode(500, new { message = "Error interno" });
            }
        }

        /// <summary>
        /// Crea un nuevo menú
        /// POST: api/menu/admin
        /// </summary>
        [Authorize]  // Solo requiere JWT válido, permisos se validan en UI
        [HttpPost("admin")]
        public async Task<IActionResult> Create([FromBody] MenuCreateRequest dto)
        {
            try
            {
                var menu = new CMS.Entities.Menu
                {
                    ID_PARENT = dto.IdParent,
                    NAME = dto.Name,
                    URL = dto.Url,
                    ICON = dto.Icon,
                    ORDER = dto.Order,
                    PERMISSION_KEY = dto.PermissionKey,
                    IS_ACTIVE = dto.IsActive,
                    RecordDate = DateTime.UtcNow,
                    CreateDate = DateTime.UtcNow,
                    RowPointer = Guid.NewGuid(),
                    CreatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM",
                    UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM"
                };

                _db.Menus.Add(menu);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Menú creado: {Name} (ID: {Id})", menu.NAME, menu.ID_MENU);

                return CreatedAtAction(nameof(GetById), new { id = menu.ID_MENU }, new MenuDto
                {
                    IdMenu = menu.ID_MENU,
                    IdParent = menu.ID_PARENT,
                    Name = menu.NAME,
                    Url = menu.URL,
                    Icon = menu.ICON,
                    Order = menu.ORDER,
                    PermissionKey = menu.PERMISSION_KEY,
                    IsActive = menu.IS_ACTIVE
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando menú");
                return StatusCode(500, new { message = "Error creando menú" });
            }
        }

        /// <summary>
        /// Actualiza un menú existente
        /// PUT: api/menu/admin/{id}
        /// </summary>
        [Authorize]  // Solo requiere JWT válido
        [HttpPut("admin/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] MenuUpdateRequest dto)
        {
            try
            {
                var menu = await _db.Menus.FindAsync(id);
                if (menu == null)
                    return NotFound(new { message = "Menú no encontrado" });

                menu.ID_PARENT = dto.IdParent;
                menu.NAME = dto.Name;
                menu.URL = dto.Url;
                menu.ICON = dto.Icon;
                menu.ORDER = dto.Order;
                menu.PERMISSION_KEY = dto.PermissionKey;
                menu.IS_ACTIVE = dto.IsActive;
                menu.UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM";

                await _db.SaveChangesAsync();

                _logger.LogInformation("Menú actualizado: {Name} (ID: {Id})", menu.NAME, menu.ID_MENU);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando menú {Id}", id);
                return StatusCode(500, new { message = "Error actualizando menú" });
            }
        }

        /// <summary>
        /// Elimina un menú
        /// DELETE: api/menu/admin/{id}
        /// </summary>
        [Authorize]  // Solo requiere JWT válido
        [HttpDelete("admin/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var menu = await _db.Menus.FindAsync(id);
                if (menu == null)
                    return NotFound(new { message = "Menú no encontrado" });

                // Verificar si tiene hijos
                var hasChildren = await _db.Menus.AnyAsync(m => m.ID_PARENT == id);
                if (hasChildren)
                    return BadRequest(new { message = "No se puede eliminar un menú con submenús. Elimine los submenús primero." });

                _db.Menus.Remove(menu);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Menú eliminado: {Name} (ID: {Id})", menu.NAME, menu.ID_MENU);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando menú {Id}", id);
                return StatusCode(500, new { message = "Error eliminando menú" });
            }
        }

        #region DTOs para Admin

        public class MenuCreateRequest
        {
            public int IdParent { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Url { get; set; }
            public string? Icon { get; set; }
            public int Order { get; set; }
            public string? PermissionKey { get; set; }
            public bool IsActive { get; set; } = true;
        }

        public class MenuUpdateRequest
        {
            public int IdParent { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Url { get; set; }
            public string? Icon { get; set; }
            public int Order { get; set; }
            public string? PermissionKey { get; set; }
            public bool IsActive { get; set; }
        }

        #endregion
    }
}