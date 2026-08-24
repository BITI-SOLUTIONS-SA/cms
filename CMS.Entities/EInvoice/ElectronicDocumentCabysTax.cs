// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentCabysTax.cs
// PROPÓSITO: Relación CAByS ↔ Tipo de impuesto permitido para facturación
//            electrónica CR v4.4.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_cabys_tax
//              Define qué tipos de impuesto (admin.electronic_document_tax_type)
//              están permitidos para cada código CAByS
//              (admin.electronic_document_cabys). Si un CAByS no tiene filas, la
//              aplicación permite todos los tipos de impuesto.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_cabys_tax", Schema = "admin")]
    public class ElectronicDocumentCabysTax
    {
        [Key]
        [Column("id_electronic_document_cabys_tax")]
        public int Id { get; set; }

        [Column("id_electronic_document_cabys")]
        public int IdElectronicDocumentCabys { get; set; }

        [Column("id_electronic_document_tax_type")]
        public int IdElectronicDocumentTaxType { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [ForeignKey(nameof(IdElectronicDocumentCabys))]
        public ElectronicDocumentCabys? Cabys { get; set; }

        [ForeignKey(nameof(IdElectronicDocumentTaxType))]
        public ElectronicDocumentTaxType? TaxType { get; set; }

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
