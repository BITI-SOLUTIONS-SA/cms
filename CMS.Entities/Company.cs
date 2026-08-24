// ================================================================================
// ARCHIVO: CMS.Entities/Company.cs
// PROPÓSITO: Entidad que representa la configuración de una compañía
// DESCRIPCIÓN: Almacena todas las configuraciones que antes estaban en appsettings.json
//              Permite tener configuraciones multi-tenant por compañía
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2025-12-19
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CMS.Entities.EInvoice;

namespace CMS.Entities
{
    /// <summary>
    /// Entidad que representa una compañía, tenant o subsidiaria en el sistema.
    /// Soporta estructura jerárquica multi-nivel con gestión de suscripción integrada.
    /// </summary>
    [Table("company", Schema = "admin")]
    public class Company : IAuditableEntity
    {
        // ===== IDENTIFICACIÓN =====
        [Key]
        [Column("id_company")]
        public int ID { get; set; }

        [Column("id_company_parent")]
        public int ParentCompanyId { get; set; }

        [Column("is_tenant")]
        public bool IsTenant { get; set; }

        [Column("id_electronic_document_identification_type")]
        public int IdElectronicDocumentIdentificationType { get; set; }

        [Column("company_schema")]
        [Required]
        [MaxLength(10)]
        public string COMPANY_SCHEMA { get; set; } = default!;

        [Column("company_name")]
        [Required]
        [MaxLength(200)]
        public string COMPANY_NAME { get; set; } = default!;

        [Column("company_tax_id")]
        [MaxLength(50)]
        public string? COMPANY_TAX_ID { get; set; }

        // ===== CADENAS DE CONEXIÓN =====
        [Column("connection_string_development")]
        [MaxLength(500)]
        public string? CONNECTION_STRING_DEVELOPMENT { get; set; }

        [Column("connection_string_production")]
        [MaxLength(500)]
        public string? CONNECTION_STRING_PRODUCTION { get; set; }

        [Column("is_production")]
        public bool IS_PRODUCTION { get; set; }

        // ===== AZURE AD - API =====
        [Column("azure_ad_api_instance")]
        [MaxLength(200)]
        public string? AZURE_AD_API_INSTANCE { get; set; }

        [Column("azure_ad_api_tenant_id")]
        [MaxLength(50)]
        public string? AZURE_AD_API_TENANT_ID { get; set; }

        [Column("azure_ad_api_client_id")]
        [MaxLength(50)]
        public string? AZURE_AD_API_CLIENT_ID { get; set; }

        [Column("azure_ad_api_audience")]
        [MaxLength(200)]
        public string? AZURE_AD_API_AUDIENCE { get; set; }

        [Column("azure_ad_api_scopes")]
        [MaxLength(500)]
        public string? AZURE_AD_API_SCOPES { get; set; }

        // ===== AZURE AD - UI =====
        [Column("azure_ad_ui_instance")]
        [MaxLength(200)]
        public string? AZURE_AD_UI_INSTANCE { get; set; }

        [Column("azure_ad_ui_tenant_id")]
        [MaxLength(50)]
        public string? AZURE_AD_UI_TENANT_ID { get; set; }

        [Column("azure_ad_ui_domain")]
        [MaxLength(100)]
        public string? AZURE_AD_UI_DOMAIN { get; set; }

        [Column("azure_ad_ui_client_id")]
        [MaxLength(50)]
        public string? AZURE_AD_UI_CLIENT_ID { get; set; }

        [Column("azure_ad_ui_client_secret")]
        [MaxLength(500)]
        public string? AZURE_AD_UI_CLIENT_SECRET { get; set; }

        [Column("azure_ad_ui_call_back_path")]
        [MaxLength(100)]
        public string? AZURE_AD_UI_CALL_BACK_PATH { get; set; }

        [Column("azure_ad_ui_call_back_path_development")]
        [MaxLength(100)]
        public string? AZURE_AD_UI_CALL_BACK_PATH_DEVELOPMENT { get; set; }

        // ===== API ENDPOINTS =====
        [Column("api_development_base_url")]
        [MaxLength(200)]
        public string? API_DEVELOPMENT_BASE_URL { get; set; }

