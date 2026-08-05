// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/ElectronicDocumentXmlBuilder.cs
// PROPÓSITO: Generación del XML v4.4 de comprobantes electrónicos (Hacienda CR)
// DESCRIPCIÓN: Construye el XML con los namespaces exactos exigidos por la v4.4 y la
//              estructura de nodos oficial. El nombre del nodo raíz y el namespace
//              dependen del tipo de documento (FE, NC, ND, TE, FEC, REP).
//
//   IMPORTANTE: Cualquier desviación de los namespaces o de la estructura provoca
//   rechazo total del XSD por parte de la DGT.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Globalization;
using System.Xml.Linq;
using CMS.Entities.EInvoice;
using CMS.Entities.Operational;

namespace CMS.Data.Services.EInvoice
{
    /// <inheritdoc cref="IElectronicDocumentXmlBuilder"/>
    public class ElectronicDocumentXmlBuilder : IElectronicDocumentXmlBuilder
    {
        private const string XsdBase = "https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Cédula del PROVEEDOR DEL SISTEMA (software de facturación) ante Hacienda.
        /// v4.4 exige el nodo ProveedorSistemas. Valor real de BITI Solutions.
        /// </summary>
        private const string ProveedorSistemasId = "2100042005";

        /// <summary>Metadatos de cada tipo de documento: nodo raíz, segmento de namespace y URL del XSD.</summary>
        private static (string Root, string Ns, string Xsd) DocMeta(string documentType) => documentType switch
        {
            EInvoiceDocumentType.FacturaElectronica =>
                ("FacturaElectronica", "facturaElectronica", "FacturaElectronica_V4.4.xsd"),
            EInvoiceDocumentType.NotaDebito =>
                ("NotaDebitoElectronica", "notaDebitoElectronica", "NotaDebitoElectronica_V4.4.xsd"),
            EInvoiceDocumentType.NotaCredito =>
                ("NotaCreditoElectronica", "notaCreditoElectronica", "NotaCreditoElectronica_V4.4.xsd"),
            EInvoiceDocumentType.TiqueteElectronico =>
                ("TiqueteElectronico", "tiqueteElectronico", "TiqueteElectronico_V4.4.xsd"),
            EInvoiceDocumentType.FacturaCompra =>
                ("FacturaElectronicaCompra", "facturaElectronicaCompra", "FacturaElectronicaCompra_V4.4.xsd"),
            EInvoiceDocumentType.ReciboElectronicoPago =>
                ("ReciboElectronicoPago", "reciboElectronicoPago", "ReciboElectronicoPago_V4.4.xsd"),
            _ => ("FacturaElectronica", "facturaElectronica", "FacturaElectronica_V4.4.xsd")
        };

