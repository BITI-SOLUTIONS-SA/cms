// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ElectronicDocumentOtherChargeLine.cs
// PROPÓSITO: Otro cargo aplicado a un comprobante electrónico (nodo <OtrosCargos>)
// DESCRIPCIÓN: Cada fila representa un nodo <OtrosCargos> de Hacienda v4.4. Vive en
//              la BD operacional de cada compañía. El resumen JSON se conserva en
//              ElectronicDocument.OtherCharges por compatibilidad con el XML builder.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Otro cargo por documento. Tabla: {schema}.electronic_document_other_charges_line.
    /// </summary>
    [Table("electronic_document_other_charges_line")]
    public class ElectronicDocumentOtherChargeLine
    {
        [Key]
        [Column("id_electronic_document_other_charges_line")]
        public int Id { get; set; }

        [Column("id_electronic_document")]
        public int IdElectronicDocument { get; set; }

        [ForeignKey(nameof(IdElectronicDocument))]
        public virtual ElectronicDocument? ElectronicDocument { get; set; }

        /// <summary>Orden del cargo dentro del documento (1..15).</summary>
        [Column("sequence")]
        public int Sequence { get; set; } = 1;

        /// <summary>TipoDocumento v4.4: código del tipo de otro cargo (01..10, 99).</summary>
        [Required]
        [MaxLength(2)]
        [Column("type_code")]
        public string TypeCode { get; set; } = string.Empty;

        /// <summary>TipoDocumentoOTro v4.4: descripción cuando TypeCode = 99.</summary>
        [MaxLength(100)]
        [Column("other_type_description")]
        public string? OtherTypeDescription { get; set; }

        /// <summary>Detalle v4.4: detalle del otro cargo.</summary>
        [MaxLength(160)]
        [Column("detail")]
        public string? Detail { get; set; }

        /// <summary>MontoCargo v4.4: monto del otro cargo.</summary>
        [Column("amount", TypeName = "decimal(18,5)")]
        public decimal Amount { get; set; }

        /// <summary>Porcentaje v4.4 (informativo/opcional).</summary>
        [Column("percent", TypeName = "decimal(9,5)")]
        public decimal? Percent { get; set; }

        /// <summary>Tipo de identificación del tercero (opcional).</summary>
        [MaxLength(2)]
        [Column("third_ident_type")]
        public string? ThirdIdentType { get; set; }

        /// <summary>Número de identificación del tercero (opcional).</summary>
        [MaxLength(20)]
        [Column("third_ident_number")]
        public string? ThirdIdentNumber { get; set; }

        /// <summary>Nombre del tercero (opcional).</summary>
        [MaxLength(100)]
        [Column("third_name")]
        public string? ThirdName { get; set; }

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
