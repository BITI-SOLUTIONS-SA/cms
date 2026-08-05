// ================================================================================
// ARCHIVO: CMS.API/Controllers/JournalEntryStatusController.cs
// PROPÓSITO: API REST CRUD para el catálogo central admin.journal_entry_status
// DESCRIPCIÓN: Gestión de los estados de asiento de diario
//              (Draft, Posted, Reversed, Cancelled).
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
    [Route("api/journal-entry-status")]
    public class JournalEntryStatusController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<JournalEntryStatusController> _logger;

        public JournalEntryStatusController(AppDbContext db, ILogger<JournalEntryStatusController> logger)
        {
            _db     = db;
            _logger = logger;
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "SYSTEM";

        // ================================================================
        // GET /api/journal-entry-status
        // ================================================================
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool? isActive = null)
        {
            var query = _db.JournalEntryStatuses.AsQueryable();

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
                    x.Color,
                    x.SortOrder,
                    x.IsActive
                })
                .ToListAsync();

            return Ok(items);
        }

        // ================================================================
        // GET /api/journal-entry-status/{id}
        // ================================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _db.JournalEntryStatuses.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        // ================================================================
        // POST /api/journal-entry-status
        // ================================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] JournalEntryStatusDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest(new { message = "El código es requerido." });

            var code = dto.Code.Trim();

            if (await _db.JournalEntryStatuses.AnyAsync(x => x.Code == code))
                return Conflict(new { message = $"El código '{code}' ya existe." });

            await ShiftOrderAsync(dto.SortOrder);

            var entity = new JournalEntryStatus
            {
                Code        = code,
                Description = dto.Description,
                Icon        = dto.Icon,
                Color       = dto.Color,
                SortOrder   = dto.SortOrder,
                IsActive    = dto.IsActive,
                CreatedBy   = GetCurrentUser(),
                UpdatedBy   = GetCurrentUser(),
                CreateDate  = DateTime.UtcNow,
                RecordDate  = DateTime.UtcNow,
                RowPointer  = Guid.NewGuid()
            };

            _db.JournalEntryStatuses.Add(entity);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Estado de asiento '{Code}' creado por {User}", entity.Code, GetCurrentUser());
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        // ================================================================
        // PUT /api/journal-entry-status/{id}
        // ================================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] JournalEntryStatusDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest(new { message = "El código es requerido." });

            var entity = await _db.JournalEntryStatuses.FindAsync(id);
            if (entity == null) return NotFound();

            var code = dto.Code.Trim();

            if (await _db.JournalEntryStatuses.AnyAsync(x => x.Code == code && x.Id != id))
                return Conflict(new { message = $"El código '{code}' ya existe en otro registro." });

            if (entity.SortOrder != dto.SortOrder)
                await ShiftOrderAsync(dto.SortOrder, excludeId: id);

            entity.Code        = code;
            entity.Description = dto.Description;
            entity.Icon        = dto.Icon;
            entity.Color       = dto.Color;
            entity.SortOrder   = dto.SortOrder;
            entity.IsActive    = dto.IsActive;
            entity.UpdatedBy   = GetCurrentUser();
            entity.RecordDate  = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Estado de asiento '{Code}' actualizado por {User}", entity.Code, GetCurrentUser());
            return Ok(entity);
        }

        // ================================================================
        // DELETE /api/journal-entry-status/{id}  (lógico — desactiva)
        // ================================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.JournalEntryStatuses.FindAsync(id);
            if (entity == null) return NotFound();

            entity.IsActive   = false;
            entity.UpdatedBy  = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Estado de asiento '{Code}' desactivado por {User}", entity.Code, GetCurrentUser());
            return Ok(new { message = $"Estado de asiento '{entity.Code}' desactivado." });
        }

        // ================================================================
        // HELPERS
        // ================================================================
        private async Task ShiftOrderAsync(int newOrder, int? excludeId = null)
        {
            var q = _db.JournalEntryStatuses.Where(x => x.SortOrder >= newOrder);
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
            if (await q.AnyAsync())
                await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + 1));
        }
    }

    // ================================================================
    // DTO
    // ================================================================
    public class JournalEntryStatusDto
    {
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
