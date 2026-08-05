// ================================================================================
// ARCHIVO: CMS.UI/Pages/Customers/Details.cshtml.cs
// PROPÓSITO: PageModel para ver detalles de customers
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
    public class DetailsModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(IHttpClientFactory httpClientFactory, ILogger<DetailsModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public Customer Customer { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var client = _httpClientFactory.CreateClient("CMSAPI");
            var token = HttpContext.Session.GetString("JWTToken");
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Account/Login");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var response = await client.GetAsync($"api/Customer/{id}");
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Customer no encontrado.";
                    return RedirectToPage("./Index");
                }

                var json = await response.Content.ReadAsStringAsync();
                Customer = JsonSerializer.Deserialize<Customer>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new Customer();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo customer {Id}", id);
                TempData["Error"] = "Error al obtener el customer.";
                return RedirectToPage("./Index");
            }
        }
    }
}
