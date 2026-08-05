// ================================================================================
// ARCHIVO: CMS.Application/DTOs/EntityTypeDtos.cs
// PROPÓSITO: DTOs para transferencia de datos de Entity Types
// ================================================================================

namespace CMS.Application.DTOs
{
    /// <summary>
    /// DTO para listar y mostrar tipos de entidad
    /// </summary>
    public class EntityTypeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// DTO para crear un nuevo tipo de entidad
    /// </summary>
    public class EntityTypeCreateDto
    {
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
    }

    /// <summary>
    /// DTO para actualizar un tipo de entidad
    /// </summary>
    public class EntityTypeUpdateDto
    {
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }
}
