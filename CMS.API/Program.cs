// ================================================================================
// ARCHIVO: CMS.API/Program.cs
// PROPÓSITO: Configuración y arranque de la API REST del Sistema CMS
// DESCRIPCIÓN: BOOTSTRAP desde appsettings.json + Configuración desde BD
// AUTOR: EAMR, BITI SOLUTIONS S.A
// ACTUALIZADO: 2026-02-11
// ================================================================================

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using CMS.API.Middleware;
using CMS.Data;
using CMS.Data.Services;
using CMS.Entities;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ⭐ CONFIGURAR FORWARDED HEADERS PARA TRAEFIK
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = 2;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ================================================================================
// FASE 1: DEBUG Y DETECTAR AMBIENTE Y CARGAR CONFIGURACIÓN
// ================================================================================
Console.WriteLine($"✅ Cargando desde: /app/appsettings.json");

try
{
    var jsonText = File.ReadAllText("/app/appsettings.json");
    Console.WriteLine("=== DEBUG: Contenido de /app/appsettings.json ===");
    Console.WriteLine(jsonText);
    Console.WriteLine("===============================================");
}
catch (Exception e)
{
    Console.WriteLine($"ERROR leyendo /app/appsettings.json: {e.Message}");
}

// ⭐ LEER AMBIENTE
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? builder.Configuration["Environment"]
    ?? "Development";

var isDevelopment = environment == "Development";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  🌍 Ambiente: {environment.PadRight(45)}║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

// ⭐ LEER CONFIGURACIÓN SEGÚN AMBIENTE
// El CompanySchema para bootstrap usa "admin" por defecto (esquema principal del sistema)
// La compañía real se determina por cada request según el JWT del usuario
var companySchema = builder.Configuration["CompanySchema"] ?? "admin";

// 🚩 Prints de debug para identificar keys reales y valores del config
Console.WriteLine($"➡️  Environment variable for ConnectionStrings lookup: [{environment}]");
Console.WriteLine("==== Todas las keys configuradas (debug) ====");
foreach (var kv in builder.Configuration.AsEnumerable())
{
    Console.WriteLine($"{kv.Key} = {kv.Value}");
}
Console.WriteLine("=============================================");

// ⭐ LECTURA ROBUSTA DEL CONNECTION STRING ANIDADO
var defaultConnectionSection = builder.Configuration
    .GetSection("ConnectionStrings")
    .GetSection(environment)
    .GetValue<string>("DefaultConnection");

if (string.IsNullOrEmpty(defaultConnectionSection))
{
    var alternative = builder.Configuration[$"ConnectionStrings:{environment}:DefaultConnection"];
    Console.WriteLine($"🔍 Intentando alternativa: ConnectionStrings:{environment}:DefaultConnection = [{alternative}]");
    defaultConnectionSection = alternative;
}

if (string.IsNullOrEmpty(defaultConnectionSection))
    throw new InvalidOperationException($"❌ ConnectionStrings:{environment}:DefaultConnection no encontrado");

var bootstrapConnectionString = defaultConnectionSection;

// ⭐ LEER JWT SECRET KEY
var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"]
    ?? throw new InvalidOperationException("❌ JwtSettings:SecretKey no configurada");

var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "CMS.API";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "CMS.UI";

// ⭐ CLAVE DE ENCRIPTACIÓN (para datos sensibles en system_config)
var encryptionKey = builder.Configuration["Security:EncryptionKey"] 
    ?? "CMS_BITI_SOLUTIONS_DEFAULT_KEY_32"; // ⚠️ Cambiar en producción

Console.WriteLine($"📂 Schema: {companySchema}");
Console.WriteLine($"🗄️  BD: {(isDevelopment ? "10.0.0.1 (Development)" : "cms-postgres (Production)")}");
Console.WriteLine($"🔑 JWT: Configurado ({jwtSecretKey.Substring(0, 10)}...)");
Console.WriteLine($"🔒 Encriptación: Configurada");

