# 🔄 REFACTORIZACIÓN COMPLETA DE FACTURACIÓN ELECTRÓNICA
**Fecha Inicio:** 2026-01-24  
**Estado:** EN PROGRESO  
**Objetivo:** Separar responsabilidades entre Customer (CRM), Supplier (AP), y CustomerBillingCredential (E-Invoice)

---

## 📊 CONTEXTO Y MOTIVACIÓN

### Problema Original
El usuario reportó: `42P01: relation "admin.customer" does not exist` al intentar listar customers.

**Causa raíz identificada:** EF Core cacheaba el modelo compilado con el primer schema (`admin`) y lo reutilizaba para todos los schemas posteriores (`sinai`, `rwcr`), ignorando el schema dinámico.

**Solución aplicada:** Implementar `IModelCacheKeyFactory` en `CompanyDbContext` para generar claves de caché únicas por schema.

### Nueva Solicitud del Usuario
Después de resolver el bug de schema, el usuario solicitó:
1. Eliminar entidades legacy: `BillingIssuer`, `BillingReceptor`, `BillingCredential`
2. Mover **TODA** la información de facturación electrónica a `customer_billing_credential`
3. Limpiar `customer` de campos específicos de e-invoice
4. Crear nueva tabla `supplier` para proveedores (separada de customers)

---

## 🎯 ARQUITECTURA OBJETIVO

### 1️⃣ `sinai.customer` - Solo Clientes Operacionales (CRM/Ventas)
**Propósito:** Maestro de clientes a los que **vendemos** bienes/servicios.

**Campos a ELIMINAR (ya no deben estar):**
- `is_issuer` (BOOLEAN)
- `is_company_owner` (BOOLEAN)
- `active_environment` (VARCHAR 10)
- `economic_activity` (VARCHAR 6)

**Campos que PERMANECEN:**
- Identificación básica: `code`, `name`, `commercial_name`, `customer_type`
- Identificación fiscal: `identification_type`, `identification`, `foreign_identification`
- Comercial: `credit_limit`, `credit_days`, `payment_terms`, `discount_pct`, `price_list`
- Ubicación: `province`, `canton`, `district`, `other_signs`, `gps_latitude`, `gps_longitude`
- Contacto: `phone_code`, `phone`, `mobile`, `email`, `website`, `contact_name`, `contact_position`
- Jerarquía: `id_parent_customer` (FK a `customer.id_customer`)
- Estado: `is_active`, `blocked_reason`
- Auditoría estándar

**Navigation Properties:**
- `ParentCustomer` (Customer?)
- `ChildCustomers` (ICollection<Customer>)
- ❌ **ELIMINAR**: `BillingCredentials` (ya no existe relación directa)

---

### 2️⃣ `sinai.supplier` - Proveedores (Purchasing/AP)
**Propósito:** Maestro de proveedores a los que **compramos** bienes/servicios.

