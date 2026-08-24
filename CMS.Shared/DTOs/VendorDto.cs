// ================================================================================
// ARCHIVO: CMS.Shared/DTOs/VendorDto.cs
// PROPÓSITO: DTO de entrada/salida para el mantenimiento de proveedores (vendors).
// DESCRIPCIÓN: Contrato consumido por CMS.UI/wwwroot/js/vendors.js a través de
//              /api/Vendor. El tipo de identificación se maneja como número
//              (1..6) en la UI y se persiste como código Hacienda ("01".."06").
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

namespace CMS.Shared.DTOs
{
    /// <summary>
    /// DTO para crear/editar/listar un proveedor (vendor).
    /// </summary>
    public class VendorDto
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? CommercialName { get; set; }

        /// <summary>Tipo de identificación Hacienda como número (1=Física, 2=Jurídica, ...).</summary>
        public int? IdElectronicDocumentIdentificationType { get; set; }

        public string? Identification { get; set; }
        public string? EconomicActivity { get; set; }
        public string? VendorType { get; set; }
        public string? Email { get; set; }
        public string? PhoneCode { get; set; }
        public string? Phone { get; set; }
        public string? Currency { get; set; }
        public int? CreditDays { get; set; }
        public decimal? CreditLimit { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>Actividades económicas del vendor (solo en salida al editar).</summary>
        public List<VendorEconomicActivityDto> EconomicActivities { get; set; } = new();
    }

    /// <summary>DTO de una actividad económica asociada a un vendor.</summary>
    public class VendorEconomicActivityDto
    {
        public int Id { get; set; }
        public int IdVendor { get; set; }
        public int IdElectronicDocumentEconomicActivity { get; set; }
        public string? EconomicActivityCode { get; set; }
        public string? Description { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>DTO de entrada para agregar una actividad económica a un vendor.</summary>
    public class VendorEconomicActivityInputDto
    {
        public int IdElectronicDocumentEconomicActivity { get; set; }
    }
}
