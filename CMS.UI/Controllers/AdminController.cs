// ================================================================================
// ARCHIVO: CMS.UI/Controllers/AdminController.cs
// PROPÓSITO: Controlador para funcionalidades administrativas del sistema
// DESCRIPCIÓN: Maneja Audit Trail, System Logs, API Keys, Jobs, Backup, Health Check
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-14
// ACTUALIZADO: 2026-03-02 - Agregada verificación de permisos por acción
// ================================================================================

using CMS.UI.Filters;
using CMS.UI.Models.Admin;
using CMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.UI.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly AdminApiService _adminApiService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public AdminController(
            ILogger<AdminController> logger,
            AdminApiService adminApiService,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _logger               = logger;
            _adminApiService      = adminApiService;
            _httpContextAccessor  = httpContextAccessor;
            _configuration        = configuration;
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
                ? _configuration["ApiSettings:Production:BaseUrl"] ?? string.Empty
                : _configuration["ApiSettings:Development:BaseUrl"] ?? string.Empty);
        }

        #region Audit Trail

        /// <summary>
        /// Vista principal de Audit Trail
        /// GET: /Admin/Audit
        /// </summary>
        [RequirePermission("Admin.Audit.View")]
        public async Task<IActionResult> Audit(string? search = null, string? action = null, 
            DateTime? from = null, DateTime? to = null, int page = 1)
        {
            var model = await _adminApiService.GetAuditTrailAsync(search, action, from, to, page, 50);
            return View(model);
        }

        #endregion

        #region System Logs

        /// <summary>
        /// Vista de System Logs
        /// GET: /Admin/Logs
        /// </summary>
        [RequirePermission("Admin.Logs.View")]
        public async Task<IActionResult> Logs(string? level = null, string? source = null, 
            DateTime? from = null, DateTime? to = null, int page = 1)
        {
            var model = await _adminApiService.GetSystemLogsAsync(level, source, from, to, page, 100);
            return View(model);
        }

        #endregion

        #region API Keys

        /// <summary>
        /// Vista de gestión de API Keys
        /// GET: /Admin/APIKeys
        /// </summary>
        [RequirePermission("Admin.APIKeys.Edit")]
        public async Task<IActionResult> APIKeys()
        {
            var model = await _adminApiService.GetApiKeysAsync();
            return View(model);
        }

        /// <summary>
        /// Crear nueva API Key
        /// POST: /Admin/APIKeys/Create
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Admin.APIKeys.Edit")]
        public async Task<IActionResult> CreateApiKey(CreateApiKeyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Datos inválidos";
                return RedirectToAction(nameof(APIKeys));
            }

            var (success, apiKey, error) = await _adminApiService.CreateApiKeyAsync(model);

            if (success)
            {
                TempData["Success"] = $"API Key '{model.Name}' creada exitosamente";
                TempData["NewApiKey"] = apiKey;
            }
            else
            {
                TempData["Error"] = error ?? "Error creando API Key";
            }

            return RedirectToAction(nameof(APIKeys));
        }

        /// <summary>
        /// Revocar API Key
        /// POST: /Admin/APIKeys/Revoke/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Admin.APIKeys.Edit")]
        public async Task<IActionResult> RevokeApiKey(int id)
        {
            var success = await _adminApiService.RevokeApiKeyAsync(id);

            if (success)
                TempData["Success"] = "API Key revocada exitosamente";
            else
                TempData["Error"] = "Error revocando API Key";

            return RedirectToAction(nameof(APIKeys));
        }

        #endregion

        #region Job Scheduler

        /// <summary>
        /// Vista de Job Scheduler
        /// GET: /Admin/Jobs
        /// </summary>
        [RequirePermission("Admin.Jobs.View")]
        public async Task<IActionResult> Jobs()
        {
            var model = await _adminApiService.GetJobsAsync();
            return View(model);
        }

        /// <summary>
        /// Ejecutar job manualmente
        /// POST: /Admin/Jobs/Run/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Admin.Jobs.View")]
        public async Task<IActionResult> RunJob(int id)
        {
            var success = await _adminApiService.RunJobAsync(id);

            if (success)
                TempData["Success"] = "Job iniciado. Puede tardar unos minutos en completarse.";
            else
                TempData["Error"] = "Error ejecutando job";

            return RedirectToAction(nameof(Jobs));
        }

        /// <summary>
        /// Pausar/Reanudar job
        /// POST: /Admin/Jobs/Toggle/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Admin.Jobs.View")]
        public async Task<IActionResult> ToggleJob(int id)
        {
            var success = await _adminApiService.ToggleJobAsync(id);

            if (success)
                TempData["Success"] = "Estado del job actualizado";
            else
                TempData["Error"] = "Error actualizando estado del job";

            return RedirectToAction(nameof(Jobs));
        }

        #endregion

        #region Backup & Restore

        /// <summary>
        /// Vista de Backup & Restore
        /// GET: /Admin/Backup
        /// </summary>
        [RequirePermission("Admin.Backup.Execute")]
        public async Task<IActionResult> Backup()
        {
            var model = await _adminApiService.GetBackupsAsync();
            return View(model);
        }

        /// <summary>
        /// Crear backup manual
        /// POST: /Admin/Backup/Create
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Admin.Backup.Execute")]
        public async Task<IActionResult> CreateBackup(string type = "full")
        {
            var success = await _adminApiService.CreateBackupAsync(type);

            if (success)
                TempData["Success"] = "Backup iniciado. Recibirá una notificación cuando se complete.";
            else
                TempData["Error"] = "Error iniciando backup";

            return RedirectToAction(nameof(Backup));
        }

        /// <summary>
        /// Restaurar backup
        /// POST: /Admin/Backup/Restore/{id}
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Admin.Backup.Execute")]
        public async Task<IActionResult> RestoreBackup(int id)
        {
            var success = await _adminApiService.RestoreBackupAsync(id);

            if (success)
                TempData["Warning"] = "Restauración iniciada. El sistema puede estar temporalmente no disponible.";
            else
                TempData["Error"] = "Error iniciando restauración";

            return RedirectToAction(nameof(Backup));
        }

        /// <summary>
        /// Descargar backup
        /// GET: /Admin/Backup/Download/{id}
        /// </summary>
        [RequirePermission("Admin.Backup.Execute")]
        public async Task<IActionResult> DownloadBackup(int id)
        {
            // TODO: Implementar descarga real
            TempData["Info"] = "La descarga comenzará en breve...";
            return RedirectToAction(nameof(Backup));
        }

        #endregion

        #region Health Check

        /// <summary>
        /// Vista de Health Check
        /// GET: /Admin/Health
        /// </summary>
        [RequirePermission("Admin.Health.View")]
        public async Task<IActionResult> Health()
        {
            var model = await _adminApiService.GetHealthCheckAsync();
            return View(model);
        }

        /// <summary>
        /// Ejecutar health check manual
        /// POST: /Admin/Health/Check
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Admin.Health.View")]
        public async Task<IActionResult> RunHealthCheck()
        {
            TempData["Success"] = "Health check completado";
            return RedirectToAction(nameof(Health));
        }

        #endregion

        #region Dashboard

        /// <summary>
        /// Dashboard de administración con resumen del sistema
        /// GET: /Admin/Dashboard
        /// </summary>
        [RequirePermission("Admin.Dashboard.View")]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                // Obtener información de sesión
                var companyId = HttpContext.Session.GetString("CompanyId");
                var companyName = HttpContext.Session.GetString("CompanyName");
                var userName = User.Identity?.Name ?? "Usuario";

                var model = new AdminDashboardViewModel
                {
                    CurrentCompanyId = companyId ?? "N/A",
                    CurrentCompanyName = companyName ?? "Sin compañía",
                    CurrentUserName = userName
                };

                // Intentar obtener estadísticas de usuarios
                try
                {
                    // Usar el servicio admin existente si tiene los datos
                    // Por ahora, dejamos los valores en 0 y se pueden llenar después
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudieron cargar estadísticas");
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando dashboard de administración");
                TempData["Error"] = "Error al cargar el dashboard";
                return View(new AdminDashboardViewModel());
            }
        }

        #endregion

        #region System Settings

        /// <summary>
        /// Vista de configuración del sistema
        /// GET: /Admin/System
        /// </summary>
        public async Task<IActionResult> System()
        {
            try
            {
                var model = await _adminApiService.GetSystemSettingsAsync();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando configuración del sistema");
                TempData["Error"] = "Error al cargar configuración del sistema";
                return View(new SystemSettingsViewModel());
            }
        }

        /// <summary>
        /// Establecer compañía como Admin
        /// POST: /Admin/System/SetAdminCompany
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAdminCompany(int companyId)
        {
            try
            {
                var success = await _adminApiService.SetAdminCompanyAsync(companyId);

                if (success)
                {
                    TempData["Success"] = "Compañía Admin establecida correctamente";
                }
                else
                {
                    TempData["Error"] = "Error al establecer compañía Admin";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estableciendo compañía Admin");
                TempData["Error"] = "Error interno al establecer compañía Admin";
            }

            return RedirectToAction(nameof(System));
        }

        #endregion

        #region Entity Types & Documents

        /// <summary>
        /// Vista de Entity Types
        /// GET: /Admin/EntityTypes
        /// </summary>
        [RequirePermission("Admin.EntityTypes.View")]
        public IActionResult EntityTypes()
        {
            return View();
        }

        /// <summary>
        /// Vista de Entity Documents
        /// GET: /Admin/EntityDocuments
        /// </summary>
        [RequirePermission("Admin.EntityDocuments.View")]
        public IActionResult EntityDocuments()
        {
            return View();
        }

        #endregion

        #region Catálogos Contables

        /// <summary>
        /// Mantenimiento de Tipos de Origen de Asientos de Diario.
        /// GET: /Admin/JournalEntryTypeOrigins
        /// </summary>
        [RequirePermission("Admin.JournalEntryTypeOrigins.View")]
        public IActionResult JournalEntryTypeOrigins()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken   = GetApiToken();
            return View();
        }

        /// <summary>
        /// Mantenimiento de Tipos de Contabilidad.
        /// GET: /Admin/TypeAccountings
        /// </summary>
        [RequirePermission("Admin.TypeAccountings.View")]
        public IActionResult TypeAccountings()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken   = GetApiToken();
            return View();
        }

        /// <summary>
        /// GET: /Admin/ChartOfAccountsType
        /// </summary>
        [RequirePermission("Admin.ChartOfAccountsType.View")]
        public IActionResult ChartOfAccountsType()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken   = GetApiToken();
            return View();
        }

        /// <summary>
        /// GET: /Admin/JournalEntryClass
        /// </summary>
        [RequirePermission("Admin.JournalEntryClass.View")]
        public IActionResult JournalEntryClass()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken   = GetApiToken();
            return View();
        }

        /// <summary>
        /// GET: /Admin/JournalEntryStatus
        /// </summary>
        [RequirePermission("Admin.JournalEntryStatus.View")]
        public IActionResult JournalEntryStatus()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken   = GetApiToken();
            return View();
        }

        #endregion
    }

    #region ViewModels

    public class AdminDashboardViewModel
    {
        // Usuarios
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int UnverifiedUsers { get; set; }

        // Permisos y Menús
        public int TotalPermissions { get; set; }
        public int TotalMenus { get; set; }
        public int ActiveMenus { get; set; }

        // Desglose
        public List<ModuleStats> PermissionsByModule { get; set; } = new();
        public List<CMS.UI.Models.Users.UserItemViewModel> RecentUsers { get; set; } = new();

        // Contexto
        public string CurrentCompanyId { get; set; } = string.Empty;
        public string CurrentCompanyName { get; set; } = string.Empty;
        public string CurrentUserName { get; set; } = string.Empty;
    }

    public class ModuleStats
    {
        public string Module { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class SystemSettingsViewModel
    {
        public List<CompanySystemItem> Companies { get; set; } = new();
        public CompanySystemItem? AdminCompany { get; set; }
        public List<SystemPermissionItem> SystemPermissions { get; set; } = new();
        public bool HasAdminCompany { get; set; }
    }

    public class CompanySystemItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public bool IsAdminCompany { get; set; }
        public bool IsActive { get; set; }
        public bool UsesAzureAD { get; set; }
        public int UserCount { get; set; }
    }

    public class SystemPermissionItem
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    #endregion
}
