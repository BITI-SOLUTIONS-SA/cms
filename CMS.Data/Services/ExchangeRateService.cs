// ================================================================================
// ARCHIVO: CMS.Data/Services/ExchangeRateService.cs
// PROPÓSITO: Servicio para gestión del catálogo de tipos de tasa de cambio
// DESCRIPCIÓN: CRUD completo para el catálogo exchange_rate por compañía
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2026-06-28
// ================================================================================

using CMS.Entities.Operational;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services
{
    public interface IExchangeRateService
    {
        Task<List<ExchangeRate>> GetAllAsync(int companyId, bool? isActive = null);
        Task<ExchangeRate?> GetByIdAsync(int companyId, int id);
        Task<ExchangeRate?> GetByCodeAsync(int companyId, string code);
        Task<ExchangeRate> CreateAsync(int companyId, ExchangeRate exchangeRate, string currentUser);
        Task<ExchangeRate> UpdateAsync(int companyId, ExchangeRate exchangeRate, string currentUser);
        Task<bool> DeleteAsync(int companyId, int id);
        Task<bool> CodeExistsAsync(int companyId, string code, int? excludeId = null);
    }

    public class ExchangeRateService : IExchangeRateService
    {
        private readonly ICompanyDbContextFactory _contextFactory;
        private readonly ILogger<ExchangeRateService> _logger;

        public ExchangeRateService(
            ICompanyDbContextFactory contextFactory,
            ILogger<ExchangeRateService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>Obtiene todos los tipos de tasa de cambio con filtro opcional por estado</summary>
        public async Task<List<ExchangeRate>> GetAllAsync(int companyId, bool? isActive = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var query = context.ExchangeRates.AsQueryable();

            if (isActive.HasValue)
                query = query.Where(r => r.IsActive == isActive.Value);

            return await query
                .OrderBy(r => r.DisplayOrder)
                .ThenBy(r => r.Code)
                .ToListAsync();
        }

        /// <summary>Obtiene un tipo de tasa por su ID</summary>
        public async Task<ExchangeRate?> GetByIdAsync(int companyId, int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);
            return await context.ExchangeRates
                .FirstOrDefaultAsync(r => r.IdExchangeRate == id);
        }

        /// <summary>Obtiene un tipo de tasa por su código</summary>
        public async Task<ExchangeRate?> GetByCodeAsync(int companyId, string code)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);
            return await context.ExchangeRates
                .FirstOrDefaultAsync(r => r.Code == code);
        }

        /// <summary>Crea un nuevo tipo de tasa de cambio</summary>
        public async Task<ExchangeRate> CreateAsync(
            int companyId,
            ExchangeRate exchangeRate,
            string currentUser)
        {
            if (await CodeExistsAsync(companyId, exchangeRate.Code))
                throw new InvalidOperationException($"El código '{exchangeRate.Code}' ya existe.");

            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            exchangeRate.CreatedBy  = currentUser;
            exchangeRate.UpdatedBy  = currentUser;
            exchangeRate.CreateDate = DateTime.UtcNow;
            exchangeRate.RecordDate = DateTime.UtcNow;
            exchangeRate.Rowpointer = Guid.NewGuid();

            context.ExchangeRates.Add(exchangeRate);
            await context.SaveChangesAsync();

            _logger.LogInformation(
                "Tipo de tasa de cambio creado: {Code} por {User}",
                exchangeRate.Code, currentUser);

            return exchangeRate;
        }

        /// <summary>Actualiza un tipo de tasa de cambio existente</summary>
        public async Task<ExchangeRate> UpdateAsync(
            int companyId,
            ExchangeRate exchangeRate,
            string currentUser)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var existing = await context.ExchangeRates
                .FirstOrDefaultAsync(r => r.IdExchangeRate == exchangeRate.IdExchangeRate)
                ?? throw new InvalidOperationException(
                    $"Tipo de tasa de cambio con ID {exchangeRate.IdExchangeRate} no encontrado.");

            // Validar código único (excluyendo el propio registro)
            if (await CodeExistsAsync(companyId, exchangeRate.Code, exchangeRate.IdExchangeRate))
                throw new InvalidOperationException($"El código '{exchangeRate.Code}' ya existe.");

            existing.Code         = exchangeRate.Code;
            existing.Description  = exchangeRate.Description;
            existing.IsActive     = exchangeRate.IsActive;
            existing.DisplayOrder = exchangeRate.DisplayOrder;
            existing.UpdatedBy    = currentUser;
            existing.RecordDate   = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogInformation(
                "Tipo de tasa de cambio actualizado: {Code} (ID={Id}) por {User}",
                existing.Code, existing.IdExchangeRate, currentUser);

            return existing;
        }

        /// <summary>Elimina un tipo de tasa de cambio por su ID</summary>
        public async Task<bool> DeleteAsync(int companyId, int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var existing = await context.ExchangeRates
                .FirstOrDefaultAsync(r => r.IdExchangeRate == id);

            if (existing == null)
                return false;

            context.ExchangeRates.Remove(existing);
            await context.SaveChangesAsync();

            _logger.LogInformation(
                "Tipo de tasa de cambio eliminado: {Code} (ID={Id})",
                existing.Code, id);

            return true;
        }

        /// <summary>Verifica si un código ya existe, opcionalmente excluyendo un ID</summary>
        public async Task<bool> CodeExistsAsync(int companyId, string code, int? excludeId = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var query = context.ExchangeRates.Where(r => r.Code == code);

            if (excludeId.HasValue)
                query = query.Where(r => r.IdExchangeRate != excludeId.Value);

            return await query.AnyAsync();
        }
    }
}