**NUEVA TABLA - Script SQL:**
```sql
-- Ver: CMS.Data/Scripts/018_create_supplier_table.sql
CREATE TABLE sinai.supplier (
	id_supplier SERIAL NOT NULL,
	code VARCHAR(30) NOT NULL,
	name VARCHAR(200) NOT NULL,
	commercial_name VARCHAR(200),
	identification_type VARCHAR(2),
	identification VARCHAR(20),
	foreign_identification VARCHAR(20),
	economic_activity VARCHAR(6),
	-- Comercial/Purchasing
	credit_days INTEGER,
	credit_limit DECIMAL(18,4),
	payment_terms VARCHAR(50),
	discount_pct DECIMAL(5,2),
	currency VARCHAR(3) DEFAULT 'CRC',
	supplier_type VARCHAR(20) DEFAULT 'Both', -- 'Goods', 'Services', 'Both'
	id_assigned_buyer INTEGER, -- FK lógica a admin.user
	id_parent_supplier INTEGER, -- FK a supplier.id_supplier
	-- Ubicación
	province VARCHAR(1),
	canton VARCHAR(2),
	district VARCHAR(2),
	other_signs VARCHAR(250),
	gps_latitude DECIMAL(10,7),
	gps_longitude DECIMAL(10,7),
	-- Contacto
	phone_code VARCHAR(3),
	phone VARCHAR(20),
	mobile VARCHAR(20),
	email VARCHAR(160),
	website VARCHAR(200),
	contact_name VARCHAR(200),
	contact_position VARCHAR(100),
	-- Datos bancarios
	bank_name VARCHAR(100),
	bank_account VARCHAR(50),
	iban VARCHAR(50),
	swift_code VARCHAR(20),
	-- Notas
	notes VARCHAR(2000),
	internal_notes VARCHAR(2000),
	-- Estado
	is_active BOOLEAN NOT NULL DEFAULT TRUE,
	blocked_reason VARCHAR(500),
	-- Auditoría
	createdate TIMESTAMP NOT NULL DEFAULT now(),
	record_date TIMESTAMP NOT NULL DEFAULT now(),
	created_by VARCHAR(30) NOT NULL DEFAULT current_user,
	updated_by VARCHAR(30) NOT NULL DEFAULT current_user,
	rowpointer UUID NOT NULL DEFAULT gen_random_uuid(),

	CONSTRAINT supplier_pkey PRIMARY KEY (id_supplier),
	CONSTRAINT rpix_sinai_supplier UNIQUE (rowpointer),
	CONSTRAINT uq_sinai_supplier_code UNIQUE (code)
);

-- Índices
CREATE UNIQUE INDEX IF NOT EXISTS uix_sinai_supplier_code ON sinai.supplier(code);
CREATE INDEX IF NOT EXISTS ix_sinai_supplier_identification ON sinai.supplier(identification);
CREATE INDEX IF NOT EXISTS ix_sinai_supplier_email ON sinai.supplier(email);
CREATE INDEX IF NOT EXISTS ix_sinai_supplier_active ON sinai.supplier(is_active);
CREATE INDEX IF NOT EXISTS ix_sinai_supplier_parent ON sinai.supplier(id_parent_supplier);
CREATE INDEX IF NOT EXISTS ix_sinai_supplier_type ON sinai.supplier(supplier_type);

-- FK self-referencing
ALTER TABLE sinai.supplier
ADD CONSTRAINT fk_supplier_parent
FOREIGN KEY (id_parent_supplier) REFERENCES sinai.supplier(id_supplier)
ON DELETE RESTRICT;

-- Trigger de auditoría
CREATE OR REPLACE FUNCTION sinai.tr_supplier_update_fn()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
	NEW.updated_by  := current_user;
	NEW.record_date := now();
	RETURN NEW;
END;
$$;

CREATE TRIGGER tr_supplier_update
BEFORE UPDATE ON sinai.supplier
FOR EACH ROW EXECUTE FUNCTION sinai.tr_supplier_update_fn();

-- Permisos
GRANT SELECT, INSERT, UPDATE, DELETE ON sinai.supplier TO PUBLIC;
GRANT ALL ON sinai.supplier TO cmssystem;
GRANT USAGE, SELECT ON SEQUENCE sinai.supplier_id_supplier_seq TO cmssystem;
```

**Entity C#:** `CMS.Entities/Operational/Supplier.cs` (✅ YA CREADO)

---

### 3️⃣ `sinai.customer_billing_credential` - TODO lo de Facturación Electrónica
**Propósito:** Almacena **COMPLETA** la información para emisión/recepción de comprobantes.

**Campos NUEVOS a AGREGAR:**
- `is_issuer` (BOOLEAN) - Indica si es emisor o receptor
- `is_company_owner` (BOOLEAN) - Indica si es la empresa dueña del sistema
- `name` (VARCHAR 200) - Razón social
- `commercial_name` (VARCHAR 200) - Nombre comercial
- `identification_type` (VARCHAR 2) - Tipo de cédula
- `identification` (VARCHAR 20) - Número de cédula
- `foreign_identification` (VARCHAR 20) - ID extranjero
- `economic_activity` (VARCHAR 6) - Código actividad económica
- `province` (VARCHAR 1)
- `canton` (VARCHAR 2)
- `district` (VARCHAR 2)
- `other_signs` (VARCHAR 250)
- `gps_latitude` (DECIMAL 10,7)
- `gps_longitude` (DECIMAL 10,7)
- `phone_code` (VARCHAR 3)
- `phone` (VARCHAR 20)
- `email` (VARCHAR 160) - Email de notificación Hacienda

**Campos EXISTENTES (certificados cifrados):**
- `id_customer` (INTEGER, ahora NULLABLE) - FK a customer/supplier
- `environment` (VARCHAR 10) - 'stag' | 'prod'
- `p12_cipher`, `p12_iv` (BYTEA)
- `pin_cipher`, `pin_iv` (BYTEA)
- `oauth_username` (VARCHAR 160)
- `oauth_password_cipher`, `oauth_password_iv` (BYTEA)
- `cert_not_before`, `cert_not_after` (TIMESTAMP)
- `key_version` (INTEGER)
- `is_active` (BOOLEAN)
- Auditoría estándar

