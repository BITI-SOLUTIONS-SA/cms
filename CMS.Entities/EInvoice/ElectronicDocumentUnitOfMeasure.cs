// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentUnitOfMeasure.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de códigos de unidad de medida de Hacienda CR.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_unit_of_measure
//              Es compartida por TODAS las compañías. Contiene el catálogo oficial
//              de códigos de unidad de medida (CodigoUnidadMedida) v4.4.
//              Relación lógica cross-DB con tablas operacionales (sinai.item, etc.).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_unit_of_measure", Schema = "admin")]
    public class ElectronicDocumentUnitOfMeasure
    {
        [Key]
        [Column("id_electronic_document_unit_of_measure")]
        public int Id { get; set; }

        /// <summary>CodigoUnidadMedida Hacienda v4.4 (ej. 'Unid', 'kg', 'l').</summary>
        [Required]
        [MaxLength(20)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Nombre de la unidad de medida.</summary>
        [Required]
        [MaxLength(200)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Descripción de la unidad de medida.</summary>
        [MaxLength(1000)]
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>Símbolo de la unidad (ej. 'u', 'kg', 'L').</summary>
        [MaxLength(20)]
        [Column("symbol")]
        public string? Symbol { get; set; }

        /// <summary>Indica si la unidad permite cantidades decimales.</summary>
        [Column("allows_decimals")]
        public bool AllowsDecimals { get; set; } = true;

        /// <summary>Marca la unidad por defecto del sistema (Unid).</summary>
        [Column("is_default")]
        public bool IsDefault { get; set; } = false;

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
