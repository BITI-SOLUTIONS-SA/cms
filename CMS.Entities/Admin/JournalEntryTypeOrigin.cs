// ================================================================================
// ARCHIVO: CMS.Entities/Admin/JournalEntryTypeOrigin.cs
// PROPÓSITO: Catálogo central de tipos de origen de asientos de diario
// DESCRIPCIÓN: Define desde qué módulo o proceso fue generado un asiento contable.
//              Tabla: admin.journal_entry_type_origin (BD central: cms)
//              Compartida por TODAS las compañías.
//              La columna id_type_origin de sinai.journal_entry_line hace
//              referencia lógica (cross-DB) a esta tabla.
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Admin
{
    [Table("journal_entry_type_origin", Schema = "admin")]
    public class JournalEntryTypeOrigin
    {
        [Key]
        [Column("id_journal_entry_type_origin")]
        public int Id { get; set; }

        /// <summary>
        /// Código único en snake_case que identifica el origen del asiento.
        /// Ejemplos: manual, accounts_payable, accounts_receivable, sales, purchases,
        ///           payroll, inventory, fixed_assets, banking, closing, opening, reversal
        /// </summary>
        [Required]
        [MaxLength(30)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción legible del tipo de origen para mostrar en la UI</summary>
        [MaxLength(100)]
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>Clase Bootstrap Icons (ej: bi-journal-text, bi-receipt)</summary>
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