// ================================================================================
// FASE 2: CONFIGURAR DbContext (PostgreSQL)
// ================================================================================
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(bootstrapConnectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(60);
    });

    if (isDevelopment)
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<CompanyConfigService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<AuthorizationService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton(new CMS.Shared.Security.EncryptionService(encryptionKey));
builder.Services.AddScoped<SystemConfigService>();
builder.Services.AddScoped<GlobalParameterService>();
builder.Services.AddScoped<CMS.Data.Services.IEmailService, CMS.Data.Services.EmailService>();

// ================================================================================
// SERVICIOS MULTI-BD (Bases de datos por compañía)
// ================================================================================
builder.Services.AddScoped<ICompanyDbContextFactory, CompanyDbContextFactory>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IStockTransferService, StockTransferService>();
builder.Services.AddScoped<IDistributionRouteService, DistributionRouteService>();
builder.Services.AddScoped<ILocationTypeService, LocationTypeService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IInventoryTransactionService, InventoryTransactionService>();
builder.Services.AddScoped<ICompanyConsecutiveService, CompanyConsecutiveService>();
builder.Services.AddScoped<IChartOfAccountsService, ChartOfAccountsService>();
builder.Services.AddScoped<ICostCenterService, CostCenterService>();
builder.Services.AddScoped<IJournalEntryService, JournalEntryService>();
builder.Services.AddScoped<IJournalEntryCancelReasonService, JournalEntryCancelReasonService>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<CMS.Data.Services.Interfaces.IConsecutiveService, CMS.Data.Services.ConsecutiveService>();
builder.Services.AddScoped<ICustomerService, CustomerService>(); // ⭐ Nuevo servicio Customer

// ================================================================================
// FACTURACIÓN ELECTRÓNICA CR v4.4 (HACIENDA-CORE)
// ================================================================================
builder.Services.AddScoped<CMS.Data.Services.EInvoice.IElectronicDocumentTypeCatalogService, CMS.Data.Services.EInvoice.ElectronicDocumentTypeCatalogService>();
builder.Services.AddSingleton<CMS.Data.Services.EInvoice.IEInvoiceVaultService, CMS.Data.Services.EInvoice.EInvoiceVaultService>();
builder.Services.AddScoped<CMS.Data.Services.EInvoice.IClaveNumericaGenerator, CMS.Data.Services.EInvoice.ClaveNumericaGenerator>();
builder.Services.AddScoped<CMS.Data.Services.EInvoice.IElectronicDocumentXmlBuilder, CMS.Data.Services.EInvoice.ElectronicDocumentXmlBuilder>();
builder.Services.AddScoped<CMS.Data.Services.EInvoice.IXadesSignatureService, CMS.Data.Services.EInvoice.XadesSignatureService>();
builder.Services.AddScoped<CMS.Data.Services.EInvoice.IHaciendaAuthService, CMS.Data.Services.EInvoice.HaciendaAuthService>();
builder.Services.AddScoped<CMS.Data.Services.EInvoice.IHaciendaApiClient, CMS.Data.Services.EInvoice.HaciendaApiClient>();
builder.Services.AddScoped<CMS.Data.Services.EInvoice.IElectronicDocumentService, CMS.Data.Services.EInvoice.ElectronicDocumentService>();
builder.Services.AddScoped<CMS.Data.Services.EInvoice.IEInvoicePdfService, CMS.Data.Services.EInvoice.EInvoicePdfService>();
builder.Services.AddHttpClient("hacienda-idp");
builder.Services.AddHttpClient("hacienda-api");
builder.Services.AddHostedService<CMS.Data.Services.EInvoice.EInvoiceRetryWorker>();

// ================================================================================
// FASE 3: CARGAR CONFIGURACIÓN DESDE [ADMIN].[COMPANY]
// ================================================================================
var serviceProvider = builder.Services.BuildServiceProvider();
using var scope = serviceProvider.CreateScope();
var configService = scope.ServiceProvider.GetRequiredService<CompanyConfigService>();

