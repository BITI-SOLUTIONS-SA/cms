// ================================================================================
// ARCHIVO: CMS.Application/DTOs/EInvoice/EInvoiceDtos.cs
// PROPÓSITO: DTOs del módulo de Facturación Electrónica CR v4.4
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

namespace CMS.Application.DTOs.EInvoice
{
    // ===== EMISOR =====

    public class BillingIssuerDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public bool IsMaster { get; set; }
        public string IdentificationType { get; set; } = "02";
        public string Identification { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CommercialName { get; set; }
        public string? EconomicActivity { get; set; }
        public bool IsSpecialRegime { get; set; }
        public string? SpecialRegimeCode { get; set; }
        public string? Province { get; set; }
        public string? Canton { get; set; }
        public string? District { get; set; }
        public string? OtherSigns { get; set; }
        public string? PhoneCode { get; set; }
        public string? Phone { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Environment { get; set; } = "stag";
        public bool IsActive { get; set; } = true;
    }

    public class UpsertBillingIssuerDto
    {
        public string Code { get; set; } = string.Empty;
        public bool IsMaster { get; set; }
        public string IdentificationType { get; set; } = "02";
        public string Identification { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CommercialName { get; set; }
        public string? EconomicActivity { get; set; }
        public bool IsSpecialRegime { get; set; }
        public string? SpecialRegimeCode { get; set; }
        public string? Province { get; set; }
        public string? Canton { get; set; }
        public string? District { get; set; }
        public string? OtherSigns { get; set; }
        public string? PhoneCode { get; set; }
        public string? Phone { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Environment { get; set; } = "stag";
    }

    // ===== CREDENCIALES (Vault) =====

    /// <summary>Carga del .p12 + PIN + OAuth. El contenido nunca se devuelve al cliente.</summary>
    public class UploadCredentialDto
    {
        public int IssuerId { get; set; }
        /// <summary>Ambiente de la credencial: 'stag' (pruebas) | 'prod' (producción).</summary>
        public string Environment { get; set; } = "stag";
        /// <summary>Contenido del .p12 en base64.</summary>
        public string P12Base64 { get; set; } = string.Empty;
        public string Pin { get; set; } = string.Empty;
        public string? OAuthUsername { get; set; }
        public string? OAuthPassword { get; set; }
    }

    public class CredentialStatusDto
    {
        public int IssuerId { get; set; }
        public string Environment { get; set; } = "stag";
        public bool HasCredential { get; set; }
        public DateTime? CertNotBefore { get; set; }
        public DateTime? CertNotAfter { get; set; }
        public bool IsExpired { get; set; }
    }

    /// <summary>Estado combinado de credenciales de ambos ambientes + ambiente activo.</summary>
    public class IssuerCredentialsOverviewDto
    {
        public int IssuerId { get; set; }
        public string ActiveEnvironment { get; set; } = "stag";
        public CredentialStatusDto Sandbox { get; set; } = new();
        public CredentialStatusDto Production { get; set; } = new();
    }

    /// <summary>Cambia el ambiente activo del emisor (pruebas -> producción).</summary>
    public class SetActiveEnvironmentDto
    {
        public string Environment { get; set; } = "stag";
    }

    // ===== RECEPTOR =====

    public class BillingReceptorDto
    {
        public int Id { get; set; }
        public int IssuerId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? IdentificationType { get; set; }
        public string? Identification { get; set; }
        public string? ForeignIdentification { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CommercialName { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // ===== CAByS =====

    public class CabysDto
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal TaxRate { get; set; }
        public string? TaxRateCode { get; set; }
        public string? Category { get; set; }
    }

    // ===== EMISIÓN =====

    public class EmitLineDto
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
        /// Lista de descuentos de la línea (Hacienda v4.4 admite hasta 5). Cuando
        /// está poblada, DiscountAmount debe ser la suma y DiscountNature la
        /// naturaleza del primer descuento.
        /// </summary>
        public List<EmitLineDiscountDto> Discounts { get; set; } = new();
        /// <summary>TRUE = servicio; FALSE = mercancía/bien.</summary>
        public bool IsService { get; set; } = true;

        // ===== Exoneración por línea =====
        /// <summary>TRUE si la línea está exonerada del IVA (total o parcial).</summary>
        public bool IsExonerated { get; set; }
        public string? ExonDocumentType { get; set; }
        public string? ExonDocumentNumber { get; set; }
        public string? ExonInstitution { get; set; }
        public DateTime? ExonDate { get; set; }
        /// <summary>Número de artículo que establece la exoneración (opcional, máx 6 dígitos).</summary>
        public int? ExonArticle { get; set; }
        /// <summary>Número de inciso que establece la exoneración (obligatorio cuando IsExonerated, máx 6 dígitos).</summary>
        public int? ExonSubsection { get; set; }
        /// <summary>PorcentajeExoneracion 0..100. Default 100 cuando IsExonerated y no se indica.</summary>
        public decimal ExonPercent { get; set; }

        // ===== Múltiples impuestos por línea =====
        /// <summary>
        /// Impuestos de la línea (Hacienda v4.4 admite varios: IVA, selectivo de
        /// consumo, etc.). Cuando está poblada, cada elemento genera un bloque
        /// &lt;Impuesto&gt; propio y su exoneración es independiente. Si está vacía,
        /// se usa el impuesto plano definido arriba (retrocompatibilidad).
        /// </summary>
        public List<EmitLineTaxDto> Taxes { get; set; } = new();
    }

    /// <summary>Un impuesto de la línea, con su exoneración opcional.</summary>
    public class EmitLineTaxDto
    {
        /// <summary>Tipo de impuesto Hacienda (FK lógica a admin.electronic_document_tax_type).</summary>
        public int IdElectronicDocumentTaxType { get; set; } = 1;
        /// <summary>Código de impuesto Hacienda (CodigoImpuesto v4.4, p.ej. 01=IVA, 02=selectivo).</summary>
        public string TaxCode { get; set; } = "01";
        /// <summary>Porcentaje de la tarifa del impuesto (p.ej. 13).</summary>
        public decimal TaxRatePercent { get; set; } = 13m;
        /// <summary>Código de tarifa (01..08) cuando aplica.</summary>
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
        /// <summary>Descripción libre del impuesto (código 99 - Otros, v4.4). Máx 160 caracteres.</summary>
        public string? TaxDescription { get; set; }
        // ===== Exoneración de este impuesto =====
        /// <summary>TRUE si este impuesto está exonerado (total o parcial).</summary>
        public bool IsExonerated { get; set; }
        public string? ExonDocumentType { get; set; }
        public string? ExonDocumentNumber { get; set; }
        public string? ExonInstitution { get; set; }
        public DateTime? ExonDate { get; set; }
        /// <summary>Número de artículo que establece la exoneración (opcional, máx 6 dígitos).</summary>
        public int? ExonArticle { get; set; }
        /// <summary>Número de inciso que establece la exoneración (obligatorio cuando IsExonerated).</summary>
        public int? ExonSubsection { get; set; }
        /// <summary>PorcentajeExoneracion 0..100 (% del impuesto exonerado).</summary>
        public decimal ExonPercent { get; set; }
    }

    public class EmitLineDiscountDto
    {
        /// <summary>Naturaleza/código del descuento (04=Volumen, 05=Temporada, 06=Promoción...).</summary>
        public string? Nature { get; set; }
        /// <summary>Monto del descuento concedido (debe ser mayor a 0).</summary>
        public decimal Amount { get; set; }
    }

    public class EmitReferenceDto
    {
        public string RefDocumentType { get; set; } = "01";
        public string RefClave { get; set; } = string.Empty;
        public DateTime RefDate { get; set; } = DateTime.UtcNow;
        public string RefCode { get; set; } = "01";
        public string RefReason { get; set; } = string.Empty;
    }

    public class EmitOtherChargeDto
    {
        /// <summary>Código Hacienda del tipo de documento de otros cargos (ej: "01".."99").</summary>
        public string TypeCode { get; set; } = string.Empty;
        /// <summary>Descripción del "otro" tipo de documento; usada cuando TypeCode es "99".</summary>
        public string? OtherTypeDescription { get; set; }
        public string Detail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        /// <summary>(Opcional) Porcentaje informativo del cargo.</summary>
        public decimal? Percent { get; set; }
        public string? ThirdIdentType { get; set; }
        public string? ThirdIdentNumber { get; set; }
        public string? ThirdName { get; set; }
    }

    public class EmitDocumentDto
    {
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
        public List<EmitLineDto> Lines { get; set; } = new();
        public List<EmitReferenceDto> References { get; set; } = new();
        /// <summary>(Opcional) Otros cargos (OtroCargo) a nivel de documento (Hacienda CR v4.4).</summary>
        public List<EmitOtherChargeDto> OtherCharges { get; set; } = new();
    }

    public class EmitResultDto
    {
        public int DocumentId { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Consecutive { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool SentToHacienda { get; set; }
        public string? Message { get; set; }
        public List<EmitLogDto> Logs { get; set; } = new();
    }

    public class EmitLogDto
    {
        public DateTime Timestamp { get; set; }
        public string Step { get; set; } = string.Empty;
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
    }

    public class ElectronicDocumentSummaryDto
    {
        public int Id { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string? Clave { get; set; }
        public string? Consecutive { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? HaciendaStatus { get; set; }
        public string? HaciendaDetail { get; set; }
        public DateTime IssueDate { get; set; }
        public decimal Total { get; set; }
        public string Currency { get; set; } = "CRC";

        /// <summary>
        /// Condición de venta del comprobante ("01"=Contado, "02"=Crédito, ...).
        /// Se usa en la UI para habilitar el botón REP solo en facturas a crédito,
        /// ya que el Recibo Electrónico de Pago aplica al régimen de IVA diferido.
        /// </summary>
        public string? SaleCondition { get; set; }

        /// <summary>
        /// Indica si el documento ya fue reversado en su totalidad (100%) por notas
        /// de crédito/débito aceptadas o pendientes. Cuando es true, no se debe permitir
        /// generar otra reversa (NC sobre factura, ND sobre NC). Si aún queda cantidad
        /// disponible (reversa parcial), permanece en false.
        /// </summary>
        public bool FullyReversed { get; set; }

        /// <summary>
        /// Indica si la factura a crédito ya fue pagada en su totalidad (100%) por
        /// Recibos Electrónicos de Pago (REP) aceptados o pendientes. Cuando es true,
        /// no se debe permitir generar otro REP. Si aún queda saldo por pagar (pago
        /// parcial), permanece en false.
        /// </summary>
        public bool FullyPaid { get; set; }
    }
}
