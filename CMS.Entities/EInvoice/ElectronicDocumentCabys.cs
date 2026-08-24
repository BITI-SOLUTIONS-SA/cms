// ================================================================================
// ARCHIVO: CMS.Entities/EInvoice/ElectronicDocumentCabys.cs
// PROPÓSITO: Entidad del catálogo CENTRAL de códigos CAByS para facturación
//            electrónica (versión de admin.cabys con relaciones a los catálogos
//            de tarifa y tipo de impuesto de Hacienda).
// DESCRIPCIÓN: Se almacena en la BD central (cms), schema admin.
//              Tabla: admin.electronic_document_cabys
//              Compartida por TODAS las compañías. A diferencia de admin.cabys,
//              NO incluye tax_rate ni category; en lugar de tax_rate_code usa
//              id_electronic_document_tax_rate (FK) y agrega
//              id_electronic_document_tax_type (FK).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.EInvoice
{
    [Table("electronic_document_cabys", Schema = "admin")]
    public class ElectronicDocumentCabys
    {
        [Key]
        [Column("id_electronic_document_cabys")]
        public int Id { get; set; }

        /// <summary>Código CAByS de 13 dígitos.</summary>
        [Required]
        [MaxLength(13)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Descripción del bien o servicio.</summary>
        [Required]
        [MaxLength(1000)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Relación al catálogo central de tarifas (admin.electronic_document_tax_rate).
        /// NOT NULL, default 8 (Tarifa general 13%).
        /// </summary>
        [Column("id_electronic_document_tax_rate")]
        public int IdElectronicDocumentTaxRate { get; set; } = 8;

        /// <summary>
        /// Relación al catálogo central de tipos de impuesto (admin.electronic_document_tax_type).
        /// NOT NULL, default 1 (IVA).
        /// </summary>
        [Column("id_electronic_document_tax_type")]
        public int IdElectronicDocumentTaxType { get; set; } = 1;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ===== Navegación (opcional) =====
        [ForeignKey(nameof(IdElectronicDocumentTaxRate))]
        public ElectronicDocumentTaxRate? TaxRate { get; set; }

        [ForeignKey(nameof(IdElectronicDocumentTaxType))]
        public ElectronicDocumentTaxType? TaxType { get; set; }

        // ===== Auditoría =====
        // Generadas por la BD (DEFAULT now() / gen_random_uuid() y trigger de auditoría).
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [Column("created_by")]
        [MaxLength(30)]
        public string CreatedBy { get; set; } = default!;

        [Column("updated_by")]
        [MaxLength(30)]
        public string UpdatedBy { get; set; } = default!;

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("rowpointer")]
        public Guid RowPointer { get; set; }
    }
}
