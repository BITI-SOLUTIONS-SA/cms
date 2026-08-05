// ================================================================================
// ARCHIVO: CMS.Data/Services/IInventoryTransactionService.cs
// PROPÓSITO: Contrato del servicio de movimientos de inventario
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-13
// ================================================================================

using CMS.Entities.Operational;

namespace CMS.Data.Services
{
    public interface IInventoryTransactionService
    {
        // ================================================================
        // CONSULTAS
        // ================================================================

        /// <summary>Obtiene lista paginada de movimientos con filtros.</summary>
        Task<(List<InventoryTransaction> Items, int TotalCount)> GetTransactionsAsync(
            int companyId,
            string? search = null,
            int? idInventoryTransactionType = null,
            int? idInventoryTransactionStatus = null,
            int? warehouseOriginId = null,
            int? warehouseDestId = null,
            DateOnly? dateFrom = null,
            DateOnly? dateTo = null,
            int page = 1,
            int pageSize = 20);

        /// <summary>Obtiene los conteos por estado aplicando los mismos filtros que GetTransactionsAsync.</summary>
        Task<Dictionary<int, int>> GetStatusCountsAsync(
            int companyId,
            string? search = null,
            int? idInventoryTransactionType = null,
            int? warehouseOriginId = null,
            int? warehouseDestId = null,
            DateOnly? dateFrom = null,
            DateOnly? dateTo = null);

        /// <summary>Obtiene un movimiento por ID con sus líneas.</summary>
        Task<InventoryTransaction?> GetByIdAsync(int companyId, int id);

        /// <summary>Obtiene las líneas de un movimiento.</summary>
        Task<List<InventoryTransactionLine>> GetLinesAsync(int companyId, int transactionId);

        /// <summary>Obtiene los grupos de tránsito de un movimiento TransitTransfer.</summary>
        Task<List<InventoryTransactionWarehouseTransit>> GetTransitGroupsAsync(int companyId, int transactionId);

        /// <summary>Verifica si el número de transacción ya existe.</summary>
        Task<bool> TransactionNumberExistsAsync(int companyId, string transactionNumber, int? excludeId = null);

        /// <summary>Verifica si el sello de seguridad ya está usado en otra transacción (encabezado o línea).</summary>
        Task<bool> SecuritySealExistsAsync(int companyId, string securitySeal, int? excludeId = null);

        /// <summary>
        /// Verifica si un sello ya está usado como sello de destino en alguna línea,
        /// o como sello principal en algún encabezado, excluyendo opcionalmente una transacción.
        /// Se usa para validar el "Nuevo Sello Destino" al recibir.
        /// </summary>
        Task<bool> AnySealExistsAsync(int companyId, string seal, int? excludeTransactionId = null);

        // ================================================================
        // GENERACIÓN DE NÚMERO
        // ================================================================

        /// <summary>Genera el próximo número de transacción (INV-YYYY-NNNNN).</summary>
        Task<string> GenerateTransactionNumberAsync(int companyId);

        // ================================================================
        // CRUD
        // ================================================================

        /// <summary>
        /// Verifica si la bodega de tránsito tiene algún movimiento activo (no Completado/Cancelado).
        /// Retorna (true, transactionNumber) si está ocupada.
        /// </summary>
        Task<(bool IsBusy, string? TransactionNumber)> CheckTransitWarehouseBusyAsync(int companyId, int warehouseId, int? excludeTransactionId = null);

        /// <summary>Crea un nuevo movimiento en estado Draft.</summary>
        Task<InventoryTransaction> CreateAsync(int companyId, InventoryTransaction transaction, List<InventoryTransactionLine> lines, string createdBy, int userId, Dictionary<int, string?>? groupNotes = null);

        /// <summary>Actualiza el encabezado de un movimiento en estado Draft.</summary>
        Task<InventoryTransaction> UpdateAsync(int companyId, InventoryTransaction transaction, string updatedBy);

        /// <summary>Reemplaza las líneas de un movimiento Draft.</summary>
        Task SaveLinesAsync(int companyId, int transactionId, List<InventoryTransactionLine> lines, string updatedBy, Dictionary<int, string?>? groupNotes = null);

        // ================================================================
        // FLUJO DE ESTADOS
        // ================================================================

        /// <summary>
        /// Confirma el movimiento (Draft → Confirmed).
        /// Para TransitTransfer pasa a InTransit.
        /// Actualiza qty_in_transit en existencias.
        /// </summary>
        Task<InventoryTransaction> ConfirmAsync(int companyId, int transactionId, string updatedBy, int userId);

        /// <summary>
        /// Confirma la recepción de una o varias líneas específicas (para TransitTransfer).
        /// Cuando todas las líneas están recibidas, el estado pasa a Completed.
        /// <summary>
        /// Confirma la recepción del grupo de tránsito activo (para TransitTransfer).
        /// Actualiza el grupo (line_status, sello, horarios, odómetro, firma) y las líneas del grupo.
        /// Cuando todos los grupos están recibidos, el estado pasa a Completed.
        /// Actualiza existencias (qty_in_transit ↓, qty_on_hand ↑).
        /// </summary>
        Task<InventoryTransaction> ReceiveLinesAsync(int companyId, int transactionId, List<int> lineIds, int receivedByUserId, string receivedBy, string? arrivalTime = null, string? departureTime = null, decimal? odometerOut = null, string? destSeal = null, int? nextWarehouseId = null, Dictionary<int, decimal>? lineQtys = null, string? signature = null, int? transitGroupId = null, Dictionary<int, decimal>? lineReturns = null, List<NewReturnLineDto>? newReturnLines = null, string? receiveNotes = null);

        /// <summary>
        /// Completa un movimiento simple (Confirmed → Completed).
        /// Actualiza existencias.
        /// </summary>
        Task<InventoryTransaction> CompleteAsync(int companyId, int transactionId, string updatedBy, int userId);

        /// <summary>Cancela un movimiento.</summary>
        Task<InventoryTransaction> CancelAsync(int companyId, int transactionId, string reason, string updatedBy, int userId);

        // ================================================================
        // EXISTENCIAS
        // ================================================================

        /// <summary>Obtiene el saldo de un artículo en todas las bodegas.</summary>
        Task<List<ExistenceWarehouse>> GetExistencesByItemAsync(int companyId, int itemId);

        /// <summary>Obtiene el saldo de todos los artículos en una bodega.</summary>
        Task<List<ExistenceWarehouse>> GetExistencesByWarehouseAsync(int companyId, int warehouseId);

        /// <summary>Obtiene el saldo de un artículo en una bodega específica.</summary>
        Task<ExistenceWarehouse?> GetExistenceAsync(int companyId, int itemId, int warehouseId);
    }
}
