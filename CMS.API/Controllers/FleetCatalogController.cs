// ================================================================================
// ARCHIVO: CMS.API/Controllers/FleetCatalogController.cs
// PROPÓSITO: API REST CRUD para los 5 catálogos centrales de Fleet Management
// DESCRIPCIÓN: Gestión de: TipoUnidad, EstadoUnidad, TipoCombustible, Marca y Modelo.
//              Todos los catálogos viven en la BD central (cms, schema admin).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-14
// ================================================================================

using CMS.Data;
using CMS.Entities.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/fleet-catalog")]
    public class FleetCatalogController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<FleetCatalogController> _logger;

        public FleetCatalogController(AppDbContext db, ILogger<FleetCatalogController> logger)
        {
            _db = db;
            _logger = logger;
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? "SYSTEM";

        // ──────────────────────────────────────────────────────────────
        // Desplaza +1 el sort_order de todos los registros >= newOrder,
        // excluyendo el propio registro que se está editando (excludeId).
        // Se llama ANTES de insertar/actualizar para evitar orden duplicado.
        // ──────────────────────────────────────────────────────────────
        private async Task ShiftUnitTypeOrderAsync(int newOrder, int? excludeId = null)
        {
            var q = _db.TransportUnitTypes.Where(x => x.SortOrder >= newOrder);
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
            if (await q.AnyAsync())
                await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + 1));
        }

        private async Task ShiftStatusOrderAsync(int newOrder, int? excludeId = null)
        {
            var q = _db.TransportUnitStatuses.Where(x => x.SortOrder >= newOrder);
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
            if (await q.AnyAsync())
                await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + 1));
        }

        private async Task ShiftFuelOrderAsync(int newOrder, int? excludeId = null)
        {
            var q = _db.FuelTypes.Where(x => x.SortOrder >= newOrder);
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
            if (await q.AnyAsync())
                await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + 1));
        }

        private async Task ShiftBrandOrderAsync(int newOrder, int? excludeId = null)
        {
            var q = _db.TransportUnitBrands.Where(x => x.SortOrder >= newOrder);
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
            if (await q.AnyAsync())
                await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + 1));
        }

        private async Task ShiftModelOrderAsync(int newOrder, int? excludeId = null)
        {
            var q = _db.TransportUnitModels.Where(x => x.SortOrder >= newOrder);
            if (excludeId.HasValue) q = q.Where(x => x.Id != excludeId.Value);
            if (await q.AnyAsync())
                await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder + 1));
        }

        // ============================================================
        // TIPO DE UNIDAD
        // ============================================================

        [HttpGet("unit-types")]
        public async Task<IActionResult> GetUnitTypes([FromQuery] bool? isActive = null)
        {
            var q = _db.TransportUnitTypes.AsQueryable();
            if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
            var list = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
            return Ok(list);
        }

        [HttpGet("unit-types/{id}")]
        public async Task<IActionResult> GetUnitType(int id)
        {
            var item = await _db.TransportUnitTypes.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost("unit-types")]
        public async Task<IActionResult> CreateUnitType([FromBody] TransportUnitTypeCatalog dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (await _db.TransportUnitTypes.AnyAsync(x => x.Code == dto.Code))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe." });

            await ShiftUnitTypeOrderAsync(dto.SortOrder);

            dto.CreatedBy = GetCurrentUser();
            dto.UpdatedBy = GetCurrentUser();
            dto.CreateDate = DateTime.UtcNow;
            dto.RecordDate = DateTime.UtcNow;
            dto.Rowpointer = Guid.NewGuid();

            _db.TransportUnitTypes.Add(dto);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUnitType), new { id = dto.Id }, dto);
        }

        [HttpPut("unit-types/{id}")]
        public async Task<IActionResult> UpdateUnitType(int id, [FromBody] TransportUnitTypeCatalog dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var entity = await _db.TransportUnitTypes.FindAsync(id);
            if (entity == null) return NotFound();
            if (await _db.TransportUnitTypes.AnyAsync(x => x.Code == dto.Code && x.Id != id))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe en otro registro." });

            if (entity.SortOrder != dto.SortOrder)
                await ShiftUnitTypeOrderAsync(dto.SortOrder, excludeId: id);

            entity.Code = dto.Code;
            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.Icon = dto.Icon;
            entity.SortOrder = dto.SortOrder;
            entity.IsActive = dto.IsActive;
            entity.UpdatedBy = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(entity);
        }

        [HttpDelete("unit-types/{id}")]
        public async Task<IActionResult> DeleteUnitType(int id)
        {
            var entity = await _db.TransportUnitTypes.FindAsync(id);
            if (entity == null) return NotFound();
            entity.IsActive = false;
            entity.UpdatedBy = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Tipo de unidad desactivado." });
        }

        // ============================================================
        // ESTADO DE UNIDAD
        // ============================================================

        [HttpGet("unit-statuses")]
        public async Task<IActionResult> GetUnitStatuses([FromQuery] bool? isActive = null)
        {
            var q = _db.TransportUnitStatuses.AsQueryable();
            if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
            var list = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
            return Ok(list);
        }

        [HttpGet("unit-statuses/{id}")]
        public async Task<IActionResult> GetUnitStatus(int id)
        {
            var item = await _db.TransportUnitStatuses.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost("unit-statuses")]
        public async Task<IActionResult> CreateUnitStatus([FromBody] TransportUnitStatusCatalog dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (await _db.TransportUnitStatuses.AnyAsync(x => x.Code == dto.Code))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe." });

            await ShiftStatusOrderAsync(dto.SortOrder);

            dto.CreatedBy = GetCurrentUser();
            dto.UpdatedBy = GetCurrentUser();
            dto.CreateDate = DateTime.UtcNow;
            dto.RecordDate = DateTime.UtcNow;
            dto.Rowpointer = Guid.NewGuid();

            _db.TransportUnitStatuses.Add(dto);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUnitStatus), new { id = dto.Id }, dto);
        }

        [HttpPut("unit-statuses/{id}")]
        public async Task<IActionResult> UpdateUnitStatus(int id, [FromBody] TransportUnitStatusCatalog dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var entity = await _db.TransportUnitStatuses.FindAsync(id);
            if (entity == null) return NotFound();
            if (await _db.TransportUnitStatuses.AnyAsync(x => x.Code == dto.Code && x.Id != id))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe en otro registro." });

            if (entity.SortOrder != dto.SortOrder)
                await ShiftStatusOrderAsync(dto.SortOrder, excludeId: id);

            entity.Code = dto.Code;
            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.BadgeColor = dto.BadgeColor;
            entity.Icon = dto.Icon;
            entity.SortOrder = dto.SortOrder;
            entity.IsActive = dto.IsActive;
            entity.UpdatedBy = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(entity);
        }

        [HttpDelete("unit-statuses/{id}")]
        public async Task<IActionResult> DeleteUnitStatus(int id)
        {
            var entity = await _db.TransportUnitStatuses.FindAsync(id);
            if (entity == null) return NotFound();
            entity.IsActive = false;
            entity.UpdatedBy = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Estado de unidad desactivado." });
        }

        // ============================================================
        // TIPO DE COMBUSTIBLE
        // ============================================================

        [HttpGet("fuel-types")]
        public async Task<IActionResult> GetFuelTypes([FromQuery] bool? isActive = null)
        {
            var q = _db.FuelTypes.AsQueryable();
            if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
            var list = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
            return Ok(list);
        }

        [HttpGet("fuel-types/{id}")]
        public async Task<IActionResult> GetFuelType(int id)
        {
            var item = await _db.FuelTypes.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost("fuel-types")]
        public async Task<IActionResult> CreateFuelType([FromBody] FuelTypeCatalog dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (await _db.FuelTypes.AnyAsync(x => x.Code == dto.Code))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe." });

            await ShiftFuelOrderAsync(dto.SortOrder);

            dto.CreatedBy = GetCurrentUser();
            dto.UpdatedBy = GetCurrentUser();
            dto.CreateDate = DateTime.UtcNow;
            dto.RecordDate = DateTime.UtcNow;
            dto.Rowpointer = Guid.NewGuid();

            _db.FuelTypes.Add(dto);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetFuelType), new { id = dto.Id }, dto);
        }

        [HttpPut("fuel-types/{id}")]
        public async Task<IActionResult> UpdateFuelType(int id, [FromBody] FuelTypeCatalog dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var entity = await _db.FuelTypes.FindAsync(id);
            if (entity == null) return NotFound();
            if (await _db.FuelTypes.AnyAsync(x => x.Code == dto.Code && x.Id != id))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe en otro registro." });

            if (entity.SortOrder != dto.SortOrder)
                await ShiftFuelOrderAsync(dto.SortOrder, excludeId: id);

            entity.Code = dto.Code;
            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.Icon = dto.Icon;
            entity.SortOrder = dto.SortOrder;
            entity.IsActive = dto.IsActive;
            entity.UpdatedBy = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(entity);
        }

        [HttpDelete("fuel-types/{id}")]
        public async Task<IActionResult> DeleteFuelType(int id)
        {
            var entity = await _db.FuelTypes.FindAsync(id);
            if (entity == null) return NotFound();
            entity.IsActive = false;
            entity.UpdatedBy = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Tipo de combustible desactivado." });
        }

        // ============================================================
        // PAÍSES (selector para Marca)
        // ============================================================

        [HttpGet("countries")]
        public async Task<IActionResult> GetCountries()
        {
            var list = await _db.Countries
                .Where(c => c.IS_ACTIVE)
                .OrderBy(c => c.NAME)
                .Select(c => new { id = c.ID_COUNTRY, name = c.NAME, iso2 = c.ISO2_CODE })
                .ToListAsync();
            return Ok(list);
        }

        // ============================================================
        // MARCA
        // ============================================================

        [HttpGet("brands")]
        public async Task<IActionResult> GetBrands([FromQuery] bool? isActive = null)
        {
            var q = _db.TransportUnitBrands.Include(b => b.Country).AsQueryable();
            if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
            var list = await q
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(b => new
                {
                    b.Id, b.Code, b.Name, b.Description,
                    b.IdCountry,
                    countryName = b.Country != null ? b.Country.NAME : null,
                    b.SortOrder, b.IsActive, b.CreatedBy, b.UpdatedBy
                })
                .ToListAsync();
            return Ok(list);
        }

        [HttpGet("brands/{id}")]
        public async Task<IActionResult> GetBrand(int id)
        {
            var item = await _db.TransportUnitBrands.Include(b => b.Country)
                .Where(b => b.Id == id)
                .Select(b => new
                {
                    b.Id, b.Code, b.Name, b.Description,
                    b.IdCountry,
                    countryName = b.Country != null ? b.Country.NAME : null,
                    b.SortOrder, b.IsActive
                })
                .FirstOrDefaultAsync();
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost("brands")]
        public async Task<IActionResult> CreateBrand([FromBody] TransportUnitBrand dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.IdCountry <= 0)
                return BadRequest(new { message = "El país de origen es obligatorio." });
            if (await _db.TransportUnitBrands.AnyAsync(x => x.Code == dto.Code))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe." });
            if (!await _db.Countries.AnyAsync(c => c.ID_COUNTRY == dto.IdCountry))
                return BadRequest(new { message = "El país seleccionado no existe." });

            await ShiftBrandOrderAsync(dto.SortOrder);

            dto.CreatedBy = GetCurrentUser();
            dto.UpdatedBy = GetCurrentUser();
            dto.CreateDate = DateTime.UtcNow;
            dto.RecordDate = DateTime.UtcNow;
            dto.Rowpointer = Guid.NewGuid();

            _db.TransportUnitBrands.Add(dto);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetBrand), new { id = dto.Id }, dto);
        }

        [HttpPut("brands/{id}")]
        public async Task<IActionResult> UpdateBrand(int id, [FromBody] TransportUnitBrand dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.IdCountry <= 0)
                return BadRequest(new { message = "El país de origen es obligatorio." });
            var entity = await _db.TransportUnitBrands.FindAsync(id);
            if (entity == null) return NotFound();
            if (await _db.TransportUnitBrands.AnyAsync(x => x.Code == dto.Code && x.Id != id))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe en otro registro." });
            if (!await _db.Countries.AnyAsync(c => c.ID_COUNTRY == dto.IdCountry))
                return BadRequest(new { message = "El país seleccionado no existe." });

            if (entity.SortOrder != dto.SortOrder)
                await ShiftBrandOrderAsync(dto.SortOrder, excludeId: id);

            entity.Code = dto.Code;
            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.IdCountry = dto.IdCountry;
            entity.SortOrder = dto.SortOrder;
            entity.IsActive = dto.IsActive;
            entity.UpdatedBy = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { entity.Id, entity.Code, entity.Name, entity.IdCountry, entity.SortOrder, entity.IsActive });
        }

        [HttpDelete("brands/{id}")]
        public async Task<IActionResult> DeleteBrand(int id)
        {
            var entity = await _db.TransportUnitBrands.FindAsync(id);
            if (entity == null) return NotFound();
            entity.IsActive = false;
            entity.UpdatedBy = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Marca desactivada." });
        }

        // ============================================================
        // MODELO
        // ============================================================

        [HttpGet("models")]
        public async Task<IActionResult> GetModels([FromQuery] int? brandId = null, [FromQuery] bool? isActive = null)
        {
            var q = _db.TransportUnitModels.Include(m => m.Brand).Include(m => m.TransportUnitType).AsQueryable();
            if (brandId.HasValue) q = q.Where(x => x.IdTransportUnitBrand == brandId.Value);
            if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);
            var list = await q
                .OrderBy(x => x.IdTransportUnitBrand).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
                .Select(m => new
                {
                    m.Id, m.Code, m.Name, m.Description,
                    idVehicleBrand = m.IdTransportUnitBrand,   // alias para compatibilidad con el JS existente
                    brandName = m.Brand != null ? m.Brand.Name : null,
                    m.IdTransportUnitType,
                    unitTypeName = m.TransportUnitType != null ? m.TransportUnitType.Name : null,
                    m.SortOrder, m.IsActive
                })
                .ToListAsync();
            return Ok(list);
        }

        [HttpGet("models/{id}")]
        public async Task<IActionResult> GetModel(int id)
        {
            var item = await _db.TransportUnitModels.Include(m => m.Brand).Include(m => m.TransportUnitType)
                .Where(m => m.Id == id)
                .Select(m => new
                {
                    m.Id, m.Code, m.Name, m.Description,
                    idVehicleBrand = m.IdTransportUnitBrand,   // alias para compatibilidad con el JS existente
                    brandName = m.Brand != null ? m.Brand.Name : null,
                    m.IdTransportUnitType,
                    unitTypeName = m.TransportUnitType != null ? m.TransportUnitType.Name : null,
                    m.SortOrder, m.IsActive
                })
                .FirstOrDefaultAsync();
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost("models")]
        public async Task<IActionResult> CreateModel([FromBody] TransportUnitModel dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (await _db.TransportUnitModels.AnyAsync(x => x.Code == dto.Code))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe." });
            if (!await _db.TransportUnitBrands.AnyAsync(x => x.Id == dto.IdTransportUnitBrand))
                return BadRequest(new { message = "La marca especificada no existe." });

            await ShiftModelOrderAsync(dto.SortOrder);

            dto.CreatedBy = GetCurrentUser();
            dto.UpdatedBy = GetCurrentUser();
            dto.CreateDate = DateTime.UtcNow;
            dto.RecordDate = DateTime.UtcNow;
            dto.Rowpointer = Guid.NewGuid();

            _db.TransportUnitModels.Add(dto);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetModel), new { id = dto.Id }, dto);
        }

        [HttpPut("models/{id}")]
        public async Task<IActionResult> UpdateModel(int id, [FromBody] TransportUnitModel dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var entity = await _db.TransportUnitModels.FindAsync(id);
            if (entity == null) return NotFound();
            if (await _db.TransportUnitModels.AnyAsync(x => x.Code == dto.Code && x.Id != id))
                return Conflict(new { message = $"El código '{dto.Code}' ya existe en otro registro." });

            if (entity.SortOrder != dto.SortOrder)
                await ShiftModelOrderAsync(dto.SortOrder, excludeId: id);

            entity.IdTransportUnitBrand = dto.IdTransportUnitBrand;
            entity.Code = dto.Code;
            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.IdTransportUnitType = dto.IdTransportUnitType;
            entity.SortOrder = dto.SortOrder;
            entity.IsActive = dto.IsActive;
            entity.UpdatedBy = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(entity);
        }

        [HttpDelete("models/{id}")]
        public async Task<IActionResult> DeleteModel(int id)
        {
            var entity = await _db.TransportUnitModels.FindAsync(id);
            if (entity == null) return NotFound();
            entity.IsActive = false;
            entity.UpdatedBy = GetCurrentUser();
            entity.RecordDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Modelo desactivado." });
        }
    }
}
