// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/XadesSignatureService.cs
// PROPÓSITO: Firma XAdES-EPES Enveloped (RSA-SHA256) de comprobantes CR v4.4
// DESCRIPCIÓN: Usa FirmaXadesNetCore para generar la firma EXACTAMENTE en el formato
//              que exige el validador del Ministerio de Hacienda de Costa Rica
//              (prefijos ds:/xades:, canonicalización y Signature Policy EPES).
//              El certificado se obtiene del Vault (memoria volátil, Zero-Trust).
//
//   NOTA: La implementación manual previa (SignedXml) producía una firma
//   matemáticamente válida (CheckSignature=True) pero Hacienda la rechazaba por el
//   namespace por defecto (sin prefijo ds:). FirmaXadesNet resuelve esto.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Security.Cryptography.X509Certificates;
using CMS.Entities.Operational;
using FirmaXadesNetCore;
using FirmaXadesNetCore.Crypto;
using FirmaXadesNetCore.Signature.Parameters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services.EInvoice
{
    /// <inheritdoc cref="IXadesSignatureService"/>
    public class XadesSignatureService : IXadesSignatureService
    {
        // Política de firma obligatoria (Resolución General v4.4 2024 — valor real verificado).
        private const string PolicyIdentifier =
            "https://atv.hacienda.go.cr/ATV/ComprobanteElectronico/docs/esquemas/2024/v4.4/Resoluci%C3%B3n_General_sobre_disposiciones_t%C3%A9cnicas_comprobantes_electr%C3%B3nicos_para_efectos_tributarios.pdf";

        // SHA-256 (HEX) del PDF de la política vigente (valor oficial verificado en facturas reales).
        private const string PolicyHashHex =
            "0D6C629F5C5639E23C3AE5905DACE1E158CB5806822C003DE787A6EC3321D21F";

        private readonly IEInvoiceVaultService _vault;
        private readonly ILogger<XadesSignatureService> _logger;

        // Digest SHA-256 (base64) del PDF de la política vigente (config).
        private readonly string _policyDigestBase64;

        public XadesSignatureService(
            IEInvoiceVaultService vault,
            IConfiguration configuration,
            ILogger<XadesSignatureService> logger)
        {
            _vault = vault;
            _logger = logger;
            // Prioridad: config; si no, el valor oficial verificado (HEX de la política v4.4 2024).
            _policyDigestBase64 = configuration["EInvoice:PolicyDigestSha256"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_policyDigestBase64))
                _policyDigestBase64 = PolicyHashHex;
        }

        /// <inheritdoc />
        public string SignXml(string unsignedXml, CustomerBillingCredential credential)
        {
            return _vault.UseCertificate(credential, cert => SignWithCertificate(unsignedXml, cert));
        }

        private string SignWithCertificate(string unsignedXml, X509Certificate2 cert)
        {
            var xadesService = new XadesService();

            var parameters = new SignatureParameters
            {
                SignaturePackaging = SignaturePackaging.ENVELOPED,
                DataFormat = new DataFormat { MimeType = "text/xml" },
                SignatureMethod = SignatureMethod.RSAwithSHA256,
                DigestMethod = DigestMethod.SHA256,
                SigningDate = DateTime.Now,
                Signer = new Signer(cert),
                SignaturePolicyInfo = new SignaturePolicyInfo
                {
                    PolicyIdentifier = PolicyIdentifier,
                    PolicyHash = _policyDigestBase64,
                    PolicyDigestAlgorithm = DigestMethod.SHA256
                }
            };

            using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(unsignedXml));
            var signedDocument = xadesService.Sign(input, parameters);

            using var output = new MemoryStream();
            signedDocument.Save(output);
            var result = System.Text.Encoding.UTF8.GetString(output.ToArray());

            _logger.LogInformation("✍️ XML firmado XAdES-EPES (FirmaXadesNet) subject: {Subject}", cert.Subject);
            return result;
        }
    }
}
