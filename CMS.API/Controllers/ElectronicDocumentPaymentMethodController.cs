// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentPaymentMethodController.cs
// PROPÓSITO: API REST CRUD para el catálogo CENTRAL
//            admin.electronic_document_payment_methods (medios de pago Hacienda CR v4.4).
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
    [Route("api/electronicdocumentpaymentmethod")]
    public class ElectronicDocumentPaymentMethodController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ElectronicDocumentPaymentMethodController(AppDbContext db)
        {
            _db = db;
        }

        public class PaymentMethodDto
        {
            public string Code { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int SortOrder { get; set; } = 0;
            public bool IsActive { get; set; } = true;
        }

        // GET /api/electronicdocumentpaymentmethod?search=&onlyActive=&page=&pageSize=
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

            var query = _db.ElectronicDocumentPaymentMethods.AsNoTracking().AsQueryable();

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

        // GET /api/electronicdocumentpaymentmethod/active  → lista simple activa (para selectores)
        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] string? search, CancellationToken ct = default)
        {
            var query = _db.ElectronicDocumentPaymentMethods.AsNoTracking().Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Description.ToLower().Contains(s));
            }

            var items = await query
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Code)
                .Select(a => new { a.Id, a.Code, a.Description })
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET /api/electronicdocumentpaymentmethod/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var item = await _db.ElectronicDocumentPaymentMethods.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (item == null)
                return NotFound(new { message = $"No existe el medio de pago con id {id}." });
            return Ok(item);
        }

        // POST /api/electronicdocumentpaymentmethod
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PaymentMethodDto input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
                return BadRequest(new { message = "El código del medio de pago es obligatorio." });
            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var code = input.Code.Trim();
            var exists = await _db.ElectronicDocumentPaymentMethods.AnyAsync(a => a.Code == code, ct);
            if (exists)
                return Conflict(new { message = $"Ya existe un medio de pago con el código '{code}'." });

            var entity = new ElectronicDocumentPaymentMethod
            {
                Code = code,
                Description = input.Description.Trim(),
                SortOrder = input.SortOrder,
                IsActive = input.IsActive
            };

            _db.ElectronicDocumentPaymentMethods.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // PUT /api/electronicdocumentpaymentmethod/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] PaymentMethodDto input, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentPaymentMethods.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el medio de pago con id {id}." });

            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var newCode = (input.Code ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(newCode) && newCode != entity.Code)
            {
                var dup = await _db.ElectronicDocumentPaymentMethods.AnyAsync(a => a.Code == newCode && a.Id != id, ct);
                if (dup)
                    return Conflict(new { message = $"Ya existe un medio de pago con el código '{newCode}'." });
                entity.Code = newCode;
            }

            entity.Description = input.Description.Trim();
            entity.SortOrder = input.SortOrder;
            entity.IsActive = input.IsActive;

            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // DELETE /api/electronicdocumentpaymentmethod/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentPaymentMethods.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el medio de pago con id {id}." });

            _db.ElectronicDocumentPaymentMethods.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(new { message = "Medio de pago eliminado." });
        }
    }
}
