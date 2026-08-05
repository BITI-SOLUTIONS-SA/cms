using CMS.Entities.Operational;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services;

public interface ICostCenterService
{
    Task<List<CostCenter>> GetCostCentersAsync(int companyId, bool? isActive = null, string? costCenterType = null, string? category = null, bool? isPostingAllowed = null);
    Task<List<CostCenter>> GetHierarchyAsync(int companyId);
    Task<List<CostCenter>> GetPostingCostCentersAsync(int companyId);
    Task<CostCenter?> GetCostCenterByIdAsync(int companyId, int idCostCenter);
    Task<CostCenter?> GetCostCenterByCodeAsync(int companyId, string code);
    Task<CostCenter> CreateCostCenterAsync(int companyId, CostCenter costCenter);
    Task<CostCenter> UpdateCostCenterAsync(int companyId, CostCenter costCenter);
    Task<bool> DeleteCostCenterAsync(int companyId, int idCostCenter);
    Task<bool> CodeExistsAsync(int companyId, string code, int? excludeId = null);
    Task<bool> HasChildrenAsync(int companyId, int idCostCenter);
    Task<bool> HasTransactionsAsync(int companyId, int idCostCenter);
    Task<List<CostCenter>> GetValidCostCentersAsync(int companyId, DateTime? asOfDate = null);
    Task<List<CostCenter>> GetChildrenAsync(int companyId, int idParentCostCenter);
}

public class CostCenterService : ICostCenterService
{
    private readonly ICompanyDbContextFactory _contextFactory;
    private readonly ILogger<CostCenterService> _logger;

    public CostCenterService(ICompanyDbContextFactory contextFactory, ILogger<CostCenterService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<List<CostCenter>> GetCostCentersAsync(int companyId, bool? isActive = null, string? costCenterType = null, string? category = null, bool? isPostingAllowed = null)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);
            var query = context.Set<CostCenter>().AsQueryable();

            if (isActive.HasValue)
                query = query.Where(cc => cc.IsActive == isActive.Value);

            if (!string.IsNullOrWhiteSpace(costCenterType))
                query = query.Where(cc => cc.CostCenterType == costCenterType);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(cc => cc.Category == category);

            if (isPostingAllowed.HasValue)
                query = query.Where(cc => cc.IsPostingAllowed == isPostingAllowed.Value);

            return await query
                .OrderBy(cc => cc.Code)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost centers for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<List<CostCenter>> GetHierarchyAsync(int companyId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            // Obtener todos los centros de costo con sus relaciones
            var costCenters = await context.Set<CostCenter>()
                .Include(cc => cc.ParentCostCenter)
                .Include(cc => cc.ChildCostCenters)
                .OrderBy(cc => cc.Code)
                .ToListAsync();

            return costCenters;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost center hierarchy for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<List<CostCenter>> GetPostingCostCentersAsync(int companyId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            return await context.Set<CostCenter>()
                .Where(cc => cc.IsActive && cc.IsPostingAllowed && !cc.IsBlocked)
                .OrderBy(cc => cc.Code)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting posting cost centers for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<CostCenter?> GetCostCenterByIdAsync(int companyId, int idCostCenter)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            return await context.Set<CostCenter>()
                .Include(cc => cc.ParentCostCenter)
                .Include(cc => cc.ChildCostCenters)
                .FirstOrDefaultAsync(cc => cc.IdCostCenter == idCostCenter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost center {IdCostCenter} for company {CompanyId}", idCostCenter, companyId);
            throw;
        }
    }

    public async Task<CostCenter?> GetCostCenterByCodeAsync(int companyId, string code)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            return await context.Set<CostCenter>()
                .Include(cc => cc.ParentCostCenter)
                .Include(cc => cc.ChildCostCenters)
                .FirstOrDefaultAsync(cc => cc.Code == code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost center with code {Code} for company {CompanyId}", code, companyId);
            throw;
        }
    }

    public async Task<CostCenter> CreateCostCenterAsync(int companyId, CostCenter costCenter)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            // Validar que el código no exista
            if (await CodeExistsAsync(companyId, costCenter.Code))
                throw new InvalidOperationException($"Ya existe un centro de costo con el código '{costCenter.Code}'");

            // Validar código jerárquico
            ValidateHierarchicalCode(costCenter.Code);

            // Validar que el padre exista si se especifica
            if (costCenter.IdParentCostCenter.HasValue)
            {
                var parent = await GetCostCenterByIdAsync(companyId, costCenter.IdParentCostCenter.Value);
                if (parent == null)
                    throw new InvalidOperationException($"El centro de costo padre con ID {costCenter.IdParentCostCenter.Value} no existe");

                // Validar que el código sea consistente con el padre
                ValidateCodeHierarchyConsistency(costCenter.Code, parent.Code);
            }

            // Validar que no se cree un ciclo
            if (costCenter.IdParentCostCenter.HasValue)
            {
                if (await WouldCreateCycleAsync(companyId, costCenter.IdCostCenter, costCenter.IdParentCostCenter.Value))
                    throw new InvalidOperationException("No se puede asignar el padre especificado porque crearía un ciclo en la jerarquía");
            }

