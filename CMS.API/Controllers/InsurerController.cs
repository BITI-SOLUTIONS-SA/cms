// ================================================================================
// ARCHIVO: CMS.API/Controllers/InsurerController.cs
// PROPÓSITO: API REST CRUD para aseguradoras de la flota (por compañía)
// DESCRIPCIÓN: Gestión de aseguradoras y datos de agentes/contactos.
//              Tabla en la BD de cada compañía, schema {company_code}.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-14
// ================================================================================

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
    [Route("api/insurers")]
    public class InsurerController : ControllerBase
    {
        private readonly ICompanyDbContextFactory _factory;
        private readonly ILogger<InsurerController> _logger;

        public InsurerController(ICompanyDbContextFactory factory, ILogger<InsurerController> logger)
        {
            _factory = factory;
            _logger = logger;
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
        // GET /api/insurers
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var q = db.Insurers.AsQueryable();
                if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.ToLower();
                    q = q.Where(x =>
                        x.Name.ToLower().Contains(s) ||
                        x.Code.ToLower().Contains(s) ||
                        (x.TaxId != null && x.TaxId.ToLower().Contains(s)) ||
                        (x.TradeName != null && x.TradeName.ToLower().Contains(s)));
                }

                var total = await q.CountAsync();
                var items = await q
                    .OrderBy(x => x.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Ok(new { total, page, pageSize, items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener aseguradoras");
                return StatusCode(500, new { message = "Error al obtener aseguradoras." });
            }
        }

        // ============================================================
        // GET /api/insurers/{id}
        // ============================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var insurer = await db.Insurers.FindAsync(id);
                if (insurer == null) return NotFound(new { message = "Aseguradora no encontrada." });
                return Ok(insurer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener aseguradora {Id}", id);
                return StatusCode(500, new { message = "Error al obtener la aseguradora." });
            }
        }

        // ============================================================
        // POST /api/insurers
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Insurer dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                if (await db.Insurers.AnyAsync(x => x.Code == dto.Code))
                    return Conflict(new { message = $"El código '{dto.Code}' ya existe." });

                if (!string.IsNullOrWhiteSpace(dto.TaxId) && await db.Insurers.AnyAsync(x => x.TaxId == dto.TaxId))
                    return Conflict(new { message = $"El número de identificación tributaria '{dto.TaxId}' ya existe." });

                dto.CreatedBy = GetCurrentUser();
                dto.UpdatedBy = GetCurrentUser();
                dto.CreateDate = DateTime.UtcNow;
                dto.RecordDate = DateTime.UtcNow;
                dto.Rowpointer = Guid.NewGuid();

                db.Insurers.Add(dto);
                await db.SaveChangesAsync();
                return CreatedAtAction(nameof(GetById), new { id = dto.Id }, new { dto.Id, dto.Code, dto.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear aseguradora");
                return StatusCode(500, new { message = "Error al crear la aseguradora." });
            }
        }

        // ============================================================
        // PUT /api/insurers/{id}
        // ============================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Insurer dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var entity = await db.Insurers.FindAsync(id);
                if (entity == null) return NotFound(new { message = "Aseguradora no encontrada." });

                if (await db.Insurers.AnyAsync(x => x.Code == dto.Code && x.Id != id))
                    return Conflict(new { message = $"El código '{dto.Code}' ya existe en otra aseguradora." });

                if (!string.IsNullOrWhiteSpace(dto.TaxId) &&
                    await db.Insurers.AnyAsync(x => x.TaxId == dto.TaxId && x.Id != id))
                    return Conflict(new { message = $"El número de identificación tributaria '{dto.TaxId}' ya existe en otra aseguradora." });

                entity.Code = dto.Code;
                entity.Name = dto.Name;
                entity.TradeName = dto.TradeName;
                entity.TaxId = dto.TaxId;
                entity.Phone = dto.Phone;
                entity.PhoneClaims = dto.PhoneClaims;
                entity.Email = dto.Email;
                entity.Website = dto.Website;
                entity.Address = dto.Address;
                entity.AgentName = dto.AgentName;
                entity.AgentPhone = dto.AgentPhone;
                entity.AgentEmail = dto.AgentEmail;
                entity.IsActive = dto.IsActive;
                entity.Notes = dto.Notes;
                entity.UpdatedBy = GetCurrentUser();
                entity.RecordDate = DateTime.UtcNow;

                await db.SaveChangesAsync();
                return Ok(new { entity.Id, entity.Code, entity.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar aseguradora {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar la aseguradora." });
            }
        }

        // ============================================================
        // DELETE /api/insurers/{id}
        // ============================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var entity = await db.Insurers.FindAsync(id);
                if (entity == null) return NotFound(new { message = "Aseguradora no encontrada." });

                entity.IsActive = false;
                entity.UpdatedBy = GetCurrentUser();
                entity.RecordDate = DateTime.UtcNow;

                await db.SaveChangesAsync();
                return Ok(new { message = "Aseguradora desactivada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar aseguradora {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar la aseguradora." });
            }
        }
    }
}
