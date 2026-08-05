// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ElectronicDocumentReference.cs
// PROPÓSITO: Referencia a documentos previos (obligatorio en NC/ND/REP)
// DESCRIPCIÓN: Apunta a la Clave Numérica de 50 díg. del documento referenciado.
//              Sin esta referencia, Hacienda rechaza NC/ND/REP.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Referencia a documento previo. Tabla: {schema}.electronic_document_reference.
    /// </summary>
    [Table("electronic_document_reference")]
    public class ElectronicDocumentReference
    {
        [Key]
        [Column("id_electronic_document_reference")]
        public int Id { get; set; }

        [Column("id_electronic_document")]
        public int IdElectronicDocument { get; set; }

        [ForeignKey(nameof(IdElectronicDocument))]
        public virtual ElectronicDocument? ElectronicDocument { get; set; }

        /// <summary>Tipo de documento referenciado.</summary>
        [Required]
        [MaxLength(2)]
        [Column("ref_document_type")]
        public string RefDocumentType { get; set; } = "01";

        /// <summary>Clave Numérica de 50 díg. del documento referenciado.</summary>
        [Required]
        [MaxLength(50)]
        [Column("ref_clave")]
        public string RefClave { get; set; } = string.Empty;

        [Column("ref_date")]
        public DateTime RefDate { get; set; }

        /// <summary>Código de referencia (01=anula, 02=corrige monto, 04=referencia otro doc...).</summary>
        [Required]
        [MaxLength(2)]
        [Column("ref_code")]
        public string RefCode { get; set; } = "01";

        [Required]
        [MaxLength(180)]
        [Column("ref_reason")]
        public string RefReason { get; set; } = string.Empty;

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
