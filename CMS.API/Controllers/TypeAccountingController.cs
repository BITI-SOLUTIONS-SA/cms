// ================================================================================
// ARCHIVO: CMS.API/Controllers/TypeAccountingController.cs
// PROPÓSITO: API REST CRUD para el catálogo central admin.type_accounting
// DESCRIPCIÓN: Gestión de los tipos de contabilidad (general, fiscal, corporativo).
//              La tabla vive en la BD central (cms, schema admin) y es compartida
//              por todas las compañías.
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

using CMS.Data;
using CMS.Entities.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/type-accounting")]
    public class TypeAccountingController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<TypeAccountingController> _logger;

        public TypeAccountingController(AppDbContext db, ILogger<TypeAccountingController> logger)
        {
            _db     = db;
            _logger = logger;
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "SYSTEM";

        // ================================================================
        // GET /api/type-accounting
        // ================================================================
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool? isActive = null)
        {
            var query = _db.TypeAccountings.AsQueryable();

            if (isActive.HasValue)
                query = query.Where(x => x.IsActive == isActive.Value);

            var items = await query
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Code)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Description,
                    x.Icon,
                    x.SortOrder,
                    x.IsActive
                })
                .ToListAsync();

            return Ok(items);
        }

        // ================================================================
        // GET /api/type-accounting/{id}
        // ================================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _db.TypeAccountings.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        // ================================================================
        // POST /api/type-accounting
        // ================================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TypeAccountingDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest(new { message = "El código es requerido." });

            var code = dto.Code.Trim().ToLowerInvariant();

            if (await _db.TypeAccountings.AnyAsync(x => x.Code == code))
                return Conflict(new { message = $"El código '{code}' ya existe." });

            await ShiftOrderAsync(dto.SortOrder);

            var entity = new TypeAccounting
            {
                Code        = code,
                Description = dto.Description,
                Icon        = dto.Icon,
                SortOrder   = dto.SortOrder,
                IsActive    = dto.IsActive,
                CreatedBy   = GetCurrentUser(),
                UpdatedBy   = GetCurrentUser(),
                CreateDate  = DateTime.UtcNow,
                RecordDate  = DateTime.UtcNow,
                RowPointer  = Guid.NewGuid()
            };

            _db.TypeAccountings.Add(entity);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Tipo de contabilidad '{Code}' creado por {User}", entity.Code, GetCurrentUser());
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        // ================================================================
        // PUT /api/type-accounting/{id}
        // ================================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] TypeAccountingDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest(new { message = "El código es requerido." });

            var entity = await _db.TypeAccountings.FindAsync(id);
            if (entity == null) return NotFound();

            var code = dto.Code.Trim().ToLowerInvariant();

            if (await _db.TypeAccountings.AnyAsync(x => x.Code == code && x.Id != id))
                return Conflict(new { message = $"El código '{code}' ya existe en otro registro." });

            if (entity.SortOrder != dto.SortOrder)
                await ShiftOrderAsync(dto.SortOrder, excludeId: id);

            entity.Code        = code;
            entity.Description = dto.Description;
            entity.Icon        = dto.Icon;
            entity.SortOrder   = dto.SortOrder;
            entity.IsActive    = dto.IsActive;
            entity.UpdatedBy   = GetCurrentUser();
            entity.RecordDate  = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Tipo de contabilidad '{Code}' actualizado por {User}", entity.Code, GetCurrentUser());
            return Ok(entity);
        }

        // ================================================================
        // DELETE /api/type-accounting/{id}  (lógico — desactiva)
        // ================================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.TypeAccountings.FindAsync(id);
            if (entity == null) return NotFound();

            entity.IsActive   = false;
            entity.UpdatedBy  = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Tipo de contabilidad '{Code}' desactivado por {User}", entity.Code, GetCurrentUser());
            return Ok(new { message = $"Tipo de contabilidad '{entity.Code}' desactivado." });
        }

        // ================================================================
        // HELPERS
        // ================================================================

        private async Task ShiftOrderAsync(int newOrder, int? excludeId = null)
        {
            var q = _db.TypeAccountings.Where(x => x.SortOrder >= newOrder);
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
            if (await q.AnyAsync())
                await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + 1));
        }
    }

    // ================================================================
    // DTO — solo los campos que el cliente puede enviar
    // ================================================================

    public class TypeAccountingDto
    {
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
