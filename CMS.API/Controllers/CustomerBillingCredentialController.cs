// ================================================================================
// ARCHIVO: CMS.API/Controllers/CustomerBillingCredentialController.cs
// PROPÓSITO: API Controller para gestión de credenciales de facturación electrónica
// DESCRIPCIÓN: CRUD de CustomerBillingCredential (emisores y receptores)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-01-24
// ================================================================================

using CMS.Data.Services;
using CMS.Entities.Operational;
using CMS.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerBillingCredentialController : ControllerBase
    {
        private readonly ICompanyDbContextFactory _factory;
        private readonly GlobalParameterService _globalParameters;
        private readonly ILogger<CustomerBillingCredentialController> _logger;

        // Menú /Settings/GlobalParameters (cms.admin.menu id_menu=204) y código del parámetro global
        private const int GlobalParametersMenuId = 204;
        private const string DefaultEconomicActivityCode = "default_economic_activity";
        private const string FallbackEconomicActivity = "0000.1";

        public CustomerBillingCredentialController(
            ICompanyDbContextFactory factory,
            GlobalParameterService globalParameters,
            ILogger<CustomerBillingCredentialController> logger)
        {
            _factory = factory;
            _globalParameters = globalParameters;
            _logger = logger;
        }

        /// <summary>
        /// Devuelve el código de actividad económica por defecto leído del parámetro
        /// global 'default_economic_activity'. Si no existe, usa el fallback 0000.1.
        /// </summary>
        private async Task<string> GetDefaultEconomicActivityAsync(int companyId)
        {
            var value = await _globalParameters.GetStringValueAsync(
                companyId, GlobalParametersMenuId, DefaultEconomicActivityCode, FallbackEconomicActivity);
            return string.IsNullOrWhiteSpace(value) ? FallbackEconomicActivity : value;
        }

        private int GetCompanyId()
        {
            var claim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out var id))
                throw new UnauthorizedAccessException("CompanyId no encontrado en el token");
            return id;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        /// <summary>Lista todas las credenciales activas.</summary>
        [HttpGet]
        public async Task<ActionResult<List<CustomerBillingCredential>>> GetAll([FromQuery] bool includeInactive = false)
        {
            try
            {
                var companyId = GetCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var query = db.CustomerBillingCredentials.AsNoTracking();

                if (!includeInactive)
                    query = query.Where(c => c.IsActive);

                var credentials = await query
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return Ok(credentials);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener credentials");
                return StatusCode(500, new { message = "Error al obtener credentials", error = ex.Message });
            }
        }

        /// <summary>Busca emisores con filtros (combina customer + credential).</summary>
        [HttpGet("search-issuers")]
        public async Task<ActionResult<List<IssuerSearchResultDto>>> SearchIssuers(
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? identificationType = null,
            [FromQuery] bool includeInactive = false)
        {
            try
            {
                _logger.LogInformation("SearchIssuers iniciado. User claims: {Claims}", 
                    string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));

                var companyId = GetCompanyId();
                _logger.LogInformation("SearchIssuers - companyId: {CompanyId}", companyId);

                await using var db = await _factory.CreateDbContextAsync(companyId);

                var query = from cred in db.CustomerBillingCredentials
                            join cust in db.Customers on cred.IdCustomer equals cust.Id into custGroup
                            from cust in custGroup.DefaultIfEmpty()
                            where cred.IsIssuer
                            select new
                            {
                                cred,
                                cust
                            };

                // Filtros
                if (!includeInactive)
                    query = query.Where(x => x.cred.IsActive);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.ToLower();
                    query = query.Where(x =>
                        x.cred.Name.ToLower().Contains(term) ||
                        x.cred.Identification.Contains(term) ||
                        (x.cust != null && x.cust.Code.ToLower().Contains(term)));
                }

                if (!string.IsNullOrWhiteSpace(identificationType))
                    query = query.Where(x => x.cred.IdentificationType == identificationType);

                var results = await query
                    .Select(x => new IssuerSearchResultDto
                    {
                        IdCredential = x.cred.Id,
                        IdCustomer = x.cred.IdCustomer,
                        Name = x.cred.Name,
                        Identification = x.cred.Identification,
                        IdentificationType = x.cred.IdentificationType,
                        CommercialName = x.cred.CommercialName,
                        EconomicActivity = x.cred.EconomicActivity,
                        Email = x.cred.Email,
                        Phone = x.cred.Phone,
                        PhoneCode = x.cred.PhoneCode,
                        CustomerCode = x.cust != null ? x.cust.Code : null,
                        CustomerType = x.cust != null ? x.cust.CustomerType : null,
                        Environment = x.cred.Environment,
                        IsCompanyOwner = x.cred.IsCompanyOwner,
                        IsActive = x.cred.IsActive
                    })
                    .OrderByDescending(x => x.IsCompanyOwner)
                    .ThenBy(x => x.Name)
                    .Take(50)
                    .ToListAsync();

                _logger.LogInformation("SearchIssuers - found {Count} issuers", results.Count);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar emisores. SearchTerm={SearchTerm}, IdentificationType={IdentificationType}", 
                    searchTerm, identificationType);
                return StatusCode(500, new { message = "Error al buscar emisores", error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>Lista solo los emisores activos (is_issuer = true).</summary>
        [HttpGet("issuers")]
        public async Task<ActionResult<List<CustomerBillingCredential>>> GetIssuers([FromQuery] bool includeInactive = false)
        {
            try
            {
                var companyId = GetCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var query = db.CustomerBillingCredentials
                    .AsNoTracking()
                    .Where(c => c.IsIssuer);

                if (!includeInactive)
                    query = query.Where(c => c.IsActive);

                var issuers = await query
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return Ok(issuers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener emisores");
                return StatusCode(500, new { message = "Error al obtener emisores", error = ex.Message });
            }
        }

        /// <summary>Lista solo los receptores activos (is_issuer = false).</summary>
        [HttpGet("receptors")]
        public async Task<ActionResult<List<CustomerBillingCredential>>> GetReceptors([FromQuery] bool includeInactive = false)
        {
            try
            {
                var companyId = GetCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var query = db.CustomerBillingCredentials
                    .AsNoTracking()
                    .Where(c => !c.IsIssuer);

                if (!includeInactive)
                    query = query.Where(c => c.IsActive);

                var receptors = await query
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return Ok(receptors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener receptores");
                return StatusCode(500, new { message = "Error al obtener receptores", error = ex.Message });
            }
        }

        /// <summary>Obtiene la credencial marcada como company owner.</summary>
        [HttpGet("company-owner")]
        public async Task<ActionResult<CustomerBillingCredential>> GetCompanyOwner()
        {
            try
            {
                var companyId = GetCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var owner = await db.CustomerBillingCredentials
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.IsCompanyOwner && c.IsActive);

                if (owner == null)
                    return NotFound(new { message = "No hay credencial marcada como company owner" });

                return Ok(owner);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener company owner");
                return StatusCode(500, new { message = "Error al obtener company owner", error = ex.Message });
            }
        }

        /// <summary>Obtiene una credencial por ID.</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CustomerBillingCredential>> GetById(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var credential = await db.CustomerBillingCredentials
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (credential == null)
                    return NotFound(new { message = $"Credencial {id} no encontrada" });

                return Ok(credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener credencial {Id}", id);
                return StatusCode(500, new { message = "Error al obtener credencial", error = ex.Message });
            }
        }

        /// <summary>Crea una nueva credencial de facturación.</summary>
        [HttpPost]
        public async Task<ActionResult<CustomerBillingCredential>> Create([FromBody] CustomerBillingCredential credential)
        {
            try
            {
                var companyId = GetCompanyId();
                var userId = GetUserId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                // Validar que no exista otra credential activa para el mismo customer y ambiente
                if (credential.IdCustomer.HasValue)
                {
                    var exists = await db.CustomerBillingCredentials
                        .AnyAsync(c => c.IdCustomer == credential.IdCustomer && c.Environment == credential.Environment && c.IsActive);
                    if (exists)
                        return BadRequest(new { message = $"Ya existe una credencial activa para este customer en ambiente {credential.Environment}" });
                }

                // Validar que solo haya 1 company owner activo
                if (credential.IsCompanyOwner)
                {
                    var hasOwner = await db.CustomerBillingCredentials
                        .AnyAsync(c => c.IsCompanyOwner && c.IsActive);
                    if (hasOwner)
                        return BadRequest(new { message = "Ya existe otra credencial marcada como company owner activa" });
                }

                // Actividad económica obligatoria: si no se indica, aplicar default global (0000.1)
                if (string.IsNullOrWhiteSpace(credential.EconomicActivity))
                    credential.EconomicActivity = await GetDefaultEconomicActivityAsync(companyId);

                // Régimen especial: si está marcado, el código de régimen es obligatorio
                if (credential.IsSpecialRegime && string.IsNullOrWhiteSpace(credential.SpecialRegimeCode))
                    return BadRequest(new { message = "El código de régimen especial es obligatorio cuando el emisor está en régimen especial" });

                // Auditoría
                credential.CreateDate = DateTime.UtcNow;
                credential.RecordDate = DateTime.UtcNow;
                credential.CreatedBy = userId.ToString();
                credential.UpdatedBy = userId.ToString();
                credential.RowPointer = Guid.NewGuid();

                db.CustomerBillingCredentials.Add(credential);
                await db.SaveChangesAsync();

                _logger.LogInformation("Credencial creada: {Name} (ID: {Id})", credential.Name, credential.Id);

                return CreatedAtAction(nameof(GetById), new { id = credential.Id }, credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear credencial");
                return StatusCode(500, new { message = "Error al crear credencial", error = ex.Message });
            }
        }

        /// <summary>Actualiza una credencial existente.</summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<CustomerBillingCredential>> Update(int id, [FromBody] CustomerBillingCredential credential)
        {
            try
            {
                if (id != credential.Id)
                    return BadRequest(new { message = "El ID de la URL no coincide con el ID del body" });

                var companyId = GetCompanyId();
                var userId = GetUserId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var existing = await db.CustomerBillingCredentials.FindAsync(id);
                if (existing == null)
                    return NotFound(new { message = $"Credencial {id} no encontrada" });

                // Validar unicidad de customer/ambiente (excluyendo el actual)
                if (credential.IdCustomer.HasValue)
                {
                    var duplicate = await db.CustomerBillingCredentials
                        .AnyAsync(c => c.IdCustomer == credential.IdCustomer 
                            && c.Environment == credential.Environment 
                            && c.IsActive 
                            && c.Id != id);
                    if (duplicate)
                        return BadRequest(new { message = $"Ya existe otra credencial activa para este customer en ambiente {credential.Environment}" });
                }

                // Validar company owner único
                if (credential.IsCompanyOwner)
                {
                    var hasOtherOwner = await db.CustomerBillingCredentials
                        .AnyAsync(c => c.IsCompanyOwner && c.IsActive && c.Id != id);
                    if (hasOtherOwner)
                        return BadRequest(new { message = "Ya existe otra credencial marcada como company owner activa" });
                }

                // Régimen especial: si está marcado, el código de régimen es obligatorio
                if (credential.IsSpecialRegime && string.IsNullOrWhiteSpace(credential.SpecialRegimeCode))
                    return BadRequest(new { message = "El código de régimen especial es obligatorio cuando el emisor está en régimen especial" });

                // Copiar propiedades (excepto auditoría de creación y datos cifrados que no se editan aquí)
                existing.IdCustomer = credential.IdCustomer;
                existing.Environment = credential.Environment;
                existing.IsIssuer = credential.IsIssuer;
                existing.IsCompanyOwner = credential.IsCompanyOwner;
                existing.IsSpecialRegime = credential.IsSpecialRegime;
                existing.SpecialRegimeCode = credential.IsSpecialRegime ? credential.SpecialRegimeCode : null;
                existing.Name = credential.Name;
                existing.CommercialName = credential.CommercialName;
                existing.IdentificationType = credential.IdentificationType;
                existing.Identification = credential.Identification;
                existing.ForeignIdentification = credential.ForeignIdentification;
                existing.EconomicActivity = string.IsNullOrWhiteSpace(credential.EconomicActivity)
                    ? await GetDefaultEconomicActivityAsync(companyId)
                    : credential.EconomicActivity;
                existing.Province = credential.Province;
                existing.Canton = credential.Canton;
                existing.District = credential.District;
                existing.OtherSigns = credential.OtherSigns;
                existing.GpsLatitude = credential.GpsLatitude;
                existing.GpsLongitude = credential.GpsLongitude;
                existing.PhoneCode = credential.PhoneCode;
                existing.Phone = credential.Phone;
                existing.Email = credential.Email;
                existing.OAuthUsername = credential.OAuthUsername;
                existing.IsActive = credential.IsActive;

                // Auditoría de actualización
                existing.RecordDate = DateTime.UtcNow;
                existing.UpdatedBy = userId.ToString();

                await db.SaveChangesAsync();

                _logger.LogInformation("Credencial actualizada: {Name} (ID: {Id})", existing.Name, existing.Id);

                return Ok(existing);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar credencial {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar credencial", error = ex.Message });
            }
        }

        /// <summary>Elimina (desactiva) una credencial.</summary>
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var credential = await db.CustomerBillingCredentials.FindAsync(id);
                if (credential == null)
                    return NotFound(new { message = $"Credencial {id} no encontrada" });

                // Soft delete: solo desactivar
                credential.IsActive = false;
                credential.RecordDate = DateTime.UtcNow;
                await db.SaveChangesAsync();

                _logger.LogInformation("Credencial desactivada: {Name} (ID: {Id})", credential.Name, credential.Id);

                return Ok(new { message = "Credencial desactivada exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar credencial {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar credencial", error = ex.Message });
            }
        }
    }
}
