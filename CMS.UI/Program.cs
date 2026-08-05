// ================================================================================
// ARCHIVO: CMS.UI/Program.cs
// PROPÓSITO: Configuración y arranque de la interfaz web del Sistema CMS
// DESCRIPCIÓN: BOOTSTRAP desde appsettings.json + Configuración desde BD
//              Autenticación dual: Azure AD + Local (email/password)
// AUTOR: EAMR, BITI SOLUTIONS S.A
// ACTUALIZADO: 2026-02-14 - Agregado middleware de validación de sesión
// ================================================================================

using CMS.UI.Services;
using CMS.UI.Middleware;
using CMS.Data;
using CMS.Data.Services;
using CMS.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ⭐ Configuración de Forwarded Headers (para proxy/reverse proxy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = 2;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ================================================================================
// FASE 1: DETECTAR AMBIENTE Y CARGAR CONFIGURACIÓN DESDE /app/appsettings.json o local
// ================================================================================
var configFile = "/app/appsettings.json";
if (!File.Exists(configFile))
{
    // fallback para desarrollo local: ContentRoot/appsettings.json
    configFile = Path.Combine(builder.Environment.ContentRootPath, "appsettings.json");
}

if (!File.Exists(configFile))
{
    throw new FileNotFoundException($"❌ No se encontró: {configFile}");
}

Console.WriteLine($"✅ Cargando desde: {configFile}");

builder.Configuration.AddJsonFile(configFile, optional: false, reloadOnChange: true);

// ⭐ LEER AMBIENTE
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? builder.Configuration["Environment"]
    ?? "Development";

var isDevelopment = environment == "Development";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  🌍 Ambiente: {environment.PadRight(45)}║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

// ⭐ LEER CONFIGURACIÓN BÁSICA
// El CompanySchema para bootstrap usa "admin" por defecto (esquema principal del sistema)
// La compañía real se selecciona dinámicamente por el usuario en SelectCompany
var companySchema = builder.Configuration["CompanySchema"] ?? "admin";

var bootstrapConnectionString = builder.Configuration[$"ConnectionStrings:{environment}:DefaultConnection"]
    ?? throw new InvalidOperationException($"❌ ConnectionStrings:{environment}:DefaultConnection no encontrado");

var apiBaseUrlFromConfig = builder.Configuration[$"ApiSettings:{environment}:BaseUrl"];

Console.WriteLine($"📂 Bootstrap Schema: {companySchema}");
Console.WriteLine($"🗄️  BD: {(isDevelopment ? "10.0.0.1 (Development)" : "cms-postgres (Production)")}");
Console.WriteLine($"🔗 API: {apiBaseUrlFromConfig}");

// ================================================================================
// FASE 2: CONFIGURAR DbContext
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
builder.Services.AddScoped<CompanyConfigService>();

// ⭐ CONFIGURAR ENCRIPTACIÓN
var encryptionKey = builder.Configuration["Security:EncryptionKey"] 
    ?? "CMS_BITI_SOLUTIONS_DEFAULT_KEY_32"; // ⚠️ Cambiar en producción
builder.Services.AddSingleton(new CMS.Shared.Security.EncryptionService(encryptionKey));

// ================================================================================
// FASE 3: CARGAR CONFIGURACIÓN DESDE BD
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

var apiBaseUrl = apiBaseUrlFromConfig ?? companyConfig.GetAPIBaseUrl();
var environmentName = isDevelopment ? "DEVELOPMENT" : "PRODUCTION";

builder.Services.AddSingleton(companyConfig);

// ================================================================================
// FASE 4: CONFIGURAR AUTENTICACIÓN DUAL (Azure AD + Local)
// ================================================================================

// ⭐ CONFIGURAR AZURE AD (solo si la compañía lo usa y tiene configuración)
var hasAzureAdConfig = !string.IsNullOrEmpty(companyConfig.AZURE_AD_UI_INSTANCE) &&
                       !string.IsNullOrEmpty(companyConfig.AZURE_AD_UI_TENANT_ID) &&
                       !string.IsNullOrEmpty(companyConfig.AZURE_AD_UI_CLIENT_ID);

if (hasAzureAdConfig)
{
    var azureAdConfig = new Dictionary<string, string>
    {
        ["AzureAd:Instance"] = companyConfig.AZURE_AD_UI_INSTANCE!,
        ["AzureAd:TenantId"] = companyConfig.AZURE_AD_UI_TENANT_ID!,
        ["AzureAd:Domain"] = companyConfig.AZURE_AD_UI_DOMAIN ?? "",
        ["AzureAd:ClientId"] = companyConfig.AZURE_AD_UI_CLIENT_ID!,
        ["AzureAd:ClientSecret"] = companyConfig.AZURE_AD_UI_CLIENT_SECRET ?? "",
        ["AzureAd:CallbackPath"] = companyConfig.AZURE_AD_UI_CALL_BACK_PATH ?? "/signin-oidc"
    };

    var inMemoryConfig = new ConfigurationBuilder()
        .AddInMemoryCollection(azureAdConfig!)
        .Build();

    // ⭐ AUTENTICACIÓN CON COOKIES COMO DEFAULT + AZURE AD DISPONIBLE
    // El esquema por defecto es Cookies, Azure AD se invoca explícitamente desde el controlador
    builder.Services
        .AddAuthentication(options =>
        {
            // Cookies es el esquema por defecto (para auth local)
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            // El challenge por defecto también es Cookies (redirige a /Account/SelectCompany)
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddMicrosoftIdentityWebApp(options =>
        {
            inMemoryConfig.GetSection("AzureAd").Bind(options);

            var scopes = companyConfig.API_SCOPES?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? new[] { "api://b231a44d-7e9d-4d9b-8866-9a4b3c5ab5cd/access_as_user" };

            foreach (var scope in scopes)
            {
                options.Scope.Add(scope);
            }

            options.ResponseType = "code";
            options.SaveTokens = true;

            options.Events = new OpenIdConnectEvents
            {
                OnTokenValidated = async context =>
                {
                    var services = context.HttpContext.RequestServices;
                    var syncService = services.GetRequiredService<UserSyncApiService>();
                    var tokenService = services.GetRequiredService<TokenApiService>();
                    var logger = services.GetRequiredService<ILogger<Program>>();

                    var principal = context.Principal;
                    if (principal == null)
                    {
                        logger.LogWarning("⚠️ Principal es null");
                        return;
                    }

                    var oid = principal.FindFirstValue("oid")
                        ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
                        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

                    var upn = principal.FindFirstValue("preferred_username")
                        ?? principal.FindFirstValue(ClaimTypes.Upn)
                        ?? principal.FindFirstValue(ClaimTypes.Email);

                    var email = principal.FindFirstValue(ClaimTypes.Email)
                        ?? principal.FindFirstValue("preferred_username")
                        ?? principal.FindFirstValue("email");

                    var displayName = principal.FindFirstValue("name")
                        ?? principal.Identity?.Name
                        ?? email;

                    logger.LogInformation("🔍 Claims recibidos de Azure AD:");
                    logger.LogInformation("  - OID: {Oid}", oid ?? "(null)");
                    logger.LogInformation("  - UPN: {Upn}", upn ?? "(null)");
                    logger.LogInformation("  - Email: {Email}", email ?? "(null)");
                    logger.LogInformation("  - DisplayName: {DisplayName}", displayName ?? "(null)");

                    // Guardar tipo de autenticación en sesión
                    context.HttpContext.Session.SetString("AuthType", "azure");

                    if (string.IsNullOrEmpty(oid) && string.IsNullOrEmpty(upn))
                    {
                        logger.LogError("❌ No se pudo obtener OID ni UPN del token de Azure AD");
                        return;
                    }

                    var azureUser = new UserSyncApiService.AzureUserInfo
                    {
                        ObjectId = oid,
                        UserPrincipalName = upn,
                        DisplayName = displayName,
                        Email = email
                    };

                    var syncResult = await syncService.SyncAzureUserAsync(azureUser);

                    if (syncResult == null)
                    {
                        logger.LogWarning("⚠️ No se pudo sincronizar usuario");
                        return;
                    }

                    logger.LogInformation("✅ Usuario sincronizado: {User}", syncResult.Username);

                    // Obtener CompanyId de la sesión (ya debe estar guardado de SelectCompany)
                    var companyIdStr = context.HttpContext.Session.GetString("CompanyId");
                    var companyId = int.TryParse(companyIdStr, out var cid) ? cid : 0;

                    if (companyId <= 0)
                    {
                        logger.LogWarning("⚠️ No hay CompanyId en sesión, no se puede generar token con permisos");
                        return;
                    }

                    var tokenResult = await tokenService.GetApiTokenAsync(azureUser, companyId);

                    if (tokenResult?.Success == true && !string.IsNullOrEmpty(tokenResult.Token))
                    {
                        context.HttpContext.Session.SetString("ApiToken", tokenResult.Token);
                        context.HttpContext.Session.SetString("ApiTokenExpiry", tokenResult.ExpiresAt.ToUniversalTime().ToString("O"));

                        logger.LogInformation("✅ JWT obtenido y guardado en sesión. Expira: {Expiry}",
                            tokenResult.ExpiresAt.ToUniversalTime());
                    }
                    else
                    {
                        logger.LogWarning("⚠️ No se pudo obtener JWT del API: {Message}",
                            tokenResult?.Message ?? "Sin respuesta");
                    }
                },

                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(context.Exception, "❌ Error en autenticación de Azure AD");
                    context.HandleResponse();
                    context.Response.Redirect("/Home/Error?statusCode=401");
                    return Task.CompletedTask;
                }
            };
        })
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches();

    // ⭐ CONFIGURAR OPCIONES DE COOKIES (separado porque AddMicrosoftIdentityWebApp ya registra el esquema)
    builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/SelectCompany";
        options.LogoutPath = "/Account/SignOut";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // ⭐ Cookie de sesión (expira al cerrar el navegador)
        options.Cookie.IsEssential = true;
        options.Cookie.MaxAge = null; // Sin MaxAge = cookie de sesión
    });

    Console.WriteLine("✅ Autenticación configurada: Azure AD + Cookies");
}
else
{
    // ⭐ SOLO AUTENTICACIÓN LOCAL (sin Azure AD)
    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.LoginPath = "/Account/SelectCompany";
            options.LogoutPath = "/Account/SignOut";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            // ⭐ Cookie de sesión (expira al cerrar el navegador)
            options.Cookie.IsEssential = true;
            options.Cookie.MaxAge = null; // Sin MaxAge = cookie de sesión
        });

    Console.WriteLine("✅ Autenticación configurada: Solo Cookies (sin Azure AD)");
}

