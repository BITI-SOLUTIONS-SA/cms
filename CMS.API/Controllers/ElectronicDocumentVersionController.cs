// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentVersionController.cs
// PROPÓSITO: API REST para el catálogo CENTRAL admin.electronic_document_version.
//            Gestiona las versiones del esquema de Hacienda CR y cuál es la vigente.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Data;
using CMS.Data.Services.EInvoice;
using CMS.Entities.EInvoice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/electronicdocumentversion")]
    public class ElectronicDocumentVersionController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IElectronicDocumentTypeCatalogService _catalog;

        public ElectronicDocumentVersionController(AppDbContext db, IElectronicDocumentTypeCatalogService catalog)
        {
            _db = db;
            _catalog = catalog;
        }

        // GET /api/electronicdocumentversion  → todas las versiones activas
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var items = await _catalog.GetVersionsAsync(ct);
            return Ok(items);
        }

        // GET /api/electronicdocumentversion/current  → la versión vigente
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent(CancellationToken ct)
        {
            var item = await _catalog.GetCurrentVersionAsync(ct);
            if (item == null)
                return NotFound(new { message = "No hay una versión vigente configurada." });
            return Ok(item);
        }

        // POST /api/electronicdocumentversion  → crea una nueva versión
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ElectronicDocumentVersion input, CancellationToken ct)
        {
            var entity = new ElectronicDocumentVersion
            {
                Code = input.Code,
                Name = input.Name,
                Description = input.Description,
                EffectiveDate = input.EffectiveDate,
                SortOrder = input.SortOrder,
                IsActive = input.IsActive,
                Notes = input.Notes,
                IsCurrent = false, // La marca de vigente se hace por el endpoint dedicado
                CreatedBy = User?.Identity?.Name ?? "SYSTEM",
                UpdatedBy = User?.Identity?.Name ?? "SYSTEM",
            };
            _db.ElectronicDocumentVersions.Add(entity);
            await _db.SaveChangesAsync(ct);
            _catalog.InvalidateCache();
            return Ok(entity);
        }

        // PUT /api/electronicdocumentversion/{id}  → actualiza datos de una versión
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ElectronicDocumentVersion input, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentVersions.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe la versión con id {id}." });

            entity.Code = input.Code;
            entity.Name = input.Name;
            entity.Description = input.Description;
            entity.EffectiveDate = input.EffectiveDate;
            entity.SortOrder = input.SortOrder;
            entity.IsActive = input.IsActive;
            entity.Notes = input.Notes;
            entity.RecordDate = DateTime.UtcNow;
            entity.UpdatedBy = User?.Identity?.Name ?? "SYSTEM";

            await _db.SaveChangesAsync(ct);
            _catalog.InvalidateCache();
            return Ok(entity);
        }

        // PUT /api/electronicdocumentversion/{id}/current  → marca esta versión como la vigente
        // Solo una versión puede ser vigente: se apaga is_current en las demás.
        [HttpPut("{id:int}/current")]
        public async Task<IActionResult> SetCurrent(int id, CancellationToken ct)
        {
            var target = await _db.ElectronicDocumentVersions.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (target == null)
                return NotFound(new { message = $"No existe la versión con id {id}." });

            var currents = await _db.ElectronicDocumentVersions
                .Where(v => v.IsCurrent && v.Id != id)
                .ToListAsync(ct);

            // Apagar la anterior vigente PRIMERO y guardar, para no violar el índice único parcial.
            foreach (var c in currents)
            {
                c.IsCurrent = false;
                c.UpdatedBy = User?.Identity?.Name ?? "SYSTEM";
            }
            if (currents.Count > 0)
                await _db.SaveChangesAsync(ct);

            target.IsCurrent = true;
            target.UpdatedBy = User?.Identity?.Name ?? "SYSTEM";
            await _db.SaveChangesAsync(ct);

            _catalog.InvalidateCache();
            return Ok(target);
        }
    }
}
