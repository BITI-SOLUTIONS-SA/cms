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
using CMS.Shared.Validation;
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
        private readonly AppDbContext _centralDb;
        private readonly IClaveNumericaGenerator _claveGenerator;
        private readonly IElectronicDocumentXmlBuilder _xmlBuilder;
        private readonly IXadesSignatureService _signatureService;
        private readonly IHaciendaAuthService _authService;
        private readonly IHaciendaApiClient _apiClient;
        private readonly IEInvoicePdfService _pdfService;
        private readonly IElectronicDocumentTypeCatalogService _typeCatalog;
        private readonly ILogger<ElectronicDocumentService> _logger;

        public ElectronicDocumentService(
            ICompanyDbContextFactory companyDbContextFactory,
            AppDbContext centralDb,
            IClaveNumericaGenerator claveGenerator,
            IElectronicDocumentXmlBuilder xmlBuilder,
            IXadesSignatureService signatureService,
            IHaciendaAuthService authService,
            IHaciendaApiClient apiClient,
            IEInvoicePdfService pdfService,
            IElectronicDocumentTypeCatalogService typeCatalog,
            ILogger<ElectronicDocumentService> logger)
        {
            _companyDbContextFactory = companyDbContextFactory;
            _centralDb = centralDb;
            _claveGenerator = claveGenerator;
            _xmlBuilder = xmlBuilder;
            _signatureService = signatureService;
            _authService = authService;
            _apiClient = apiClient;
            _pdfService = pdfService;
            _typeCatalog = typeCatalog;
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

            // Actividad económica del comprobante. La fuente de verdad es la tabla
            // {schema}.customer_economic_activity (ya NO se guarda en la credencial).
            //  - Si el usuario seleccionó una actividad en la pantalla de emisión, prevalece.
            //  - Si no, se toma la actividad predeterminada (is_default) activa del cliente.
            // El valor se coloca en la propiedad NO mapeada EconomicActivity únicamente como
            // portador en memoria para el generador de XML (no se persiste en la credencial).
            issuerCredential.EconomicActivity = !string.IsNullOrWhiteSpace(input.IssuerEconomicActivity)
                ? input.IssuerEconomicActivity.Trim()
                : await ResolveDefaultActivityCodeAsync(db, issuerCredential.IdCustomer, cancellationToken);
            Log("EMISOR", $"Actividad económica del emisor: {issuerCredential.EconomicActivity ?? "(ninguna)"}.");

            if (receptorCredential != null)
            {
                receptorCredential.EconomicActivity = !string.IsNullOrWhiteSpace(input.ReceptorEconomicActivity)
                    ? input.ReceptorEconomicActivity.Trim()
                    : await ResolveDefaultActivityCodeAsync(db, receptorCredential.IdCustomer, cancellationToken);
                Log("RECEPTOR", $"Actividad económica del receptor: {receptorCredential.EconomicActivity ?? "(ninguna)"}.");
            }

            // Metadatos/banderas del tipo de documento desde el catálogo central
            // (admin.electronic_document_type). Gobierna la generación del XML y las
            // validaciones parametrizables (p.ej. referencia obligatoria de FEC/FEE).
            var typeMeta = await _typeCatalog.GetByCodeAsync(input.DocumentType, cancellationToken);

            // Validación del número de identificación del emisor y receptor según su
            // tipo (Hacienda CR v4.4). Evita emitir comprobantes con identificaciones mal formadas.
            if (!IdentificationNumberValidator.TryValidate(
                    issuerCredential.IdentificationType, issuerCredential.Identification, out var issuerIdError))
                throw new InvalidOperationException($"Identificación del emisor inválida. {issuerIdError}");

            if (receptorCredential != null
                && !string.IsNullOrWhiteSpace(receptorCredential.Identification)
                && !IdentificationNumberValidator.TryValidate(
                    receptorCredential.IdentificationType, receptorCredential.Identification, out var receptorIdError))
                throw new InvalidOperationException($"Identificación del receptor inválida. {receptorIdError}");

            Log("VALIDACION", "Números de identificación de emisor/receptor validados.", "SUCCESS");

            // Validaciones fiscales.
            var cabysDiscountRules = await GetCabysDiscountRulesAsync(input, cancellationToken);
            ValidateBusinessRules(input, typeMeta, cabysDiscountRules);
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
                input.Branch, input.Terminal, EInvoiceSituation.Normal, crLocalDate, input.UserId, input.ConsecutiveId);
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
                PaymentMethods = string.IsNullOrWhiteSpace(input.PaymentMethods) ? input.PaymentMethod : input.PaymentMethods,
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
                EmisorCorreo                = !string.IsNullOrWhiteSpace(input.IssuerEmailOverride)
                                                ? input.IssuerEmailOverride.Trim()
                                                : issuerCredential.Email,
                EmisorUbicacionProvincia    = issuerCredential.Province,
                EmisorUbicacionCanton       = issuerCredential.Canton,
                EmisorUbicacionDistrito     = issuerCredential.District,
                EmisorUbicacionOtrasSenas   = issuerCredential.OtherSigns,
                EmisorTelefonoCodigoPais    = issuerCredential.PhoneCode,
                EmisorTelefonoNumero        = !string.IsNullOrWhiteSpace(input.IssuerPhoneOverride)
                                                ? input.IssuerPhoneOverride.Trim()
                                                : issuerCredential.Phone,
                CodigoActividadEmisor       = issuerCredential.EconomicActivity,
                ProveedorSistemas           = "2100042005", // Proveedor BSFLOW registrado en Hacienda

                // ── Datos del Receptor (si aplica) ────────────────────────────
                ReceptorNombre                    = receptorCredential?.Name,
                ReceptorNombreComercial           = receptorCredential?.CommercialName,
                ReceptorIdentificacionTipo        = receptorCredential?.IdentificationType,
                ReceptorIdentificacionNumero      = receptorCredential?.Identification,
                ReceptorIdentificacionExtranjero  = receptorCredential?.ForeignIdentification,
                ReceptorCorreo                    = !string.IsNullOrWhiteSpace(input.ReceptorEmailOverride)
                                                        ? input.ReceptorEmailOverride.Trim()
                                                        : receptorCredential?.Email,
                ReceptorUbicacionProvincia        = receptorCredential?.Province,
                ReceptorUbicacionCanton           = receptorCredential?.Canton,
                ReceptorUbicacionDistrito         = receptorCredential?.District,
                ReceptorUbicacionOtrasSenas       = receptorCredential?.OtherSigns,
                ReceptorTelefonoCodigoPais        = receptorCredential?.PhoneCode,
                ReceptorTelefonoNumero            = !string.IsNullOrWhiteSpace(input.ReceptorPhoneOverride)
                                                        ? input.ReceptorPhoneOverride.Trim()
                                                        : receptorCredential?.Phone,
            };

            var taxesByLine = new Dictionary<int, List<ElectronicDocumentTax>>();
            var lineNumber = 0;
            foreach (var li in input.Lines)
            {
                lineNumber++;

                // ── Descuentos múltiples (Hacienda v4.4 admite hasta 5) ──────────
                // Se normaliza la lista: solo entradas con monto > 0 y naturaleza.
                // Los escalares DiscountAmount/DiscountNature se derivan de la lista
                // (suma total y naturaleza principal) para mantener compatibilidad
                // con el cálculo, totales, PDF y la ruta REP.
                var normalizedDiscounts = (li.Discounts ?? new())
                    .Where(d => d.Amount > 0)
                    .Select(d => new EmitLineDiscountInput
                    {
                        Nature = string.IsNullOrWhiteSpace(d.Nature) ? EInvoiceDiscountNature.Promocion : d.Nature!.Trim(),
                        Amount = d.Amount
                    })
                    .Take(5)
                    .ToList();
                if (normalizedDiscounts.Count > 0)
                {
                    li.DiscountAmount = normalizedDiscounts.Sum(d => d.Amount);
                    li.DiscountNature = normalizedDiscounts[0].Nature;
                }

                // ── Impuestos de la línea (Hacienda v4.4 admite varios) ──────────
                // Si el UI envía la colección Taxes, cada elemento es un impuesto
                // independiente (IVA, selectivo de consumo, etc.) con su propia
                // exoneración. Si viene vacía, se reconstruye un único impuesto a
                // partir de los campos planos de la línea (retrocompatibilidad).
                bool docExon = input.IsExonerated;
                var normalizedTaxes = (li.Taxes != null && li.Taxes.Count > 0)
                    ? li.Taxes.Select(t => new EmitLineTaxInput
                    {
                        IdElectronicDocumentTaxType = t.IdElectronicDocumentTaxType > 0 ? t.IdElectronicDocumentTaxType : 1,
                        TaxCode = string.IsNullOrWhiteSpace(t.TaxCode) ? "01" : t.TaxCode.Trim(),
                        TaxRatePercent = t.TaxRatePercent,
                        TaxRateCode = t.TaxRateCode,
                        UnitMeasureQty = t.UnitMeasureQty,
                        VolumeUnit = t.VolumeUnit,
                        SpecPercent = t.SpecPercent,
                        Proportion = t.Proportion,
                        PerUnitTax = t.PerUnitTax,
                        SpecialTaxableBase = t.SpecialTaxableBase,
                        IsFactoryTax = t.IsFactoryTax,
                        TaxAmount = t.TaxAmount,
                        TaxDescription = t.TaxDescription,
                        IsExonerated = docExon || t.IsExonerated,
                        ExonDocumentType = t.ExonDocumentType,
                        ExonDocumentNumber = t.ExonDocumentNumber,
                        ExonInstitution = t.ExonInstitution,
                        ExonDate = t.ExonDate,
                        ExonArticle = t.ExonArticle,
                        ExonSubsection = t.ExonSubsection,
                        ExonPercent = t.ExonPercent
                    }).ToList()
                    : new List<EmitLineTaxInput>
                    {
                        new EmitLineTaxInput
                        {
                            IdElectronicDocumentTaxType = li.IdElectronicDocumentTaxType > 0 ? li.IdElectronicDocumentTaxType : 1,
                            TaxCode = "01",
                            TaxRatePercent = li.TaxRatePercent,
                            TaxRateCode = li.TaxRateCode,
                            IsExonerated = docExon || li.IsExonerated,
                            ExonDocumentType = li.ExonDocumentType,
                            ExonDocumentNumber = li.ExonDocumentNumber,
                            ExonInstitution = li.ExonInstitution,
                            ExonDate = li.ExonDate,
                            ExonArticle = li.ExonArticle,
                            ExonSubsection = li.ExonSubsection,
                            ExonPercent = li.ExonPercent
                        }
                    };

                // Base imponible compartida: el descuento y el I.V.I. se calculan con
                // la tarifa combinada de todos los impuestos de la línea.
                decimal combinedRate = normalizedTaxes.Sum(t => t.TaxRatePercent);
                var bd = EInvoiceCalculator.BreakdownLine(
                    li.UnitPrice, li.Quantity, combinedRate, li.DiscountAmount, li.PriceIncludesTax);

                // Impuesto principal (primer IVA o, en su defecto, el primer impuesto)
                // usado para los campos escalares de desglose de la línea.
                var primaryTax = normalizedTaxes.FirstOrDefault(t => t.TaxCode == "01") ?? normalizedTaxes[0];

                // Cálculo por impuesto: cada impuesto grava la misma BaseImponible
                // con su propia tarifa y aplica su exoneración de forma independiente.
                var taxEntities = new List<ElectronicDocumentTax>();
                decimal lineTaxTotal = 0m;
                decimal lineExonTotal = 0m;
                foreach (var t in normalizedTaxes)
                {
                    // ── Cálculo del monto del impuesto según el código de Hacienda ──
                    // Por defecto (01, 02, 07 y otros ad valorem): BaseImponible × Tarifa%.
                    // Los códigos específicos (03, 04, 05, 06) se calculan por unidad
                    // física digitada por el usuario y NO por tarifa porcentual.
                    decimal taxAmount;
                    switch (t.TaxCode)
                    {
                        case "06": // Productos de tabaco: Cantidad × CantidadUnidadMedida × ImpuestoPorUnidad
                            taxAmount = Math.Round(
                                li.Quantity * (t.UnitMeasureQty ?? 0m) * (t.PerUnitTax ?? 0m),
                                5, MidpointRounding.AwayFromZero);
                            break;
                        case "03": // Combustibles: CantidadUnidadMedida × ImpuestoPorUnidad
                            taxAmount = Math.Round(
                                (t.UnitMeasureQty ?? 0m) * (t.PerUnitTax ?? 0m),
                                5, MidpointRounding.AwayFromZero);
                            break;
                        case "04": // Bebidas alcohólicas: Proporción × ImpuestoPorUnidad
                            taxAmount = Math.Round(
                                (t.Proportion ?? ((t.UnitMeasureQty ?? 0m) * (t.SpecPercent ?? 0m) / 100m)) * (t.PerUnitTax ?? 0m),
                                5, MidpointRounding.AwayFromZero);
                            break;
                        case "05": // Bebidas envasadas sin alcohol: Volumen × ImpuestoPorUnidad
                            taxAmount = Math.Round(
                                (t.VolumeUnit ?? 0m) * (t.PerUnitTax ?? 0m),
                                5, MidpointRounding.AwayFromZero);
                            break;
                        case "02": // IVA (selectivo por monto): monto ya calculado por la UI si viene
                            taxAmount = t.TaxAmount.HasValue && t.TaxAmount.Value > 0
                                ? Math.Round(t.TaxAmount.Value, 5, MidpointRounding.AwayFromZero)
                                : Math.Round(bd.TaxableBase * t.TaxRatePercent / 100m, 5, MidpointRounding.AwayFromZero);
                            break;
                        default:   // 01, 07 y demás ad valorem: BaseImponible × Tarifa%
                            taxAmount = Math.Round(bd.TaxableBase * t.TaxRatePercent / 100m, 5, MidpointRounding.AwayFromZero);
                            break;
                    }
                    // Proporción persistida del código 04 (si no vino calculada).
                    decimal? proportion = t.Proportion
                        ?? (t.TaxCode == "04" ? (decimal?)((t.UnitMeasureQty ?? 0m) * (t.SpecPercent ?? 0m) / 100m) : null);
                    // Hacienda v4.4 (rechazos -190/-290): MontoExoneracion es el IMPUESTO
                    // exonerado. Debe cumplir simultáneamente:
                    //   -190: MontoExoneracion = (TarifaExonerada/100) × SubTotal
                    //   -290: ImpuestoNeto     = Monto − MontoExoneracion
                    decimal tExonPercent = t.IsExonerated
                        ? (t.ExonPercent > 0 ? Math.Min(t.ExonPercent, 100m) : 100m)
                        : 0m;
                    decimal tExonAmount = Math.Round(taxAmount * tExonPercent / 100m, 5, MidpointRounding.AwayFromZero);

                    lineTaxTotal += taxAmount;
                    lineExonTotal += tExonAmount;

                    taxEntities.Add(new ElectronicDocumentTax
                    {
                        TaxCode = t.TaxCode,
                        TaxRateCode = t.TaxRateCode,
                        TaxRate = t.TaxRatePercent,
                        TaxAmount = taxAmount,
                        // ── Datos físicos de impuestos específicos / cálculo especial ─
                        UnitMeasureQty     = t.UnitMeasureQty,
                        VolumeUnit         = t.VolumeUnit,
                        SpecPercent        = t.SpecPercent,
                        Proportion         = proportion,
                        PerUnitTax         = t.PerUnitTax,
                        SpecialTaxableBase = t.SpecialTaxableBase,
                        IsFactoryTax       = t.IsFactoryTax,
                        // ── Exoneración por impuesto ─────────────────
                        IsExonerated       = t.IsExonerated,
                        ExonPercent        = tExonPercent,
                        ExonAmount         = tExonAmount,
                        ExonDocumentType   = t.IsExonerated ? (t.ExonDocumentType ?? "99") : null,
                        ExonDocumentNumber = t.IsExonerated ? t.ExonDocumentNumber : null,
                        ExonInstitution    = t.IsExonerated ? t.ExonInstitution : null,
                        ExonDate           = t.IsExonerated ? (t.ExonDate ?? DateTime.UtcNow) : null,
                        ExonArticle        = t.IsExonerated ? t.ExonArticle : null,
                        ExonSubsection     = t.IsExonerated ? t.ExonSubsection : null,
                        CreateDate = DateTime.UtcNow,
                        RecordDate = DateTime.UtcNow,
                        CreatedBy = input.UserId.ToString(),
                        UpdatedBy = input.UserId.ToString()
                    });
                }

                bool lineExonerated = taxEntities.Any(t => t.IsExonerated && t.ExonAmount > 0);
                // Exoneración representativa de la línea = suma de exoneraciones.
                var repExonTax = taxEntities.FirstOrDefault(t => t.IsExonerated && t.ExonAmount > 0);
                decimal impuestoNeto = lineTaxTotal - lineExonTotal;   // impuesto efectivamente cobrado
                decimal totalLineNet = bd.TaxableBase + lineTaxTotal - lineExonTotal;

                var line = new ElectronicDocumentLine
                {
                    LineNumber = lineNumber,
                    IdItem = li.ItemId,
                    CabysCode = li.CabysCode,
                    ItemCode = li.ItemCode,
                    IdElectronicDocumentTaxType = primaryTax.IdElectronicDocumentTaxType,
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
                    Discounts = normalizedDiscounts.Count > 0
                        ? System.Text.Json.JsonSerializer.Serialize(
                            normalizedDiscounts.Select(d => new { nature = d.Nature, amount = d.Amount }))
                        : null,
                    // Hacienda v4.4: SubTotal = MontoTotal − MontoDescuento (neto tras descuento).
                    // Poner el bruto aquí provoca los rechazos -44/-454/-46. Como no hay
                    // impuesto selectivo de consumo, SubTotal == BaseImponible (bd.TaxableBase).
                    SubTotal = bd.UnitPriceBase * li.Quantity - li.DiscountAmount,
                    TaxableBase = bd.TaxableBase,
                    TotalTax = lineTaxTotal,
                    TotalLine = totalLineNet,
                    // ── Campos fiscales adicionales ──────────────────
                    ImpuestoAsumidoEmisor = 0,                        // Se actualiza si aplica emisor/fábrica
                    ImpuestoNeto          = impuestoNeto,            // impuestos - exoneración
                    MontoTotalLinea       = totalLineNet,           // total con impuesto neto
                    TaxRateCodeIva        = primaryTax.TaxRateCode,
                    TaxRateIva            = primaryTax.TaxRatePercent / 100m, // porcentaje → decimal (0.13)
                    MontoTaxIva           = primaryTax.TaxRatePercent > 0
                                             ? Math.Round(bd.TaxableBase * primaryTax.TaxRatePercent / 100m, 5, MidpointRounding.AwayFromZero)
                                             : 0m,
                    // ── Exoneración representativa de la línea ────────
                    IsExonerated       = lineExonerated,
                    ExonPercent        = repExonTax?.ExonPercent ?? 0m,
                    ExonAmount         = lineExonTotal,   // MontoExoneracion total de la línea
                    ExonDocumentType   = repExonTax?.ExonDocumentType,
                    ExonDocumentNumber = repExonTax?.ExonDocumentNumber,
                    ExonInstitution    = repExonTax?.ExonInstitution,
                    ExonDate           = repExonTax?.ExonDate,
                    ExonArticle        = repExonTax?.ExonArticle,
                    ExonSubsection     = repExonTax?.ExonSubsection,
                    CreateDate = DateTime.UtcNow,
                    RecordDate = DateTime.UtcNow,
                    CreatedBy = input.UserId.ToString(),
                    UpdatedBy = input.UserId.ToString()
                };

                foreach (var te in taxEntities)
                    line.Taxes.Add(te);

                // ── Descuentos individuales de la línea (una fila por descuento) ──
                // Persistimos cada descuento en electronic_document_discount_line para
                // trazabilidad/auditoría; el JSON y el escalar se conservan como resumen.
                int discSeq = 1;
                foreach (var d in normalizedDiscounts)
                {
                    if (d.Amount <= 0) continue;
                    line.DiscountLines.Add(new ElectronicDocumentDiscountLine
                    {
                        Sequence = discSeq++,
                        DiscountAmount = Math.Round(d.Amount, 5, MidpointRounding.AwayFromZero),
                        DiscountNatureCode = d.Nature,
                        DiscountNature = ResolveDiscountNatureName(d.Nature),
                        CreateDate = DateTime.UtcNow,
                        RecordDate = DateTime.UtcNow,
                        CreatedBy = input.UserId.ToString(),
                        UpdatedBy = input.UserId.ToString()
                    });
                }

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

            // Otros cargos (OtrosCargos v4.4): se persisten como JSON en el documento
            // para reconstruir el nodo <OtrosCargos> del XML, y ademas se guardan como
            // filas en la tabla operacional electronic_document_other_charges_line.
            if (input.OtherCharges.Count > 0)
            {
                document.OtherCharges = System.Text.Json.JsonSerializer.Serialize(
                    input.OtherCharges.Select(o => new
                    {
                        typeCode = o.TypeCode,
                        otherTypeDescription = o.OtherTypeDescription,
                        detail = o.Detail,
                        amount = o.Amount,
                        thirdIdentType = o.ThirdIdentType,
                        thirdIdentNumber = o.ThirdIdentNumber,
                        thirdName = o.ThirdName
                    }));

                var ocSeq = 0;
                foreach (var o in input.OtherCharges)
                {
                    ocSeq++;
                    document.OtherChargeLines.Add(new ElectronicDocumentOtherChargeLine
                    {
                        Sequence = ocSeq,
                        TypeCode = o.TypeCode,
                        OtherTypeDescription = o.OtherTypeDescription,
                        Detail = o.Detail,
                        Amount = o.Amount,
                        Percent = o.Percent,
                        ThirdIdentType = o.ThirdIdentType,
                        ThirdIdentNumber = o.ThirdIdentNumber,
                        ThirdName = o.ThirdName
                    });
                }
            }

            db.ElectronicDocuments.Add(document);
            await db.SaveChangesAsync(cancellationToken);
            Log("PERSISTENCIA", $"Documento persistido (ID {document.Id}, consecutivo {document.Consecutive}).", "SUCCESS");

            // Mapear impuestos por Id de línea (ya persistido).
            foreach (var line in document.Lines)
                taxesByLine[line.Id] = line.Taxes.ToList();

            // 3. Construir y firmar el XML. (typeMeta ya resuelto arriba desde el catálogo)
            var unsignedXml = _xmlBuilder.BuildXml(
                document, issuerCredential, receptorCredential, document.Lines.ToList(), taxesByLine, document.References.ToList(), typeMeta);
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

        /// <summary>
        /// Resuelve el código de actividad económica predeterminado (is_default) y activo de un
        /// cliente desde {schema}.customer_economic_activity. Si el cliente no tiene una marcada
        /// como predeterminada, toma la primera activa. Devuelve null si no hay ninguna.
        /// Esta es la ÚNICA fuente de la actividad económica (ya no se guarda en la credencial).
        /// </summary>
        private async Task<string?> ResolveDefaultActivityCodeAsync(
            CompanyDbContext db, int? customerId, CancellationToken ct)
        {
            if (customerId == null || customerId == 0)
                return null;

            var activityId = await db.CustomerEconomicActivities.AsNoTracking()
                .Where(a => a.IdCustomer == customerId.Value && a.IsActive)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.IdElectronicDocumentEconomicActivity)
                .Select(a => a.IdElectronicDocumentEconomicActivity)
                .FirstOrDefaultAsync(ct);

            if (activityId == 0)
                return null;

            // Resolver el CodigoActividad desde el catálogo central (cross-DB) por id.
            return await _centralDb.ElectronicDocumentEconomicActivities.AsNoTracking()
                .Where(x => x.Id == activityId)
                .Select(x => x.Code)
                .FirstOrDefaultAsync(ct);
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

        /// <summary>Devuelve el nombre legible de la naturaleza de descuento a partir de su código.</summary>
        private static string ResolveDiscountNatureName(string? code) => (code ?? string.Empty).Trim() switch
        {
            EInvoiceDiscountNature.Regalia => "Regalía",
            EInvoiceDiscountNature.Volumen => "Descuento por volumen",
            EInvoiceDiscountNature.Temporada => "Descuento por temporada",
            EInvoiceDiscountNature.Promocion => "Promoción",
            _ => "Promoción"
        };

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

        // Obtiene las naturalezas de descuento permitidas por CAByS desde la BD central
        // (admin.electronic_document_cabys_discount). Devuelve un diccionario
        // CAByS(13 díg) -> conjunto de códigos de naturaleza permitidos. Si un CAByS no
        // tiene reglas configuradas, NO aparece en el diccionario y se aplican las reglas
        // por defecto (rechazo de 01/03).
        private async Task<Dictionary<string, HashSet<string>>> GetCabysDiscountRulesAsync(
            EmitDocumentInput input, CancellationToken cancellationToken)
        {
            var codes = (input.Lines ?? new())
                .Select(l => l.CabysCode?.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (codes.Count == 0)
                return result;

            var rows = await _centralDb.ElectronicDocumentCabysDiscounts.AsNoTracking()
                .Where(r => r.IsActive && r.Cabys != null && r.Discount != null
                            && codes.Contains(r.Cabys.Code))
                .Select(r => new { Cabys = r.Cabys!.Code, Nature = r.Discount!.Code })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (!result.TryGetValue(row.Cabys, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    result[row.Cabys] = set;
                }
                set.Add(row.Nature);
            }
            return result;
        }

        private static void ValidateBusinessRules(EmitDocumentInput input,
            ElectronicDocumentTypeCatalog? typeMeta = null,
            Dictionary<string, HashSet<string>>? cabysDiscountRules = null)
        {
            foreach (var line in input.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.CabysCode) || line.CabysCode.Length != 13)
                    throw new InvalidOperationException(
                        $"La línea '{line.Detail}' requiere un código CAByS válido de 13 dígitos.");

                // ── Validación de descuentos múltiples ──────────────────────────
                HashSet<string>? allowedNatures = null;
                cabysDiscountRules?.TryGetValue(line.CabysCode.Trim(), out allowedNatures);

                var discountEntries = (line.Discounts ?? new())
                    .Where(d => d.Amount != 0 || !string.IsNullOrWhiteSpace(d.Nature))
                    .ToList();
                if (discountEntries.Count > 0)
                {
                    if (discountEntries.Count > 5)
                        throw new InvalidOperationException(
                            $"La línea '{line.Detail}' excede el máximo de 5 descuentos permitidos (v4.4).");
                    foreach (var d in discountEntries)
                    {
                        if (d.Amount <= 0)
                            throw new InvalidOperationException(
                                $"La línea '{line.Detail}' tiene un descuento con monto menor o igual a 0. El monto debe ser mayor a 0.");
                        if (string.IsNullOrWhiteSpace(d.Nature))
                            throw new InvalidOperationException(
                                $"La línea '{line.Detail}' tiene un descuento sin código/naturaleza seleccionado (obligatorio).");
                        if (allowedNatures != null)
                        {
                            if (!allowedNatures.Contains(d.Nature!.Trim()))
                                throw new InvalidOperationException(
                                    $"La línea '{line.Detail}' usa un código de descuento ({d.Nature}) no permitido " +
                                    $"para el CAByS {line.CabysCode}. Seleccione un descuento válido para ese código.");
                        }
                        else if (d.Nature is "01" or "03")
                            throw new InvalidOperationException(
                                $"La línea '{line.Detail}' usa un código de descuento de Regalía/Bonificación (01/03), " +
                                "no soportado. Use un descuento comercial (04 Volumen, 05 Temporada o 06 Promoción).");
                    }
                    // El total de descuentos no puede superar el monto bruto de la línea.
                    decimal gross = line.UnitPrice * line.Quantity;
                    decimal totalDisc = discountEntries.Sum(d => d.Amount);
                    if (totalDisc > gross)
                        throw new InvalidOperationException(
                            $"La línea '{line.Detail}' tiene descuentos ({totalDisc:0.00}) que superan el monto total de la línea ({gross:0.00}).");
                }

                if (line.DiscountAmount > 0 && string.IsNullOrWhiteSpace(line.DiscountNature))
                    throw new InvalidOperationException(
                        $"La línea '{line.Detail}' tiene descuento pero falta la naturaleza del descuento (v4.4).");

                // Naturaleza escalar (compatibilidad): validar contra reglas CAByS si existen,
                // de lo contrario aplicar el rechazo por defecto de 01/03.
                if (!string.IsNullOrWhiteSpace(line.DiscountNature))
                {
                    if (allowedNatures != null)
                    {
                        if (!allowedNatures.Contains(line.DiscountNature!.Trim()))
                            throw new InvalidOperationException(
                                $"La línea '{line.Detail}' usa un código de descuento ({line.DiscountNature}) no permitido " +
                                $"para el CAByS {line.CabysCode}. Seleccione un descuento válido para ese código.");
                    }
                    // Hacienda v4.4: los códigos de descuento 01 (Regalía) y 03 (Bonificación)
                    // exigen que el MontoDescuento sea el 100% del MontoTotal y un tratamiento
                    // especial de ImpuestoAsumidoEmisorFabrica (errores -518/-476). No se
                    // soportan como descuento parcial: se rechaza cuando el CAByS no tiene reglas.
                    else if (line.DiscountNature is "01" or "03")
                        throw new InvalidOperationException(
                            $"La línea '{line.Detail}' usa un código de descuento de Regalía/Bonificación (01/03), " +
                            "no soportado. Use un descuento comercial (04 Volumen, 05 Temporada o 06 Promoción).");
                }
            }

            // Referencia obligatoria: se resuelve desde el catálogo cuando está disponible
            // (requires_reference). Fallback histórico: NC/ND/REP y FEC/FEE.
            var requiresReference = typeMeta?.RequiresReference
                ?? input.DocumentType is
                    EInvoiceDocumentType.NotaCredito or
                    EInvoiceDocumentType.NotaDebito or
                    EInvoiceDocumentType.ReciboElectronicoPago or
                    EInvoiceDocumentType.FacturaCompra;

            if (requiresReference && input.References.Count == 0)
                throw new InvalidOperationException(
                    "Este tipo de comprobante requiere al menos una referencia (InformacionReferencia) al documento relacionado.");

            // Venta a crédito ("02") exige PlazoCredito; sin él Hacienda rechaza con
            // error -58 ("El campo 'Plazo del crédito' no posee la estructura establecida").
            if (input.SaleCondition == "02" && (!input.CreditTerm.HasValue || input.CreditTerm.Value < 1))
                throw new InvalidOperationException(
                    "La condición de venta a crédito requiere un plazo de crédito (PlazoCredito) mayor a 0.");
        }
    }
}