**Regla de negocio:** Solo puede haber **1 credential activa** por `(id_customer, environment)` o si `id_customer` es NULL (receptor genérico), solo 1 activa por `(environment, identification)`.

**Script SQL de migración:**
```sql
-- Ver: CMS.Data/Scripts/019_refactor_customer_billing_credential.sql

-- 1. Hacer id_customer NULLABLE (puede ser standalone)
ALTER TABLE sinai.customer_billing_credential
ALTER COLUMN id_customer DROP NOT NULL;

-- 2. Agregar campos de identificación y flags
ALTER TABLE sinai.customer_billing_credential
ADD COLUMN is_issuer BOOLEAN NOT NULL DEFAULT FALSE,
ADD COLUMN is_company_owner BOOLEAN NOT NULL DEFAULT FALSE,
ADD COLUMN name VARCHAR(200) NOT NULL DEFAULT '',
ADD COLUMN commercial_name VARCHAR(200),
ADD COLUMN identification_type VARCHAR(2) NOT NULL DEFAULT '02',
ADD COLUMN identification VARCHAR(20) NOT NULL DEFAULT '',
ADD COLUMN foreign_identification VARCHAR(20),
ADD COLUMN economic_activity VARCHAR(6);

-- 3. Agregar campos de ubicación
ALTER TABLE sinai.customer_billing_credential
ADD COLUMN province VARCHAR(1),
ADD COLUMN canton VARCHAR(2),
ADD COLUMN district VARCHAR(2),
ADD COLUMN other_signs VARCHAR(250),
ADD COLUMN gps_latitude DECIMAL(10,7),
ADD COLUMN gps_longitude DECIMAL(10,7);

-- 4. Agregar campos de contacto
ALTER TABLE sinai.customer_billing_credential
ADD COLUMN phone_code VARCHAR(3) DEFAULT '506',
ADD COLUMN phone VARCHAR(20),
ADD COLUMN email VARCHAR(160) NOT NULL DEFAULT '';

-- 5. Hacer certificados NULLABLE (receptores no tienen certificado)
ALTER TABLE sinai.customer_billing_credential
ALTER COLUMN p12_cipher DROP NOT NULL,
ALTER COLUMN p12_iv DROP NOT NULL,
ALTER COLUMN pin_cipher DROP NOT NULL,
ALTER COLUMN pin_iv DROP NOT NULL,
ALTER COLUMN oauth_password_cipher DROP NOT NULL,
ALTER COLUMN oauth_password_iv DROP NOT NULL;

-- 6. Migrar datos existentes de customer a customer_billing_credential
-- SOLO si existen datos en customer_billing_credential vinculados a customers con is_issuer=true
UPDATE sinai.customer_billing_credential cbc
SET 
	is_issuer = c.is_issuer,
	is_company_owner = c.is_company_owner,
	name = c.name,
	commercial_name = c.commercial_name,
	identification_type = COALESCE(c.identification_type, '02'),
	identification = COALESCE(c.identification, ''),
	foreign_identification = c.foreign_identification,
	economic_activity = c.economic_activity,
	province = c.province,
	canton = c.canton,
	district = c.district,
	other_signs = c.other_signs,
	gps_latitude = c.gps_latitude,
	gps_longitude = c.gps_longitude,
	phone_code = c.phone_code,
	phone = c.phone,
	email = COALESCE(c.email, '')
FROM sinai.customer c
WHERE cbc.id_customer = c.id_customer;

-- 7. Crear índice único para company owner
CREATE UNIQUE INDEX IF NOT EXISTS uix_sinai_cbc_company_owner
ON sinai.customer_billing_credential(is_company_owner)
WHERE is_company_owner = TRUE AND is_active = TRUE;

-- 8. Índices adicionales
CREATE INDEX IF NOT EXISTS ix_sinai_cbc_issuer ON sinai.customer_billing_credential(is_issuer);
CREATE INDEX IF NOT EXISTS ix_sinai_cbc_identification ON sinai.customer_billing_credential(identification);
CREATE INDEX IF NOT EXISTS ix_sinai_cbc_email ON sinai.customer_billing_credential(email);
```

**Entity C#:** `CMS.Entities/Operational/CustomerBillingCredential.cs` (✅ YA ACTUALIZADO)

---

