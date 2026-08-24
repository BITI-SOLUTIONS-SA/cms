// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentTypeController.cs
// PROPÓSITO: API REST para el catálogo CENTRAL admin.electronic_document_type.
//            Gobierna la visibilidad en la pantalla Emit y todo el comportamiento
//            parametrizable de generación de XML por tipo de documento (Hacienda CR v4.4).
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
    [Route("api/electronicdocumenttype")]
    public class ElectronicDocumentTypeController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IElectronicDocumentTypeCatalogService _catalog;

        public ElectronicDocumentTypeController(AppDbContext db, IElectronicDocumentTypeCatalogService catalog)
        {
            _db = db;
            _catalog = catalog;
        }

        // GET /api/electronicdocumenttype  → todos los tipos activos (cacheado)
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var items = await _catalog.GetAllAsync(ct);
            return Ok(items);
        }

        // GET /api/electronicdocumenttype/byversion/{versionId}  → tipos de una versión específica
        [HttpGet("byversion/{versionId:int}")]
        public async Task<IActionResult> GetByVersion(int versionId, CancellationToken ct)
        {
            var items = await _catalog.GetAllByVersionAsync(versionId, ct);
            return Ok(items);
        }

        // GET /api/electronicdocumenttype/visible  → solo los visibles en el selector de Emit
        [HttpGet("visible")]
        public async Task<IActionResult> GetVisibleForEmit(CancellationToken ct)
        {
            var items = await _catalog.GetVisibleForEmitAsync(ct);
            var result = items.Select(t => new
            {
                t.Code,
                t.ShortCode,
                t.Name,
                t.Description,
                t.RequiresReceptor,
                t.RequiresReference,
                t.SortOrder,
            });
            return Ok(result);
        }

        // GET /api/electronicdocumenttype/{code}  → un tipo por su código fiscal (01..10)
        [HttpGet("{code}")]
        public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
        {
            var item = await _catalog.GetByCodeAsync(code, ct);
            if (item == null)
                return NotFound(new { message = $"No existe el tipo de documento electrónico '{code}'." });
            return Ok(item);
        }

        // PUT /api/electronicdocumenttype/{id}  → actualiza el metadato/banderas de un tipo
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ElectronicDocumentTypeCatalog input, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentTypes.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe el tipo de documento electrónico con id {id}." });

            // El código fiscal (01..10) es inmutable; solo se editan nombre, presentación y banderas.
            entity.ShortCode = input.ShortCode;
            entity.Name = input.Name;
            entity.Description = input.Description;

            entity.XmlRoot = input.XmlRoot;
            entity.XmlNamespaceSegment = input.XmlNamespaceSegment;
            entity.XsdFile = input.XsdFile;

            entity.IsReceiverMessage = input.IsReceiverMessage;
            entity.IsSalesDocument = input.IsSalesDocument;

            entity.ShowInEmit = input.ShowInEmit;
            entity.SortOrder = input.SortOrder;

            entity.RequiresReceptor = input.RequiresReceptor;
            entity.EmisorReduced = input.EmisorReduced;
            entity.AllowCodigoActividadEmisor = input.AllowCodigoActividadEmisor;
            entity.AllowCodigoActividadReceptor = input.AllowCodigoActividadReceptor;

            entity.LineReduced = input.LineReduced;
            entity.AllowLineDiscount = input.AllowLineDiscount;
            entity.AllowImpuestoAsumido = input.AllowImpuestoAsumido;

            entity.AllowResumenClassification = input.AllowResumenClassification;
            entity.AllowTotalDescuentos = input.AllowTotalDescuentos;
            entity.ForceVentaNetaEqualsVenta = input.ForceVentaNetaEqualsVenta;

            entity.ForcedSaleCondition = input.ForcedSaleCondition;
            entity.RequiresReference = input.RequiresReference;
            entity.EmitsOtrosClave = input.EmitsOtrosClave;
            entity.BalanceControlled = input.BalanceControlled;

            entity.PermissionCode = input.PermissionCode;
            entity.IsActive = input.IsActive;
            entity.Notes = input.Notes;

            entity.RecordDate = DateTime.UtcNow;
            entity.UpdatedBy = User?.Identity?.Name ?? "SYSTEM";

            await _db.SaveChangesAsync(ct);

            // Invalidar cache para que el resto de la app vea los cambios de inmediato.
            _catalog.InvalidateCache();

            return Ok(entity);
        }
    }
}
