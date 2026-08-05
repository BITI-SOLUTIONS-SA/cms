// ================================================================================
// ARCHIVO: CMS.API/Controllers/SupplierController.cs
// PROPÓSITO: API Controller para búsqueda de proveedores (receptores)
// DESCRIPCIÓN: Búsqueda de suppliers con filtros para emisión de facturas
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-01-24
// ================================================================================

using CMS.Data.Services;
using CMS.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SupplierController : ControllerBase
    {
        private readonly ICompanyDbContextFactory _factory;
        private readonly ILogger<SupplierController> _logger;

        public SupplierController(
            ICompanyDbContextFactory factory,
            ILogger<SupplierController> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        private int GetCompanyId()
        {
            var claim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out var id))
                throw new UnauthorizedAccessException("CompanyId no encontrado en el token");
            return id;
        }

        /// <summary>Busca proveedores (receptores) con filtros.</summary>
        [HttpGet("search-receptors")]
        public async Task<ActionResult<List<ReceptorSearchResultDto>>> SearchReceptors(
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? identificationType = null,
            [FromQuery] string? supplierType = null,
            [FromQuery] bool includeInactive = false)
        {
            try
            {
                var companyId = GetCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var query = db.Suppliers.AsNoTracking();

                // Filtros
                if (!includeInactive)
                    query = query.Where(s => s.IsActive);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.ToLower();
                    query = query.Where(s =>
                        s.Name.ToLower().Contains(term) ||
                        s.Code.ToLower().Contains(term) ||
                        (s.Identification != null && s.Identification.Contains(term)));
                }

                if (!string.IsNullOrWhiteSpace(identificationType))
                    query = query.Where(s => s.IdentificationType == identificationType);

                if (!string.IsNullOrWhiteSpace(supplierType))
                    query = query.Where(s => s.SupplierType == supplierType);

                var results = await query
                    .Select(s => new ReceptorSearchResultDto
                    {
                        IdSupplier = s.Id,
                        Code = s.Code,
                        Name = s.Name,
                        CommercialName = s.CommercialName,
                        IdentificationType = s.IdentificationType,
                        Identification = s.Identification,
                        ForeignIdentification = s.ForeignIdentification,
                        Email = s.Email,
                        Phone = s.Phone,
                        PhoneCode = s.PhoneCode,
                        SupplierType = s.SupplierType,
                        EconomicActivity = s.EconomicActivity,
                        IsActive = s.IsActive
                    })
                    .OrderBy(s => s.Name)
                    .Take(50)
                    .ToListAsync();

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar receptores");
                return StatusCode(500, new { message = "Error al buscar receptores", error = ex.Message });
            }
        }
    }
}
