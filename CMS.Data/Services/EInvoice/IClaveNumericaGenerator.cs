// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/IClaveNumericaGenerator.cs
// PROPÓSITO: Interfaz del generador de Clave Numérica de 50 díg. y consecutivo 20 díg.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

namespace CMS.Data.Services.EInvoice
{
    /// <summary>Resultado de la generación: clave de 50 díg. + consecutivo de 20 díg.</summary>
    public readonly record struct ClaveNumericaResult(string Clave, string Consecutive, long Sequence);

    /// <summary>
    /// Genera la Clave Numérica de 50 dígitos y el consecutivo de 20 dígitos
    /// de forma atómica (bloqueo Serializable) por emisor/sucursal/terminal/tipo.
    /// </summary>
    public interface IClaveNumericaGenerator
    {
        /// <param name="companyId">Compañía (para resolver la BD operacional).</param>
        /// <param name="issuerId">Emisor facturador.</param>
        /// <param name="issuerIdentification">Cédula del emisor (se rellena a 12).</param>
        /// <param name="documentType">Tipo de documento (01,02,03,04,08,09).</param>
        /// <param name="branch">Sucursal (3 díg.).</param>
        /// <param name="terminal">Terminal/POS (5 díg.).</param>
        /// <param name="situation">Situación (1=normal,2=contingencia,3=sin internet).</param>
        /// <param name="issueDate">Fecha de emisión (para día/mes/año de la clave).</param>
        /// <param name="userId">Usuario que genera.</param>
        /// <param name="consecutiveId">
        /// (Opcional) Id del consecutivo fiscal específico seleccionado por el usuario en la
        /// pantalla de emisión. Cuando se indica, se usa exactamente ese registro (validando que
        /// esté activo y corresponda al emisor y tipo de documento). Si es null, se resuelve el
        /// consecutivo por defecto activo (comportamiento histórico).
        /// </param>
        Task<ClaveNumericaResult> GenerateAsync(
            int companyId,
            int issuerId,
            string issuerIdentification,
            string documentType,
            string branch,
            string terminal,
            string situation,
            DateTime issueDate,
            int userId,
            int? consecutiveId = null);
    }
}
