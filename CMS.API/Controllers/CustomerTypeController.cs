// ================================================================================
// ARCHIVO: CMS.API/Controllers/CustomerTypeController.cs
// PROPÓSITO: API REST CRUD para el catálogo CENTRAL admin.customer_type
//            (tipos de cliente: Issuer, Receptor, Issuer-Receptor, Corporate, ...).
// DESCRIPCIÓN: Reemplaza el antiguo campo string sinai.customer.customer_type.
//              sinai.customer.id_customer_type referencia (lógicamente, cross-DB)
//              esta tabla. Es compartida por todas las compañías.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Data;
using CMS.Entities.EInvoice;
using CMS.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/customertype")]
    public class CustomerTypeController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CustomerTypeController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/customertype?search=&onlyActive=&page=&pageSize=
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

            var query = _db.CustomerTypes.AsNoTracking().AsQueryable();

            if (onlyActive)
                query = query.Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Name.ToLower().Contains(s));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => ToDto(a))
                .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        // GET /api/customertype/active  → lista simple activa (para selectores)
        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] string? search, CancellationToken ct = default)
        {
            var query = _db.CustomerTypes.AsNoTracking().Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Name.ToLower().Contains(s));
            }

            var items = await query
                .OrderBy(a => a.SortOrder).ThenBy(a => a.Code)
                .Select(a => ToDto(a))
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET /api/customertype/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var item = await _db.CustomerTypes.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (item == null)
                return NotFound(new { message = $"No existe el tipo de cliente con id {id}." });
            return Ok(ToDto(item));
        }

        // POST /api/customertype
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CustomerTypeDto input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
                return BadRequest(new { message = "El código del tipo de cliente es obligatorio." });
            if (string.IsNullOrWhiteSpace(input.Name))
                return BadRequest(new { message = "El nombre es obligatorio." });

            var code = input.Code.Trim();
            var exists = await _db.CustomerTypes.AnyAsync(a => a.Code == code, ct);
            if (exists)
                return Conflict(new { message = $"Ya existe un tipo de cliente con el código '{code}'." });

            var entity = new CustomerType
            {
                Code = code,
                Name = input.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                IsIssuer = input.IsIssuer,
                IsReceptor = input.IsReceptor,
                SortOrder = input.SortOrder,
                IsActive = input.IsActive
            };

            _db.CustomerTypes.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(ToDto(entity));
        }

        // PUT /api/customertype/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CustomerTypeDto input, CancellationToken ct)
        {
            var entity = await _db.CustomerTypes.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el tipo de cliente con id {id}." });

            if (string.IsNullOrWhiteSpace(input.Name))
                return BadRequest(new { message = "El nombre es obligatorio." });

            var newCode = (input.Code ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(newCode) && newCode != entity.Code)
            {
                var dup = await _db.CustomerTypes.AnyAsync(a => a.Code == newCode && a.Id != id, ct);
                if (dup)
                    return Conflict(new { message = $"Ya existe un tipo de cliente con el código '{newCode}'." });
                entity.Code = newCode;
            }

            entity.Name = input.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            entity.IsIssuer = input.IsIssuer;
            entity.IsReceptor = input.IsReceptor;
            entity.SortOrder = input.SortOrder;
            entity.IsActive = input.IsActive;

            await _db.SaveChangesAsync(ct);
            return Ok(ToDto(entity));
        }

        // DELETE /api/customertype/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _db.CustomerTypes.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el tipo de cliente con id {id}." });

            _db.CustomerTypes.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        private static CustomerTypeDto ToDto(CustomerType a) => new()
        {
            Id = a.Id,
            Code = a.Code,
            Name = a.Name,
            Description = a.Description,
            IsIssuer = a.IsIssuer,
            IsReceptor = a.IsReceptor,
            SortOrder = a.SortOrder,
            IsActive = a.IsActive
        };
    }
}
