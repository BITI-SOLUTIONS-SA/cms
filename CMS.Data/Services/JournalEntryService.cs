// ================================================================================
// ARCHIVO: CMS.Data/Services/JournalEntryService.cs
// PROPÓSITO: Servicio para gestión de Asientos de Diario (Journal Entries)
// DESCRIPCIÓN: Lógica de negocio completa para asientos contables: CRUD,
//              validación de cuadre, contabilización, reversión, aprobación.
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-XX
// ================================================================================

using CMS.Entities.Operational;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Data.Services
{
    public interface IJournalEntryService
    {
        // ===== CONSULTAS =====
        Task<List<JournalEntry>> GetJournalEntriesAsync(
            int companyId,
            int? idJournalEntryStatus = null,
            int? idTypeAccounting = null,
            DateOnly? dateFrom = null,
            DateOnly? dateTo = null,
            string? search = null);
        Task<JournalEntry?> GetJournalEntryByIdAsync(int companyId, int idJournalEntry);
        Task<JournalEntry?> GetJournalEntryByNumberAsync(int companyId, string entryNumber);
        Task<string> GetNextEntryNumberAsync(int companyId, string period);
        Task<bool> EntryNumberExistsAsync(int companyId, string entryNumber, int? excludeId = null);

        // ===== CRUD =====
        Task<JournalEntry> CreateJournalEntryAsync(int companyId, JournalEntry entry, int idMenu, int userId, string currentUser);
        Task<JournalEntry> UpdateJournalEntryAsync(int companyId, JournalEntry entry, string currentUser);
        Task DeleteJournalEntryAsync(int companyId, int idJournalEntry);

        // ===== OPERACIONES CONTABLES =====
        Task<JournalEntry> PostJournalEntryAsync(int companyId, int idJournalEntry, int userId, string currentUser);
        Task<JournalEntry> ReverseJournalEntryAsync(int companyId, int idJournalEntry, DateOnly reversalDate, int idCancelReason, int idMenu, int userId, string currentUser);
        Task<JournalEntry> CancelJournalEntryAsync(int companyId, int idJournalEntry, int idCancelReason, int userId, string currentUser);
        Task<JournalEntry> ApproveJournalEntryAsync(int companyId, int idJournalEntry, int userId, string notes, string currentUser);

        // ===== VALIDACIONES =====
        Task<(bool IsBalanced, decimal Difference)> ValidateBalanceAsync(JournalEntry entry);
        Task<List<string>> ValidateJournalEntryAsync(int companyId, JournalEntry entry);
    }

    public class JournalEntryService : IJournalEntryService
    {
        private readonly ICompanyDbContextFactory _contextFactory;
        private readonly CMS.Data.Services.Interfaces.IConsecutiveService _consecutiveService;
        private readonly ILogger<JournalEntryService> _logger;
        private const int ENTITY_DOCUMENT_ID_JOURNAL_ENTRY = 1; // ID del documento "Journal Entry" en admin.entity_document

        public JournalEntryService(
            ICompanyDbContextFactory contextFactory,
            CMS.Data.Services.Interfaces.IConsecutiveService consecutiveService,
            ILogger<JournalEntryService> logger)
        {
            _contextFactory = contextFactory;
            _consecutiveService = consecutiveService;
            _logger = logger;
        }

        // ===== CONSULTAS =====

        public async Task<List<JournalEntry>> GetJournalEntriesAsync(
            int companyId,
            int? idJournalEntryStatus = null,
            int? idTypeAccounting = null,
            DateOnly? dateFrom = null,
            DateOnly? dateTo = null,
            string? search = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var query = context.JournalEntries.AsQueryable();

            if (idJournalEntryStatus.HasValue)
                query = query.Where(je => je.IdJournalEntryStatus == idJournalEntryStatus.Value);

            if (idTypeAccounting.HasValue)
                query = query.Where(je => je.IdTypeAccounting == idTypeAccounting.Value);

            if (dateFrom.HasValue)
                query = query.Where(je => je.PostingDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(je => je.PostingDate <= dateTo.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(je =>
                    je.EntryNumber.ToLower().Contains(searchLower) ||
                    (je.Reference != null && je.Reference.ToLower().Contains(searchLower)));
            }

            return await query
                .OrderByDescending(je => je.PostingDate)
                .ThenByDescending(je => je.IdJournalEntry)
                .ToListAsync();
        }

        public async Task<JournalEntry?> GetJournalEntryByIdAsync(int companyId, int idJournalEntry)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var entry = await context.JournalEntries
                .FirstOrDefaultAsync(je => je.IdJournalEntry == idJournalEntry);

            if (entry != null)
            {
                // Cargar líneas
                entry.Lines = await context.JournalEntryLines
                    .Where(l => l.IdJournalEntry == idJournalEntry)
                    .OrderBy(l => l.IdJournalEntryLine)
                    .ToListAsync();
            }

            return entry;
        }

        public async Task<JournalEntry?> GetJournalEntryByNumberAsync(int companyId, string entryNumber)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var entry = await context.JournalEntries
                .FirstOrDefaultAsync(je => je.EntryNumber == entryNumber);

            if (entry != null)
            {
                entry.Lines = await context.JournalEntryLines
                    .Where(l => l.IdJournalEntry == entry.IdJournalEntry)
                    .OrderBy(l => l.IdJournalEntryLine)
                    .ToListAsync();
            }

            return entry;
        }

        public async Task<string> GetNextEntryNumberAsync(int companyId, string period)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            // Calcular período desde posting_date más reciente
            var lastEntry = await context.JournalEntries
                .Where(je => je.PostingDate.Year.ToString() + "-" + je.PostingDate.Month.ToString("D2") == period)
                .OrderByDescending(je => je.EntryNumber)
                .FirstOrDefaultAsync();

            if (lastEntry == null)
            {
                return $"JE-{period}-0001";
            }

            // Extraer el último número
            var parts = lastEntry.EntryNumber.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[^1], out var lastNumber))
            {
                return $"JE-{period}-{(lastNumber + 1):D4}";
            }

            return $"JE-{period}-0001";
        }

        public async Task<bool> EntryNumberExistsAsync(int companyId, string entryNumber, int? excludeId = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            return await context.JournalEntries
                .AnyAsync(je => je.EntryNumber == entryNumber &&
                               (excludeId == null || je.IdJournalEntry != excludeId));
        }

        // ===== CRUD =====

        public async Task<JournalEntry> CreateJournalEntryAsync(int companyId, JournalEntry entry, int idMenu, int userId, string currentUser)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            // Validaciones
            var validationErrors = await ValidateJournalEntryAsync(companyId, entry);
            if (validationErrors.Any())
            {
                throw new InvalidOperationException(string.Join("; ", validationErrors));
            }

            // ⭐ GENERACIÓN AUTOMÁTICA DEL ENTRY_NUMBER USANDO CONSECUTIVO JERÁRQUICO
            if (string.IsNullOrWhiteSpace(entry.EntryNumber))
            {
                _logger.LogInformation(
                    "🔢 Generando consecutivo automático para Journal Entry en compañía {CompanyId}, menú {MenuId}",
                    companyId, idMenu);

                entry.EntryNumber = await _consecutiveService.GenerateNextNumberAsync(
                    companyId,
                    idMenu,
                    ENTITY_DOCUMENT_ID_JOURNAL_ENTRY,
                    userId);

                _logger.LogInformation("✅ Entry number generado: {EntryNumber}", entry.EntryNumber);
            }

            // Verificar unicidad del número
            if (await EntryNumberExistsAsync(companyId, entry.EntryNumber))
            {
                throw new InvalidOperationException($"El número de asiento '{entry.EntryNumber}' ya existe");
            }

            // Calcular totales
            entry.DebitTotal = entry.Lines.Sum(l => l.DebitAmount);
            entry.CreditTotal = entry.Lines.Sum(l => l.CreditAmount);

            // Auditoría
            entry.CreatedBy = currentUser;
            entry.UpdatedBy = currentUser;
            entry.CreateDate = DateTime.UtcNow;
            entry.RecordDate = DateTime.UtcNow;

            // Agregar encabezado
            context.JournalEntries.Add(entry);
            await context.SaveChangesAsync();

            // Agregar líneas con números secuenciales
            int lineNumber = 1;
            // Leer el default de centro de costo una sola vez para todas las líneas
            var defaultCostCenterId = await GetDefaultCostCenterIdAsync(context);
            foreach (var line in entry.Lines)
            {
                line.IdJournalEntry     = entry.IdJournalEntry;
                line.IdJournalEntryLine = lineNumber++;
                // Aplicar centro de costo default si no viene definido en la línea
                if (line.IdCostCenter == 0 && defaultCostCenterId.HasValue)
                    line.IdCostCenter = defaultCostCenterId.Value;
                line.CreatedBy  = currentUser;
                line.UpdatedBy  = currentUser;
                line.CreateDate = DateTime.UtcNow;
                line.RecordDate = DateTime.UtcNow;
                context.JournalEntryLines.Add(line);
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("Journal entry {EntryNumber} created by {User}", entry.EntryNumber, currentUser);

            return entry;
        }

        public async Task<JournalEntry> UpdateJournalEntryAsync(int companyId, JournalEntry entry, string currentUser)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var existing = await context.JournalEntries
                .FirstOrDefaultAsync(je => je.IdJournalEntry == entry.IdJournalEntry);

            if (existing == null)
            {
                throw new InvalidOperationException("Asiento de diario no encontrado");
            }

            // Solo se puede editar si está en borrador
            if (existing.Status != JournalEntryStatus.Draft)
            {
                throw new InvalidOperationException($"No se puede editar un asiento con estado '{existing.Status}'");
            }

            // Validaciones
            var validationErrors = await ValidateJournalEntryAsync(companyId, entry);
            if (validationErrors.Any())
            {
                throw new InvalidOperationException(string.Join("; ", validationErrors));
            }

            // Verificar unicidad del número (excluyendo el actual)
            if (await EntryNumberExistsAsync(companyId, entry.EntryNumber, entry.IdJournalEntry))
            {
                throw new InvalidOperationException($"El número de asiento '{entry.EntryNumber}' ya existe");
            }

            // Actualizar encabezado
            existing.EntryNumber        = entry.EntryNumber;
            existing.IdTypeAccounting   = entry.IdTypeAccounting;
            existing.Reference          = entry.Reference;
            existing.EntryDate          = entry.EntryDate;
            existing.PostingDate = entry.PostingDate;
            existing.CurrencyLocal = entry.CurrencyLocal;
            existing.CurrencyExchange = entry.CurrencyExchange;
            existing.RequiresApproval = entry.RequiresApproval;
            existing.UpdatedBy = currentUser;
            existing.RecordDate = DateTime.UtcNow;

            // Calcular totales
            existing.DebitTotal = entry.Lines.Sum(l => l.DebitAmount);
            existing.CreditTotal = entry.Lines.Sum(l => l.CreditAmount);

            // Eliminar líneas existentes
            var existingLines = await context.JournalEntryLines
                .Where(l => l.IdJournalEntry == entry.IdJournalEntry)
                .ToListAsync();
            context.JournalEntryLines.RemoveRange(existingLines);

            // Agregar líneas nuevas
            int lineNumber = 1;
            // Leer el default de centro de costo una sola vez para todas las líneas
            var defaultCostCenterId = await GetDefaultCostCenterIdAsync(context);
            foreach (var line in entry.Lines)
            {
                line.IdJournalEntry     = entry.IdJournalEntry;
                line.IdJournalEntryLine = lineNumber++;
                // Aplicar centro de costo default si no viene definido en la línea
                if (line.IdCostCenter == 0 && defaultCostCenterId.HasValue)
                    line.IdCostCenter = defaultCostCenterId.Value;
                line.CreatedBy  = currentUser;
                line.UpdatedBy  = currentUser;
                line.CreateDate = DateTime.UtcNow;
                line.RecordDate = DateTime.UtcNow;
                context.JournalEntryLines.Add(line);
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("Journal entry {EntryNumber} updated by {User}", entry.EntryNumber, currentUser);

            existing.Lines = entry.Lines;
            return existing;
        }

        public async Task DeleteJournalEntryAsync(int companyId, int idJournalEntry)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var entry = await context.JournalEntries
                .FirstOrDefaultAsync(je => je.IdJournalEntry == idJournalEntry);

            if (entry == null)
            {
                throw new InvalidOperationException("Asiento de diario no encontrado");
            }

            // Solo se puede eliminar si está en borrador
            if (entry.Status != JournalEntryStatus.Draft)
            {
                throw new InvalidOperationException($"No se puede eliminar un asiento con estado '{entry.Status}'");
            }

            // Eliminar líneas
            var lines = await context.JournalEntryLines
                .Where(l => l.IdJournalEntry == idJournalEntry)
                .ToListAsync();
            context.JournalEntryLines.RemoveRange(lines);

            // Eliminar encabezado
            context.JournalEntries.Remove(entry);

            await context.SaveChangesAsync();

            _logger.LogInformation("Journal entry {EntryNumber} deleted", entry.EntryNumber);
        }

        // ===== OPERACIONES CONTABLES =====

        public async Task<JournalEntry> PostJournalEntryAsync(int companyId, int idJournalEntry, int userId, string currentUser)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var entry = await GetJournalEntryByIdAsync(companyId, idJournalEntry);

            if (entry == null)
            {
                throw new InvalidOperationException("Asiento de diario no encontrado");
            }

            if (entry.Status != JournalEntryStatus.Draft)
            {
                throw new InvalidOperationException($"No se puede contabilizar un asiento con estado '{entry.Status}'");
            }

            // Validar cuadre
            var (isBalanced, difference) = await ValidateBalanceAsync(entry);
            if (!isBalanced)
            {
                throw new InvalidOperationException($"El asiento no está cuadrado. Diferencia: {difference:N2}");
            }

            // Validar aprobación si es requerida
            if (entry.RequiresApproval && !entry.ApprovedDate.HasValue)
            {
                throw new InvalidOperationException("El asiento requiere aprobación antes de contabilizarse");
            }

            // Actualizar estado
            var existing = await context.JournalEntries
                .FirstOrDefaultAsync(je => je.IdJournalEntry == idJournalEntry);

            if (existing == null)
            {
                throw new InvalidOperationException("Asiento de diario no encontrado");
            }

            existing.Status = JournalEntryStatus.Posted;
            existing.PostedDate = DateTime.UtcNow;
            existing.PostedByUserId = userId;
            existing.UpdatedBy = currentUser;
            existing.RecordDate = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogInformation("Journal entry {EntryNumber} posted by user {UserId}", entry.EntryNumber, userId);

            entry.Status = JournalEntryStatus.Posted;
            entry.PostedDate = DateTime.UtcNow;
            entry.PostedByUserId = userId;

            return entry;
        }

        public async Task<JournalEntry> ReverseJournalEntryAsync(int companyId, int idJournalEntry, DateOnly reversalDate, int idCancelReason, int idMenu, int userId, string currentUser)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var originalEntry = await GetJournalEntryByIdAsync(companyId, idJournalEntry);

            if (originalEntry == null)
            {
                throw new InvalidOperationException("Asiento de diario no encontrado");
            }

            if (originalEntry.Status != JournalEntryStatus.Posted)
            {
                throw new InvalidOperationException("Solo se pueden revertir asientos contabilizados");
            }

            if (originalEntry.Status == JournalEntryStatus.Reversed)
            {
                throw new InvalidOperationException("El asiento ya está revertido");
            }

            // Validar que la razón de cancelación/reversión existe
            var cancelReason = await context.JournalEntryCancelReasons
                .FirstOrDefaultAsync(r => r.IdJournalEntryCancelReason == idCancelReason);

            if (cancelReason == null)
            {
                throw new InvalidOperationException($"Razón de cancelación con ID {idCancelReason} no encontrada");
            }

            // Crear asiento de reversión
            var reversalEntry = new JournalEntry
            {
                // ⭐ NO generar entry_number aquí - el servicio lo hará automáticamente
                EntryNumber       = string.Empty, // Se genera en CreateJournalEntryAsync
                IdTypeAccounting  = originalEntry.IdTypeAccounting,
                Reference         = $"REV-{originalEntry.EntryNumber}",
                EntryDate = reversalDate,
                PostingDate = reversalDate,
                CurrencyLocal = originalEntry.CurrencyLocal,
                CurrencyExchange = originalEntry.CurrencyExchange,
                Status = JournalEntryStatus.Draft,
                IsReversing = true,
                IdReversedEntry = originalEntry.IdJournalEntry,
                ReversalDate = reversalDate,
                CreatedBy = currentUser,
                UpdatedBy = currentUser
            };

            // Invertir líneas (débito ↔ crédito)
            foreach (var originalLine in originalEntry.Lines)
            {
                reversalEntry.Lines.Add(new JournalEntryLine
                {
                    IdChartOfAccounts = originalLine.IdChartOfAccounts,
                    Description       = $"REVERSIÓN: {originalLine.Description}",
                    Reference         = originalLine.Reference,
                    DebitAmount       = originalLine.CreditAmount,   // INVERTIR
                    CreditAmount      = originalLine.DebitAmount,    // INVERTIR
                    IdCostCenter                = originalLine.IdCostCenter,
                    IdJournalEntryTypeOrigin    = originalLine.IdJournalEntryTypeOrigin,
                    IdDocumentOrigin            = originalLine.IdDocumentOrigin
                });
            }

            // ⭐ Crear asiento de reversión usando el idMenu proporcionado
            // El consecutivo se resuelve desde sinai.consecutive según el menú
            var createdReversal = await CreateJournalEntryAsync(
                companyId, reversalEntry, idMenu, userId, currentUser);

            // Contabilizar automáticamente
            await PostJournalEntryAsync(companyId, createdReversal.IdJournalEntry, userId, currentUser);

            // Actualizar asiento original
            var originalInDb = await context.JournalEntries
                .FirstOrDefaultAsync(je => je.IdJournalEntry == idJournalEntry);

            if (originalInDb != null)
            {
                originalInDb.Status = JournalEntryStatus.Reversed;
                originalInDb.UpdatedBy = currentUser;
                originalInDb.RecordDate = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }

            _logger.LogInformation("Journal entry {EntryNumber} reversed with entry {ReversalNumber}",
                originalEntry.EntryNumber, createdReversal.EntryNumber);

            return createdReversal;
        }

        public async Task<JournalEntry> CancelJournalEntryAsync(int companyId, int idJournalEntry, int idCancelReason, int userId, string currentUser)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var entry = await context.JournalEntries
                .FirstOrDefaultAsync(je => je.IdJournalEntry == idJournalEntry);

            if (entry == null)
            {
                throw new InvalidOperationException("Asiento de diario no encontrado");
            }

            if (entry.Status != JournalEntryStatus.Draft)
            {
                throw new InvalidOperationException("Solo se pueden cancelar asientos en borrador");
            }

            // Validar que la razón de cancelación existe
            var cancelReason = await context.JournalEntryCancelReasons
                .FirstOrDefaultAsync(r => r.IdJournalEntryCancelReason == idCancelReason);

            if (cancelReason == null)
            {
                throw new InvalidOperationException($"Razón de cancelación con ID {idCancelReason} no encontrada");
            }

            entry.Status = JournalEntryStatus.Cancelled;
            entry.CancelledDate = DateTime.UtcNow;
            entry.CancelledByUserId = userId;
            entry.IdJournalEntryCancelReason = idCancelReason;
            entry.UpdatedBy = currentUser;
            entry.RecordDate = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogInformation("Journal entry {EntryNumber} cancelled by user {UserId} with reason {ReasonCode}",
                entry.EntryNumber, userId, cancelReason.Code);

            return entry;
        }

        public async Task<JournalEntry> ApproveJournalEntryAsync(int companyId, int idJournalEntry, int userId, string notes, string currentUser)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(companyId);

            var entry = await context.JournalEntries
                .FirstOrDefaultAsync(je => je.IdJournalEntry == idJournalEntry);

            if (entry == null)
            {
                throw new InvalidOperationException("Asiento de diario no encontrado");
            }

            if (entry.Status != JournalEntryStatus.Draft)
            {
                throw new InvalidOperationException("Solo se pueden aprobar asientos en borrador");
            }

            if (!entry.RequiresApproval)
            {
                throw new InvalidOperationException("Este asiento no requiere aprobación");
            }

            entry.ApprovedDate = DateTime.UtcNow;
            entry.ApprovedByUserId = userId;
            entry.ApprovalNotes = notes;
            entry.UpdatedBy = currentUser;
            entry.RecordDate = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogInformation("Journal entry {EntryNumber} approved by user {UserId}",
                entry.EntryNumber, userId);

            return entry;
        }

        // ===== VALIDACIONES =====

        public Task<(bool IsBalanced, decimal Difference)> ValidateBalanceAsync(JournalEntry entry)
        {
            var totalDebit = entry.Lines.Sum(l => l.DebitAmount);
            var totalCredit = entry.Lines.Sum(l => l.CreditAmount);
            var difference = Math.Abs(totalDebit - totalCredit);

            // Tolerancia de 0.01 para errores de redondeo
            var isBalanced = difference < 0.01m;

            return Task.FromResult((isBalanced, difference));
        }

        public async Task<List<string>> ValidateJournalEntryAsync(int companyId, JournalEntry entry)
        {
            var errors = new List<string>();

            // Validar campos requeridos
            if (entry.Lines == null || !entry.Lines.Any())
                errors.Add("El asiento debe tener al menos una línea");

            if (entry.Lines != null && entry.Lines.Count < 2)
                errors.Add("El asiento debe tener al menos dos líneas (débito y crédito)");

            // Validar cuadre
            if (entry.Lines != null && entry.Lines.Any())
            {
                var (isBalanced, difference) = await ValidateBalanceAsync(entry);
                if (!isBalanced)
                {
                    errors.Add($"El asiento no está cuadrado. Diferencia: {difference:N2}");
                }
            }

            // Validar líneas
            if (entry.Lines != null)
            {
                for (int i = 0; i < entry.Lines.Count; i++)
                {
                    var line = entry.Lines[i];

                    if (line.IdChartOfAccounts <= 0)
                        errors.Add($"Línea {i + 1}: La cuenta contable es requerida");

                    if (string.IsNullOrWhiteSpace(line.Description))
                        errors.Add($"Línea {i + 1}: La descripción es requerida");

                    if (line.DebitAmount == 0 && line.CreditAmount == 0)
                        errors.Add($"Línea {i + 1}: Debe tener débito o crédito");

                    if (line.DebitAmount > 0 && line.CreditAmount > 0)
                        errors.Add($"Línea {i + 1}: No puede tener débito y crédito simultáneamente");

                    if (line.DebitAmount < 0 || line.CreditAmount < 0)
                        errors.Add($"Línea {i + 1}: Los montos no pueden ser negativos");

                    if (line.IdCostCenter == 0)
                        errors.Add($"Línea {i + 1}: El centro de costo es requerido");
                }
            }

            return errors;
        }

        // ===== HELPERS PRIVADOS =====

        /// <summary>
        /// Lee el parámetro global 'cost_center_default' de la BD de la compañía.
        /// Retorna el id_cost_center configurado, o null si no existe / no tiene valor.
        /// </summary>
        private static async Task<int?> GetDefaultCostCenterIdAsync(CompanyDbContext context)
        {
            var param = await context.GlobalParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == "cost_center_default" && p.IsActive);

            return param?.ValueInteger;
        }
    }
}