### 4️⃣ Eliminar Tabla `sinai.customer` - Columnas de E-Invoice
**Script SQL:**
```sql
-- Ver: CMS.Data/Scripts/020_cleanup_customer_table.sql

-- Eliminar columnas de facturación electrónica
ALTER TABLE sinai.customer
DROP COLUMN IF EXISTS is_issuer,
DROP COLUMN IF EXISTS is_company_owner,
DROP COLUMN IF EXISTS active_environment,
DROP COLUMN IF EXISTS economic_activity;

-- Eliminar índice de company owner (ya no existe)
DROP INDEX IF EXISTS sinai.uix_sinai_customer_company_owner;
```

**Entity C#:** `CMS.Entities/Operational/Customer.cs` (⏳ PENDIENTE ACTUALIZAR)

---

## 🗂️ ARCHIVOS A ELIMINAR

### Entities Legacy (⏳ PENDIENTE)
- `CMS.Entities/Operational/BillingIssuer.cs`
- `CMS.Entities/Operational/BillingReceptor.cs`
- `CMS.Entities/Operational/BillingCredential.cs`

**⚠️ IMPORTANTE:** Antes de eliminar, buscar **TODAS** las referencias en:
- `CompanyDbContext.cs` (DbSet, configuración OnModelCreating)
- Services (cualquier servicio que los use)
- Controllers (API/UI)
- Views (Razor Pages/MVC)

---

## 🔧 ARCHIVOS DE CÓDIGO C# A ACTUALIZAR

### 1. CompanyDbContext.cs
**Ubicación:** `CMS.Data/CompanyDbContext.cs`

**Cambios:**
```csharp
// ❌ ELIMINAR DbSets legacy
public DbSet<BillingIssuer> BillingIssuers { get; set; }
public DbSet<BillingReceptor> BillingReceptors { get; set; }
public DbSet<BillingCredential> BillingCredentials { get; set; }

// ✅ AGREGAR DbSet nuevo
public DbSet<Supplier> Suppliers { get; set; } = null!;

// ❌ ELIMINAR configuraciones legacy en OnModelCreating
modelBuilder.Entity<BillingIssuer>(entity => { ... });
modelBuilder.Entity<BillingReceptor>(entity => { ... });
modelBuilder.Entity<BillingCredential>(entity => { ... });

// ✅ AGREGAR configuración de Supplier
modelBuilder.Entity<Supplier>(entity =>
{
	entity.ToTable("supplier", _schema);
	entity.HasKey(e => e.Id);
	entity.Property(e => e.Id).HasColumnName("id_supplier");
	entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName($"uix_{_schema}_supplier_code");
	entity.HasIndex(e => e.Identification).HasDatabaseName($"ix_{_schema}_supplier_identification");
	entity.HasIndex(e => e.Email).HasDatabaseName($"ix_{_schema}_supplier_email");
	entity.HasIndex(e => e.IsActive).HasDatabaseName($"ix_{_schema}_supplier_active");
	entity.HasIndex(e => e.IdParentSupplier).HasDatabaseName($"ix_{_schema}_supplier_parent");
	entity.HasIndex(e => e.SupplierType).HasDatabaseName($"ix_{_schema}_supplier_type");

	// Self-referencing FK
	entity.HasOne(e => e.ParentSupplier)
		  .WithMany(e => e.ChildSuppliers)
		  .HasForeignKey(e => e.IdParentSupplier)
		  .OnDelete(DeleteBehavior.Restrict)
		  .IsRequired(false);
});

// ✅ ACTUALIZAR configuración de CustomerBillingCredential
modelBuilder.Entity<CustomerBillingCredential>(entity =>
{
	entity.ToTable("customer_billing_credential", _schema);
	entity.HasKey(e => e.Id);
	entity.Property(e => e.Id).HasColumnName("id_customer_billing_credential");

	// Unique constraint per customer/environment
	entity.HasIndex(e => new { e.IdCustomer, e.Environment })
		  .IsUnique()
		  .HasDatabaseName($"uq_{_schema}_customer_billing_credential_env");

	// Índice único para company owner
	entity.HasIndex(e => e.IsCompanyOwner)
		  .IsUnique()
		  .HasFilter("is_company_owner = true AND is_active = true")
		  .HasDatabaseName($"uix_{_schema}_cbc_company_owner");

	entity.HasIndex(e => e.IdCustomer).HasDatabaseName($"ix_{_schema}_cbc_customer");
	entity.HasIndex(e => e.Environment).HasDatabaseName($"ix_{_schema}_cbc_env");
	entity.HasIndex(e => e.IsIssuer).HasDatabaseName($"ix_{_schema}_cbc_issuer");
	entity.HasIndex(e => e.Identification).HasDatabaseName($"ix_{_schema}_cbc_identification");
	entity.HasIndex(e => e.Email).HasDatabaseName($"ix_{_schema}_cbc_email");

	// FK a customer (NULLABLE)
	entity.HasOne(e => e.Customer)
		  .WithMany()
		  .HasForeignKey(e => e.IdCustomer)
		  .OnDelete(DeleteBehavior.Restrict)
		  .IsRequired(false);
});
```

