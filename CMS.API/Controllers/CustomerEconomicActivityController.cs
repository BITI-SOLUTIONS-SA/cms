// ================================================================================
// ARCHIVO: CMS.API/Controllers/CustomerEconomicActivityController.cs
// PROPÓSITO: API REST para las actividades económicas por cliente (BD de la compañía).
//            De aquí el sistema toma la actividad económica al emitir una factura.
// DESCRIPCIÓN: CRUD por cliente + endpoint for-emit (lista activa, predeterminada primero).
//              Cada cliente debe mantener al menos un registro (uno predeterminado).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Data;
using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/customereconomicactivity")]
    public class CustomerEconomicActivityController : ControllerBase
    {
        private readonly ICompanyDbContextFactory _dbContextFactory;
        private readonly AppDbContext _centralDb;
        private readonly ILogger<CustomerEconomicActivityController> _logger;

        public CustomerEconomicActivityController(
            ICompanyDbContextFactory dbContextFactory,
            AppDbContext centralDb,
            ILogger<CustomerEconomicActivityController> logger)
        {
            _dbContextFactory = dbContextFactory;
            _centralDb = centralDb;
            _logger = logger;
        }

        private int GetCurrentCompanyId()
        {
            var claim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(claim, out var id)) return id;
            throw new UnauthorizedAccessException("companyId no encontrado en el token JWT");
        }

        private string GetCurrentUser()
        {
            var raw = User.FindFirstValue("cms_username") ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "SYSTEM";
            return raw.Length > 30 ? raw[..30] : raw;
        }

        /// <summary>
        /// Resuelve un mapa id -> (code, description) desde el catálogo central
        /// cms.admin.electronic_document_economic_activity (relación cross-DB).
        /// </summary>
        private async Task<Dictionary<int, (string Code, string Description)>> ResolveCatalogAsync(IEnumerable<int> ids)
        {
            var distinct = ids.Where(i => i > 0).Distinct().ToList();
            if (distinct.Count == 0)
                return new Dictionary<int, (string, string)>();

            return await _centralDb.ElectronicDocumentEconomicActivities.AsNoTracking()
                .Where(x => distinct.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => (x.Code, x.Description ?? string.Empty));
        }

        /// <summary>Resuelve el (code, description) de un único id desde el catálogo central.</summary>
        private async Task<(string Code, string Description)> ResolveCatalogAsync(int id)
        {
            var map = await ResolveCatalogAsync(new[] { id });
            return map.TryGetValue(id, out var info) ? info : (string.Empty, string.Empty);
        }

        public class CustomerActivityDto
        {
            public int IdElectronicDocumentEconomicActivity { get; set; }
            public bool IsDefault { get; set; }
            public bool IsActive { get; set; } = true;
            public string? Notes { get; set; }
        }

        private static object Map(CustomerEconomicActivity a, string? code, string? description) => new
        {
            id = a.Id,
            idCustomer = a.IdCustomer,
            idElectronicDocumentEconomicActivity = a.IdElectronicDocumentEconomicActivity,
            economicActivityCode = code,
            description,
            isDefault = a.IsDefault,
            isActive = a.IsActive,
            notes = a.Notes,
            createDate = a.CreateDate,
            recordDate = a.RecordDate,
            createdBy = a.CreatedBy,
            updatedBy = a.UpdatedBy
        };

        // GET /api/customereconomicactivity/customer/{customerId}
        [HttpGet("customer/{customerId:int}")]
        public async Task<IActionResult> GetByCustomer(int customerId)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

                var items = await db.CustomerEconomicActivities.AsNoTracking()
                    .Where(a => a.IdCustomer == customerId)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.IdElectronicDocumentEconomicActivity)
                    .ToListAsync();

                // Code + descripción resueltos desde el catálogo central (cross-DB).
                var catalog = await ResolveCatalogAsync(items.Select(a => a.IdElectronicDocumentEconomicActivity));
                return Ok(items.Select(a =>
                {
                    catalog.TryGetValue(a.IdElectronicDocumentEconomicActivity, out var info);
                    return Map(a, info.Code, info.Description);
                }));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error GetByCustomer"); return StatusCode(500, new { message = ex.Message }); }
        }

        // GET /api/customereconomicactivity/for-emit/{customerId}
        // Lista solo actividades ACTIVAS del cliente, predeterminada primero (para el selector de emisión).
        [HttpGet("for-emit/{customerId:int}")]
        public async Task<IActionResult> GetForEmit(int customerId)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

                var rows = await db.CustomerEconomicActivities.AsNoTracking()
                    .Where(a => a.IdCustomer == customerId && a.IsActive)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.IdElectronicDocumentEconomicActivity)
                    .Select(a => new { a.Id, a.IdElectronicDocumentEconomicActivity, a.IsDefault })
                    .ToListAsync();

                var catalog = await ResolveCatalogAsync(rows.Select(a => a.IdElectronicDocumentEconomicActivity));
                var items = rows.Select(a =>
                {
                    catalog.TryGetValue(a.IdElectronicDocumentEconomicActivity, out var info);
                    return new
                    {
                        a.Id,
                        code = info.Code,
                        Description = info.Description,
                        a.IsDefault
                    };
                });
                return Ok(items);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error GetForEmit"); return StatusCode(500, new { message = ex.Message }); }
        }

        // GET /api/customereconomicactivity/for-emit-issuer/{issuerCredentialId}
        // Resuelve el IdCustomer del emisor a partir de su credencial de facturación y
        // devuelve sus actividades económicas activas (predeterminada primero).
        [HttpGet("for-emit-issuer/{issuerCredentialId:int}")]
        public async Task<IActionResult> GetForEmitIssuer(int issuerCredentialId)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

                var issuerCustomerId = await db.CustomerBillingCredentials
                    .Where(c => c.Id == issuerCredentialId && c.IsIssuer)
                    .Select(c => c.IdCustomer)
                    .FirstOrDefaultAsync();

                if (issuerCustomerId == null || issuerCustomerId == 0)
                    return Ok(Array.Empty<object>());

                var rows = await db.CustomerEconomicActivities.AsNoTracking()
                    .Where(a => a.IdCustomer == issuerCustomerId.Value && a.IsActive)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.IdElectronicDocumentEconomicActivity)
                    .Select(a => new { a.Id, a.IdElectronicDocumentEconomicActivity, a.IsDefault })
                    .ToListAsync();

                var catalog = await ResolveCatalogAsync(rows.Select(a => a.IdElectronicDocumentEconomicActivity));
                var items = rows.Select(a =>
                {
                    catalog.TryGetValue(a.IdElectronicDocumentEconomicActivity, out var info);
                    return new
                    {
                        a.Id,
                        code = info.Code,
                        Description = info.Description,
                        a.IsDefault
                    };
                });
                return Ok(items);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error GetForEmitIssuer"); return StatusCode(500, new { message = ex.Message }); }
        }

        // POST /api/customereconomicactivity/customer/{customerId}
        [HttpPost("customer/{customerId:int}")]
        public async Task<IActionResult> Create(int customerId, [FromBody] CustomerActivityDto input)
        {
            try
            {
                if (input.IdElectronicDocumentEconomicActivity <= 0)
                    return BadRequest(new { message = "La actividad económica es obligatoria." });

                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

                var actId = input.IdElectronicDocumentEconomicActivity;

                // Validar que exista en el catálogo central.
                var info = await ResolveCatalogAsync(actId);
                if (string.IsNullOrEmpty(info.Code))
                    return BadRequest(new { message = "La actividad económica no existe en el catálogo central." });

                var dup = await db.CustomerEconomicActivities
                    .AnyAsync(a => a.IdCustomer == customerId && a.IdElectronicDocumentEconomicActivity == actId);
                if (dup)
                    return Conflict(new { message = $"El cliente ya tiene registrada la actividad '{info.Code}'." });

                var hasAny = await db.CustomerEconomicActivities.AnyAsync(a => a.IdCustomer == customerId);
                // Si es el primer registro, forzarlo como predeterminado.
                var isDefault = input.IsDefault || !hasAny;

                if (isDefault)
                    await ClearDefaultAsync(db, customerId);

                var entity = new CustomerEconomicActivity
                {
                    IdCustomer = customerId,
                    IdElectronicDocumentEconomicActivity = actId,
                    IsDefault = isDefault,
                    IsActive = input.IsActive,
                    // La columna notes es NOT NULL en la BD; usar texto por defecto si no se indica.
                    Notes = string.IsNullOrWhiteSpace(input.Notes) ? "Actividad Principal" : input.Notes,
                    CreatedBy = user,
                    UpdatedBy = user
                };

                db.CustomerEconomicActivities.Add(entity);
                await db.SaveChangesAsync();
                return Ok(Map(entity, info.Code, info.Description));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error Create"); return StatusCode(500, new { message = ex.Message }); }
        }

        // PUT /api/customereconomicactivity/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CustomerActivityDto input)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

                var entity = await db.CustomerEconomicActivities.FirstOrDefaultAsync(a => a.Id == id);
                if (entity == null)
                    return NotFound(new { message = $"No existe la actividad económica con id {id}." });

                var newActId = input.IdElectronicDocumentEconomicActivity;
                if (newActId > 0 && newActId != entity.IdElectronicDocumentEconomicActivity)
                {
                    var info2 = await ResolveCatalogAsync(newActId);
                    if (string.IsNullOrEmpty(info2.Code))
                        return BadRequest(new { message = "La actividad económica no existe en el catálogo central." });

                    var dup = await db.CustomerEconomicActivities
                        .AnyAsync(a => a.IdCustomer == entity.IdCustomer && a.IdElectronicDocumentEconomicActivity == newActId && a.Id != id);
                    if (dup)
                        return Conflict(new { message = $"El cliente ya tiene registrada la actividad '{info2.Code}'." });
                    entity.IdElectronicDocumentEconomicActivity = newActId;
                }

                entity.IsActive = input.IsActive;
                // La columna notes es NOT NULL en la BD; usar texto por defecto si no se indica.
                entity.Notes = string.IsNullOrWhiteSpace(input.Notes) ? "Actividad Principal" : input.Notes;

                if (input.IsDefault && !entity.IsDefault)
                {
                    await ClearDefaultAsync(db, entity.IdCustomer);
                    entity.IsDefault = true;
                }

                await db.SaveChangesAsync();
                var info = await ResolveCatalogAsync(entity.IdElectronicDocumentEconomicActivity);
                return Ok(Map(entity, info.Code, info.Description));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error Update"); return StatusCode(500, new { message = ex.Message }); }
        }

        // PUT /api/customereconomicactivity/{id}/set-default
        [HttpPut("{id:int}/set-default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

                var entity = await db.CustomerEconomicActivities.FirstOrDefaultAsync(a => a.Id == id);
                if (entity == null)
                    return NotFound(new { message = $"No existe la actividad económica con id {id}." });

                await ClearDefaultAsync(db, entity.IdCustomer);
                entity.IsDefault = true;
                entity.IsActive = true;
                await db.SaveChangesAsync();
                var info = await ResolveCatalogAsync(entity.IdElectronicDocumentEconomicActivity);
                return Ok(Map(entity, info.Code, info.Description));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error SetDefault"); return StatusCode(500, new { message = ex.Message }); }
        }

        // DELETE /api/customereconomicactivity/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

                var entity = await db.CustomerEconomicActivities.FirstOrDefaultAsync(a => a.Id == id);
                if (entity == null)
                    return NotFound(new { message = $"No existe la actividad económica con id {id}." });

                // No permitir eliminar la única/predeterminada si es el último registro.
                var count = await db.CustomerEconomicActivities.CountAsync(a => a.IdCustomer == entity.IdCustomer);
                if (count <= 1)
                    return BadRequest(new { message = "El cliente debe mantener al menos una actividad económica." });

                var wasDefault = entity.IsDefault;
                db.CustomerEconomicActivities.Remove(entity);
                await db.SaveChangesAsync();

                // Si se eliminó la predeterminada, promover otra activa como predeterminada.
                if (wasDefault)
                {
                    var next = await db.CustomerEconomicActivities
                        .Where(a => a.IdCustomer == entity.IdCustomer)
                        .OrderByDescending(a => a.IsActive)
                        .ThenBy(a => a.IdElectronicDocumentEconomicActivity)
                        .FirstOrDefaultAsync();
                    if (next != null)
                    {
                        next.IsDefault = true;
                        await db.SaveChangesAsync();
                    }
                }

                return Ok(new { message = "Actividad económica eliminada." });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error Delete"); return StatusCode(500, new { message = ex.Message }); }
        }

        private static async Task ClearDefaultAsync(CMS.Data.CompanyDbContext db, int customerId)
        {
            var currentDefaults = await db.CustomerEconomicActivities
                .Where(a => a.IdCustomer == customerId && a.IsDefault)
                .ToListAsync();
            foreach (var d in currentDefaults)
                d.IsDefault = false;
        }
    }
}
