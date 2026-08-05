// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/IEInvoiceVaultService.cs
// PROPÓSITO: Interfaz del servicio de cifrado/descifrado de secretos (Vault)
// DESCRIPCIÓN: Gestiona el cifrado AES-256 de .p12/PIN/OAuth y su uso en memoria
//              volátil durante la firma (Zero-Trust).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.Security.Cryptography.X509Certificates;
using CMS.Entities.Operational;

namespace CMS.Data.Services.EInvoice
{
    /// <summary>Resultado de cifrado: dato cifrado + IV.</summary>
    public readonly record struct EncryptedSecret(byte[] Cipher, byte[] Iv);

    /// <summary>
    /// Servicio Vault: cifra/descifra secretos del emisor con AES-256.
    /// Zero-Trust: los secretos solo se descifran en memoria volátil.
    /// </summary>
    public interface IEInvoiceVaultService
    {
        /// <summary>Cifra un array de bytes (ej. contenido del .p12) con AES-256.</summary>
        EncryptedSecret Encrypt(byte[] plain, int keyVersion = 1);

        /// <summary>Cifra una cadena (ej. PIN, password OAuth) con AES-256.</summary>
        EncryptedSecret EncryptString(string plain, int keyVersion = 1);

        /// <summary>Descifra a bytes. El llamador DEBE limpiar el resultado tras usarlo.</summary>
        byte[] Decrypt(byte[] cipher, byte[] iv, int keyVersion = 1);

        /// <summary>Descifra a cadena. Usar solo transitoriamente.</summary>
        string DecryptString(byte[] cipher, byte[] iv, int keyVersion = 1);

        /// <summary>
        /// Ejecuta una operación de firma cargando el certificado en memoria volátil,
        /// y limpia p12/PIN inmediatamente después (GC forzado). Zero-Trust.
        /// </summary>
        T UseCertificate<T>(CustomerBillingCredential credential, Func<X509Certificate2, T> action);
    }
}
