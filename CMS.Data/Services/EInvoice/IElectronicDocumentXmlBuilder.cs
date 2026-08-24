// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/IElectronicDocumentXmlBuilder.cs
// PROPÓSITO: Interfaz del generador de XML v4.4 de comprobantes electrónicos
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.Operational;
using CMS.Entities.EInvoice;

namespace CMS.Data.Services.EInvoice
{
    /// <summary>
    /// Construye el XML v4.4 (sin firmar) de un comprobante electrónico según el
    /// esquema oficial de Hacienda. El namespace raíz cambia según el tipo de documento.
    /// </summary>
    public interface IElectronicDocumentXmlBuilder
    {
        /// <summary>Genera el XML v4.4 del documento (con líneas, impuestos y referencias).</summary>
        /// <param name="typeMeta">
        /// Metadato/banderas parametrizables del tipo de documento (admin.electronic_document_type).
        /// Si es null, el builder usa el comportamiento por constante como fallback.
        /// </param>
        string BuildXml(
            ElectronicDocument document,
            CustomerBillingCredential issuerCredential,
            CustomerBillingCredential? receptorCredential,
            IReadOnlyList<ElectronicDocumentLine> lines,
            IReadOnlyDictionary<int, List<ElectronicDocumentTax>> taxesByLine,
            IReadOnlyList<ElectronicDocumentReference> references,
            ElectronicDocumentTypeCatalog? typeMeta = null);
    }
}
