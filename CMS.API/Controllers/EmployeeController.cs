// ================================================================================
// ARCHIVO: CMS.API/Controllers/EmployeeController.cs
// PROPÓSITO: API REST CRUD para empleados del módulo Human Resources
// DESCRIPCIÓN: Gestión de empleados por compañía. La tabla employee reside
//              en la BD de cada compañía ({schema}.employee).
//              Departamento y JobPosition son catálogos por compañía.
//              Vínculo opcional a usuario del sistema (FK lógica cross-DB).
//              Gender, TypeId y Currency son catálogos centrales (admin.*).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-07-04
// ================================================================================

using CMS.Data;
using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/employees")]
    public class EmployeeController : ControllerBase
    {
        private readonly ICompanyDbContextFactory _factory;
        private readonly AppDbContext _adminDb;
        private readonly ILocationService _locationService;
        private readonly ILogger<EmployeeController> _logger;

        public EmployeeController(
            ICompanyDbContextFactory factory,
            AppDbContext adminDb,
            ILocationService locationService,
            ILogger<EmployeeController> logger)
        {
            _factory          = factory;
            _adminDb          = adminDb;
            _locationService  = locationService;
            _logger           = logger;
        }

        private int GetCurrentCompanyId()
        {
            var v = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(v, out var id)) return id;
            throw new UnauthorizedAccessException("companyId no encontrado en el token JWT");
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "SYSTEM";

        // ============================================================
        // GET /api/employees
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search       = null,
            [FromQuery] bool?   isActive     = null,
            [FromQuery] int?    idDepartment = null,
            [FromQuery] string? employmentType = null,
            [FromQuery] int     page         = 1,
            [FromQuery] int     pageSize     = 20)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var q = db.Employees.AsQueryable();

                if (isActive.HasValue)      q = q.Where(x => x.IsActive == isActive.Value);
                if (idDepartment.HasValue)  q = q.Where(x => x.IdDepartment == idDepartment.Value);
                if (!string.IsNullOrWhiteSpace(employmentType))
                    q = q.Where(x => x.EmploymentType == employmentType);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.ToLower();
                    q = q.Where(x =>
                        x.FullName.ToLower().Contains(s) ||
                        x.Code.ToLower().Contains(s) ||
                        x.IdNumber.ToLower().Contains(s) ||
                        (x.Email != null && x.Email.ToLower().Contains(s)));
                }

                var total = await q.CountAsync();
                var items = await q.OrderBy(x => x.FullName)
                                   .Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

                // Enriquecer con usuario del sistema
                var userIds = items.Where(x => x.IdSystemUser.HasValue)
                                   .Select(x => x.IdSystemUser!.Value)
                                   .Distinct().ToList();
                var sysUsers = await _adminDb.Users
                    .Where(u => userIds.Contains(u.ID_USER))
                    .Select(u => new { Id = u.ID_USER, Name = u.DISPLAY_NAME, Email = u.EMAIL })
                    .ToListAsync();

                // Enriquecer con departamento (catálogo por compañía)
                var deptIds = items.Where(x => x.IdDepartment.HasValue)
                                   .Select(x => x.IdDepartment!.Value)
                                   .Distinct().ToList();
                var depts = await db.Departments
                    .Where(d => deptIds.Contains(d.Id))
                    .Select(d => new { d.Id, d.Name, d.Icon, d.Color })
                    .ToListAsync();

                var result = items.Select(e => new
                {
                    e.Id,
                    e.Code,
                    e.FirstName,
                    e.SecondName,
                    e.LastName,
                    e.SecondLastName,
                    e.FullName,
                    e.IdNumber,
                    e.IdTypeId,
                    e.BirthDate,
                    e.Gender,
                    e.Phone,
                    e.Mobile,
                    e.Email,
                    e.EmploymentType,
                    e.HireDate,
                    e.TerminationDate,
                    e.BaseSalary,
                    e.IdCurrency,
                    e.PaymentFrequency,
                    e.EmergencyContactName,
                    e.EmergencyContactPhone,
                    e.EmergencyContactRelation,
                    e.IsActive,
                    e.Notes,
                    e.IdSystemUser,
                    e.IdDepartment,
                    e.IdJobPosition,
                    e.IdLocation,
                    SystemUserName  = sysUsers.FirstOrDefault(u => u.Id == e.IdSystemUser)?.Name,
                    SystemUserEmail = sysUsers.FirstOrDefault(u => u.Id == e.IdSystemUser)?.Email,
                    DepartmentName  = depts.FirstOrDefault(d => d.Id == e.IdDepartment)?.Name,
                    DepartmentIcon  = depts.FirstOrDefault(d => d.Id == e.IdDepartment)?.Icon,
                    DepartmentColor = depts.FirstOrDefault(d => d.Id == e.IdDepartment)?.Color,
                    e.CreateDate,
                    e.RecordDate
                });

                return Ok(new { total, page, pageSize, items = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener empleados");
                return StatusCode(500, new { message = "Error al obtener empleados." });
            }
        }

        // ============================================================
        // GET /api/employees/{id}
        // ============================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var emp = await db.Employees.FindAsync(id);
                if (emp == null) return NotFound(new { message = "Empleado no encontrado." });

                CMS.Entities.User? sysUser = null;
                if (emp.IdSystemUser.HasValue)
                    sysUser = await _adminDb.Users.FindAsync(emp.IdSystemUser.Value);

                CMS.Entities.Operational.Department? dept = null;
                if (emp.IdDepartment.HasValue)
                    dept = await db.Departments.FindAsync(emp.IdDepartment.Value);

                return Ok(new
                {
                    emp.Id, emp.Code, emp.FirstName, emp.SecondName, emp.LastName, emp.SecondLastName,
                    emp.FullName, emp.IdNumber, emp.IdTypeId, emp.BirthDate, emp.Gender,
                    emp.Phone, emp.Mobile, emp.Email, emp.IdLocation,
                    emp.IdJobPosition,
                    emp.EmploymentType, emp.HireDate, emp.TerminationDate,
                    emp.TerminationReason, emp.BaseSalary, emp.IdCurrency,
                    emp.PaymentFrequency, emp.EmergencyContactName,
                    emp.EmergencyContactPhone, emp.EmergencyContactRelation,
                    emp.IsActive, emp.Notes, emp.IdSystemUser, emp.IdDepartment,
                    SystemUserName  = sysUser?.DISPLAY_NAME,
                    SystemUserEmail = sysUser?.EMAIL,
                    DepartmentName  = dept?.Name,
                    DepartmentIcon  = dept?.Icon,
                    DepartmentColor = dept?.Color
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener empleado {Id}", id);
                return StatusCode(500, new { message = "Error al obtener el empleado." });
            }
        }

        // ============================================================
        // POST /api/employees
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Employee dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                if (await db.Employees.AnyAsync(x => x.Code == dto.Code))
                    return Conflict(new { message = $"El código '{dto.Code}' ya existe." });

                if (await db.Employees.AnyAsync(x => x.IdNumber == dto.IdNumber))
                    return Conflict(new { message = $"El número de identificación '{dto.IdNumber}' ya existe." });

                if (dto.IdSystemUser.HasValue)
                {
                    var sysUser = await _adminDb.Users.FindAsync(dto.IdSystemUser.Value);
                    if (sysUser == null || !sysUser.IS_ACTIVE)
                        return BadRequest(new { message = "El usuario del sistema no existe o está inactivo." });
                }

                dto.Code     = dto.Code.Trim().ToUpper();
                dto.FullName = $"{dto.FirstName.Trim()}{(string.IsNullOrWhiteSpace(dto.SecondName) ? "" : " " + dto.SecondName.Trim())} {dto.LastName.Trim()}{(string.IsNullOrWhiteSpace(dto.SecondLastName) ? "" : " " + dto.SecondLastName.Trim())}".Trim();
                dto.CreateDate  = DateTime.UtcNow;
                dto.RecordDate  = DateTime.UtcNow;
                dto.CreatedBy   = GetCurrentUser();
                dto.UpdatedBy   = GetCurrentUser();

                db.Employees.Add(dto);
                await db.SaveChangesAsync();

                // Vincular localización con este empleado
                if (dto.IdLocation.HasValue)
                    await _locationService.SetLocationCatalogAsync(companyId, dto.IdLocation.Value, dto.Id, GetCurrentUser());

                _logger.LogInformation("Empleado creado: {Code} por {User}", dto.Code, dto.CreatedBy);
                return CreatedAtAction(nameof(GetById), new { id = dto.Id }, new { dto.Id, dto.Code, dto.FullName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear empleado");
                return StatusCode(500, new { message = "Error al crear el empleado." });
            }
        }

        // ============================================================
        // PUT /api/employees/{id}
        // ============================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Employee dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var emp = await db.Employees.FindAsync(id);
                if (emp == null) return NotFound(new { message = "Empleado no encontrado." });

                if (await db.Employees.AnyAsync(x => x.Code == dto.Code.Trim().ToUpper() && x.Id != id))
                    return Conflict(new { message = $"El código '{dto.Code}' ya está en uso." });

                if (await db.Employees.AnyAsync(x => x.IdNumber == dto.IdNumber && x.Id != id))
                    return Conflict(new { message = $"El número de identificación '{dto.IdNumber}' ya está en uso." });

                if (dto.IdSystemUser.HasValue)
                {
                    var sysUser = await _adminDb.Users.FindAsync(dto.IdSystemUser.Value);
                    if (sysUser == null || !sysUser.IS_ACTIVE)
                        return BadRequest(new { message = "El usuario del sistema no existe o está inactivo." });
                }

                var oldLocationId = emp.IdLocation;

                emp.Code                    = dto.Code.Trim().ToUpper();
                emp.FirstName               = dto.FirstName.Trim();
                emp.SecondName              = dto.SecondName?.Trim();
                emp.LastName                = dto.LastName.Trim();
                emp.SecondLastName          = dto.SecondLastName.Trim();
                emp.FullName                = $"{emp.FirstName}{(string.IsNullOrWhiteSpace(emp.SecondName) ? "" : " " + emp.SecondName)} {emp.LastName}{(string.IsNullOrWhiteSpace(emp.SecondLastName) ? "" : " " + emp.SecondLastName)}".Trim();
                emp.IdNumber                = dto.IdNumber.Trim();
                emp.IdTypeId                = dto.IdTypeId;
                emp.BirthDate               = dto.BirthDate;
                emp.Gender                  = dto.Gender;
                emp.Phone                   = dto.Phone?.Trim();
                emp.Mobile                  = dto.Mobile.Trim();
                emp.Email                   = dto.Email.Trim();
                emp.IdLocation              = dto.IdLocation;
                emp.IdJobPosition           = dto.IdJobPosition;
                emp.EmploymentType          = dto.EmploymentType;
                emp.HireDate                = dto.HireDate;
                emp.TerminationDate         = dto.TerminationDate;
                emp.TerminationReason       = dto.TerminationReason?.Trim();
                emp.BaseSalary              = dto.BaseSalary;
                emp.IdCurrency              = dto.IdCurrency;
                emp.PaymentFrequency        = dto.PaymentFrequency;
                emp.EmergencyContactName    = dto.EmergencyContactName?.Trim();
                emp.EmergencyContactPhone   = dto.EmergencyContactPhone?.Trim();
                emp.EmergencyContactRelation = dto.EmergencyContactRelation?.Trim();
                emp.IsActive                = dto.IsActive;
                emp.Notes                   = dto.Notes?.Trim();
                emp.IdSystemUser            = dto.IdSystemUser;
                emp.IdDepartment            = dto.IdDepartment;
                emp.RecordDate              = DateTime.UtcNow;
                emp.UpdatedBy               = GetCurrentUser();

                await db.SaveChangesAsync();

                var currentUser = GetCurrentUser();
                // Si cambió la localización, liberar la anterior y vincular la nueva
                if (oldLocationId != dto.IdLocation)
                {
                    if (oldLocationId.HasValue)
                        await _locationService.ClearLocationCatalogAsync(companyId, oldLocationId.Value, currentUser);
                    if (dto.IdLocation.HasValue)
                        await _locationService.SetLocationCatalogAsync(companyId, dto.IdLocation.Value, id, currentUser);
                }

                _logger.LogInformation("Empleado actualizado: {Code} por {User}", emp.Code, emp.UpdatedBy);
                return Ok(new { emp.Id, emp.Code, emp.FullName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar empleado {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar el empleado." });
            }
        }

        // ============================================================
        // PATCH /api/employees/{id}/deactivate
        // ============================================================
        [HttpPatch("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(int id, [FromBody] TerminationDto? termDto = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var emp = await db.Employees.FindAsync(id);
                if (emp == null) return NotFound();

                emp.IsActive          = false;
                emp.TerminationDate   = termDto?.TerminationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                emp.TerminationReason = termDto?.Reason?.Trim();
                emp.RecordDate        = DateTime.UtcNow;
                emp.UpdatedBy         = GetCurrentUser();

                await db.SaveChangesAsync();
                return Ok(new { message = "Empleado dado de baja." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar empleado {Id}", id);
                return StatusCode(500, new { message = "Error al dar de baja al empleado." });
            }
        }

        // ============================================================
        // PATCH /api/employees/{id}/activate
        // ============================================================
        [HttpPatch("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var emp = await db.Employees.FindAsync(id);
                if (emp == null) return NotFound();

                emp.IsActive        = true;
                emp.TerminationDate = null;
                emp.RecordDate      = DateTime.UtcNow;
                emp.UpdatedBy       = GetCurrentUser();

                await db.SaveChangesAsync();
                return Ok(new { message = "Empleado reactivado." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reactivar empleado {Id}", id);
                return StatusCode(500, new { message = "Error al reactivar el empleado." });
            }
        }

        // ============================================================
        // DELETE /api/employees/{id}
        // ============================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var emp = await db.Employees.FindAsync(id);
                if (emp == null) return NotFound();

                db.Employees.Remove(emp);
                await db.SaveChangesAsync();
                return Ok(new { message = "Empleado eliminado." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar empleado {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar el empleado." });
            }
        }

        // ============================================================
        // GET /api/employees/system-users — Usuarios activos de la compañía
        // ============================================================
        [HttpGet("system-users")]
        public async Task<IActionResult> GetSystemUsers()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var users = await _adminDb.UserCompanies
                    .Where(uc => uc.ID_COMPANY == companyId && uc.IS_ACTIVE)
                    .Join(_adminDb.Users,
                          uc => uc.ID_USER,
                          u  => u.ID_USER,
                          (uc, u) => new
                          {
                              Id        = u.ID_USER,
                              Name      = u.DISPLAY_NAME,
                              Email     = u.EMAIL,
                              FirstName = u.FIRST_NAME,
                              LastName  = u.LAST_NAME,
                              Phone     = u.PHONE_NUMBER,
                              IsActive  = u.IS_ACTIVE
                          })
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios del sistema");
                return StatusCode(500, new { message = "Error al obtener usuarios." });
            }
        }

        // ============================================================
        // GET /api/employees/departments — Catálogo de departamentos (por compañía)
        // ============================================================
        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments([FromQuery] bool? isActive = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var q = db.Departments.AsQueryable();
                if (isActive.HasValue) q = q.Where(d => d.IsActive == isActive.Value);

                var depts = await q.OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
                    .Select(d => new { d.Id, d.Code, d.Name, d.Description, d.Icon, d.Color, d.IsActive })
                    .ToListAsync();

                return Ok(depts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener departamentos");
                return StatusCode(500, new { message = "Error al obtener departamentos." });
            }
        }

        // ============================================================
        // GET /api/employees/job-positions — Catálogo de puestos (por compañía)
        // ============================================================
        [HttpGet("job-positions")]
        public async Task<IActionResult> GetJobPositions([FromQuery] bool? isActive = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var q = db.JobPositions.AsQueryable();
                if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);

                var positions = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                    .Select(x => new { x.Id, x.Code, x.Name, x.Level, x.IdDepartment, x.IsActive })
                    .ToListAsync();

                return Ok(positions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener puestos");
                return StatusCode(500, new { message = "Error al obtener puestos." });
            }
        }

        // ============================================================
        // GET /api/employees/locations — Ubicaciones de tipo EMPLOYEE disponibles
        // ?currentEmployeeId=X incluye la ya asignada al empleado en edición
        // ============================================================
        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations([FromQuery] int? currentEmployeeId = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();

                // Obtener el id del tipo EMPLOYEE desde la BD central
                var employeeTypeId = await _adminDb.LocationTypes
                    .Where(t => t.Code == "EMPLOYEE" && t.IsActive)
                    .Select(t => t.Id)
                    .FirstOrDefaultAsync();

                if (employeeTypeId == 0)
                    return Ok(new List<object>());

                // Si estamos editando un empleado, permitir también su dirección actual
                int? currentLocationId = null;
                if (currentEmployeeId.HasValue)
                {
                    await using var db = await _factory.CreateDbContextAsync(companyId);
                    currentLocationId = await db.Employees
                        .Where(e => e.Id == currentEmployeeId.Value)
                        .Select(e => e.IdLocation)
                        .FirstOrDefaultAsync();
                }

                // Obtener ubicaciones disponibles (catalog IS NULL) + la del empleado actual
                var locs = (await _locationService.GetAvailableByTypeAsync(companyId, employeeTypeId, currentLocationId))
                    .ToList();

                // Resolver nombres de divisiones geográficas desde BD central
                var div1Ids = locs.Where(l => l.IdGeographicDivision1.HasValue).Select(l => l.IdGeographicDivision1!.Value).Distinct().ToList();
                var div2Ids = locs.Where(l => l.IdGeographicDivision2.HasValue).Select(l => l.IdGeographicDivision2!.Value).Distinct().ToList();
                var div3Ids = locs.Where(l => l.IdGeographicDivision3.HasValue).Select(l => l.IdGeographicDivision3!.Value).Distinct().ToList();

                var div1Names = div1Ids.Count > 0
                    ? await _adminDb.GeographicDivisions1
                        .Where(d => div1Ids.Contains(d.IdGeographicDivision1))
                        .Select(d => new { d.IdGeographicDivision1, d.Name })
                        .ToDictionaryAsync(d => d.IdGeographicDivision1, d => d.Name)
                    : new Dictionary<int, string>();

                var div2Names = div2Ids.Count > 0
                    ? await _adminDb.GeographicDivisions2
                        .Where(d => div2Ids.Contains(d.IdGeographicDivision2))
                        .Select(d => new { d.IdGeographicDivision2, d.Name })
                        .ToDictionaryAsync(d => d.IdGeographicDivision2, d => d.Name)
                    : new Dictionary<int, string>();

                var div3Names = div3Ids.Count > 0
                    ? await _adminDb.GeographicDivisions3
                        .Where(d => div3Ids.Contains(d.IdGeographicDivision3))
                        .Select(d => new { d.IdGeographicDivision3, d.Name })
                        .ToDictionaryAsync(d => d.IdGeographicDivision3, d => d.Name)
                    : new Dictionary<int, string>();

                var result = locs.Select(l =>
                {
                    var parts = new List<string>();
                    if (l.IdGeographicDivision1.HasValue && div1Names.TryGetValue(l.IdGeographicDivision1.Value, out var n1)) parts.Add(n1);
                    if (l.IdGeographicDivision2.HasValue && div2Names.TryGetValue(l.IdGeographicDivision2.Value, out var n2)) parts.Add(n2);
                    if (l.IdGeographicDivision3.HasValue && div3Names.TryGetValue(l.IdGeographicDivision3.Value, out var n3)) parts.Add(n3);
                    if (!string.IsNullOrWhiteSpace(l.Address)) parts.Add(l.Address);
                    return new
                    {
                        l.Id,
                        Display = parts.Count > 0 ? string.Join(", ", parts) : $"Ubicación #{l.Id}"
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ubicaciones");
                return StatusCode(500, new { message = "Error al obtener ubicaciones." });
            }
        }

        // ============================================================
        // GET /api/employees/genders — Catálogo de géneros (admin.gender)
        // ============================================================
        [HttpGet("genders")]
        public async Task<IActionResult> GetGenders()
        {
            try
            {
                var genders = await _adminDb.Genders
                    .Where(g => g.IS_ACTIVE)
                    .OrderBy(g => g.DESCRIPTION)
                    .Select(g => new { Code = g.GENDER_CODE, Description = g.DESCRIPTION })
                    .ToListAsync();

                return Ok(genders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener géneros");
                return StatusCode(500, new { message = "Error al obtener géneros." });
            }
        }

        // ============================================================
        // GET /api/employees/type-ids — Catálogo de tipos de ID (admin.type_id)
        // ============================================================
        [HttpGet("type-ids")]
        public async Task<IActionResult> GetTypeIds()
        {
            try
            {
                var typeIds = await _adminDb.TypeIds
                    .Where(t => t.IS_ACTIVE)
                    .OrderBy(t => t.SORT_ORDER)
                    .Select(t => new
                    {
                        Id              = t.ID_TYPE_ID,
                        Description     = t.DESCRIPTION,
                        NumberChars     = t.NUMBER_CHARACTERS,
                        AllowLetters    = t.ALLOW_LETTERS,
                        FormatValidation = t.FORMAT_VALIDATION
                    })
                    .ToListAsync();

                return Ok(typeIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tipos de ID");
                return StatusCode(500, new { message = "Error al obtener tipos de ID." });
            }
        }

        // ============================================================
        // GET /api/employees/currencies — Monedas activas (admin.currency)
        // ============================================================
        [HttpGet("currencies")]
        public async Task<IActionResult> GetCurrencies()
        {
            try
            {
                var currencies = await _adminDb.Currencies
                    .Where(c => c.IS_ACTIVE)
                    .OrderBy(c => c.SORT_ORDER).ThenBy(c => c.CURRENCY_CODE)
                    .Select(c => new
                    {
                        id     = c.ID_CURRENCY,
                        code   = c.CURRENCY_CODE,
                        name   = c.CURRENCY_NAME,
                        symbol = c.CURRENCY_SYMBOL
                    })
                    .ToListAsync();

                return Ok(currencies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener monedas");
                return StatusCode(500, new { message = "Error al obtener monedas." });
            }
        }

        // ============================================================
        // GET /api/employees/company-currency — Moneda por defecto de la compañía
        // ============================================================
        [HttpGet("company-currency")]
        public async Task<IActionResult> GetCompanyCurrency()
        {
            try
            {
                var companyId = GetCurrentCompanyId();

                var currency = await _adminDb.Companies
                    .Where(c => c.ID == companyId && c.IdCountry.HasValue)
                    .Join(_adminDb.Countries,
                          co => co.IdCountry!.Value,
                          ct => ct.ID_COUNTRY,
                          (co, ct) => ct.ID_CURRENCY)
                    .Join(_adminDb.Currencies,
                          idCur => idCur,
                          cur => cur.ID_CURRENCY,
                          (idCur, cur) => new
                          {
                              id     = cur.ID_CURRENCY,
                              code   = cur.CURRENCY_CODE,
                              name   = cur.CURRENCY_NAME,
                              symbol = cur.CURRENCY_SYMBOL
                          })
                    .FirstOrDefaultAsync();

                return Ok(currency);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener moneda de la compañía");
                return StatusCode(500, new { message = "Error al obtener moneda de la compañía." });
            }
        }

        // ============================================================
        // GET /api/employees/stats — KPIs del dashboard
        // ============================================================
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var now = DateOnly.FromDateTime(DateTime.UtcNow);
                var total       = await db.Employees.CountAsync();
                var active      = await db.Employees.CountAsync(e => e.IsActive);
                var newThisMonth = await db.Employees.CountAsync(e =>
                    e.HireDate.Year == now.Year &&
                    e.HireDate.Month == now.Month);
                var byDept = await db.Employees
                    .Where(e => e.IsActive && e.IdDepartment.HasValue)
                    .GroupBy(e => e.IdDepartment)
                    .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
                    .ToListAsync();

                return Ok(new { total, active, inactive = total - active, newThisMonth, byDept });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stats");
                return StatusCode(500, new { message = "Error al obtener estadísticas." });
            }
        }

        // ============================================================
        // GET /api/employees/drivers
        // Devuelve empleados activos cuyo puesto tiene is_driver = true.
        // Usado por TransportUnits y DistributionRoutes para el selector de conductor.
        // ============================================================
        [HttpGet("drivers")]
        public async Task<IActionResult> GetDrivers()
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                await using var db = await _factory.CreateDbContextAsync(companyId);

                var driverPositionIds = await db.JobPositions
                    .Where(p => p.IsDriver && p.IsActive)
                    .Select(p => p.Id)
                    .ToListAsync();

                var drivers = await db.Employees
                    .Where(e => e.IsActive && e.IdJobPosition.HasValue && driverPositionIds.Contains(e.IdJobPosition!.Value))
                    .OrderBy(e => e.FullName)
                    .Select(e => new
                    {
                        id       = e.Id,
                        code     = e.Code,
                        fullName = e.FullName
                    })
                    .ToListAsync();

                return Ok(drivers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener conductores");
                return StatusCode(500, new { message = "Error al obtener conductores." });
            }
        }
    }

    // ── DTOs auxiliares ──────────────────────────────────────────────
    public class TerminationDto
    {
        public DateOnly? TerminationDate { get; set; }
        public string? Reason { get; set; }
    }
}
