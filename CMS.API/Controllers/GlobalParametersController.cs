// ================================================================================
// ARCHIVO: CMS.API/Controllers/GlobalParametersController.cs
// PROPÓSITO: API REST para gestión de parámetros globales del sistema (POR COMPAÑÍA)
// DESCRIPCIÓN: CRUD completo y consultas por módulo. Los parámetros están en la BD de cada compañía.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-01-22
// MODIFICADO: 2026-01-22 - Actualizado para usar CompanyId del JWT
//             2026-01-22 - Actualizado para usar MenuId y Code
// ================================================================================

using CMS.Data;
using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GlobalParametersController : ControllerBase
    {
        private readonly GlobalParameterService _service;
        private readonly AppDbContext _centralDb;
        private readonly ILogger<GlobalParametersController> _logger;

        public GlobalParametersController(
            GlobalParameterService service,
            AppDbContext centralDb,
            ILogger<GlobalParametersController> logger)
        {
            _service = service;
            _centralDb = centralDb;
            _logger = logger;
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private int GetCompanyId()
        {
            var companyIdClaim = User.FindFirstValue("companyId") ?? User.FindFirstValue("CompanyId");
            if (!int.TryParse(companyIdClaim, out var companyId))
                throw new UnauthorizedAccessException("companyId no encontrado en el token");
            return companyId;
        }

        /// <summary>
        /// Obtiene el ID de un menú por su nombre desde la BD central
        /// </summary>
        private async Task<int?> GetMenuIdByNameAsync(string menuName)
        {
            var menu = await _centralDb.Menus
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.NAME == menuName && m.ID_PARENT == 0);
            return menu?.ID_MENU;
        }

        // ============================================================
        // CONSULTAS
        // ============================================================

        /// <summary>
        /// GET: api/globalparameters
        /// Obtiene todos los parámetros globales activos de la compañía actual
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<GlobalParameterDto>>> GetAll()
        {
            try
            {
                var companyId = GetCompanyId();
                var parameters = await _service.GetAllParametersAsync(companyId);
                var dtos = parameters.Select(MapToDto).ToList();
                return Ok(dtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo parámetros globales");
                return StatusCode(500, new { message = "Error obteniendo parámetros globales" });
            }
        }

        /// <summary>
        /// GET: api/globalparameters/menus
        /// Obtiene la lista de IDs de menús que tienen parámetros en la compañía actual
        /// </summary>
        [HttpGet("menus")]
        public async Task<ActionResult<List<int>>> GetMenus()
        {
            try
            {
                var companyId = GetCompanyId();
                var menuIds = await _service.GetMenuIdsAsync(companyId);
                return Ok(menuIds);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo menús");
                return StatusCode(500, new { message = "Error obteniendo menús" });
            }
        }

        /// <summary>
        /// GET: api/globalparameters/menus/with-names
        /// Obtiene la lista de menús con parámetros incluyendo sus nombres desde admin.menu
        /// </summary>
        [HttpGet("menus/with-names")]
        public async Task<ActionResult<List<MenuWithParametersDto>>> GetMenusWithNames()
        {
            try
            {
                var companyId = GetCompanyId();
                _logger.LogInformation("🔍 Obteniendo menús con parámetros para compañía {CompanyId}", companyId);

                var menuIds = await _service.GetMenuIdsAsync(companyId);
                _logger.LogInformation("📋 MenuIds encontrados: {Count} - IDs: {Ids}", 
                    menuIds.Count, string.Join(", ", menuIds));

                if (!menuIds.Any())
                {
                    _logger.LogWarning("⚠️ No se encontraron menús con parámetros para la compañía {CompanyId}", companyId);
                    return Ok(new List<MenuWithParametersDto>());
                }

                // Cargar nombres desde admin.menu
                var menus = await _centralDb.Menus
                    .AsNoTracking()
                    .Where(m => menuIds.Contains(m.ID_MENU))
                    .Select(m => new MenuWithParametersDto
                    {
                        MenuId = m.ID_MENU,
                        MenuName = m.NAME
                    })
                    .OrderBy(m => m.MenuName)
                    .ToListAsync();

                _logger.LogInformation("✅ Menús con nombres cargados: {Count}", menus.Count);

                return Ok(menus);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "❌ Error de autorización obteniendo menús");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo menús con nombres");
                return StatusCode(500, new { message = "Error obteniendo menús con nombres", detail = ex.Message });
            }
        }

        /// <summary>
        /// GET: api/globalparameters/menu/{menuId}
        /// Obtiene todos los parámetros de un menú específico de la compañía actual
        /// </summary>
        [HttpGet("menu/{menuId:int}")]
        public async Task<ActionResult<List<GlobalParameterDto>>> GetByMenuId(int menuId)
        {
            try
            {
                var companyId = GetCompanyId();
                _logger.LogInformation("🔍 Obteniendo parámetros del menú {MenuId} para compañía {CompanyId}", menuId, companyId);

                var parameters = await _service.GetParametersByMenuIdAsync(companyId, menuId);

                _logger.LogInformation("📋 Parámetros encontrados: {Count}", parameters.Count);

                var dtos = parameters.Select(MapToDto).ToList();

                _logger.LogInformation("✅ DTOs generados: {Count}", dtos.Count);

                return Ok(dtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "❌ Error de autorización");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo parámetros del menú {MenuId}", menuId);
                return StatusCode(500, new { message = $"Error obteniendo parámetros del menú {menuId}" });
            }
        }

        /// <summary>
        /// GET: api/globalparameters/module/{moduleName}
        /// Obtiene todos los parámetros de un módulo por nombre (compatibilidad con frontend legacy)
        /// </summary>
        [HttpGet("module/{moduleName}")]
        public async Task<ActionResult<List<GlobalParameterDto>>> GetByModuleName(string moduleName)
        {
            try
            {
                var companyId = GetCompanyId();

                // Convertir nombre de módulo a menu ID
                var menuId = await GetMenuIdByNameAsync(moduleName);
                if (menuId == null)
                {
                    return NotFound(new { message = $"Menú '{moduleName}' no encontrado" });
                }

                var parameters = await _service.GetParametersByMenuIdAsync(companyId, menuId.Value);
                var dtos = parameters.Select(MapToDto).ToList();
                return Ok(dtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo parámetros del módulo {Module}", moduleName);
                return StatusCode(500, new { message = $"Error obteniendo parámetros del módulo {moduleName}" });
            }
        }

        /// <summary>
        /// GET: api/globalparameters/{menuId:int}/{code}/value
        /// Obtiene el valor de un parámetro específico por menu ID y código (SIN CACHÉ - siempre lee de BD)
        /// </summary>
        [HttpGet("{menuId:int}/{code}/value")]
        public async Task<ActionResult<object>> GetParameterValueByMenuId(int menuId, string code)
        {
            try
            {
                var companyId = GetCompanyId();
                // ⚠️ IMPORTANTE: Usar GetParameterNoCacheAsync para obtener datos frescos de la BD
                var parameter = await _service.GetParameterNoCacheAsync(companyId, menuId, code);

                if (parameter == null)
                {
                    return NotFound(new { message = $"Parámetro Menu{menuId}.{code} no encontrado" });
                }

                var value = parameter.GetValue();
                return Ok(new { 
                    menuId = parameter.MenuId,
                    code = parameter.Code,
                    dataType = parameter.DataType,
                    value = value 
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo valor de Menu{MenuId}.{Code}", menuId, code);
                return StatusCode(500, new { message = "Error obteniendo valor del parámetro" });
            }
        }

        /// <summary>
        /// GET: api/globalparameters/{moduleName}/{code}/value
        /// Obtiene el valor de un parámetro por nombre de módulo y código (compatibilidad con frontend legacy)
        /// </summary>
        [HttpGet("{moduleName}/{code}/value")]
        public async Task<ActionResult<object>> GetParameterValueByModuleName(string moduleName, string code)
        {
            try
            {
                var companyId = GetCompanyId();

                // Convertir nombre de módulo a menu ID
                var menuId = await GetMenuIdByNameAsync(moduleName);
                if (menuId == null)
                {
                    return NotFound(new { message = $"Menú '{moduleName}' no encontrado" });
                }

                var parameter = await _service.GetParameterAsync(companyId, menuId.Value, code);

                if (parameter == null)
                {
                    return NotFound(new { message = $"Parámetro {moduleName}.{code} no encontrado" });
                }

                var value = parameter.GetValue();
                return Ok(new { 
                    menuId = parameter.MenuId,
                    code = parameter.Code,
                    dataType = parameter.DataType,
                    value = value 
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo valor de {Module}.{Code}", moduleName, code);
                return StatusCode(500, new { message = "Error obteniendo valor del parámetro" });
            }
        }

        /// <summary>
        /// GET: api/globalparameters/{id}
        /// Obtiene un parámetro por ID de la compañía actual
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<GlobalParameterDto>> GetById(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var parameter = await _service.GetByIdAsync(companyId, id);

                if (parameter == null)
                {
                    return NotFound(new { message = $"Parámetro con ID {id} no encontrado" });
                }

                return Ok(MapToDto(parameter));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo parámetro {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo parámetro" });
            }
        }

        // ============================================================
        // CRUD (Requiere permiso Settings.GlobalParameters.Edit)
        // ============================================================

        /// <summary>
        /// POST: api/globalparameters
        /// Crea un nuevo parámetro global en la compañía actual
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "Settings.GlobalParameters.Edit")]
        public async Task<ActionResult<GlobalParameterDto>> Create([FromBody] GlobalParameterCreateDto dto)
        {
            try
            {
                var companyId = GetCompanyId();
                var parameter = MapFromCreateDto(dto);
                parameter.CreatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM";
                parameter.UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM";

                var created = await _service.CreateAsync(companyId, parameter);
                return CreatedAtAction(nameof(GetById), new { id = created.ID }, MapToDto(created));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando parámetro global");
                return StatusCode(500, new { message = "Error creando parámetro" });
            }
        }

        /// <summary>
        /// PUT: api/globalparameters/{id}
        /// Actualiza un parámetro existente de la compañía actual
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Policy = "Settings.GlobalParameters.Edit")]
        public async Task<ActionResult<GlobalParameterDto>> Update(int id, [FromBody] GlobalParameterUpdateDto dto)
        {
            try
            {
                var companyId = GetCompanyId();
                var parameter = MapFromUpdateDto(dto);
                parameter.ID = id;
                parameter.UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM";

                var updated = await _service.UpdateAsync(companyId, parameter);
                return Ok(MapToDto(updated));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando parámetro {Id}", id);
                return StatusCode(500, new { message = "Error actualizando parámetro" });
            }
        }

        /// <summary>
        /// DELETE: api/globalparameters/{id}
        /// Elimina un parámetro de la compañía actual (solo si no es del sistema)
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "Settings.GlobalParameters.Edit")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var result = await _service.DeleteAsync(companyId, id);

                if (!result)
                {
                    return NotFound(new { message = $"Parámetro con ID {id} no encontrado" });
                }

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando parámetro {Id}", id);
                return StatusCode(500, new { message = "Error eliminando parámetro" });
            }
        }

        // ============================================================
        // MAPEO DE DTOs
        // ============================================================

        private GlobalParameterDto MapToDto(GlobalParameter parameter)
        {
            return new GlobalParameterDto
            {
                Id = parameter.ID,
                MenuId = parameter.MenuId,
                Code = parameter.Code,
                ParameterName = parameter.ParameterName,
                Description = parameter.Description,
                DataType = parameter.DataType,
                ValueString = parameter.ValueString,
                ValueBoolean = parameter.ValueBoolean,
                ValueInteger = parameter.ValueInteger,
                ValueDecimal = parameter.ValueDecimal,
                ValueJson = parameter.ValueJson,
                Value = parameter.GetValue(),
                Category = parameter.Category,
                SortOrder = parameter.SortOrder,
                IsSystem = parameter.IsSystem,
                IsActive = parameter.IsActive,
                CreatedBy = parameter.CreatedBy,
                UpdatedBy = parameter.UpdatedBy,
                CreateDate = parameter.CreateDate,
                RecordDate = parameter.RecordDate
            };
        }

        private GlobalParameter MapFromCreateDto(GlobalParameterCreateDto dto)
        {
            var parameter = new GlobalParameter
            {
                MenuId = dto.MenuId,
                Code = dto.Code,
                ParameterName = dto.ParameterName,
                Description = dto.Description,
                DataType = dto.DataType,
                Category = dto.Category,
                SortOrder = dto.SortOrder,
                IsSystem = dto.IsSystem,
                IsActive = dto.IsActive
            };

            parameter.SetValue(dto.Value);
            return parameter;
        }

        private GlobalParameter MapFromUpdateDto(GlobalParameterUpdateDto dto)
        {
            var parameter = new GlobalParameter
            {
                MenuId = dto.MenuId,
                Code = dto.Code,
                ParameterName = dto.ParameterName,
                Description = dto.Description,
                DataType = dto.DataType,
                Category = dto.Category,
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive
            };

            parameter.SetValue(dto.Value);
            return parameter;
        }
    }

    // ============================================================
    // DTOs
    // ============================================================

    public class GlobalParameterDto
    {
        public int Id { get; set; }
        public int MenuId { get; set; }
        public string Code { get; set; } = default!;
        public string ParameterName { get; set; } = default!;
        public string? Description { get; set; }
        public string DataType { get; set; } = default!;
        public string? ValueString { get; set; }
        public bool? ValueBoolean { get; set; }
        public int? ValueInteger { get; set; }
        public decimal? ValueDecimal { get; set; }
        public string? ValueJson { get; set; }
        public object? Value { get; set; }
        public string? Category { get; set; }
        public int SortOrder { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public string CreatedBy { get; set; } = default!;
        public string UpdatedBy { get; set; } = default!;
        public DateTime CreateDate { get; set; }
        public DateTime RecordDate { get; set; }
    }

    public class GlobalParameterCreateDto
    {
        public int MenuId { get; set; }
        public string Code { get; set; } = default!;
        public string ParameterName { get; set; } = default!;
        public string? Description { get; set; }
        public string DataType { get; set; } = "string";
        public object? Value { get; set; }
        public string? Category { get; set; }
        public int SortOrder { get; set; } = 0;
        public bool IsSystem { get; set; } = false;
        public bool IsActive { get; set; } = true;
    }

    public class GlobalParameterUpdateDto
    {
        public int MenuId { get; set; }
        public string Code { get; set; } = default!;
        public string ParameterName { get; set; } = default!;
        public string? Description { get; set; }
        public string DataType { get; set; } = "string";
        public object? Value { get; set; }
        public string? Category { get; set; }
        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class MenuWithParametersDto
    {
        public int MenuId { get; set; }
        public string MenuName { get; set; } = default!;
    }
}
