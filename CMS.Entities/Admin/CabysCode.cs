// ================================================================================
// ARCHIVO: CMS.Entities/Admin/CabysCode.cs
// PROPÓSITO: Catálogo central CAByS (Catálogo de Bienes y Servicios) - 13 dígitos
// DESCRIPCIÓN: Catálogo gubernamental compartido por TODAS las compañías.
//              Se almacena en la BD central (cms), schema admin.
//              Cada código incluye la tarifa de IVA asociada.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Admin
{
    /// <summary>
    /// Código CAByS (Catálogo de Bienes y Servicios) de 13 dígitos.
    /// Tabla: admin.cabys (BD Central: cms).
    /// </summary>
    [Table("cabys", Schema = "admin")]
    public class CabysCode : IAuditableEntity
    {
        [Key]
        [Column("id_cabys")]
        public int ID_CABYS { get; set; }

        /// <summary>Código CAByS de 13 dígitos.</summary>
        [Column("code")]
        [Required]
        [MaxLength(13)]
        public string CODE { get; set; } = default!;

        /// <summary>Descripción del bien o servicio.</summary>
        [Column("description")]
        [Required]
        [MaxLength(1000)]
        public string DESCRIPTION { get; set; } = default!;

        /// <summary>Tarifa de IVA asociada (ej. 13.00, 4.00, 2.00, 1.00, 0.00).</summary>
        [Column("tax_rate", TypeName = "decimal(5,2)")]
        public decimal TAX_RATE { get; set; }

        /// <summary>Código de tarifa Hacienda (01=0%, 08=13%, etc.).</summary>
        [Column("tax_rate_code")]
        [MaxLength(2)]
        public string? TAX_RATE_CODE { get; set; }

        /// <summary>Jerarquía/categoría del código.</summary>
        [Column("category")]
        [MaxLength(500)]
        public string? CATEGORY { get; set; }

        [Column("is_active")]
        public bool IS_ACTIVE { get; set; } = true;

        // ===== Auditoría =====
        [Column("rowpointer")]
        public Guid RowPointer { get; set; }

        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [Column("created_by")]
        [Required]
        [MaxLength(30)]
        public string CreatedBy { get; set; } = default!;

        [Column("updated_by")]
        [Required]
        [MaxLength(30)]
        public string UpdatedBy { get; set; } = default!;
    }
}
