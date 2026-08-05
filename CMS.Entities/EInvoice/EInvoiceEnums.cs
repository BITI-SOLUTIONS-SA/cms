// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/EInvoiceEnums.cs
// PROPÓSITO: Enumeraciones y constantes del módulo de Facturación Electrónica CR v4.4
// DESCRIPCIÓN: Códigos oficiales del Ministerio de Hacienda de Costa Rica (DGT)
//              usados en la generación de comprobantes electrónicos v4.4.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

namespace CMS.Entities.EInvoice
{
    /// <summary>
    /// Tipos de documento electrónico (2 dígitos) según catálogo de Hacienda v4.4.
    /// </summary>
    public static class EInvoiceDocumentType
    {
        public const string FacturaElectronica = "01";      // FE
        public const string NotaDebito = "02";              // ND
        public const string NotaCredito = "03";             // NC
        public const string TiqueteElectronico = "04";      // TE
        public const string FacturaCompra = "08";           // FEC (proveedores extranjeros)
        public const string ReciboElectronicoPago = "10";   // REP (exclusivo v4.4). Código fiscal 10 (el 09 es Factura de Exportación).
    }

    /// <summary>
    /// Situación del comprobante (1 dígito) usada en la Clave Numérica.
    /// </summary>
    public static class EInvoiceSituation
    {
        public const string Normal = "1";
        public const string Contingencia = "2";
        public const string SinInternet = "3";
    }

    /// <summary>
    /// Estados internos del documento electrónico (máquina de estados).
    /// </summary>
    public static class EInvoiceStatus
    {
        public const string Borrador = "Borrador";
        public const string Firmado = "Firmado";
        public const string Enviado = "Enviado";
        public const string Pendiente = "Pendiente";
        public const string Contingencia = "Contingencia";
        public const string Procesando = "Procesando";
        public const string Aceptado = "Aceptado";
        public const string Rechazado = "Rechazado";
        public const string Anulado = "Anulado";
    }

    /// <summary>
    /// Tipos de identificación (2 dígitos) según Hacienda.
    /// </summary>
    public static class EInvoiceIdentificationType
    {
        public const string Fisica = "01";                  // Cédula física
        public const string Juridica = "02";                // Cédula jurídica
        public const string Dimex = "03";                   // DIMEX
        public const string Nite = "04";                    // NITE
        public const string ExtranjeroNoDomiciliado = "05"; // Extranjero (FEC)
    }

    /// <summary>
    /// Naturaleza del descuento (obligatorio en v4.4).
    /// </summary>
    public static class EInvoiceDiscountNature
    {
        public const string Regalia = "01";
        public const string Volumen = "04";
        public const string Temporada = "05";
        public const string Promocion = "06";
    }

    /// <summary>
    /// Códigos de tarifa del IVA (2 dígitos) según Hacienda v4.4.
    /// </summary>
    public static class EInvoiceTaxRateCode
    {
        public const string Exento0 = "01";     // 0%
        public const string Reducido1 = "02";   // 1%
        public const string Reducido2 = "03";   // 2%
        public const string Reducido4 = "04";   // 4%
        public const string Transitorio05 = "05"; // 0.5%
        public const string Transitorio1 = "06";  // 1%
        public const string Transitorio2 = "07";  // 2%
        public const string General13 = "08";     // 13%
    }

    /// <summary>
    /// Ambiente de conexión con Hacienda.
    /// </summary>
    public static class EInvoiceEnvironment
    {
        public const string Sandbox = "stag";
        public const string Production = "prod";
    }

    /// <summary>
    /// Operación encolada en la cola de reintentos.
    /// </summary>
    public static class EInvoiceRetryOperation
    {
        public const string Send = "send";
        public const string PollStatus = "poll_status";
    }
}