Company companyConfig;
try
{
    companyConfig = await configService.GetCompanyConfigAsync(companySchema);
}
catch (Exception ex)
{
    throw new InvalidOperationException($"❌ Error cargando configuración: {ex.Message}", ex);
}

Console.WriteLine($"✅ Compañía: {companyConfig.COMPANY_NAME}");

var environmentName = isDevelopment ? "DEVELOPMENT" : "PRODUCTION";

builder.Services.AddSingleton(companyConfig);

// ================================================================================
// CONFIGURACIÓN DE SERVICIOS
// ================================================================================

// 1. AUTENTICACIÓN - JWT PROPIO (SIN AZURE AD)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // ⭐ Logs para debugging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"❌ JWT Auth Failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var userId = context.Principal?.FindFirst("userId")?.Value;
                Console.WriteLine($"✅ JWT Validated: userId={userId}");
                return Task.CompletedTask;
            }
        };
    });

// 2. AUTORIZACIÓN CON PERMISOS DINÁMICOS
builder.Services.AddSingleton<IAuthorizationHandler, CMS.API.Authorization.PermissionAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    // Definir políticas dinámicas basadas en permisos
    // Cada política valida que el usuario tenga el permiso correspondiente en su JWT

    // Global Parameters
    options.AddPolicy("Settings.GlobalParameters.View", policy =>
        policy.Requirements.Add(new CMS.API.Authorization.PermissionRequirement("Settings.GlobalParameters.View")));

    options.AddPolicy("Settings.GlobalParameters.Edit", policy =>
        policy.Requirements.Add(new CMS.API.Authorization.PermissionRequirement("Settings.GlobalParameters.Edit")));

    // TODO: Agregar más políticas según sea necesario
    // Ejemplo:
    // options.AddPolicy("Admin.Users.View", policy =>
    //     policy.Requirements.Add(new PermissionRequirement("Admin.Users.View")));
});

// 3. CONTROLLERS Y JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = isDevelopment;
    });

// 4. SWAGGER
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Usa el nombre completo del tipo como schemaId para evitar colisiones
    // cuando existen DTOs con el mismo nombre corto en distintos controladores/namespaces.
    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = $"CMS API - {companyConfig.COMPANY_NAME}",
        Version = "v1",
        Description = $"API REST del CMS ({environmentName})\n\n" +
                      "**Autenticación:**\n" +
                      "1. Login en UI con Azure AD\n" +
                      "2. POST /api/auth/token con datos de Azure para obtener JWT\n" +
                      "3. Usar JWT en header Authorization: Bearer {token}\n\n" +
                      "**Endpoints públicos (sin auth):**\n" +
                      "- POST /api/auth/token\n" +
                      "- POST /api/auth/sync-user\n" +
                      "- GET /health"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT generado por POST /api/auth/token.\n\n" +
                      "Ejemplo: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\""
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 5. CORS
if (isDevelopment)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        });
    });
}
else
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ProductionCors", policy =>
        {
            policy.WithOrigins("https://cms.biti-solutions.com")
                  .AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        });
    });
}

// ================================================================================
// PIPELINE
// ================================================================================
var app = builder.Build();

// 👇 AGREGA ESTE PATHBASE para publicar TODO bajo /api
app.UsePathBase("/api");

app.UseForwardedHeaders();

if (isDevelopment)
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1");
        c.RoutePrefix = "swagger";
    });
    app.UseCors("AllowAll");
    Console.WriteLine("📖 Swagger: /swagger");
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
    app.UseCors("ProductionCors");
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<AuditUserMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => new
{
    Status = "Healthy",
    Company = companyConfig.COMPANY_NAME,
    Schema = companyConfig.COMPANY_SCHEMA,
    Environment = environmentName,
    Timestamp = DateTime.UtcNow
}).WithTags("Health").AllowAnonymous();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  ✅ CMS.API INICIADA - {environmentName.PadRight(35)}║");
Console.WriteLine($"║  🔐 Auth: JWT PROPIO (sin Azure AD)                         ║");
Console.WriteLine($"║  🌐 URLs: {string.Join(", ", app.Urls).PadRight(46)}║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

app.Run();