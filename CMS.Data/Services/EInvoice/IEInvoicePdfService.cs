// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/IEInvoicePdfService.cs
// PROPÓSITO: Interfaz del generador de PDF (representación gráfica) del comprobante
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.Operational;

namespace CMS.Data.Services.EInvoice
{
    /// <summary>
    /// Genera la representación PDF de un comprobante electrónico (para el receptor).
    /// </summary>
    public interface IEInvoicePdfService
    {
        /// <summary>
        /// Genera el PDF del comprobante. Si el documento está en contingencia, incluye
        /// la leyenda "Comprobante Provisional - Contingencia".
        /// </summary>
        byte[] GeneratePdf(
            ElectronicDocument document,
            Customer issuer,
            Customer? receptor,
            IReadOnlyList<ElectronicDocumentLine> lines);

        /// <summary>
        /// Genera el PDF del comprobante usando exclusivamente los datos fiscales ya
        /// persistidos en el propio documento (emisor/receptor/resumen). No requiere
        /// entidades <see cref="Customer"/>; útil para generar el PDF bajo demanda a
        /// partir de un documento ya emitido.
        /// </summary>
        byte[] GeneratePdf(
            ElectronicDocument document,
            IReadOnlyList<ElectronicDocumentLine> lines);
    }
}
