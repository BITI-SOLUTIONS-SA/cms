// ================================================================================
// ARCHIVO: CMS.API/Controllers/VendorController.cs
// PROPÓSITO: API REST para el mantenimiento de proveedores (vendors) y sus
//            actividades económicas. Vive en la BD operacional de cada compañía.
// DESCRIPCIÓN: CRUD paginado + gestión de actividades económicas, satisfaciendo
//              el contrato consumido por CMS.UI/wwwroot/js/vendors.js.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Security.Claims;
using CMS.Data;
using CMS.Data.Services;
using CMS.Entities.Operational;
using CMS.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class VendorController : ControllerBase
    {
        private readonly ICompanyDbContextFactory _factory;
        private readonly AppDbContext _centralDb;
        private readonly ILogger<VendorController> _logger;

        public VendorController(
            ICompanyDbContextFactory factory,
            AppDbContext centralDb,
            ILogger<VendorController> logger)
        {
            _factory = factory;
            _centralDb = centralDb;
            _logger = logger;
        }

        private int GetCompanyId()
        {
            var claim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(claim, out var id)) return id;
            throw new UnauthorizedAccessException("companyId no encontrado en el token JWT");
        }

        private string CurrentUser()
        {
            var raw = User.FindFirstValue("cms_username") ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "SYSTEM";
            return raw.Length > 30 ? raw[..30] : raw;
        }

        // El tipo de identificación se maneja como número (1..6) en la UI y como
        // código Hacienda ("01".."06") en la BD.
        private static string? NumberToIdentificationType(int? n) =>
            n is > 0 ? n.Value.ToString("D2") : null;

        private static int? IdentificationTypeToNumber(string? code) =>
            int.TryParse(code, out var n) && n > 0 ? n : null;

        private static VendorDto MapToDto(Vendor v) => new()
        {
            Id = v.Id,
            Code = v.Code,
            Name = v.Name,
            CommercialName = v.CommercialName,
            IdElectronicDocumentIdentificationType = IdentificationTypeToNumber(v.IdentificationType),
            Identification = v.Identification,
            EconomicActivity = v.EconomicActivity,
            VendorType = v.VendorType,
            Email = v.Email,
            PhoneCode = v.PhoneCode,
            Phone = v.Phone,
            Currency = v.Currency,
            CreditDays = v.CreditDays,
            CreditLimit = v.CreditLimit,
            Notes = v.Notes,
            IsActive = v.IsActive
        };

        /// <summary>
        /// Resuelve el mapa id -> (code, description) desde el catálogo central
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

        private async Task<(string Code, string Description)> ResolveCatalogAsync(int id)
        {
            var map = await ResolveCatalogAsync(new[] { id });
            return map.TryGetValue(id, out var info) ? info : (string.Empty, string.Empty);
        }

        private async Task<List<VendorEconomicActivityDto>> GetActivitiesAsync(CompanyDbContext db, int vendorId)
        {
            var rows = await db.VendorEconomicActivities.AsNoTracking()
                .Where(a => a.IdVendor == vendorId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.IdElectronicDocumentEconomicActivity)
                .ToListAsync();

            var catalog = await ResolveCatalogAsync(rows.Select(a => a.IdElectronicDocumentEconomicActivity));
            return rows.Select(a =>
            {
                catalog.TryGetValue(a.IdElectronicDocumentEconomicActivity, out var info);
                return new VendorEconomicActivityDto
                {
                    Id = a.Id,
                    IdVendor = a.IdVendor,
                    IdElectronicDocumentEconomicActivity = a.IdElectronicDocumentEconomicActivity,
                    EconomicActivityCode = info.Code,
                    Description = info.Description,
                    IsDefault = a.IsDefault,
                    IsActive = a.IsActive
                };
            }).ToList();
        }

        // GET /api/Vendor?searchTerm=&includeInactive=&page=&pageSize=
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? searchTerm = null,
            [FromQuery] bool includeInactive = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 25;

                var companyId = GetCompanyId();
                using var db = await _factory.CreateDbContextAsync(companyId);

                var query = db.Vendors.AsNoTracking().AsQueryable();

                if (!includeInactive)
                    query = query.Where(v => v.IsActive);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.Trim().ToLower();
                    query = query.Where(v =>
                        v.Code.ToLower().Contains(term) ||
                        v.Name.ToLower().Contains(term) ||
                        (v.CommercialName != null && v.CommercialName.ToLower().Contains(term)) ||
                        (v.Identification != null && v.Identification.ToLower().Contains(term)) ||
                        (v.Email != null && v.Email.ToLower().Contains(term)));
                }

                var total = await query.CountAsync();
                var items = await query
                    .OrderBy(v => v.Code)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Ok(new { total, items = items.Select(MapToDto) });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar vendors");
                return StatusCode(500, new { message = "Error al listar vendors", error = ex.Message });
            }
        }

        // GET /api/Vendor/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                using var db = await _factory.CreateDbContextAsync(companyId);

                var vendor = await db.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
                if (vendor == null)
                    return NotFound(new { message = $"Vendor ID {id} no encontrado" });

                var dto = MapToDto(vendor);
                dto.EconomicActivities = await GetActivitiesAsync(db, id);
                return Ok(dto);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener vendor {Id}", id);
                return StatusCode(500, new { message = "Error al obtener vendor", error = ex.Message });
            }
        }

        // POST /api/Vendor
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] VendorDto input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input.Code))
                    return BadRequest(new { message = "El código es obligatorio." });
                if (string.IsNullOrWhiteSpace(input.Name))
                    return BadRequest(new { message = "El nombre es obligatorio." });

                var companyId = GetCompanyId();
                var user = CurrentUser();
                using var db = await _factory.CreateDbContextAsync(companyId);

                var code = input.Code.Trim();
                if (await db.Vendors.AnyAsync(v => v.Code == code))
                    return Conflict(new { message = $"Ya existe un vendor con el código '{code}'." });

                var entity = new Vendor
                {
                    Code = code,
                    Name = input.Name.Trim(),
                    CommercialName = input.CommercialName,
                    IdentificationType = NumberToIdentificationType(input.IdElectronicDocumentIdentificationType),
                    Identification = input.Identification,
                    EconomicActivity = input.EconomicActivity,
                    VendorType = string.IsNullOrWhiteSpace(input.VendorType) ? "Both" : input.VendorType,
                    Email = input.Email,
                    PhoneCode = input.PhoneCode,
                    Phone = input.Phone,
                    Currency = string.IsNullOrWhiteSpace(input.Currency) ? "CRC" : input.Currency,
                    CreditDays = input.CreditDays,
                    CreditLimit = input.CreditLimit,
                    Notes = input.Notes,
                    IsActive = input.IsActive,
                    CreatedBy = user,
                    UpdatedBy = user
                };

                db.Vendors.Add(entity);
                await db.SaveChangesAsync();
                return Ok(MapToDto(entity));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear vendor");
                return StatusCode(500, new { message = "Error al crear vendor", error = ex.Message });
            }
        }

        // PUT /api/Vendor/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] VendorDto input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input.Code))
                    return BadRequest(new { message = "El código es obligatorio." });
                if (string.IsNullOrWhiteSpace(input.Name))
                    return BadRequest(new { message = "El nombre es obligatorio." });

                var companyId = GetCompanyId();
                var user = CurrentUser();
                using var db = await _factory.CreateDbContextAsync(companyId);

                var entity = await db.Vendors.FirstOrDefaultAsync(v => v.Id == id);
                if (entity == null)
                    return NotFound(new { message = $"Vendor ID {id} no encontrado" });

                var code = input.Code.Trim();
                if (await db.Vendors.AnyAsync(v => v.Code == code && v.Id != id))
                    return Conflict(new { message = $"Ya existe un vendor con el código '{code}'." });

                entity.Code = code;
                entity.Name = input.Name.Trim();
                entity.CommercialName = input.CommercialName;
                entity.IdentificationType = NumberToIdentificationType(input.IdElectronicDocumentIdentificationType);
                entity.Identification = input.Identification;
                entity.EconomicActivity = input.EconomicActivity;
                entity.VendorType = string.IsNullOrWhiteSpace(input.VendorType) ? "Both" : input.VendorType;
                entity.Email = input.Email;
                entity.PhoneCode = input.PhoneCode;
                entity.Phone = input.Phone;
                entity.Currency = string.IsNullOrWhiteSpace(input.Currency) ? "CRC" : input.Currency;
                entity.CreditDays = input.CreditDays;
                entity.CreditLimit = input.CreditLimit;
                entity.Notes = input.Notes;
                entity.IsActive = input.IsActive;
                entity.UpdatedBy = user;

                await db.SaveChangesAsync();
                return Ok(MapToDto(entity));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar vendor {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar vendor", error = ex.Message });
            }
        }

        // DELETE /api/Vendor/{id} (baja lógica)
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Deactivate(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var user = CurrentUser();
                using var db = await _factory.CreateDbContextAsync(companyId);

                var entity = await db.Vendors.FirstOrDefaultAsync(v => v.Id == id);
                if (entity == null)
                    return NotFound(new { message = $"Vendor ID {id} no encontrado" });

                entity.IsActive = false;
                entity.UpdatedBy = user;
                await db.SaveChangesAsync();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar vendor {Id}", id);
                return StatusCode(500, new { message = "Error al desactivar vendor", error = ex.Message });
            }
        }

        // ===== ACTIVIDADES ECONÓMICAS DEL VENDOR =====

        // GET /api/Vendor/{id}/economic-activities
        [HttpGet("{id:int}/economic-activities")]
        public async Task<IActionResult> GetEconomicActivities(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                using var db = await _factory.CreateDbContextAsync(companyId);
                return Ok(await GetActivitiesAsync(db, id));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar actividades del vendor {Id}", id);
                return StatusCode(500, new { message = "Error al listar actividades", error = ex.Message });
            }
        }

        // POST /api/Vendor/{id}/economic-activities
        [HttpPost("{id:int}/economic-activities")]
        public async Task<IActionResult> AddEconomicActivity(int id, [FromBody] VendorEconomicActivityInputDto input)
        {
            try
            {
                if (input.IdElectronicDocumentEconomicActivity <= 0)
                    return BadRequest(new { message = "La actividad económica es obligatoria." });

                var companyId = GetCompanyId();
                var user = CurrentUser();
                using var db = await _factory.CreateDbContextAsync(companyId);

                if (!await db.Vendors.AnyAsync(v => v.Id == id))
                    return NotFound(new { message = $"Vendor ID {id} no encontrado" });

                var actId = input.IdElectronicDocumentEconomicActivity;
                var info = await ResolveCatalogAsync(actId);
                if (string.IsNullOrEmpty(info.Code))
                    return BadRequest(new { message = "La actividad económica no existe en el catálogo central." });

                var dup = await db.VendorEconomicActivities
                    .AnyAsync(a => a.IdVendor == id && a.IdElectronicDocumentEconomicActivity == actId);
                if (dup)
                    return Conflict(new { message = $"El vendor ya tiene registrada la actividad '{info.Code}'." });

                var hasAny = await db.VendorEconomicActivities.AnyAsync(a => a.IdVendor == id);
                var isDefault = !hasAny;

                var entity = new VendorEconomicActivity
                {
                    IdVendor = id,
                    IdElectronicDocumentEconomicActivity = actId,
                    IsDefault = isDefault,
                    IsActive = true,
                    Notes = "Actividad Principal",
                    CreatedBy = user,
                    UpdatedBy = user
                };

                db.VendorEconomicActivities.Add(entity);
                await db.SaveChangesAsync();

                return Ok(new VendorEconomicActivityDto
                {
                    Id = entity.Id,
                    IdVendor = entity.IdVendor,
                    IdElectronicDocumentEconomicActivity = entity.IdElectronicDocumentEconomicActivity,
                    EconomicActivityCode = info.Code,
                    Description = info.Description,
                    IsDefault = entity.IsDefault,
                    IsActive = entity.IsActive
                });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar actividad al vendor {Id}", id);
                return StatusCode(500, new { message = "Error al agregar actividad", error = ex.Message });
            }
        }

        // PUT /api/Vendor/{id}/economic-activities/{activityId}/default
        [HttpPut("{id:int}/economic-activities/{activityId:int}/default")]
        public async Task<IActionResult> SetDefaultActivity(int id, int activityId)
        {
            try
            {
                var companyId = GetCompanyId();
                var user = CurrentUser();
                using var db = await _factory.CreateDbContextAsync(companyId);

                var entity = await db.VendorEconomicActivities
                    .FirstOrDefaultAsync(a => a.Id == activityId && a.IdVendor == id);
                if (entity == null)
                    return NotFound(new { message = $"No existe la actividad {activityId} para el vendor {id}." });

                var current = await db.VendorEconomicActivities
                    .Where(a => a.IdVendor == id && a.IsDefault)
                    .ToListAsync();
                foreach (var c in current)
                {
                    c.IsDefault = false;
                    c.UpdatedBy = user;
                }

                entity.IsDefault = true;
                entity.IsActive = true;
                entity.UpdatedBy = user;
                await db.SaveChangesAsync();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar actividad predeterminada del vendor {Id}", id);
                return StatusCode(500, new { message = "Error al marcar actividad", error = ex.Message });
            }
        }

        // DELETE /api/Vendor/{id}/economic-activities/{activityId}
        [HttpDelete("{id:int}/economic-activities/{activityId:int}")]
        public async Task<IActionResult> DeleteActivity(int id, int activityId)
        {
            try
            {
                var companyId = GetCompanyId();
                using var db = await _factory.CreateDbContextAsync(companyId);

                var entity = await db.VendorEconomicActivities
                    .FirstOrDefaultAsync(a => a.Id == activityId && a.IdVendor == id);
                if (entity == null)
                    return NotFound(new { message = $"No existe la actividad {activityId} para el vendor {id}." });

                db.VendorEconomicActivities.Remove(entity);
                await db.SaveChangesAsync();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar actividad del vendor {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar actividad", error = ex.Message });
            }
        }
    }
}
