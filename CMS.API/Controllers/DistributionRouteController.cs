// ================================================================================
// ARCHIVO: CMS.API/Controllers/DistributionRouteController.cs
// PROPÓSITO: API REST para rutas de distribución (WMS / Distribution)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-10
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
    [Route("api/distributionroute")]
    public class DistributionRouteController : ControllerBase
    {
        private readonly IDistributionRouteService _service;
        private readonly ILogger<DistributionRouteController> _logger;

        public DistributionRouteController(
            IDistributionRouteService service,
            ILogger<DistributionRouteController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        private int GetCompanyId()
        {
            var claim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(claim, out var id)) return id;
            throw new UnauthorizedAccessException("companyId no encontrado en el token");
        }

        private string GetCurrentUser() =>
            User.FindFirstValue("cms_username") ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "SYSTEM";

        // ============================================================
        // GET /api/distributionroute
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string?  search    = null,
            [FromQuery] string?  status    = null,
            [FromQuery] string?  frequency = null,
            [FromQuery] bool?    isActive  = null,
            [FromQuery] int      page      = 1,
            [FromQuery] int      pageSize  = 20)
        {
            try
            {
                var companyId = GetCompanyId();
                var (items, total) = await _service.GetRoutesAsync(
                    companyId, search, status, frequency, isActive, page, pageSize);

                return Ok(new
                {
                    items      = items.Select(MapToDto),
                    totalCount = total,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting distribution routes");
                return StatusCode(500, new { error = "Error al obtener rutas de distribución", detail = ex.Message });
            }
        }

        // ============================================================
        // GET /api/distributionroute/{id}
        // ============================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var route = await _service.GetByIdAsync(companyId, id);
                if (route == null) return NotFound(new { error = "Ruta no encontrada" });
                var dto = MapToDto(route);
                dto.Stops = route.Stops.Select(MapStopToDto).ToList();
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting route {Id}", id);
                return StatusCode(500, new { error = "Error al obtener ruta" });
            }
        }

        // ============================================================
        // POST /api/distributionroute
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RouteUpsertDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var companyId = GetCompanyId();
                var user = GetCurrentUser();

                if (await _service.CodeExistsAsync(companyId, dto.Code))
                    return Conflict(new { error = $"El código '{dto.Code}' ya existe" });

                var route   = MapFromDto(dto);
                var created = await _service.CreateAsync(companyId, route, user);

                // Guardar paradas si vienen en el mismo request
                if (dto.Stops?.Count > 0)
                    await _service.SaveStopsAsync(companyId, created.Id, dto.Stops.Select(MapStopFromDto).ToList(), user);

                return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating route");
                return StatusCode(500, new { error = "Error al crear ruta", detail = ex.Message });
            }
        }

        // ============================================================
        // PUT /api/distributionroute/{id}
        // ============================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] RouteUpsertDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var companyId = GetCompanyId();
                var user = GetCurrentUser();

                var existing = await _service.GetByIdAsync(companyId, id);
                if (existing == null) return NotFound(new { error = "Ruta no encontrada" });

                if (await _service.CodeExistsAsync(companyId, dto.Code, excludeId: id))
                    return Conflict(new { error = $"El código '{dto.Code}' ya existe" });

                var route = MapFromDto(dto);
                route.Id  = id;
                var updated = await _service.UpdateAsync(companyId, route, user);

                return Ok(MapToDto(updated));
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "Ruta no encontrada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating route {Id}", id);
                return StatusCode(500, new { error = "Error al actualizar ruta", detail = ex.Message });
            }
        }

        // ============================================================
        // PATCH /api/distributionroute/{id}/deactivate
        // ============================================================
        [HttpPatch("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var result    = await _service.DeactivateAsync(companyId, id, GetCurrentUser());
                if (!result) return NotFound(new { error = "Ruta no encontrada" });
                return Ok(new { message = "Ruta desactivada exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating route {Id}", id);
                return StatusCode(500, new { error = "Error al desactivar ruta" });
            }
        }

        // ============================================================
        // PATCH /api/distributionroute/{id}/activate
        // ============================================================
        [HttpPatch("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var result    = await _service.ActivateAsync(companyId, id, GetCurrentUser());
                if (!result) return NotFound(new { error = "Ruta no encontrada" });
                return Ok(new { message = "Ruta activada exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating route {Id}", id);
                return StatusCode(500, new { error = "Error al activar ruta" });
            }
        }

        // ============================================================
        // GET /api/distributionroute/{id}/stops
        // ============================================================
        [HttpGet("{id:int}/stops")]
        public async Task<IActionResult> GetStops(int id)
        {
            try
            {
                var companyId = GetCompanyId();
                var stops     = await _service.GetStopsAsync(companyId, id);
                return Ok(stops.Select(MapStopToDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stops for route {Id}", id);
                return StatusCode(500, new { error = "Error al obtener paradas" });
            }
        }

        // ============================================================
        // POST /api/distributionroute/{id}/stops
        // ============================================================
        [HttpPost("{id:int}/stops")]
        public async Task<IActionResult> SaveStops(int id, [FromBody] List<StopUpsertDto> dtos)
        {
            try
            {
                var companyId = GetCompanyId();
                var user      = GetCurrentUser();
                var stops     = dtos.Select(MapStopFromDto).ToList();
                await _service.SaveStopsAsync(companyId, id, stops, user);
                var saved = await _service.GetStopsAsync(companyId, id);
                return Ok(saved.Select(MapStopToDto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving stops for route {Id}", id);
                return StatusCode(500, new { error = "Error al guardar paradas", detail = ex.Message });
            }
        }

        // ============================================================
        // GET /api/distributionroute/check-code
        // ============================================================
        [HttpGet("check-code")]
        public async Task<IActionResult> CheckCode([FromQuery] string code, [FromQuery] int? excludeId = null)
        {
            try
            {
                var companyId = GetCompanyId();
                var exists    = await _service.CodeExistsAsync(companyId, code, excludeId);
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

        private static RouteDto MapToDto(DistributionRoute r) => new()
        {
            Id                        = r.Id,
            Code                      = r.Code,
            Name                      = r.Name,
            Description               = r.Description,
            Status                    = r.Status,
            Frequency                 = r.Frequency,
            OperationDays             = r.OperationDays,
            DepartureTime             = r.DepartureTime?.ToString("HH:mm"),
            EstimatedDurationMinutes  = r.EstimatedDurationMinutes,
            EstimatedDistanceKm       = r.EstimatedDistanceKm,
            VehiclePlate              = r.VehiclePlate,
            VehicleDescription        = r.VehicleDescription,
            DriverUserId              = r.DriverUserId,
            DriverName                = r.DriverName,
            IdOriginWarehouse         = r.IdOriginWarehouse,
            OriginWarehouseName       = r.OriginWarehouseName,
            MaxWeightKg               = r.MaxWeightKg,
            MaxVolumeM3               = r.MaxVolumeM3,
            RequiresSignature         = r.RequiresSignature,
            RequiresPhoto             = r.RequiresPhoto,
            AllowsPartialDelivery     = r.AllowsPartialDelivery,
            IsActive                  = r.IsActive,
            Notes                     = r.Notes,
            CreatedAt                 = r.CreatedAt,
            UpdatedAt                 = r.UpdatedAt,
            CreatedBy                 = r.CreatedBy,
            UpdatedBy                 = r.UpdatedBy,
            StopCount                 = r.Stops.Count
        };

        private static StopDto MapStopToDto(DistributionRouteStop s) => new()
        {
            Id                       = s.Id,
            IdRoute                  = s.IdRoute,
            StopOrder                = s.StopOrder,
            CustomerName             = s.CustomerName,
            Address                  = s.Address,
            City                     = s.City,
            GpsLatitude              = s.GpsLatitude,
            GpsLongitude             = s.GpsLongitude,
            ContactName              = s.ContactName,
            ContactPhone             = s.ContactPhone,
            TimeWindowStart          = s.TimeWindowStart?.ToString("HH:mm"),
            TimeWindowEnd            = s.TimeWindowEnd?.ToString("HH:mm"),
            EstimatedServiceMinutes  = s.EstimatedServiceMinutes,
            Status                   = s.Status,
            Notes                    = s.Notes,
            IsActive                 = s.IsActive
        };

        private static DistributionRoute MapFromDto(RouteUpsertDto dto) => new()
        {
            Code                      = dto.Code.Trim().ToUpper(),
            Name                      = dto.Name.Trim(),
            Description               = dto.Description?.Trim(),
            Status                    = dto.Status,
            Frequency                 = dto.Frequency,
            OperationDays             = dto.OperationDays,
            DepartureTime             = string.IsNullOrWhiteSpace(dto.DepartureTime) ? null : TimeOnly.Parse(dto.DepartureTime),
            EstimatedDurationMinutes  = dto.EstimatedDurationMinutes,
            EstimatedDistanceKm       = dto.EstimatedDistanceKm,
            VehiclePlate              = dto.VehiclePlate?.Trim().ToUpper(),
            VehicleDescription        = dto.VehicleDescription?.Trim(),
            DriverUserId              = dto.DriverUserId,
            IdOriginWarehouse         = dto.IdOriginWarehouse,
            MaxWeightKg               = dto.MaxWeightKg,
            MaxVolumeM3               = dto.MaxVolumeM3,
            RequiresSignature         = dto.RequiresSignature,
            RequiresPhoto             = dto.RequiresPhoto,
            AllowsPartialDelivery     = dto.AllowsPartialDelivery,
            IsActive                  = dto.IsActive,
            Notes                     = dto.Notes?.Trim()
        };

        private static DistributionRouteStop MapStopFromDto(StopUpsertDto dto) => new()
        {
            CustomerName            = dto.CustomerName?.Trim(),
            Address                 = dto.Address?.Trim(),
            City                    = dto.City?.Trim(),
            GpsLatitude             = dto.GpsLatitude,
            GpsLongitude            = dto.GpsLongitude,
            ContactName             = dto.ContactName?.Trim(),
            ContactPhone            = dto.ContactPhone?.Trim(),
            TimeWindowStart         = string.IsNullOrWhiteSpace(dto.TimeWindowStart) ? null : TimeOnly.Parse(dto.TimeWindowStart),
            TimeWindowEnd           = string.IsNullOrWhiteSpace(dto.TimeWindowEnd)   ? null : TimeOnly.Parse(dto.TimeWindowEnd),
            EstimatedServiceMinutes = dto.EstimatedServiceMinutes,
            Notes                   = dto.Notes?.Trim(),
            Status                  = StopStatus.Pending
        };
    }

    // ============================================================
    // DTOs
    // ============================================================

    public class RouteDto
    {
        public int     Id                        { get; set; }
        public string  Code                      { get; set; } = string.Empty;
        public string  Name                      { get; set; } = string.Empty;
        public string? Description               { get; set; }
        public string  Status                    { get; set; } = RouteStatus.Active;
        public string  Frequency                 { get; set; } = RouteFrequency.Daily;
        public int     OperationDays             { get; set; }
        public string? DepartureTime             { get; set; }
        public int?    EstimatedDurationMinutes  { get; set; }
        public decimal? EstimatedDistanceKm      { get; set; }
        public string? VehiclePlate              { get; set; }
        public string? VehicleDescription        { get; set; }
        public int?    DriverUserId              { get; set; }
        public string? DriverName                { get; set; }
        public int?    IdOriginWarehouse         { get; set; }
        public string? OriginWarehouseName       { get; set; }
        public decimal? MaxWeightKg              { get; set; }
        public decimal? MaxVolumeM3              { get; set; }
        public bool    RequiresSignature         { get; set; }
        public bool    RequiresPhoto             { get; set; }
        public bool    AllowsPartialDelivery     { get; set; }
        public bool    IsActive                  { get; set; }
        public string? Notes                     { get; set; }
        public DateTime CreatedAt                { get; set; }
        public DateTime UpdatedAt                { get; set; }
        public string  CreatedBy                 { get; set; } = string.Empty;
        public string  UpdatedBy                 { get; set; } = string.Empty;
        public int     StopCount                 { get; set; }
        public List<StopDto> Stops               { get; set; } = new();
    }

    public class RouteUpsertDto
    {
        public string  Code                      { get; set; } = string.Empty;
        public string  Name                      { get; set; } = string.Empty;
        public string? Description               { get; set; }
        public string  Status                    { get; set; } = RouteStatus.Active;
        public string  Frequency                 { get; set; } = RouteFrequency.Daily;
        public int     OperationDays             { get; set; } = 31;
        public string? DepartureTime             { get; set; }
        public int?    EstimatedDurationMinutes  { get; set; }
        public decimal? EstimatedDistanceKm      { get; set; }
        public string? VehiclePlate              { get; set; }
        public string? VehicleDescription        { get; set; }
        public int?    DriverUserId              { get; set; }
        public int?    IdOriginWarehouse         { get; set; }
        public decimal? MaxWeightKg              { get; set; }
        public decimal? MaxVolumeM3              { get; set; }
        public bool    RequiresSignature         { get; set; }
        public bool    RequiresPhoto             { get; set; } = false;
        public bool    AllowsPartialDelivery     { get; set; } = true;
        public bool    IsActive                  { get; set; } = true;
        public string? Notes                     { get; set; }
        public List<StopUpsertDto> Stops         { get; set; } = new();
    }

    public class StopDto
    {
        public int     Id                       { get; set; }
        public int     IdRoute                  { get; set; }
        public int     StopOrder                { get; set; }
        public string? CustomerName             { get; set; }
        public string? Address                  { get; set; }
        public string? City                     { get; set; }
        public double? GpsLatitude              { get; set; }
        public double? GpsLongitude             { get; set; }
        public string? ContactName              { get; set; }
        public string? ContactPhone             { get; set; }
        public string? TimeWindowStart          { get; set; }
        public string? TimeWindowEnd            { get; set; }
        public int?    EstimatedServiceMinutes  { get; set; }
        public string  Status                   { get; set; } = StopStatus.Pending;
        public string? Notes                    { get; set; }
        public bool    IsActive                 { get; set; } = true;
    }

    public class StopUpsertDto
    {
        public string? CustomerName             { get; set; }
        public string? Address                  { get; set; }
        public string? City                     { get; set; }
        public double? GpsLatitude              { get; set; }
        public double? GpsLongitude             { get; set; }
        public string? ContactName              { get; set; }
        public string? ContactPhone             { get; set; }
        public string? TimeWindowStart          { get; set; }
        public string? TimeWindowEnd            { get; set; }
        public int?    EstimatedServiceMinutes  { get; set; }
        public string? Notes                    { get; set; }
    }
}
