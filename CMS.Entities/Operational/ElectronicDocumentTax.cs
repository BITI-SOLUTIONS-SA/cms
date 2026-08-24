// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ElectronicDocumentTax.cs
// PROPÓSITO: Impuesto asociado a una línea de comprobante
// DESCRIPCIÓN: Desglose del IVA (u otros impuestos) por línea. Vive en la BD
//              operacional de cada compañía.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Impuesto por línea. Tabla: {schema}.electronic_document_tax.
    /// </summary>
    [Table("electronic_document_tax")]
    public class ElectronicDocumentTax
    {
        [Key]
        [Column("id_electronic_document_tax")]
        public int Id { get; set; }

        [Column("id_electronic_document_line")]
        public int IdElectronicDocumentLine { get; set; }

        [ForeignKey(nameof(IdElectronicDocumentLine))]
        public virtual ElectronicDocumentLine? ElectronicDocumentLine { get; set; }

        /// <summary>Código de impuesto (01=IVA...).</summary>
        [Required]
        [MaxLength(2)]
        [Column("tax_code")]
        public string TaxCode { get; set; } = "01";

        /// <summary>Código de tarifa (01..08).</summary>
        [MaxLength(2)]
        [Column("tax_rate_code")]
        public string? TaxRateCode { get; set; }

        [Column("tax_rate", TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; }

        [Column("tax_amount", TypeName = "decimal(18,5)")]
        public decimal TaxAmount { get; set; }

        // ===== Datos físicos de impuestos específicos / cálculo especial (v4.4) =====
        /// <summary>Cantidad de la unidad de medida a utilizar (códigos 03, 04, 06).</summary>
        [Column("unit_measure_qty", TypeName = "decimal(18,5)")]
        public decimal? UnitMeasureQty { get; set; }

        /// <summary>Volumen por unidad de consumo (código 05).</summary>
        [Column("volume_unit", TypeName = "decimal(18,5)")]
        public decimal? VolumeUnit { get; set; }

        /// <summary>Porcentaje usado en el cálculo (código 04 - bebidas alcohólicas).</summary>
        [Column("spec_percent", TypeName = "decimal(9,4)")]
        public decimal? SpecPercent { get; set; }

        /// <summary>Proporción calculada (código 04) = UnitMeasureQty * SpecPercent / 100.</summary>
        [Column("proportion", TypeName = "decimal(18,5)")]
        public decimal? Proportion { get; set; }

        /// <summary>Impuesto por unidad (códigos 03, 04, 05, 06).</summary>
        [Column("per_unit_tax", TypeName = "decimal(18,5)")]
        public decimal? PerUnitTax { get; set; }

        /// <summary>Base imponible especial digitada (código 07 - IVA cálculo especial).</summary>
        [Column("special_taxable_base", TypeName = "decimal(18,5)")]
        public decimal? SpecialTaxableBase { get; set; }

        /// <summary>TRUE si el producto tiene impuesto cobrado a nivel de fábrica (código 07).</summary>
        [Column("is_factory_tax")]
        public bool IsFactoryTax { get; set; }

        /// <summary>Descripción libre del impuesto (código 99 - Otros, v4.4). Máx 160 caracteres.</summary>
        [Column("tax_description")]
        public string? TaxDescription { get; set; }

        // ===== Exoneración por impuesto (v4.4) =====
        /// <summary>TRUE si este impuesto está exonerado (total o parcial).</summary>
        [Column("is_exonerated")]
        public bool IsExonerated { get; set; }

        /// <summary>Porcentaje del impuesto exonerado (0..100).</summary>
        [Column("exon_percent", TypeName = "decimal(5,2)")]
        public decimal ExonPercent { get; set; }

        /// <summary>MontoExoneracion v4.4: impuesto efectivamente exonerado.</summary>
        [Column("exon_amount", TypeName = "decimal(18,5)")]
        public decimal ExonAmount { get; set; }

        /// <summary>Código del tipo de documento de exoneración o de autorización.</summary>
        [MaxLength(2)]
        [Column("exon_document_type")]
        public string? ExonDocumentType { get; set; }

        /// <summary>Número de documento de exoneración o de autorización (máx 40).</summary>
        [MaxLength(40)]
        [Column("exon_document_number")]
        public string? ExonDocumentNumber { get; set; }

        /// <summary>Código de institución o dependencia que emitió la exoneración.</summary>
        [MaxLength(4)]
        [Column("exon_institution")]
        public string? ExonInstitution { get; set; }

        /// <summary>Fecha de emisión del documento de exoneración.</summary>
        [Column("exon_date")]
        public DateTime? ExonDate { get; set; }

        /// <summary>Número de artículo que establece la exoneración (opcional).</summary>
        [Column("exon_article")]
        public int? ExonArticle { get; set; }

        /// <summary>Número de inciso que establece la exoneración (obligatorio cuando exonerado).</summary>
        [Column("exon_subsection")]
        public int? ExonSubsection { get; set; }

        // ===== Auditoría =====
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
    }
}
