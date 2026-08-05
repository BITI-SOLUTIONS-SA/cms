// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ExchangeRate.cs
// PROPÓSITO: Entidad para el catálogo de tipos de tasa de cambio
// DESCRIPCIÓN: Catálogo maestro de tipos de tasa de cambio (PER, SER, UER, etc.)
//              utilizados en transacciones contables del sistema.
// AUTOR: BITI SOLUTIONS S.A
// CREADO: 2026-06-28
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Catálogo de tipos de tasa de cambio.
    /// Tabla: {company_schema}.exchange_rate
    /// </summary>
    [Table("exchange_rate")]
    public class ExchangeRate
    {
        // ===== PK =====

        [Key]
        [Column("id_exchange_rate")]
        public int IdExchangeRate { get; set; }

        // ===== CAMPOS PRINCIPALES =====

        /// <summary>Código único del tipo de tasa. Ej: PER, SER, UER</summary>
        [Required]
        [MaxLength(20)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción legible del tipo de tasa de cambio</summary>
        [MaxLength(200)]
        [Column("description")]
        public string? Description { get; set; }

        // ===== CONTROL =====

        /// <summary>Indica si el tipo de tasa está activo y disponible para uso</summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Orden de aparición en listas y selectores</summary>
        [Column("display_order")]
        public int DisplayOrder { get; set; } = 0;

        // ===== AUDITORÍA ESTÁNDAR =====

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

    // ===== CONSTANTES =====

    /// <summary>Códigos predefinidos de tipos de tasa de cambio</summary>
    public static class ExchangeRateCodes
    {
        /// <summary>Purchase Exchange Rate — Tasa de cambio de compra</summary>
        public const string PER = "PER";

        /// <summary>Sale Exchange Rate — Tasa de cambio de venta</summary>
        public const string SER = "SER";

        /// <summary>Undefined Exchange Rate — Tasa de cambio sin definir</summary>
        public const string UER = "UER";
    }
}
