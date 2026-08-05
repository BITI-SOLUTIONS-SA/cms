// ================================================================================
// ARCHIVO: CMS.Entities/Operational/JournalEntry.cs
// PROPÓSITO: Entidades para Asientos de Diario (Journal Entries)
// DESCRIPCIÓN: Encabezado y líneas de asientos contables. Basado en mejores
//              prácticas de SAP FI, Oracle Financials, y otros ERP reconocidos.
//              Soporta multi-moneda, reversiones, aprobaciones, y trazabilidad.
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-20
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    // ============================================================
    // ENCABEZADO DE ASIENTO DE DIARIO
    // ============================================================

    /// <summary>
    /// Encabezado de asiento de diario (journal entry header).
    /// Registra todas las transacciones contables del sistema.
    /// </summary>
    [Table("journal_entry")]
    public class JournalEntry
    {
        // ===== PK + IDENTIFICACIÓN =====

        [Key]
        [Column("id_journal_entry")]
        public int IdJournalEntry { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("entry_number")]
        public string EntryNumber { get; set; } = string.Empty;

        /// <summary>
        /// FK lógica cross-DB a cms.admin.type_accounting(id_type_accounting).
        /// Define el enfoque contable del asiento: general (1), fiscal (2), corporativo (3).
        /// Default = 1 (Contabilidad General).
        /// </summary>
        [Column("id_type_accounting")]
        public int IdTypeAccounting { get; set; } = 1;

        /// <summary>
        /// FK lógica cross-DB a cms.admin.journal_entry_class(id_journal_entry_class).
        /// Clasifica el asiento según su naturaleza: N=Normal(3), C=Closing(1), D=Exchange Rate Diff(2), B=Banks(4).
        /// Default = 3 (Normal).
        /// </summary>
        [Column("id_journal_entry_class")]
        public int IdJournalEntryClass { get; set; } = 3;

        /// <summary>
        /// FK lógica cross-DB a cms.admin.journal_entry_status(id_journal_entry_status).
        /// Normaliza el estado: Draft(1), Posted(2), Reversed(3), Cancelled(4).
        /// El campo status (varchar) se mantiene por compatibilidad. Default = 1 (Draft).
        /// </summary>
        [Column("id_journal_entry_status")]
        public int IdJournalEntryStatus { get; set; } = 1;

        // ===== REFERENCIA =====

        /// <summary>Referencia del documento origen (factura, recibo, etc.)</summary>
        [MaxLength(100)]
        [Column("reference")]
        public string? Reference { get; set; }

        // ===== FECHAS =====

        [Column("entry_date")]
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Column("posting_date")]
        public DateOnly PostingDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        // ===== MENÚ ORIGEN =====

        /// <summary>FK lógica al menú de origen del asiento (admin.menu.id_menu)</summary>
        [Column("id_menu")]
        public int? IdMenu { get; set; }

        // ===== ESTADO Y CONTROL =====

        /// <summary>
        /// Estado calculado desde IdJournalEntryStatus.
        /// Propiedad [NotMapped]: no se persiste en DB, se deriva de IdJournalEntryStatus.
        /// Mapeo: 1=Draft, 2=Posted, 3=Reversed, 4=Cancelled.
        /// </summary>
        [NotMapped]
        public string Status
        {
            get => IdJournalEntryStatus switch
            {
                1 => JournalEntryStatus.Draft,
                2 => JournalEntryStatus.Posted,
                3 => JournalEntryStatus.Reversed,
                4 => JournalEntryStatus.Cancelled,
                _ => JournalEntryStatus.Draft
            };
            set => IdJournalEntryStatus = value switch
            {
                JournalEntryStatus.Draft      => 1,
                JournalEntryStatus.Posted     => 2,
                JournalEntryStatus.Reversed   => 3,
                JournalEntryStatus.Cancelled  => 4,
                _                             => 1
            };
        }

        [Column("is_reversing")]
        public bool IsReversing { get; set; } = false;

        /// <summary>FK al asiento que está siendo revertido</summary>
        [Column("id_reversed_entry")]
        public int? IdReversedEntry { get; set; }

        [Column("reversal_date")]
        public DateOnly? ReversalDate { get; set; }

        // ===== TOTALES =====

        [Column("debit_total")]
        public decimal DebitTotal { get; set; } = 0.00m;

        [Column("credit_total")]
        public decimal CreditTotal { get; set; } = 0.00m;

        // ===== CONTROL DE APROBACIÓN =====

        [Column("requires_approval")]
        public bool RequiresApproval { get; set; } = false;

        [Column("approved_date")]
        public DateTime? ApprovedDate { get; set; }

        /// <summary>FK lógica cross-DB a cms.admin.user</summary>
        [Column("approved_by_user_id")]
        public int? ApprovedByUserId { get; set; }

        [MaxLength(500)]
        [Column("approval_notes")]
        public string? ApprovalNotes { get; set; }

        // ===== AUDITORÍA Y CONTROL =====

        [Column("posted_date")]
        public DateTime? PostedDate { get; set; }

        /// <summary>FK lógica cross-DB a cms.admin.user</summary>
        [Column("posted_by_user_id")]
        public int? PostedByUserId { get; set; }

        [Column("cancelled_date")]
        public DateTime? CancelledDate { get; set; }

        /// <summary>FK lógica cross-DB a cms.admin.user</summary>
        [Column("cancelled_by_user_id")]
        public int? CancelledByUserId { get; set; }

        /// <summary>FK a journal_entry_cancel_reason</summary>
        [Column("id_journal_entry_cancel_reason")]
        public int? IdJournalEntryCancelReason { get; set; }

        // ===== MONEDA (IDs de admin.currency) =====

        /// <summary>ID de la moneda base de la compañía (admin.currency.id_currency). Default 33 = CRC</summary>
        [Column("currency_local")]
        public int CurrencyLocal { get; set; } = 33;

        /// <summary>ID de la moneda de tipo de cambio secundaria (admin.currency.id_currency). Default 141 = USD</summary>
        [Column("currency_exchange")]
        public int CurrencyExchange { get; set; } = 141;

        // ===== CAMPOS DE AUDITORÍA ESTÁNDAR =====

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

        /// <summary>Líneas del asiento (detalle)</summary>
        [NotMapped]
        public List<JournalEntryLine> Lines { get; set; } = new();

        /// <summary>Razón de cancelación (si fue cancelado)</summary>
        public virtual JournalEntryCancelReason? CancelReason { get; set; }
    }

    // ============================================================
    // LÍNEA DE ASIENTO DE DIARIO
    // ============================================================

    /// <summary>
    /// Línea de asiento de diario (journal entry line).
    /// Detalle de las partidas contables (débitos y créditos).
    /// PK compuesta: (IdJournalEntry, IdJournalEntryLine)
    /// IdJournalEntryLine es el número de línea secuencial dentro del asiento (1, 2, 3...)
    /// </summary>
    [Table("journal_entry_line")]
    public class JournalEntryLine
    {
        // ===== PK COMPUESTA + RELACIÓN CON ENCABEZADO =====

        [Key]
        [Column("id_journal_entry", Order = 0)]
        public int IdJournalEntry { get; set; }

        [Key]
        [Column("id_journal_entry_line", Order = 1)]
        public int IdJournalEntryLine { get; set; }

        // ===== CUENTA CONTABLE =====

        /// <summary>FK lógica a sinai.chart_of_accounts (REQUERIDO)</summary>
        [Required]
        [Column("id_chart_of_accounts")]
        public int IdChartOfAccounts { get; set; }

        // ===== DESCRIPCIÓN Y REFERENCIA =====

        [Required]
        [MaxLength(500)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Column("reference")]
        public string Reference { get; set; } = string.Empty;

        // ===== DÉBITO / CRÉDITO =====

        [Column("debit_amount")]
        public decimal DebitAmount { get; set; } = 0.00m;

        [Column("credit_amount")]
        public decimal CreditAmount { get; set; } = 0.00m;

        // ===== CENTRO DE COSTO =====

        /// <summary>FK real a sinai.cost_center.id_cost_center. Se asigna automáticamente desde el parámetro cost_center_default si no viene en el request.</summary>
        [Required]
        [Column("id_cost_center")]
        public int IdCostCenter { get; set; }

        // Navegación
        [ForeignKey(nameof(IdCostCenter))]
        public CostCenter? CostCenter { get; set; }

        // ===== ORIGEN =====

        /// <summary>
        /// FK lógica cross-DB a cms.admin.journal_entry_type_origin(id_journal_entry_type_origin).
        /// Define desde qué módulo fue generado el asiento (ej: manual, accounts_payable, sales).
        /// No se declara FK real porque admin.journal_entry_type_origin vive en la BD central (cms).
        /// </summary>
        [Required]
        [Column("id_journal_entry_type_origin")]
        public int IdJournalEntryTypeOrigin { get; set; }

        /// <summary>FK lógica al documento de origen (id_document_origin)</summary>
        [Required]
        [Column("id_document_origin")]
        public int IdDocumentOrigin { get; set; }

        // ===== CAMPOS DE AUDITORÍA ESTÁNDAR =====

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
    // CONSTANTES
    // ============================================================

    /// <summary>Tipos de asiento de diario</summary>
    public static class JournalEntryType
    {
        public const string Manual = "Manual";              // Asiento manual
        public const string Automatic = "Automatic";        // Asiento automático
        public const string Reversal = "Reversal";          // Asiento de reversión
        public const string Adjustment = "Adjustment";      // Asiento de ajuste
        public const string Closing = "Closing";            // Asiento de cierre
        public const string Opening = "Opening";            // Asiento de apertura
    }

    /// <summary>Estados de asiento de diario</summary>
    public static class JournalEntryStatus
    {
        public const string Draft = "Draft";                // Borrador
        public const string Posted = "Posted";              // Contabilizado
        public const string Reversed = "Reversed";          // Revertido
        public const string Cancelled = "Cancelled";        // Cancelado
    }

    /// <summary>Tipos de socio de negocio</summary>
    public static class BusinessPartnerType
    {
        public const string Customer = "Customer";          // Cliente
        public const string Supplier = "Supplier";          // Proveedor
        public const string Employee = "Employee";          // Empleado
        public const string Other = "Other";                // Otro
    }

    /// <summary>Módulos fuente de asientos automáticos</summary>
    public static class JournalEntrySourceModule
    {
        public const string Sales = "Sales";                // Ventas
        public const string Purchasing = "Purchasing";      // Compras
        public const string Inventory = "Inventory";        // Inventario
        public const string Payroll = "Payroll";            // Nómina
        public const string FixedAssets = "FixedAssets";    // Activos Fijos
        public const string Banking = "Banking";            // Banca
        public const string Accounting = "Accounting";      // Contabilidad
    }

    /// <summary>Tipos de documento fuente</summary>
    public static class JournalEntrySourceDocumentType
    {
        public const string Invoice = "Invoice";            // Factura
        public const string Payment = "Payment";            // Pago
        public const string Receipt = "Receipt";            // Recibo
        public const string Adjustment = "Adjustment";      // Ajuste
        public const string Depreciation = "Depreciation";  // Depreciación
        public const string Transfer = "Transfer";          // Traslado
    }
}
