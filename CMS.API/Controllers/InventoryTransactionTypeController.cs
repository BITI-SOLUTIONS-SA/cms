// ================================================================================
// ARCHIVO: CMS.API/Controllers/InventoryTransactionTypeController.cs
// PROPÓSITO: API REST CRUD para el catálogo central admin.inventory_transaction_type
// DESCRIPCIÓN: Gestión de tipos de movimiento de inventario.
//              La tabla vive en la BD central (cms, schema admin) y es compartida
//              por todas las compañías.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-07-02
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
    [Route("api/inventory-transaction-type")]
    public class InventoryTransactionTypeController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<InventoryTransactionTypeController> _logger;

        public InventoryTransactionTypeController(AppDbContext db, ILogger<InventoryTransactionTypeController> logger)
        {
            _db     = db;
            _logger = logger;
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? "SYSTEM";

        // ================================================================
        // GET /api/inventory-transaction-type
        // ================================================================
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? showInInventoryMovements = null)
        {
            var query = _db.InventoryTransactionTypes.AsQueryable();

            if (isActive.HasValue)
                query = query.Where(x => x.IsActive == isActive.Value);

            // ✅ Filtrar por show_in_inventory_movements si se solicita
            if (showInInventoryMovements.HasValue)
                query = query.Where(x => x.ShowInInventoryMovements == showInInventoryMovements.Value);

            var items = await query
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name,
                    x.Description,
                    x.Icon,
                    x.Emoji,
                    x.CssClass,
                    x.IsTransitTransfer,
                    x.SortOrder,
                    x.IsActive,
                    x.ShowInInventoryMovements  // ✅ Incluir en la respuesta
                })
                .ToListAsync();

            return Ok(items);
        }

        // ================================================================
        // GET /api/inventory-transaction-type/{id}
        // ================================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _db.InventoryTransactionTypes.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        // ================================================================
        // POST /api/inventory-transaction-type
        // ================================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] InventoryTransactionType dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            dto.Code = dto.Code.Trim();
            if (await _db.InventoryTransactionTypes.AnyAsync(x => x.Code == dto.Code))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe." });

            // Desplazar sort_order para evitar duplicados
            await ShiftOrderAsync(dto.SortOrder);

            dto.CreatedBy  = GetCurrentUser();
            dto.UpdatedBy  = GetCurrentUser();
            dto.CreateDate = DateTime.UtcNow;
            dto.RecordDate = DateTime.UtcNow;
            dto.RowPointer = Guid.NewGuid();

            _db.InventoryTransactionTypes.Add(dto);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Tipo de movimiento '{Code}' creado", dto.Code);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        // ================================================================
        // PUT /api/inventory-transaction-type/{id}
        // ================================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] InventoryTransactionType dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = await _db.InventoryTransactionTypes.FindAsync(id);
            if (entity == null) return NotFound();

            dto.Code = dto.Code.Trim();
            if (await _db.InventoryTransactionTypes.AnyAsync(x => x.Code == dto.Code && x.Id != id))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe en otro registro." });

            if (entity.SortOrder != dto.SortOrder)
                await ShiftOrderAsync(dto.SortOrder, excludeId: id);

            entity.Code              = dto.Code;
            entity.Name              = dto.Name;
            entity.Description       = dto.Description;
            entity.Icon              = dto.Icon;
            entity.Emoji             = dto.Emoji;
            entity.CssClass          = dto.CssClass;
            entity.IsTransitTransfer = dto.IsTransitTransfer;
            entity.SortOrder         = dto.SortOrder;
            entity.IsActive          = dto.IsActive;
            entity.ShowInInventoryMovements = dto.ShowInInventoryMovements;  // ✅ Actualizar campo
            entity.UpdatedBy         = GetCurrentUser();
            entity.RecordDate        = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Tipo de movimiento '{Code}' actualizado", entity.Code);
            return Ok(entity);
        }

        // ================================================================
        // DELETE /api/inventory-transaction-type/{id}  (lógico — desactiva)
        // ================================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.InventoryTransactionTypes.FindAsync(id);
            if (entity == null) return NotFound();

            entity.IsActive   = false;
            entity.UpdatedBy  = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Tipo de movimiento '{Code}' desactivado", entity.Code);
            return Ok(new { message = $"Tipo de movimiento '{entity.Name}' desactivado." });
        }

        // ================================================================
        // HELPERS
        // ================================================================

        /// <summary>Desplaza +1 el sort_order de todos los registros >= newOrder (excepto excludeId).</summary>
        private async Task ShiftOrderAsync(int newOrder, int? excludeId = null)
        {
            var q = _db.InventoryTransactionTypes.Where(x => x.SortOrder >= newOrder);
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
            if (await q.AnyAsync())
                await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + 1));
        }
    }
}