// ================================================================================
// FASE 5: CONFIGURAR SERVICIOS
// ================================================================================

builder.Services.AddControllersWithViews();

// ⭐ CONFIGURAR ANTIFORGERY para evitar problemas con la primera solicitud
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isDevelopment 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
    options.HeaderName = "X-CSRF-TOKEN";
});
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // ⭐ En desarrollo permitir cookies sin HTTPS
    options.Cookie.SecurePolicy = isDevelopment 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
});

builder.Services.AddHttpClient("cmsapi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient("cmsapi-authenticated", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
})
.AddHttpMessageHandler<AuthenticatedApiMessageHandler>();

Console.WriteLine($"✅ HttpClients configurados para JWT");

// ⭐ AGREGAR CACHE EN MEMORIA
builder.Services.AddMemoryCache();

// ⭐ Registrar el handler ANTES de usarlo en AddHttpClient
builder.Services.AddTransient<AuthenticatedApiMessageHandler>();

builder.Services.AddScoped<MenuApiService>();
builder.Services.AddScoped<UserSyncApiService>();
builder.Services.AddScoped<TokenApiService>();
builder.Services.AddScoped<SettingsApiService>();
builder.Services.AddScoped<UsersApiService>();
builder.Services.AddScoped<AdminApiService>();
builder.Services.AddScoped<LocalAuthService>();
builder.Services.AddScoped<CMS.UI.Services.EmailService>();
builder.Services.AddScoped<SystemConfigService>();
builder.Services.AddScoped<UserAuthApiService>();
builder.Services.AddScoped<DashboardApiService>();
builder.Services.AddScoped<UserSettingsApiService>();

