// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ElectronicDocumentLog.cs
// PROPÓSITO: Bitácora de seguimiento del proceso de emisión de un comprobante
//            electrónico (paso a paso: firma, envío a Hacienda, respuesta, etc.).
// DESCRIPCIÓN: Cada registro representa un paso/evento del pipeline de emisión.
//              Vive en la BD operacional de cada compañía. Tabla:
//              {schema}.electronic_document_log.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Bitácora de un evento/paso del proceso de emisión de un comprobante
    /// electrónico. Tabla: {schema}.electronic_document_log.
    /// </summary>
    [Table("electronic_document_log")]
    public class ElectronicDocumentLog
    {
        [Key]
        [Column("id_electronic_document_log")]
        public long Id { get; set; }

        /// <summary>Documento asociado (puede ser null si aún no se persistió).</summary>
        [Column("id_electronic_document")]
        public int? IdElectronicDocument { get; set; }

        /// <summary>Clave numérica de 50 dígitos (si ya está disponible).</summary>
        [Column("clave")]
        [MaxLength(50)]
        public string? Clave { get; set; }

        /// <summary>Paso del proceso (ej: VALIDACION, CLAVE, XML, FIRMA, ENVIO, RESPUESTA).</summary>
        [Column("step")]
        [MaxLength(50)]
        public string Step { get; set; } = string.Empty;

        /// <summary>Nivel del evento: INFO, SUCCESS, WARNING, ERROR.</summary>
        [Column("level")]
        [MaxLength(20)]
        public string Level { get; set; } = "INFO";

        /// <summary>Mensaje descriptivo del evento.</summary>
        [Column("message")]
        [MaxLength(2000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>Detalle adicional (respuesta cruda de Hacienda, stacktrace, etc.).</summary>
        [Column("detail")]
        public string? Detail { get; set; }

        // ── Auditoría ────────────────────────────────────────────────────────
        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        [MaxLength(30)]
        public string CreatedBy { get; set; } = "system";

        [Column("updated_by")]
        [MaxLength(30)]
        public string UpdatedBy { get; set; } = "system";

        [Column("rowpointer")]
        public Guid RowPointer { get; set; } = Guid.NewGuid();
    }
}
