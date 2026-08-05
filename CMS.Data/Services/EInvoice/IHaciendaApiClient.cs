// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/IHaciendaApiClient.cs
// PROPÓSITO: Interfaz del cliente HTTP de la API de recepción de Hacienda CR
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.Operational;

namespace CMS.Data.Services.EInvoice
{
    /// <summary>Resultado de una operación contra la API de Hacienda.</summary>
    public sealed class HaciendaApiResult
    {
        /// <summary>El comprobante fue aceptado para procesamiento (202) o ya existía (duplicado).</summary>
        public bool Accepted { get; init; }
        /// <summary>Estado reportado: 'recibido','procesando','aceptado','rechazado','error'.</summary>
        public string Status { get; init; } = "error";
        /// <summary>Indica que se debe reintentar más tarde (429/5xx/red).</summary>
        public bool ShouldRetry { get; init; }
        /// <summary>Segundos sugeridos de espera antes del próximo intento (rate limit).</summary>
        public int? RetryAfterSeconds { get; init; }
        /// <summary>Cuerpo/respuesta XML o mensaje (MensajeHacienda).</summary>
        public string? ResponseBody { get; init; }
        /// <summary>Mensaje de error legible.</summary>
        public string? Error { get; init; }
        /// <summary>Indica que el token expiró (401) y hay que reautenticar.</summary>
        public bool Unauthorized { get; init; }
        /// <summary>MensajeHacienda decodificado (XML del comprobante de respuesta).</summary>
        public string? HaciendaMessageXml { get; init; }
        /// <summary>Detalle legible del mensaje de Hacienda (DetalleMensaje).</summary>
        public string? HaciendaDetail { get; init; }
    }

    /// <summary>Cliente de la API de recepción de comprobantes de Hacienda.</summary>
    public interface IHaciendaApiClient
    {
        /// <summary>Envía (POST /recepcion) el comprobante firmado.</summary>
        Task<HaciendaApiResult> SubmitAsync(
            CustomerBillingCredential credential, string accessToken, string clave, object receptionPayload,
            CancellationToken cancellationToken = default);

        /// <summary>Consulta el estado (GET /recepcion/{clave}) de un comprobante.</summary>
        Task<HaciendaApiResult> GetStatusAsync(
            CustomerBillingCredential credential, string accessToken, string clave,
            CancellationToken cancellationToken = default);
    }
}
