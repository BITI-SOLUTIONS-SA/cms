// ================================================================================
// ARCHIVO: CMS.Entities/Operational/Item.cs
// PROPÓSITO: Entidad que representa un artículo/producto en el inventario
// DESCRIPCIÓN: Esta entidad se almacena en la BD de cada compañía, NO en la BD central
//              Esquema: {company_code}.item (ej: sinai.item, admin.item)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-02-19
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Representa un artículo o producto en el inventario.
    /// Se almacena en la base de datos de cada compañía.
    /// </summary>
    public class Item
    {
        [Key]
        [Column("id_item")]
        public int Id { get; set; }

        /// <summary>
        /// Código único del artículo (SKU)
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Nombre del artículo
        /// </summary>
        [Required]
        [MaxLength(200)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descripción detallada del artículo
        /// </summary>
        [Required]
        [MaxLength(1000)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        // ===== LABEL ITEM (Etiquetas) =====

        /// <summary>
        /// Nombre de la etiqueta del artículo
        /// </summary>
        [Required]
        [MaxLength(200)]
        [Column("label_item")]
        public string LabelItem { get; set; } = string.Empty;

        /// <summary>
        /// Precio para la etiqueta
        /// </summary>
        [Column("label_price", TypeName = "decimal(28,8)")]
        public decimal LabelPrice { get; set; }

        /// <summary>
        /// Código de barras para la etiqueta
        /// </summary>
        [Required]
        [MaxLength(200)]
        [Column("label_item_barcode")]
        public string LabelItemBarcode { get; set; } = string.Empty;

        /// <summary>
        /// Indica si es un artículo de etiqueta
        /// </summary>
        [Column("is_label_item")]
        public bool IsLabelItem { get; set; }

        /// <summary>
        /// Indica si se debe imprimir el nombre en la etiqueta
        /// </summary>
        [Column("print_label_name")]
        public bool PrintLabelName { get; set; } = true;

        /// <summary>
        /// Indica si se debe imprimir el precio en la etiqueta
        /// </summary>
        [Column("print_label_price")]
        public bool PrintLabelPrice { get; set; } = true;

        /// <summary>
        /// Indica si se debe imprimir el código de barras en la etiqueta
        /// </summary>
        [Column("print_label_barcode")]
        public bool PrintLabelBarcode { get; set; } = true;

        // ===== CONFIGURACIÓN DE TAMAÑO Y FORMATO DE ETIQUETA =====

        /// <summary>
        /// Ancho de la etiqueta en centímetros (horizontal)
        /// </summary>
        [Column("label_width_cm", TypeName = "decimal(5,2)")]
        public decimal LabelWidthCm { get; set; } = 4.0m;

        /// <summary>
        /// Alto de la etiqueta en centímetros (vertical)
        /// </summary>
        [Column("label_height_cm", TypeName = "decimal(5,2)")]
        public decimal LabelHeightCm { get; set; } = 2.0m;

        /// <summary>
        /// Orientación de la etiqueta: 'horizontal' o 'vertical'
        /// </summary>
        [MaxLength(20)]
        [Column("label_orientation")]
        public string LabelOrientation { get; set; } = "horizontal";

        /// <summary>
        /// Indica si se debe imprimir el borde/recuadro de la etiqueta
        /// </summary>
        [Column("print_label_border")]
        public bool PrintLabelBorder { get; set; } = true;

        /// <summary>
        /// Color del borde de la etiqueta (formato hex: #000000)
        /// </summary>
        [MaxLength(7)]
        [Column("label_border_color")]
        public string LabelBorderColor { get; set; } = "#000000";

        /// <summary>
        /// Color del nombre en la etiqueta (formato hex: #000000)
        /// </summary>
        [MaxLength(7)]
        [Column("label_name_color")]
        public string LabelNameColor { get; set; } = "#000000";

        /// <summary>
        /// Color del precio en la etiqueta (formato hex: #000000)
        /// </summary>
        [MaxLength(7)]
        [Column("label_price_color")]
        public string LabelPriceColor { get; set; } = "#16a34a";

        /// <summary>
        /// Color del código de barras en la etiqueta (formato hex: #000000)
        /// </summary>
        [MaxLength(7)]
        [Column("label_barcode_color")]
        public string LabelBarcodeColor { get; set; } = "#000000";

        // ===== CONFIGURACIÓN DE FUENTE Y FORMATO DE PRECIO =====

        /// <summary>
        /// Tamaño de fuente para la etiqueta en puntos
        /// </summary>
        [Column("label_font_size", TypeName = "decimal(4,1)")]
        public decimal LabelFontSize { get; set; } = 14.0m;

        /// <summary>
        /// Tipo de fuente para la etiqueta
        /// </summary>
        [MaxLength(50)]
        [Column("label_font_family")]
        public string LabelFontFamily { get; set; } = "Arial";

        /// <summary>
        /// Cantidad de decimales para el precio
        /// </summary>
        [Column("label_price_decimals")]
        public int LabelPriceDecimals { get; set; } = 2;

        /// <summary>
        /// Separador de miles: ',' o '.'
        /// </summary>
        [MaxLength(1)]
        [Column("label_thousand_separator")]
        public string LabelThousandSeparator { get; set; } = ",";

        /// <summary>
        /// Símbolo de moneda: '₡', '$', '€'
        /// </summary>
        [MaxLength(5)]
        [Column("label_currency_symbol")]
        public string LabelCurrencySymbol { get; set; } = "₡";

        /// <summary>
        /// Indica si se debe imprimir el símbolo de moneda
        /// </summary>
        [Column("print_currency_symbol")]
        public bool PrintCurrencySymbol { get; set; } = true;

        // ===== INFORMACIÓN BÁSICA =====

        /// <summary>
        /// Código de barras (UPC, EAN, etc.)
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("barcode")]
        public string Barcode { get; set; } = string.Empty;

        // ===== CLASIFICACIONES =====

        /// <summary>
        /// ID de la clasificación 1 (Categoría principal)
        /// </summary>
        [Column("id_classification1")]
        public int IdClassification1 { get; set; } = 1;

        /// <summary>
        /// ID de la clasificación 2 (Subcategoría)
        /// </summary>
        [Column("id_classification2")]
        public int IdClassification2 { get; set; } = 2;

        /// <summary>
        /// ID de la clasificación 3 (Familia)
        /// </summary>
        [Column("id_classification3")]
        public int IdClassification3 { get; set; } = 3;

        /// <summary>
        /// ID de la clasificación 4 (Grupo)
        /// </summary>
        [Column("id_classification4")]
        public int IdClassification4 { get; set; } = 4;

        /// <summary>
        /// ID de la clasificación 5 (Línea)
        /// </summary>
        [Column("id_classification5")]
        public int IdClassification5 { get; set; } = 5;

        /// <summary>
        /// ID de la clasificación 6 (Tipo)
        /// </summary>
        [Column("id_classification6")]
        public int IdClassification6 { get; set; } = 6;

        /// <summary>
        /// Marca del artículo
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Column("brand")]
        public string Brand { get; set; } = "Marca A";

        /// <summary>
        /// ID de la unidad de medida MH v4.4 (relación lógica cross-DB con
        /// cms.admin.electronic_document_unit_of_measure). NOT NULL, default 1 (Unid).
        /// </summary>
        [Column("id_electronic_document_unit_of_measure")]
        public int IdElectronicDocumentUnitOfMeasure { get; set; } = 1;

        // ===== PRECIOS =====

        /// <summary>
        /// Precio de costo
        /// </summary>
        [Column("cost_price", TypeName = "decimal(28,8)")]
        public decimal CostPrice { get; set; } = 1;

        /// <summary>
        /// Precio de venta
        /// </summary>
        [Column("sale_price", TypeName = "decimal(28,8)")]
        public decimal SalePrice { get; set; } = 1;

        /// <summary>
        /// Porcentaje de impuesto
        /// </summary>
        [Column("tax_rate", TypeName = "decimal(28,8)")]
        public decimal TaxRate { get; set; }

        // ===== FACTURACIÓN ELECTRÓNICA (Fase B) =====

        /// <summary>
        /// Cliente propietario del ítem. NOT NULL DEFAULT 1. FK → sinai.customer.
        /// </summary>
        [Column("id_customer")]
        public int IdCustomer { get; set; } = 1;

        /// <summary>
        /// Relación lógica cross-DB → cms.admin.electronic_document_tax_rate.id_electronic_document_tax_rate.
        /// Reemplaza al antiguo tax_rate_code (string). El code Hacienda (CodigoTarifaIVA v4.4)
        /// se resuelve por join contra el catálogo central. Default 8 (Tarifa general 13%).
        /// </summary>
        [Column("id_electronic_document_tax_rate")]
        public int IdElectronicDocumentTaxRate { get; set; } = 8;

        /// <summary>
        /// Código CAByS de 13 dígitos. Default '9799000000000'.
        /// </summary>
        [MaxLength(13)]
        [Column("cabys_code")]
        public string CabysCode { get; set; } = "9799000000000";

        /// <summary>
        /// Tipo de impuesto de Hacienda por defecto del artículo (CodigoImpuesto v4.4).
        /// Relación lógica cross-DB → cms.admin.electronic_document_tax_type.
        /// Default 1 (código '01' = Impuesto al valor agregado). Se preselecciona en la
        /// columna Impuestos de las líneas de emisión, aunque el usuario lo puede cambiar.
        /// </summary>
        [Column("id_electronic_document_tax_type")]
        public int IdElectronicDocumentTaxType { get; set; } = 1;

        // ===== STOCK =====

        /// <summary>
        /// Stock mínimo para alertas
        /// </summary>
        [Column("min_stock", TypeName = "decimal(28,8)")]
        public decimal MinStock { get; set; }

        /// <summary>
        /// Stock máximo recomendado
        /// </summary>
        [Column("max_stock", TypeName = "decimal(28,8)")]
        public decimal MaxStock { get; set; }

        /// <summary>
        /// Stock actual (calculado o manual)
        /// </summary>
        [Column("current_stock", TypeName = "decimal(28,8)")]
        public decimal CurrentStock { get; set; }

        // ===== CONFIGURACIÓN =====

        /// <summary>
        /// URL de la imagen del artículo
        /// </summary>
        [MaxLength(500)]
        [Column("image_url")]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Indica si se puede vender
        /// </summary>
        [Column("is_sellable")]
        public bool IsSellable { get; set; } = true;

        /// <summary>
        /// Indica si se puede comprar
        /// </summary>
        [Column("is_purchasable")]
        public bool IsPurchasable { get; set; } = true;

        /// <summary>
        /// Indica si requiere manejo de lotes
        /// </summary>
        [Column("track_lots")]
        public bool TrackLots { get; set; }

        /// <summary>
        /// Indica si requiere manejo de números de serie
        /// </summary>
        [Column("track_serial_numbers")]
        public bool TrackSerialNumbers { get; set; }

        /// <summary>
        /// Indica si el artículo está activo
        /// </summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // ===== AUDITORÍA =====

        /// <summary>
        /// Fecha de creación del registro
        /// </summary>
        [Column("createdate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha de última modificación
        /// </summary>
        [Column("record_date")]
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Usuario que creó el registro
        /// </summary>
        [Required]
        [MaxLength(150)]
        [Column("created_by")]
        public string CreatedBy { get; set; } = "SYSTEM";

        /// <summary>
        /// Usuario que modificó el registro
        /// </summary>
        [Required]
        [MaxLength(150)]
        [Column("updated_by")]
        public string UpdatedBy { get; set; } = "SYSTEM";

        /// <summary>
        /// Identificador único del registro (UUID)
        /// </summary>
        [Column("rowpointer")]
        public Guid RowPointer { get; set; } = Guid.NewGuid();
    }
}
