// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/IElectronicDocumentService.cs
// PROPÓSITO: Interfaz del orquestador de emisión de comprobantes electrónicos
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

namespace CMS.Data.Services.EInvoice
{
    /// <summary>Línea de entrada para emitir un comprobante.</summary>
    public sealed class EmitLineInput
    {
        public int? ItemId { get; set; }
        public string CabysCode { get; set; } = string.Empty;
        public string? ItemCode { get; set; }
        public string Detail { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1;
        public string UnitMeasure { get; set; } = "Unid";
        public decimal UnitPrice { get; set; }
        public bool PriceIncludesTax { get; set; }
        public decimal TaxRatePercent { get; set; } = 13m;
        public string TaxRateCode { get; set; } = "08";
        public decimal DiscountAmount { get; set; }
        public string? DiscountNature { get; set; }
        /// <summary>TRUE = servicio; FALSE = mercancía/bien. Afecta el desglose del resumen.</summary>
        public bool IsService { get; set; } = true;

        // ===== Exoneración por línea =====
        public bool IsExonerated { get; set; }
        public string? ExonDocumentType { get; set; }
        public string? ExonDocumentNumber { get; set; }
        public string? ExonInstitution { get; set; }
        public DateTime? ExonDate { get; set; }
        /// <summary>PorcentajeExoneracion 0..100.</summary>
        public decimal ExonPercent { get; set; }
    }

    /// <summary>Referencia a documento previo (NC/ND/REP).</summary>
    public sealed class EmitReferenceInput
    {
        public string RefDocumentType { get; set; } = "01";
        public string RefClave { get; set; } = string.Empty;
        public DateTime RefDate { get; set; } = DateTime.UtcNow;
        public string RefCode { get; set; } = "01";
        public string RefReason { get; set; } = string.Empty;
    }

    /// <summary>Datos de entrada para emitir un comprobante electrónico.</summary>
    public sealed class EmitDocumentInput
    {
        public int CompanyId { get; set; }
        public int IssuerId { get; set; }
        public int? ReceptorId { get; set; }
        public string DocumentType { get; set; } = "01";
        public string SaleCondition { get; set; } = "01";
        public int? CreditTerm { get; set; }
        public string PaymentMethod { get; set; } = "01";
        public string Currency { get; set; } = "CRC";
        public decimal ExchangeRate { get; set; } = 1;
        public string Branch { get; set; } = "001";
        public string Terminal { get; set; } = "00001";
        public int UserId { get; set; }
        /// <summary>TRUE = documento exonerado completo (todas las líneas exoneradas).</summary>
        public bool IsExonerated { get; set; }
        public List<EmitLineInput> Lines { get; set; } = new();
        public List<EmitReferenceInput> References { get; set; } = new();
    }

    /// <summary>Resultado de la emisión.</summary>
    public sealed class EmitDocumentResult
    {
        public int DocumentId { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Consecutive { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool SentToHacienda { get; set; }
        public string? Message { get; set; }

        /// <summary>Bitácora paso a paso del proceso de emisión (para consola UI).</summary>
        public List<EmitLogEntry> Logs { get; set; } = new();
    }

    /// <summary>Entrada de bitácora del proceso de emisión (paso a paso).</summary>
    public sealed class EmitLogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Step { get; set; } = string.Empty;
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Orquesta la emisión de comprobantes electrónicos: valida, calcula, genera la
    /// clave, arma y firma el XML, e intenta el envío a Hacienda (con contingencia).
    /// </summary>
    public interface IElectronicDocumentService
    {
        Task<EmitDocumentResult> EmitAsync(EmitDocumentInput input, CancellationToken cancellationToken = default);

        /// <summary>Procesa un documento pendiente/contingencia: envía y consulta estado.</summary>
        Task ProcessPendingAsync(int companyId, int documentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Consulta on-demand el estado actual del comprobante en Hacienda (ind-estado),
        /// actualiza el documento si Hacienda ya resolvió (aceptado/rechazado) y registra
        /// el resultado en la bitácora. Devuelve el estado resultante.
        /// </summary>
        Task<PollStatusResult> PollStatusAsync(int companyId, int documentId, CancellationToken cancellationToken = default);
    }

    /// <summary>Resultado de una consulta de estado on-demand a Hacienda.</summary>
    public sealed class PollStatusResult
    {
        /// <summary>Estado del documento tras la consulta (Aceptado/Rechazado/Procesando/etc).</summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>Estado crudo reportado por Hacienda (ind-estado).</summary>
        public string? HaciendaStatus { get; set; }
        /// <summary>Detalle o mensaje devuelto por Hacienda.</summary>
        public string? HaciendaDetail { get; set; }
        /// <summary>TRUE si Hacienda ya resolvió el comprobante (aceptado o rechazado).</summary>
        public bool Resolved { get; set; }
        /// <summary>Mensaje legible para el usuario.</summary>
        public string? Message { get; set; }
    }
}
