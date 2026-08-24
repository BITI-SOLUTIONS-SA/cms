// ================================================================================
// ARCHIVO: CMS.UI/Controllers/InventoryController.cs
// PROPÓSITO: Controlador para las vistas de Inventory incluyendo Label Items
// DESCRIPCIÓN: Maneja las vistas de gestión de artículos
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-19
// ================================================================================

using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.UI.Controllers
{
    [Authorize]
    public class InventoryController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<InventoryController> _logger;
        private readonly IConfiguration _configuration;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions JsonCamelCaseOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public InventoryController(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            ILogger<InventoryController> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _configuration = configuration;
        }

        private void ConfigureAuthHeader()
        {
            var token = _httpContextAccessor.HttpContext?.Session.GetString("ApiToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                token = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
        }

        private string GetApiBaseUrl()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var baseUrl = _configuration[$"ApiSettings:{environment}:BaseUrl"];
            return baseUrl ?? (environment == "Production" 
                ? "https://cms.biti-solutions.com" 
                : "https://localhost:7001");
        }

        #region Label Items

        /// <summary>
        /// Pantalla de impresión de etiquetas
        /// GET: /Inventory/LabelItems
        /// </summary>
        /// <remarks>
        /// Limitado a máximo 100 artículos (10 páginas x 10 artículos por página)
        /// para mejorar el rendimiento de la UI
        /// </remarks>
        public async Task<IActionResult> LabelItems(string? search = null, int page = 1, int? itemId = null, int? returnToItemId = null, string? orderBy = "sale_price")
        {
            try
            {
                ConfigureAuthHeader();

                // Limitar a 16 artículos por página, máximo 10 páginas (160 artículos total)
                const int pageSize = 16;
                const int maxPages = 10;

                var url = $"{GetApiBaseUrl()}/api/item/labels?page={page}&pageSize={pageSize}&orderBy={orderBy ?? "sale_price"}";
                if (!string.IsNullOrEmpty(search))
                    url += $"&search={Uri.EscapeDataString(search)}";

                var response = await _httpClient.GetAsync(url);

                var viewModel = new LabelItemsViewModel
                {
                    Search = search,
                    CurrentPage = page,
                    SelectedItemId = itemId,
                    ReturnToItemId = returnToItemId ?? itemId,
                    OrderBy = orderBy ?? "sale_price"
                };

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ItemListResponse>(json, JsonOptions);

                    if (result != null)
                    {
                        viewModel.Items = result.Items;
                        // Limitar a 160 items máximo (10 páginas x 16 items)
                        viewModel.TotalCount = Math.Min(result.TotalCount, maxPages * pageSize);
                        viewModel.TotalPages = Math.Min(result.TotalPages, maxPages);
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Error obteniendo artículos de etiqueta: {StatusCode} - {Error}",
                        response.StatusCode, errorContent);
                    TempData["Error"] = "Error al cargar los artículos de etiqueta.";
                }

                // Pasar token JWT y URL de la API para las llamadas directas desde JS
                var apiToken = _httpContextAccessor.HttpContext?.Session.GetString("ApiToken")
                            ?? _httpContextAccessor.HttpContext?.Session.GetString("JwtToken")
                            ?? string.Empty;
                ViewBag.ApiToken = apiToken;
                ViewBag.ApiBaseUrl = GetApiBaseUrl();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en LabelItems");
                TempData["Error"] = "Error al cargar los artículos de etiqueta";
                return View(new LabelItemsViewModel());
            }
        }

        /// <summary>
        /// Obtener datos de un artículo para mostrar en el panel de etiqueta
        /// GET: /Inventory/LabelItems/GetItem/{id}
        /// </summary>
        [HttpGet("Inventory/LabelItems/GetItem/{id:int}")]
        public async Task<IActionResult> GetLabelItem(int id)
        {
            try
            {
                ConfigureAuthHeader();

                var response = await _httpClient.GetAsync($"{GetApiBaseUrl()}/api/item/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "Artículo no encontrado" });
                }

                var json = await response.Content.ReadAsStringAsync();
                var item = JsonSerializer.Deserialize<ItemDto>(json, JsonOptions);

                // Serialize using camelCase for JavaScript compatibility
                var resultJson = JsonSerializer.Serialize(new { success = true, item }, JsonCamelCaseOptions);
                return Content(resultJson, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo artículo {Id}", id);
                return Json(new { success = false, message = "Error al cargar el artículo" });
            }
        }

        /// <summary>
        /// Guardar datos de etiqueta de un artículo
        /// POST: /Inventory/LabelItems/SaveLabel
        /// </summary>
        [HttpPost("Inventory/LabelItems/SaveLabel")]
        [IgnoreAntiforgeryToken] // AJAX call with JWT authentication, no need for antiforgery token
        public async Task<IActionResult> SaveLabel([FromBody] SaveLabelRequest request)
        {
            try
            {
                ConfigureAuthHeader();

                var payload = JsonSerializer.Serialize(new
                {
                    labelItem = request.LabelItem,
                    labelPrice = request.LabelPrice,
                    labelItemBarcode = request.LabelItemBarcode,
                    printLabelName = request.PrintLabelName,
                    printLabelPrice = request.PrintLabelPrice,
                    printLabelBarcode = request.PrintLabelBarcode,
                    labelWidthCm = request.LabelWidthCm,
                    labelHeightCm = request.LabelHeightCm,
                    labelOrientation = request.LabelOrientation,
                    printLabelBorder = request.PrintLabelBorder,
                    labelBorderColor = request.LabelBorderColor,
                    labelNameColor = request.LabelNameColor,
                    labelPriceColor = request.LabelPriceColor,
                    labelBarcodeColor = request.LabelBarcodeColor,
                    labelFontSize = request.LabelFontSize,
                    labelFontFamily = request.LabelFontFamily,
                    labelPriceDecimals = request.LabelPriceDecimals,
                    labelThousandSeparator = request.LabelThousandSeparator,
                    labelCurrencySymbol = request.LabelCurrencySymbol,
                    printCurrencySymbol = request.PrintCurrencySymbol
                });

                var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{GetApiBaseUrl()}/api/item/{request.ItemId}/label", content);

                if (response.IsSuccessStatusCode)
                {
                    // Devolver también el item actualizado para refrescar la lista
                    var json = await response.Content.ReadAsStringAsync();
                    var updatedItem = JsonSerializer.Deserialize<ItemDto>(json, JsonOptions);
                    // Serialize using camelCase for JavaScript compatibility
                    var resultJson = JsonSerializer.Serialize(new { success = true, message = "Etiqueta guardada exitosamente", item = updatedItem }, JsonCamelCaseOptions);
                    return Content(resultJson, "application/json");
                }

                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = $"Error al guardar: {error}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando etiqueta del artículo {Id}", request.ItemId);
                return Json(new { success = false, message = "Error al guardar la etiqueta" });
            }
        }

        /// <summary>
        /// Registra una impresión de etiqueta en el historial
        /// POST: /Inventory/LabelItems/RecordPrint
        /// </summary>
        [HttpPost("Inventory/LabelItems/RecordPrint")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RecordPrint([FromBody] RecordPrintRequest request)
        {
            try
            {
                ConfigureAuthHeader();

                var payload = JsonSerializer.Serialize(new
                {
                    labelItem = request.LabelItem,
                    labelPrice = request.LabelPrice,
                    labelItemBarcode = request.LabelItemBarcode,
                    printLabelName = request.PrintLabelName,
                    printLabelPrice = request.PrintLabelPrice,
                    printLabelBarcode = request.PrintLabelBarcode,
                    printLabelBorder = request.PrintLabelBorder,
                    printCurrencySymbol = request.PrintCurrencySymbol,
                    labelWidthCm = request.LabelWidthCm,
                    labelHeightCm = request.LabelHeightCm,
                    labelOrientation = request.LabelOrientation,
                    labelBorderColor = request.LabelBorderColor,
                    labelNameColor = request.LabelNameColor,
                    labelPriceColor = request.LabelPriceColor,
                    labelBarcodeColor = request.LabelBarcodeColor,
                    labelFontSize = request.LabelFontSize,
                    labelFontFamily = request.LabelFontFamily,
                    labelPriceDecimals = request.LabelPriceDecimals,
                    labelThousandSeparator = request.LabelThousandSeparator,
                    labelCurrencySymbol = request.LabelCurrencySymbol,
                    formattedPrice = request.FormattedPrice,
                    quantityPrinted = request.QuantityPrinted,
                    containerNumber = request.ContainerNumber,
                    printerName = request.PrinterName,
                    printNotes = request.PrintNotes
                });

                var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{GetApiBaseUrl()}/api/item/{request.ItemId}/print", content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return Json(new { success = true, message = "Impresión registrada exitosamente" });
                }

                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = $"Error al registrar impresión: {error}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando impresión del artículo {Id}", request.ItemId);
                return Json(new { success = false, message = "Error al registrar la impresión" });
            }
        }

        /// <summary>
        /// Historial de impresiones de etiquetas
        /// GET: /Inventory/LabelPrintHistory
        /// </summary>
        public async Task<IActionResult> LabelPrintHistory(
            int? itemId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? printedBy = null,
            string? containerNumber = null,
            int page = 1)
        {
            try
            {
                ConfigureAuthHeader();

                // Defaultear fechas al día actual en el primer acceso
                fromDate ??= DateTime.Today;
                toDate ??= DateTime.Today;

                const int pageSize = 20;

                var url = $"{GetApiBaseUrl()}/api/item/print-history?page={page}&pageSize={pageSize}";

                if (itemId.HasValue)
                    url += $"&itemId={itemId}";
                if (fromDate.HasValue)
                    url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
                if (toDate.HasValue)
                    url += $"&toDate={toDate.Value:yyyy-MM-dd}";
                if (!string.IsNullOrEmpty(printedBy))
                    url += $"&printedBy={Uri.EscapeDataString(printedBy)}";
                if (!string.IsNullOrEmpty(containerNumber))
                    url += $"&containerNumber={Uri.EscapeDataString(containerNumber)}";

                var response = await _httpClient.GetAsync(url);

                var viewModel = new LabelPrintHistoryViewModel
                {
                    ItemId = itemId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    PrintedBy = printedBy,
                    ContainerNumber = containerNumber,
                    CurrentPage = page,
                    PageSize = pageSize
                };

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var history = JsonSerializer.Deserialize<List<LabelPrintHistoryDto>>(json, JsonOptions);
                    viewModel.History = history ?? new List<LabelPrintHistoryDto>();
                    viewModel.TotalCount = history?.Count ?? 0;
                }
                else
                {
                    _logger.LogWarning("Error obteniendo historial: {StatusCode}", response.StatusCode);
                    TempData["Error"] = "Error al cargar el historial de impresiones.";
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en LabelPrintHistory");
                TempData["Error"] = "Error al cargar el historial de impresiones";
                return View(new LabelPrintHistoryViewModel());
            }
        }

        #endregion

        #region Item Master (Mantenimiento)

        /// <summary>
        /// Lista de artículos (Mantenimiento)
        /// GET: /Inventory/Items
        /// </summary>
        /// <remarks>
        /// Limitado a máximo 100 artículos (10 páginas x 10 artículos por página)
        /// para mejorar el rendimiento de la UI
        /// </remarks>
        public async Task<IActionResult> Items(
            string? search = null, 
            int? classificationGroup = null, 
            int? classificationId = null, 
            bool? isActive = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int page = 1)
        {
            try
            {
                ConfigureAuthHeader();

                // Limitar a 10 artículos por página, máximo 10 páginas (100 artículos total)
                const int pageSize = 10;
                const int maxPages = 10;

                var url = $"{GetApiBaseUrl()}/api/item?page={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(search))
                    url += $"&search={Uri.EscapeDataString(search)}";

                // Filtrar por clasificación si se seleccionó
                if (classificationGroup.HasValue && classificationId.HasValue)
                {
                    url += $"&classificationGroup={classificationGroup}&classificationId={classificationId}";
                }

                // Filtrar por estado si se seleccionó
                if (isActive.HasValue)
                {
                    url += $"&isActive={isActive.Value.ToString().ToLower()}";
                }

                // Filtrar por fechas
                if (dateFrom.HasValue)
                {
                    url += $"&dateFrom={dateFrom.Value:yyyy-MM-dd}";
                }
                if (dateTo.HasValue)
                {
                    url += $"&dateTo={dateTo.Value:yyyy-MM-dd}";
                }

                var response = await _httpClient.GetAsync(url);

                var viewModel = new ItemListViewModel
                {
                    Search = search,
                    ClassificationGroup = classificationGroup,
                    ClassificationId = classificationId,
                    IsActive = isActive,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    CurrentPage = page
                };

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ItemListResponse>(json, JsonOptions);


                    if (result != null)
                    {
                        viewModel.Items = result.Items;
                        // Limitar a 100 items máximo (10 páginas x 10 items)
                        viewModel.TotalCount = Math.Min(result.TotalCount, maxPages * pageSize);
                        viewModel.TotalPages = Math.Min(result.TotalPages, maxPages);
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Error obteniendo artículos: {StatusCode} - {Error}", 
                        response.StatusCode, errorContent);
                    TempData["Error"] = "Error al cargar los artículos. Verifique que la base de datos de la compañía existe.";
                }

                // Cargar clasificaciones para los filtros
                try
                {
                    var classificationsResponse = await _httpClient.GetAsync($"{GetApiBaseUrl()}/api/item/all-classifications");
                    if (classificationsResponse.IsSuccessStatusCode)
                    {
                        var classificationsJson = await classificationsResponse.Content.ReadAsStringAsync();
                        var classifications = JsonSerializer.Deserialize<AllClassificationsResponse>(classificationsJson, JsonOptions);

                        if (classifications != null)
                        {
                            viewModel.Classifications1 = classifications.Classifications1 ?? new();
                            viewModel.Classifications2 = classifications.Classifications2 ?? new();
                            viewModel.Classifications3 = classifications.Classifications3 ?? new();
                            viewModel.Classifications4 = classifications.Classifications4 ?? new();
                            viewModel.Classifications5 = classifications.Classifications5 ?? new();
                            viewModel.Classifications6 = classifications.Classifications6 ?? new();
                        }
                    }
                }
                catch (Exception ex) 
                { 
                    _logger.LogWarning(ex, "Error cargando clasificaciones para filtros");
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Items");
                TempData["Error"] = "Error al cargar los artículos";
                return View(new ItemListViewModel());
            }
        }

        /// <summary>
        /// Detalle de un artículo
        /// GET: /Inventory/Items/{id}
        /// </summary>
        [HttpGet("Inventory/Items/{id:int}")]
        public async Task<IActionResult> ItemDetails(int id)
        {
            try
            {
                ConfigureAuthHeader();

                var response = await _httpClient.GetAsync($"{GetApiBaseUrl()}/api/item/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Artículo no encontrado";
                    return RedirectToAction(nameof(Items));
                }

                var json = await response.Content.ReadAsStringAsync();
                var item = JsonSerializer.Deserialize<ItemDto>(json, JsonOptions);

                return View("ItemDetails", item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo detalle de artículo {Id}", id);
                TempData["Error"] = "Error al cargar el artículo";
                return RedirectToAction(nameof(Items));
            }
        }

        /// <summary>
        /// Formulario para crear artículo
        /// GET: /Inventory/Items/Create
        /// </summary>
        [HttpGet("Inventory/Items/Create")]
        public async Task<IActionResult> CreateItem()
        {
            ConfigureAuthHeader();
            var model = new CreateItemViewModel();

            // Cargar las listas de clasificaciones y unidades de medida
            await LoadClassificationsAsync(model);

            return View("CreateItem", model);
        }

        /// <summary>
        /// Crear artículo
        /// POST: /Inventory/Items/Create
        /// </summary>
        [HttpPost("Inventory/Items/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateItem(CreateItemViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Recargar listas en caso de error de validación
                ConfigureAuthHeader();
                await LoadClassificationsAsync(model);
                return View("CreateItem", model);
            }

            try
            {
                ConfigureAuthHeader();

                var payload = JsonSerializer.Serialize(model);
                var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{GetApiBaseUrl()}/api/item", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Artículo creado exitosamente";
                    return RedirectToAction(nameof(Items));
                }

                var error = await response.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al crear artículo: {error}";

                // Recargar listas en caso de error
                await LoadClassificationsAsync(model);
                return View("CreateItem", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando artículo");
                TempData["Error"] = "Error al crear el artículo";

                // Recargar listas en caso de error
                await LoadClassificationsAsync(model);
                return View("CreateItem", model);
            }
        }

        /// <summary>
        /// Formulario para editar artículo
        /// GET: /Inventory/Items/Edit/{id}
        /// </summary>
        [HttpGet("Inventory/Items/Edit/{id:int}")]
        public async Task<IActionResult> EditItem(int id)
        {
            try
            {
                ConfigureAuthHeader();

                var response = await _httpClient.GetAsync($"{GetApiBaseUrl()}/api/item/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Artículo no encontrado";
                    return RedirectToAction(nameof(Items));
                }

                var json = await response.Content.ReadAsStringAsync();
                var item = JsonSerializer.Deserialize<EditItemViewModel>(json, JsonOptions);

                // Cargar las listas de clasificaciones y unidades de medida
                if (item != null)
                {
                    await LoadClassificationsAsync(item);
                }

                return View("EditItem", item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo artículo para editar {Id}", id);
                TempData["Error"] = "Error al cargar el artículo";
                return RedirectToAction(nameof(Items));
            }
        }

        /// <summary>
        /// Carga las listas de clasificaciones y unidades de medida desde el API
        /// </summary>
        private async Task LoadClassificationsAsync(CreateItemViewModel model)
        {
            try
            {
                var classificationsResponse = await _httpClient.GetAsync($"{GetApiBaseUrl()}/api/item/all-classifications");
                if (classificationsResponse.IsSuccessStatusCode)
                {
                    var json = await classificationsResponse.Content.ReadAsStringAsync();
                    var classifications = JsonSerializer.Deserialize<AllClassificationsResponse>(json, JsonOptions);

                    if (classifications != null)
                    {
                        model.UnitsOfMeasure = classifications.UnitsOfMeasure ?? new();
                        model.Classifications1 = classifications.Classifications1 ?? new();
                        model.Classifications2 = classifications.Classifications2 ?? new();
                        model.Classifications3 = classifications.Classifications3 ?? new();
                        model.Classifications4 = classifications.Classifications4 ?? new();
                        model.Classifications5 = classifications.Classifications5 ?? new();
                        model.Classifications6 = classifications.Classifications6 ?? new();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cargando clasificaciones para edición de artículo");
            }
        }

        /// <summary>
        /// Actualizar artículo
        /// POST: /Inventory/Items/Edit/{id}
        /// </summary>
        [HttpPost("Inventory/Items/Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditItem(int id, EditItemViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("EditItem", model);
            }

            try
            {
                ConfigureAuthHeader();

                var payload = JsonSerializer.Serialize(model);
                var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"{GetApiBaseUrl()}/api/item/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Artículo actualizado exitosamente";
                    return RedirectToAction(nameof(Items));
                }

                var error = await response.Content.ReadAsStringAsync();
                TempData["Error"] = $"Error al actualizar artículo: {error}";
                return View("EditItem", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando artículo {Id}", id);
                TempData["Error"] = "Error al actualizar el artículo";
                return View("EditItem", model);
            }
        }

        /// <summary>
        /// Eliminar artículo
        /// POST: /Inventory/Items/Delete/{id}
        /// </summary>
        [HttpPost("Inventory/Items/Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteItem(int id)
        {
            try
            {
                ConfigureAuthHeader();

                var response = await _httpClient.DeleteAsync($"{GetApiBaseUrl()}/api/item/{id}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Artículo eliminado exitosamente";
                }
                else
                {
                    TempData["Error"] = "Error al eliminar el artículo";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando artículo {Id}", id);
                TempData["Error"] = "Error al eliminar el artículo";
            }

            return RedirectToAction(nameof(Items));
        }

        /// <summary>
        /// Vista de carga masiva de artículos
        /// GET: /Inventory/Items/BulkUpload
        /// </summary>
        [HttpGet("Inventory/Items/BulkUpload")]
        public IActionResult BulkUploadItems()
        {
            return View("BulkUploadItems");
        }

        /// <summary>
        /// Procesar carga masiva de artículos
        /// POST: /Inventory/Items/BulkUpload
        /// </summary>
        [HttpPost("Inventory/Items/BulkUpload")]
        public async Task<IActionResult> BulkUploadItems([FromBody] BulkUploadRequest request)
        {
            var results = new BulkUploadResult
            {
                TotalCount = request.Items?.Count ?? 0,
                SuccessCount = 0,
                ErrorCount = 0,
                Errors = new List<BulkUploadError>()
            };

            if (request.Items == null || request.Items.Count == 0)
            {
                return Json(results);
            }

            try
            {
                ConfigureAuthHeader();
                var rowNumber = 1;

                foreach (var item in request.Items)
                {
                    rowNumber++;
                    try
                    {
                        // Validar campos requeridos
                        if (string.IsNullOrWhiteSpace(item.Code))
                        {
                            results.Errors.Add(new BulkUploadError
                            {
                                Row = rowNumber,
                                Code = item.Code,
                                Name = item.Name,
                                Error = "El código es obligatorio"
                            });
                            results.ErrorCount++;
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(item.Name))
                        {
                            results.Errors.Add(new BulkUploadError
                            {
                                Row = rowNumber,
                                Code = item.Code,
                                Name = item.Name,
                                Error = "El nombre es obligatorio"
                            });
                            results.ErrorCount++;
                            continue;
                        }

                        // Crear el artículo vía API
                        _logger.LogInformation(
                            "Carga masiva - Artículo: Code={Code}, Name={Name}, IsLabelItem={IsLabelItem}, SalePrice={SalePrice}, CostPrice={CostPrice}",
                            item.Code, item.Name, item.IsLabelItem, item.SalePrice, item.CostPrice);

                        var payload = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            code = item.Code?.Trim(),
                            name = item.Name?.Trim(),
                            description = item.Description?.Trim(),
                            barcode = item.Barcode?.Trim(),
                            brand = item.Brand?.Trim(),
                            salePrice = item.SalePrice,
                            costPrice = item.CostPrice,
                            taxRate = item.TaxRate,
                            idElectronicDocumentUnitOfMeasure = item.IdElectronicDocumentUnitOfMeasure,
                            idClassification1 = item.IdClassification1,
                            idClassification2 = item.IdClassification2,
                            idClassification3 = item.IdClassification3,
                            idClassification4 = item.IdClassification4,
                            idClassification5 = item.IdClassification5,
                            idClassification6 = item.IdClassification6,
                            labelItem = item.LabelItem?.Trim(),
                            labelPrice = item.LabelPrice,
                            labelItemBarcode = item.LabelItemBarcode?.Trim(),
                            isLabelItem = item.IsLabelItem,
                            isActive = true,
                            isSellable = true,
                            isPurchasable = true
                        });

                        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                        var response = await _httpClient.PostAsync($"{GetApiBaseUrl()}/api/item", content);

                        if (response.IsSuccessStatusCode)
                        {
                            results.SuccessCount++;
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            var errorMessage = "Error al crear artículo";

                            // Intentar extraer mensaje de error
                            try
                            {
                                var errorObj = System.Text.Json.JsonDocument.Parse(errorContent);
                                if (errorObj.RootElement.TryGetProperty("message", out var msgProp))
                                {
                                    errorMessage = msgProp.GetString() ?? errorMessage;
                                }
                            }
                            catch { }

                            results.Errors.Add(new BulkUploadError
                            {
                                Row = rowNumber,
                                Code = item.Code,
                                Name = item.Name,
                                Description = item.Description,
                                Barcode = item.Barcode,
                                Brand = item.Brand,
                                SalePrice = item.SalePrice,
                                CostPrice = item.CostPrice,
                                TaxRate = item.TaxRate,
                                IdElectronicDocumentUnitOfMeasure = item.IdElectronicDocumentUnitOfMeasure,
                                IdClassification1 = item.IdClassification1,
                                IdClassification2 = item.IdClassification2,
                                IdClassification3 = item.IdClassification3,
                                IdClassification4 = item.IdClassification4,
                                IdClassification5 = item.IdClassification5,
                                IdClassification6 = item.IdClassification6,
                                LabelItem = item.LabelItem,
                                LabelPrice = item.LabelPrice,
                                LabelItemBarcode = item.LabelItemBarcode,
                                IsLabelItem = item.IsLabelItem,
                                Error = errorMessage
                            });
                            results.ErrorCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error procesando artículo en fila {Row}", rowNumber);
                        results.Errors.Add(new BulkUploadError
                        {
                            Row = rowNumber,
                            Code = item.Code,
                            Name = item.Name,
                            Error = $"Error interno: {ex.Message}"
                        });
                        results.ErrorCount++;
                    }
                }

                _logger.LogInformation(
                    "Carga masiva completada: {Success} exitosos, {Errors} errores de {Total} total",
                    results.SuccessCount, results.ErrorCount, results.TotalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en carga masiva de artículos");
                return Json(new { success = false, message = $"Error general: {ex.Message}" });
            }

            return Json(results);
        }

        #endregion
    }

    // ===== VIEW MODELS =====

    public class ItemListViewModel
    {
        public List<ItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public string? Search { get; set; }

        // Filtro de clasificación: número de grupo (1-6) y ID de clasificación seleccionada
        public int? ClassificationGroup { get; set; }
        public int? ClassificationId { get; set; }

        // Filtro de estado
        public bool? IsActive { get; set; }

        // Filtro de fechas
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // Listas para los dropdowns de filtro
        public List<ClassificationDto> Classifications1 { get; set; } = new();
        public List<ClassificationDto> Classifications2 { get; set; } = new();
        public List<ClassificationDto> Classifications3 { get; set; } = new();
        public List<ClassificationDto> Classifications4 { get; set; } = new();
        public List<ClassificationDto> Classifications5 { get; set; } = new();
        public List<ClassificationDto> Classifications6 { get; set; } = new();

        // Compatibilidad con código existente (deprecated)
        [Obsolete("Usar ClassificationGroup + ClassificationId")]
        public string? Category { get; set; }
        [Obsolete("Usar Classifications1-6")]
        public List<string> Categories { get; set; } = new();
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
        public int? IdElectronicDocumentUnitOfMeasure { get; set; }
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

    public class CreateItemViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Barcode { get; set; }
        public int? IdClassification1 { get; set; } = 1;
        public int? IdClassification2 { get; set; } = 2;
        public int? IdClassification3 { get; set; } = 3;
        public int? IdClassification4 { get; set; } = 4;
        public int? IdClassification5 { get; set; } = 5;
        public int? IdClassification6 { get; set; } = 6;
        public string? Brand { get; set; } = "Marca A";
        public int? IdElectronicDocumentUnitOfMeasure { get; set; }
        public decimal CostPrice { get; set; } = 1;
        public decimal SalePrice { get; set; } = 1;
        public decimal TaxRate { get; set; }
        public decimal MinStock { get; set; }
        public decimal MaxStock { get; set; }
        public decimal CurrentStock { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSellable { get; set; } = true;
        public bool IsPurchasable { get; set; } = true;
        public bool TrackLots { get; set; }
        public bool TrackSerialNumbers { get; set; }

        // Facturación electrónica (Fase B)
        public int IdCustomer { get; set; } = 1;
        public string TaxRateCode { get; set; } = "08";
        public string CabysCode { get; set; } = "9799000000000";

        // Label Item fields
        public string? LabelItem { get; set; }
        public decimal LabelPrice { get; set; } = 0;
        public string? LabelItemBarcode { get; set; }
        public bool IsLabelItem { get; set; }
        public bool PrintLabelName { get; set; } = true;
        public bool PrintLabelPrice { get; set; } = true;
        public bool PrintLabelBarcode { get; set; } = true;

        // Listas para dropdowns (no se envían al API)
        public List<UnitOfMeasureDto> UnitsOfMeasure { get; set; } = new();
        public List<ClassificationDto> Classifications1 { get; set; } = new();
        public List<ClassificationDto> Classifications2 { get; set; } = new();
        public List<ClassificationDto> Classifications3 { get; set; } = new();
        public List<ClassificationDto> Classifications4 { get; set; } = new();
        public List<ClassificationDto> Classifications5 { get; set; } = new();
        public List<ClassificationDto> Classifications6 { get; set; } = new();
    }

    public class EditItemViewModel : CreateItemViewModel
    {
        public int Id { get; set; }
    }

    public class UnitOfMeasureDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public bool AllowsDecimals { get; set; }
        public bool IsDefault { get; set; }
    }

    public class ClassificationDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

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

    // ===== LABEL ITEMS VIEW MODELS =====

    public class LabelItemsViewModel
    {
        public List<ItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public string? Search { get; set; }
        public ItemDto? SelectedItem { get; set; }

        /// <summary>
        /// ID del artículo preseleccionado (viene desde ItemDetails)
        /// </summary>
        public int? SelectedItemId { get; set; }

        /// <summary>
        /// ID del artículo al que regresar (para el botón Volver)
        /// </summary>
        public int? ReturnToItemId { get; set; }

        /// <summary>
        /// Campo por el cual ordenar los artículos
        /// Valores: sale_price, name, code, label_price, label_item
        /// </summary>
        public string OrderBy { get; set; } = "sale_price";
    }

    public class SaveLabelRequest
    {
        public int ItemId { get; set; }
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

    public class RecordPrintRequest
    {
        public int ItemId { get; set; }
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
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

    // ===== BULK UPLOAD VIEW MODELS =====

    public class BulkUploadRequest
    {
        public List<BulkUploadItemDto>? Items { get; set; }
    }

    public class BulkUploadItemDto
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Barcode { get; set; }
        public string? Brand { get; set; }
        public decimal SalePrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal TaxRate { get; set; }
        public int? IdElectronicDocumentUnitOfMeasure { get; set; }
        public int? IdClassification1 { get; set; }
        public int? IdClassification2 { get; set; }
        public int? IdClassification3 { get; set; }
        public int? IdClassification4 { get; set; }
        public int? IdClassification5 { get; set; }
        public int? IdClassification6 { get; set; }
        public string? LabelItem { get; set; }
        public decimal LabelPrice { get; set; }
        public string? LabelItemBarcode { get; set; }
        public bool IsLabelItem { get; set; }
    }

    public class BulkUploadResult
    {
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public List<BulkUploadError> Errors { get; set; } = new();
    }

    public class BulkUploadError
    {
        public int Row { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Barcode { get; set; }
        public string? Brand { get; set; }
        public decimal SalePrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal TaxRate { get; set; }
        public int? IdElectronicDocumentUnitOfMeasure { get; set; }
        public int? IdClassification1 { get; set; }
        public int? IdClassification2 { get; set; }
        public int? IdClassification3 { get; set; }
        public int? IdClassification4 { get; set; }
        public int? IdClassification5 { get; set; }
        public int? IdClassification6 { get; set; }
        public string? LabelItem { get; set; }
        public decimal LabelPrice { get; set; }
        public string? LabelItemBarcode { get; set; }
        public bool IsLabelItem { get; set; }
        public string? Error { get; set; }
    }

    // ===== HISTORIAL DE IMPRESIONES VIEW MODELS =====

    public class LabelPrintHistoryViewModel
    {
        public List<LabelPrintHistoryDto> History { get; set; } = new();
        public int? ItemId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? PrintedBy { get; set; }
        public string? ContainerNumber { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

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
}
