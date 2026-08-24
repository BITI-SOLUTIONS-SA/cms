// ================================================================================
// ARCHIVO: CMS.Entities/Operational/Customer.cs
// PROPÓSITO: Maestro de clientes (CRM/Ventas)
// DESCRIPCIÓN: Clientes a los que vendemos bienes/servicios.
//              NO incluye datos de facturación electrónica (ver customer_billing_credential).
//              Vive en la BD operacional de cada compañía ({schema}).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Maestro de clientes. Tabla: {schema}.customer.
    /// </summary>
    [Table("customer")]
    public class Customer
    {
        [Key]
        [Column("id_customer")]
        public int Id { get; set; }

        /// <summary>Código único de negocio del cliente.</summary>
        [Required]
        [MaxLength(30)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

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
        /// Tipo de cliente (referencia lógica cross-DB).
        /// FK lógica a cms.admin.customer_type.id_customer_type.
        /// Valores del catálogo: Issuer, Receptor, Issuer-Receptor, Corporate, Retail, Wholesale, Government.
        /// </summary>
        [Column("id_customer_type")]
        public int? IdCustomerType { get; set; }

        // ===== IDENTIFICACIÓN FISCAL =====

        /// <summary>
        /// Tipo de identificación Hacienda CR (referencia lógica cross-DB).
        /// FK lógica a cms.admin.electronic_document_identification_type.
        /// </summary>
        [Column("id_electronic_document_identification_type")]
        public int? IdElectronicDocumentIdentificationType { get; set; }

        /// <summary>Número de identificación/cédula.</summary>
        [MaxLength(20)]
        [Column("identification")]
        public string? Identification { get; set; }

        /// <summary>Identificación extranjero.</summary>
        [MaxLength(20)]
        [Column("foreign_identification")]
        public string? ForeignIdentification { get; set; }

        // ===== COMERCIAL =====

        /// <summary>Límite de crédito.</summary>
        [Column("credit_limit")]
        public decimal? CreditLimit { get; set; }

        /// <summary>Días de crédito.</summary>
        [Column("credit_days")]
        public int? CreditDays { get; set; }

        /// <summary>Condiciones de pago (texto libre).</summary>
        [MaxLength(50)]
        [Column("payment_terms")]
        public string? PaymentTerms { get; set; }

        /// <summary>Porcentaje de descuento general.</summary>
        [Column("discount_pct")]
        public decimal? DiscountPct { get; set; }

        /// <summary>Lista de precios asignada.</summary>
        [MaxLength(30)]
        [Column("price_list")]
        public string? PriceList { get; set; }

        /// <summary>
        /// ID del vendedor asignado.
        /// ⚠️ FK lógica a cms.admin.user.id_user (cross-DB).
        /// </summary>
        [Column("id_assigned_salesperson")]
        public int? IdAssignedSalesperson { get; set; }

        /// <summary>
        /// ID del cliente padre (jerarquía: sucursales, franquicias, etc.).
        /// FK real a {schema}.customer.id_customer.
        /// </summary>
        [Column("id_parent_customer")]
        public int? IdParentCustomer { get; set; }

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

        /// <summary>Otras señas (dirección completa en texto).</summary>
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

        /// <summary>Código de área telefónica.</summary>
        [MaxLength(3)]
        [Column("phone_code")]
        public string? PhoneCode { get; set; }

        /// <summary>Teléfono fijo.</summary>
        [MaxLength(20)]
        [Column("phone")]
        public string? Phone { get; set; }

        /// <summary>Teléfono móvil.</summary>
        [MaxLength(20)]
        [Column("mobile")]
        public string? Mobile { get; set; }

        /// <summary>Email principal.</summary>
        [MaxLength(160)]
        [Column("email")]
        public string? Email { get; set; }

        /// <summary>Sitio web.</summary>
        [MaxLength(200)]
        [Column("website")]
        public string? Website { get; set; }

        /// <summary>Nombre del contacto principal.</summary>
        [MaxLength(200)]
        [Column("contact_name")]
        public string? ContactName { get; set; }

        /// <summary>Cargo del contacto principal.</summary>
        [MaxLength(100)]
        [Column("contact_position")]
        public string? ContactPosition { get; set; }

        // ===== NOTAS Y OBSERVACIONES =====

        /// <summary>Notas visibles (impresas en documentos).</summary>
        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        /// <summary>Notas internas (no visibles al cliente).</summary>
        [MaxLength(2000)]
        [Column("internal_notes")]
        public string? InternalNotes { get; set; }

        // ===== ESTADO =====

        /// <summary>Cliente activo/inactivo.</summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Razón de bloqueo (ej: morosidad, fraude).</summary>
        [MaxLength(500)]
        [Column("blocked_reason")]
        public string? BlockedReason { get; set; }

        // ===== EXONERACIÓN (mapeo directo al nodo <Exoneracion> de la FE v4.4) =====

        /// <summary>Indica si el cliente tiene exoneración de IVA vigente (usa los campos Exon*).</summary>
        [Column("is_exonerated")]
        public bool IsExonerated { get; set; }

        /// <summary>XML &lt;TipoDocumentoEX1&gt;: código del tipo de documento de exoneración.</summary>
        [MaxLength(2)]
        [Column("exon_document_type")]
        public string? ExonDocumentType { get; set; }

        /// <summary>XML &lt;TipoDocumentoOTRO&gt;: descripción cuando ExonDocumentType = 99.</summary>
        [MaxLength(100)]
        [Column("exon_document_type_other")]
        public string? ExonDocumentTypeOther { get; set; }

        /// <summary>XML &lt;NumeroDocumento&gt;: número/autorización de la exoneración (minLength 3).</summary>
        [MaxLength(40)]
        [Column("exon_document_number")]
        public string? ExonDocumentNumber { get; set; }

        /// <summary>XML &lt;Articulo&gt;: número de artículo que establece la exoneración.</summary>
        [MaxLength(10)]
        [Column("exon_article")]
        public string? ExonArticle { get; set; }

        /// <summary>XML &lt;Inciso&gt;: número de inciso que establece la exoneración.</summary>
        [MaxLength(10)]
        [Column("exon_subsection")]
        public string? ExonSubsection { get; set; }

        /// <summary>XML &lt;NombreInstitucion&gt;: código de la institución que emitió la exoneración.</summary>
        [MaxLength(2)]
        [Column("exon_institution_code")]
        public string? ExonInstitutionCode { get; set; }

        /// <summary>XML &lt;NombreInstitucionOtros&gt;: descripción cuando ExonInstitutionCode = 99.</summary>
        [MaxLength(160)]
        [Column("exon_institution_other")]
        public string? ExonInstitutionOther { get; set; }

        /// <summary>XML &lt;FechaEmisionEX&gt;: fecha de emisión del documento de exoneración.</summary>
        [Column("exon_issue_date")]
        public DateTime? ExonIssueDate { get; set; }

        /// <summary>XML &lt;TarifaExonerada&gt;: porcentaje de tarifa exonerada (ej: 13.00).</summary>
        [Column("exon_tariff_percent")]
        public decimal? ExonTariffPercent { get; set; }

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

        /// <summary>Cliente padre (jerarquía).</summary>
        [ForeignKey(nameof(IdParentCustomer))]
        public Customer? ParentCustomer { get; set; }

        /// <summary>Clientes hijos (sucursales, franquicias).</summary>
        public ICollection<Customer> ChildCustomers { get; set; } = new List<Customer>();
    }
}
