using CMS.Application.DTOs;
using CMS.Data;
using CMS.Entities.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class EntityTypeController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<EntityTypeController> _logger;

        public EntityTypeController(AppDbContext db, ILogger<EntityTypeController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // GET: api/EntityType - Lista completa
        [HttpGet]
        public async Task<ActionResult<List<EntityTypeDto>>> GetAll()
        {
            try
            {
                var entityTypes = await _db.EntityTypes
                    .OrderBy(et => et.SORT_ORDER)
                    .ThenBy(et => et.NAME)
                    .Select(et => new EntityTypeDto
                    {
                        Id = et.ID_ENTITY_TYPE,
                        Code = et.CODE,
                        Name = et.NAME,
                        Description = et.DESCRIPTION,
                        IsActive = et.IS_ACTIVE,
                        SortOrder = et.SORT_ORDER
                    })
                    .ToListAsync();

                return Ok(entityTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo tipos de entidad");
                return StatusCode(500, new { message = "Error obteniendo tipos de entidad", error = ex.Message });
            }
        }

        // GET: api/EntityType/active - Solo activos
        [HttpGet("active")]
        public async Task<ActionResult<List<EntityTypeDto>>> GetActive()
        {
            try
            {
                var entityTypes = await _db.EntityTypes
                    .Where(et => et.IS_ACTIVE)
                    .OrderBy(et => et.SORT_ORDER)
                    .ThenBy(et => et.NAME)
                    .Select(et => new EntityTypeDto
                    {
                        Id = et.ID_ENTITY_TYPE,
                        Code = et.CODE,
                        Name = et.NAME,
                        Description = et.DESCRIPTION,
                        IsActive = et.IS_ACTIVE,
                        SortOrder = et.SORT_ORDER
                    })
                    .ToListAsync();

                return Ok(entityTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo tipos de entidad activos");
                return StatusCode(500, new { message = "Error obteniendo tipos de entidad activos", error = ex.Message });
            }
        }

        // GET: api/EntityType/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<EntityTypeDto>> GetById(int id)
        {
            try
            {
                var entityType = await _db.EntityTypes.FindAsync(id);
                if (entityType == null)
                    return NotFound(new { message = "Tipo de entidad no encontrado" });

                var dto = new EntityTypeDto
                {
                    Id = entityType.ID_ENTITY_TYPE,
                    Code = entityType.CODE,
                    Name = entityType.NAME,
                    Description = entityType.DESCRIPTION,
                    IsActive = entityType.IS_ACTIVE,
                    SortOrder = entityType.SORT_ORDER
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo tipo de entidad {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo tipo de entidad", error = ex.Message });
            }
        }

        // POST: api/EntityType
        [HttpPost]
        public async Task<ActionResult<EntityTypeDto>> Create([FromBody] EntityTypeCreateDto dto)
        {
            try
            {
                // Validar código único
                if (await _db.EntityTypes.AnyAsync(et => et.CODE == dto.Code))
                    return BadRequest(new { message = "El código ya existe" });

                var entityType = new EntityType
                {
                    CODE = dto.Code,
                    NAME = dto.Name,
                    DESCRIPTION = dto.Description,
                    IS_ACTIVE = dto.IsActive,
                    SORT_ORDER = dto.SortOrder,
                    CreateDate = DateTime.UtcNow,
                    RecordDate = DateTime.UtcNow,
                    CreatedBy = User.FindFirstValue("cms_username") ?? "SYSTEM",
                    UpdatedBy = User.FindFirstValue("cms_username") ?? "SYSTEM",
                    RowPointer = Guid.NewGuid()
                };

                _db.EntityTypes.Add(entityType);
                await _db.SaveChangesAsync();

                var result = new EntityTypeDto
                {
                    Id = entityType.ID_ENTITY_TYPE,
                    Code = entityType.CODE,
                    Name = entityType.NAME,
                    Description = entityType.DESCRIPTION,
                    IsActive = entityType.IS_ACTIVE,
                    SortOrder = entityType.SORT_ORDER
                };

                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando tipo de entidad");
                return StatusCode(500, new { message = "Error creando tipo de entidad", error = ex.Message });
            }
        }

        // PUT: api/EntityType/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<EntityTypeDto>> Update(int id, [FromBody] EntityTypeUpdateDto dto)
        {
            try
            {
                var entityType = await _db.EntityTypes.FindAsync(id);
                if (entityType == null)
                    return NotFound(new { message = "Tipo de entidad no encontrado" });

                // Validar código único (excluyendo el actual)
                if (await _db.EntityTypes.AnyAsync(et => et.CODE == dto.Code && et.ID_ENTITY_TYPE != id))
                    return BadRequest(new { message = "El código ya existe" });

                entityType.CODE = dto.Code;
                entityType.NAME = dto.Name;
                entityType.DESCRIPTION = dto.Description;
                entityType.IS_ACTIVE = dto.IsActive;
                entityType.SORT_ORDER = dto.SortOrder;
                // UpdatedBy y RecordDate se actualizan por trigger

                await _db.SaveChangesAsync();

                var result = new EntityTypeDto
                {
                    Id = entityType.ID_ENTITY_TYPE,
                    Code = entityType.CODE,
                    Name = entityType.NAME,
                    Description = entityType.DESCRIPTION,
                    IsActive = entityType.IS_ACTIVE,
                    SortOrder = entityType.SORT_ORDER
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando tipo de entidad {Id}", id);
                return StatusCode(500, new { message = "Error actualizando tipo de entidad", error = ex.Message });
            }
        }

        // DELETE: api/EntityType/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var entityType = await _db.EntityTypes.FindAsync(id);
                if (entityType == null)
                    return NotFound(new { message = "Tipo de entidad no encontrado" });

                // Verificar si tiene documentos asociados
                if (await _db.EntityDocuments.AnyAsync(ed => ed.ID_ENTITY_TYPE == id))
                    return BadRequest(new { message = "No se puede eliminar el tipo porque tiene documentos asociados" });

                _db.EntityTypes.Remove(entityType);
                await _db.SaveChangesAsync();

                return Ok(new { message = "Tipo de entidad eliminado exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando tipo de entidad {Id}", id);
                return StatusCode(500, new { message = "Error eliminando tipo de entidad", error = ex.Message });
            }
        }
    }
}
