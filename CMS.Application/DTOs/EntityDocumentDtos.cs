// ================================================================================
// ARCHIVO: CMS.Application/DTOs/EntityDocumentDtos.cs
// PROPÓSITO: DTOs para transferencia de datos de Entity Documents
// ================================================================================

namespace CMS.Application.DTOs
{
    /// <summary>
    /// DTO para listar y mostrar tipos de documento
    /// </summary>
    public class EntityDocumentDto
    {
        public int Id { get; set; }
        public int IdEntityType { get; set; }
        public string EntityTypeCode { get; set; } = default!;
        public string EntityTypeName { get; set; } = default!;
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// DTO para crear un nuevo tipo de documento
    /// </summary>
    public class EntityDocumentCreateDto
    {
        public int IdEntityType { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
    }

    /// <summary>
    /// DTO para actualizar un tipo de documento
    /// </summary>
    public class EntityDocumentUpdateDto
    {
        public int IdEntityType { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }
}
