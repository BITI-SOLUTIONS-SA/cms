// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ElectronicDocumentLine.cs
// PROPÓSITO: Línea de detalle de un comprobante electrónico
// DESCRIPCIÓN: Cada línea referencia un item + código CAByS + impuestos + descuentos.
//              Vive en la BD operacional de cada compañía.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Línea de detalle. Tabla: {schema}.electronic_document_line.
    /// </summary>
    [Table("electronic_document_line")]
    public class ElectronicDocumentLine
    {
        [Key]
        [Column("id_electronic_document_line")]
        public int Id { get; set; }

        [Column("id_electronic_document")]
        public int IdElectronicDocument { get; set; }

        [ForeignKey(nameof(IdElectronicDocument))]
        public virtual ElectronicDocument? ElectronicDocument { get; set; }

        [Column("line_number")]
        public int LineNumber { get; set; }

        /// <summary>Item (FK lógica a {schema}.item).</summary>
        [Column("id_item")]
        public int? IdItem { get; set; }

        /// <summary>Código CAByS de 13 dígitos.</summary>
        [Required]
        [MaxLength(13)]
        [Column("cabys_code")]
        public string CabysCode { get; set; } = string.Empty;

        [MaxLength(20)]
        [Column("item_code")]
        public string? ItemCode { get; set; }

        /// <summary>Tipo de impuesto de Hacienda de la línea (CodigoImpuesto v4.4).
        /// Relación lógica cross-DB → cms.admin.electronic_document_tax_type. Default 1 (01=IVA).</summary>
        [Column("id_electronic_document_tax_type")]
        public int IdElectronicDocumentTaxType { get; set; } = 1;

        [Required]
        [MaxLength(200)]
        [Column("detail")]
        public string Detail { get; set; } = string.Empty;

        /// <summary>TRUE=servicio, FALSE=mercancía/bien (afecta el desglose del resumen).</summary>
        [Column("is_service")]
        public bool IsService { get; set; } = true;

        [Column("quantity", TypeName = "decimal(16,3)")]
        public decimal Quantity { get; set; }

        [Required]
        [MaxLength(15)]
        [Column("unit_measure")]
        public string UnitMeasure { get; set; } = "Unid";

        /// <summary>Precio unitario base (sin IVA).</summary>
        [Column("unit_price", TypeName = "decimal(18,5)")]
        public decimal UnitPrice { get; set; }

        [Column("total_amount", TypeName = "decimal(18,5)")]
        public decimal TotalAmount { get; set; }

        // ===== Descuento (naturaleza obligatoria v4.4) =====
        [Column("discount_amount", TypeName = "decimal(18,5)")]
        public decimal DiscountAmount { get; set; }

        /// <summary>Naturaleza del descuento: 01=Regalía,04=Volumen,05=Temporada,06=Promoción.</summary>
        [MaxLength(2)]
        [Column("discount_nature")]
        public string? DiscountNature { get; set; }

        /// <summary>
        /// JSON con la lista de descuentos de la línea (Hacienda v4.4 admite hasta 5
        /// nodos &lt;Descuento&gt;). Forma: [{"nature":"06","amount":70.0}].
        /// Los escalares DiscountAmount/DiscountNature guardan la suma total y la
        /// naturaleza principal para compatibilidad con totales, PDF y ruta REP.
        /// </summary>
        [Column("discounts")]
        public string? Discounts { get; set; }

        [Column("sub_total", TypeName = "decimal(18,5)")]
        public decimal SubTotal { get; set; }

        /// <summary>Base imponible (desglosada; nunca el total con IVA).</summary>
        [Column("taxable_base", TypeName = "decimal(18,5)")]
        public decimal TaxableBase { get; set; }

        [Column("total_tax", TypeName = "decimal(18,5)")]
        public decimal TotalTax { get; set; }

        [Column("total_line", TypeName = "decimal(18,5)")]
        public decimal TotalLine { get; set; }

        // ===== Campos fiscales adicionales del XML v4.4 =====
        /// <summary>ImpuestoAsumidoEmisorFabrica — impuesto asumido por el emisor.</summary>
        [Column("impuesto_asumido_emisor", TypeName = "decimal(18,5)")]
        public decimal ImpuestoAsumidoEmisor { get; set; }

        /// <summary>ImpuestoNeto = TotalTax - ImpuestoAsumidoEmisor.</summary>
        [Column("impuesto_neto", TypeName = "decimal(18,5)")]
        public decimal ImpuestoNeto { get; set; }

        /// <summary>MontoTotalLinea = TotalLine + ImpuestoAsumidoEmisor (total con impuesto asumido).</summary>
        [Column("monto_total_linea", TypeName = "decimal(18,5)")]
        public decimal MontoTotalLinea { get; set; }

        /// <summary>Código de tarifa IVA del impuesto principal de la línea (p.ej. 08 = 13%).</summary>
        [MaxLength(2)]
        [Column("tax_rate_code_iva")]
        public string? TaxRateCodeIva { get; set; }
        /// <summary>Porcentaje de IVA aplicado (p.ej. 0.13).</summary>
        [Column("tax_rate_iva", TypeName = "decimal(5,4)")]
        public decimal? TaxRateIva { get; set; }

        /// <summary>Monto del IVA de la línea (igual que TotalTax cuando solo hay un impuesto).</summary>
        [Column("monto_tax_iva", TypeName = "decimal(18,5)")]
        public decimal? MontoTaxIva { get; set; }

        // ===== Exoneración (bloque <Exoneracion> del XML v4.4) =====
        /// <summary>TRUE si la línea está exonerada (total o parcialmente según ExonPercent).</summary>
        [Column("is_exonerated")]
        public bool IsExonerated { get; set; }

        /// <summary>TipoDocumento de la exoneración (p.ej. 03, 99).</summary>
        [MaxLength(2)]
        [Column("exon_document_type")]
        public string? ExonDocumentType { get; set; }

        /// <summary>NumeroDocumento de la autorización de exoneración.</summary>
        [MaxLength(40)]
        [Column("exon_document_number")]
        public string? ExonDocumentNumber { get; set; }

        /// <summary>NombreInstitucion que emite la exoneración.</summary>
        [MaxLength(160)]
        [Column("exon_institution")]
        public string? ExonInstitution { get; set; }
        /// <summary>Número de artículo que establece la exoneración o autorización (opcional, máx 6 dígitos).</summary>
        [Column("exon_article")]
        public int? ExonArticle { get; set; }

        /// <summary>Número de inciso que establece la exoneración o autorización (obligatorio cuando hay exoneración, máx 6 dígitos).</summary>
        [Column("exon_subsection")]
        public int? ExonSubsection { get; set; }

        [Column("exon_date")]
        public DateTime? ExonDate { get; set; }

        /// <summary>PorcentajeExoneracion (0..100). 100 = exoneración total.</summary>
        [Column("exon_percent", TypeName = "decimal(5,2)")]
        public decimal ExonPercent { get; set; }

        /// <summary>MontoExoneracion = TaxAmount * ExonPercent/100.</summary>
        [Column("exon_amount", TypeName = "decimal(18,5)")]
        public decimal ExonAmount { get; set; }

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

        // ===== Navegación =====
        [InverseProperty(nameof(ElectronicDocumentTax.ElectronicDocumentLine))]
        public virtual ICollection<ElectronicDocumentTax> Taxes { get; set; } = new List<ElectronicDocumentTax>();

        [InverseProperty(nameof(ElectronicDocumentDiscountLine.ElectronicDocumentLine))]
        public virtual ICollection<ElectronicDocumentDiscountLine> DiscountLines { get; set; } = new List<ElectronicDocumentDiscountLine>();
    }
}
