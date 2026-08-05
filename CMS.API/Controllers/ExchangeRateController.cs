// ================================================================================
// ARCHIVO: CMS.API/Controllers/ExchangeRateController.cs
// PROPÓSITO: API REST para catálogo de Tipos de Tasa de Cambio
// DESCRIPCIÓN: CRUD completo para mantenimiento del catálogo exchange_rate.
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2026-06-28
// ================================================================================

using System.Security.Claims;
using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ExchangeRateController : ControllerBase
    {
        private readonly IExchangeRateService _service;
        private readonly ILogger<ExchangeRateController> _logger;

        public ExchangeRateController(
            IExchangeRateService service,
            ILogger<ExchangeRateController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        private int GetCompanyId()
        {
            var claim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out var companyId))
                throw new UnauthorizedAccessException("CompanyId no encontrado en el token");
            return companyId;
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM";

        /// <summary>Obtener todos los tipos de tasa de cambio</summary>
        [HttpGet]
        public async Task<ActionResult<List<ExchangeRateDto>>> GetAll([FromQuery] bool? isActive = null)
        {
            try
            {
                var companyId = GetCompanyId();
                var items = await _service.GetAllAsync(companyId, isActive);
                return Ok(items.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchange rates");
                return StatusCode(500, new { message = "Error al obtener tipos de tasa de cambio", error = ex.Message });
            }
        }

        /// <summary>Obtener tipo de tasa por ID</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ExchangeRateDto>> GetById(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var item = await _service.GetByIdAsync(companyId, id);
                if (item == null)
                    return NotFound(new { message = "Tipo de tasa de cambio no encontrado" });
                return Ok(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchange rate {Id}", id);
                return StatusCode(500, new { message = "Error al obtener tipo de tasa de cambio", error = ex.Message });
            }
        }

        /// <summary>Obtener tipo de tasa por código</summary>
        [HttpGet("byCode/{code}")]
        public async Task<ActionResult<ExchangeRateDto>> GetByCode(string code)
        {
            try
            {
                var companyId = GetCompanyId();
                var item = await _service.GetByCodeAsync(companyId, code);
                if (item == null)
                    return NotFound(new { message = "Tipo de tasa de cambio no encontrado" });
                return Ok(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchange rate by code {Code}", code);
                return StatusCode(500, new { message = "Error al obtener tipo de tasa de cambio", error = ex.Message });
            }
        }

        /// <summary>Crear nuevo tipo de tasa de cambio</summary>
        [HttpPost]
        public async Task<ActionResult<ExchangeRateDto>> Create([FromBody] ExchangeRateDto dto)
        {
            try
            {
                var companyId   = GetCompanyId();
                var currentUser = GetCurrentUser();

                var entity  = MapToEntity(dto);
                var created = await _service.CreateAsync(companyId, entity, currentUser);

                return CreatedAtAction(nameof(GetById), new { id = created.IdExchangeRate }, MapToDto(created));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating exchange rate");
                return StatusCode(500, new { message = "Error al crear tipo de tasa de cambio", error = ex.Message });
            }
        }

        /// <summary>Actualizar tipo de tasa de cambio</summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ExchangeRateDto>> Update(int id, [FromBody] ExchangeRateDto dto)
        {
            try
            {
                if (id != dto.IdExchangeRate)
                    return BadRequest(new { message = "El ID no coincide" });

                var companyId   = GetCompanyId();
                var currentUser = GetCurrentUser();

                var entity  = MapToEntity(dto);
                var updated = await _service.UpdateAsync(companyId, entity, currentUser);

                return Ok(MapToDto(updated));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating exchange rate {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar tipo de tasa de cambio", error = ex.Message });
            }
        }

        /// <summary>Eliminar tipo de tasa de cambio</summary>
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var deleted   = await _service.DeleteAsync(companyId, id);

                if (!deleted)
                    return NotFound(new { message = "Tipo de tasa de cambio no encontrado" });

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting exchange rate {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar tipo de tasa de cambio", error = ex.Message });
            }
        }

        // ===== MAPEO DTO ↔ ENTITY =====

        private static ExchangeRateDto MapToDto(ExchangeRate entity) => new()
        {
            IdExchangeRate = entity.IdExchangeRate,
            Code           = entity.Code,
            Description    = entity.Description,
            IsActive       = entity.IsActive,
            DisplayOrder   = entity.DisplayOrder
        };

        private static ExchangeRate MapToEntity(ExchangeRateDto dto) => new()
        {
            IdExchangeRate = dto.IdExchangeRate,
            Code           = dto.Code ?? string.Empty,
            Description    = dto.Description,
            IsActive       = dto.IsActive,
            DisplayOrder   = dto.DisplayOrder
        };
    }

    // ===== DTO =====

    public class ExchangeRateDto
    {
        public int     IdExchangeRate { get; set; }
        public string? Code           { get; set; }
        public string? Description    { get; set; }
        public bool    IsActive       { get; set; } = true;
        public int     DisplayOrder   { get; set; }
    }
}
