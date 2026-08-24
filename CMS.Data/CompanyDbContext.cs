// ================================================================================
// ARCHIVO: CMS.Data/CompanyDbContext.cs
// PROPÓSITO: DbContext para acceder a la base de datos operacional de una compañía
// DESCRIPCIÓN: Cada compañía tiene su propia BD con el mismo nombre que company_schema
//              Este contexto permite acceder a datos operacionales como items, inventory, etc.
//              NOTA: UnitOfMeasure está en la BD central (admin.unit_of_measure)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-19
// ================================================================================

using CMS.Entities.Operational;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CMS.Data
{
    /// <summary>
    /// DbContext para la base de datos operacional de una compañía específica.
    /// Cada compañía tiene su propia base de datos con el nombre igual a COMPANY_SCHEMA.
    /// </summary>
    public class CompanyDbContext : DbContext
    {
        private readonly string _schema;

        public CompanyDbContext(DbContextOptions<CompanyDbContext> options, string schema) 
            : base(options)
        {
            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("El schema de la compañía no puede estar vacío", nameof(schema));

            _schema = schema;
        }

        /// <summary>
        /// CRÍTICO: Retornar un ID único por schema para evitar caché de modelos.
        /// Sin esto, EF Core cachea el modelo con el primer schema y lo reutiliza para todos.
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            // Forzar modelo único por schema (evita caché cross-schema)
            optionsBuilder.ReplaceService<IModelCacheKeyFactory, DynamicSchemaModelCacheKeyFactory>();
        }

        /// <summary>
        /// Factory para generar claves de caché únicas por schema.
        /// Esto asegura que cada schema tenga su propio modelo compilado.
        /// </summary>
        private class DynamicSchemaModelCacheKeyFactory : IModelCacheKeyFactory
        {
            public object Create(DbContext context, bool designTime)
            {
                if (context is CompanyDbContext companyContext)
                {
                    // Clave de caché única por schema
                    return (context.GetType(), companyContext._schema, designTime);
                }

                return (context.GetType(), designTime);
            }
        }

        // ===== TABLAS OPERACIONALES =====

        /// <summary>
        /// Artículos/Productos del inventario
        /// </summary>
        public DbSet<Item> Items { get; set; } = null!;

        /// <summary>
        /// Historial de impresiones de etiquetas
        /// </summary>
        public DbSet<LabelPrintHistory> LabelPrintHistory { get; set; } = null!;

        /// <summary>
        /// Clasificaciones de artículos (una sola tabla con classification_group 1-6)
        /// </summary>
        public DbSet<Classification> Classifications { get; set; } = null!;

        // ===== FILE MANAGEMENT =====

        /// <summary>
        /// Carpetas de archivos
        /// </summary>
        public DbSet<FileFolder> FileFolders { get; set; } = null!;

        /// <summary>
        /// Archivos/Documentos
        /// </summary>
        public DbSet<FileDocument> Files { get; set; } = null!;

        /// <summary>
        /// Versiones de archivos
        /// </summary>
        public DbSet<FileVersion> FileVersions { get; set; } = null!;

        /// <summary>
        /// Comentarios en archivos
        /// </summary>
        public DbSet<FileComment> FileComments { get; set; } = null!;

        /// <summary>
        /// Etiquetas de archivos
        /// </summary>
        public DbSet<FileTag> FileTags { get; set; } = null!;

        // ===== WAREHOUSE MANAGEMENT =====

        /// <summary>
        /// Bodegas físicas y lógicas (WMS)
        /// </summary>
        public DbSet<Warehouse> Warehouses { get; set; } = null!;

        /// <summary>
        /// Traslados de stock entre bodegas
        /// </summary>
        public DbSet<StockTransfer> StockTransfers { get; set; } = null!;

        /// <summary>
        /// Líneas de artículos de un traslado de stock
        /// </summary>
        public DbSet<StockTransferLine> StockTransferLines { get; set; } = null!;

        // ===== LOCALIZATION =====
        // Nota: LocationType es un catálogo CENTRAL en admin.location_type (BD cms).
        // No se expone aquí; acceder via AppDbContext.LocationTypes.

        /// <summary>
        /// Localizaciones físicas centralizadas (dirección, GPS, contacto)
        /// </summary>
        public DbSet<Location> Locations { get; set; } = null!;

        /// <summary>
        /// Rutas de distribución
        /// </summary>
        public DbSet<DistributionRoute> DistributionRoutes { get; set; } = null!;

        /// <summary>
        /// Paradas de rutas de distribución
        /// </summary>
        public DbSet<DistributionRouteStop> DistributionRouteStops { get; set; } = null!;

        // ===== INVENTORY MOVEMENTS =====

        /// <summary>
        /// Movimientos de inventario (traslados, ajustes, entradas, salidas)
        /// </summary>
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;

        /// <summary>
        /// Grupos de tránsito por bodega destino (solo movimientos TransitTransfer)
        /// </summary>
        public DbSet<InventoryTransactionWarehouseTransit> InventoryTransactionWarehouseTransits { get; set; } = null!;

        /// <summary>
        /// Líneas de movimientos de inventario
        /// </summary>
        public DbSet<InventoryTransactionLine> InventoryTransactionLines { get; set; } = null!;

        /// <summary>
        /// Saldo de existencias por artículo y bodega
        /// </summary>
        public DbSet<ExistenceWarehouse> ExistenceWarehouses { get; set; } = null!;

        // ===== FLEET MANAGEMENT =====

        /// <summary>
        /// Unidades de transporte de la flota (camiones, carros, motos, etc.)
        /// </summary>
        public DbSet<TransportUnit> TransportUnits { get; set; } = null!;

        /// <summary>
        /// Historial de mantenimiento de unidades de transporte
        /// </summary>
        public DbSet<TransportUnitMaintenance> TransportUnitMaintenances { get; set; } = null!;

        /// <summary>
        /// Aseguradoras registradas por la compañía
        /// </summary>
        public DbSet<Insurer> Insurers { get; set; } = null!;

        /// <summary>
        /// Departamentos organizacionales del módulo Human Resources
        /// </summary>
        public DbSet<Department> Departments { get; set; } = null!;

        /// <summary>
        /// Puestos/Cargos laborales del módulo Human Resources
        /// </summary>
        public DbSet<JobPosition> JobPositions { get; set; } = null!;

        /// <summary>
        /// Empleados del módulo Human Resources
        /// </summary>
        public DbSet<Employee> Employees { get; set; } = null!;

        // ===== SYSTEM CONFIGURATION =====

        /// <summary>
        /// Parámetros globales del sistema por módulo (menú)
        /// Configuración dinámica de comportamiento del sistema
        /// </summary>
        public DbSet<GlobalParameter> GlobalParameters { get; set; } = null!;

        /// <summary>
        /// Consecutivos de numeración de documentos
        /// Configuración de consecutivos por entidad y tipo de documento
        /// </summary>
        public DbSet<Consecutive> Consecutives { get; set; } = null!;

        // ===== ACCOUNTING TABLES =====

        /// <summary>
        /// Plan de Cuentas (Chart of Accounts)
        /// Catálogo jerárquico de cuentas contables
        /// </summary>
        public DbSet<ChartOfAccounts> ChartOfAccounts { get; set; } = null!;

        /// <summary>
        /// Centros de Costo (Cost Centers)
        /// Dimensiones analíticas para control de costos
        /// </summary>
        public DbSet<CostCenter> CostCenters { get; set; } = null!;

        /// <summary>
        /// Asientos de Diario (Journal Entries) - Encabezados
        /// Transacciones contables del sistema
        /// </summary>
        public DbSet<JournalEntry> JournalEntries { get; set; } = null!;

        /// <summary>
        /// Líneas de Asientos de Diario (Journal Entry Lines)
        /// Detalle de débitos y créditos de cada asiento
        /// </summary>
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; } = null!;

        /// <summary>
        /// Razones de Cancelación de Asientos de Diario
        /// Catálogo de razones predefinidas para cancelar asientos
        /// </summary>
        public DbSet<JournalEntryCancelReason> JournalEntryCancelReasons { get; set; } = null!;

        /// <summary>
        /// Catálogo de tipos de tasa de cambio (PER, SER, UER, etc.)
        /// </summary>
        public DbSet<ExchangeRate> ExchangeRates { get; set; } = null!;

        // ===== CRM & CLIENTES =====

        /// <summary>
        /// Maestro de clientes (CRM/Ventas).
        /// Solo datos operacionales, sin facturación electrónica.
        /// </summary>
        public DbSet<Customer> Customers { get; set; } = null!;

        /// <summary>
        /// Actividades económicas por cliente (facturación electrónica).
        /// Fuente de la actividad económica al emitir factura. Cada cliente debe
        /// tener al menos un registro (uno predeterminado).
        /// </summary>
        public DbSet<CustomerEconomicActivity> CustomerEconomicActivities { get; set; } = null!;

        // ===== PURCHASING / ACCOUNTS PAYABLE =====

        /// <summary>
        /// Maestro de proveedores (Purchasing/AP).
        /// Proveedores a los que compramos bienes/servicios.
        /// </summary>
        public DbSet<Vendor> Vendors { get; set; } = null!;

        /// <summary>
        /// Actividades económicas por proveedor (vendor).
        /// Relación con el catálogo central de actividades económicas.
        /// </summary>
        public DbSet<VendorEconomicActivity> VendorEconomicActivities { get; set; } = null!;

        // ===== FACTURACIÓN ELECTRÓNICA CR v4.4 (HACIENDA-CORE) =====

        /// <summary>
        /// Credenciales completas de facturación electrónica (emisores y receptores).
        /// Contiene TODA la información necesaria para emisión/recepción.
        /// </summary>
        public DbSet<CustomerBillingCredential> CustomerBillingCredentials { get; set; } = null!;

        /// <summary>Consecutivos fiscales por emisor/sucursal/terminal/tipo/versión.</summary>
        public DbSet<ElectronicDocumentConsecutive> ElectronicDocumentConsecutives { get; set; } = null!;

        /// <summary>Comprobantes electrónicos (cabecera).</summary>
        public DbSet<ElectronicDocument> ElectronicDocuments { get; set; } = null!;

        /// <summary>Líneas de detalle de comprobantes.</summary>
        public DbSet<ElectronicDocumentLine> ElectronicDocumentLines { get; set; } = null!;

        /// <summary>Impuestos por línea de comprobante.</summary>
        public DbSet<ElectronicDocumentTax> ElectronicDocumentTaxes { get; set; } = null!;

        /// <summary>Descuentos individuales por línea de comprobante.</summary>
        public DbSet<ElectronicDocumentDiscountLine> ElectronicDocumentDiscountLines { get; set; } = null!;

        /// <summary>Referencias a documentos previos (NC/ND/REP).</summary>
        public DbSet<ElectronicDocumentReference> ElectronicDocumentReferences { get; set; } = null!;

        /// <summary>Otros cargos por documento (nodo &lt;OtrosCargos&gt; v4.4).</summary>
        public DbSet<ElectronicDocumentOtherChargeLine> ElectronicDocumentOtherChargeLines { get; set; } = null!;

        /// <summary>Cola de reintentos para resiliencia ante Hacienda.</summary>
        public DbSet<EInvoiceRetryQueue> EInvoiceRetryQueue { get; set; } = null!;

        /// <summary>Bitácora de seguimiento del proceso de emisión.</summary>
        public DbSet<ElectronicDocumentLog> ElectronicDocumentLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurar el esquema dinámico para todas las entidades
            modelBuilder.HasDefaultSchema(_schema);

            // Configurar la tabla Item
            modelBuilder.Entity<Item>(entity =>
            {
                entity.ToTable("item", _schema);

                // Índices
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.Barcode);
                entity.HasIndex(e => e.IdClassification1);
                entity.HasIndex(e => e.IdClassification2);
                entity.HasIndex(e => e.IdElectronicDocumentUnitOfMeasure);
                entity.HasIndex(e => e.IsActive);
            });

            // Configurar la tabla LabelPrintHistory
            modelBuilder.Entity<LabelPrintHistory>(entity =>
            {
                entity.ToTable("label_print_history", _schema);

                // Índices
                entity.HasIndex(e => e.IdItem);
                entity.HasIndex(e => e.ItemCode);
                entity.HasIndex(e => e.PrintDate);
                entity.HasIndex(e => e.PrintedBy);
            });

            // Configurar la tabla Classification (única tabla con classification_group)
            modelBuilder.Entity<Classification>(entity =>
            {
                entity.ToTable("classification", _schema);

                // Índice único compuesto: code + classification_group
                entity.HasIndex(e => new { e.Code, e.ClassificationGroup }).IsUnique();
                entity.HasIndex(e => e.ClassificationGroup);
                entity.HasIndex(e => e.IsActive);
            });

            // ===== FILE MANAGEMENT TABLES =====

            // Configurar la tabla FileFolder
            modelBuilder.Entity<FileFolder>(entity =>
            {
                entity.ToTable("file_folder", _schema);
                entity.HasKey(e => e.IdFileFolder);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.ParentId);
                entity.HasIndex(e => e.Path);
                entity.HasIndex(e => e.CategoryCode);
                entity.HasIndex(e => e.IsActive);

                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configurar la tabla File (FileDocument)
            modelBuilder.Entity<FileDocument>(entity =>
            {
                entity.ToTable("file", _schema);
                entity.HasKey(e => e.IdFile);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.Uuid).IsUnique();
                entity.HasIndex(e => e.IdFileFolder);
                entity.HasIndex(e => e.CategoryCode);
                entity.HasIndex(e => e.FileExtension);
                entity.HasIndex(e => e.FileHash);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.DeletedAt);
                entity.HasIndex(e => e.IsLocked);
                entity.HasIndex(e => new { e.RelatedEntityType, e.RelatedEntityId });

                entity.HasOne(e => e.Folder)
                    .WithMany(e => e.Files)
                    .HasForeignKey(e => e.IdFileFolder)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configurar la tabla FileVersion
            modelBuilder.Entity<FileVersion>(entity =>
            {
                entity.ToTable("file_version", _schema);
                entity.HasKey(e => e.IdFileVersion);
                entity.HasIndex(e => e.IdFile);
                entity.HasIndex(e => new { e.IdFile, e.VersionNumber }).IsUnique();
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.File)
                    .WithMany(e => e.Versions)
                    .HasForeignKey(e => e.IdFile)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configurar la tabla FileComment
            modelBuilder.Entity<FileComment>(entity =>
            {
                entity.ToTable("file_comment", _schema);
                entity.HasKey(e => e.IdFileComment);
                entity.HasIndex(e => e.IdFile);
                entity.HasIndex(e => e.ParentId);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.File)
                    .WithMany(e => e.Comments)
                    .HasForeignKey(e => e.IdFile)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Replies)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configurar la tabla FileTag
            modelBuilder.Entity<FileTag>(entity =>
            {
                entity.ToTable("file_tag", _schema);
                entity.HasKey(e => e.IdFileTag);
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // ===== WAREHOUSE =====

            modelBuilder.Entity<Warehouse>(entity =>
            {
                entity.ToTable("warehouse", _schema);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.WarehouseType);
                entity.HasIndex(e => e.WarehouseLevel);
                entity.HasIndex(e => e.IdParentWarehouse);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.IsDefault);
                entity.HasIndex(e => e.IdLocation);
                entity.HasIndex(e => e.IdTransportUnit);
            });

            modelBuilder.Entity<StockTransfer>(entity =>
            {
                entity.ToTable("stock_transfer", _schema);
                entity.HasIndex(e => e.TransferNumber).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.TransferDate);
                entity.HasIndex(e => e.IdWarehouseOrigin);
                entity.HasIndex(e => e.IdWarehouseDest);
                entity.HasIndex(e => e.RequestedBy);
            });

            modelBuilder.Entity<StockTransferLine>(entity =>
            {
                entity.ToTable("stock_transfer_line", _schema);
                entity.HasIndex(e => e.IdStockTransfer);
                entity.HasIndex(e => e.IdItem);
                entity.HasIndex(e => new { e.IdStockTransfer, e.LineNumber }).IsUnique();
            });

            // ===== LOCALIZATION =====
            // Nota: LocationType está en AppDbContext (admin.location_type, BD central cms).
            // id_location_type en location es FK lógica cross-DB.

            modelBuilder.Entity<Location>(entity =>
            {
                entity.ToTable("location", _schema);
                entity.HasIndex(e => e.IdLocationType);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.IdCountry);
                entity.HasIndex(e => e.IdGeographicDivision1);
                entity.HasIndex(e => e.IdGeographicDivision2);
                entity.HasIndex(e => e.IdGeographicDivision3);
                entity.HasIndex(e => e.IdGeographicDivision4);
                // IdLocationType es FK lógica cross-DB a admin.location_type (BD cms)
                // No se configura HasOne porque LocationType vive en AppDbContext
            });

            // Configurar la tabla FileFileTag (relación muchos a muchos)
            modelBuilder.Entity<FileFileTag>(entity =>
            {
                entity.ToTable("file_file_tag", _schema);
                entity.HasKey(e => new { e.IdFile, e.IdFileTag });

                entity.HasOne(e => e.File)
                    .WithMany()
                    .HasForeignKey(e => e.IdFile)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Tag)
                    .WithMany()
                    .HasForeignKey(e => e.IdFileTag)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== DISTRIBUTION ROUTES =====

            modelBuilder.Entity<DistributionRoute>(entity =>
            {
                entity.ToTable("distribution_route", _schema);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Frequency);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.IdOriginWarehouse);
            });

            modelBuilder.Entity<DistributionRouteStop>(entity =>
            {
                entity.ToTable("distribution_route_stop", _schema);
                entity.HasIndex(e => e.IdRoute);
                entity.HasIndex(e => e.StopOrder);
                entity.HasIndex(e => new { e.IdRoute, e.StopOrder }).IsUnique();
            });

            // ===== INVENTORY MOVEMENTS =====

            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.ToTable("inventory_transaction", _schema);
                entity.HasIndex(e => e.TransactionNumber).IsUnique();
                entity.HasIndex(e => e.IdInventoryTransactionType);
                entity.HasIndex(e => e.IdInventoryTransactionStatus);
                entity.HasIndex(e => e.TransactionDate);
                entity.HasIndex(e => e.IdWarehouseOrigin);
                entity.HasIndex(e => e.IdWarehouseDest);
                entity.HasIndex(e => e.IsTransitTransfer);
                entity.HasIndex(e => e.SecuritySeal).IsUnique().HasFilter("security_seal IS NOT NULL");

                // FK real a sinai.journal_entry (misma BD/schema de compañía)
                entity.HasIndex(e => e.IdJournalEntry)
                      .HasDatabaseName($"ix_{_schema}_inventory_transaction_journal_entry");
                entity.HasOne(e => e.JournalEntry)
                      .WithMany()
                      .HasForeignKey(e => e.IdJournalEntry)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(true);
            });

            modelBuilder.Entity<InventoryTransactionWarehouseTransit>(entity =>
            {
                entity.ToTable("inventory_transaction_warehouse_transit", _schema);
                entity.HasIndex(e => e.IdInventoryTransaction);
                entity.HasIndex(e => e.LineStatus);
                entity.HasIndex(e => e.IdWarehouseDestLine);
                entity.HasIndex(e => new { e.IdInventoryTransaction, e.LineNumber }).IsUnique();
            });

            modelBuilder.Entity<InventoryTransactionLine>(entity =>
            {
                entity.ToTable("inventory_transaction_line", _schema);
                entity.HasIndex(e => e.IdInventoryTransaction);
                entity.HasIndex(e => e.IdItem);
                entity.HasIndex(e => e.IdInventoryTransactionWarehouseTransit);
                entity.HasIndex(e => new { e.IdInventoryTransaction, e.LineNumber }).IsUnique();
            });

            modelBuilder.Entity<ExistenceWarehouse>(entity =>
            {
                entity.ToTable("existence_warehouse", _schema);
                entity.HasIndex(e => e.IdItem);
                entity.HasIndex(e => e.IdWarehouse);
                entity.HasIndex(e => new { e.IdItem, e.IdWarehouse }).IsUnique();
            });

            // ===== FLEET MANAGEMENT =====

            modelBuilder.Entity<TransportUnit>(entity =>
            {
                entity.ToTable("transport_unit", _schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_transport_unit");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.PlateNumber).IsUnique();
                entity.HasIndex(e => e.UnitType);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.IdWarehouse);
                // VIN y Número de motor únicos por compañía
                entity.HasIndex(e => e.VinNumber).IsUnique().HasFilter($"\"{_schema}\".transport_unit.vin_number IS NOT NULL");
                entity.HasIndex(e => e.EngineNumber).IsUnique().HasFilter($"\"{_schema}\".transport_unit.engine_number IS NOT NULL");
                entity.HasIndex(e => e.IdDriver);
                entity.HasIndex(e => e.IdInsurer);
            });

            modelBuilder.Entity<TransportUnitMaintenance>(entity =>
            {
                entity.ToTable("transport_unit_maintenance", _schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_transport_unit_maintenance");
                entity.HasIndex(e => e.IdTransportUnit);
                entity.HasIndex(e => e.MaintenanceType);
                entity.HasIndex(e => e.MaintenanceDate);

                entity.HasOne(e => e.TransportUnit)
                    .WithMany(e => e.MaintenanceRecords)
                    .HasForeignKey(e => e.IdTransportUnit)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Insurer>(entity =>
            {
                entity.ToTable("insurer", _schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_insurer");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.IsActive);
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.ToTable("department", _schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_department");
                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName($"uix_{_schema}_department_code");
                entity.HasIndex(e => e.IsActive).HasDatabaseName($"ix_{_schema}_department_active");
                entity.HasIndex(e => e.SortOrder).HasDatabaseName($"ix_{_schema}_department_sort");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
            });

            modelBuilder.Entity<JobPosition>(entity =>
            {
                entity.ToTable("job_position", _schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_job_position");
                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName($"uix_{_schema}_job_position_code");
                entity.HasIndex(e => e.IsActive).HasDatabaseName($"ix_{_schema}_job_position_active");
                entity.HasIndex(e => e.SortOrder).HasDatabaseName($"ix_{_schema}_job_position_sort");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.ToTable("employee", _schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_employee");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.IdNumber).IsUnique();
                entity.HasIndex(e => e.IdSystemUser);
                entity.HasIndex(e => e.IdDepartment);
                entity.HasIndex(e => e.IdJobPosition);
                entity.HasIndex(e => e.IdLocation);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.HireDate);
                // FK lógicas cross-DB: ignorar propiedades de display
                entity.Ignore(e => e.DepartmentName);
                entity.Ignore(e => e.DepartmentIcon);
                entity.Ignore(e => e.DepartmentColor);
                entity.Ignore(e => e.JobPositionName);
                entity.Ignore(e => e.LocationDisplay);
                entity.Ignore(e => e.TypeIdDescription);
                entity.Ignore(e => e.GenderDescription);
                entity.Ignore(e => e.CurrencyCode);
                entity.Ignore(e => e.CurrencySymbol);
            });

            // ===== SYSTEM CONFIGURATION =====

            modelBuilder.Entity<GlobalParameter>(entity =>
            {
                entity.ToTable("global_parameter", _schema);
                entity.HasKey(e => e.ID);
                entity.Property(e => e.ID).HasColumnName("id_global_parameter");
                entity.HasIndex(e => new { e.MenuId, e.Code })
                      .IsUnique()
                      .HasDatabaseName($"uq_{_schema}_global_parameter_code");
                entity.HasIndex(e => e.MenuId).HasDatabaseName($"ix_{_schema}_global_parameter_menu");
                entity.HasIndex(e => e.Category).HasDatabaseName($"ix_{_schema}_global_parameter_category");
                entity.HasIndex(e => e.IsActive).HasDatabaseName($"ix_{_schema}_global_parameter_active");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.IsSystem).HasDefaultValue(false);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
            });

            // ===== ACCOUNTING TABLES =====

            modelBuilder.Entity<ChartOfAccounts>(entity =>
            {
                entity.ToTable("chart_of_accounts", _schema);
                entity.HasKey(e => e.IdChartOfAccounts);
                entity.Property(e => e.IdChartOfAccounts).HasColumnName("id_chart_of_accounts");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.IdChartOfAccountsType);
                entity.HasIndex(e => e.IdParentAccount);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.IsDetail);
            });

            modelBuilder.Entity<CostCenter>(entity =>
            {
                entity.ToTable("cost_center", _schema);
                entity.HasKey(e => e.IdCostCenter);
                entity.Property(e => e.IdCostCenter).HasColumnName("id_cost_center");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.CostCenterType);
                entity.HasIndex(e => e.IdParentCostCenter);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.IsPostingAllowed);
            });

            modelBuilder.Entity<JournalEntry>(entity =>
            {
                entity.ToTable("journal_entry", _schema);
                entity.HasKey(e => e.IdJournalEntry);
                entity.Property(e => e.IdJournalEntry).HasColumnName("id_journal_entry");
                entity.HasIndex(e => e.EntryNumber).IsUnique();
                entity.HasIndex(e => e.IdTypeAccounting);
                entity.HasIndex(e => e.IdJournalEntryClass);
                entity.HasIndex(e => e.IdJournalEntryStatus);
                entity.HasIndex(e => e.PostingDate);
                entity.HasIndex(e => e.IdJournalEntryCancelReason);
                entity.Ignore(e => e.Lines); // Navigation property

                // Relación con JournalEntryCancelReason
                entity.HasOne(e => e.CancelReason)
                      .WithMany(r => r.JournalEntries)
                      .HasForeignKey(e => e.IdJournalEntryCancelReason)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<JournalEntryCancelReason>(entity =>
            {
                entity.ToTable("journal_entry_cancel_reason", _schema);
                entity.HasKey(e => e.IdJournalEntryCancelReason);
                entity.Property(e => e.IdJournalEntryCancelReason).HasColumnName("id_journal_entry_cancel_reason");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.SortOrder);
            });

            modelBuilder.Entity<JournalEntryLine>(entity =>
            {
                entity.ToTable("journal_entry_line", _schema);
                entity.HasKey(e => new { e.IdJournalEntry, e.IdJournalEntryLine });
                entity.Property(e => e.IdJournalEntry).HasColumnName("id_journal_entry");
                entity.Property(e => e.IdJournalEntryLine).HasColumnName("id_journal_entry_line");
                entity.HasIndex(e => e.IdChartOfAccounts);
                // FK real a sinai.cost_center
                entity.HasOne(e => e.CostCenter)
                      .WithMany()
                      .HasForeignKey(e => e.IdCostCenter)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(true);
                entity.HasIndex(e => e.IdCostCenter)
                      .HasDatabaseName($"ix_{_schema}_journal_entry_line_cost_center");
                entity.HasIndex(e => e.IdJournalEntryTypeOrigin)
                      .HasDatabaseName($"ix_{_schema}_journal_entry_line_type_origin");
                entity.HasIndex(e => e.IdDocumentOrigin)
                      .HasDatabaseName($"ix_{_schema}_journal_entry_line_document_origin");
            });

            modelBuilder.Entity<ExchangeRate>(entity =>
            {
                entity.ToTable("exchange_rate", _schema);
                entity.HasKey(e => e.IdExchangeRate);
                entity.Property(e => e.IdExchangeRate).HasColumnName("id_exchange_rate");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.DisplayOrder);
            });

            // ===== CUSTOMER & BILLING CREDENTIALS =====

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("customer", _schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_customer");
                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName($"uix_{_schema}_customer_code");
                entity.HasIndex(e => e.Identification).HasDatabaseName($"ix_{_schema}_customer_identification");
                entity.HasIndex(e => e.Email).HasDatabaseName($"ix_{_schema}_customer_email");
                entity.HasIndex(e => e.IsActive).HasDatabaseName($"ix_{_schema}_customer_active");
                entity.HasIndex(e => e.IdParentCustomer).HasDatabaseName($"ix_{_schema}_customer_parent");
                entity.HasIndex(e => e.IdCustomerType).HasDatabaseName($"ix_{_schema}_customer_type");

                // Self-referencing relationship (parent/child customers)
                entity.HasOne(e => e.ParentCustomer)
                      .WithMany(e => e.ChildCustomers)
                      .HasForeignKey(e => e.IdParentCustomer)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);
            });

            modelBuilder.Entity<Vendor>(entity =>
            {
                entity.ToTable("vendor", _schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_vendor");
                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName($"uix_{_schema}_vendor_code");
                entity.HasIndex(e => e.Identification).HasDatabaseName($"ix_{_schema}_vendor_identification");
                entity.HasIndex(e => e.Email).HasDatabaseName($"ix_{_schema}_vendor_email");
                entity.HasIndex(e => e.IsActive).HasDatabaseName($"ix_{_schema}_vendor_active");
                entity.HasIndex(e => e.IdParentVendor).HasDatabaseName($"ix_{_schema}_vendor_parent");
                entity.HasIndex(e => e.VendorType).HasDatabaseName($"ix_{_schema}_vendor_type");

                // Self-referencing relationship (parent/child vendors)
                entity.HasOne(e => e.ParentVendor)
                      .WithMany(e => e.ChildVendors)
                      .HasForeignKey(e => e.IdParentVendor)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);
            });

            modelBuilder.Entity<VendorEconomicActivity>(entity =>
            {
                entity.ToTable("vendor_economic_activity", _schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_vendor_economic_activity");

                entity.HasIndex(e => e.IdVendor).HasDatabaseName($"ix_{_schema}_vend_econ_act_vendor");
                entity.HasIndex(e => e.IdElectronicDocumentEconomicActivity).HasDatabaseName($"ix_{_schema}_vend_econ_act_edea");

                // Un único registro predeterminado por vendor
                entity.HasIndex(e => e.IdVendor)
                      .IsUnique()
                      .HasFilter("is_default = true")
                      .HasDatabaseName($"uix_{_schema}_vend_econ_act_default");

                entity.HasOne(e => e.Vendor)
                      .WithMany()
                      .HasForeignKey(e => e.IdVendor)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CustomerBillingCredential>(entity =>
            {
                entity.ToTable("customer_billing_credential", _schema);
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id_customer_billing_credential");

                // Unique constraint per customer/environment
                entity.HasIndex(e => new { e.IdCustomer, e.Environment })
                      .IsUnique()
                      .HasDatabaseName($"uq_{_schema}_customer_billing_credential_env");

                // Índice único para company owner
                entity.HasIndex(e => e.IsCompanyOwner)
                      .IsUnique()
                      .HasFilter("is_company_owner = true AND is_active = true")
                      .HasDatabaseName($"uix_{_schema}_cbc_company_owner");

                entity.HasIndex(e => e.IdCustomer).HasDatabaseName($"ix_{_schema}_cbc_customer");
                entity.HasIndex(e => e.Environment).HasDatabaseName($"ix_{_schema}_cbc_env");
                entity.HasIndex(e => e.IsActive).HasDatabaseName($"ix_{_schema}_cbc_active");
                entity.HasIndex(e => e.IsIssuer).HasDatabaseName($"ix_{_schema}_cbc_issuer");
                entity.HasIndex(e => e.Identification).HasDatabaseName($"ix_{_schema}_cbc_identification");
                entity.HasIndex(e => e.Email).HasDatabaseName($"ix_{_schema}_cbc_email");

                // FK to Customer (NULLABLE)
                entity.HasOne(e => e.Customer)
                      .WithMany()
                      .HasForeignKey(e => e.IdCustomer)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);
            });
        }
    }
}
