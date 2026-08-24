// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicDocumentEconomicActivityController.cs
// PROPÓSITO: API REST CRUD + importación masiva para el catálogo CENTRAL
//            admin.electronic_document_economic_activity (actividades económicas Hacienda CR).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Data;
using CMS.Entities.EInvoice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/electronicdocumenteconomicactivity")]
    public class ElectronicDocumentEconomicActivityController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ElectronicDocumentEconomicActivityController(AppDbContext db)
        {
            _db = db;
        }

        public class EconomicActivityDto
        {
            public string Code { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public bool IsActive { get; set; } = true;
        }

        public class ImportResultDto
        {
            public int Inserted { get; set; }
            public int Updated { get; set; }
            public int Skipped { get; set; }
            public int Total { get; set; }
        }

        // GET /api/electronicdocumenteconomicactivity?search=&onlyActive=&page=&pageSize=
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] bool onlyActive = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 500) pageSize = 500;

            var query = _db.ElectronicDocumentEconomicActivities.AsNoTracking().AsQueryable();

            if (onlyActive)
                query = query.Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Description.ToLower().Contains(s));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(a => a.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        // GET /api/electronicdocumenteconomicactivity/active  → lista simple activa (para selectores)
        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] string? search, CancellationToken ct = default)
        {
            var query = _db.ElectronicDocumentEconomicActivities.AsNoTracking().Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.Code.ToLower().Contains(s) || a.Description.ToLower().Contains(s));
            }

            var items = await query
                .OrderBy(a => a.Code)
                .Take(1000)
                .Select(a => new { a.Id, a.Code, a.Description })
                .ToListAsync(ct);

            return Ok(items);
        }

        // GET /api/electronicdocumenteconomicactivity/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var item = await _db.ElectronicDocumentEconomicActivities.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (item == null)
                return NotFound(new { message = $"No existe la actividad económica con id {id}." });
            return Ok(item);
        }

        // POST /api/electronicdocumenteconomicactivity
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EconomicActivityDto input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
                return BadRequest(new { message = "El código de actividad económica es obligatorio." });
            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var code = input.Code.Trim();
            var exists = await _db.ElectronicDocumentEconomicActivities.AnyAsync(a => a.Code == code, ct);
            if (exists)
                return Conflict(new { message = $"Ya existe una actividad económica con el código '{code}'." });

            var entity = new ElectronicDocumentEconomicActivity
            {
                Code = code,
                Description = input.Description.Trim(),
                IsActive = input.IsActive
            };

            _db.ElectronicDocumentEconomicActivities.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // PUT /api/electronicdocumenteconomicactivity/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] EconomicActivityDto input, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentEconomicActivities.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe la actividad económica con id {id}." });

            if (string.IsNullOrWhiteSpace(input.Description))
                return BadRequest(new { message = "La descripción es obligatoria." });

            var newCode = (input.Code ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(newCode) && newCode != entity.Code)
            {
                var dup = await _db.ElectronicDocumentEconomicActivities.AnyAsync(a => a.Code == newCode && a.Id != id, ct);
                if (dup)
                    return Conflict(new { message = $"Ya existe una actividad económica con el código '{newCode}'." });
                entity.Code = newCode;
            }

            entity.Description = input.Description.Trim();
            entity.IsActive = input.IsActive;

            await _db.SaveChangesAsync(ct);
            return Ok(entity);
        }

        // DELETE /api/electronicdocumenteconomicactivity/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _db.ElectronicDocumentEconomicActivities.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity == null)
                return NotFound(new { message = $"No existe la actividad económica con id {id}." });

            _db.ElectronicDocumentEconomicActivities.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(new { message = "Actividad económica eliminada." });
        }

        // POST /api/electronicdocumenteconomicactivity/import
        // Importación masiva del catálogo (upsert por 'code'). Acepta JSON con la lista completa.
        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] List<EconomicActivityDto> items, CancellationToken ct)
        {
            if (items == null || items.Count == 0)
                return BadRequest(new { message = "No se recibieron actividades económicas para importar." });

            var result = new ImportResultDto { Total = items.Count };

            // Índice actual por código para hacer upsert eficiente.
            var existing = await _db.ElectronicDocumentEconomicActivities
                .ToDictionaryAsync(a => a.Code, a => a, ct);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dto in items)
            {
                var code = (dto.Code ?? string.Empty).Trim();
                var desc = (dto.Description ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(desc) || !seen.Add(code))
                {
                    result.Skipped++;
                    continue;
                }

                if (existing.TryGetValue(code, out var entity))
                {
                    entity.Description = desc;
                    entity.IsActive = dto.IsActive;
                    result.Updated++;
                }
                else
                {
                    _db.ElectronicDocumentEconomicActivities.Add(new ElectronicDocumentEconomicActivity
                    {
                        Code = code,
                        Description = desc,
                        IsActive = dto.IsActive
                    });
                    result.Inserted++;
                }
            }

            await _db.SaveChangesAsync(ct);
            return Ok(result);
        }
    }
}
