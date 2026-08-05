// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/IXadesSignatureService.cs
// PROPÓSITO: Interfaz del servicio de firma XAdES-EPES para comprobantes CR v4.4
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.Operational;

namespace CMS.Data.Services.EInvoice
{
    /// <summary>
    /// Firma XML de comprobantes electrónicos con el perfil XAdES-EPES Enveloped
    /// (RSA-SHA256) exigido por Hacienda, incluyendo la Política de Firma obligatoria.
    /// </summary>
    public interface IXadesSignatureService
    {
        /// <summary>
        /// Firma el XML sin firmar usando el certificado del emisor (descifrado en
        /// memoria volátil por el Vault). Devuelve el XML firmado.
        /// </summary>
        string SignXml(string unsignedXml, CustomerBillingCredential credential);
    }
}
