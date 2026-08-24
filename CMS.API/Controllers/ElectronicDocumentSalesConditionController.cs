// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentSalesConditionController.cs
// PROPÓSITO: API REST CRUD para el catálogo CENTRAL
//            admin.electronic_document_sales_conditions (condiciones de venta Hacienda CR v4.4).
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
    [Route("api/electronicdocumentsalescondition")]
    public class ElectronicDocumentSalesConditionController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ElectronicDocumentSalesConditionController(AppDbContext db)
        {
            _db = db;
        }

        public class SalesConditionDto
        {
            public string Code { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public bool RequiresCreditTerm { get; set; } = false;
            public int DefaultCreditTermDays { get; set; } = 30;
            public int SortOrder { get; set; } = 0;
            public bool IsActive { get; set; } = true;
        }

        // GET /api/electronicdocumentsalescondition?search=&onlyActive=&page=&pageSize=
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

            var query = _db.ElectronicDocumentSalesConditions.AsNoTracking().AsQueryable();

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

        // GET /api/electronicdocumentsalescondition/active  → lista simple activa (para selectores)
        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] string? search, CancellationToken ct = default)
        {
            var query = _db.ElectronicDocumentSalesConditions.AsNoTracking().Where(a => a.IsActive);

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
                    a.RequiresCreditTerm,
                    a.DefaultCreditTermDays
                })
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET /api/electronicdocumentsalescondition/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var item = await _db.ElectronicDocumentSalesConditions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (item == null)
                return NotFound(new { message = $"No existe la condición de venta con id {id}." });
            return Ok(item);
        }

        // POST /api/electronicdocumentsalescondition
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SalesConditionDto input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
                return BadRequest(new { message = "El código de condición de venta es obligatorio." });
            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var code = input.Code.Trim();
            var exists = await _db.ElectronicDocumentSalesConditions.AnyAsync(a => a.Code == code, ct);
            if (exists)
                return Conflict(new { message = $"Ya existe una condición de venta con el código '{code}'." });

            var entity = new ElectronicDocumentSalesCondition
            {
                Code = code,
                Description = input.Description.Trim(),
                RequiresCreditTerm = input.RequiresCreditTerm,
                DefaultCreditTermDays = input.DefaultCreditTermDays > 0 ? input.DefaultCreditTermDays : 30,
                SortOrder = input.SortOrder,
                IsActive = input.IsActive
            };

            _db.ElectronicDocumentSalesConditions.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // PUT /api/electronicdocumentsalescondition/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SalesConditionDto input, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentSalesConditions.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe la condición de venta con id {id}." });

            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var newCode = (input.Code ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(newCode) && newCode != entity.Code)
            {
                var dup = await _db.ElectronicDocumentSalesConditions.AnyAsync(a => a.Code == newCode && a.Id != id, ct);
                if (dup)
                    return Conflict(new { message = $"Ya existe una condición de venta con el código '{newCode}'." });
                entity.Code = newCode;
            }

            entity.Description = input.Description.Trim();
            entity.RequiresCreditTerm = input.RequiresCreditTerm;
            entity.DefaultCreditTermDays = input.DefaultCreditTermDays > 0 ? input.DefaultCreditTermDays : 30;
            entity.SortOrder = input.SortOrder;
            entity.IsActive = input.IsActive;

            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // DELETE /api/electronicdocumentsalescondition/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentSalesConditions.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe la condición de venta con id {id}." });

            _db.ElectronicDocumentSalesConditions.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(new { message = "Condición de venta eliminada." });
        }
    }
}
