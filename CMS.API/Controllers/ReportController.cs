// ================================================================================
// ARCHIVO: CMS.API/Controllers/ReportController.cs
// PROPÓSITO: API para gestión y ejecución de reportes dinámicos
// AUTOR: EAMR, BITI SOLUTIONS S.A
// ================================================================================

using CMS.Application.DTOs;
using CMS.Data;
using CMS.Data.Services;
using CMS.Entities.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Npgsql;

namespace CMS.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ReportController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ICompanyDbContextFactory _companyDbContextFactory;

        public ReportController(
            AppDbContext db,
            ILogger<ReportController> logger,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ICompanyDbContextFactory companyDbContextFactory)
        {
            _db = db;
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
            _companyDbContextFactory = companyDbContextFactory;
        }

        #region Categorías

        // GET: api/report/diagnose - ENDPOINT DE DIAGNÓSTICO para verificar configuración
        [HttpGet("diagnose")]
        public async Task<IActionResult> DiagnoseReportSetup()
        {
            var result = new Dictionary<string, object>();

            try
            {
                var companyId = GetCurrentCompanyId();
                result["companyId"] = companyId ?? 0;

                if (!companyId.HasValue)
                {
                    result["error"] = "No se encontró companyId en el JWT";
                    return Ok(result);
                }

                // ⭐ USAR EL FACTORY CENTRALIZADO (misma lógica que ItemService)
                try
                {
                    var (connectionString, schema) = await _companyDbContextFactory.GetConnectionStringAsync(companyId.Value);
                    result["companySchema"] = schema;
                    result["connectionStringObtained"] = true;
                    result["connectionStringPreview"] = connectionString.Length > 60 
                        ? connectionString.Substring(0, 60) + "..." 
                        : connectionString;

                    // Intentar conectar y verificar tablas
                    await using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync();

                    // Verificar BD conectada
                    await using var checkCmd = new NpgsqlCommand("SELECT current_database()", connection);
                    result["connectedDatabase"] = await checkCmd.ExecuteScalarAsync();

                    // Verificar si existe la tabla item en el schema
                    await using var checkTableCmd = new NpgsqlCommand(
                        @"SELECT EXISTS (
                            SELECT 1 FROM information_schema.tables 
                            WHERE table_schema = @schema AND table_name = 'item'
                        )", connection);
                    checkTableCmd.Parameters.AddWithValue("schema", schema);
                    result["tableItemExists"] = await checkTableCmd.ExecuteScalarAsync();

                    // Si la tabla existe, contar registros
                    if ((bool)result["tableItemExists"])
                    {
                        await using var countCmd = new NpgsqlCommand(
                            $"SELECT COUNT(*) FROM {schema}.item", connection);
                        result["itemCount"] = await countCmd.ExecuteScalarAsync();

                        // Mostrar primeros 3 registros
                        await using var sampleCmd = new NpgsqlCommand(
                            $"SELECT item_code, item_name FROM {schema}.item LIMIT 3", connection);
                        await using var reader = await sampleCmd.ExecuteReaderAsync();
                        var samples = new List<string>();
                        while (await reader.ReadAsync())
                        {
                            samples.Add($"{reader.GetString(0)}: {reader.GetString(1)}");
                        }
                        result["sampleItems"] = samples;
                        result["status"] = "✅ TODO OK - La tabla item existe y tiene datos";
                    }
                    else
                    {
                        // Mostrar qué tablas SÍ existen en el schema
                        await using var listTablesCmd = new NpgsqlCommand(
                            @"SELECT table_name FROM information_schema.tables 
                              WHERE table_schema = @schema 
                              ORDER BY table_name LIMIT 20", connection);
                        listTablesCmd.Parameters.AddWithValue("schema", schema);

                        var tables = new List<string>();
                        await using var reader = await listTablesCmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            tables.Add(reader.GetString(0));
                        }
                        result["existingTables"] = tables;
                        result["status"] = $"❌ ERROR - La tabla '{schema}.item' NO EXISTE. Tablas encontradas: {tables.Count}";
                    }
                }
                catch (Exception factoryEx)
                {
                    result["factoryError"] = factoryEx.Message;
                    result["status"] = "❌ ERROR obteniendo connection string del factory";
                }
            }
            catch (Exception ex)
            {
                result["exception"] = ex.Message;
            }

            return Ok(result);
        }

        // GET: api/report/categories
        [HttpGet("categories")]
        public async Task<ActionResult<List<ReportCategoryDto>>> GetCategories()
        {
            try
            {
                var categories = await _db.ReportCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .Select(c => new ReportCategoryDto
                    {
                        Id = c.Id,
                        CategoryCode = c.CategoryCode,
                        CategoryName = c.CategoryName,
                        Description = c.Description,
                        Icon = c.Icon,
                        SortOrder = c.SortOrder,
                        IsActive = c.IsActive,
                        ReportCount = _db.ReportDefinitions.Count(r => r.CategoryId == c.Id && r.IsActive)
                    })
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo categorías de reportes");
                return StatusCode(500, new { message = "Error obteniendo categorías" });
            }
        }

        #endregion

        #region Listado de Reportes

        // GET: api/report - Lista de reportes (filtrados por permisos del usuario)
        [HttpGet]
        public async Task<ActionResult<List<ReportListDto>>> GetReports(
            [FromQuery] int? categoryId = null,
            [FromQuery] string? search = null,
            [FromQuery] bool? onlyFavorites = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var companyId = GetCurrentCompanyId();

                _logger.LogInformation("📊 Obteniendo reportes para userId={UserId}, companyId={CompanyId}", 
                    userId, companyId);

                // Obtener los IDs de reportes a los que el usuario tiene acceso en esta compañía
                var allowedReportIds = new HashSet<int>();

                if (userId.HasValue && companyId.HasValue)
                {
                    allowedReportIds = (await _db.UserCompanyReports
                        .Where(ucr => ucr.UserId == userId.Value 
                            && ucr.CompanyId == companyId.Value 
                            && ucr.IsAllowed 
                            && ucr.IsActive)
                        .Select(ucr => ucr.ReportDefinitionId)
                        .ToListAsync())
                        .ToHashSet();

                    _logger.LogInformation("📊 Usuario tiene acceso a {Count} reportes: [{Ids}]", 
                        allowedReportIds.Count, string.Join(", ", allowedReportIds));
                }

                var query = _db.ReportDefinitions
                    .Include(r => r.Category)
                    .Where(r => r.IsActive);

                // FILTRO DE PERMISOS: Solo mostrar reportes a los que el usuario tiene acceso
                if (userId.HasValue && companyId.HasValue && allowedReportIds.Count > 0)
                {
                    query = query.Where(r => allowedReportIds.Contains(r.Id));
                }
                else if (userId.HasValue && companyId.HasValue)
                {
                    // Si tiene userId y companyId pero no hay permisos configurados,
                    // no mostrar ningún reporte (comportamiento restrictivo por defecto)
                    _logger.LogWarning("⚠️ Usuario {UserId} no tiene reportes asignados en compañía {CompanyId}", 
                        userId, companyId);
                    return Ok(new List<ReportListDto>());
                }

                if (categoryId.HasValue)
                    query = query.Where(r => r.CategoryId == categoryId.Value);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(r => 
                        r.ReportName.ToLower().Contains(searchLower) ||
                        r.ReportCode.ToLower().Contains(searchLower) ||
                        (r.Description != null && r.Description.ToLower().Contains(searchLower)));
                }

                var reports = await query
                    .OrderBy(r => r.Category!.SortOrder)
                    .ThenBy(r => r.SortOrder)
                    .ThenBy(r => r.ReportName)
                    .Select(r => new ReportListDto
                    {
                        Id = r.Id,
                        ReportCode = r.ReportCode,
                        ReportName = r.ReportName,
                        Description = r.Description,
                        CategoryName = r.Category!.CategoryName,
                        Icon = r.Icon,
                        IsActive = r.IsActive,
                        SortOrder = r.SortOrder,
                        IsFavorite = userId.HasValue && _db.ReportFavorites
                            .Any(f => f.ReportId == r.Id && f.UserId == userId.Value)
                    })
                    .ToListAsync();

                if (onlyFavorites == true)
                {
                    reports = reports.Where(r => r.IsFavorite).ToList();
                }

                _logger.LogInformation("📊 Retornando {Count} reportes para el usuario", reports.Count);
                return Ok(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo reportes");
                return StatusCode(500, new { message = "Error obteniendo reportes" });
            }
        }

        // GET: api/report/{id} - Detalle de reporte con filtros y columnas
        [HttpGet("{id}")]
        public async Task<ActionResult<ReportDetailDto>> GetReport(int id)
        {
            try
            {
                // Verificar que el usuario tiene acceso a este reporte
                var userId = GetCurrentUserId();
                var companyId = GetCurrentCompanyId();

                if (userId.HasValue && companyId.HasValue)
                {
                    var hasAccess = await _db.UserCompanyReports
                        .AnyAsync(ucr => ucr.UserId == userId.Value 
                            && ucr.CompanyId == companyId.Value 
                            && ucr.ReportDefinitionId == id
                            && ucr.IsAllowed 
                            && ucr.IsActive);

                    if (!hasAccess)
                    {
                        _logger.LogWarning("⛔ Usuario {UserId} intentó acceder al reporte {ReportId} sin permiso", 
                            userId, id);
                        return Forbid();
                    }
                }

                var report = await _db.ReportDefinitions
                    .Include(r => r.Category)
                    .Include(r => r.Filters.Where(f => f.IsActive).OrderBy(f => f.SortOrder))
                    .Include(r => r.Columns.Where(c => c.IsActive).OrderBy(c => c.SortOrder))
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (report == null)
                    return NotFound(new { message = "Reporte no encontrado" });

                var dto = new ReportDetailDto
                {
                    Id = report.Id,
                    ReportCode = report.ReportCode,
                    ReportName = report.ReportName,
                    Description = report.Description,
                    CategoryId = report.CategoryId,
                    CategoryName = report.Category?.CategoryName ?? "",
                    DataSourceType = report.DataSourceType,
                    DataSource = report.DataSource,
                    ConnectionType = report.ConnectionType,
                    Icon = report.Icon,
                    DefaultPageSize = report.DefaultPageSize,
                    AllowExportExcel = report.AllowExportExcel,
                    AllowExportPdf = report.AllowExportPdf,
                    AllowExportCsv = report.AllowExportCsv,
                    RequiredPermission = report.RequiredPermission,
                    IsActive = report.IsActive,
                    Filters = report.Filters.Select(f => MapFilterToDto(f)).ToList(),
                    Columns = report.Columns.Select(c => new ReportColumnDto
                    {
                        Id = c.Id,
                        ReportId = c.ReportId,
                        ColumnKey = c.ColumnKey,
                        ColumnName = c.ColumnName,
                        ColumnDescription = c.ColumnDescription,
                        DataType = c.DataType,
                        FormatPattern = c.FormatPattern,
                        Width = c.Width,
                        MinWidth = c.MinWidth,
                        TextAlign = c.TextAlign,
                        CssClass = c.CssClass,
                        BadgeConfig = c.BadgeConfig,
                        LinkTemplate = c.LinkTemplate,
                        LinkTarget = c.LinkTarget,
                        IsSortable = c.IsSortable,
                        IsFilterable = c.IsFilterable,
                        IsVisible = c.IsVisible,
                        IsExportable = c.IsExportable,
                        ShowTotal = c.ShowTotal,
                        AggregationType = c.AggregationType,
                        SortOrder = c.SortOrder,
                        DefaultSortDirection = c.DefaultSortDirection,
                        IsActive = c.IsActive
                    }).ToList()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo reporte {Id}", id);
                return StatusCode(500, new { message = "Error obteniendo reporte" });
            }
        }

        // GET: api/report/filter/{filterId}/options - Obtener opciones dinámicas de un filtro DROPDOWN_SQL
        [HttpGet("filter/{filterId}/options")]
        public async Task<ActionResult<List<SelectOption>>> GetFilterOptions(int filterId)
        {
            try
            {
                var filter = await _db.ReportFilters
                    .Include(f => f.Report)
                    .FirstOrDefaultAsync(f => f.Id == filterId);

                if (filter == null)
                    return NotFound(new { message = "Filtro no encontrado" });

                if (filter.FilterType != "DROPDOWN_SQL" || string.IsNullOrEmpty(filter.DataSource))
                    return Ok(new List<SelectOption>());

                // Obtener connection string según el tipo de conexión del reporte
                string? connectionString;
                string? schema = null;

                if (filter.Report?.ConnectionType == "COMPANY")
                {
                    // ⭐ USAR EL FACTORY CENTRALIZADO
                    var (connStr, companySchema) = await GetCompanyConnectionStringAsync();
                    connectionString = connStr;
                    schema = companySchema;
                }
                else
                {
                    var env = _environment.IsDevelopment() ? "Development" : "Production";
                    connectionString = _configuration[$"ConnectionStrings:{env}:DefaultConnection"];
                    schema = "admin";
                }

                if (string.IsNullOrEmpty(connectionString))
                    return Ok(new List<SelectOption>());

                // Reemplazar {schema} en el query
                var query = filter.DataSource;
                if (!string.IsNullOrEmpty(schema))
                {
                    query = query.Replace("{schema}", schema);
                }

                var options = new List<SelectOption>();

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                await using var cmd = new NpgsqlCommand(query, connection);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    options.Add(new SelectOption
                    {
                        Value = reader["value"]?.ToString() ?? "",
                        Text = reader["text"]?.ToString() ?? ""
                    });
                }

                _logger.LogInformation("📋 Filtro {FilterId} cargó {Count} opciones dinámicas", filterId, options.Count);
                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo opciones del filtro {FilterId}", filterId);
                return StatusCode(500, new { message = "Error obteniendo opciones del filtro" });
            }
        }

        private ReportFilterDto MapFilterToDto(ReportFilter filter)
        {
            var dto = new ReportFilterDto
            {
                Id = filter.Id,
                ReportId = filter.ReportId,
                FilterKey = filter.FilterKey,
                FilterName = filter.FilterName,
                FilterDescription = filter.FilterDescription,
                FilterType = filter.FilterType,
                DataSource = filter.DataSource,
                DefaultValue = filter.DefaultValue,
                Placeholder = filter.Placeholder,
                IsRequired = filter.IsRequired,
                MinValue = filter.MinValue,
                MaxValue = filter.MaxValue,
                ColSpan = filter.ColSpan,
                SortOrder = filter.SortOrder,
                GroupName = filter.GroupName,
                IsActive = filter.IsActive,
                IsVisible = filter.IsVisible
            };

            // Parsear opciones si es SELECT/MULTISELECT/DROPDOWN
            var selectTypes = new[] { "SELECT", "MULTISELECT", "DROPDOWN" };
            if (selectTypes.Contains(filter.FilterType) && !string.IsNullOrEmpty(filter.DataSource))
            {
                try
                {
                    // Si empieza con [ es JSON directo
                    if (filter.DataSource.TrimStart().StartsWith("["))
                    {
                        // Usar opciones case-insensitive para deserializar JSON con propiedades en minúsculas
                        var jsonOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        dto.Options = JsonSerializer.Deserialize<List<SelectOption>>(filter.DataSource, jsonOptions);
                        _logger.LogDebug("📋 Filtro {FilterKey} tiene {Count} opciones JSON", filter.FilterKey, dto.Options?.Count ?? 0);
                    }
                    // Si no, es una referencia a tabla (TABLE:nombre:id:text)
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parseando opciones del filtro {FilterKey}: {DataSource}", 
                        filter.FilterKey, filter.DataSource);
                }
            }

            // Para DROPDOWN_SQL, se debe cargar dinámicamente en otro endpoint
            // Por ahora, marcamos que necesita carga dinámica
            if (filter.FilterType == "DROPDOWN_SQL")
            {
                dto.RequiresDynamicLoad = true;
                _logger.LogDebug("📋 Filtro {FilterKey} requiere carga dinámica (DROPDOWN_SQL)", filter.FilterKey);
            }

            return dto;
        }

        #endregion

        #region Ejecución de Reportes

        // POST: api/report/execute
        [HttpPost("execute")]
        public async Task<ActionResult<ReportExecuteResult>> ExecuteReport([FromBody] ReportExecuteRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            int? userId = GetCurrentUserId();
            int? companyId = GetCurrentCompanyId();

            try
            {
                var report = await _db.ReportDefinitions
                    .Include(r => r.Filters)
                    .Include(r => r.Columns)
                    .FirstOrDefaultAsync(r => r.Id == request.ReportId && r.IsActive);

                if (report == null)
                    return NotFound(new { message = "Reporte no encontrado" });

                // Verificar permiso si es requerido
                if (!string.IsNullOrEmpty(report.RequiredPermission))
                {
                    // TODO: Verificar permiso del usuario
                }

                // Construir y ejecutar query
                var result = await ExecuteReportQueryAsync(report, request);

                stopwatch.Stop();
                result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;

                // Registrar ejecución
                await LogReportExecutionAsync(report.Id, userId, companyId, request.Filters, 
                    result.TotalRows, result.ExecutionTimeMs, request.ExportType, 
                    result.Success ? "SUCCESS" : "ERROR", result.ErrorMessage);

                return Ok(result);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error ejecutando reporte {ReportId}", request.ReportId);

                await LogReportExecutionAsync(request.ReportId, userId, companyId, request.Filters,
                    0, (int)stopwatch.ElapsedMilliseconds, request.ExportType, "ERROR", ex.Message);

                return StatusCode(500, new ReportExecuteResult
                {
                    Success = false,
                    ErrorMessage = "Error ejecutando reporte: " + ex.Message
                });
            }
        }

        private async Task<ReportExecuteResult> ExecuteReportQueryAsync(ReportDefinition report, ReportExecuteRequest request)
        {
            var result = new ReportExecuteResult { Success = true };

            if (string.IsNullOrEmpty(report.DataSource))
            {
                result.Success = false;
                result.ErrorMessage = "El reporte no tiene una fuente de datos configurada";
                return result;
            }

            // Obtener connection string
            string? connectionString;
            string? companySchema = null;

            if (report.ConnectionType == "COMPANY")
            {
                // ⭐ USAR EL FACTORY CENTRALIZADO (misma lógica que ItemService)
                var (connStr, schema) = await GetCompanyConnectionStringAsync();
                connectionString = connStr;
                companySchema = schema;
            }
            else
            {
                // Usar connection string central (cms) según el ambiente real (ASPNETCORE_ENVIRONMENT)
                var env = _environment.IsDevelopment() ? "Development" : "Production";
                connectionString = _configuration[$"ConnectionStrings:{env}:DefaultConnection"];
                companySchema = "admin";
                _logger.LogDebug("Usando connection string {Env}", env);
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                result.Success = false;
                result.ErrorMessage = "No se pudo obtener la conexión a la base de datos";
                return result;
            }

            try
            {
                // LOG DETALLADO PARA DIAGNÓSTICO
                _logger.LogWarning("🔍 DIAGNÓSTICO REPORTE:");
                _logger.LogWarning("   - ConnectionString: {CS}", connectionString?.Substring(0, Math.Min(50, connectionString?.Length ?? 0)) + "...");
                _logger.LogWarning("   - Schema: {Schema}", companySchema);
                _logger.LogWarning("   - DataSource (primeros 200 chars): {DS}", report.DataSource?.Substring(0, Math.Min(200, report.DataSource?.Length ?? 0)));

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                // Verificar la base de datos a la que estamos conectados
                await using var checkDbCmd = new NpgsqlCommand("SELECT current_database(), current_schema()", connection);
                await using var dbReader = await checkDbCmd.ExecuteReaderAsync();
                if (await dbReader.ReadAsync())
                {
                    _logger.LogWarning("   - BD Conectada: {DB}, Schema actual: {Schema}", dbReader.GetString(0), dbReader.IsDBNull(1) ? "NULL" : dbReader.GetString(1));
                }
                await dbReader.CloseAsync();

                // Preparar el query base y reemplazar {schema} si es necesario
                var baseQuery = report.DataSource;
                if (!string.IsNullOrEmpty(companySchema))
                {
                    baseQuery = baseQuery.Replace("{schema}", companySchema);
                    _logger.LogInformation("📊 Query con schema reemplazado: {Schema}", companySchema);
                }

                _logger.LogWarning("   - Query final (primeros 300 chars): {Q}", baseQuery?.Substring(0, Math.Min(300, baseQuery?.Length ?? 0)));

                var whereConditions = new List<string>();
                var parameters = new List<NpgsqlParameter>();
                var paramIndex = 1;

                // Detectar el contexto del reporte basado en el query
                var reportContext = DetectReportContext(baseQuery);

                // Procesar filtros activos que tienen valor
                foreach (var filter in report.Filters.Where(f => f.IsActive))
                {
                    var filterKey = filter.FilterKey; // search, estado, etc.

                    // Verificar si el filtro tiene valor en el request
                    if (request.Filters.TryGetValue(filterKey, out var filterValue) && 
                        filterValue != null && 
                        !string.IsNullOrWhiteSpace(filterValue.ToString()))
                    {
                        var value = ConvertFilterValue(filterValue, filter.FilterType);
                        if (value != null)
                        {
                            // Generar condición WHERE según el tipo de filtro y contexto del reporte
                            var condition = GenerateWhereCondition(filterKey, filter.FilterType, paramIndex, value, reportContext);
                            if (!string.IsNullOrEmpty(condition))
                            {
                                whereConditions.Add(condition);
                                parameters.Add(new NpgsqlParameter { Value = value });
                                paramIndex++;
                            }
                        }
                    }
                }

                // Agregar condiciones WHERE al query base si hay filtros
                if (whereConditions.Count > 0)
                {
                    var whereClause = string.Join(" AND ", whereConditions);

                    // Detectar si el query tiene ORDER BY, GROUP BY, HAVING, LIMIT, OFFSET
                    // para insertar el WHERE/AND en el lugar correcto
                    var orderByIndex = baseQuery.LastIndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
                    var groupByIndex = baseQuery.LastIndexOf("GROUP BY", StringComparison.OrdinalIgnoreCase);
                    var havingIndex = baseQuery.LastIndexOf("HAVING", StringComparison.OrdinalIgnoreCase);
                    var limitIndex = baseQuery.LastIndexOf("LIMIT", StringComparison.OrdinalIgnoreCase);

                    // Encontrar la posición más temprana de estos modificadores
                    var insertPositions = new[] { orderByIndex, groupByIndex, havingIndex, limitIndex }
                        .Where(pos => pos > 0)
                        .ToList();

                    var insertPosition = insertPositions.Count > 0 ? insertPositions.Min() : -1;

                    if (baseQuery.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ya tiene WHERE, agregar con AND
                        if (insertPosition > 0)
                        {
                            // Insertar AND antes de ORDER BY/GROUP BY/etc.
                            baseQuery = baseQuery.Insert(insertPosition, $" AND {whereClause} ");
                        }
                        else
                        {
                            // No hay ORDER BY, agregar al final
                            baseQuery += " AND " + whereClause;
                        }
                    }
                    else
                    {
                        // No tiene WHERE, agregar WHERE
                        if (insertPosition > 0)
                        {
                            // Insertar WHERE antes de ORDER BY/GROUP BY/etc.
                            baseQuery = baseQuery.Insert(insertPosition, $" WHERE {whereClause} ");
                        }
                        else
                        {
                            // No hay ORDER BY, agregar al final
                            baseQuery += " WHERE " + whereClause;
                        }
                    }

                    // Log de diagnóstico para filtros
                    _logger.LogWarning("📋 WHERE CONDITIONS: {Conditions}", whereClause);
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        _logger.LogWarning("   - Param ${Index}: {Value} (Type: {Type})", i + 1, parameters[i].Value, parameters[i].Value?.GetType().Name);
                    }
                }

                _logger.LogDebug("Query con filtros: {Query}", baseQuery.Substring(0, Math.Min(500, baseQuery.Length)));

                // Construir query con paginación
                var offset = (request.Page - 1) * request.PageSize;
                // Solo considerar columnas ACTIVAS para el sort
                var activeColumns = report.Columns.Where(c => c.IsActive).ToList();
                var sortColumn = request.SortColumn ?? activeColumns.FirstOrDefault()?.ColumnKey ?? "1";
                var sortDirection = request.SortDirection?.ToUpper() == "DESC" ? "DESC" : "ASC";

                // Query para contar total
                var countQuery = $"SELECT COUNT(*) FROM ({baseQuery}) AS count_query";

                // Query con paginación
                var pagedQuery = $@"
                    SELECT * FROM ({baseQuery}) AS base_query
                    ORDER BY ""{sortColumn}"" {sortDirection}
                    OFFSET {offset} LIMIT {request.PageSize}";

                // Log de la query completa
                _logger.LogWarning("🔍 COUNT QUERY: {Query}", countQuery.Length > 1000 ? countQuery.Substring(0, 1000) + "..." : countQuery);

                await using var countCmd = new NpgsqlCommand(countQuery, connection);
                await using var dataCmd = new NpgsqlCommand(pagedQuery, connection);

                // Agregar los parámetros a ambos comandos
                foreach (var param in parameters)
                {
                    countCmd.Parameters.Add(param.Clone());
                    dataCmd.Parameters.Add(param.Clone());
                }

                // Ejecutar count
                var totalRows = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                _logger.LogWarning("📊 TOTAL ROWS: {TotalRows}", totalRows);
                result.TotalRows = totalRows;
                result.Page = request.Page;
                result.PageSize = request.PageSize;
                result.TotalPages = (int)Math.Ceiling(totalRows / (double)request.PageSize);

                // Ejecutar query de datos
                await using var reader = await dataCmd.ExecuteReaderAsync();
                var columns = Enumerable.Range(0, reader.FieldCount)
                    .Select(i => reader.GetName(i))
                    .ToList();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    foreach (var column in columns)
                    {
                        var value = reader[column];
                        row[column] = value == DBNull.Value ? null : value;
                    }
                    result.Data.Add(row);
                }

                return result;
            }
            catch (Npgsql.PostgresException pgEx)
            {
                _logger.LogError(pgEx, "Error PostgreSQL ejecutando reporte {ReportId}", report.Id);
                result.Success = false;

                // Mensajes de error amigables según el código de error
                result.ErrorMessage = pgEx.SqlState switch
                {
                    "42P01" => $"La tabla requerida no existe en la base de datos. Contacte al administrador. (Tabla: {companySchema})",
                    "42703" => $"Una columna del reporte no existe en la tabla. Verifique la configuración del reporte.",
                    "42501" => "No tiene permisos para acceder a esta información.",
                    "08001" or "08006" => "No se pudo conectar a la base de datos. Intente nuevamente.",
                    _ => $"Error de base de datos: {pgEx.MessageText}"
                };

                return result;
            }
        }

        private object? ConvertFilterValue(object value, string filterType)
        {
            if (value == null) return null;

            var strValue = value.ToString();
            if (string.IsNullOrEmpty(strValue)) return null;

            return filterType switch
            {
                "NUMBER" or "INTEGER" => int.TryParse(strValue, out var intVal) ? intVal : null,
                "DECIMAL" => decimal.TryParse(strValue, out var decVal) ? decVal : null,
                // Para fechas: mantener como string en formato ISO para que PostgreSQL haga el cast correctamente
                "DATE" or "DATETIME" => strValue, // Mantener como string "YYYY-MM-DD"
                "CHECKBOX" => bool.TryParse(strValue, out var boolVal) && boolVal,
                _ => strValue
            };
        }

        /// <summary>
        /// Detecta el contexto del reporte basado en el query (qué tabla/alias usa)
        /// </summary>
        private string DetectReportContext(string query)
        {
            // Detectar por alias específicos en el query
            if (query.Contains("label_print_history") || query.Contains(" h.item_code") || query.Contains(" h.print_date"))
                return "LABEL_PRINT_HISTORY";

            if (query.Contains(".item ") || query.Contains(" i.code") || query.Contains(" i.name") || query.Contains("FROM item"))
                return "ITEMS";

            if (query.Contains(".user ") || query.Contains(" u.email") || query.Contains(" u.first_name"))
                return "USERS";

            return "GENERIC";
        }

        /// <summary>
        /// Genera una condición WHERE basada en el filtro, su tipo y el contexto del reporte
        /// </summary>
        private string GenerateWhereCondition(string filterKey, string filterType, int paramIndex, object value, string reportContext)
        {
            var paramPlaceholder = $"${paramIndex}";
            var key = filterKey.ToLower();

            // Primero, manejar el filtro "search" basado en el contexto del reporte
            if (key == "search" && filterType == "TEXT")
            {
                return reportContext switch
                {
                    "LABEL_PRINT_HISTORY" =>
                        $"(LOWER(h.item_code) LIKE LOWER('%' || {paramPlaceholder} || '%') OR " +
                        $"LOWER(h.item_name) LIKE LOWER('%' || {paramPlaceholder} || '%') OR " +
                        $"LOWER(COALESCE(h.label_item, '')) LIKE LOWER('%' || {paramPlaceholder} || '%') OR " +
                        $"LOWER(COALESCE(h.printed_by, '')) LIKE LOWER('%' || {paramPlaceholder} || '%') OR " +
                        $"LOWER(COALESCE(h.printer_name, '')) LIKE LOWER('%' || {paramPlaceholder} || '%'))",

                    "ITEMS" =>
                        $"(LOWER(i.code) LIKE LOWER('%' || {paramPlaceholder} || '%') OR " +
                        $"LOWER(i.name) LIKE LOWER('%' || {paramPlaceholder} || '%') OR " +
                        $"LOWER(COALESCE(i.brand, '')) LIKE LOWER('%' || {paramPlaceholder} || '%') OR " +
                        $"LOWER(COALESCE(i.description, '')) LIKE LOWER('%' || {paramPlaceholder} || '%'))",

                    "USERS" =>
                        $"(LOWER(u.first_name) LIKE LOWER('%' || {paramPlaceholder} || '%') OR " +
                        $"LOWER(u.last_name) LIKE LOWER('%' || {paramPlaceholder} || '%') OR " +
                        $"LOWER(u.email) LIKE LOWER('%' || {paramPlaceholder} || '%') OR " +
                        $"LOWER(u.display_name) LIKE LOWER('%' || {paramPlaceholder} || '%'))",

                    _ => $"LOWER({filterKey}) LIKE LOWER('%' || {paramPlaceholder} || '%')"
                };
            }

            // Mapeo de filtros específicos por nombre de filtro
            return key switch
            {
                // ====== FILTROS PARA REPORTE DE ITEMS (ITM001) ======
                // Filtro de estado (Activo/Inactivo) para items
                "isactive" => value.ToString()?.ToLower() switch
                {
                    "true" => "i.is_active = true",
                    "false" => "i.is_active = false",
                    _ => "" // Si está vacío, no filtrar
                },

                // Filtro de clasificación para items - CAST a INTEGER porque el valor viene como string
                "classificationid" when !string.IsNullOrEmpty(value?.ToString()) && value.ToString() != "0" =>
                    $"(i.id_classification1 = {paramPlaceholder}::INTEGER OR " +
                    $"i.id_classification2 = {paramPlaceholder}::INTEGER OR " +
                    $"i.id_classification3 = {paramPlaceholder}::INTEGER OR " +
                    $"i.id_classification4 = {paramPlaceholder}::INTEGER OR " +
                    $"i.id_classification5 = {paramPlaceholder}::INTEGER OR " +
                    $"i.id_classification6 = {paramPlaceholder}::INTEGER)",

                // Filtros de fecha para items - usar i.createdate::DATE para comparar solo fecha
                "datefrom" => $"i.createdate::DATE >= {paramPlaceholder}::DATE",
                "dateto" => $"i.createdate::DATE <= {paramPlaceholder}::DATE",

                // ====== FILTROS PARA REPORTE DE HISTORIAL DE ETIQUETAS (LABEL_PRINT_HISTORY) ======
                // Filtros de fecha para label_print_history
                "fecha_desde" => $"h.print_date::DATE >= {paramPlaceholder}::DATE",
                "fecha_hasta" => $"h.print_date::DATE <= {paramPlaceholder}::DATE",

                // ====== FILTROS PARA REPORTE DE USUARIOS ======
                // Filtro de estado para usuarios
                "estado" => value.ToString()?.ToLower() switch
                {
                    "activo" => "u.is_active = true",
                    "inactivo" => "u.is_active = false",
                    _ => ""
                },

                // Filtro de email verificado
                "email_verificado" => value.ToString()?.ToLower() switch
                {
                    "si" or "sí" => "u.is_email_verified = true",
                    "no" => "u.is_email_verified = false",
                    _ => ""
                },

                // Filtros de fecha para reporte de usuarios
                "fecha_creacion_desde" => $"DATE(u.createdate) >= DATE({paramPlaceholder})",
                "fecha_creacion_hasta" => $"DATE(u.createdate) <= DATE({paramPlaceholder})",
                "ultimo_login_desde" => $"DATE(u.last_login) >= DATE({paramPlaceholder})",
                "ultimo_login_hasta" => $"DATE(u.last_login) <= DATE({paramPlaceholder})",

                // ====== FILTROS GENÉRICOS ======
                _ => filterType.ToUpper() switch
                {
                    "TEXT" => $"LOWER({filterKey}) LIKE LOWER('%' || {paramPlaceholder} || '%')",
                    "SELECT" or "DROPDOWN" => $"{filterKey} = {paramPlaceholder}",
                    "NUMBER" or "INTEGER" => $"{filterKey} = {paramPlaceholder}",
                    "DATE" => $"DATE({filterKey}) = DATE({paramPlaceholder})",
                    "CHECKBOX" => $"{filterKey} = {paramPlaceholder}",
                    _ => ""
                }
            };
        }

        /// <summary>
        /// Obtiene el connection string de la compañía usando el factory centralizado.
        /// IMPORTANTE: Usa ICompanyDbContextFactory para garantizar la misma lógica que ItemService.
        /// </summary>
        private async Task<(string? ConnectionString, string? Schema)> GetCompanyConnectionStringAsync()
        {
            var companyId = GetCurrentCompanyId();
            if (!companyId.HasValue) 
            {
                _logger.LogWarning("❌ No se pudo obtener companyId del JWT");
                return (null, null);
            }

            try
            {
                var (connectionString, schema) = await _companyDbContextFactory.GetConnectionStringAsync(companyId.Value);
                _logger.LogInformation("✅ Connection string obtenido para compañía {CompanyId}, schema={Schema}", companyId, schema);
                return (connectionString, schema);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo connection string para compañía {CompanyId}", companyId);
                return (null, null);
            }
        }

        private async Task LogReportExecutionAsync(int reportId, int? userId, int? companyId,
            Dictionary<string, object?> filters, int rowsReturned, int executionTimeMs,
            string? exportType, string status, string? errorMessage)
        {
            try
            {
                // No registrar si no hay userId o companyId (requeridos en la tabla)
                if (!userId.HasValue || !companyId.HasValue)
                {
                    _logger.LogWarning("No se puede registrar ejecución de reporte: userId={UserId}, companyId={CompanyId}", userId, companyId);
                    return;
                }

                var log = new ReportExecutionLog
                {
                    ReportId = reportId,
                    UserId = userId.Value,
                    CompanyId = companyId.Value,
                    FiltersUsed = JsonSerializer.Serialize(filters),
                    RowsReturned = rowsReturned,
                    ExecutionTimeMs = executionTimeMs,
                    ExportType = exportType,
                    Status = status,
                    ErrorMessage = errorMessage,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString(),
                    CreatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM",
                    UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM"
                };

                _db.ReportExecutionLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error registrando ejecución de reporte");
            }
        }

        #endregion

        #region Exportación de Reportes

        // POST: api/report/export
        [HttpPost("export")]
        public async Task<IActionResult> ExportReport([FromBody] ReportExecuteRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            int? userId = GetCurrentUserId();
            int? companyId = GetCurrentCompanyId();

            try
            {
                if (string.IsNullOrEmpty(request.ExportType))
                {
                    return BadRequest(new { message = "Tipo de exportación requerido (EXCEL, PDF, CSV)" });
                }

                var report = await _db.ReportDefinitions
                    .Include(r => r.Filters)
                    .Include(r => r.Columns)
                    .FirstOrDefaultAsync(r => r.Id == request.ReportId && r.IsActive);

                if (report == null)
                    return NotFound(new { message = "Reporte no encontrado" });

                // Verificar permisos de exportación
                if (request.ExportType == "EXCEL" && !report.AllowExportExcel)
                    return BadRequest(new { message = "Este reporte no permite exportación a Excel" });
                if (request.ExportType == "PDF" && !report.AllowExportPdf)
                    return BadRequest(new { message = "Este reporte no permite exportación a PDF" });
                if (request.ExportType == "CSV" && !report.AllowExportCsv)
                    return BadRequest(new { message = "Este reporte no permite exportación a CSV" });

                // Para exportación, obtener TODOS los datos (sin paginación)
                request.Page = 1;
                request.PageSize = 100000; // Límite máximo para exportación

                var result = await ExecuteReportQueryAsync(report, request);

                if (!result.Success)
                {
                    return StatusCode(500, new { message = result.ErrorMessage });
                }

                // Obtener solo las columnas exportables
                var exportableColumns = report.Columns
                    .Where(c => c.IsExportable && c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ToList();

                byte[] fileContent;
                string contentType;
                string fileName;

                switch (request.ExportType.ToUpper())
                {
                    case "EXCEL":
                        fileContent = GenerateExcel(report.ReportName, result.Data, exportableColumns);
                        contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        fileName = $"{SanitizeFileName(report.ReportName)}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                        break;

                    case "CSV":
                        fileContent = GenerateCsv(result.Data, exportableColumns);
                        contentType = "text/csv";
                        fileName = $"{SanitizeFileName(report.ReportName)}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                        break;

                    case "PDF":
                        fileContent = GeneratePdf(report.ReportName, result.Data, exportableColumns);
                        contentType = "text/html";
                        fileName = $"{SanitizeFileName(report.ReportName)}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                        break;

                    default:
                        return BadRequest(new { message = $"Tipo de exportación no soportado: {request.ExportType}" });
                }

                stopwatch.Stop();

                // Registrar ejecución
                await LogReportExecutionAsync(report.Id, userId, companyId, request.Filters,
                    result.TotalRows, (int)stopwatch.ElapsedMilliseconds, request.ExportType, "SUCCESS", null);

                return File(fileContent, contentType, fileName);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error exportando reporte {ReportId} a {ExportType}", request.ReportId, request.ExportType);

                await LogReportExecutionAsync(request.ReportId, userId, companyId, request.Filters,
                    0, (int)stopwatch.ElapsedMilliseconds, request.ExportType, "ERROR", ex.Message);

                return StatusCode(500, new { message = "Error exportando reporte: " + ex.Message });
            }
        }

        private byte[] GenerateExcel(string reportName, List<Dictionary<string, object?>> data, List<ReportColumn> columns)
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Datos");

            // Header con estilo
            for (int i = 0; i < columns.Count; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = columns[i].ColumnName;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1a1a2e");
                cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                cell.Style.Border.BottomBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            }

            // Datos
            for (int row = 0; row < data.Count; row++)
            {
                for (int col = 0; col < columns.Count; col++)
                {
                    var columnKey = columns[col].ColumnKey;
                    // Usar búsqueda case-insensitive para manejar camelCase del JSON
                    var value = GetRowValue(data[row], columnKey);
                    var cell = worksheet.Cell(row + 2, col + 1);

                    if (value != null)
                    {
                        // Formatear según el tipo de dato
                        switch (columns[col].DataType?.ToUpper())
                        {
                            case "CURRENCY":
                            case "DECIMAL":
                            case "NUMBER":
                                if (decimal.TryParse(value.ToString(), out var numValue))
                                {
                                    cell.Value = numValue;
                                    if (columns[col].DataType?.ToUpper() == "CURRENCY")
                                        cell.Style.NumberFormat.Format = "₡#,##0.00";
                                }
                                else
                                {
                                    cell.Value = value.ToString();
                                }
                                break;

                            case "DATE":
                            case "DATETIME":
                                if (DateTime.TryParse(value.ToString(), out var dateValue))
                                {
                                    cell.Value = dateValue;
                                    cell.Style.NumberFormat.Format = columns[col].DataType?.ToUpper() == "DATE"
                                        ? "dd/MM/yyyy"
                                        : "dd/MM/yyyy HH:mm";
                                }
                                else
                                {
                                    cell.Value = value.ToString();
                                }
                                break;

                            default:
                                cell.Value = value.ToString();
                                break;
                        }
                    }
                }
            }

            // Ajustar ancho de columnas
            worksheet.Columns().AdjustToContents();

            // Agregar título
            worksheet.PageSetup.Header.Left.AddText(reportName);
            worksheet.PageSetup.Header.Right.AddText($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private byte[] GenerateCsv(List<Dictionary<string, object?>> data, List<ReportColumn> columns)
        {
            var sb = new System.Text.StringBuilder();

            // BOM para UTF-8 (para que Excel lo abra correctamente)
            sb.Append('\uFEFF');

            // Header
            sb.AppendLine(string.Join(",", columns.Select(c => EscapeCsvValue(c.ColumnName))));

            // Datos
            foreach (var row in data)
            {
                var values = columns.Select(col =>
                {
                    // Usar búsqueda case-insensitive para manejar camelCase del JSON
                    var value = GetRowValue(row, col.ColumnKey);
                    return EscapeCsvValue(value?.ToString() ?? "");
                });
                sb.AppendLine(string.Join(",", values));
            }

            return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        }

        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // Si contiene caracteres especiales, encerrar en comillas
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        /// <summary>
        /// Busca un valor en el diccionario de forma case-insensitive.
        /// El JSON usa camelCase pero las columnas tienen el nombre original.
        /// </summary>
        private object? GetRowValue(Dictionary<string, object?> row, string columnKey)
        {
            // Primero intentar el key exacto
            if (row.ContainsKey(columnKey))
                return row[columnKey];

            // Intentar camelCase (primera letra minúscula)
            var camelKey = char.ToLowerInvariant(columnKey[0]) + columnKey.Substring(1);
            if (row.ContainsKey(camelKey))
                return row[camelKey];

            // Búsqueda case-insensitive completa
            var lowerKey = columnKey.ToLowerInvariant();
            foreach (var key in row.Keys)
            {
                if (key.ToLowerInvariant() == lowerKey)
                    return row[key];
            }

            return null;
        }

        private byte[] GeneratePdf(string reportName, List<Dictionary<string, object?>> data, List<ReportColumn> columns)
        {
            // Generar un HTML básico y convertirlo a PDF
            // Por ahora, generamos un HTML que se puede imprimir como PDF
            var html = new System.Text.StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine($"<title>{reportName}</title>");
            html.AppendLine(@"<style>
                body { font-family: Arial, sans-serif; font-size: 10px; margin: 20px; }
                h1 { color: #1a1a2e; font-size: 16px; margin-bottom: 5px; }
                .meta { color: #666; font-size: 9px; margin-bottom: 15px; }
                table { width: 100%; border-collapse: collapse; }
                th { background: #1a1a2e; color: white; padding: 6px 4px; text-align: left; font-size: 9px; }
                td { padding: 4px; border-bottom: 1px solid #ddd; font-size: 9px; }
                tr:nth-child(even) { background: #f8f9fa; }
                .number { text-align: right; }
                @media print {
                    body { margin: 0; }
                    h1 { font-size: 14px; }
                }
            </style>");
            html.AppendLine("</head><body>");
            html.AppendLine($"<h1>{reportName}</h1>");
            html.AppendLine($"<div class='meta'>Generado: {DateTime.Now:dd/MM/yyyy HH:mm} | Total registros: {data.Count}</div>");
            html.AppendLine("<table>");

            // Header
            html.AppendLine("<thead><tr>");
            foreach (var col in columns)
            {
                html.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(col.ColumnName)}</th>");
            }
            html.AppendLine("</tr></thead>");

            // Body
            html.AppendLine("<tbody>");
            foreach (var row in data)
            {
                html.AppendLine("<tr>");
                foreach (var col in columns)
                {
                    // Usar búsqueda case-insensitive para manejar camelCase del JSON
                    var value = GetRowValue(row, col.ColumnKey);
                    var cssClass = (col.DataType?.ToUpper() == "CURRENCY" || col.DataType?.ToUpper() == "NUMBER" || col.DataType?.ToUpper() == "DECIMAL")
                        ? "class='number'"
                        : "";
                    html.AppendLine($"<td {cssClass}>{System.Net.WebUtility.HtmlEncode(value?.ToString() ?? "")}</td>");
                }
                html.AppendLine("</tr>");
            }
            html.AppendLine("</tbody></table>");
            html.AppendLine("</body></html>");

            // Retornar el HTML como "PDF" (el navegador lo abrirá y el usuario podrá imprimir/guardar como PDF)
            // Para una implementación más avanzada se necesitaría una librería como iText, QuestPDF, etc.
            return System.Text.Encoding.UTF8.GetBytes(html.ToString());
        }

        private string SanitizeFileName(string fileName)
        {
            // Remover caracteres no válidos para nombres de archivo
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            var sanitized = string.Join("", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Replace(" ", "_");
        }

        #endregion

        #region Favoritos

        // GET: api/report/favorites
        [HttpGet("favorites")]
        public async Task<ActionResult<List<ReportFavoriteDto>>> GetFavorites()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "Usuario no autenticado" });

                var favorites = await _db.ReportFavorites
                    .Include(f => f.Report)
                    .Where(f => f.UserId == userId.Value)
                    .OrderBy(f => f.Report!.ReportName)
                    .Select(f => new ReportFavoriteDto
                    {
                        Id = f.Id,
                        ReportId = f.ReportId,
                        ReportName = f.Report!.ReportName,
                        FavoriteName = f.FavoriteName,
                        SavedFilters = f.SavedFilters,
                        CreateDate = f.CreateDate
                    })
                    .ToListAsync();

                return Ok(favorites);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo favoritos");
                return StatusCode(500, new { message = "Error obteniendo favoritos" });
            }
        }

        // POST: api/report/favorites
        [HttpPost("favorites")]
        public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "Usuario no autenticado" });

                // Verificar si ya existe
                var exists = await _db.ReportFavorites
                    .AnyAsync(f => f.UserId == userId.Value && f.ReportId == request.ReportId);

                if (exists)
                    return BadRequest(new { message = "El reporte ya está en favoritos" });

                var favorite = new ReportFavorite
                {
                    UserId = userId.Value,
                    ReportId = request.ReportId,
                    FavoriteName = request.FavoriteName,
                    SavedFilters = request.SavedFilters != null 
                        ? JsonSerializer.Serialize(request.SavedFilters) 
                        : null
                };

                _db.ReportFavorites.Add(favorite);
                await _db.SaveChangesAsync();

                return Ok(new { success = true, message = "Reporte agregado a favoritos" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error agregando favorito");
                return StatusCode(500, new { message = "Error agregando favorito" });
            }
        }

        // DELETE: api/report/favorites/{reportId}
        [HttpDelete("favorites/{reportId}")]
        public async Task<IActionResult> RemoveFavorite(int reportId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "Usuario no autenticado" });

                var favorite = await _db.ReportFavorites
                    .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.ReportId == reportId);

                if (favorite == null)
                    return NotFound(new { message = "Favorito no encontrado" });

                _db.ReportFavorites.Remove(favorite);
                await _db.SaveChangesAsync();

                return Ok(new { success = true, message = "Reporte removido de favoritos" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removiendo favorito");
                return StatusCode(500, new { message = "Error removiendo favorito" });
            }
        }

        #endregion

        #region Administración (CRUD)

        // POST: api/report - Crear reporte
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<ReportDetailDto>> CreateReport([FromBody] ReportCreateDto dto)
        {
            try
            {
                // Validar código único
                if (await _db.ReportDefinitions.AnyAsync(r => r.ReportCode == dto.ReportCode))
                    return BadRequest(new { message = "El código de reporte ya existe" });

                var report = new ReportDefinition
                {
                    ReportCode = dto.ReportCode,
                    ReportName = dto.ReportName,
                    Description = dto.Description,
                    CategoryId = dto.CategoryId,
                    DataSourceType = dto.DataSourceType,
                    DataSource = dto.DataSource,
                    ConnectionType = dto.ConnectionType,
                    Icon = dto.Icon,
                    DefaultPageSize = dto.DefaultPageSize,
                    AllowExportExcel = dto.AllowExportExcel,
                    AllowExportPdf = dto.AllowExportPdf,
                    AllowExportCsv = dto.AllowExportCsv,
                    RequiredPermission = dto.RequiredPermission,
                    IsActive = dto.IsActive,
                    CreatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM",
                    UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM"
                };

                _db.ReportDefinitions.Add(report);
                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Reporte creado: {ReportCode} - {ReportName}", report.ReportCode, report.ReportName);

                return CreatedAtAction(nameof(GetReport), new { id = report.Id }, await GetReport(report.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando reporte");
                return StatusCode(500, new { message = "Error creando reporte" });
            }
        }

        // PUT: api/report/{id}
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReport(int id, [FromBody] ReportUpdateDto dto)
        {
            try
            {
                var report = await _db.ReportDefinitions.FindAsync(id);
                if (report == null)
                    return NotFound(new { message = "Reporte no encontrado" });

                // Validar código único si cambió
                if (dto.ReportCode != report.ReportCode)
                {
                    if (await _db.ReportDefinitions.AnyAsync(r => r.ReportCode == dto.ReportCode && r.Id != id))
                        return BadRequest(new { message = "El código de reporte ya existe" });
                }

                report.ReportCode = dto.ReportCode;
                report.ReportName = dto.ReportName;
                report.Description = dto.Description;
                report.CategoryId = dto.CategoryId;
                report.DataSourceType = dto.DataSourceType;
                report.DataSource = dto.DataSource;
                report.ConnectionType = dto.ConnectionType;
                report.Icon = dto.Icon;
                report.DefaultPageSize = dto.DefaultPageSize;
                report.AllowExportExcel = dto.AllowExportExcel;
                report.AllowExportPdf = dto.AllowExportPdf;
                report.AllowExportCsv = dto.AllowExportCsv;
                report.RequiredPermission = dto.RequiredPermission;
                report.IsActive = dto.IsActive;
                report.UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM";

                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Reporte actualizado: {ReportCode}", report.ReportCode);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando reporte {Id}", id);
                return StatusCode(500, new { message = "Error actualizando reporte" });
            }
        }

        // DELETE: api/report/{id}
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReport(int id)
        {
            try
            {
                var report = await _db.ReportDefinitions.FindAsync(id);
                if (report == null)
                    return NotFound(new { message = "Reporte no encontrado" });

                // Soft delete
                report.IsActive = false;
                report.UpdatedBy = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM";
                await _db.SaveChangesAsync();

                _logger.LogInformation("🗑️ Reporte desactivado: {ReportCode}", report.ReportCode);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando reporte {Id}", id);
                return StatusCode(500, new { message = "Error eliminando reporte" });
            }
        }

        #endregion

        #region Permisos de Reportes por Usuario

        /// <summary>
        /// Obtiene los reportes asignados a un usuario en una compañía
        /// GET: api/report/user/{userId}/company/{companyId}/permissions
        /// </summary>
        [HttpGet("user/{userId}/company/{companyId}/permissions")]
        public async Task<ActionResult<List<UserReportPermissionDto>>> GetUserReportPermissions(int userId, int companyId)
        {
            try
            {
                // Obtener todos los reportes activos
                var allReports = await _db.ReportDefinitions
                    .Where(r => r.IsActive)
                    .Include(r => r.Category)
                    .OrderBy(r => r.Category!.SortOrder)
                    .ThenBy(r => r.SortOrder)
                    .ToListAsync();

                // Obtener los reportes asignados al usuario
                var userReports = await _db.UserCompanyReports
                    .Where(ucr => ucr.UserId == userId && ucr.CompanyId == companyId)
                    .ToListAsync();

                var result = allReports.Select(r => new UserReportPermissionDto
                {
                    ReportId = r.Id,
                    ReportCode = r.ReportCode,
                    ReportName = r.ReportName,
                    Description = r.Description,
                    CategoryId = r.CategoryId,
                    CategoryName = r.Category?.CategoryName ?? "Sin categoría",
                    Icon = r.Icon,
                    IsAllowed = userReports.Any(ur => ur.ReportDefinitionId == r.Id && ur.IsAllowed && ur.IsActive),
                    IsDenied = userReports.Any(ur => ur.ReportDefinitionId == r.Id && !ur.IsAllowed && ur.IsActive)
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo permisos de reportes para usuario {UserId} en compañía {CompanyId}", userId, companyId);
                return StatusCode(500, new { message = "Error obteniendo permisos de reportes" });
            }
        }

        /// <summary>
        /// Actualiza los permisos de reportes de un usuario en una compañía
        /// PUT: api/report/user/{userId}/company/{companyId}/permissions
        /// </summary>
        [HttpPut("user/{userId}/company/{companyId}/permissions")]
        public async Task<IActionResult> UpdateUserReportPermissions(
            int userId, 
            int companyId, 
            [FromBody] UpdateUserReportPermissionsRequest request)
        {
            try
            {
                var currentUser = User.FindFirstValue("cms_username") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? "SYSTEM";

                foreach (var permission in request.Permissions)
                {
                    var existing = await _db.UserCompanyReports
                        .FirstOrDefaultAsync(ucr => 
                            ucr.UserId == userId && 
                            ucr.CompanyId == companyId && 
                            ucr.ReportDefinitionId == permission.ReportId);

                    if (permission.IsAllowed)
                    {
                        if (existing != null)
                        {
                            existing.IsAllowed = true;
                            existing.IsActive = true;
                            existing.UpdatedBy = currentUser;
                            existing.RecordDate = DateTime.UtcNow;
                        }
                        else
                        {
                            _db.UserCompanyReports.Add(new UserCompanyReport
                            {
                                UserId = userId,
                                CompanyId = companyId,
                                ReportDefinitionId = permission.ReportId,
                                IsAllowed = true,
                                IsActive = true,
                                CreatedBy = currentUser,
                                UpdatedBy = currentUser
                            });
                        }
                    }
                    else
                    {
                        if (existing != null)
                        {
                            existing.IsAllowed = false;
                            existing.IsActive = true;
                            existing.UpdatedBy = currentUser;
                            existing.RecordDate = DateTime.UtcNow;
                        }
                    }
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("✅ Permisos de reportes actualizados para usuario {UserId} en compañía {CompanyId}", userId, companyId);

                return Ok(new { success = true, message = "Permisos actualizados correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando permisos de reportes para usuario {UserId}", userId);
                return StatusCode(500, new { message = "Error actualizando permisos de reportes" });
            }
        }

        /// <summary>
        /// Obtiene todas las categorías de reportes (para agrupar en la UI)
        /// GET: api/report/categories/all
        /// </summary>
        [HttpGet("categories/all")]
        public async Task<ActionResult<List<ReportCategoryDto>>> GetAllCategories()
        {
            try
            {
                var categories = await _db.ReportCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .Select(c => new ReportCategoryDto
                    {
                        Id = c.Id,
                        CategoryCode = c.CategoryCode,
                        CategoryName = c.CategoryName,
                        Description = c.Description,
                        Icon = c.Icon,
                        SortOrder = c.SortOrder,
                        IsActive = c.IsActive,
                        ReportCount = c.Reports.Count(r => r.IsActive)
                    })
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo categorías de reportes");
                return StatusCode(500, new { message = "Error obteniendo categorías" });
            }
        }

        #endregion

        #region Helpers

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private int? GetCurrentCompanyId()
        {
            // El claim es "companyId" (minúscula) según JwtTokenService
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            return int.TryParse(companyIdClaim, out var companyId) ? companyId : null;
        }

        #endregion
    }

    // ===== DTOs para permisos de reportes =====

    public class UserReportPermissionDto
    {
        public int ReportId { get; set; }
        public string ReportCode { get; set; } = string.Empty;
        public string ReportName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public bool IsAllowed { get; set; }
        public bool IsDenied { get; set; }
    }

    public class UpdateUserReportPermissionsRequest
    {
        public List<ReportPermissionItem> Permissions { get; set; } = new();
    }

    public class ReportPermissionItem
    {
        public int ReportId { get; set; }
        public bool IsAllowed { get; set; }
    }
}
