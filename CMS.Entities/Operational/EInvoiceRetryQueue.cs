// ================================================================================
// ARCHIVO: CMS.Entities/Operational/EInvoiceRetryQueue.cs
// PROPÓSITO: Cola de reintentos para resiliencia ante caídas de Hacienda
// DESCRIPCIÓN: Un worker en background procesa esta cola con backoff exponencial.
//              Vive en la BD operacional de cada compañía.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Cola de reintentos. Tabla: {schema}.einvoice_retry_queue.
    /// </summary>
    [Table("einvoice_retry_queue")]
    public class EInvoiceRetryQueue
    {
        [Key]
        [Column("id_einvoice_retry_queue")]
        public int Id { get; set; }

        [Column("id_electronic_document")]
        public int IdElectronicDocument { get; set; }

        /// <summary>Operación: 'send' | 'poll_status'.</summary>
        [Required]
        [MaxLength(20)]
        [Column("operation")]
        public string Operation { get; set; } = "send";

        [Column("attempt_count")]
        public int AttemptCount { get; set; }

        [Column("next_attempt_at")]
        public DateTime NextAttemptAt { get; set; }

        [Column("last_error")]
        public string? LastError { get; set; }

        [Column("is_done")]
        public bool IsDone { get; set; }

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
