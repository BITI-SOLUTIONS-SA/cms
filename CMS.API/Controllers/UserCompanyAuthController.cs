// ================================================================================
// ARCHIVO: CMS.API/Controllers/UserCompanyAuthController.cs
// PROPÓSITO: API REST para gestionar roles y permisos de usuarios POR COMPAÑÍA
// DESCRIPCIÓN: Endpoints para asignar/remover roles y permisos a usuarios
//              en compañías específicas (arquitectura multi-tenant)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-16
// ================================================================================

using CMS.Data;
using CMS.Data.Services;
using CMS.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    /// <summary>
    /// Controlador para gestionar roles y permisos de usuarios POR COMPAÑÍA.
    /// Implementa la arquitectura de seguridad multi-tenant.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/users/{userId}/companies/{companyId}")]
    public class UserCompanyAuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly AuthorizationService _authService;
        private readonly ILogger<UserCompanyAuthController> _logger;

        public UserCompanyAuthController(
            AppDbContext db,
            AuthorizationService authService,
            ILogger<UserCompanyAuthController> logger)
        {
            _db = db;
            _authService = authService;
            _logger = logger;
        }

        #region DTOs

        public class UserCompanyRoleDto
        {
            public int RoleId { get; set; }
            public string RoleName { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public bool IsAssigned { get; set; }
        }

        public class UserCompanyPermissionDto
        {
            public int PermissionId { get; set; }
            public string PermissionKey { get; set; } = string.Empty;
            public string PermissionName { get; set; } = string.Empty;
            public string Module { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty; // "role", "direct", "denied"
            public bool IsAllowed { get; set; }
            public bool IsDenied { get; set; }
        }

        public class AssignRoleRequest
        {
            public int RoleId { get; set; }
        }

        public class SetPermissionRequest
        {
            public int PermissionId { get; set; }
            public bool IsAllowed { get; set; }
        }

        public class UserCompanyAuthSummary
        {
            public int UserId { get; set; }
            public string UserName { get; set; } = string.Empty;
            public int CompanyId { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public List<UserCompanyRoleDto> Roles { get; set; } = new();
            public List<UserCompanyPermissionDto> Permissions { get; set; } = new();
            public int TotalEffectivePermissions { get; set; }
        }

        #endregion

        // =====================================================================
        // OBTENER RESUMEN DE AUTORIZACIÓN
        // =====================================================================

        /// <summary>
        /// Obtiene el resumen completo de autorización de un usuario en una compañía
        /// </summary>
        [HttpGet("auth")]
        public async Task<ActionResult<UserCompanyAuthSummary>> GetAuthSummary(int userId, int companyId)
        {
            try
            {
                // Verificar que existe la relación usuario-compañía
                var userCompany = await _db.UserCompanies
                    .Include(uc => uc.User)
                    .Include(uc => uc.Company)
                    .FirstOrDefaultAsync(uc => uc.ID_USER == userId && uc.ID_COMPANY == companyId);

                if (userCompany == null)
                {
                    return NotFound(new { message = "Usuario no tiene acceso a esta compañía" });
                }

                // Obtener todos los roles disponibles y marcar los asignados
                var allRoles = await _db.Roles
                    .Where(r => r.IS_ACTIVE)
                    .OrderBy(r => r.ROLE_NAME)
                    .ToListAsync();

                var assignedRoleIds = await _db.UserCompanyRoles
                    .Where(ucr => ucr.ID_USER == userId && ucr.ID_COMPANY == companyId && ucr.IS_ACTIVE)
                    .Select(ucr => ucr.ID_ROLE)
                    .ToListAsync();

                var roles = allRoles.Select(r => new UserCompanyRoleDto
                {
                    RoleId = r.ID_ROLE,
                    RoleName = r.ROLE_NAME,
                    IsActive = r.IS_ACTIVE,
                    IsAssigned = assignedRoleIds.Contains(r.ID_ROLE)
                }).ToList();

                // Obtener permisos efectivos
                var effectivePerms = await _authService.GetEffectivePermissionsAsync(userId, companyId);

                // Obtener permisos directos (para mostrar cuáles son override)
                var directPermissions = await _db.UserCompanyPermissions
                    .Where(ucp => ucp.ID_USER == userId && ucp.ID_COMPANY == companyId)
                    .ToListAsync();

                var directPermIds = directPermissions.ToDictionary(dp => dp.ID_PERMISSION, dp => dp.IS_ALLOWED);

                // Construir lista de permisos con su origen
                var allPermissions = await _db.Permissions
                    .Where(p => p.IS_ACTIVE)
                    .OrderBy(p => p.MODULE)
                    .ThenBy(p => p.PERMISSION_KEY)
                    .ToListAsync();

                var permissionDtos = allPermissions.Select(p =>
                {
                    var isDirect = directPermIds.ContainsKey(p.ID_PERMISSION);
                    var isDirectAllowed = isDirect && directPermIds[p.ID_PERMISSION];
                    var isDirectDenied = isDirect && !directPermIds[p.ID_PERMISSION];
                    var isEffective = effectivePerms.EffectivePermissions.Contains(p.PERMISSION_KEY);
                    var isFromRole = effectivePerms.AllowedPermissions.Contains(p.PERMISSION_KEY) && !isDirect;

                    string source;
                    if (isDirectDenied)
                        source = "denied";
                    else if (isDirectAllowed)
                        source = "direct";
                    else if (isFromRole)
                        source = "role";
                    else
                        source = "none";

                    return new UserCompanyPermissionDto
                    {
                        PermissionId = p.ID_PERMISSION,
                        PermissionKey = p.PERMISSION_KEY,
                        PermissionName = p.PERMISSION_NAME,
                        Module = p.MODULE,
                        Source = source,
                        IsAllowed = isEffective,
                        IsDenied = isDirectDenied
                    };
                }).ToList();

                return Ok(new UserCompanyAuthSummary
                {
                    UserId = userId,
                    UserName = userCompany.User?.DISPLAY_NAME ?? "N/A",
                    CompanyId = companyId,
                    CompanyName = userCompany.Company?.COMPANY_NAME ?? "N/A",
                    Roles = roles,
                    Permissions = permissionDtos,
                    TotalEffectivePermissions = effectivePerms.EffectivePermissions.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo resumen de autorización");
                return StatusCode(500, new { message = "Error interno" });
            }
        }

        // =====================================================================
        // GESTIÓN DE ROLES
        // =====================================================================

        /// <summary>
        /// Obtiene los roles asignados a un usuario en una compañía
        /// </summary>
        [HttpGet("roles")]
        public async Task<ActionResult<List<UserCompanyRoleDto>>> GetRoles(int userId, int companyId)
        {
            try
            {
                var roles = await (
                    from ucr in _db.UserCompanyRoles
                    join r in _db.Roles on ucr.ID_ROLE equals r.ID_ROLE
                    where ucr.ID_USER == userId && ucr.ID_COMPANY == companyId
                    select new UserCompanyRoleDto
                    {
                        RoleId = r.ID_ROLE,
                        RoleName = r.ROLE_NAME,
                        IsActive = ucr.IS_ACTIVE && r.IS_ACTIVE,
                        IsAssigned = true
                    }
                ).ToListAsync();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo roles");
                return StatusCode(500, new { message = "Error obteniendo roles" });
            }
        }

        /// <summary>
        /// Asigna un rol a un usuario en una compañía
        /// </summary>
        [HttpPost("roles")]
        public async Task<ActionResult> AssignRole(int userId, int companyId, [FromBody] AssignRoleRequest request)
        {
            try
            {
                var currentUser = User.FindFirstValue("cms_username") ?? "SYSTEM";

                var success = await _authService.AssignRoleToUserInCompanyAsync(
                    userId, companyId, request.RoleId, currentUser);

                if (!success)
                {
                    return BadRequest(new { message = "No se pudo asignar el rol (ya existe o datos inválidos)" });
                }

                _logger.LogInformation("✅ Rol {RoleId} asignado a usuario {UserId} en compañía {CompanyId}",
                    request.RoleId, userId, companyId);

                return Ok(new { message = "Rol asignado exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error asignando rol");
                return StatusCode(500, new { message = "Error interno" });
            }
        }

        /// <summary>
        /// Remueve un rol de un usuario en una compañía
        /// </summary>
        [HttpDelete("roles/{roleId}")]
        public async Task<ActionResult> RemoveRole(int userId, int companyId, int roleId)
        {
            try
            {
                var success = await _authService.RemoveRoleFromUserInCompanyAsync(userId, companyId, roleId);

                if (!success)
                {
                    return NotFound(new { message = "Rol no encontrado" });
                }

                _logger.LogInformation("✅ Rol {RoleId} removido de usuario {UserId} en compañía {CompanyId}",
                    roleId, userId, companyId);

                return Ok(new { message = "Rol removido exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removiendo rol");
                return StatusCode(500, new { message = "Error interno" });
            }
        }

        // =====================================================================
        // GESTIÓN DE PERMISOS DIRECTOS
        // =====================================================================

        /// <summary>
        /// Obtiene los permisos directos de un usuario en una compañía
        /// </summary>
        [HttpGet("permissions")]
        public async Task<ActionResult<List<UserCompanyPermissionDto>>> GetDirectPermissions(int userId, int companyId)
        {
            try
            {
                var permissions = await (
                    from ucp in _db.UserCompanyPermissions
                    join p in _db.Permissions on ucp.ID_PERMISSION equals p.ID_PERMISSION
                    where ucp.ID_USER == userId && ucp.ID_COMPANY == companyId
                    select new UserCompanyPermissionDto
                    {
                        PermissionId = p.ID_PERMISSION,
                        PermissionKey = p.PERMISSION_KEY,
                        PermissionName = p.PERMISSION_NAME,
                        Module = p.MODULE,
                        Source = ucp.IS_ALLOWED ? "direct" : "denied",
                        IsAllowed = ucp.IS_ALLOWED,
                        IsDenied = !ucp.IS_ALLOWED
                    }
                ).ToListAsync();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo permisos directos");
                return StatusCode(500, new { message = "Error obteniendo permisos" });
            }
        }

        /// <summary>
        /// Establece un permiso directo (otorgar o denegar) a un usuario en una compañía
        /// </summary>
        [HttpPost("permissions")]
        public async Task<ActionResult> SetPermission(int userId, int companyId, [FromBody] SetPermissionRequest request)
        {
            try
            {
                var currentUser = User.FindFirstValue("cms_username") ?? "SYSTEM";

                bool success;
                if (request.IsAllowed)
                {
                    success = await _authService.GrantPermissionAsync(
                        userId, companyId, request.PermissionId, currentUser);
                }
                else
                {
                    success = await _authService.DenyPermissionAsync(
                        userId, companyId, request.PermissionId, currentUser);
                }

                if (!success)
                {
                    return BadRequest(new { message = "No se pudo configurar el permiso" });
                }

                var action = request.IsAllowed ? "otorgado" : "denegado";
                _logger.LogInformation("✅ Permiso {PermissionId} {Action} a usuario {UserId} en compañía {CompanyId}",
                    request.PermissionId, action, userId, companyId);

                return Ok(new { message = $"Permiso {action} exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configurando permiso");
                return StatusCode(500, new { message = "Error interno" });
            }
        }

        /// <summary>
        /// Remueve un permiso directo (el usuario vuelve a depender de sus roles)
        /// </summary>
        [HttpDelete("permissions/{permissionId}")]
        public async Task<ActionResult> RemoveDirectPermission(int userId, int companyId, int permissionId)
        {
            try
            {
                var success = await _authService.RemoveDirectPermissionAsync(userId, companyId, permissionId);

                if (!success)
                {
                    return NotFound(new { message = "Permiso directo no encontrado" });
                }

                _logger.LogInformation("✅ Permiso directo {PermissionId} removido de usuario {UserId} en compañía {CompanyId}",
                    permissionId, userId, companyId);

                return Ok(new { message = "Permiso directo removido" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removiendo permiso directo");
                return StatusCode(500, new { message = "Error interno" });
            }
        }

        // =====================================================================
        // PERMISOS EFECTIVOS (SOLO LECTURA)
        // =====================================================================

        /// <summary>
        /// Obtiene los permisos efectivos calculados de un usuario en una compañía
        /// </summary>
        [HttpGet("effective-permissions")]
        public async Task<ActionResult<HashSet<string>>> GetEffectivePermissions(int userId, int companyId)
        {
            try
            {
                var result = await _authService.GetEffectivePermissionsAsync(userId, companyId);
                return Ok(new
                {
                    userId,
                    companyId,
                    roles = result.Roles,
                    effectivePermissions = result.EffectivePermissions,
                    deniedPermissions = result.DeniedPermissions,
                    totalPermissions = result.EffectivePermissions.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculando permisos efectivos");
                return StatusCode(500, new { message = "Error interno" });
            }
        }
    }
}
