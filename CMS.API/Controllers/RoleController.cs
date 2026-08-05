// ================================================================================
// ARCHIVO: CMS.API/Controllers/RoleController.cs
// PROPÓSITO: Endpoints para gestión de roles
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-14
// ================================================================================

using CMS.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RoleController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<RoleController> _logger;

        public RoleController(AppDbContext db, ILogger<RoleController> logger)
        {
            _db = db;
            _logger = logger;
        }

        private string GetAuditUser() =>
            User.FindFirstValue("cms_username")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "SYSTEM";

        /// <summary>
        /// Obtener todos los roles activos
        /// GET: api/role
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var roles = await _db.Roles
                    .Where(r => r.IS_ACTIVE)
                    .OrderBy(r => r.ROLE_NAME)
                    .Select(r => new RoleDto
                    {
                        Id = r.ID_ROLE,
                        RoleName = r.ROLE_NAME,
                        Description = r.DESCRIPTION,
                        IsSystem = r.IS_SYSTEM,
                        IsActive = r.IS_ACTIVE,
                        UserCount = _db.UserCompanyRoles.Count(ucr => ucr.ID_ROLE == r.ID_ROLE && ucr.IS_ACTIVE),
                        PermissionCount = _db.RolePermissions.Count(rp => rp.RoleId == r.ID_ROLE && rp.IsAllowed)
                    })
                    .ToListAsync();

                _logger.LogInformation("📋 Roles obtenidos: {Count}", roles.Count);

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo roles");
                return StatusCode(500, new { message = "Error obteniendo roles" });
            }
        }

        /// <summary>
        /// Obtener un rol por ID
        /// GET: api/role/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var role = await _db.Roles
                    .Where(r => r.ID_ROLE == id)
                    .Select(r => new RoleDto
                    {
                        Id = r.ID_ROLE,
                        RoleName = r.ROLE_NAME,
                        Description = r.DESCRIPTION,
                        IsSystem = r.IS_SYSTEM,
                        IsActive = r.IS_ACTIVE,
                        UserCount = _db.UserCompanyRoles.Count(ucr => ucr.ID_ROLE == r.ID_ROLE && ucr.IS_ACTIVE),
                        PermissionCount = _db.RolePermissions.Count(rp => rp.RoleId == r.ID_ROLE && rp.IsAllowed)
                    })
                    .FirstOrDefaultAsync();

                if (role == null)
                    return NotFound(new { message = "Rol no encontrado" });

                return Ok(role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo rol {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo rol" });
            }
        }

        /// <summary>
        /// Obtener detalles completos de un rol con permisos y usuarios
        /// GET: api/role/{id}/details
        /// </summary>
        [HttpGet("{id}/details")]
        public async Task<IActionResult> GetDetails(int id)
        {
            try
            {
                var role = await _db.Roles
                    .Where(r => r.ID_ROLE == id)
                    .FirstOrDefaultAsync();

                if (role == null)
                    return NotFound(new { message = "Rol no encontrado" });

                // Obtener permisos del rol
                var permissions = await (from rp in _db.RolePermissions
                                         join p in _db.Permissions on rp.PermissionId equals p.ID_PERMISSION
                                         where rp.RoleId == id && p.IS_ACTIVE
                                         select new PermissionDto
                                         {
                                             Id = p.ID_PERMISSION,
                                             PermissionKey = p.PERMISSION_KEY,
                                             PermissionName = p.PERMISSION_NAME,
                                             IsAllowed = rp.IsAllowed
                                         }).ToListAsync();

                // Obtener usuarios con este rol (de todas las compañías)
                var users = await (from ucr in _db.UserCompanyRoles
                                   join u in _db.Users on ucr.ID_USER equals u.ID_USER
                                   where ucr.ID_ROLE == id && ucr.IS_ACTIVE && u.IS_ACTIVE
                                   select new UserSimpleDto
                                   {
                                       Id = u.ID_USER,
                                       Username = u.USER_NAME,
                                       Email = u.EMAIL
                                   }).Distinct().ToListAsync();

                var result = new RoleDetailDto
                {
                    Id = role.ID_ROLE,
                    RoleName = role.ROLE_NAME,
                    Description = role.DESCRIPTION,
                    IsSystem = role.IS_SYSTEM,
                    IsActive = role.IS_ACTIVE,
                    Permissions = permissions,
                    Users = users
                };

                _logger.LogInformation("📋 Detalles del rol {RoleName}: {PermCount} permisos, {UserCount} usuarios",
                    role.ROLE_NAME, permissions.Count, users.Count);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo detalles del rol {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo detalles del rol" });
            }
        }

        /// <summary>
        /// Obtener permisos de un rol
        /// GET: api/role/{id}/permissions
        /// </summary>
        [HttpGet("{id}/permissions")]
        public async Task<IActionResult> GetRolePermissions(int id)
        {
            try
            {
                var permissions = await (from rp in _db.RolePermissions
                                         join p in _db.Permissions on rp.PermissionId equals p.ID_PERMISSION
                                         where rp.RoleId == id
                                         select new PermissionDto
                                         {
                                             Id = p.ID_PERMISSION,
                                             PermissionKey = p.PERMISSION_KEY,
                                             PermissionName = p.PERMISSION_NAME,
                                             IsAllowed = rp.IsAllowed
                                         }).ToListAsync();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo permisos del rol {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo permisos" });
            }
        }

        /// <summary>
        /// Crear un nuevo rol
        /// POST: api/role
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RoleCreateDto model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.RoleName))
                    return BadRequest(new { message = "El nombre del rol es requerido" });

                // Verificar si ya existe un rol con ese nombre
                var exists = await _db.Roles.AnyAsync(r => r.ROLE_NAME.ToLower() == model.RoleName.ToLower());
                if (exists)
                    return BadRequest(new { message = "Ya existe un rol con ese nombre" });

                var role = new CMS.Entities.Role
                {
                    ROLE_NAME = model.RoleName.Trim(),
                    DESCRIPTION = model.Description?.Trim() ?? string.Empty,
                    IS_SYSTEM = false,
                    IS_ACTIVE = true,
                    CreateDate = DateTime.UtcNow,
                    CreatedBy = GetAuditUser(),
                    UpdatedBy = GetAuditUser(),
                    RecordDate = DateTime.UtcNow
                };

                _db.Roles.Add(role);
                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Rol creado: {RoleName} (ID: {Id})", role.ROLE_NAME, role.ID_ROLE);

                return CreatedAtAction(nameof(GetById), new { id = role.ID_ROLE }, new RoleDto
                {
                    Id = role.ID_ROLE,
                    RoleName = role.ROLE_NAME,
                    Description = role.DESCRIPTION,
                    IsSystem = role.IS_SYSTEM,
                    IsActive = role.IS_ACTIVE
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando rol");
                return StatusCode(500, new { message = "Error creando rol" });
            }
        }

        /// <summary>
        /// Actualizar un rol
        /// PUT: api/role/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] RoleUpdateDto model)
        {
            try
            {
                var role = await _db.Roles.FindAsync(id);
                if (role == null)
                    return NotFound(new { message = "Rol no encontrado" });

                if (role.IS_SYSTEM)
                    return BadRequest(new { message = "No se puede modificar un rol del sistema" });

                if (!string.IsNullOrWhiteSpace(model.RoleName))
                {
                    // Verificar nombre duplicado
                    var exists = await _db.Roles.AnyAsync(r => r.ROLE_NAME.ToLower() == model.RoleName.ToLower() && r.ID_ROLE != id);
                    if (exists)
                        return BadRequest(new { message = "Ya existe un rol con ese nombre" });

                    role.ROLE_NAME = model.RoleName.Trim();
                }

                if (model.Description != null)
                    role.DESCRIPTION = model.Description.Trim();

                if (model.IsActive.HasValue)
                    role.IS_ACTIVE = model.IsActive.Value;

                role.UpdatedBy = GetAuditUser();
                role.RecordDate = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                _logger.LogInformation("Rol actualizado: {RoleName} (ID: {Id})", role.ROLE_NAME, role.ID_ROLE);

                return Ok(new RoleDto
                {
                    Id = role.ID_ROLE,
                    RoleName = role.ROLE_NAME,
                    Description = role.DESCRIPTION,
                    IsSystem = role.IS_SYSTEM,
                    IsActive = role.IS_ACTIVE
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando rol {Id}", id);
                return StatusCode(500, new { message = "Error actualizando rol" });
            }
        }

        /// <summary>
        /// Eliminar un rol (soft delete)
        /// DELETE: api/role/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var role = await _db.Roles.FindAsync(id);
                if (role == null)
                    return NotFound(new { message = "Rol no encontrado" });

                if (role.IS_SYSTEM)
                    return BadRequest(new { message = "No se puede eliminar un rol del sistema" });

                // Verificar si hay usuarios con este rol
                var usersWithRole = await _db.UserCompanyRoles.AnyAsync(ucr => ucr.ID_ROLE == id && ucr.IS_ACTIVE);
                if (usersWithRole)
                    return BadRequest(new { message = "No se puede eliminar un rol que tiene usuarios asignados" });

                // Soft delete
                role.IS_ACTIVE = false;
                role.UpdatedBy = GetAuditUser();
                role.RecordDate = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                _logger.LogInformation("Rol eliminado: {RoleName} (ID: {Id})", role.ROLE_NAME, role.ID_ROLE);

                return Ok(new { message = "Rol eliminado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando rol {Id}", id);
                return StatusCode(500, new { message = "Error eliminando rol" });
            }
        }

        /// <summary>
        /// Actualizar permisos de un rol
        /// PUT: api/role/{id}/permissions
        /// </summary>
        [HttpPut("{id}/permissions")]
        public async Task<IActionResult> UpdateRolePermissions(int id, [FromBody] UpdatePermissionsDto model)
        {
            try
            {
                var role = await _db.Roles.FindAsync(id);
                if (role == null)
                    return NotFound(new { message = "Rol no encontrado" });

                if (role.IS_SYSTEM)
                    return BadRequest(new { message = "No se pueden modificar los permisos de un rol del sistema" });

                // Eliminar permisos actuales
                var currentPermissions = await _db.RolePermissions
                    .Where(rp => rp.RoleId == id)
                    .ToListAsync();

                _db.RolePermissions.RemoveRange(currentPermissions);

                // Agregar nuevos permisos
                var auditUser = GetAuditUser();
                foreach (var permissionId in model.PermissionIds ?? new List<int>())
                {
                    var newRolePermission = new CMS.Entities.RolePermission
                    {
                        RoleId = id,
                        PermissionId = permissionId,
                        IsAllowed = true,
                        CreateDate = DateTime.UtcNow,
                        CreatedBy = auditUser,
                        UpdatedBy = auditUser,
                        RecordDate = DateTime.UtcNow
                    };
                    _db.RolePermissions.Add(newRolePermission);
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Permisos del rol {RoleName} actualizados: {Count} permisos", 
                    role.ROLE_NAME, model.PermissionIds?.Count ?? 0);

                return Ok(new { message = "Permisos actualizados correctamente", count = model.PermissionIds?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando permisos del rol {Id}", id);
                return StatusCode(500, new { message = "Error actualizando permisos" });
            }
        }

        /// <summary>
        /// Obtener usuarios disponibles para asignar a un rol en una compañía específica
        /// GET: api/role/{roleId}/available-users/{companyId}
        /// </summary>
        [HttpGet("{roleId}/available-users/{companyId}")]
        public async Task<IActionResult> GetAvailableUsers(int roleId, int companyId)
        {
            try
            {
                // Obtener usuarios que están en la compañía pero NO tienen este rol en esa compañía
                var usersWithRole = await _db.UserCompanyRoles
                    .Where(ucr => ucr.ID_COMPANY == companyId && ucr.ID_ROLE == roleId && ucr.IS_ACTIVE)
                    .Select(ucr => ucr.ID_USER)
                    .ToListAsync();

                var availableUsers = await (from uc in _db.UserCompanies
                                             join u in _db.Users on uc.ID_USER equals u.ID_USER
                                             where uc.ID_COMPANY == companyId
                                                   && uc.IS_ACTIVE
                                                   && u.IS_ACTIVE
                                                   && !usersWithRole.Contains(u.ID_USER)
                                             orderby u.DISPLAY_NAME ?? u.USER_NAME
                                             select new UserForRoleDto
                                             {
                                                 Id = u.ID_USER,
                                                 Username = u.USER_NAME,
                                                 Email = u.EMAIL,
                                                 DisplayName = u.DISPLAY_NAME
                                             }).ToListAsync();

                _logger.LogInformation("📋 Usuarios disponibles para rol {RoleId} en compañía {CompanyId}: {Count}",
                    roleId, companyId, availableUsers.Count);

                return Ok(availableUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo usuarios disponibles para rol {RoleId}", roleId);
                return StatusCode(500, new { message = "Error obteniendo usuarios" });
            }
        }

        /// <summary>
        /// Asignar un usuario a un rol en una compañía
        /// POST: api/role/{roleId}/assign-user
        /// </summary>
        [HttpPost("{roleId}/assign-user")]
        public async Task<IActionResult> AssignUserToRole(int roleId, [FromBody] AssignUserToRoleDto model)
        {
            try
            {
                // Validar rol
                var role = await _db.Roles.FindAsync(roleId);
                if (role == null)
                    return NotFound(new { message = "Rol no encontrado" });

                // Validar usuario
                var user = await _db.Users.FindAsync(model.UserId);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Validar que el usuario esté en la compañía
                var userInCompany = await _db.UserCompanies
                    .AnyAsync(uc => uc.ID_USER == model.UserId && uc.ID_COMPANY == model.CompanyId && uc.IS_ACTIVE);

                if (!userInCompany)
                    return BadRequest(new { message = "El usuario no está asignado a esa compañía" });

                // Verificar si ya tiene el rol
                var alreadyAssigned = await _db.UserCompanyRoles
                    .AnyAsync(ucr => ucr.ID_USER == model.UserId
                                     && ucr.ID_COMPANY == model.CompanyId
                                     && ucr.ID_ROLE == roleId);

                if (alreadyAssigned)
                {
                    // Reactivar si estaba inactivo
                    var existingAssignment = await _db.UserCompanyRoles
                        .FirstOrDefaultAsync(ucr => ucr.ID_USER == model.UserId
                                                    && ucr.ID_COMPANY == model.CompanyId
                                                    && ucr.ID_ROLE == roleId);
                    if (existingAssignment != null && !existingAssignment.IS_ACTIVE)
                    {
                        existingAssignment.IS_ACTIVE = true;
                        existingAssignment.RecordDate = DateTime.UtcNow;
                        existingAssignment.UpdatedBy = GetAuditUser();
                        await _db.SaveChangesAsync();

                        _logger.LogInformation("✅ Usuario {Username} reactivado en rol {RoleName} para compañía {CompanyId}",
                            user.USER_NAME, role.ROLE_NAME, model.CompanyId);

                        return Ok(new { message = "Usuario asignado al rol", reactivated = true });
                    }
                    return BadRequest(new { message = "El usuario ya tiene este rol en esa compañía" });
                }

                // Crear la asignación
                var assignment = new CMS.Entities.UserCompanyRole
                {
                    ID_USER = model.UserId,
                    ID_COMPANY = model.CompanyId,
                    ID_ROLE = roleId,
                    IS_ACTIVE = true,
                    RowPointer = Guid.NewGuid(),
                    CreateDate = DateTime.UtcNow,
                    RecordDate = DateTime.UtcNow,
                    CreatedBy = GetAuditUser(),
                    UpdatedBy = GetAuditUser()
                };

                _db.UserCompanyRoles.Add(assignment);
                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Usuario {Username} asignado al rol {RoleName} para compañía {CompanyId}",
                    user.USER_NAME, role.ROLE_NAME, model.CompanyId);

                return Ok(new { message = "Usuario asignado al rol correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error asignando usuario al rol {RoleId}", roleId);
                return StatusCode(500, new { message = "Error asignando usuario al rol" });
            }
        }

        #region DTOs

        public class AssignUserToRoleDto
        {
            public int UserId { get; set; }
            public int CompanyId { get; set; }
        }

        public class UserForRoleDto
        {
            public int Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? DisplayName { get; set; }
        }

        public class UpdatePermissionsDto
        {
            public List<int>? PermissionIds { get; set; }
        }

        public class RoleDto
        {
            public int Id { get; set; }
            public string RoleName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public bool IsSystem { get; set; }
            public bool IsActive { get; set; }
            public int UserCount { get; set; }
            public int PermissionCount { get; set; }
        }

        public class RoleCreateDto
        {
            public string RoleName { get; set; } = string.Empty;
            public string? Description { get; set; }
        }

        public class RoleUpdateDto
        {
            public string? RoleName { get; set; }
            public string? Description { get; set; }
            public bool? IsActive { get; set; }
        }

        public class PermissionDto
        {
            public int Id { get; set; }
            public string PermissionKey { get; set; } = string.Empty;
            public string PermissionName { get; set; } = string.Empty;
            public bool IsAllowed { get; set; }
        }

        public class UserSimpleDto
        {
            public int Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public string? Email { get; set; }
        }

        public class RoleDetailDto
        {
            public int Id { get; set; }
            public string RoleName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public bool IsSystem { get; set; }
            public bool IsActive { get; set; }
            public List<PermissionDto> Permissions { get; set; } = new();
            public List<UserSimpleDto> Users { get; set; } = new();
        }

        #endregion
    }
}
