// ================================================================================
// ARCHIVO: CMS.UI/Pages/Customers/Index.cshtml.cs
// PROPÓSITO: Page Model para listado de clientes
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CMS.UI.Pages.Customers
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public List<Customer> Customers { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("CMSAPI");
                var token = HttpContext.Session.GetString("jwt_token");

                if (string.IsNullOrEmpty(token))
                    return RedirectToPage("/Auth/Login");

                client.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);

                var response = await client.GetAsync("api/Customer?includeInactive=false");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Customers = JsonSerializer.Deserialize<List<Customer>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<Customer>();
                }
                else
                {
                    ErrorMessage = $"Error al cargar clientes: {response.ReasonPhrase}";
                    _logger.LogError("Error al cargar customers: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error al conectar con el servidor";
                _logger.LogError(ex, "Error al cargar customers");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("CMSAPI");
                var token = HttpContext.Session.GetString("jwt_token");

                if (string.IsNullOrEmpty(token))
                    return RedirectToPage("/Auth/Login");

                client.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);

                var response = await client.DeleteAsync($"api/Customer/{id}");

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Error al eliminar el cliente";
                    _logger.LogError("Error al eliminar customer {Id}: {StatusCode}", id, response.StatusCode);
                }
                else
                {
                    TempData["Success"] = "Cliente eliminado exitosamente";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar el cliente";
                _logger.LogError(ex, "Error al eliminar customer {Id}", id);
            }

            return RedirectToPage();
        }
    }
}
