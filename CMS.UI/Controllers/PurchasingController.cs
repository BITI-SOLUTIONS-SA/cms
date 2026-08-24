// ================================================================================
// ARCHIVO: CMS.UI/Controllers/PurchasingController.cs
// PROPÓSITO: Controlador de vistas para el módulo Purchasing (Compras / AP).
// DESCRIPCIÓN: Expone la pantalla de mantenimiento de vendors (proveedores) y sus
//              actividades económicas. Consume el API /api/Vendor.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.UI.Controllers
{
    [Authorize]
    public class PurchasingController : Controller
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public PurchasingController(
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
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

        private void SetApiViewBag()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = _httpContextAccessor.HttpContext?.Session.GetString("ApiToken")
                             ?? _httpContextAccessor.HttpContext?.Session.GetString("JwtToken")
                             ?? string.Empty;
        }

        // GET /Purchasing  => redirige a la pantalla de vendors
        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Vendors));

        // GET /Purchasing/Vendors
        [HttpGet]
        public IActionResult Vendors()
        {
            SetApiViewBag();
            return View();
        }
    }
}
