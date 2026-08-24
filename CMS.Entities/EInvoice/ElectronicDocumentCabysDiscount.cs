// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentCabysDiscount.cs
// PROPÓSITO: Relación CAByS ↔ Naturaleza de descuento permitida para facturación
//            electrónica CR v4.4.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_cabys_discount
//              Define qué naturalezas de descuento (admin.electronic_document_discount)
//              están permitidas para cada código CAByS
//              (admin.electronic_document_cabys). Si un CAByS no tiene filas, la
//              aplicación permite todas las naturalezas.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_cabys_discount", Schema = "admin")]
    public class ElectronicDocumentCabysDiscount
    {
        [Key]
        [Column("id_electronic_document_cabys_discount")]
        public int Id { get; set; }

        [Column("id_electronic_document_cabys")]
        public int IdElectronicDocumentCabys { get; set; }

        [Column("id_electronic_document_discount")]
        public int IdElectronicDocumentDiscount { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [ForeignKey(nameof(IdElectronicDocumentCabys))]
        public ElectronicDocumentCabys? Cabys { get; set; }

        [ForeignKey(nameof(IdElectronicDocumentDiscount))]
        public ElectronicDocumentDiscount? Discount { get; set; }

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