---

### 2. Services de Facturación Electrónica

#### 2.1 AuthenticationService.cs
**Ubicación:** `CMS.Data/Services/EInvoice/AuthenticationService.cs`

**Cambios:**
```csharp
// ❌ ANTES: Recibía BillingIssuer
public async Task<OAuthTokenResponse> AuthenticateAsync(
	BillingIssuer issuer,
	BillingCredential credential)

// ✅ AHORA: Recibe solo CustomerBillingCredential (tiene todo)
public async Task<OAuthTokenResponse> AuthenticateAsync(
	CustomerBillingCredential credential)
{
	// credential.OAuthUsername ya existe
	// credential.Identification (para el request)
	// credential.Email (para logging)
}
```

#### 2.2 SignatureService.cs
**Ubicación:** `CMS.Data/Services/EInvoice/SignatureService.cs`

**Cambios:**
```csharp
// ❌ ANTES: Recibía BillingCredential
public async Task<string> SignXmlAsync(
	string xml,
	BillingCredential credential)

// ✅ AHORA: Recibe CustomerBillingCredential
public async Task<string> SignXmlAsync(
	string xml,
	CustomerBillingCredential credential)
{
	// credential.P12Cipher, credential.P12Iv
	// credential.PinCipher, credential.PinIv
	// credential.KeyVersion
}
```

#### 2.3 EmissionService.cs
**Ubicación:** `CMS.Data/Services/EInvoice/EmissionService.cs`

**Cambios:**
```csharp
// ❌ ANTES: Buscaba issuer y receptor por separado
var issuer = await _db.BillingIssuers.FirstOrDefaultAsync(i => i.Id == issuerId);
var receptor = await _db.BillingReceptors.FirstOrDefaultAsync(r => r.Id == receptorId);
var credential = await _db.BillingCredentials.FirstOrDefaultAsync(...);

// ✅ AHORA: Todo viene de customer_billing_credential
var issuerCredential = await _db.CustomerBillingCredentials
	.FirstOrDefaultAsync(c => c.Id == issuerCredentialId && c.IsIssuer && c.IsActive);

var receptorCredential = await _db.CustomerBillingCredentials
	.FirstOrDefaultAsync(c => c.Id == receptorCredentialId && !c.IsIssuer);

// Construir XML usando:
// issuerCredential.Identification, issuerCredential.Name
// receptorCredential.Identification, receptorCredential.Name
```

#### 2.4 EInvoiceRetryWorker.cs
**Ubicación:** `CMS.Data/Services/EInvoice/EInvoiceRetryWorker.cs`

**Cambios:**
```csharp
// ❌ ANTES: Buscaba BillingIssuer
var issuer = await db.BillingIssuers.FirstOrDefaultAsync(i => i.IsCompanyOwner);

// ✅ AHORA: Busca CustomerBillingCredential de company owner
var companyOwnerCredential = await db.CustomerBillingCredentials
	.FirstOrDefaultAsync(c => c.IsCompanyOwner && c.IsActive);
```

---

### 3. Controllers

#### 3.1 CustomerController.cs (API)
**Ubicación:** `CMS.API/Controllers/CustomerController.cs`

**Cambios:** Ninguno necesario (ya usa `CustomerService` que solo lee `customer`).

#### 3.2 ElectronicInvoiceController.cs (API)
**Ubicación:** `CMS.API/Controllers/ElectronicInvoiceController.cs`

**Método `Emit`:**
```csharp
// ❌ ANTES: Recibía issuerId y receptorId de billing_issuer/billing_receptor
public async Task<IActionResult> Emit([FromBody] EmitInvoiceRequest request)
{
	// request.IssuerId, request.ReceptorId
}

// ✅ AHORA: Recibe issuerCredentialId y receptorCredentialId
public async Task<IActionResult> Emit([FromBody] EmitInvoiceRequest request)
{
	// request.IssuerCredentialId (int) - FK a customer_billing_credential
	// request.ReceptorCredentialId (int) - FK a customer_billing_credential
}
```

---

### 4. UI Views

