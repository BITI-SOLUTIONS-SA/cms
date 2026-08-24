// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentTaxType.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de tipos de impuesto de Hacienda CR.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_tax_type
//              Es compartida por TODAS las compañías. Contiene el catálogo oficial
//              de códigos de impuesto (CodigoImpuesto) v4.4:
//              01=IVA, 02=Selectivo consumo, 03=Único combustibles, ..., 99=Otros.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_tax_type", Schema = "admin")]
    public class ElectronicDocumentTaxType
    {
        [Key]
        [Column("id_electronic_document_tax_type")]
        public int Id { get; set; }

        /// <summary>CodigoImpuesto Hacienda, formato '00' (01=IVA, 02=Selectivo, ..., 99=Otros).</summary>
        [Required]
        [MaxLength(2)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción oficial del tipo de impuesto.</summary>
        [Required]
        [MaxLength(200)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>Orden de despliegue en el selector.</summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Si es true, el modal de impuesto muestra y exige el selector "Código de la tarifa"
        /// (admin.electronic_document_tax_rate). NOT NULL, default false; solo el id 1 (IVA) es true.
        /// </summary>
        [Column("requires_tax_rate")]
        public bool RequiresTaxRate { get; set; } = false;

        // ===== Auditoría =====
        // Generadas por la BD (DEFAULT now() / gen_random_uuid() y trigger de auditoría).
        // Se marcan como Computed para que EF NO las envíe en INSERT/UPDATE y respete
        // los DEFAULT del servidor.
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
