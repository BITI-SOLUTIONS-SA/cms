// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/EInvoiceCalculator.cs
// PROPÓSITO: Utilidades de cálculo fiscal (IVI inverso, totales, impuestos)
// DESCRIPCIÓN: Centraliza la aritmética fiscal para evitar enviar el total en la base
//              imponible. Implementa el desglose inverso de precios con impuesto
//              incluido (I.V.I.): Base = Total / (1 + tarifa).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

namespace CMS.Data.Services.EInvoice
{
    /// <summary>Resultado del desglose de una línea.</summary>
    public readonly record struct LineTaxBreakdown(
        decimal UnitPriceBase, decimal TaxableBase, decimal TaxAmount, decimal TotalLine);

    /// <summary>Utilidades de cálculo fiscal para comprobantes electrónicos.</summary>
    public static class EInvoiceCalculator
    {
        /// <summary>
        /// Desglosa una línea. Si <paramref name="priceIncludesTax"/> es true, el
        /// precio unitario viene con IVA incluido (I.V.I.) y se calcula la base hacia atrás.
        /// </summary>
        public static LineTaxBreakdown BreakdownLine(
            decimal unitPrice, decimal quantity, decimal taxRatePercent,
            decimal discountAmount = 0, bool priceIncludesTax = false)
        {
            var rate = taxRatePercent / 100m;

            decimal unitBase = priceIncludesTax && rate > 0
                ? decimal.Round(unitPrice / (1 + rate), 5, MidpointRounding.AwayFromZero)
                : unitPrice;

            var gross = unitBase * quantity;
            var taxableBase = gross - discountAmount;
            if (taxableBase < 0) taxableBase = 0;

            var taxAmount = decimal.Round(taxableBase * rate, 5, MidpointRounding.AwayFromZero);
            var totalLine = taxableBase + taxAmount;

            return new LineTaxBreakdown(unitBase, taxableBase, taxAmount, totalLine);
        }
    }
}