// ⭐ Servicios que usan HttpClient CON autenticación JWT automática
builder.Services.AddHttpClient<PermissionsApiService>()
    .AddHttpMessageHandler<AuthenticatedApiMessageHandler>();
builder.Services.AddHttpClient<MenusAdminApiService>()
    .AddHttpMessageHandler<AuthenticatedApiMessageHandler>();

// ⭐ Warehouse module
builder.Services.AddHttpClient<CMS.UI.Controllers.WarehouseController>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ⭐ Localization module (Settings/Localization + Settings/LocalizationType)
builder.Services.AddHttpClient<CMS.UI.Controllers.LocalizationController>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});


builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(
    isDevelopment ? LogLevel.Information : LogLevel.Warning
);

// ================================================================================
// FASE 6: CONSTRUIR Y CONFIGURAR PIPELINE
// ================================================================================
var app = builder.Build();

app.UseForwardedHeaders();

if (isDevelopment)
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// ⭐ VALIDACIÓN DE SESIÓN - Redirige a SelectCompany si la sesión es inválida
app.UseSessionValidation();

// ⭐ CONFIGURAR RUTAS
// Ruta para /Home sin acción (redirige a Index)
app.MapControllerRoute(
    name: "home",
    pattern: "Home",
    defaults: new { controller = "Home", action = "Index" });

