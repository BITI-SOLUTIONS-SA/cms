// ================================================================================
// ARCHIVO: CMS.Entities/Admin/JournalEntryClass.cs
// PROPÓSITO: Catálogo central de clases de asiento de diario
// DESCRIPCIÓN: Clasifica los asientos según su naturaleza contable.
//              Ejemplos: N=Normal, C=Closing, D=Exchange Rate Differential, B=Banks
//              Tabla: admin.journal_entry_class (BD central: cms)
//              La columna id_journal_entry_class de sinai.journal_entry referencia
//              lógicamente (cross-DB) a esta tabla.
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Admin
{
    [Table("journal_entry_class", Schema = "admin")]
    public class JournalEntryClass
    {
        [Key]
        [Column("id_journal_entry_class")]
        public int Id { get; set; }

        /// <summary>
        /// Código único de una letra o sigla. Ejemplos: N, C, D, B
        /// </summary>
        [Required]
        [MaxLength(30)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción legible para mostrar en la UI</summary>
        [MaxLength(100)]
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>Clase Bootstrap Icons (ej: bi-journal-text, bi-bank)</summary>
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
