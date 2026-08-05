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
        public decimal DiscountAmount { get; set; }
        public string? DiscountNature { get; set; }
        /// <summary>TRUE = servicio; FALSE = mercancía/bien.</summary>
        public bool IsService { get; set; } = true;

        // ===== Exoneración por línea =====
        /// <summary>TRUE si la línea está exonerada del IVA (total o parcial).</summary>
        public bool IsExonerated { get; set; }
        public string? ExonDocumentType { get; set; }
        public string? ExonDocumentNumber { get; set; }
        public string? ExonInstitution { get; set; }
        public DateTime? ExonDate { get; set; }
        /// <summary>PorcentajeExoneracion 0..100. Default 100 cuando IsExonerated y no se indica.</summary>
        public decimal ExonPercent { get; set; }
    }

    public class EmitReferenceDto
    {
        public string RefDocumentType { get; set; } = "01";
        public string RefClave { get; set; } = string.Empty;
        public DateTime RefDate { get; set; } = DateTime.UtcNow;
        public string RefCode { get; set; } = "01";
        public string RefReason { get; set; } = string.Empty;
    }

    public class EmitDocumentDto
    {
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
        /// <summary>TRUE = documento exonerado completo (todas las líneas exoneradas).</summary>
        public bool IsExonerated { get; set; }
        public List<EmitLineDto> Lines { get; set; } = new();
        public List<EmitReferenceDto> References { get; set; } = new();
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
