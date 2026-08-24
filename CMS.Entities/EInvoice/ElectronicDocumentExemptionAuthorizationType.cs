// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentExemptionAuthorizationType.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de "Tipo de documento de exoneración o
//            de autorización" (TipoDocumentoEX) de Hacienda CR v4.4.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_exemption_authorization_type
//              Es compartida por TODAS las compañías.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_exemption_authorization_type", Schema = "admin")]
    public class ElectronicDocumentExemptionAuthorizationType
    {
        [Key]
        [Column("id_electronic_document_exemption_authorization_type")]
        public int Id { get; set; }

        /// <summary>TipoDocumentoEX Hacienda, formato '00' (01..99).</summary>
        [Required]
        [MaxLength(2)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Nombre / descripción del tipo de documento de exoneración.</summary>
        [Required]
        [MaxLength(300)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Descripción extendida.</summary>
        [MaxLength(1000)]
        [Column("description")]
        public string? Description { get; set; }

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
