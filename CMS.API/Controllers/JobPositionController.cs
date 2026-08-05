// ================================================================================
// ARCHIVO: CMS.API/Controllers/JobPositionController.cs
// PROPÓSITO: API REST CRUD para catálogo de puestos/cargos del módulo Human Resources
// DESCRIPCIÓN: Gestión de puestos por compañía. La tabla job_position reside
//              en la BD de cada compañía ({schema}.job_position).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-07-05
// ================================================================================

using CMS.Data;
using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/jobpositions")]
    public class JobPositionController : ControllerBase
    {
        private readonly ICompanyDbContextFactory _factory;
        private readonly ILogger<JobPositionController> _logger;

        public JobPositionController(
            ICompanyDbContextFactory factory,
            ILogger<JobPositionController> logger)
        {
            _factory = factory;
            _logger  = logger;
        }

        private int GetCurrentCompanyId()
        {
            var v = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(v, out var id)) return id;
            throw new UnauthorizedAccessException("companyId no encontrado en el token JWT");
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "SYSTEM";

        // ============================================================
        // GET /api/jobpositions
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool? isActive = null, [FromQuery] int? idDepartment = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var q = db.JobPositions.AsQueryable();
                if (isActive.HasValue)    q = q.Where(x => x.IsActive == isActive.Value);
                if (idDepartment.HasValue) q = q.Where(x => x.IdDepartment == idDepartment.Value);

                var items = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                    .Select(x => new { x.Id, x.Code, x.Name, x.Description, x.Level, x.IdDepartment, x.IsDriver, x.SortOrder, x.IsActive, x.CreateDate, x.RecordDate })
                    .ToListAsync();

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener puestos");
                return StatusCode(500, new { message = "Error al obtener puestos." });
            }
        }

        // ============================================================
        // GET /api/jobpositions/{id}
        // ============================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);
                var item = await db.JobPositions.FindAsync(id);
                return item == null ? NotFound(new { message = "Puesto no encontrado." }) : Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener puesto {Id}", id);
                return StatusCode(500, new { message = "Error al obtener el puesto." });
            }
        }

        // ============================================================
        // POST /api/jobpositions
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] JobPosition dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.IdDepartment <= 0)
                return BadRequest(new { message = "El Departamento es obligatorio." });
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                if (await db.JobPositions.AnyAsync(x => x.Code == dto.Code.Trim().ToUpper()))
                    return Conflict(new { message = $"El código '{dto.Code}' ya existe." });

                dto.Code       = dto.Code.Trim().ToUpper();
                dto.Name       = dto.Name.Trim();
                dto.CreateDate = DateTime.UtcNow;
                dto.RecordDate = DateTime.UtcNow;
                dto.CreatedBy  = GetCurrentUser();
                dto.UpdatedBy  = GetCurrentUser();
                dto.RowPointer = Guid.NewGuid();

                db.JobPositions.Add(dto);
                await db.SaveChangesAsync();

                _logger.LogInformation("Puesto creado: {Code} por {User}", dto.Code, dto.CreatedBy);
                return CreatedAtAction(nameof(GetById), new { id = dto.Id }, new { dto.Id, dto.Code, dto.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear puesto");
                return StatusCode(500, new { message = "Error al crear el puesto." });
            }
        }

        // ============================================================
        // PUT /api/jobpositions/{id}
        // ============================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] JobPosition dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.IdDepartment <= 0)
                return BadRequest(new { message = "El Departamento es obligatorio." });
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var item = await db.JobPositions.FindAsync(id);
                if (item == null) return NotFound(new { message = "Puesto no encontrado." });

                if (await db.JobPositions.AnyAsync(x => x.Code == dto.Code.Trim().ToUpper() && x.Id != id))
                    return Conflict(new { message = $"El código '{dto.Code}' ya está en uso." });

                item.Code        = dto.Code.Trim().ToUpper();
                item.Name        = dto.Name.Trim();
                item.Description = dto.Description?.Trim();
                item.Level       = dto.Level?.Trim();
                item.IdDepartment = dto.IdDepartment;
                item.IsDriver    = dto.IsDriver;
                item.SortOrder   = dto.SortOrder;
                item.IsActive    = dto.IsActive;
                item.RecordDate  = DateTime.UtcNow;
                item.UpdatedBy   = GetCurrentUser();

                await db.SaveChangesAsync();
                _logger.LogInformation("Puesto actualizado: {Code} por {User}", item.Code, item.UpdatedBy);
                return Ok(new { item.Id, item.Code, item.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar puesto {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar el puesto." });
            }
        }

        // ============================================================
        // DELETE /api/jobpositions/{id}
        // ============================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);
                var item = await db.JobPositions.FindAsync(id);
                if (item == null) return NotFound();

                var inUse = await db.Employees.AnyAsync(e => e.IdJobPosition == id);
                if (inUse) return Conflict(new { message = "El puesto está asignado a uno o más empleados y no puede eliminarse." });

                db.JobPositions.Remove(item);
                await db.SaveChangesAsync();
                return Ok(new { message = "Puesto eliminado." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar puesto {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar el puesto." });
            }
        }
    }
}
