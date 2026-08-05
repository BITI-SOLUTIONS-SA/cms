// ================================================================================
// ARCHIVO: CMS.API/Controllers/DocumentationController.cs
// PROPÓSITO: API para gestión de documentación del sistema (PDFs)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// CREADO: 2026-03-08
// ================================================================================

using CMS.Application.DTOs;
using CMS.Data;
using CMS.Entities.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentationController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<DocumentationController> _logger;

        public DocumentationController(AppDbContext db, ILogger<DocumentationController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Obtener lista de documentos agrupados por categoría
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<DocumentationCategoryDto>>> GetDocumentation()
        {
            try
            {
                var documents = await _db.SystemDocumentations
                    .Where(d => d.IS_ACTIVE && d.IS_PUBLIC)
                    .OrderBy(d => d.SORT_ORDER)
                    .ThenBy(d => d.DOCUMENT_TITLE)
                    .Select(d => new DocumentationListDto
                    {
                        Id = d.ID_SYSTEM_DOCUMENTATION,
                        DocumentCode = d.DOCUMENT_CODE,
                        DocumentTitle = d.DOCUMENT_TITLE,
                        DocumentDescription = d.DOCUMENT_DESCRIPTION,
                        DocumentCategory = d.DOCUMENT_CATEGORY,
                        ModuleName = d.MODULE_NAME,
                        FileName = d.FILE_NAME,
                        FileSizeBytes = d.FILE_SIZE_BYTES,
                        Version = d.VERSION,
                        HasFile = d.FILE_DATA != null && d.FILE_DATA.Length > 0,
                        SortOrder = d.SORT_ORDER,
                        IsActive = d.IS_ACTIVE
                    })
                    .ToListAsync();

                // Agrupar por categoría
                var categoryNames = new Dictionary<string, (string Name, string Icon)>
                {
                    { "GENERAL", ("Documentación General", "bi-book") },
                    { "MODULE", ("Documentación por Módulo", "bi-grid-3x3-gap") },
                    { "TUTORIAL", ("Tutoriales", "bi-play-circle") },
                    { "FAQ", ("Preguntas Frecuentes", "bi-question-circle") }
                };

                var categories = documents
                    .GroupBy(d => d.DocumentCategory)
                    .Select(g => new DocumentationCategoryDto
                    {
                        CategoryCode = g.Key,
                        CategoryName = categoryNames.ContainsKey(g.Key) ? categoryNames[g.Key].Name : g.Key,
                        Icon = categoryNames.ContainsKey(g.Key) ? categoryNames[g.Key].Icon : "bi-file-earmark-pdf",
                        DocumentCount = g.Count(),
                        Documents = g.ToList()
                    })
                    .OrderBy(c => c.CategoryCode == "GENERAL" ? 0 : 
                                  c.CategoryCode == "MODULE" ? 1 : 
                                  c.CategoryCode == "TUTORIAL" ? 2 : 3)
                    .ToList();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener documentación");
                return StatusCode(500, new { error = "Error al cargar la documentación" });
            }
        }

        /// <summary>
        /// Obtener lista simple de documentos
        /// </summary>
        [HttpGet("list")]
        public async Task<ActionResult<List<DocumentationListDto>>> GetDocumentList()
        {
            try
            {
                var documents = await _db.SystemDocumentations
                    .Where(d => d.IS_ACTIVE && d.IS_PUBLIC)
                    .OrderBy(d => d.SORT_ORDER)
                    .ThenBy(d => d.DOCUMENT_TITLE)
                    .Select(d => new DocumentationListDto
                    {
                        Id = d.ID_SYSTEM_DOCUMENTATION,
                        DocumentCode = d.DOCUMENT_CODE,
                        DocumentTitle = d.DOCUMENT_TITLE,
                        DocumentDescription = d.DOCUMENT_DESCRIPTION,
                        DocumentCategory = d.DOCUMENT_CATEGORY,
                        ModuleName = d.MODULE_NAME,
                        FileName = d.FILE_NAME,
                        FileSizeBytes = d.FILE_SIZE_BYTES,
                        Version = d.VERSION,
                        HasFile = d.FILE_DATA != null && d.FILE_DATA.Length > 0,
                        SortOrder = d.SORT_ORDER,
                        IsActive = d.IS_ACTIVE
                    })
                    .ToListAsync();

                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lista de documentos");
                return StatusCode(500, new { error = "Error al cargar los documentos" });
            }
        }

        /// <summary>
        /// Obtener detalle de un documento
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<DocumentationDetailDto>> GetDocument(int id)
        {
            try
            {
                var document = await _db.SystemDocumentations
                    .Where(d => d.ID_SYSTEM_DOCUMENTATION == id && d.IS_ACTIVE)
                    .Select(d => new DocumentationDetailDto
                    {
                        Id = d.ID_SYSTEM_DOCUMENTATION,
                        DocumentCode = d.DOCUMENT_CODE,
                        DocumentTitle = d.DOCUMENT_TITLE,
                        DocumentDescription = d.DOCUMENT_DESCRIPTION,
                        DocumentCategory = d.DOCUMENT_CATEGORY,
                        ModuleName = d.MODULE_NAME,
                        FileName = d.FILE_NAME,
                        FileSizeBytes = d.FILE_SIZE_BYTES,
                        ContentType = d.CONTENT_TYPE,
                        Version = d.VERSION,
                        SortOrder = d.SORT_ORDER,
                        IsActive = d.IS_ACTIVE,
                        IsPublic = d.IS_PUBLIC,
                        RequiredPermission = d.REQUIRED_PERMISSION,
                        CreateDate = d.CreateDate,
                        CreatedBy = d.CreatedBy
                    })
                    .FirstOrDefaultAsync();

                if (document == null)
                    return NotFound(new { error = "Documento no encontrado" });

                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener documento {Id}", id);
                return StatusCode(500, new { error = "Error al cargar el documento" });
            }
        }

        /// <summary>
        /// Descargar archivo PDF del documento
        /// </summary>
        [HttpGet("{id:int}/download")]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            try
            {
                var document = await _db.SystemDocumentations
                    .Where(d => d.ID_SYSTEM_DOCUMENTATION == id && d.IS_ACTIVE)
                    .Select(d => new
                    {
                        d.FILE_NAME,
                        d.FILE_DATA,
                        d.CONTENT_TYPE
                    })
                    .FirstOrDefaultAsync();

                if (document == null)
                    return NotFound(new { error = "Documento no encontrado" });

                if (document.FILE_DATA == null || document.FILE_DATA.Length == 0)
                    return NotFound(new { error = "El documento no tiene archivo adjunto" });

                return File(document.FILE_DATA, document.CONTENT_TYPE, document.FILE_NAME);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar documento {Id}", id);
                return StatusCode(500, new { error = "Error al descargar el documento" });
            }
        }

        /// <summary>
        /// Visualizar PDF en el navegador (inline)
        /// </summary>
        [HttpGet("{id:int}/view")]
        public async Task<IActionResult> ViewDocument(int id)
        {
            try
            {
                var document = await _db.SystemDocumentations
                    .Where(d => d.ID_SYSTEM_DOCUMENTATION == id && d.IS_ACTIVE)
                    .Select(d => new
                    {
                        d.FILE_NAME,
                        d.FILE_DATA,
                        d.CONTENT_TYPE
                    })
                    .FirstOrDefaultAsync();

                if (document == null)
                    return NotFound(new { error = "Documento no encontrado" });

                if (document.FILE_DATA == null || document.FILE_DATA.Length == 0)
                    return NotFound(new { error = "El documento no tiene archivo adjunto" });

                // Content-Disposition: inline para que se muestre en el navegador
                Response.Headers.Append("Content-Disposition", $"inline; filename=\"{document.FILE_NAME}\"");
                return File(document.FILE_DATA, document.CONTENT_TYPE);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al visualizar documento {Id}", id);
                return StatusCode(500, new { error = "Error al visualizar el documento" });
            }
        }

        /// <summary>
        /// Subir archivo PDF a un documento existente (Admin only)
        /// </summary>
        [HttpPost("{id:int}/upload")]
        public async Task<IActionResult> UploadDocument(int id, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "No se ha enviado ningún archivo" });

                if (file.ContentType != "application/pdf")
                    return BadRequest(new { error = "Solo se permiten archivos PDF" });

                // Límite de 50 MB
                if (file.Length > 50 * 1024 * 1024)
                    return BadRequest(new { error = "El archivo no puede exceder 50 MB" });

                var document = await _db.SystemDocumentations
                    .FirstOrDefaultAsync(d => d.ID_SYSTEM_DOCUMENTATION == id);

                if (document == null)
                    return NotFound(new { error = "Documento no encontrado" });

                // Leer archivo
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                
                document.FILE_DATA = memoryStream.ToArray();
                document.FILE_SIZE_BYTES = file.Length;
                document.FILE_NAME = file.FileName;
                document.CONTENT_TYPE = file.ContentType;
                document.RecordDate = DateTime.UtcNow;
                document.UpdatedBy = User.FindFirstValue("cms_username") ?? "SYSTEM";

                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Archivo PDF subido para documento {Id}: {FileName} ({Size} bytes)", 
                    id, file.FileName, file.Length);

                return Ok(new { 
                    success = true, 
                    message = "Archivo subido correctamente",
                    fileName = file.FileName,
                    fileSize = file.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir archivo para documento {Id}", id);
                return StatusCode(500, new { error = "Error al subir el archivo" });
            }
        }
    }
}
