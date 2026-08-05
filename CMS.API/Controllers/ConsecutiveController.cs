using CMS.Application.DTOs;
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
    public class ConsecutiveController : ControllerBase
    {
        private readonly CompanyDbContextFactory _companyDbContextFactory;
        private readonly AppDbContext _db;
        private readonly ILogger<ConsecutiveController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ConsecutiveController(
            CompanyDbContextFactory companyDbContextFactory,
            AppDbContext db,
            ILogger<ConsecutiveController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _companyDbContextFactory = companyDbContextFactory;
            _db = db;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private int GetCompanyId()
        {
            var companyIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("CompanyId")?.Value;
            return int.TryParse(companyIdClaim, out var companyId) ? companyId : 0;
        }

        // GET: api/Consecutive - Lista completa con entity type y document
        [HttpGet]
        public async Task<ActionResult<List<ConsecutiveDto>>> GetAll()
        {
            try
            {
                var companyId = GetCompanyId();
                if (companyId == 0)
                    return BadRequest(new { message = "No se pudo determinar la compañía activa" });

                await using var companyDb = await _companyDbContextFactory.CreateDbContextAsync(companyId);

                var consecutives = await companyDb.Consecutives
                    .OrderBy(c => c.CODE)
                    .Select(c => new ConsecutiveDto
                    {
                        Id = c.ID_CONSECUTIVE,
                        Code = c.CODE,
                        Description = c.DESCRIPTION,
                        IdEntityType = c.ID_ENTITY_TYPE,
                        IdEntityDocument = c.ID_ENTITY_DOCUMENT,
                        IdMenu = c.ID_MENU,
                        Mask = c.MASK,
                        Length = c.LENGTH,
                        InitialValue = c.INITIAL_VALUE,
                        FinalValue = c.FINAL_VALUE,
                        LastValue = c.LAST_VALUE,
                        LastUser = c.LAST_USER,
                        LastDate = c.LAST_DATE,
                        IsActive = c.IS_ACTIVE
                    })
                    .ToListAsync();

                // Obtener nombres de entity type, document y menu desde la BD central
                foreach (var cons in consecutives)
                {
                    var entityType = await _db.EntityTypes.FindAsync(cons.IdEntityType);
                    var entityDocument = await _db.EntityDocuments.FindAsync(cons.IdEntityDocument);
                    var menu = await _db.Menus.FindAsync(cons.IdMenu);

                    cons.EntityTypeCode = entityType?.CODE ?? string.Empty;
                    cons.EntityTypeName = entityType?.NAME ?? string.Empty;
                    cons.EntityDocumentCode = entityDocument?.CODE ?? string.Empty;
                    cons.EntityDocumentName = entityDocument?.NAME ?? string.Empty;
                    cons.MenuName = menu?.NAME ?? string.Empty;
                    cons.MenuUrl = menu?.URL ?? string.Empty;
                }

                return Ok(consecutives);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo consecutivos");
                return StatusCode(500, new { message = "Error obteniendo consecutivos", error = ex.Message });
            }
        }

        // GET: api/Consecutive/active - Solo activos
        [HttpGet("active")]
        public async Task<ActionResult<List<ConsecutiveDto>>> GetActive()
        {
            try
            {
                var companyId = GetCompanyId();
                if (companyId == 0)
                    return BadRequest(new { message = "No se pudo determinar la compañía activa" });

                await using var companyDb = await _companyDbContextFactory.CreateDbContextAsync(companyId);

                var consecutives = await companyDb.Consecutives
                    .Where(c => c.IS_ACTIVE)
                    .OrderBy(c => c.CODE)
                    .Select(c => new ConsecutiveDto
                    {
                        Id = c.ID_CONSECUTIVE,
                        Code = c.CODE,
                        Description = c.DESCRIPTION,
                        IdEntityType = c.ID_ENTITY_TYPE,
                        IdEntityDocument = c.ID_ENTITY_DOCUMENT,
                        IdMenu = c.ID_MENU,
                        Mask = c.MASK,
                        Length = c.LENGTH,
                        InitialValue = c.INITIAL_VALUE,
                        FinalValue = c.FINAL_VALUE,
                        LastValue = c.LAST_VALUE,
                        LastUser = c.LAST_USER,
                        LastDate = c.LAST_DATE,
                        IsActive = c.IS_ACTIVE
                    })
                    .ToListAsync();

                // Obtener nombres de entity type, document y menu desde la BD central
                foreach (var cons in consecutives)
                {
                    var entityType = await _db.EntityTypes.FindAsync(cons.IdEntityType);
                    var entityDocument = await _db.EntityDocuments.FindAsync(cons.IdEntityDocument);
                    var menu = await _db.Menus.FindAsync(cons.IdMenu);

                    cons.EntityTypeCode = entityType?.CODE ?? string.Empty;
                    cons.EntityTypeName = entityType?.NAME ?? string.Empty;
                    cons.EntityDocumentCode = entityDocument?.CODE ?? string.Empty;
                    cons.EntityDocumentName = entityDocument?.NAME ?? string.Empty;
                    cons.MenuName = menu?.NAME ?? string.Empty;
                    cons.MenuUrl = menu?.URL ?? string.Empty;
                }

                return Ok(consecutives);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo consecutivos activos");
                return StatusCode(500, new { message = "Error obteniendo consecutivos activos", error = ex.Message });
            }
        }

        // GET: api/Consecutive/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ConsecutiveDto>> GetById(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                if (companyId == 0)
                    return BadRequest(new { message = "No se pudo determinar la compañía activa" });

                await using var companyDb = await _companyDbContextFactory.CreateDbContextAsync(companyId);

                var consecutive = await companyDb.Consecutives.FindAsync(id);
                if (consecutive == null)
                    return NotFound(new { message = "Consecutivo no encontrado" });

                var entityType = await _db.EntityTypes.FindAsync(consecutive.ID_ENTITY_TYPE);
                var entityDocument = await _db.EntityDocuments.FindAsync(consecutive.ID_ENTITY_DOCUMENT);
                var menu = await _db.Menus.FindAsync(consecutive.ID_MENU);

                var dto = new ConsecutiveDto
                {
                    Id = consecutive.ID_CONSECUTIVE,
                    Code = consecutive.CODE,
                    Description = consecutive.DESCRIPTION,
                    IdEntityType = consecutive.ID_ENTITY_TYPE,
                    IdEntityDocument = consecutive.ID_ENTITY_DOCUMENT,
                    IdMenu = consecutive.ID_MENU,
                    EntityTypeCode = entityType?.CODE ?? string.Empty,
                    EntityTypeName = entityType?.NAME ?? string.Empty,
                    EntityDocumentCode = entityDocument?.CODE ?? string.Empty,
                    EntityDocumentName = entityDocument?.NAME ?? string.Empty,
                    MenuName = menu?.NAME ?? string.Empty,
                    MenuUrl = menu?.URL ?? string.Empty,
                    Mask = consecutive.MASK,
                    Length = consecutive.LENGTH,
                    InitialValue = consecutive.INITIAL_VALUE,
                    FinalValue = consecutive.FINAL_VALUE,
                    LastValue = consecutive.LAST_VALUE,
                    LastUser = consecutive.LAST_USER,
                    LastDate = consecutive.LAST_DATE,
                    IsActive = consecutive.IS_ACTIVE
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo consecutivo {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo consecutivo", error = ex.Message });
            }
        }

        // POST: api/Consecutive
        [HttpPost]
        public async Task<ActionResult<ConsecutiveDto>> Create([FromBody] ConsecutiveCreateDto dto)
        {
            try
            {
                var companyId = GetCompanyId();
                if (companyId == 0)
                    return BadRequest(new { message = "No se pudo determinar la compañía activa" });

                await using var companyDb = await _companyDbContextFactory.CreateDbContextAsync(companyId);

                // Validar código único
                if (await companyDb.Consecutives.AnyAsync(c => c.CODE == dto.Code))
                    return BadRequest(new { message = "El código ya existe" });

                // Validar que existen entity type y document en la BD central
                if (!await _db.EntityTypes.AnyAsync(et => et.ID_ENTITY_TYPE == dto.IdEntityType))
                    return BadRequest(new { message = "El tipo de entidad no existe" });

                if (!await _db.EntityDocuments.AnyAsync(ed => ed.ID_ENTITY_DOCUMENT == dto.IdEntityDocument))
                    return BadRequest(new { message = "El tipo de documento no existe" });

                if (!await _db.Menus.AnyAsync(m => m.ID_MENU == dto.IdMenu))
                    return BadRequest(new { message = "El menú no existe" });

                var consecutive = new Consecutive
                {
                    CODE = dto.Code,
                    DESCRIPTION = dto.Description,
                    ID_ENTITY_TYPE = dto.IdEntityType,
                    ID_ENTITY_DOCUMENT = dto.IdEntityDocument,
                    ID_MENU = dto.IdMenu,
                    MASK = dto.Mask,
                    LENGTH = dto.Length,
                    INITIAL_VALUE = dto.InitialValue,
                    FINAL_VALUE = dto.FinalValue,
                    LAST_VALUE = null,
                    LAST_USER = null,
                    LAST_DATE = null,
                    IS_ACTIVE = dto.IsActive,
                    CreateDate = DateTime.UtcNow,
                    RecordDate = DateTime.UtcNow,
                    CreatedBy = User.FindFirstValue("cms_username") ?? "SYSTEM",
                    UpdatedBy = User.FindFirstValue("cms_username") ?? "SYSTEM",
                    RowPointer = Guid.NewGuid()
                };

                companyDb.Consecutives.Add(consecutive);
                await companyDb.SaveChangesAsync();

                var entityType = await _db.EntityTypes.FindAsync(consecutive.ID_ENTITY_TYPE);
                var entityDocument = await _db.EntityDocuments.FindAsync(consecutive.ID_ENTITY_DOCUMENT);
                var menu = await _db.Menus.FindAsync(consecutive.ID_MENU);

                var result = new ConsecutiveDto
                {
                    Id = consecutive.ID_CONSECUTIVE,
                    Code = consecutive.CODE,
                    Description = consecutive.DESCRIPTION,
                    IdEntityType = consecutive.ID_ENTITY_TYPE,
                    IdEntityDocument = consecutive.ID_ENTITY_DOCUMENT,
                    IdMenu = consecutive.ID_MENU,
                    EntityTypeCode = entityType?.CODE ?? string.Empty,
                    EntityTypeName = entityType?.NAME ?? string.Empty,
                    EntityDocumentCode = entityDocument?.CODE ?? string.Empty,
                    EntityDocumentName = entityDocument?.NAME ?? string.Empty,
                    MenuName = menu?.NAME ?? string.Empty,
                    MenuUrl = menu?.URL ?? string.Empty,
                    Mask = consecutive.MASK,
                    Length = consecutive.LENGTH,
                    InitialValue = consecutive.INITIAL_VALUE,
                    FinalValue = consecutive.FINAL_VALUE,
                    LastValue = consecutive.LAST_VALUE,
                    LastUser = consecutive.LAST_USER,
                    LastDate = consecutive.LAST_DATE,
                    IsActive = consecutive.IS_ACTIVE
                };

                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando consecutivo");
                return StatusCode(500, new { message = "Error creando consecutivo", error = ex.Message });
            }
        }

        // PUT: api/Consecutive/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<ConsecutiveDto>> Update(int id, [FromBody] ConsecutiveUpdateDto dto)
        {
            try
            {
                var companyId = GetCompanyId();
                if (companyId == 0)
                    return BadRequest(new { message = "No se pudo determinar la compañía activa" });

                await using var companyDb = await _companyDbContextFactory.CreateDbContextAsync(companyId);

                var consecutive = await companyDb.Consecutives.FindAsync(id);
                if (consecutive == null)
                    return NotFound(new { message = "Consecutivo no encontrado" });

                // Validar código único (excluyendo el actual)
                if (await companyDb.Consecutives.AnyAsync(c => c.CODE == dto.Code && c.ID_CONSECUTIVE != id))
                    return BadRequest(new { message = "El código ya existe" });

                // Validar que existen entity type y document en la BD central
                if (!await _db.EntityTypes.AnyAsync(et => et.ID_ENTITY_TYPE == dto.IdEntityType))
                    return BadRequest(new { message = "El tipo de entidad no existe" });

                if (!await _db.EntityDocuments.AnyAsync(ed => ed.ID_ENTITY_DOCUMENT == dto.IdEntityDocument))
                    return BadRequest(new { message = "El tipo de documento no existe" });

                if (!await _db.Menus.AnyAsync(m => m.ID_MENU == dto.IdMenu))
                    return BadRequest(new { message = "El menú no existe" });

                consecutive.CODE = dto.Code;
                consecutive.DESCRIPTION = dto.Description;
                consecutive.ID_ENTITY_TYPE = dto.IdEntityType;
                consecutive.ID_ENTITY_DOCUMENT = dto.IdEntityDocument;
                consecutive.ID_MENU = dto.IdMenu;
                consecutive.MASK = dto.Mask;
                consecutive.LENGTH = dto.Length;
                consecutive.INITIAL_VALUE = dto.InitialValue;
                consecutive.FINAL_VALUE = dto.FinalValue;
                consecutive.IS_ACTIVE = dto.IsActive;
                // UpdatedBy y RecordDate se actualizan por trigger

                await companyDb.SaveChangesAsync();

                var entityType = await _db.EntityTypes.FindAsync(consecutive.ID_ENTITY_TYPE);
                var entityDocument = await _db.EntityDocuments.FindAsync(consecutive.ID_ENTITY_DOCUMENT);
                var menu = await _db.Menus.FindAsync(consecutive.ID_MENU);

                var result = new ConsecutiveDto
                {
                    Id = consecutive.ID_CONSECUTIVE,
                    Code = consecutive.CODE,
                    Description = consecutive.DESCRIPTION,
                    IdEntityType = consecutive.ID_ENTITY_TYPE,
                    IdEntityDocument = consecutive.ID_ENTITY_DOCUMENT,
                    IdMenu = consecutive.ID_MENU,
                    EntityTypeCode = entityType?.CODE ?? string.Empty,
                    EntityTypeName = entityType?.NAME ?? string.Empty,
                    EntityDocumentCode = entityDocument?.CODE ?? string.Empty,
                    EntityDocumentName = entityDocument?.NAME ?? string.Empty,
                    MenuName = menu?.NAME ?? string.Empty,
                    MenuUrl = menu?.URL ?? string.Empty,
                    Mask = consecutive.MASK,
                    Length = consecutive.LENGTH,
                    InitialValue = consecutive.INITIAL_VALUE,
                    FinalValue = consecutive.FINAL_VALUE,
                    LastValue = consecutive.LAST_VALUE,
                    LastUser = consecutive.LAST_USER,
                    LastDate = consecutive.LAST_DATE,
                    IsActive = consecutive.IS_ACTIVE
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando consecutivo {Id}", id);
                return StatusCode(500, new { message = "Error actualizando consecutivo", error = ex.Message });
            }
        }

        // DELETE: api/Consecutive/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                if (companyId == 0)
                    return BadRequest(new { message = "No se pudo determinar la compañía activa" });

                await using var companyDb = await _companyDbContextFactory.CreateDbContextAsync(companyId);

                var consecutive = await companyDb.Consecutives.FindAsync(id);
                if (consecutive == null)
                    return NotFound(new { message = "Consecutivo no encontrado" });

                companyDb.Consecutives.Remove(consecutive);
                await companyDb.SaveChangesAsync();

                return Ok(new { message = "Consecutivo eliminado exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando consecutivo {Id}", id);
                return StatusCode(500, new { message = "Error eliminando consecutivo", error = ex.Message });
            }
        }
    }
}