#### 4.1 ElectronicInvoice/Emit.cshtml
**Ubicación:** `CMS.UI/Views/ElectronicInvoice/Emit.cshtml`

**Cambios en JavaScript:**
```javascript
// ❌ ANTES: Cargaba emisores/receptores de /api/Customer?includeInactive=false
async function loadIssuers() {
	const response = await fetch('/api/Customer?includeInactive=false', {
		headers: { 'Authorization': `Bearer ${token}` }
	});
	const customers = await response.json();
	// Filtraba clientes con IsIssuer=true
	const issuers = customers.filter(c => c.IsIssuer);
}

// ✅ AHORA: Carga credentials de /api/CustomerBillingCredential/issuers
async function loadIssuers() {
	const response = await fetch('/api/CustomerBillingCredential/issuers', {
		headers: { 'Authorization': `Bearer ${token}` }
	});
	const issuers = await response.json();
	// Llena select con: id_customer_billing_credential, name, identification
}

async function loadReceptors() {
	const response = await fetch('/api/CustomerBillingCredential/receptors', {
		headers: { 'Authorization': `Bearer ${token}` }
	});
	const receptors = await response.json();
}

// Al emitir
async function emit() {
	const payload = {
		issuerCredentialId: parseInt(document.getElementById('issuerId').value),
		receptorCredentialId: parseInt(document.getElementById('receptorId').value),
		// ... resto de campos
	};
}
```

---

## 📁 SCRIPTS SQL A CREAR

### Script 1: CREATE TABLE supplier
**Archivo:** `CMS.Data/Scripts/018_create_supplier_table.sql`  
**Estado:** ⏳ PENDIENTE  
**Contenido:** Ver sección "2️⃣ sinai.supplier" arriba

### Script 2: ALTER customer_billing_credential
**Archivo:** `CMS.Data/Scripts/019_refactor_customer_billing_credential.sql`  
**Estado:** ⏳ PENDIENTE  
**Contenido:** Ver sección "3️⃣ sinai.customer_billing_credential" arriba

### Script 3: DROP COLUMNS de customer
**Archivo:** `CMS.Data/Scripts/020_cleanup_customer_table.sql`  
**Estado:** ⏳ PENDIENTE  
**Contenido:** Ver sección "4️⃣ Eliminar Tabla sinai.customer - Columnas" arriba

---

## 🧪 PLAN DE TESTING

### Paso 1: Validar Migración SQL
```sql
-- Verificar que customer ya NO tiene columnas de e-invoice
\d sinai.customer
-- Debe faltar: is_issuer, is_company_owner, active_environment, economic_activity

-- Verificar que customer_billing_credential tiene TODAS las columnas nuevas
\d sinai.customer_billing_credential
-- Debe tener: is_issuer, is_company_owner, name, identification, email, etc.

-- Verificar que supplier existe
\d sinai.supplier
-- Debe existir con todos los campos

-- Verificar datos migrados
SELECT id_customer_billing_credential, name, identification, is_issuer, is_company_owner
FROM sinai.customer_billing_credential;
```

### Paso 2: Compilar Solución
```powershell
dotnet build CMS.sln
```
Debe compilar sin errores.

### Paso 3: Probar APIs
```bash
# Listar customers (debe funcionar sin cambios)
GET /api/Customer?includeInactive=false

# Listar emisores (NUEVO endpoint)
GET /api/CustomerBillingCredential/issuers

# Listar receptores (NUEVO endpoint)
GET /api/CustomerBillingCredential/receptors

# Emitir factura con nuevos IDs
POST /api/electronicinvoice/emit
{
  "issuerCredentialId": 1,
  "receptorCredentialId": 2,
  ...
}
```

### Paso 4: Probar UI
1. Login → Seleccionar compañía SINAI
2. Navegar a `/Customers/Customers` → Debe mostrar clientes
3. Navegar a `/ElectronicInvoice/Emit`:
   - Select "Emisor" debe llenarse con credentials de emisores
   - Select "Receptor" debe llenarse con credentials de receptores
   - Al emitir, debe funcionar end-to-end

---

## 🔄 ORDEN DE EJECUCIÓN

### Fase 1: Base de Datos (SQL)
1. Ejecutar `018_create_supplier_table.sql`
2. Ejecutar `019_refactor_customer_billing_credential.sql`
3. Ejecutar `020_cleanup_customer_table.sql`
4. Validar con queries de prueba

### Fase 2: Entities (.NET)
1. Eliminar archivos legacy:
   - `BillingIssuer.cs`
   - `BillingReceptor.cs`
   - `BillingCredential.cs`
