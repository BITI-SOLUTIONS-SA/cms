// ================================================================================
// ARCHIVO: CMS.Shared/DTOs/ReceptorSearchResultDto.cs
// PROPÓSITO: DTO para resultados de búsqueda de receptores
// DESCRIPCIÓN: Combina datos de supplier
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-01-24
// ================================================================================

namespace CMS.Shared.DTOs;

/// <summary>
/// Resultado de búsqueda de receptores (Supplier).
/// </summary>
public class ReceptorSearchResultDto
{
    public int IdSupplier { get; set; }

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
    public string? SupplierType { get; set; }
    public string? EconomicActivity { get; set; }

    // Estado
    public bool IsActive { get; set; }
}
