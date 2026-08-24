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

        /// <summary>Mantenimiento del catálogo central de tipos de documento electrónico.
        /// GET: /ElectronicInvoice/DocumentTypes</summary>
        public IActionResult DocumentTypes()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de versiones del esquema de documentos electrónicos.
        /// GET: /ElectronicInvoice/DocumentVersions</summary>
        public IActionResult DocumentVersions()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento de los consecutivos fiscales por emisor/sucursal/terminal/tipo/versión.
        /// GET: /ElectronicInvoice/DocumentConsecutives</summary>
        public IActionResult DocumentConsecutives()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de actividades económicas de Hacienda.
        /// GET: /ElectronicInvoice/EconomicActivities</summary>
        public IActionResult EconomicActivities()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de condiciones de venta de Hacienda.
        /// GET: /ElectronicInvoice/SalesConditions</summary>
        public IActionResult SalesConditions()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de tipos de identificación de Hacienda.
        /// GET: /ElectronicInvoice/IdentificationTypes</summary>
        public IActionResult IdentificationTypes()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de tipos de cliente (admin.customer_type).
        /// GET: /ElectronicInvoice/CustomerTypes</summary>
        public IActionResult CustomerTypes()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de medios de pago de Hacienda.
        /// GET: /ElectronicInvoice/PaymentMethods</summary>
        public IActionResult PaymentMethods()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de naturalezas de descuento de Hacienda.
        /// GET: /ElectronicInvoice/Discounts</summary>
        public IActionResult Discounts()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de tipos de impuesto de Hacienda.
        /// GET: /ElectronicInvoice/TaxTypes</summary>
        public IActionResult TaxTypes()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de códigos de tarifa del IVA de Hacienda.
        /// GET: /ElectronicInvoice/TaxRates</summary>
        public IActionResult TaxRates()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de códigos de unidad de medida de Hacienda (v4.4).
        /// GET: /ElectronicInvoice/UnitsOfMeasure</summary>
        public IActionResult UnitsOfMeasure()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de códigos CAByS para facturación electrónica.
        /// GET: /ElectronicInvoice/CabysCodes</summary>
        public IActionResult CabysCodes()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de tipos de documento de exoneración
        /// o de autorización de Hacienda.
        /// GET: /ElectronicInvoice/ExemptionAuthorizationTypes</summary>
        public IActionResult ExemptionAuthorizationTypes()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de nombre de institución o
        /// dependencia que emitió la exoneración (Hacienda CR v4.4).
        /// GET: /ElectronicInvoice/InstitutionDepartments</summary>
        public IActionResult InstitutionDepartments()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de "Otros cargos" (OtroCargo
        /// Hacienda CR v4.4).
        /// GET: /ElectronicInvoice/OtherCharges</summary>
        public IActionResult OtherCharges()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>Mantenimiento del catálogo central de "Tipo documento de referencia"
        /// (InformacionReferencia Hacienda CR v4.4).
        /// GET: /ElectronicInvoice/ReferenceTypes</summary>
        public IActionResult ReferenceTypes()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }
    }
}
