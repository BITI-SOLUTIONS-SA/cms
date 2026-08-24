// ================================================================================
// ARCHIVO: CMS.Entities/Operational/StockTransferLine.cs
// PROPÓSITO: Línea de un traslado de stock (artículo + cantidad)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-06-12
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Línea de artículo dentro de un documento de traslado de stock.
    /// </summary>
    public class StockTransferLine
    {
        [Key]
        [Column("id_stock_transfer_line")]
        public int Id { get; set; }

        [Column("id_stock_transfer")]
        public int IdStockTransfer { get; set; }

        [Column("line_number")]
        public int LineNumber { get; set; } = 1;

        // ===== ARTÍCULO =====

        /// <summary>FK lógica a {schema}.item</summary>
        [Column("id_item")]
        public int IdItem { get; set; }

        /// <summary>Snapshot del código del artículo al momento del traslado</summary>
        [Required]
        [MaxLength(30)]
        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>Snapshot del nombre del artículo al momento del traslado</summary>
        [Required]
        [MaxLength(200)]
        [Column("item_name")]
        public string ItemName { get; set; } = string.Empty;

        // ===== CANTIDADES =====

        [Column("qty_requested")]
        public decimal QtyRequested { get; set; } = 0;

        [Column("qty_transferred")]
        public decimal QtyTransferred { get; set; } = 0;

        // ===== UNIDAD DE MEDIDA =====

        /// <summary>FK lógica cross-DB a cms.admin.electronic_document_unit_of_measure (MH v4.4)</summary>
        [Column("id_electronic_document_unit_of_measure")]
        public int? IdElectronicDocumentUnitOfMeasure { get; set; }

        [MaxLength(10)]
        [Column("unit_of_measure_code")]
        public string? UnitOfMeasureCode { get; set; }

        // ===== LOTE / VENCIMIENTO =====

        [MaxLength(50)]
        [Column("lot_number")]
        public string? LotNumber { get; set; }

        [Column("expiry_date")]
        public DateOnly? ExpiryDate { get; set; }

        [MaxLength(500)]
        [Column("notes")]
        public string? Notes { get; set; }

        // ===== AUDITORÍA =====

        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("rowpointer")]
        public Guid Rowpointer { get; set; } = Guid.NewGuid();
    }
}
