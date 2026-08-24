// ================================================================================
// ARCHIVO: CMS.Shared/DTOs/ReceptorSearchResultDto.cs
// PROPÓSITO: DTO para resultados de búsqueda de receptores
// DESCRIPCIÓN: Combina datos del maestro de clientes (sinai.customer) para el
//              selector de receptor en la emisión de documentos electrónicos.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-01-24
// ================================================================================

namespace CMS.Shared.DTOs;

/// <summary>
/// Resultado de búsqueda de receptores (Customer).
/// </summary>
public class ReceptorSearchResultDto
{
    public int IdCustomer { get; set; }

    // Datos de identificación
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CommercialName { get; set; }
    public string? IdentificationType { get; set; }
    public string? Identification { get; set; }
    public string? ForeignIdentification { get; set; }

    // Datos de contacto
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhoneCode { get; set; }

    // Datos comerciales
    public int? IdCustomerType { get; set; }
    public string? CustomerType { get; set; }
    public string? EconomicActivity { get; set; }

    // Exoneración
    public bool IsExonerated { get; set; }

    // Estado
    public bool IsActive { get; set; }
}
