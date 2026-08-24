// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentOtherCharge.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de "Otros cargos" (OtroCargo) de Hacienda CR.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_other_charges
//              Compartida por TODAS las compañías. Contiene el catálogo oficial del
//              código "Tipo documento otros cargos" v4.4: 01=Contribución parafiscal,
//              02=Timbre de la Cruz Roja, ..., 99=Otros cargos.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_other_charges", Schema = "admin")]
    public class ElectronicDocumentOtherCharge
    {
        [Key]
        [Column("id_electronic_document_other_charges")]
        public int Id { get; set; }

        /// <summary>TipoDocumento del otro cargo, formato '00' (01..13, 99).</summary>
        [Required]
        [MaxLength(2)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción oficial del tipo de otro cargo.</summary>
        [Required]
        [MaxLength(200)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>Indica si el tipo exige identificación del tercero (ej. 04 Cobro de un tercero).</summary>
        [Column("requires_identification")]
        public bool RequiresIdentification { get; set; } = false;

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
