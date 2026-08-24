// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentConsecutiveController.cs
// PROPÓSITO: API CRUD para los consecutivos fiscales de documentos electrónicos.
// DESCRIPCIÓN: Tabla operacional {schema}.electronic_document_consecutives.
//              Permite listar/crear/actualizar consecutivos y marcar el default
//              (único por emisor + tipo + versión). Vive en la BD de la compañía.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Data;
using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ElectronicDocumentConsecutiveController : ControllerBase
    {
        private readonly ICompanyDbContextFactory _factory;
        private readonly ILogger<ElectronicDocumentConsecutiveController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ElectronicDocumentConsecutiveController(
            ICompanyDbContextFactory factory,
            ILogger<ElectronicDocumentConsecutiveController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _factory = factory;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private int GetCompanyId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var claim = user?.FindFirst("companyId")?.Value ?? user?.FindFirst("CompanyId")?.Value;
            return int.TryParse(claim, out var companyId) ? companyId : 0;
        }

        private string GetUserName()
            => _httpContextAccessor.HttpContext?.User.Identity?.Name ?? "system";

        // ============================================================================
        // GET: api/ElectronicDocumentConsecutive
        // ============================================================================
        [HttpGet]
        public async Task<ActionResult<List<ElectronicDocumentConsecutiveDto>>> GetAll(
            [FromQuery] int? versionId = null)
        {
            var companyId = GetCompanyId();
            if (companyId == 0)
                return BadRequest(new { message = "No se pudo determinar la compañía activa" });

            await using var db = await _factory.CreateDbContextAsync(companyId);

            var query = db.ElectronicDocumentConsecutives.AsQueryable();
            if (versionId.HasValue)
                query = query.Where(c => c.IdElectronicDocumentVersion == versionId.Value);

            var list = await query
                .OrderBy(c => c.DocumentType)
                .ThenByDescending(c => c.IsDefault)
                .ThenBy(c => c.Branch)
                .ThenBy(c => c.Terminal)
                .Select(c => ToDto(c))
                .ToListAsync();

            return Ok(list);
        }

        // ============================================================================
        // GET: api/ElectronicDocumentConsecutive/for-emit?issuerCredentialId=&documentType=
        // Devuelve los consecutivos ACTIVOS disponibles para emitir, para el emisor
        // (resuelto desde su credencial de facturación) y el tipo de documento indicados.
        // El registro DEFAULT viene primero para preseleccionarlo en la UI.
        // ============================================================================
        [HttpGet("for-emit")]
        public async Task<ActionResult<List<ElectronicDocumentConsecutiveDto>>> GetForEmit(
            [FromQuery] int issuerCredentialId,
            [FromQuery] string documentType)
        {
            var companyId = GetCompanyId();
            if (companyId == 0)
                return BadRequest(new { message = "No se pudo determinar la compañía activa" });

            if (issuerCredentialId <= 0 || string.IsNullOrWhiteSpace(documentType))
                return Ok(new List<ElectronicDocumentConsecutiveDto>());

            await using var db = await _factory.CreateDbContextAsync(companyId);

            // La UI maneja el Id de la credencial del emisor; el consecutivo se liga al
            // IdCustomer del emisor (IdBillingIssuer). Resolvemos ese IdCustomer aquí.
            var issuerCustomerId = await db.CustomerBillingCredentials
                .Where(c => c.Id == issuerCredentialId && c.IsIssuer)
                .Select(c => c.IdCustomer)
                .FirstOrDefaultAsync();

            if (issuerCustomerId == null || issuerCustomerId == 0)
                return Ok(new List<ElectronicDocumentConsecutiveDto>());

            var list = await db.ElectronicDocumentConsecutives
                .Where(c => c.IdBillingIssuer == issuerCustomerId.Value &&
                            c.DocumentType == documentType &&
                            c.IsActive)
                .OrderByDescending(c => c.IsDefault)
                .ThenBy(c => c.Branch)
                .ThenBy(c => c.Terminal)
                .Select(c => ToDto(c))
                .ToListAsync();

            return Ok(list);
        }

        // ============================================================================
        // GET: api/ElectronicDocumentConsecutive/5
        // ============================================================================
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ElectronicDocumentConsecutiveDto>> GetById(int id)
        {
            var companyId = GetCompanyId();
            if (companyId == 0)
                return BadRequest(new { message = "No se pudo determinar la compañía activa" });

            await using var db = await _factory.CreateDbContextAsync(companyId);
            var entity = await db.ElectronicDocumentConsecutives.FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null) return NotFound();

            return Ok(ToDto(entity));
        }

        // ============================================================================
        // POST: api/ElectronicDocumentConsecutive
        // ============================================================================
        [HttpPost]
        public async Task<ActionResult<ElectronicDocumentConsecutiveDto>> Create(
            [FromBody] ElectronicDocumentConsecutiveDto dto)
        {
            var companyId = GetCompanyId();
            if (companyId == 0)
                return BadRequest(new { message = "No se pudo determinar la compañía activa" });

            await using var db = await _factory.CreateDbContextAsync(companyId);
            var user = GetUserName();

            var entity = new ElectronicDocumentConsecutive
            {
                IdBillingIssuer = dto.IdBillingIssuer,
                IdElectronicDocumentType = dto.IdElectronicDocumentType,
                IdElectronicDocumentVersion = dto.IdElectronicDocumentVersion,
                DocumentType = dto.DocumentType,
                Branch = dto.Branch,
                Terminal = dto.Terminal,
                Consecutive = dto.Consecutive,
                IsDefault = dto.IsDefault,
                IsActive = dto.IsActive,
                Description = dto.Description,
                Notes = dto.Notes,
                CreateDate = DateTime.UtcNow,
                RecordDate = DateTime.UtcNow,
                CreatedBy = user,
                UpdatedBy = user
            };

            // Si se marca como default, desmarcar el default existente del mismo (emisor+tipo+versión).
            if (entity.IsDefault)
                await ClearDefaultAsync(db, entity.IdBillingIssuer, entity.IdElectronicDocumentType,
                    entity.IdElectronicDocumentVersion, excludeId: null);

            db.ElectronicDocumentConsecutives.Add(entity);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
        }

        // ============================================================================
        // PUT: api/ElectronicDocumentConsecutive/5
        // ============================================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ElectronicDocumentConsecutiveDto dto)
        {
            var companyId = GetCompanyId();
            if (companyId == 0)
                return BadRequest(new { message = "No se pudo determinar la compañía activa" });

            await using var db = await _factory.CreateDbContextAsync(companyId);
            var entity = await db.ElectronicDocumentConsecutives.FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null) return NotFound();

            entity.IdBillingIssuer = dto.IdBillingIssuer;
            entity.IdElectronicDocumentType = dto.IdElectronicDocumentType;
            entity.IdElectronicDocumentVersion = dto.IdElectronicDocumentVersion;
            entity.DocumentType = dto.DocumentType;
            entity.Branch = dto.Branch;
            entity.Terminal = dto.Terminal;
            entity.Consecutive = dto.Consecutive;
            entity.IsActive = dto.IsActive;
            entity.Description = dto.Description;
            entity.Notes = dto.Notes;
            entity.UpdatedBy = GetUserName();

            // Manejo del default.
            if (dto.IsDefault && !entity.IsDefault)
                await ClearDefaultAsync(db, entity.IdBillingIssuer, entity.IdElectronicDocumentType,
                    entity.IdElectronicDocumentVersion, excludeId: entity.Id);
            entity.IsDefault = dto.IsDefault;

            await db.SaveChangesAsync();
            return NoContent();
        }

        // ============================================================================
        // POST: api/ElectronicDocumentConsecutive/5/setdefault
        // ============================================================================
        [HttpPost("{id:int}/setdefault")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var companyId = GetCompanyId();
            if (companyId == 0)
                return BadRequest(new { message = "No se pudo determinar la compañía activa" });

            await using var db = await _factory.CreateDbContextAsync(companyId);
            var entity = await db.ElectronicDocumentConsecutives.FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null) return NotFound();

            await ClearDefaultAsync(db, entity.IdBillingIssuer, entity.IdElectronicDocumentType,
                entity.IdElectronicDocumentVersion, excludeId: entity.Id);

            entity.IsDefault = true;
            entity.IsActive = true;
            entity.UpdatedBy = GetUserName();
            await db.SaveChangesAsync();

            return NoContent();
        }

        // ============================================================================
        // DELETE: api/ElectronicDocumentConsecutive/5
        // ============================================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var companyId = GetCompanyId();
            if (companyId == 0)
                return BadRequest(new { message = "No se pudo determinar la compañía activa" });

            await using var db = await _factory.CreateDbContextAsync(companyId);
            var entity = await db.ElectronicDocumentConsecutives.FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null) return NotFound();

            db.ElectronicDocumentConsecutives.Remove(entity);
            await db.SaveChangesAsync();
            return NoContent();
        }

        // ============================================================================
        // Helpers
        // ============================================================================
        private static async Task ClearDefaultAsync(
            CompanyDbContext db, int issuerId, int typeId, int versionId, int? excludeId)
        {
            var current = await db.ElectronicDocumentConsecutives
                .Where(c => c.IdBillingIssuer == issuerId &&
                            c.IdElectronicDocumentType == typeId &&
                            c.IdElectronicDocumentVersion == versionId &&
                            c.IsDefault &&
                            (excludeId == null || c.Id != excludeId.Value))
                .ToListAsync();

            foreach (var c in current)
                c.IsDefault = false;
        }

        private static ElectronicDocumentConsecutiveDto ToDto(ElectronicDocumentConsecutive c)
            => new()
            {
                Id = c.Id,
                IdBillingIssuer = c.IdBillingIssuer,
                IdElectronicDocumentType = c.IdElectronicDocumentType,
                IdElectronicDocumentVersion = c.IdElectronicDocumentVersion,
                DocumentType = c.DocumentType,
                Branch = c.Branch,
                Terminal = c.Terminal,
                Consecutive = c.Consecutive,
                IsDefault = c.IsDefault,
                IsActive = c.IsActive,
                Description = c.Description,
                Notes = c.Notes
            };
    }

    /// <summary>DTO para el mantenimiento de consecutivos de documentos electrónicos.</summary>
    public sealed class ElectronicDocumentConsecutiveDto
    {
        public int Id { get; set; }
        public int IdBillingIssuer { get; set; }
        public int IdElectronicDocumentType { get; set; }
        public int IdElectronicDocumentVersion { get; set; }
        public string DocumentType { get; set; } = "01";
        public string Branch { get; set; } = "001";
        public string Terminal { get; set; } = "00001";
        public long Consecutive { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Description { get; set; }
        public string? Notes { get; set; }
    }
}