        /// <inheritdoc />
        public string BuildXml(
            ElectronicDocument document,
            CustomerBillingCredential issuerCredential,
            CustomerBillingCredential? receptorCredential,
            IReadOnlyList<ElectronicDocumentLine> lines,
            IReadOnlyDictionary<int, List<ElectronicDocumentTax>> taxesByLine,
            IReadOnlyList<ElectronicDocumentReference> references)
        {
            var (root, nsSegment, xsdFile) = DocMeta(document.DocumentType);
            XNamespace ns = $"{XsdBase}{nsSegment}";
            XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";
            XNamespace xsd = "http://www.w3.org/2001/XMLSchema";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

            var rootEl = new XElement(ns + root,
                new XAttribute("xmlns", ns.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ds", ds.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xsd", xsd.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),
                new XAttribute(xsi + "schemaLocation",
                    $"{ns.NamespaceName} https://www.hacienda.go.cr/ATV/ComprobanteElectronico/docs/esquemas/2016/v4.4/{xsdFile}"),

                new XElement(ns + "Clave", document.Clave),
                new XElement(ns + "ProveedorSistemas", ProveedorSistemasId),
                // CodigoActividadEmisor: presente en FE/FEC/NC/ND/TE.
                // EXCEPCIÓN: el Recibo Electrónico de Pago (REP) NO admite este nodo en su XSD;
                // su esquema va directo de ProveedorSistemas a NumeroConsecutivo (Hacienda rechaza
                // con cvc-complex-type.2.4.a si se incluye).
                document.DocumentType == EInvoiceDocumentType.ReciboElectronicoPago
                    ? null
                    : new XElement(ns + "CodigoActividadEmisor",
                        string.IsNullOrWhiteSpace(issuerCredential.EconomicActivity) ? "0000.1" : issuerCredential.EconomicActivity),
                // CodigoActividadReceptor: opcional en v4.4. Se emite cuando el receptor tiene
                // actividad económica registrada (obligatorio de facto en NC/ND aceptadas por Hacienda).
                // EXCEPCIÓN: el Tiquete Electrónico (TE) y el Recibo Electrónico de Pago (REP) NO
                // admiten este nodo en su XSD; van directo a NumeroConsecutivo (Hacienda rechaza
                // con cvc-complex-type.2.4.a si se incluye).
                document.DocumentType != EInvoiceDocumentType.TiqueteElectronico
                    && document.DocumentType != EInvoiceDocumentType.ReciboElectronicoPago
                    && receptorCredential is not null && !string.IsNullOrWhiteSpace(receptorCredential.EconomicActivity)
                    ? new XElement(ns + "CodigoActividadReceptor", receptorCredential.EconomicActivity)
                    : null,
                new XElement(ns + "NumeroConsecutivo", document.Consecutive),
                new XElement(ns + "FechaEmision", ToCrDateString(document.IssueDate)),
                BuildEmisor(ns, issuerCredential, document.DocumentType),
                receptorCredential is null ? null : BuildReceptor(ns, receptorCredential),
                new XElement(ns + "CondicionVenta",
                    // El REP restringe CondicionVenta a la enumeración [09, 11]:
                    //   09 = Pago de servicios prestados al Estado
                    //   11 = Pago de venta a crédito en IVA hasta 90 días
                    // El resto de comprobantes usan la condición del documento (01, 02, ...).
                    // Como el REP documenta el pago de una factura a crédito, se usa 11.
                    document.DocumentType == EInvoiceDocumentType.ReciboElectronicoPago
                        ? "11"
                        : document.SaleCondition),
                document.SaleCondition == "02" && document.CreditTerm.HasValue
                    ? new XElement(ns + "PlazoCredito", document.CreditTerm.Value.ToString(Inv))
                    : null,
                BuildDetalle(ns, document.DocumentType, lines, taxesByLine),
                BuildResumen(ns, document, lines, taxesByLine),
                BuildReferencias(ns, document, references),
                BuildOtros(ns, document.DocumentType, references)
                // Nota: el nodo 'Normativa' fue ELIMINADO en v4.4. Tras el resumen/
                // referencias (y el nodo Otros) va directamente la firma (ds:Signature),
                // añadida al firmar.
            );

            // Se devuelve solo el elemento raíz (sin declaración ni saltos), para que
            // el firmador lo cargue sin alteraciones. La declaración UTF-8 se añade al firmar.
            return rootEl.ToString(SaveOptions.DisableFormatting);
        }

        private static XElement BuildEmisor(XNamespace ns, CustomerBillingCredential issuerCredential, string documentType)
        {
            // El Recibo Electrónico de Pago (REP) usa un Emisor REDUCIDO: su XSD va de
            // Identificacion (y NombreComercial opcional) directo a CorreoElectronico.
            // NO admite Ubicacion ni Telefono (Hacienda rechaza con cvc-complex-type.2.4.a).
            var isRep = documentType == EInvoiceDocumentType.ReciboElectronicoPago;
            return new XElement(ns + "Emisor",
                new XElement(ns + "Nombre", issuerCredential.Name),
                new XElement(ns + "Identificacion",
                    new XElement(ns + "Tipo", issuerCredential.IdentificationType),
                    new XElement(ns + "Numero", issuerCredential.Identification)),
                // Registrofiscal8707: solo emisores en régimen especial (Ley 8707).
                // Orden XSD v4.4: tras Identificacion, antes de NombreComercial.
                issuerCredential.IsSpecialRegime && !string.IsNullOrWhiteSpace(issuerCredential.SpecialRegimeCode)
                    ? new XElement(ns + "Registrofiscal8707", issuerCredential.SpecialRegimeCode)
                    : null,
                string.IsNullOrWhiteSpace(issuerCredential.CommercialName) ? null
                    : new XElement(ns + "NombreComercial", issuerCredential.CommercialName),
                isRep ? null : BuildUbicacion(ns, issuerCredential.Province, issuerCredential.Canton, issuerCredential.District, issuerCredential.OtherSigns),
                isRep ? null : BuildTelefono(ns, issuerCredential.PhoneCode, issuerCredential.Phone),
                new XElement(ns + "CorreoElectronico", issuerCredential.Email));
        }

