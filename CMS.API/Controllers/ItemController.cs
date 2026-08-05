// ================================================================================
// ARCHIVO: CMS.API/Controllers/ItemController.cs
// PROPÓSITO: API REST para gestionar artículos en las BD operacionales
// DESCRIPCIÓN: CRUD de artículos usando la BD de la compañía activa del usuario
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-19
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
    public class ItemController : ControllerBase
    {
        private readonly IItemService _itemService;
        private readonly ICompanyConsecutiveService _companyConsecutiveService;
        private readonly ILogger<ItemController> _logger;

        public ItemController(
            IItemService itemService,
            ICompanyConsecutiveService companyConsecutiveService,
            ILogger<ItemController> logger)
        {
            _itemService = itemService;
            _companyConsecutiveService = companyConsecutiveService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene la compañía activa del token JWT
        /// </summary>
        private int GetCurrentCompanyId()
        {
            // El JWT usa "companyId" (minúsculas)
            var companyIdClaim = User.FindFirst("companyId")?.Value 
                              ?? User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(companyIdClaim, out var companyId))
            {
                return companyId;
            }
            throw new UnauthorizedAccessException("companyId no encontrado en el token JWT");
        }

        /// <summary>
        /// Obtiene el usuario actual
        /// </summary>
        private string? GetCurrentUser()
        {
            // El JWT local usa JwtRegisteredClaimNames.Name que se mapea como "name"
            // También verificar ClaimTypes.Name para compatibilidad con otros esquemas
            return User.FindFirstValue("cms_username") ??
                   User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst("preferred_username")?.Value ??
                   User.FindFirst("name")?.Value;
        }

        /// <summary>
        /// Obtiene el ID del usuario actual del token JWT
        /// </summary>
        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value 
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                           ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
            return null;
        }

        /// <summary>
        /// Lista artículos con filtros y paginación
        /// GET: api/item?search=xxx&classificationGroup=1&classificationId=5&isActive=true&dateFrom=2024-01-01&dateTo=2024-12-31&page=1&pageSize=20
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ItemListResponse>> GetItems(
            [FromQuery] string? search = null,
            [FromQuery] int? classificationGroup = null,
            [FromQuery] int? classificationId = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var (items, totalCount) = await _itemService.GetItemsAsync(
                    companyId, search, classificationGroup, classificationId, isActive, dateFrom, dateTo, page, pageSize);

                return Ok(new ItemListResponse
                {
                    Items = items.Select(MapToDto).ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo artículos");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Lista SOLO artículos de etiqueta (is_label_item = true)
        /// GET: api/item/labels?search=xxx&page=1&pageSize=20
        /// </summary>
        [HttpGet("labels")]
        public async Task<ActionResult<ItemListResponse>> GetLabelItems(
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? orderBy = "sale_price")
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var (items, totalCount) = await _itemService.GetLabelItemsAsync(
                    companyId, search, page, pageSize, orderBy);

                return Ok(new ItemListResponse
                {
                    Items = items.Select(MapToDto).ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo artículos de etiqueta");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Actualiza SOLO los campos de etiqueta de un artículo
        /// PUT: api/item/{id}/label
        /// </summary>
        [HttpPut("{id}/label")]
        public async Task<ActionResult<ItemDto>> UpdateLabelInfo(int id, [FromBody] UpdateLabelRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var updated = await _itemService.UpdateLabelInfoAsync(
                    companyId, id, 
                    request.LabelItem, 
                    request.LabelPrice, 
                    request.LabelItemBarcode,
                    request.PrintLabelName,
                    request.PrintLabelPrice,
                    request.PrintLabelBarcode,
                    request.LabelWidthCm,
                    request.LabelHeightCm,
                    request.LabelOrientation,
                    request.PrintLabelBorder,
                    request.LabelBorderColor,
                    request.LabelNameColor,
                    request.LabelPriceColor,
                    request.LabelBarcodeColor,
                    request.LabelFontSize,
                    request.LabelFontFamily,
                    request.LabelPriceDecimals,
                    request.LabelThousandSeparator,
                    request.LabelCurrencySymbol,
                    request.PrintCurrencySymbol,
                    user,
                    userId);

                if (updated == null)
                {
                    return NotFound(new { message = "Artículo no encontrado" });
                }

                return Ok(MapToDto(updated));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando etiqueta del artículo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene un artículo por ID
        /// GET: api/item/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ItemDto>> GetItem(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var item = await _itemService.GetItemByIdAsync(companyId, id);

                if (item == null)
                {
                    return NotFound(new { message = "Artículo no encontrado" });
                }

                return Ok(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo artículo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene un artículo por código
        /// GET: api/item/code/{code}
        /// </summary>
        [HttpGet("code/{code}")]
        public async Task<ActionResult<ItemDto>> GetItemByCode(string code)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var item = await _itemService.GetItemByCodeAsync(companyId, code);

                if (item == null)
                {
                    return NotFound(new { message = "Artículo no encontrado" });
                }

                return Ok(MapToDto(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo artículo por código {Code}", code);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Lista ítems para el modal de emisión de comprobantes, filtrados por cliente.
        /// Devuelve solo los campos requeridos (código, nombre, precio, CAByS, IVA).
        /// GET: api/item/for-billing?customerId=1&search=xxx
        /// </summary>
        [HttpGet("for-billing")]
        public async Task<ActionResult<List<BillingItemDto>>> GetItemsForBilling(
            [FromQuery] int? customerId = null,
            [FromQuery] string? search = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                // Reutiliza el listado paginado del servicio (primera página amplia).
                var (items, _) = await _itemService.GetItemsAsync(
                    companyId, search, null, null, true, null, null, 1, 200);

                var result = items
                    .Where(i => i.IsSellable && (customerId == null || i.IdCustomer == customerId.Value))
                    .Select(i => new BillingItemDto
                    {
                        Id = i.Id,
                        Code = i.Code,
                        Name = i.Name,
                        Description = i.Description,
                        SalePrice = i.SalePrice,
                        TaxRate = i.TaxRate,
                        TaxRateCode = i.TaxRateCode,
                        CabysCode = i.CabysCode,
                        IdCustomer = i.IdCustomer
                    })
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo ítems para facturación");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Crea un nuevo artículo
        /// POST: api/item
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ItemDto>> CreateItem([FromBody] CreateItemRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();

                // Verificar si ya existe un artículo con ese código
                var existing = await _itemService.GetItemByCodeAsync(companyId, request.Code);
                if (existing != null)
                {
                    return BadRequest(new { message = $"Ya existe un artículo con el código {request.Code}" });
                }

                var item = new Item
                {
                    Code = request.Code,
                    Name = request.Name,
                    Description = request.Description,
                    Barcode = request.Barcode,
                    IdClassification1 = request.IdClassification1,
                    IdClassification2 = request.IdClassification2,
                    IdClassification3 = request.IdClassification3,
                    IdClassification4 = request.IdClassification4,
                    IdClassification5 = request.IdClassification5,
                    IdClassification6 = request.IdClassification6,
                    Brand = request.Brand,
                    IdUnitOfMeasure = request.IdUnitOfMeasure,
                    CostPrice = request.CostPrice,
                    SalePrice = request.SalePrice,
                    TaxRate = request.TaxRate,
                    MinStock = request.MinStock,
                    MaxStock = request.MaxStock,
                    CurrentStock = request.CurrentStock,
                    ImageUrl = request.ImageUrl,
                    IsActive = request.IsActive ?? true,
                    IsSellable = request.IsSellable ?? true,
                    IsPurchasable = request.IsPurchasable ?? true,
                    TrackLots = request.TrackLots ?? false,
                    TrackSerialNumbers = request.TrackSerialNumbers ?? false,

                    // Facturación electrónica (Fase B)
                    IdCustomer = request.IdCustomer ?? 1,
                    TaxRateCode = string.IsNullOrWhiteSpace(request.TaxRateCode) ? "08" : request.TaxRateCode,
                    CabysCode = string.IsNullOrWhiteSpace(request.CabysCode) ? "9799000000000" : request.CabysCode,

                    // Label Item fields
                    LabelItem = request.LabelItem,
                    LabelPrice = request.LabelPrice,
                    LabelItemBarcode = request.LabelItemBarcode,
                    IsLabelItem = request.IsLabelItem
                };

                var created = await _itemService.CreateItemAsync(companyId, item, user);

                return CreatedAtAction(nameof(GetItem), new { id = created.Id }, MapToDto(created));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando artículo");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Actualiza un artículo
        /// PUT: api/item/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ItemDto>> UpdateItem(int id, [FromBody] UpdateItemRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var item = new Item
                {
                    Id = id,
                    Code = request.Code,
                    Name = request.Name,
                    Description = request.Description,
                    Barcode = request.Barcode,
                    IdClassification1 = request.IdClassification1,
                    IdClassification2 = request.IdClassification2,
                    IdClassification3 = request.IdClassification3,
                    IdClassification4 = request.IdClassification4,
                    IdClassification5 = request.IdClassification5,
                    IdClassification6 = request.IdClassification6,
                    Brand = request.Brand,
                    IdUnitOfMeasure = request.IdUnitOfMeasure,
                    CostPrice = request.CostPrice,
                    SalePrice = request.SalePrice,
                    TaxRate = request.TaxRate,
                    MinStock = request.MinStock,
                    MaxStock = request.MaxStock,
                    CurrentStock = request.CurrentStock,
                    ImageUrl = request.ImageUrl,
                    IsActive = request.IsActive,
                    IsSellable = request.IsSellable,
                    IsPurchasable = request.IsPurchasable,
                    TrackLots = request.TrackLots,
                    TrackSerialNumbers = request.TrackSerialNumbers,

                    // Facturación electrónica (Fase B)
                    IdCustomer = request.IdCustomer ?? 1,
                    TaxRateCode = string.IsNullOrWhiteSpace(request.TaxRateCode) ? "08" : request.TaxRateCode,
                    CabysCode = string.IsNullOrWhiteSpace(request.CabysCode) ? "9799000000000" : request.CabysCode,

                    // Label Item fields
                    LabelItem = request.LabelItem,
                    LabelPrice = request.LabelPrice,
                    LabelItemBarcode = request.LabelItemBarcode,
                    IsLabelItem = request.IsLabelItem
                };

                var updated = await _itemService.UpdateItemAsync(companyId, item, user, userId);

                if (updated == null)
                {
                    return NotFound(new { message = "Artículo no encontrado" });
                }

                return Ok(MapToDto(updated));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando artículo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Elimina (desactiva) un artículo
        /// DELETE: api/item/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteItem(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var result = await _itemService.DeleteItemAsync(companyId, id, user, userId);

                if (!result)
                {
                    return NotFound(new { message = "Artículo no encontrado" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando artículo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene las unidades de medida disponibles
        /// GET: api/item/units-of-measure
        /// </summary>
        [HttpGet("units-of-measure")]
        public async Task<ActionResult<List<UnitOfMeasureDto>>> GetUnitsOfMeasure()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var units = await _itemService.GetUnitsOfMeasureAsync(companyId);
                var dtos = units.Select(u => new UnitOfMeasureDto
                {
                    Id = u.Id,
                    Code = u.Code,
                    Name = u.Name,
                    Symbol = u.Symbol,
                    AllowsDecimals = u.AllowsDecimals,
                    IsDefault = u.IsDefault
                }).ToList();
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo unidades de medida");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene las clasificaciones 1 disponibles (Categoría principal)
        /// GET: api/item/classifications/1
        /// </summary>
        [HttpGet("classifications/{level}")]
        public async Task<ActionResult<List<ClassificationDto>>> GetClassifications(int level)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var classifications = await _itemService.GetClassificationsAsync(companyId, level);
                return Ok(classifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo clasificaciones nivel {Level}", level);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene todas las clasificaciones (1-6) en una sola llamada
        /// GET: api/item/all-classifications
        /// </summary>
        [HttpGet("all-classifications")]
        public async Task<ActionResult<AllClassificationsResponse>> GetAllClassifications()
        {
            try
            {
                var companyId = GetCurrentCompanyId();

                // GetUnitsOfMeasureAsync ahora retorna UnitOfMeasureDto directamente
                var unitsOfMeasure = await _itemService.GetUnitsOfMeasureAsync(companyId);

                // GetClassificationsAsync ahora usa una sola tabla con classification_group
                var classifications1 = await _itemService.GetClassificationsAsync(companyId, 1);
                var classifications2 = await _itemService.GetClassificationsAsync(companyId, 2);
                var classifications3 = await _itemService.GetClassificationsAsync(companyId, 3);
                var classifications4 = await _itemService.GetClassificationsAsync(companyId, 4);
                var classifications5 = await _itemService.GetClassificationsAsync(companyId, 5);
                var classifications6 = await _itemService.GetClassificationsAsync(companyId, 6);

                var response = new AllClassificationsResponse
                {
                    UnitsOfMeasure = unitsOfMeasure.Select(u => new UnitOfMeasureDto 
                    { 
                        Id = u.Id, 
                        Code = u.Code, 
                        Name = u.Name, 
                        Symbol = u.Symbol, 
                        AllowsDecimals = u.AllowsDecimals, 
                        IsDefault = u.IsDefault 
                    }).ToList(),
                    Classifications1 = classifications1.Select(c => new ClassificationDto 
                    { 
                        Id = c.Id, 
                        Code = c.Code, 
                        Name = c.Name, 
                        Description = c.Description 
                    }).ToList(),
                    Classifications2 = classifications2.Select(c => new ClassificationDto 
                    { 
                        Id = c.Id, 
                        Code = c.Code, 
                        Name = c.Name, 
                        Description = c.Description 
                    }).ToList(),
                    Classifications3 = classifications3.Select(c => new ClassificationDto 
                    { 
                        Id = c.Id, 
                        Code = c.Code, 
                        Name = c.Name, 
                        Description = c.Description 
                    }).ToList(),
                    Classifications4 = classifications4.Select(c => new ClassificationDto 
                    { 
                        Id = c.Id, 
                        Code = c.Code, 
                        Name = c.Name, 
                        Description = c.Description 
                    }).ToList(),
                    Classifications5 = classifications5.Select(c => new ClassificationDto 
                    { 
                        Id = c.Id, 
                        Code = c.Code, 
                        Name = c.Name, 
                        Description = c.Description 
                    }).ToList(),
                    Classifications6 = classifications6.Select(c => new ClassificationDto 
                    { 
                        Id = c.Id, 
                        Code = c.Code, 
                        Name = c.Name, 
                        Description = c.Description 
                    }).ToList()
                };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo todas las clasificaciones");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Registra una impresión de etiqueta en el historial
        /// POST: api/item/{id}/print
        /// </summary>
        [HttpPost("{id}/print")]
        public async Task<ActionResult<LabelPrintHistoryDto>> RecordLabelPrint(int id, [FromBody] RecordPrintRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();

                // Obtener el artículo para validar y obtener datos
                var item = await _itemService.GetItemByIdAsync(companyId, id);
                if (item == null)
                {
                    return NotFound(new { message = "Artículo no encontrado" });
                }

                var printHistory = await _itemService.RecordLabelPrintAsync(
                    companyId,
                    id,
                    item.Code,
                    item.Name,
                    request.LabelItem ?? item.LabelItem ?? item.Name,
                    request.LabelPrice,
                    request.LabelItemBarcode,
                    request.PrintLabelName,
                    request.PrintLabelPrice,
                    request.PrintLabelBarcode,
                    request.PrintLabelBorder,
                    request.PrintCurrencySymbol,
                    request.LabelWidthCm,
                    request.LabelHeightCm,
                    request.LabelOrientation,
                    request.LabelBorderColor,
                    request.LabelNameColor,
                    request.LabelPriceColor,
                    request.LabelBarcodeColor,
                    request.LabelFontSize,
                    request.LabelFontFamily,
                    request.LabelPriceDecimals,
                    request.LabelThousandSeparator,
                    request.LabelCurrencySymbol,
                    request.FormattedPrice,
                    request.QuantityPrinted,
                    request.ContainerNumber,
                    user,
                    request.PrinterName,
                    request.PrintNotes
                );

                return Ok(new LabelPrintHistoryDto
                {
                    Id = printHistory.Id,
                    IdItem = printHistory.IdItem,
                    ItemCode = printHistory.ItemCode,
                    ItemName = printHistory.ItemName,
                    LabelItem = printHistory.LabelItem,
                    LabelPrice = printHistory.LabelPrice,
                    FormattedPrice = printHistory.FormattedPrice,
                    QuantityPrinted = printHistory.QuantityPrinted,
                    ContainerNumber = printHistory.ContainerNumber,
                    PrintDate = printHistory.PrintDate,
                    PrintedBy = printHistory.PrintedBy
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando impresión de etiqueta para artículo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene el historial de impresiones de etiquetas
        /// GET: api/item/print-history
        /// </summary>
        [HttpGet("print-history")]
        public async Task<ActionResult<List<LabelPrintHistoryDto>>> GetLabelPrintHistory(
            [FromQuery] int? itemId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? printedBy = null,
            [FromQuery] string? containerNumber = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var history = await _itemService.GetLabelPrintHistoryAsync(
                    companyId, itemId, fromDate, toDate, printedBy, containerNumber, page, pageSize);

                var dtos = history.Select(h => new LabelPrintHistoryDto
                {
                    Id = h.Id,
                    IdItem = h.IdItem,
                    ItemCode = h.ItemCode,
                    ItemName = h.ItemName,
                    LabelItem = h.LabelItem,
                    LabelPrice = h.LabelPrice,
                    FormattedPrice = h.FormattedPrice,
                    QuantityPrinted = h.QuantityPrinted,
                    ContainerNumber = h.ContainerNumber,
                    PrintDate = h.PrintDate,
                    PrintedBy = h.PrintedBy
                }).ToList();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo historial de impresiones");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        // ===== COMPANY CONSECUTIVE (Container Number) =====

        /// <summary>
        /// Obtiene el container_number actual de la compañía
        /// GET: api/item/container-number
        /// </summary>
        [HttpGet("container-number")]
        public async Task<ActionResult<ContainerNumberResponse>> GetContainerNumber()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var containerNumber = await _companyConsecutiveService.GetContainerNumberAsync(companyId);

                return Ok(new ContainerNumberResponse
                {
                    ContainerNumber = containerNumber,
                    CompanyId = companyId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo container_number");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Actualiza el container_number de la compañía
        /// PUT: api/item/container-number
        /// </summary>
        [HttpPut("container-number")]
        public async Task<ActionResult> UpdateContainerNumber([FromBody] UpdateContainerNumberRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser() ?? "SYSTEM";

                var success = await _companyConsecutiveService.UpdateContainerNumberAsync(
                    companyId,
                    request.ContainerNumber,
                    user
                );

                if (success)
                {
                    return Ok(new { message = "Container number actualizado exitosamente" });
                }
                return StatusCode(500, new { message = "Error actualizando container number" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando container_number");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        // ===== MAPPERS Y DTOS =====

            private static ItemDto MapToDto(Item item) => new()
                {
                    Id = item.Id,
                    Code = item.Code,
                    Name = item.Name,
                    Description = item.Description,
                    Barcode = item.Barcode,
                    IdClassification1 = item.IdClassification1,
                    IdClassification2 = item.IdClassification2,
                    IdClassification3 = item.IdClassification3,
                    IdClassification4 = item.IdClassification4,
                    IdClassification5 = item.IdClassification5,
                    IdClassification6 = item.IdClassification6,
                    Brand = item.Brand,
                    IdUnitOfMeasure = item.IdUnitOfMeasure,
                    CostPrice = item.CostPrice,
                    SalePrice = item.SalePrice,
                    TaxRate = item.TaxRate,
                    MinStock = item.MinStock,
                    MaxStock = item.MaxStock,
                    CurrentStock = item.CurrentStock,
                    ImageUrl = item.ImageUrl,
                    IsActive = item.IsActive,
                    IsSellable = item.IsSellable,
                    IsPurchasable = item.IsPurchasable,
                    TrackLots = item.TrackLots,
                    TrackSerialNumbers = item.TrackSerialNumbers,

                    // Facturación electrónica (Fase B)
                    IdCustomer = item.IdCustomer,
                    TaxRateCode = item.TaxRateCode,
                    CabysCode = item.CabysCode,

                    // Label Item fields
                    LabelItem = item.LabelItem,
                    LabelPrice = item.LabelPrice,
                    LabelItemBarcode = item.LabelItemBarcode,
                    IsLabelItem = item.IsLabelItem,
                    PrintLabelName = item.PrintLabelName,
                    PrintLabelPrice = item.PrintLabelPrice,
                    PrintLabelBarcode = item.PrintLabelBarcode,

                    // Label size and format
                    LabelWidthCm = item.LabelWidthCm,
                    LabelHeightCm = item.LabelHeightCm,
                    LabelOrientation = item.LabelOrientation,
                    PrintLabelBorder = item.PrintLabelBorder,
                    LabelBorderColor = item.LabelBorderColor,
                    LabelNameColor = item.LabelNameColor,
                    LabelPriceColor = item.LabelPriceColor,
                    LabelBarcodeColor = item.LabelBarcodeColor,

                    // Label font and price format
                    LabelFontSize = item.LabelFontSize,
                    LabelFontFamily = item.LabelFontFamily,
                    LabelPriceDecimals = item.LabelPriceDecimals,
                    LabelThousandSeparator = item.LabelThousandSeparator,
                    LabelCurrencySymbol = item.LabelCurrencySymbol,
                    PrintCurrencySymbol = item.PrintCurrencySymbol,

                    // Auditoría
                    CreateDate = item.CreateDate,
                    RecordDate = item.RecordDate,
                    CreatedBy = item.CreatedBy,
                    UpdatedBy = item.UpdatedBy,
                    RowPointer = item.RowPointer
                };
        }

        // ===== REQUEST/RESPONSE DTOS =====

        /// <summary>DTO ligero para el modal de selección de ítems en emisión de comprobantes.</summary>
        public class BillingItemDto
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public decimal SalePrice { get; set; }
            public decimal TaxRate { get; set; }
            public string TaxRateCode { get; set; } = "08";
            public string CabysCode { get; set; } = string.Empty;
            public int IdCustomer { get; set; }
        }

        public class ItemListResponse
        {
            public List<ItemDto> Items { get; set; } = new();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
        }

        public class ItemDto
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? Barcode { get; set; }
            public int? IdClassification1 { get; set; }
            public int? IdClassification2 { get; set; }
            public int? IdClassification3 { get; set; }
            public int? IdClassification4 { get; set; }
            public int? IdClassification5 { get; set; }
            public int? IdClassification6 { get; set; }
            public string? Brand { get; set; }
            public int? IdUnitOfMeasure { get; set; }
            public decimal CostPrice { get; set; }
            public decimal SalePrice { get; set; }
            public decimal TaxRate { get; set; }
            public decimal MinStock { get; set; }
            public decimal MaxStock { get; set; }
            public decimal CurrentStock { get; set; }
            public string? ImageUrl { get; set; }
            public bool IsActive { get; set; }
            public bool IsSellable { get; set; }
            public bool IsPurchasable { get; set; }
            public bool TrackLots { get; set; }
            public bool TrackSerialNumbers { get; set; }

            // Facturación electrónica (Fase B)
            public int IdCustomer { get; set; }
            public string TaxRateCode { get; set; } = "08";
            public string CabysCode { get; set; } = string.Empty;

            // Label Item fields
            public string? LabelItem { get; set; }
            public decimal LabelPrice { get; set; }
            public string? LabelItemBarcode { get; set; }
            public bool IsLabelItem { get; set; }
            public bool PrintLabelName { get; set; }
            public bool PrintLabelPrice { get; set; }
            public bool PrintLabelBarcode { get; set; }

            // Label size and format
            public decimal LabelWidthCm { get; set; }
            public decimal LabelHeightCm { get; set; }
            public string LabelOrientation { get; set; } = "horizontal";
            public bool PrintLabelBorder { get; set; }
            public string LabelBorderColor { get; set; } = "#000000";
            public string LabelNameColor { get; set; } = "#000000";
            public string LabelPriceColor { get; set; } = "#16a34a";
            public string LabelBarcodeColor { get; set; } = "#000000";

            // Label font and price format
            public decimal LabelFontSize { get; set; } = 14.0m;
            public string LabelFontFamily { get; set; } = "Arial";
            public int LabelPriceDecimals { get; set; } = 2;
            public string LabelThousandSeparator { get; set; } = ",";
            public string LabelCurrencySymbol { get; set; } = "₡";
            public bool PrintCurrencySymbol { get; set; } = true;

            // Auditoría
            public DateTime CreateDate { get; set; }
            public DateTime RecordDate { get; set; }
            public string CreatedBy { get; set; } = string.Empty;
            public string UpdatedBy { get; set; } = string.Empty;
            public Guid RowPointer { get; set; }
        }

        public class CreateItemRequest
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? Barcode { get; set; }
            public int? IdClassification1 { get; set; }
            public int? IdClassification2 { get; set; }
            public int? IdClassification3 { get; set; }
            public int? IdClassification4 { get; set; }
            public int? IdClassification5 { get; set; }
            public int? IdClassification6 { get; set; }
            public string? Brand { get; set; }
            public int? IdUnitOfMeasure { get; set; }
            public decimal CostPrice { get; set; }
            public decimal SalePrice { get; set; }
            public decimal TaxRate { get; set; }
            public decimal MinStock { get; set; }
            public decimal MaxStock { get; set; }
            public decimal CurrentStock { get; set; }
            public string? ImageUrl { get; set; }
            public bool? IsActive { get; set; }
            public bool? IsSellable { get; set; }
            public bool? IsPurchasable { get; set; }
            public bool? TrackLots { get; set; }
            public bool? TrackSerialNumbers { get; set; }

            // Facturación electrónica (Fase B)
            public int? IdCustomer { get; set; }
            public string? TaxRateCode { get; set; }
            public string? CabysCode { get; set; }

            // Label Item fields
            public string? LabelItem { get; set; }
            public decimal LabelPrice { get; set; }
            public string? LabelItemBarcode { get; set; }
            public bool IsLabelItem { get; set; }
            public bool PrintLabelName { get; set; } = true;
            public bool PrintLabelPrice { get; set; } = true;
            public bool PrintLabelBarcode { get; set; } = true;
        }

        public class UpdateItemRequest
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? Barcode { get; set; }
            public int? IdClassification1 { get; set; }
            public int? IdClassification2 { get; set; }
            public int? IdClassification3 { get; set; }
            public int? IdClassification4 { get; set; }
            public int? IdClassification5 { get; set; }
            public int? IdClassification6 { get; set; }
            public string? Brand { get; set; }
            public int? IdUnitOfMeasure { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal TaxRate { get; set; }
        public decimal MinStock { get; set; }
        public decimal MaxStock { get; set; }
        public decimal CurrentStock { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public bool IsSellable { get; set; }
        public bool IsPurchasable { get; set; }
        public bool TrackLots { get; set; }
        public bool TrackSerialNumbers { get; set; }

        // Facturación electrónica (Fase B)
        public int? IdCustomer { get; set; }
        public string? TaxRateCode { get; set; }
        public string? CabysCode { get; set; }

        // Label Item fields
        public string? LabelItem { get; set; }
        public decimal LabelPrice { get; set; }
        public string? LabelItemBarcode { get; set; }
        public bool IsLabelItem { get; set; }
        public bool PrintLabelName { get; set; }
        public bool PrintLabelPrice { get; set; }
        public bool PrintLabelBarcode { get; set; }

        // Label size and format
        public decimal LabelWidthCm { get; set; } = 4.0m;
        public decimal LabelHeightCm { get; set; } = 2.0m;
        public string LabelOrientation { get; set; } = "horizontal";
        public bool PrintLabelBorder { get; set; } = true;
        public string LabelBorderColor { get; set; } = "#000000";
        public string LabelNameColor { get; set; } = "#000000";
        public string LabelPriceColor { get; set; } = "#16a34a";
        public string LabelBarcodeColor { get; set; } = "#000000";

        // Label font and price format
        public decimal LabelFontSize { get; set; } = 14.0m;
        public string LabelFontFamily { get; set; } = "Arial";
        public int LabelPriceDecimals { get; set; } = 2;
        public string LabelThousandSeparator { get; set; } = ",";
        public string LabelCurrencySymbol { get; set; } = "₡";
        public bool PrintCurrencySymbol { get; set; } = true;
    }

    /// <summary>
    /// Request para actualizar solo los datos de etiqueta de un artículo
    /// </summary>
    public class UpdateLabelRequest
    {
        public string? LabelItem { get; set; }
        public decimal LabelPrice { get; set; }
        public string? LabelItemBarcode { get; set; }
        public bool PrintLabelName { get; set; } = true;
        public bool PrintLabelPrice { get; set; } = true;
        public bool PrintLabelBarcode { get; set; } = true;

        // Label size and format
        public decimal LabelWidthCm { get; set; } = 4.0m;
        public decimal LabelHeightCm { get; set; } = 2.0m;
        public string LabelOrientation { get; set; } = "horizontal";
        public bool PrintLabelBorder { get; set; } = true;
        public string LabelBorderColor { get; set; } = "#000000";
        public string LabelNameColor { get; set; } = "#000000";
        public string LabelPriceColor { get; set; } = "#16a34a";
        public string LabelBarcodeColor { get; set; } = "#000000";

        // Label font and price format
        public decimal LabelFontSize { get; set; } = 14.0m;
        public string LabelFontFamily { get; set; } = "Arial";
        public int LabelPriceDecimals { get; set; } = 2;
        public string LabelThousandSeparator { get; set; } = ",";
        public string LabelCurrencySymbol { get; set; } = "₡";
        public bool PrintCurrencySymbol { get; set; } = true;
    }

    /// <summary>
    /// Request para registrar una impresión de etiqueta
    /// </summary>
    public class RecordPrintRequest
    {
        public string? LabelItem { get; set; }
        public decimal LabelPrice { get; set; }
        public string? LabelItemBarcode { get; set; }
        public bool PrintLabelName { get; set; } = true;
        public bool PrintLabelPrice { get; set; } = true;
        public bool PrintLabelBarcode { get; set; } = true;
        public bool PrintLabelBorder { get; set; } = true;
        public bool PrintCurrencySymbol { get; set; } = true;

        // Label size
        public decimal LabelWidthCm { get; set; } = 4.0m;
        public decimal LabelHeightCm { get; set; } = 2.0m;
        public string LabelOrientation { get; set; } = "horizontal";

        // Label colors
        public string LabelBorderColor { get; set; } = "#000000";
        public string LabelNameColor { get; set; } = "#000000";
        public string LabelPriceColor { get; set; } = "#16a34a";
        public string LabelBarcodeColor { get; set; } = "#000000";

        // Label font and price format
        public decimal LabelFontSize { get; set; } = 14.0m;
        public string LabelFontFamily { get; set; } = "Arial";
        public int LabelPriceDecimals { get; set; } = 2;
        public string LabelThousandSeparator { get; set; } = ",";
        public string LabelCurrencySymbol { get; set; } = "₡";

        // Formatted price as printed
        public string? FormattedPrice { get; set; }

        // Print info
        public int QuantityPrinted { get; set; } = 1;
        public string? ContainerNumber { get; set; }
        public string? PrinterName { get; set; }
        public string? PrintNotes { get; set; }
    }

    /// <summary>
    /// DTO para historial de impresiones
    /// </summary>
    public class LabelPrintHistoryDto
    {
        public long Id { get; set; }
        public int IdItem { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string? LabelItem { get; set; }
        public decimal LabelPrice { get; set; }
        public string? FormattedPrice { get; set; }
        public int QuantityPrinted { get; set; }
        public string? ContainerNumber { get; set; }
        public DateTime PrintDate { get; set; }
        public string? PrintedBy { get; set; }
    }

    /// <summary>
    /// DTO para unidad de medida
    /// </summary>
    public class UnitOfMeasureDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public bool AllowsDecimals { get; set; }
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// DTO para clasificación
    /// </summary>
    public class ClassificationDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Respuesta con todas las clasificaciones
    /// </summary>
    public class AllClassificationsResponse
    {
        public List<UnitOfMeasureDto> UnitsOfMeasure { get; set; } = new();
        public List<ClassificationDto> Classifications1 { get; set; } = new();
        public List<ClassificationDto> Classifications2 { get; set; } = new();
        public List<ClassificationDto> Classifications3 { get; set; } = new();
        public List<ClassificationDto> Classifications4 { get; set; } = new();
        public List<ClassificationDto> Classifications5 { get; set; } = new();
        public List<ClassificationDto> Classifications6 { get; set; } = new();
    }

    /// <summary>
    /// Respuesta para container number
    /// </summary>
    public class ContainerNumberResponse
    {
        public string ContainerNumber { get; set; } = string.Empty;
        public int CompanyId { get; set; }
    }

    /// <summary>
    /// Request para actualizar container number
    /// </summary>
    public class UpdateContainerNumberRequest
    {
        public string ContainerNumber { get; set; } = string.Empty;
    }
}
