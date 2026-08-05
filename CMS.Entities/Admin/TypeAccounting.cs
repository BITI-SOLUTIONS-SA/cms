// ================================================================================
// ARCHIVO: CMS.Entities/Admin/TypeAccounting.cs
// PROPÓSITO: Catálogo central de tipos de contabilidad
// DESCRIPCIÓN: Define el enfoque contable bajo el cual se registran los asientos
//              de diario. Ejemplos: general, fiscal, corporate.
//              Tabla: admin.type_accounting (BD central: cms)
//              La columna id_type_accounting de sinai.journal_entry referencia
//              lógicamente (cross-DB) a esta tabla.
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Admin
{
    [Table("type_accounting", Schema = "admin")]
    public class TypeAccounting
    {
        [Key]
        [Column("id_type_accounting")]
        public int Id { get; set; }

        /// <summary>
        /// Código único en snake_case. Ejemplos: general, fiscal, corporate
        /// </summary>
        [Required]
        [MaxLength(30)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción legible para mostrar en la UI</summary>
        [MaxLength(100)]
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>Clase Bootstrap Icons (ej: bi-journal-text, bi-percent)</summary>
        [MaxLength(50)]
        [Column("icon")]
        public string? Icon { get; set; }

        /// <summary>Orden de aparición en selectores y listas</summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ===== AUDITORÍA =====

        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(150)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("rowpointer")]
        public Guid RowPointer { get; set; } = Guid.NewGuid();
    }
}
