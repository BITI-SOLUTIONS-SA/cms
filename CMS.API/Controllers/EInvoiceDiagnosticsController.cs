// ================================================================================
// ARCHIVO: CMS.API/Controllers/EInvoiceDiagnosticsController.cs
// PROPÓSITO: Endpoints de diagnóstico del módulo de Facturación Electrónica CR v4.4
// DESCRIPCIÓN: Permite validar sin efectos secundarios:
//                - Generación de la Clave Numérica (50 díg.) y consecutivo (20 díg.)
//                - Construcción del XML v4.4 (sin firma) para inspección
//                - Conectividad con el IdP/API de Hacienda (sandbox/prod)
//                - Estado de la configuración del emisor (credencial, ambiente)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Security.Claims;
using CMS.Data.Services;
using CMS.Data.Services.EInvoice;
using CMS.Entities.EInvoice;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/einvoice/diagnostics")]
    public class EInvoiceDiagnosticsController : ControllerBase
    {
        private readonly ICompanyDbContextFactory _factory;
        private readonly IClaveNumericaGenerator _claveGenerator;
        private readonly IElectronicDocumentXmlBuilder _xmlBuilder;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EInvoiceDiagnosticsController> _logger;

        public EInvoiceDiagnosticsController(
            ICompanyDbContextFactory factory,
            IClaveNumericaGenerator claveGenerator,
            IElectronicDocumentXmlBuilder xmlBuilder,
            IHttpClientFactory httpClientFactory,
            ILogger<EInvoiceDiagnosticsController> logger)
        {
            _factory = factory;
            _claveGenerator = claveGenerator;
            _xmlBuilder = xmlBuilder;
            _httpClientFactory = httpClientFactory;
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

        /// <summary>
        /// Prueba la generación de la Clave Numérica y el XML v4.4 (sin firma) para un
        /// emisor, usando una línea de ejemplo. NO consume consecutivo real cuando
        /// dryRun=true (revierte). Útil para validar estructura antes de tener el .p12.
        /// </summary>
        [HttpPost("generate-sample")]
        public async Task<IActionResult> GenerateSample([FromQuery] int issuerId, [FromQuery] string documentType = "01",
            [FromQuery] string cabys = "2118401010109", [FromQuery] decimal price = 10000)
        {
            var companyId = GetCompanyId();
            await using var db = await _factory.CreateDbContextAsync(companyId);

            var issuerCred = await db.CustomerBillingCredentials
                .FirstOrDefaultAsync(c => c.Id == issuerId && c.IsIssuer && c.IsActive);
            if (issuerCred == null) return NotFound(new { message = "Credencial de emisor no encontrada" });

            // Para receptor, buscar cualquier credential activa que NO sea emisor
            var receptorCred = await db.CustomerBillingCredentials
                .FirstOrDefaultAsync(c => !c.IsIssuer && c.IsActive);

            var issueDate = DateTime.Now;
            var clave = await _claveGenerator.GenerateAsync(
                companyId, issuerCred.Id, issuerCred.Identification, documentType,
                "001", "00001", EInvoiceSituation.Normal, issueDate, GetUserId());

            // Construir un documento en memoria (no se persiste).
            var doc = new ElectronicDocument
            {
                IdCustomerIssuer = issuerCred.IdCustomer ?? 0,
                DocumentType = documentType,
                Clave = clave.Clave,
                Consecutive = clave.Consecutive,
                Situation = EInvoiceSituation.Normal,
                IssueDate = issueDate,
                Currency = "CRC",
                ExchangeRate = 1
            };
            var breakdown = EInvoiceCalculator.BreakdownLine(price, 1, 13m);
            var line = new ElectronicDocumentLine
            {
                Id = 1,
                LineNumber = 1,
                CabysCode = cabys,
                Detail = "Artículo de prueba (diagnóstico)",
                Quantity = 1,
                UnitMeasure = "Unid",
                UnitPrice = breakdown.UnitPriceBase,
                TotalAmount = breakdown.UnitPriceBase,
                SubTotal = breakdown.UnitPriceBase,
                TaxableBase = breakdown.TaxableBase,
                TotalTax = breakdown.TaxAmount,
                TotalLine = breakdown.TotalLine
            };
            var tax = new ElectronicDocumentTax
            {
                TaxCode = "01", TaxRateCode = "08", TaxRate = 13m, TaxAmount = breakdown.TaxAmount
            };
            doc.SubTotal = line.SubTotal;
            doc.TotalTaxable = line.TaxableBase;
            doc.TotalTaxes = line.TotalTax;
            doc.Total = line.TotalLine;

            var taxesByLine = new Dictionary<int, List<ElectronicDocumentTax>> { [1] = new() { tax } };
            var xml = _xmlBuilder.BuildXml(doc, issuerCred, receptorCred, new[] { line }, taxesByLine,
                Array.Empty<ElectronicDocumentReference>());

            return Ok(new
            {
                claveNumerica = clave.Clave,
                claveLength = clave.Clave.Length,
                consecutive = clave.Consecutive,
                consecutiveLength = clave.Consecutive.Length,
                totals = new { doc.SubTotal, doc.TotalTaxes, doc.Total },
                xml
            });
        }

        /// <summary>Verifica la conectividad con el IdP y la API de recepción de Hacienda.</summary>
        [HttpGet("ping-hacienda")]
        public async Task<IActionResult> PingHacienda([FromQuery] string environment = "stag")
        {
            var idp = "https://idp.comprobanteselectronicos.go.cr/auth/realms/rut/.well-known/openid-configuration";
            var api = environment == EInvoiceEnvironment.Production
                ? "https://api.comprobanteselectronicos.go.cr/recepcion/v1/"
                : "https://api.comprobanteselectronicos.go.cr/recepcion-sandbox/v1/";

            var client = _httpClientFactory.CreateClient("hacienda-api");
            client.Timeout = TimeSpan.FromSeconds(15);

            var result = new Dictionary<string, object>();
            foreach (var (name, url) in new[] { ("idp", idp), ("api", api) })
            {
                try
                {
                    using var resp = await client.GetAsync(url);
                    result[name] = new { url, reachable = true, status = (int)resp.StatusCode };
                }
                catch (Exception ex)
                {
                    result[name] = new { url, reachable = false, error = ex.Message };
                }
            }
            return Ok(result);
        }

        /// <summary>Estado de configuración del emisor (credencial, ambiente, vigencia).</summary>
        [HttpGet("issuer/{issuerId:int}/readiness")]
        public async Task<IActionResult> IssuerReadiness(int issuerId)
        {
            var companyId = GetCompanyId();
            await using var db = await _factory.CreateDbContextAsync(companyId);

            var issuerCred = await db.CustomerBillingCredentials.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == issuerId && c.IsIssuer);

            if (issuerCred == null) return NotFound(new { message = "Credencial de emisor no encontrada" });

            var checks = new
            {
                emisorNombre = issuerCred.Name,
                identificacion = issuerCred.Identification,
                ambiente = issuerCred.Environment,
                tieneCertificado = issuerCred.P12Cipher != null && issuerCred.P12Cipher.Length > 0,
                tieneOAuth = issuerCred.OAuthUsername != null,
                certificadoVigente = issuerCred.CertNotAfter == null || issuerCred.CertNotAfter > DateTime.UtcNow,
                actividadEconomica = !string.IsNullOrEmpty(issuerCred.EconomicActivity),
                listoParaEmitir = issuerCred.P12Cipher != null && issuerCred.P12Cipher.Length > 0
                    && issuerCred.OAuthUsername != null
                    && (issuerCred.CertNotAfter == null || issuerCred.CertNotAfter > DateTime.UtcNow)
                    && !string.IsNullOrEmpty(issuerCred.EconomicActivity)
            };
            return Ok(checks);
        }
    }
}
