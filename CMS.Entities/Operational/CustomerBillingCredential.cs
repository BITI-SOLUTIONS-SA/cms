// ================================================================================
// ARCHIVO: CMS.Entities/Operational/CustomerBillingCredential.cs
// PROPÓSITO: Credencial completa de facturación electrónica (emisor/receptor)
// DESCRIPCIÓN: Almacena TODA la información necesaria para emisión/recepción:
//              - Datos de identificación (cédula, tipo)
//              - Datos de ubicación (provincia, cantón, distrito)
//              - Contacto (email, teléfono)
//              - Certificado .p12 y PIN CIFRADOS (AES-256)
//              - Credenciales OAuth Hacienda
//              - Flags: IsIssuer (emisor), IsCompanyOwner (empresa dueña)
//              REGLA: Máximo 2 credentials por customer (stag/prod), solo 1 activa.
//              Zero-Trust: descifrado solo en memoria volátil.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Credencial completa de facturación electrónica.
    /// Tabla: {schema}.customer_billing_credential.
    /// Contiene TODA la información para emisión/recepción de comprobantes.
    /// </summary>
    [Table("customer_billing_credential")]
    public class CustomerBillingCredential
    {
        [Key]
        [Column("id_customer_billing_credential")]
        public int Id { get; set; }

        /// <summary>
        /// FK al customer/supplier.
        /// Puede ser NULL si la credencial es standalone (ej: receptor genérico).
        /// </summary>
        [Column("id_customer")]
        public int? IdCustomer { get; set; }

        /// <summary>Ambiente: 'stag' (pruebas) | 'prod' (producción).</summary>
        [Required]
        [MaxLength(10)]
        [Column("environment")]
        public string Environment { get; set; } = "stag";

        // ===== FLAGS DE ROL =====

        /// <summary>
        /// TRUE si esta credencial es de un EMISOR de comprobantes.
        /// FALSE si es un receptor (cliente/proveedor).
        /// </summary>
        [Column("is_issuer")]
        public bool IsIssuer { get; set; }

        /// <summary>
        /// TRUE si es la empresa dueña del sistema (master issuer).
        /// Solo puede haber uno activo por schema.
        /// </summary>
        [Column("is_company_owner")]
        public bool IsCompanyOwner { get; set; }

        /// <summary>
        /// TRUE si el emisor está inscrito en un RÉGIMEN ESPECIAL de tributación
        /// (Registrofiscal8707 / Ley 8707 y afines). Cuando es TRUE, los comprobantes
        /// deben reflejar el tratamiento fiscal especial correspondiente.
        /// </summary>
        [Column("is_special_regime")]
        public bool IsSpecialRegime { get; set; }

        /// <summary>
        /// Código del régimen especial que se emite en el nodo &lt;Registrofiscal8707&gt;
        /// del Emisor. Obligatorio cuando <see cref="IsSpecialRegime"/> es TRUE.
        /// </summary>
        [MaxLength(20)]
        [Column("special_regime_code")]
        public string? SpecialRegimeCode { get; set; }

        // ===== IDENTIFICACIÓN FISCAL =====

        /// <summary>Razón social o nombre completo.</summary>
        [Required]
        [MaxLength(200)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Nombre comercial.</summary>
        [MaxLength(200)]
        [Column("commercial_name")]
        public string? CommercialName { get; set; }

        /// <summary>
        /// Tipo de identificación Hacienda CR.
        /// 01=Física, 02=Jurídica, 03=DIMEX, 04=NITE, 05=Extranjero
        /// </summary>
        [Required]
        [MaxLength(2)]
        [Column("identification_type")]
        public string IdentificationType { get; set; } = "02";

        /// <summary>Número de identificación/cédula.</summary>
        [Required]
        [MaxLength(20)]
        [Column("identification")]
        public string Identification { get; set; } = string.Empty;

        /// <summary>Identificación extranjero.</summary>
        [MaxLength(20)]
        [Column("foreign_identification")]
        public string? ForeignIdentification { get; set; }

        /// <summary>Código de actividad económica (6 dígitos).</summary>
        [MaxLength(6)]
        [Column("economic_activity")]
        public string? EconomicActivity { get; set; }

        // ===== UBICACIÓN (códigos Hacienda CR) =====

        /// <summary>Provincia (código 1 dígito).</summary>
        [MaxLength(1)]
        [Column("province")]
        public string? Province { get; set; }

        /// <summary>Cantón (código 2 dígitos).</summary>
        [MaxLength(2)]
        [Column("canton")]
        public string? Canton { get; set; }

        /// <summary>Distrito (código 2 dígitos).</summary>
        [MaxLength(2)]
        [Column("district")]
        public string? District { get; set; }

        /// <summary>Otras señas (dirección completa).</summary>
        [MaxLength(250)]
        [Column("other_signs")]
        public string? OtherSigns { get; set; }

        /// <summary>Latitud GPS.</summary>
        [Column("gps_latitude")]
        public decimal? GpsLatitude { get; set; }

        /// <summary>Longitud GPS.</summary>
        [Column("gps_longitude")]
        public decimal? GpsLongitude { get; set; }

        // ===== CONTACTO =====

        /// <summary>Código de área telefónica (506 para CR).</summary>
        [MaxLength(3)]
        [Column("phone_code")]
        public string? PhoneCode { get; set; } = "506";

        /// <summary>Teléfono fijo.</summary>
        [MaxLength(20)]
        [Column("phone")]
        public string? Phone { get; set; }

        /// <summary>Email para notificaciones de Hacienda.</summary>
        [Required]
        [MaxLength(160)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        // ===== CERTIFICADO .p12 CIFRADO (Solo para EMISORES) =====

        /// <summary>Certificado .p12 cifrado con AES-256 (bytea).</summary>
        [Column("p12_cipher")]
        public byte[]? P12Cipher { get; set; }

        [Column("p12_iv")]
        public byte[]? P12Iv { get; set; }

        // ===== PIN CIFRADO (Solo para EMISORES) =====

        /// <summary>PIN del certificado cifrado con AES-256.</summary>
        [Column("pin_cipher")]
        public byte[]? PinCipher { get; set; }

        [Column("pin_iv")]
        public byte[]? PinIv { get; set; }

        // ===== OAuth Hacienda (Solo para EMISORES) =====

        [MaxLength(160)]
        [Column("oauth_username")]
        public string? OAuthUsername { get; set; }

        [Column("oauth_password_cipher")]
        public byte[]? OAuthPasswordCipher { get; set; }

        [Column("oauth_password_iv")]
        public byte[]? OAuthPasswordIv { get; set; }

        // ===== METADATOS DEL CERTIFICADO =====

        [Column("cert_not_before")]
        public DateTime? CertNotBefore { get; set; }

        [Column("cert_not_after")]
        public DateTime? CertNotAfter { get; set; }

        /// <summary>Versión de la master key AES (para rotación).</summary>
        [Column("key_version")]
        public int KeyVersion { get; set; } = 1;

        // ===== ESTADO =====

        /// <summary>
        /// TRUE = credential activa que usa el sistema.
        /// Solo puede haber una activa por (customer/supplier, environment).
        /// </summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ===== AUDITORÍA =====

        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("rowpointer")]
        public Guid RowPointer { get; set; } = Guid.NewGuid();

        // ===== NAVIGATION PROPERTIES =====

        /// <summary>Customer al que pertenece (puede ser NULL).</summary>
        [ForeignKey(nameof(IdCustomer))]
        public Customer? Customer { get; set; }
    }
}
