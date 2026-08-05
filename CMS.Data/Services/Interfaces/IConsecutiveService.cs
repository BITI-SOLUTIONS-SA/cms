// ================================================================================
// ARCHIVO: CMS.Data/Services/Interfaces/IConsecutiveService.cs
// PROPÓSITO: Interfaz del servicio de consecutivos
// ================================================================================

namespace CMS.Data.Services.Interfaces
{
    public interface IConsecutiveService
    {
        /// <summary>
        /// Genera el siguiente número consecutivo para un menú y tipo de documento
        /// </summary>
        Task<string> GenerateNextNumberAsync(
            int companyId,
            int menuId,
            int entityDocumentId,
            int userId);

        /// <summary>
        /// Obtiene información del consecutivo sin generarlo (preview)
        /// </summary>
        Task<ConsecutiveInfo?> GetConsecutiveInfoAsync(
            int companyId,
            int menuId,
            int entityDocumentId);
    }
}
