// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ElectronicDocumentTax.cs
// PROPÓSITO: Impuesto asociado a una línea de comprobante
// DESCRIPCIÓN: Desglose del IVA (u otros impuestos) por línea. Vive en la BD
//              operacional de cada compañía.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Impuesto por línea. Tabla: {schema}.electronic_document_tax.
    /// </summary>
    [Table("electronic_document_tax")]
    public class ElectronicDocumentTax
    {
        [Key]
        [Column("id_electronic_document_tax")]
        public int Id { get; set; }

        [Column("id_electronic_document_line")]
        public int IdElectronicDocumentLine { get; set; }

        [ForeignKey(nameof(IdElectronicDocumentLine))]
        public virtual ElectronicDocumentLine? ElectronicDocumentLine { get; set; }

        /// <summary>Código de impuesto (01=IVA...).</summary>
        [Required]
        [MaxLength(2)]
        [Column("tax_code")]
        public string TaxCode { get; set; } = "01";

        /// <summary>Código de tarifa (01..08).</summary>
        [MaxLength(2)]
        [Column("tax_rate_code")]
        public string? TaxRateCode { get; set; }

        [Column("tax_rate", TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; }

        [Column("tax_amount", TypeName = "decimal(18,5)")]
        public decimal TaxAmount { get; set; }

        // ===== Auditoría =====
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
    }
}
