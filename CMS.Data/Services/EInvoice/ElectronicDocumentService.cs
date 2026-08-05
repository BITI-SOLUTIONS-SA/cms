// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/ElectronicDocumentService.cs
// PROPÓSITO: Orquestador de emisión de comprobantes electrónicos CR v4.4
// DESCRIPCIÓN: Coordina todo el flujo de emisión:
//                1. Validaciones fiscales (CAByS, referencias NC/ND/REP).
//                2. Cálculo de líneas/impuestos (incluye IVI inverso).
//                3. Generación atómica de la Clave Numérica de 50 díg.
//                4. Persistencia del documento + líneas + impuestos + referencias.
//                5. Construcción y firma XAdES-EPES del XML.
//                6. Envío a Hacienda; si falla -> contingencia + cola de reintentos.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.EInvoice;
using CMS.Entities.Operational;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services.EInvoice
{
    /// <inheritdoc cref="IElectronicDocumentService"/>
    public class ElectronicDocumentService : IElectronicDocumentService
    {
        private static readonly TimeSpan RetryBase = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RetryCap = TimeSpan.FromHours(1);

        private readonly ICompanyDbContextFactory _companyDbContextFactory;
        private readonly IClaveNumericaGenerator _claveGenerator;
        private readonly IElectronicDocumentXmlBuilder _xmlBuilder;
        private readonly IXadesSignatureService _signatureService;
        private readonly IHaciendaAuthService _authService;
        private readonly IHaciendaApiClient _apiClient;
        private readonly IEInvoicePdfService _pdfService;
        private readonly ILogger<ElectronicDocumentService> _logger;

        public ElectronicDocumentService(
            ICompanyDbContextFactory companyDbContextFactory,
            IClaveNumericaGenerator claveGenerator,
            IElectronicDocumentXmlBuilder xmlBuilder,
            IXadesSignatureService signatureService,
            IHaciendaAuthService authService,
            IHaciendaApiClient apiClient,
            IEInvoicePdfService pdfService,
            ILogger<ElectronicDocumentService> logger)
        {
            _companyDbContextFactory = companyDbContextFactory;
            _claveGenerator = claveGenerator;
            _xmlBuilder = xmlBuilder;
            _signatureService = signatureService;
            _authService = authService;
            _apiClient = apiClient;
            _pdfService = pdfService;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<EmitDocumentResult> EmitAsync(EmitDocumentInput input, CancellationToken cancellationToken = default)
        {
            var logs = new List<EmitLogEntry>();
            void Log(string step, string message, string level = "INFO")
            {
                logs.Add(new EmitLogEntry { Step = step, Message = message, Level = level });
                _logger.LogInformation("[Emit][{Step}] {Message}", step, message);
            }

            Log("INICIO", "Iniciando proceso de emisión del comprobante electrónico.");

            if (input.Lines.Count == 0)
                throw new InvalidOperationException("El comprobante debe tener al menos una línea.");

            await using var db = await _companyDbContextFactory.CreateDbContextAsync(input.CompanyId);

            // Obtener credencial del emisor (CustomerBillingCredential con is_issuer=true)
            var issuerCredential = await db.CustomerBillingCredentials
                .FirstOrDefaultAsync(c => c.Id == input.IssuerId && c.IsIssuer && c.IsActive, cancellationToken)
                ?? throw new InvalidOperationException($"Credencial de emisor {input.IssuerId} no encontrada.");
            Log("EMISOR", $"Emisor cargado: {issuerCredential.Name} ({issuerCredential.Identification}).");

            // Obtener credencial del receptor si aplica.
            // El Tiquete Electrónico (TE) es para consumidor final y su XSD NO admite
            // el bloque Receptor: forzamos su omisión aunque venga un ReceptorId en el input.
            var receptorCredential =
                input.DocumentType != EInvoiceDocumentType.TiqueteElectronico && input.ReceptorId.HasValue
                ? await db.CustomerBillingCredentials.FirstOrDefaultAsync(c => c.Id == input.ReceptorId.Value && c.IsActive, cancellationToken)
                : null;
            if (input.DocumentType == EInvoiceDocumentType.TiqueteElectronico)
                Log("RECEPTOR", "Tiquete Electrónico: se omite el receptor (consumidor final).");
            else if (receptorCredential != null)
                Log("RECEPTOR", $"Receptor cargado: {receptorCredential.Name} ({receptorCredential.Identification}).");
            else
                Log("RECEPTOR", "Comprobante sin receptor (tiquete/consumidor final).");

            // Validaciones fiscales.
            ValidateBusinessRules(input);
            Log("VALIDACION", "Reglas de negocio validadas correctamente.", "SUCCESS");

            // Validación específica de Nota de Crédito: las líneas deben corresponder
            // a la factura referenciada (no se pueden inventar líneas que no existan).
            if (input.DocumentType == EInvoiceDocumentType.NotaCredito)
            {
                await ValidateReversalLinesAsync(db, input, issuerCredential,
                    expectedSourceType: null, Log, cancellationToken);
            }
            // Validación específica de Nota de Débito: SOLO puede emitirse referenciando
            // una Nota de Crédito aceptada (no puede referenciar una factura directamente).
            else if (input.DocumentType == EInvoiceDocumentType.NotaDebito)
            {
                await ValidateReversalLinesAsync(db, input, issuerCredential,
                    expectedSourceType: EInvoiceDocumentType.NotaCredito, Log, cancellationToken);
            }
            // Validación específica de Recibo Electrónico de Pago: documenta el pago (total o
            // parcial) de una factura a crédito. Las líneas deben corresponder a la factura
            // referenciada y las cantidades no pueden superar el saldo pendiente de pago.
            else if (input.DocumentType == EInvoiceDocumentType.ReciboElectronicoPago)
            {
                await ValidateReceiptLinesAsync(db, input, issuerCredential, Log, cancellationToken);
            }

            // Hora oficial de Costa Rica (UTC-6). Se almacena en UTC (columna timestamptz)
            // pero la Clave Numérica y la FechaEmision del XML usan la fecha local CR.
            var crNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6));
            var issueDate = crNow.UtcDateTime; // Kind=Utc, INMUTABLE a partir de aquí.
            var crLocalDate = crNow.DateTime;  // fecha/hora local CR para clave y XML

            // 1. Generar Clave Numérica de 50 díg. (atómico).
            var clave = await _claveGenerator.GenerateAsync(
                input.CompanyId, issuerCredential.IdCustomer ?? 0, issuerCredential.Identification, input.DocumentType,
                input.Branch, input.Terminal, EInvoiceSituation.Normal, crLocalDate, input.UserId);
            Log("CLAVE", $"Clave numérica generada: {clave.Clave}");

            // 2. Construir cabecera + líneas + impuestos.
            var document = new ElectronicDocument
            {
                IdCustomerIssuer = issuerCredential.IdCustomer ?? 0,
                IdCustomerReceptor = receptorCredential?.IdCustomer,
                DocumentType = input.DocumentType,
                Clave = clave.Clave,
                Consecutive = clave.Consecutive,
                Situation = EInvoiceSituation.Normal,
                Status = EInvoiceStatus.Borrador,
                IssueDate = issueDate,
                SaleCondition = input.SaleCondition,
                CreditTerm = input.CreditTerm,
                PaymentMethod = input.PaymentMethod,
                Currency = input.Currency,
                ExchangeRate = input.ExchangeRate,
                CreateDate = DateTime.UtcNow,
                RecordDate = DateTime.UtcNow,
                CreatedBy = input.UserId.ToString(),
                UpdatedBy = input.UserId.ToString(),

                // ── Datos del Emisor (desde CustomerBillingCredential) ────────
                EmisorNombre                = issuerCredential.Name,
                EmisorNombreComercial       = issuerCredential.CommercialName,
                EmisorIdentificacionTipo    = issuerCredential.IdentificationType,
                EmisorIdentificacionNumero  = issuerCredential.Identification,
                EmisorCorreo                = issuerCredential.Email,
                EmisorUbicacionProvincia    = issuerCredential.Province,
                EmisorUbicacionCanton       = issuerCredential.Canton,
                EmisorUbicacionDistrito     = issuerCredential.District,
                EmisorUbicacionOtrasSenas   = issuerCredential.OtherSigns,
                EmisorTelefonoCodigoPais    = issuerCredential.PhoneCode,
                EmisorTelefonoNumero        = issuerCredential.Phone,
                CodigoActividadEmisor       = issuerCredential.EconomicActivity,
                ProveedorSistemas           = "2100042005", // Proveedor BSFLOW registrado en Hacienda

                // ── Datos del Receptor (si aplica) ────────────────────────────
                ReceptorNombre                    = receptorCredential?.Name,
                ReceptorNombreComercial           = receptorCredential?.CommercialName,
                ReceptorIdentificacionTipo        = receptorCredential?.IdentificationType,
                ReceptorIdentificacionNumero      = receptorCredential?.Identification,
                ReceptorIdentificacionExtranjero  = receptorCredential?.ForeignIdentification,
                ReceptorCorreo                    = receptorCredential?.Email,
                ReceptorUbicacionProvincia        = receptorCredential?.Province,
                ReceptorUbicacionCanton           = receptorCredential?.Canton,
                ReceptorUbicacionDistrito         = receptorCredential?.District,
                ReceptorUbicacionOtrasSenas       = receptorCredential?.OtherSigns,
                ReceptorTelefonoCodigoPais        = receptorCredential?.PhoneCode,
                ReceptorTelefonoNumero            = receptorCredential?.Phone,
            };

            var taxesByLine = new Dictionary<int, List<ElectronicDocumentTax>>();
            var lineNumber = 0;
            foreach (var li in input.Lines)
            {
                lineNumber++;
                var bd = EInvoiceCalculator.BreakdownLine(
                    li.UnitPrice, li.Quantity, li.TaxRatePercent, li.DiscountAmount, li.PriceIncludesTax);

                // ── Exoneración (documento completo o línea a línea) ─────────────
                // Regla: si el documento es exonerado, TODAS las líneas se exoneran.
                // Si no, se respeta la exoneración indicada por línea.
                bool lineExonerated = input.IsExonerated || li.IsExonerated;
                decimal exonPercent = lineExonerated
                    ? (li.ExonPercent > 0 ? Math.Min(li.ExonPercent, 100m) : 100m)
                    : 0m;
                // Hacienda v4.4 (error -190): MontoExoneracion = %exoneración × BaseImponible
                // (el subtotal NETO tras descuento), es decir la BASE que se exonera, NO el
                // impuesto. El impuesto efectivamente exonerado se deriva multiplicando esa
                // base por la tarifa de IVA de la línea.
                // Hacienda v4.4 (rechazos -190 y -290): MontoExoneracion es el IMPUESTO
                // exonerado (NO la base). Hacienda valida dos fórmulas simultáneas:
                //   -190: MontoExoneracion = (TarifaExonerada/100) × SubTotal
                //   -290: ImpuestoNeto     = Monto(IVA) − MontoExoneracion
                // Ambas se cumplen solo si: MontoExoneracion = IVA × %exon  y
                // TarifaExonerada = IVA% × %exon (tarifa efectiva, p.ej. 13 para exon. total).
                decimal exonTax  = Math.Round(bd.TaxAmount * exonPercent / 100m, 5);   // IVA exonerado = MontoExoneracion
                decimal impuestoNeto = bd.TaxAmount - exonTax;   // IVA efectivamente cobrado
                decimal totalLineNet = bd.TotalLine - exonTax;   // el cliente no paga la parte exonerada

                var line = new ElectronicDocumentLine
                {
                    LineNumber = lineNumber,
                    IdItem = li.ItemId,
                    CabysCode = li.CabysCode,
                    ItemCode = li.ItemCode,
                    Detail = li.Detail,
                    // La naturaleza bien/servicio la determina el CAByS (estándar CAByS-CR),
                    // NO el cliente. El primer dígito del código define la categoría: los
                    // dígitos 1-6 son mercancías/bienes y 7-9 son servicios. Esto garantiza
                    // que el ResumenFactura desglose correctamente TotalServGravados vs
                    // TotalMercanciasGravadas y evita los rechazos -110/-111 de Hacienda.
                    // Aplica a TODOS los tipos de comprobante (FE, FEC, NC, ND, TE, REP)
                    // porque todos construyen sus líneas por esta ruta.
                    IsService = IsServiceByCabys(li.CabysCode, li.IsService),
                    Quantity = li.Quantity,
                    UnitMeasure = li.UnitMeasure,
                    UnitPrice = bd.UnitPriceBase,
                    TotalAmount = bd.UnitPriceBase * li.Quantity,
                    DiscountAmount = li.DiscountAmount,
                    DiscountNature = li.DiscountAmount > 0 ? (li.DiscountNature ?? EInvoiceDiscountNature.Promocion) : null,
                    // Hacienda v4.4: SubTotal = MontoTotal − MontoDescuento (neto tras descuento).
                    // Poner el bruto aquí provoca los rechazos -44/-454/-46. Como no hay
                    // impuesto selectivo de consumo, SubTotal == BaseImponible (bd.TaxableBase).
                    SubTotal = bd.UnitPriceBase * li.Quantity - li.DiscountAmount,
                    TaxableBase = bd.TaxableBase,
                    TotalTax = bd.TaxAmount,
                    TotalLine = totalLineNet,
                    // ── Campos fiscales adicionales ──────────────────
                    ImpuestoAsumidoEmisor = 0,                        // Se actualiza si aplica emisor/fábrica
                    ImpuestoNeto          = impuestoNeto,            // IVA - exoneración
                    MontoTotalLinea       = totalLineNet,           // total con impuesto neto
                    TaxRateCodeIva        = li.TaxRateCode,
                    TaxRateIva            = li.TaxRatePercent / 100m, // porcentaje → decimal (0.13)
                    MontoTaxIva           = bd.TaxAmount,
                    // ── Exoneración ──────────────────────────────────
                    IsExonerated       = lineExonerated,
                    ExonPercent        = exonPercent,
                    ExonAmount         = exonTax,   // MontoExoneracion = IVA exonerado (v4.4 -290/-46)
                    ExonDocumentType   = lineExonerated ? (li.ExonDocumentType ?? "99") : null,
                    ExonDocumentNumber = lineExonerated ? li.ExonDocumentNumber : null,
                    ExonInstitution    = lineExonerated ? li.ExonInstitution : null,
                    ExonDate           = lineExonerated ? (li.ExonDate ?? DateTime.UtcNow) : null,
                    CreateDate = DateTime.UtcNow,
                    RecordDate = DateTime.UtcNow,
                    CreatedBy = input.UserId.ToString(),
                    UpdatedBy = input.UserId.ToString()
                };

                var tax = new ElectronicDocumentTax
                {
                    TaxCode = "01",
                    TaxRateCode = li.TaxRateCode,
                    TaxRate = li.TaxRatePercent,
                    TaxAmount = bd.TaxAmount,
                    CreateDate = DateTime.UtcNow,
                    RecordDate = DateTime.UtcNow,
                    CreatedBy = input.UserId.ToString(),
                    UpdatedBy = input.UserId.ToString()
                };
                line.Taxes.Add(tax);
                document.Lines.Add(line);
            }

            // Totales de cabecera.
            document.IsExonerated    = input.IsExonerated;
            document.SubTotal        = document.Lines.Sum(l => l.SubTotal);
            document.TotalDiscount   = document.Lines.Sum(l => l.DiscountAmount);
            // Base gravada = líneas con IVA efectivo (no exoneradas al 100%).
            document.TotalTaxable    = document.Lines.Where(l => l.ImpuestoNeto > 0).Sum(l => l.TaxableBase);
            document.TotalExempt     = document.Lines.Where(l => l.TotalTax == 0 && !l.IsExonerated).Sum(l => l.TaxableBase);
            // Impuesto = solo el IVA neto efectivamente cobrado (descontada la exoneración).
            document.TotalTaxes      = document.Lines.Sum(l => l.ImpuestoNeto);
            document.Total           = document.Lines.Sum(l => l.TotalLine);

            // ── ResumenFactura completo (campos del XML v4.4) ─────────────────
            var linesServ     = document.Lines.Where(l => l.IsService).ToList();
            var linesBien     = document.Lines.Where(l => !l.IsService).ToList();

            // Una línea se considera "exonerada" para el resumen cuando tiene monto exonerado > 0.
            bool IsExon(ElectronicDocumentLine l) => l.IsExonerated && l.ExonAmount > 0;

            // Hacienda v4.4: los totales de CLASIFICACIÓN usan el BRUTO por línea
            // (MontoTotal = TotalAmount, antes del descuento). Su suma = TotalVenta.
            // El descuento se refleja solo en TotalDescuentos y TotalVentaNeta.
            document.TotalServGravados       = linesServ.Where(l => l.ImpuestoNeto > 0 && !IsExon(l)).Sum(l => l.TotalAmount);
            document.TotalServExentos        = linesServ.Where(l => l.TotalTax == 0 && !IsExon(l)).Sum(l => l.TotalAmount);
            document.TotalServExonerado      = linesServ.Where(IsExon).Sum(l => l.TotalAmount);
            document.TotalMercanciasGravadas = linesBien.Where(l => l.ImpuestoNeto > 0 && !IsExon(l)).Sum(l => l.TotalAmount);
            document.TotalMercanciasExentas  = linesBien.Where(l => l.TotalTax == 0 && !IsExon(l)).Sum(l => l.TotalAmount);
            document.TotalMercExonerada      = linesBien.Where(IsExon).Sum(l => l.TotalAmount);
            document.TotalGravado            = document.TotalServGravados + document.TotalMercanciasGravadas;
            document.TotalExonerado          = document.Lines.Where(IsExon).Sum(l => l.TotalAmount);
            document.TotalNoSujeto           = 0;
            document.TotalVenta              = document.Lines.Sum(l => l.TotalAmount);   // Σ bruto
            document.TotalVentaNeta          = document.TotalVenta - document.TotalDiscount;
            document.TotalImpuestoDescontado = 0;
            document.TotalIvaDevuelto        = 0;
            document.TotalComprobante        = document.Total;
            document.MedioPagoTipo           = input.PaymentMethod;
            document.MedioPagoTotal          = document.Total;

            // Desglose del impuesto principal (primer impuesto de la primera línea gravada)
            var firstTaxLine = document.Lines.FirstOrDefault(l => l.ImpuestoNeto > 0);
            if (firstTaxLine != null)
            {
                document.DesgloseImpuestoCodigo   = "01";
                document.DesgloseImpuestoTarifaIva = firstTaxLine.TaxRateCodeIva;
                document.DesgloseImpuestoMonto    = document.TotalTaxes;
            }

            // Referencias (NC/ND/REP).
            foreach (var r in input.References)
            {
                document.References.Add(new ElectronicDocumentReference
                {
                    RefDocumentType = r.RefDocumentType,
                    RefClave = r.RefClave,
                    RefDate = r.RefDate,
                    RefCode = r.RefCode,
                    RefReason = r.RefReason,
                    CreateDate = DateTime.UtcNow,
                    RecordDate = DateTime.UtcNow,
                    CreatedBy = input.UserId.ToString(),
                    UpdatedBy = input.UserId.ToString()
                });
            }

            db.ElectronicDocuments.Add(document);
            await db.SaveChangesAsync(cancellationToken);
            Log("PERSISTENCIA", $"Documento persistido (ID {document.Id}, consecutivo {document.Consecutive}).", "SUCCESS");

            // Mapear impuestos por Id de línea (ya persistido).
            foreach (var line in document.Lines)
                taxesByLine[line.Id] = line.Taxes.ToList();

            // 3. Construir y firmar el XML.
            var unsignedXml = _xmlBuilder.BuildXml(
                document, issuerCredential, receptorCredential, document.Lines.ToList(), taxesByLine, document.References.ToList());
            Log("XML", "XML v4.4 construido.");
            var signedXml = _signatureService.SignXml(unsignedXml, issuerCredential);
            Log("FIRMA", "XML firmado con XAdES-EPES.", "SUCCESS");

            document.XmlSigned = signedXml;
            document.Status = EInvoiceStatus.Firmado;
            document.UpdatedBy = input.UserId.ToString();
            document.RecordDate = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            // Generar la representación PDF del comprobante a partir de los datos
            // ya persistidos en el documento (emisor/receptor/resumen).
            try
            {
                document.PdfDocument = _pdfService.GeneratePdf(document, document.Lines.ToList());
                await db.SaveChangesAsync(cancellationToken);
                Log("PDF", "Representación PDF generada.", "SUCCESS");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo generar el PDF del documento {Id}", document.Id);
                Log("PDF", $"No se pudo generar el PDF: {ex.Message}", "WARNING");
            }

            // 4. Intentar enviar a Hacienda (resiliente).
            var result = new EmitDocumentResult
            {
                DocumentId = document.Id,
                Clave = document.Clave!,
                Consecutive = document.Consecutive!,
                Status = document.Status
            };

            try
            {
                Log("ENVIO", "Enviando comprobante a Hacienda...");
                await SendAndTrackAsync(db, document, issuerCredential, cancellationToken);
                result.Status = document.Status;
                result.SentToHacienda = document.Status is EInvoiceStatus.Enviado or EInvoiceStatus.Procesando or EInvoiceStatus.Aceptado;
                result.Message = "Comprobante emitido y enviado a Hacienda.";
                Log("RESPUESTA", $"Respuesta recibida. Estado: {document.Status}.", "SUCCESS");
            }
            catch (Exception ex)
            {
                // Contingencia: nunca falla la emisión.
                _logger.LogWarning(ex, "Hacienda no disponible; documento {Id} a contingencia.", document.Id);
                await MoveToContingencyAsync(db, document, cancellationToken);
                result.Status = EInvoiceStatus.Contingencia;
                result.Message = "Hacienda no disponible. Comprobante en contingencia; se reintentará automáticamente.";
                Log("CONTINGENCIA", $"Hacienda no disponible: {ex.Message}. Documento en contingencia.", "WARNING");
            }

            Log("FIN", "Proceso de emisión finalizado.", "SUCCESS");
            result.Logs = logs;

            // Persistir la bitácora en sinai.electronic_document_log.
            try
            {
                foreach (var entry in logs)
                {
                    db.ElectronicDocumentLogs.Add(new ElectronicDocumentLog
                    {
                        IdElectronicDocument = document.Id,
                        Clave = document.Clave,
                        Step = entry.Step,
                        Level = entry.Level,
                        Message = entry.Message,
                        CreateDate = entry.Timestamp,
                        RecordDate = entry.Timestamp,
                        CreatedBy = input.UserId.ToString(),
                        UpdatedBy = input.UserId.ToString()
                    });
                }
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo persistir la bitácora del documento {Id}", document.Id);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task ProcessPendingAsync(int companyId, int documentId, CancellationToken cancellationToken = default)
        {
            await using var db = await _companyDbContextFactory.CreateDbContextAsync(companyId);
            var document = await db.ElectronicDocuments.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (document == null) return;

            var issuerCredential = await db.CustomerBillingCredentials
                .FirstOrDefaultAsync(c => c.IdCustomer == document.IdCustomerIssuer && c.IsIssuer && c.IsActive, cancellationToken);
            if (issuerCredential == null) return;

            await SendAndTrackAsync(db, document, issuerCredential, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<PollStatusResult> PollStatusAsync(int companyId, int documentId, CancellationToken cancellationToken = default)
        {
            await using var db = await _companyDbContextFactory.CreateDbContextAsync(companyId);
            var document = await db.ElectronicDocuments.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
            if (document == null)
                return new PollStatusResult { Status = "NoEncontrado", Message = "Comprobante no encontrado." };

            // Si ya está resuelto, no hace falta volver a consultar a Hacienda.
            if (document.Status is EInvoiceStatus.Aceptado or EInvoiceStatus.Rechazado)
            {
                return new PollStatusResult
                {
                    Status = document.Status,
                    HaciendaStatus = document.HaciendaStatus,
                    HaciendaDetail = document.HaciendaDetail,
                    Resolved = true,
                    Message = document.Status == EInvoiceStatus.Aceptado
                        ? "Comprobante aceptado por Hacienda."
                        : "Comprobante rechazado por Hacienda."
                };
            }

            // Solo tiene sentido consultar si el documento ya fue enviado/está procesando.
            // Si está "Pendiente" o en "Contingencia", el envío quedó encolado y aún NO
            // llegó a Hacienda: primero intentamos reenviarlo (ProcessPending/SendAndTrack)
            // y, si pasa a "Procesando", continuamos con la consulta de estado.
            if (document.Status is EInvoiceStatus.Pendiente or EInvoiceStatus.Contingencia)
            {
                var issuer = await db.CustomerBillingCredentials
                    .FirstOrDefaultAsync(c => c.IdCustomer == document.IdCustomerIssuer && c.IsIssuer && c.IsActive, cancellationToken);
                if (issuer == null)
                    return new PollStatusResult { Status = document.Status, Resolved = false, Message = "Sin credencial de emisor para reenviar." };

                await SendAndTrackAsync(db, document, issuer, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                // Si tras reenviar quedó resuelto (rechazo inmediato) o sigue pendiente, devolvemos ya.
                if (document.Status is EInvoiceStatus.Aceptado or EInvoiceStatus.Rechazado)
                {
                    return new PollStatusResult
                    {
                        Status = document.Status,
                        HaciendaStatus = document.HaciendaStatus,
                        HaciendaDetail = document.HaciendaDetail,
                        Resolved = true,
                        Message = document.Status == EInvoiceStatus.Aceptado
                            ? "Comprobante aceptado por Hacienda."
                            : "Comprobante rechazado por Hacienda."
                    };
                }
                if (document.Status is not (EInvoiceStatus.Procesando or EInvoiceStatus.Enviado))
                {
                    return new PollStatusResult
                    {
                        Status = document.Status,
                        HaciendaStatus = document.HaciendaStatus,
                        HaciendaDetail = document.HaciendaDetail,
                        Resolved = false,
                        Message = "El comprobante fue reenviado a Hacienda. Vuelva a consultar en unos momentos."
                    };
                }
                // Si pasó a Procesando, continuamos abajo con la consulta de estado.
            }
            else if (document.Status is not (EInvoiceStatus.Procesando or EInvoiceStatus.Enviado) || string.IsNullOrEmpty(document.Clave))
            {
                return new PollStatusResult
                {
                    Status = document.Status,
                    HaciendaStatus = document.HaciendaStatus,
                    HaciendaDetail = document.HaciendaDetail,
                    Resolved = false,
                    Message = "El comprobante aún no ha sido enviado a Hacienda."
                };
            }

            var credential = await db.CustomerBillingCredentials
                .FirstOrDefaultAsync(c => c.IdCustomer == document.IdCustomerIssuer && c.IsIssuer && c.IsActive, cancellationToken);
            if (credential == null)
                return new PollStatusResult { Status = document.Status, Resolved = false, Message = "Sin credencial de emisor para consultar." };

            var result = new PollStatusResult();
            try
            {
                var token = await _authService.GetAccessTokenAsync(credential, cancellationToken);
                var status = await _apiClient.GetStatusAsync(credential, token, document.Clave!, cancellationToken);
                if (status.Unauthorized)
                {
                    token = await _authService.ForceRefreshAsync(credential, cancellationToken);
                    status = await _apiClient.GetStatusAsync(credential, token, document.Clave!, cancellationToken);
                }

                if (status.Status is "aceptado")
                {
                    document.Status = EInvoiceStatus.Aceptado;
                    document.HaciendaStatus = status.Status;
                    document.HaciendaDetail = status.HaciendaDetail;
                    document.XmlResponse = status.HaciendaMessageXml ?? status.ResponseBody;
                    document.AcceptedAt = DateTime.UtcNow;
                    ParseAndSaveHaciendaResponse(document, document.XmlResponse);
                    result.Resolved = true;
                    result.Message = "Comprobante aceptado por Hacienda.";
                    // Marcar como resueltos los reintentos de PollStatus pendientes.
                    await CloseRetriesAsync(db, document.Id, cancellationToken);
                }
                else if (status.Status is "rechazado")
                {
                    document.Status = EInvoiceStatus.Rechazado;
                    document.HaciendaStatus = status.Status;
                    document.HaciendaDetail = status.HaciendaDetail;
                    document.XmlResponse = status.HaciendaMessageXml ?? status.ResponseBody;
                    ParseAndSaveHaciendaResponse(document, document.XmlResponse);
                    result.Resolved = true;
                    result.Message = "Comprobante rechazado por Hacienda.";
                    await CloseRetriesAsync(db, document.Id, cancellationToken);
                }
                else
                {
                    // Sigue procesando en Hacienda.
                    document.HaciendaStatus = status.Status;
                    result.Resolved = false;
                    result.Message = "Hacienda aún está procesando el comprobante. Intente nuevamente en unos momentos.";
                }

                result.Status = document.Status;
                result.HaciendaStatus = document.HaciendaStatus;
                result.HaciendaDetail = document.HaciendaDetail;

                // Registrar la consulta en la bitácora.
                db.ElectronicDocumentLogs.Add(new ElectronicDocumentLog
                {
                    IdElectronicDocument = document.Id,
                    Clave = document.Clave,
                    Step = "CONSULTA",
                    Level = result.Resolved
                        ? (document.Status == EInvoiceStatus.Aceptado ? "SUCCESS" : "ERROR")
                        : "INFO",
                    Message = result.Message ?? $"Estado consultado: {document.Status}.",
                    Detail = document.HaciendaDetail,
                    CreateDate = DateTime.UtcNow,
                    RecordDate = DateTime.UtcNow,
                    CreatedBy = "poll",
                    UpdatedBy = "poll"
                });

                document.RecordDate = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo consultar el estado del documento {Id} en Hacienda.", document.Id);
                result.Status = document.Status;
                result.Resolved = false;
                result.Message = "No se pudo consultar el estado en Hacienda en este momento.";
            }

            return result;
        }

        /// <summary>Marca como completados los reintentos de PollStatus de un documento ya resuelto.</summary>
        private static async Task CloseRetriesAsync(CompanyDbContext db, int documentId, CancellationToken ct)
        {
            var pending = await db.EInvoiceRetryQueue
                .Where(q => q.IdElectronicDocument == documentId && !q.IsDone)
                .ToListAsync(ct);
            foreach (var q in pending) q.IsDone = true;
        }

        /// <summary>Envía el comprobante y actualiza el estado, manejando 401/429/duplicado.</summary>
        private async Task SendAndTrackAsync(
            CompanyDbContext db, ElectronicDocument document, CustomerBillingCredential issuerCredential,
            CancellationToken ct)
        {
            var token = await _authService.GetAccessTokenAsync(issuerCredential, ct);

            var receptorCredential = document.IdCustomerReceptor.HasValue
                ? await db.CustomerBillingCredentials.FirstOrDefaultAsync(c => c.IdCustomer == document.IdCustomerReceptor.Value && c.IsActive, ct)
                : null;
            var payload = BuildReceptionPayload(document, issuerCredential, receptorCredential);
            var result = await _apiClient.SubmitAsync(issuerCredential, token, document.Clave!, payload, ct);

            if (result.Unauthorized)
            {
                token = await _authService.ForceRefreshAsync(issuerCredential, ct);
                result = await _apiClient.SubmitAsync(issuerCredential, token, document.Clave!, payload, ct);
            }

            if (result.ShouldRetry)
            {
                await EnqueueRetryAsync(db, document, EInvoiceRetryOperation.Send, result.RetryAfterSeconds, result.Error, ct);
                document.Status = EInvoiceStatus.Pendiente;
            }
            else if (result.Accepted)
            {
                document.Status = EInvoiceStatus.Procesando;
                document.HaciendaStatus = result.Status;
                document.SubmittedAt = DateTime.UtcNow;
                document.XmlResponse = result.ResponseBody;
                ParseAndSaveHaciendaResponse(document, result.ResponseBody);
                await EnqueueRetryAsync(db, document, EInvoiceRetryOperation.PollStatus, 30, null, ct);
            }
            else
            {
                document.Status = EInvoiceStatus.Rechazado;
                document.HaciendaStatus = result.Status;
                document.XmlResponse = result.ResponseBody ?? result.Error;
                ParseAndSaveHaciendaResponse(document, document.XmlResponse);
            }

            document.RecordDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        private async Task MoveToContingencyAsync(CompanyDbContext db, ElectronicDocument document, CancellationToken ct)
        {
            document.Status = EInvoiceStatus.Contingencia;
            document.Situation = EInvoiceSituation.SinInternet;
            await EnqueueRetryAsync(db, document, EInvoiceRetryOperation.Send, 60, "Contingencia: sin conexión a Hacienda.", ct);
            document.RecordDate = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        private static async Task EnqueueRetryAsync(
            CompanyDbContext db, ElectronicDocument document, string operation,
            int? retryAfterSeconds, string? error, CancellationToken ct)
        {
            var existing = await db.EInvoiceRetryQueue
                .FirstOrDefaultAsync(q => q.IdElectronicDocument == document.Id && q.Operation == operation && !q.IsDone, ct);

            var delay = retryAfterSeconds.HasValue
                ? TimeSpan.FromSeconds(retryAfterSeconds.Value)
                : RetryBase;

            if (existing == null)
            {
                db.EInvoiceRetryQueue.Add(new EInvoiceRetryQueue
                {
                    IdElectronicDocument = document.Id,
                    Operation = operation,
                    AttemptCount = 1,
                    NextAttemptAt = DateTime.UtcNow.Add(delay),
                    LastError = error,
                    IsDone = false,
                    CreateDate = DateTime.UtcNow,
                    RecordDate = DateTime.UtcNow,
                    CreatedBy = "ElectronicDocumentService",
                    UpdatedBy = "ElectronicDocumentService"
                });
            }
            else
            {
                existing.AttemptCount++;
                var backoff = TimeSpan.FromSeconds(
                    Math.Min(RetryCap.TotalSeconds, RetryBase.TotalSeconds * Math.Pow(2, existing.AttemptCount)));
                existing.NextAttemptAt = DateTime.UtcNow.Add(retryAfterSeconds.HasValue ? delay : backoff);
                existing.LastError = error;
                existing.RecordDate = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Arma el payload JSON de recepción según el estándar de Hacienda:
        /// clave, fecha, emisor (tipo+número identificación), receptor (opcional) y
        /// el XML firmado en base64 (nodo comprobanteXml).
        /// </summary>
        private static object BuildReceptionPayload(
            ElectronicDocument document, CustomerBillingCredential issuerCredential, CustomerBillingCredential? receptorCredential)
        {
            var payload = new Dictionary<string, object?>
            {
                ["clave"] = document.Clave,
                ["fecha"] = ToCrDateString(document.IssueDate),
                ["emisor"] = new
                {
                    tipoIdentificacion = issuerCredential.IdentificationType,
                    numeroIdentificacion = new string(issuerCredential.Identification.Where(char.IsDigit).ToArray())
                },
                ["comprobanteXml"] = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(document.XmlSigned ?? string.Empty))
            };

            if (receptorCredential != null && !string.IsNullOrWhiteSpace(receptorCredential.IdentificationType)
                && !string.IsNullOrWhiteSpace(receptorCredential.Identification))
            {
                payload["receptor"] = new
                {
                    tipoIdentificacion = receptorCredential.IdentificationType,
                    numeroIdentificacion = new string(receptorCredential.Identification.Where(char.IsDigit).ToArray())
                };
            }

            return payload;
        }

        /// <summary>Parsea el XML de respuesta de Hacienda y guarda los campos en el documento.</summary>
        private static void ParseAndSaveHaciendaResponse(ElectronicDocument document, string? xmlResponse)
        {
            if (string.IsNullOrEmpty(xmlResponse)) return;
            try
            {
                var xdoc = System.Xml.Linq.XDocument.Parse(xmlResponse);
                var ns = xdoc.Root?.Name.Namespace ?? System.Xml.Linq.XNamespace.None;
                string? V(string name) =>
                    (xdoc.Root?.Element(ns + name) ?? xdoc.Root?.Element(System.Xml.Linq.XName.Get(name)))?.Value?.Trim();

                document.HaciendaMensajeCodigo = V("Mensaje");
                document.HaciendaDetail        = V("DetalleMensaje") ?? document.HaciendaDetail;

                if (decimal.TryParse(V("MontoTotalImpuesto"), System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var monto))
                    document.HaciendaMontoImpuesto = monto;

                if (decimal.TryParse(V("TotalFactura"), System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var total))
                    document.HaciendaTotalFactura = total;

                if (DateTime.TryParse(V("FechaEmisionDoc"), out var fechaDoc))
                    document.HaciendaFechaEmisionDoc = DateTime.SpecifyKind(fechaDoc, DateTimeKind.Utc);

                if (DateTime.TryParse(V("FechaRecepcion"), out var fechaRec))
                    document.HaciendaFechaRecepcion = DateTime.SpecifyKind(fechaRec, DateTimeKind.Utc);
            }
            catch { /* No bloquear el flujo si falla el parseo */ }
        }

        /// <summary>Convierte una fecha UTC/local a la hora oficial de Costa Rica (UTC-6).</summary>
        /// <summary>
        /// Determina si una línea es SERVICIO (true) o MERCANCÍA/BIEN (false) a partir
        /// de su código CAByS, según el estándar CAByS-CR del Ministerio de Hacienda.
        /// El primer dígito del código define la categoría:
        ///   • 1-6  → Mercancías / Bienes  (agricultura, minería, manufactura, etc.)
        ///   • 7-9  → Servicios            (comercio, transporte, profesionales, etc.)
        /// Si el CAByS es inválido/ausente se usa el valor recibido como respaldo.
        /// Esta clasificación es la que alimenta TotalServGravados vs
        /// TotalMercanciasGravadas en el ResumenFactura y evita los rechazos
        /// -110/-111 de Hacienda.
        /// </summary>
        private static bool IsServiceByCabys(string? cabys, bool fallback)
        {
            var code = (cabys ?? string.Empty).Trim();
            if (code.Length == 0 || !char.IsDigit(code[0]))
                return fallback;

            // Primer dígito: 7, 8 o 9 = servicio; 1-6 = mercancía.
            return code[0] is '7' or '8' or '9';
        }

        private static string ToCrDateString(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt
                : dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime()
                : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return new DateTimeOffset(utc, TimeSpan.Zero)
                .ToOffset(TimeSpan.FromHours(-6))
                .ToString("yyyy-MM-ddTHH:mm:ss-06:00");
        }

        /// <summary>
        /// Valida las líneas de un documento de reversa (Nota de Crédito o Nota de
        /// Débito) contra el documento referenciado. Reglas aplicadas:
        ///  1. El documento referenciado debe existir en el sistema.
        ///  2. El documento referenciado debe estar ACEPTADO por Hacienda.
        ///  3. Si <paramref name="expectedSourceType"/> tiene valor, el documento
        ///     referenciado debe ser exactamente de ese tipo (p.ej. una ND solo
        ///     puede referenciar una NC).
        ///  4. El emisor de la reversa debe ser el mismo emisor del documento origen.
        ///  5. Cada línea de la reversa debe existir (por CAByS) en el documento origen.
        ///  6. La cantidad a reversar (ESTA reversa + reversas previas aceptadas del
        ///     mismo documento) no puede superar la cantidad original.
        /// Soporta múltiples reversas parciales hasta agotar cada línea.
        /// </summary>
        private async Task ValidateReversalLinesAsync(
            CompanyDbContext db,
            EmitDocumentInput input,
            CustomerBillingCredential issuerCredential,
            string? expectedSourceType,
            Action<string, string, string> log,
            CancellationToken cancellationToken)
        {
            var docLabel = input.DocumentType == EInvoiceDocumentType.NotaDebito
                ? "Nota de Débito" : "Nota de Crédito";
            var sourceLabel = expectedSourceType == EInvoiceDocumentType.NotaCredito
                ? "Nota de Crédito" : "factura";

            var refClave = input.References.FirstOrDefault()?.RefClave?.Trim();
            if (string.IsNullOrWhiteSpace(refClave))
                throw new InvalidOperationException(
                    $"La {docLabel} debe referenciar la clave de la {sourceLabel} a reversar.");

            var sourceDoc = await db.ElectronicDocuments
                .AsNoTracking()
                .Include(d => d.Lines)
                .FirstOrDefaultAsync(d => d.Clave == refClave, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"No se encontró en el sistema el documento referenciado (clave {refClave}). " +
                    $"Una {docLabel} solo puede reversar un documento existente.");

            // 2. El documento origen debe estar ACEPTADO por Hacienda.
            if (sourceDoc.Status != EInvoiceStatus.Aceptado)
                throw new InvalidOperationException(
                    $"Solo se puede emitir una {docLabel} sobre un documento ACEPTADO por Hacienda. " +
                    $"El documento referenciado está en estado '{sourceDoc.Status}'.");

            // 3. Tipo de documento origen esperado (una ND solo referencia una NC).
            if (expectedSourceType != null && sourceDoc.DocumentType != expectedSourceType)
                throw new InvalidOperationException(
                    $"Una {docLabel} solo puede emitirse referenciando una {sourceLabel}. " +
                    $"El documento referenciado es de tipo '{sourceDoc.DocumentType}'.");

            // 4. El emisor de la reversa debe coincidir con el del documento origen.
            var sameById = sourceDoc.IdCustomerIssuer == (issuerCredential.IdCustomer ?? 0);
            var sameByIdentification = !string.IsNullOrWhiteSpace(sourceDoc.EmisorIdentificacionNumero)
                && string.Equals(sourceDoc.EmisorIdentificacionNumero, issuerCredential.Identification,
                    StringComparison.OrdinalIgnoreCase);
            if (!sameById && !sameByIdentification)
                throw new InvalidOperationException(
                    $"El emisor de la {docLabel} debe ser el mismo emisor del documento original " +
                    $"({sourceDoc.EmisorIdentificacionNumero}).");

            if (input.Lines.Count == 0)
                throw new InvalidOperationException(
                    $"La {docLabel} debe reversar al menos una línea del documento de referencia.");

            // Cantidades originales agrupadas por CAByS (un documento puede repetir CAByS).
            var sourceByCabys = sourceDoc.Lines
                .GroupBy(l => l.CabysCode)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

            // 6. Cantidades ya reversadas por documentos previos del MISMO tipo aceptados/pendientes.
            var previousReversals = await db.ElectronicDocuments
                .AsNoTracking()
                .Include(d => d.Lines)
                .Include(d => d.References)
                .Where(d => d.DocumentType == input.DocumentType
                            && d.Status != EInvoiceStatus.Rechazado
                            && d.Status != EInvoiceStatus.Anulado
                            && d.References.Any(r => r.RefClave == refClave))
                .ToListAsync(cancellationToken);

            var alreadyReversedByCabys = previousReversals
                .SelectMany(n => n.Lines)
                .GroupBy(l => l.CabysCode)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

            // Cantidades solicitadas en ESTA reversa.
            var requestedByCabys = new Dictionary<string, decimal>();
            foreach (var line in input.Lines)
            {
                if (!sourceByCabys.TryGetValue(line.CabysCode, out var originalQty))
                    throw new InvalidOperationException(
                        $"La línea con CAByS '{line.CabysCode}' no existe en la {sourceLabel} a reversar. " +
                        "No se pueden agregar líneas que no formen parte del documento original.");

                requestedByCabys.TryGetValue(line.CabysCode, out var acc);
                acc += line.Quantity;
                requestedByCabys[line.CabysCode] = acc;

                alreadyReversedByCabys.TryGetValue(line.CabysCode, out var prev);
                var remaining = originalQty - prev;

                if (acc > remaining)
                    throw new InvalidOperationException(
                        $"La cantidad a reversar del CAByS '{line.CabysCode}' ({acc}) supera lo " +
                        $"disponible en el documento original. Original: {originalQty}, " +
                        $"ya reversado en documentos previos: {prev}, disponible: {remaining}.");
            }

            // ¿Con esta reversa queda completamente reversado el documento?
            var isPartial = sourceByCabys.Any(kv =>
            {
                alreadyReversedByCabys.TryGetValue(kv.Key, out var prev);
                requestedByCabys.TryGetValue(kv.Key, out var now);
                return (prev + now) < kv.Value;
            });

            log("VALIDACION",
                isPartial
                    ? $"{docLabel} PARCIAL validada contra {refClave} " +
                      $"({input.Lines.Count} línea(s); {previousReversals.Count} reversa(s) previa(s))."
                    : $"{docLabel} TOTAL (reversa completa) validada contra {refClave}.",
                "SUCCESS");
        }

        /// <summary>
        /// Valida las líneas de un Recibo Electrónico de Pago (REP) contra la factura a
        /// crédito referenciada: el documento origen debe existir, estar ACEPTADO, ser del
        /// mismo emisor, y las cantidades de pago no pueden superar el saldo pendiente
        /// (cantidad original − REP previos aceptados/pendientes). Funciona igual que la
        /// validación de reversas de N/C, pero acumulando REP en lugar de notas de crédito.
        /// </summary>
        private async Task ValidateReceiptLinesAsync(
            CompanyDbContext db,
            EmitDocumentInput input,
            CustomerBillingCredential issuerCredential,
            Action<string, string, string> log,
            CancellationToken cancellationToken)
        {
            const string docLabel = "Recibo Electrónico de Pago";
            const string sourceLabel = "factura";

            var refClave = input.References.FirstOrDefault()?.RefClave?.Trim();
            if (string.IsNullOrWhiteSpace(refClave))
                throw new InvalidOperationException(
                    $"El {docLabel} debe referenciar la clave de la {sourceLabel} a pagar.");

            var sourceDoc = await db.ElectronicDocuments
                .AsNoTracking()
                .Include(d => d.Lines)
                .FirstOrDefaultAsync(d => d.Clave == refClave, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"No se encontró en el sistema el documento referenciado (clave {refClave}). " +
                    $"Un {docLabel} solo puede documentar el pago de un documento existente.");

            // El documento origen debe estar ACEPTADO por Hacienda.
            if (sourceDoc.Status != EInvoiceStatus.Aceptado)
                throw new InvalidOperationException(
                    $"Solo se puede emitir un {docLabel} sobre un documento ACEPTADO por Hacienda. " +
                    $"El documento referenciado está en estado '{sourceDoc.Status}'.");

            // El emisor del REP debe coincidir con el del documento origen.
            var sameById = sourceDoc.IdCustomerIssuer == (issuerCredential.IdCustomer ?? 0);
            var sameByIdentification = !string.IsNullOrWhiteSpace(sourceDoc.EmisorIdentificacionNumero)
                && string.Equals(sourceDoc.EmisorIdentificacionNumero, issuerCredential.Identification,
                    StringComparison.OrdinalIgnoreCase);
            if (!sameById && !sameByIdentification)
                throw new InvalidOperationException(
                    $"El emisor del {docLabel} debe ser el mismo emisor del documento original " +
                    $"({sourceDoc.EmisorIdentificacionNumero}).");

            if (input.Lines.Count == 0)
                throw new InvalidOperationException(
                    $"El {docLabel} debe documentar el pago de al menos una línea del documento de referencia.");

            // Cantidades originales agrupadas por CAByS (un documento puede repetir CAByS).
            var sourceByCabys = sourceDoc.Lines
                .GroupBy(l => l.CabysCode)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

            // Cantidades ya pagadas por REP previos aceptados/pendientes.
            var previousReceipts = await db.ElectronicDocuments
                .AsNoTracking()
                .Include(d => d.Lines)
                .Include(d => d.References)
                .Where(d => d.DocumentType == EInvoiceDocumentType.ReciboElectronicoPago
                            && d.Status != EInvoiceStatus.Rechazado
                            && d.Status != EInvoiceStatus.Anulado
                            && d.References.Any(r => r.RefClave == refClave))
                .ToListAsync(cancellationToken);

            var alreadyPaidByCabys = previousReceipts
                .SelectMany(n => n.Lines)
                .GroupBy(l => l.CabysCode)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

            // Cantidades solicitadas en ESTE recibo.
            var requestedByCabys = new Dictionary<string, decimal>();
            foreach (var line in input.Lines)
            {
                if (!sourceByCabys.TryGetValue(line.CabysCode, out var originalQty))
                    throw new InvalidOperationException(
                        $"La línea con CAByS '{line.CabysCode}' no existe en la {sourceLabel} a pagar. " +
                        "No se pueden agregar líneas que no formen parte del documento original.");

                requestedByCabys.TryGetValue(line.CabysCode, out var acc);
                acc += line.Quantity;
                requestedByCabys[line.CabysCode] = acc;

                alreadyPaidByCabys.TryGetValue(line.CabysCode, out var prev);
                var remaining = originalQty - prev;

                if (acc > remaining)
                    throw new InvalidOperationException(
                        $"La cantidad a pagar del CAByS '{line.CabysCode}' ({acc}) supera el saldo " +
                        $"pendiente en la factura original. Original: {originalQty}, " +
                        $"ya pagado en REP previos: {prev}, disponible: {remaining}.");
            }

            // ¿Con este recibo queda completamente pagada la factura?
            var isPartial = sourceByCabys.Any(kv =>
            {
                alreadyPaidByCabys.TryGetValue(kv.Key, out var prev);
                requestedByCabys.TryGetValue(kv.Key, out var now);
                return (prev + now) < kv.Value;
            });

            log("VALIDACION",
                isPartial
                    ? $"{docLabel} PARCIAL validado contra {refClave} " +
                      $"({input.Lines.Count} línea(s); {previousReceipts.Count} REP previo(s))."
                    : $"{docLabel} TOTAL (pago completo) validado contra {refClave}.",
                "SUCCESS");
        }

        private static void ValidateBusinessRules(EmitDocumentInput input)
        {
            foreach (var line in input.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.CabysCode) || line.CabysCode.Length != 13)
                    throw new InvalidOperationException(
                        $"La línea '{line.Detail}' requiere un código CAByS válido de 13 dígitos.");
                if (line.DiscountAmount > 0 && string.IsNullOrWhiteSpace(line.DiscountNature))
                    throw new InvalidOperationException(
                        $"La línea '{line.Detail}' tiene descuento pero falta la naturaleza del descuento (v4.4).");

                // Hacienda v4.4: los códigos de descuento 01 (Regalía) y 03 (Bonificación)
                // exigen que el MontoDescuento sea el 100% del MontoTotal y un tratamiento
                // especial de ImpuestoAsumidoEmisorFabrica (errores -518/-476). No se
                // soportan como descuento parcial: se rechaza aquí.
                if (line.DiscountNature is "01" or "03")
                    throw new InvalidOperationException(
                        $"La línea '{line.Detail}' usa un código de descuento de Regalía/Bonificación (01/03), " +
                        "no soportado. Use un descuento comercial (04 Volumen, 05 Temporada o 06 Promoción).");
            }

            // NC/ND/REP requieren referencia obligatoria.
            var requiresReference = input.DocumentType is
                EInvoiceDocumentType.NotaCredito or
                EInvoiceDocumentType.NotaDebito or
                EInvoiceDocumentType.ReciboElectronicoPago;

            if (requiresReference && input.References.Count == 0)
                throw new InvalidOperationException(
                    "Nota de Crédito/Débito y REP requieren referencia (InformacionReferencia) al documento previo.");

            // Venta a crédito ("02") exige PlazoCredito; sin él Hacienda rechaza con
            // error -58 ("El campo 'Plazo del crédito' no posee la estructura establecida").
            if (input.SaleCondition == "02" && (!input.CreditTerm.HasValue || input.CreditTerm.Value < 1))
                throw new InvalidOperationException(
                    "La condición de venta a crédito requiere un plazo de crédito (PlazoCredito) mayor a 0.");
        }
    }
}
