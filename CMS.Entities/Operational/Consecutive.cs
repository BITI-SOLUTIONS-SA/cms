using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Consecutive - Configuración de consecutivos por compañía
    /// Tabla: sinai.consecutive (BD Operacional por compañía)
    /// </summary>
    [Table("consecutive", Schema = "sinai")]
    public class Consecutive : IAuditableEntity
    {
        [Key]
        [Column("id_consecutive")]
        public int ID_CONSECUTIVE { get; set; }

        [Column("code")]
        [Required]
        [MaxLength(50)]
        public string CODE { get; set; } = default!;

        [Column("description")]
        [Required]
        [MaxLength(200)]
        public string DESCRIPTION { get; set; } = default!;

        /// <summary>
        /// Relación lógica cross-DB: referencia admin.entity_type.id_entity_type (BD central)
        /// No se puede declarar FK real porque las tablas están en diferentes bases de datos.
        /// La integridad se mantiene a nivel de aplicación.
        /// </summary>
        [Column("id_entity_type")]
        [Required]
        public int ID_ENTITY_TYPE { get; set; }

        /// <summary>
        /// Relación lógica cross-DB: referencia admin.entity_document.id_entity_document (BD central)
        /// No se puede declarar FK real porque las tablas están en diferentes bases de datos.
        /// La integridad se mantiene a nivel de aplicación.
        /// </summary>
        [Column("id_entity_document")]
        [Required]
        public int ID_ENTITY_DOCUMENT { get; set; }

        /// <summary>
        /// Relación lógica cross-DB: referencia admin.menu.id_menu (BD central)
        /// Asocia el consecutivo con un menú específico del sistema para diferenciar
        /// numeraciones según el módulo/proceso (ej: asiento manual vs cierre contable).
        /// No se puede declarar FK real porque las tablas están en diferentes bases de datos.
        /// La integridad se mantiene a nivel de aplicación.
        /// </summary>
        [Column("id_menu")]
        [Required]
        public int ID_MENU { get; set; }

        [Column("mask")]
        [Required]
        [MaxLength(50)]
        public string MASK { get; set; } = default!;

        [Column("length")]
        public int LENGTH { get; set; } = 4;

        [Column("initial_value")]
        [Required]
        [MaxLength(50)]
        public string INITIAL_VALUE { get; set; } = default!;

        [Column("final_value")]
        [Required]
        [MaxLength(50)]
        public string FINAL_VALUE { get; set; } = default!;

        [Column("last_value")]
        [MaxLength(50)]
        public string? LAST_VALUE { get; set; }

        [Column("last_user")]
        public int? LAST_USER { get; set; }

        [Column("last_date")]
        public DateTime? LAST_DATE { get; set; }

        [Column("is_active")]
        public bool IS_ACTIVE { get; set; } = true;

        // Auditoría
        // Generadas por la BD (DEFAULT gen_random_uuid() / now() y trigger).
        // Computed => EF no las envía en INSERT/UPDATE (evita rowpointer duplicado).
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("rowpointer")]
        public Guid RowPointer { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
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
