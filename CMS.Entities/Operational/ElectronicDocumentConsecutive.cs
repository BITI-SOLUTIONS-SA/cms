// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ElectronicDocumentConsecutive.cs
// PROPÓSITO: Consecutivo fiscal por emisor/sucursal/terminal/tipo/versión.
// DESCRIPCIÓN: Reemplaza a FiscalConsecutive. Genera la secuencia de 10 dígitos que
//              forma parte del consecutivo de 20 díg. y de la Clave Numérica de 50 díg.
//              Está ligada a los catálogos centrales admin.electronic_document_type y
//              admin.electronic_document_version (relaciones lógicas cross-DB).
//              Un único registro DEFAULT por (emisor + tipo + versión) aparece primero
//              al emitir; el usuario puede usar cualquier otro que esté activo.
//              Se incrementa con bloqueo Serializable.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Consecutivo fiscal. Tabla: {schema}.electronic_document_consecutives.
    /// Único por (id_billing_issuer, branch, terminal, id_electronic_document_type, id_electronic_document_version).
    /// </summary>
    [Table("electronic_document_consecutives")]
    public class ElectronicDocumentConsecutive
    {
        [Key]
        [Column("id_electronic_document_consecutive")]
        public int Id { get; set; }

        /// <summary>Emisor (id_customer del emisor). Sin FK real.</summary>
        [Column("id_billing_issuer")]
        public int IdBillingIssuer { get; set; }

        /// <summary>Relación lógica cross-DB a cms.admin.electronic_document_type.</summary>
        [Column("id_electronic_document_type")]
        public int IdElectronicDocumentType { get; set; }

        /// <summary>Relación lógica cross-DB a cms.admin.electronic_document_version.</summary>
        [Column("id_electronic_document_version")]
        public int IdElectronicDocumentVersion { get; set; }

        /// <summary>Tipo de documento (2 díg.): 01,02,03,04,05,08,09,10.</summary>
        [Required]
        [MaxLength(2)]
        [Column("document_type")]
        public string DocumentType { get; set; } = "01";

        /// <summary>Sucursal (3 díg.), casa matriz = '001'.</summary>
        [Required]
        [MaxLength(3)]
        [Column("branch")]
        public string Branch { get; set; } = "001";

        /// <summary>Terminal/POS (5 díg.), por defecto '00001'.</summary>
        [Required]
        [MaxLength(5)]
        [Column("terminal")]
        public string Terminal { get; set; } = "00001";

        /// <summary>Última secuencia usada (hasta 10 díg.).</summary>
        [Column("consecutive")]
        public long Consecutive { get; set; }

        /// <summary>Consecutivo por defecto. Solo uno por (emisor+tipo+versión).</summary>
        [Column("is_default")]
        public bool IsDefault { get; set; }

        /// <summary>Indica si el consecutivo está activo y disponible para emisión.</summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Descripción opcional (ej: "Casa Matriz - Caja 1").</summary>
        [MaxLength(200)]
        [Column("description")]
        public string? Description { get; set; }

        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        // ===== Auditoría =====
        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("rowpointer")]
        public Guid RowPointer { get; set; } = Guid.NewGuid();
    }
}
