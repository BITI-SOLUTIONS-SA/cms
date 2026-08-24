// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentTaxTypeController.cs
// PROPÓSITO: API REST CRUD para el catálogo CENTRAL
//            admin.electronic_document_tax_type (tipos de impuesto Hacienda CR v4.4).
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
    [Route("api/electronicdocumenttaxtype")]
    public class ElectronicDocumentTaxTypeController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ElectronicDocumentTaxTypeController(AppDbContext db)
        {
            _db = db;
        }

        public class TaxTypeDto
        {
            public string Code { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int SortOrder { get; set; } = 0;
            public bool IsActive { get; set; } = true;
            public bool RequiresTaxRate { get; set; } = false;
        }

        // GET /api/electronicdocumenttaxtype?search=&onlyActive=&page=&pageSize=
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

            var query = _db.ElectronicDocumentTaxTypes.AsNoTracking().AsQueryable();

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

        // GET /api/electronicdocumenttaxtype/active  → lista simple activa (para selectores)
        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] string? search, CancellationToken ct = default)
        {
            var query = _db.ElectronicDocumentTaxTypes.AsNoTracking().Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Description.ToLower().Contains(s));
            }

            var items = await query
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Code)
                .Select(a => new { a.Id, a.Code, a.Description, a.RequiresTaxRate })
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET /api/electronicdocumenttaxtype/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var item = await _db.ElectronicDocumentTaxTypes.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (item == null)
                return NotFound(new { message = $"No existe el tipo de impuesto con id {id}." });
            return Ok(item);
        }

        // POST /api/electronicdocumenttaxtype
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TaxTypeDto input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
                return BadRequest(new { message = "El código del tipo de impuesto es obligatorio." });
            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var code = input.Code.Trim();
            var exists = await _db.ElectronicDocumentTaxTypes.AnyAsync(a => a.Code == code, ct);
            if (exists)
                return Conflict(new { message = $"Ya existe un tipo de impuesto con el código '{code}'." });

            var entity = new ElectronicDocumentTaxType
            {
                Code = code,
                Description = input.Description.Trim(),
                SortOrder = input.SortOrder,
                IsActive = input.IsActive,
                RequiresTaxRate = input.RequiresTaxRate
            };

            _db.ElectronicDocumentTaxTypes.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // PUT /api/electronicdocumenttaxtype/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] TaxTypeDto input, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentTaxTypes.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el tipo de impuesto con id {id}." });

            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var newCode = (input.Code ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(newCode) && newCode != entity.Code)
            {
                var dup = await _db.ElectronicDocumentTaxTypes.AnyAsync(a => a.Code == newCode && a.Id != id, ct);
                if (dup)
                    return Conflict(new { message = $"Ya existe un tipo de impuesto con el código '{newCode}'." });
                entity.Code = newCode;
            }

            entity.Description = input.Description.Trim();
            entity.SortOrder = input.SortOrder;
            entity.IsActive = input.IsActive;
            entity.RequiresTaxRate = input.RequiresTaxRate;

            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // DELETE /api/electronicdocumenttaxtype/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentTaxTypes.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el tipo de impuesto con id {id}." });

            _db.ElectronicDocumentTaxTypes.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(new { message = "Tipo de impuesto eliminado." });
        }
    }
}
