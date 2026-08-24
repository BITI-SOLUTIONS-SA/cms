// ================================================================================
// ARCHIVO: CMS.Data/Services/EInvoice/ElectronicDocumentTypeCatalogService.cs
// PROPÓSITO: Implementación cacheada del catálogo de tipos de documento electrónico.
// DESCRIPCIÓN: Lee la tabla CENTRAL admin.electronic_document_type desde AppDbContext
//              y cachea el resultado en memoria. El catálogo cambia muy raramente
//              (solo desde el mantenimiento), por lo que el cache reduce consultas.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using CMS.Entities.EInvoice;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CMS.Data.Services.EInvoice
{
    /// <inheritdoc cref="IElectronicDocumentTypeCatalogService"/>
    public class ElectronicDocumentTypeCatalogService : IElectronicDocumentTypeCatalogService
    {
        private const string CacheKey = "einvoice:electronic_document_types:all";
        private const string VersionsCacheKey = "einvoice:electronic_document_versions:all";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public ElectronicDocumentTypeCatalogService(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<IReadOnlyList<ElectronicDocumentTypeCatalog>> GetAllAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<ElectronicDocumentTypeCatalog>? cached) && cached is not null)
                return cached;

            var list = await _db.ElectronicDocumentTypes
                .AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Code)
                .ToListAsync(ct);

            _cache.Set(CacheKey, (IReadOnlyList<ElectronicDocumentTypeCatalog>)list, CacheTtl);
            return list;
        }

        public async Task<IReadOnlyList<ElectronicDocumentTypeCatalog>> GetAllByVersionAsync(int versionId, CancellationToken ct = default)
        {
            var all = await GetAllAsync(ct);
            return all.Where(t => t.IdVersion == versionId)
                      .OrderBy(t => t.SortOrder).ThenBy(t => t.Code).ToList();
        }

        public async Task<IReadOnlyList<ElectronicDocumentVersion>> GetVersionsAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(VersionsCacheKey, out IReadOnlyList<ElectronicDocumentVersion>? cached) && cached is not null)
                return cached;

            var list = await _db.ElectronicDocumentVersions
                .AsNoTracking()
                .Where(v => v.IsActive)
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.Code)
                .ToListAsync(ct);

            _cache.Set(VersionsCacheKey, (IReadOnlyList<ElectronicDocumentVersion>)list, CacheTtl);
            return list;
        }

        public async Task<ElectronicDocumentVersion?> GetCurrentVersionAsync(CancellationToken ct = default)
        {
            var versions = await GetVersionsAsync(ct);
            return versions.FirstOrDefault(v => v.IsCurrent);
        }

        public async Task<IReadOnlyList<ElectronicDocumentTypeCatalog>> GetVisibleForEmitAsync(CancellationToken ct = default)
        {
            var all = await GetAllAsync(ct);
            var current = await GetCurrentVersionAsync(ct);
            var query = all.Where(t => t.ShowInEmit);
            // La emisión SIEMPRE usa la versión vigente.
            if (current is not null)
                query = query.Where(t => t.IdVersion == current.Id);
            return query.OrderBy(t => t.SortOrder).ToList();
        }

        public async Task<ElectronicDocumentTypeCatalog?> GetByCodeAsync(string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var all = await GetAllAsync(ct);
            var current = await GetCurrentVersionAsync(ct);
            // La emisión resuelve el tipo dentro de la versión vigente.
            if (current is not null)
                return all.FirstOrDefault(t => t.Code == code && t.IdVersion == current.Id);
            return all.FirstOrDefault(t => t.Code == code);
        }

        public void InvalidateCache()
        {
            _cache.Remove(CacheKey);
            _cache.Remove(VersionsCacheKey);
        }
    }
}
