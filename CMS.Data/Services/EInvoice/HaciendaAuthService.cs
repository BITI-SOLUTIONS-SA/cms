// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/HaciendaAuthService.cs
// PROPÓSITO: Autenticación OAuth2 con el IdP de Hacienda (Keycloak realm 'rut')
// DESCRIPCIÓN: Implementa el flujo legacy Resource Owner Password Credentials
//              (grant_type=password) exigido por Hacienda. Cachea el token por emisor
//              y lo refresca 5 minutos antes de expirar. Maneja refresh_token.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CMS.Entities.EInvoice;
using CMS.Entities.Operational;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services.EInvoice
{
    /// <inheritdoc cref="IHaciendaAuthService"/>
    public class HaciendaAuthService : IHaciendaAuthService
    {
        // IMPORTANTE (Tribu 2025+): el SANDBOX migró al realm 'rut-stag'.
        //   - Producción: realm 'rut'      + client_id 'api-prod'
        //   - Sandbox   : realm 'rut-stag' + client_id 'api-stag'
        private const string TokenEndpointProd =
            "https://idp.comprobanteselectronicos.go.cr/auth/realms/rut/protocol/openid-connect/token";
        private const string TokenEndpointStag =
            "https://idp.comprobanteselectronicos.go.cr/auth/realms/rut-stag/protocol/openid-connect/token";
        private const string ClientIdProd = "api-prod";
        private const string ClientIdStag = "api-stag";
        private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IEInvoiceVaultService _vault;
        private readonly ILogger<HaciendaAuthService> _logger;

        // Caché de tokens por credential (thread-safe).
        private static readonly ConcurrentDictionary<int, CachedToken> _cache = new();

        public HaciendaAuthService(
            IHttpClientFactory httpClientFactory,
            IEInvoiceVaultService vault,
            ILogger<HaciendaAuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _vault = vault;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> GetAccessTokenAsync(
            CustomerBillingCredential credential, CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(credential.Id, out var cached) &&
                cached.ExpiresAt - RefreshBuffer > DateTimeOffset.UtcNow)
            {
                return cached.AccessToken;
            }

            // Intentar refresh_token si existe y no ha expirado del todo.
            if (_cache.TryGetValue(credential.Id, out cached) &&
                !string.IsNullOrEmpty(cached.RefreshToken) &&
                cached.RefreshExpiresAt > DateTimeOffset.UtcNow)
            {
                try
                {
                    return await RequestWithRefreshTokenAsync(credential, cached.RefreshToken, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Refresh token falló para credential {CredentialId}; reautenticando", credential.Id);
                }
            }

            return await RequestWithPasswordAsync(credential, cancellationToken);
        }

        /// <inheritdoc />
        public Task<string> ForceRefreshAsync(
            CustomerBillingCredential credential, CancellationToken cancellationToken = default)
        {
            _cache.TryRemove(credential.Id, out _);
            return RequestWithPasswordAsync(credential, cancellationToken);
        }

        private async Task<string> RequestWithPasswordAsync(
            CustomerBillingCredential credential, CancellationToken ct)
        {
            var username = credential.OAuthUsername
                ?? throw new InvalidOperationException($"Credential {credential.Id} sin usuario OAuth configurado.");
            var password = _vault.DecryptString(
                credential.OAuthPasswordCipher, credential.OAuthPasswordIv, credential.KeyVersion);

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = ClientIdFor(credential.Environment),
                ["username"] = username,
                ["password"] = password
            };

            return await ExchangeAsync(credential.Id, credential.Environment, form, ct);
        }

        private async Task<string> RequestWithRefreshTokenAsync(CustomerBillingCredential credential, string refreshToken, CancellationToken ct)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = ClientIdFor(credential.Environment),
                ["refresh_token"] = refreshToken
            };
            return await ExchangeAsync(credential.Id, credential.Environment, form, ct);
        }

        private async Task<string> ExchangeAsync(int credentialId, string environment, Dictionary<string, string> form, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("hacienda-idp");
            var tokenEndpoint = TokenEndpointFor(environment);
            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(tokenEndpoint, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Fallo OAuth Hacienda ({Status}) credential {CredentialId}: {Body}",
                    response.StatusCode, credentialId, body);
                response.EnsureSuccessStatusCode();
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Respuesta OAuth de Hacienda vacía.");

            var now = DateTimeOffset.UtcNow;
            _cache[credentialId] = new CachedToken(
                token.AccessToken,
                token.RefreshToken,
                now.AddSeconds(token.ExpiresIn),
                now.AddSeconds(token.RefreshExpiresIn > 0 ? token.RefreshExpiresIn : token.ExpiresIn));

            _logger.LogInformation("🔓 Token Hacienda obtenido para credential {CredentialId} (expira en {Sec}s)",
                credentialId, token.ExpiresIn);

            return token.AccessToken;
        }

        private static string ClientIdFor(string environment) =>
            environment == EInvoiceEnvironment.Production ? ClientIdProd : ClientIdStag;

        private static string TokenEndpointFor(string environment) =>
            environment == EInvoiceEnvironment.Production ? TokenEndpointProd : TokenEndpointStag;

        private sealed record CachedToken(
            string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt, DateTimeOffset RefreshExpiresAt);

        private sealed class TokenResponse
        {
            [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
            [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
            [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
            [JsonPropertyName("refresh_expires_in")] public int RefreshExpiresIn { get; set; }
        }
    }
}
