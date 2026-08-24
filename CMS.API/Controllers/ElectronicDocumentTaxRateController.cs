// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentTaxRateController.cs
// PROPÓSITO: API REST CRUD para el catálogo CENTRAL
//            admin.electronic_document_tax_rate (códigos de tarifa IVA Hacienda CR v4.4).
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
    [Route("api/electronicdocumenttaxrate")]
    public class ElectronicDocumentTaxRateController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ElectronicDocumentTaxRateController(AppDbContext db)
        {
            _db = db;
        }

        public class TaxRateDto
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public decimal RatePercent { get; set; } = 0;
            public bool IsDefault { get; set; } = false;
            public bool IsExempt { get; set; } = false;
            public int DisplayOrder { get; set; } = 0;
            public bool IsActive { get; set; } = true;
            public string? Notes { get; set; }
        }

        // GET /api/electronicdocumenttaxrate?search=&onlyActive=&page=&pageSize=
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

            var query = _db.ElectronicDocumentTaxRates.AsNoTracking().AsQueryable();

            if (onlyActive)
                query = query.Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Name.ToLower().Contains(s));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(a => a.DisplayOrder).ThenBy(a => a.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        // GET /api/electronicdocumenttaxrate/active  → lista simple activa (para selectores)
        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] string? search, CancellationToken ct = default)
        {
            var query = _db.ElectronicDocumentTaxRates.AsNoTracking().Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Name.ToLower().Contains(s));
            }

            var items = await query
                .OrderBy(a => a.DisplayOrder).ThenBy(a => a.Code)
                .Select(a => new { a.Id, a.Code, a.Name, a.RatePercent, a.IsDefault, a.IsExempt })
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET /api/electronicdocumenttaxrate/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var item = await _db.ElectronicDocumentTaxRates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (item == null)
                return NotFound(new { message = $"No existe el código de tarifa con id {id}." });
            return Ok(item);
        }

        // POST /api/electronicdocumenttaxrate
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TaxRateDto input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
                return BadRequest(new { message = "El código de tarifa es obligatorio." });
            if (string.IsNullOrWhiteSpace(input.Name))
                return BadRequest(new { message = "El nombre es obligatorio." });

            var code = input.Code.Trim();
            var exists = await _db.ElectronicDocumentTaxRates.AnyAsync(a => a.Code == code, ct);
            if (exists)
                return Conflict(new { message = $"Ya existe un código de tarifa con el código '{code}'." });

            var entity = new ElectronicDocumentTaxRate
            {
                Code = code,
                Name = input.Name.Trim(),
                Description = input.Description?.Trim(),
                RatePercent = input.RatePercent,
                IsDefault = input.IsDefault,
                IsExempt = input.IsExempt,
                DisplayOrder = input.DisplayOrder,
                IsActive = input.IsActive,
                Notes = input.Notes?.Trim()
            };

            _db.ElectronicDocumentTaxRates.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // PUT /api/electronicdocumenttaxrate/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] TaxRateDto input, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentTaxRates.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el código de tarifa con id {id}." });

            if (string.IsNullOrWhiteSpace(input.Name))
                return BadRequest(new { message = "El nombre es obligatorio." });

            var newCode = (input.Code ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(newCode) && newCode != entity.Code)
            {
                var dup = await _db.ElectronicDocumentTaxRates.AnyAsync(a => a.Code == newCode && a.Id != id, ct);
                if (dup)
                    return Conflict(new { message = $"Ya existe un código de tarifa con el código '{newCode}'." });
                entity.Code = newCode;
            }

            entity.Name = input.Name.Trim();
            entity.Description = input.Description?.Trim();
            entity.RatePercent = input.RatePercent;
            entity.IsDefault = input.IsDefault;
            entity.IsExempt = input.IsExempt;
            entity.DisplayOrder = input.DisplayOrder;
            entity.IsActive = input.IsActive;
            entity.Notes = input.Notes?.Trim();

            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // DELETE /api/electronicdocumenttaxrate/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentTaxRates.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el código de tarifa con id {id}." });

            _db.ElectronicDocumentTaxRates.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(new { message = "Código de tarifa eliminado." });
        }
    }
}