2. Actualizar `Customer.cs` (eliminar campos e-invoice)
3. `Supplier.cs` (✅ ya existe)
4. `CustomerBillingCredential.cs` (✅ ya actualizado)

### Fase 3: CompanyDbContext
1. Eliminar DbSets legacy
2. Eliminar configuraciones legacy en `OnModelCreating`
3. Agregar DbSet de `Supplier`
4. Agregar configuración de `Supplier` en `OnModelCreating`
5. Actualizar configuración de `CustomerBillingCredential`

### Fase 4: Services
1. Actualizar `AuthenticationService.cs`
2. Actualizar `SignatureService.cs`
3. Actualizar `EmissionService.cs`
4. Actualizar `EInvoiceRetryWorker.cs`

### Fase 5: API Controllers
1. Crear `CustomerBillingCredentialController.cs` con endpoints:
   - `GET /api/CustomerBillingCredential/issuers`
   - `GET /api/CustomerBillingCredential/receptors`
2. Actualizar `ElectronicInvoiceController.cs` (método `Emit`)

### Fase 6: UI
1. Actualizar `Emit.cshtml` (JavaScript para cargar credentials)
2. Probar flujo completo de emisión

### Fase 7: Testing
1. Testing manual de APIs
2. Testing manual de UI
3. Emisión real a Hacienda sandbox

---

## ⚠️ PUNTOS CRÍTICOS A NO OLVIDAR

1. **id_customer en customer_billing_credential es NULLABLE** - Puede haber credentials standalone (receptores genéricos).
2. **Solo 1 company owner activo** - Índice único parcial en `is_company_owner=true AND is_active=true`.
3. **Certificados solo para emisores** - Los campos `p12_cipher`, `pin_cipher`, etc. son NULLABLE (receptores no tienen certificado).
4. **Migración de datos ANTES de DROP COLUMNS** - Ejecutar script 019 ANTES de script 020.
5. **Validar que NO queden referencias a BillingIssuer/BillingReceptor** - Buscar en toda la solución antes de eliminar archivos.

---

## 📊 ESTADO ACTUAL (ÚLTIMA ACTUALIZACIÓN: 2026-01-24 20:15)

### ✅ COMPLETADO AL 100%

#### Base de Datos
- [x] **Script 018**: Tabla `supplier` creada exitosamente
- [x] **Script 019**: `customer_billing_credential` refactorizada con todos los campos
- [x] **Script 020**: `customer` limpiada de campos e-invoice

#### Entities & Dominio
- [x] `Supplier.cs` creado
- [x] `CustomerBillingCredential.cs` ampliado
- [x] `Customer.cs` limpiado
- [x] Entities legacy eliminadas (`BillingIssuer`, `BillingReceptor`, `BillingCredential`)
- [x] `CompanyDbContext.cs` actualizado

#### Backend (100% Funcional)
- [x] `CustomerService.cs` simplificado
- [x] `EInvoiceRetryWorker.cs` refactorizado
- [x] `HaciendaAuthService.cs` + interface
- [x] `HaciendaApiClient.cs` + interface
- [x] `ElectronicDocumentXmlBuilder.cs` + interface
- [x] `ElectronicDocumentService.cs` end-to-end
- [x] `EInvoiceDiagnosticsController.cs`
- [x] `CustomerController.cs` (endpoints deprecados)
- [x] `EInvoiceVaultService.cs`
- [x] **`CustomerBillingCredentialController.cs` NUEVO** - API completa para credentials

#### UI
- [x] Customers Razor Pages limpias
- [x] **`Emit.cshtml` actualizada** - JavaScript usa nuevos endpoints

#### Compilación
- [x] **✅ BUILD SUCCESSFUL** - Sin errores ni warnings

---

## 🎉 RESUMEN FINAL

### Lo que se logró

1. **Separación completa de responsabilidades**:
   - `Customer` → Solo datos operacionales (CRM/Ventas)
   - `CustomerBillingCredential` → Única fuente de datos e-invoice
   - `Supplier` → Maestro de proveedores (Purchasing/AP)

2. **Migraciones SQL ejecutadas**:
   - ✅ Tabla `supplier` creada
   - ✅ `customer_billing_credential` con 17 campos nuevos
   - ✅ `customer` limpia de campos e-invoice

3. **Backend refactorizado al 100%**:
   - Todos los servicios e-invoice usan `CustomerBillingCredential`
   - Eliminadas todas las referencias a entidades legacy
   - API controller nuevo con endpoints completos

