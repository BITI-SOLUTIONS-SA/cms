// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentIdentificationTypeController.cs
// PROPÓSITO: API REST CRUD para el catálogo CENTRAL
//            admin.electronic_document_identification_type (tipos de identificación Hacienda CR v4.4).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Data;
using CMS.Entities.EInvoice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/electronicdocumentidentificationtype")]
    public class ElectronicDocumentIdentificationTypeController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ElectronicDocumentIdentificationTypeController(AppDbContext db)
        {
            _db = db;
        }

        public class IdentificationTypeDto
        {
            public string Code { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int? Length { get; set; }
            public string? RegexPattern { get; set; }
            public string? FormatHint { get; set; }
            public int SortOrder { get; set; } = 0;
            public bool IsActive { get; set; } = true;
        }

        // GET /api/electronicdocumentidentificationtype?search=&onlyActive=&page=&pageSize=
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] bool onlyActive = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 500) pageSize = 500;

            var query = _db.ElectronicDocumentIdentificationTypes.AsNoTracking().AsQueryable();

            if (onlyActive)
                query = query.Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Description.ToLower().Contains(s));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        // GET /api/electronicdocumentidentificationtype/active  → lista simple activa (para selectores)
        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] string? search, CancellationToken ct = default)
        {
            var query = _db.ElectronicDocumentIdentificationTypes.AsNoTracking().Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Description.ToLower().Contains(s));
            }

            var items = await query
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Code)
                .Select(a => new
                {
                    a.Id,
                    a.Code,
                    a.Description,
                    a.Length,
                    a.RegexPattern,
                    a.FormatHint
                })
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET /api/electronicdocumentidentificationtype/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var item = await _db.ElectronicDocumentIdentificationTypes.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (item == null)
                return NotFound(new { message = $"No existe el tipo de identificación con id {id}." });
            return Ok(item);
        }

        // POST /api/electronicdocumentidentificationtype
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] IdentificationTypeDto input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
                return BadRequest(new { message = "El código del tipo de identificación es obligatorio." });
            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var code = input.Code.Trim();
            var exists = await _db.ElectronicDocumentIdentificationTypes.AnyAsync(a => a.Code == code, ct);
            if (exists)
                return Conflict(new { message = $"Ya existe un tipo de identificación con el código '{code}'." });

            var entity = new ElectronicDocumentIdentificationType
            {
                Code = code,
                Description = input.Description.Trim(),
                Length = input.Length,
                RegexPattern = string.IsNullOrWhiteSpace(input.RegexPattern) ? null : input.RegexPattern.Trim(),
                FormatHint = string.IsNullOrWhiteSpace(input.FormatHint) ? null : input.FormatHint.Trim(),
                SortOrder = input.SortOrder,
                IsActive = input.IsActive
            };

            _db.ElectronicDocumentIdentificationTypes.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // PUT /api/electronicdocumentidentificationtype/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] IdentificationTypeDto input, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentIdentificationTypes.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el tipo de identificación con id {id}." });

            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var newCode = (input.Code ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(newCode) && newCode != entity.Code)
            {
                var dup = await _db.ElectronicDocumentIdentificationTypes.AnyAsync(a => a.Code == newCode && a.Id != id, ct);
                if (dup)
                    return Conflict(new { message = $"Ya existe un tipo de identificación con el código '{newCode}'." });
                entity.Code = newCode;
            }

            entity.Description = input.Description.Trim();
            entity.Length = input.Length;
            entity.RegexPattern = string.IsNullOrWhiteSpace(input.RegexPattern) ? null : input.RegexPattern.Trim();
            entity.FormatHint = string.IsNullOrWhiteSpace(input.FormatHint) ? null : input.FormatHint.Trim();
            entity.SortOrder = input.SortOrder;
            entity.IsActive = input.IsActive;

            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // DELETE /api/electronicdocumentidentificationtype/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentIdentificationTypes.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el tipo de identificación con id {id}." });

            _db.ElectronicDocumentIdentificationTypes.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
