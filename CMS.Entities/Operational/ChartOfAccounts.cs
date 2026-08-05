// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ChartOfAccounts.cs
// PROPÓSITO: Entidad para el Plan de Cuentas (Chart of Accounts)
// DESCRIPCIÓN: Catálogo maestro de cuentas contables con estructura jerárquica
//              tipo SAP. Soporta hasta 6 niveles, cuentas de encabezado/detalle,
//              validaciones para transacciones, y dimensiones analíticas.
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2025-01-20
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Plan de Cuentas - Catálogo maestro de cuentas contables.
    /// Basado en mejores prácticas de SAP, Oracle Financials y otros ERP de clase mundial.
    /// </summary>
    [Table("chart_of_accounts")]
    public class ChartOfAccounts
    {
        // ===== PK + CÓDIGO =====

        [Key]
        [Column("id_chart_of_accounts")]
        public int IdChartOfAccounts { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        // ===== DESCRIPCIÓN Y ALIAS =====

        [MaxLength(500)]
        [Column("description")]
        public string? Description { get; set; }

        [MaxLength(100)]
        [Column("alias")]
        public string? Alias { get; set; }

        // ===== JERARQUÍA =====

        /// <summary>FK a la cuenta padre (NULL para cuentas de primer nivel)</summary>
        [Column("id_parent_account")]
        public int? IdParentAccount { get; set; }

        /// <summary>Nivel en la jerarquía: 1 (raíz) hasta 6 (máximo detalle)</summary>
        [Column("account_level")]
        public int AccountLevel { get; set; } = 1;

        /// <summary>TRUE = Cuenta de encabezado (totalizadora, NO acepta transacciones)</summary>
        [Column("is_header")]
        public bool IsHeader { get; set; } = false;

        /// <summary>TRUE = Cuenta de detalle (hoja, SÍ acepta transacciones)</summary>
        [Column("is_detail")]
        public bool IsDetail { get; set; } = true;

        /// <summary>TRUE = Tiene subcuentas (calculado automáticamente por trigger)</summary>
        [Column("has_children")]
        public bool HasChildren { get; set; } = false;

        // ===== TIPO DE CUENTA =====

        /// <summary>
        /// FK lógica cross-DB a cms.admin.chart_of_accounts_type(id_chart_of_accounts_type).
        /// Valores: 1=asset, 2=liability, 3=equity, 4=revenue, 5=expense, 6=off_balance.
        /// Default = 1 (Activo).
        /// </summary>
        [Column("id_chart_of_accounts_type")]
        public int IdChartOfAccountsType { get; set; } = 1;

        /// <summary>Subclasificación: Current, Non-Current, Operating, Financial, etc.</summary>
        [MaxLength(50)]
        [Column("account_class")]
        public string? AccountClass { get; set; }

        // ===== NATURALEZA DE LA CUENTA =====

        /// <summary>Naturaleza normal: Debit (deudora) o Credit (acreedora)</summary>
        [Required]
        [MaxLength(10)]
        [Column("normal_balance")]
        public string NormalBalance { get; set; } = "Debit";

        /// <summary>TRUE = naturaleza deudora, FALSE = naturaleza acreedora</summary>
        [Column("is_debit_balance")]
        public bool IsDebitBalance { get; set; } = true;

        // ===== CONTROL DE TRANSACCIONES =====

        /// <summary>Permite crear asientos manuales</summary>
        [Column("accepts_manual_entry")]
        public bool AcceptsManualEntry { get; set; } = true;

        /// <summary>Permite asientos automáticos generados por el sistema</summary>
        [Column("accepts_auto_entry")]
        public bool AcceptsAutoEntry { get; set; } = true;

        /// <summary>Todas las transacciones DEBEN incluir centro de costo</summary>
        [Column("requires_cost_center")]
        public bool RequiresCostCenter { get; set; } = false;

        /// <summary>Todas las transacciones DEBEN incluir código de proyecto</summary>
        [Column("requires_project")]
        public bool RequiresProject { get; set; } = false;

        /// <summary>Todas las transacciones DEBEN incluir socio de negocio</summary>
        [Column("requires_partner")]
        public bool RequiresPartner { get; set; } = false;

        // ===== MONEDA =====

        /// <summary>Moneda por defecto (ISO 4217: CRC, USD, EUR, etc.)</summary>
        [Required]
        [MaxLength(3)]
        [Column("currency_code")]
        public string CurrencyCode { get; set; } = "CRC";

        /// <summary>Permite transacciones en monedas diferentes a la moneda base</summary>
        [Column("allows_multi_currency")]
        public bool AllowsMultiCurrency { get; set; } = false;

        // ===== RECONCILIACIÓN =====

        /// <summary>Requiere conciliación bancaria</summary>
        [Column("is_reconciliation")]
        public bool IsReconciliation { get; set; } = false;

        // ===== IMPUESTOS =====

        [MaxLength(20)]
        [Column("tax_code")]
        public string? TaxCode { get; set; }

        /// <summary>La cuenta maneja o calcula impuestos</summary>
        [Column("is_tax_relevant")]
        public bool IsTaxRelevant { get; set; } = false;

        // ===== CUENTAS POR COBRAR / PAGAR =====

        /// <summary>Es una cuenta de cuentas por cobrar (clientes)</summary>
        [Column("is_receivable")]
        public bool IsReceivable { get; set; } = false;

        /// <summary>Es una cuenta de cuentas por pagar (proveedores)</summary>
        [Column("is_payable")]
        public bool IsPayable { get; set; } = false;

        // ===== FLUJO DE EFECTIVO =====

        /// <summary>Categoría de flujo de efectivo: Operating, Investing, Financing</summary>
        [MaxLength(50)]
        [Column("cash_flow_category")]
        public string? CashFlowCategory { get; set; }

        // ===== REPORTES FINANCIEROS =====

        /// <summary>Estado financiero: Balance Sheet, Income Statement, Cash Flow</summary>
        [MaxLength(50)]
        [Column("financial_statement")]
        public string? FinancialStatement { get; set; }

        /// <summary>Línea específica del reporte financiero</summary>
        [MaxLength(100)]
        [Column("report_line_item")]
        public string? ReportLineItem { get; set; }

        /// <summary>Orden de presentación en reportes</summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        // ===== PERÍODO DE VIGENCIA =====

        /// <summary>Fecha desde la cual está activa</summary>
        [Column("effective_date")]
        public DateOnly? EffectiveDate { get; set; }

        /// <summary>Fecha hasta la cual está activa (NULL = sin vencimiento)</summary>
        [Column("expiration_date")]
        public DateOnly? ExpirationDate { get; set; }

        // ===== ESTADO =====

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Bloqueada temporalmente (no permite transacciones)</summary>
        [Column("is_blocked")]
        public bool IsBlocked { get; set; } = false;

        [MaxLength(200)]
        [Column("block_reason")]
        public string? BlockReason { get; set; }

        // ===== NOTAS =====

        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

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

        // ===== NAVEGACIÓN (no mapeadas) =====

        /// <summary>Cuenta padre (para navegación en jerarquía)</summary>
        [NotMapped]
        public ChartOfAccounts? ParentAccount { get; set; }

        /// <summary>Subcuentas hijas (para árbol jerárquico)</summary>
        [NotMapped]
        public List<ChartOfAccounts> ChildAccounts { get; set; } = new();
    }

    /// <summary>Tipos de cuenta estándar (según clasificación contable internacional)</summary>
    public static class AccountType
    {
        public const string Asset = "Asset";              // Activo
        public const string Liability = "Liability";      // Pasivo
        public const string Equity = "Equity";            // Patrimonio
        public const string Revenue = "Revenue";          // Ingreso
        public const string Expense = "Expense";          // Gasto
        public const string OffBalance = "Off-Balance";   // Fuera de balance (cuentas de orden)
    }

    /// <summary>Clasificación de cuentas</summary>
    public static class AccountClass
    {
        // Activos / Pasivos
        public const string Current = "Current";          // Corriente / Circulante
        public const string NonCurrent = "Non-Current";   // No corriente / Fijo

        // Ingresos / Gastos
        public const string Operating = "Operating";      // Operacional
        public const string Financial = "Financial";      // Financiero
        public const string Extraordinary = "Extraordinary"; // Extraordinario

        // Patrimonio
        public const string Capital = "Capital";          // Capital
        public const string Retained = "Retained";        // Utilidades retenidas
    }

    /// <summary>Naturaleza normal de la cuenta</summary>
    public static class NormalBalanceType
    {
        public const string Debit = "Debit";    // Deudora (Activos, Gastos)
        public const string Credit = "Credit";  // Acreedora (Pasivos, Patrimonio, Ingresos)
    }

    /// <summary>Categorías de flujo de efectivo</summary>
    public static class CashFlowCategory
    {
        public const string Operating = "Operating";      // Actividades de operación
        public const string Investing = "Investing";      // Actividades de inversión
        public const string Financing = "Financing";      // Actividades de financiamiento
    }

    /// <summary>Estados financieros</summary>
    public static class FinancialStatementType
    {
        public const string BalanceSheet = "Balance Sheet";         // Estado de Situación Financiera
        public const string IncomeStatement = "Income Statement";   // Estado de Resultados
        public const string CashFlow = "Cash Flow";                 // Estado de Flujo de Efectivo
    }
}
