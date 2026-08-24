// ================================================================================
// ARCHIVO: CMS.Shared/DTOs/CustomerTypeDto.cs
// PROPÓSITO: DTO del catálogo central admin.customer_type para el CRUD de
//            mantenimiento y los selectores de la UI.
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026
// ================================================================================

namespace CMS.Shared.DTOs;

/// <summary>
/// Tipo de cliente del catálogo central admin.customer_type.
/// </summary>
public class CustomerTypeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsIssuer { get; set; }
    public bool IsReceptor { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
