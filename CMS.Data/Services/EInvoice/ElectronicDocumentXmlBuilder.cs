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

        /// <summary>
        /// Banderas de comportamiento resueltas para un tipo de documento. Se derivan del
        /// catálogo parametrizable (admin.electronic_document_type) cuando está disponible,
        /// y caen al comportamiento por constante (hardcode histórico) como fallback para
        /// no romper la compatibilidad con los comprobantes ya aceptados por Hacienda.
        /// </summary>
        private readonly struct DocFlags
        {
            public string Root { get; init; }
            public string Ns { get; init; }
            public string Xsd { get; init; }
            public bool AllowCodigoActividadEmisor { get; init; }
            public bool AllowCodigoActividadReceptor { get; init; }
            public bool EmisorReduced { get; init; }
            public bool LineReduced { get; init; }
            public bool AllowLineDiscount { get; init; }
            public bool AllowImpuestoAsumido { get; init; }
            public bool AllowResumenClassification { get; init; }
            public bool AllowTotalDescuentos { get; init; }
            public bool ForceVentaNetaEqualsVenta { get; init; }
            public string? ForcedSaleCondition { get; init; }
            public bool RequiresReference { get; init; }
            public bool EmitsOtrosClave { get; init; }

            public static DocFlags Resolve(string documentType, ElectronicDocumentTypeCatalog? meta)
            {
                var (root, ns, xsd) = DocMeta(documentType);
                // Fallback por constante (comportamiento histórico verificado con Hacienda).
                bool isRep = documentType == EInvoiceDocumentType.ReciboElectronicoPago;
                bool isTe = documentType == EInvoiceDocumentType.TiqueteElectronico;
                bool isFec = documentType == EInvoiceDocumentType.FacturaCompra;
                bool isNcNd = documentType == EInvoiceDocumentType.NotaCredito
                              || documentType == EInvoiceDocumentType.NotaDebito;

                if (meta is null)
                {
                    return new DocFlags
                    {
                        Root = root,
                        Ns = ns,
                        Xsd = xsd,
                        AllowCodigoActividadEmisor = !isRep,
                        AllowCodigoActividadReceptor = !isTe && !isRep,
                        EmisorReduced = isRep,
                        LineReduced = isRep,
                        AllowLineDiscount = !isRep,
                        AllowImpuestoAsumido = !isFec && !isRep,
                        AllowResumenClassification = !isRep,
                        AllowTotalDescuentos = !isRep,
                        ForceVentaNetaEqualsVenta = isRep,
                        ForcedSaleCondition = isRep ? "11" : null,
                        RequiresReference = isFec,
                        EmitsOtrosClave = isNcNd
                    };
                }

                // Catálogo parametrizable: usa los metadatos XML del catálogo si vienen
                // informados; de lo contrario conserva los del switch DocMeta.
                return new DocFlags
                {
                    Root = string.IsNullOrWhiteSpace(meta.XmlRoot) ? root : meta.XmlRoot!,
                    Ns = string.IsNullOrWhiteSpace(meta.XmlNamespaceSegment) ? ns : meta.XmlNamespaceSegment!,
                    Xsd = string.IsNullOrWhiteSpace(meta.XsdFile) ? xsd : meta.XsdFile!,
                    AllowCodigoActividadEmisor = meta.AllowCodigoActividadEmisor,
                    AllowCodigoActividadReceptor = meta.AllowCodigoActividadReceptor,
                    EmisorReduced = meta.EmisorReduced,
                    LineReduced = meta.LineReduced,
                    AllowLineDiscount = meta.AllowLineDiscount,
                    AllowImpuestoAsumido = meta.AllowImpuestoAsumido,
                    AllowResumenClassification = meta.AllowResumenClassification,
                    AllowTotalDescuentos = meta.AllowTotalDescuentos,
                    ForceVentaNetaEqualsVenta = meta.ForceVentaNetaEqualsVenta,
                    ForcedSaleCondition = string.IsNullOrWhiteSpace(meta.ForcedSaleCondition) ? null : meta.ForcedSaleCondition,
                    RequiresReference = meta.RequiresReference,
                    EmitsOtrosClave = meta.EmitsOtrosClave
                };
            }
        }

        /// <inheritdoc />
        public string BuildXml(
            ElectronicDocument document,
            CustomerBillingCredential issuerCredential,
            CustomerBillingCredential? receptorCredential,
            IReadOnlyList<ElectronicDocumentLine> lines,
            IReadOnlyDictionary<int, List<ElectronicDocumentTax>> taxesByLine,
            IReadOnlyList<ElectronicDocumentReference> references,
            ElectronicDocumentTypeCatalog? typeMeta = null)
        {
            // Banderas de comportamiento parametrizables (catálogo) con fallback por constante.
            var flags = DocFlags.Resolve(document.DocumentType, typeMeta);
            var root = flags.Root;
            var nsSegment = flags.Ns;
            var xsdFile = flags.Xsd;
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
                // con cvc-complex-type.2.4.a si se incluye). Parametrizado vía allow_codigo_actividad_emisor.
                !flags.AllowCodigoActividadEmisor
                    ? null
                    : new XElement(ns + "CodigoActividadEmisor",
                        string.IsNullOrWhiteSpace(issuerCredential.EconomicActivity) ? "0000.1" : issuerCredential.EconomicActivity),
                // CodigoActividadReceptor: opcional en v4.4. Se emite cuando el receptor tiene
                // actividad económica registrada (obligatorio de facto en NC/ND aceptadas por Hacienda).
                // EXCEPCIÓN: el Tiquete Electrónico (TE) y el Recibo Electrónico de Pago (REP) NO
                // admiten este nodo en su XSD. Parametrizado vía allow_codigo_actividad_receptor.
                flags.AllowCodigoActividadReceptor
                    && receptorCredential is not null && !string.IsNullOrWhiteSpace(receptorCredential.EconomicActivity)
                    ? new XElement(ns + "CodigoActividadReceptor", receptorCredential.EconomicActivity)
                    : null,
                new XElement(ns + "NumeroConsecutivo", document.Consecutive),
                new XElement(ns + "FechaEmision", ToCrDateString(document.IssueDate)),
                BuildEmisor(ns, issuerCredential, flags),
                receptorCredential is null ? null : BuildReceptor(ns, receptorCredential),
                new XElement(ns + "CondicionVenta",
                    // El REP restringe CondicionVenta a la enumeración [09, 11]:
                    //   09 = Pago de servicios prestados al Estado
                    //   11 = Pago de venta a crédito en IVA hasta 90 días
                    // El resto de comprobantes usan la condición del documento (01, 02, ...).
                    // Parametrizado vía forced_sale_condition (REP=11).
                    !string.IsNullOrWhiteSpace(flags.ForcedSaleCondition)
                        ? flags.ForcedSaleCondition
                        : document.SaleCondition),
                document.SaleCondition == "02" && document.CreditTerm.HasValue
                    ? new XElement(ns + "PlazoCredito", document.CreditTerm.Value.ToString(Inv))
                    : null,
                BuildDetalle(ns, flags, lines, taxesByLine),
                BuildOtrosCargos(ns, document, out var totalOtrosCargos),
                BuildResumen(ns, document, flags, lines, taxesByLine, totalOtrosCargos),
                BuildReferencias(ns, document, flags, references),
                BuildOtros(ns, flags, references)
                // Nota: el nodo 'Normativa' fue ELIMINADO en v4.4. Tras el resumen/
                // referencias (y el nodo Otros) va directamente la firma (ds:Signature),
                // añadida al firmar.
            );

            // Se devuelve solo el elemento raíz (sin declaración ni saltos), para que
            // el firmador lo cargue sin alteraciones. La declaración UTF-8 se añade al firmar.
            return rootEl.ToString(SaveOptions.DisableFormatting);
        }

        private static XElement BuildEmisor(XNamespace ns, CustomerBillingCredential issuerCredential, DocFlags flags)
        {
            // El Recibo Electrónico de Pago (REP) usa un Emisor REDUCIDO: su XSD va de
            // Identificacion (y NombreComercial opcional) directo a CorreoElectronico.
            // NO admite Ubicacion ni Telefono. Parametrizado vía emisor_reduced.
            var isRep = flags.EmisorReduced;
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
            DocFlags flags,
            IReadOnlyList<ElectronicDocumentLine> lines,
            IReadOnlyDictionary<int, List<ElectronicDocumentTax>> taxesByLine)
        {
            // El elemento <ImpuestoAsumidoEmisorFabrica> es válido en FE/TE/NC/ND,
            // pero el XSD de FacturaElectronicaCompra (FEC) v4.4 NO lo permite, y el de
            // ReciboElectronicoPago (REP) TAMPOCO. Parametrizado vía allow_impuesto_asumido.
            bool allowImpuestoAsumido = flags.AllowImpuestoAsumido;

            // El Recibo Electrónico de Pago (REP) usa una LineaDetalle con la estructura
            // reducida. Parametrizado vía line_reduced.
            bool isRep = flags.LineReduced;

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
                    // v4.4 admite hasta 5 nodos <Descuento> por línea: se emite uno por cada
                    // descuento de la lista persistida (line.Discounts). Si la lista está
                    // vacía (documentos antiguos) se cae al escalar único.
                    var discountList = ParseLineDiscounts(line);
                    if (discountList.Count == 0)
                    {
                        discountList.Add((line.DiscountAmount, line.DiscountNature ?? "06"));
                    }
                    foreach (var (amount, nature) in discountList)
                    {
                        lineEl.Add(new XElement(ns + "Descuento",
                            new XElement(ns + "MontoDescuento", Num(amount)),
                            new XElement(ns + "CodigoDescuento", nature ?? "06"),
                            new XElement(ns + "NaturalezaDescuento", NatureText(nature))));
                    }
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
                        var code = string.IsNullOrWhiteSpace(tax.TaxCode) ? "01" : tax.TaxCode.Trim();
                        var impuestoEl = new XElement(ns + "Impuesto",
                            new XElement(ns + "Codigo", code));

                        // Estructura del nodo <Impuesto> según el código (v4.4):
                        //   01 (IVA):                CodigoTarifaIVA + Tarifa
                        //   07 (IVA cálculo especial): CodigoTarifaIVA + Tarifa
                        //   08 (IVA bienes usados):  FactorIVA (sin CodigoTarifaIVA/Tarifa)
                        //   02,03,04,05,06,12:       Tarifa (impuestos específicos, sin CodigoTarifaIVA)
                        //   99 (Otros):              Tarifa (con nombre libre del impuesto)
                        if (code == "01" || code == "07")
                        {
                            impuestoEl.Add(new XElement(ns + "CodigoTarifaIVA", tax.TaxRateCode ?? "08"));
                            impuestoEl.Add(new XElement(ns + "Tarifa", Num(tax.TaxRate, 2)));
                        }
                        else if (code == "08")
                        {
                            // Régimen de bienes usados: se emite el factor de IVA.
                            impuestoEl.Add(new XElement(ns + "FactorIVA", Num(tax.TaxRate, 5)));
                        }
                        else
                        {
                            // Impuestos específicos (02..06, 12) y Otros (99): llevan Tarifa.
                            impuestoEl.Add(new XElement(ns + "Tarifa", Num(tax.TaxRate, 2)));
                        }

                        impuestoEl.Add(new XElement(ns + "Monto", Num(tax.TaxAmount)));

                        // Bloque <Exoneracion> a nivel de IMPUESTO (v4.4). Cada
                        // impuesto de la línea puede tener su propia exoneración.
                        // Retrocompatibilidad: si el impuesto no trae datos de
                        // exoneración pero la línea sí (documentos antiguos), se usan
                        // los valores de la línea.
                        // ⚠️ v4.4 renombró los elementos respecto de v4.3:
                        //   TipoDocumento        → TipoDocumentoEX1 (+ TipoDocumentoOTRO si = 99)
                        //   NombreInstitucion    → CÓDIGO de catálogo (01..99) (+ NombreInstitucionOtros si = 99)
                        //   FechaEmision         → FechaEmisionEX
                        //   PorcentajeExoneracion→ TarifaExonerada (%)
                        bool taxExon = tax.IsExonerated && tax.ExonAmount > 0;
                        bool useLineExon = !taxExon && line.IsExonerated && line.ExonAmount > 0;
                        if (taxExon || useLineExon)
                        {
                            var exDocType   = taxExon ? tax.ExonDocumentType   : line.ExonDocumentType;
                            var exDocNumber = taxExon ? tax.ExonDocumentNumber : line.ExonDocumentNumber;
                            var exInst      = taxExon ? tax.ExonInstitution    : line.ExonInstitution;
                            var exDate      = taxExon ? tax.ExonDate           : line.ExonDate;
                            var exArticle   = taxExon ? tax.ExonArticle        : line.ExonArticle;
                            var exSubsect   = taxExon ? tax.ExonSubsection     : line.ExonSubsection;
                            var exPercent   = taxExon ? tax.ExonPercent        : line.ExonPercent;
                            var exAmount    = taxExon ? tax.ExonAmount         : line.ExonAmount;

                            var tipoDocEx = string.IsNullOrWhiteSpace(exDocType) ? "99" : exDocType!;
                            var nombreInst = string.IsNullOrWhiteSpace(exInst) ? "99" : exInst!;
                            var exoneracionEl = new XElement(ns + "Exoneracion",
                                new XElement(ns + "TipoDocumentoEX1", tipoDocEx));

                            // TipoDocumentoOTRO es obligatorio cuando TipoDocumentoEX1 = 99.
                            if (tipoDocEx == "99")
                                exoneracionEl.Add(new XElement(ns + "TipoDocumentoOTRO", "Otro documento de exoneración"));

                            // NumeroDocumento (v4.4): el XSD exige minLength = 3.
                            // Un valor placeholder de "0" (length 1) provoca el rechazo
                            // "cvc-minLength-valid ... minLength '3'". Se normaliza para
                            // garantizar al menos 3 caracteres cuando no hay número real.
                            var numeroDocumento = string.IsNullOrWhiteSpace(exDocNumber) ? "000" : exDocNumber!.Trim();
                            if (numeroDocumento.Length < 3)
                                numeroDocumento = numeroDocumento.PadLeft(3, '0');
                            exoneracionEl.Add(new XElement(ns + "NumeroDocumento", numeroDocumento));

                            // Artículo/Inciso que establece la exoneración (v4.4).
                            //   Articulo → opcional (entero, máx. 6 dígitos)
                            //   Inciso   → obligatorio si se emite el bloque de artículo/inciso
                            // Orden XSD v4.4: … NumeroDocumento → Articulo → Inciso → NombreInstitucion …
                            if (exArticle.HasValue && exArticle.Value > 0)
                                exoneracionEl.Add(new XElement(ns + "Articulo", exArticle.Value));
                            if (exSubsect.HasValue && exSubsect.Value > 0)
                                exoneracionEl.Add(new XElement(ns + "Inciso", exSubsect.Value));

                            exoneracionEl.Add(new XElement(ns + "NombreInstitucion", nombreInst));

                            // NombreInstitucionOtros es obligatorio cuando NombreInstitucion = 99.
                            if (nombreInst == "99")
                                exoneracionEl.Add(new XElement(ns + "NombreInstitucionOtros", "Otra institución"));

                            exoneracionEl.Add(new XElement(ns + "FechaEmisionEX", ToCrDateString(exDate ?? DateTime.UtcNow)));
                            // TarifaExonerada = tarifa EFECTIVA exonerada = Tarifa% × %exon.
                            // Hacienda (-190) valida: MontoExoneracion = (TarifaExonerada/100) × SubTotal.
                            decimal tarifaEfectiva = tax.TaxRate * exPercent / 100m;
                            exoneracionEl.Add(new XElement(ns + "TarifaExonerada", Num(tarifaEfectiva, 2)));
                            exoneracionEl.Add(new XElement(ns + "MontoExoneracion", Num(exAmount)));

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
            DocFlags flags,
            IReadOnlyList<ElectronicDocumentLine> lines,
            IReadOnlyDictionary<int, List<ElectronicDocumentTax>> taxesByLine,
            decimal totalOtrosCargos = 0m)
        {
            // Clasificación por naturaleza (servicio/mercancía) y estado fiscal
            // (gravado / exento / exonerado). Una línea se considera exonerada
            // cuando tiene monto exonerado > 0.
            bool IsExon(ElectronicDocumentLine l) => l.IsExonerated && l.ExonAmount > 0;

            // Códigos de tarifa que Hacienda considera GRAVADOS a 0% (no exentos):
            //   01 = 0% reducida (canasta básica), 05 = transitorio 0%, 11 = 0% sin crédito.
            // Estas líneas llevan IVA con Monto 0 y DEBEN clasificarse como gravadas y
            // aparecer en TotalDesgloseImpuesto. Los códigos realmente exentos son 10
            // (Tarifa Exenta) o la ausencia de tarifa IVA.
            static bool IsExemptRateCode(string? code)
            {
                var c = (code ?? string.Empty).Trim();
                return string.IsNullOrEmpty(c) || c == "10";
            }

            // Una línea es GRAVADA cuando tiene un código de tarifa IVA aplicable que no
            // es exento — incluye las tarifas de 0% (01/05/11), cuyo IVA neto es 0. Una
            // línea es EXENTA solo cuando su tarifa es 10 o carece de tarifa IVA.
            bool IsGravada(ElectronicDocumentLine l) => !IsExon(l) && !IsExemptRateCode(l.TaxRateCodeIva);
            bool IsExenta(ElectronicDocumentLine l) => !IsExon(l) && IsExemptRateCode(l.TaxRateCodeIva);

            // Hacienda v4.4: los totales de CLASIFICACIÓN (TotalServGravados,
            // TotalServExonerado, TotalMercanciasGravadas, TotalExonerado, etc.) se
            // calculan sobre el BRUTO por línea (MontoTotal = cantidad × precio, ANTES
            // del descuento), NO sobre la base neta ni sobre el IVA. Su suma (gravado +
            // exento + exonerado + noSujeto) debe cuadrar con TotalVenta (= Σ MontoTotal).
            // El descuento se refleja solo en TotalDescuentos y TotalVentaNeta.
            // Confirmado con rechazos de Hacienda: con descuento presente, esperaba el
            // bruto (líneas exoneradas 6400+400 = 6800, no 6700) para -106/-108/-111/-51.
            decimal servGrav = lines.Where(l => l.IsService && IsGravada(l)).Sum(l => l.TotalAmount);
            decimal servExen = lines.Where(l => l.IsService && IsExenta(l)).Sum(l => l.TotalAmount);
            decimal servExon = lines.Where(l => l.IsService && IsExon(l)).Sum(l => l.TotalAmount);
            decimal mercGrav = lines.Where(l => !l.IsService && IsGravada(l)).Sum(l => l.TotalAmount);
            decimal mercExen = lines.Where(l => !l.IsService && IsExenta(l)).Sum(l => l.TotalAmount);
            decimal mercExon = lines.Where(l => !l.IsService && IsExon(l)).Sum(l => l.TotalAmount);

            decimal totalExonerado = servExon + mercExon;
            decimal totalImpuestoNeto = lines.Sum(l => l.ImpuestoNeto);
            decimal totalBruto = lines.Sum(l => l.TotalAmount);      // Σ MontoTotal (antes de descuento)
            decimal totalDescuentos = lines.Sum(l => l.DiscountAmount);

            // Estructura verificada contra factura real v4.4.
            // ⚠️ El ResumenFactura del REP es REDUCIDO. Parametrizado por banderas del catálogo:
            //   allow_resumen_classification, allow_total_descuentos, force_venta_neta_equals_venta.
            bool allowClassification = flags.AllowResumenClassification;
            bool allowTotalDescuentos = flags.AllowTotalDescuentos;
            bool forceVentaNeta = flags.ForceVentaNetaEqualsVenta;

            var resumen = new XElement(ns + "ResumenFactura",
                new XElement(ns + "CodigoTipoMoneda",
                    new XElement(ns + "CodigoMoneda", d.Currency),
                    new XElement(ns + "TipoCambio", Num(d.ExchangeRate, 5))));

            if (allowClassification)
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
            if (allowTotalDescuentos && totalDescuentos > 0)
                resumen.Add(new XElement(ns + "TotalDescuentos", Num(totalDescuentos)));

            // El REP no maneja descuentos: Hacienda (regla -53) exige TotalVentaNeta = TotalVenta.
            // Para el resto de tipos, TotalVentaNeta = bruto − descuentos.
            decimal ventaNeta = forceVentaNeta ? totalBruto : totalBruto - totalDescuentos;
            resumen.Add(new XElement(ns + "TotalVentaNeta", Num(ventaNeta)));

            if (totalImpuestoNeto > 0 || lines.Any(IsGravada))
            {
                // R4 - Multi-rate: un TotalDesgloseImpuesto por cada combinación de
                // (Codigo de impuesto, CodigoTarifaIVA) presente en las líneas GRAVADAS,
                // usando el IVA NETO (descontada la exoneración). Incluye las tarifas de
                // 0% (01/05/11): Hacienda las considera gravadas y exige su desglose con
                // TotalMontoImpuesto 0 (regla -488). Antes se filtraba por ImpuestoNeto > 0,
                // omitiendo las líneas de 0% y provocando el rechazo.
                var desgloses = lines
                    .Where(IsGravada)
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

            // TotalOtrosCargos (v4.4): suma de los <OtroCargo> a nivel de documento.
            // Va después de TotalImpuesto y antes de TotalComprobante (orden XSD).
            if (totalOtrosCargos > 0)
                resumen.Add(new XElement(ns + "TotalOtrosCargos", Num(totalOtrosCargos)));

            // El REP no maneja descuentos: forzamos TotalVentaNeta = TotalVenta (bruto).
            // Por coherencia, Hacienda (regla -55) exige TotalComprobante = TotalVentaNeta +
            // TotalImpuesto. Como d.Total se calculó sobre el neto CON descuento, para el REP
            // recomputamos el total a partir de los mismos valores emitidos en el resumen.
            // Los otros cargos SIEMPRE se suman al total del comprobante.
            decimal totalComprobante = (forceVentaNeta ? ventaNeta + totalImpuestoNeto : d.Total) + totalOtrosCargos;

            // Medios de pago: Hacienda v4.4 admite varios <MedioPago>. La suma de
            // <TotalMedioPago> debe igualar el TotalComprobante. Repartimos el total
            // en partes iguales entre los medios seleccionados y ajustamos el residuo
            // de redondeo en el primero para que la suma cuadre exactamente.
            var paymentCodes = (string.IsNullOrWhiteSpace(d.PaymentMethods) ? d.PaymentMethod : d.PaymentMethods)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
            if (paymentCodes.Count == 0)
                paymentCodes.Add(d.PaymentMethod);

            decimal each = Math.Round(totalComprobante / paymentCodes.Count, 5, MidpointRounding.AwayFromZero);
            for (int i = 0; i < paymentCodes.Count; i++)
            {
                // El primer medio absorbe el residuo de redondeo.
                decimal montoMedio = (i == 0)
                    ? totalComprobante - (each * (paymentCodes.Count - 1))
                    : each;
                resumen.Add(new XElement(ns + "MedioPago",
                    new XElement(ns + "TipoMedioPago", paymentCodes[i]),
                    new XElement(ns + "TotalMedioPago", Num(montoMedio))));
            }
            resumen.Add(new XElement(ns + "TotalComprobante", Num(totalComprobante)));
            return resumen;
        }

        private static IEnumerable<XElement>? BuildReferencias(
            XNamespace ns, ElectronicDocument document, DocFlags flags, IReadOnlyList<ElectronicDocumentReference> refs)
        {
            // La Factura Electrónica de Compra (FEC) y la de Exportación (FEE) EXIGEN al
            // menos un bloque <InformacionReferencia>. Parametrizado vía requires_reference.
            // Cuando el usuario no aporta una referencia explícita, se emite una
            // autorreferencia con código "99" (Otros) apuntando al propio comprobante.
            if (refs.Count == 0)
            {
                if (!flags.RequiresReference)
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
        private static XElement? BuildOtros(XNamespace ns, DocFlags flags, IReadOnlyList<ElectronicDocumentReference> refs)
        {
            if (!flags.EmitsOtrosClave)
                return null;
            if (refs.Count == 0) return null;

            // La primera referencia identifica el documento origen a reversar.
            var claveRef = refs[0].RefClave;
            if (string.IsNullOrWhiteSpace(claveRef)) return null;

            return new XElement(ns + "Otros",
                new XElement(ns + "OtroTexto", claveRef));
        }

        /// <summary>
        /// Construye los nodos &lt;OtrosCargos&gt; (v4.4) a partir del JSON persistido en
        /// <see cref="ElectronicDocument.OtherCharges"/> y devuelve la suma de los montos
        /// en <paramref name="totalOtrosCargos"/>. Devuelve null si no hay cargos.
        /// En v4.4 cada cargo es un elemento &lt;OtrosCargos&gt; independiente (se repite,
        /// no lleva envoltorio). Cuando el tipo es "99" se emite &lt;TipoDocumentoOtroOC&gt;
        /// con la descripción libre. Los datos del tercero son opcionales.
        /// </summary>
        private static IEnumerable<XElement>? BuildOtrosCargos(XNamespace ns, ElectronicDocument d, out decimal totalOtrosCargos)
        {
            totalOtrosCargos = 0m;
            if (string.IsNullOrWhiteSpace(d.OtherCharges)) return null;

            List<OtherChargeData> charges;
            try
            {
                charges = System.Text.Json.JsonSerializer.Deserialize<List<OtherChargeData>>(
                    d.OtherCharges,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }

            var nodes = new List<XElement>();
            foreach (var c in charges)
            {
                if (c.Amount <= 0 || string.IsNullOrWhiteSpace(c.TypeCode)) continue;

                var oc = new XElement(ns + "OtrosCargos",
                    new XElement(ns + "TipoDocumentoOC", c.TypeCode));

                // Solo el tipo "99" admite la descripción libre del documento (TipoDocumentoOtroOC).
                if (c.TypeCode.Trim() == "99" && !string.IsNullOrWhiteSpace(c.OtherTypeDescription))
                    oc.Add(new XElement(ns + "TipoDocumentoOtroOC", c.OtherTypeDescription.Trim()));

                // Datos del tercero (opcionales).
                if (!string.IsNullOrWhiteSpace(c.ThirdIdentNumber))
                    oc.Add(new XElement(ns + "NumeroIdentidadTercero", c.ThirdIdentNumber.Trim()));
                if (!string.IsNullOrWhiteSpace(c.ThirdName))
                    oc.Add(new XElement(ns + "NombreTercero", c.ThirdName.Trim()));

                oc.Add(new XElement(ns + "Detalle", c.Detail ?? string.Empty));
                oc.Add(new XElement(ns + "MontoCargo", Num(c.Amount)));

                nodes.Add(oc);
                totalOtrosCargos += c.Amount;
            }

            return nodes.Count == 0 ? null : nodes;
        }

        /// <summary>Modelo interno para deserializar los otros cargos persistidos en JSON.</summary>
        private sealed class OtherChargeData
        {
            public string TypeCode { get; set; } = string.Empty;
            public string? OtherTypeDescription { get; set; }
            public string? Detail { get; set; }
            public decimal Amount { get; set; }
            public string? ThirdIdentType { get; set; }
            public string? ThirdIdentNumber { get; set; }
            public string? ThirdName { get; set; }
        }

        private static string Num(decimal value, int decimals = 5) =>
            Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, Inv);

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

        /// <summary>
        /// Deserializa la lista de descuentos persistida en la columna JSON de la línea.
        /// Devuelve una lista vacía si no hay datos o el JSON es inválido (fallback al
        /// escalar único en el llamador).
        /// </summary>
        private static List<(decimal Amount, string? Nature)> ParseLineDiscounts(ElectronicDocumentLine line)
        {
            var result = new List<(decimal, string?)>();
            if (string.IsNullOrWhiteSpace(line.Discounts)) return result;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(line.Discounts);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return result;
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    decimal amount = 0;
                    string? nature = null;
                    if (el.TryGetProperty("amount", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.Number)
                        amount = a.GetDecimal();
                    if (el.TryGetProperty("nature", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String)
                        nature = n.GetString();
                    if (amount > 0)
                        result.Add((amount, nature));
                }
            }
            catch (System.Text.Json.JsonException)
            {
                return new List<(decimal, string?)>();
            }
            return result;
        }
    }
}
