// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/CustomerType.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de tipos de cliente.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.customer_type
//              Es compartida por TODAS las compañías. Reemplaza el antiguo campo
//              string sinai.customer.customer_type. sinai.customer.id_customer_type
//              referencia (lógicamente, cross-DB) esta tabla.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("customer_type", Schema = "admin")]
    public class CustomerType
    {
        [Key]
        [Column("id_customer_type")]
        public int Id { get; set; }

        /// <summary>Código único del tipo de cliente (ej: Issuer, Receptor, Issuer-Receptor).</summary>
        [Required]
        [MaxLength(30)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Nombre para mostrar del tipo de cliente.</summary>
        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Descripción opcional del tipo de cliente.</summary>
        [MaxLength(500)]
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>Indica si este tipo puede actuar como emisor de documentos electrónicos.</summary>
        [Column("is_issuer")]
        public bool IsIssuer { get; set; }

        /// <summary>Indica si este tipo puede actuar como receptor de documentos electrónicos.</summary>
        [Column("is_receptor")]
        public bool IsReceptor { get; set; }

        /// <summary>Orden de despliegue en el selector.</summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ===== Auditoría =====
        // Generadas por la BD (DEFAULT now() / gen_random_uuid() y trigger de auditoría).
        // Se marcan como Computed para que EF NO las envíe en INSERT/UPDATE.
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
