// ================================================================================
// ARCHIVO: CMS.API/Controllers/WarehouseController.cs
// PROPÓSITO: API REST para gestión de bodegas (WMS - Warehouse Management System)
// DESCRIPCIÓN: CRUD de bodegas físicas y lógicas usando la BD de la compañía activa
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-03
// ================================================================================

using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class WarehouseController : ControllerBase
    {
        private readonly IWarehouseService _warehouseService;
        private readonly ILocationService _locationService;
        private readonly ILogger<WarehouseController> _logger;

        public WarehouseController(
            IWarehouseService warehouseService,
            ILocationService locationService,
            ILogger<WarehouseController> logger)
        {
            _warehouseService = warehouseService;
            _locationService  = locationService;
            _logger = logger;
        }

        private int GetCurrentCompanyId()
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(companyIdClaim, out var companyId)) return companyId;
            throw new UnauthorizedAccessException("companyId no encontrado en el token JWT");
        }

        private string GetCurrentUser()
        {
            return User.FindFirstValue("cms_username") ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? "SYSTEM";
        }

        // ============================================================
        // GET /api/warehouse
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetWarehouses(
            [FromQuery] string? search = null,
            [FromQuery] string? warehouseType = null,
            [FromQuery] int? warehouseLevel = null,
            [FromQuery] int? parentId = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var (items, total) = await _warehouseService.GetWarehousesAsync(
                    companyId, search, warehouseType, warehouseLevel, parentId, isActive, page, pageSize);

                return Ok(new
                {
                    items = items.Select(MapToDto),
                    totalCount = total,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting warehouses");
                return StatusCode(500, new { error = "Error al obtener bodegas", detail = ex.Message });
            }
        }

        // ============================================================
        // GET /api/warehouse/tree
        // ============================================================
        [HttpGet("tree")]
        public async Task<IActionResult> GetTree([FromQuery] bool activeOnly = true)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var tree = await _warehouseService.GetWarehouseTreeAsync(companyId, activeOnly);
                return Ok(tree.Select(MapToTreeDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting warehouse tree");
                return StatusCode(500, new { error = "Error al obtener árbol de bodegas" });
            }
        }

        // ============================================================
        // GET /api/warehouse/{id}
        // ============================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var warehouse = await _warehouseService.GetByIdAsync(companyId, id);
                if (warehouse == null) return NotFound(new { error = "Bodega no encontrada" });

                var stats = await _warehouseService.GetStatsAsync(companyId, id);
                var dto = MapToDto(warehouse);
                dto.Stats = stats;
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting warehouse {Id}", id);
                return StatusCode(500, new { error = "Error al obtener bodega" });
            }
        }

        // ============================================================
        // POST /api/warehouse
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] WarehouseUpsertDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);

                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();

                if (await _warehouseService.CodeExistsAsync(companyId, dto.Code))
                    return Conflict(new { error = $"El código '{dto.Code}' ya existe" });

                if (!await _warehouseService.ValidateResponsibleUserAsync(dto.ResponsibleUserId))
                    return BadRequest(new { error = $"El usuario responsable con ID {dto.ResponsibleUserId} no existe." });

                var warehouse = MapFromDto(dto);
                var created = await _warehouseService.CreateAsync(companyId, warehouse, user);

                // Vincular localización con esta bodega
                if (created.IdLocation.HasValue)
                    await _locationService.SetLocationCatalogAsync(companyId, created.IdLocation.Value, created.Id, user);

                return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating warehouse");
                return StatusCode(500, new { error = "Error al crear bodega", detail = ex.Message });
            }
        }

        // ============================================================
        // PUT /api/warehouse/{id}
        // ============================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] WarehouseUpsertDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);

                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();

                var existing = await _warehouseService.GetByIdAsync(companyId, id);
                if (existing == null) return NotFound(new { error = "Bodega no encontrada" });

                if (await _warehouseService.CodeExistsAsync(companyId, dto.Code, excludeId: id))
                    return Conflict(new { error = $"El código '{dto.Code}' ya existe" });

                if (!await _warehouseService.ValidateResponsibleUserAsync(dto.ResponsibleUserId))
                    return BadRequest(new { error = $"El usuario responsable con ID {dto.ResponsibleUserId} no existe." });

                var oldLocationId = existing.IdLocation;

                // Prevenir ciclos jerárquicos
                if (dto.IdParentWarehouse.HasValue && dto.IdParentWarehouse.Value == id)
                    return BadRequest(new { error = "Una bodega no puede ser su propio padre" });

                var warehouse = MapFromDto(dto);
                warehouse.Id = id;
                var updated = await _warehouseService.UpdateAsync(companyId, warehouse, user);

                // Gestionar vinculación de localización
                if (oldLocationId != dto.IdLocation)
                {
                    if (oldLocationId.HasValue)
                        await _locationService.ClearLocationCatalogAsync(companyId, oldLocationId.Value, user);
                    if (dto.IdLocation.HasValue)
                        await _locationService.SetLocationCatalogAsync(companyId, dto.IdLocation.Value, id, user);
                }

                return Ok(MapToDto(updated));
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "Bodega no encontrada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating warehouse {Id}", id);
                return StatusCode(500, new { error = "Error al actualizar bodega", detail = ex.Message });
            }
        }

        // ============================================================
        // PATCH /api/warehouse/{id}/deactivate
        // ============================================================
        [HttpPatch("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var result = await _warehouseService.DeactivateAsync(companyId, id, user);
                if (!result) return NotFound(new { error = "Bodega no encontrada" });
                return Ok(new { message = "Bodega desactivada exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating warehouse {Id}", id);
                return StatusCode(500, new { error = "Error al desactivar bodega" });
            }
        }

        // ============================================================
        // PATCH /api/warehouse/{id}/activate
        // ============================================================
        [HttpPatch("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var result = await _warehouseService.ActivateAsync(companyId, id, user);
                if (!result) return NotFound(new { error = "Bodega no encontrada" });
                return Ok(new { message = "Bodega activada exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating warehouse {Id}", id);
                return StatusCode(500, new { error = "Error al activar bodega" });
            }
        }

        // ============================================================
        // GET /api/warehouse/check-code?code=xxx[&excludeId=1]
        // ============================================================
        [HttpGet("check-code")]
        public async Task<IActionResult> CheckCode([FromQuery] string code, [FromQuery] int? excludeId = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var exists = await _warehouseService.CodeExistsAsync(companyId, code, excludeId);
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // MAPPERS
        // ============================================================

        private static WarehouseDto MapToDto(Warehouse w) => new()
        {
            Id = w.Id,
            Code = w.Code,
            Name = w.Name,
            Description = w.Description,
            WarehouseType = w.WarehouseType,
            WarehouseLevel = w.WarehouseLevel,
            IdParentWarehouse = w.IdParentWarehouse,
            IsDefault = w.IsDefault,
            AllowsNegativeStock = w.AllowsNegativeStock,
            RequiresLocation = w.RequiresLocation,
            RequiresLotTracking = w.RequiresLotTracking,
            RequiresExpiryDate = w.RequiresExpiryDate,
            IsManaged = w.IsManaged,
            MaxCapacity = w.MaxCapacity,
            CapacityUnit = w.CapacityUnit,
            IdLocation = w.IdLocation,
            LocationAddress = w.LocationAddress,
            LocationCity = w.LocationCity,
            LocationCountryCode = w.LocationCountryCode,
            LocationGpsLatitude = w.LocationGpsLatitude,
            LocationGpsLongitude = w.LocationGpsLongitude,
            ResponsibleUserId = w.ResponsibleUserId,
            ResponsibleName  = w.ResponsibleName,   // resuelto cross-DB [NotMapped]
            ResponsibleEmail = w.ResponsibleEmail,  // resuelto cross-DB [NotMapped]
            ResponsiblePhone = w.ResponsiblePhone,  // resuelto cross-DB [NotMapped]
            Notes = w.Notes,
            IsActive = w.IsActive,
            IdTransportUnit  = w.IdTransportUnit,
            TransportUnitName = w.TransportUnitName,
            CreatedAt = w.CreatedAt,
            CreatedBy = w.CreatedBy,
            UpdatedAt = w.UpdatedAt,
            UpdatedBy = w.UpdatedBy
        };

        private static WarehouseTreeDto MapToTreeDto(Warehouse w) => new()
        {
            Id = w.Id,
            Code = w.Code,
            Name = w.Name,
            WarehouseType = w.WarehouseType,
            WarehouseLevel = w.WarehouseLevel,
            IsActive = w.IsActive,
            IsDefault = w.IsDefault,
            IsManaged = w.IsManaged,
            Children = w.Children.Select(MapToTreeDto).ToList()
        };

        private static Warehouse MapFromDto(WarehouseUpsertDto dto) => new()
        {
            Code = dto.Code.Trim().ToUpper(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            WarehouseType = dto.WarehouseType,
            WarehouseLevel = dto.WarehouseLevel,
            IdParentWarehouse = dto.IdParentWarehouse,
            IsDefault = dto.IsDefault,
            AllowsNegativeStock = dto.AllowsNegativeStock,
            RequiresLocation = dto.RequiresLocation,
            RequiresLotTracking = dto.RequiresLotTracking,
            RequiresExpiryDate = dto.RequiresExpiryDate,
            IsManaged = dto.IsManaged,
            MaxCapacity = dto.MaxCapacity,
            CapacityUnit = dto.CapacityUnit,
            IdLocation = dto.IdLocation,
            ResponsibleUserId = dto.ResponsibleUserId,
            Notes = dto.Notes?.Trim(),
            IsActive = dto.IsActive,
            IdTransportUnit = dto.IdTransportUnit,
        };
    }

    // ============================================================
    // DTOs
    // ============================================================

    public class WarehouseDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string WarehouseType { get; set; } = string.Empty;
        public int WarehouseLevel { get; set; }
        public int? IdParentWarehouse { get; set; }
        public bool IsDefault { get; set; }
        public bool AllowsNegativeStock { get; set; }
        public bool RequiresLocation { get; set; }
        public bool RequiresLotTracking { get; set; }
        public bool RequiresExpiryDate { get; set; }
        public bool IsManaged { get; set; }
        public decimal? MaxCapacity { get; set; }
        public string? CapacityUnit { get; set; }
        /// <summary>FK a location.id_location en BD de compañía</summary>
        public int? IdLocation { get; set; }
        /// <summary>Resuelto desde location (address)</summary>
        public string? LocationAddress { get; set; }
        /// <summary>Resuelto desde location (city)</summary>
        public string? LocationCity { get; set; }
        /// <summary>Resuelto desde location (country_code)</summary>
        public string? LocationCountryCode { get; set; }
        /// <summary>Resuelto desde location (gps_latitude)</summary>
        public decimal? LocationGpsLatitude { get; set; }
        /// <summary>Resuelto desde location (gps_longitude)</summary>
        public decimal? LocationGpsLongitude { get; set; }
        /// <summary>FK lógica a cms.admin.user.id_user</summary>
        public int? ResponsibleUserId { get; set; }
        /// <summary>Resuelto desde cms.admin.user (DISPLAY_NAME)</summary>
        public string? ResponsibleName { get; set; }
        /// <summary>Resuelto desde cms.admin.user (EMAIL)</summary>
        public string? ResponsibleEmail { get; set; }
        /// <summary>Resuelto desde cms.admin.user (PHONE_NUMBER)</summary>
        public string? ResponsiblePhone { get; set; }
        public string? Notes { get; set; }
        /// <summary>FK a sinai.transport_unit. Solo aplica cuando WarehouseType = 'Transit'.</summary>
        public int? IdTransportUnit { get; set; }
        /// <summary>Nombre/placa de la unidad de transporte (resuelto en la respuesta).</summary>
        public string? TransportUnitName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public WarehouseStats? Stats { get; set; }
    }

    public class WarehouseTreeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string WarehouseType { get; set; } = string.Empty;
        public int WarehouseLevel { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public bool IsManaged { get; set; }
        public List<WarehouseTreeDto> Children { get; set; } = new();
    }

    public class WarehouseUpsertDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(30)]
        public string Code { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.MaxLength(1000)]
        public string? Description { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        public string WarehouseType { get; set; } = "Physical";

        public int WarehouseLevel { get; set; } = 0;
        public int? IdParentWarehouse { get; set; }
        public bool IsDefault { get; set; } = false;
        public bool AllowsNegativeStock { get; set; } = false;
        public bool RequiresLocation { get; set; } = false;
        public bool RequiresLotTracking { get; set; } = false;
        public bool RequiresExpiryDate { get; set; } = false;
        public bool IsManaged { get; set; } = false;
        public decimal? MaxCapacity { get; set; }
        public string? CapacityUnit { get; set; }
        /// <summary>FK a location.id_location en BD de compañía</summary>
        public int? IdLocation { get; set; }
        /// <summary>FK lógica a cms.admin.user.id_user. Validado cross-DB antes de guardar.</summary>
        public int? ResponsibleUserId { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>FK a sinai.transport_unit. Obligatorio cuando WarehouseType = 'Transit'.</summary>
        public int? IdTransportUnit { get; set; }
    }
}
