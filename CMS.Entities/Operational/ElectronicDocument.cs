// ================================================================================
// ARCHIVO: CMS.Entities/Operational/ElectronicDocument.cs
// PROPÓSITO: Cabecera del comprobante electrónico (FE/NC/ND/TE/FEC/REP)
// DESCRIPCIÓN: Agregado principal del documento fiscal. Almacena la Clave Numérica,
//              el XML firmado, la respuesta de Hacienda y el PDF. Vive en la BD
//              operacional de cada compañía.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    /// <summary>
    /// Cabecera de comprobante electrónico. Tabla: {schema}.electronic_document.
    /// </summary>
    [Table("electronic_document")]
    public class ElectronicDocument
    {
        [Key]
        [Column("id_electronic_document")]
        public int Id { get; set; }

        [Column("id_customer_issuer")]
        public int IdCustomerIssuer { get; set; }

        /// <summary>Receptor (FK a customer).</summary>
        [Column("id_customer_receptor")]
        public int? IdCustomerReceptor { get; set; }

        /// <summary>Tipo de documento (2 díg.): 01,02,03,04,08,09.</summary>
        [Required]
        [MaxLength(2)]
        [Column("document_type")]
        public string DocumentType { get; set; } = "01";

        /// <summary>Clave Numérica de 50 dígitos.</summary>
        [MaxLength(50)]
        [Column("clave")]
        public string? Clave { get; set; }

        /// <summary>Consecutivo de 20 dígitos.</summary>
        [MaxLength(20)]
        [Column("consecutive")]
        public string? Consecutive { get; set; }

        /// <summary>Situación (1 díg.): 1=normal, 2=contingencia, 3=sin internet.</summary>
        [Required]
        [MaxLength(2)]
        [Column("situation")]
        public string Situation { get; set; } = "1";

        /// <summary>Estado interno (máquina de estados).</summary>
        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "Borrador";

        /// <summary>Fecha de emisión (INMUTABLE una vez generada; se conserva en contingencia).</summary>
        [Column("issue_date")]
        public DateTime IssueDate { get; set; }

        /// <summary>Condición de venta (01=contado, 02=crédito...).</summary>
        [MaxLength(2)]
        [Column("sale_condition")]
        public string SaleCondition { get; set; } = "01";

        /// <summary>Plazo de crédito en días (para condición 02).</summary>
        [Column("credit_term")]
        public int? CreditTerm { get; set; }

        /// <summary>Medio de pago (01=efectivo, 02=tarjeta...).</summary>
        [MaxLength(2)]
        [Column("payment_method")]
        public string PaymentMethod { get; set; } = "01";

        // ===== Moneda y montos =====
        [Required]
        [MaxLength(3)]
        [Column("currency")]
        public string Currency { get; set; } = "CRC";

        [Column("exchange_rate", TypeName = "decimal(18,5)")]
        public decimal ExchangeRate { get; set; } = 1;

        [Column("sub_total", TypeName = "decimal(18,5)")]
        public decimal SubTotal { get; set; }

        [Column("total_discount", TypeName = "decimal(18,5)")]
        public decimal TotalDiscount { get; set; }

        [Column("total_taxable", TypeName = "decimal(18,5)")]
        public decimal TotalTaxable { get; set; }

        [Column("total_exempt", TypeName = "decimal(18,5)")]
        public decimal TotalExempt { get; set; }

        [Column("total_taxes", TypeName = "decimal(18,5)")]
        public decimal TotalTaxes { get; set; }

        [Column("total", TypeName = "decimal(18,5)")]
        public decimal Total { get; set; }

        /// <summary>TRUE si el comprobante completo es exonerado (todas las líneas exoneradas).</summary>
        [Column("is_exonerated")]
        public bool IsExonerated { get; set; }

        // ===== Artefactos fiscales (almacenados en BD) =====
        /// <summary>XML firmado XAdES-EPES.</summary>
        [Column("xml_signed")]
        public string? XmlSigned { get; set; }

        /// <summary>Respuesta MensajeHacienda (XML).</summary>
        [Column("xml_response")]
        public string? XmlResponse { get; set; }

        /// <summary>Estado reportado por Hacienda: aceptado/rechazado/procesando.</summary>
        [MaxLength(20)]
        [Column("hacienda_status")]
        public string? HaciendaStatus { get; set; }

        /// <summary>DetalleMensaje de la respuesta de Hacienda (motivo aceptación/rechazo).</summary>
        [Column("hacienda_detail")]
        public string? HaciendaDetail { get; set; }

        /// <summary>Representación PDF del comprobante.</summary>
        [Column("pdf_document")]
        public byte[]? PdfDocument { get; set; }

        [Column("submitted_at")]
        public DateTime? SubmittedAt { get; set; }

        [Column("accepted_at")]
        public DateTime? AcceptedAt { get; set; }

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

        // ===== Datos fiscales del Emisor (guardados al emitir) =====
        [MaxLength(200)]
        [Column("emisor_nombre")]
        public string? EmisorNombre { get; set; }

        [MaxLength(200)]
        [Column("emisor_nombre_comercial")]
        public string? EmisorNombreComercial { get; set; }

        [MaxLength(2)]
        [Column("emisor_identificacion_tipo")]
        public string? EmisorIdentificacionTipo { get; set; }

        [MaxLength(20)]
        [Column("emisor_identificacion_numero")]
        public string? EmisorIdentificacionNumero { get; set; }

        [MaxLength(100)]
        [Column("emisor_correo")]
        public string? EmisorCorreo { get; set; }

        [MaxLength(1)]
        [Column("emisor_ubicacion_provincia")]
        public string? EmisorUbicacionProvincia { get; set; }

        [MaxLength(2)]
        [Column("emisor_ubicacion_canton")]
        public string? EmisorUbicacionCanton { get; set; }

        [MaxLength(2)]
        [Column("emisor_ubicacion_distrito")]
        public string? EmisorUbicacionDistrito { get; set; }

        [MaxLength(2)]
        [Column("emisor_ubicacion_barrio")]
        public string? EmisorUbicacionBarrio { get; set; }

        [MaxLength(250)]
        [Column("emisor_ubicacion_otras_senas")]
        public string? EmisorUbicacionOtrasSenas { get; set; }

        [MaxLength(3)]
        [Column("emisor_telefono_codigo_pais")]
        public string? EmisorTelefonoCodigoPais { get; set; }

        [MaxLength(20)]
        [Column("emisor_telefono_numero")]
        public string? EmisorTelefonoNumero { get; set; }

        [MaxLength(6)]
        [Column("codigo_actividad_emisor")]
        public string? CodigoActividadEmisor { get; set; }

        [MaxLength(10)]
        [Column("proveedor_sistemas")]
        public string? ProveedorSistemas { get; set; }

        // ===== Datos fiscales del Receptor (guardados al emitir) =====
        [MaxLength(200)]
        [Column("receptor_nombre")]
        public string? ReceptorNombre { get; set; }

        [MaxLength(200)]
        [Column("receptor_nombre_comercial")]
        public string? ReceptorNombreComercial { get; set; }

        [MaxLength(2)]
        [Column("receptor_identificacion_tipo")]
        public string? ReceptorIdentificacionTipo { get; set; }

        [MaxLength(20)]
        [Column("receptor_identificacion_numero")]
        public string? ReceptorIdentificacionNumero { get; set; }

        [MaxLength(30)]
        [Column("receptor_identificacion_extranjero")]
        public string? ReceptorIdentificacionExtranjero { get; set; }

        [MaxLength(100)]
        [Column("receptor_correo")]
        public string? ReceptorCorreo { get; set; }

        [MaxLength(1)]
        [Column("receptor_ubicacion_provincia")]
        public string? ReceptorUbicacionProvincia { get; set; }

        [MaxLength(2)]
        [Column("receptor_ubicacion_canton")]
        public string? ReceptorUbicacionCanton { get; set; }

        [MaxLength(2)]
        [Column("receptor_ubicacion_distrito")]
        public string? ReceptorUbicacionDistrito { get; set; }

        [MaxLength(250)]
        [Column("receptor_ubicacion_otras_senas")]
        public string? ReceptorUbicacionOtrasSenas { get; set; }

        [MaxLength(3)]
        [Column("receptor_telefono_codigo_pais")]
        public string? ReceptorTelefonoCodigoPais { get; set; }

        [MaxLength(20)]
        [Column("receptor_telefono_numero")]
        public string? ReceptorTelefonoNumero { get; set; }

        // ===== Resumen Fiscal completo (ResumenFactura del XML v4.4) =====
        [Column("total_serv_gravados", TypeName = "decimal(18,5)")]
        public decimal? TotalServGravados { get; set; }

        [Column("total_serv_exentos", TypeName = "decimal(18,5)")]
        public decimal? TotalServExentos { get; set; }

        [Column("total_serv_exonerado", TypeName = "decimal(18,5)")]
        public decimal? TotalServExonerado { get; set; }

        [Column("total_mercancias_gravadas", TypeName = "decimal(18,5)")]
        public decimal? TotalMercanciasGravadas { get; set; }

        [Column("total_mercancias_exentas", TypeName = "decimal(18,5)")]
        public decimal? TotalMercanciasExentas { get; set; }

        [Column("total_merc_exonerada", TypeName = "decimal(18,5)")]
        public decimal? TotalMercExonerada { get; set; }

        [Column("total_gravado", TypeName = "decimal(18,5)")]
        public decimal? TotalGravado { get; set; }

        [Column("total_exonerado", TypeName = "decimal(18,5)")]
        public decimal? TotalExonerado { get; set; }

        [Column("total_no_sujeto", TypeName = "decimal(18,5)")]
        public decimal? TotalNoSujeto { get; set; }

        [Column("total_venta", TypeName = "decimal(18,5)")]
        public decimal? TotalVenta { get; set; }

        [Column("total_venta_neta", TypeName = "decimal(18,5)")]
        public decimal? TotalVentaNeta { get; set; }

        [Column("total_impuesto_descontado", TypeName = "decimal(18,5)")]
        public decimal? TotalImpuestoDescontado { get; set; }

        [Column("total_iva_devuelto", TypeName = "decimal(18,5)")]
        public decimal? TotalIvaDevuelto { get; set; }

        [Column("total_comprobante", TypeName = "decimal(18,5)")]
        public decimal? TotalComprobante { get; set; }

        // Desglose de impuesto del resumen
        [MaxLength(2)]
        [Column("desglose_impuesto_codigo")]
        public string? DesgloseImpuestoCodigo { get; set; }

        [MaxLength(2)]
        [Column("desglose_impuesto_tarifa_iva")]
        public string? DesgloseImpuestoTarifaIva { get; set; }

        [Column("desglose_impuesto_monto", TypeName = "decimal(18,5)")]
        public decimal? DesgloseImpuestoMonto { get; set; }

        // Medio de pago del resumen
        [MaxLength(2)]
        [Column("medio_pago_tipo")]
        public string? MedioPagoTipo { get; set; }

        [Column("medio_pago_total", TypeName = "decimal(18,5)")]
        public decimal? MedioPagoTotal { get; set; }

        // ===== Respuesta de Hacienda parseada =====
        [MaxLength(1)]
        [Column("hacienda_mensaje_codigo")]
        public string? HaciendaMensajeCodigo { get; set; }    // 1=Aceptado, 2=Con obs., 3=Rechazado

        [Column("hacienda_monto_impuesto", TypeName = "decimal(18,5)")]
        public decimal? HaciendaMontoImpuesto { get; set; }

        [Column("hacienda_total_factura", TypeName = "decimal(18,5)")]
        public decimal? HaciendaTotalFactura { get; set; }

        [Column("hacienda_fecha_emision_doc")]
        public DateTime? HaciendaFechaEmisionDoc { get; set; }

        [Column("hacienda_fecha_recepcion")]
        public DateTime? HaciendaFechaRecepcion { get; set; }

        // ===== Navegación =====
        [InverseProperty(nameof(ElectronicDocumentLine.ElectronicDocument))]
        public virtual ICollection<ElectronicDocumentLine> Lines { get; set; } = new List<ElectronicDocumentLine>();

        [InverseProperty(nameof(ElectronicDocumentReference.ElectronicDocument))]
        public virtual ICollection<ElectronicDocumentReference> References { get; set; } = new List<ElectronicDocumentReference>();
    }
}
