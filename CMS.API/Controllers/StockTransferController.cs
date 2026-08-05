// ================================================================================
// ARCHIVO: CMS.API/Controllers/StockTransferController.cs
// PROPÓSITO: API REST para gestión de traslados de stock entre bodegas
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-12
// ================================================================================

using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class StockTransferController : ControllerBase
    {
        private readonly IStockTransferService _service;
        private readonly ILogger<StockTransferController> _logger;

        public StockTransferController(IStockTransferService service, ILogger<StockTransferController> logger)
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
            return User.FindFirstValue("cms_username") ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? "SYSTEM";
        }

        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst("userId")?.Value ?? User.FindFirst("UserId")?.Value;
            if (int.TryParse(idClaim, out var id)) return id;
            return 0;
        }

        // ================================================================
        // GET /api/stocktransfer
        // ================================================================
        [HttpGet]
        public async Task<IActionResult> GetTransfers(
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] int? warehouseOriginId = null,
            [FromQuery] int? warehouseDestId = null,
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var companyId = GetCurrentCompanyId();

                DateOnly? from = null, to = null;
                if (DateOnly.TryParse(dateFrom, out var df)) from = df;
                if (DateOnly.TryParse(dateTo, out var dt)) to = dt;

                var (items, total) = await _service.GetTransfersAsync(
                    companyId, search, status, warehouseOriginId, warehouseDestId, from, to, page, pageSize);

                return Ok(new
                {
                    items = items.Select(t => MapToDto(t)),
                    totalCount = total,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stock transfers");
                return StatusCode(500, new { error = "Error al obtener traslados", detail = ex.Message });
            }
        }

        // ================================================================
        // GET /api/stocktransfer/{id}
        // ================================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var transfer = await _service.GetByIdAsync(companyId, id);
                if (transfer == null) return NotFound(new { error = "Traslado no encontrado" });
                return Ok(MapToDto(transfer, includeLines: true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stock transfer {Id}", id);
                return StatusCode(500, new { error = "Error al obtener traslado", detail = ex.Message });
            }
        }

        // ================================================================
        // GET /api/stocktransfer/next-number
        // ================================================================
        [HttpGet("next-number")]
        public async Task<IActionResult> GetNextNumber()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var number = await _service.GenerateTransferNumberAsync(companyId);
                return Ok(new { transferNumber = number });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating transfer number");
                return StatusCode(500, new { error = "Error generando número", detail = ex.Message });
            }
        }

        // ================================================================
        // POST /api/stocktransfer
        // ================================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] StockTransferUpsertDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var transfer = MapFromUpsertDto(dto);
                transfer.RequestedBy = userId > 0 ? userId : dto.RequestedBy;

                var created = await _service.CreateAsync(companyId, transfer, user);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating stock transfer");
                return StatusCode(500, new { error = "Error al crear traslado", detail = ex.Message });
            }
        }

        // ================================================================
        // PUT /api/stocktransfer/{id}
        // ================================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] StockTransferUpsertDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();

                var transfer = MapFromUpsertDto(dto);
                transfer.Id = id;

                var updated = await _service.UpdateAsync(companyId, transfer, user);
                return Ok(MapToDto(updated));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock transfer {Id}", id);
                return StatusCode(500, new { error = "Error al actualizar traslado", detail = ex.Message });
            }
        }

        // ================================================================
        // POST /api/stocktransfer/{id}/approve
        // ================================================================
        [HttpPost("{id:int}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var transfer = await _service.ApproveAsync(companyId, id, userId, user);
                return Ok(MapToDto(transfer));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving stock transfer {Id}", id);
                return StatusCode(500, new { error = "Error al aprobar traslado", detail = ex.Message });
            }
        }

        // ================================================================
        // POST /api/stocktransfer/{id}/complete
        // ================================================================
        [HttpPost("{id:int}/complete")]
        public async Task<IActionResult> Complete(int id, [FromBody] CompleteTransferDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var lines = dto.Lines.Select(l => new StockTransferLine
                {
                    Id = l.Id,
                    QtyTransferred = l.QtyTransferred
                }).ToList();

                var transfer = await _service.CompleteAsync(companyId, id, lines, userId, user);
                return Ok(MapToDto(transfer));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing stock transfer {Id}", id);
                return StatusCode(500, new { error = "Error al completar traslado", detail = ex.Message });
            }
        }

        // ================================================================
        // POST /api/stocktransfer/{id}/cancel
        // ================================================================
        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelTransferDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();

                var transfer = await _service.CancelAsync(companyId, id, dto.CancelReason ?? string.Empty, user);
                return Ok(MapToDto(transfer));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling stock transfer {Id}", id);
                return StatusCode(500, new { error = "Error al cancelar traslado", detail = ex.Message });
            }
        }

        // ================================================================
        // DTOs & MAPPERS
        // ================================================================
        private static object MapToDto(StockTransfer t, bool includeLines = false)
        {
            var dto = new
            {
                id = t.Id,
                transferNumber = t.TransferNumber,
                reference = t.Reference,
                notes = t.Notes,
                idWarehouseOrigin = t.IdWarehouseOrigin,
                idWarehouseDest = t.IdWarehouseDest,
                originWarehouseName = t.OriginWarehouseName,
                destWarehouseName = t.DestWarehouseName,
                status = t.Status,
                transferDate = t.TransferDate.ToString("yyyy-MM-dd"),
                expectedDate = t.ExpectedDate?.ToString("yyyy-MM-dd"),
                completedDate = t.CompletedDate,
                cancelledDate = t.CancelledDate,
                requestedBy = t.RequestedBy,
                requestedByName = t.RequestedByName,
                approvedBy = t.ApprovedBy,
                approvedByName = t.ApprovedByName,
                executedBy = t.ExecutedBy,
                executedByName = t.ExecutedByName,
                cancelReason = t.CancelReason,
                createDate = t.CreateDate,
                lines = includeLines ? t.Lines.Select(l => new
                {
                    id = l.Id,
                    lineNumber = l.LineNumber,
                    idItem = l.IdItem,
                    itemCode = l.ItemCode,
                    itemName = l.ItemName,
                    qtyRequested = l.QtyRequested,
                    qtyTransferred = l.QtyTransferred,
                    unitOfMeasureCode = l.UnitOfMeasureCode,
                    lotNumber = l.LotNumber,
                    expiryDate = l.ExpiryDate?.ToString("yyyy-MM-dd"),
                    notes = l.Notes
                }) : null
            };
            return dto;
        }

        private static StockTransfer MapFromUpsertDto(StockTransferUpsertDto dto)
        {
            var transfer = new StockTransfer
            {
                TransferNumber = dto.TransferNumber,
                Reference = dto.Reference,
                Notes = dto.Notes,
                IdWarehouseOrigin = dto.IdWarehouseOrigin,
                IdWarehouseDest = dto.IdWarehouseDest,
                RequestedBy = dto.RequestedBy,
                Lines = dto.Lines.Select((l, i) => new StockTransferLine
                {
                    IdItem = l.IdItem,
                    ItemCode = l.ItemCode,
                    ItemName = l.ItemName,
                    QtyRequested = l.QtyRequested,
                    IdUnitOfMeasure = l.IdUnitOfMeasure,
                    UnitOfMeasureCode = l.UnitOfMeasureCode,
                    LotNumber = l.LotNumber,
                    Notes = l.Notes
                }).ToList()
            };

            if (DateOnly.TryParse(dto.TransferDate, out var td)) transfer.TransferDate = td;
            if (DateOnly.TryParse(dto.ExpectedDate, out var ed)) transfer.ExpectedDate = ed;

            return transfer;
        }
    }

    // ================================================================
    // DTO CLASSES
    // ================================================================
    public class StockTransferUpsertDto
    {
        public string TransferNumber { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public int IdWarehouseOrigin { get; set; }
        public int IdWarehouseDest { get; set; }
        public string TransferDate { get; set; } = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        public string? ExpectedDate { get; set; }
        public int RequestedBy { get; set; }
        public List<StockTransferLineUpsertDto> Lines { get; set; } = [];
    }

    public class StockTransferLineUpsertDto
    {
        public int IdItem { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal QtyRequested { get; set; }
        public int? IdUnitOfMeasure { get; set; }
        public string? UnitOfMeasureCode { get; set; }
        public string? LotNumber { get; set; }
        public string? Notes { get; set; }
    }

    public class CompleteTransferDto
    {
        public List<CompleteLineDto> Lines { get; set; } = [];
    }

    public class CompleteLineDto
    {
        public int Id { get; set; }
        public decimal QtyTransferred { get; set; }
    }

    public class CancelTransferDto
    {
        public string? CancelReason { get; set; }
    }
}