            // Validar fechas de vigencia
            if (costCenter.ValidTo.HasValue && costCenter.ValidTo.Value < costCenter.ValidFrom)
                throw new InvalidOperationException("La fecha 'válido hasta' no puede ser anterior a la fecha 'válido desde'");

            // Validar presupuesto
            if (costCenter.AnnualBudget.HasValue && costCenter.AnnualBudget.Value < 0)
                throw new InvalidOperationException("El presupuesto anual no puede ser negativo");

            context.Set<CostCenter>().Add(costCenter);
            await context.SaveChangesAsync();

            _logger.LogInformation("Cost center {Code} created for company {CompanyId}", costCenter.Code, companyId);
            return costCenter;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cost center for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<CostCenter> UpdateCostCenterAsync(int companyId, CostCenter costCenter)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var existing = await context.Set<CostCenter>()
                .FirstOrDefaultAsync(cc => cc.IdCostCenter == costCenter.IdCostCenter);

            if (existing == null)
                throw new InvalidOperationException($"Centro de costo con ID {costCenter.IdCostCenter} no encontrado");

            // Validar código único (si cambió)
            if (existing.Code != costCenter.Code)
            {
                if (await CodeExistsAsync(companyId, costCenter.Code, costCenter.IdCostCenter))
                    throw new InvalidOperationException($"Ya existe un centro de costo con el código '{costCenter.Code}'");

                ValidateHierarchicalCode(costCenter.Code);
            }

            // Validar que el padre exista y no cree ciclos
            if (costCenter.IdParentCostCenter.HasValue)
            {
                var parent = await GetCostCenterByIdAsync(companyId, costCenter.IdParentCostCenter.Value);
                if (parent == null)
                    throw new InvalidOperationException($"El centro de costo padre con ID {costCenter.IdParentCostCenter.Value} no existe");

                ValidateCodeHierarchyConsistency(costCenter.Code, parent.Code);

                if (await WouldCreateCycleAsync(companyId, costCenter.IdCostCenter, costCenter.IdParentCostCenter.Value))
                    throw new InvalidOperationException("No se puede asignar el padre especificado porque crearía un ciclo en la jerarquía");
            }

            // Validar fechas de vigencia
            if (costCenter.ValidTo.HasValue && costCenter.ValidTo.Value < costCenter.ValidFrom)
                throw new InvalidOperationException("La fecha 'válido hasta' no puede ser anterior a la fecha 'válido desde'");

            // Validar presupuesto
            if (costCenter.AnnualBudget.HasValue && costCenter.AnnualBudget.Value < 0)
                throw new InvalidOperationException("El presupuesto anual no puede ser negativo");

            // Actualizar campos
            existing.Code = costCenter.Code;
            existing.Name = costCenter.Name;
            existing.Description = costCenter.Description;
            existing.IdParentCostCenter = costCenter.IdParentCostCenter;
            existing.CostCenterType = costCenter.CostCenterType;
            existing.Category = costCenter.Category;
            existing.ResponsibleUserId = costCenter.ResponsibleUserId;
            existing.ResponsibleName = costCenter.ResponsibleName;
            existing.Location = costCenter.Location;
            existing.Department = costCenter.Department;
            existing.Division = costCenter.Division;
            existing.ValidFrom = costCenter.ValidFrom;
            existing.ValidTo = costCenter.ValidTo;
            existing.AnnualBudget = costCenter.AnnualBudget;
            existing.BudgetCurrency = costCenter.BudgetCurrency;
            existing.AllowOverBudget = costCenter.AllowOverBudget;
            existing.IsPostingAllowed = costCenter.IsPostingAllowed;
            existing.IsBlocked = costCenter.IsBlocked;
            existing.IsActive = costCenter.IsActive;
            existing.ProfitCenterCode = costCenter.ProfitCenterCode;
            existing.BusinessAreaCode = costCenter.BusinessAreaCode;
            existing.CompanyCode = costCenter.CompanyCode;
            existing.Notes = costCenter.Notes;

            await context.SaveChangesAsync();

