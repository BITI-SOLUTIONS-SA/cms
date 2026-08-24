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
        /// <summary>Tipo de impuesto Hacienda (CodigoImpuesto v4.4). FK lógica a admin.electronic_document_tax_type.</summary>
        public int IdElectronicDocumentTaxType { get; set; } = 1;
        /// <summary>TRUE cuando el usuario digitó el CAByS manualmente en lugar de resolverlo por código de ítem.</summary>
        public bool ManualCabys { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? DiscountNature { get; set; }
        /// <summary>
        /// Lista de descuentos de la línea (Hacienda v4.4 admite hasta 5 nodos
        /// &lt;Descuento&gt;). Cuando está poblada, DiscountAmount es la suma y
        /// DiscountNature la naturaleza del primer descuento.
        /// </summary>
        public List<EmitLineDiscountInput> Discounts { get; set; } = new();
        /// <summary>TRUE = servicio; FALSE = mercancía/bien. Afecta el desglose del resumen.</summary>
        public bool IsService { get; set; } = true;

        // ===== Exoneración por línea =====
        public bool IsExonerated { get; set; }
        public string? ExonDocumentType { get; set; }
        public string? ExonDocumentNumber { get; set; }
        public string? ExonInstitution { get; set; }
        public DateTime? ExonDate { get; set; }
        /// <summary>Número de artículo que establece la exoneración (opcional, máx 6 dígitos).</summary>
        public int? ExonArticle { get; set; }
        /// <summary>Número de inciso que establece la exoneración (obligatorio cuando IsExonerated, máx 6 dígitos).</summary>
        public int? ExonSubsection { get; set; }
        /// <summary>PorcentajeExoneracion 0..100.</summary>
        public decimal ExonPercent { get; set; }

        // ===== Múltiples impuestos por línea =====
        /// <summary>
        /// Impuestos de la línea. Cuando está poblada, cada elemento genera un
        /// impuesto propio con su exoneración; si está vacía se usa el impuesto
        /// plano de arriba (retrocompatibilidad).
        /// </summary>
        public List<EmitLineTaxInput> Taxes { get; set; } = new();
    }

    /// <summary>Un impuesto de la línea con su exoneración opcional.</summary>
    public sealed class EmitLineTaxInput
    {
        public int IdElectronicDocumentTaxType { get; set; } = 1;
        public string TaxCode { get; set; } = "01";
        public decimal TaxRatePercent { get; set; } = 13m;
        public string? TaxRateCode { get; set; }

        // ===== Datos físicos de impuestos específicos / cálculo especial (v4.4) =====
        /// <summary>Cantidad de la unidad de medida a utilizar (códigos 03, 04, 06).</summary>
        public decimal? UnitMeasureQty { get; set; }
        /// <summary>Volumen por unidad de consumo (código 05).</summary>
        public decimal? VolumeUnit { get; set; }
        /// <summary>Porcentaje usado en el cálculo (código 04 - bebidas alcohólicas).</summary>
        public decimal? SpecPercent { get; set; }
        /// <summary>Proporción calculada (código 04) = UnitMeasureQty * SpecPercent / 100.</summary>
        public decimal? Proportion { get; set; }
        /// <summary>Impuesto por unidad (códigos 03, 04, 05, 06).</summary>
        public decimal? PerUnitTax { get; set; }
        /// <summary>Base imponible especial digitada (código 07 - IVA cálculo especial).</summary>
        public decimal? SpecialTaxableBase { get; set; }
        /// <summary>TRUE si el producto tiene impuesto cobrado a nivel de fábrica (código 07).</summary>
        public bool IsFactoryTax { get; set; }
        /// <summary>Monto del impuesto ya calculado por la UI (opcional; el backend puede recalcular).</summary>
        public decimal? TaxAmount { get; set; }
        /// <summary>Descripción libre del impuesto (código 99 - Otros, v4.4).</summary>
        public string? TaxDescription { get; set; }

        public bool IsExonerated { get; set; }
        public string? ExonDocumentType { get; set; }
        public string? ExonDocumentNumber { get; set; }
        public string? ExonInstitution { get; set; }
        public DateTime? ExonDate { get; set; }
        public int? ExonArticle { get; set; }
        public int? ExonSubsection { get; set; }
        public decimal ExonPercent { get; set; }
    }

    /// <summary>Descuento individual de una línea (Hacienda v4.4).</summary>
    public sealed class EmitLineDiscountInput
    {
        /// <summary>Naturaleza/código del descuento (04=Volumen, 05=Temporada, 06=Promoción...).</summary>
        public string? Nature { get; set; }
        /// <summary>Monto del descuento concedido (mayor a 0).</summary>
        public decimal Amount { get; set; }
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

    /// <summary>
    /// Otro cargo a nivel de documento (nodo &lt;OtrosCargos&gt;&lt;OtroCargo&gt; Hacienda CR v4.4).
    /// Los datos del tercero son opcionales; cuando el tipo es "99" se usa
    /// <see cref="OtherTypeDescription"/> para la descripción libre del documento.
    /// </summary>
    public sealed class EmitOtherChargeInput
    {
        /// <summary>Código Hacienda del tipo de documento de otros cargos (TipoDocumentoOC, ej: "01".."99").</summary>
        public string TypeCode { get; set; } = string.Empty;
        /// <summary>Descripción del "otro" tipo de documento; obligatoria solo cuando <see cref="TypeCode"/> es "99".</summary>
        public string? OtherTypeDescription { get; set; }
        /// <summary>Detalle del cargo (Detalle).</summary>
        public string Detail { get; set; } = string.Empty;
        /// <summary>Monto del cargo (MontoCargo), mayor a 0.</summary>
        public decimal Amount { get; set; }
        /// <summary>(Opcional) Porcentaje informativo del cargo.</summary>
        public decimal? Percent { get; set; }
        /// <summary>(Opcional) Código del tipo de identificación del tercero.</summary>
        public string? ThirdIdentType { get; set; }
        /// <summary>(Opcional) Número de identificación del tercero.</summary>
        public string? ThirdIdentNumber { get; set; }
        /// <summary>(Opcional) Nombre del tercero.</summary>
        public string? ThirdName { get; set; }
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
        /// <summary>(Opcional) Lista de medios de pago seleccionados, separados por coma (CSV).
        /// Ej: "01,02,06". Si viene vacío se usa <see cref="PaymentMethod"/>.</summary>
        public string? PaymentMethods { get; set; }
        public string Currency { get; set; } = "CRC";
        public decimal ExchangeRate { get; set; } = 1;
        public string Branch { get; set; } = "001";
        public string Terminal { get; set; } = "00001";
        public int UserId { get; set; }
        /// <summary>(Opcional) Id del consecutivo fiscal seleccionado por el usuario en la pantalla de emisión.</summary>
        public int? ConsecutiveId { get; set; }
        /// <summary>(Opcional) Código de actividad económica del emisor seleccionado en la pantalla de emisión.</summary>
        public string? IssuerEconomicActivity { get; set; }
        /// <summary>(Opcional) Código de actividad económica del receptor seleccionado en la pantalla de emisión.</summary>
        public string? ReceptorEconomicActivity { get; set; }
        /// <summary>(Opcional) Correo del emisor editado inline para este comprobante (no persiste en la credencial).</summary>
        public string? IssuerEmailOverride { get; set; }
        /// <summary>(Opcional) Teléfono del emisor editado inline para este comprobante (no persiste en la credencial).</summary>
        public string? IssuerPhoneOverride { get; set; }
        /// <summary>(Opcional) Correo del receptor editado inline para este comprobante (no persiste en el proveedor).</summary>
        public string? ReceptorEmailOverride { get; set; }
        /// <summary>(Opcional) Teléfono del receptor editado inline para este comprobante (no persiste en el proveedor).</summary>
        public string? ReceptorPhoneOverride { get; set; }
        /// <summary>TRUE = documento exonerado completo (todas las líneas exoneradas).</summary>
        public bool IsExonerated { get; set; }
        public List<EmitLineInput> Lines { get; set; } = new();
        public List<EmitReferenceInput> References { get; set; } = new();
        /// <summary>(Opcional) Otros cargos (OtroCargo) a nivel de documento (Hacienda CR v4.4).</summary>
        public List<EmitOtherChargeInput> OtherCharges { get; set; } = new();
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
