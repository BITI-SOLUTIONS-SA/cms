// ================================================================================
// ARCHIVO: CMS.API/Controllers/ElectronicInvoiceController.cs
// PROPÓSITO: API REST para emisión y consulta de comprobantes electrónicos CR v4.4
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Security.Claims;
using System.Xml.Linq;
using CMS.Application.DTOs.EInvoice;
using CMS.Data.Services;
using CMS.Data.Services.EInvoice;
using CMS.Entities.EInvoice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ElectronicInvoiceController : ControllerBase
    {
        private readonly IElectronicDocumentService _documentService;
        private readonly ICompanyDbContextFactory _factory;
        private readonly IEInvoicePdfService _pdfService;
        private readonly ILogger<ElectronicInvoiceController> _logger;

        public ElectronicInvoiceController(
            IElectronicDocumentService documentService,
            ICompanyDbContextFactory factory,
            IEInvoicePdfService pdfService,
            ILogger<ElectronicInvoiceController> logger)
        {
            _documentService = documentService;
            _factory = factory;
            _pdfService = pdfService;
            _logger = logger;
        }

        private int GetCompanyId()
        {
            var claim = User.FindFirst("companyId")?.Value ?? User.FindFirst("CompanyId")?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out var id))
                throw new UnauthorizedAccessException("CompanyId no encontrado en el token");
            return id;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        /// <summary>Emite un comprobante electrónico (FE/NC/ND/TE/FEC/REP).</summary>
        [HttpPost("emit")]
        public async Task<ActionResult<EmitResultDto>> Emit([FromBody] EmitDocumentDto dto)
        {
            try
            {
                var input = new EmitDocumentInput
                {
                    CompanyId = GetCompanyId(),
                    UserId = GetUserId(),
                    IssuerId = dto.IssuerId,
                    ReceptorId = dto.ReceptorId,
                    DocumentType = dto.DocumentType,
                    SaleCondition = dto.SaleCondition,
                    CreditTerm = dto.CreditTerm,
                    PaymentMethod = dto.PaymentMethod,
                    Currency = dto.Currency,
                    ExchangeRate = dto.ExchangeRate,
                    Branch = dto.Branch,
                    Terminal = dto.Terminal,
                    IsExonerated = dto.IsExonerated,
                    Lines = dto.Lines.Select(l => new EmitLineInput
                    {
                        ItemId = l.ItemId,
                        CabysCode = l.CabysCode,
                        ItemCode = l.ItemCode,
                        Detail = l.Detail,
                        Quantity = l.Quantity,
                        UnitMeasure = l.UnitMeasure,
                        UnitPrice = l.UnitPrice,
                        PriceIncludesTax = l.PriceIncludesTax,
                        TaxRatePercent = l.TaxRatePercent,
                        TaxRateCode = l.TaxRateCode,
                        DiscountAmount = l.DiscountAmount,
                        DiscountNature = l.DiscountNature,
                        IsService = l.IsService,
                        IsExonerated = l.IsExonerated,
                        ExonDocumentType = l.ExonDocumentType,
                        ExonDocumentNumber = l.ExonDocumentNumber,
                        ExonInstitution = l.ExonInstitution,
                        ExonDate = l.ExonDate,
                        ExonPercent = l.ExonPercent
                    }).ToList(),
                    References = dto.References.Select(r => new EmitReferenceInput
                    {
                        RefDocumentType = r.RefDocumentType,
                        RefClave = r.RefClave,
                        RefDate = r.RefDate,
                        RefCode = r.RefCode,
                        RefReason = r.RefReason
                    }).ToList()
                };

                var result = await _documentService.EmitAsync(input);
                return Ok(new EmitResultDto
                {
                    DocumentId = result.DocumentId,
                    Clave = result.Clave,
                    Consecutive = result.Consecutive,
                    Status = result.Status,
                    SentToHacienda = result.SentToHacienda,
                    Message = result.Message,
                    Logs = result.Logs.Select(l => new EmitLogDto
                    {
                        Timestamp = l.Timestamp,
                        Step = l.Step,
                        Level = l.Level,
                        Message = l.Message
                    }).ToList()
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error emitiendo comprobante");
                // Desanidar la excepción interna (p. ej. errores de EF/PostgreSQL) para
                // exponer la causa real (violación de constraint, valor fuera de rango, etc.).
                var detail = ex.Message;
                var inner = ex.InnerException;
                while (inner != null)
                {
                    detail += " -> " + inner.Message;
                    inner = inner.InnerException;
                }
                return StatusCode(500, new { message = "Error emitiendo comprobante", error = detail });
            }
        }

        /// <summary>Lista comprobantes de la compañía (resumen).</summary>
        [HttpGet]
        public async Task<ActionResult<List<ElectronicDocumentSummaryDto>>> GetAll(
            [FromQuery] int? issuerId = null, [FromQuery] string? status = null, [FromQuery] int limit = 100)
        {
            var companyId = GetCompanyId();
            await using var db = await _factory.CreateDbContextAsync(companyId);
            limit = Math.Clamp(limit, 1, 500);

            var query = db.ElectronicDocuments.AsNoTracking().AsQueryable();
            if (issuerId.HasValue) query = query.Where(d => d.IdCustomerIssuer == issuerId.Value);
            if (!string.IsNullOrEmpty(status)) query = query.Where(d => d.Status == status);

            var list = await query
                .OrderByDescending(d => d.IssueDate)
                .Take(limit)
                .Select(d => new ElectronicDocumentSummaryDto
                {
                    Id = d.Id,
                    DocumentType = d.DocumentType,
                    Clave = d.Clave,
                    Consecutive = d.Consecutive,
                    Status = d.Status,
                    HaciendaStatus = d.HaciendaStatus,
                    HaciendaDetail = d.HaciendaDetail,
                    IssueDate = d.IssueDate,
                    Total = d.Total,
                    Currency = d.Currency,
                    SaleCondition = d.SaleCondition
                })
                .ToListAsync();

            // ── Calcular si cada documento reversable ya fue reversado al 100% ──────────
            // Un documento reversable es una Factura (01/04/08) o una Nota de Crédito (03)
            // ACEPTADA. Se compara la cantidad original por CAByS contra lo ya reversado
            // por notas de crédito/débito que lo referencian (aceptadas o pendientes).
            var reversableTypes = new[] { "01", "04", "08", EInvoiceDocumentType.NotaCredito };
            var reversableClaves = list
                .Where(d => !string.IsNullOrEmpty(d.Clave)
                            && reversableTypes.Contains(d.DocumentType)
                            && (d.HaciendaStatus == "aceptado" || d.Status == EInvoiceStatus.Aceptado))
                .Select(d => d.Clave!)
                .ToList();

            if (reversableClaves.Count > 0)
            {
                // Líneas de los documentos origen (para cantidades originales por CAByS).
                var sourceLines = await db.ElectronicDocuments
                    .AsNoTracking()
                    .Where(d => reversableClaves.Contains(d.Clave!))
                    .Select(d => new
                    {
                        d.Clave,
                        Lines = d.Lines.Select(l => new { l.CabysCode, l.Quantity }).ToList()
                    })
                    .ToListAsync();

                // Reversas aceptadas/pendientes que referencian esos documentos.
                var reversals = await db.ElectronicDocuments
                    .AsNoTracking()
                    .Where(d => (d.DocumentType == EInvoiceDocumentType.NotaCredito
                                 || d.DocumentType == EInvoiceDocumentType.NotaDebito)
                                && d.Status != EInvoiceStatus.Rechazado
                                && d.Status != EInvoiceStatus.Anulado
                                && d.References.Any(r => reversableClaves.Contains(r.RefClave!)))
                    .Select(d => new
                    {
                        RefClaves = d.References.Select(r => r.RefClave).ToList(),
                        Lines = d.Lines.Select(l => new { l.CabysCode, l.Quantity }).ToList()
                    })
                    .ToListAsync();

                foreach (var doc in list)
                {
                    if (string.IsNullOrEmpty(doc.Clave) || !reversableClaves.Contains(doc.Clave))
                        continue;

                    var source = sourceLines.FirstOrDefault(s => s.Clave == doc.Clave);
                    if (source == null || source.Lines.Count == 0) continue;

                    var originalByCabys = source.Lines
                        .GroupBy(l => l.CabysCode)
                        .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

                    var reversedByCabys = reversals
                        .Where(r => r.RefClaves.Contains(doc.Clave))
                        .SelectMany(r => r.Lines)
                        .GroupBy(l => l.CabysCode)
                        .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

                    // Totalmente reversado si toda cantidad original ya fue reversada.
                    doc.FullyReversed = originalByCabys.All(kv =>
                    {
                        reversedByCabys.TryGetValue(kv.Key, out var rev);
                        return rev >= kv.Value;
                    });
                }
            }

            // ── Calcular si cada factura a crédito ya fue pagada al 100% por REPs ────────
            // Solo aplica a facturas (01/08) a crédito (SaleCondition == "02"). Se compara
            // la cantidad original por CAByS contra lo ya documentado por Recibos
            // Electrónicos de Pago (REP, tipo 10) aceptados o pendientes que la referencian.
            var payableClaves = list
                .Where(d => !string.IsNullOrEmpty(d.Clave)
                            && (d.DocumentType == "01" || d.DocumentType == "08")
                            && d.SaleCondition == "02"
                            && (d.HaciendaStatus == "aceptado" || d.Status == EInvoiceStatus.Aceptado))
                .Select(d => d.Clave!)
                .ToList();

            if (payableClaves.Count > 0)
            {
                var payableSourceLines = await db.ElectronicDocuments
                    .AsNoTracking()
                    .Where(d => payableClaves.Contains(d.Clave!))
                    .Select(d => new
                    {
                        d.Clave,
                        Lines = d.Lines.Select(l => new { l.CabysCode, l.Quantity }).ToList()
                    })
                    .ToListAsync();

                // REP (tipo 10) aceptados/pendientes que referencian esas facturas.
                var receipts = await db.ElectronicDocuments
                    .AsNoTracking()
                    .Where(d => d.DocumentType == EInvoiceDocumentType.ReciboElectronicoPago
                                && d.Status != EInvoiceStatus.Rechazado
                                && d.Status != EInvoiceStatus.Anulado
                                && d.References.Any(r => payableClaves.Contains(r.RefClave!)))
                    .Select(d => new
                    {
                        RefClaves = d.References.Select(r => r.RefClave).ToList(),
                        Lines = d.Lines.Select(l => new { l.CabysCode, l.Quantity }).ToList()
                    })
                    .ToListAsync();

                foreach (var doc in list)
                {
                    if (string.IsNullOrEmpty(doc.Clave) || !payableClaves.Contains(doc.Clave))
                        continue;

                    var source = payableSourceLines.FirstOrDefault(s => s.Clave == doc.Clave);
                    if (source == null || source.Lines.Count == 0) continue;

                    var originalByCabys = source.Lines
                        .GroupBy(l => l.CabysCode)
                        .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

                    var paidByCabys = receipts
                        .Where(r => r.RefClaves.Contains(doc.Clave))
                        .SelectMany(r => r.Lines)
                        .GroupBy(l => l.CabysCode)
                        .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

                    // Totalmente pagada si toda cantidad original ya fue documentada por REP.
                    doc.FullyPaid = originalByCabys.All(kv =>
                    {
                        paidByCabys.TryGetValue(kv.Key, out var paid);
                        return paid >= kv.Value;
                    });
                }
            }

            return Ok(list);
        }

        /// <summary>Detalle completo de un comprobante (cabecera + líneas + impuestos + referencias + respuesta Hacienda).</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetDetail(int id)
        {
            var companyId = GetCompanyId();
            await using var db = await _factory.CreateDbContextAsync(companyId);
            var doc = await db.ElectronicDocuments
                .AsNoTracking()
                .Include(d => d.Lines).ThenInclude(l => l.Taxes)
                .Include(d => d.References)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();

            // Cantidades ya acreditadas por Notas de Crédito previas (no rechazadas/anuladas)
            // que referencian ESTE documento, agrupadas por CAByS. Permite que la precarga de
            // una nueva N/C muestre solo el saldo disponible por línea y evitar reversar de más.
            var alreadyCreditedByCabys = new Dictionary<string, decimal>();
            if (!string.IsNullOrWhiteSpace(doc.Clave))
            {
                var previousCredits = await db.ElectronicDocuments
                    .AsNoTracking()
                    .Include(d => d.Lines)
                    .Include(d => d.References)
                    .Where(d => d.DocumentType == EInvoiceDocumentType.NotaCredito
                                && d.Status != EInvoiceStatus.Rechazado
                                && d.Status != EInvoiceStatus.Anulado
                                && d.References.Any(r => r.RefClave == doc.Clave))
                    .ToListAsync();

                alreadyCreditedByCabys = previousCredits
                    .SelectMany(n => n.Lines)
                    .GroupBy(l => l.CabysCode ?? string.Empty)
                    .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            }

            // Saldo disponible por CAByS = original − ya acreditado. Se distribuye por línea
            // (un documento puede repetir el mismo CAByS en varias líneas).
            var originalByCabys = doc.Lines
                .GroupBy(l => l.CabysCode ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            var remainingByCabys = originalByCabys.ToDictionary(
                kv => kv.Key,
                kv => kv.Value - (alreadyCreditedByCabys.TryGetValue(kv.Key, out var c) ? c : 0m));

            // AvailableQuantity por línea (consumiendo el saldo disponible del CAByS en orden).
            var availableByLineNumber = new Dictionary<int, decimal>();
            foreach (var l in doc.Lines.OrderBy(l => l.LineNumber))
            {
                var key = l.CabysCode ?? string.Empty;
                remainingByCabys.TryGetValue(key, out var rem);
                var take = Math.Max(0m, Math.Min(l.Quantity, rem));
                availableByLineNumber[l.LineNumber] = take;
                remainingByCabys[key] = rem - take;
            }

            // Cantidades ya pagadas por Recibos Electrónicos de Pago (REP) previos (no
            // rechazados/anulados) que referencian ESTE documento, agrupadas por CAByS.
            // Permite que la precarga de un nuevo REP parcial muestre solo el saldo pendiente.
            var alreadyPaidByCabys = new Dictionary<string, decimal>();
            if (!string.IsNullOrWhiteSpace(doc.Clave))
            {
                var previousReceipts = await db.ElectronicDocuments
                    .AsNoTracking()
                    .Include(d => d.Lines)
                    .Include(d => d.References)
                    .Where(d => d.DocumentType == EInvoiceDocumentType.ReciboElectronicoPago
                                && d.Status != EInvoiceStatus.Rechazado
                                && d.Status != EInvoiceStatus.Anulado
                                && d.References.Any(r => r.RefClave == doc.Clave))
                    .ToListAsync();

                alreadyPaidByCabys = previousReceipts
                    .SelectMany(n => n.Lines)
                    .GroupBy(l => l.CabysCode ?? string.Empty)
                    .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            }

            // Saldo pendiente de pago por CAByS = original − ya pagado (REP previos).
            var remainingPayByCabys = originalByCabys.ToDictionary(
                kv => kv.Key,
                kv => kv.Value - (alreadyPaidByCabys.TryGetValue(kv.Key, out var p) ? p : 0m));

            // AvailableQuantityRep por línea (saldo pendiente de pago, en orden).
            var availableRepByLineNumber = new Dictionary<int, decimal>();
            foreach (var l in doc.Lines.OrderBy(l => l.LineNumber))
            {
                var key = l.CabysCode ?? string.Empty;
                remainingPayByCabys.TryGetValue(key, out var rem);
                var take = Math.Max(0m, Math.Min(l.Quantity, rem));
                availableRepByLineNumber[l.LineNumber] = take;
                remainingPayByCabys[key] = rem - take;
            }

            return Ok(new
            {
                // ── Identificación ──────────────────────────────────
                doc.Id,
                doc.IdCustomerIssuer,
                doc.IdCustomerReceptor,
                doc.DocumentType,
                doc.Clave,
                doc.Consecutive,
                doc.Situation,
                doc.Status,
                doc.IssueDate,

                // ── Condiciones comerciales ──────────────────────────
                doc.SaleCondition,
                doc.CreditTerm,
                doc.PaymentMethod,
                doc.Currency,
                doc.ExchangeRate,

                // ── Totales detallados ───────────────────────────────
                doc.SubTotal,
                doc.TotalDiscount,
                doc.TotalTaxable,
                doc.TotalExempt,
                doc.TotalTaxes,
                doc.Total,

                // ── Estado Hacienda ──────────────────────────────────
                doc.HaciendaStatus,
                doc.HaciendaDetail,
                doc.SubmittedAt,
                doc.AcceptedAt,
                doc.XmlResponse,

                // ── Artefactos disponibles ───────────────────────────
                HasXml = !string.IsNullOrEmpty(doc.XmlSigned),
                HasPdf = doc.PdfDocument != null && doc.PdfDocument.Length > 0,

                // ── Auditoría ────────────────────────────────────────
                doc.CreatedBy,
                doc.CreateDate,
                doc.UpdatedBy,
                doc.RecordDate,

                // ── Emisor (persistido en BD) ────────────────────────
                Emisor = new
                {
                    Nombre                  = doc.EmisorNombre,
                    NombreComercial         = doc.EmisorNombreComercial,
                    IdentificacionTipo      = doc.EmisorIdentificacionTipo,
                    IdentificacionNumero    = doc.EmisorIdentificacionNumero,
                    Correo                  = doc.EmisorCorreo,
                    UbicacionProvincia      = doc.EmisorUbicacionProvincia,
                    UbicacionCanton         = doc.EmisorUbicacionCanton,
                    UbicacionDistrito       = doc.EmisorUbicacionDistrito,
                    UbicacionBarrio         = doc.EmisorUbicacionBarrio,
                    UbicacionOtrasSenas     = doc.EmisorUbicacionOtrasSenas,
                    TelefonoCodigoPais      = doc.EmisorTelefonoCodigoPais,
                    TelefonoNumero          = doc.EmisorTelefonoNumero,
                    CodigoActividadEmisor   = doc.CodigoActividadEmisor,
                    ProveedorSistemas       = doc.ProveedorSistemas
                },

                // ── Receptor (persistido en BD) ──────────────────────
                Receptor = new
                {
                    Nombre                      = doc.ReceptorNombre,
                    NombreComercial             = doc.ReceptorNombreComercial,
                    IdentificacionTipo          = doc.ReceptorIdentificacionTipo,
                    IdentificacionNumero        = doc.ReceptorIdentificacionNumero,
                    IdentificacionExtranjero    = doc.ReceptorIdentificacionExtranjero,
                    Correo                      = doc.ReceptorCorreo,
                    UbicacionProvincia          = doc.ReceptorUbicacionProvincia,
                    UbicacionCanton             = doc.ReceptorUbicacionCanton,
                    UbicacionDistrito           = doc.ReceptorUbicacionDistrito,
                    UbicacionOtrasSenas         = doc.ReceptorUbicacionOtrasSenas,
                    TelefonoCodigoPais          = doc.ReceptorTelefonoCodigoPais,
                    TelefonoNumero              = doc.ReceptorTelefonoNumero
                },

                // ── ResumenFactura completo (persistido en BD) ───────
                ResumenFactura = new
                {
                    CodigoMoneda                = doc.Currency,
                    TipoCambio                  = doc.ExchangeRate,
                    doc.TotalServGravados,
                    doc.TotalServExentos,
                    doc.TotalServExonerado,
                    doc.TotalMercanciasGravadas,
                    doc.TotalMercanciasExentas,
                    doc.TotalMercExonerada,
                    doc.TotalGravado,
                    TotalExento                 = doc.TotalExempt,
                    doc.TotalExonerado,
                    doc.TotalNoSujeto,
                    doc.TotalVenta,
                    TotalDescuentos             = doc.TotalDiscount,
                    doc.TotalVentaNeta,
                    TotalImpuesto               = doc.TotalTaxes,
                    doc.TotalImpuestoDescontado,
                    doc.TotalIvaDevuelto,
                    doc.TotalComprobante,
                    DesgloseImpuestoCodigo      = doc.DesgloseImpuestoCodigo,
                    DesgloseImpuestoTarifaIva   = doc.DesgloseImpuestoTarifaIva,
                    DesgloseImpuestoMonto       = doc.DesgloseImpuestoMonto,
                    MedioPagoTipo               = doc.MedioPagoTipo,
                    MedioPagoTotal              = doc.MedioPagoTotal
                },

                // ── Respuesta de Hacienda parseada (persistida en BD) ─
                HaciendaResponseParsed = new
                {
                    MensajeCodigo   = doc.HaciendaMensajeCodigo,
                    DetalleMensaje  = doc.HaciendaDetail,
                    MontoImpuesto   = doc.HaciendaMontoImpuesto,
                    TotalFactura    = doc.HaciendaTotalFactura,
                    FechaEmisionDoc = doc.HaciendaFechaEmisionDoc,
                    FechaRecepcion  = doc.HaciendaFechaRecepcion
                },

                // ── Líneas con impuestos ─────────────────────────────
                Lines = doc.Lines.OrderBy(l => l.LineNumber).Select(l => new
                {
                    l.LineNumber,
                    l.CabysCode,
                    l.ItemCode,
                    l.Detail,
                    l.IsService,
                    l.Quantity,
                    // Cantidad aún disponible para acreditar (original − N/C previas). En una FE
                    // sin N/C previas es igual a Quantity; tras una N/C parcial refleja el saldo.
                    AvailableQuantity = availableByLineNumber.TryGetValue(l.LineNumber, out var aq) ? aq : l.Quantity,
                    // Cantidad aún pendiente de pago (original − REP previos). Usada por la
                    // precarga de un REP parcial para mostrar solo el saldo por cobrar.
                    AvailableQuantityRep = availableRepByLineNumber.TryGetValue(l.LineNumber, out var aqr) ? aqr : l.Quantity,
                    l.UnitMeasure,
                    l.UnitPrice,
                    l.TotalAmount,
                    l.DiscountAmount,
                    l.DiscountNature,
                    l.SubTotal,
                    l.TaxableBase,
                    l.TotalTax,
                    l.TotalLine,
                    l.ImpuestoAsumidoEmisor,
                    l.ImpuestoNeto,
                    l.MontoTotalLinea,
                    l.TaxRateCodeIva,
                    l.TaxRateIva,
                    l.MontoTaxIva,
                    Taxes = l.Taxes.Select(t => new
                    {
                        t.TaxCode,
                        t.TaxRateCode,
                        t.TaxRate,
                        t.TaxAmount
                    })
                }),

                // ── Referencias (NC/ND/REP) ──────────────────────────
                References = doc.References.Select(r => new
                {
                    r.RefDocumentType,
                    r.RefClave,
                    r.RefDate,
                    r.RefCode,
                    r.RefReason
                })
            });
        }


        /// <summary>Obtiene el XML firmado de un comprobante.</summary>
        [HttpGet("{id:int}/xml")]
        public async Task<IActionResult> GetXml(int id)
        {
            var companyId = GetCompanyId();
            await using var db = await _factory.CreateDbContextAsync(companyId);
            var doc = await db.ElectronicDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
            if (doc?.XmlSigned == null) return NotFound();
            return Content(doc.XmlSigned, "application/xml");
        }

        /// <summary>Descarga el PDF (representación gráfica) de un comprobante.</summary>
        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> GetPdf(int id)
        {
            var companyId = GetCompanyId();
            await using var db = await _factory.CreateDbContextAsync(companyId);
            var doc = await db.ElectronicDocuments
                .Include(d => d.Lines)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();

            // Si el PDF aún no existe (p. ej. documentos emitidos antes de habilitar
            // la generación, o rechazados), se genera bajo demanda a partir de los
            // datos ya persistidos en el documento y se almacena para futuras cargas.
            if (doc.PdfDocument == null || doc.PdfDocument.Length == 0)
            {
                try
                {
                    var bytes = _pdfService.GeneratePdf(doc, doc.Lines.OrderBy(l => l.LineNumber).ToList());
                    doc.PdfDocument = bytes;
                    doc.RecordDate = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo generar el PDF bajo demanda del documento {Id}", id);
                    return StatusCode(500, new { message = "No se pudo generar el PDF del comprobante." });
                }
            }

            return File(doc.PdfDocument!, "application/pdf", $"comprobante_{doc.Consecutive}.pdf");
        }

        /// <summary>Devuelve la bitácora histórica (paso a paso) de un comprobante.</summary>
        [HttpGet("{id:int}/logs")]
        public async Task<IActionResult> GetLogs(int id)
        {
            var companyId = GetCompanyId();
            await using var db = await _factory.CreateDbContextAsync(companyId);
            var logs = await db.ElectronicDocumentLogs.AsNoTracking()
                .Where(l => l.IdElectronicDocument == id)
                .OrderBy(l => l.Id)
                .Select(l => new
                {
                    l.Id,
                    l.Step,
                    l.Level,
                    l.Message,
                    l.Detail,
                    Timestamp = l.CreateDate
                })
                .ToListAsync();
            return Ok(logs);
        }

        /// <summary>Reprocesa manualmente un comprobante pendiente/contingencia.</summary>
        [HttpPost("{id:int}/process")]
        public async Task<IActionResult> Process(int id)
        {
            var companyId = GetCompanyId();
            await _documentService.ProcessPendingAsync(companyId, id);
            return Ok(new { message = "Reproceso solicitado" });
        }

        /// <summary>
        /// Consulta on-demand el estado actual del comprobante en Hacienda (ind-estado).
        /// Actualiza el documento a Aceptado/Rechazado si Hacienda ya resolvió, y deja
        /// constancia en la bitácora. Lo usa la consola de emisión y el botón de la lista.
        /// </summary>
        [HttpPost("{id:int}/poll-status")]
        public async Task<IActionResult> PollStatus(int id)
        {
            var companyId = GetCompanyId();
            var result = await _documentService.PollStatusAsync(companyId, id);
            return Ok(new
            {
                result.Status,
                result.HaciendaStatus,
                result.HaciendaDetail,
                result.Resolved,
                result.Message
            });
        }

        /// <summary>
        /// Backfill: lee el XML firmado y el XML de respuesta de Hacienda ya
        /// almacenados para este documento, los parsea, y actualiza los campos
        /// fiscales de electronic_document y electronic_document_line en la BD.
        /// Útil para documentos emitidos antes de que se agregaran las columnas
        /// dedicadas (emisor, receptor, resumen fiscal, respuesta Hacienda).
        /// </summary>
        [HttpPost("{id:int}/backfill")]
        public async Task<IActionResult> Backfill(int id)
        {
            var companyId = GetCompanyId();
            await using var db = await _factory.CreateDbContextAsync(companyId);
            var doc = await db.ElectronicDocuments
                .Include(d => d.Lines)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound(new { message = "Documento no encontrado" });

            var updated = new List<string>();

            // ── 1. Parsear XML firmado (emisor, receptor, resumen, líneas) ──
            if (!string.IsNullOrEmpty(doc.XmlSigned))
            {
                try
                {
                    var xdoc = XDocument.Parse(doc.XmlSigned);
                    var ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;
                    var root = xdoc.Root;

                    XElement? G(XElement? p, string n) => p?.Element(ns + n) ?? p?.Element(XName.Get(n));
                    string? V(XElement? p, string n) => G(p, n)?.Value?.Trim();
                    decimal? D(XElement? p, string n)
                    {
                        var v = V(p, n);
                        return decimal.TryParse(v, System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null;
                    }

                    var emisor      = G(root, "Emisor");
                    var emisorIdent = G(emisor, "Identificacion");
                    var emisorUbic  = G(emisor, "Ubicacion");
                    var emisorTel   = G(emisor, "Telefono");

                    var receptor      = G(root, "Receptor");
                    var receptorIdent = G(receptor, "Identificacion");
                    var receptorUbic  = G(receptor, "Ubicacion");
                    var receptorTel   = G(receptor, "Telefono");

                    var resumen      = G(root, "ResumenFactura");
                    var codigoMoneda = G(resumen, "CodigoTipoMoneda");
                    var medioPago    = G(resumen, "MedioPago");
                    var desgloseImp  = G(resumen, "TotalDesgloseImpuesto");

                    // Cabecera
                    doc.ProveedorSistemas     = V(root, "ProveedorSistemas") ?? doc.ProveedorSistemas;
                    doc.CodigoActividadEmisor = V(root, "CodigoActividadEmisor") ?? doc.CodigoActividadEmisor;

                    // Emisor
                    if (emisor != null)
                    {
                        doc.EmisorNombre               = V(emisor, "Nombre");
                        doc.EmisorNombreComercial       = V(emisor, "NombreComercial");
                        doc.EmisorIdentificacionTipo    = V(emisorIdent, "Tipo");
                        doc.EmisorIdentificacionNumero  = V(emisorIdent, "Numero");
                        doc.EmisorCorreo                = V(emisor, "CorreoElectronico");
                        doc.EmisorUbicacionProvincia    = V(emisorUbic, "Provincia");
                        doc.EmisorUbicacionCanton       = V(emisorUbic, "Canton");
                        doc.EmisorUbicacionDistrito     = V(emisorUbic, "Distrito");
                        doc.EmisorUbicacionBarrio       = V(emisorUbic, "Barrio");
                        doc.EmisorUbicacionOtrasSenas   = V(emisorUbic, "OtrasSenas");
                        doc.EmisorTelefonoCodigoPais     = V(emisorTel, "CodigoPais");
                        doc.EmisorTelefonoNumero        = V(emisorTel, "NumTelefono");
                        updated.Add("Emisor");
                    }

                    // Receptor
                    if (receptor != null)
                    {
                        doc.ReceptorNombre                    = V(receptor, "Nombre");
                        doc.ReceptorNombreComercial           = V(receptor, "NombreComercial");
                        doc.ReceptorIdentificacionTipo        = V(receptorIdent, "Tipo");
                        doc.ReceptorIdentificacionNumero      = V(receptorIdent, "Numero");
                        doc.ReceptorIdentificacionExtranjero  = V(receptor, "IdentificacionExtranjero");
                        doc.ReceptorCorreo                    = V(receptor, "CorreoElectronico");
                        doc.ReceptorUbicacionProvincia         = V(receptorUbic, "Provincia");
                        doc.ReceptorUbicacionCanton            = V(receptorUbic, "Canton");
                        doc.ReceptorUbicacionDistrito           = V(receptorUbic, "Distrito");
                        doc.ReceptorUbicacionOtrasSenas         = V(receptorUbic, "OtrasSenas");
                        doc.ReceptorTelefonoCodigoPais           = V(receptorTel, "CodigoPais");
                        doc.ReceptorTelefonoNumero               = V(receptorTel, "NumTelefono");
                        updated.Add("Receptor");
                    }

                    // ResumenFactura
                    if (resumen != null)
                    {
                        doc.TotalServGravados          = D(resumen, "TotalServGravados");
                        doc.TotalServExentos            = D(resumen, "TotalServExentos");
                        doc.TotalServExonerado           = D(resumen, "TotalServExonerado");
                        doc.TotalMercanciasGravadas      = D(resumen, "TotalMercanciasGravadas");
                        doc.TotalMercanciasExentas       = D(resumen, "TotalMercanciasExentas");
                        doc.TotalMercExonerada           = D(resumen, "TotalMercExonerada");
                        doc.TotalGravado                 = D(resumen, "TotalGravado");
                        doc.TotalExonerado               = D(resumen, "TotalExonerado");
                        doc.TotalNoSujeto                = D(resumen, "TotalNoSujeto");
                        doc.TotalVenta                   = D(resumen, "TotalVenta");
                        doc.TotalVentaNeta                = D(resumen, "TotalVentaNeta");
                        doc.TotalImpuestoDescontado       = D(resumen, "TotalImpuestoDescontado");
                        doc.TotalIvaDevuelto               = D(resumen, "TotalIVADevuelto");
                        doc.TotalComprobante              = D(resumen, "TotalComprobante");
                        doc.MedioPagoTipo                 = V(medioPago, "TipoMedioPago");
                        doc.MedioPagoTotal                = D(medioPago, "TotalMedioPago");

                        if (desgloseImp != null)
                        {
                            doc.DesgloseImpuestoCodigo     = V(desgloseImp, "Codigo");
                            doc.DesgloseImpuestoTarifaIva  = V(desgloseImp, "CodigoTarifaIVA");
                            doc.DesgloseImpuestoMonto      = D(desgloseImp, "TotalMontoImpuesto");
                        }
                        updated.Add("ResumenFactura");
                    }

                    // Líneas: leer LineaDetalle y aplicar a las líneas existentes por NumeroLinea
                    var xmlLines = root?.Elements(ns + "DetalleServicio")
                        .SelectMany(dsEl => dsEl.Elements(ns + "LineaDetalle"))
                        .ToList() ?? new List<XElement>();

                    foreach (var xl in xmlLines)
                    {
                        var lineNum = V(xl, "NumeroLinea");
                        if (!int.TryParse(lineNum, out var lineNumber)) continue;

                        var dbLine = doc.Lines.FirstOrDefault(l => l.LineNumber == lineNumber);
                        if (dbLine == null) continue;

                        dbLine.ImpuestoAsumidoEmisor = D(xl, "ImpuestoAsumidoEmisorFabrica") ?? 0;
                        dbLine.ImpuestoNeto          = D(xl, "ImpuestoNeto") ?? dbLine.TotalTax;
                        dbLine.MontoTotalLinea       = D(xl, "MontoTotalLinea") ?? dbLine.TotalLine;

                        var impuesto = xl.Elements(ns + "Impuesto").FirstOrDefault();
                        if (impuesto != null)
                        {
                            dbLine.TaxRateCodeIva = V(impuesto, "CodigoTarifaIVA");
                            // El XML v4.4 trae <Tarifa> como porcentaje (ej: 13.00000).
                            // Se almacena como fracción decimal (0.13) para ser consistente
                            // con la ruta de emisión (ElectronicDocumentService: Tarifa/100)
                            // y respetar la precisión de la columna NUMERIC(5,4).
                            var tarifa = D(impuesto, "Tarifa");
                            dbLine.TaxRateIva     = tarifa.HasValue ? tarifa.Value / 100m : null;
                            dbLine.MontoTaxIva    = D(impuesto, "Monto");
                        }
                    }
                    if (xmlLines.Count > 0) updated.Add($"Lines ({xmlLines.Count})");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parseando XML firmado en backfill del documento {Id}", id);
                }
            }

            // ── 2. Parsear XML de respuesta de Hacienda ──────────────────────
            if (!string.IsNullOrEmpty(doc.XmlResponse))
            {
                try
                {
                    var xdocR = XDocument.Parse(doc.XmlResponse);
                    var nsR = xdocR.Root?.Name.Namespace ?? XNamespace.None;
                    string? VR(string n) =>
                        (xdocR.Root?.Element(nsR + n) ?? xdocR.Root?.Element(XName.Get(n)))?.Value?.Trim();

                    doc.HaciendaMensajeCodigo = VR("Mensaje");
                    doc.HaciendaDetail        = VR("DetalleMensaje") ?? doc.HaciendaDetail;

                    if (decimal.TryParse(VR("MontoTotalImpuesto"), System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var monto))
                        doc.HaciendaMontoImpuesto = monto;

                    if (decimal.TryParse(VR("TotalFactura"), System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var total))
                        doc.HaciendaTotalFactura = total;

                    if (DateTime.TryParse(VR("FechaEmisionDoc"), out var fechaDoc))
                        doc.HaciendaFechaEmisionDoc = DateTime.SpecifyKind(fechaDoc, DateTimeKind.Utc);

                    if (DateTime.TryParse(VR("FechaRecepcion"), out var fechaRec))
                        doc.HaciendaFechaRecepcion = DateTime.SpecifyKind(fechaRec, DateTimeKind.Utc);

                    updated.Add("HaciendaResponse");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parseando XML de respuesta Hacienda en backfill del documento {Id}", id);
                }
            }

            if (updated.Count == 0)
                return Ok(new { message = "Nada que actualizar: el documento no tiene XML firmado ni respuesta de Hacienda almacenados.", updated });

            doc.UpdatedBy = GetUserId().ToString();
            doc.RecordDate = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Ok(new { message = "Backfill completado.", updated });
        }
    }
}
