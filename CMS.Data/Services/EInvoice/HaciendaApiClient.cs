// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/HaciendaApiClient.cs
// PROPÓSITO: Cliente HTTP de la API de recepción de comprobantes de Hacienda CR v4.4
// DESCRIPCIÓN: Envía comprobantes y consulta su estado. Maneja los códigos HTTP
//              críticos de forma resiliente:
//                202 -> aceptado (encolar consulta de estado)
//                429 -> leer X-RateLimit-Reset / Retry-After y reintentar
//                400 duplicado ("ya recibido") -> tratar como enviado, consultar estado
//                401 -> token expirado, señalar reautenticación
//                5xx / red -> reintentar con backoff
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Entities.EInvoice;
using CMS.Entities.Operational;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services.EInvoice
{
    /// <inheritdoc cref="IHaciendaApiClient"/>
    public class HaciendaApiClient : IHaciendaApiClient
    {
        // IMPORTANTE (Tribu 2025+): el SANDBOX usa un HOST distinto (api-sandbox...),
        // NO el path 'recepcion-sandbox'. Mapeo verificado (2026, respuesta 202 Accepted):
        //   - Producción: api.comprobanteselectronicos.go.cr/recepcion/v1/
        //   - Sandbox   : api-sandbox.comprobanteselectronicos.go.cr/recepcion/v1/
        private const string ProdBaseUrl = "https://api.comprobanteselectronicos.go.cr/recepcion/v1/";
        private const string StagBaseUrl = "https://api-sandbox.comprobanteselectronicos.go.cr/recepcion/v1/";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HaciendaApiClient> _logger;

        public HaciendaApiClient(IHttpClientFactory httpClientFactory, ILogger<HaciendaApiClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        private static string BaseUrlFor(string environment) =>
            environment == EInvoiceEnvironment.Production ? ProdBaseUrl : StagBaseUrl;

        /// <inheritdoc />
        public async Task<HaciendaApiResult> SubmitAsync(
            CustomerBillingCredential credential, string accessToken, string clave, object receptionPayload,
            CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient("hacienda-api");
            var url = BaseUrlFor(credential.Environment) + "recepcion";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("CMS-HaciendaCore/4.4 (BITI Solutions)");
            request.Content = JsonContent.Create(receptionPayload);

            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Accepted:      // 202
                    case HttpStatusCode.OK:            // 200
                        return new HaciendaApiResult { Accepted = true, Status = "recibido", ResponseBody = body };

                    case HttpStatusCode.TooManyRequests: // 429
                        return new HaciendaApiResult
                        {
                            ShouldRetry = true,
                            Status = "rate_limited",
                            RetryAfterSeconds = ReadRetryAfter(response),
                            ResponseBody = body
                        };

                    case HttpStatusCode.BadRequest:      // 400
                        // "Comprobante ya recibido" => tratar como enviado, ir a consultar estado.
                        if (IsDuplicate(body))
                        {
                            _logger.LogInformation("Comprobante {Clave} ya recibido por Hacienda (duplicado).", clave);
                            return new HaciendaApiResult { Accepted = true, Status = "recibido", ResponseBody = body };
                        }
                        return new HaciendaApiResult { Status = "rechazado", Error = body, ResponseBody = body };

                    case HttpStatusCode.Unauthorized:    // 401
                        return new HaciendaApiResult { Unauthorized = true, Status = "unauthorized", ResponseBody = body };

                    default:
                        if ((int)response.StatusCode >= 500)
                            return new HaciendaApiResult { ShouldRetry = true, Status = "error", Error = body, ResponseBody = body };
                        return new HaciendaApiResult { Status = "error", Error = body, ResponseBody = body };
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Error de red enviando {Clave} a Hacienda; se reintentará.", clave);
                return new HaciendaApiResult { ShouldRetry = true, Status = "error", Error = ex.Message };
            }
        }

        /// <inheritdoc />
        public async Task<HaciendaApiResult> GetStatusAsync(
            CustomerBillingCredential credential, string accessToken, string clave, CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient("hacienda-api");
            var url = BaseUrlFor(credential.Environment) + "recepcion/" + clave;

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("CMS-HaciendaCore/4.4 (BITI Solutions)");

            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        var parsed = ParseHaciendaResponse(body);
                        return new HaciendaApiResult
                        {
                            Accepted = true,
                            Status = parsed.Status,
                            ResponseBody = body,
                            HaciendaMessageXml = parsed.MessageXml,
                            HaciendaDetail = parsed.Detail
                        };

                    case HttpStatusCode.TooManyRequests:
                        return new HaciendaApiResult
                        {
                            ShouldRetry = true, Status = "rate_limited",
                            RetryAfterSeconds = ReadRetryAfter(response), ResponseBody = body
                        };

                    case HttpStatusCode.Unauthorized:
                        return new HaciendaApiResult { Unauthorized = true, Status = "unauthorized", ResponseBody = body };

                    case HttpStatusCode.NotFound:
                        // Aún no procesado.
                        return new HaciendaApiResult { ShouldRetry = true, Status = "procesando", ResponseBody = body };

                    default:
                        if ((int)response.StatusCode >= 500)
                            return new HaciendaApiResult { ShouldRetry = true, Status = "error", Error = body, ResponseBody = body };
                        return new HaciendaApiResult { Status = "error", Error = body, ResponseBody = body };
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Error de red consultando estado {Clave}; se reintentará.", clave);
                return new HaciendaApiResult { ShouldRetry = true, Status = "procesando", Error = ex.Message };
            }
        }

        private static int? ReadRetryAfter(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset) &&
                int.TryParse(reset.FirstOrDefault(), out var seconds))
                return seconds;
            if (response.Headers.RetryAfter?.Delta is { } delta)
                return (int)delta.TotalSeconds;
            return null;
        }

        private static bool IsDuplicate(string body) =>
            body.Contains("ya recibido", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("ya fue recibido", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("duplicad", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Parsea la respuesta de consulta de estado de Hacienda. El JSON incluye
        /// 'ind-estado' y 'respuesta-xml' (base64 del MensajeHacienda). Del XML se
        /// extraen EstadoMensaje (Aceptado/Rechazado) y DetalleMensaje.
        /// </summary>
        private static (string Status, string? MessageXml, string? Detail) ParseHaciendaResponse(string body)
        {
            string status = "procesando";
            string? messageXml = null;
            string? detail = null;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("ind-estado", out var est))
                    status = est.GetString() ?? "procesando";

                if (root.TryGetProperty("respuesta-xml", out var xmlB64))
                {
                    var b64 = xmlB64.GetString();
                    if (!string.IsNullOrWhiteSpace(b64))
                    {
                        messageXml = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                        var (estadoMensaje, detalleMensaje) = ParseMensajeHacienda(messageXml);
                        if (!string.IsNullOrWhiteSpace(estadoMensaje))
                            status = estadoMensaje.ToLowerInvariant();
                        detail = detalleMensaje;
                    }
                }
                return (NormalizeStatus(status), messageXml, detail);
            }
            catch
            {
                // Cuerpo no-JSON: heurística por texto.
            }

            if (body.Contains("aceptado", StringComparison.OrdinalIgnoreCase)) status = "aceptado";
            else if (body.Contains("rechazado", StringComparison.OrdinalIgnoreCase)) status = "rechazado";
            return (status, messageXml, detail);
        }

        /// <summary>Extrae EstadoMensaje y DetalleMensaje del XML MensajeHacienda.</summary>
        private static (string? Estado, string? Detalle) ParseMensajeHacienda(string xml)
        {
            try
            {
                var xdoc = System.Xml.Linq.XDocument.Parse(xml);
                System.Xml.Linq.XNamespace ns = xdoc.Root!.Name.Namespace;
                var estado = xdoc.Root.Element(ns + "EstadoMensaje")?.Value;
                var detalle = xdoc.Root.Element(ns + "DetalleMensaje")?.Value;
                return (estado, detalle);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>Normaliza el estado textual de Hacienda a los valores internos.</summary>
        private static string NormalizeStatus(string s)
        {
            s = s.ToLowerInvariant();
            if (s.Contains("acept")) return "aceptado";
            if (s.Contains("rechaz")) return "rechazado";
            if (s.Contains("recib")) return "procesando";
            if (s.Contains("proces")) return "procesando";
            return s;
        }
    }
}
