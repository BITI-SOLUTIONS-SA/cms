// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/EInvoiceVaultService.cs
// PROPÓSITO: Implementación del Vault (AES-256) para Facturación Electrónica CR v4.4
// DESCRIPCIÓN: Cifra/descifra .p12, PIN y credenciales OAuth. La master key se deriva
//              (PBKDF2) de un secreto de aplicación gestionado FUERA de la BD
//              (configuración/variable de entorno/Kubernetes Secret). Nunca se guarda
//              junto a los datos cifrados. Soporta rotación por key_version.
//
//              PRINCIPIO ZERO-TRUST: el certificado .p12 y su PIN solo existen en
//              memoria RAM durante la firma; luego se sobrescriben los buffers y se
//              fuerza recolección de basura.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CMS.Entities.Operational;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services.EInvoice
{
    /// <inheritdoc cref="IEInvoiceVaultService"/>
    public class EInvoiceVaultService : IEInvoiceVaultService
    {
        private const int KeySizeBytes = 32;   // AES-256
        private const int SaltSizeBytes = 16;
        private const int Pbkdf2Iterations = 100_000;

        private readonly ILogger<EInvoiceVaultService> _logger;

        // Secreto maestro (fuera de la BD). Config key: "EInvoice:MasterKey".
        // En Kubernetes se monta desde un Secret; en dev desde appsettings/user-secrets.
        private readonly string _masterSecret;
        private readonly byte[] _fixedSalt;

        public EInvoiceVaultService(IConfiguration configuration, ILogger<EInvoiceVaultService> logger)
        {
            _logger = logger;

            _masterSecret = configuration["EInvoice:MasterKey"]
                ?? Environment.GetEnvironmentVariable("EINVOICE_MASTER_KEY")
                ?? throw new InvalidOperationException(
                    "No se configuró EInvoice:MasterKey (o EINVOICE_MASTER_KEY). " +
                    "Es obligatorio para el Vault de Facturación Electrónica.");

            // Salt determinístico derivado del secreto (permite descifrar sin persistir el salt).
            // La entropía real proviene del masterSecret gestionado externamente.
            var saltSource = configuration["EInvoice:KeySalt"] ?? "biti-hacienda-core-v4.4";
            _fixedSalt = SHA256.HashData(Encoding.UTF8.GetBytes(saltSource))[..SaltSizeBytes];
        }

        /// <summary>Deriva la clave AES-256 de la versión indicada (PBKDF2).</summary>
        private byte[] DeriveKey(int keyVersion)
        {
            var material = $"{_masterSecret}:v{keyVersion}";
            using var pbkdf2 = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(material), _fixedSalt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(KeySizeBytes);
        }

        /// <inheritdoc />
        public EncryptedSecret Encrypt(byte[] plain, int keyVersion = 1)
        {
            var key = DeriveKey(keyVersion);
            try
            {
                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.Key = key;
                aes.GenerateIV();
                using var encryptor = aes.CreateEncryptor();
                var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
                return new EncryptedSecret(cipher, aes.IV);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <inheritdoc />
        public EncryptedSecret EncryptString(string plain, int keyVersion = 1)
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            try
            {
                return Encrypt(bytes, keyVersion);
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        /// <inheritdoc />
        public byte[] Decrypt(byte[] cipher, byte[] iv, int keyVersion = 1)
        {
            var key = DeriveKey(keyVersion);
            try
            {
                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.Key = key;
                aes.IV = iv;
                using var decryptor = aes.CreateDecryptor();
                return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <inheritdoc />
        public string DecryptString(byte[] cipher, byte[] iv, int keyVersion = 1)
        {
            var plain = Decrypt(cipher, iv, keyVersion);
            try
            {
                return Encoding.UTF8.GetString(plain);
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
            }
        }

        /// <summary>
        /// Usa el certificado .p12 descifrado para ejecutar una operación.
        /// El certificado NUNCA se persiste en disco; vive solo en memoria durante la operación.
        /// </summary>
        public T UseCertificate<T>(CustomerBillingCredential credential, Func<X509Certificate2, T> action)
        {
            byte[]? p12 = null;
            string? pin = null;
            X509Certificate2? cert = null;
            try
            {
                p12 = Decrypt(credential.P12Cipher, credential.P12Iv, credential.KeyVersion);
                pin = DecryptString(credential.PinCipher, credential.PinIv, credential.KeyVersion);

                cert = X509CertificateLoader.LoadPkcs12(
                    p12, pin, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

                return action(cert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error usando el certificado del emisor {IssuerId}", credential.IdCustomer);
                throw;
            }
            finally
            {
                // Zero-Trust: limpiar secretos de la memoria inmediatamente.
                cert?.Dispose();
                if (p12 is not null) CryptographicOperations.ZeroMemory(p12);
                pin = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
}
