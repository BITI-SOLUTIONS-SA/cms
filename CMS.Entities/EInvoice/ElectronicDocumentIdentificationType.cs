// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentIdentificationType.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de tipos de identificación de Hacienda CR.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_identification_type
//              Es compartida por TODAS las compañías. Contiene el catálogo oficial
//              de tipos de identificación (TipoIdentificacion, formato '00') v4.4.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_identification_type", Schema = "admin")]
    public class ElectronicDocumentIdentificationType
    {
        [Key]
        [Column("id_electronic_document_identification_type")]
        public int Id { get; set; }

        /// <summary>TipoIdentificacion Hacienda, formato '00' (01=Cédula física, 02=Cédula jurídica, ...).</summary>
        [Required]
        [MaxLength(2)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción oficial del tipo de identificación.</summary>
        [Required]
        [MaxLength(200)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>Longitud exacta esperada del número de identificación (null = variable).</summary>
        [Column("length")]
        public int? Length { get; set; }

        /// <summary>Patrón opcional (regex) para validar el número de identificación.</summary>
        [MaxLength(200)]
        [Column("regex_pattern")]
        public string? RegexPattern { get; set; }

        /// <summary>Texto de ayuda que describe el formato esperado del número.</summary>
        [MaxLength(200)]
        [Column("format_hint")]
        public string? FormatHint { get; set; }

        /// <summary>Orden de despliegue en el selector.</summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ===== Auditoría =====
        // Generadas por la BD (DEFAULT now() / gen_random_uuid() y trigger de auditoría).
        // Se marcan como Computed para que EF NO las envíe en INSERT/UPDATE y así
        // respete los DEFAULT del servidor (evita rowpointer duplicado = Guid.Empty).
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