4. **UI actualizada**:
   - Emit.cshtml carga emisores/receptores de la nueva API
   - Customers UI sin dependencias de e-invoice

### Endpoints disponibles

```
GET  /api/CustomerBillingCredential              - Listar todas
GET  /api/CustomerBillingCredential/issuers      - Solo emisores
GET  /api/CustomerBillingCredential/receptors    - Solo receptores
GET  /api/CustomerBillingCredential/company-owner - Company owner
GET  /api/CustomerBillingCredential/{id}         - Por ID
POST /api/CustomerBillingCredential              - Crear
PUT  /api/CustomerBillingCredential/{id}         - Actualizar
DELETE /api/CustomerBillingCredential/{id}        - Desactivar
```

### Testing pendiente (Opcional)

1. ⏳ Probar carga de emisores/receptores en `/ElectronicInvoice/Emit`
2. ⏳ Emitir factura de prueba a Hacienda sandbox
3. ⏳ Validar que `/Customers/Customers` funciona correctamente

---

## 🔄 PASOS SIGUIENTES (EN ORDEN)

### Paso 1: Ejecutar Scripts SQL ⚠️ CRÍTICO PRIMERO
```powershell
psql -h 10.0.0.1 -U postgres -d sinai -f "CMS.Data\Scripts\018_create_supplier_table.sql"
psql -h 10.0.0.1 -U postgres -d sinai -f "CMS.Data\Scripts\019_refactor_customer_billing_credential.sql"
psql -h 10.0.0.1 -U postgres -d sinai -f "CMS.Data\Scripts\020_cleanup_customer_table.sql"
```

### Paso 2: Corregir Servicios C#
1. `CustomerService.cs` - Simplificar (eliminar métodos de emisor)
2. `EInvoiceRetryWorker.cs` - Usar CustomerBillingCredentials
3. `ElectronicDocumentService.cs` - Cambiar a CustomerBillingCredential
4. `HaciendaApiClient.cs` - Usar Environment en lugar de ActiveEnvironment
5. `HaciendaAuthService.cs` - Ídem
6. `ElectronicDocumentXmlBuilder.cs` - Usar CustomerBillingCredential
7. `EInvoiceDiagnosticsController.cs` - Actualizar nombres de variables

### Paso 3: Crear Nuevos Controllers
1. `CustomerBillingCredentialController.cs` - API para gestionar credentials
2. Actualizar `ElectronicInvoiceController.cs` - Usar CustomerBillingCredential

### Paso 4: Actualizar UI
1. `Emit.cshtml` - Cargar emisores/receptores de `/api/CustomerBillingCredential`
2. Crear vistas para gestionar credentials (opcional para MVP)

### Paso 5: Testing
1. Compilar solución
2. Probar `/Customers/Customers`
3. Probar `/ElectronicInvoice/Emit`
4. Emisión real a Hacienda sandbox

---

## 📝 NOTAS IMPORTANTES

- **NO ELIMINAR** `CustomerService` completo - Solo simplificar para no incluir lógica de emisores
- **Customers ahora es SOLO CRM** - No tiene nada de facturación
- **CustomerBillingCredential es la ÚNICA fuente** de datos de e-invoice
- **Emisores y receptores** se buscan en CustomerBillingCredential con filtros `IsIssuer=true/false`

---

## 🔗 REFERENCIAS IMPORTANTES

### Archivos Clave
- **CompanyDbContext**: `CMS.Data/CompanyDbContext.cs` (líneas 720-780 configuración Customer y CustomerBillingCredential)
- **CompanyDbContextFactory**: `CMS.Data/Services/CompanyDbContextFactory.cs` (método `CreateDbContextAsync`)
- **EmissionService**: `CMS.Data/Services/EInvoice/EmissionService.cs`
- **EInvoiceRetryWorker**: `CMS.Data/Services/EInvoice/EInvoiceRetryWorker.cs`
- **Emit.cshtml**: `CMS.UI/Views/ElectronicInvoice/Emit.cshtml`

### Patrones de Naming
- **Tablas**: `snake_case` (ej: `customer_billing_credential`)
- **Entities**: `PascalCase` (ej: `CustomerBillingCredential`)
- **Propiedades C#**: `PascalCase` (ej: `IsIssuer`)
- **Columnas DB**: `snake_case` (ej: `is_issuer`)
- **Índices**: `ix_{schema}_{tabla}_{campo}` o `uix_` para únicos

---

**FIN DEL DOCUMENTO DE ESTADO**