// ⭐ Ruta para CompanyAuth (SIN prefijo Admin)
app.MapControllerRoute(
    name: "users-companyauth",
    pattern: "Users/CompanyAuth/{userId}/{companyId}",
    defaults: new { controller = "Users", action = "CompanyAuth" });

// ⭐ Rutas del área Admin (mapean a controladores sin área)
// CompanyAuth - Ruta específica para autorización por compañía (ANTES de admin-users)
app.MapControllerRoute(
    name: "admin-users-companyauth",
    pattern: "Admin/Users/CompanyAuth/{userId}/{companyId}",
    defaults: new { controller = "Users", action = "CompanyAuth" });

// Usuarios - UsersController
app.MapControllerRoute(
    name: "admin-users",
    pattern: "Admin/Users/{action}/{id?}",
    defaults: new { controller = "Users", action = "Index" });

// Roles - SettingsController
app.MapControllerRoute(
    name: "admin-roles",
    pattern: "Admin/Roles/{id?}",
    defaults: new { controller = "Settings", action = "Roles" });

// Menus - SettingsController
app.MapControllerRoute(
    name: "admin-menus",
    pattern: "Admin/Menus/{id?}",
    defaults: new { controller = "Settings", action = "Menus" });

// Audit Trail - AdminController
app.MapControllerRoute(
    name: "admin-audit",
    pattern: "Admin/Audit/{id?}",
    defaults: new { controller = "Admin", action = "Audit" });

// System Logs - AdminController
app.MapControllerRoute(
    name: "admin-logs",
    pattern: "Admin/Logs/{id?}",
    defaults: new { controller = "Admin", action = "Logs" });

// API Keys - AdminController
app.MapControllerRoute(
    name: "admin-apikeys",
    pattern: "Admin/APIKeys/{action}/{id?}",
    defaults: new { controller = "Admin", action = "APIKeys" });

// Job Scheduler - AdminController
app.MapControllerRoute(
    name: "admin-jobs",
    pattern: "Admin/Jobs/{action}/{id?}",
    defaults: new { controller = "Admin", action = "Jobs" });

// Backup & Restore - AdminController
app.MapControllerRoute(
    name: "admin-backup",
    pattern: "Admin/Backup/{action}/{id?}",
    defaults: new { controller = "Admin", action = "Backup" });

// Health Check - AdminController
app.MapControllerRoute(
    name: "admin-health",
    pattern: "Admin/Health/{id?}",
    defaults: new { controller = "Admin", action = "Health" });

// =====================================================================
// RUTAS PARA MÓDULOS DE SEGURIDAD
// =====================================================================

// Roles - RolesController
app.MapControllerRoute(
    name: "roles",
    pattern: "Roles/{action=Index}/{id?}",
    defaults: new { controller = "Roles" });

// Permissions - PermissionsController
app.MapControllerRoute(
    name: "permissions",
    pattern: "Permissions/{action=Index}/{id?}",
    defaults: new { controller = "Permissions" });

// Menus - MenusController
app.MapControllerRoute(
    name: "menus",
    pattern: "Menus/{action=Index}/{id?}",
    defaults: new { controller = "Menus" });

// Admin Dashboard
app.MapControllerRoute(
    name: "admin-dashboard",
    pattern: "Admin/Dashboard",
    defaults: new { controller = "Admin", action = "Dashboard" });

// Warehouse & Distribution
app.MapControllerRoute(
    name: "warehouse",
    pattern: "Warehouse/{action=Warehouses}/{id?}",
    defaults: new { controller = "Warehouse" });

// Electronic Invoice (Facturación Electrónica CR v4.4)
app.MapControllerRoute(
    name: "electronicinvoice",
    pattern: "ElectronicInvoice/{action=Index}/{id?}",
    defaults: new { controller = "ElectronicInvoice" });

// Human Resources
app.MapControllerRoute(
    name: "hr",
    pattern: "HR/{action=Employees}/{id?}",
    defaults: new { controller = "HR" });
app.MapControllerRoute(
    name: "settings-localization-type",
    pattern: "Settings/LocalizationType",
    defaults: new { controller = "Localization", action = "LocalizationType" });

// Settings - Localization
app.MapControllerRoute(
    name: "settings-localization",
    pattern: "Settings/Localization",
    defaults: new { controller = "Localization", action = "Localization" });

// Ruta por defecto
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=SelectCompany}/{id?}");

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  ✅ CMS.UI INICIADA - {environmentName.PadRight(36)}║");
Console.WriteLine($"║  🔐 Auth: Azure AD + Local (email/password)                 ║");
Console.WriteLine($"║  🌐 URLs: https://localhost:5001, http://localhost:5000    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

app.Run();