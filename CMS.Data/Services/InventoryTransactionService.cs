// ================================================================================
// ARCHIVO: CMS.Data/Services/InventoryTransactionService.cs
// PROPÓSITO: Implementación del servicio de movimientos de inventario
// DESCRIPCIÓN: Gestiona TODOS los movimientos de inventario:
//              - Traslados simples (Transfer)
//              - Traslados vía bodega de tránsito (TransitTransfer)
//              - Ajustes, entradas, salidas
//              Actualiza el saldo en existence_warehouse al confirmar/completar.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-13
// ================================================================================

using CMS.Entities;
using CMS.Entities.Operational;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services
{
    public class NewReturnLineDto
    {
        public int ItemId { get; set; }
        public decimal QtyReturned { get; set; }
    }

    public class InventoryTransactionService : IInventoryTransactionService
    {
        private readonly ICompanyDbContextFactory _dbContextFactory;
        private readonly AppDbContext _centralDbContext;
        private readonly ILogger<InventoryTransactionService> _logger;

        public InventoryTransactionService(
            ICompanyDbContextFactory dbContextFactory,
            AppDbContext centralDbContext,
            ILogger<InventoryTransactionService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _centralDbContext = centralDbContext;
            _logger = logger;
        }

        // ================================================================
        // CONSULTAS
        // ================================================================

        public async Task<(List<InventoryTransaction> Items, int TotalCount)> GetTransactionsAsync(
            int companyId,
            string? search = null,
            int? idInventoryTransactionType = null,
            int? idInventoryTransactionStatus = null,
            int? warehouseOriginId = null,
            int? warehouseDestId = null,
            DateOnly? dateFrom = null,
            DateOnly? dateTo = null,
            int page = 1,
            int pageSize = 20)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var query = db.InventoryTransactions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(t =>
                    t.TransactionNumber.ToLower().Contains(s) ||
                    (t.Reference != null && t.Reference.ToLower().Contains(s)) ||
                    (t.Notes != null && t.Notes.ToLower().Contains(s)));
            }

            if (idInventoryTransactionType.HasValue && idInventoryTransactionType.Value > 0)
                query = query.Where(t => t.IdInventoryTransactionType == idInventoryTransactionType.Value);

            if (idInventoryTransactionStatus.HasValue && idInventoryTransactionStatus.Value > 0)
                query = query.Where(t => t.IdInventoryTransactionStatus == idInventoryTransactionStatus.Value);

            if (warehouseOriginId.HasValue)
                query = query.Where(t => t.IdWarehouseOrigin == warehouseOriginId.Value);

            if (warehouseDestId.HasValue)
                query = query.Where(t => t.IdWarehouseDest == warehouseDestId.Value);

            if (dateFrom.HasValue)
                query = query.Where(t => t.TransactionDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(t => t.TransactionDate <= dateTo.Value);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        /// <summary>
        /// Obtiene los conteos por estado aplicando los mismos filtros que GetTransactionsAsync
        /// (excepto el filtro de estado, para obtener conteos de TODOS los estados).
        /// Devuelve un diccionario: { idInventoryTransactionStatus: count }
        /// </summary>
        public async Task<Dictionary<int, int>> GetStatusCountsAsync(
            int companyId,
            string? search = null,
            int? idInventoryTransactionType = null,
            int? warehouseOriginId = null,
            int? warehouseDestId = null,
            DateOnly? dateFrom = null,
            DateOnly? dateTo = null)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var query = db.InventoryTransactions.AsQueryable();

            // Aplicar los mismos filtros que GetTransactionsAsync (EXCEPTO el filtro de estado)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(t =>
                    t.TransactionNumber.ToLower().Contains(s) ||
                    (t.Reference != null && t.Reference.ToLower().Contains(s)) ||
                    (t.Notes != null && t.Notes.ToLower().Contains(s)));
            }

            if (idInventoryTransactionType.HasValue && idInventoryTransactionType.Value > 0)
                query = query.Where(t => t.IdInventoryTransactionType == idInventoryTransactionType.Value);

            if (warehouseOriginId.HasValue)
                query = query.Where(t => t.IdWarehouseOrigin == warehouseOriginId.Value);

            if (warehouseDestId.HasValue)
                query = query.Where(t => t.IdWarehouseDest == warehouseDestId.Value);

            if (dateFrom.HasValue)
                query = query.Where(t => t.TransactionDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(t => t.TransactionDate <= dateTo.Value);

            // Agrupar por estado y contar
            var counts = await query
                .GroupBy(t => t.IdInventoryTransactionStatus)
                .Select(g => new { StatusId = g.Key, Count = g.Count() })
                .ToListAsync();

            return counts.ToDictionary(x => x.StatusId, x => x.Count);
        }

        public async Task<InventoryTransaction?> GetByIdAsync(int companyId, int id)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await db.InventoryTransactions
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<InventoryTransactionLine>> GetLinesAsync(int companyId, int transactionId)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await db.InventoryTransactionLines
                .Where(l => l.IdInventoryTransaction == transactionId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync();
        }

        public async Task<List<InventoryTransactionWarehouseTransit>> GetTransitGroupsAsync(int companyId, int transactionId)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await db.InventoryTransactionWarehouseTransits
                .Where(g => g.IdInventoryTransaction == transactionId)
                .OrderBy(g => g.LineNumber)
                .ToListAsync();
        }

        public async Task<bool> TransactionNumberExistsAsync(int companyId, string transactionNumber, int? excludeId = null)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            var query = db.InventoryTransactions.Where(t => t.TransactionNumber == transactionNumber);
            if (excludeId.HasValue)
                query = query.Where(t => t.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<bool> SecuritySealExistsAsync(int companyId, string securitySeal, int? excludeId = null)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            var query = db.InventoryTransactions
                .Where(t => t.SecuritySeal != null && t.SecuritySeal == securitySeal);
            if (excludeId.HasValue)
                query = query.Where(t => t.Id != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<bool> AnySealExistsAsync(int companyId, string seal, int? excludeTransactionId = null)
        {
            if (string.IsNullOrWhiteSpace(seal)) return false;
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            seal = seal.Trim();

            // Sellos en encabezado
            var headerQuery = db.InventoryTransactions
                .Where(t => t.SecuritySeal != null && t.SecuritySeal == seal);
            if (excludeTransactionId.HasValue)
                headerQuery = headerQuery.Where(t => t.Id != excludeTransactionId.Value);
            if (await headerQuery.AnyAsync()) return true;

            // Sellos destino en grupos de tránsito
            var groupQuery = db.InventoryTransactionWarehouseTransits
                .Where(g => g.DestSecuritySeal != null && g.DestSecuritySeal == seal);
            if (excludeTransactionId.HasValue)
                groupQuery = groupQuery.Where(g => g.IdInventoryTransaction != excludeTransactionId.Value);
            return await groupQuery.AnyAsync();
        }

        // ================================================================
        // GENERACIÓN DE NÚMERO
        // ================================================================

        public async Task<string> GenerateTransactionNumberAsync(int companyId)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            var year = DateTime.Today.Year;
            var prefix = $"INV-{year}-";

            var lastNumber = await db.InventoryTransactions
                .Where(t => t.TransactionNumber.StartsWith(prefix))
                .OrderByDescending(t => t.Id)
                .Select(t => t.TransactionNumber)
                .FirstOrDefaultAsync();

            int seq = 1;
            if (lastNumber != null)
            {
                var parts = lastNumber.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out var n))
                    seq = n + 1;
            }

            return $"{prefix}{seq:D5}";
        }

        // ================================================================
        // CRUD
        // ================================================================

        /// <summary>
        /// Returns true if the given transit warehouse has any active (non-Completed, non-Cancelled)
        /// TransitTransfer movement. An optional excludeTransactionId can be passed to ignore the
        /// current draft when editing (not used in Create, but available for future use).
        /// </summary>
        public async Task<(bool IsBusy, string? TransactionNumber)> CheckTransitWarehouseBusyAsync(
            int companyId, int warehouseId, int? excludeTransactionId = null)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var busyStatuses = new[]
            {
                InventoryTransactionStatusCode.IdDraft,
                InventoryTransactionStatusCode.IdConfirmed,
                InventoryTransactionStatusCode.IdInTransit,
                InventoryTransactionStatusCode.IdPartiallyReceived,
            };

            var conflict = await db.InventoryTransactions
                .Where(t => t.IsTransitTransfer
                         && t.IdWarehouseDest == warehouseId
                         && busyStatuses.Contains(t.IdInventoryTransactionStatus)
                         && (excludeTransactionId == null || t.Id != excludeTransactionId))
                .Select(t => t.TransactionNumber)
                .FirstOrDefaultAsync();

            return (conflict != null, conflict);
        }

        public async Task<InventoryTransaction> CreateAsync(
            int companyId,
            InventoryTransaction transaction,
            List<InventoryTransactionLine> lines,
            string createdBy,
            int userId,
            Dictionary<int, string?>? groupNotes = null)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            // Server-side guard: transit warehouse must not have any active movement
            if (transaction.IsTransitTransfer && transaction.IdWarehouseDest.HasValue)
            {
                var (isBusy, conflictNum) = await CheckTransitWarehouseBusyAsync(
                    companyId, transaction.IdWarehouseDest.Value);
                if (isBusy)
                    throw new InvalidOperationException(
                        $"La bodega de tránsito seleccionada ya tiene un movimiento activo ({conflictNum}). " +
                        "Todos los movimientos asignados a esa bodega deben estar en estado Completado antes de crear uno nuevo.");
            }

            if (string.IsNullOrWhiteSpace(transaction.TransactionNumber))
                transaction.TransactionNumber = await GenerateTransactionNumberAsync(companyId);

            // Truncar a 30 chars para respetar el límite de la columna de auditoría
            var auditUser = (createdBy?.Length > 30 ? createdBy[..30] : createdBy) ?? "system";

            transaction.IdInventoryTransactionStatus = InventoryTransactionStatusCode.IdDraft;
            transaction.CreatedBy = auditUser;
            transaction.UpdatedBy = auditUser;
            transaction.CreatedByUserId = userId;
            transaction.CreateDate = DateTime.UtcNow;
            transaction.RecordDate = DateTime.UtcNow;
            transaction.Rowpointer = Guid.NewGuid();
            transaction.AffectsStock = false;

            db.InventoryTransactions.Add(transaction);
            await db.SaveChangesAsync();

            if (transaction.IsTransitTransfer)
            {
                // Agrupar líneas por bodega destino para crear los grupos de tránsito
                // Preserve group order based on the sequence of lines provided by the client
                var destGroups = lines
                    .GroupBy(l => l.IdWarehouseDestLine ?? 0)
                    .ToList();

                int groupNum = 1;
                int lineNumGlobal = 1;
                foreach (var grp in destGroups)
                {
                    var destKey = grp.Key == 0 ? (int?)null : grp.Key;
                    string? grpNotes = null;
                    groupNotes?.TryGetValue(grp.Key, out grpNotes);
                    var transit = new InventoryTransactionWarehouseTransit
                    {
                        IdInventoryTransaction = transaction.Id,
                        LineNumber             = groupNum++,
                        IdWarehouseOriginLine  = transaction.IdWarehouseOrigin,
                        IdWarehouseDestLine    = destKey,
                        LineStatus             = "Pending",
                        Notes      = string.IsNullOrWhiteSpace(grpNotes) ? null : grpNotes.Trim(),
                        CreatedBy  = auditUser,
                        UpdatedBy  = auditUser,
                        CreateDate = DateTime.UtcNow,
                        RecordDate = DateTime.UtcNow,
                        Rowpointer = Guid.NewGuid()
                    };
                    db.InventoryTransactionWarehouseTransits.Add(transit);
                    await db.SaveChangesAsync(); // necesitamos el ID del grupo

                    foreach (var line in grp)
                    {
                        line.IdInventoryTransaction = transaction.Id;
                        line.IdInventoryTransactionWarehouseTransit = transit.Id;
                        line.LineNumber = lineNumGlobal++;
                        line.CreatedBy  = auditUser;
                        line.UpdatedBy  = auditUser;
                        line.CreateDate = DateTime.UtcNow;
                        line.RecordDate = DateTime.UtcNow;
                        line.Rowpointer = Guid.NewGuid();
                        db.InventoryTransactionLines.Add(line);
                    }
                }
            }
            else
            {
                // Movimiento simple: guardar líneas sin grupo
                int lineNum = 1;
                foreach (var line in lines)
                {
                    line.IdInventoryTransaction = transaction.Id;
                    line.LineNumber = lineNum++;
                    line.CreatedBy  = auditUser;
                    line.UpdatedBy  = auditUser;
                    line.CreateDate = DateTime.UtcNow;
                    line.RecordDate = DateTime.UtcNow;
                    line.Rowpointer = Guid.NewGuid();
                    db.InventoryTransactionLines.Add(line);
                }
            }

            await db.SaveChangesAsync();

            _logger.LogInformation("Movimiento {Num} creado para compañía {CompanyId}", transaction.TransactionNumber, companyId);
            return transaction;
        }

        public async Task<InventoryTransaction> UpdateAsync(int companyId, InventoryTransaction transaction, string updatedBy)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var existing = await db.InventoryTransactions.FirstOrDefaultAsync(t => t.Id == transaction.Id)
                ?? throw new InvalidOperationException($"Movimiento {transaction.Id} no encontrado");

            if (existing.IdInventoryTransactionStatus != InventoryTransactionStatusCode.IdDraft)
                throw new InvalidOperationException("Solo se pueden editar movimientos en estado Draft");

            existing.IdInventoryTransactionType = transaction.IdInventoryTransactionType;
            existing.IdMenu = transaction.IdMenu;  // ✅ Actualizar IdMenu
            existing.IdWarehouseOrigin = transaction.IdWarehouseOrigin;
            existing.IdWarehouseDest = transaction.IdWarehouseDest;
            existing.Reference = transaction.Reference;
            existing.Notes = transaction.Notes;
            existing.TransactionDate = transaction.TransactionDate;
            existing.ExpectedArrivalDate = transaction.ExpectedArrivalDate;
            existing.IsTransitTransfer = transaction.IsTransitTransfer;
            existing.SecuritySeal = transaction.SecuritySeal;
            existing.DepartureTime = transaction.DepartureTime;
            existing.OdometerOut = transaction.OdometerOut;
            existing.UpdatedBy = (updatedBy?.Length > 30 ? updatedBy[..30] : updatedBy) ?? "system";
            existing.RecordDate = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return existing;
        }

        public async Task SaveLinesAsync(int companyId, int transactionId, List<InventoryTransactionLine> lines, string updatedBy, Dictionary<int, string?>? groupNotes = null)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var existing = await db.InventoryTransactions.FirstOrDefaultAsync(t => t.Id == transactionId)
                ?? throw new InvalidOperationException("Movimiento no encontrado");

            if (existing.IdInventoryTransactionStatus != InventoryTransactionStatusCode.IdDraft)
                throw new InvalidOperationException("Solo se pueden editar líneas en estado Draft");

            var oldLines = await db.InventoryTransactionLines
                .Where(l => l.IdInventoryTransaction == transactionId)
                .ToListAsync();
            db.InventoryTransactionLines.RemoveRange(oldLines);

            var oldGroups = await db.InventoryTransactionWarehouseTransits
                .Where(g => g.IdInventoryTransaction == transactionId)
                .ToListAsync();
            db.InventoryTransactionWarehouseTransits.RemoveRange(oldGroups);

            await db.SaveChangesAsync();

            int lineNumGlobal = 1;
            var auditUser = updatedBy?.Length > 30 ? updatedBy[..30] : updatedBy ?? "SYSTEM";

            if (existing.IsTransitTransfer)
            {
                // Preserve group order based on the sequence of lines provided by the client
                var destGroups = lines
                    .GroupBy(l => l.IdWarehouseDestLine ?? 0)
                    .ToList();

                int groupNum = 1;
                foreach (var grp in destGroups)
                {
                    string? grpNotes = null;
                    groupNotes?.TryGetValue(grp.Key, out grpNotes);
                    var transit = new InventoryTransactionWarehouseTransit
                    {
                        IdInventoryTransaction = transactionId,
                        LineNumber             = groupNum++,
                        IdWarehouseOriginLine  = existing.IdWarehouseOrigin,
                        IdWarehouseDestLine    = grp.Key == 0 ? null : grp.Key,
                        LineStatus             = "Pending",
                        Notes      = string.IsNullOrWhiteSpace(grpNotes) ? null : grpNotes.Trim(),
                        CreatedBy  = auditUser,
                        UpdatedBy  = auditUser,
                        CreateDate = DateTime.UtcNow,
                        RecordDate = DateTime.UtcNow,
                        Rowpointer = Guid.NewGuid()
                    };
                    db.InventoryTransactionWarehouseTransits.Add(transit);
                    await db.SaveChangesAsync();

                    int lineNum = 1;
                    foreach (var line in grp)
                    {
                        line.Id = 0;
                        line.IdInventoryTransaction = transactionId;
                        line.IdInventoryTransactionWarehouseTransit = transit.Id;
                        line.LineNumber = lineNumGlobal++;
                        line.CreatedBy  = auditUser;
                        line.UpdatedBy  = auditUser;
                        line.CreateDate = DateTime.UtcNow;
                        line.RecordDate = DateTime.UtcNow;
                        line.Rowpointer = Guid.NewGuid();
                        db.InventoryTransactionLines.Add(line);
                        lineNum++;
                    }
                }
            }
            else
            {
                foreach (var line in lines)
                {
                    line.Id = 0;
                    line.IdInventoryTransaction = transactionId;
                    line.LineNumber = lineNumGlobal++;
                    line.CreatedBy  = auditUser;
                    line.UpdatedBy  = auditUser;
                    line.CreateDate = DateTime.UtcNow;
                    line.RecordDate = DateTime.UtcNow;
                    line.Rowpointer = Guid.NewGuid();
                    db.InventoryTransactionLines.Add(line);
                }
            }
            await db.SaveChangesAsync();
        }

        // ================================================================
        // FLUJO DE ESTADOS
        // ================================================================

        public async Task<InventoryTransaction> ConfirmAsync(int companyId, int transactionId, string updatedBy, int userId)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var txn = await db.InventoryTransactions.FirstOrDefaultAsync(t => t.Id == transactionId)
                ?? throw new InvalidOperationException("Movimiento no encontrado");

            if (txn.IdInventoryTransactionStatus != InventoryTransactionStatusCode.IdDraft)
                throw new InvalidOperationException("Solo se pueden confirmar movimientos en Draft");

            var lines = await db.InventoryTransactionLines
                .Where(l => l.IdInventoryTransaction == transactionId)
                .ToListAsync();

            if (!lines.Any())
                throw new InvalidOperationException("El movimiento no tiene líneas");

            var now = DateTime.UtcNow;
            var auditUser = (updatedBy?.Length > 30 ? updatedBy[..30] : updatedBy) ?? "SYSTEM";

            if (txn.IsTransitTransfer)
            {
                txn.IdInventoryTransactionStatus = InventoryTransactionStatusCode.IdInTransit;

                var groups = await db.InventoryTransactionWarehouseTransits
                    .Where(g => g.IdInventoryTransaction == transactionId)
                    .ToListAsync();

                foreach (var grp in groups)
                {
                    grp.LineStatus = "InTransit";
                    grp.RecordDate = now;
                    grp.UpdatedBy  = auditUser;
                }

                foreach (var line in lines)
                {
                    line.QtyDispatched = line.QtyRequested;
                    line.RecordDate    = now;
                    line.UpdatedBy     = auditUser;

                    // Reducir stock en bodega origen
                    await AdjustExistenceAsync(db, txn.IdWarehouseOrigin, line.IdItem, line.ItemCode,
                        -line.QtyDispatched, 0, 0, line.UnitCost ?? 0, transactionId, now, auditUser);

                    // Aumentar in_transit en bodega tránsito (destino del header)
                    if (txn.IdWarehouseDest.HasValue)
                        await AdjustExistenceAsync(db, txn.IdWarehouseDest.Value, line.IdItem, line.ItemCode,
                            0, 0, line.QtyDispatched, line.UnitCost ?? 0, transactionId, now, auditUser);
                }
            }
            else
            {
                // Movimiento simple: confirmar = ya despachado (espera recepción o completa directo)
                txn.IdInventoryTransactionStatus = InventoryTransactionStatusCode.IdConfirmed;

                foreach (var line in lines)
                {
                    line.QtyDispatched = line.QtyRequested;
                    line.RecordDate    = now;
                    line.UpdatedBy     = auditUser;

                    // Reducir en bodega origen
                    await AdjustExistenceAsync(db, txn.IdWarehouseOrigin, line.IdItem, line.ItemCode,
                        -line.QtyDispatched, 0, 0, line.UnitCost ?? 0, transactionId, now, auditUser);
                }
            }

            txn.ConfirmedDate = now;
            txn.ConfirmedByUserId = userId;
            txn.UpdatedBy = auditUser;
            txn.RecordDate = now;

            await db.SaveChangesAsync();
            return txn;
        }

        public async Task<InventoryTransaction> ReceiveLinesAsync(
            int companyId,
            int transactionId,
            List<int> lineIds,
            int receivedByUserId,
            string receivedBy,
            string? arrivalTime = null,
            string? departureTime = null,
            decimal? odometerOut = null,
            string? destSeal = null,
            int? nextWarehouseId = null,
            Dictionary<int, decimal>? lineQtys = null,
            string? signature = null,
            int? transitGroupId = null,
            Dictionary<int, decimal>? lineReturns = null,
            List<NewReturnLineDto>? newReturnLines = null,
            string? receiveNotes = null)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var txn = await db.InventoryTransactions.FirstOrDefaultAsync(t => t.Id == transactionId)
                ?? throw new InvalidOperationException("Movimiento no encontrado");

            if (txn.IdInventoryTransactionStatus != InventoryTransactionStatusCode.IdInTransit &&
                txn.IdInventoryTransactionStatus != InventoryTransactionStatusCode.IdPartiallyReceived &&
                txn.IdInventoryTransactionStatus != InventoryTransactionStatusCode.IdConfirmed)
                throw new InvalidOperationException("El movimiento no está en tránsito o confirmado");

            // Validate dest seal uniqueness (server-side guard)
            if (!string.IsNullOrWhiteSpace(destSeal))
            {
                var sealTrimmed = destSeal.Trim();
                var headerMatch = await db.InventoryTransactions
                    .AnyAsync(t => t.SecuritySeal == sealTrimmed && t.Id != transactionId);
                var groupMatch = await db.InventoryTransactionWarehouseTransits
                    .AnyAsync(g => g.DestSecuritySeal == sealTrimmed && g.IdInventoryTransaction != transactionId);
                if (headerMatch || groupMatch)
                    throw new InvalidOperationException($"El sello '{sealTrimmed}' ya está en uso. Debe ingresar un sello único.");
            }

            var allLines = await db.InventoryTransactionLines
                .Where(l => l.IdInventoryTransaction == transactionId)
                .ToListAsync();

            // Para TransitTransfer: localizar el grupo activo — priorizar transitGroupId enviado desde el cliente
            InventoryTransactionWarehouseTransit? activeGroup = null;
            if (txn.IsTransitTransfer)
            {
                if (transitGroupId.HasValue)
                {
                    activeGroup = await db.InventoryTransactionWarehouseTransits
                        .FirstOrDefaultAsync(g => g.Id == transitGroupId.Value && g.IdInventoryTransaction == transactionId);
                }
                // Fallback: inferir desde la primera línea recibida
                if (activeGroup == null && lineIds.Count > 0)
                {
                    var firstLine = allLines.FirstOrDefault(l => l.Id == lineIds.First());
                    if (firstLine?.IdInventoryTransactionWarehouseTransit != null)
                    {
                        activeGroup = await db.InventoryTransactionWarehouseTransits
                            .FirstOrDefaultAsync(g => g.Id == firstLine.IdInventoryTransactionWarehouseTransit.Value);
                    }
                }
            }

            var now = DateTime.UtcNow;
            var auditUser = (receivedBy?.Length > 30 ? receivedBy[..30] : receivedBy) ?? "SYSTEM";
            var parsedArrival   = TimeOnly.TryParse(arrivalTime,   out var at) ? at : (TimeOnly?)null;
            var parsedDeparture = TimeOnly.TryParse(departureTime, out var dt) ? dt : (TimeOnly?)null;

            // Hora Llegada must be strictly before Hora Salida (server-side guard)
            if (parsedArrival.HasValue && parsedDeparture.HasValue && parsedArrival.Value >= parsedDeparture.Value)
                throw new InvalidOperationException(
                    $"La Hora de Llegada ({parsedArrival.Value:HH:mm}) debe ser menor a la Hora de Salida ({parsedDeparture.Value:HH:mm}).");

            // Actualizar el grupo de tránsito activo con los datos de logística
            if (activeGroup != null)
            {
                activeGroup.LineStatus        = "Received";
                activeGroup.ReceivedDate      = now;
                activeGroup.ReceivedByUserId  = receivedByUserId;
                if (parsedArrival.HasValue)                         activeGroup.ArrivalTime      = parsedArrival;
                if (parsedDeparture.HasValue)                       activeGroup.DepartureTime    = parsedDeparture;
                if (odometerOut.HasValue)                           activeGroup.OdometerOut      = odometerOut;
                if (!string.IsNullOrWhiteSpace(signature))          activeGroup.Signature        = signature;
                if (!string.IsNullOrWhiteSpace(destSeal))
                    activeGroup.DestSecuritySeal = destSeal.Trim().Length > 50
                        ? destSeal.Trim()[..50] : destSeal.Trim();
                // Concatenar notas: preservar lo que se escribió al crear/editar y agregar las de recepción
                if (!string.IsNullOrWhiteSpace(receiveNotes))
                {
                    var prev = activeGroup.Notes;
                    activeGroup.Notes = string.IsNullOrWhiteSpace(prev)
                        ? receiveNotes.Trim()
                        : $"{prev}\n---\n{receiveNotes.Trim()}";
                }
                // Si no hay notas de recepción, se preservan las previas sin cambio
                activeGroup.UpdatedBy   = auditUser;
                activeGroup.RecordDate  = now;
            }

            foreach (var lineId in lineIds)
            {
                var line = allLines.FirstOrDefault(l => l.Id == lineId);
                if (line == null) continue;

                // Usar cantidad explícita si se proveyó
                line.QtyReceived = (lineQtys != null && lineQtys.TryGetValue(lineId, out var overrideQty) && overrideQty > 0)
                    ? overrideQty
                    : (line.QtyDispatched > 0 ? line.QtyDispatched : line.QtyRequested);

                // Guardar cantidad de devolución si se proveyó
                if (lineReturns != null && lineReturns.TryGetValue(lineId, out var returnQty))
                {
                    line.QtyReturned = returnQty;
                }

                line.RecordDate = now;
                line.UpdatedBy  = auditUser;

                // Destino final de la línea: leer del grupo si existe
                var destWarehouseId = activeGroup?.IdWarehouseDestLine
                    ?? txn.IdWarehouseDest
                    ?? txn.IdWarehouseOrigin;

                if (txn.IsTransitTransfer && txn.IdWarehouseDest.HasValue)
                {
                    // Quitar de in_transit de la bodega tránsito
                    await AdjustExistenceAsync(db, txn.IdWarehouseDest.Value, line.IdItem, line.ItemCode,
                        0, 0, -line.QtyReceived, line.UnitCost ?? 0, transactionId, now, auditUser);
                }

                // Agregar a qty_on_hand en bodega destino final
                await AdjustExistenceAsync(db, destWarehouseId, line.IdItem, line.ItemCode,
                    line.QtyReceived, 0, 0, line.UnitCost ?? 0, transactionId, now, auditUser);
            }

            // Procesar nuevas líneas de devolución
            if (newReturnLines != null && newReturnLines.Count > 0 && activeGroup != null)
            {
                var destWarehouseId = activeGroup.IdWarehouseDestLine ?? txn.IdWarehouseDest ?? txn.IdWarehouseOrigin;

                // Obtener el máximo LineNumber existente para asignar números únicos
                var maxLineNumber = await db.InventoryTransactionLines
                    .Where(l => l.IdInventoryTransaction == transactionId)
                    .MaxAsync(l => (int?)l.LineNumber) ?? 0;

                var currentLineNumber = maxLineNumber;

                foreach (var newReturn in newReturnLines)
                {
                    // Buscar información del artículo
                    var item = await db.Items.FirstOrDefaultAsync(i => i.Id == newReturn.ItemId);
                    if (item == null) continue;

                    currentLineNumber++; // Incrementar para la nueva línea

                    // Crear nueva línea de transacción
                    var newLine = new InventoryTransactionLine
                    {
                        IdInventoryTransaction = transactionId,
                        LineNumber = currentLineNumber,
                        IdItem = newReturn.ItemId,
                        ItemCode = item.Code,
                        ItemName = item.Name,
                        QtyRequested = 0,
                        QtyDispatched = 0,
                        QtyReceived = 0,
                        QtyReturned = newReturn.QtyReturned,
                        IdUnitOfMeasure = item.IdUnitOfMeasure,
                        UnitOfMeasureCode = null, // Se puede agregar si es necesario
                        UnitCost = item.CostPrice,
                        TotalCost = item.CostPrice * newReturn.QtyReturned,
                        IdInventoryTransactionWarehouseTransit = activeGroup.Id,
                        CreateDate = now,
                        RecordDate = now,
                        CreatedBy = auditUser,
                        UpdatedBy = auditUser,
                        Rowpointer = Guid.NewGuid()
                    };

                    db.InventoryTransactionLines.Add(newLine);
                    await db.SaveChangesAsync(); // Guardar para obtener el ID
                }
            }

            // Verificar si TODOS los grupos están recibidos
            var allGroups = txn.IsTransitTransfer
                ? await db.InventoryTransactionWarehouseTransits
                    .Where(g => g.IdInventoryTransaction == transactionId)
                    .ToListAsync()
                : null;

            var allReceived = txn.IsTransitTransfer
                ? (allGroups!.Count > 0 && allGroups.All(g => g.LineStatus == "Received" || g.LineStatus == "Cancelled"))
                : allLines.All(l => l.QtyReceived >= l.QtyDispatched);

            txn.IdInventoryTransactionStatus = allReceived
                ? InventoryTransactionStatusCode.IdCompleted
                : InventoryTransactionStatusCode.IdPartiallyReceived;

            if (allReceived)
            {
                txn.CompletedDate = now;
                txn.AffectsStock = true;
            }

            txn.UpdatedBy = auditUser;
            txn.RecordDate = now;

            // Actualizar kilometraje actual de la unidad de transporte con el Km de Salida registrado
            if (odometerOut.HasValue && txn.IdWarehouseDest.HasValue)
            {
                var transitWarehouse = await db.Warehouses
                    .FirstOrDefaultAsync(w => w.Id == txn.IdWarehouseDest.Value);
                if (transitWarehouse?.IdTransportUnit.HasValue == true)
                {
                    var transportUnit = await db.TransportUnits
                        .FirstOrDefaultAsync(u => u.Id == transitWarehouse.IdTransportUnit.Value);
                    if (transportUnit != null && odometerOut.Value > transportUnit.CurrentOdometerKm)
                    {
                        transportUnit.CurrentOdometerKm = odometerOut.Value;
                        transportUnit.RecordDate = now;
                        transportUnit.UpdatedBy  = auditUser;
                    }
                }
            }

            await db.SaveChangesAsync();
            return txn;
        }

        public async Task<InventoryTransaction> CompleteAsync(int companyId, int transactionId, string updatedBy, int userId)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var txn = await db.InventoryTransactions.FirstOrDefaultAsync(t => t.Id == transactionId)
                ?? throw new InvalidOperationException("Movimiento no encontrado");

            if (txn.IdInventoryTransactionStatus != InventoryTransactionStatusCode.IdConfirmed)
                throw new InvalidOperationException("El movimiento debe estar Confirmado para completar");

            var lines = await db.InventoryTransactionLines
                .Where(l => l.IdInventoryTransaction == transactionId)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var auditUser = (updatedBy?.Length > 30 ? updatedBy[..30] : updatedBy) ?? "SYSTEM";

            foreach (var line in lines)
            {
                line.QtyReceived = line.QtyDispatched > 0 ? line.QtyDispatched : line.QtyRequested;
                line.RecordDate  = now;
                line.UpdatedBy   = auditUser;

                // Destino: buscar en el grupo de tránsito si existe
                int destWarehouseId;
                if (line.IdInventoryTransactionWarehouseTransit.HasValue)
                {
                    var grp = await db.InventoryTransactionWarehouseTransits
                        .FirstOrDefaultAsync(g => g.Id == line.IdInventoryTransactionWarehouseTransit.Value);
                    destWarehouseId = grp?.IdWarehouseDestLine ?? txn.IdWarehouseDest ?? txn.IdWarehouseOrigin;
                    if (grp != null)
                    {
                        grp.LineStatus       = "Received";
                        grp.ReceivedDate     = now;
                        grp.ReceivedByUserId = userId;
                        grp.RecordDate       = now;
                        grp.UpdatedBy        = auditUser;
                    }
                }
                else
                {
                    destWarehouseId = txn.IdWarehouseDest ?? txn.IdWarehouseOrigin;
                }

                await AdjustExistenceAsync(db, destWarehouseId, line.IdItem, line.ItemCode,
                    line.QtyReceived, 0, 0, line.UnitCost ?? 0, transactionId, now, auditUser);
            }

            txn.IdInventoryTransactionStatus = InventoryTransactionStatusCode.IdCompleted;
            txn.CompletedDate = now;
            txn.AffectsStock = true;
            txn.UpdatedBy = auditUser;
            txn.RecordDate = now;

            await db.SaveChangesAsync();
            return txn;
        }

        public async Task<InventoryTransaction> CancelAsync(int companyId, int transactionId, string reason, string updatedBy, int userId)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);

            var txn = await db.InventoryTransactions.FirstOrDefaultAsync(t => t.Id == transactionId)
                ?? throw new InvalidOperationException("Movimiento no encontrado");

            if (txn.IdInventoryTransactionStatus == InventoryTransactionStatusCode.IdCompleted || txn.IdInventoryTransactionStatus == InventoryTransactionStatusCode.IdCancelled)
                throw new InvalidOperationException("No se puede cancelar un movimiento Completado o ya Cancelado");

            var now = DateTime.UtcNow;
            var auditUser = (updatedBy?.Length > 30 ? updatedBy[..30] : updatedBy) ?? "SYSTEM";

            // Revertir movimientos de existencias si ya estaba confirmado / en tránsito
            if (txn.IdInventoryTransactionStatus == InventoryTransactionStatusCode.IdConfirmed ||
                txn.IdInventoryTransactionStatus == InventoryTransactionStatusCode.IdInTransit ||
                txn.IdInventoryTransactionStatus == InventoryTransactionStatusCode.IdPartiallyReceived)
            {
                var lines = await db.InventoryTransactionLines
                    .Where(l => l.IdInventoryTransaction == transactionId)
                    .ToListAsync();

                var groups = await db.InventoryTransactionWarehouseTransits
                    .Where(g => g.IdInventoryTransaction == transactionId)
                    .ToListAsync();

                foreach (var grp in groups)
                {
                    grp.LineStatus  = "Cancelled";
                    grp.RecordDate  = now;
                    grp.UpdatedBy   = auditUser;
                }

                foreach (var line in lines)
                {
                    // Devolver qty_on_hand a bodega origen
                    await AdjustExistenceAsync(db, txn.IdWarehouseOrigin, line.IdItem, line.ItemCode,
                        line.QtyDispatched, 0, 0, line.UnitCost ?? 0, transactionId, now, auditUser);

                    // Quitar qty_in_transit de bodega tránsito si aplica
                    if (txn.IsTransitTransfer && txn.IdWarehouseDest.HasValue)
                        await AdjustExistenceAsync(db, txn.IdWarehouseDest.Value, line.IdItem, line.ItemCode,
                            0, 0, -line.QtyDispatched, line.UnitCost ?? 0, transactionId, now, auditUser);

                    line.RecordDate = now;
                    line.UpdatedBy  = auditUser;
                }
            }

            txn.IdInventoryTransactionStatus = InventoryTransactionStatusCode.IdCancelled;
            txn.CancelledDate = now;
            txn.CancelledByUserId = userId;
            txn.CancelReason = reason;
            txn.UpdatedBy = auditUser;
            txn.RecordDate = now;

            await db.SaveChangesAsync();
            return txn;
        }

        // ================================================================
        // EXISTENCIAS
        // ================================================================

        public async Task<List<ExistenceWarehouse>> GetExistencesByItemAsync(int companyId, int itemId)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await db.ExistenceWarehouses
                .Where(e => e.IdItem == itemId)
                .OrderBy(e => e.IdWarehouse)
                .ToListAsync();
        }

        public async Task<List<ExistenceWarehouse>> GetExistencesByWarehouseAsync(int companyId, int warehouseId)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await db.ExistenceWarehouses
                .Where(e => e.IdWarehouse == warehouseId && e.QtyOnHand > 0)
                .OrderBy(e => e.ItemCode)
                .ToListAsync();
        }

        public async Task<ExistenceWarehouse?> GetExistenceAsync(int companyId, int itemId, int warehouseId)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(companyId);
            return await db.ExistenceWarehouses
                .FirstOrDefaultAsync(e => e.IdItem == itemId && e.IdWarehouse == warehouseId);
        }

        // ================================================================
        // HELPERS PRIVADOS
        // ================================================================

        private static async Task AdjustExistenceAsync(
            CompanyDbContext db,
            int warehouseId,
            int itemId,
            string itemCode,
            decimal deltaOnHand,
            decimal deltaReserved,
            decimal deltaInTransit,
            decimal unitCost,
            int transactionId,
            DateTime now,
            string updatedBy)
        {
            var auditUser = (updatedBy?.Length > 30 ? updatedBy[..30] : updatedBy) ?? "system";

            // Check EF change-tracker first (avoids duplicate-insert when same (item, warehouse)
            // appears in multiple lines of the same transaction and no DB row exists yet)
            var existence = db.ExistenceWarehouses.Local
                .FirstOrDefault(e => e.IdItem == itemId && e.IdWarehouse == warehouseId)
                ?? await db.ExistenceWarehouses
                    .FirstOrDefaultAsync(e => e.IdItem == itemId && e.IdWarehouse == warehouseId);

            if (existence == null)
            {
                existence = new ExistenceWarehouse
                {
                    IdItem = itemId,
                    ItemCode = itemCode,
                    IdWarehouse = warehouseId,
                    QtyOnHand = 0,
                    QtyReserved = 0,
                    QtyInTransit = 0,
                    QtyAvailable = 0,
                    AverageCost = unitCost,
                    LastCost = unitCost,
                    CreatedBy = auditUser,
                    UpdatedBy = auditUser,
                    CreateDate = now,
                    RecordDate = now,
                    Rowpointer = Guid.NewGuid()
                };
                db.ExistenceWarehouses.Add(existence);
            }

            existence.QtyOnHand = Math.Max(0, existence.QtyOnHand + deltaOnHand);
            existence.QtyReserved = Math.Max(0, existence.QtyReserved + deltaReserved);
            existence.QtyInTransit = Math.Max(0, existence.QtyInTransit + deltaInTransit);
            existence.QtyAvailable = Math.Max(0, existence.QtyOnHand - existence.QtyReserved);

            if (unitCost > 0)
            {
                existence.LastCost = unitCost;
                if (deltaOnHand > 0 && existence.QtyOnHand > 0)
                    existence.AverageCost = ((existence.AverageCost * (existence.QtyOnHand - deltaOnHand)) + (unitCost * deltaOnHand)) / existence.QtyOnHand;
            }

            existence.LastTransactionId = transactionId;
            existence.LastMovementDate = now;
            existence.UpdatedBy = auditUser;
            existence.RecordDate = now;
        }
    }
}
