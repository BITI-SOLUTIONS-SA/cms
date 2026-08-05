using CMS.Application.DTOs;
using CMS.Data;
using CMS.Data.Services;
using CMS.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;
using System.Security.Cryptography;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _repo;
        private readonly ILogger<UserController> _logger;
        private readonly AppDbContext _db;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        // Expiración del token de verificación: 30 minutos
        private const int VERIFICATION_TOKEN_EXPIRY_MINUTES = 30;

        public UserController(
            IUserRepository repo, 
            ILogger<UserController> logger, 
            AppDbContext db,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _repo = repo;
            _logger = logger;
            _db = db;
            _emailService = emailService;
            _configuration = configuration;
        }

        // GET: api/user (solo admins) - Lista completa con roles
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<List<UserListDto>>> Get()
        {
            try
            {
                var users = await _db.Users
                    .Select(u => new UserListDto
                    {
                        Id = u.ID_USER,
                        Username = u.USER_NAME,
                        Email = u.EMAIL,
                        DisplayName = u.DISPLAY_NAME,
                        IsActive = u.IS_ACTIVE,
                        IsEmailVerified = u.IS_EMAIL_VERIFIED,
                        CreateDate = u.CreateDate,
                        LastLogin = u.LAST_LOGIN,
                        Roles = (from ucr in _db.UserCompanyRoles
                                 join r in _db.Roles on ucr.ID_ROLE equals r.ID_ROLE
                                 where ucr.ID_USER == u.ID_USER && ucr.IS_ACTIVE
                                 select r.ROLE_NAME).Distinct().ToList()
                    })
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo usuarios");
                return StatusCode(500, new { message = "Error obteniendo usuarios" });
            }
        }

        // Devuelve el nombre del usuario autenticado para campos de auditoría (created_by / updated_by)
        private string GetAuditUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirstValue(ClaimTypes.Name) ?? "SYSTEM";

        // GET: api/user/me (usuario actual)
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var userInfo = new
            {
                Id = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Name = User.FindFirstValue("cms_username") ?? User.FindFirstValue(ClaimTypes.Name),
                Email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("preferred_username"),
                Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
                Groups = User.FindAll("groups").Select(c => c.Value).ToList(),
                Claims = User.Claims.Select(c => new { c.Type, c.Value })
            };

            return Ok(userInfo);
        }

        // GET: api/user/for-company — usuarios activos de la compañía del token (sin rol Admin)
        [HttpGet("for-company")]
        public async Task<IActionResult> GetForCompany()
        {
            try
            {
                var companyIdClaim = User.FindFirstValue("companyId") ?? User.FindFirstValue("CompanyId");
                if (!int.TryParse(companyIdClaim, out var companyId))
                    return Unauthorized(new { message = "companyId no encontrado en el token" });

                var users = await (from uc in _db.UserCompanies
                                   join u in _db.Users on uc.ID_USER equals u.ID_USER
                                   where uc.ID_COMPANY == companyId && uc.IS_ACTIVE && u.IS_ACTIVE
                                   orderby u.DISPLAY_NAME
                                   select new
                                   {
                                       id          = u.ID_USER,
                                       username    = u.USER_NAME,
                                       displayName = u.DISPLAY_NAME,
                                       email       = u.EMAIL,
                                       isActive    = u.IS_ACTIVE,
                                   }).ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo usuarios de la compañía");
                return StatusCode(500, new { message = "Error obteniendo usuarios" });
            }
        }

        // GET: api/user/5 - Detalle completo con roles y permisos
        [Authorize(Roles = "Admin")]
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDetailDto>> Get(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Obtener roles de todas las compañías del usuario (sin duplicados)
                var roles = await (from ucr in _db.UserCompanyRoles
                                   join r in _db.Roles on ucr.ID_ROLE equals r.ID_ROLE
                                   where ucr.ID_USER == id && ucr.IS_ACTIVE
                                   select new RoleSimpleDto
                                   {
                                       Id = r.ID_ROLE,
                                       RoleName = r.ROLE_NAME
                                   }).Distinct().ToListAsync();

                // Obtener permisos directos de todas las compañías del usuario
                var permissions = await (from ucp in _db.UserCompanyPermissions
                                         join p in _db.Permissions on ucp.ID_PERMISSION equals p.ID_PERMISSION
                                         where ucp.ID_USER == id
                                         select new PermissionSimpleDto
                                         {
                                             Id = p.ID_PERMISSION,
                                             PermissionKey = p.PERMISSION_KEY,
                                             PermissionName = p.PERMISSION_NAME,
                                             IsAllowed = ucp.IS_ALLOWED
                                         }).Distinct().ToListAsync();

                // Obtener compañías del usuario
                var companies = await (from uc in _db.UserCompanies
                                       join c in _db.Companies on uc.ID_COMPANY equals c.ID
                                       where uc.ID_USER == id
                                       select new UserCompanySimpleDto
                                       {
                                           CompanyId = c.ID,
                                           CompanyName = c.COMPANY_NAME,
                                           IsDefault = uc.IS_DEFAULT,
                                           IsActive = uc.IS_ACTIVE
                                       }).ToListAsync();

                var dto = new UserDetailDto
                {
                    Id = user.ID_USER,
                    Username = user.USER_NAME,
                    Email = user.EMAIL,
                    DisplayName = user.DISPLAY_NAME,
                    FirstName = user.FIRST_NAME,
                    LastName = user.LAST_NAME,
                    PhoneNumber = user.PHONE_NUMBER,
                    DateOfBirth = user.DATE_OF_BIRTH,
                    IdCountry = user.ID_COUNTRY,
                    IdGender = user.ID_GENDER,
                    TimeZone = user.TIME_ZONE,
                    AzureOid = user.AZURE_OID,
                    AzureUpn = user.AZURE_UPN,
                    IsActive = user.IS_ACTIVE,
                    IsEmailVerified = user.IS_EMAIL_VERIFIED,
                    CreateDate = user.CreateDate,
                    CreatedBy = user.CreatedBy,
                    Roles = roles,
                    DirectPermissions = permissions,
                    Companies = companies
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo usuario {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo usuario" });
            }
        }

        // POST: api/user - Crear usuario
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<UserDetailDto>> Post([FromBody] UserCreateDto dto)
        {
            try
            {
                // Validar username único
                if (await _db.Users.AnyAsync(u => u.USER_NAME == dto.Username))
                    return BadRequest(new { message = "El username ya existe" });

                // Validar email único
                if (await _db.Users.AnyAsync(u => u.EMAIL == dto.Email))
                    return BadRequest(new { message = "El email ya existe" });

                // ⭐ Obtener valores por defecto de catálogos si no vienen
                var idCountry = dto.IdCountry > 0 ? dto.IdCountry : 
                    await _db.Countries.Where(c => c.IS_ACTIVE).Select(c => c.ID_COUNTRY).FirstOrDefaultAsync();

                var idGender = dto.IdGender > 0 ? dto.IdGender : 
                    await _db.Genders.Where(g => g.IS_ACTIVE).Select(g => g.ID_GENDER).FirstOrDefaultAsync();

                // Validar que los IDs existen
                if (idCountry == 0)
                    return BadRequest(new { 
                        message = "País inválido. No hay países activos en el sistema.",
                        field = "idCountry",
                        code = "INVALID_COUNTRY"
                    });

                if (idGender == 0)
                    return BadRequest(new { 
                        message = "Género inválido. No hay géneros activos en el sistema.",
                        field = "idGender",
                        code = "INVALID_GENDER"
                    });

                // ⭐ Generar contraseña temporal y token de verificación
                var temporaryPassword = _emailService.GenerateTemporaryPassword();
                var verificationToken = _emailService.GenerateSecureToken();

                // ⭐ Hashear la contraseña temporal usando PBKDF2 (compatible con LocalAuthService)
                var passwordHash = HashPasswordPBKDF2(temporaryPassword);

                // ⭐ Convertir DateOfBirth a UTC (Npgsql requiere DateTimeKind.Utc)
                DateTime dateOfBirth;
                if (dto.DateOfBirth.HasValue)
                {
                    var dob = dto.DateOfBirth.Value;
                    dateOfBirth = dob.Kind == DateTimeKind.Unspecified 
                        ? DateTime.SpecifyKind(dob, DateTimeKind.Utc) 
                        : dob.ToUniversalTime();
                }
                else
                {
                    dateOfBirth = DateTime.UtcNow.AddYears(-25);
                }

                _logger.LogInformation("📝 Creando usuario: {Username}, País: {Country}, Género: {Gender}",
                    dto.Username, idCountry, idGender);

                var user = new User
                {
                    USER_NAME = dto.Username,
                    EMAIL = dto.Email,
                    DISPLAY_NAME = dto.DisplayName ?? $"{dto.FirstName} {dto.LastName}",
                    PASSWORD_HASH = passwordHash,
                    IS_ACTIVE = dto.IsActive,
                    FIRST_NAME = dto.FirstName ?? "User",
                    LAST_NAME = dto.LastName ?? "System",
                    PHONE_NUMBER = dto.PhoneNumber ?? string.Empty,
                    TIME_ZONE = dto.TimeZone ?? "America/Costa_Rica",
                    DATE_OF_BIRTH = dateOfBirth,
                    ID_COUNTRY = idCountry,
                    ID_GENDER = idGender,
                    ID_LANGUAGE = 1832, // Español
                    IS_EMAIL_VERIFIED = false, // Siempre falso hasta que verifique
                    EMAIL_VERIFICATION_TOKEN = verificationToken,
                    EMAIL_VERIFICATION_TOKEN_EXPIRY = DateTime.UtcNow.AddMinutes(VERIFICATION_TOKEN_EXPIRY_MINUTES),
                    RecordDate = DateTime.UtcNow,
                    CreateDate = DateTime.UtcNow,
                    RowPointer = Guid.NewGuid(),
                    CreatedBy = GetAuditUser(),
                    UpdatedBy = GetAuditUser()
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Usuario creado: {UserId} - {Username}", user.ID_USER, user.USER_NAME);

                // ⭐ Asignar compañías al usuario
                var companyIdsToAssign = dto.CompanyIds ?? new List<int>();

                // Si no se especificaron compañías, usar la primera compañía activa
                if (!companyIdsToAssign.Any())
                {
                    var defaultCompanyId = await _db.Companies
                        .Where(c => c.IS_ACTIVE)
                        .Select(c => c.ID)
                        .FirstOrDefaultAsync();
                    if (defaultCompanyId > 0)
                    {
                        companyIdsToAssign.Add(defaultCompanyId);
                    }
                }

                foreach (var companyId in companyIdsToAssign)
                {
                    var companyExists = await _db.Companies.AnyAsync(c => c.ID == companyId && c.IS_ACTIVE);
                    if (companyExists)
                    {
                        _db.UserCompanies.Add(new UserCompany
                        {
                            ID_USER = user.ID_USER,
                            ID_COMPANY = companyId,
                            IS_DEFAULT = companyId == companyIdsToAssign.First(),
                            IS_ACTIVE = true,
                            ACCESS_GRANTED_DATE = DateTime.UtcNow,
                            RecordDate = DateTime.UtcNow,
                            CreateDate = DateTime.UtcNow,
                            RowPointer = Guid.NewGuid(),
                            CreatedBy = GetAuditUser(),
                            UpdatedBy = GetAuditUser()
                        });
                    }
                }
                await _db.SaveChangesAsync();
                _logger.LogInformation("Compañías asignadas: {Companies}", string.Join(", ", companyIdsToAssign));

                // ⭐ Asignar roles EN CADA COMPAÑÍA (nueva arquitectura)
                if (dto.RoleIds != null && dto.RoleIds.Any())
                {
                    foreach (var companyId in companyIdsToAssign)
                    {
                        foreach (var roleId in dto.RoleIds)
                        {
                            var roleExists = await _db.Roles.AnyAsync(r => r.ID_ROLE == roleId && r.IS_ACTIVE);
                            if (roleExists)
                            {
                                _db.UserCompanyRoles.Add(new UserCompanyRole
                                {
                                    ID_USER = user.ID_USER,
                                    ID_COMPANY = companyId,
                                    ID_ROLE = roleId,
                                    IS_ACTIVE = true,
                                    RecordDate = DateTime.UtcNow,
                                    CreateDate = DateTime.UtcNow,
                                    RowPointer = Guid.NewGuid(),
                                    CreatedBy = GetAuditUser(),
                                    UpdatedBy = GetAuditUser()
                                });
                            }
                        }
                    }
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Roles por compañía asignados: {Roles} en {Companies} compañías",
                        string.Join(", ", dto.RoleIds), companyIdsToAssign.Count);
                }

                // ⭐ Enviar email de verificación - Usar URL de la compañía desde BD
                var baseUrl = await GetUiBaseUrlFromCompanyAsync();
                _logger.LogInformation("📧 Enviando email de verificación a {Email} con baseUrl: {BaseUrl}", user.EMAIL, baseUrl);

                var emailResult = await _emailService.SendVerificationEmailAsync(
                    user.EMAIL,
                    user.DISPLAY_NAME,
                    verificationToken,
                    temporaryPassword,
                    baseUrl
                );

                if (emailResult.IsSuccess)
                {
                    _logger.LogInformation("📧 Email de verificación enviado a {Email}", user.EMAIL);
                }
                else
                {
                    _logger.LogWarning("⚠️ Error enviando email de verificación: {Error}", emailResult.ErrorMessage);
                }

                // Obtener el usuario creado con sus datos completos
                var createdUser = await _db.Users
                    .Where(u => u.ID_USER == user.ID_USER)
                    .Select(u => new UserDetailDto
                    {
                        Id = u.ID_USER,
                        Username = u.USER_NAME,
                        Email = u.EMAIL,
                        DisplayName = u.DISPLAY_NAME,
                        IsActive = u.IS_ACTIVE,
                        CreateDate = u.CreateDate,
                        CreatedBy = u.CreatedBy,
                        IsEmailVerified = u.IS_EMAIL_VERIFIED
                    })
                    .FirstOrDefaultAsync();

                if (createdUser != null)
                {
                    // Agregar roles (de todas las compañías, sin duplicados)
                    createdUser.Roles = await (from ucr in _db.UserCompanyRoles
                                               join r in _db.Roles on ucr.ID_ROLE equals r.ID_ROLE
                                               where ucr.ID_USER == user.ID_USER && ucr.IS_ACTIVE
                                               select new RoleSimpleDto
                                               {
                                                   Id = r.ID_ROLE,
                                                   RoleName = r.ROLE_NAME
                                               }).Distinct().ToListAsync();
                }

                return CreatedAtAction(nameof(Get), new { id = user.ID_USER }, createdUser);
            }
            catch (DbUpdateException dbEx)
            {
                // Extraer información detallada del error de base de datos
                var innerException = dbEx.InnerException;
                var errorMessage = "Error de base de datos al crear usuario.";
                var errorCode = "DB_ERROR";
                var errorDetails = dbEx.Message;

                if (innerException is Npgsql.PostgresException pgEx)
                {
                    errorCode = pgEx.SqlState;
                    errorDetails = $"Tabla: {pgEx.TableName}, Constraint: {pgEx.ConstraintName}, Detalle: {pgEx.Detail}";

                    // Traducir códigos de error comunes de PostgreSQL
                    errorMessage = pgEx.SqlState switch
                    {
                        "23503" => $"Error de FK: {pgEx.Detail}. Verifique que los valores de país, género y rol existan.",
                        "23505" => $"Valor duplicado: {pgEx.Detail}",
                        "23502" => $"Campo requerido vacío: {pgEx.ColumnName}",
                        _ => $"Error PostgreSQL [{pgEx.SqlState}]: {pgEx.MessageText}"
                    };
                }

                _logger.LogError(dbEx, "❌ Error DB creando usuario: {ErrorCode} - {Details}", errorCode, errorDetails);

                return StatusCode(500, new { 
                    message = errorMessage,
                    code = errorCode,
                    details = errorDetails,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error inesperado creando usuario");
                return StatusCode(500, new { 
                    message = $"Error inesperado: {ex.Message}",
                    code = "UNEXPECTED_ERROR",
                    details = ex.InnerException?.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        // PUT: api/user/5 - Actualizar usuario
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] UserUpdateDto dto)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Validar email único si cambió
                if (dto.Email != null && dto.Email != user.EMAIL)
                {
                    if (await _db.Users.AnyAsync(u => u.EMAIL == dto.Email && u.ID_USER != id))
                        return BadRequest(new { message = "El email ya existe" });
                    user.EMAIL = dto.Email;
                }

                // Actualizar todos los campos editables
                if (!string.IsNullOrWhiteSpace(dto.DisplayName))
                    user.DISPLAY_NAME = dto.DisplayName;

                if (!string.IsNullOrWhiteSpace(dto.FirstName))
                    user.FIRST_NAME = dto.FirstName;

                if (!string.IsNullOrWhiteSpace(dto.LastName))
                    user.LAST_NAME = dto.LastName;

                if (dto.PhoneNumber != null)
                    user.PHONE_NUMBER = dto.PhoneNumber;

                if (!string.IsNullOrWhiteSpace(dto.TimeZone))
                    user.TIME_ZONE = dto.TimeZone;

                if (dto.IdCountry > 0)
                    user.ID_COUNTRY = dto.IdCountry;

                if (dto.IdGender > 0)
                    user.ID_GENDER = dto.IdGender;

                if (dto.DateOfBirth.HasValue)
                {
                    var dob = dto.DateOfBirth.Value;
                    user.DATE_OF_BIRTH = dob.Kind == DateTimeKind.Unspecified 
                        ? DateTime.SpecifyKind(dob, DateTimeKind.Utc) 
                        : dob.ToUniversalTime();
                }

                user.IS_ACTIVE = dto.IsActive;
                user.IS_EMAIL_VERIFIED = dto.IsEmailVerified;
                user.UpdatedBy = GetAuditUser();

                await _db.SaveChangesAsync();

                // Actualizar roles si se especificaron
                if (dto.RoleIds != null)
                {
                    // Actualizar roles en TODAS las compañías del usuario
                    var userCompanyIds = await _db.UserCompanies
                        .Where(uc => uc.ID_USER == id && uc.IS_ACTIVE)
                        .Select(uc => uc.ID_COMPANY)
                        .ToListAsync();

                    // Eliminar roles actuales en todas las compañías
                    var currentRoles = await _db.UserCompanyRoles
                        .Where(ucr => ucr.ID_USER == id)
                        .ToListAsync();
                    _db.UserCompanyRoles.RemoveRange(currentRoles);

                    // Agregar nuevos roles en cada compañía
                    foreach (var companyId in userCompanyIds)
                    {
                        foreach (var roleId in dto.RoleIds)
                        {
                            var roleExists = await _db.Roles.AnyAsync(r => r.ID_ROLE == roleId && r.IS_ACTIVE);
                            if (roleExists)
                            {
                                _db.UserCompanyRoles.Add(new UserCompanyRole
                                {
                                    ID_USER = id,
                                    ID_COMPANY = companyId,
                                    ID_ROLE = roleId,
                                    IS_ACTIVE = true,
                                    RecordDate = DateTime.UtcNow,
                                    CreateDate = DateTime.UtcNow,
                                    RowPointer = Guid.NewGuid(),
                                    CreatedBy = GetAuditUser(),
                                    UpdatedBy = GetAuditUser()
                                });
                            }
                        }
                    }
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Roles actualizados para usuario {UserId}: {Roles} en {Count} compañías",
                        id, string.Join(", ", dto.RoleIds), userCompanyIds.Count);
                }

                _logger.LogInformation("Usuario {UserId} actualizado", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando usuario {Id}", id);
                return StatusCode(500, new { message = "Error actualizando usuario" });
            }
        }

        // DELETE: api/user/5 - Soft delete
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Soft delete
                user.IS_ACTIVE = false;
                user.UpdatedBy = GetAuditUser();
                await _db.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando usuario {Id}", id);
                return StatusCode(500, new { message = "Error eliminando usuario" });
            }
        }

        // =====================================================
        // GESTIÓN DE ROLES
        // =====================================================

        // GET: api/user/{id}/roles - Obtener roles del usuario (de todas las compañías)
        [Authorize(Roles = "Admin")]
        [HttpGet("{id}/roles")]
        public async Task<ActionResult<List<RoleSimpleDto>>> GetUserRoles(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                var roles = await (from ucr in _db.UserCompanyRoles
                   join r in _db.Roles on ucr.ID_ROLE equals r.ID_ROLE
                   where ucr.ID_USER == id && ucr.IS_ACTIVE
                   select new RoleSimpleDto
                   {
                       Id = r.ID_ROLE,
                       RoleName = r.ROLE_NAME
                   }).Distinct().ToListAsync();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo roles del usuario {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo roles" });
            }
        }

        // POST: api/user/{id}/roles - Asignar roles al usuario (en todas sus compañías)
        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/roles")]
        public async Task<IActionResult> AssignRoles(int id, [FromBody] List<int> roleIds)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Validar que todos los roles existen
                var existingRoleIds = await _db.Roles
                    .Where(r => roleIds.Contains(r.ID_ROLE))
                    .Select(r => r.ID_ROLE)
                    .ToListAsync();

                if (existingRoleIds.Count != roleIds.Count)
                    return BadRequest(new { message = "Uno o más roles no existen" });

                // Obtener compañías del usuario
                var userCompanyIds = await _db.UserCompanies
                    .Where(uc => uc.ID_USER == id && uc.IS_ACTIVE)
                    .Select(uc => uc.ID_COMPANY)
                    .ToListAsync();

                // Eliminar roles existentes
                var currentRoles = await _db.UserCompanyRoles.Where(ucr => ucr.ID_USER == id).ToListAsync();
                _db.UserCompanyRoles.RemoveRange(currentRoles);

                // Agregar nuevos roles en cada compañía
                var userName = GetAuditUser();
                foreach (var companyId in userCompanyIds)
                {
                    foreach (var roleId in roleIds)
                    {
                        _db.UserCompanyRoles.Add(new UserCompanyRole
                        {
                            ID_USER = id,
                            ID_COMPANY = companyId,
                            ID_ROLE = roleId,
                            IS_ACTIVE = true,
                            RecordDate = DateTime.UtcNow,
                            CreateDate = DateTime.UtcNow,
                            RowPointer = Guid.NewGuid(),
                            CreatedBy = userName,
                            UpdatedBy = userName
                        });
                    }
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("Roles asignados al usuario {UserId}: {Roles} en {Count} compañías",
                    id, string.Join(", ", roleIds), userCompanyIds.Count);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error asignando roles al usuario {Id}", id);
                return StatusCode(500, new { message = "Error asignando roles" });
            }
        }

        // DELETE: api/user/{id}/roles/{roleId} - Remover un rol específico (de todas las compañías)
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}/roles/{roleId}")]
        public async Task<IActionResult> RemoveRole(int id, int roleId)
        {
            try
            {
                var userRoles = await _db.UserCompanyRoles
                    .Where(ucr => ucr.ID_USER == id && ucr.ID_ROLE == roleId)
                    .ToListAsync();

                if (!userRoles.Any())
                    return NotFound(new { message = "Asignación de rol no encontrada" });

                _db.UserCompanyRoles.RemoveRange(userRoles);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Rol {RoleId} removido del usuario {UserId} en {Count} compañías", 
                    roleId, id, userRoles.Count);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removiendo rol {RoleId} del usuario {UserId}", roleId, id);
                return StatusCode(500, new { message = "Error removiendo rol" });
            }
        }

        // =====================================================
        // GESTIÓN DE PERMISOS DIRECTOS (por compañía)
        // =====================================================

        // GET: api/user/{id}/permissions - Obtener permisos directos (de todas las compañías)
        [Authorize(Roles = "Admin")]
        [HttpGet("{id}/permissions")]
        public async Task<ActionResult<List<PermissionSimpleDto>>> GetUserPermissions(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                var permissions = await (from ucp in _db.UserCompanyPermissions
                         join p in _db.Permissions on ucp.ID_PERMISSION equals p.ID_PERMISSION
                         where ucp.ID_USER == id
                         select new PermissionSimpleDto
                         {
                             Id = p.ID_PERMISSION,
                             PermissionKey = p.PERMISSION_KEY,
                             PermissionName = p.PERMISSION_NAME,
                             IsAllowed = ucp.IS_ALLOWED
                         }).Distinct().ToListAsync();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo permisos del usuario {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo permisos" });
            }
        }

        // POST: api/user/{id}/permissions - Asignar permisos directos (en todas las compañías)
        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/permissions")]
        public async Task<IActionResult> AssignPermissions(int id, [FromBody] List<PermissionAssignment> permissions)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Obtener compañías del usuario
                var userCompanyIds = await _db.UserCompanies
                    .Where(uc => uc.ID_USER == id && uc.IS_ACTIVE)
                    .Select(uc => uc.ID_COMPANY)
                    .ToListAsync();

                // Eliminar permisos existentes
                var currentPermissions = await _db.UserCompanyPermissions.Where(ucp => ucp.ID_USER == id).ToListAsync();
                _db.UserCompanyPermissions.RemoveRange(currentPermissions);

                // Agregar nuevos permisos en cada compañía
                var userName = GetAuditUser();
                foreach (var companyId in userCompanyIds)
                {
                    foreach (var perm in permissions)
                    {
                        _db.UserCompanyPermissions.Add(new UserCompanyPermission
                        {
                            ID_USER = id,
                            ID_COMPANY = companyId,
                            ID_PERMISSION = perm.PermissionId,
                            IS_ALLOWED = perm.IsAllowed,
                            RecordDate = DateTime.UtcNow,
                            CreateDate = DateTime.UtcNow,
                            RowPointer = Guid.NewGuid(),
                            CreatedBy = userName,
                            UpdatedBy = userName
                        });
                    }
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("Permisos asignados al usuario {UserId}: {Count} permisos en {CompanyCount} compañías", 
                    id, permissions.Count, userCompanyIds.Count);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error asignando permisos al usuario {Id}", id);
                return StatusCode(500, new { message = "Error asignando permisos" });
            }
        }

        // =====================================================
        // ACCIONES ADICIONALES
        // =====================================================

        // POST: api/user/{id}/activate - Activar usuario
        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/activate")]
        public async Task<IActionResult> ActivateUser(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                user.IS_ACTIVE = true;
                user.UpdatedBy = GetAuditUser();
                await _db.SaveChangesAsync();

                _logger.LogInformation("Usuario activado: {UserId}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activando usuario {Id}", id);
                return StatusCode(500, new { message = "Error activando usuario" });
            }
        }

        // POST: api/user/{id}/reset-password - Enviar reset de contraseña con email
        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Generar contraseña temporal y token
                var temporaryPassword = _emailService.GenerateTemporaryPassword();
                var resetToken = _emailService.GenerateSecureToken();

                // ⭐ Hashear la contraseña temporal usando PBKDF2 (compatible con LocalAuthService)
                var passwordHash = HashPasswordPBKDF2(temporaryPassword);

                // ⭐ Hashear el token para guardarlo en BD (el token plano se envía por email)
                using var sha256 = SHA256.Create();
                var tokenBytes = System.Text.Encoding.UTF8.GetBytes(resetToken);
                var tokenHash = sha256.ComputeHash(tokenBytes);
                var tokenHashBase64 = Convert.ToBase64String(tokenHash);

                // Actualizar usuario
                user.PASSWORD_HASH = passwordHash;
                user.PASSWORD_RESET_TOKEN = tokenHashBase64; // ⭐ Guardar HASH del token
                user.PASSWORD_RESET_TOKEN_EXPIRY = DateTime.UtcNow.AddMinutes(VERIFICATION_TOKEN_EXPIRY_MINUTES);
                user.LAST_PASSWORD_CHANGE = null; // Forzar cambio de contraseña
                user.LOCKOUT_END = null; // ⭐ Desbloquear cuenta
                user.FAILED_LOGIN_ATTEMPTS = 0; // ⭐ Reiniciar intentos fallidos
                user.UpdatedBy = GetAuditUser();
                await _db.SaveChangesAsync();

                // Enviar email - Usar URL de la compañía desde BD
                var baseUrl = await GetUiBaseUrlFromCompanyAsync();
                _logger.LogInformation("Enviando email de reset a {Email} con baseUrl: {BaseUrl}", user.EMAIL, baseUrl);

                var emailResult = await _emailService.SendPasswordResetEmailAsync(
                    user.EMAIL,
                    user.DISPLAY_NAME,
                    resetToken, // ⭐ Enviar token PLANO por email
                    temporaryPassword,
                    baseUrl
                );

                if (!emailResult.IsSuccess)
                {
                    _logger.LogWarning("⚠️ Error enviando email de reset: {Error}", emailResult.ErrorMessage);
                    return Ok(new { 
                        success = true, 
                        message = "Token generado pero hubo un error enviando el correo",
                        warning = emailResult.ErrorMessage
                    });
                }

                _logger.LogInformation("✅ Password reset enviado para usuario {UserId} ({Email})", id, user.EMAIL);

                return Ok(new { 
                    success = true, 
                    message = $"Se ha enviado un correo a {user.EMAIL} con las instrucciones para cambiar la contraseña" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando reset de contraseña para usuario {Id}", id);
                return StatusCode(500, new { message = "Error generando reset de contraseña" });
            }
        }

        // POST: api/user/{id}/set-password - Establecer contraseña directamente (Admin)
        /// <summary>
        /// Permite a un administrador establecer la contraseña de un usuario directamente
        /// sin enviar correo ni requerir cambio posterior.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/set-password")]
        public async Task<IActionResult> SetPassword(int id, [FromBody] SetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.NewPassword))
                    return BadRequest(new { success = false, message = "La contraseña es requerida" });

                if (request.NewPassword.Length < 8)
                    return BadRequest(new { success = false, message = "La contraseña debe tener al menos 8 caracteres" });

                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { success = false, message = "Usuario no encontrado" });

                // ⭐ Hashear la contraseña usando PBKDF2 (igual que LocalAuthService)
                var passwordHash = HashPasswordPBKDF2(request.NewPassword);

                _logger.LogInformation("🔐 Cambiando contraseña usuario {UserId}: NewHashLength={HashLength}", 
                    id, passwordHash.Length);

                // Actualizar usuario - NO marca como temporal
                // También desbloquea la cuenta y reinicia intentos fallidos
                user.PASSWORD_HASH = passwordHash;
                user.PASSWORD_RESET_TOKEN = null; // Limpiar token de reset
                user.PASSWORD_RESET_TOKEN_EXPIRY = null;
                user.LAST_PASSWORD_CHANGE = DateTime.UtcNow; // Marcar como cambiada para evitar prompt de cambio
                user.LOCKOUT_END = null; // ⭐ Desbloquear cuenta
                user.FAILED_LOGIN_ATTEMPTS = 0; // ⭐ Reiniciar intentos fallidos
                user.UpdatedBy = GetAuditUser();

                // Forzar que EF Core detecte
                _db.Entry(user).Property(u => u.PASSWORD_HASH).IsModified = true;

                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Contraseña establecida por administrador para usuario {UserId} (cuenta desbloqueada)", id);

                return Ok(new { 
                    success = true, 
                    message = "Contraseña establecida correctamente" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estableciendo contraseña para usuario {Id}", id);
                return StatusCode(500, new { success = false, message = "Error estableciendo contraseña" });
            }
        }

        /// <summary>
        /// Genera un hash seguro de la contraseña usando PBKDF2 (compatible con LocalAuthService)
        /// </summary>
        private static string HashPasswordPBKDF2(string password)
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
        /// Obtiene la URL base del UI desde la configuración de la compañía en la base de datos.
        /// Usa IS_PRODUCTION para determinar si usar ui_development_base_url o ui_production_base_url.
        /// Si no está configurado, usa el appsettings.json como fallback.
        /// </summary>
        private async Task<string> GetUiBaseUrlFromCompanyAsync()
        {
            try
            {
                // Obtener companyId del JWT
                var companyIdClaim = User.FindFirst("CompanyId")?.Value;
                if (string.IsNullOrEmpty(companyIdClaim) || !int.TryParse(companyIdClaim, out var companyId))
                {
                    // Fallback: usar la compañía por defecto (la primera activa que es admin_company)
                    var defaultCompany = await _db.Companies
                        .Where(c => c.IS_ACTIVE && c.IS_ADMIN_COMPANY)
                        .FirstOrDefaultAsync();

                    if (defaultCompany == null)
                    {
                        defaultCompany = await _db.Companies.FirstOrDefaultAsync(c => c.IS_ACTIVE);
                    }

                    if (defaultCompany != null)
                    {
                        companyId = defaultCompany.ID;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ No se encontró companyId en JWT ni compañía por defecto");
                        return GetFallbackUiUrl();
                    }
                }

                var company = await _db.Companies.FindAsync(companyId);
                if (company == null)
                {
                    _logger.LogWarning("⚠️ Compañía {CompanyId} no encontrada", companyId);
                    return GetFallbackUiUrl();
                }

                // Determinar qué URL usar basándose en IS_PRODUCTION
                string? baseUrl;
                if (company.IS_PRODUCTION)
                {
                    baseUrl = company.UI_PRODUCTION_BASE_URL;
                    _logger.LogInformation("🌐 Usando UI_PRODUCTION_BASE_URL: {Url}", baseUrl);
                }
                else
                {
                    baseUrl = company.UI_DEVELOPMENT_BASE_URL;
                    _logger.LogInformation("🌐 Usando UI_DEVELOPMENT_BASE_URL: {Url}", baseUrl);
                }

                if (string.IsNullOrEmpty(baseUrl))
                {
                    _logger.LogWarning("⚠️ URL del UI no configurada en compañía {CompanyId}, usando fallback", companyId);
                    return GetFallbackUiUrl();
                }

                return baseUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo URL del UI desde la compañía");
                return GetFallbackUiUrl();
            }
        }

        /// <summary>
        /// Obtiene la URL fallback del UI desde appsettings o valores por defecto
        /// </summary>
        private string GetFallbackUiUrl()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var baseUrl = _configuration[$"UiSettings:{environment}:BaseUrl"];

            if (!string.IsNullOrEmpty(baseUrl))
            {
                return baseUrl;
            }

            return environment == "Development" 
                ? "https://localhost:5001" 
                : "https://cms.biti-solutions.com";
        }

        // POST: api/user/{id}/send-verification - Enviar/Reenviar email de verificación
        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/send-verification")]
        public async Task<IActionResult> SendVerificationEmail(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                if (user.IS_EMAIL_VERIFIED)
                    return BadRequest(new { message = "El correo ya está verificado" });

                // Generar contraseña temporal y token
                var temporaryPassword = _emailService.GenerateTemporaryPassword();
                var verificationToken = _emailService.GenerateSecureToken();

                // ⭐ Hashear la contraseña temporal usando PBKDF2 (compatible con LocalAuthService)
                var passwordHash = HashPasswordPBKDF2(temporaryPassword);

                // Actualizar usuario
                user.PASSWORD_HASH = passwordHash;
                user.EMAIL_VERIFICATION_TOKEN = verificationToken;
                user.EMAIL_VERIFICATION_TOKEN_EXPIRY = DateTime.UtcNow.AddMinutes(VERIFICATION_TOKEN_EXPIRY_MINUTES);
                user.UpdatedBy = GetAuditUser();
                await _db.SaveChangesAsync();

                // Enviar email - Usar URL de la compañía desde BD
                var baseUrl = await GetUiBaseUrlFromCompanyAsync();
                _logger.LogInformation("Enviando email de verificación a {Email} con baseUrl: {BaseUrl}", user.EMAIL, baseUrl);

                var emailResult = await _emailService.SendVerificationEmailAsync(
                    user.EMAIL,
                    user.DISPLAY_NAME,
                    verificationToken,
                    temporaryPassword,
                    baseUrl
                );

                if (!emailResult.IsSuccess)
                {
                    _logger.LogWarning("⚠️ Error enviando email de verificación: {Error}", emailResult.ErrorMessage);
                    return Ok(new { 
                        success = true, 
                        message = "Token generado pero hubo un error enviando el correo",
                        warning = emailResult.ErrorMessage
                    });
                }

                _logger.LogInformation("✅ Email de verificación enviado para usuario {UserId} ({Email})", id, user.EMAIL);

                return Ok(new { 
                    success = true, 
                    message = $"Se ha enviado un correo de verificación a {user.EMAIL}" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email de verificación para usuario {Id}", id);
                return StatusCode(500, new { message = "Error enviando email de verificación" });
            }
        }

        // DELETE: api/user/{id}/permanent - Eliminación permanente (solo si no hay referencias)
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}/permanent")]
        public async Task<IActionResult> DeletePermanent(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Verificar referencias en otras tablas
                var references = new List<string>();

                // Verificar user_company
                if (await _db.UserCompanies.AnyAsync(uc => uc.ID_USER == id))
                    references.Add("user_company (asignaciones de compañía)");

                // Verificar user_company_role
                if (await _db.UserCompanyRoles.AnyAsync(ucr => ucr.ID_USER == id))
                    references.Add("user_company_role (roles por compañía)");

                // Verificar user_company_permission
                if (await _db.UserCompanyPermissions.AnyAsync(ucp => ucp.ID_USER == id))
                    references.Add("user_company_permission (permisos por compañía)");

                // Verificar otras tablas que podrían referenciar usuarios
                // (Se pueden agregar más verificaciones según las tablas del sistema)

                // Por ahora, si tiene roles/permisos/compañías, ofrecemos limpiarlos
                if (references.Any())
                {
                    return BadRequest(new { 
                        message = "El usuario tiene referencias que deben eliminarse primero",
                        canDelete = true, // Se puede eliminar si se limpian las referencias
                        references = references,
                        suggestion = "Use el parámetro 'force=true' para eliminar las referencias automáticamente"
                    });
                }

                // Eliminar permanentemente
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();

                _logger.LogInformation("🗑️ Usuario {UserId} ({Username}) eliminado permanentemente por {DeletedBy}", 
                    id, user.USER_NAME, GetAuditUser());

                return Ok(new { 
                    success = true, 
                    message = $"Usuario '{user.USER_NAME}' eliminado permanentemente" 
                });
            }
            catch (DbUpdateException dbEx)
            {
                // Error de FK - hay referencias que no verificamos
                _logger.LogError(dbEx, "Error eliminando usuario {Id} - tiene referencias pendientes", id);
                return BadRequest(new { 
                    message = "No se puede eliminar: el usuario tiene referencias en otras tablas del sistema",
                    details = dbEx.InnerException?.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando permanentemente usuario {Id}", id);
                return StatusCode(500, new { message = "Error eliminando usuario" });
            }
        }

        // DELETE: api/user/{id}/force - Eliminación forzada (elimina referencias)
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}/force")]
        public async Task<IActionResult> DeleteForce(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Eliminar referencias primero (nuevas tablas por compañía)
                var deletedCompanyRoles = await _db.UserCompanyRoles.Where(ucr => ucr.ID_USER == id).ExecuteDeleteAsync();
                var deletedCompanyPermissions = await _db.UserCompanyPermissions.Where(ucp => ucp.ID_USER == id).ExecuteDeleteAsync();
                var deletedCompanies = await _db.UserCompanies.Where(uc => uc.ID_USER == id).ExecuteDeleteAsync();

                _logger.LogInformation("🧹 Limpieza de referencias para usuario {UserId}: {Roles} roles, {Permissions} permisos, {Companies} compañías", 
                    id, deletedCompanyRoles, deletedCompanyPermissions, deletedCompanies);

                // Eliminar usuario
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();

                _logger.LogInformation("🗑️ Usuario {UserId} ({Username}) eliminado permanentemente (forzado) por {DeletedBy}", 
                    id, user.USER_NAME, GetAuditUser());

                return Ok(new { 
                    success = true, 
                    message = $"Usuario '{user.USER_NAME}' eliminado permanentemente",
                    deletedReferences = new {
                        companyRoles = deletedCompanyRoles,
                        companyPermissions = deletedCompanyPermissions,
                        companies = deletedCompanies
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando (forzado) usuario {Id}", id);
                return StatusCode(500, new { message = "Error eliminando usuario: " + ex.Message });
            }
        }

        // GET: api/user/check-username - Verificar disponibilidad de username
        [Authorize(Roles = "Admin")]
        [HttpGet("check-username")]
        public async Task<IActionResult> CheckUsername([FromQuery] string username, [FromQuery] int? excludeId = null)
        {
            try
            {
                var query = _db.Users.Where(u => u.USER_NAME == username);
                if (excludeId.HasValue)
                    query = query.Where(u => u.ID_USER != excludeId.Value);

                var exists = await query.AnyAsync();

                return Ok(new { available = !exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando username");
                return StatusCode(500, new { available = false });
            }
        }

        // GET: api/user/check-email - Verificar disponibilidad de email
        [Authorize(Roles = "Admin")]
        [HttpGet("check-email")]
        public async Task<IActionResult> CheckEmail([FromQuery] string email, [FromQuery] int? excludeId = null)
        {
            try
            {
                var query = _db.Users.Where(u => u.EMAIL == email);
                if (excludeId.HasValue)
                    query = query.Where(u => u.ID_USER != excludeId.Value);

                var exists = await query.AnyAsync();

                return Ok(new { available = !exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando email");
                return StatusCode(500, new { available = false });
            }
        }

        #region User Companies Management

        // GET: api/user/{id}/companies - Obtener compañías asignadas a un usuario
        [Authorize(Roles = "Admin")]
        [HttpGet("{id}/companies")]
        public async Task<IActionResult> GetUserCompanies(int id)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                var companies = await (from uc in _db.UserCompanies
                                       join c in _db.Companies on uc.ID_COMPANY equals c.ID
                                       where uc.ID_USER == id
                                       select new UserCompanyDto
                                       {
                                           CompanyId = c.ID,
                                           CompanyName = c.COMPANY_NAME,
                                           CompanySchema = c.COMPANY_SCHEMA,
                                           IsDefault = uc.IS_DEFAULT,
                                           IsActive = uc.IS_ACTIVE,
                                           AccessGrantedDate = uc.ACCESS_GRANTED_DATE,
                                           AccessExpiryDate = uc.ACCESS_EXPIRY_DATE
                                       }).ToListAsync();

                return Ok(companies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo compañías del usuario {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo compañías" });
            }
        }

        // POST: api/user/{id}/companies - Asignar compañías a un usuario
        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/companies")]
        public async Task<IActionResult> AssignCompanies(int id, [FromBody] AssignCompaniesDto dto)
        {
            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Validar que las compañías existen
                var validCompanyIds = await _db.Companies
                    .Where(c => dto.CompanyIds.Contains(c.ID) && c.IS_ACTIVE)
                    .Select(c => c.ID)
                    .ToListAsync();

                if (validCompanyIds.Count != dto.CompanyIds.Count)
                {
                    _logger.LogWarning("⚠️ Algunas compañías no existen o están inactivas");
                }

                // Eliminar asignaciones actuales
                var currentAssignments = await _db.UserCompanies
                    .Where(uc => uc.ID_USER == id)
                    .ToListAsync();
                _db.UserCompanies.RemoveRange(currentAssignments);

                // Crear nuevas asignaciones
                var isFirst = true;
                foreach (var companyId in validCompanyIds)
                {
                    var userCompany = new CMS.Entities.UserCompany
                    {
                        ID_USER = id,
                        ID_COMPANY = companyId,
                        IS_DEFAULT = isFirst, // Primera compañía es la default
                        IS_ACTIVE = true,
                        ACCESS_GRANTED_DATE = DateTime.UtcNow,
                        CreateDate = DateTime.UtcNow,
                        RowPointer = Guid.NewGuid()
                    };
                    _db.UserCompanies.Add(userCompany);
                    isFirst = false;
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Compañías asignadas a usuario {UserId}: {Companies}", 
                    id, string.Join(", ", validCompanyIds));

                return Ok(new { 
                    success = true, 
                    message = $"Se asignaron {validCompanyIds.Count} compañía(s) al usuario",
                    assignedCompanies = validCompanyIds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error asignando compañías al usuario {Id}", id);
                return StatusCode(500, new { message = "Error asignando compañías: " + ex.Message });
            }
        }

        // DELETE: api/user/{id}/companies/{companyId} - Remover una compañía de un usuario
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}/companies/{companyId}")]
        public async Task<IActionResult> RemoveCompany(int id, int companyId)
        {
            try
            {
                var userCompany = await _db.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.ID_USER == id && uc.ID_COMPANY == companyId);

                if (userCompany == null)
                    return NotFound(new { message = "Asignación no encontrada" });

                _db.UserCompanies.Remove(userCompany);
                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Compañía {CompanyId} removida del usuario {UserId}", companyId, id);

                return Ok(new { success = true, message = "Compañía removida del usuario" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removiendo compañía {CompanyId} del usuario {Id}", companyId, id);
                return StatusCode(500, new { message = "Error removiendo compañía" });
            }
        }

        #endregion

        #region Autorización por Compañía

        // GET: api/user/{userId}/companies/{companyId}/auth - Obtener resumen de autorización
        [Authorize(Roles = "Admin")]
        [HttpGet("{userId}/companies/{companyId}/auth")]
        public async Task<IActionResult> GetCompanyAuthSummary(int userId, int companyId)
        {
            try
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                var company = await _db.Companies.FindAsync(companyId);
                if (company == null)
                    return NotFound(new { message = "Compañía no encontrada" });

                // Verificar que el usuario tiene acceso a esta compañía
                var userCompany = await _db.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.ID_USER == userId && uc.ID_COMPANY == companyId);

                if (userCompany == null)
                    return BadRequest(new { message = "El usuario no tiene acceso a esta compañía" });

                // Obtener todos los roles con indicador de si están asignados al usuario en esta compañía
                var allRoles = await _db.Roles
                    .Where(r => r.IS_ACTIVE)
                    .Select(r => new UserCompanyRoleDto
                    {
                        RoleId = r.ID_ROLE,
                        RoleName = r.ROLE_NAME,
                        IsActive = r.IS_ACTIVE,
                        IsAssigned = _db.UserCompanyRoles
                            .Any(ucr => ucr.ID_ROLE == r.ID_ROLE && ucr.ID_USER == userId && ucr.ID_COMPANY == companyId && ucr.IS_ACTIVE)
                    })
                    .OrderBy(r => r.RoleName)
                    .ToListAsync();

                // Obtener permisos efectivos del usuario en esta compañía
                // 1. Permisos de roles asignados
                var roleIds = await _db.UserCompanyRoles
                    .Where(ucr => ucr.ID_USER == userId && ucr.ID_COMPANY == companyId && ucr.IS_ACTIVE)
                    .Select(ucr => ucr.ID_ROLE)
                    .ToListAsync();

                // Primero obtener datos sin transformación compleja
                var rolePermissionsRaw = await (from rp in _db.RolePermissions
                                             join p in _db.Permissions on rp.PermissionId equals p.ID_PERMISSION
                                             where roleIds.Contains(rp.RoleId) && rp.IsAllowed && p.IS_ACTIVE
                                             select new 
                                             {
                                                 p.ID_PERMISSION,
                                                 p.PERMISSION_KEY,
                                                 p.PERMISSION_NAME
                                             }).ToListAsync();

                var rolePermissions = rolePermissionsRaw.Select(p => new UserCompanyPermissionDto
                {
                    PermissionId = p.ID_PERMISSION,
                    PermissionKey = p.PERMISSION_KEY,
                    PermissionName = p.PERMISSION_NAME ?? p.PERMISSION_KEY,
                    Module = p.PERMISSION_KEY.Contains('.') ? p.PERMISSION_KEY.Split('.')[0] : "General",
                    Source = "Role",
                    IsAllowed = true,
                    IsDenied = false
                }).ToList();

                // 2. Permisos directos del usuario en esta compañía
                var directPermissionsRaw = await (from ucp in _db.UserCompanyPermissions
                                               join p in _db.Permissions on ucp.ID_PERMISSION equals p.ID_PERMISSION
                                               where ucp.ID_USER == userId && ucp.ID_COMPANY == companyId && p.IS_ACTIVE
                                               select new 
                                               {
                                                   p.ID_PERMISSION,
                                                   p.PERMISSION_KEY,
                                                   p.PERMISSION_NAME,
                                                   ucp.IS_ALLOWED
                                               }).ToListAsync();

                var directPermissions = directPermissionsRaw.Select(p => new UserCompanyPermissionDto
                {
                    PermissionId = p.ID_PERMISSION,
                    PermissionKey = p.PERMISSION_KEY,
                    PermissionName = p.PERMISSION_NAME ?? p.PERMISSION_KEY,
                    Module = p.PERMISSION_KEY.Contains('.') ? p.PERMISSION_KEY.Split('.')[0] : "General",
                    Source = p.IS_ALLOWED ? "DirectGrant" : "DirectDeny",
                    IsAllowed = p.IS_ALLOWED,
                    IsDenied = !p.IS_ALLOWED
                }).ToList();

                // Combinar permisos (directo gana sobre rol)
                var allPermissions = new Dictionary<int, UserCompanyPermissionDto>();

                foreach (var perm in rolePermissions)
                {
                    allPermissions[perm.PermissionId] = perm;
                }

                foreach (var perm in directPermissions)
                {
                    allPermissions[perm.PermissionId] = perm; // Directo sobreescribe
                }

                // Filtrar denegados
                var effectivePermissions = allPermissions.Values
                    .Where(p => p.IsAllowed && !p.IsDenied)
                    .OrderBy(p => p.Module)
                    .ThenBy(p => p.PermissionKey)
                    .ToList();

                var summary = new UserCompanyAuthSummaryDto
                {
                    UserId = userId,
                    UserName = user.DISPLAY_NAME ?? user.USER_NAME,
                    CompanyId = companyId,
                    CompanyName = company.COMPANY_NAME,
                    Roles = allRoles,
                    Permissions = effectivePermissions,
                    TotalEffectivePermissions = effectivePermissions.Count
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo autorización del usuario {UserId} en compañía {CompanyId}", userId, companyId);
                return StatusCode(500, new { message = "Error obteniendo autorización" });
            }
        }

        // POST: api/user/{userId}/companies/{companyId}/roles - Asignar roles en una compañía
        [Authorize(Roles = "Admin")]
        [HttpPost("{userId}/companies/{companyId}/roles")]
        public async Task<IActionResult> AssignRolesInCompany(int userId, int companyId, [FromBody] List<int> roleIds)
        {
            try
            {
                // Validar usuario y compañía
                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                var userCompany = await _db.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.ID_USER == userId && uc.ID_COMPANY == companyId);

                if (userCompany == null)
                    return BadRequest(new { message = "El usuario no tiene acceso a esta compañía" });

                // Eliminar roles actuales en esta compañía
                var currentRoles = await _db.UserCompanyRoles
                    .Where(ucr => ucr.ID_USER == userId && ucr.ID_COMPANY == companyId)
                    .ToListAsync();
                _db.UserCompanyRoles.RemoveRange(currentRoles);

                // Agregar nuevos roles
                var userName = GetAuditUser();
                foreach (var roleId in roleIds)
                {
                    _db.UserCompanyRoles.Add(new UserCompanyRole
                    {
                        ID_USER = userId,
                        ID_COMPANY = companyId,
                        ID_ROLE = roleId,
                        IS_ACTIVE = true,
                        CreateDate = DateTime.UtcNow,
                        RowPointer = Guid.NewGuid(),
                        CreatedBy = userName,
                        UpdatedBy = userName
                    });
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("Roles asignados a usuario {UserId} en compañía {CompanyId}: {Roles}",
                    userId, companyId, string.Join(", ", roleIds));

                return Ok(new { success = true, message = $"Se asignaron {roleIds.Count} rol(es)" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error asignando roles al usuario {UserId} en compañía {CompanyId}", userId, companyId);
                return StatusCode(500, new { message = "Error asignando roles" });
            }
        }

        // POST: api/user/{userId}/companies/{companyId}/permissions - Asignar permisos directos en una compañía
        [Authorize(Roles = "Admin")]
        [HttpPost("{userId}/companies/{companyId}/permissions")]
        public async Task<IActionResult> AssignPermissionsInCompany(int userId, int companyId, [FromBody] List<PermissionAssignment> permissions)
        {
            try
            {
                // Validar usuario y compañía
                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                var userCompany = await _db.UserCompanies
                    .FirstOrDefaultAsync(uc => uc.ID_USER == userId && uc.ID_COMPANY == companyId);

                if (userCompany == null)
                    return BadRequest(new { message = "El usuario no tiene acceso a esta compañía" });

                // Eliminar permisos directos actuales en esta compañía
                var currentPermissions = await _db.UserCompanyPermissions
                    .Where(ucp => ucp.ID_USER == userId && ucp.ID_COMPANY == companyId)
                    .ToListAsync();
                _db.UserCompanyPermissions.RemoveRange(currentPermissions);

                // Agregar nuevos permisos
                var userName = GetAuditUser();
                foreach (var perm in permissions)
                {
                    _db.UserCompanyPermissions.Add(new UserCompanyPermission
                    {
                        ID_USER = userId,
                        ID_COMPANY = companyId,
                        ID_PERMISSION = perm.PermissionId,
                        IS_ALLOWED = perm.IsAllowed,
                        RecordDate = DateTime.UtcNow,
                        CreateDate = DateTime.UtcNow,
                        RowPointer = Guid.NewGuid(),
                        CreatedBy = userName,
                        UpdatedBy = userName
                    });
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Permisos asignados a usuario {UserId} en compañía {CompanyId}: {Count}", 
                    userId, companyId, permissions.Count);

                return Ok(new { success = true, message = $"Se asignaron {permissions.Count} permiso(s)" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error asignando permisos al usuario {UserId} en compañía {CompanyId}", userId, companyId);
                return StatusCode(500, new { message = "Error asignando permisos" });
            }
        }

        #region DTOs for Company Auth

        public class UserCompanyRoleDto
        {
            public int RoleId { get; set; }
            public string RoleName { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public bool IsAssigned { get; set; }
        }

        public class UserCompanyPermissionDto
        {
            public int PermissionId { get; set; }
            public string PermissionKey { get; set; } = string.Empty;
            public string PermissionName { get; set; } = string.Empty;
            public string Module { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public bool IsAllowed { get; set; }
            public bool IsDenied { get; set; }
        }

        public class UserCompanyAuthSummaryDto
        {
            public int UserId { get; set; }
            public string UserName { get; set; } = string.Empty;
            public int CompanyId { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public List<UserCompanyRoleDto> Roles { get; set; } = new();
            public List<UserCompanyPermissionDto> Permissions { get; set; } = new();
            public int TotalEffectivePermissions { get; set; }
        }

        #endregion

        #endregion

        #region DTOs for User Companies

        public class UserCompanyDto
        {
            public int CompanyId { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public string? CompanySchema { get; set; }
            public bool IsDefault { get; set; }
            public bool IsActive { get; set; }
            public DateTime? AccessGrantedDate { get; set; }
            public DateTime? AccessExpiryDate { get; set; }
        }

        public class AssignCompaniesDto
        {
            public List<int> CompanyIds { get; set; } = new();
        }

        #endregion
    }
}