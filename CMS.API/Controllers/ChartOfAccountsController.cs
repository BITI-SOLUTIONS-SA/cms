// ================================================================================
// ARCHIVO: CMS.API/Controllers/ChartOfAccountsController.cs
// PROPÓSITO: API REST para gestión del Plan de Cuentas (Chart of Accounts)
// DESCRIPCIÓN: CRUD de cuentas contables con validación de jerarquía tipo SAP
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-20
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
    [Route("api/chart-of-accounts")]
    public class ChartOfAccountsController : ControllerBase
    {
        private readonly IChartOfAccountsService _service;
        private readonly ILogger<ChartOfAccountsController> _logger;

        public ChartOfAccountsController(
            IChartOfAccountsService service,
            ILogger<ChartOfAccountsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int GetCurrentCompanyId()
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(companyIdClaim, out var companyId)) return companyId;
            throw new UnauthorizedAccessException("companyId no encontrado en el token JWT");
        }

        private string GetCurrentUser()
        {
            return User.FindFirstValue("cms_username")
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? "SYSTEM";
        }

        // ============================================================
        // GET /api/chart-of-accounts
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetAccounts(
            [FromQuery] string? search = null,
            [FromQuery] int? idChartOfAccountsType = null,
            [FromQuery] bool? isDetail = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var (items, total) = await _service.GetAccountsAsync(
                    companyId, search, idChartOfAccountsType, isDetail, isActive, page, pageSize);

                return Ok(new
                {
                    items = items.Select(MapToDto),
                    totalCount = total,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cuentas contables");
                return StatusCode(500, new { error = "Error al obtener cuentas contables", detail = ex.Message });
            }
        }

        // ============================================================
        // GET /api/chart-of-accounts/hierarchy
        // ============================================================
        [HttpGet("hierarchy")]
        public async Task<IActionResult> GetHierarchy([FromQuery] int? parentId = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var accounts = await _service.GetAccountHierarchyAsync(companyId, parentId);
                return Ok(accounts.Select(MapToDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener jerarquía de cuentas");
                return StatusCode(500, new { error = "Error al obtener jerarquía", detail = ex.Message });
            }
        }

        // ============================================================
        // GET /api/chart-of-accounts/detail
        // ============================================================
        [HttpGet("detail")]
        public async Task<IActionResult> GetDetailAccounts()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var accounts = await _service.GetDetailAccountsAsync(companyId);
                return Ok(accounts.Select(MapToDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cuentas de detalle");
                return StatusCode(500, new { error = "Error al obtener cuentas de detalle", detail = ex.Message });
            }
        }

        // ============================================================
        // GET /api/chart-of-accounts/{id}
        // ============================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAccountById(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var account = await _service.GetAccountByIdAsync(companyId, id);

                if (account == null)
                    return NotFound(new { error = "Cuenta no encontrada" });

                return Ok(MapToDto(account));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cuenta {Id}", id);
                return StatusCode(500, new { error = "Error al obtener cuenta", detail = ex.Message });
            }
        }

        // ============================================================
        // GET /api/chart-of-accounts/by-code/{code}
        // ============================================================
        [HttpGet("by-code/{code}")]
        public async Task<IActionResult> GetAccountByCode(string code)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var account = await _service.GetAccountByCodeAsync(companyId, code);

                if (account == null)
                    return NotFound(new { error = "Cuenta no encontrada" });

                return Ok(MapToDto(account));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cuenta por código {Code}", code);
                return StatusCode(500, new { error = "Error al obtener cuenta", detail = ex.Message });
            }
        }

        // ============================================================
        // POST /api/chart-of-accounts
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> CreateAccount([FromBody] ChartOfAccountsDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var currentUser = GetCurrentUser();

                var account = MapFromDto(dto, currentUser);
                var created = await _service.CreateAccountAsync(companyId, account);

                _logger.LogInformation("Cuenta {Code} creada por {User}", created.Code, currentUser);
                return CreatedAtAction(nameof(GetAccountById), new { id = created.IdChartOfAccounts }, MapToDto(created));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear cuenta");
                return StatusCode(500, new { error = "Error al crear cuenta", detail = ex.Message });
            }
        }

        // ============================================================
        // PUT /api/chart-of-accounts/{id}
        // ============================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] ChartOfAccountsDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var currentUser = GetCurrentUser();

                var account = MapFromDto(dto, currentUser);
                account.IdChartOfAccounts = id;

                var updated = await _service.UpdateAccountAsync(companyId, account);

                _logger.LogInformation("Cuenta {Code} actualizada por {User}", updated.Code, currentUser);
                return Ok(MapToDto(updated));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar cuenta {Id}", id);
                return StatusCode(500, new { error = "Error al actualizar cuenta", detail = ex.Message });
            }
        }

        // ============================================================
        // DELETE /api/chart-of-accounts/{id}
        // ============================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await _service.DeleteAccountAsync(companyId, id);

                _logger.LogInformation("Cuenta {Id} eliminada por {User}", id, GetCurrentUser());
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar cuenta {Id}", id);
                return StatusCode(500, new { error = "Error al eliminar cuenta", detail = ex.Message });
            }
        }

        // ============================================================
        // GET /api/chart-of-accounts/code-exists
        // ============================================================
        [HttpGet("code-exists")]
        public async Task<IActionResult> CheckCodeExists(
            [FromQuery] string code,
            [FromQuery] int? excludeId = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var exists = await _service.CodeExistsAsync(companyId, code, excludeId);
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar código de cuenta");
                return StatusCode(500, new { error = "Error al verificar código", detail = ex.Message });
            }
        }

        // ============================================================
        // MAPEO DTO
        // ============================================================

        private static ChartOfAccountsDto MapToDto(ChartOfAccounts entity)
        {
            return new ChartOfAccountsDto
            {
                IdChartOfAccounts = entity.IdChartOfAccounts,
                Code = entity.Code,
                Name = entity.Name,
                Description = entity.Description,
                Alias = entity.Alias,
                IdParentAccount = entity.IdParentAccount,
                AccountLevel = entity.AccountLevel,
                IsHeader = entity.IsHeader,
                IsDetail = entity.IsDetail,
                HasChildren = entity.HasChildren,
                IdChartOfAccountsType = entity.IdChartOfAccountsType,
                AccountClass = entity.AccountClass,
                NormalBalance = entity.NormalBalance,
                IsDebitBalance = entity.IsDebitBalance,
                AcceptsManualEntry = entity.AcceptsManualEntry,
                AcceptsAutoEntry = entity.AcceptsAutoEntry,
                RequiresCostCenter = entity.RequiresCostCenter,
                RequiresProject = entity.RequiresProject,
                RequiresPartner = entity.RequiresPartner,
                CurrencyCode = entity.CurrencyCode,
                AllowsMultiCurrency = entity.AllowsMultiCurrency,
                IsReconciliation = entity.IsReconciliation,
                TaxCode = entity.TaxCode,
                IsTaxRelevant = entity.IsTaxRelevant,
                IsReceivable = entity.IsReceivable,
                IsPayable = entity.IsPayable,
                CashFlowCategory = entity.CashFlowCategory,
                FinancialStatement = entity.FinancialStatement,
                ReportLineItem = entity.ReportLineItem,
                SortOrder = entity.SortOrder,
                EffectiveDate = entity.EffectiveDate,
                ExpirationDate = entity.ExpirationDate,
                IsActive = entity.IsActive,
                IsBlocked = entity.IsBlocked,
                BlockReason = entity.BlockReason,
                Notes = entity.Notes,
                CreateDate = entity.CreateDate,
                RecordDate = entity.RecordDate,
                CreatedBy = entity.CreatedBy,
                UpdatedBy = entity.UpdatedBy
            };
        }

        private static ChartOfAccounts MapFromDto(ChartOfAccountsDto dto, string currentUser)
        {
            return new ChartOfAccounts
            {
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                Alias = dto.Alias,
                IdParentAccount = dto.IdParentAccount,
                AccountLevel = dto.AccountLevel,
                IsHeader = dto.IsHeader,
                IsDetail = dto.IsDetail,
                IdChartOfAccountsType = dto.IdChartOfAccountsType,
                AccountClass = dto.AccountClass,
                NormalBalance = dto.NormalBalance,
                IsDebitBalance = dto.IsDebitBalance,
                AcceptsManualEntry = dto.AcceptsManualEntry,
                AcceptsAutoEntry = dto.AcceptsAutoEntry,
                RequiresCostCenter = dto.RequiresCostCenter,
                RequiresProject = dto.RequiresProject,
                RequiresPartner = dto.RequiresPartner,
                CurrencyCode = dto.CurrencyCode,
                AllowsMultiCurrency = dto.AllowsMultiCurrency,
                IsReconciliation = dto.IsReconciliation,
                TaxCode = dto.TaxCode,
                IsTaxRelevant = dto.IsTaxRelevant,
                IsReceivable = dto.IsReceivable,
                IsPayable = dto.IsPayable,
                CashFlowCategory = dto.CashFlowCategory,
                FinancialStatement = dto.FinancialStatement,
                ReportLineItem = dto.ReportLineItem,
                SortOrder = dto.SortOrder,
                EffectiveDate = dto.EffectiveDate,
                ExpirationDate = dto.ExpirationDate,
                IsActive = dto.IsActive,
                IsBlocked = dto.IsBlocked,
                BlockReason = dto.BlockReason,
                Notes = dto.Notes,
                CreatedBy = currentUser,
                UpdatedBy = currentUser
            };
        }
    }

    // ============================================================
    // DTO
    // ============================================================

    public class ChartOfAccountsDto
    {
        public int IdChartOfAccounts { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Alias { get; set; }
        public int? IdParentAccount { get; set; }
        public int AccountLevel { get; set; } = 1;
        public bool IsHeader { get; set; } = false;
        public bool IsDetail { get; set; } = true;
        public bool HasChildren { get; set; } = false;
        /// <summary>FK lógica cross-DB → cms.admin.chart_of_accounts_type (1=asset, 2=liability, 3=equity, 4=revenue, 5=expense, 6=off_balance)</summary>
        public int IdChartOfAccountsType { get; set; } = 1;
        public string? AccountClass { get; set; }
        public string NormalBalance { get; set; } = "Debit";
        public bool IsDebitBalance { get; set; } = true;
        public bool AcceptsManualEntry { get; set; } = true;
        public bool AcceptsAutoEntry { get; set; } = true;
        public bool RequiresCostCenter { get; set; } = false;
        public bool RequiresProject { get; set; } = false;
        public bool RequiresPartner { get; set; } = false;
        public string CurrencyCode { get; set; } = "CRC";
        public bool AllowsMultiCurrency { get; set; } = false;
        public bool IsReconciliation { get; set; } = false;
        public string? TaxCode { get; set; }
        public bool IsTaxRelevant { get; set; } = false;
        public bool IsReceivable { get; set; } = false;
        public bool IsPayable { get; set; } = false;
        public string? CashFlowCategory { get; set; }
        public string? FinancialStatement { get; set; }
        public string? ReportLineItem { get; set; }
        public int SortOrder { get; set; } = 0;
        public DateOnly? EffectiveDate { get; set; }
        public DateOnly? ExpirationDate { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsBlocked { get; set; } = false;
        public string? BlockReason { get; set; }
        public string? Notes { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime RecordDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = string.Empty;
    }
}
