using CMS.Application.DTOs;
using CMS.Data;
using CMS.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class PermissionsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<PermissionsController> _logger;

        public PermissionsController(AppDbContext db, ILogger<PermissionsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // GET: api/permissions - Lista completa de permisos
        [HttpGet]
        public async Task<ActionResult<List<PermissionListDto>>> GetAll()
        {
            try
            {
                var permissions = await _db.Permissions
                    .Select(p => new PermissionListDto
                    {
                        Id = p.ID_PERMISSION,
                        PermissionKey = p.PERMISSION_KEY,
                        PermissionName = p.PERMISSION_NAME,
                        Description = p.DESCRIPTION,
                        Module = p.MODULE,
                        IsActive = p.IS_ACTIVE
                    })
                    .OrderBy(p => p.Module)
                    .ThenBy(p => p.PermissionName)
                    .ToListAsync();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo permisos");
                return StatusCode(500, new { message = "Error obteniendo permisos" });
            }
        }

        // GET: api/permissions/modules - Lista de módulos únicos
        [HttpGet("modules")]
        public async Task<ActionResult<List<string>>> GetModules()
        {
            try
            {
                var modules = await _db.Permissions
                    .Where(p => p.MODULE != null)
                    .Select(p => p.MODULE!)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToListAsync();

                return Ok(modules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo módulos");
                return StatusCode(500, new { message = "Error obteniendo módulos" });
            }
        }

        // GET: api/permissions/{id} - Detalle de permiso
        [HttpGet("{id}")]
        public async Task<ActionResult<PermissionListDto>> GetById(int id)
        {
            try
            {
                var permission = await _db.Permissions.FindAsync(id);
                if (permission == null)
                    return NotFound(new { message = "Permiso no encontrado" });

                var dto = new PermissionListDto
                {
                    Id = permission.ID_PERMISSION,
                    PermissionKey = permission.PERMISSION_KEY,
                    PermissionName = permission.PERMISSION_NAME,
                    Description = permission.DESCRIPTION,
                    Module = permission.MODULE,
                    IsActive = permission.IS_ACTIVE
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo permiso {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo permiso" });
            }
        }

        // POST: api/permissions - Crear nuevo permiso
        [HttpPost]
        public async Task<ActionResult<PermissionListDto>> Create([FromBody] PermissionCreateDto dto)
        {
            try
            {
                // Validar PermissionKey único
                if (await _db.Permissions.AnyAsync(p => p.PERMISSION_KEY == dto.PermissionKey))
                    return BadRequest(new { message = "El PermissionKey ya existe" });

                var permission = new Permission
                {
                    PERMISSION_KEY = dto.PermissionKey,
                    PERMISSION_NAME = dto.PermissionName,
                    DESCRIPTION = dto.Description ?? string.Empty,
                    MODULE = dto.Module ?? string.Empty,
                    IS_ACTIVE = dto.IsActive,
                    RecordDate = DateTime.UtcNow,
                    CreateDate = DateTime.UtcNow,
                    RowPointer = Guid.NewGuid(),
                    CreatedBy = User.FindFirstValue("cms_username") ?? "SYSTEM",
                    UpdatedBy = User.FindFirstValue("cms_username") ?? "SYSTEM"
                };

                _db.Permissions.Add(permission);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Permiso creado: {PermissionKey} (ID: {PermissionId})",
                    permission.PERMISSION_KEY, permission.ID_PERMISSION);

                return CreatedAtAction(nameof(GetById), new { id = permission.ID_PERMISSION },
                    await GetById(permission.ID_PERMISSION));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando permiso");
                return StatusCode(500, new { message = "Error creando permiso" });
            }
        }

        // PUT: api/permissions/{id} - Actualizar permiso
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PermissionCreateDto dto)
        {
            try
            {
                var permission = await _db.Permissions.FindAsync(id);
                if (permission == null)
                    return NotFound(new { message = "Permiso no encontrado" });

                // Validar PermissionKey único si cambió
                if (dto.PermissionKey != permission.PERMISSION_KEY &&
                    await _db.Permissions.AnyAsync(p => p.PERMISSION_KEY == dto.PermissionKey && p.ID_PERMISSION != id))
                    return BadRequest(new { message = "El PermissionKey ya existe" });

                permission.PERMISSION_KEY = dto.PermissionKey;
                permission.PERMISSION_NAME = dto.PermissionName;
                permission.DESCRIPTION = dto.Description ?? permission.DESCRIPTION;
                permission.MODULE = dto.Module ?? permission.MODULE;
                permission.IS_ACTIVE = dto.IsActive;
                permission.UpdatedBy = User.FindFirstValue("cms_username") ?? "SYSTEM";

                await _db.SaveChangesAsync();

                _logger.LogInformation("Permiso actualizado: {PermissionKey} (ID: {PermissionId})",
                    permission.PERMISSION_KEY, permission.ID_PERMISSION);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando permiso {Id}", id);
                return StatusCode(500, new { message = "Error actualizando permiso" });
            }
        }

        // DELETE: api/permissions/{id} - Eliminar permiso
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var permission = await _db.Permissions.FindAsync(id);
                if (permission == null)
                    return NotFound(new { message = "Permiso no encontrado" });

                // Verificar si está en uso
                var inUseByRoles = await _db.RolePermissions.AnyAsync(rp => rp.PermissionId == id);
                var inUseByUsers = await _db.UserCompanyPermissions.AnyAsync(ucp => ucp.ID_PERMISSION == id);

                if (inUseByRoles || inUseByUsers)
                    return BadRequest(new { message = "No se puede eliminar un permiso en uso" });

                _db.Permissions.Remove(permission);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Permiso eliminado: {PermissionKey} (ID: {PermissionId})",
                    permission.PERMISSION_KEY, permission.ID_PERMISSION);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando permiso {Id}", id);
                return StatusCode(500, new { message = "Error eliminando permiso" });
            }
        }

        // PATCH: api/permissions/{id}/toggle - Activar/Desactivar permiso
        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            try
            {
                var permission = await _db.Permissions.FindAsync(id);
                if (permission == null)
                    return NotFound(new { message = "Permiso no encontrado" });

                permission.IS_ACTIVE = !permission.IS_ACTIVE;
                permission.UpdatedBy = User.FindFirstValue("cms_username") ?? "SYSTEM";

                await _db.SaveChangesAsync();

                _logger.LogInformation("Permiso {PermissionKey} {Status}",
                    permission.PERMISSION_KEY, permission.IS_ACTIVE ? "activado" : "desactivado");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cambiando estado del permiso {Id}", id);
                return StatusCode(500, new { message = "Error cambiando estado" });
            }
        }
    }
}