            _logger.LogInformation("Cost center {Code} updated for company {CompanyId}", costCenter.Code, companyId);
            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cost center {IdCostCenter} for company {CompanyId}", costCenter.IdCostCenter, companyId);
            throw;
        }
    }

    public async Task<bool> DeleteCostCenterAsync(int companyId, int idCostCenter)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var costCenter = await context.Set<CostCenter>()
                .FirstOrDefaultAsync(cc => cc.IdCostCenter == idCostCenter);

            if (costCenter == null)
                return false;

            // Validar que no tenga hijos
            if (await HasChildrenAsync(companyId, idCostCenter))
                throw new InvalidOperationException("No se puede eliminar el centro de costo porque tiene centros hijos");

            // Validar que no tenga transacciones
            if (await HasTransactionsAsync(companyId, idCostCenter))
                throw new InvalidOperationException("No se puede eliminar el centro de costo porque tiene transacciones asociadas");

            context.Set<CostCenter>().Remove(costCenter);
            await context.SaveChangesAsync();

            _logger.LogInformation("Cost center {Code} deleted for company {CompanyId}", costCenter.Code, companyId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cost center {IdCostCenter} for company {CompanyId}", idCostCenter, companyId);
            throw;
        }
    }

    public async Task<bool> CodeExistsAsync(int companyId, string code, int? excludeId = null)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var query = context.Set<CostCenter>().Where(cc => cc.Code == code);

            if (excludeId.HasValue)
                query = query.Where(cc => cc.IdCostCenter != excludeId.Value);

            return await query.AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if code exists for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<bool> HasChildrenAsync(int companyId, int idCostCenter)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            return await context.Set<CostCenter>()
                .AnyAsync(cc => cc.IdParentCostCenter == idCostCenter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if cost center has children for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<bool> HasTransactionsAsync(int companyId, int idCostCenter)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            // Verificar en journal_entry_line (ahora usa FK real id_cost_center)
            var hasJournalEntries = await context.Set<JournalEntryLine>()
                .AnyAsync(jel => jel.IdCostCenter == idCostCenter);

            return hasJournalEntries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if cost center has transactions for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<List<CostCenter>> GetValidCostCentersAsync(int companyId, DateTime? asOfDate = null)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);
            var date = asOfDate ?? DateTime.UtcNow.Date;

            return await context.Set<CostCenter>()
                .Where(cc => cc.IsActive 
                    && !cc.IsBlocked
                    && cc.ValidFrom <= date 
                    && (cc.ValidTo == null || cc.ValidTo >= date))
                .OrderBy(cc => cc.Code)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting valid cost centers for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<List<CostCenter>> GetChildrenAsync(int companyId, int idParentCostCenter)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            return await context.Set<CostCenter>()
                .Where(cc => cc.IdParentCostCenter == idParentCostCenter)
                .OrderBy(cc => cc.Code)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting children for cost center {IdParentCostCenter} in company {CompanyId}", idParentCostCenter, companyId);
            throw;
        }
    }

    // ===== MÉTODOS PRIVADOS DE VALIDACIÓN =====

    private void ValidateHierarchicalCode(string code)
    {
        // Validar formato X-XX-XX-XX-XX
        var parts = code.Split('-');
        if (parts.Length != 5)
            throw new InvalidOperationException($"El código '{code}' debe tener el formato X-XX-XX-XX-XX (5 segmentos separados por guión)");

        // Validar que cada segmento sea numérico
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out _))
                throw new InvalidOperationException($"El código '{code}' debe contener solo números en cada segmento");
        }

        // Validar longitudes
        if (parts[0].Length > 2 || parts[1].Length != 2 || parts[2].Length != 2 || parts[3].Length != 2 || parts[4].Length != 2)
            throw new InvalidOperationException($"El código '{code}' debe tener el formato X-XX-XX-XX-XX (primer segmento 1-2 dígitos, resto 2 dígitos)");
    }

    private void ValidateCodeHierarchyConsistency(string childCode, string parentCode)
    {
        // El código del hijo debe comenzar con el prefijo del padre
        // Ejemplo: padre "1-00-00-00-00", hijo debe ser "1-XX-00-00-00"
        var parentParts = parentCode.Split('-');
        var childParts = childCode.Split('-');

        // Encontrar el nivel del padre (primer segmento no-00)
        int parentLevel = 0;
        for (int i = 0; i < parentParts.Length; i++)
        {
            if (parentParts[i] != "00")
                parentLevel = i;
        }

        // El hijo debe coincidir con el padre en todos los segmentos hasta el nivel del padre
        for (int i = 0; i <= parentLevel; i++)
        {
            if (childParts[i] != parentParts[i])
                throw new InvalidOperationException($"El código del centro hijo '{childCode}' no es consistente con el código del padre '{parentCode}'");
        }

        // El hijo debe tener al menos un nivel más que el padre
        if (childParts[parentLevel + 1] == "00")
            throw new InvalidOperationException($"El código del centro hijo '{childCode}' debe tener un nivel jerárquico mayor que el padre '{parentCode}'");
    }

    private async Task<bool> WouldCreateCycleAsync(int companyId, int costCenterId, int newParentId)
    {
        // Si el nuevo padre es el mismo centro de costo, es un ciclo directo
        if (costCenterId == newParentId)
            return true;

        await using var context = await _contextFactory.CreateDbContextAsync(companyId);

        // Recorrer hacia arriba desde el nuevo padre para ver si encontramos el centro de costo
        var currentParentId = newParentId;
        var visited = new HashSet<int> { costCenterId };

        while (currentParentId != 0)
        {
            if (visited.Contains(currentParentId))
                return true; // Ciclo detectado

            visited.Add(currentParentId);

            var parent = await context.Set<CostCenter>()
                .FirstOrDefaultAsync(cc => cc.IdCostCenter == currentParentId);

            if (parent?.IdParentCostCenter == null)
                break;

            currentParentId = parent.IdParentCostCenter.Value;
        }

        return false;
    }
}
