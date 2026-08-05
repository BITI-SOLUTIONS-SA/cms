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
    public class EntityDocumentController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<EntityDocumentController> _logger;

        public EntityDocumentController(AppDbContext db, ILogger<EntityDocumentController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // GET: api/EntityDocument - Lista completa con entity type
        [HttpGet]
        public async Task<ActionResult<List<EntityDocumentDto>>> GetAll()
        {
            try
            {
                var documents = await _db.EntityDocuments
                    .Include(ed => ed.EntityType)
                    .OrderBy(ed => ed.SORT_ORDER)
                    .ThenBy(ed => ed.NAME)
                    .Select(ed => new EntityDocumentDto
                    {
                        Id = ed.ID_ENTITY_DOCUMENT,
                        IdEntityType = ed.ID_ENTITY_TYPE,
                        EntityTypeCode = ed.EntityType != null ? ed.EntityType.CODE : string.Empty,
                        EntityTypeName = ed.EntityType != null ? ed.EntityType.NAME : string.Empty,
                        Code = ed.CODE,
                        Name = ed.NAME,
                        Description = ed.DESCRIPTION,
                        IsActive = ed.IS_ACTIVE,
                        SortOrder = ed.SORT_ORDER
                    })
                    .ToListAsync();

                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo tipos de documento");
                return StatusCode(500, new { message = "Error obteniendo tipos de documento", error = ex.Message });
            }
        }

        // GET: api/EntityDocument/active - Solo activos
        [HttpGet("active")]
        public async Task<ActionResult<List<EntityDocumentDto>>> GetActive()
        {
            try
            {
                var documents = await _db.EntityDocuments
                    .Include(ed => ed.EntityType)
                    .Where(ed => ed.IS_ACTIVE)
                    .OrderBy(ed => ed.SORT_ORDER)
                    .ThenBy(ed => ed.NAME)
                    .Select(ed => new EntityDocumentDto
                    {
                        Id = ed.ID_ENTITY_DOCUMENT,
                        IdEntityType = ed.ID_ENTITY_TYPE,
                        EntityTypeCode = ed.EntityType != null ? ed.EntityType.CODE : string.Empty,
                        EntityTypeName = ed.EntityType != null ? ed.EntityType.NAME : string.Empty,
                        Code = ed.CODE,
                        Name = ed.NAME,
                        Description = ed.DESCRIPTION,
                        IsActive = ed.IS_ACTIVE,
                        SortOrder = ed.SORT_ORDER
                    })
                    .ToListAsync();

                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo tipos de documento activos");
                return StatusCode(500, new { message = "Error obteniendo tipos de documento activos", error = ex.Message });
            }
        }

        // GET: api/EntityDocument/bytype/{entityTypeId} - Por tipo de entidad
        [HttpGet("bytype/{entityTypeId}")]
        public async Task<ActionResult<List<EntityDocumentDto>>> GetByEntityType(int entityTypeId)
        {
            try
            {
                var documents = await _db.EntityDocuments
                    .Include(ed => ed.EntityType)
                    .Where(ed => ed.ID_ENTITY_TYPE == entityTypeId && ed.IS_ACTIVE)
                    .OrderBy(ed => ed.SORT_ORDER)
                    .ThenBy(ed => ed.NAME)
                    .Select(ed => new EntityDocumentDto
                    {
                        Id = ed.ID_ENTITY_DOCUMENT,
                        IdEntityType = ed.ID_ENTITY_TYPE,
                        EntityTypeCode = ed.EntityType != null ? ed.EntityType.CODE : string.Empty,
                        EntityTypeName = ed.EntityType != null ? ed.EntityType.NAME : string.Empty,
                        Code = ed.CODE,
                        Name = ed.NAME,
                        Description = ed.DESCRIPTION,
                        IsActive = ed.IS_ACTIVE,
                        SortOrder = ed.SORT_ORDER
                    })
                    .ToListAsync();

                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo documentos por tipo {EntityTypeId}", entityTypeId);
                return StatusCode(500, new { message = "Error obteniendo documentos por tipo", error = ex.Message });
            }
        }

        // GET: api/EntityDocument/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<EntityDocumentDto>> GetById(int id)
        {
            try
            {
                var document = await _db.EntityDocuments
                    .Include(ed => ed.EntityType)
                    .FirstOrDefaultAsync(ed => ed.ID_ENTITY_DOCUMENT == id);

                if (document == null)
                    return NotFound(new { message = "Tipo de documento no encontrado" });

                var dto = new EntityDocumentDto
                {
                    Id = document.ID_ENTITY_DOCUMENT,
                    IdEntityType = document.ID_ENTITY_TYPE,
                    EntityTypeCode = document.EntityType != null ? document.EntityType.CODE : string.Empty,
                    EntityTypeName = document.EntityType != null ? document.EntityType.NAME : string.Empty,
                    Code = document.CODE,
                    Name = document.NAME,
                    Description = document.DESCRIPTION,
                    IsActive = document.IS_ACTIVE,
                    SortOrder = document.SORT_ORDER
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo tipo de documento {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo tipo de documento", error = ex.Message });
            }
        }

        // POST: api/EntityDocument
        [HttpPost]
        public async Task<ActionResult<EntityDocumentDto>> Create([FromBody] EntityDocumentCreateDto dto)
        {
            try
            {
                // Validar código único
                if (await _db.EntityDocuments.AnyAsync(ed => ed.CODE == dto.Code))
                    return BadRequest(new { message = "El código ya existe" });

                // Validar que existe el entity type
                if (!await _db.EntityTypes.AnyAsync(et => et.ID_ENTITY_TYPE == dto.IdEntityType))
                    return BadRequest(new { message = "El tipo de entidad no existe" });

                var document = new EntityDocument
                {
                    ID_ENTITY_TYPE = dto.IdEntityType,
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

                _db.EntityDocuments.Add(document);
                await _db.SaveChangesAsync();

                // Recargar con entity type para el DTO
                await _db.Entry(document).Reference(d => d.EntityType).LoadAsync();

                var result = new EntityDocumentDto
                {
                    Id = document.ID_ENTITY_DOCUMENT,
                    IdEntityType = document.ID_ENTITY_TYPE,
                    EntityTypeCode = document.EntityType?.CODE ?? string.Empty,
                    EntityTypeName = document.EntityType?.NAME ?? string.Empty,
                    Code = document.CODE,
                    Name = document.NAME,
                    Description = document.DESCRIPTION,
                    IsActive = document.IS_ACTIVE,
                    SortOrder = document.SORT_ORDER
                };

                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando tipo de documento");
                return StatusCode(500, new { message = "Error creando tipo de documento", error = ex.Message });
            }
        }

        // PUT: api/EntityDocument/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<EntityDocumentDto>> Update(int id, [FromBody] EntityDocumentUpdateDto dto)
        {
            try
            {
                var document = await _db.EntityDocuments
                    .Include(ed => ed.EntityType)
                    .FirstOrDefaultAsync(ed => ed.ID_ENTITY_DOCUMENT == id);

                if (document == null)
                    return NotFound(new { message = "Tipo de documento no encontrado" });

                // Validar código único (excluyendo el actual)
                if (await _db.EntityDocuments.AnyAsync(ed => ed.CODE == dto.Code && ed.ID_ENTITY_DOCUMENT != id))
                    return BadRequest(new { message = "El código ya existe" });

                // Validar que existe el entity type
                if (!await _db.EntityTypes.AnyAsync(et => et.ID_ENTITY_TYPE == dto.IdEntityType))
                    return BadRequest(new { message = "El tipo de entidad no existe" });

                document.ID_ENTITY_TYPE = dto.IdEntityType;
                document.CODE = dto.Code;
                document.NAME = dto.Name;
                document.DESCRIPTION = dto.Description;
                document.IS_ACTIVE = dto.IsActive;
                document.SORT_ORDER = dto.SortOrder;
                // UpdatedBy y RecordDate se actualizan por trigger

                await _db.SaveChangesAsync();

                var result = new EntityDocumentDto
                {
                    Id = document.ID_ENTITY_DOCUMENT,
                    IdEntityType = document.ID_ENTITY_TYPE,
                    EntityTypeCode = document.EntityType?.CODE ?? string.Empty,
                    EntityTypeName = document.EntityType?.NAME ?? string.Empty,
                    Code = document.CODE,
                    Name = document.NAME,
                    Description = document.DESCRIPTION,
                    IsActive = document.IS_ACTIVE,
                    SortOrder = document.SORT_ORDER
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando tipo de documento {Id}", id);
                return StatusCode(500, new { message = "Error actualizando tipo de documento", error = ex.Message });
            }
        }

        // DELETE: api/EntityDocument/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var document = await _db.EntityDocuments.FindAsync(id);
                if (document == null)
                    return NotFound(new { message = "Tipo de documento no encontrado" });

                _db.EntityDocuments.Remove(document);
                await _db.SaveChangesAsync();

                return Ok(new { message = "Tipo de documento eliminado exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando tipo de documento {Id}", id);
                return StatusCode(500, new { message = "Error eliminando tipo de documento", error = ex.Message });
            }
        }
    }
}
