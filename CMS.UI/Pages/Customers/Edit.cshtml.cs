// ================================================================================
// ARCHIVO: CMS.UI/Pages/Customers/Edit.cshtml.cs
// PROPÓSITO: PageModel para editar customers existentes
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
    public class EditModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EditModel> _logger;

        public EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [BindProperty]
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

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var client = _httpClientFactory.CreateClient("CMSAPI");
            var token = HttpContext.Session.GetString("JWTToken");
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Account/Login");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var json = JsonSerializer.Serialize(Customer);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PutAsync($"api/Customer/{Customer.Id}", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Customer actualizado exitosamente.";
                    return RedirectToPage("./Index");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Error al actualizar: {error}";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando customer {Id}", Customer.Id);
                TempData["Error"] = "Error al actualizar el customer.";
                return Page();
            }
        }
    }
}
