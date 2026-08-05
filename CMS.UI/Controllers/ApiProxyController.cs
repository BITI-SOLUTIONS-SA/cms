// ================================================================================
// ARCHIVO: CMS.UI/Controllers/ApiProxyController.cs
// PROPÓSITO: Proxy local para llamadas a la API desde el frontend
// DESCRIPCIÓN: Permite que el frontend llame a rutas locales (/api/*) que se
//              reenvían a la API con el token JWT de la sesión
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-03-04
// ================================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CMS.UI.Controllers
{
    [Route("api")]
    [ApiController]
    [Authorize]
    public class ApiProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ApiProxyController> _logger;

        public ApiProxyController(
            IHttpClientFactory httpClientFactory,
            ILogger<ApiProxyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // =====================================================
        // USER SETTINGS
        // =====================================================

        /// <summary>
        /// GET /api/usersettings - Obtiene configuración del usuario
        /// </summary>
        [HttpGet("usersettings")]
        public async Task<IActionResult> GetUserSettings()
        {
            return await ProxyGetAsync("api/usersettings");
        }

        /// <summary>
        /// PUT /api/usersettings - Actualiza configuración del usuario
        /// </summary>
        [HttpPut("usersettings")]
        public async Task<IActionResult> UpdateUserSettings()
        {
            return await ProxyPutAsync("api/usersettings");
        }

        /// <summary>
        /// GET /api/usersettings/activity - Historial de actividad
        /// </summary>
        [HttpGet("usersettings/activity")]
        public async Task<IActionResult> GetActivityLog([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            return await ProxyGetAsync($"api/usersettings/activity?page={page}&pageSize={pageSize}");
        }

        /// <summary>
        /// POST /api/usersettings/activity - Registra actividad
        /// </summary>
        [HttpPost("usersettings/activity")]
        public async Task<IActionResult> LogActivity()
        {
            return await ProxyPostAsync("api/usersettings/activity");
        }

        // =====================================================
        // SUPPORT
        // =====================================================

        /// <summary>
        /// POST /api/support/request - Envía solicitud de soporte
        /// </summary>
        [HttpPost("support/request")]
        public async Task<IActionResult> SendSupportRequest()
        {
            return await ProxyPostAsync("api/support/request");
        }

        // =====================================================
        // FACTURACIÓN ELECTRÓNICA CR v4.4
        // =====================================================

        /// <summary>GET /api/cabys/search?q=... - Buscar códigos CAByS</summary>
        [HttpGet("cabys/search")]
        public Task<IActionResult> CabysSearch([FromQuery] string q, [FromQuery] int limit = 50)
            => ProxyGetAsync($"api/cabys/search?q={Uri.EscapeDataString(q ?? string.Empty)}&limit={limit}");

        /// <summary>GET /api/billingissuer - Listar emisores</summary>
        [HttpGet("billingissuer")]
        public Task<IActionResult> BillingIssuers() => ProxyGetAsync("api/billingissuer");

        /// <summary>POST /api/billingissuer - Crear emisor</summary>
        [HttpPost("billingissuer")]
        public Task<IActionResult> CreateBillingIssuer() => ProxyPostAsync("api/billingissuer");

        /// <summary>POST /api/billingissuer/{id}/credential - Cargar .p12 (cifrado AES-256)</summary>
        [HttpPost("billingissuer/{id:int}/credential")]
        public Task<IActionResult> UploadCredential(int id) => ProxyPostAsync($"api/billingissuer/{id}/credential");

        /// <summary>GET /api/billingissuer/{id}/credentials - Overview ambos ambientes</summary>
        [HttpGet("billingissuer/{id:int}/credentials")]
        public Task<IActionResult> CredentialsOverview(int id) => ProxyGetAsync($"api/billingissuer/{id}/credentials");

        /// <summary>PUT /api/billingissuer/{id}/active-environment - Cambiar ambiente activo</summary>
        [HttpPut("billingissuer/{id:int}/active-environment")]
        public Task<IActionResult> SetActiveEnvironment(int id) => ProxyPutAsync($"api/billingissuer/{id}/active-environment");

        /// <summary>GET /api/electronicinvoice - Listar comprobantes</summary>
        [HttpGet("electronicinvoice")]
        public Task<IActionResult> ElectronicInvoices([FromQuery] int? issuerId, [FromQuery] string? status, [FromQuery] int limit = 100)
            => ProxyGetAsync($"api/electronicinvoice?issuerId={issuerId}&status={status}&limit={limit}");

        /// <summary>POST /api/electronicinvoice/emit - Emitir comprobante</summary>
        [HttpPost("electronicinvoice/emit")]
        public Task<IActionResult> EmitInvoice() => ProxyPostAsync("api/electronicinvoice/emit");

        /// <summary>POST /api/electronicinvoice/{id}/process - Reprocesar</summary>
        [HttpPost("electronicinvoice/{id:int}/process")]
        public Task<IActionResult> ProcessInvoice(int id) => ProxyPostAsync($"api/electronicinvoice/{id}/process");

        /// <summary>GET /api/electronicinvoice/{id}/pdf - Descargar PDF</summary>
        [HttpGet("electronicinvoice/{id:int}/pdf")]
        public async Task<IActionResult> InvoicePdf(int id)
        {
            var client = GetAuthenticatedClient();
            var response = await client.GetAsync($"api/electronicinvoice/{id}/pdf");
            if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return File(bytes, "application/pdf", $"comprobante_{id}.pdf");
        }

        /// <summary>GET /api/electronicinvoice/{id}/xml - Descargar XML firmado</summary>
        [HttpGet("electronicinvoice/{id:int}/xml")]
        public async Task<IActionResult> InvoiceXml(int id)
        {
            var client = GetAuthenticatedClient();
            var response = await client.GetAsync($"api/electronicinvoice/{id}/xml");
            if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode);
            var xml = await response.Content.ReadAsStringAsync();
            return Content(xml, "application/xml");
        }

        // =====================================================
        // HELPERS
        // =====================================================

        private async Task<IActionResult> ProxyGetAsync(string path)
        {
            try
            {
                var client = GetAuthenticatedClient();
                var response = await client.GetAsync(path);

                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, 
                    TryParseJson(content) ?? content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en proxy GET {Path}", path);
                return StatusCode(500, new { message = "Error de conexión con el servidor" });
            }
        }

        private async Task<IActionResult> ProxyPostAsync(string path)
        {
            try
            {
                var client = GetAuthenticatedClient();
                
                // Leer el body del request
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                var content = new StringContent(body, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(path, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                return StatusCode((int)response.StatusCode, 
                    TryParseJson(responseContent) ?? responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en proxy POST {Path}", path);
                return StatusCode(500, new { message = "Error de conexión con el servidor" });
            }
        }

        private async Task<IActionResult> ProxyPutAsync(string path)
        {
            try
            {
                var client = GetAuthenticatedClient();
                
                // Leer el body del request
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                var content = new StringContent(body, Encoding.UTF8, "application/json");

                var response = await client.PutAsync(path, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                return StatusCode((int)response.StatusCode, 
                    TryParseJson(responseContent) ?? responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en proxy PUT {Path}", path);
                return StatusCode(500, new { message = "Error de conexión con el servidor" });
            }
        }

        private HttpClient GetAuthenticatedClient()
        {
            var client = _httpClientFactory.CreateClient("cmsapi-authenticated");
            var token = HttpContext.Session.GetString("ApiToken");

            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }

        private static object? TryParseJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(content);
            }
            catch
            {
                return null;
            }
        }
    }
}
