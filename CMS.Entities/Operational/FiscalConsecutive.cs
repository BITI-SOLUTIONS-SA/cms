// ================================================================================
// ARCHIVO: CMS.Entities/Operational/FiscalConsecutive.cs
// PROPÓSITO: Consecutivo fiscal por emisor/sucursal/terminal/tipo de documento
// DESCRIPCIÓN: Genera la secuencia de 10 dígitos que forma parte del consecutivo de
//              20 díg. y de la Clave Numérica de 50 díg. Es DISTINTO del Consecutive
//              interno del CMS. Se incrementa con bloqueo Serializable.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Consecutivo fiscal. Tabla: {schema}.fiscal_consecutive.
    /// Único por (id_billing_issuer, branch, terminal, document_type).
    /// </summary>
    [Table("fiscal_consecutive")]
    public class FiscalConsecutive
    {
        [Key]
        [Column("id_fiscal_consecutive")]
        public int Id { get; set; }

        [Column("id_billing_issuer")]
        public int IdBillingIssuer { get; set; }

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

        /// <summary>Tipo de documento (2 díg.): 01,02,03,04,08,09.</summary>
        [Required]
        [MaxLength(2)]
        [Column("document_type")]
        public string DocumentType { get; set; } = "01";

        /// <summary>Última secuencia usada (hasta 10 díg.).</summary>
        [Column("last_value")]
        public long LastValue { get; set; }

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
