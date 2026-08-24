// ================================================================================
// ARCHIVO: CMS.Entities/Operational/VendorEconomicActivity.cs
// PROPÓSITO: Actividades económicas por proveedor (vendor).
// DESCRIPCIÓN: Relaciona un vendor con una o más actividades económicas del
//              catálogo central (cms.admin.electronic_document_economic_activity).
//              Una sola actividad predeterminada por vendor.
//              Vive en la BD operacional de cada compañía ({schema}).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Actividad económica de un proveedor. Tabla: {schema}.vendor_economic_activity.
    /// </summary>
    [Table("vendor_economic_activity")]
    public class VendorEconomicActivity
    {
        [Key]
        [Column("id_vendor_economic_activity")]
        public int Id { get; set; }

        /// <summary>FK real al vendor del mismo schema.</summary>
        [Column("id_vendor")]
        public int IdVendor { get; set; }

        /// <summary>
        /// Relación lógica cross-DB al catálogo central
        /// cms.admin.electronic_document_economic_activity. El code y la
        /// description se resuelven desde el catálogo central por este id.
        /// </summary>
        [Column("id_electronic_document_economic_activity")]
        public int IdElectronicDocumentEconomicActivity { get; set; }

        /// <summary>Actividad económica predeterminada del vendor (una sola por vendor).</summary>
        [Column("is_default")]
        public bool IsDefault { get; set; }

        /// <summary>Registro activo/inactivo.</summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Notas.</summary>
        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        // ===== AUDITORÍA =====

        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = string.Empty;

        [Column("rowpointer")]
        public Guid RowPointer { get; set; } = Guid.NewGuid();

        // ===== NAVIGATION PROPERTIES =====

        /// <summary>Vendor propietario de esta actividad.</summary>
        [ForeignKey(nameof(IdVendor))]
        public Vendor? Vendor { get; set; }
    }
}