        private static XElement BuildReceptor(XNamespace ns, CustomerBillingCredential r)
        {
            var el = new XElement(ns + "Receptor",
                new XElement(ns + "Nombre", r.Name));
            if (!string.IsNullOrWhiteSpace(r.IdentificationType) && !string.IsNullOrWhiteSpace(r.Identification))
                el.Add(new XElement(ns + "Identificacion",
                    new XElement(ns + "Tipo", r.IdentificationType),
                    new XElement(ns + "Numero", r.Identification)));
            else if (!string.IsNullOrWhiteSpace(r.ForeignIdentification))
                el.Add(new XElement(ns + "IdentificacionExtranjero", r.ForeignIdentification));
            if (!string.IsNullOrWhiteSpace(r.Email))
                el.Add(new XElement(ns + "CorreoElectronico", r.Email));
            return el;
        }

        private static XElement? BuildUbicacion(XNamespace ns, string? prov, string? canton, string? distr, string? signs)
        {
            if (string.IsNullOrWhiteSpace(prov)) return null;
            return new XElement(ns + "Ubicacion",
                new XElement(ns + "Provincia", prov),
                new XElement(ns + "Canton", canton ?? "01"),
                new XElement(ns + "Distrito", distr ?? "01"),
                // OtrasSenas es obligatorio en el XSD v4.4 cuando hay Ubicacion.
                new XElement(ns + "OtrasSenas",
                    string.IsNullOrWhiteSpace(signs) ? "Sin otras señas" : signs));
        }

        private static XElement? BuildTelefono(XNamespace ns, string? code, string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            return new XElement(ns + "Telefono",
                new XElement(ns + "CodigoPais", code ?? "506"),
                new XElement(ns + "NumTelefono", phone));
        }

