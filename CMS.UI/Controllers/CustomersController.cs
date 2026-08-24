// ================================================================================
// ARCHIVO: CMS.UI/Controllers/CustomersController.cs
// PROPÓSITO: Controlador MVC para gestión de customers (maestro unificado)
// DESCRIPCIÓN: Maneja las vistas de customers (emisores/receptores/clientes)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Net.Http.Headers;
using System.Text.Json;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.UI.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CustomersController> _logger;
        private readonly IConfiguration _configuration;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public CustomersController(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            ILogger<CustomersController> logger,
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
        }

        private string GetApiBaseUrl()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var baseUrl = _configuration[$"ApiSettings:{environment}:BaseUrl"];
            return baseUrl ?? (environment == "Production" 
                ? "https://cms.biti-solutions.com" 
                : "https://localhost:7082");
        }

        /// <summary>
        /// Lista de customers
        /// GET: /Customers/Customers
        /// </summary>
        public async Task<IActionResult> Customers(string? search = null, bool? includeInactive = null, int page = 1)
        {
            try
            {
                ConfigureAuthHeader();

                var url = $"{GetApiBaseUrl()}/api/Customer?includeInactive={includeInactive ?? false}";

                _logger.LogInformation("Llamando a API: {Url}", url);

                var response = await _httpClient.GetAsync(url);

                _logger.LogInformation("Respuesta de API: {StatusCode}", response.StatusCode);

                var customers = new List<Customer>();

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    _logger.LogInformation("JSON recibido (primeros 200 chars): {Json}", 
                        json.Length > 200 ? json.Substring(0, 200) : json);

                    customers = JsonSerializer.Deserialize<List<Customer>>(json, JsonOptions) ?? new();

                    _logger.LogInformation("Customers deserializados: {Count}", customers.Count);

                    // Filtrar por búsqueda si se especificó
                    if (!string.IsNullOrEmpty(search))
                    {
                        customers = customers.Where(c =>
                            (c.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (c.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (c.Identification?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (c.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                        ).ToList();
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error obteniendo customers: {StatusCode} - {Error}", 
                        response.StatusCode, errorContent);
                    TempData["Error"] = $"Error al cargar los customers: {response.StatusCode} - {errorContent}";
                }

                ViewBag.Search = search;
                ViewBag.IncludeInactive = includeInactive;

                return View(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción en Customers");
                TempData["Error"] = $"Error al cargar los customers: {ex.Message}";
                return View(new List<Customer>());
            }
        }

        /// <summary>
        /// Obtiene un customer por id (JSON) para poblar el modal de editar/ver.
        /// GET: /Customers/GetCustomer/{id}
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCustomer(int id)
        {
            try
            {
                ConfigureAuthHeader();
                var response = await _httpClient.GetAsync($"{GetApiBaseUrl()}/api/Customer/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    return NotFound(new { message = "Customer no encontrado." });
                }

                var json = await response.Content.ReadAsStringAsync();
                var customer = JsonSerializer.Deserialize<Customer>(json, JsonOptions);
                return Json(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo customer {Id}", id);
                return StatusCode(500, new { message = "Error al obtener el customer." });
            }
        }

        /// <summary>
        /// Cataloga los tipos de cliente activos (JSON) para los selectores de los modales.
        /// GET: /Customers/CustomerTypes
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CustomerTypes()
        {
            try
            {
                ConfigureAuthHeader();
                var response = await _httpClient.GetAsync($"{GetApiBaseUrl()}/api/customertype/active");

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new List<object>());
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo tipos de cliente");
                return Json(new List<object>());
            }
        }

        /// <summary>
        /// Crea un customer (recibe JSON del modal, reenvía a la API).
        /// POST: /Customers/Create
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] Customer customer)
        {
            try
            {
                ConfigureAuthHeader();

                var json = JsonSerializer.Serialize(customer, JsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{GetApiBaseUrl()}/api/Customer", content);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Customer creado exitosamente." });
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error al crear customer: {StatusCode} - {Error}", response.StatusCode, error);
                return BadRequest(new { success = false, message = $"Error al crear el customer: {error}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al crear customer");
                return StatusCode(500, new { success = false, message = "Error al crear el customer." });
            }
        }

        /// <summary>
        /// Actualiza un customer (recibe JSON del modal, reenvía a la API).
        /// POST: /Customers/Edit/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromBody] Customer customer)
        {
            try
            {
                ConfigureAuthHeader();

                var json = JsonSerializer.Serialize(customer, JsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{GetApiBaseUrl()}/api/Customer/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Customer actualizado exitosamente." });
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error al actualizar customer {Id}: {StatusCode} - {Error}", id, response.StatusCode, error);
                return BadRequest(new { success = false, message = $"Error al actualizar el customer: {error}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al actualizar customer {Id}", id);
                return StatusCode(500, new { success = false, message = "Error al actualizar el customer." });
            }
        }

        /// <summary>
        /// Elimina (o inactiva) un customer.
        /// POST: /Customers/Delete/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                ConfigureAuthHeader();
                var response = await _httpClient.DeleteAsync($"{GetApiBaseUrl()}/api/Customer/{id}");

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Customer eliminado exitosamente." });
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error al eliminar customer {Id}: {StatusCode} - {Error}", id, response.StatusCode, error);
                return BadRequest(new { success = false, message = $"Error al eliminar el customer: {error}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al eliminar customer {Id}", id);
                return StatusCode(500, new { success = false, message = "Error al eliminar el customer." });
            }
        }

        // =====================================================================
        // ACTIVIDADES ECONÓMICAS DEL CUSTOMER (UI -> Controller -> API)
        // =====================================================================

        /// <summary>
        /// Catálogo de actividades económicas activas (JSON) para el selector del modal.
        /// GET: /Customers/EconomicActivities
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EconomicActivities()
        {
            try
            {
                ConfigureAuthHeader();
                var response = await _httpClient.GetAsync($"{GetApiBaseUrl()}/api/electronicdocumenteconomicactivity/active");
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new List<object>());
                }

                var json = await response.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo catálogo de actividades económicas");
                return Json(new List<object>());
            }
        }

        /// <summary>
        /// Lista las actividades económicas de un customer.
        /// GET: /Customers/CustomerActivities/{customerId}
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CustomerActivities(int customerId)
        {
            try
            {
                ConfigureAuthHeader();
                var response = await _httpClient.GetAsync($"{GetApiBaseUrl()}/api/customereconomicactivity/customer/{customerId}");
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new List<object>());
                }

                var json = await response.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo actividades del customer {Id}", customerId);
                return Json(new List<object>());
            }
        }

        /// <summary>
        /// Agrega una actividad económica a un customer.
        /// POST: /Customers/CreateCustomerActivity/{customerId}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomerActivity(int customerId, [FromBody] JsonElement activity)
        {
            return await ForwardActivityAsync(HttpMethod.Post, $"/api/customereconomicactivity/customer/{customerId}", activity, "agregar");
        }

        /// <summary>
        /// Actualiza una actividad económica de un customer.
        /// POST: /Customers/UpdateCustomerActivity/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCustomerActivity(int id, [FromBody] JsonElement activity)
        {
            return await ForwardActivityAsync(HttpMethod.Put, $"/api/customereconomicactivity/{id}", activity, "actualizar");
        }

        /// <summary>
        /// Marca una actividad económica como predeterminada.
        /// POST: /Customers/SetDefaultCustomerActivity/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultCustomerActivity(int id)
        {
            try
            {
                ConfigureAuthHeader();
                var response = await _httpClient.PutAsync($"{GetApiBaseUrl()}/api/customereconomicactivity/{id}/set-default", null);
                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true });
                }

                var error = await response.Content.ReadAsStringAsync();
                return BadRequest(new { success = false, message = $"Error al marcar predeterminada: {error}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error set-default actividad {Id}", id);
                return StatusCode(500, new { success = false, message = "Error al marcar predeterminada." });
            }
        }

        /// <summary>
        /// Elimina una actividad económica de un customer.
        /// POST: /Customers/DeleteCustomerActivity/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomerActivity(int id)
        {
            try
            {
                ConfigureAuthHeader();
                var response = await _httpClient.DeleteAsync($"{GetApiBaseUrl()}/api/customereconomicactivity/{id}");
                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true });
                }

                var error = await response.Content.ReadAsStringAsync();
                return BadRequest(new { success = false, message = $"Error al eliminar actividad: {error}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando actividad {Id}", id);
                return StatusCode(500, new { success = false, message = "Error al eliminar la actividad." });
            }
        }

        /// <summary>
        /// Reenvía el cuerpo JSON de una actividad económica a la API (POST o PUT).
        /// </summary>
        private async Task<IActionResult> ForwardActivityAsync(HttpMethod method, string apiPath, JsonElement activity, string action)
        {
            try
            {
                ConfigureAuthHeader();

                var content = new StringContent(activity.GetRawText(), System.Text.Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(method, $"{GetApiBaseUrl()}{apiPath}")
                {
                    Content = content
                };
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true });
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error al {Action} actividad económica: {StatusCode} - {Error}", action, response.StatusCode, error);
                return BadRequest(new { success = false, message = $"Error al {action} la actividad: {error}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al {Action} actividad económica", action);
                return StatusCode(500, new { success = false, message = $"Error al {action} la actividad." });
            }
        }
    }
}
