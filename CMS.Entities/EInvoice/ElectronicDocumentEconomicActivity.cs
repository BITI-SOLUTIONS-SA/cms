// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentEconomicActivity.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de actividades económicas de Hacienda CR.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_economic_activity
//              Es compartida por TODAS las compañías. Contiene el 100% del catálogo
//              oficial de actividades económicas (CodigoActividad, formato 0000.0).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_economic_activity", Schema = "admin")]
    public class ElectronicDocumentEconomicActivity
    {
        [Key]
        [Column("id_electronic_document_economic_activity")]
        public int Id { get; set; }

        /// <summary>CodigoActividad Hacienda, formato '0000.0'.</summary>
        [Required]
        [MaxLength(6)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción oficial de la actividad económica.</summary>
        [Required]
        [MaxLength(500)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ===== Auditoría =====
        // Generadas por la BD (DEFAULT now() / gen_random_uuid() y trigger de auditoría).
        // Se marcan como Computed para que EF NO las envíe en INSERT/UPDATE y así
        // respete los DEFAULT del servidor (evita rowpointer duplicado = Guid.Empty).
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