        private static XElement BuildDetalle(
            XNamespace ns,
            string documentType,
            IReadOnlyList<ElectronicDocumentLine> lines,
            IReadOnlyDictionary<int, List<ElectronicDocumentTax>> taxesByLine)
        {
            // El elemento <ImpuestoAsumidoEmisorFabrica> es válido en FE/TE/NC/ND,
            // pero el XSD de FacturaElectronicaCompra (FEC) v4.4 NO lo permite, y el de
            // ReciboElectronicoPago (REP) TAMPOCO: en ambos la línea pasa directamente de
            // <Impuesto> a <ImpuestoNeto>. Emitirlo provoca el rechazo cvc-complex-type.2.4.a.
            bool allowImpuestoAsumido = documentType != EInvoiceDocumentType.FacturaCompra
                && documentType != EInvoiceDocumentType.ReciboElectronicoPago;

            // El Recibo Electrónico de Pago (REP) usa una LineaDetalle con la estructura
            // reducida: su XSD va NumeroLinea → Detalle → MontoTotal → SubTotal → Impuesto →
            // ImpuestoNeto → MontoTotalLinea. Omite CodigoCABYS, Cantidad, UnidadMedida,
            // PrecioUnitario, Descuento, BaseImponible e ImpuestoAsumidoEmisorFabrica (esos
            // datos ya viven en la factura referenciada). Verificado paso a paso por los
            // rechazos del validador de Hacienda.
            bool isRep = documentType == EInvoiceDocumentType.ReciboElectronicoPago;

            var detalle = new XElement(ns + "DetalleServicio");
            foreach (var line in lines.OrderBy(l => l.LineNumber))
            {
                var lineEl = new XElement(ns + "LineaDetalle",
                    new XElement(ns + "NumeroLinea", line.LineNumber.ToString(Inv)));
                if (!isRep)
                {
                    lineEl.Add(new XElement(ns + "CodigoCABYS", line.CabysCode));
                    lineEl.Add(new XElement(ns + "Cantidad", Num(line.Quantity, 3)));
                    lineEl.Add(new XElement(ns + "UnidadMedida", line.UnitMeasure));
                }
                lineEl.Add(new XElement(ns + "Detalle", line.Detail));
                if (!isRep)
                    lineEl.Add(new XElement(ns + "PrecioUnitario", Num(line.UnitPrice)));
                lineEl.Add(new XElement(ns + "MontoTotal", Num(line.TotalAmount)));

                if (!isRep && line.DiscountAmount > 0)
                {
                    // Orden exacto exigido por el XSD v4.4 (verificado contra comprobante
                    // ACEPTADO): MontoDescuento → CodigoDescuento → NaturalezaDescuento.
                    // ⚠️ El REP NO admite <Descuento> en la LineaDetalle: su XSD va directo
                    // de <MontoTotal> a <SubTotal> (verificado por rechazo cvc-complex-type.2.4.a).
                    lineEl.Add(new XElement(ns + "Descuento",
                        new XElement(ns + "MontoDescuento", Num(line.DiscountAmount)),
                        new XElement(ns + "CodigoDescuento", line.DiscountNature ?? "06"),
                        new XElement(ns + "NaturalezaDescuento", NatureText(line.DiscountNature))));
                }

                lineEl.Add(new XElement(ns + "SubTotal", Num(line.SubTotal)));
                // El REP NO admite <BaseImponible> en la LineaDetalle: su XSD va directo de
                // <SubTotal> a <Impuesto>/<ImpuestoNeto> (verificado por rechazo cvc-complex-type.2.4.a).
                if (!isRep)
                    lineEl.Add(new XElement(ns + "BaseImponible", Num(line.TaxableBase)));

                if (taxesByLine.TryGetValue(line.Id, out var taxes))
                {
                    foreach (var tax in taxes)
                    {
                        var impuestoEl = new XElement(ns + "Impuesto",
                            new XElement(ns + "Codigo", tax.TaxCode),
                            new XElement(ns + "CodigoTarifaIVA", tax.TaxRateCode ?? "08"),
                            new XElement(ns + "Tarifa", Num(tax.TaxRate, 2)),
                            new XElement(ns + "Monto", Num(tax.TaxAmount)));

                        // Bloque <Exoneracion> cuando la línea está exonerada (v4.4).
                        // ⚠️ v4.4 renombró los elementos respecto de v4.3:
                        //   TipoDocumento        → TipoDocumentoEX1 (+ TipoDocumentoOTRO si = 99)
                        //   NombreInstitucion    → sigue igual PERO es CÓDIGO de catálogo (01..99),
                        //                          (+ NombreInstitucionOtros si = 99)
                        //   FechaEmision         → FechaEmisionEX
                        //   PorcentajeExoneracion→ TarifaExonerada (entero, %)
                        //   MontoExoneracion     → MontoExoneracion (igual)
                        // Orden XSD v4.4: TipoDocumentoEX1 → [TipoDocumentoOTRO] → NumeroDocumento →
                        //   NombreInstitucion → [NombreInstitucionOtros] → FechaEmisionEX →
                        //   TarifaExonerada → MontoExoneracion.
                        if (line.IsExonerated && line.ExonAmount > 0)
                        {
                            var tipoDocEx = string.IsNullOrWhiteSpace(line.ExonDocumentType) ? "99" : line.ExonDocumentType!;
                            var nombreInst = string.IsNullOrWhiteSpace(line.ExonInstitution) ? "99" : line.ExonInstitution!;

                            var exoneracionEl = new XElement(ns + "Exoneracion",
                                new XElement(ns + "TipoDocumentoEX1", tipoDocEx));

                            // TipoDocumentoOTRO es obligatorio cuando TipoDocumentoEX1 = 99.
                            if (tipoDocEx == "99")
                                exoneracionEl.Add(new XElement(ns + "TipoDocumentoOTRO", "Otro documento de exoneración"));

                            exoneracionEl.Add(new XElement(ns + "NumeroDocumento",
                                string.IsNullOrWhiteSpace(line.ExonDocumentNumber) ? "0" : line.ExonDocumentNumber!));

                            exoneracionEl.Add(new XElement(ns + "NombreInstitucion", nombreInst));

                            // NombreInstitucionOtros es obligatorio cuando NombreInstitucion = 99.
                            if (nombreInst == "99")
                                exoneracionEl.Add(new XElement(ns + "NombreInstitucionOtros", "Otra institución"));

                            exoneracionEl.Add(new XElement(ns + "FechaEmisionEX", ToCrDateString(line.ExonDate ?? DateTime.UtcNow)));
                            // TarifaExonerada = tarifa EFECTIVA exonerada = IVA% × %exon.
                            // Hacienda (-190) valida: MontoExoneracion = (TarifaExonerada/100) × SubTotal.
                            // Con MontoExoneracion = IVA exonerado, la tarifa efectiva es
                            // (Tarifa IVA) × (%exon/100). Ej.: 13% IVA exonerado al 100% → 13.
                            decimal tarifaEfectiva = tax.TaxRate * line.ExonPercent / 100m;
                            exoneracionEl.Add(new XElement(ns + "TarifaExonerada", Num(tarifaEfectiva, 2)));
                            exoneracionEl.Add(new XElement(ns + "MontoExoneracion", Num(line.ExonAmount)));

                            impuestoEl.Add(exoneracionEl);
                        }

                        lineEl.Add(impuestoEl);
                    }
                }

                if (allowImpuestoAsumido)
                    lineEl.Add(new XElement(ns + "ImpuestoAsumidoEmisorFabrica", Num(0)));
                lineEl.Add(new XElement(ns + "ImpuestoNeto", Num(line.ImpuestoNeto)));
                lineEl.Add(new XElement(ns + "MontoTotalLinea", Num(line.TotalLine)));
                detalle.Add(lineEl);
            }
            return detalle;
        }

