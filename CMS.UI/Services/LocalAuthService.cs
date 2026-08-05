// ================================================================================
// ARCHIVO: CMS.UI/Services/LocalAuthService.cs
// PROPÓSITO: Servicio de autenticación local (email + contraseña)
// DESCRIPCIÓN: Maneja login local, bloqueo por intentos fallidos, 
//              recuperación de contraseña y validación de usuarios
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-13
// ACTUALIZADO: 2026-02-14 - Tiempo de expiración configurable desde BD
// ================================================================================

using CMS.Data;
using CMS.Data.Services;
using CMS.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CMS.UI.Services
{
    public class LocalAuthService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LocalAuthService> _logger;
        private readonly TokenApiService _tokenService;
        private readonly SystemConfigService _configService;

        public LocalAuthService(
            AppDbContext context,
            ILogger<LocalAuthService> logger,
            TokenApiService tokenService,
            SystemConfigService configService)
        {
            _context = context;
            _logger = logger;
            _tokenService = tokenService;
            _configService = configService;
        }

        #region DTOs

        public class LoginResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public User? User { get; set; }
            public string? Token { get; set; }
            public DateTime? TokenExpiry { get; set; }
            public bool IsLocked { get; set; }
            public DateTime? LockoutEnd { get; set; }
            public int RemainingAttempts { get; set; }
            /// <summary>
            /// Indica si el usuario no existe o no tiene acceso (no mostrar intentos restantes)
            /// </summary>
            public bool UserNotFound { get; set; }
        }

        public class CompanyValidationResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public Company? Company { get; set; }
            /// <summary>
            /// Timezone de la compañía en formato UTC offset, ej: "UTC-06:00"
            /// Se obtiene del país asociado (admin.country.time_zone)
            /// </summary>
            public string? CompanyTimezone { get; set; }
        }

        public class PasswordResetResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public string? Token { get; set; }
        }

        public class UserExistsResult
        {
            public bool Exists { get; set; }
            public string? Message { get; set; }
            public int? UserId { get; set; }
            public string? DisplayName { get; set; }
            public string? Email { get; set; }
        }

        #endregion

        #region Company Validation

        /// <summary>
        /// Valida si existe una compañía con el schema proporcionado
        /// </summary>
        public async Task<CompanyValidationResult> ValidateCompanyAsync(string companySchema)
        {
            try
            {
                var company = await _context.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => 
                        c.COMPANY_SCHEMA.ToLower() == companySchema.ToLower() && 
                        c.IS_ACTIVE);

                if (company == null)
                {
                    _logger.LogWarning("❌ Compañía no encontrada: {Schema}", companySchema);
                    return new CompanyValidationResult
                    {
                        Success = false,
                        Message = "Compañía no encontrada o inactiva"
                    };
                }

                _logger.LogInformation("✅ Compañía validada: {Name} ({Schema})", 
                    company.COMPANY_NAME, company.COMPANY_SCHEMA);
                _logger.LogInformation("   🔐 USES_AZURE_AD = {UsesAzureAD}", company.USES_AZURE_AD);

                // Obtener timezone del país asociado a la compañía
                string? timezone = null;
                if (company.IdCountry.HasValue)
                {
                    timezone = await _context.Countries
                        .AsNoTracking()
                        .Where(c => c.ID_COUNTRY == company.IdCountry.Value)
                        .Select(c => c.TIME_ZONE)
                        .FirstOrDefaultAsync();
                }

                return new CompanyValidationResult
                {
                    Success = true,
                    Company = company,
                    CompanyTimezone = timezone
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error validando compañía: {Schema}", companySchema);
                return new CompanyValidationResult
                {
                    Success = false,
                    Message = "Error interno al validar la compañía"
                };
            }
        }

        #endregion

        #region User Validation

        /// <summary>
        /// Valida si un usuario existe y tiene acceso a la compañía especificada
        /// </summary>
        public async Task<UserExistsResult> ValidateUserExistsInCompanyAsync(string email, int companyId)
        {
            try
            {
                // Buscar usuario por email
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.EMAIL.ToLower() == email.ToLower());

                if (user == null)
                {
                    return new UserExistsResult
                    {
                        Exists = false,
                        Message = "No existe una cuenta registrada con este correo electrónico."
                    };
                }

                if (!user.IS_ACTIVE)
                {
                    return new UserExistsResult
                    {
                        Exists = false,
                        Message = "La cuenta asociada a este correo está desactivada. Contacte al administrador."
                    };
                }

                // Verificar acceso a la compañía
                var hasAccess = await UserHasAccessToCompanyAsync(user.ID_USER, companyId);
                if (!hasAccess)
                {
                    return new UserExistsResult
                    {
                        Exists = false,
                        Message = "Este correo no tiene acceso a esta compañía."
                    };
                }

                return new UserExistsResult
                {
                    Exists = true,
                    UserId = user.ID_USER,
                    DisplayName = user.DISPLAY_NAME,
                    Email = user.EMAIL
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error validando existencia de usuario: {Email}", email);
                return new UserExistsResult
                {
                    Exists = false,
                    Message = "Error interno al validar el correo."
                };
            }
        }

        #endregion

        #region User-Company Access

        /// <summary>
        /// Verifica si un usuario tiene acceso a una compañía específica
        /// </summary>
        public async Task<bool> UserHasAccessToCompanyAsync(int userId, int companyId)
        {
            var access = await _context.UserCompanies
                .AsNoTracking()
                .FirstOrDefaultAsync(uc => 
                    uc.ID_USER == userId && 
                    uc.ID_COMPANY == companyId &&
                    uc.IS_ACTIVE);

            return access?.IsAccessValid() ?? false;
        }

        /// <summary>
        /// Obtiene las compañías a las que un usuario tiene acceso
        /// </summary>
        public async Task<List<Company>> GetUserCompaniesAsync(int userId)
        {
            return await _context.UserCompanies
                .AsNoTracking()
                .Include(uc => uc.Company)
                .Where(uc => uc.ID_USER == userId && uc.IS_ACTIVE)
                .Select(uc => uc.Company)
                .Where(c => c.IS_ACTIVE)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene las compañías a las que un usuario (por email) tiene acceso
        /// </summary>
        public async Task<List<Company>> GetUserCompaniesByEmailAsync(string email)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.EMAIL.ToLower() == email.ToLower() && u.IS_ACTIVE);

            if (user == null)
                return new List<Company>();

            return await GetUserCompaniesAsync(user.ID_USER);
        }

        #endregion

        #region Local Login

        /// <summary>
        /// Intenta autenticar un usuario con email y contraseña
        /// </summary>
        public async Task<LoginResult> LoginAsync(string email, string password, int companyId)
        {
            try
            {
                // 1. Buscar usuario por email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EMAIL.ToLower() == email.ToLower());

                if (user == null)
                {
                    _logger.LogWarning("❌ Usuario no encontrado: {Email}", email);
                    return new LoginResult
                    {
                        Success = false,
                        Message = "No existe una cuenta registrada con este correo electrónico.",
                        UserNotFound = true
                    };
                }

                // 2. Verificar si el usuario está activo
                if (!user.IS_ACTIVE)
                {
                    _logger.LogWarning("❌ Usuario inactivo: {Email}", email);
                    return new LoginResult
                    {
                        Success = false,
                        Message = "Esta cuenta está desactivada. Contacte al administrador.",
                        UserNotFound = true
                    };
                }

                // 3. Verificar si tiene acceso a la compañía
                var hasAccess = await UserHasAccessToCompanyAsync(user.ID_USER, companyId);
                if (!hasAccess)
                {
                    _logger.LogWarning("❌ Usuario {Email} no tiene acceso a compañía {CompanyId}", 
                        email, companyId);
                    return new LoginResult
                    {
                        Success = false,
                        Message = "Este correo no tiene acceso a esta compañía.",
                        UserNotFound = true
                    };
                }

                // 4. Verificar si el email está verificado
                if (!user.IS_EMAIL_VERIFIED)
                {
                    _logger.LogWarning("⚠️ Usuario {Email} no ha verificado su email", email);
                    return new LoginResult
                    {
                        Success = false,
                        Message = "Debe verificar su correo electrónico antes de iniciar sesión. Revise su bandeja de entrada."
                    };
                }

                // 5. Obtener configuración de la compañía para intentos máximos
                var company = await _context.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ID == companyId);

                var maxAttempts = company?.MAX_FAILED_LOGIN_ATTEMPTS ?? 3;
                var lockoutMinutes = company?.LOCKOUT_DURATION_MINUTES ?? 30;

                // 6. Verificar si está bloqueado
                if (user.LOCKOUT_END.HasValue && user.LOCKOUT_END.Value > DateTime.UtcNow)
                {
                    _logger.LogWarning("🔒 Usuario bloqueado: {Email} hasta {LockoutEnd}", 
                        email, user.LOCKOUT_END);
                    return new LoginResult
                    {
                        Success = false,
                        Message = "Cuenta bloqueada por múltiples intentos fallidos",
                        IsLocked = true,
                        LockoutEnd = user.LOCKOUT_END
                    };
                }

                // 7. Verificar si tiene contraseña configurada
                if (string.IsNullOrEmpty(user.PASSWORD_HASH))
                {
                    _logger.LogWarning("❌ Usuario sin contraseña configurada: {Email}", email);
                    return new LoginResult
                    {
                        Success = false,
                        Message = "Debe restablecer su contraseña antes de iniciar sesión."
                    };
                }

                // 8. Verificar contraseña
                if (!VerifyPassword(password, user.PASSWORD_HASH))
                {
                    // Incrementar intentos fallidos
                    user.FAILED_LOGIN_ATTEMPTS++;
                    var remaining = maxAttempts - user.FAILED_LOGIN_ATTEMPTS;

                    if (user.FAILED_LOGIN_ATTEMPTS >= maxAttempts)
                    {
                        // Bloquear usuario
                        user.LOCKOUT_END = DateTime.UtcNow.AddMinutes(lockoutMinutes);
                        await _context.SaveChangesAsync();

                        _logger.LogWarning("🔒 Usuario bloqueado por {Minutes} minutos: {Email}", 
                            lockoutMinutes, email);

                        return new LoginResult
                        {
                            Success = false,
                            Message = $"Cuenta bloqueada por {lockoutMinutes} minutos debido a múltiples intentos fallidos",
                            IsLocked = true,
                            LockoutEnd = user.LOCKOUT_END,
                            RemainingAttempts = 0
                        };
                    }

                    await _context.SaveChangesAsync();

                    _logger.LogWarning("❌ Contraseña incorrecta para: {Email}. Intentos restantes: {Remaining}", 
                        email, remaining);

                    return new LoginResult
                    {
                        Success = false,
                        Message = $"Contraseña incorrecta. Intentos restantes: {remaining}",
                        RemainingAttempts = remaining
                    };
                }

                // 9. Login exitoso - resetear intentos fallidos
                user.FAILED_LOGIN_ATTEMPTS = 0;
                user.LOCKOUT_END = null;
                user.LAST_LOGIN = DateTime.UtcNow;
                user.LOGIN_COUNT++;

                // Actualizar login en UserCompany
                var userCompany = await _context.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.ID_USER == user.ID_USER && uc.ID_COMPANY == companyId);

                if (userCompany != null)
                {
                    userCompany.LAST_LOGIN_AT_COMPANY = DateTime.UtcNow;
                    userCompany.LOGIN_COUNT_AT_COMPANY++;
                }

                await _context.SaveChangesAsync();

                // 10. Obtener token JWT del API
                var tokenResult = await _tokenService.GetApiTokenForLocalUserAsync(user, companyId);

                _logger.LogInformation("✅ Login exitoso: {Email} en compañía {CompanyId}", email, companyId);

                return new LoginResult
                {
                    Success = true,
                    User = user,
                    Token = tokenResult?.Token,
                    TokenExpiry = tokenResult?.ExpiresAt,
                    Message = "Login exitoso"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en login: {Email}", email);
                return new LoginResult
                {
                    Success = false,
                    Message = "Error interno. Intente nuevamente."
                };
            }
        }

        #endregion

        #region Password Reset

        /// <summary>
        /// Genera un token de restablecimiento de contraseña
        /// </summary>
        public async Task<PasswordResetResult> GeneratePasswordResetTokenAsync(string email, int companyId, string? requestIp)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EMAIL.ToLower() == email.ToLower() && u.IS_ACTIVE);

                if (user == null)
                {
                    // Por seguridad, no revelar si el usuario existe
                    _logger.LogWarning("⚠️ Solicitud de reset para usuario inexistente: {Email}", email);
                    return new PasswordResetResult
                    {
                        Success = true, // Simular éxito
                        Message = "Si el correo existe, recibirá un enlace de recuperación"
                    };
                }

                // Verificar acceso a la compañía
                var hasAccess = await UserHasAccessToCompanyAsync(user.ID_USER, companyId);
                if (!hasAccess)
                {
                    _logger.LogWarning("⚠️ Reset solicitado para compañía sin acceso: {Email}, {CompanyId}", 
                        email, companyId);
                    return new PasswordResetResult
                    {
                        Success = true,
                        Message = "Si el correo existe, recibirá un enlace de recuperación"
                    };
                }

                // Generar token
                var token = GenerateSecureToken();
                var tokenHash = HashToken(token);

                // Obtener tiempo de expiración desde configuración (default 0.5 horas = 30 minutos)
                var expiryHours = await _configService.GetConfigValueAsync("SECURITY", "PASSWORD_RESET_EXPIRY_HOURS");
                var hours = double.TryParse(expiryHours, out var h) ? h : 0.5; // Default 30 minutos
                var expiresAt = DateTime.UtcNow.AddHours(hours);

                _logger.LogInformation("🔑 Token de reset generado, expira en {Hours} horas", hours);

                // Guardar token en usuario
                user.PASSWORD_RESET_TOKEN = tokenHash;
                user.PASSWORD_RESET_TOKEN_EXPIRY = expiresAt;

                // Guardar en tabla de auditoría
                var resetRequest = new PasswordResetRequest
                {
                    ID_USER = user.ID_USER,
                    TOKEN = token, // Token en texto plano para el email
                    TOKEN_HASH = tokenHash,
                    EXPIRES_AT = expiresAt,
                    REQUESTED_AT = DateTime.UtcNow,
                    REQUESTED_IP = requestIp
                };

                _context.PasswordResetRequests.Add(resetRequest);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Token de reset generado para: {Email}", email);

                return new PasswordResetResult
                {
                    Success = true,
                    Token = token,
                    Message = "Token generado exitosamente"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generando token de reset: {Email}", email);
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "Error interno. Intente nuevamente."
                };
            }
        }

        /// <summary>
        /// Valida un token de restablecimiento de contraseña
        /// </summary>
        public async Task<(bool IsValid, User? User)> ValidatePasswordResetTokenAsync(string token, string email)
        {
            try
            {
                var tokenHash = HashToken(token);
                
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => 
                        u.EMAIL.ToLower() == email.ToLower() &&
                        u.PASSWORD_RESET_TOKEN == tokenHash &&
                        u.PASSWORD_RESET_TOKEN_EXPIRY > DateTime.UtcNow);

                if (user == null)
                {
                    _logger.LogWarning("❌ Token de reset inválido o expirado: {Email}", email);
                    return (false, null);
                }

                return (true, user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error validando token de reset");
                return (false, null);
            }
        }

        /// <summary>
        /// Restablece la contraseña de un usuario
        /// </summary>
        public async Task<bool> ResetPasswordAsync(string token, string email, string newPassword, string? requestIp)
        {
            try
            {
                var (isValid, user) = await ValidatePasswordResetTokenAsync(token, email);
                
                if (!isValid || user == null)
                    return false;

                // Actualizar contraseña
                user.PASSWORD_HASH = HashPassword(newPassword);
                user.PASSWORD_RESET_TOKEN = null;
                user.PASSWORD_RESET_TOKEN_EXPIRY = null;
                user.LAST_PASSWORD_CHANGE = DateTime.UtcNow;
                
                // Desbloquear si estaba bloqueado
                user.FAILED_LOGIN_ATTEMPTS = 0;
                user.LOCKOUT_END = null;

                // Marcar token como usado en la auditoría
                var tokenHash = HashToken(token);
                var resetRequest = await _context.PasswordResetRequests
                    .FirstOrDefaultAsync(r => r.TOKEN_HASH == tokenHash && !r.IS_USED);

                if (resetRequest != null)
                {
                    resetRequest.IS_USED = true;
                    resetRequest.USED_AT = DateTime.UtcNow;
                    resetRequest.USED_IP = requestIp;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Contraseña restablecida para: {Email}", email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error restableciendo contraseña: {Email}", email);
                return false;
            }
        }

        #endregion

        #region Email Verification

        /// <summary>
        /// Verifica el email del usuario usando el token enviado por correo
        /// </summary>
        public async Task<bool> VerifyEmailAsync(string token, string email)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EMAIL.ToLower() == email.ToLower());

                if (user == null)
                {
                    _logger.LogWarning("❌ Usuario no encontrado para verificación: {Email}", email);
                    return false;
                }

                // Verificar si ya está verificado
                if (user.IS_EMAIL_VERIFIED)
                {
                    _logger.LogInformation("ℹ️ Email ya verificado: {Email}", email);
                    return true;
                }

                // Verificar token
                var storedTokenHash = user.EMAIL_VERIFICATION_TOKEN;
                var tokenExpiry = user.EMAIL_VERIFICATION_TOKEN_EXPIRY;

                if (string.IsNullOrEmpty(storedTokenHash) || tokenExpiry == null)
                {
                    _logger.LogWarning("❌ No hay token de verificación para: {Email}", email);
                    return false;
                }

                // Verificar expiración
                if (DateTime.UtcNow > tokenExpiry)
                {
                    _logger.LogWarning("❌ Token de verificación expirado: {Email}", email);
                    return false;
                }

                // Comparar token (el token se almacena sin hashear en este caso)
                if (storedTokenHash != token)
                {
                    _logger.LogWarning("❌ Token de verificación inválido: {Email}", email);
                    return false;
                }

                // Marcar como verificado
                user.IS_EMAIL_VERIFIED = true;
                user.EMAIL_VERIFICATION_TOKEN = null;
                user.EMAIL_VERIFICATION_TOKEN_EXPIRY = null;
                user.UpdatedBy = user.USER_NAME;
                user.RecordDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Email verificado exitosamente: {Email}", email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error verificando email: {Email}", email);
                return false;
            }
        }

        #endregion

        #region Password Hashing

        /// <summary>
        /// Genera un hash seguro de la contraseña usando PBKDF2
        /// </summary>
        public static string HashPassword(string password)
        {
            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[16];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(32);

            var combined = new byte[salt.Length + hash.Length];
            Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
            Buffer.BlockCopy(hash, 0, combined, salt.Length, hash.Length);

            return Convert.ToBase64String(combined);
        }

        /// <summary>
        /// Verifica si la contraseña coincide con el hash almacenado
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                var combined = Convert.FromBase64String(storedHash);
                var salt = new byte[16];
                var storedHashBytes = new byte[32];

                Buffer.BlockCopy(combined, 0, salt, 0, 16);
                Buffer.BlockCopy(combined, 16, storedHashBytes, 0, 32);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
                var computedHash = pbkdf2.GetBytes(32);

                return CryptographicOperations.FixedTimeEquals(computedHash, storedHashBytes);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Genera un token seguro aleatorio
        /// </summary>
        private static string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        /// <summary>
        /// Hash de un token para almacenamiento seguro
        /// </summary>
        private static string HashToken(string token)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(token);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        #endregion

        #region Change Password (Usuario logueado)

        /// <summary>
        /// Cambia la contraseña de un usuario autenticado
        /// Verifica la contraseña actual antes de permitir el cambio
        /// </summary>
        public async Task<(bool Success, string Message)> ChangePasswordAsync(
            int userId, string currentPassword, string newPassword)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.ID_USER == userId && u.IS_ACTIVE);

                if (user == null)
                {
                    return (false, "Usuario no encontrado.");
                }

                // Verificar contraseña actual
                if (string.IsNullOrEmpty(user.PASSWORD_HASH) || 
                    !VerifyPassword(currentPassword, user.PASSWORD_HASH))
                {
                    _logger.LogWarning("❌ Contraseña actual incorrecta para usuario: {UserId}", userId);
                    return (false, "La contraseña actual es incorrecta.");
                }

                // Validar nueva contraseña
                if (newPassword.Length < 8)
                {
                    return (false, "La nueva contraseña debe tener al menos 8 caracteres.");
                }

                // No permitir contraseña igual a la actual
                if (VerifyPassword(newPassword, user.PASSWORD_HASH))
                {
                    return (false, "La nueva contraseña debe ser diferente a la actual.");
                }

                // Actualizar contraseña
                user.PASSWORD_HASH = HashPassword(newPassword);
                user.LAST_PASSWORD_CHANGE = DateTime.UtcNow;
                user.UpdatedBy = user.USER_NAME;
                user.RecordDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Contraseña cambiada exitosamente para usuario: {UserId}", userId);
                return (true, "¡Contraseña actualizada exitosamente!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error cambiando contraseña para usuario: {UserId}", userId);
                return (false, "Error interno al cambiar la contraseña.");
            }
        }

        #endregion
    }
}
