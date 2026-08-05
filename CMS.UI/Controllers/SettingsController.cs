// ================================================================================
// ARCHIVO: CMS.UI/Controllers/SettingsController.cs
// PROPÓSITO: Controller de la UI para gestión del módulo Settings (Admin)
// DESCRIPCIÓN: Maneja las vistas de gestión de usuarios, roles, permisos,
//              parámetros del sistema y backup/restore. Consume la API REST
//              a través del servicio SettingsApiService.
// ================================================================================

using CMS.Application.DTOs; // ⭐ AGREGAR ESTA LÍNEA
using CMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.UI.Controllers
{
    /// <summary>
    /// Controller que maneja todas las funcionalidades del módulo Settings.
    /// Requiere autenticación y en producción debería restringirse a roles de Admin.
    /// Actúa como intermediario entre las vistas Razor y la API REST.
    /// </summary>
    [Authorize] // En producción cambiar a: [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly SettingsApiService _api;
        private readonly ILogger<SettingsController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public SettingsController(
            SettingsApiService api,
            ILogger<SettingsController> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _api = api;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
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

        // =====================================================
        // USUARIOS
        // =====================================================

        /// <summary>
        /// Muestra la vista principal de gestión de usuarios.
        /// Lista todos los usuarios del sistema con sus roles asignados.
        /// GET: /Settings/Users
        /// </summary>
        /// <returns>Vista Users.cshtml con lista de UserListDto</returns>
        public async Task<IActionResult> Users()
        {
            var users = await _api.GetUsersAsync();
            return View(users ?? new List<UserListDto>());
        }

        /// <summary>
        /// Muestra el detalle completo de un usuario específico.
        /// Incluye roles asignados y permisos directos.
        /// GET: /Settings/UserDetail/{id}
        /// </summary>
        /// <param name="id">ID del usuario a visualizar</param>
        /// <returns>Vista UserDetail.cshtml con UserDetailDto o NotFound</returns>
        public async Task<IActionResult> UserDetail(int id)
        {
            var user = await _api.GetUserByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("Usuario {UserId} no encontrado", id);
                return NotFound();
            }

            // Obtener todos los roles disponibles para el modal de asignación
            ViewBag.AllRoles = await _api.GetRolesAsync() ?? new List<RoleListDto>();

            return View(user);
        }

        // =====================================================
        // ROLES
        // =====================================================

        /// <summary>
        /// Muestra la vista principal de gestión de roles.
        /// Lista todos los roles del sistema con contadores de usuarios y permisos.
        /// GET: /Settings/Roles
        /// </summary>
        /// <returns>Vista Roles.cshtml con lista de RoleListDto</returns>
        public async Task<IActionResult> Roles()
        {
            var roles = await _api.GetRolesAsync();
            return View(roles ?? new List<RoleListDto>());
        }

        /// <summary>
        /// Muestra el detalle completo de un rol específico.
        /// Incluye permisos asignados y usuarios que tienen este rol.
        /// GET: /Settings/RoleDetail/{id}
        /// </summary>
        /// <param name="id">ID del rol a visualizar</param>
        /// <returns>Vista RoleDetail.cshtml con RoleDetailDto o NotFound</returns>
        public async Task<IActionResult> RoleDetail(int id)
        {
            var role = await _api.GetRoleByIdAsync(id);
            if (role == null)
            {
                _logger.LogWarning("Rol {RoleId} no encontrado", id);
                return NotFound();
            }

            // Obtener todos los permisos disponibles para el modal de asignación
            ViewBag.AllPermissions = await _api.GetPermissionsAsync() ?? new List<PermissionListDto>();

            return View(role);
        }

        // =====================================================
        // PERMISOS
        // =====================================================

        /// <summary>
        /// Muestra la vista principal de gestión de permisos.
        /// Lista todos los permisos del sistema agrupados por módulo.
        /// GET: /Settings/Permissions
        /// </summary>
        /// <returns>Vista Permissions.cshtml con lista de PermissionListDto</returns>
        public async Task<IActionResult> Permissions()
        {
            var permissions = await _api.GetPermissionsAsync();

            // Obtener lista de módulos únicos para filtrado en la vista
            ViewBag.Modules = await _api.GetModulesAsync() ?? new List<string>();

            return View(permissions ?? new List<PermissionListDto>());
        }

        // =====================================================
        // PARÁMETROS DEL SISTEMA
        // =====================================================

        /// <summary>
        /// Muestra la vista de gestión de parámetros del sistema.
        /// Permite configurar valores globales como URLs, timeouts, límites, etc.
        /// GET: /Settings/Parameters
        /// </summary>
        /// <returns>Vista Parameters.cshtml</returns>
        public IActionResult Parameters()
        {
            // TODO: Implementar gestión de parámetros del sistema
            // Crear tabla SYSTEM_PARAMETERS con campos: Key, Value, Description, Type
            // Ejemplos: "MaxUploadSize", "SessionTimeout", "EmailServer", etc.
            _logger.LogInformation("Acceso a la vista de parámetros del sistema");
            return View();
        }

        // =====================================================
        // BACKUP Y RESTORE
        // =====================================================

        /// <summary>
        /// Muestra la vista de backup y restore de la base de datos.
        /// Permite crear backups manuales y restaurar desde backups existentes.
        /// GET: /Settings/Backup
        /// </summary>
        /// <returns>Vista Backup.cshtml</returns>
        public IActionResult Backup()
        {
            _logger.LogInformation("Acceso a la vista de backup y restore");
            return View();
        }

        // =====================================================
        // CATÁLOGOS FLEET MANAGEMENT
        // =====================================================

        /// <summary>
        /// Pantalla de mantenimiento de catálogos Fleet:
        /// Tipo de Unidad, Estado, Combustible, Marca y Modelo.
        /// GET: /Settings/FleetCatalogs
        /// </summary>
        public IActionResult FleetCatalogs()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken   = GetApiToken();
            return View();
        }

        // =====================================================
        // GLOBAL PARAMETERS
        // =====================================================

        /// <summary>
        /// Pantalla de mantenimiento de Parámetros Globales del Sistema por módulo.
        /// GET: /Settings/GlobalParameters
        /// </summary>
        public IActionResult GlobalParameters()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken   = GetApiToken();
            return View();
        }

        // =====================================================
        // CATÁLOGOS INVENTORY
        // =====================================================

        /// <summary>
        /// Pantalla de mantenimiento del catálogo de Tipos de Movimiento de Inventario.
        /// GET: /Settings/InventoryTransactionTypes
        /// </summary>
        public IActionResult InventoryTransactionTypes()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken   = GetApiToken();
            return View();
        }

        // =====================================================
        // CONSECUTIVOS
        // =====================================================

        /// <summary>
        /// Pantalla de mantenimiento de Consecutivos de Documentos.
        /// GET: /Settings/Consecutives
        /// </summary>
        public IActionResult Consecutives()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken   = GetApiToken();
            return View();
        }

        // =====================================================
        // TIPOS DE TASA DE CAMBIO
        // =====================================================

        /// <summary>
        /// Pantalla de mantenimiento de Tipos de Tasa de Cambio.
        /// GET: /Settings/ExchangeRates
        /// </summary>
        public IActionResult ExchangeRates()
        {
            ViewBag.ApiBaseUrl = GetApiBaseUrl();
            ViewBag.ApiToken   = GetApiToken();
            return View();
        }
    }
}