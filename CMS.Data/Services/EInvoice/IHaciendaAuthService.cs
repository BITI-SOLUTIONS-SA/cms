// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/IHaciendaAuthService.cs
// PROPÓSITO: Interfaz del servicio de autenticación OAuth2 con Hacienda CR
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.Operational;

namespace CMS.Data.Services.EInvoice
{
    /// <summary>
    /// Autenticación OAuth2 (Resource Owner Password Credentials) con el IdP de Hacienda.
    /// Cachea el access_token por credential y lo renueva 5 min antes de expirar.
    /// </summary>
    public interface IHaciendaAuthService
    {
        /// <summary>
        /// Obtiene un access_token válido para la credential, renovándolo si está por expirar.
        /// </summary>
        Task<string> GetAccessTokenAsync(
            CustomerBillingCredential credential,
            CancellationToken cancellationToken = default);

        /// <summary>Fuerza la renovación inmediata del token (ante un 401).</summary>
        Task<string> ForceRefreshAsync(
            CustomerBillingCredential credential,
            CancellationToken cancellationToken = default);
    }
}
