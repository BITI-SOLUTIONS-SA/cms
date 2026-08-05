// ================================================================================
// ARCHIVO: CMS.Entities/Operational/Supplier.cs
// PROPÓSITO: Maestro de proveedores (Accounts Payable / Purchasing)
// DESCRIPCIÓN: Proveedores a los que compramos bienes/servicios.
//              Incluye datos para emitirles comprobantes (facturas de compra).
//              Vive en la BD operacional de cada compañía ({schema}).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Maestro de proveedores. Tabla: {schema}.supplier.
    /// </summary>
    [Table("supplier")]
    public class Supplier
    {
        [Key]
        [Column("id_supplier")]
        public int Id { get; set; }

        /// <summary>Código único de negocio del proveedor.</summary>
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

        // ===== IDENTIFICACIÓN FISCAL =====

        /// <summary>
        /// Tipo de identificación Hacienda CR.
        /// 01=Física, 02=Jurídica, 03=DIMEX, 04=NITE, 05=Extranjero
        /// </summary>
        [MaxLength(2)]
        [Column("identification_type")]
        public string? IdentificationType { get; set; }

        /// <summary>Número de identificación/cédula.</summary>
        [MaxLength(20)]
        [Column("identification")]
        public string? Identification { get; set; }

        /// <summary>Identificación extranjero.</summary>
        [MaxLength(20)]
        [Column("foreign_identification")]
        public string? ForeignIdentification { get; set; }

        /// <summary>Código de actividad económica.</summary>
        [MaxLength(6)]
        [Column("economic_activity")]
        public string? EconomicActivity { get; set; }

        // ===== COMERCIAL / PURCHASING =====

        /// <summary>Días de crédito que nos otorga.</summary>
        [Column("credit_days")]
        public int? CreditDays { get; set; }

        /// <summary>Límite de crédito que nos otorga.</summary>
        [Column("credit_limit")]
        public decimal? CreditLimit { get; set; }

        /// <summary>Condiciones de pago (texto libre).</summary>
        [MaxLength(50)]
        [Column("payment_terms")]
        public string? PaymentTerms { get; set; }

        /// <summary>Descuento general que nos aplica (%).</summary>
        [Column("discount_pct")]
        public decimal? DiscountPct { get; set; }

        /// <summary>Moneda principal de negociación (CRC, USD, EUR, etc.).</summary>
        [MaxLength(3)]
        [Column("currency")]
        public string? Currency { get; set; } = "CRC";

        /// <summary>
        /// Tipo de proveedor: 'Goods' (bienes), 'Services' (servicios), 'Both'
        /// </summary>
        [MaxLength(20)]
        [Column("supplier_type")]
        public string? SupplierType { get; set; } = "Both";

        /// <summary>
        /// ID del comprador asignado.
        /// ⚠️ FK lógica a cms.admin.user.id_user (cross-DB).
        /// </summary>
        [Column("id_assigned_buyer")]
        public int? IdAssignedBuyer { get; set; }

        /// <summary>
        /// ID del proveedor padre (jerarquía: grupo corporativo, filiales).
        /// FK real a {schema}.supplier.id_supplier.
        /// </summary>
        [Column("id_parent_supplier")]
        public int? IdParentSupplier { get; set; }

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

        // ===== DATOS BANCARIOS =====

        /// <summary>Nombre del banco.</summary>
        [MaxLength(100)]
        [Column("bank_name")]
        public string? BankName { get; set; }

        /// <summary>Número de cuenta bancaria.</summary>
        [MaxLength(50)]
        [Column("bank_account")]
        public string? BankAccount { get; set; }

        /// <summary>IBAN (internacional).</summary>
        [MaxLength(50)]
        [Column("iban")]
        public string? Iban { get; set; }

        /// <summary>SWIFT/BIC.</summary>
        [MaxLength(20)]
        [Column("swift_code")]
        public string? SwiftCode { get; set; }

        // ===== NOTAS Y OBSERVACIONES =====

        /// <summary>Notas visibles.</summary>
        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        /// <summary>Notas internas (no visibles al proveedor).</summary>
        [MaxLength(2000)]
        [Column("internal_notes")]
        public string? InternalNotes { get; set; }

        // ===== ESTADO =====

        /// <summary>Proveedor activo/inactivo.</summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Razón de bloqueo.</summary>
        [MaxLength(500)]
        [Column("blocked_reason")]
        public string? BlockedReason { get; set; }

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

        /// <summary>Proveedor padre (jerarquía).</summary>
        [ForeignKey(nameof(IdParentSupplier))]
        public Supplier? ParentSupplier { get; set; }

        /// <summary>Proveedores hijos (filiales).</summary>
        public ICollection<Supplier> ChildSuppliers { get; set; } = new List<Supplier>();
    }
}
