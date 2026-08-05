// ================================================================================
// ARCHIVO: CMS.API/Controllers/JournalEntryController.cs
// PROPÓSITO: API REST para gestión de Asientos de Diario (Journal Entries)
// DESCRIPCIÓN: Endpoints para CRUD completo, contabilización, reversión,
//              aprobación y consultas de asientos contables.
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-XX
// ================================================================================

using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class JournalEntryController : ControllerBase
    {
        private readonly IJournalEntryService _journalEntryService;
        private readonly ILogger<JournalEntryController> _logger;

        public JournalEntryController(
            IJournalEntryService journalEntryService,
            ILogger<JournalEntryController> logger)
        {
            _journalEntryService = journalEntryService;
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

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("UserId no encontrado en el token");
            }
            return userId;
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";

        // ===== CONSULTAS =====

        /// <summary>
        /// Obtener todos los asientos de diario con filtros opcionales
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<JournalEntryDto>>> GetJournalEntries(
            [FromQuery] int? idJournalEntryStatus = null,
            [FromQuery] int? idTypeAccounting = null,
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] string? search = null)
        {
            try
            {
                var companyId = GetCompanyId();

                DateOnly? parsedDateFrom = null;
                DateOnly? parsedDateTo = null;

                if (!string.IsNullOrWhiteSpace(dateFrom) && DateOnly.TryParse(dateFrom, out var from))
                    parsedDateFrom = from;

                if (!string.IsNullOrWhiteSpace(dateTo) && DateOnly.TryParse(dateTo, out var to))
                    parsedDateTo = to;

                var entries = await _journalEntryService.GetJournalEntriesAsync(
                    companyId, idJournalEntryStatus, idTypeAccounting, parsedDateFrom, parsedDateTo, search);

                var dtos = entries.Select(MapToDto).ToList();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting journal entries");
                return StatusCode(500, new { message = "Error al obtener asientos de diario", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener un asiento por ID (incluye líneas)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<JournalEntryDto>> GetJournalEntryById(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var entry = await _journalEntryService.GetJournalEntryByIdAsync(companyId, id);

                if (entry == null)
                    return NotFound(new { message = "Asiento de diario no encontrado" });

                return Ok(MapToDto(entry));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting journal entry {Id}", id);
                return StatusCode(500, new { message = "Error al obtener asiento de diario", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener un asiento por número
        /// </summary>
        [HttpGet("number/{entryNumber}")]
        public async Task<ActionResult<JournalEntryDto>> GetJournalEntryByNumber(string entryNumber)
        {
            try
            {
                var companyId = GetCompanyId();
                var entry = await _journalEntryService.GetJournalEntryByNumberAsync(companyId, entryNumber);

                if (entry == null)
                    return NotFound(new { message = "Asiento de diario no encontrado" });

                return Ok(MapToDto(entry));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting journal entry {EntryNumber}", entryNumber);
                return StatusCode(500, new { message = "Error al obtener asiento de diario", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener siguiente número de asiento para un período
        /// </summary>
        [HttpGet("next-number")]
        public async Task<ActionResult<string>> GetNextEntryNumber([FromQuery] string period)
        {
            try
            {
                var companyId = GetCompanyId();
                var nextNumber = await _journalEntryService.GetNextEntryNumberAsync(companyId, period);
                return Ok(new { nextNumber });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next entry number for period {Period}", period);
                return StatusCode(500, new { message = "Error al obtener siguiente número", error = ex.Message });
            }
        }

        /// <summary>
        /// Verificar si un número de asiento existe
        /// </summary>
        [HttpGet("exists")]
        public async Task<ActionResult<bool>> EntryNumberExists(
            [FromQuery] string entryNumber,
            [FromQuery] int? excludeId = null)
        {
            try
            {
                var companyId = GetCompanyId();
                var exists = await _journalEntryService.EntryNumberExistsAsync(companyId, entryNumber, excludeId);
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking entry number existence");
                return StatusCode(500, new { message = "Error al verificar número", error = ex.Message });
            }
        }

        // ===== CRUD =====

        /// <summary>
        /// Crear un nuevo asiento de diario
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<JournalEntryDto>> CreateJournalEntry(JournalEntryDto dto)
        {
            try
            {
                var companyId = GetCompanyId();
                var userId = GetUserId();
                var currentUser = GetCurrentUser();

                // Validar que se haya enviado el id_menu
                if ((dto.IdMenu ?? 0) <= 0)
                {
                    return BadRequest(new { message = "El campo IdMenu es requerido para generar el consecutivo." });
                }

                var entry = MapToEntity(dto);
                var created = await _journalEntryService.CreateJournalEntryAsync(
                    companyId, entry, dto.IdMenu!.Value, userId, currentUser);

                return CreatedAtAction(nameof(GetJournalEntryById), new { id = created.IdJournalEntry }, MapToDto(created));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating journal entry");
                return StatusCode(500, new { message = "Error al crear asiento de diario", error = ex.Message });
            }
        }

        /// <summary>
        /// Actualizar un asiento de diario existente (solo borrador)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<JournalEntryDto>> UpdateJournalEntry(int id, JournalEntryDto dto)
        {
            try
            {
                if (id != dto.IdJournalEntry)
                    return BadRequest(new { message = "El ID del asiento no coincide" });

                var companyId = GetCompanyId();
                var currentUser = GetCurrentUser();

                var entry = MapToEntity(dto);
                var updated = await _journalEntryService.UpdateJournalEntryAsync(companyId, entry, currentUser);

                return Ok(MapToDto(updated));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating journal entry {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar asiento de diario", error = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar un asiento de diario (solo borrador)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteJournalEntry(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                await _journalEntryService.DeleteJournalEntryAsync(companyId, id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting journal entry {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar asiento de diario", error = ex.Message });
            }
        }

        // ===== OPERACIONES CONTABLES =====

        /// <summary>
        /// Contabilizar un asiento (cambiar estado a Posted)
        /// </summary>
        [HttpPost("{id}/post")]
        public async Task<ActionResult<JournalEntryDto>> PostJournalEntry(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var userId = GetUserId();
                var currentUser = GetCurrentUser();

                var posted = await _journalEntryService.PostJournalEntryAsync(companyId, id, userId, currentUser);
                return Ok(MapToDto(posted));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting journal entry {Id}", id);
                return StatusCode(500, new { message = "Error al contabilizar asiento", error = ex.Message });
            }
        }

        /// <summary>
        /// Revertir un asiento contabilizado
        /// </summary>
        [HttpPost("{id}/reverse")]
        public async Task<ActionResult<JournalEntryDto>> ReverseJournalEntry(int id, [FromBody] ReversalRequest request)
        {
            try
            {
                var companyId = GetCompanyId();
                var userId = GetUserId();
                var currentUser = GetCurrentUser();

                if (!DateOnly.TryParse(request.ReversalDate, out var reversalDate))
                    return BadRequest(new { message = "Fecha de reversión inválida" });

                if (request.IdMenu <= 0)
                    return BadRequest(new { message = "El campo IdMenu es requerido para generar el consecutivo de la reversión." });

                var reversed = await _journalEntryService.ReverseJournalEntryAsync(
                    companyId, id, reversalDate, request.IdCancelReason, request.IdMenu, userId, currentUser);

                return Ok(MapToDto(reversed));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reversing journal entry {Id}", id);
                return StatusCode(500, new { message = "Error al revertir asiento", error = ex.Message });
            }
        }

        /// <summary>
        /// Cancelar un asiento en borrador
        /// </summary>
        [HttpPost("{id}/cancel")]
        public async Task<ActionResult<JournalEntryDto>> CancelJournalEntry(int id, [FromBody] CancelRequest request)
        {
            try
            {
                var companyId = GetCompanyId();
                var userId = GetUserId();
                var currentUser = GetCurrentUser();

                var cancelled = await _journalEntryService.CancelJournalEntryAsync(
                    companyId, id, request.IdCancelReason, userId, currentUser);

                return Ok(MapToDto(cancelled));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling journal entry {Id}", id);
                return StatusCode(500, new { message = "Error al cancelar asiento", error = ex.Message });
            }
        }

        /// <summary>
        /// Aprobar un asiento que requiere aprobación
        /// </summary>
        [HttpPost("{id}/approve")]
        public async Task<ActionResult<JournalEntryDto>> ApproveJournalEntry(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var companyId = GetCompanyId();
                var userId = GetUserId();
                var currentUser = GetCurrentUser();

                var approved = await _journalEntryService.ApproveJournalEntryAsync(
                    companyId, id, userId, request.Notes ?? string.Empty, currentUser);

                return Ok(MapToDto(approved));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving journal entry {Id}", id);
                return StatusCode(500, new { message = "Error al aprobar asiento", error = ex.Message });
            }
        }

        // ===== VALIDACIONES =====

        /// <summary>
        /// Validar cuadre de un asiento
        /// </summary>
        [HttpPost("validate-balance")]
        public async Task<ActionResult<object>> ValidateBalance([FromBody] JournalEntryDto dto)
        {
            try
            {
                var entry = MapToEntity(dto);
                var (isBalanced, difference) = await _journalEntryService.ValidateBalanceAsync(entry);

                return Ok(new { isBalanced, difference });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating balance");
                return StatusCode(500, new { message = "Error al validar cuadre", error = ex.Message });
            }
        }

        /// <summary>
        /// Validar asiento completo
        /// </summary>
        [HttpPost("validate")]
        public async Task<ActionResult<object>> ValidateJournalEntry([FromBody] JournalEntryDto dto)
        {
            try
            {
                var companyId = GetCompanyId();
                var entry = MapToEntity(dto);
                var errors = await _journalEntryService.ValidateJournalEntryAsync(companyId, entry);

                return Ok(new { isValid = !errors.Any(), errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating journal entry");
                return StatusCode(500, new { message = "Error al validar asiento", error = ex.Message });
            }
        }

        // ===== MAPEO DTO ↔ ENTITY =====

        private JournalEntryDto MapToDto(JournalEntry entry)
        {
            return new JournalEntryDto
            {
                IdJournalEntry = entry.IdJournalEntry,
                EntryNumber = entry.EntryNumber,
                IdTypeAccounting = entry.IdTypeAccounting,
                IdJournalEntryClass = entry.IdJournalEntryClass,
                IdJournalEntryStatus = entry.IdJournalEntryStatus,
                Reference = entry.Reference,
                EntryDate = entry.EntryDate.ToString("yyyy-MM-dd"),
                PostingDate = entry.PostingDate.ToString("yyyy-MM-dd"),
                IdMenu = entry.IdMenu,
                CurrencyLocal = entry.CurrencyLocal,
                CurrencyExchange = entry.CurrencyExchange,
                Status = entry.Status,
                IsReversing = entry.IsReversing,
                IdReversedEntry = entry.IdReversedEntry,
                ReversalDate = entry.ReversalDate?.ToString("yyyy-MM-dd"),
                DebitTotal = entry.DebitTotal,
                CreditTotal = entry.CreditTotal,
                RequiresApproval = entry.RequiresApproval,
                ApprovedDate = entry.ApprovedDate,
                ApprovedByUserId = entry.ApprovedByUserId,
                ApprovalNotes = entry.ApprovalNotes,
                PostedDate = entry.PostedDate,
                PostedByUserId = entry.PostedByUserId,
                CancelledDate = entry.CancelledDate,
                CancelledByUserId = entry.CancelledByUserId,
                IdJournalEntryCancelReason = entry.IdJournalEntryCancelReason,
                Lines = entry.Lines?.Select(MapLineToDto).ToList() ?? new List<JournalEntryLineDto>()
            };
        }

        private JournalEntryLineDto MapLineToDto(JournalEntryLine line)
        {
            return new JournalEntryLineDto
            {
                IdJournalEntry      = line.IdJournalEntry,
                IdJournalEntryLine  = line.IdJournalEntryLine,
                IdChartOfAccounts   = line.IdChartOfAccounts,
                Description         = line.Description,
                Reference           = line.Reference,
                DebitAmount         = line.DebitAmount,
                CreditAmount        = line.CreditAmount,
                IdCostCenter                = line.IdCostCenter,
                CostCenterName              = line.CostCenter?.Name,
                IdJournalEntryTypeOrigin    = line.IdJournalEntryTypeOrigin,
                IdDocumentOrigin            = line.IdDocumentOrigin
            };
        }

        private JournalEntry MapToEntity(JournalEntryDto dto)
        {
            var entry = new JournalEntry
            {
                IdJournalEntry = dto.IdJournalEntry,
                EntryNumber = dto.EntryNumber ?? string.Empty,
                IdTypeAccounting = dto.IdTypeAccounting,
                IdJournalEntryClass = dto.IdJournalEntryClass,
                IdJournalEntryStatus = dto.IdJournalEntryStatus,
                Reference = dto.Reference,
                EntryDate = DateOnly.Parse(dto.EntryDate ?? DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd")),
                PostingDate = DateOnly.Parse(dto.PostingDate ?? DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd")),
                IdMenu = dto.IdMenu,
                CurrencyLocal = dto.CurrencyLocal,
                CurrencyExchange = dto.CurrencyExchange,
                IsReversing = dto.IsReversing,
                IdReversedEntry = dto.IdReversedEntry,
                ReversalDate = string.IsNullOrWhiteSpace(dto.ReversalDate) ? null : DateOnly.Parse(dto.ReversalDate),
                RequiresApproval = dto.RequiresApproval
            };

            if (dto.Lines != null)
            {
                entry.Lines = dto.Lines.Select(MapLineToEntity).ToList();
            }

            return entry;
        }

        private JournalEntryLine MapLineToEntity(JournalEntryLineDto dto)
        {
            return new JournalEntryLine
            {
                IdJournalEntry     = dto.IdJournalEntry,
                IdJournalEntryLine = dto.IdJournalEntryLine,
                IdChartOfAccounts  = dto.IdChartOfAccounts,
                Description        = dto.Description ?? string.Empty,
                Reference          = dto.Reference ?? string.Empty,
                DebitAmount        = dto.DebitAmount,
                CreditAmount       = dto.CreditAmount,
                IdCostCenter                = dto.IdCostCenter,
                IdJournalEntryTypeOrigin    = dto.IdJournalEntryTypeOrigin,
                IdDocumentOrigin            = dto.IdDocumentOrigin
            };
        }
    }

    // ===== DTOs =====

    public class JournalEntryDto
    {
        public int IdJournalEntry { get; set; }
        public string? EntryNumber { get; set; }
        /// <summary>FK lógica cross-DB → cms.admin.type_accounting (default 1 = general)</summary>
        public int IdTypeAccounting { get; set; } = 1;
        /// <summary>FK lógica cross-DB → cms.admin.journal_entry_class (default 3 = Normal)</summary>
        public int IdJournalEntryClass { get; set; } = 3;
        /// <summary>FK lógica cross-DB → cms.admin.journal_entry_status (default 1 = Draft)</summary>
        public int IdJournalEntryStatus { get; set; } = 1;
        public string? Reference { get; set; }
        public string? EntryDate { get; set; }
        public string? PostingDate { get; set; }
        public int? IdMenu { get; set; }  // FK lógica a admin.menu
        public int CurrencyLocal { get; set; } = 33;   // ID moneda base (admin.currency)
        public int CurrencyExchange { get; set; } = 141; // ID moneda secundaria (admin.currency)
        public string? Status { get; set; }
        public bool IsReversing { get; set; }
        public int? IdReversedEntry { get; set; }
        public string? ReversalDate { get; set; }
        public decimal DebitTotal { get; set; }
        public decimal CreditTotal { get; set; }
        public bool RequiresApproval { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public int? ApprovedByUserId { get; set; }
        public string? ApprovalNotes { get; set; }
        public DateTime? PostedDate { get; set; }
        public int? PostedByUserId { get; set; }
        public DateTime? CancelledDate { get; set; }
        public int? CancelledByUserId { get; set; }
        public int? IdJournalEntryCancelReason { get; set; }
        public List<JournalEntryLineDto> Lines { get; set; } = new();
    }

    public class JournalEntryLineDto
    {
        public int IdJournalEntry { get; set; }
        public int IdJournalEntryLine { get; set; }
        public int IdChartOfAccounts { get; set; }
        public string? AccountCode { get; set; }   // Para display
        public string? AccountName { get; set; }   // Para display
        public string? Description { get; set; }
        public string? Reference { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public int IdCostCenter { get; set; }
        public string? CostCenterName { get; set; } // Para display
        /// <summary>FK lógica cross-DB → cms.admin.journal_entry_type_origin</summary>
        public int IdJournalEntryTypeOrigin { get; set; }
        public int IdDocumentOrigin { get; set; }
    }

    public class ReversalRequest
    {
        public string? ReversalDate { get; set; }
        public int IdCancelReason { get; set; }
        public int IdMenu { get; set; } // ⭐ Menú desde donde se hace la reversión
    }

    public class CancelRequest
    {
        public int IdCancelReason { get; set; }
    }

    public class ApprovalRequest
    {
        public string? Notes { get; set; }
    }
}
