// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/IElectronicDocumentTypeCatalogService.cs
// PROPÓSITO: Interfaz del servicio de catálogo de tipos de documento electrónico.
// DESCRIPCIÓN: Expone acceso cacheado a la tabla CENTRAL admin.electronic_document_type
//              y helpers para resolver el metadato/banderas de un tipo por su código.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.EInvoice;

namespace CMS.Data.Services.EInvoice
{
    /// <summary>
    /// Provee acceso (cacheado) al catálogo parametrizable de tipos de documento
    /// electrónico. Es la ÚNICA fuente de verdad para: qué tipos aparecen en el
    /// selector de Emit y qué comportamiento de generación de XML aplica cada tipo.
    /// </summary>
    public interface IElectronicDocumentTypeCatalogService
    {
        /// <summary>Devuelve todos los tipos activos del catálogo (cacheado).</summary>
        Task<IReadOnlyList<ElectronicDocumentTypeCatalog>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Devuelve los tipos activos de una versión específica (por id de versión).</summary>
        Task<IReadOnlyList<ElectronicDocumentTypeCatalog>> GetAllByVersionAsync(int versionId, CancellationToken ct = default);

        /// <summary>Devuelve todas las versiones del esquema (cacheado), ordenadas.</summary>
        Task<IReadOnlyList<ElectronicDocumentVersion>> GetVersionsAsync(CancellationToken ct = default);

        /// <summary>Devuelve la versión vigente (is_current=true) o null si no hay ninguna.</summary>
        Task<ElectronicDocumentVersion?> GetCurrentVersionAsync(CancellationToken ct = default);

        /// <summary>Devuelve solo los tipos visibles en el selector de la pantalla Emit (show_in_emit=true) de la versión vigente, ordenados.</summary>
        Task<IReadOnlyList<ElectronicDocumentTypeCatalog>> GetVisibleForEmitAsync(CancellationToken ct = default);

        /// <summary>
        /// Resuelve el metadato/banderas de un tipo por su código fiscal (01..10)
        /// dentro de la versión VIGENTE. Devuelve null si no existe en el catálogo.
        /// </summary>
        Task<ElectronicDocumentTypeCatalog?> GetByCodeAsync(string code, CancellationToken ct = default);

        /// <summary>Invalida el cache tras una modificación desde el mantenimiento.</summary>
        void InvalidateCache();
    }
}
