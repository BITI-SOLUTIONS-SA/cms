// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/EInvoicePdfService.cs
// PROPÓSITO: Generación del PDF (representación gráfica) del comprobante CR v4.4
// DESCRIPCIÓN: Usa QuestPDF para producir un PDF legible con los datos del emisor,
//              receptor, detalle, impuestos y totales, además de la Clave Numérica.
//              En contingencia agrega la leyenda "Comprobante Provisional".
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Globalization;
using CMS.Entities.EInvoice;
using CMS.Entities.Operational;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CMS.Data.Services.EInvoice
{
    /// <inheritdoc cref="IEInvoicePdfService"/>
    public class EInvoicePdfService : IEInvoicePdfService
    {
        private static readonly CultureInfo Cr = CultureInfo.GetCultureInfo("es-CR");

        static EInvoicePdfService()
        {
            // QuestPDF Community license (gratuita para empresas < $1M USD/año).
            QuestPDF.Settings.License = LicenseType.Community;
        }

        private static string DocTitle(string t) => t switch
        {
            EInvoiceDocumentType.FacturaElectronica => "FACTURA ELECTRÓNICA",
            EInvoiceDocumentType.NotaDebito => "NOTA DE DÉBITO ELECTRÓNICA",
            EInvoiceDocumentType.NotaCredito => "NOTA DE CRÉDITO ELECTRÓNICA",
            EInvoiceDocumentType.TiqueteElectronico => "TIQUETE ELECTRÓNICO",
            EInvoiceDocumentType.FacturaCompra => "FACTURA ELECTRÓNICA DE COMPRA",
            EInvoiceDocumentType.ReciboElectronicoPago => "RECIBO ELECTRÓNICO DE PAGO",
            _ => "COMPROBANTE ELECTRÓNICO"
        };

        /// <inheritdoc />
        public byte[] GeneratePdf(
            ElectronicDocument document,
            Customer issuer,
            Customer? receptor,
            IReadOnlyList<ElectronicDocumentLine> lines)
        {
            var issuerInfo = new PartyInfo(
                issuer.Name, issuer.CommercialName, issuer.Identification, issuer.Phone, issuer.Email);
            PartyInfo? receptorInfo = receptor == null
                ? null
                : new PartyInfo(receptor.Name, receptor.CommercialName, receptor.Identification, receptor.Phone, receptor.Email);
            return RenderPdf(document, issuerInfo, receptorInfo, lines);
        }

        /// <inheritdoc />
        public byte[] GeneratePdf(
            ElectronicDocument document,
            IReadOnlyList<ElectronicDocumentLine> lines)
        {
            // Construir la información de las partes a partir de los campos ya
            // persistidos en el documento (emisor/receptor del XML v4.4).
            var issuerInfo = new PartyInfo(
                document.EmisorNombre ?? "",
                document.EmisorNombreComercial,
                document.EmisorIdentificacionNumero,
                document.EmisorTelefonoNumero,
                document.EmisorCorreo);

            PartyInfo? receptorInfo = string.IsNullOrWhiteSpace(document.ReceptorNombre)
                ? null
                : new PartyInfo(
                    document.ReceptorNombre!,
                    document.ReceptorNombreComercial,
                    document.ReceptorIdentificacionNumero,
                    document.ReceptorTelefonoNumero,
                    document.ReceptorCorreo);

            return RenderPdf(document, issuerInfo, receptorInfo, lines);
        }

        /// <summary>Datos mínimos de una parte (emisor/receptor) para el PDF.</summary>
        private sealed record PartyInfo(
            string Name, string? CommercialName, string? Identification, string? Phone, string? Email);

        private static byte[] RenderPdf(
            ElectronicDocument document,
            PartyInfo issuer,
            PartyInfo? receptor,
            IReadOnlyList<ElectronicDocumentLine> lines)
        {
            var isContingency = document.Status == EInvoiceStatus.Contingencia
                || document.Situation != EInvoiceSituation.Normal;

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Black));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(issuer.Name).Bold().FontSize(13);
                                if (!string.IsNullOrWhiteSpace(issuer.CommercialName))
                                    c.Item().Text(issuer.CommercialName!).FontSize(9);
                                c.Item().Text($"Cédula: {issuer.Identification}");
                                if (!string.IsNullOrWhiteSpace(issuer.Phone))
                                    c.Item().Text($"Tel: {issuer.Phone}");
                                c.Item().Text($"Correo: {issuer.Email}");
                            });
                            row.ConstantItem(200).AlignRight().Column(c =>
                            {
                                c.Item().Text(DocTitle(document.DocumentType)).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                c.Item().Text($"Consecutivo: {document.Consecutive}");
                                c.Item().Text($"Fecha: {document.IssueDate.ToString("dd/MM/yyyy HH:mm", Cr)}");
                                c.Item().Text($"Moneda: {document.Currency}");
                            });
                        });

                        if (isContingency)
                            col.Item().PaddingTop(4).Background(Colors.Orange.Lighten3).Padding(4)
                                .Text("COMPROBANTE PROVISIONAL - CONTINGENCIA").Bold().FontColor(Colors.Orange.Darken3);

                        col.Item().PaddingTop(4).Text($"Clave: {document.Clave}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(3).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    page.Content().PaddingVertical(6).Column(col =>
                    {
                        // Receptor
                        col.Item().PaddingBottom(6).Column(c =>
                        {
                            c.Item().Text("RECEPTOR").Bold().FontColor(Colors.Blue.Darken1);
                            if (receptor != null)
                            {
                                c.Item().Text(receptor.Name);
                                if (!string.IsNullOrWhiteSpace(receptor.Identification))
                                    c.Item().Text($"Identificación: {receptor.Identification}");
                                if (!string.IsNullOrWhiteSpace(receptor.Email))
                                    c.Item().Text($"Correo: {receptor.Email}");
                            }
                            else c.Item().Text("Cliente de contado").Italic();
                        });

                        // Detalle
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(28);   // #
                                cols.RelativeColumn(3);    // detalle
                                cols.ConstantColumn(45);   // cant
                                cols.ConstantColumn(70);   // precio
                                cols.ConstantColumn(45);   // desc
                                cols.ConstantColumn(45);   // iva
                                cols.ConstantColumn(75);   // total
                            });

                            table.Header(h =>
                            {
                                void HC(string t) => h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(t).Bold().FontSize(8);
                                HC("#"); HC("Detalle"); HC("Cant."); HC("Precio"); HC("Desc."); HC("IVA"); HC("Total");
                            });

                            foreach (var l in lines.OrderBy(x => x.LineNumber))
                            {
                                void C(string t, bool right = false)
                                {
                                    var cell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3);
                                    (right ? cell.AlignRight() : cell.AlignLeft()).Text(t).FontSize(8);
                                }
                                C(l.LineNumber.ToString());
                                C($"{l.Detail}\n[{l.CabysCode}]");
                                C(l.Quantity.ToString("0.###", Cr), true);
                                C(l.UnitPrice.ToString("#,##0.00", Cr), true);
                                C(l.DiscountAmount.ToString("#,##0.00", Cr), true);
                                C(l.TotalTax.ToString("#,##0.00", Cr), true);
                                C(l.TotalLine.ToString("#,##0.00", Cr), true);
                            }
                        });

                        // Totales
                        col.Item().PaddingTop(8).AlignRight().Column(c =>
                        {
                            void TR(string label, decimal val, bool bold = false)
                            {
                                c.Item().Row(r =>
                                {
                                    r.ConstantItem(120).Text(label).FontSize(9);
                                    var t = r.ConstantItem(90).AlignRight().Text($"{val.ToString("#,##0.00", Cr)} {document.Currency}").FontSize(9);
                                    if (bold) t.Bold();
                                });
                            }
                            TR("Subtotal:", document.SubTotal);
                            TR("Descuentos:", document.TotalDiscount);
                            TR("Gravado:", document.TotalTaxable);
                            TR("Exento:", document.TotalExempt);
                            TR("Impuesto (IVA):", document.TotalTaxes);
                            c.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                            TR("TOTAL:", document.Total, bold: true);
                        });
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        col.Item().PaddingTop(3).Row(r =>
                        {
                            r.RelativeItem().Text($"Estado: {document.Status}" +
                                (document.HaciendaStatus != null ? $" / Hacienda: {document.HaciendaStatus}" : ""))
                                .FontSize(7).FontColor(Colors.Grey.Darken1);
                            r.ConstantItem(160).AlignRight().Text("Autorizado mediante resolución DGT-R-033-2019")
                                .FontSize(7).FontColor(Colors.Grey.Darken1);
                        });
                    });
                });
            });

            return doc.GeneratePdf();
        }
    }
}
