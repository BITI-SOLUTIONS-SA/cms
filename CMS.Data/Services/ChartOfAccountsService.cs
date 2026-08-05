// ================================================================================
// ARCHIVO: CMS.Data/Services/ChartOfAccountsService.cs
// PROPÓSITO: Servicio para gestión del Plan de Cuentas (Chart of Accounts)
// DESCRIPCIÓN: Implementa lógica de negocio para el catálogo de cuentas contables:
//              - CRUD de cuentas con validación de jerarquía
//              - Validación de que solo cuentas de detalle acepten transacciones
//              - Construcción de árbol jerárquico
//              - Validación de unicidad de códigos
//              - Control de cuentas con saldo (no se pueden borrar)
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-20
// ================================================================================

using CMS.Entities.Operational;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services
{
    public interface IChartOfAccountsService
    {
        Task<(List<ChartOfAccounts> Items, int TotalCount)> GetAccountsAsync(
            int companyId,
            string? search = null,
            int? idChartOfAccountsType = null,
            bool? isDetail = null,
            bool? isActive = null,
            int page = 1,
            int pageSize = 50);

        Task<ChartOfAccounts?> GetAccountByIdAsync(int companyId, int idAccount);
        Task<ChartOfAccounts?> GetAccountByCodeAsync(int companyId, string accountCode);
        Task<List<ChartOfAccounts>> GetAccountHierarchyAsync(int companyId, int? idParentAccount = null);
        Task<List<ChartOfAccounts>> GetDetailAccountsAsync(int companyId);
        Task<ChartOfAccounts> CreateAccountAsync(int companyId, ChartOfAccounts account);
        Task<ChartOfAccounts> UpdateAccountAsync(int companyId, ChartOfAccounts account);
        Task DeleteAccountAsync(int companyId, int idAccount);
        Task<bool> CodeExistsAsync(int companyId, string accountCode, int? excludeId = null);
        Task<bool> HasTransactionsAsync(int companyId, int idAccount);
    }

    public class ChartOfAccountsService : IChartOfAccountsService
    {
        private readonly ICompanyDbContextFactory _dbContextFactory;
        private readonly ILogger<ChartOfAccountsService> _logger;

        public ChartOfAccountsService(
            ICompanyDbContextFactory dbContextFactory,
            ILogger<ChartOfAccountsService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        // ================================================================
        // CONSULTAS
        // ================================================================

        public async Task<(List<ChartOfAccounts> Items, int TotalCount)> GetAccountsAsync(
            int companyId,
            string? search = null,
            int? idChartOfAccountsType = null,
            bool? isDetail = null,
            bool? isActive = null,
            int page = 1,
            int pageSize = 50)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var query = db.Set<ChartOfAccounts>().AsQueryable();

            // Filtros
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(a =>
                    a.Code.ToLower().Contains(searchLower) ||
                    a.Name.ToLower().Contains(searchLower) ||
                    (a.Description != null && a.Description.ToLower().Contains(searchLower)));
            }

            if (idChartOfAccountsType.HasValue)
                query = query.Where(a => a.IdChartOfAccountsType == idChartOfAccountsType.Value);

            if (isDetail.HasValue)
                query = query.Where(a => a.IsDetail == isDetail.Value);

            if (isActive.HasValue)
                query = query.Where(a => a.IsActive == isActive.Value);

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(a => a.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<ChartOfAccounts?> GetAccountByIdAsync(int companyId, int idChartOfAccounts)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await db.Set<ChartOfAccounts>()
                .FirstOrDefaultAsync(a => a.IdChartOfAccounts == idChartOfAccounts);
        }

        public async Task<ChartOfAccounts?> GetAccountByCodeAsync(int companyId, string code)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await db.Set<ChartOfAccounts>()
                .FirstOrDefaultAsync(a => a.Code == code);
        }

        /// <summary>
        /// Obtiene la jerarquía completa de cuentas o las subcuentas de un padre específico
        /// </summary>
        public async Task<List<ChartOfAccounts>> GetAccountHierarchyAsync(int companyId, int? idParentAccount = null)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var query = db.Set<ChartOfAccounts>().AsQueryable();

            if (idParentAccount.HasValue)
                query = query.Where(a => a.IdParentAccount == idParentAccount.Value);
            else
                query = query.Where(a => a.IdParentAccount == null); // Cuentas de primer nivel

            return await query
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Code)
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene solo las cuentas de detalle (que aceptan transacciones)
        /// </summary>
        public async Task<List<ChartOfAccounts>> GetDetailAccountsAsync(int companyId)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await db.Set<ChartOfAccounts>()
                .Where(a => a.IsDetail == true && a.IsActive == true && a.IsBlocked == false)
                .OrderBy(a => a.Code)
                .ToListAsync();
        }

        // ================================================================
        // CREACIÓN
        // ================================================================

        public async Task<ChartOfAccounts> CreateAccountAsync(int companyId, ChartOfAccounts account)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            // Validar código único
            if (await CodeExistsAsync(companyId, account.Code))
                throw new InvalidOperationException($"El código de cuenta '{account.Code}' ya existe.");

            // Validar cuenta padre si existe
            if (account.IdParentAccount.HasValue)
            {
                var parent = await db.Set<ChartOfAccounts>()
                    .FirstOrDefaultAsync(a => a.IdChartOfAccounts == account.IdParentAccount.Value);

                if (parent == null)
                    throw new InvalidOperationException("La cuenta padre no existe.");

                if (parent.IsDetail)
                    throw new InvalidOperationException("No se pueden crear subcuentas bajo una cuenta de detalle.");

                // Calcular nivel automáticamente
                account.AccountLevel = parent.AccountLevel + 1;

                if (account.AccountLevel > 6)
                    throw new InvalidOperationException("Se ha alcanzado el máximo de 6 niveles de jerarquía.");
            }

            // Validar is_header vs is_detail
            if (account.IsHeader && account.IsDetail)
                throw new InvalidOperationException("Una cuenta no puede ser de encabezado y de detalle al mismo tiempo.");

            if (!account.IsHeader && !account.IsDetail)
                throw new InvalidOperationException("Una cuenta debe ser de encabezado o de detalle.");

            db.Set<ChartOfAccounts>().Add(account);
            await db.SaveChangesAsync();

            _logger.LogInformation($"Cuenta {account.Code} - {account.Name} creada exitosamente.");
            return account;
        }

        // ================================================================
        // ACTUALIZACIÓN
        // ================================================================

        public async Task<ChartOfAccounts> UpdateAccountAsync(int companyId, ChartOfAccounts account)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var existing = await db.Set<ChartOfAccounts>()
                .FirstOrDefaultAsync(a => a.IdChartOfAccounts == account.IdChartOfAccounts);

            if (existing == null)
                throw new InvalidOperationException("La cuenta no existe.");

            // Validar código único (excluyendo la cuenta actual)
            if (existing.Code != account.Code &&
                await CodeExistsAsync(companyId, account.Code, account.IdChartOfAccounts))
                throw new InvalidOperationException($"El código de cuenta '{account.Code}' ya existe.");

            // Validar que no se cambie de detalle a encabezado si tiene transacciones
            if (existing.IsDetail && account.IsHeader)
            {
                if (await HasTransactionsAsync(companyId, account.IdChartOfAccounts))
                    throw new InvalidOperationException("No se puede cambiar una cuenta de detalle a encabezado si tiene transacciones.");
            }

            // Validar que no se cambie de encabezado a detalle si tiene hijos
            if (existing.IsHeader && account.IsDetail && existing.HasChildren)
                throw new InvalidOperationException("No se puede cambiar una cuenta de encabezado a detalle si tiene subcuentas.");

            // Validar cuenta padre
            if (account.IdParentAccount.HasValue && account.IdParentAccount != existing.IdParentAccount)
            {
                var parent = await db.Set<ChartOfAccounts>()
                    .FirstOrDefaultAsync(a => a.IdChartOfAccounts == account.IdParentAccount.Value);

                if (parent == null)
                    throw new InvalidOperationException("La cuenta padre no existe.");

                if (parent.IsDetail)
                    throw new InvalidOperationException("No se pueden mover cuentas bajo una cuenta de detalle.");

                // Validar que no se cree un ciclo
                if (await WouldCreateCycle(db, account.IdChartOfAccounts, account.IdParentAccount.Value))
                    throw new InvalidOperationException("No se puede mover la cuenta: se crearía un ciclo en la jerarquía.");

                account.AccountLevel = parent.AccountLevel + 1;
            }

            // Actualizar campos
            existing.Code = account.Code;
            existing.Name = account.Name;
            existing.Description = account.Description;
            existing.Alias = account.Alias;
            existing.IdParentAccount = account.IdParentAccount;
            existing.AccountLevel = account.AccountLevel;
            existing.IsHeader = account.IsHeader;
            existing.IsDetail               = account.IsDetail;
            existing.IdChartOfAccountsType  = account.IdChartOfAccountsType;
            existing.AccountClass           = account.AccountClass;
            existing.NormalBalance = account.NormalBalance;
            existing.IsDebitBalance = account.IsDebitBalance;
            existing.AcceptsManualEntry = account.AcceptsManualEntry;
            existing.AcceptsAutoEntry = account.AcceptsAutoEntry;
            existing.RequiresCostCenter = account.RequiresCostCenter;
            existing.RequiresProject = account.RequiresProject;
            existing.RequiresPartner = account.RequiresPartner;
            existing.CurrencyCode = account.CurrencyCode;
            existing.AllowsMultiCurrency = account.AllowsMultiCurrency;
            existing.IsReconciliation = account.IsReconciliation;
            existing.TaxCode = account.TaxCode;
            existing.IsTaxRelevant = account.IsTaxRelevant;
            existing.IsReceivable = account.IsReceivable;
            existing.IsPayable = account.IsPayable;
            existing.CashFlowCategory = account.CashFlowCategory;
            existing.FinancialStatement = account.FinancialStatement;
            existing.ReportLineItem = account.ReportLineItem;
            existing.SortOrder = account.SortOrder;
            existing.EffectiveDate = account.EffectiveDate;
            existing.ExpirationDate = account.ExpirationDate;
            existing.IsActive = account.IsActive;
            existing.IsBlocked = account.IsBlocked;
            existing.BlockReason = account.BlockReason;
            existing.Notes = account.Notes;

            await db.SaveChangesAsync();

            _logger.LogInformation($"Cuenta {existing.Code} - {existing.Name} actualizada exitosamente.");
            return existing;
        }

        // ================================================================
        // ELIMINACIÓN
        // ================================================================

        public async Task DeleteAccountAsync(int companyId, int idChartOfAccounts)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var account = await db.Set<ChartOfAccounts>()
                .FirstOrDefaultAsync(a => a.IdChartOfAccounts == idChartOfAccounts);

            if (account == null)
                throw new InvalidOperationException("La cuenta no existe.");

            // Validar que no tenga subcuentas
            if (account.HasChildren)
                throw new InvalidOperationException("No se puede eliminar una cuenta que tiene subcuentas.");

            // Validar que no tenga transacciones
            if (await HasTransactionsAsync(companyId, idChartOfAccounts))
                throw new InvalidOperationException("No se puede eliminar una cuenta que tiene transacciones contables.");

            db.Set<ChartOfAccounts>().Remove(account);
            await db.SaveChangesAsync();

            _logger.LogInformation($"Cuenta {account.Code} - {account.Name} eliminada exitosamente.");
        }

        // ================================================================
        // VALIDACIONES
        // ================================================================

        public async Task<bool> CodeExistsAsync(int companyId, string code, int? excludeId = null)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var query = db.Set<ChartOfAccounts>()
                .Where(a => a.Code == code);

            if (excludeId.HasValue)
                query = query.Where(a => a.IdChartOfAccounts != excludeId.Value);

            return await query.AnyAsync();
        }

        public async Task<bool> HasTransactionsAsync(int companyId, int idChartOfAccounts)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            // Verificar si existen líneas de asiento con esta cuenta
            return await db.Set<JournalEntryLine>()
                .AnyAsync(l => l.IdChartOfAccounts == idChartOfAccounts);
        }

        /// <summary>
        /// Valida que mover una cuenta no cree un ciclo en la jerarquía
        /// </summary>
        private async Task<bool> WouldCreateCycle(CompanyDbContext db, int accountId, int newParentId)
        {
            var currentParentId = newParentId;

            while (currentParentId != null)
            {
                if (currentParentId == accountId)
                    return true; // Ciclo detectado

                var parent = await db.Set<ChartOfAccounts>()
                    .FirstOrDefaultAsync(a => a.IdChartOfAccounts == currentParentId);

                if (parent == null || parent.IdParentAccount == null)
                    break;

                currentParentId = parent.IdParentAccount.Value;
            }

            return false;
        }
    }
}
