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
        /// Formulario de creación de customer
        /// GET: /Customers/Create
        /// </summary>
        public IActionResult Create()
        {
            // Redirigir a la Razor Page
            return RedirectToPage("/Customers/Create");
        }

        /// <summary>
        /// Formulario de edición de customer
        /// GET: /Customers/Edit/{id}
        /// </summary>
        public IActionResult Edit(int id)
        {
            // Redirigir a la Razor Page
            return RedirectToPage("/Customers/Edit", new { id });
        }

        /// <summary>
        /// Vista de detalle de customer (solo lectura)
        /// GET: /Customers/Details/{id}
        /// </summary>
        public IActionResult Details(int id)
        {
            // Redirigir a la Razor Page
            return RedirectToPage("/Customers/Details", new { id });
        }
    }
}
