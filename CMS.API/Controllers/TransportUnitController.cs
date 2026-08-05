// ================================================================================
// ARCHIVO: CMS.API/Controllers/TransportUnitController.cs
// PROPÓSITO: API REST para gestión de unidades de transporte (Fleet Management)
// DESCRIPCIÓN: CRUD de unidades de transporte e historial de mantenimiento usando
//              la BD de la compañía activa.
//              Sub-recurso /api/transportunit/{id}/maintenance.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-14
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
    [Route("api/[controller]")]
    public class TransportUnitController : ControllerBase
    {
        private readonly ICompanyDbContextFactory _dbContextFactory;
        private readonly ILogger<TransportUnitController> _logger;

        public TransportUnitController(
            ICompanyDbContextFactory dbContextFactory,
            ILogger<TransportUnitController> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        // ── Helpers ───────────────────────────────────────────────

        private int GetCurrentCompanyId()
        {
            var claim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(claim, out var id)) return id;
            throw new UnauthorizedAccessException("companyId no encontrado en el token JWT");
        }

        private string GetCurrentUser()
        {
            var raw = User.FindFirstValue("cms_username") ?? User.FindFirst(ClaimTypes.Name)?.Value
                   ?? "SYSTEM";
            return raw.Length > 30 ? raw[..30] : raw;
        }

        private static object MapUnit(TransportUnit u, IEnumerable<TransportUnitMaintenance>? maintenance = null) => new
        {
            id             = u.Id,
            code           = u.Code,
            plateNumber    = u.PlateNumber,
            name           = u.Name,
            unitType       = u.UnitType,
            idVehicleBrand = u.IdVehicleBrand,
            brandName      = u.BrandName,
            idVehicleModel = u.IdVehicleModel,
            modelName      = u.ModelName,
            year           = u.Year,
            colorHex       = u.ColorHex,
            vinNumber      = u.VinNumber,
            engineNumber   = u.EngineNumber,
            fuelType       = u.FuelType,
            maxLoadKg      = u.MaxLoadKg,
            maxVolumeM3    = u.MaxVolumeM3,
            cargoLengthM   = u.CargoLengthM,
            cargoWidthM    = u.CargoWidthM,
            cargoHeightM   = u.CargoHeightM,
            palletCapacity = u.PalletCapacity,
            currentOdometerKm      = u.CurrentOdometerKm,
            lastOdometerDate       = u.LastOdometerDate?.ToString("yyyy-MM-dd"),
            nextInspectionDate     = u.NextInspectionDate?.ToString("yyyy-MM-dd"),
            insuranceExpiredDate   = u.InsuranceExpiredDate?.ToString("yyyy-MM-dd"),
            insuranceCompany       = u.InsuranceCompany,
            insurancePolicyNumber  = u.InsurancePolicyNumber,
            idDriver               = u.IdDriver,
            assignedDriverName     = u.AssignedDriverName,
            idInsurer              = u.IdInsurer,
            insurerName            = u.InsurerName,
            idWarehouse            = u.IdWarehouse,
            status                 = u.Status,
            notes                  = u.Notes,
            isActive               = u.IsActive,
            createDate             = u.CreateDate,
            recordDate             = u.RecordDate,
            createdBy              = u.CreatedBy,
            updatedBy              = u.UpdatedBy,
            maintenance            = maintenance?.Select(MapMaintenance).ToList()
        };

        private static object MapMaintenance(TransportUnitMaintenance m) => new
        {
            id                   = m.Id,
            idTransportUnit      = m.IdTransportUnit,
            maintenanceType      = m.MaintenanceType,
            description          = m.Description,
            maintenanceDate      = m.MaintenanceDate.ToString("yyyy-MM-dd"),
            odometerAtServiceKm  = m.OdometerAtServiceKm,
            nextServiceDate      = m.NextServiceDate?.ToString("yyyy-MM-dd"),
            nextServiceKm        = m.NextServiceKm,
            cost                 = m.Cost,
            currency             = m.Currency,
            supplierName         = m.SupplierName,
            invoiceNumber        = m.InvoiceNumber,
            vehicleOutOfService  = m.VehicleOutOfService,
            notes                = m.Notes,
            createDate           = m.CreateDate,
            createdBy            = m.CreatedBy,
            updatedBy            = m.UpdatedBy
        };

        // ============================================================
        // GET /api/transportunit
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search              = null,
            [FromQuery] string? type                = null,
            [FromQuery] string? status              = null,
            [FromQuery] bool?   isActive            = null,
            [FromQuery] bool    availableOnly        = false,
            [FromQuery] int?    currentWarehouseId   = null,
            [FromQuery] int     page                = 1,
            [FromQuery] int     pageSize            = 20)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                var query = db.TransportUnits.AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(u =>
                        u.Code.Contains(search) ||
                        u.Name.Contains(search) ||
                        u.PlateNumber.Contains(search) ||
                        (u.BrandName != null && u.BrandName.Contains(search)) ||
                        (u.ModelName != null && u.ModelName.Contains(search)));

                if (!string.IsNullOrWhiteSpace(type))
                    query = query.Where(u => u.UnitType == type);

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(u => u.Status == status);

                if (isActive.HasValue)
                    query = query.Where(u => u.IsActive == isActive.Value);

                // Excluir unidades ya asignadas a otra bodega en tránsito
                // Si currentWarehouseId está informado, la unidad actualmente asignada
                // a esa bodega sí debe aparecer en la lista (para poder re-guardarla).
                if (availableOnly)
                {
                    // Obtener los ids de transport_unit ya vinculados a CUALQUIER bodega
                    var linkedIds = await db.Warehouses
                        .Where(w => w.IdTransportUnit != null)
                        .Select(w => w.IdTransportUnit!.Value)
                        .ToListAsync();

                    // Quitar de esa lista la unidad que ya tiene la bodega que se está editando
                    if (currentWarehouseId.HasValue)
                    {
                        var currentUnit = await db.Warehouses
                            .Where(w => w.Id == currentWarehouseId.Value)
                            .Select(w => w.IdTransportUnit)
                            .FirstOrDefaultAsync();

                        if (currentUnit.HasValue)
                            linkedIds.Remove(currentUnit.Value);
                    }

                    if (linkedIds.Count > 0)
                        query = query.Where(u => !linkedIds.Contains(u.Id));
                }

                var total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(u => u.CreateDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Ok(new
                {
                    items      = items.Select(u => MapUnit(u)),
                    total,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)total / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo unidades de transporte");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // GET /api/transportunit/{id}
        // ============================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                var unit = await db.TransportUnits.FirstOrDefaultAsync(u => u.Id == id);
                if (unit == null) return NotFound(new { error = "Unidad de transporte no encontrada" });

                var maintenance = await db.TransportUnitMaintenances
                    .Where(m => m.IdTransportUnit == id)
                    .OrderByDescending(m => m.MaintenanceDate)
                    .ToListAsync();

                return Ok(MapUnit(unit, maintenance));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo unidad de transporte {Id}", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // POST /api/transportunit
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TransportUnitDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var auditUser = GetCurrentUser();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                if (await db.TransportUnits.AnyAsync(u => u.Code == dto.Code.Trim()))
                    return BadRequest(new { error = $"El código '{dto.Code}' ya está en uso." });

                if (await db.TransportUnits.AnyAsync(u => u.PlateNumber == dto.PlateNumber.Trim()))
                    return BadRequest(new { error = $"La placa '{dto.PlateNumber}' ya está registrada." });

                if (!string.IsNullOrWhiteSpace(dto.VinNumber) &&
                    await db.TransportUnits.AnyAsync(u => u.VinNumber == dto.VinNumber.Trim()))
                    return BadRequest(new { error = $"El número de chasis/VIN '{dto.VinNumber}' ya está registrado en esta compañía." });

                if (!string.IsNullOrWhiteSpace(dto.EngineNumber) &&
                    await db.TransportUnits.AnyAsync(u => u.EngineNumber == dto.EngineNumber.Trim()))
                    return BadRequest(new { error = $"El número de motor '{dto.EngineNumber}' ya está registrado en esta compañía." });

                var unit = new TransportUnit
                {
                    Code                  = dto.Code.Trim().ToUpper(),
                    PlateNumber           = dto.PlateNumber.Trim().ToUpper(),
                    Name                  = dto.Name.Trim(),
                    UnitType              = dto.UnitType,
                    IdVehicleBrand        = dto.IdVehicleBrand,
                    BrandName             = dto.BrandName?.Trim(),
                    IdVehicleModel        = dto.IdVehicleModel,
                    ModelName             = dto.ModelName?.Trim(),
                    Year                  = dto.Year,
                    ColorHex              = dto.ColorHex?.Trim(),
                    VinNumber             = dto.VinNumber?.Trim(),
                    EngineNumber          = dto.EngineNumber?.Trim(),
                    FuelType              = dto.FuelType?.Trim(),
                    MaxLoadKg             = dto.MaxLoadKg,
                    MaxVolumeM3           = dto.MaxVolumeM3,
                    CargoLengthM          = dto.CargoLengthM,
                    CargoWidthM           = dto.CargoWidthM,
                    CargoHeightM          = dto.CargoHeightM,
                    PalletCapacity        = dto.PalletCapacity,
                    CurrentOdometerKm     = dto.CurrentOdometerKm ?? 0,
                    LastOdometerDate      = dto.LastOdometerDate.HasValue ? DateOnly.FromDateTime(dto.LastOdometerDate.Value) : null,
                    NextInspectionDate    = dto.NextInspectionDate.HasValue ? DateOnly.FromDateTime(dto.NextInspectionDate.Value) : null,
                    InsuranceExpiredDate  = dto.InsuranceExpiredDate.HasValue ? DateOnly.FromDateTime(dto.InsuranceExpiredDate.Value) : null,
                    InsuranceCompany      = dto.InsuranceCompany?.Trim(),
                    InsurancePolicyNumber = dto.InsurancePolicyNumber?.Trim(),
                    IdDriver              = dto.IdDriver,
                    AssignedDriverName    = dto.AssignedDriverName?.Trim(),
                    IdInsurer             = dto.IdInsurer,
                    InsurerName           = dto.InsurerName?.Trim(),
                    IdWarehouse           = dto.IdWarehouse,
                    Status                = dto.Status ?? TransportUnitStatus.Active,
                    Notes                 = dto.Notes?.Trim(),
                    IsActive              = true,
                    CreateDate            = DateTime.UtcNow,
                    RecordDate            = DateTime.UtcNow,
                    CreatedBy             = auditUser,
                    UpdatedBy             = auditUser,
                    Rowpointer            = Guid.NewGuid()
                };

                db.TransportUnits.Add(unit);
                await db.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = unit.Id }, MapUnit(unit));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando unidad de transporte");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // PUT /api/transportunit/{id}
        // ============================================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] TransportUnitDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var auditUser = GetCurrentUser();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                var unit = await db.TransportUnits.FirstOrDefaultAsync(u => u.Id == id);
                if (unit == null) return NotFound(new { error = "Unidad de transporte no encontrada" });

                if (await db.TransportUnits.AnyAsync(u => u.Code == dto.Code.Trim() && u.Id != id))
                    return BadRequest(new { error = $"El código '{dto.Code}' ya está en uso." });

                if (await db.TransportUnits.AnyAsync(u => u.PlateNumber == dto.PlateNumber.Trim() && u.Id != id))
                    return BadRequest(new { error = $"La placa '{dto.PlateNumber}' ya está registrada." });

                if (!string.IsNullOrWhiteSpace(dto.VinNumber) &&
                    await db.TransportUnits.AnyAsync(u => u.VinNumber == dto.VinNumber.Trim() && u.Id != id))
                    return BadRequest(new { error = $"El número de chasis/VIN '{dto.VinNumber}' ya está registrado en esta compañía." });

                if (!string.IsNullOrWhiteSpace(dto.EngineNumber) &&
                    await db.TransportUnits.AnyAsync(u => u.EngineNumber == dto.EngineNumber.Trim() && u.Id != id))
                    return BadRequest(new { error = $"El número de motor '{dto.EngineNumber}' ya está registrado en esta compañía." });

                unit.Code                  = dto.Code.Trim().ToUpper();
                unit.PlateNumber           = dto.PlateNumber.Trim().ToUpper();
                unit.Name                  = dto.Name.Trim();
                unit.UnitType              = dto.UnitType;
                unit.IdVehicleBrand        = dto.IdVehicleBrand;
                unit.BrandName             = dto.BrandName?.Trim();
                unit.IdVehicleModel        = dto.IdVehicleModel;
                unit.ModelName             = dto.ModelName?.Trim();
                unit.Year                  = dto.Year;
                unit.ColorHex              = dto.ColorHex?.Trim();
                unit.VinNumber             = dto.VinNumber?.Trim();
                unit.EngineNumber          = dto.EngineNumber?.Trim();
                unit.FuelType              = dto.FuelType?.Trim();
                unit.MaxLoadKg             = dto.MaxLoadKg;
                unit.MaxVolumeM3           = dto.MaxVolumeM3;
                unit.CargoLengthM          = dto.CargoLengthM;
                unit.CargoWidthM           = dto.CargoWidthM;
                unit.CargoHeightM          = dto.CargoHeightM;
                unit.PalletCapacity        = dto.PalletCapacity;
                unit.CurrentOdometerKm     = dto.CurrentOdometerKm ?? unit.CurrentOdometerKm;
                unit.LastOdometerDate      = dto.LastOdometerDate.HasValue ? DateOnly.FromDateTime(dto.LastOdometerDate.Value) : null;
                unit.NextInspectionDate    = dto.NextInspectionDate.HasValue ? DateOnly.FromDateTime(dto.NextInspectionDate.Value) : null;
                unit.InsuranceExpiredDate  = dto.InsuranceExpiredDate.HasValue ? DateOnly.FromDateTime(dto.InsuranceExpiredDate.Value) : null;
                unit.InsuranceCompany      = dto.InsuranceCompany?.Trim();
                unit.InsurancePolicyNumber = dto.InsurancePolicyNumber?.Trim();
                unit.IdDriver              = dto.IdDriver;
                unit.AssignedDriverName    = dto.AssignedDriverName?.Trim();
                unit.IdInsurer             = dto.IdInsurer;
                unit.InsurerName           = dto.InsurerName?.Trim();
                unit.IdWarehouse           = dto.IdWarehouse;
                unit.Status                = dto.Status ?? unit.Status;
                unit.Notes                 = dto.Notes?.Trim();
                unit.RecordDate            = DateTime.UtcNow;
                unit.UpdatedBy             = auditUser;

                await db.SaveChangesAsync();
                return Ok(MapUnit(unit));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando unidad de transporte {Id}", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // PATCH /api/transportunit/{id}/status
        // ============================================================
        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeTransportUnitStatusDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var auditUser = GetCurrentUser();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                var unit = await db.TransportUnits.FirstOrDefaultAsync(u => u.Id == id);
                if (unit == null) return NotFound(new { error = "Unidad de transporte no encontrada" });

                unit.Status     = dto.Status;
                unit.IsActive   = dto.Status != TransportUnitStatus.Retired;
                unit.RecordDate = DateTime.UtcNow;
                unit.UpdatedBy  = auditUser;

                await db.SaveChangesAsync();
                return Ok(new { id = unit.Id, status = unit.Status, isActive = unit.IsActive });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cambiando estado de la unidad de transporte {Id}", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // DELETE /api/transportunit/{id}
        // ============================================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var auditUser = GetCurrentUser();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                var unit = await db.TransportUnits.FirstOrDefaultAsync(u => u.Id == id);
                if (unit == null) return NotFound(new { error = "Unidad de transporte no encontrada" });

                // Soft delete
                unit.IsActive   = false;
                unit.Status     = TransportUnitStatus.Retired;
                unit.RecordDate = DateTime.UtcNow;
                unit.UpdatedBy  = auditUser;

                await db.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando unidad de transporte {Id}", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // PATCH /api/transportunit/{id}/odometer
        // ============================================================
        [HttpPatch("{id:int}/odometer")]
        public async Task<IActionResult> UpdateOdometer(int id, [FromBody] UpdateOdometerDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var auditUser = GetCurrentUser();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                var unit = await db.TransportUnits.FirstOrDefaultAsync(u => u.Id == id);
                if (unit == null) return NotFound(new { error = "Unidad de transporte no encontrada" });

                if (dto.OdometerKm < unit.CurrentOdometerKm)
                    return BadRequest(new { error = $"El nuevo kilometraje ({dto.OdometerKm}) no puede ser menor al actual ({unit.CurrentOdometerKm})." });

                unit.CurrentOdometerKm = dto.OdometerKm;
                unit.LastOdometerDate  = DateOnly.FromDateTime(dto.Date ?? DateTime.Today);
                unit.RecordDate        = DateTime.UtcNow;
                unit.UpdatedBy         = auditUser;

                await db.SaveChangesAsync();
                return Ok(new { id = unit.Id, currentOdometerKm = unit.CurrentOdometerKm, lastOdometerDate = unit.LastOdometerDate?.ToString("yyyy-MM-dd") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando kilometraje de la unidad {Id}", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // GET /api/transportunit/{id}/maintenance
        // ============================================================
        [HttpGet("{id:int}/maintenance")]
        public async Task<IActionResult> GetMaintenance(
            int id,
            [FromQuery] string? type = null,
            [FromQuery] int page     = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                if (!await db.TransportUnits.AnyAsync(u => u.Id == id))
                    return NotFound(new { error = "Unidad de transporte no encontrada" });

                var query = db.TransportUnitMaintenances.Where(m => m.IdTransportUnit == id);
                if (!string.IsNullOrWhiteSpace(type))
                    query = query.Where(m => m.MaintenanceType == type);

                var total = await query.CountAsync();
                var items = await query
                    .OrderByDescending(m => m.MaintenanceDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Ok(new
                {
                    items      = items.Select(MapMaintenance),
                    total,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)total / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo mantenimientos de la unidad {Id}", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // POST /api/transportunit/{id}/maintenance
        // ============================================================
        [HttpPost("{id:int}/maintenance")]
        public async Task<IActionResult> AddMaintenance(int id, [FromBody] TransportUnitMaintenanceDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var auditUser = GetCurrentUser();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                var unit = await db.TransportUnits.FirstOrDefaultAsync(u => u.Id == id);
                if (unit == null) return NotFound(new { error = "Unidad de transporte no encontrada" });

                var record = new TransportUnitMaintenance
                {
                    IdTransportUnit     = id,
                    MaintenanceType     = dto.MaintenanceType,
                    Description         = dto.Description.Trim(),
                    MaintenanceDate     = DateOnly.Parse(dto.MaintenanceDate),
                    OdometerAtServiceKm = dto.OdometerAtServiceKm,
                    NextServiceDate     = !string.IsNullOrEmpty(dto.NextServiceDate) ? DateOnly.Parse(dto.NextServiceDate) : null,
                    NextServiceKm       = dto.NextServiceKm,
                    Cost                = dto.Cost,
                    Currency            = dto.Currency?.Trim(),
                    SupplierName        = dto.SupplierName?.Trim(),
                    InvoiceNumber       = dto.InvoiceNumber?.Trim(),
                    VehicleOutOfService = dto.VehicleOutOfService,
                    Notes               = dto.Notes?.Trim(),
                    CreateDate          = DateTime.UtcNow,
                    RecordDate          = DateTime.UtcNow,
                    CreatedBy           = auditUser,
                    UpdatedBy           = auditUser,
                    Rowpointer          = Guid.NewGuid()
                };

                db.TransportUnitMaintenances.Add(record);

                if (dto.OdometerAtServiceKm.HasValue && dto.OdometerAtServiceKm > unit.CurrentOdometerKm)
                {
                    unit.CurrentOdometerKm = dto.OdometerAtServiceKm.Value;
                    unit.LastOdometerDate  = record.MaintenanceDate;
                }
                if (dto.VehicleOutOfService)
                    unit.Status = TransportUnitStatus.Maintenance;

                unit.RecordDate = DateTime.UtcNow;
                unit.UpdatedBy  = auditUser;

                await db.SaveChangesAsync();
                return Ok(MapMaintenance(record));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando mantenimiento para unidad {Id}", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // PUT /api/transportunit/{unitId}/maintenance/{maintenanceId}
        // ============================================================
        [HttpPut("{unitId:int}/maintenance/{maintenanceId:int}")]
        public async Task<IActionResult> UpdateMaintenance(int unitId, int maintenanceId, [FromBody] TransportUnitMaintenanceDto dto)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var auditUser = GetCurrentUser();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                var record = await db.TransportUnitMaintenances
                    .FirstOrDefaultAsync(m => m.Id == maintenanceId && m.IdTransportUnit == unitId);
                if (record == null) return NotFound(new { error = "Registro de mantenimiento no encontrado" });

                record.MaintenanceType     = dto.MaintenanceType;
                record.Description         = dto.Description.Trim();
                record.MaintenanceDate     = DateOnly.Parse(dto.MaintenanceDate);
                record.OdometerAtServiceKm = dto.OdometerAtServiceKm;
                record.NextServiceDate     = !string.IsNullOrEmpty(dto.NextServiceDate) ? DateOnly.Parse(dto.NextServiceDate) : null;
                record.NextServiceKm       = dto.NextServiceKm;
                record.Cost                = dto.Cost;
                record.Currency            = dto.Currency?.Trim();
                record.SupplierName        = dto.SupplierName?.Trim();
                record.InvoiceNumber       = dto.InvoiceNumber?.Trim();
                record.VehicleOutOfService = dto.VehicleOutOfService;
                record.Notes               = dto.Notes?.Trim();
                record.RecordDate          = DateTime.UtcNow;
                record.UpdatedBy           = auditUser;

                await db.SaveChangesAsync();
                return Ok(MapMaintenance(record));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando mantenimiento {MaintenanceId}", maintenanceId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // DELETE /api/transportunit/{unitId}/maintenance/{maintenanceId}
        // ============================================================
        [HttpDelete("{unitId:int}/maintenance/{maintenanceId:int}")]
        public async Task<IActionResult> DeleteMaintenance(int unitId, int maintenanceId)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                using var db  = await _dbContextFactory.CreateDbContextAsync(companyId);

                var record = await db.TransportUnitMaintenances
                    .FirstOrDefaultAsync(m => m.Id == maintenanceId && m.IdTransportUnit == unitId);
                if (record == null) return NotFound(new { error = "Registro de mantenimiento no encontrado" });

                db.TransportUnitMaintenances.Remove(record);
                await db.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando mantenimiento {MaintenanceId}", maintenanceId);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    // ============================================================
    // DTOs
    // ============================================================

    public class TransportUnitDto
    {
        public string   Code                  { get; set; } = string.Empty;
        public string   PlateNumber           { get; set; } = string.Empty;
        public string   Name                  { get; set; } = string.Empty;
        public string   UnitType              { get; set; } = TransportUnitType.Truck;
        public int?     IdVehicleBrand        { get; set; }
        public string?  BrandName             { get; set; }
        public int?     IdVehicleModel        { get; set; }
        public string?  ModelName             { get; set; }
        public int?     Year                  { get; set; }
        public string?  ColorHex              { get; set; }
        public string?  VinNumber             { get; set; }
        public string?  EngineNumber          { get; set; }
        public string?  FuelType              { get; set; }
        public decimal? MaxLoadKg             { get; set; }
        public decimal? MaxVolumeM3           { get; set; }
        public decimal? CargoLengthM          { get; set; }
        public decimal? CargoWidthM           { get; set; }
        public decimal? CargoHeightM          { get; set; }
        public int?     PalletCapacity        { get; set; }
        public decimal? CurrentOdometerKm     { get; set; }
        public DateTime? LastOdometerDate     { get; set; }
        public DateTime? NextInspectionDate   { get; set; }
        public DateTime? InsuranceExpiredDate { get; set; }
        public string?  InsuranceCompany      { get; set; }
        public string?  InsurancePolicyNumber { get; set; }
        public int?     IdDriver              { get; set; }
        public string?  AssignedDriverName    { get; set; }
        public int?     IdInsurer             { get; set; }
        public string?  InsurerName           { get; set; }
        public int?     IdWarehouse           { get; set; }
        public string?  Status                { get; set; }
        public string?  Notes                 { get; set; }
    }

    public class ChangeTransportUnitStatusDto
    {
        public string Status { get; set; } = TransportUnitStatus.Active;
    }

    public class UpdateOdometerDto
    {
        public decimal   OdometerKm { get; set; }
        public DateTime? Date       { get; set; }
    }

    public class TransportUnitMaintenanceDto
    {
        public string   MaintenanceType      { get; set; } = TransportUnitMaintenanceType.OilChange;
        public string   Description          { get; set; } = string.Empty;
        public string   MaintenanceDate      { get; set; } = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        public decimal? OdometerAtServiceKm  { get; set; }
        public string?  NextServiceDate      { get; set; }
        public decimal? NextServiceKm        { get; set; }
        public decimal? Cost                 { get; set; }
        public string?  Currency             { get; set; }
        public string?  SupplierName         { get; set; }
        public string?  InvoiceNumber        { get; set; }
        public bool     VehicleOutOfService  { get; set; }
        public string?  Notes                { get; set; }
    }
}
