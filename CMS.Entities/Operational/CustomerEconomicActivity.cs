// ================================================================================
// ARCHIVO: CMS.Entities/Operational/CustomerEconomicActivity.cs
// PROPÓSITO: Entidad operacional de las actividades económicas por cliente.
// DESCRIPCIÓN: Se almacena en la BD de la compañía (schema de la compañía, ej. sinai).
//              Tabla: {schema}.customer_economic_activity
//              De aquí el sistema toma la actividad económica al emitir una factura.
//              El código referencia lógicamente el catálogo central
//              cms.admin.electronic_document_economic_activity (relación cross-DB).
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMS.Entities.Operational
{
    [Table("customer_economic_activity")]
    public class CustomerEconomicActivity
    {
        [Key]
        [Column("id_customer_economic_activity")]
        public int Id { get; set; }

        [Column("id_customer")]
        public int IdCustomer { get; set; }

        /// <summary>
        /// Id del catálogo central admin.electronic_document_economic_activity.
        /// Relación LÓGICA cross-DB (esta tabla vive en la BD de la compañía y el
        /// catálogo en la BD central cms): NO se declara FK real.
        /// </summary>
        [Column("id_electronic_document_economic_activity")]
        public int IdElectronicDocumentEconomicActivity { get; set; }

        /// <summary>
        /// CodigoActividad Hacienda (0000.0). NO se persiste: se resuelve desde el
        /// catálogo central por IdElectronicDocumentEconomicActivity. Propiedad de
        /// transporte para la UI/API.
        /// </summary>
        [NotMapped]
        public string? EconomicActivityCode { get; set; }

        /// <summary>
        /// Descripción de la actividad. NO se persiste: se resuelve desde el catálogo
        /// central cms.admin.electronic_document_economic_activity por
        /// IdElectronicDocumentEconomicActivity. Propiedad de transporte.
        /// </summary>
        [NotMapped]
        public string? Description { get; set; }

        /// <summary>Actividad económica predeterminada del cliente (una sola por cliente).</summary>
        [Column("is_default")]
        public bool IsDefault { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(2000)]
        [Column("notes")]
        public string? Notes { get; set; }

        // ===== Auditoría =====
        // Generadas por la BD (DEFAULT now() / gen_random_uuid() y trigger de auditoría).
        // Se marcan como Computed para que EF NO las envíe en INSERT/UPDATE y así
        // respete los DEFAULT del servidor (evita rowpointer duplicado = Guid.Empty).
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("record_date")]
        public DateTime RecordDate { get; set; }

        [MaxLength(30)]
        [Column("created_by")]
        public string? CreatedBy { get; set; }

        [MaxLength(30)]
        [Column("updated_by")]
        public string? UpdatedBy { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("rowpointer")]
        public Guid RowPointer { get; set; }

        // ===== Navegación =====
        [ForeignKey(nameof(IdCustomer))]
        public Customer? Customer { get; set; }
    }
}
