// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentTypeCatalog.cs
// PROPÓSITO: Entidad del catálogo CENTRAL parametrizable de tipos de documento
//            electrónico Hacienda CR v4.4.
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_type
//              Es compartida por TODAS las compañías y gobierna tanto la
//              visibilidad en la pantalla Emit como todo el comportamiento
//              diferenciado de generación de XML por tipo de documento.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_type", Schema = "admin")]
    public class ElectronicDocumentTypeCatalog
    {
        [Key]
        [Column("id_electronic_document_type")]
        public int Id { get; set; }

        [Required]
        [MaxLength(2)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(5)]
        [Column("short_code")]
        public string ShortCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("description")]
        public string? Description { get; set; }

        // ===== Versión del esquema (FK a admin.electronic_document_version) =====
        [Column("id_electronic_document_version")]
        public int IdVersion { get; set; }

        // ===== Metadatos XML / XSD (v4.4) =====
        [MaxLength(60)]
        [Column("xml_root")]
        public string? XmlRoot { get; set; }

        [MaxLength(80)]
        [Column("xml_namespace_segment")]
        public string? XmlNamespaceSegment { get; set; }

        [MaxLength(100)]
        [Column("xsd_file")]
        public string? XsdFile { get; set; }

        // ===== Clasificación / Naturaleza =====
        [Column("is_receiver_message")]
        public bool IsReceiverMessage { get; set; }

        [Column("is_sales_document")]
        public bool IsSalesDocument { get; set; } = true;

        // ===== Visibilidad / Presentación =====
        [Column("show_in_emit")]
        public bool ShowInEmit { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        // ===== Comportamiento Emisor / Receptor =====
        [Column("requires_receptor")]
        public bool RequiresReceptor { get; set; } = true;

        [Column("emisor_reduced")]
        public bool EmisorReduced { get; set; }

        [Column("allow_codigo_actividad_emisor")]
        public bool AllowCodigoActividadEmisor { get; set; } = true;

        [Column("allow_codigo_actividad_receptor")]
        public bool AllowCodigoActividadReceptor { get; set; } = true;

        // ===== Comportamiento Línea de detalle =====
        [Column("line_reduced")]
        public bool LineReduced { get; set; }

        [Column("allow_line_discount")]
        public bool AllowLineDiscount { get; set; } = true;

        [Column("allow_impuesto_asumido")]
        public bool AllowImpuestoAsumido { get; set; } = true;

        // ===== Comportamiento Resumen =====
        [Column("allow_resumen_classification")]
        public bool AllowResumenClassification { get; set; } = true;

        [Column("allow_total_descuentos")]
        public bool AllowTotalDescuentos { get; set; } = true;

        [Column("force_venta_neta_equals_venta")]
        public bool ForceVentaNetaEqualsVenta { get; set; }

        // ===== Condición de venta =====
        [MaxLength(2)]
        [Column("forced_sale_condition")]
        public string? ForcedSaleCondition { get; set; }

        // ===== Referencias =====
        [Column("requires_reference")]
        public bool RequiresReference { get; set; }

        [Column("emits_otros_clave")]
        public bool EmitsOtrosClave { get; set; }

        // ===== Control de saldos =====
        [Column("balance_controlled")]
        public bool BalanceControlled { get; set; }

        // ===== Seguridad =====
        [MaxLength(100)]
        [Column("permission_code")]
        public string? PermissionCode { get; set; }

        // ===== Estado =====
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        // ===== Auditoría =====
        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(30)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = "SYSTEM";

        [Required]
        [MaxLength(30)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = "SYSTEM";

        [Column("rowpointer")]
        public Guid RowPointer { get; set; } = Guid.NewGuid();
    }
}
