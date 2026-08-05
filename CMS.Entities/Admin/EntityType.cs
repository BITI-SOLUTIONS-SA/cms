using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Admin
{
    /// <summary>
    /// Entity Type - Catálogo central de tipos de entidad del sistema
    /// Tabla: admin.entity_type (BD Central: cms)
    /// </summary>
    [Table("entity_type", Schema = "admin")]
    public class EntityType : IAuditableEntity
    {
        [Key]
        [Column("id_entity_type")]
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
        public virtual ICollection<EntityDocument> EntityDocuments { get; set; } = new List<EntityDocument>();
    }
}
