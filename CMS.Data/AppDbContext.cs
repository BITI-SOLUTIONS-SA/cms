using CMS.Entities;
using CMS.Entities.Admin;
using CMS.Entities.Reports;
using LocationTypeCatalog = CMS.Entities.Operational.LocationType;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            IHttpContextAccessor httpContextAccessor
        ) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // ===== TABLAS DEL SISTEMA EXISTENTES =====
        public DbSet<Menu> Menus { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Page> Pages { get; set; }

        // ===== TABLAS DE CONFIGURACIÓN =====
        public DbSet<Company> Companies { get; set; }
        public DbSet<StoredProcedure> StoredProcedures { get; set; }

        // ===== TABLAS NUEVAS: SOPORTE SAAS (CATÁLOGOS) =====
        public DbSet<Language> Languages { get; set; }
        public DbSet<DataTranslation> DataTranslations { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<GeographicDivision1> GeographicDivisions1 { get; set; }
        public DbSet<GeographicDivision2> GeographicDivisions2 { get; set; }
        public DbSet<GeographicDivision3> GeographicDivisions3 { get; set; }
        public DbSet<GeographicDivision4> GeographicDivisions4 { get; set; }
        public DbSet<Gender> Genders { get; set; }

        // ===== TABLAS NUEVAS: LICENCIAMIENTO =====
        public DbSet<LicensePlan> LicensePlans { get; set; }
        public DbSet<SubscriptionStatus> SubscriptionStatuses { get; set; }
        public DbSet<BillingCycle> BillingCycles { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<SubscriptionFeature> SubscriptionFeatures { get; set; }

        // ===== TABLAS NUEVAS: FACTURACIÓN =====
        public DbSet<PaymentCategory> PaymentCategories { get; set; }
        public DbSet<PaymentProvider> PaymentProviders { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<PaymentStatus> PaymentStatuses { get; set; }
        public DbSet<BillingInvoice> BillingInvoices { get; set; }

        // ===== TABLAS NUEVAS: MULTI-TENANT AUTH =====
        public DbSet<UserCompany> UserCompanies { get; set; }
        public DbSet<UserCompanyRole> UserCompanyRoles { get; set; }
        public DbSet<UserCompanyPermission> UserCompanyPermissions { get; set; }
        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }

        // ===== TABLAS NUEVAS: CONFIGURACIÓN GLOBAL =====
        public DbSet<SystemConfig> SystemConfigs { get; set; }
        // NOTA: GlobalParameter movido a BD de compañía (ver CMS.Entities.Operational.GlobalParameter)

        // ===== TABLAS NUEVAS: CONSECUTIVOS POR COMPAÑÍA =====
        /// <summary>
        /// Consecutivos globales por compañía (container_number, etc.)
        /// </summary>
        public DbSet<CompanyConsecutive> CompanyConsecutives { get; set; }

        // ===== TABLAS CENTRALES: CATÁLOGOS COMPARTIDOS =====
        /// <summary>
        /// Unidades de medida - Tabla CENTRAL compartida por todas las compañías
        /// </summary>
        public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; }

        /// <summary>
        /// Tipos de bodega - Tabla CENTRAL compartida por todas las compañías
        /// </summary>
        public DbSet<WarehouseType> WarehouseTypes { get; set; }

        /// <summary>
        /// Catálogo parametrizable de tipos de documento electrónico (Hacienda CR v4.4).
        /// Tabla CENTRAL admin.electronic_document_type compartida por todas las compañías.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentTypeCatalog> ElectronicDocumentTypes { get; set; }

        /// <summary>
        /// Catálogo central de actividades económicas de Hacienda CR (CodigoActividad).
        /// Tabla CENTRAL admin.electronic_document_economic_activity compartida por todas las compañías.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentEconomicActivity> ElectronicDocumentEconomicActivities { get; set; }

        /// <summary>
        /// Tabla CENTRAL admin.electronic_document_sales_conditions compartida por todas las compañías.
        /// Catálogo de condiciones de venta (CondicionVenta Hacienda CR v4.4).
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentSalesCondition> ElectronicDocumentSalesConditions { get; set; }

        /// <summary>
        /// Tabla CENTRAL admin.electronic_document_identification_type compartida por todas las compañías.
        /// Catálogo de tipos de identificación (TipoIdentificacion Hacienda CR v4.4).
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentIdentificationType> ElectronicDocumentIdentificationTypes { get; set; }

        /// <summary>
        /// Tabla CENTRAL admin.customer_type compartida por todas las compañías.
        /// Catálogo de tipos de cliente (reemplaza el string customer_type de sinai.customer).
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.CustomerType> CustomerTypes { get; set; }

        /// <summary>
        /// Tabla CENTRAL admin.electronic_document_payment_methods compartida por todas las compañías.
        /// Catálogo de medios de pago (MedioPago Hacienda CR v4.4).
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentPaymentMethod> ElectronicDocumentPaymentMethods { get; set; }
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentDiscount> ElectronicDocumentDiscounts { get; set; }

        /// <summary>
        /// Catálogo de tipos de impuesto (CodigoImpuesto Hacienda CR v4.4).
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentTaxType> ElectronicDocumentTaxTypes { get; set; }

        /// <summary>
        /// Catálogo de códigos de tarifa del IVA (CodigoTarifa Hacienda CR v4.4).
        /// Tabla CENTRAL admin.electronic_document_tax_rate.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentTaxRate> ElectronicDocumentTaxRates { get; set; }

        /// <summary>
        /// Catálogo CAByS para facturación electrónica con relaciones a tarifa y tipo de impuesto.
        /// Tabla CENTRAL admin.electronic_document_cabys.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentCabys> ElectronicDocumentCabys { get; set; }

        /// <summary>
        /// Catálogo de códigos de unidad de medida (CodigoUnidadMedida Hacienda CR v4.4).
        /// Tabla CENTRAL admin.electronic_document_unit_of_measure.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentUnitOfMeasure> ElectronicDocumentUnitsOfMeasure { get; set; }

        /// <summary>
        /// Catálogo de tipos de documento de exoneración o de autorización
        /// (TipoDocumentoEX Hacienda CR v4.4).
        /// Tabla CENTRAL admin.electronic_document_exemption_authorization_type.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentExemptionAuthorizationType> ElectronicDocumentExemptionAuthorizationTypes { get; set; }

        /// <summary>
        /// Catálogo central de "Nombre de institución o dependencia que emitió la exoneración".
        /// Tabla CENTRAL admin.electronic_document_institution_department.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentInstitutionDepartment> ElectronicDocumentInstitutionDepartments { get; set; }

        /// <summary>
        /// Catálogo de versiones del esquema de documentos electrónicos (Hacienda CR).
        /// Tabla CENTRAL admin.electronic_document_version. Solo un registro es la versión vigente.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentVersion> ElectronicDocumentVersions { get; set; }

        /// <summary>
        /// Catálogo central de "Otros cargos" (OtroCargo Hacienda CR v4.4).
        /// Tabla CENTRAL admin.electronic_document_other_charges.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentOtherCharge> ElectronicDocumentOtherCharges { get; set; }

        /// <summary>
        /// Catálogo central de "Tipo documento de referencia" (InformacionReferencia Hacienda CR v4.4).
        /// Tabla CENTRAL admin.electronic_document_reference_type.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentReferenceType> ElectronicDocumentReferenceTypes { get; set; }

        /// <summary>
        /// Relación CAByS ↔ Tipo de impuesto permitido.
        /// Tabla CENTRAL admin.electronic_document_cabys_tax.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentCabysTax> ElectronicDocumentCabysTaxes { get; set; }

        /// <summary>
        /// Relación CAByS ↔ Naturaleza de descuento permitida.
        /// Tabla CENTRAL admin.electronic_document_cabys_discount.
        /// </summary>
        public DbSet<CMS.Entities.EInvoice.ElectronicDocumentCabysDiscount> ElectronicDocumentCabysDiscounts { get; set; }

        /// <summary>
        /// Tipos de localización - Tabla CENTRAL compartida por todas las compañías (admin.location_type)
        /// </summary>
        public DbSet<LocationTypeCatalog> LocationTypes { get; set; }

        // ===== TABLAS NUEVAS: SISTEMA DE REPORTES =====
        public DbSet<ReportCategory> ReportCategories { get; set; }
        public DbSet<ReportDefinition> ReportDefinitions { get; set; }
        public DbSet<ReportFilter> ReportFilters { get; set; }
        public DbSet<ReportColumn> ReportColumns { get; set; }
        public DbSet<ReportExecutionLog> ReportExecutionLogs { get; set; }
        public DbSet<ReportFavorite> ReportFavorites { get; set; }
        public DbSet<UserCompanyReport> UserCompanyReports { get; set; }

        // ===== TABLAS NUEVAS: SISTEMA DE AUDITORÍA =====
        public DbSet<AuditTableConfig> AuditTableConfigs { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        // ===== TABLAS NUEVAS: ACTIVIDAD Y CONFIGURACIÓN DE USUARIO =====
        public DbSet<UserActivityLog> UserActivityLogs { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }

        // ===== TABLAS NUEVAS: DOCUMENTACIÓN DEL SISTEMA =====
        public DbSet<SystemDocumentation> SystemDocumentations { get; set; }

        // ===== CATÁLOGOS FLEET MANAGEMENT (admin schema) =====
        public DbSet<TransportUnitTypeCatalog> TransportUnitTypes { get; set; }
        public DbSet<TransportUnitStatusCatalog> TransportUnitStatuses { get; set; }
        public DbSet<FuelTypeCatalog> FuelTypes { get; set; }
        public DbSet<TransportUnitBrand> TransportUnitBrands { get; set; }
        public DbSet<TransportUnitModel> TransportUnitModels { get; set; }

        // ===== CATÁLOGOS INVENTORY (admin schema) =====
        public DbSet<InventoryTransactionType> InventoryTransactionTypes { get; set; }
        public DbSet<InventoryTransactionStatus> InventoryTransactionStatuses { get; set; }

        // ===== CATÁLOGOS CONTABLES (admin schema) =====
        public DbSet<JournalEntryTypeOrigin> JournalEntryTypeOrigins { get; set; }
        public DbSet<TypeAccounting> TypeAccountings { get; set; }
        public DbSet<ChartOfAccountsType> ChartOfAccountsTypes { get; set; }
        public DbSet<JournalEntryClass> JournalEntryClasses { get; set; }
        public DbSet<JournalEntryStatus> JournalEntryStatuses { get; set; }

        // ===== CATÁLOGOS DE ENTIDADES Y CONSECUTIVOS (admin schema) =====
        /// <summary>
        /// Tipos de entidad del sistema - Tabla CENTRAL
        /// </summary>
        public DbSet<EntityType> EntityTypes { get; set; }

        /// <summary>
        /// Tipos de documentos por entidad - Tabla CENTRAL
        /// </summary>
        public DbSet<EntityDocument> EntityDocuments { get; set; }

        /// <summary>
        /// Catálogo CAByS (Bienes y Servicios, 13 díg.) - Tabla CENTRAL (admin.cabys)
        /// Compartido por todas las compañías para Facturación Electrónica CR v4.4
        /// </summary>
        public DbSet<CabysCode> CabysCodes { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Default schema (PostgreSQL, lowercase)
            modelBuilder.HasDefaultSchema("admin");

            // =====================================================================
            // CATÁLOGOS - CONFIGURACIÓN MÍNIMA
            // =====================================================================
            modelBuilder.Entity<Language>(entity =>
            {
                entity.HasKey(e => e.ID_LANGUAGE);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.IS_ISO_639_3).HasDefaultValue(true);
                entity.HasIndex(e => e.LANGUAGE_CODE).IsUnique().HasDatabaseName("uix_admin_language_code");
            });

            modelBuilder.Entity<Currency>(entity =>
            {
                entity.HasKey(e => e.ID_CURRENCY);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.IS_CRYPTO).HasDefaultValue(false);
                entity.Property(e => e.MINOR_UNIT).HasDefaultValue(2);
                entity.Property(e => e.ROUNDING_INCREMENT).HasDefaultValue(0);
                entity.Property(e => e.SORT_ORDER).HasDefaultValue(0);
                entity.HasIndex(e => e.CURRENCY_CODE).IsUnique().HasDatabaseName("uk_admin_currency_code");
            });

            modelBuilder.Entity<Country>(entity =>
            {
                entity.HasKey(e => e.ID_COUNTRY);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.HAS_ELECTRONIC_INVOICE).HasDefaultValue(false);
                entity.HasIndex(e => e.ISO2_CODE).IsUnique();
                entity.HasIndex(e => e.ISO3_CODE).IsUnique();
                entity.HasOne(e => e.Language)
                    .WithMany(l => l.Countries)
                    .HasForeignKey(e => e.ID_LANGUAGE)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Currency)
                    .WithMany(c => c.Countries)
                    .HasForeignKey(e => e.ID_CURRENCY)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<GeographicDivision1>(entity =>
            {
                entity.HasKey(e => e.IdGeographicDivision1);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasOne(e => e.Country)
                    .WithMany()
                    .HasForeignKey(e => e.IdCountry)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<GeographicDivision2>(entity =>
            {
                entity.HasKey(e => e.IdGeographicDivision2);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasOne(e => e.Country)
                    .WithMany()
                    .HasForeignKey(e => e.IdCountry)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Division1)
                    .WithMany()
                    .HasForeignKey(e => e.IdGeographicDivision1)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<GeographicDivision3>(entity =>
            {
                entity.HasKey(e => e.IdGeographicDivision3);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasOne(e => e.Country)
                    .WithMany()
                    .HasForeignKey(e => e.IdCountry)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Division1)
                    .WithMany()
                    .HasForeignKey(e => e.IdGeographicDivision1)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Division2)
                    .WithMany()
                    .HasForeignKey(e => e.IdGeographicDivision2)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<GeographicDivision4>(entity =>
            {
                entity.HasKey(e => e.IdGeographicDivision4);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasOne(e => e.Country)
                    .WithMany()
                    .HasForeignKey(e => e.IdCountry)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Division1)
                    .WithMany()
                    .HasForeignKey(e => e.IdGeographicDivision1)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Division2)
                    .WithMany()
                    .HasForeignKey(e => e.IdGeographicDivision2)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Division3)
                    .WithMany()
                    .HasForeignKey(e => e.IdGeographicDivision3)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Gender>(entity =>
            {
                entity.HasKey(e => e.ID_GENDER);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.HasIndex(e => e.GENDER_CODE).IsUnique().HasDatabaseName("uix_admin_gender_code");
            });

            modelBuilder.Entity<DataTranslation>(entity =>
            {
                entity.HasKey(e => e.ID_TRANSLATION);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.HasIndex(e => new { e.TABLE_NAME, e.PK_JSON, e.FIELD_NAME, e.ID_LANGUAGE }).IsUnique();
                entity.HasOne(e => e.Language)
                    .WithMany()
                    .HasForeignKey(e => e.ID_LANGUAGE)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================================================
            // LICENCIAMIENTO
            // =====================================================================
            modelBuilder.Entity<LicensePlan>(entity =>
            {
                entity.HasKey(e => e.ID_LICENSE_PLAN);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.SORT_ORDER).HasDefaultValue(0);
            });

            modelBuilder.Entity<SubscriptionStatus>(entity =>
            {
                entity.HasKey(e => e.ID_SUBSCRIPTION_STATUS);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
            });

            modelBuilder.Entity<BillingCycle>(entity =>
            {
                entity.HasKey(e => e.ID_BILLING_CYCLE);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
            });

            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.ID_SUBSCRIPTION);
                entity.Property(e => e.AUTO_RENEW).HasDefaultValue(true);
                entity.Property(e => e.CANCEL_AT_PERIOD_END).HasDefaultValue(false);
                entity.Property(e => e.TOTAL_AMOUNT_PAID_USD).HasDefaultValue(0);
                entity.Property(e => e.ID_SUBSCRIPTION_STATUS).HasDefaultValue(3);
                entity.HasOne(e => e.Company)
                    .WithMany(c => c.Subscriptions)
                    .HasForeignKey(e => e.ID_COMPANY)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.LicensePlan)
                    .WithMany(lp => lp.Subscriptions)
                    .HasForeignKey(e => e.ID_LICENSE_PLAN)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.SubscriptionStatus)
                    .WithMany(ss => ss.Subscriptions)
                    .HasForeignKey(e => e.ID_SUBSCRIPTION_STATUS)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.BillingCycle)
                    .WithMany(bc => bc.Subscriptions)
                    .HasForeignKey(e => e.ID_BILLING_CYCLE)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.PaymentMethod)
                    .WithMany(pm => pm.Subscriptions)
                    .HasForeignKey(e => e.ID_PAYMENT_METHOD)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SubscriptionFeature>(entity =>
            {
                entity.HasKey(e => e.ID_SUBSCRIPTION_FEATURE);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.USAGE_CURRENT).HasDefaultValue(0);
                entity.Property(e => e.USAGE_LIMIT).HasDefaultValue(1);
                entity.Property(e => e.USAGE_PERCENTAGE).HasDefaultValue(0);
                entity.HasOne(e => e.Subscription)
                    .WithMany(s => s.SubscriptionFeatures)
                    .HasForeignKey(e => e.ID_SUBSCRIPTION)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // =====================================================================
            // FACTURACIÓN
            // =====================================================================
            modelBuilder.Entity<PaymentCategory>(entity =>
            {
                entity.HasKey(e => e.ID_PAYMENT_CATEGORY);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.SORT_ORDER).HasDefaultValue(0);
            });

            modelBuilder.Entity<PaymentProvider>(entity =>
            {
                entity.HasKey(e => e.ID_PAYMENT_PROVIDER);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.SORT_ORDER).HasDefaultValue(0);
            });

            modelBuilder.Entity<PaymentStatus>(entity =>
            {
                entity.HasKey(e => e.ID_PAYMENT_STATUS);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.SORT_ORDER).HasDefaultValue(0);
            });

            modelBuilder.Entity<PaymentMethod>(entity =>
            {
                entity.HasKey(e => e.ID_PAYMENT_METHOD);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.SORT_ORDER).HasDefaultValue(0);
                entity.Property(e => e.IS_ONLINE).HasDefaultValue(false);
                entity.Property(e => e.REQUIRES_REFERENCE).HasDefaultValue(false);
                entity.Property(e => e.REQUIRES_CONFIRMATION).HasDefaultValue(false);
                entity.Property(e => e.ALLOWS_PARTIAL_PAYMENTS).HasDefaultValue(false);
                entity.Property(e => e.ALLOWS_REFUNDS).HasDefaultValue(false);
                entity.HasOne(e => e.PaymentCategory)
                    .WithMany(pc => pc.PaymentMethods)
                    .HasForeignKey(e => e.ID_PAYMENT_CATEGORY)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.PaymentProvider)
                    .WithMany(pp => pp.PaymentMethods)
                    .HasForeignKey(e => e.ID_PAYMENT_PROVIDER)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);
            });

            modelBuilder.Entity<BillingInvoice>(entity =>
            {
                entity.HasKey(e => e.ID_BILLING_INVOICE);
                entity.Property(e => e.TAX).HasDefaultValue(0);
                entity.Property(e => e.ID_PAYMENT_METHOD).HasDefaultValue(1);
                entity.Property(e => e.ID_PAYMENT_STATUS).HasDefaultValue(1);
                entity.Property(e => e.ID_CURRENCY).HasDefaultValue(141);
                entity.HasOne(e => e.Subscription)
                    .WithMany(s => s.BillingInvoices)
                    .HasForeignKey(e => e.ID_SUBSCRIPTION)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Currency)
                    .WithMany(c => c.BillingInvoices)
                    .HasForeignKey(e => e.ID_CURRENCY)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.PaymentStatus)
                    .WithMany(ps => ps.BillingInvoices)
                    .HasForeignKey(e => e.ID_PAYMENT_STATUS)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.PaymentMethod)
                    .WithMany(pm => pm.BillingInvoices)
                    .HasForeignKey(e => e.ID_PAYMENT_METHOD)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================================================
            // TABLAS EXISTENTES (SIN CAMBIOS SALVO MAPEO COMPANY)
            // =====================================================================
            modelBuilder.Entity<Company>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.ToTable("company", "admin");

                entity.Property(e => e.ID).HasColumnName("id_company");
                entity.Property(e => e.ParentCompanyId).HasColumnName("id_company_parent");
                entity.Property(e => e.IsTenant).HasColumnName("is_tenant");
                entity.Property(e => e.IdElectronicDocumentIdentificationType).HasColumnName("id_electronic_document_identification_type");
                entity.Property(e => e.IdCountry).HasColumnName("id_country");
                entity.Property(e => e.IdSubscriptionStatus).HasColumnName("id_subscription_status");
                entity.Property(e => e.COMPANY_SCHEMA).HasColumnName("company_schema");
                entity.Property(e => e.COMPANY_NAME).HasColumnName("company_name");
                entity.Property(e => e.COMPANY_TAX_ID).HasColumnName("company_tax_id");
                entity.Property(e => e.CONNECTION_STRING_DEVELOPMENT).HasColumnName("connection_string_development");
                entity.Property(e => e.CONNECTION_STRING_PRODUCTION).HasColumnName("connection_string_production");
                entity.Property(e => e.IS_PRODUCTION).HasColumnName("is_production");
                entity.Property(e => e.AZURE_AD_API_INSTANCE).HasColumnName("azure_ad_api_instance");
                entity.Property(e => e.AZURE_AD_API_TENANT_ID).HasColumnName("azure_ad_api_tenant_id");
                entity.Property(e => e.AZURE_AD_API_CLIENT_ID).HasColumnName("azure_ad_api_client_id");
                entity.Property(e => e.AZURE_AD_API_AUDIENCE).HasColumnName("azure_ad_api_audience");
                entity.Property(e => e.AZURE_AD_API_SCOPES).HasColumnName("azure_ad_api_scopes");
                entity.Property(e => e.AZURE_AD_UI_INSTANCE).HasColumnName("azure_ad_ui_instance");
                entity.Property(e => e.AZURE_AD_UI_TENANT_ID).HasColumnName("azure_ad_ui_tenant_id");
                entity.Property(e => e.AZURE_AD_UI_DOMAIN).HasColumnName("azure_ad_ui_domain");
                entity.Property(e => e.AZURE_AD_UI_CLIENT_ID).HasColumnName("azure_ad_ui_client_id");
                entity.Property(e => e.AZURE_AD_UI_CLIENT_SECRET).HasColumnName("azure_ad_ui_client_secret");
                entity.Property(e => e.AZURE_AD_UI_CALL_BACK_PATH).HasColumnName("azure_ad_ui_call_back_path");
                entity.Property(e => e.API_DEVELOPMENT_BASE_URL).HasColumnName("api_development_base_url");
                entity.Property(e => e.API_PRODUCTION_BASE_URL).HasColumnName("api_production_base_url");
                entity.Property(e => e.API_SCOPES).HasColumnName("api_scopes");

                entity.Property(e => e.HACIENDA_ENVIRONMENT).HasColumnName("hacienda_environment");
                entity.Property(e => e.HACIENDA_RECEPTION_URL).HasColumnName("hacienda_reception_url");
                entity.Property(e => e.HACIENDA_TOKEN_URL).HasColumnName("hacienda_token_url");
                entity.Property(e => e.HACIENDA_CLIENT_ID).HasColumnName("hacienda_client_id");
                entity.Property(e => e.HACIENDA_USERNAME).HasColumnName("hacienda_username");
                entity.Property(e => e.HACIENDA_PASSWORD).HasColumnName("hacienda_password");
                entity.Property(e => e.HACIENDA_CERT_SECRET_NAME).HasColumnName("hacienda_cert_secret_name");
                entity.Property(e => e.HACIENDA_CERT_PIN_SECRET_NAME).HasColumnName("hacienda_cert_pin_secret_name");
                entity.Property(e => e.HACIENDA_SCHEMA_VERSION).HasColumnName("hacienda_schema_version");
                entity.Property(e => e.HACIENDA_ALLOW_V43_NOTES).HasColumnName("hacienda_allow_v43_notes").HasDefaultValue(true);
                entity.Property(e => e.HACIENDA_CALLBACK_URL).HasColumnName("hacienda_callback_url");

                entity.Property(e => e.LOGGING_DEFAULT).HasColumnName("logging_default");
                entity.Property(e => e.LOGGING_ASP_NET_CORE).HasColumnName("logging_asp_net_core");
                entity.Property(e => e.LOGGING_EF_CORE).HasColumnName("logging_ef_core");

                // Auth settings
                entity.Property(e => e.USES_AZURE_AD).HasColumnName("uses_azure_ad").HasDefaultValue(true);
                entity.Property(e => e.MAX_FAILED_LOGIN_ATTEMPTS).HasColumnName("max_failed_login_attempts").HasDefaultValue(3);
                entity.Property(e => e.LOCKOUT_DURATION_MINUTES).HasColumnName("lockout_duration_minutes").HasDefaultValue(30);

                entity.Property(e => e.ALLOWED_HOSTS).HasColumnName("allowed_hosts");
                entity.Property(e => e.IS_ACTIVE).HasColumnName("is_active");
                entity.Property(e => e.DESCRIPTION).HasColumnName("description");
                entity.Property(e => e.CITY).HasColumnName("city");
                entity.Property(e => e.ADDRESS).HasColumnName("address");
                entity.Property(e => e.CONTACT_EMAIL).HasColumnName("contact_email");
                entity.Property(e => e.CONTACT_PHONE).HasColumnName("contact_phone");
                entity.Property(e => e.MANAGER_NAME).HasColumnName("manager_name");
                entity.Property(e => e.LOGO_URL).HasColumnName("logo_url");
                entity.Property(e => e.INDUSTRY).HasColumnName("industry");
                entity.Property(e => e.WEBSITE).HasColumnName("website");
                entity.Property(e => e.TRIAL_END_DATE).HasColumnName("trial_end_date");
                entity.Property(e => e.RowPointer).HasColumnName("rowpointer");
                entity.Property(e => e.RecordDate).HasColumnName("record_date");
                entity.Property(e => e.CreateDate).HasColumnName("createdate");
                entity.Property(e => e.CreatedBy).HasColumnName("created_by");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.IS_PRODUCTION).HasDefaultValue(false);

                entity.HasIndex(e => e.COMPANY_SCHEMA).IsUnique();
                entity.HasIndex(e => e.IS_ACTIVE);

                entity.HasOne(e => e.ParentCompany)
                    .WithMany(c => c.ChildCompanies)
                    .HasForeignKey(e => e.ParentCompanyId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                entity.HasOne(e => e.IdentificationType)
                    .WithMany()
                    .HasForeignKey(e => e.IdElectronicDocumentIdentificationType)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Country)
                    .WithMany()
                    .HasForeignKey(e => e.IdCountry)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                entity.HasOne(e => e.SubscriptionStatus)
                    .WithMany(ss => ss.Companies)
                    .HasForeignKey(e => e.IdSubscriptionStatus)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<StoredProcedure>(entity =>
            {
                entity.HasKey(e => e.ID_SP);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.HasIndex(e => e.SP_CODE).IsUnique().HasDatabaseName("uix_admin_stored_procedure_code");
                entity.HasIndex(e => e.IS_ACTIVE);
            });

            modelBuilder.Entity<Menu>(entity =>
            {
                entity.HasKey(e => e.ID_MENU);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.HasIndex(e => e.ID_PARENT);
            });

            modelBuilder.Entity<Page>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasIndex(e => e.PageUrl).IsUnique();
            });

            // ⭐ CONFIGURACIÓN EXPLÍCITA DE USER
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.ID_USER);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.ID_LANGUAGE).HasDefaultValue(1832);
                entity.HasIndex(e => e.USER_NAME).IsUnique();
                entity.HasIndex(e => e.EMAIL).IsUnique();

                // ⭐ CONFIGURAR RELACIÓN CON ROLE EXPLÍCITAMENTE
                //entity.HasOne(e => e.Role)
                //      .WithMany()
                //      .HasForeignKey(e => e.ID_ROLE)
                //      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Country)
                    .WithMany(c => c.Users)
                    .HasForeignKey(e => e.ID_COUNTRY)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Language)
                    .WithMany(l => l.Users)
                    .HasForeignKey(e => e.ID_LANGUAGE)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Gender)
                    .WithMany(g => g.Users)
                    .HasForeignKey(e => e.ID_GENDER)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(e => e.ID_ROLE);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.HasIndex(e => e.ROLE_NAME).IsUnique().HasDatabaseName("uix_admin_role_name");
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasKey(e => e.ID_PERMISSION);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.HasIndex(e => e.PERMISSION_KEY).IsUnique().HasDatabaseName("uix_admin_permission_code");
            });

            modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.PermissionId });

            // =====================================================================
            // MULTI-TENANT AUTH
            // =====================================================================
            modelBuilder.Entity<UserCompany>(entity =>
            {
                // ⚠️ PK COMPUESTA - coincide con la tabla real
                entity.HasKey(e => new { e.ID_USER, e.ID_COMPANY });

                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.IS_DEFAULT).HasDefaultValue(false);
                entity.Property(e => e.LOGIN_COUNT_AT_COMPANY).HasDefaultValue(0);

                entity.HasIndex(e => e.IS_ACTIVE);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.ID_USER)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Company)
                    .WithMany()
                    .HasForeignKey(e => e.ID_COMPANY)
                    .OnDelete(DeleteBehavior.Restrict);

                // ⚠️ ELIMINADO: Ya no existe ID_ROLE en UserCompany
                // Los roles se gestionan en user_company_role
            });

            // =====================================================================
            // SEGURIDAD POR COMPAÑÍA - NUEVAS TABLAS
            // =====================================================================
            modelBuilder.Entity<UserCompanyRole>(entity =>
            {
                // PK COMPUESTA: (id_user, id_company, id_role)
                entity.HasKey(e => new { e.ID_USER, e.ID_COMPANY, e.ID_ROLE });

                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);

                entity.HasIndex(e => new { e.ID_USER, e.ID_COMPANY });
                entity.HasIndex(e => e.IS_ACTIVE);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.ID_USER)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Company)
                    .WithMany()
                    .HasForeignKey(e => e.ID_COMPANY)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Role)
                    .WithMany()
                    .HasForeignKey(e => e.ID_ROLE)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserCompanyPermission>(entity =>
            {
                // PK COMPUESTA: (id_user, id_company, id_permission)
                entity.HasKey(e => new { e.ID_USER, e.ID_COMPANY, e.ID_PERMISSION });

                entity.Property(e => e.IS_ALLOWED).HasDefaultValue(true);

                entity.HasIndex(e => new { e.ID_USER, e.ID_COMPANY });
                entity.HasIndex(e => e.IS_ALLOWED);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.ID_USER)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Company)
                    .WithMany()
                    .HasForeignKey(e => e.ID_COMPANY)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Permission)
                    .WithMany()
                    .HasForeignKey(e => e.ID_PERMISSION)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PasswordResetRequest>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.Property(e => e.IS_USED).HasDefaultValue(false);

                entity.HasIndex(e => e.TOKEN_HASH);
                entity.HasIndex(e => new { e.ID_USER, e.IS_USED });

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.ID_USER)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SystemConfig>(entity =>
            {
                entity.HasKey(e => e.ID);
                entity.Property(e => e.IS_ACTIVE).HasDefaultValue(true);
                entity.Property(e => e.IS_ENCRYPTED).HasDefaultValue(false);
                entity.Property(e => e.IS_REQUIRED).HasDefaultValue(false);
                entity.Property(e => e.SORT_ORDER).HasDefaultValue(0);
                entity.Property(e => e.DATA_TYPE).HasDefaultValue("string");

                entity.HasIndex(e => new { e.CONFIG_CATEGORY, e.CONFIG_KEY }).IsUnique();
                entity.HasIndex(e => e.CONFIG_CATEGORY);
                entity.HasIndex(e => e.IS_ACTIVE);
            });

            // ⭐ CONFIGURACIÓN DE UserCompanyReport (Clave compuesta)
            modelBuilder.Entity<UserCompanyReport>(entity =>
            {
                // Clave primaria compuesta (no tiene id serial)
                entity.HasKey(e => new { e.UserId, e.CompanyId, e.ReportDefinitionId });

                entity.Property(e => e.IsAllowed).HasDefaultValue(true);
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CompanyId);
                entity.HasIndex(e => e.ReportDefinitionId);
                entity.HasIndex(e => new { e.UserId, e.CompanyId });

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Company)
                    .WithMany()
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ReportDefinition)
                    .WithMany()
                    .HasForeignKey(e => e.ReportDefinitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Ajustar defaults audit PostgreSQL
            ConfigureAuditDefaults(modelBuilder);

            // =====================================================================
            // CATÁLOGOS FLEET MANAGEMENT
            // =====================================================================
            modelBuilder.Entity<TransportUnitTypeCatalog>(entity =>
            {
                entity.ToTable("transport_unit_type", "admin");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
            });

            modelBuilder.Entity<TransportUnitStatusCatalog>(entity =>
            {
                entity.ToTable("transport_unit_status", "admin");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
            });

            modelBuilder.Entity<FuelTypeCatalog>(entity =>
            {
                entity.ToTable("fuel_type", "admin");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
            });

            modelBuilder.Entity<TransportUnitBrand>(entity =>
            {
                entity.ToTable("transport_unit_brand", "admin");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.HasMany(e => e.Models)
                      .WithOne(m => m.Brand)
                      .HasForeignKey(m => m.IdTransportUnitBrand)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Country)
                      .WithMany()
                      .HasForeignKey(e => e.IdCountry)
                      .HasPrincipalKey(c => c.ID_COUNTRY)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TransportUnitModel>(entity =>
            {
                entity.ToTable("transport_unit_model", "admin");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.HasOne(e => e.TransportUnitType)
                      .WithMany()
                      .HasForeignKey(e => e.IdTransportUnitType)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ===== CATÁLOGO CENTRAL: TIPOS DE LOCALIZACIÓN =====
            modelBuilder.Entity<LocationTypeCatalog>(entity =>
            {
                entity.ToTable("location_type", "admin");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("uix_admin_location_type_code");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("ix_admin_location_type_active");
                entity.HasIndex(e => e.SortOrder).HasDatabaseName("ix_admin_location_type_sort");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
            });

        }

        private static void ConfigureAuditDefaults(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var rowPointer = entityType.FindProperty("RowPointer");
                if (rowPointer != null && rowPointer.ClrType == typeof(Guid))
                    rowPointer.SetDefaultValueSql("gen_random_uuid()");

                var recordDate = entityType.FindProperty("RecordDate");
                if (recordDate != null)
                    recordDate.SetDefaultValueSql("now()");

                var createDate = entityType.FindProperty("CreateDate");
                if (createDate != null)
                    createDate.SetDefaultValueSql("now()");

                var createdBy = entityType.FindProperty("CreatedBy");
                if (createdBy != null)
                    createdBy.SetDefaultValueSql("current_user");

                var updatedBy = entityType.FindProperty("UpdatedBy");
                if (updatedBy != null)
                    updatedBy.SetDefaultValueSql("current_user");
            }
        }

        // 🔐 AUDITORÍA AUTOMÁTICA
        public override int SaveChanges()
        {
            ApplyAudit();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAudit();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyAudit()
        {
            // ⭐ Usar "SYSTEM" para auditoría (máximo 30 caracteres por restricción de BD)
            // El usuario de la aplicación se puede rastrear en logs si es necesario
            const string auditUser = "SYSTEM";

            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries()
                         .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
            {
                if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedBy"))
                {
                    entry.Property("UpdatedBy").CurrentValue = auditUser;
                }

                if (entry.State == EntityState.Added)
                {
                    if (entry.Properties.Any(p => p.Metadata.Name == "CreatedBy"))
                        entry.Property("CreatedBy").CurrentValue = auditUser;

                    if (entry.Properties.Any(p => p.Metadata.Name == "CreateDate"))
                        entry.Property("CreateDate").CurrentValue = now;
                }

                // ⭐ Convertir todos los DateTime a UTC (requerido por Npgsql con timestamp with time zone)
                foreach (var property in entry.Properties)
                {
                    if (property.CurrentValue is DateTime dateTime && dateTime.Kind == DateTimeKind.Unspecified)
                    {
                        property.CurrentValue = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                    }
                }
            }
        }
    }
}