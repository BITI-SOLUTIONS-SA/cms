// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentTaxRate.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de códigos de tarifa del IVA de Hacienda CR.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_tax_rate
//              Es compartida por TODAS las compañías. Contiene el catálogo oficial
//              de códigos de tarifa (CodigoTarifa) v4.4: 01..11.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_tax_rate", Schema = "admin")]
    public class ElectronicDocumentTaxRate
    {
        [Key]
        [Column("id_electronic_document_tax_rate")]
        public int Id { get; set; }

        /// <summary>CodigoTarifa Hacienda, formato '00' (01..11).</summary>
        [Required]
        [MaxLength(2)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Nombre de la tarifa.</summary>
        [Required]
        [MaxLength(200)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Descripción de la tarifa.</summary>
        [MaxLength(1000)]
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>Porcentaje de la tarifa (0, 0.5, 1, 2, 4, 8, 13).</summary>
        [Column("rate_percent")]
        public decimal RatePercent { get; set; } = 0;

        /// <summary>Marca el código por defecto del sistema (08 = 13% tarifa general).</summary>
        [Column("is_default")]
        public bool IsDefault { get; set; } = false;

        /// <summary>Indica tarifas exentas o de 0%.</summary>
        [Column("is_exempt")]
        public bool IsExempt { get; set; } = false;

        /// <summary>Orden de despliegue en el selector.</summary>
        [Column("display_order")]
        public int DisplayOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        // ===== Auditoría =====
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [MaxLength(30)]
        [Column("created_by")]
        public string? CreatedBy { get; set; }

        [MaxLength(30)]
        [Column("updated_by")]
        public string? UpdatedBy { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("rowpointer")]
        public Guid RowPointer { get; set; }
    }
}