        private static XElement BuildResumen(
            XNamespace ns,
            ElectronicDocument d,
            IReadOnlyList<ElectronicDocumentLine> lines,
            IReadOnlyDictionary<int, List<ElectronicDocumentTax>> taxesByLine)
        {
            // Clasificación por naturaleza (servicio/mercancía) y estado fiscal
            // (gravado / exento / exonerado). Una línea se considera exonerada
            // cuando tiene monto exonerado > 0.
            bool IsExon(ElectronicDocumentLine l) => l.IsExonerated && l.ExonAmount > 0;

            // Hacienda v4.4: los totales de CLASIFICACIÓN (TotalServGravados,
            // TotalServExonerado, TotalMercanciasGravadas, TotalExonerado, etc.) se
            // calculan sobre el BRUTO por línea (MontoTotal = cantidad × precio, ANTES
            // del descuento), NO sobre la base neta ni sobre el IVA. Su suma (gravado +
            // exento + exonerado + noSujeto) debe cuadrar con TotalVenta (= Σ MontoTotal).
            // El descuento se refleja solo en TotalDescuentos y TotalVentaNeta.
            // Confirmado con rechazos de Hacienda: con descuento presente, esperaba el
            // bruto (líneas exoneradas 6400+400 = 6800, no 6700) para -106/-108/-111/-51.
            decimal servGrav = lines.Where(l => l.IsService && l.ImpuestoNeto > 0 && !IsExon(l)).Sum(l => l.TotalAmount);
            decimal servExen = lines.Where(l => l.IsService && l.TotalTax == 0 && !IsExon(l)).Sum(l => l.TotalAmount);
            decimal servExon = lines.Where(l => l.IsService && IsExon(l)).Sum(l => l.TotalAmount);
            decimal mercGrav = lines.Where(l => !l.IsService && l.ImpuestoNeto > 0 && !IsExon(l)).Sum(l => l.TotalAmount);
            decimal mercExen = lines.Where(l => !l.IsService && l.TotalTax == 0 && !IsExon(l)).Sum(l => l.TotalAmount);
            decimal mercExon = lines.Where(l => !l.IsService && IsExon(l)).Sum(l => l.TotalAmount);

            decimal totalExonerado = servExon + mercExon;
            decimal totalImpuestoNeto = lines.Sum(l => l.ImpuestoNeto);
            decimal totalBruto = lines.Sum(l => l.TotalAmount);      // Σ MontoTotal (antes de descuento)
            decimal totalDescuentos = lines.Sum(l => l.DiscountAmount);

            // Estructura verificada contra factura real v4.4.
            // ⚠️ El ResumenFactura del REP es REDUCIDO: tras <CodigoTipoMoneda> va directo a
            // <TotalVenta>, omitiendo TODOS los totales de clasificación (TotalServGravados,
            // TotalServExentos, TotalServExonerado, TotalMercancias*, TotalGravado, TotalExento,
            // TotalExonerado, TotalNoSujeto). Verificado por rechazo cvc-complex-type.2.4.a.
            bool isRep = d.DocumentType == EInvoiceDocumentType.ReciboElectronicoPago;

            var resumen = new XElement(ns + "ResumenFactura",
                new XElement(ns + "CodigoTipoMoneda",
                    new XElement(ns + "CodigoMoneda", d.Currency),
                    new XElement(ns + "TipoCambio", Num(d.ExchangeRate, 5))));

            if (!isRep)
            {
                if (servGrav > 0) resumen.Add(new XElement(ns + "TotalServGravados", Num(servGrav)));
                if (servExen > 0) resumen.Add(new XElement(ns + "TotalServExentos", Num(servExen)));
                if (servExon > 0) resumen.Add(new XElement(ns + "TotalServExonerado", Num(servExon)));
                if (mercGrav > 0) resumen.Add(new XElement(ns + "TotalMercanciasGravadas", Num(mercGrav)));
                if (mercExen > 0) resumen.Add(new XElement(ns + "TotalMercanciasExentas", Num(mercExen)));
                if (mercExon > 0) resumen.Add(new XElement(ns + "TotalMercExonerada", Num(mercExon)));

                resumen.Add(new XElement(ns + "TotalGravado", Num(servGrav + mercGrav)));
                resumen.Add(new XElement(ns + "TotalExento", Num(servExen + mercExen)));
                resumen.Add(new XElement(ns + "TotalExonerado", Num(totalExonerado)));
                resumen.Add(new XElement(ns + "TotalNoSujeto", Num(0)));
            }

            resumen.Add(new XElement(ns + "TotalVenta", Num(totalBruto)));

            // Orden exacto del XSD v4.4 (verificado contra comprobante ACEPTADO):
            // TotalVenta → TotalDescuentos → TotalVentaNeta.
            // ⚠️ El REP NO admite <TotalDescuentos> en el resumen: va directo de <TotalVenta>
            // a <TotalVentaNeta> (verificado por rechazo cvc-complex-type.2.4.a).
            if (!isRep && totalDescuentos > 0)
                resumen.Add(new XElement(ns + "TotalDescuentos", Num(totalDescuentos)));

            // El REP no maneja descuentos: Hacienda (regla -53) exige TotalVentaNeta = TotalVenta.
            // Para el resto de tipos, TotalVentaNeta = bruto − descuentos.
            decimal ventaNeta = isRep ? totalBruto : totalBruto - totalDescuentos;
            resumen.Add(new XElement(ns + "TotalVentaNeta", Num(ventaNeta)));

            if (totalImpuestoNeto > 0)
            {
                // R4 - Multi-rate: un TotalDesgloseImpuesto por cada combinación de
                // (Codigo de impuesto, CodigoTarifaIVA) presente en las líneas, usando
                // el IVA NETO (descontada la exoneración). Antes se emitía uno fijo con
                // código '01'/'08', lo cual es incorrecto con varias tarifas de IVA.
                var desgloses = lines
                    .Where(l => l.ImpuestoNeto > 0)
                    .GroupBy(l => string.IsNullOrWhiteSpace(l.TaxRateCodeIva) ? "08" : l.TaxRateCodeIva!)
                    .OrderBy(g => g.Key)
                    .ToList();

                if (desgloses.Count > 0)
                {
                    foreach (var g in desgloses)
                    {
                        resumen.Add(new XElement(ns + "TotalDesgloseImpuesto",
                            new XElement(ns + "Codigo", "01"),
                            new XElement(ns + "CodigoTarifaIVA", g.Key),
                            new XElement(ns + "TotalMontoImpuesto", Num(g.Sum(l => l.ImpuestoNeto)))));
                    }
                }
                else
                {
                    // Fallback: sin detalle por línea pero con total de impuestos.
                    resumen.Add(new XElement(ns + "TotalDesgloseImpuesto",
                        new XElement(ns + "Codigo", "01"),
                        new XElement(ns + "CodigoTarifaIVA", "08"),
                        new XElement(ns + "TotalMontoImpuesto", Num(totalImpuestoNeto))));
                }
            }

            resumen.Add(new XElement(ns + "TotalImpuesto", Num(totalImpuestoNeto)));

            // El REP no maneja descuentos: forzamos TotalVentaNeta = TotalVenta (bruto).
            // Por coherencia, Hacienda (regla -55) exige TotalComprobante = TotalVentaNeta +
            // TotalImpuesto. Como d.Total se calculó sobre el neto CON descuento, para el REP
            // recomputamos el total a partir de los mismos valores emitidos en el resumen.
            decimal totalComprobante = isRep ? ventaNeta + totalImpuestoNeto : d.Total;
            resumen.Add(new XElement(ns + "MedioPago",
                new XElement(ns + "TipoMedioPago", d.PaymentMethod),
                new XElement(ns + "TotalMedioPago", Num(totalComprobante))));
            resumen.Add(new XElement(ns + "TotalComprobante", Num(totalComprobante)));
            return resumen;
        }

