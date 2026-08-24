// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentReferenceType.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de "Tipo documento de referencia"
//            (InformacionReferencia/TipoDoc) de Hacienda CR.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_reference_type
//              Compartida por TODAS las compañías. Contiene el catálogo oficial del
//              código "Tipo documento de referencia" v4.4: 01=Factura electrónica,
//              02=Nota de débito electrónica, ..., 20=Recibo electrónico de pago, 99=Otros.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_reference_type", Schema = "admin")]
    public class ElectronicDocumentReferenceType
    {
        [Key]
        [Column("id_electronic_document_reference_type")]
        public int Id { get; set; }

        /// <summary>Tipo documento de referencia, formato '00' (01..20, 99).</summary>
        [Required]
        [MaxLength(2)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción oficial del tipo de documento de referencia.</summary>
        [Required]
        [MaxLength(200)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>Orden de despliegue en el selector.</summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ===== Auditoría =====
        // Generadas por la BD (DEFAULT now() / gen_random_uuid() y trigger de auditoría).
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
