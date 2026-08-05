// ================================================================================
// ARCHIVO: CMS.API/Controllers/UserSettingsController.cs
// PROPÓSITO: API para gestión de configuración de usuario y historial de actividad
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-03-04
// ================================================================================

using CMS.Data;
using CMS.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserSettingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserSettingsController> _logger;

        public UserSettingsController(AppDbContext context, ILogger<UserSettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene la configuración del usuario actual
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<UserSettingsDto>> GetSettings()
        {
            var userId = GetUserIdFromClaims();
            if (userId == 0)
                return Unauthorized();

            var settings = await _context.UserSettings
                .FirstOrDefaultAsync(s => s.ID_USER == userId);

            if (settings == null)
            {
                // Crear configuración por defecto
                settings = new UserSettings
                {
                    ID_USER = userId,
                    THEME = "dark",
                    SIDEBAR_COMPACT = true,
                    NOTIFY_EMAIL = true,
                    NOTIFY_BROWSER = true,
                    NOTIFY_SOUND = false,
                    LANGUAGE = "es",
                    TIMEZONE = "America/Costa_Rica",
                    DATE_FORMAT = "dd/MM/yyyy",
                    TIME_FORMAT = "24h"
                };
                _context.UserSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return Ok(MapToDto(settings));
        }

        /// <summary>
        /// Actualiza la configuración del usuario
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<UserSettingsDto>> UpdateSettings([FromBody] UserSettingsDto dto)
        {
            var userId = GetUserIdFromClaims();
            if (userId == 0)
                return Unauthorized();

            var settings = await _context.UserSettings
                .FirstOrDefaultAsync(s => s.ID_USER == userId);

            if (settings == null)
            {
                settings = new UserSettings { ID_USER = userId };
                _context.UserSettings.Add(settings);
            }

            // Actualizar campos
            settings.THEME = dto.Theme ?? "dark";
            settings.SIDEBAR_COMPACT = dto.SidebarCompact;
            settings.NOTIFY_EMAIL = dto.NotifyEmail;
            settings.NOTIFY_BROWSER = dto.NotifyBrowser;
            settings.NOTIFY_SOUND = dto.NotifySound;
            settings.LANGUAGE = dto.Language ?? "es";
            settings.TIMEZONE = dto.Timezone ?? "America/Costa_Rica";
            settings.DATE_FORMAT = dto.DateFormat ?? "dd/MM/yyyy";
            settings.TIME_FORMAT = dto.TimeFormat ?? "24h";
            settings.UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";
            settings.RecordDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Registrar actividad
            await LogActivityAsync(userId, ActivityTypes.SETTINGS_CHANGE, "Configuración actualizada");

            _logger.LogInformation("✅ Configuración actualizada para usuario: {UserId}", userId);

            return Ok(MapToDto(settings));
        }

        /// <summary>
        /// Obtiene el historial de actividad del usuario (paginado)
        /// </summary>
        [HttpGet("activity")]
        public async Task<ActionResult<ActivityLogPagedResult>> GetActivityLog(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = GetUserIdFromClaims();
            if (userId == 0)
                return Unauthorized();

            // Máximo 3 páginas de 10 = 30 registros
            if (page < 1) page = 1;
            if (page > 3) page = 3;
            if (pageSize > 10) pageSize = 10;

            var query = _context.UserActivityLogs
                .Where(a => a.ID_USER == userId)
                .OrderByDescending(a => a.ACTIVITY_DATE);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages > 3) totalPages = 3; // Máximo 3 páginas

            var activities = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new ActivityLogDto
                {
                    Id = a.ID_USER_ACTIVITY_LOG,
                    ActivityType = a.ACTIVITY_TYPE,
                    Description = a.ACTIVITY_DESCRIPTION,
                    IpAddress = a.IP_ADDRESS,
                    DeviceInfo = a.DEVICE_INFO,
                    IsSuccess = a.IS_SUCCESS,
                    ActivityDate = a.ACTIVITY_DATE
                })
                .ToListAsync();

            return Ok(new ActivityLogPagedResult
            {
                Activities = activities,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = Math.Min(totalCount, 30) // Máximo 30 registros
            });
        }

        /// <summary>
        /// Registra una actividad del usuario
        /// </summary>
        [HttpPost("activity")]
        public async Task<ActionResult> LogActivity([FromBody] LogActivityRequest request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == 0)
                return Unauthorized();

            await LogActivityAsync(userId, request.ActivityType, request.Description);

            return Ok();
        }

        private async Task LogActivityAsync(int userId, string activityType, string description)
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            int? companyId = int.TryParse(companyIdClaim, out var cid) ? cid : null;

            var log = new UserActivityLog
            {
                ID_USER = userId,
                ID_COMPANY = companyId,
                ACTIVITY_TYPE = activityType,
                ACTIVITY_DESCRIPTION = description,
                IP_ADDRESS = HttpContext.Connection.RemoteIpAddress?.ToString(),
                USER_AGENT = Request.Headers.UserAgent.ToString(),
                DEVICE_INFO = GetDeviceInfo(Request.Headers.UserAgent.ToString()),
                IS_SUCCESS = true,
                ACTIVITY_DATE = DateTime.UtcNow
            };

            _context.UserActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        private string GetDeviceInfo(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Desconocido";
            if (userAgent.Contains("Windows")) return "Windows PC";
            if (userAgent.Contains("Mac")) return "Mac";
            if (userAgent.Contains("iPhone")) return "iPhone";
            if (userAgent.Contains("Android")) return "Android";
            if (userAgent.Contains("Linux")) return "Linux";
            return "Navegador Web";
        }

        private int GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("userId")?.Value;

            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        private static UserSettingsDto MapToDto(UserSettings s) => new()
        {
            Theme = s.THEME,
            SidebarCompact = s.SIDEBAR_COMPACT,
            NotifyEmail = s.NOTIFY_EMAIL,
            NotifyBrowser = s.NOTIFY_BROWSER,
            NotifySound = s.NOTIFY_SOUND,
            Language = s.LANGUAGE,
            Timezone = s.TIMEZONE,
            DateFormat = s.DATE_FORMAT,
            TimeFormat = s.TIME_FORMAT
        };
    }

    // =====================================================
    // DTOs
    // =====================================================

    public class UserSettingsDto
    {
        public string? Theme { get; set; } = "dark";
        public bool SidebarCompact { get; set; } = true;
        public bool NotifyEmail { get; set; } = true;
        public bool NotifyBrowser { get; set; } = true;
        public bool NotifySound { get; set; }
        public string? Language { get; set; } = "es";
        public string? Timezone { get; set; } = "America/Costa_Rica";
        public string? DateFormat { get; set; } = "dd/MM/yyyy";
        public string? TimeFormat { get; set; } = "24h";
    }

    public class ActivityLogDto
    {
        public int Id { get; set; }
        public string ActivityType { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string? IpAddress { get; set; }
        public string? DeviceInfo { get; set; }
        public bool IsSuccess { get; set; }
        public DateTime ActivityDate { get; set; }
    }

    public class ActivityLogPagedResult
    {
        public List<ActivityLogDto> Activities { get; set; } = new();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }

    public class LogActivityRequest
    {
        public string ActivityType { get; set; } = default!;
        public string Description { get; set; } = default!;
    }
}
