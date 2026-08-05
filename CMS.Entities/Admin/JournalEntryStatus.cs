// ================================================================================
// ARCHIVO: CMS.Entities/Admin/JournalEntryStatus.cs
// PROPÓSITO: Catálogo central de estados de asiento de diario
// DESCRIPCIÓN: Normaliza los estados del sistema: Draft, Posted, Reversed, Cancelled.
//              El código coincide con el valor del campo status (varchar) en
//              sinai.journal_entry para mantener consistencia.
//              Tabla: admin.journal_entry_status (BD central: cms)
//              La columna id_journal_entry_status de sinai.journal_entry referencia
//              lógicamente (cross-DB) a esta tabla.
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Admin
{
    [Table("journal_entry_status", Schema = "admin")]
    public class JournalEntryStatus
    {
        [Key]
        [Column("id_journal_entry_status")]
        public int Id { get; set; }

        /// <summary>
        /// Código único. Coincide con el valor del campo status en sinai.journal_entry.
        /// Ejemplos: Draft, Posted, Reversed, Cancelled
        /// </summary>
        [Required]
        [MaxLength(30)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción legible para mostrar en la UI</summary>
        [MaxLength(100)]
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>Clase Bootstrap Icons (ej: bi-pencil-square, bi-check-circle-fill)</summary>
        [MaxLength(50)]
        [Column("icon")]
        public string? Icon { get; set; }

        /// <summary>Color Bootstrap para badges (ej: warning, success, info, danger)</summary>
        [MaxLength(30)]
        [Column("color")]
        public string? Color { get; set; }

        /// <summary>Orden de aparición en selectores y listas</summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ===== AUDITORÍA =====
        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [Required]
        [MaxLength(150)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("rowpointer")]
        public Guid RowPointer { get; set; }
    }
}
