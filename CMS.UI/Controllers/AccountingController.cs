// ================================================================================
// ARCHIVO: CMS.UI/Controllers/AccountingController.cs
// PROPÓSITO: Controller UI para gestión del módulo de Contabilidad
// DESCRIPCIÓN: Maneja las vistas del módulo de contabilidad: Plan de Cuentas,
//              Asientos de Diario, etc. Pasa configuración API/Token a las vistas.
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-20
// ================================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CMS.Data;
using Microsoft.EntityFrameworkCore;

namespace CMS.UI.Controllers
{
    [Authorize]
    public class AccountingController : Controller
    {
        private readonly ILogger<AccountingController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _dbContext;

        public AccountingController(
            ILogger<AccountingController> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            AppDbContext dbContext)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _dbContext = dbContext;
        }

        private string GetApiToken() =>
            _httpContextAccessor.HttpContext?.Session.GetString("ApiToken")
            ?? _httpContextAccessor.HttpContext?.Session.GetString("JwtToken")
            ?? string.Empty;

        private string GetApiBaseUrl()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var baseUrl = _configuration[$"ApiSettings:{environment}:BaseUrl"];
            return baseUrl ?? (environment == "Production"
                ? "https://cms.biti-solutions.com"
                : "https://localhost:7001");
        }

        /// <summary>
        /// Vista principal del Plan de Cuentas (Chart of Accounts)
        /// GET: /Accounting/ChartOfAccounts
        /// </summary>
        public IActionResult ChartOfAccounts()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>
        /// Vista de Asientos de Diario (Journal Entries)
        /// GET: /Accounting/JournalEntries
        /// </summary>
        public async Task<IActionResult> JournalEntries()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();

            // ⭐ Obtener el ID del menú actual desde la base de datos
            // El sistema de consecutivos usa esto para búsqueda jerárquica en sinai.consecutive
            ViewBag.CurrentMenuId = await GetMenuIdByUrlAsync("/Accounting/JournalEntries");

            return View();
        }

        /// <summary>
        /// Obtiene el ID del menú desde la base de datos según la URL
        /// </summary>
        private async Task<int> GetMenuIdByUrlAsync(string url)
        {
            try
            {
                var menu = await _dbContext.Menus
                    .Where(m => m.URL == url && m.IS_ACTIVE)
                    .Select(m => m.ID_MENU)
                    .FirstOrDefaultAsync();

                if (menu == 0)
                {
                    _logger.LogWarning("No se encontró menú activo para URL: {Url}", url);
                    // Retornar 0 si no existe - el backend debe manejar el error apropiadamente
                }

                return menu;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo menú para URL: {Url}", url);
                return 0;
            }
        }

        /// <summary>
        /// Vista de Centros de Costo (Cost Centers)
        /// GET: /Accounting/CostCenters
        /// </summary>
        public IActionResult CostCenters()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }

        /// <summary>
        /// Vista de Catálogos Generales (General Catalogs)
        /// GET: /Accounting/GeneralCatalogs
        /// </summary>
        public IActionResult GeneralCatalogs()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken = GetApiToken();
            return View();
        }
    }
}
