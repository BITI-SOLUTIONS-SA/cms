// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentCabysController.cs
// PROPÓSITO: API REST CRUD para el catálogo CENTRAL admin.electronic_document_cabys
//            (códigos CAByS para facturación electrónica con relación a tarifa y
//            tipo de impuesto de Hacienda CR v4.4).
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
    [Route("api/electronicdocumentcabys")]
    public class ElectronicDocumentCabysController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ElectronicDocumentCabysController(AppDbContext db)
        {
            _db = db;
        }

        public class ElectronicDocumentCabysDto
        {
            public string Code { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int IdElectronicDocumentTaxRate { get; set; } = 8;
            public int IdElectronicDocumentTaxType { get; set; } = 1;
            public bool IsActive { get; set; } = true;
        }

        // GET /api/electronicdocumentcabys?search=&onlyActive=&page=&pageSize=
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

            var query = _db.ElectronicDocumentCabys.AsNoTracking().AsQueryable();

            if (onlyActive)
                query = query.Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Description.ToLower().Contains(s));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(a => a.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.Code,
                    a.Description,
                    a.IdElectronicDocumentTaxRate,
                    TaxRateCode = a.TaxRate != null ? a.TaxRate.Code : null,
                    TaxRateName = a.TaxRate != null ? a.TaxRate.Name : null,
                    a.IdElectronicDocumentTaxType,
                    TaxTypeCode = a.TaxType != null ? a.TaxType.Code : null,
                    TaxTypeDescription = a.TaxType != null ? a.TaxType.Description : null,
                    a.IsActive
                })
                .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        // GET /api/electronicdocumentcabys/active  → lista simple activa (para selectores)
        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] string? search, CancellationToken ct = default)
        {
            var query = _db.ElectronicDocumentCabys.AsNoTracking().Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Description.ToLower().Contains(s));
            }

            var items = await query
                .OrderBy(a => a.Code)
                .Take(100)
                .Select(a => new { a.Id, a.Code, a.Description, a.IdElectronicDocumentTaxRate, a.IdElectronicDocumentTaxType })
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET /api/electronicdocumentcabys/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var item = await _db.ElectronicDocumentCabys.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (item == null)
                return NotFound(new { message = $"No existe el código CAByS con id {id}." });
            return Ok(item);
        }

        // GET /api/electronicdocumentcabys/by-code/{code}
        // Devuelve un código CAByS por su código exacto con la tarifa de IVA y tipo de
        // impuesto resueltos (usado para autocompletar la línea de facturación).
        [HttpGet("by-code/{code}")]
        public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { message = "El código CAByS es obligatorio." });

            var c = code.Trim();
            var item = await _db.ElectronicDocumentCabys.AsNoTracking()
                .Where(a => a.Code == c && a.IsActive)
                .Select(a => new
                {
                    a.Id,
                    a.Code,
                    a.Description,
                    a.IdElectronicDocumentTaxRate,
                    TaxRateCode = a.TaxRate != null ? a.TaxRate.Code : null,
                    TaxRatePercent = a.TaxRate != null ? a.TaxRate.RatePercent : (decimal?)null,
                    a.IdElectronicDocumentTaxType,
                    TaxTypeCode = a.TaxType != null ? a.TaxType.Code : null,
                    a.IsActive
                })
                .FirstOrDefaultAsync(ct);

            if (item == null)
                return NotFound(new { message = $"No existe un código CAByS activo '{c}'." });
            return Ok(item);
        }

        // GET /api/electronicdocumentcabys/by-code/{code}/allowed-taxes
        // Devuelve los TIPOS DE IMPUESTO permitidos para un código CAByS según la tabla de
        // relación admin.electronic_document_cabys_tax. Si el CAByS no tiene reglas
        // configuradas, se devuelve una lista vacía y la UI interpreta "todos permitidos".
        [HttpGet("by-code/{code}/allowed-taxes")]
        public async Task<IActionResult> GetAllowedTaxes(string code, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { message = "El código CAByS es obligatorio." });

            var c = code.Trim();
            var cabysId = await _db.ElectronicDocumentCabys.AsNoTracking()
                .Where(a => a.Code == c && a.IsActive)
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync(ct);

            if (cabysId == null)
                return Ok(Array.Empty<object>());

            var items = await _db.ElectronicDocumentCabysTaxes.AsNoTracking()
                .Where(r => r.IdElectronicDocumentCabys == cabysId && r.IsActive && r.TaxType != null && r.TaxType.IsActive)
                .OrderBy(r => r.TaxType!.SortOrder).ThenBy(r => r.TaxType!.Code)
                .Select(r => new { r.IdElectronicDocumentTaxType, Code = r.TaxType!.Code, Description = r.TaxType!.Description })
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET /api/electronicdocumentcabys/by-code/{code}/allowed-discounts
        // Devuelve las NATURALEZAS DE DESCUENTO permitidas para un código CAByS según la
        // tabla admin.electronic_document_cabys_discount. Lista vacía = todas permitidas.
        [HttpGet("by-code/{code}/allowed-discounts")]
        public async Task<IActionResult> GetAllowedDiscounts(string code, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { message = "El código CAByS es obligatorio." });

            var c = code.Trim();
            var cabysId = await _db.ElectronicDocumentCabys.AsNoTracking()
                .Where(a => a.Code == c && a.IsActive)
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync(ct);

            if (cabysId == null)
                return Ok(Array.Empty<object>());

            var items = await _db.ElectronicDocumentCabysDiscounts.AsNoTracking()
                .Where(r => r.IdElectronicDocumentCabys == cabysId && r.IsActive && r.Discount != null)
                .Select(r => new { r.IdElectronicDocumentDiscount, Code = r.Discount!.Code, Description = r.Discount!.Description })
                .ToListAsync(ct);

            return Ok(items);
        }

        // POST /api/electronicdocumentcabys
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ElectronicDocumentCabysDto input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
                return BadRequest(new { message = "El código CAByS es obligatorio." });
            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var code = input.Code.Trim();
            var exists = await _db.ElectronicDocumentCabys.AnyAsync(a => a.Code == code, ct);
            if (exists)
                return Conflict(new { message = $"Ya existe un código CAByS '{code}'." });

            var entity = new ElectronicDocumentCabys
            {
                Code = code,
                Description = input.Description.Trim(),
                IdElectronicDocumentTaxRate = input.IdElectronicDocumentTaxRate > 0 ? input.IdElectronicDocumentTaxRate : 8,
                IdElectronicDocumentTaxType = input.IdElectronicDocumentTaxType > 0 ? input.IdElectronicDocumentTaxType : 1,
                IsActive = input.IsActive
            };

            _db.ElectronicDocumentCabys.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // PUT /api/electronicdocumentcabys/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ElectronicDocumentCabysDto input, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentCabys.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el código CAByS con id {id}." });

            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var newCode = (input.Code ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(newCode) && newCode != entity.Code)
            {
                var dup = await _db.ElectronicDocumentCabys.AnyAsync(a => a.Code == newCode && a.Id != id, ct);
                if (dup)
                    return Conflict(new { message = $"Ya existe un código CAByS '{newCode}'." });
                entity.Code = newCode;
            }

            entity.Description = input.Description.Trim();
            entity.IdElectronicDocumentTaxRate = input.IdElectronicDocumentTaxRate > 0 ? input.IdElectronicDocumentTaxRate : 8;
            entity.IdElectronicDocumentTaxType = input.IdElectronicDocumentTaxType > 0 ? input.IdElectronicDocumentTaxType : 1;
            entity.IsActive = input.IsActive;

            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // DELETE /api/electronicdocumentcabys/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentCabys.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el código CAByS con id {id}." });

            _db.ElectronicDocumentCabys.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(new { message = "Código CAByS eliminado." });
        }
    }
}
