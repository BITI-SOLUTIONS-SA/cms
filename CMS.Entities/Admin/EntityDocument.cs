using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Admin
{
    /// <summary>
    /// Entity Document - Catálogo central de tipos de documentos asociados a tipos de entidad
    /// Tabla: admin.entity_document (BD Central: cms)
    /// </summary>
    [Table("entity_document", Schema = "admin")]
    public class EntityDocument : IAuditableEntity
    {
        [Key]
        [Column("id_entity_document")]
        public int ID_ENTITY_DOCUMENT { get; set; }

        [Column("id_entity_type")]
        [Required]
        public int ID_ENTITY_TYPE { get; set; }

        [Column("code")]
        [Required]
        [MaxLength(10)]
        public string CODE { get; set; } = default!;

        [Column("name")]
        [Required]
        [MaxLength(100)]
        public string NAME { get; set; } = default!;

        [Column("description")]
        [MaxLength(500)]
        public string? DESCRIPTION { get; set; }

        [Column("is_active")]
        public bool IS_ACTIVE { get; set; } = true;

        [Column("sort_order")]
        public int SORT_ORDER { get; set; } = 0;

        // Auditoría
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

        // Navigation properties
        [ForeignKey("ID_ENTITY_TYPE")]
        public virtual EntityType? EntityType { get; set; }
    }
}
