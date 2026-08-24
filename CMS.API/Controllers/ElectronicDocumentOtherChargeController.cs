// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentOtherChargeController.cs
// PROPÓSITO: API REST CRUD para el catálogo CENTRAL
//            admin.electronic_document_other_charges ("Otros cargos" Hacienda CR v4.4).
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
    [Route("api/electronicdocumentothercharge")]
    public class ElectronicDocumentOtherChargeController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ElectronicDocumentOtherChargeController(AppDbContext db)
        {
            _db = db;
        }

        public class OtherChargeDto
        {
            public string Code { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public bool RequiresIdentification { get; set; } = false;
            public int SortOrder { get; set; } = 0;
            public bool IsActive { get; set; } = true;
        }

        // GET /api/electronicdocumentothercharge?search=&onlyActive=&page=&pageSize=
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

            var query = _db.ElectronicDocumentOtherCharges.AsNoTracking().AsQueryable();

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

        // GET /api/electronicdocumentothercharge/active  → lista simple activa (para selectores)
        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] string? search, CancellationToken ct = default)
        {
            var query = _db.ElectronicDocumentOtherCharges.AsNoTracking().Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Description.ToLower().Contains(s));
            }

            var items = await query
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Code)
                .Select(a => new { a.Id, a.Code, a.Description, a.RequiresIdentification })
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET /api/electronicdocumentothercharge/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var item = await _db.ElectronicDocumentOtherCharges.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (item == null)
                return NotFound(new { message = $"No existe el otro cargo con id {id}." });
            return Ok(item);
        }

        // POST /api/electronicdocumentothercharge
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] OtherChargeDto input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
                return BadRequest(new { message = "El código del otro cargo es obligatorio." });
            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var code = input.Code.Trim();
            var exists = await _db.ElectronicDocumentOtherCharges.AnyAsync(a => a.Code == code, ct);
            if (exists)
                return Conflict(new { message = $"Ya existe un otro cargo con el código '{code}'." });

            var entity = new ElectronicDocumentOtherCharge
            {
                Code = code,
                Description = input.Description.Trim(),
                RequiresIdentification = input.RequiresIdentification,
                SortOrder = input.SortOrder,
                IsActive = input.IsActive
            };

            _db.ElectronicDocumentOtherCharges.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // PUT /api/electronicdocumentothercharge/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] OtherChargeDto input, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentOtherCharges.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el otro cargo con id {id}." });

            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var newCode = (input.Code ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(newCode) && newCode != entity.Code)
            {
                var dup = await _db.ElectronicDocumentOtherCharges.AnyAsync(a => a.Code == newCode && a.Id != id, ct);
                if (dup)
                    return Conflict(new { message = $"Ya existe un otro cargo con el código '{newCode}'." });
                entity.Code = newCode;
            }

            entity.Description = input.Description.Trim();
            entity.RequiresIdentification = input.RequiresIdentification;
            entity.SortOrder = input.SortOrder;
            entity.IsActive = input.IsActive;

            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // DELETE /api/electronicdocumentothercharge/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentOtherCharges.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el otro cargo con id {id}." });

            _db.ElectronicDocumentOtherCharges.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(new { message = "Otro cargo eliminado." });
        }
    }
}
