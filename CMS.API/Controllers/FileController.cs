// ================================================================================
// ARCHIVO: CMS.API/Controllers/FileController.cs
// PROPÓSITO: API REST para gestión de archivos
// AUTOR: EAMR - BITI Solutions S.A
// FECHA: Marzo 2026
// ================================================================================

using CMS.Data.Services;
using CMS.Entities.Operational;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly ILogger<FileController> _logger;

        public FileController(
            IFileService fileService,
            ILogger<FileController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        #region Helpers

        private int GetCurrentCompanyId()
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value 
                              ?? User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(companyIdClaim, out var companyId))
                return companyId;
            throw new UnauthorizedAccessException("companyId no encontrado en el token JWT");
        }

        private string? GetCurrentUser()
        {
            return User.FindFirstValue("cms_username") ??
                   User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst("preferred_username")?.Value ??
                   User.FindFirst("name")?.Value;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value 
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                           ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
                return userId;
            return null;
        }

        #endregion

        #region Carpetas

        /// <summary>
        /// Lista carpetas (opcionalmente por carpeta padre).
        /// GET: api/file/folders?parentId=1
        /// </summary>
        [HttpGet("folders")]
        public async Task<ActionResult<List<FolderDto>>> GetFolders([FromQuery] int? parentId = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var folders = await _fileService.GetFoldersAsync(companyId, parentId);

                return Ok(folders.Select(f => new FolderDto
                {
                    Id = f.IdFileFolder,
                    Code = f.Code,
                    Name = f.Name,
                    Description = f.Description,
                    ParentId = f.ParentId,
                    Path = f.Path,
                    Level = f.Level,
                    CategoryCode = f.CategoryCode,
                    Icon = f.Icon,
                    Color = f.Color,
                    IsSystem = f.IsSystem,
                    IsPrivate = f.IsPrivate,
                    SortOrder = f.SortOrder,
                    CreatedAt = f.Createdate,
                    CreatedBy = f.CreatedBy
                }).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo carpetas");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene una carpeta por ID.
        /// GET: api/file/folders/1
        /// </summary>
        [HttpGet("folders/{id:int}")]
        public async Task<ActionResult<FolderDto>> GetFolder(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var folder = await _fileService.GetFolderByIdAsync(companyId, id);

                if (folder == null)
                    return NotFound(new { message = "Carpeta no encontrada" });

                return Ok(new FolderDto
                {
                    Id = folder.IdFileFolder,
                    Code = folder.Code,
                    Name = folder.Name,
                    Description = folder.Description,
                    ParentId = folder.ParentId,
                    Path = folder.Path,
                    Level = folder.Level,
                    CategoryCode = folder.CategoryCode,
                    Icon = folder.Icon,
                    Color = folder.Color,
                    IsSystem = folder.IsSystem,
                    IsPrivate = folder.IsPrivate,
                    SortOrder = folder.SortOrder,
                    CreatedAt = folder.Createdate,
                    CreatedBy = folder.CreatedBy
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo carpeta {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Crea una nueva carpeta.
        /// POST: api/file/folders
        /// </summary>
        [HttpPost("folders")]
        public async Task<ActionResult<FolderDto>> CreateFolder([FromBody] CreateFolderRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var folder = new FileFolder
                {
                    Code = request.Code ?? string.Empty,
                    Name = request.Name,
                    Description = request.Description,
                    ParentId = request.ParentId,
                    CategoryCode = request.CategoryCode,
                    Icon = request.Icon ?? "bi-folder",
                    Color = request.Color ?? "#fbbf24",
                    IsPrivate = request.IsPrivate
                };

                var created = await _fileService.CreateFolderAsync(companyId, folder, user, userId);

                return CreatedAtAction(nameof(GetFolder), new { id = created.IdFileFolder }, new FolderDto
                {
                    Id = created.IdFileFolder,
                    Code = created.Code,
                    Name = created.Name,
                    Description = created.Description,
                    ParentId = created.ParentId,
                    Path = created.Path,
                    Level = created.Level,
                    CategoryCode = created.CategoryCode,
                    Icon = created.Icon,
                    Color = created.Color,
                    IsSystem = created.IsSystem,
                    IsPrivate = created.IsPrivate,
                    SortOrder = created.SortOrder,
                    CreatedAt = created.Createdate,
                    CreatedBy = created.CreatedBy
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando carpeta");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Elimina una carpeta.
        /// DELETE: api/file/folders/1
        /// </summary>
        [HttpDelete("folders/{id:int}")]
        public async Task<IActionResult> DeleteFolder(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var result = await _fileService.DeleteFolderAsync(companyId, id, user, userId);

                if (!result)
                    return BadRequest(new { message = "No se puede eliminar la carpeta (sistema o no encontrada)" });

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando carpeta {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Mueve una carpeta a otro padre (drag-and-drop).
        /// PUT: api/file/folders/1/move
        /// </summary>
        [HttpPut("folders/{id:int}/move")]
        public async Task<IActionResult> MoveFolder(int id, [FromBody] MoveFolderRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                // Validar que no se mueva a sí misma
                if (request.NewParentId == id)
                {
                    return BadRequest(new { message = "No se puede mover una carpeta dentro de sí misma" });
                }

                // Obtener la carpeta actual
                var folder = await _fileService.GetFolderByIdAsync(companyId, id);
                if (folder == null)
                    return NotFound(new { message = "Carpeta no encontrada" });

                if (folder.IsSystem)
                    return BadRequest(new { message = "No se puede mover una carpeta del sistema" });

                // Validar que no se mueva a un descendiente (evitar ciclos)
                if (request.NewParentId.HasValue)
                {
                    var isDescendant = await IsDescendantOfAsync(companyId, request.NewParentId.Value, id);
                    if (isDescendant)
                    {
                        return BadRequest(new { message = "No se puede mover una carpeta dentro de uno de sus descendientes" });
                    }
                }

                // Actualizar el parentId
                folder.ParentId = request.NewParentId;
                var updated = await _fileService.UpdateFolderAsync(companyId, folder, user, userId);

                if (updated == null)
                    return BadRequest(new { message = "Error al mover la carpeta" });

                _logger.LogInformation("Carpeta {Id} movida a parentId {NewParentId}", id, request.NewParentId);

                return Ok(new { 
                    success = true, 
                    message = "Carpeta movida correctamente",
                    folder = new FolderDto
                    {
                        Id = updated.IdFileFolder,
                        Code = updated.Code,
                        Name = updated.Name,
                        Description = updated.Description,
                        ParentId = updated.ParentId,
                        Path = updated.Path,
                        Level = updated.Level,
                        CategoryCode = updated.CategoryCode,
                        Icon = updated.Icon,
                        Color = updated.Color,
                        IsSystem = updated.IsSystem,
                        IsPrivate = updated.IsPrivate,
                        SortOrder = updated.SortOrder,
                        CreatedAt = updated.Createdate,
                        CreatedBy = updated.CreatedBy
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moviendo carpeta {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Verifica si targetId es descendiente de potentialAncestorId (para evitar ciclos)
        /// </summary>
        private async Task<bool> IsDescendantOfAsync(int companyId, int targetId, int potentialAncestorId)
        {
            var current = await _fileService.GetFolderByIdAsync(companyId, targetId);
            while (current != null && current.ParentId.HasValue)
            {
                if (current.ParentId.Value == potentialAncestorId)
                    return true;
                current = await _fileService.GetFolderByIdAsync(companyId, current.ParentId.Value);
            }
            return false;
        }

        #endregion

        #region Archivos

        /// <summary>
        /// Lista archivos con filtros y paginación.
        /// GET: api/file?search=xxx&folderId=1&page=1&pageSize=20
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<FileListResponse>> GetFiles(
            [FromQuery] string? search = null,
            [FromQuery] int? folderId = null,
            [FromQuery] string? categoryCode = null,
            [FromQuery] string? extension = null,
            [FromQuery] bool? isActive = true,
            [FromQuery] bool includeDeleted = false,
            [FromQuery] string? orderBy = "created_at",
            [FromQuery] bool descending = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var (files, totalCount) = await _fileService.GetFilesAsync(
                    companyId, search, folderId, categoryCode, extension,
                    isActive, includeDeleted, orderBy, descending, page, pageSize);

                return Ok(new FileListResponse
                {
                    Files = files.Select(MapToDto).ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo archivos");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene un archivo por ID.
        /// GET: api/file/1
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<FileDto>> GetFile(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var file = await _fileService.GetFileByIdAsync(companyId, id);

                if (file == null)
                    return NotFound(new { message = "Archivo no encontrado" });

                // Registrar visualización
                await _fileService.RecordViewAsync(companyId, id);

                return Ok(MapToDto(file));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene un archivo por código.
        /// GET: api/file/code/FILE-000001
        /// </summary>
        [HttpGet("code/{code}")]
        public async Task<ActionResult<FileDto>> GetFileByCode(string code)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var file = await _fileService.GetFileByCodeAsync(companyId, code);

                if (file == null)
                    return NotFound(new { message = "Archivo no encontrado" });

                await _fileService.RecordViewAsync(companyId, file.IdFile);

                return Ok(MapToDto(file));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo archivo por código {Code}", code);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Descarga el contenido de un archivo.
        /// GET: api/file/1/download
        /// </summary>
        [HttpGet("{id:int}/download")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var file = await _fileService.GetFileByIdAsync(companyId, id, includeContent: true);

                if (file == null)
                    return NotFound(new { message = "Archivo no encontrado" });

                if (!file.AllowDownload)
                    return Forbid();

                await _fileService.RecordDownloadAsync(companyId, id);

                return File(file.FileContent, file.MimeType ?? "application/octet-stream", file.OriginalFilename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error descargando archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Sube un nuevo archivo.
        /// POST: api/file/upload
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)] // 100MB
        public async Task<ActionResult<FileDto>> UploadFile([FromForm] UploadFileRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                    return BadRequest(new { message = "No se proporcionó archivo" });

                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                using var memoryStream = new MemoryStream();
                await request.File.CopyToAsync(memoryStream);
                var content = memoryStream.ToArray();

                var file = await _fileService.CreateFileAsync(
                    companyId,
                    request.File.FileName,
                    content,
                    request.Title,
                    request.Description,
                    request.FolderId,
                    request.CategoryCode,
                    request.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray(),
                    user,
                    userId);

                return CreatedAtAction(nameof(GetFile), new { id = file.IdFile }, MapToDto(file));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subiendo archivo");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Sube una nueva versión de un archivo existente.
        /// POST: api/file/1/upload-version
        /// </summary>
        [HttpPost("{id:int}/upload-version")]
        [RequestSizeLimit(100_000_000)]
        public async Task<ActionResult<FileDto>> UploadNewVersion(int id, [FromForm] UploadVersionRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                    return BadRequest(new { message = "No se proporcionó archivo" });

                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                using var memoryStream = new MemoryStream();
                await request.File.CopyToAsync(memoryStream);
                var content = memoryStream.ToArray();

                var file = await _fileService.UpdateFileContentAsync(
                    companyId, id, content, request.ChangeSummary, user, userId);

                if (file == null)
                    return NotFound(new { message = "Archivo no encontrado" });

                return Ok(MapToDto(file));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subiendo nueva versión");
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Actualiza los metadatos de un archivo.
        /// PUT: api/file/1
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<FileDto>> UpdateFile(int id, [FromBody] UpdateFileRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var file = await _fileService.UpdateFileMetadataAsync(
                    companyId, id,
                    request.Name,
                    request.Title,
                    request.Description,
                    request.FolderId,
                    request.CategoryCode,
                    request.Tags,
                    request.DocumentDate,
                    request.ExpiryDate,
                    request.IsFavorite,
                    request.IsPinned,
                    request.IsPrivate,
                    request.AllowDownload,
                    user, userId);

                if (file == null)
                    return NotFound(new { message = "Archivo no encontrado" });

                return Ok(MapToDto(file));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Mueve un archivo a otra carpeta.
        /// PUT: api/file/1/move
        /// </summary>
        [HttpPut("{id:int}/move")]
        public async Task<ActionResult<FileDto>> MoveFile(int id, [FromBody] MoveFileRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                // Verificar que el archivo existe
                var existingFile = await _fileService.GetFileByIdAsync(companyId, id);
                if (existingFile == null)
                    return NotFound(new { message = "Archivo no encontrado" });

                // Verificar que el archivo no está bloqueado por otro usuario
                if (existingFile.IsLocked && existingFile.LockedByUserId != userId)
                    return Conflict(new { message = "El archivo está bloqueado por otro usuario" });

                // Validar que la carpeta destino existe (si se especifica)
                if (request.FolderId.HasValue && request.FolderId.Value > 0)
                {
                    var targetFolder = await _fileService.GetFolderByIdAsync(companyId, request.FolderId.Value);
                    if (targetFolder == null)
                        return BadRequest(new { message = "La carpeta destino no existe" });
                }

                // Mover el archivo actualizando su FolderId
                var file = await _fileService.UpdateFileMetadataAsync(
                    companyId, id,
                    folderId: request.FolderId,
                    updatedBy: user,
                    updatedById: userId);

                if (file == null)
                    return NotFound(new { message = "Error al mover el archivo" });

                _logger.LogInformation("📦 Archivo {FileId} movido a carpeta {FolderId} por {User}", 
                    id, request.FolderId, user);

                return Ok(MapToDto(file));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moviendo archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Elimina un archivo (a papelera).
        /// DELETE: api/file/1
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteFile(int id, [FromQuery] string? reason = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var result = await _fileService.DeleteFileAsync(companyId, id, reason, user, userId);

                if (!result)
                    return NotFound(new { message = "Archivo no encontrado" });

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Restaura un archivo de la papelera.
        /// POST: api/file/1/restore
        /// </summary>
        [HttpPost("{id:int}/restore")]
        public async Task<IActionResult> RestoreFile(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var result = await _fileService.RestoreFileAsync(companyId, id, user, userId);

                if (!result)
                    return NotFound(new { message = "Archivo no encontrado o no está en papelera" });

                return Ok(new { message = "Archivo restaurado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restaurando archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        #endregion

        #region Bloqueos

        /// <summary>
        /// Bloquea un archivo para edición exclusiva.
        /// POST: api/file/1/lock
        /// </summary>
        [HttpPost("{id:int}/lock")]
        public async Task<IActionResult> LockFile(int id, [FromBody] LockFileRequest? request = null)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser() ?? "Usuario";
                var userId = GetCurrentUserId() ?? 0;

                var result = await _fileService.LockFileAsync(
                    companyId, id, userId, user,
                    request?.Reason,
                    request?.DurationMinutes ?? 60);

                if (!result)
                    return Conflict(new { message = "El archivo ya está bloqueado por otro usuario" });

                return Ok(new { message = "Archivo bloqueado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bloqueando archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Desbloquea un archivo.
        /// POST: api/file/1/unlock
        /// </summary>
        [HttpPost("{id:int}/unlock")]
        public async Task<IActionResult> UnlockFile(int id, [FromQuery] bool force = false)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var userId = GetCurrentUserId() ?? 0;

                var result = await _fileService.UnlockFileAsync(companyId, id, userId, force);

                if (!result)
                    return BadRequest(new { message = "No se puede desbloquear el archivo" });

                return Ok(new { message = "Archivo desbloqueado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error desbloqueando archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        #endregion

        #region Versiones

        /// <summary>
        /// Obtiene el historial de versiones de un archivo.
        /// GET: api/file/1/versions
        /// </summary>
        [HttpGet("{id:int}/versions")]
        public async Task<ActionResult<List<FileVersionDto>>> GetVersions(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var versions = await _fileService.GetFileVersionsAsync(companyId, id);

                return Ok(versions.Select(v => new FileVersionDto
                {
                    Id = v.IdFileVersion,
                    VersionNumber = v.VersionNumber,
                    FileSizeBytes = v.FileSizeBytes,
                    FileSizeFormatted = v.FileSizeFormatted,
                    Filename = v.Filename,
                    ChangeType = v.ChangeType,
                    ChangeSummary = v.ChangeSummary,
                    CreatedAt = v.CreatedAt,
                    CreatedBy = v.CreatedBy
                }).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo versiones del archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Descarga una versión específica.
        /// GET: api/file/1/versions/2/download
        /// </summary>
        [HttpGet("{id:int}/versions/{version:int}/download")]
        public async Task<IActionResult> DownloadVersion(int id, int version)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var file = await _fileService.GetFileByIdAsync(companyId, id);
                if (file == null)
                    return NotFound(new { message = "Archivo no encontrado" });

                var content = await _fileService.GetVersionContentAsync(companyId, id, version);
                if (content == null)
                    return NotFound(new { message = "Versión no encontrada" });

                return File(content, file.MimeType ?? "application/octet-stream", 
                    $"v{version}_{file.OriginalFilename}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error descargando versión {Version} del archivo {Id}", version, id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Restaura una versión anterior.
        /// POST: api/file/1/versions/2/restore
        /// </summary>
        [HttpPost("{id:int}/versions/{version:int}/restore")]
        public async Task<ActionResult<FileDto>> RestoreVersion(int id, int version)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var file = await _fileService.RestoreVersionAsync(companyId, id, version, user, userId);

                if (file == null)
                    return NotFound(new { message = "Archivo o versión no encontrada" });

                return Ok(MapToDto(file));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restaurando versión {Version} del archivo {Id}", version, id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        #endregion

        #region Comentarios

        /// <summary>
        /// Obtiene los comentarios de un archivo.
        /// GET: api/file/1/comments
        /// </summary>
        [HttpGet("{id:int}/comments")]
        public async Task<ActionResult<List<FileCommentDto>>> GetComments(int id)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var comments = await _fileService.GetFileCommentsAsync(companyId, id);

                return Ok(comments.Where(c => c.ParentId == null).Select(c => new FileCommentDto
                {
                    Id = c.IdFileComment,
                    CommentText = c.CommentText,
                    IsResolved = c.IsResolved,
                    IsEdited = c.IsEdited,
                    CreatedAt = c.CreatedAt,
                    CreatedBy = c.CreatedBy,
                    Replies = c.Replies.Select(r => new FileCommentDto
                    {
                        Id = r.IdFileComment,
                        CommentText = r.CommentText,
                        IsResolved = r.IsResolved,
                        IsEdited = r.IsEdited,
                        CreatedAt = r.CreatedAt,
                        CreatedBy = r.CreatedBy
                    }).ToList()
                }).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo comentarios del archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Agrega un comentario a un archivo.
        /// POST: api/file/1/comments
        /// </summary>
        [HttpPost("{id:int}/comments")]
        public async Task<ActionResult<FileCommentDto>> AddComment(int id, [FromBody] AddCommentRequest request)
        {
            try
            {
                var companyId = GetCurrentCompanyId();
                var user = GetCurrentUser();
                var userId = GetCurrentUserId();

                var comment = await _fileService.AddCommentAsync(
                    companyId, id, request.Text, request.ParentId,
                    request.MentionedUserIds, user, userId);

                return CreatedAtAction(nameof(GetComments), new { id }, new FileCommentDto
                {
                    Id = comment.IdFileComment,
                    CommentText = comment.CommentText,
                    CreatedAt = comment.CreatedAt,
                    CreatedBy = comment.CreatedBy
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error agregando comentario al archivo {Id}", id);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        #endregion

        #region Mappers

        private static FileDto MapToDto(FileDocument file)
        {
            return new FileDto
            {
                Id = file.IdFile,
                Code = file.Code,
                Uuid = file.Uuid,
                Name = file.Name,
                Title = file.Title,
                Description = file.Description,
                FolderId = file.IdFileFolder,
                Path = file.Path,
                CategoryCode = file.CategoryCode,
                Tags = file.Tags,
                FileExtension = file.FileExtension,
                MimeType = file.MimeType,
                FileSizeBytes = file.FileSizeBytes,
                FileSizeFormatted = file.FileSizeFormatted,
                OriginalFilename = file.OriginalFilename,
                FileIcon = file.FileIcon,
                CurrentVersion = file.CurrentVersion,
                VersionCount = file.VersionCount,
                IsLocked = file.IsLocked,
                LockedByUserId = file.LockedByUserId,
                LockedByUserName = file.LockedByUserName,
                LockedAt = file.LockedAt,
                LockExpiresAt = file.LockExpiresAt,
                IsFavorite = file.IsFavorite,
                IsPinned = file.IsPinned,
                IsPrivate = file.IsPrivate,
                AllowDownload = file.AllowDownload,
                DocumentDate = file.DocumentDate,
                ExpiryDate = file.ExpiryDate,
                IsExpired = file.IsExpired,
                RelatedEntityType = file.RelatedEntityType,
                RelatedEntityId = file.RelatedEntityId,
                RelatedEntityCode = file.RelatedEntityCode,
                IsActive = file.IsActive,
                DeletedAt = file.DeletedAt,
                ViewCount = file.ViewCount,
                DownloadCount = file.DownloadCount,
                LastViewedAt = file.LastViewedAt,
                LastDownloadedAt = file.LastDownloadedAt,
                CreatedAt = file.Createdate,
                CreatedBy = file.CreatedBy,
                UpdatedAt = file.RecordDate,
                UpdatedBy = file.UpdatedBy
            };
        }

        #endregion
    }

    #region DTOs

    public class FolderDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentId { get; set; }
        public string? Path { get; set; }
        public int Level { get; set; }
        public string? CategoryCode { get; set; }
        public string Icon { get; set; } = "bi-folder";
        public string Color { get; set; } = "#fbbf24";
        public bool IsSystem { get; set; }
        public bool IsPrivate { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class CreateFolderRequest
    {
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentId { get; set; }
        public string? CategoryCode { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool IsPrivate { get; set; }
    }

    /// <summary>
    /// Request para mover una carpeta (drag-and-drop)
    /// </summary>
    public class MoveFolderRequest
    {
        /// <summary>
        /// Nuevo ID de carpeta padre (null = mover a raíz)
        /// </summary>
        public int? NewParentId { get; set; }
    }

    /// <summary>
    /// Request para mover un archivo a otra carpeta
    /// </summary>
    public class MoveFileRequest
    {
        /// <summary>
        /// ID de la carpeta destino (null = mover a raíz)
        /// </summary>
        public int? FolderId { get; set; }
    }

    public class FileDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public Guid Uuid { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? FolderId { get; set; }
        public string? Path { get; set; }
        public string? CategoryCode { get; set; }
        public string[]? Tags { get; set; }
        public string FileExtension { get; set; } = string.Empty;
        public string? MimeType { get; set; }
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public string OriginalFilename { get; set; } = string.Empty;
        public string FileIcon { get; set; } = "bi-file";
        public int CurrentVersion { get; set; }
        public int VersionCount { get; set; }
        public bool IsLocked { get; set; }
        public int? LockedByUserId { get; set; }
        public string? LockedByUserName { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? LockExpiresAt { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsPinned { get; set; }
        public bool IsPrivate { get; set; }
        public bool AllowDownload { get; set; }
        public DateOnly? DocumentDate { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public bool IsExpired { get; set; }
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityCode { get; set; }
        public bool IsActive { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int ViewCount { get; set; }
        public int DownloadCount { get; set; }
        public DateTime? LastViewedAt { get; set; }
        public DateTime? LastDownloadedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class FileListResponse
    {
        public List<FileDto> Files { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class UploadFileRequest
    {
        public IFormFile? File { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? FolderId { get; set; }
        public string? CategoryCode { get; set; }
        public string? Tags { get; set; } // Comma-separated
    }

    public class UploadVersionRequest
    {
        public IFormFile? File { get; set; }
        public string? ChangeSummary { get; set; }
    }

    public class UpdateFileRequest
    {
        public string? Name { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? FolderId { get; set; }
        public string? CategoryCode { get; set; }
        public string[]? Tags { get; set; }
        public DateOnly? DocumentDate { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public bool? IsFavorite { get; set; }
        public bool? IsPinned { get; set; }
        public bool? IsPrivate { get; set; }
        public bool? AllowDownload { get; set; }
    }

    public class LockFileRequest
    {
        public string? Reason { get; set; }
        public int DurationMinutes { get; set; } = 60;
    }

    public class FileVersionDto
    {
        public int Id { get; set; }
        public int VersionNumber { get; set; }
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public string? Filename { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public string? ChangeSummary { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class FileCommentDto
    {
        public int Id { get; set; }
        public string CommentText { get; set; } = string.Empty;
        public bool IsResolved { get; set; }
        public bool IsEdited { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public List<FileCommentDto> Replies { get; set; } = new();
    }

    public class AddCommentRequest
    {
        public string Text { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public int[]? MentionedUserIds { get; set; }
    }

    #endregion
}
