// ================================================================================
// ARCHIVO: CMS.Entities/Admin/ChartOfAccountsType.cs
// PROPÓSITO: Catálogo central de tipos de cuenta del plan de cuentas
// DESCRIPCIÓN: Define la clasificación contable estándar de las cuentas:
//              Activo, Pasivo, Patrimonio, Ingreso, Gasto, Fuera de Balance.
//              Tabla: admin.chart_of_accounts_type (BD central: cms)
//              La columna id_chart_of_accounts_type de sinai.chart_of_accounts
//              referencia lógicamente (cross-DB) a esta tabla.
// AUTOR: BITI SOLUTIONS S.A
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Admin
{
    [Table("chart_of_accounts_type", Schema = "admin")]
    public class ChartOfAccountsType
    {
        [Key]
        [Column("id_chart_of_accounts_type")]
        public int Id { get; set; }

        /// <summary>
        /// Código único en snake_case. Ejemplos: asset, liability, equity, revenue, expense, off_balance
        /// </summary>
        [Required]
        [MaxLength(30)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción legible (ej: Activo, Pasivo, Patrimonio)</summary>
        [MaxLength(100)]
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>Clase Bootstrap Icons (ej: bi-box-seam, bi-bank)</summary>
        [MaxLength(50)]
        [Column("icon")]
        public string? Icon { get; set; }

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
