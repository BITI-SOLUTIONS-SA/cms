// ================================================================================
// ARCHIVO: CMS.Shared/DTOs/PartyEmitDetailDto.cs
// PROPÓSITO: DTO de detalle para las tarjetas "Datos del emisor" / "Datos del receptor"
//            de la pantalla /ElectronicInvoice/Emit.
// DESCRIPCIÓN: Expone nombre/razón social, identificación (cédula), dirección fiscal
//              ya compuesta a texto legible, correo y teléfono para mostrar en las
//              tarjetas resumen del comprobante.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-04
// ================================================================================

namespace CMS.Shared.DTOs;

/// <summary>
/// Detalle de una parte (emisor o receptor) para las tarjetas resumen de la
/// pantalla de emisión de comprobantes electrónicos.
/// </summary>
public class PartyEmitDetailDto
{
    /// <summary>Id de la entidad de origen (credencial de facturación o proveedor).</summary>
    public int Id { get; set; }

    /// <summary>Nombre o razón social.</summary>
    public string? Name { get; set; }

    /// <summary>Nombre comercial (opcional).</summary>
    public string? CommercialName { get; set; }

    /// <summary>Tipo de identificación Hacienda (01=Física, 02=Jurídica, ...).</summary>
    public string? IdentificationType { get; set; }

    /// <summary>Número de identificación / cédula.</summary>
    public string? Identification { get; set; }

    /// <summary>
    /// Dirección fiscal compuesta y legible: "Provincia - Cantón - Distrito - OtrasSeñas".
    /// Los códigos Hacienda ya fueron resueltos a nombres desde el catálogo geográfico.
    /// </summary>
    public string? AddressText { get; set; }

    /// <summary>Correo electrónico.</summary>
    public string? Email { get; set; }

    /// <summary>Código de país del teléfono (ej: 506).</summary>
    public string? PhoneCode { get; set; }

    /// <summary>Número de teléfono.</summary>
    public string? Phone { get; set; }

    // ===== EXONERACIÓN (solo aplica para el receptor) =====

    /// <summary>Indica si el receptor tiene exoneración de IVA vigente.</summary>
    public bool IsExonerated { get; set; }

    /// <summary>XML &lt;TipoDocumentoEX1&gt;: código del tipo de documento de exoneración.</summary>
    public string? ExonDocumentType { get; set; }

    /// <summary>XML &lt;TipoDocumentoOTRO&gt;: descripción cuando ExonDocumentType = 99.</summary>
    public string? ExonDocumentTypeOther { get; set; }

    /// <summary>XML &lt;NumeroDocumento&gt;: número/autorización de la exoneración.</summary>
    public string? ExonDocumentNumber { get; set; }

    /// <summary>XML &lt;Articulo&gt;: número de artículo que establece la exoneración.</summary>
    public string? ExonArticle { get; set; }

    /// <summary>XML &lt;Inciso&gt;: número de inciso que establece la exoneración.</summary>
    public string? ExonSubsection { get; set; }

    /// <summary>XML &lt;NombreInstitucion&gt;: código de la institución que emitió la exoneración.</summary>
    public string? ExonInstitutionCode { get; set; }

    /// <summary>XML &lt;NombreInstitucionOtros&gt;: descripción cuando ExonInstitutionCode = 99.</summary>
    public string? ExonInstitutionOther { get; set; }

    /// <summary>XML &lt;FechaEmisionEX&gt;: fecha de emisión del documento de exoneración (ISO 8601).</summary>
    public string? ExonIssueDate { get; set; }

    /// <summary>XML &lt;TarifaExonerada&gt;: porcentaje de tarifa exonerada (ej: 13.00).</summary>
    public decimal? ExonTariffPercent { get; set; }
}
