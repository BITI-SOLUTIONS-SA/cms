// ================================================================================
// ARCHIVO: CMS.API/Controllers/CompanyController.cs
// PROPÓSITO: API REST para gestión de compañías del sistema
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-27
// ================================================================================

using CMS.Data;
using CMS.Data.Services;
using CMS.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CompanyController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly CompanyConfigService _companyService;
        private readonly ILogger<CompanyController> _logger;

        public CompanyController(
            AppDbContext db,
            CompanyConfigService companyService,
            ILogger<CompanyController> logger)
        {
            _db = db;
            _companyService = companyService;
            _logger = logger;
        }

        /// <summary>
        /// Obtener lista de compañías visibles para el usuario
        /// GET: api/company
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<CompanyListDto>>> GetAll()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Usuario no identificado" });
                }

                var companies = await _companyService.GetVisibleCompaniesForUserAsync(userId);

                var result = companies.Select(c => new CompanyListDto
                {
                    Id = c.ID,
                    CompanyName = c.COMPANY_NAME,
                    CompanySchema = c.COMPANY_SCHEMA,
                    Description = c.DESCRIPTION,
                    IsActive = c.IS_ACTIVE,
                    IsAdminCompany = c.IS_ADMIN_COMPANY,
                    ContactEmail = c.CONTACT_EMAIL,
                    ContactPhone = c.CONTACT_PHONE,
                    Website = c.WEBSITE,
                    LogoUrl = c.LOGO_URL,
                    LogoData = c.LOGO_DATA,
                    CreateDate = c.CreateDate
                }).ToList();

                _logger.LogInformation("📋 Compañías obtenidas: {Count}", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo compañías");
                return StatusCode(500, new { message = "Error obteniendo compañías" });
            }
        }

        /// <summary>
        /// Obtener detalle de una compañía
        /// GET: api/company/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<CompanyDetailDto>> GetById(int id)
        {
            try
            {
                var company = await _db.Companies
                    .Include(c => c.Country)
                    .FirstOrDefaultAsync(c => c.ID == id);

                if (company == null)
                {
                    return NotFound(new { message = "Compañía no encontrada" });
                }

                var userCount = await _db.UserCompanies
                    .Where(uc => uc.ID_COMPANY == id && uc.IS_ACTIVE)
                    .Select(uc => uc.ID_USER)
                    .Distinct()
                    .CountAsync();

                var result = new CompanyDetailDto
                {
                    Id = company.ID,
                    CompanyName = company.COMPANY_NAME,
                    CompanySchema = company.COMPANY_SCHEMA,
                    Description = company.DESCRIPTION,
                    CompanyTaxId = company.COMPANY_TAX_ID,
                    Address = company.ADDRESS,
                    City = company.CITY,
                    CountryId = company.IdCountry,
                    CountryName = company.Country?.NAME,
                    ContactEmail = company.CONTACT_EMAIL,
                    ContactPhone = company.CONTACT_PHONE,
                    Website = company.WEBSITE,
                    LogoUrl = company.LOGO_URL,
                    LogoData = company.LOGO_DATA,
                    ManagerName = company.MANAGER_NAME,
                    Industry = company.INDUSTRY,
                    IsActive = company.IS_ACTIVE,
                    IsAdminCompany = company.IS_ADMIN_COMPANY,
                    IsTenant = company.IsTenant,
                    IsProduction = company.IS_PRODUCTION,
                    UsesAzureAd = company.USES_AZURE_AD,
                    DashboardWelcomeMessage = company.DASHBOARD_WELCOME_MESSAGE,
                    MaxFailedLoginAttempts = company.MAX_FAILED_LOGIN_ATTEMPTS,
                    LockoutDurationMinutes = company.LOCKOUT_DURATION_MINUTES,
                    CreateDate = company.CreateDate,
                    RecordDate = company.RecordDate,
                    UserCount = userCount
                };

                _logger.LogInformation("📋 Detalle de compañía {Id}: {Name}", id, company.COMPANY_NAME);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo compañía {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo compañía" });
            }
        }

        /// <summary>
        /// Crear nueva compañía
        /// POST: api/company
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<CompanyDetailDto>> Create([FromBody] CompanyCreateDto dto)
        {
            try
            {
                // Verificar que el schema no exista
                var existingSchema = await _db.Companies
                    .AnyAsync(c => c.COMPANY_SCHEMA.ToLower() == dto.CompanySchema.ToLower());

                if (existingSchema)
                {
                    return BadRequest(new { message = "Ya existe una compañía con ese schema" });
                }

                var company = new Company
                {
                    COMPANY_NAME = dto.CompanyName,
                    COMPANY_SCHEMA = dto.CompanySchema.ToLower(),
                    DESCRIPTION = dto.Description,
                    COMPANY_TAX_ID = dto.CompanyTaxId,
                    ADDRESS = dto.Address,
                    CITY = dto.City,
                    IdCountry = dto.CountryId,
                    CONTACT_EMAIL = dto.ContactEmail,
                    CONTACT_PHONE = dto.ContactPhone,
                    WEBSITE = dto.Website,
                    LOGO_URL = dto.LogoUrl,
                    MANAGER_NAME = dto.ManagerName,
                    INDUSTRY = dto.Industry,
                    IS_ACTIVE = true,
                    IS_ADMIN_COMPANY = false,
                    IsTenant = true,
                    IS_PRODUCTION = false,
                    USES_AZURE_AD = false,
                    MAX_FAILED_LOGIN_ATTEMPTS = 5,
                    LOCKOUT_DURATION_MINUTES = 15,
                    CreateDate = DateTime.UtcNow,
                    RecordDate = DateTime.UtcNow,
                    CreatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM",
                    UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM"
                };

                _db.Companies.Add(company);
                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Compañía creada: {Name} ({Schema})", company.COMPANY_NAME, company.COMPANY_SCHEMA);

                return CreatedAtAction(nameof(GetById), new { id = company.ID }, new { id = company.ID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando compañía");
                return StatusCode(500, new { message = "Error creando compañía" });
            }
        }

        /// <summary>
        /// Actualizar compañía
        /// PUT: api/company/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CompanyUpdateDto dto)
        {
            try
            {
                var company = await _db.Companies.FindAsync(id);
                if (company == null)
                {
                    return NotFound(new { message = "Compañía no encontrada" });
                }

                company.COMPANY_NAME = dto.CompanyName;
                company.DESCRIPTION = dto.Description;
                company.COMPANY_TAX_ID = dto.CompanyTaxId;
                company.ADDRESS = dto.Address;
                company.CITY = dto.City;
                company.IdCountry = dto.CountryId;
                company.CONTACT_EMAIL = dto.ContactEmail;
                company.CONTACT_PHONE = dto.ContactPhone;
                company.WEBSITE = dto.Website;
                company.LOGO_URL = dto.LogoUrl;
                company.MANAGER_NAME = dto.ManagerName;
                company.INDUSTRY = dto.Industry;
                company.DASHBOARD_WELCOME_MESSAGE = dto.DashboardWelcomeMessage;
                company.MAX_FAILED_LOGIN_ATTEMPTS = dto.MaxFailedLoginAttempts;
                company.LOCKOUT_DURATION_MINUTES = dto.LockoutDurationMinutes;
                company.RecordDate = DateTime.UtcNow;
                company.UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM";

                await _db.SaveChangesAsync();

                _logger.LogInformation("Compañía actualizada: {Id} - {Name}", id, company.COMPANY_NAME);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando compañía {Id}", id);
                return StatusCode(500, new { message = "Error actualizando compañía" });
            }
        }

        /// <summary>
        /// Activar/Desactivar compañía
        /// PATCH: api/company/{id}/toggle-active
        /// </summary>
        [HttpPatch("{id}/toggle-active")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            try
            {
                var company = await _db.Companies.FindAsync(id);
                if (company == null)
                {
                    return NotFound(new { message = "Compañía no encontrada" });
                }

                if (company.IS_ADMIN_COMPANY)
                {
                    return BadRequest(new { message = "No se puede desactivar la compañía de administración" });
                }

                company.IS_ACTIVE = !company.IS_ACTIVE;
                company.RecordDate = DateTime.UtcNow;
                company.UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM";

                await _db.SaveChangesAsync();

                _logger.LogInformation("🔄 Compañía {Id} {Status}", id, company.IS_ACTIVE ? "activada" : "desactivada");

                return Ok(new { isActive = company.IS_ACTIVE });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cambiando estado de compañía {Id}", id);
                return StatusCode(500, new { message = "Error cambiando estado" });
            }
        }

        // ================================================================================
        // DTOs
        // ================================================================================

        public class CompanyListDto
        {
            public int Id { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public string CompanySchema { get; set; } = string.Empty;
            public string? Description { get; set; }
            public bool IsActive { get; set; }
            public bool IsAdminCompany { get; set; }
            public string? ContactEmail { get; set; }
            public string? ContactPhone { get; set; }
            public string? Website { get; set; }
            public string? LogoUrl { get; set; }
            /// <summary>
            /// Logo en formato Data URI (base64)
            /// </summary>
            public string? LogoData { get; set; }
            public DateTime? CreateDate { get; set; }
        }

        public class CompanyDetailDto : CompanyListDto
        {
            public string? CompanyTaxId { get; set; }
            public string? Address { get; set; }
            public string? City { get; set; }
            public int? CountryId { get; set; }
            public string? CountryName { get; set; }
            public string? ManagerName { get; set; }
            public string? Industry { get; set; }
            public bool IsTenant { get; set; }
            public bool IsProduction { get; set; }
            public bool UsesAzureAd { get; set; }
            public string? DashboardWelcomeMessage { get; set; }
            public int MaxFailedLoginAttempts { get; set; }
            public int LockoutDurationMinutes { get; set; }
            public DateTime? RecordDate { get; set; }
            public int UserCount { get; set; }
        }

        public class CompanyCreateDto
        {
            public string CompanyName { get; set; } = string.Empty;
            public string CompanySchema { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? CompanyTaxId { get; set; }
            public string? Address { get; set; }
            public string? City { get; set; }
            public int? CountryId { get; set; }
            public string? ContactEmail { get; set; }
            public string? ContactPhone { get; set; }
            public string? Website { get; set; }
            public string? LogoUrl { get; set; }
            public string? ManagerName { get; set; }
            public string? Industry { get; set; }
        }

        public class CompanyUpdateDto
        {
            public string CompanyName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? CompanyTaxId { get; set; }
            public string? Address { get; set; }
            public string? City { get; set; }
            public int? CountryId { get; set; }
            public string? ContactEmail { get; set; }
            public string? ContactPhone { get; set; }
            public string? Website { get; set; }
            public string? LogoUrl { get; set; }
            public string? ManagerName { get; set; }
            public string? Industry { get; set; }
            public string? DashboardWelcomeMessage { get; set; }
            public int MaxFailedLoginAttempts { get; set; } = 5;
            public int LockoutDurationMinutes { get; set; } = 15;
        }
    }
}