        [Column("api_production_base_url")]
        [MaxLength(200)]
        public string? API_PRODUCTION_BASE_URL { get; set; }

        [Column("api_scopes")]
        [MaxLength(500)]
        public string? API_SCOPES { get; set; }

        // ===== UI ENDPOINTS =====
        [Column("ui_development_base_url")]
        [MaxLength(200)]
        public string? UI_DEVELOPMENT_BASE_URL { get; set; }

        [Column("ui_production_base_url")]
        [MaxLength(200)]
        public string? UI_PRODUCTION_BASE_URL { get; set; }

        // ===== HACIENDA - E-INVOICE =====
        [Column("hacienda_environment")]
        [MaxLength(20)]
        public string? HACIENDA_ENVIRONMENT { get; set; } // Sandbox | Production

        [Column("hacienda_reception_url")]
        [MaxLength(300)]
        public string? HACIENDA_RECEPTION_URL { get; set; }

        [Column("hacienda_token_url")]
        [MaxLength(300)]
        public string? HACIENDA_TOKEN_URL { get; set; }

        [Column("hacienda_client_id")]
        [MaxLength(100)]
        public string? HACIENDA_CLIENT_ID { get; set; }

        [Column("hacienda_username")]
        [MaxLength(150)]
        public string? HACIENDA_USERNAME { get; set; }

        [Column("hacienda_password")]
        [MaxLength(200)]
        public string? HACIENDA_PASSWORD { get; set; } // usar secreto, no texto plano

        [Column("hacienda_cert_secret_name")]
        [MaxLength(150)]
        public string? HACIENDA_CERT_SECRET_NAME { get; set; } // nombre del secreto Key Vault con el .p12

        [Column("hacienda_cert_pin_secret_name")]
        [MaxLength(150)]
        public string? HACIENDA_CERT_PIN_SECRET_NAME { get; set; } // nombre del secreto Key Vault con el PIN

        [Column("hacienda_schema_version")]
        [MaxLength(10)]
        public string? HACIENDA_SCHEMA_VERSION { get; set; } // ej: "4.4"

        [Column("hacienda_allow_v43_notes")]
        public bool HACIENDA_ALLOW_V43_NOTES { get; set; } = true; // NC/ND de históricos v4.3

        [Column("hacienda_callback_url")]
        [MaxLength(300)]
        public string? HACIENDA_CALLBACK_URL { get; set; } // opcional webhooks/middleware

        // ===== LOGGING =====
        [Column("logging_default")]
        [Required]
        [MaxLength(50)]
        public string LOGGING_DEFAULT { get; set; } = "Information";

        [Column("logging_asp_net_core")]
        [Required]
        [MaxLength(50)]
        public string LOGGING_ASP_NET_CORE { get; set; } = "Warning";

        [Column("logging_ef_core")]
        [Required]
        [MaxLength(50)]
        public string LOGGING_EF_CORE { get; set; } = "Information";

        // ===== AUTENTICACIÓN =====
        /// <summary>
        /// Indica si la compañía usa Azure AD para autenticación.
        /// Si es false, los usuarios se autentican con email + contraseña.
        /// </summary>
        [Column("uses_azure_ad")]
        public bool USES_AZURE_AD { get; set; } = true;

        /// <summary>
        /// Máximo de intentos de login fallidos antes de bloquear (solo para auth local)
        /// </summary>
        [Column("max_failed_login_attempts")]
        public int MAX_FAILED_LOGIN_ATTEMPTS { get; set; } = 3;

        /// <summary>
        /// Duración del bloqueo en minutos después de exceder intentos fallidos
        /// </summary>
        [Column("lockout_duration_minutes")]
        public int LOCKOUT_DURATION_MINUTES { get; set; } = 30;

        // ===== OTROS =====
        [Column("allowed_hosts")]
        [Required]
        [MaxLength(500)]
        public string ALLOWED_HOSTS { get; set; } = "*";

        [Column("is_active")]
        public bool IS_ACTIVE { get; set; } = true;

        /// <summary>
        /// Indica si esta es la compañía de administración del sistema.
        /// Los usuarios con permisos System.ViewAllCompanies en esta compañía
        /// pueden ver y gestionar TODAS las compañías del sistema.
        /// </summary>
        [Column("is_admin_company")]
        public bool IS_ADMIN_COMPANY { get; set; } = false;