        private static IEnumerable<XElement>? BuildReferencias(
            XNamespace ns, ElectronicDocument document, IReadOnlyList<ElectronicDocumentReference> refs)
        {
            // La Factura Electrónica de Compra (FEC) EXIGE al menos un bloque
            // <InformacionReferencia> (a diferencia de la FE normal, donde es opcional).
            // Cuando el usuario no aporta una referencia explícita, se emite una
            // autorreferencia con código "99" (Otros) apuntando al propio comprobante,
            // que es la práctica aceptada por Hacienda para FEC sin documento previo.
            if (refs.Count == 0)
            {
                if (document.DocumentType != EInvoiceDocumentType.FacturaCompra)
                    return null;

                // Cuando TipoDocIR = "99" (Otro) el esquema v4.4 EXIGE tambi\u00e9n el nodo
                // <TipoDocRefOTRO>; y cuando Codigo = "99" (Otro) exige <CodigoReferenciaOTRO>.
                // El orden de los elementos debe respetar la secuencia del XSD:
                // TipoDocIR, TipoDocRefOTRO, Numero, FechaEmisionIR, Codigo, CodigoReferenciaOTRO, Razon.
                return new[]
                {
                    new XElement(ns + "InformacionReferencia",
                        new XElement(ns + "TipoDocIR", "99"),
                        new XElement(ns + "TipoDocRefOTRO", "Documento de respaldo de compra"),
                        new XElement(ns + "Numero", document.Clave ?? string.Empty),
                        new XElement(ns + "FechaEmisionIR", ToCrDateString(document.IssueDate)),
                        new XElement(ns + "Codigo", "99"),
                        new XElement(ns + "CodigoReferenciaOTRO", "Factura Electronica de Compra"),
                        new XElement(ns + "Razon", "Factura Electronica de Compra"))
                };
            }

            // InformacionReferencia va directo bajo la raíz y se repite por cada referencia.
            // Nombres de campo exactos del esquema v4.4: TipoDocIR, Numero, FechaEmisionIR, Codigo, Razon.
            return refs.Select(r => new XElement(ns + "InformacionReferencia",
                new XElement(ns + "TipoDocIR", r.RefDocumentType),
                new XElement(ns + "Numero", r.RefClave),
                new XElement(ns + "FechaEmisionIR", ToCrDateString(r.RefDate)),
                new XElement(ns + "Codigo", r.RefCode),
                new XElement(ns + "Razon", r.RefReason)));
        }

