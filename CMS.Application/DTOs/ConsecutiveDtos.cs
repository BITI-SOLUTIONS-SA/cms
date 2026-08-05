// ================================================================================
// ARCHIVO: CMS.Application/DTOs/ConsecutiveDtos.cs
// PROPÓSITO: DTOs para transferencia de datos de Consecutivos
// ================================================================================

namespace CMS.Application.DTOs
{
    /// <summary>
    /// DTO para listar y mostrar consecutivos
    /// </summary>
    public class ConsecutiveDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public string Description { get; set; } = default!;
        public int IdEntityType { get; set; }
        public int IdEntityDocument { get; set; }
        public int IdMenu { get; set; }
        public string EntityTypeCode { get; set; } = default!;
        public string EntityTypeName { get; set; } = default!;
        public string EntityDocumentCode { get; set; } = default!;
        public string EntityDocumentName { get; set; } = default!;
        public string MenuName { get; set; } = default!;
        public string MenuUrl { get; set; } = default!;
        public string Mask { get; set; } = default!;
        public int Length { get; set; }
        public string InitialValue { get; set; } = default!;
        public string FinalValue { get; set; } = default!;
        public string? LastValue { get; set; }
        public int? LastUser { get; set; }
        public DateTime? LastDate { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// DTO para crear un nuevo consecutivo
    /// </summary>
    public class ConsecutiveCreateDto
    {
        public string Code { get; set; } = default!;
        public string Description { get; set; } = default!;
        public int IdEntityType { get; set; }
        public int IdEntityDocument { get; set; }
        public int IdMenu { get; set; }
        public string Mask { get; set; } = default!;
        public int Length { get; set; } = 4;
        public string InitialValue { get; set; } = default!;
        public string FinalValue { get; set; } = default!;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// DTO para actualizar un consecutivo
    /// </summary>
    public class ConsecutiveUpdateDto
    {
        public string Code { get; set; } = default!;
        public string Description { get; set; } = default!;
        public int IdEntityType { get; set; }
        public int IdEntityDocument { get; set; }
        public int IdMenu { get; set; }
        public string Mask { get; set; } = default!;
        public int Length { get; set; }
        public string InitialValue { get; set; } = default!;
        public string FinalValue { get; set; } = default!;
        public bool IsActive { get; set; }
    }
}
