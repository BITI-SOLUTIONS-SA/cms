// ================================================================================
// ARCHIVO: CMS.Entities/Operational/TransportUnit.cs
// PROPÓSITO: Entidades para la gestión de unidades de transporte (Fleet Management)
// DESCRIPCIÓN: Soporta cualquier tipo de unidad (camión, carro, moto, etc.)
//              con datos de capacidad, dimensiones, marca/modelo, kilometraje
//              y el historial de mantenimiento (aceite, llantas, etc.).
//              Se almacena en la BD de cada compañía, schema {company_code}.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-14
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    // ============================================================
    // CONSTANTES DE TIPO Y ESTADO
    // ============================================================

    /// <summary>Tipo de unidad de transporte.</summary>
    public static class TransportUnitType
    {
        public const string Truck       = "Truck";        // Camión / tráiler
        public const string Van         = "Van";          // Furgoneta / van de carga
        public const string Car         = "Car";          // Automóvil
        public const string Pickup      = "Pickup";       // Pickup / camioneta
        public const string Motorcycle  = "Motorcycle";   // Moto / motocicleta
        public const string Forklift    = "Forklift";     // Montacargas
        public const string Trailer     = "Trailer";      // Remolque / trailer
        public const string Other       = "Other";        // Otro
    }

    /// <summary>Estado operacional de la unidad de transporte.</summary>
    public static class TransportUnitStatus
    {
        public const string Active       = "Active";       // Operativa
        public const string Maintenance  = "Maintenance";  // En mantenimiento
        public const string OutOfService = "OutOfService"; // Fuera de servicio
        public const string Retired      = "Retired";      // Retirada / dada de baja
    }

    /// <summary>Tipo de registro de mantenimiento.</summary>
    public static class TransportUnitMaintenanceType
    {
        public const string OilChange           = "OilChange";           // Cambio de aceite
        public const string TireChange          = "TireChange";          // Cambio de llantas
        public const string TireRotation        = "TireRotation";        // Rotación de llantas
        public const string BrakeService        = "BrakeService";        // Servicio de frenos
        public const string FilterChange        = "FilterChange";        // Cambio de filtros
        public const string BatteryReplacement  = "BatteryReplacement";  // Cambio de batería
        public const string Inspection          = "Inspection";          // Revisión técnica / inspección
        public const string Revision            = "Revision";            // Revisión general
        public const string Repair              = "Repair";              // Reparación
        public const string Wash                = "Wash";                // Lavado / limpieza
        public const string Insurance           = "Insurance";           // Renovación de seguro
        public const string Other               = "Other";               // Otro
    }

    // ============================================================
    // ENTIDAD: TransportUnit
    // ============================================================

    /// <summary>
    /// Representa una unidad de transporte de la flota de la compañía.
    /// Almacena datos de identificación, capacidad, dimensiones y estado operacional.
    /// </summary>
    [Table("transport_unit")]
    public class TransportUnit
    {
        [Key]
        [Column("id_transport_unit")]
        public int Id { get; set; }

        // ── Identificación ────────────────────────────────────────

        /// <summary>Código interno único (ej: TU-001)</summary>
        [Required]
        [MaxLength(30)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Número de placa / patente</summary>
        [Required]
        [MaxLength(30)]
        [Column("plate_number")]
        public string PlateNumber { get; set; } = string.Empty;

        /// <summary>Nombre descriptivo (ej: "Camión Refrigerado Norte")</summary>
        [Required]
        [MaxLength(200)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Tipo de unidad (Truck, Van, Car, Pickup, Motorcycle, etc.)</summary>
        [Required]
        [MaxLength(30)]
        [Column("unit_type")]
        public string UnitType { get; set; } = TransportUnitType.Truck;

        // ── Marca / Modelo ─────────────────────────────────────────

        /// <summary>FK a admin.vehicle_brand (catálogo central)</summary>
        [Column("id_vehicle_brand")]
        public int? IdVehicleBrand { get; set; }

        /// <summary>Nombre de marca desnormalizado para lectura rápida</summary>
        [MaxLength(100)][Column("brand_name")]
        public string? BrandName { get; set; }

        /// <summary>FK a admin.vehicle_model (catálogo central)</summary>
        [Column("id_vehicle_model")]
        public int? IdVehicleModel { get; set; }

        /// <summary>Nombre de modelo desnormalizado para lectura rápida</summary>
        [MaxLength(100)][Column("model_name")]
        public string? ModelName { get; set; }

        /// <summary>Año de fabricación</summary>
        [Column("year")]
        public int? Year { get; set; }

        /// <summary>Color en formato HEX (ej: #FF5733)</summary>
        [MaxLength(20)]
        [Column("color_hex")]
        public string? ColorHex { get; set; }

        /// <summary>Número de chasis / VIN — ÚNICO por compañía</summary>
        [MaxLength(50)]
        [Column("vin_number")]
        public string? VinNumber { get; set; }

        /// <summary>Número de motor — ÚNICO por compañía</summary>
        [MaxLength(50)]
        [Column("engine_number")]
        public string? EngineNumber { get; set; }

        /// <summary>Código de tipo de combustible (FK lógica a admin.fuel_type)</summary>
        [MaxLength(30)]
        [Column("fuel_type")]
        public string? FuelType { get; set; }

        // ── Capacidad y Dimensiones ────────────────────────────────

        /// <summary>Capacidad máxima de carga en kg</summary>
        [Column("max_load_kg")]
        public decimal? MaxLoadKg { get; set; }

        /// <summary>Capacidad volumétrica en m³</summary>
        [Column("max_volume_m3")]
        public decimal? MaxVolumeM3 { get; set; }

        /// <summary>Largo del área de carga en metros</summary>
        [Column("cargo_length_m")]
        public decimal? CargoLengthM { get; set; }

        /// <summary>Ancho del área de carga en metros</summary>
        [Column("cargo_width_m")]
        public decimal? CargoWidthM { get; set; }

        /// <summary>Alto del área de carga en metros</summary>
        [Column("cargo_height_m")]
        public decimal? CargoHeightM { get; set; }

        /// <summary>Número de paletas que caben</summary>
        [Column("pallet_capacity")]
        public int? PalletCapacity { get; set; }

        // ── Kilometraje y Operación ────────────────────────────────

        /// <summary>Kilometraje actual</summary>
        [Column("current_odometer_km")]
        public decimal CurrentOdometerKm { get; set; } = 0;

        /// <summary>Fecha del último registro de kilometraje</summary>
        [Column("last_odometer_date")]
        public DateOnly? LastOdometerDate { get; set; }

        /// <summary>Fecha de próxima revisión técnica / RITEVE</summary>
        [Column("next_inspection_date")]
        public DateOnly? NextInspectionDate { get; set; }

        /// <summary>Fecha de vencimiento del seguro</summary>
        [Column("insurance_expiry_date")]
        public DateOnly? InsuranceExpiredDate { get; set; }

        /// <summary>Nombre de la aseguradora</summary>
        [MaxLength(100)]
        [Column("insurance_company")]
        public string? InsuranceCompany { get; set; }

        /// <summary>Número de póliza de seguro</summary>
        [MaxLength(50)]
        [Column("insurance_policy_number")]
        public string? InsurancePolicyNumber { get; set; }

        // ── Asignación ─────────────────────────────────────────────

        /// <summary>FK a {schema}.driver — conductor asignado a la unidad</summary>
        [Column("id_driver")]
        public int? IdDriver { get; set; }

        /// <summary>Nombre del conductor desnormalizado para lectura rápida</summary>
        [MaxLength(300)]
        [Column("assigned_driver_name")]
        public string? AssignedDriverName { get; set; }

        /// <summary>ID de la bodega/base asignada (referencia a {schema}.warehouse)</summary>
        [Column("id_warehouse")]
        public int? IdWarehouse { get; set; }

        /// <summary>FK a {schema}.insurer — aseguradora de la unidad</summary>
        [Column("id_insurer")]
        public int? IdInsurer { get; set; }

        /// <summary>Nombre de aseguradora desnormalizado para lectura rápida</summary>
        [MaxLength(200)]
        [Column("insurer_name")]
        public string? InsurerName { get; set; }

        // ── Estado y Control ───────────────────────────────────────

        /// <summary>Estado (Active, Maintenance, OutOfService, Retired)</summary>
        [Required]
        [MaxLength(30)]
        [Column("status")]
        public string Status { get; set; } = TransportUnitStatus.Active;

        /// <summary>Notas adicionales</summary>
        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ── Auditoría ──────────────────────────────────────────────

        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(150)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = "SYSTEM";

        [Required]
        [MaxLength(150)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = "SYSTEM";

        [Column("rowpointer")]
        public Guid Rowpointer { get; set; } = Guid.NewGuid();

        // ── Navegación ─────────────────────────────────────────────

        /// <summary>Registros de mantenimiento de esta unidad</summary>
        public ICollection<TransportUnitMaintenance> MaintenanceRecords { get; set; } = new List<TransportUnitMaintenance>();
    }

    // ============================================================
    // ENTIDAD: TransportUnitMaintenance
    // ============================================================

    /// <summary>
    /// Registro de mantenimiento de una unidad de transporte (aceite, llantas, frenos, etc.).
    /// Cada registro captura tipo, costo, proveedor, kilometraje y próximo servicio.
    /// </summary>
    [Table("transport_unit_maintenance")]
    public class TransportUnitMaintenance
    {
        [Key]
        [Column("id_transport_unit_maintenance")]
        public int Id { get; set; }

        /// <summary>Unidad a la que corresponde el mantenimiento</summary>
        [Required]
        [Column("id_transport_unit")]
        public int IdTransportUnit { get; set; }

        // ── Tipo y Descripción ─────────────────────────────────────

        /// <summary>Tipo de mantenimiento (OilChange, TireChange, BrakeService, etc.)</summary>
        [Required]
        [MaxLength(50)]
        [Column("maintenance_type")]
        public string MaintenanceType { get; set; } = TransportUnitMaintenanceType.OilChange;

        /// <summary>Descripción detallada del trabajo realizado</summary>
        [Required]
        [MaxLength(1000)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        // ── Fechas y Kilometraje ───────────────────────────────────

        /// <summary>Fecha en que se realizó el mantenimiento</summary>
        [Required]
        [Column("maintenance_date")]
        public DateOnly MaintenanceDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        /// <summary>Kilometraje al momento del mantenimiento</summary>
        [Column("odometer_at_service_km")]
        public decimal? OdometerAtServiceKm { get; set; }

        /// <summary>Próximo mantenimiento por fecha</summary>
        [Column("next_service_date")]
        public DateOnly? NextServiceDate { get; set; }

        /// <summary>Próximo mantenimiento por kilometraje</summary>
        [Column("next_service_km")]
        public decimal? NextServiceKm { get; set; }

        // ── Costo y Proveedor ──────────────────────────────────────

        /// <summary>Costo total del mantenimiento</summary>
        [Column("cost")]
        public decimal? Cost { get; set; }

        /// <summary>Moneda del costo (USD, CRC, etc.)</summary>
        [MaxLength(10)]
        [Column("currency")]
        public string? Currency { get; set; }

        /// <summary>Nombre del taller / vendor</summary>
        [MaxLength(200)]
        [Column("vendor_name")]
        public string? VendorName { get; set; }

        /// <summary>Número de factura o comprobante</summary>
        [MaxLength(50)]
        [Column("invoice_number")]
        public string? InvoiceNumber { get; set; }

        // ── Estado ─────────────────────────────────────────────────

        /// <summary>¿La unidad estuvo fuera de servicio durante este mantenimiento?</summary>
        [Column("unit_out_of_service")]
        public bool VehicleOutOfService { get; set; } = false;

        /// <summary>Notas adicionales</summary>
        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        // ── Auditoría ──────────────────────────────────────────────

        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(150)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = "SYSTEM";

        [Required]
        [MaxLength(150)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = "SYSTEM";

        [Column("rowpointer")]
        public Guid Rowpointer { get; set; } = Guid.NewGuid();

        // ── Navegación ─────────────────────────────────────────────

        [ForeignKey(nameof(IdTransportUnit))]
        public TransportUnit? TransportUnit { get; set; }
    }
}