        /// <summary>
        /// Construye el nodo &lt;Otros&gt;&lt;OtroTexto&gt; que Hacienda espera en las notas
        /// de crédito/débito aceptadas: contiene la Clave del documento referenciado.
        /// Solo se emite para NC (03) y ND (02) cuando existe al menos una referencia.
        /// </summary>
        private static XElement? BuildOtros(XNamespace ns, string documentType, IReadOnlyList<ElectronicDocumentReference> refs)
        {
            if (documentType != EInvoiceDocumentType.NotaCredito &&
                documentType != EInvoiceDocumentType.NotaDebito)
                return null;
            if (refs.Count == 0) return null;

            // La primera referencia identifica el documento origen a reversar.
            var claveRef = refs[0].RefClave;
            if (string.IsNullOrWhiteSpace(claveRef)) return null;

            return new XElement(ns + "Otros",
                new XElement(ns + "OtroTexto", claveRef));
        }

        private static string Num(decimal value, int decimals = 5) =>            Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, Inv);

        /// <summary>Convierte una fecha (UTC o local) a la hora oficial de Costa Rica (UTC-6) con offset.</summary>
        private static string ToCrDateString(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt
                : dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime()
                : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            var cr = new DateTimeOffset(utc, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(-6));
            // Formato verificado en facturas reales: yyyy-MM-ddTHH:mm:ss.000 (hora CR, sin offset).
            return cr.ToString("yyyy-MM-ddTHH:mm:ss.fff", Inv);
        }

        private static string NatureText(string? code) => code switch
        {
            EInvoiceDiscountNature.Regalia => "Regalía",
            EInvoiceDiscountNature.Volumen => "Descuento por volumen",
            EInvoiceDiscountNature.Temporada => "Descuento por temporada",
            EInvoiceDiscountNature.Promocion => "Promoción",
            _ => "Promoción"
        };
    }
}
