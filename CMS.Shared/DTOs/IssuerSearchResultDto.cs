// ================================================================================
// ARCHIVO: CMS.Shared/DTOs/IssuerSearchResultDto.cs
// PROPÓSITO: DTO para resultados de búsqueda de emisores
// DESCRIPCIÓN: Combina datos de customer y customer_billing_credential
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-01-24
// ================================================================================

namespace CMS.Shared.DTOs;

/// <summary>
/// Resultado de búsqueda de emisores (Customer + CustomerBillingCredential).
/// </summary>
public class IssuerSearchResultDto
{
    public int IdCredential { get; set; }
    public int? IdCustomer { get; set; }

    // Datos de identificación (de credential)
    public string Name { get; set; } = string.Empty;
    public string Identification { get; set; } = string.Empty;
    public string IdentificationType { get; set; } = string.Empty;
    public string? CommercialName { get; set; }
    public string? EconomicActivity { get; set; }

    // Datos de contacto (de credential)
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhoneCode { get; set; }

    // Datos operacionales (de customer, si existe)
    public string? CustomerCode { get; set; }
    public string? CustomerType { get; set; }

    // Ambiente
    public string Environment { get; set; } = string.Empty;

    // Flags
    public bool IsCompanyOwner { get; set; }
    public bool IsActive { get; set; }
}
