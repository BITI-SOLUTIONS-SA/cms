// ================================================================================
// ARCHIVO: CMS.API/Controllers/AdminController.cs
// PROPÓSITO: Endpoints de API para funcionalidades administrativas
// DESCRIPCIÓN: Audit Trail, System Logs, API Keys, Jobs, Backup, Health Check, System Config
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-14
// ================================================================================

using CMS.Data;
using CMS.Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly CompanyConfigService _companyService;
        private readonly ILogger<AdminController> _logger;
        private readonly IConfiguration _configuration;

        public AdminController(
            AppDbContext db, 
            CompanyConfigService companyService,
            ILogger<AdminController> logger, 
            IConfiguration configuration)
        {
            _db = db;
            _companyService = companyService;
            _logger = logger;
            _configuration = configuration;
        }

        #region Health Check

        /// <summary>
        /// Health check general del sistema
        /// GET: api/admin/health
        /// </summary>
        [AllowAnonymous]
        [HttpGet("health")]
        public async Task<IActionResult> GetHealth()
        {
            var health = new HealthCheckResult
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Services = new List<ServiceHealth>(),
                Metrics = new SystemMetrics()
            };

            // Check Database
            var dbHealth = await CheckDatabaseHealth();
            health.Services.Add(dbHealth);

            // Check API
            health.Services.Add(new ServiceHealth
            {
                Name = "CMS.API",
                Status = "Healthy",
                ResponseTime = 1,
                Message = "API responding",
                Endpoint = "localhost:8080"
            });

            // Get System Metrics
            health.Metrics = GetSystemMetrics();

            // Determine overall status
            if (health.Services.Any(s => s.Status == "Unhealthy"))
                health.Status = "Unhealthy";
            else if (health.Services.Any(s => s.Status == "Degraded"))
                health.Status = "Degraded";

            return Ok(health);
        }

        private async Task<ServiceHealth> CheckDatabaseHealth()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await _db.Database.ExecuteSqlRawAsync("SELECT 1");
                sw.Stop();

                return new ServiceHealth
                {
                    Name = "PostgreSQL Database",
                    Status = sw.ElapsedMilliseconds > 1000 ? "Degraded" : "Healthy",
                    ResponseTime = (int)sw.ElapsedMilliseconds,
                    Message = $"Connection OK",
                    Endpoint = "cms-postgres:5432"
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Database health check failed");
                return new ServiceHealth
                {
                    Name = "PostgreSQL Database",
                    Status = "Unhealthy",
                    ResponseTime = (int)sw.ElapsedMilliseconds,
                    Message = ex.Message,
                    Endpoint = "cms-postgres:5432"
                };
            }
        }

        private SystemMetrics GetSystemMetrics()
        {
            var process = Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64;
            var totalMemory = GC.GetTotalMemory(false);

            return new SystemMetrics
            {
                CpuUsage = 0, // Requiere implementación más compleja
                MemoryUsage = Math.Round((double)workingSet / (1024 * 1024 * 1024) * 100 / 4, 1), // Asumiendo 4GB total
                MemoryUsed = $"{workingSet / (1024 * 1024)} MB",
                MemoryTotal = "4 GB",
                DiskUsage = 0,
                DiskUsed = "N/A",
                DiskTotal = "N/A",
                ActiveConnections = 0,
                RequestsPerMinute = 0,
                AverageResponseTime = 0,
                UptimeSeconds = (int)(DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds
            };
        }

        #endregion

        #region Audit Trail

        /// <summary>
        /// Obtener registros de auditoría
        /// GET: api/admin/audit
        /// </summary>
        [HttpGet("audit")]
        public async Task<IActionResult> GetAuditTrail(
            [FromQuery] string? search = null,
            [FromQuery] string? action = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                // TODO: Implementar tabla de auditoría real
                // Por ahora retornamos datos de ejemplo
                var entries = new List<AuditEntry>
                {
                    new() { Id = 1, Timestamp = DateTime.UtcNow.AddMinutes(-5), User = "ernesto.martinez", Action = "LOGIN", Entity = "User", EntityId = "1", Details = "Inicio de sesión exitoso", IpAddress = "192.168.1.100" },
                    new() { Id = 2, Timestamp = DateTime.UtcNow.AddMinutes(-15), User = "admin", Action = "UPDATE", Entity = "Company", EntityId = "1", Details = "Actualización de configuración", IpAddress = "192.168.1.101" },
                    new() { Id = 3, Timestamp = DateTime.UtcNow.AddMinutes(-30), User = "system", Action = "CREATE", Entity = "User", EntityId = "5", Details = "Usuario creado", IpAddress = "127.0.0.1" },
                };

                return Ok(new
                {
                    entries,
                    totalCount = entries.Count,
                    page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo audit trail");
                return StatusCode(500, new { message = "Error obteniendo registros de auditoría" });
            }
        }

        /// <summary>
        /// Registrar evento de auditoría
        /// POST: api/admin/audit
        /// </summary>
        [HttpPost("audit")]
        public async Task<IActionResult> LogAuditEvent([FromBody] AuditEntry entry)
        {
            try
            {
                // TODO: Guardar en tabla de auditoría
                _logger.LogInformation("Audit: {Action} on {Entity} by {User}", 
                    entry.Action, entry.Entity, entry.User);
                
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando evento de auditoría");
                return StatusCode(500, new { message = "Error registrando evento" });
            }
        }

        #endregion

        #region System Logs

        /// <summary>
        /// Obtener logs del sistema
        /// GET: api/admin/logs
        /// </summary>
        [HttpGet("logs")]
        public async Task<IActionResult> GetSystemLogs(
            [FromQuery] string? level = null,
            [FromQuery] string? source = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            try
            {
                // TODO: Implementar lectura de logs reales (archivo o base de datos)
                var logs = new List<LogEntry>
                {
                    new() { Id = 1, Timestamp = DateTime.UtcNow.AddSeconds(-30), Level = "INFO", Source = "CMS.API", Message = "Request completed: GET /api/menu - 200 OK", CorrelationId = Guid.NewGuid().ToString() },
                    new() { Id = 2, Timestamp = DateTime.UtcNow.AddMinutes(-1), Level = "WARN", Source = "CMS.UI", Message = "Session validation warning", CorrelationId = Guid.NewGuid().ToString() },
                    new() { Id = 3, Timestamp = DateTime.UtcNow.AddMinutes(-5), Level = "ERROR", Source = "CMS.API", Message = "Database connection timeout", CorrelationId = Guid.NewGuid().ToString() },
                };

                if (!string.IsNullOrEmpty(level))
                    logs = logs.Where(l => l.Level == level).ToList();

                if (!string.IsNullOrEmpty(source))
                    logs = logs.Where(l => l.Source == source).ToList();

                return Ok(new
                {
                    entries = logs,
                    totalCount = logs.Count,
                    page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo logs");
                return StatusCode(500, new { message = "Error obteniendo logs" });
            }
        }

        #endregion

        #region API Keys

        /// <summary>
        /// Listar API Keys
        /// GET: api/admin/apikeys
        /// </summary>
        [HttpGet("apikeys")]
        public async Task<IActionResult> GetApiKeys()
        {
            try
            {
                // TODO: Implementar tabla de API keys
                var keys = new List<ApiKeyInfo>
                {
                    new() { Id = 1, Name = "Mobile App", KeyPrefix = "pk_live_****", Environment = "Production", CreatedAt = DateTime.UtcNow.AddMonths(-3), IsActive = true, Permissions = new[] { "read:users", "read:menu" } },
                    new() { Id = 2, Name = "Integration", KeyPrefix = "pk_test_****", Environment = "Development", CreatedAt = DateTime.UtcNow.AddDays(-30), IsActive = true, Permissions = new[] { "read:all", "write:all" } },
                };

                return Ok(keys);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo API keys");
                return StatusCode(500, new { message = "Error obteniendo API keys" });
            }
        }

        /// <summary>
        /// Crear API Key
        /// POST: api/admin/apikeys
        /// </summary>
        [HttpPost("apikeys")]
        public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKeyRequest request)
        {
            try
            {
                var apiKey = $"pk_{request.Environment.ToLower()}_{Guid.NewGuid():N}";
                
                // TODO: Guardar en base de datos (hash de la key)
                _logger.LogInformation("API Key created: {Name} for {Environment}", request.Name, request.Environment);

                return Ok(new
                {
                    id = 1,
                    name = request.Name,
                    apiKey, // Solo se muestra una vez
                    environment = request.Environment,
                    createdAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando API key");
                return StatusCode(500, new { message = "Error creando API key" });
            }
        }

        /// <summary>
        /// Revocar API Key
        /// DELETE: api/admin/apikeys/{id}
        /// </summary>
        [HttpDelete("apikeys/{id}")]
        public async Task<IActionResult> RevokeApiKey(int id)
        {
            try
            {
                // TODO: Marcar como revocada en base de datos
                _logger.LogInformation("API Key revoked: {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revocando API key {Id}", id);
                return StatusCode(500, new { message = "Error revocando API key" });
            }
        }

        #endregion

        #region Job Scheduler

        /// <summary>
        /// Listar jobs programados
        /// GET: api/admin/jobs
        /// </summary>
        [HttpGet("jobs")]
        public async Task<IActionResult> GetJobs()
        {
            try
            {
                // TODO: Implementar integración con scheduler real (Hangfire, Quartz, etc.)
                var jobs = new List<ScheduledJob>
                {
                    new() { Id = 1, Name = "Database Backup", Description = "Backup completo", CronExpression = "0 2 * * *", NextRun = DateTime.UtcNow.Date.AddDays(1).AddHours(2), LastStatus = "Success", IsEnabled = true },
                    new() { Id = 2, Name = "Email Queue", Description = "Procesa emails pendientes", CronExpression = "*/5 * * * *", NextRun = DateTime.UtcNow.AddMinutes(5), LastStatus = "Success", IsEnabled = true },
                    new() { Id = 3, Name = "Report Generator", Description = "Genera reportes diarios", CronExpression = "0 6 * * *", NextRun = DateTime.UtcNow.Date.AddDays(1).AddHours(6), LastStatus = "Failed", IsEnabled = true },
                };

                return Ok(new { jobs });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo jobs");
                return StatusCode(500, new { message = "Error obteniendo jobs" });
            }
        }

        /// <summary>
        /// Ejecutar job manualmente
        /// POST: api/admin/jobs/{id}/run
        /// </summary>
        [HttpPost("jobs/{id}/run")]
        public async Task<IActionResult> RunJob(int id)
        {
            try
            {
                // TODO: Ejecutar job
                _logger.LogInformation("Job triggered manually: {Id}", id);
                return Ok(new { success = true, message = "Job iniciado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando job {Id}", id);
                return StatusCode(500, new { message = "Error ejecutando job" });
            }
        }

        /// <summary>
        /// Pausar/Reanudar job
        /// POST: api/admin/jobs/{id}/toggle
        /// </summary>
        [HttpPost("jobs/{id}/toggle")]
        public async Task<IActionResult> ToggleJob(int id)
        {
            try
            {
                // TODO: Cambiar estado del job
                _logger.LogInformation("Job toggled: {Id}", id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling job {Id}", id);
                return StatusCode(500, new { message = "Error cambiando estado del job" });
            }
        }

        #endregion

        #region Backup & Restore

        /// <summary>
        /// Listar backups disponibles
        /// GET: api/admin/backups
        /// </summary>
        [HttpGet("backups")]
        public async Task<IActionResult> GetBackups()
        {
            try
            {
                // TODO: Leer backups reales del storage
                var backups = new List<BackupInfo>
                {
                    new() { Id = 1, Name = "backup_20260214_020000.sql.gz", Type = "Full", CreatedAt = DateTime.UtcNow.AddHours(-22), Size = "2.5 GB", Status = "Completed", CreatedBy = "Scheduled" },
                    new() { Id = 2, Name = "backup_20260213_020000.sql.gz", Type = "Full", CreatedAt = DateTime.UtcNow.AddDays(-1), Size = "2.4 GB", Status = "Completed", CreatedBy = "Scheduled" },
                };

                return Ok(new
                {
                    backups,
                    storageUsed = "45.2 GB",
                    storageLimit = "100 GB",
                    storagePercentage = 45
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo backups");
                return StatusCode(500, new { message = "Error obteniendo backups" });
            }
        }

        /// <summary>
        /// Crear backup manual
        /// POST: api/admin/backups
        /// </summary>
        [HttpPost("backups")]
        public async Task<IActionResult> CreateBackup([FromBody] CreateBackupRequest? request = null)
        {
            try
            {
                var type = request?.Type ?? "full";
                
                // TODO: Ejecutar pg_dump o backup real
                _logger.LogInformation("Backup initiated: {Type}", type);

                return Ok(new
                {
                    success = true,
                    message = "Backup iniciado",
                    jobId = Guid.NewGuid()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando backup");
                return StatusCode(500, new { message = "Error creando backup" });
            }
        }

        /// <summary>
        /// Restaurar backup
        /// POST: api/admin/backups/{id}/restore
        /// </summary>
        [HttpPost("backups/{id}/restore")]
        public async Task<IActionResult> RestoreBackup(int id)
        {
            try
            {
                // TODO: Ejecutar restauración
                _logger.LogWarning("Backup restore initiated: {Id}", id);

                return Ok(new
                {
                    success = true,
                    message = "Restauración iniciada",
                    jobId = Guid.NewGuid()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restaurando backup {Id}", id);
                return StatusCode(500, new { message = "Error restaurando backup" });
            }
        }

        /// <summary>
        /// Descargar backup
        /// GET: api/admin/backups/{id}/download
        /// </summary>
        [HttpGet("backups/{id}/download")]
        public async Task<IActionResult> DownloadBackup(int id)
        {
            try
            {
                // TODO: Retornar archivo de backup
                return Ok(new { message = "Download URL generada", url = $"/backups/{id}/file" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error descargando backup {Id}", id);
                return StatusCode(500, new { message = "Error descargando backup" });
            }
        }

        #endregion

        #region System Configuration

        /// <summary>
        /// Obtener todas las compañías con información de Admin
        /// GET: api/admin/system/companies
        /// </summary>
        [HttpGet("system/companies")]
        public async Task<IActionResult> GetSystemCompanies()
        {
            try
            {
                // Verificar permiso
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no identificado" });

                var hasPermission = await _companyService.HasViewAllCompaniesPermissionAsync(userId.Value);
                if (!hasPermission)
                    return Forbid();

                var companies = await _db.Companies
                    .Where(c => c.IS_ACTIVE)
                    .OrderBy(c => c.COMPANY_NAME)
                    .Select(c => new CompanySystemInfo
                    {
                        Id = c.ID,
                        Name = c.COMPANY_NAME,
                        Schema = c.COMPANY_SCHEMA,
                        IsAdminCompany = c.IS_ADMIN_COMPANY,
                        IsActive = c.IS_ACTIVE,
                        UsesAzureAD = c.USES_AZURE_AD,
                        UserCount = _db.UserCompanies.Count(uc => uc.ID_COMPANY == c.ID && uc.IS_ACTIVE)
                    })
                    .ToListAsync();

                return Ok(companies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo compañías del sistema");
                return StatusCode(500, new { message = "Error obteniendo compañías" });
            }
        }

        /// <summary>
        /// Obtener la compañía Admin actual
        /// GET: api/admin/system/admin-company
        /// </summary>
        [HttpGet("system/admin-company")]
        public async Task<IActionResult> GetAdminCompany()
        {
            try
            {
                var adminCompany = await _companyService.GetAdminCompanyAsync();

                if (adminCompany == null)
                {
                    return Ok(new { hasAdminCompany = false, company = (object?)null });
                }

                return Ok(new
                {
                    hasAdminCompany = true,
                    company = new CompanySystemInfo
                    {
                        Id = adminCompany.ID,
                        Name = adminCompany.COMPANY_NAME,
                        Schema = adminCompany.COMPANY_SCHEMA,
                        IsAdminCompany = true,
                        IsActive = adminCompany.IS_ACTIVE,
                        UsesAzureAD = adminCompany.USES_AZURE_AD
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo compañía Admin");
                return StatusCode(500, new { message = "Error obteniendo compañía Admin" });
            }
        }

        /// <summary>
        /// Establecer una compañía como Admin del sistema
        /// POST: api/admin/system/set-admin-company
        /// </summary>
        [HttpPost("system/set-admin-company")]
        public async Task<IActionResult> SetAdminCompany([FromBody] SetAdminCompanyRequest request)
        {
            try
            {
                // Verificar permiso especial (System.SetAdminCompany)
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no identificado" });

                // Solo permitir si ya tiene acceso a una compañía Admin con el permiso correcto
                var hasPermission = await _companyService.HasViewAllCompaniesPermissionAsync(userId.Value);
                if (!hasPermission)
                    return Forbid();

                // Validar que la compañía existe
                var company = await _db.Companies.FindAsync(request.CompanyId);
                if (company == null)
                    return NotFound(new { message = "Compañía no encontrada" });

                // Quitar el flag de Admin de todas las compañías
                await _db.Companies
                    .Where(c => c.IS_ADMIN_COMPANY)
                    .ExecuteUpdateAsync(c => c.SetProperty(x => x.IS_ADMIN_COMPANY, false));

                // Establecer la nueva compañía Admin
                company.IS_ADMIN_COMPANY = true;
                company.RecordDate = DateTime.UtcNow;
                company.UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";

                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Compañía Admin cambiada a: {CompanyName} ({CompanyId}) por {User}",
                    company.COMPANY_NAME, company.ID, User.FindFirstValue("cms_username") ?? User.FindFirstValue(ClaimTypes.Name));

                return Ok(new
                {
                    success = true,
                    message = $"Compañía '{company.COMPANY_NAME}' establecida como Admin del sistema",
                    company = new CompanySystemInfo
                    {
                        Id = company.ID,
                        Name = company.COMPANY_NAME,
                        Schema = company.COMPANY_SCHEMA,
                        IsAdminCompany = true
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estableciendo compañía Admin");
                return StatusCode(500, new { message = "Error estableciendo compañía Admin" });
            }
        }

        /// <summary>
        /// Obtener permisos del sistema (módulo System)
        /// GET: api/admin/system/permissions
        /// </summary>
        [HttpGet("system/permissions")]
        public async Task<IActionResult> GetSystemPermissions()
        {
            try
            {
                var permissions = await _db.Permissions
                    .Where(p => p.MODULE == "System" && p.IS_ACTIVE)
                    .OrderBy(p => p.PERMISSION_KEY)
                    .Select(p => new SystemPermissionInfo
                    {
                        Id = p.ID_PERMISSION,
                        Key = p.PERMISSION_KEY,
                        Name = p.PERMISSION_NAME,
                        Description = p.DESCRIPTION,
                        IsActive = p.IS_ACTIVE
                    })
                    .ToListAsync();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo permisos del sistema");
                return StatusCode(500, new { message = "Error obteniendo permisos" });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return null;
            return userId;
        }

        #endregion

        #region DTOs

        public class HealthCheckResult
        {
            public string Status { get; set; } = "Healthy";
            public DateTime Timestamp { get; set; }
            public List<ServiceHealth> Services { get; set; } = new();
            public SystemMetrics Metrics { get; set; } = new();
        }

        public class ServiceHealth
        {
            public string Name { get; set; } = string.Empty;
            public string Status { get; set; } = "Healthy";
            public int ResponseTime { get; set; }
            public string? Message { get; set; }
            public string? Endpoint { get; set; }
        }

        public class SystemMetrics
        {
            public double CpuUsage { get; set; }
            public double MemoryUsage { get; set; }
            public string MemoryUsed { get; set; } = string.Empty;
            public string MemoryTotal { get; set; } = string.Empty;
            public double DiskUsage { get; set; }
            public string DiskUsed { get; set; } = string.Empty;
            public string DiskTotal { get; set; } = string.Empty;
            public int ActiveConnections { get; set; }
            public int RequestsPerMinute { get; set; }
            public double AverageResponseTime { get; set; }
            public int UptimeSeconds { get; set; }
        }

        public class AuditEntry
        {
            public int Id { get; set; }
            public DateTime Timestamp { get; set; }
            public string User { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public string Entity { get; set; } = string.Empty;
            public string? EntityId { get; set; }
            public string? Details { get; set; }
            public string? IpAddress { get; set; }
        }

        public class LogEntry
        {
            public int Id { get; set; }
            public DateTime Timestamp { get; set; }
            public string Level { get; set; } = "INFO";
            public string Source { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string? CorrelationId { get; set; }
            public string? StackTrace { get; set; }
        }

        public class ApiKeyInfo
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string KeyPrefix { get; set; } = string.Empty;
            public string Environment { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime? LastUsed { get; set; }
            public bool IsActive { get; set; }
            public string[] Permissions { get; set; } = Array.Empty<string>();
        }

        public class CreateApiKeyRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Environment { get; set; } = "Production";
            public List<string> Permissions { get; set; } = new();
        }

        public class ScheduledJob
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string CronExpression { get; set; } = string.Empty;
            public DateTime? NextRun { get; set; }
            public DateTime? LastRun { get; set; }
            public string? LastStatus { get; set; }
            public bool IsEnabled { get; set; }
        }

        public class BackupInfo
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public string Size { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? CreatedBy { get; set; }
        }

        public class CreateBackupRequest
        {
            public string Type { get; set; } = "full";
        }

        public class CompanySystemInfo
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Schema { get; set; } = string.Empty;
            public bool IsAdminCompany { get; set; }
            public bool IsActive { get; set; }
            public bool UsesAzureAD { get; set; }
            public int UserCount { get; set; }
        }

        public class SetAdminCompanyRequest
        {
            public int CompanyId { get; set; }
        }

        public class SystemPermissionInfo
        {
            public int Id { get; set; }
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }

        #endregion
    }
}
