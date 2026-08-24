// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ElectronicDocumentDiscountLine.cs
// PROPÓSITO: Descuento individual aplicado a una línea de comprobante
// DESCRIPCIÓN: Cada fila representa un nodo <Descuento> de Hacienda v4.4 (hasta 5
//              por línea). Vive en la BD operacional de cada compañía. El resumen
//              agregado se conserva en ElectronicDocumentLine (DiscountAmount y el
//              JSON Discounts) por compatibilidad.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Descuento por línea. Tabla: {schema}.electronic_document_discount_line.
    /// </summary>
    [Table("electronic_document_discount_line")]
    public class ElectronicDocumentDiscountLine
    {
        [Key]
        [Column("id_electronic_document_discount_line")]
        public int Id { get; set; }

        [Column("id_electronic_document_line")]
        public int IdElectronicDocumentLine { get; set; }

        [ForeignKey(nameof(IdElectronicDocumentLine))]
        public virtual ElectronicDocumentLine? ElectronicDocumentLine { get; set; }

        /// <summary>Orden del descuento dentro de la línea (1..5).</summary>
        [Column("sequence")]
        public int Sequence { get; set; } = 1;

        /// <summary>MontoDescuento v4.4: monto del descuento aplicado.</summary>
        [Column("discount_amount", TypeName = "decimal(18,5)")]
        public decimal DiscountAmount { get; set; }

        /// <summary>NaturalezaDescuento v4.4: descripción de la naturaleza del descuento.</summary>
        [MaxLength(80)]
        [Column("discount_nature")]
        public string? DiscountNature { get; set; }

        /// <summary>Código de naturaleza: 01=Regalía, 04=Volumen, 05=Temporada, 06=Promoción, etc.</summary>
        [MaxLength(2)]
        [Column("discount_nature_code")]
        public string? DiscountNatureCode { get; set; }

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
