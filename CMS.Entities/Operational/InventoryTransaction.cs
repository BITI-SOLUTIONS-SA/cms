// ================================================================================
// ARCHIVO: CMS.Entities/Operational/InventoryTransaction.cs
// PROPÓSITO: Entidad que representa un movimiento de inventario (entrada, salida,
//            traslado, ajuste, recepción de ruta de distribución, etc.)
// DESCRIPCIÓN: Centraliza TODOS los movimientos de inventario de la compañía.
//              Se almacena en la BD de cada compañía, schema {company_code}.
//              Soporta el flujo de "bodega a bodega con tránsito":
//               - Origen puede ser cualquier bodega
//               - Si el destino es una bodega tipo Transit (vehículo), las líneas
//                 pueden tener bodegas destino individuales diferentes
//               - El responsable de cada bodega destino confirma la recepción
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-13
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    // ============================================================
    // TIPOS DE MOVIMIENTO
    // ============================================================

    // (Los tipos de movimiento ahora viven en la tabla admin.inventory_transaction_type)

    /// <summary>Estados del documento de movimiento — códigos para lógica de negocio.</summary>
    public static class InventoryTransactionStatusCode
    {
        /// <summary>Borrador / en creación</summary>
        public const string Draft = "Draft";
        /// <summary>Confirmado — mercancía despachada desde origen</summary>
        public const string Confirmed = "Confirmed";
        /// <summary>En tránsito (aplica para TransitTransfer)</summary>
        public const string InTransit = "InTransit";
        /// <summary>Parcialmente recibido (algunas líneas confirmadas)</summary>
        public const string PartiallyReceived = "PartiallyReceived";
        /// <summary>Completado — toda la mercancía recibida</summary>
        public const string Completed = "Completed";
        /// <summary>Cancelado</summary>
        public const string Cancelled = "Cancelled";

        // IDs fijos del seed en admin.inventory_transaction_status (SERIAL en orden de INSERT)
        public const int IdDraft             = 1;
        public const int IdConfirmed         = 2;
        public const int IdInTransit         = 3;
        public const int IdPartiallyReceived = 4;
        public const int IdCompleted         = 5;
        public const int IdCancelled         = 6;
    }

    // ============================================================
    // ENCABEZADO DE MOVIMIENTO
    // ============================================================

    /// <summary>
    /// Encabezado de un movimiento de inventario.
    /// Registra TODOS los movimientos: traslados simples, vía tránsito, ajustes, entradas, salidas.
    /// </summary>
    [Table("inventory_transaction")]
    public class InventoryTransaction
    {
        [Key]
        [Column("id_inventory_transaction")]
        public int Id { get; set; }

        /// <summary>Número legible del movimiento (ej: INV-2026-00001)</summary>
        [Required]
        [MaxLength(30)]
        [Column("transaction_number")]
        public string TransactionNumber { get; set; } = string.Empty;

        /// <summary>FK lógica cross-db hacia admin.inventory_transaction_type</summary>
        [Required]
        [Column("id_inventory_transaction_type")]
        public int IdInventoryTransactionType { get; set; }

        /// <summary>FK lógica cross-db hacia admin.inventory_transaction_status</summary>
        [Required]
        [Column("id_inventory_transaction_status")]
        public int IdInventoryTransactionStatus { get; set; } = InventoryTransactionStatusCode.IdDraft;

        /// <summary>
        /// FK lógica cross-db hacia admin.menu.id_menu
        /// Indica desde qué pantalla/módulo se creó este movimiento de inventario.
        /// No se puede declarar FK real porque admin.menu está en la BD central (cms)
        /// y esta tabla está en la BD de la compañía.
        /// </summary>
        [Required]
        [Column("id_menu")]
        public int IdMenu { get; set; }

        /// <summary>
        /// FK real a sinai.journal_entry.id_journal_entry (misma BD/schema de compañía).
        /// Asiento contable generado por este movimiento de inventario.
        /// Los registros históricos (creados antes de existir esta integración) apuntan
        /// al Asiento Genérico del Sistema (id_journal_entry = 0), que no representa
        /// una transacción contable real.
        /// </summary>
        [Column("id_journal_entry")]
        public int IdJournalEntry { get; set; }

        // Navegación
        [ForeignKey(nameof(IdJournalEntry))]
        public JournalEntry? JournalEntry { get; set; }

        // ===== BODEGAS =====

        /// <summary>FK lógica a {schema}.warehouse — bodega de origen</summary>
        [Column("id_warehouse_origin")]
        public int IdWarehouseOrigin { get; set; }

        /// <summary>
        /// FK lógica a {schema}.warehouse — bodega de destino principal.
        /// En movimientos TransitTransfer, esta es la bodega de tránsito (vehículo).
        /// Las bodegas destino finales se especifican en cada línea.
        /// </summary>
        [Column("id_warehouse_dest")]
        public int? IdWarehouseDest { get; set; }

        // ===== REFERENCIAS =====



        /// <summary>Referencia externa: factura, OC, guía de remisión, etc.</summary>
        [MaxLength(100)]
        [Column("reference")]
        public string? Reference { get; set; }

        /// <summary>Notas generales</summary>
        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        // ===== FECHAS =====

        [Column("transaction_date")]
        public DateOnly TransactionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Column("expected_arrival_date")]
        public DateOnly? ExpectedArrivalDate { get; set; }

        [Column("confirmed_date")]
        public DateTime? ConfirmedDate { get; set; }

        [Column("completed_date")]
        public DateTime? CompletedDate { get; set; }

        [Column("cancelled_date")]
        public DateTime? CancelledDate { get; set; }

        // ===== RESPONSABLES (FK lógica cross-DB a cms.admin.user) =====

        /// <summary>FK lógica cross-DB hacia cms.admin.user.id_user — usuario que creó el movimiento</summary>
        [Column("created_by_user_id")]
        public int CreatedByUserId { get; set; } = 0;

        /// <summary>FK lógica cross-DB hacia cms.admin.user.id_user — usuario que confirmó el movimiento</summary>
        [Column("confirmed_by_user_id")]
        public int? ConfirmedByUserId { get; set; }

        /// <summary>FK lógica cross-DB hacia cms.admin.user.id_user — usuario que canceló el movimiento</summary>
        [Column("cancelled_by_user_id")]
        public int? CancelledByUserId { get; set; }

        [MaxLength(500)]
        [Column("cancel_reason")]
        public string? CancelReason { get; set; }

        // ===== FLAGS =====

        /// <summary>True si es un traslado con bodega de tránsito (vehículo)</summary>
        [Column("is_transit_transfer")]
        public bool IsTransitTransfer { get; set; } = false;

        /// <summary>True si el movimiento ya afectó el saldo de existencias</summary>
        [Column("affects_stock")]
        public bool AffectsStock { get; set; } = false;

        // ===== SELLO DE SEGURIDAD (solo TransitTransfer) =====

        /// <summary>Sello de seguridad único para traslados vía tránsito. No puede repetirse entre transacciones.</summary>
        [MaxLength(50)]
        [Column("security_seal")]
        public string? SecuritySeal { get; set; }

        // ===== TRAMO ORIGEN → BODEGA TRÁNSITO (solo TransitTransfer) =====

        /// <summary>Hora de salida desde la bodega origen hacia la bodega de tránsito.</summary>
        [Column("departure_time")]
        public TimeOnly? DepartureTime { get; set; }



        /// <summary>Kilometraje al salir de la bodega origen.</summary>
        [Column("odometer_out")]
        public decimal? OdometerOut { get; set; }

        // ===== AUDITORÍA =====

        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(150)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("rowpointer")]
        public Guid Rowpointer { get; set; } = Guid.NewGuid();

        // ===== NAVEGACIÓN =====

        [NotMapped]
        public List<InventoryTransactionLine> Lines { get; set; } = new();
    }

    // ============================================================
    // GRUPO DE TRÁNSITO POR BODEGA DESTINO (solo TransitTransfer)
    // ============================================================

    /// <summary>
    /// Encabezado de cada parada (grupo de bodega destino) dentro de un movimiento TransitTransfer.
    /// Una fila por bodega destino visitada: guarda sello, horarios, odómetro, firma y estado de recepción.
    /// Las líneas de artículo referencian este grupo a través de id_inventory_transaction_warehouse_transit.
    /// </summary>
    [Table("inventory_transaction_warehouse_transit")]
    public class InventoryTransactionWarehouseTransit
    {
        [Key]
        [Column("id_inventory_transaction_warehouse_transit")]
        public int Id { get; set; }

        // ===== RELACIÓN CON EL MOVIMIENTO PRINCIPAL =====

        [Column("id_inventory_transaction")]
        public int IdInventoryTransaction { get; set; }

        /// <summary>Número de parada dentro del movimiento (1, 2, 3 …)</summary>
        [Column("line_number")]
        public int LineNumber { get; set; } = 1;

        // ===== BODEGAS =====

        /// <summary>FK lógica a {schema}.warehouse — bodega de origen de este grupo</summary>
        [Column("id_warehouse_origin_line")]
        public int? IdWarehouseOriginLine { get; set; }

        /// <summary>FK lógica a {schema}.warehouse — bodega destino final de esta parada</summary>
        [Column("id_warehouse_dest_line")]
        public int? IdWarehouseDestLine { get; set; }

        // ===== ESTADO =====

        /// <summary>Estado de esta parada: Pending, InTransit, Received, Rejected, Cancelled</summary>
        [Required]
        [MaxLength(20)]
        [Column("line_status")]
        public string LineStatus { get; set; } = "Pending";

        // ===== LOGÍSTICA DEL VEHÍCULO EN ESTA PARADA =====

        /// <summary>Sello de seguridad colocado al cerrar la bodega destino tras la recepción.</summary>
        [MaxLength(50)]
        [Column("dest_security_seal")]
        public string? DestSecuritySeal { get; set; }

        /// <summary>Hora de salida del vehículo hacia el siguiente destino.</summary>
        [Column("departure_time")]
        public TimeOnly? DepartureTime { get; set; }

        /// <summary>Hora de llegada del vehículo a esta bodega destino.</summary>
        [Column("arrival_time")]
        public TimeOnly? ArrivalTime { get; set; }

        /// <summary>Lectura de odómetro al salir hacia el siguiente destino (km).</summary>
        [Column("odometer_out")]
        public decimal? OdometerOut { get; set; }

        /// <summary>Firma digital del receptor en base64 PNG data URI.</summary>
        [Column("signature")]
        public string? Signature { get; set; }

        // ===== RECEPCIÓN =====

        [Column("received_date")]
        public DateTime? ReceivedDate { get; set; }

        /// <summary>FK lógica cross-DB a cms.admin.user — quien confirmó la recepción del grupo.</summary>
        [Column("received_by_user_id")]
        public int? ReceivedByUserId { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        // ===== AUDITORÍA =====

        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(30)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("rowpointer")]
        public Guid Rowpointer { get; set; } = Guid.NewGuid();

        // ===== NAVEGACIÓN =====

        [NotMapped]
        public List<InventoryTransactionLine> Lines { get; set; } = new();
    }

    // ============================================================
    // LÍNEA DE MOVIMIENTO
    // ============================================================

    /// <summary>
    /// Línea de artículo dentro de un movimiento de inventario.
    /// Solo contiene datos del artículo: cantidades, costo, unidad de medida, lote.
    /// Los datos de logística de grupo (sello, horarios, firma, bodegas por grupo, etc.)
    /// se almacenan en <see cref="InventoryTransactionWarehouseTransit"/>.
    /// </summary>
    [Table("inventory_transaction_line")]
    public class InventoryTransactionLine
    {
        [Key]
        [Column("id_inventory_transaction_line")]
        public int Id { get; set; }

        [Column("id_inventory_transaction")]
        public int IdInventoryTransaction { get; set; }

        /// <summary>
        /// FK al grupo de tránsito (parada de bodega) al que pertenece esta línea.
        /// Solo se popula para movimientos TransitTransfer. NULL para cualquier otro tipo.
        /// </summary>
        [Column("id_inventory_transaction_warehouse_transit")]
        public int? IdInventoryTransactionWarehouseTransit { get; set; }

        // ===== CAMPOS TRANSITORIOS (NotMapped — usados al crear/guardar, no se persisten) =====

        /// <summary>
        /// Campo transitorio: bodega destino de este artículo dentro del grupo de tránsito.
        /// Se popula desde el DTO al crear/actualizar líneas de TransitTransfer.
        /// No se persiste en la BD — va en InventoryTransactionWarehouseTransit.IdWarehouseDestLine.
        /// </summary>
        [NotMapped]
        public int? IdWarehouseDestLine { get; set; }

        /// <summary>
        /// Campo transitorio: bodega origen de este artículo dentro del grupo de tránsito.
        /// Se popula desde el DTO al crear/actualizar líneas de TransitTransfer.
        /// No se persiste en la BD — va en InventoryTransactionWarehouseTransit.IdWarehouseOriginLine.
        /// </summary>
        [NotMapped]
        public int? IdWarehouseOriginLine { get; set; }

        [Column("line_number")]
        public int LineNumber { get; set; } = 1;

        // ===== ARTÍCULO =====

        /// <summary>FK lógica a {schema}.item</summary>
        [Column("id_item")]
        public int IdItem { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("item_name")]
        public string ItemName { get; set; } = string.Empty;

        // ===== CANTIDADES =====

        [Column("qty_requested")]
        public decimal QtyRequested { get; set; } = 0;

        [Column("qty_dispatched")]
        public decimal QtyDispatched { get; set; } = 0;

        [Column("qty_received")]
        public decimal QtyReceived { get; set; } = 0;

        [Column("qty_returned")]
        public decimal QtyReturned { get; set; } = 0;

        // ===== UNIDAD DE MEDIDA =====

        /// <summary>FK lógica cross-DB a cms.admin.electronic_document_unit_of_measure (MH v4.4)</summary>
        [Column("id_electronic_document_unit_of_measure")]
        public int? IdElectronicDocumentUnitOfMeasure { get; set; }

        [MaxLength(10)]
        [Column("unit_of_measure_code")]
        public string? UnitOfMeasureCode { get; set; }

        // ===== COSTO / VALOR =====

        [Column("unit_cost")]
        public decimal? UnitCost { get; set; }

        [Column("total_cost")]
        public decimal? TotalCost { get; set; }

        // ===== LOTE / VENCIMIENTO =====

        [MaxLength(50)]
        [Column("lot_number")]
        public string? LotNumber { get; set; }

        [Column("expiry_date")]
        public DateOnly? ExpiryDate { get; set; }

        // ===== AUDITORÍA =====

        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(150)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("rowpointer")]
        public Guid Rowpointer { get; set; } = Guid.NewGuid();
    }

    // ============================================================
    // SALDO DE EXISTENCIAS POR BODEGA
    // ============================================================

    /// <summary>
    /// Saldo de inventario de un artículo en una bodega específica.
    /// Se actualiza cada vez que se completa un movimiento de inventario.
    /// </summary>
    [Table("existence_warehouse")]
    public class ExistenceWarehouse
    {
        [Key]
        [Column("id_existence_warehouse")]
        public int Id { get; set; }

        /// <summary>FK lógica a {schema}.item</summary>
        [Column("id_item")]
        public int IdItem { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>FK lógica a {schema}.warehouse</summary>
        [Column("id_warehouse")]
        public int IdWarehouse { get; set; }

        // ===== SALDOS =====

        /// <summary>Stock disponible (confirmado, no reservado)</summary>
        [Column("qty_on_hand")]
        public decimal QtyOnHand { get; set; } = 0;

        /// <summary>Stock reservado (comprometido en pedidos, traslados en proceso)</summary>
        [Column("qty_reserved")]
        public decimal QtyReserved { get; set; } = 0;

        /// <summary>Stock en tránsito (enviado pero no recibido)</summary>
        [Column("qty_in_transit")]
        public decimal QtyInTransit { get; set; } = 0;

        /// <summary>Disponible = OnHand - Reserved</summary>
        [Column("qty_available")]
        public decimal QtyAvailable { get; set; } = 0;

        // ===== COSTOS =====

        [Column("average_cost")]
        public decimal AverageCost { get; set; } = 0;

        [Column("last_cost")]
        public decimal LastCost { get; set; } = 0;

        // ===== CONTROL =====

        /// <summary>Referencia al último movimiento que actualizó este registro</summary>
        [Column("last_transaction_id")]
        public int? LastTransactionId { get; set; }

        [Column("last_movement_date")]
        public DateTime? LastMovementDate { get; set; }

        // ===== AUDITORÍA =====

        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(150)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("rowpointer")]
        public Guid Rowpointer { get; set; } = Guid.NewGuid();
    }
}
