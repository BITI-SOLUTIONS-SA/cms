// ================================================================================
// ARCHIVO: CMS.API/Controllers/InventoryTransactionStatusController.cs
// PROPÓSITO: API REST para el catálogo admin.inventory_transaction_status
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-14
// ================================================================================

using CMS.Data;
using CMS.Entities.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/inventory-transaction-status")]
    public class InventoryTransactionStatusController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<InventoryTransactionStatusController> _logger;

        public InventoryTransactionStatusController(AppDbContext db, ILogger<InventoryTransactionStatusController> logger)
        {
            _db     = db;
            _logger = logger;
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "SYSTEM";

        // ================================================================
        // GET /api/inventory-transaction-status
        // ================================================================
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool? isActive = null)
        {
            var q = _db.InventoryTransactionStatuses.AsQueryable();
            if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
            var list = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
            return Ok(list);
        }

        // ================================================================
        // GET /api/inventory-transaction-status/{id}
        // ================================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var entity = await _db.InventoryTransactionStatuses.FindAsync(id);
            if (entity == null) return NotFound();
            return Ok(entity);
        }

        // ================================================================
        // POST /api/inventory-transaction-status
        // ================================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] InventoryTransactionStatus dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            dto.Code = dto.Code.Trim();
            if (await _db.InventoryTransactionStatuses.AnyAsync(x => x.Code == dto.Code))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe." });

            await ShiftOrderAsync(dto.SortOrder);

            dto.CreatedBy  = GetCurrentUser();
            dto.UpdatedBy  = GetCurrentUser();
            dto.CreateDate = DateTime.UtcNow;
            dto.RecordDate = DateTime.UtcNow;
            dto.Rowpointer = Guid.NewGuid();

            _db.InventoryTransactionStatuses.Add(dto);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Estado de transacción '{Code}' creado", dto.Code);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        // ================================================================
        // PUT /api/inventory-transaction-status/{id}
        // ================================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] InventoryTransactionStatus dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = await _db.InventoryTransactionStatuses.FindAsync(id);
            if (entity == null) return NotFound();

            dto.Code = dto.Code.Trim();
            if (await _db.InventoryTransactionStatuses.AnyAsync(x => x.Code == dto.Code && x.Id != id))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe en otro registro." });

            if (entity.SortOrder != dto.SortOrder)
                await ShiftOrderAsync(dto.SortOrder, excludeId: id);

            entity.Code         = dto.Code;
            entity.Name         = dto.Name;
            entity.Description  = dto.Description;
            entity.Icon         = dto.Icon;
            entity.Emoji        = dto.Emoji;
            entity.CssClass     = dto.CssClass;
            entity.IsFinalState = dto.IsFinalState;
            entity.AllowsEdit   = dto.AllowsEdit;
            entity.SortOrder    = dto.SortOrder;
            entity.IsActive     = dto.IsActive;
            entity.UpdatedBy    = GetCurrentUser();
            entity.RecordDate   = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Estado de transacción '{Code}' actualizado", entity.Code);
            return Ok(entity);
        }

        // ================================================================
        // DELETE /api/inventory-transaction-status/{id}  (lógico — desactiva)
        // ================================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.InventoryTransactionStatuses.FindAsync(id);
            if (entity == null) return NotFound();

            entity.IsActive   = false;
            entity.UpdatedBy  = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Estado de transacción '{Code}' desactivado", entity.Code);
            return Ok(new { message = $"Estado '{entity.Name}' desactivado." });
        }

        // ================================================================
        // HELPERS
        // ================================================================

        private async Task ShiftOrderAsync(int newOrder, int? excludeId = null)
        {
            var q = _db.InventoryTransactionStatuses.Where(x => x.SortOrder >= newOrder);
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
            if (await q.AnyAsync())
                await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + 1));
        }
    }
}
