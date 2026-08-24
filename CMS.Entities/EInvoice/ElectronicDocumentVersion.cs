// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentVersion.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de versiones del esquema de documentos
//            electrónicos de Hacienda CR (4.2, 4.3, 4.4...).
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_version
//              Solo un registro puede ser la versión vigente (IsCurrent). Esa es
//              la versión que el sistema usa para generar los documentos.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_version", Schema = "admin")]
    public class ElectronicDocumentVersion
    {
        [Key]
        [Column("id_electronic_document_version")]
        public int Id { get; set; }

        [Required]
        [MaxLength(10)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("description")]
        public string? Description { get; set; }

        // ⭐ Solo un registro puede tenerlo en TRUE (índice único parcial en BD)
        [Column("is_current")]
        public bool IsCurrent { get; set; }

        [Column("effective_date")]
        public DateTime? EffectiveDate { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        // ===== Auditoría =====
        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(30)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = "SYSTEM";

        [Required]
        [MaxLength(30)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = "SYSTEM";

        [Column("rowpointer")]
        public Guid RowPointer { get; set; } = Guid.NewGuid();
    }
}
