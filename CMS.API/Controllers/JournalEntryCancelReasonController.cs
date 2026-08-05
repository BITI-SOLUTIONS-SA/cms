// ================================================================================
// ARCHIVO: CMS.API/Controllers/JournalEntryCancelReasonController.cs
// PROPÓSITO: API REST para catálogo de Razones de Cancelación de Asientos
// DESCRIPCIÓN: CRUD completo para mantenimiento del catálogo de razones de
//              cancelación de asientos de diario.
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-XX
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
    public class JournalEntryCancelReasonController : ControllerBase
    {
        private readonly IJournalEntryCancelReasonService _service;
        private readonly ILogger<JournalEntryCancelReasonController> _logger;

        public JournalEntryCancelReasonController(
            IJournalEntryCancelReasonService service,
            ILogger<JournalEntryCancelReasonController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int GetCompanyId()
        {
            var companyIdClaim = User.FindFirst("CompanyId")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim) || !int.TryParse(companyIdClaim, out var companyId))
            {
                throw new UnauthorizedAccessException("CompanyId no encontrado en el token");
            }
            return companyId;
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";

        /// <summary>
        /// Obtener todas las razones de cancelación
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<JournalEntryCancelReasonDto>>> GetAll([FromQuery] bool? isActive = null)
        {
            try
            {
                var companyId = GetCompanyId();
                var reasons = await _service.GetAllReasonsAsync(companyId, isActive);
                var dtos = reasons.Select(MapToDto).ToList();
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cancel reasons");
                return StatusCode(500, new { message = "Error al obtener razones de cancelación", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener razón por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<JournalEntryCancelReasonDto>> GetById(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var reason = await _service.GetReasonByIdAsync(companyId, id);

                if (reason == null)
                    return NotFound(new { message = "Razón de cancelación no encontrada" });

                return Ok(MapToDto(reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cancel reason {Id}", id);
                return StatusCode(500, new { message = "Error al obtener razón de cancelación", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener razón por código
        /// </summary>
        [HttpGet("byCode/{code}")]
        public async Task<ActionResult<JournalEntryCancelReasonDto>> GetByCode(string code)
        {
            try
            {
                var companyId = GetCompanyId();
                var reason = await _service.GetReasonByCodeAsync(companyId, code);

                if (reason == null)
                    return NotFound(new { message = "Razón de cancelación no encontrada" });

                return Ok(MapToDto(reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cancel reason by code {Code}", code);
                return StatusCode(500, new { message = "Error al obtener razón de cancelación", error = ex.Message });
            }
        }

        /// <summary>
        /// Crear nueva razón de cancelación
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<JournalEntryCancelReasonDto>> Create([FromBody] JournalEntryCancelReasonDto dto)
        {
            try
            {
                var companyId = GetCompanyId();
                var currentUser = GetCurrentUser();

                var reason = MapToEntity(dto);
                var created = await _service.CreateReasonAsync(companyId, reason, currentUser);

                return CreatedAtAction(nameof(GetById), new { id = created.IdJournalEntryCancelReason }, MapToDto(created));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating cancel reason");
                return StatusCode(500, new { message = "Error al crear razón de cancelación", error = ex.Message });
            }
        }

        /// <summary>
        /// Actualizar razón de cancelación
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<JournalEntryCancelReasonDto>> Update(int id, [FromBody] JournalEntryCancelReasonDto dto)
        {
            try
            {
                if (id != dto.IdJournalEntryCancelReason)
                    return BadRequest(new { message = "El ID no coincide" });

                var companyId = GetCompanyId();
                var currentUser = GetCurrentUser();

                var reason = MapToEntity(dto);
                var updated = await _service.UpdateReasonAsync(companyId, reason, currentUser);

                return Ok(MapToDto(updated));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cancel reason {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar razón de cancelación", error = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar razón de cancelación
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                await _service.DeleteReasonAsync(companyId, id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting cancel reason {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar razón de cancelación", error = ex.Message });
            }
        }

        // ===== MAPEO DTO ↔ ENTITY =====

        private JournalEntryCancelReasonDto MapToDto(JournalEntryCancelReason entity)
        {
            return new JournalEntryCancelReasonDto
            {
                IdJournalEntryCancelReason = entity.IdJournalEntryCancelReason,
                Code = entity.Code,
                Name = entity.Name,
                Description = entity.Description,
                IsActive = entity.IsActive,
                SortOrder = entity.SortOrder
            };
        }

        private JournalEntryCancelReason MapToEntity(JournalEntryCancelReasonDto dto)
        {
            return new JournalEntryCancelReason
            {
                IdJournalEntryCancelReason = dto.IdJournalEntryCancelReason,
                Code = dto.Code ?? string.Empty,
                Name = dto.Name ?? string.Empty,
                Description = dto.Description,
                IsActive = dto.IsActive,
                SortOrder = dto.SortOrder
            };
        }
    }

    // ===== DTOs =====

    public class JournalEntryCancelReasonDto
    {
        public int IdJournalEntryCancelReason { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }
}
