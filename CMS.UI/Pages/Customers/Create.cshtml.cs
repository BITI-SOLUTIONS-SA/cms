// ================================================================================
// ARCHIVO: CMS.UI/Pages/Customers/Create.cshtml.cs
// PROPÓSITO: Page Model para crear clientes
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CMS.UI.Pages.Customers
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(IHttpClientFactory httpClientFactory, ILogger<CreateModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [BindProperty]
        public Customer Customer { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            // Inicializar valores por defecto
            Customer.CustomerType = "Retail";
            Customer.IsActive = true;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Por favor corrija los errores en el formulario";
                return Page();
            }

            try
            {
                var client = _httpClientFactory.CreateClient("CMSAPI");
                var token = HttpContext.Session.GetString("jwt_token");

                if (string.IsNullOrEmpty(token))
                    return RedirectToPage("/Auth/Login");

                client.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);

                var json = JsonSerializer.Serialize(Customer, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("api/Customer", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = $"Cliente '{Customer.Name}' creado exitosamente";
                    return RedirectToPage("Index");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Error al crear el cliente: {error}";
                    _logger.LogError("Error al crear customer: {StatusCode} - {Error}", 
                        response.StatusCode, error);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error al conectar con el servidor";
                _logger.LogError(ex, "Error al crear customer");
            }

            return Page();
        }
    }
}
