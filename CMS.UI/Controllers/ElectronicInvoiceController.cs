// ================================================================================
// ARCHIVO: CMS.UI/Controllers/ElectronicInvoiceController.cs
// PROPÓSITO: Controller UI del módulo de Facturación Electrónica CR v4.4
// DESCRIPCIÓN: Renderiza las vistas de emisores, credenciales y emisión de
//              comprobantes. Las llamadas de datos van vía /api/* (ApiProxyController).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.UI.Controllers
{
    [Authorize]
    public class ElectronicInvoiceController : Controller
    {
        private readonly ILogger<ElectronicInvoiceController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public ElectronicInvoiceController(
            ILogger<ElectronicInvoiceController> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        private string GetApiBaseUrl()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var baseUrl = _configuration[$"ApiSettings:{environment}:BaseUrl"];
            return baseUrl ?? (environment == "Production"
                ? "https://cms.biti-solutions.com"
                : "https://localhost:7001");
        }

        private string GetApiToken()
        {
            return _httpContextAccessor.HttpContext?.Session.GetString("ApiToken")
                ?? _httpContextAccessor.HttpContext?.Session.GetString("JwtToken")
                ?? string.Empty;
        }

        /// <summary>Listado y emisión de comprobantes. GET: /ElectronicInvoice</summary>
        public IActionResult Index()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }        /// <summary>Gestión de emisores facturadores. GET: /ElectronicInvoice/Issuers</summary>
        public IActionResult Issuers()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Formulario de emisión. GET: /ElectronicInvoice/Emit</summary>
        public IActionResult Emit()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }
    }
}
