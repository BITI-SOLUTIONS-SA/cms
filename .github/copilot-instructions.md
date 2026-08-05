# Copilot Instructions

## Project Guidelines
- Proyecto CMS de BITI Solutions S.A - Arquitectura Clean Architecture con capas: CMS.API (Web API REST), CMS.Application (CQRS/MediatR), CMS.Data (Repository/EF Core), CMS.Entities (Domain), CMS.UI (Razor Pages/MVC), CMS.Shared (Infraestructura compartida)
- Stack tecnológico CMS: .NET 9.0 (C# 13), PostgreSQL 15 (Alpine), Kubernetes k3s, Azure AD (Entra ID) para autenticación, Entity Framework Core 9.0 con Npgsql, Docker + Harbor registry privado.
- Puertos CMS: API interno 8080 (externo 5000 dev), UI interno 8081 (externo 3000 dev), PostgreSQL 5432 (NodePort 30432). 
- Servidor: BITISERVER1 @ 147.182.204.86 (DigitalOcean). 
- Dominio: cms.biti-solutions.com. 
- Registry: registry.biti-solutions.com.
- Base de datos CMS: PostgreSQL 15, esquema principal "admin" (ej: admin.menu). Usuario: cmssystem. Credenciales en Kubernetes Secret cms-db-secret.

## ⚠️ Configuración del Proyecto ⚠️
- **Archivos de configuración**: El proyecto usa archivos `appsettings.json` estándar de .NET (NO `connectionstrings.json`).
  - `CMS.API/appsettings.json` y `CMS.API/appsettings.Development.json`
  - `CMS.UI/appsettings.json` y `CMS.UI/appsettings.Development.json`
- **Configuración dinámica desde BD**: Las configuraciones generales del sistema (SMTP, notificaciones, etc.) se almacenan en la tabla `admin.system_config`.
- **Email/SMTP**: La configuración del correo de envío (recuperación de contraseña, notificaciones, verificación de email) se lee desde `admin.system_config` con las claves:
  - `smtp_host`, `smtp_port`, `smtp_username`, `smtp_password`, `smtp_from_email`, `smtp_from_name`, `smtp_enable_ssl`
- **En Kubernetes**: Los appsettings se montan desde ConfigMaps.

## ⚠️ REGLAS CRÍTICAS - NO ROMPER ⚠️

### 🔴 Arquitectura Multi-Base de Datos (Multi-Tenant)
**CRÍTICO**: El sistema usa múltiples bases de datos separadas por compañía.

1. **Base de datos CENTRAL (cms)**:
   - **Nombre BD**: `cms`
   - **Schema**: `admin`
   - **Propósito**: Datos administrativos y de configuración del sistema
   - **Tablas**: `user`, `company`, `role`, `permission`, `menu`, `user_company`, `user_company_role`, `user_company_permission`, `role_permission`, `system_config`, etc.
   - **Connection String**: `DefaultConnection` en appsettings.json
   - **Desarrollo**: `Host=10.0.0.1;Port=5432;Database=cms;Username=cmssystem;Password=SYScmsPOStgres1024`

2. **Bases de datos OPERACIONALES (por compañía)**:
   - **Nombre BD**: Igual al `code` de la compañía (ej: `admin`, `eamr`, `biti`)
   - **Schema**: Igual al `code` de la compañía (ej: `admin`, `eamr`, `biti`)
   - **Propósito**: Datos operacionales específicos de cada compañía
   - **Tablas**: `item` (artículos), `inventory`, `sales`, `purchases`, etc.
   - **Connection String**: Se construye dinámicamente según la compañía activa

3. **Reglas de conexión**:BD Central:  Host=localhost;Database=cms;Username=cmssystem;Password=xxx
BD Compañía: Host=localhost;Database={company_code};Username=cmssystem;Password=xxx
4. **Servicios para acceso multi-BD**:
   - `CompanyDbContextFactory` - Crea DbContext dinámico para la BD de la compañía activa
   - `ICompanyDbContext` - Interface para operaciones en BD de compañía
   - La compañía activa se obtiene de la sesión/JWT (`CompanyId`)

5. **Creación de BD de compañía**:
   - Cuando se crea una nueva compañía, se debe crear automáticamente su BD
   - Script: `CREATE DATABASE {company_code}; CREATE SCHEMA {company_code};`
   - Las migraciones de tablas operacionales se aplican a cada BD de compañía
   - **Ver script**: `CMS.Data/Scripts/004_configure_company_connection_strings.sql`

### 🔴 Flujo de Conexiones (MUY IMPORTANTE)
**El sistema SIEMPRE usa DOS tipos de conexiones según el contexto:**

1. **Conexión INICIAL (Seguridad/Admin)**:
   - **Fuente**: `appsettings.json` → `ConnectionStrings:{Environment}:DefaultConnection`
   - **Base de datos**: `cms` (schema `admin`)
   - **Se usa para**: Login, autenticación, menús, roles, permisos, usuarios, compañías, configuración del sistema
   - **Se inicializa**: Al arrancar la aplicación (Program.cs)

2. **Conexión OPERACIONAL (Por compañía)**:
   - **Fuente**: Tabla `admin.company` → campos `connection_string_development` o `connection_string_production`
   - **Base de datos**: Igual al `COMPANY_SCHEMA` de la compañía (ej: `sinai`, `eamr`)
   - **Se usa para**: Items, inventario, ventas, compras, y TODO lo operacional de cada compañía
   - **Se obtiene**: Dinámicamente según la compañía activa del usuario (de JWT o sesión)
   - **IMPORTANTE**: Si `connection_string_*` está vacío, usa fallback basado en appsettings.json cambiando solo el nombre de la BD

3. **⚠️ REGLA DE ORO - Selección de Connection String Operacional**:
   ┌─────────────────────────────────────────────────────────────────────────┐
   │ REGLA DE ORO PARA CONNECTION STRING DE COMPAÑÍA                         │
   ├─────────────────────────────────────────────────────────────────────────┤
   │                                                                       │
   │ Si appsettings.json → "Environment" = "Development"                      │
   │    → SIEMPRE usar connection_string_development                       │
   │    → Ignorar el valor de is_production en la BD                          │
   │    → Esto permite probar localmente con BD de producción sin afectar    │
   │      a los usuarios que están usando el sistema en producción           │
   │                                                                          │
   │ Si appsettings.json → "Environment" = "Production"                       │
   │    → Usar connection_string según el campo is_production de la compañía │
   │    → is_production = true  → connection_string_production             │
   │    → is_production = false → connection_string_development            │
   │                                                                          │
   │ RESUMEN:                                                              │
   │   useProductionCS = company.is_production && (appEnv != "Development")   │
   │                                                                          │
   └─────────────────────────────────────────────────────────────────────────┘
   - **Implementación**: `CMS.Data/Services/CompanyDbContextFactory.cs` → método `CreateDbContextAsync()`

4. **Configuración de compañías (admin.company)**:
   -- Ejemplo de configuración correcta para compañía SINAI
   UPDATE admin.company 
   SET 
       connection_string_development = 'Host=10.0.0.1;Port=5432;Database=sinai;Username=cmssystem;Password=xxx;Pooling=true',
       connection_string_production = 'Host=cms-postgres;Port=5432;Database=sinai;Username=cmssystem;Password=xxx;Pooling=true',
       is_production = true -- En producción, pero si ejecutas localmente (Environment=Development), usará connection_string_development
   WHERE company_schema = 'sinai';
5. **Flujo de decisión**:
   ┌─────────────────────────────────────────────────────────────┐
   │ Usuario hace login o navega                          │
   ├─────────────────────────────────────────────────────────────┤
   │ ¿Es operación de seguridad/admin?                          │
   │ (login, menús, roles, permisos, usuarios, config)          │
   │  │                                                       │
   │     ├── SÍ → Usar appsettings.json → BD: cms, Schema: admin│
   │  │                                                       │
   │     └── NO → Es operación de compañía                   │
   │           │                                           │
   │              └── Buscar company_schema de compañía activa  │
   │               Aplicar REGLA DE ORO para elegir CS   │
   │            Conectar a BD: {company_schema}     │
   │         Ejemplo: BD: sinai, Schema: sinai      │
   └─────────────────────────────────────────────────────────────┘
6. **Campos críticos en `admin.company`**:
   - `company_schema` (VARCHAR 10) - Nombre del schema Y de la BD operacional
   - `connection_string_development` - Connection string para desarrollo
   - `connection_string_production` - Connection string para producción
   - `is_production` - Indica qué connection string usar

### 🔴🔴🔴 REGLA DE ORO #1 - PERMISOS (CRÍTICO) 🔴🔴🔴
┌─────────────────────────────────────────────────────────────────────────────────┐
│ ⚠️⚠️⚠️ REGLA CRÍTICA - VERIFICACIÓN DE PERMISOS ⚠️⚠️⚠️                         │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                  │
│ Los permisos de un usuario se obtienen ÚNICAMENTE de:                           │
│                                                                                  │
│    ✅ admin.user_company_permission (user_id, company_id, permission_id)        │
│                                                                                  │
│ NUNCA usar admin.role_permission para VERIFICAR si un usuario tiene permiso.   │
│                                                                                  │
│ ❌ INCORRECTO:                                                                   │
│    1. Obtener roles del usuario                                                  │
│    2. Obtener permisos de role_permission                                        │
│    3. Verificar si tiene el permiso                                              │
│                                                                                  │
│ ✅ CORRECTO:                                                                     │
│    1. Consultar user_company_permission WHERE user_id=X AND company_id=Y        │
│    2. Filtrar por is_allowed=true (permitidos) y is_allowed=false (denegados)   │
│    3. Permisos efectivos = permitidos - denegados                               │
│                                                                                  │
│ USO PERMITIDO de admin.role_permission:                                          │
│    - SOLO para PRECARGAR permisos cuando se asigna un rol a un usuario          │
│    - Al asignar rol, COPIAR los permisos del rol a user_company_permission      │
│                                                                                  │
│ IMPLEMENTACIÓN:                                                                  │
│    - PermissionService.GetUserPermissionsForCompanyAsync() → SOLO usa UCP       │
│    - AuthorizationService.GetEffectivePermissionsAsync() → SOLO usa UCP         │
│    - AuthorizationService.AssignRoleToUserInCompanyAsync() → COPIA permisos     │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
1. **Tablas de seguridad ACTUALES**:
   - `admin.user_company_role` - Roles de un usuario EN UNA compañía (solo informativo/descriptivo)
   - `admin.user_company_permission` - **⭐ ÚNICA fuente de permisos efectivos**
   - `admin.role_permission` - Template de permisos para PRECARGAR al asignar rol (NO para verificación)
   - `admin.company.is_admin_company` - Indica si es la compañía de administración del sistema

2. **Tablas ELIMINADAS (ya no existen)**:
   - `admin.user.id_role` - Campo eliminado de la tabla user
   - `admin.user_role` - Tabla eliminada
   - `admin.user_permission` - Tabla eliminada

3. **Jerarquía de evaluación de permisos (ACTUALIZADA)**:1. Consultar admin.user_company_permission para (userId, companyId)
2. Separar en: permitidos (is_allowed=true) y denegados (is_allowed=false)
3. Permisos efectivos = permitidos - denegados
4. DENEGACIONES SIEMPRE GANAN sobre permisos permitidos

⚠️ NO consultar admin.role_permission para verificación de permisos
4. **Flujo al asignar un rol a un usuario**:1. Insertar registro en user_company_role (userId, companyId, roleId)
2. Consultar role_permission WHERE roleId = X
3. Por cada permiso del rol, insertar en user_company_permission:
   - Si no existe: crear con is_allowed=true
   - Si ya existe: NO modificar (respetar configuración existente)
5. **Servicios de autorización**:
   - `PermissionService.GetUserPermissionsForCompanyAsync(userId, companyId)` - Obtiene permisos efectivos (SOLO de user_company_permission)
   - `PermissionService.GetUserRolesForCompanyAsync(userId, companyId)` - Obtiene roles en una compañía (informativo)
   - `AuthorizationService.GetEffectivePermissionsAsync()` - Permisos efectivos (SOLO de user_company_permission)
   - `AuthorizationService.AssignRoleToUserInCompanyAsync()` - Asigna rol Y COPIA permisos
   - `CompanyConfigService.GetVisibleCompaniesForUserAsync(userId)` - Obtiene compañías visibles según permisos

6. **JWT**: El token incluye permisos y roles para la compañía activa del usuario. Los permisos del JWT están en claims con type "permission" (singular, no "permissions"). Para leer permisos del JWT en la UI, usar `JwtSecurityTokenHandler` para decodificar el token desde la sesión ya que `User.Claims` de la cookie de autenticación NO contiene los permisos del JWT. Tanto Azure AD como login local requieren `CompanyId` para generar el token.

7. **Acceso a Compañías (Multi-tenant)**:
   - Campo `is_admin_company` en `admin.company` marca la compañía de administración
   - Permiso `System.ViewAllCompanies` permite ver TODAS las compañías del sistema
   - **REGLA**: Solo usuarios con `System.ViewAllCompanies` en una compañía donde `is_admin_company=true` pueden ver todas las compañías
   - Los demás usuarios solo ven las compañías asignadas en `admin.user_company`

### Sesión y Antiforgery Token (CMS.UI)
1. **NUNCA usar `HttpContext.Session.Clear()` en métodos POST con `[ValidateAntiForgeryToken]`** - Esto invalida el token y causa error 500.
2. **SelectCompany POST NO usa [ValidateAntiForgeryToken]** - Es la primera página que ve el usuario sin cookies, el token no puede validarse. Es público y seguro.
3. **En SelectCompany GET**: Solo limpiar sesión si `forceLogout=true`. Siempre inicializar la sesión con un valor dummy para asegurar que la cookie exista.
4. **Configuración de cookies de sesión**: Usar `SameSiteMode.Lax` y `SecurePolicy.SameAsRequest` en desarrollo.

### Rutas (CMS.UI)
1. **Links de navegación**: Siempre usar tag helpers (`asp-controller`, `asp-action`) en lugar de URLs hardcodeadas.
2. **Ruta para /Home**: Debe existir una ruta específica que mapee `/Home` a `Home/Index`.

### Flujo de Creación de Usuarios
1. Al crear un usuario, se genera una **contraseña temporal** (no la del formulario) válida por 30 minutos.
2. Se envía un **email de verificación** con la contraseña temporal y un link.
3. El usuario debe verificar su email y cambiar la contraseña temporal.
4. Al completar la verificación, se envía un **email de bienvenida**.
5. Campo `is_email_verified` indica si el email está verificado.
6. **Usuario no puede iniciar sesión si email no está verificado.**

### 🎨 Estilos UI/UX (CMS.UI)
**IMPORTANTE**: El tema del CMS es OSCURO. Al crear nuevas vistas o componentes:

1. **Colores de texto legibles**:
   - Texto principal: usar `text-light`, `text-white` o colores claros (`#ffffff`, `#e0e0e0`)
   - Texto secundario: usar `text-muted` que tiene buen contraste en tema oscuro
   - **NUNCA usar** colores oscuros (`text-dark`, `text-secondary` oscuro, `#333`) sobre fondos oscuros

2. **Tarjetas y contenedores**:
   - Usar clases Bootstrap con contraste: `card-text text-light`, `text-white-50`
   - Descripciones en tarjetas: usar `text-light` o `text-white` con opacidad
   - Ejemplo: `<p class="card-text small text-light">Descripción</p>`

3. **Validación visual**:
   - Siempre verificar que el texto sea legible sobre el fondo oscuro
   - Contraste mínimo recomendado: texto claro (#fff) sobre fondos oscuros (#1a1a2e, #16213e)

4. **⚠️ TEXTOS DE AYUDA (help text / hint text) — REGLA CRÍTICA**:
   - **TODOS los `<small>`, `.form-text`, texto de ayuda debajo de campos**, deben tener color explícito claro.
   - **NUNCA** dejar textos de ayuda con la clase `text-muted` sola en formularios de fondo oscuro — resultan casi invisibles.
   - **SIEMPRE** agregar en el bloque `<style>` de cada nueva vista: small, .form-text, small.text-muted, .text-muted small { color: #cbd5e1 !important; }   - Esto aplica a TODAS las vistas nuevas sin excepción.

5. **⚠️ MODALES en páginas con sidebar — REGLA CRÍTICA**:
   - Los modales de Bootstrap DEBEN ser movidos al final del `<body>` para evitar problemas de z-index con el sidebar fijo.
   - **SIEMPRE** incluir en el `@section Scripts` de cada vista con modales: document.addEventListener('DOMContentLoaded', function () {
     ['miModal1', 'miModal2'].forEach(function (id) {
         var el = document.getElementById(id);
         if (el) document.body.appendChild(el);
     });
 });   - Sin esto el sidebar (`z-index: 1000`) puede quedar por encima del modal y bloquear la interacción.

## Branching and Commit Conventions
- Convenciones de branches CMS: main (producción), develop (desarrollo), feature/[nombre], fix/[nombre], hotfix/[nombre]. 
- Commits: Conventional Commits (feat:, fix:, docs:, test:, refactor:, chore:, ci:, perf:)

## Timezone and Working Hours
- Timezone BITI Solutions: UTC-6 (Costa Rica - CST).
- Horario: 8:00 AM - 12:00 AM.
- Owner: @BITI-SOLUTIONS (desarrollador único, full stack + DevOps).

## Estructura del Menú Principal (admin.menu)

Los menús principales tienen `id_parent = 0`. Los submenús tienen `id_parent` apuntando al menú padre.

### Menús de Nivel 0 (Principales):
| ID | Nombre | URL | Icono | Permiso |
|----|--------|-----|-------|---------|
| 1 | Dashboard | /Dashboard | bi-speedometer2 | Menu.View |
| 2 | CRM | /CRM | bi-people | Menu.View |
| 3 | Sales | /Sales | bi-cart4 | Menu.View |
| 4 | Billing | /Billing | bi-receipt | Menu.View |
| 5 | POS | /POS | bi-shop | Menu.View |
| 6 | Purchasing | /Purchasing | bi-bag-check | Menu.View |
| 7 | Inventory | /Inventory | bi-box-seam | Menu.View |
| 8 | Warehouse & Distribution | /Warehouse | bi-truck | Menu.View |
| 9 | Manufacturing | /Manufacturing | bi-gear-wide-connected | Menu.View |
| 10 | Maintenance | /Maintenance | bi-tools | Menu.View |
| 11 | Accounting | /Accounting | bi-calculator | Menu.View |
| 12 | Finance | /Finance | bi-bank | Menu.View |
| 13 | Human Resources | /HR | bi-people | Menu.View |
| 14 | Employee Portal | /EmployeePortal | bi-person-badge | Menu.View |
| 15 | Projects | /Projects | bi-kanban | Menu.View |
| 16 | Reports & BI | /Reports | bi-bar-chart | Menu.View |
| 17 | E-Invoicing | /EInvoicing | bi-receipt-cutoff | Menu.View |
| 18 | Settings | /Settings | bi-sliders | Menu.View |
| 19 | Administration | /Admin | bi-shield-lock | Menu.View |

### Submenús de Administration (id_parent = 19):
| ID | Nombre | URL | Orden | Permiso |
|----|--------|-----|-------|---------|
| 170 | Dashboard | /Admin/Dashboard | 0 | Admin.Dashboard.View |
| 151 | Users | /Admin/Users | 1 | Admin.Users.View |
| 152 | Roles & Permissions | /Roles | 2 | Admin.Roles.View |
| 153 | Menu Management | /Menus | 3 | Admin.Menus.Edit |
| 154 | Audit Trail | /Admin/Audit | 4 | Admin.Audit.View |
| 155 | System Logs | /Admin/Logs | 5 | Admin.Logs.View |
| 156 | API Keys | /Admin/APIKeys | 6 | Admin.APIKeys.Edit |
| 157 | Job Scheduler | /Admin/Jobs | 7 | Admin.Jobs.View |
| 158 | Backup & Restore | /Admin/Backup | 8 | Admin.Backup.Execute |
| 159 | Health Check | /Admin/Health | 9 | Admin.Health.View |
| 167 | Permisos | /Permissions | 10 | Admin.Permissions.View |
| 168 | Menús | /Menus | 11 | Admin.Menus.View |
| 173 | System Config | /Admin/System | 12 | System.ViewAllCompanies |
| 185 | Company Management | /Companies | 13 | Admin.Companies.View |

### Rutas UI importantes:
- `/Permissions` - Gestión de permisos (CRUD)
- `/Companies` - Gestión de compañías (CRUD)
- `/Menus` - Gestión de menús (CRUD)
- `/Roles` - Gestión de roles
- `/Users` - Gestión de usuarios
- `/Admin/Dashboard` - Dashboard de administración
- `/Users/{id}/CompanyAuth/{companyId}` - Autorización de usuario por compañía

┌─────────────────────────────────────────────────────────────┐
│                    PostgreSQL Server                         │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  BD: cms (Schema: admin)         BD: sinai (Schema: sinai)  │
│  ┌─────────────────────────┐     ┌─────────────────────────┐│
│  │ DATOS ADMINISTRATIVOS   │     │ DATOS OPERACIONALES     ││
│  │                         │     │                         ││
│  │ - admin.user            │     │ - sinai.item            ││
│  │ - admin.company         │     │ - (futuro: inventory)   ││
│  │ - admin.role            │     │ - (futuro: sales)       ││
│  │ - admin.permission      │     │                         ││
│  │ - admin.menu            │     │                         ││
│  │ - etc.                  │     │                         ││
│  └─────────────────────────┘     └─────────────────────────┘│
│                                                              │
└─────────────────────────────────────────────────────────────┘

## 🗄️ Estándares de Scripts SQL (CMS.Data/Scripts)

### Estructura obligatoria de todo script de creación de tabla operacional

Todos los scripts de creación de tablas en bases de datos de compañía **DEBEN** seguir esta estructura exacta. La tabla `sinai.warehouse` es el ejemplo de referencia canónico (`CMS.Data/Scripts/005_create_warehouse_table.sql`).

### Reglas generales

1. **Nombre de archivo**: `NNN_descripcion_snake_case.sql` (ej: `005_create_warehouse_table.sql`)
2. **Ubicación**: `CMS.Data/Scripts/`
3. **Schema**: Siempre usar el schema de la compañía como prefijo (ej: `{schema}.tabla`). En ejemplos concretos usar `sinai`.
4. **Ejecución**: El script debe incluir instrucciones de ejecución con `psql` en el encabezado.
5. **Borrado seguro**: Siempre comenzar con `DROP TABLE IF EXISTS {schema}.tabla CASCADE;`

### Secciones obligatorias (en este orden)1.  Bloque START  (comentario visual delimitador de = en 3 líneas)
2.  Encabezado    (SCRIPT, PROPÓSITO, DESCRIPCIÓN, EJECUCIÓN, AUTOR, CREADO)
3.  -- TABLA: nombre
4.  DROP TABLE IF EXISTS … CASCADE
5.  CREATE TABLE con columnas agrupadas por categoría (ver patrón)
6.  -- ÍNDICES
7.  -- COMENTARIOS  (COMMENT ON TABLE / COLUMN)
8.  -- PERMISOS     (GRANT + SEQUENCE)
9.  Función trigger  tr_{tabla}_update_fn()
10. Trigger BEFORE UPDATE  tr_{tabla}_update
11. Bloque END   (comentario visual delimitador de = en 3 líneas)
### Columnas de auditoría obligatorias (SIEMPRE las últimas columnas antes de los Constraints)createdate      TIMESTAMP    NOT NULL DEFAULT now(),
record_date     TIMESTAMP    NOT NULL DEFAULT now(),
created_by      VARCHAR(30)  NOT NULL DEFAULT current_user,
updated_by      VARCHAR(30)  NOT NULL DEFAULT current_user,
rowpointer      UUID         NOT NULL DEFAULT gen_random_uuid(),
### Constraints obligatoriosCONSTRAINT {tabla}_pkey               PRIMARY KEY (id_{tabla}),
CONSTRAINT rpix_{schema}_{tabla}      UNIQUE (rowpointer),
CONSTRAINT uq_{schema}_{tabla}_code   UNIQUE (code),   -- si la tabla tiene campo code
### Nomenclatura de índices-- Índice único sobre code:
CREATE UNIQUE INDEX IF NOT EXISTS uix_{schema}_{tabla}_code    ON {schema}.{tabla}(code);
-- Índices adicionales:
CREATE INDEX        IF NOT EXISTS ix_{schema}_{tabla}_{campo}  ON {schema}.{tabla}({campo});
### Permisos estándarGRANT SELECT, INSERT, UPDATE, DELETE ON {schema}.{tabla} TO PUBLIC;
GRANT ALL ON {schema}.{tabla} TO cmssystem;
GRANT USAGE, SELECT ON SEQUENCE {schema}.{tabla}_id_{tabla}_seq TO cmssystem;
### Trigger estándar de auditoríaCREATE OR REPLACE FUNCTION {schema}.tr_{tabla}_update_fn()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_by  := current_user;
    NEW.record_date := now();
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_{tabla}_update
BEFORE UPDATE ON {schema}.{tabla}
FOR EACH ROW EXECUTE FUNCTION {schema}.tr_{tabla}_update_fn();
### Relaciones cross-database (FK lógicas — NO declarar FK real)

Cuando una tabla operacional referencia la BD central (`cms.admin.*`), NO se declara FK real. Se documenta con un comentario estándar:-- RELACIÓN LÓGICA CROSS-DB: campo_id referencia cms.admin.tabla.id
--    No se puede declarar FK real porque esta tabla está en la BD de la compañía
--    y admin.tabla está en la BD central (cms). La integridad se mantiene a nivel
--    de aplicación en el Service / Controller correspondiente.
campo_id   INTEGER,
### Patrón de columnas agrupadas por categoríaCREATE TABLE {schema}.{tabla} (
    -- PK + campos base
    id_{tabla}       SERIAL        NOT NULL,
    code             VARCHAR(30)   NOT NULL,
    name             VARCHAR(200)  NOT NULL,
    description      VARCHAR(1000),

    -- Clasificación / Tipo
    {tipo_campo}     VARCHAR(30)   NOT NULL DEFAULT '{valor}',

    -- Comportamiento / Flags operacionales
    is_default       BOOLEAN       NOT NULL DEFAULT FALSE,
    allows_x         BOOLEAN       NOT NULL DEFAULT FALSE,

    -- Capacidad / Medidas  (si aplica)
    max_capacity     DECIMAL(18,4),
    capacity_unit    VARCHAR(20),

    -- Relaciones: FK reales a tablas del MISMO schema
    id_{rel_tabla}   INTEGER       REFERENCES {schema}.{rel_tabla}(id_{rel_tabla}) ON DELETE RESTRICT,

    -- Relaciones lógicas cross-DB (sin FK real — ver regla arriba)
    cross_db_id      INTEGER,

    -- Estado
    is_active        BOOLEAN       NOT NULL DEFAULT TRUE,
    notes            VARCHAR(2000),

    -- Auditoría (SIEMPRE AL FINAL, antes de Constraints)
    createdate       TIMESTAMP     NOT NULL DEFAULT now(),
    record_date      TIMESTAMP     NOT NULL DEFAULT now(),
    created_by       VARCHAR(30)   NOT NULL DEFAULT current_user,
    updated_by       VARCHAR(30)   NOT NULL DEFAULT current_user,
    rowpointer       UUID          NOT NULL DEFAULT gen_random_uuid(),

    -- Constraints
    CONSTRAINT {tabla}_pkey              PRIMARY KEY (id_{tabla}),
    CONSTRAINT rpix_{schema}_{tabla}     UNIQUE (rowpointer),
    CONSTRAINT uq_{schema}_{tabla}_code  UNIQUE (code)
);
## Credenciales PostgreSQL CMS
- Usuario: postgres
- Password: POStgres2026
- Puerto: 5432
- Host: 10.0.0.1
- Base de datos operacional: sinai (o según compañía)
- Base de datos central: cms

SIEMPRE usar estas credenciales para ejecutar scripts SQL o comandos psql en el proyecto CMS.

## Datos de Factura
- ProveedorSistemas: 2100042005
- Emisor: BITI SOLUTIONS S.A, cédula jurídica: 3101896397
- Código de Actividad Emisor: formato '6202.0'
- Fecha de Emisión: formato 'yyyy-MM-ddTHH:mm:ss.000' sin offset
- SignaturePolicy: Identifier='https://atv.hacienda.go.cr/ATV/ComprobanteElectronico/docs/esquemas/2024/v4.4/Resoluci%C3%B3n_General_sobre_disposiciones_t%C3%A9cnicas_comprobantes_electr%C3%B3nicos_para_efectos_tributarios.pdf'
- SigPolicyHash (HEX): '0D6C629F5C5639E23C3AE5905DACE1E158CB5806822C003DE787A6EC3321D21F'
- La firma usa formato FirmaXadesNet (confirmado correcto).

## HITO LOGRADO
- Factura electrónica CR v4.4 ACEPTADA por Hacienda sandbox (ind-estado: aceptado) end-to-end con el sistema HACIENDA-CORE del CMS. Cadena completa funcional: Vault AES-256 -> Clave 50 díg -> XML v4.4 -> firma XAdES-EPES (FirmaXadesNetCore) -> OAuth realm rut-stag -> POST api-sandbox.comprobanteselectronicos.go.cr/recepcion/v1 -> aceptado. El -37 (ubicación no concuerda) es advertencia informativa, no rechazo.