        [Column("description")]
        [MaxLength(500)]
        public string? DESCRIPTION { get; set; }

        /// <summary>
        /// Mensaje de bienvenida que se muestra en el Dashboard de la compañía.
        /// Si está vacío, usa un mensaje por defecto.
        /// </summary>
        [Column("dashboard_welcome_message")]
        [MaxLength(500)]
        public string? DASHBOARD_WELCOME_MESSAGE { get; set; }

        [Column("id_country")]
        public int? IdCountry { get; set; }

        [Column("city")]
        [MaxLength(100)]
        public string? CITY { get; set; }

        [Column("address")]
        [MaxLength(255)]
        public string? ADDRESS { get; set; }

        [Column("contact_email")]
        [MaxLength(100)]
        public string? CONTACT_EMAIL { get; set; }

        [Column("contact_phone")]
        [MaxLength(20)]
        public string? CONTACT_PHONE { get; set; }

        [Column("manager_name")]
        [MaxLength(100)]
        public string? MANAGER_NAME { get; set; }

        [Column("logo_url")]
        [MaxLength(255)]
        public string? LOGO_URL { get; set; }

        /// <summary>
        /// Logo de la compañía almacenado como Data URI (base64).
        /// Formato: data:image/png;base64,{base64data}
        /// Se usa cuando LOGO_URL está vacío o no existe.
        /// </summary>
        [Column("logo_data")]
        public string? LOGO_DATA { get; set; }

        [Column("industry")]
        [MaxLength(100)]
        public string? INDUSTRY { get; set; }

        [Column("website")]
        [MaxLength(255)]
        public string? WEBSITE { get; set; }

        // ===== SUSCRIPCIÓN =====
        [Column("id_subscription_status")]
        public int IdSubscriptionStatus { get; set; }

        [Column("trial_end_date")]
        public DateTime TRIAL_END_DATE { get; set; }

        // ===== AUDITORÍA =====
        [Column("rowpointer")]
        public Guid RowPointer { get; set; }

        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [Column("created_by")]
        [Required]
        [MaxLength(150)]
        public string CreatedBy { get; set; } = default!;

        [Column("updated_by")]
        [Required]
        [MaxLength(150)]
        public string UpdatedBy { get; set; } = default!;

        // ===== NAVEGACIÓN =====
        public virtual Company? ParentCompany { get; set; }
        public virtual ICollection<Company> ChildCompanies { get; set; } = new List<Company>();
        public virtual ElectronicDocumentIdentificationType IdentificationType { get; set; } = default!;
        public virtual Country? Country { get; set; }
        public virtual SubscriptionStatus SubscriptionStatus { get; set; } = default!;
        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

        // ===== MÉTODOS HELPER =====
        public string GetConnectionString()
        {
            return IS_PRODUCTION
                ? CONNECTION_STRING_PRODUCTION ?? throw new InvalidOperationException("CONNECTION_STRING_PRODUCTION no configurada")
                : CONNECTION_STRING_DEVELOPMENT ?? throw new InvalidOperationException("CONNECTION_STRING_DEVELOPMENT no configurada");
        }

        public string GetAPIBaseUrl()
        {
            return IS_PRODUCTION
                ? API_PRODUCTION_BASE_URL ?? throw new InvalidOperationException("API_PRODUCTION_BASE_URL no configurada")
                : API_DEVELOPMENT_BASE_URL ?? throw new InvalidOperationException("API_DEVELOPMENT_BASE_URL no configurada");
        }

        public Company GetRootTenant()
        {
            if (IsTenant || ParentCompanyId == 0)
                return this;

            return ParentCompany?.GetRootTenant() ?? this;
        }

        public int GetHierarchyLevel()
        {
            if (IsTenant || ParentCompanyId == 0)
                return 0;

            return (ParentCompany?.GetHierarchyLevel() ?? 0) + 1;
        }

        public bool IsSubsidiaryOf(int companyId)
        {
            if (ParentCompanyId == companyId)
                return true;

            return ParentCompany?.IsSubsidiaryOf(companyId) ?? false;
        }
    }
}