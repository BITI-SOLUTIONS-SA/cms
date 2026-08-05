# HACIENDA-CORE v4.4 — Análisis Maestro de Facturación Electrónica de Costa Rica

> **Documento maestro legible por IA.** Contiene TODO el conocimiento necesario para
> implementar, mantener y extender el módulo de Facturación Electrónica de Costa Rica
> v4.4 dentro del proyecto CMS de BITI Solutions S.A.
>
> **Versión normativa:** v4.4 (obligatoria desde 1 de septiembre de 2025).
> **Autor:** BITI Solutions S.A (EAMR).
> **Última actualización:** 2026.

---

## 0. Índice

1. [Contexto de negocio y modelo multi-emisor](#1-contexto-de-negocio-y-modelo-multi-emisor)
2. [Principios inquebrantables](#2-principios-inquebrantables)
3. [Arquitectura sobre el CMS existente](#3-arquitectura-sobre-el-cms-existente)
4. [Modelo de datos completo](#4-modelo-de-datos-completo)
5. [Seguridad criptográfica (Vault AES-256 + XAdES)](#5-seguridad-criptográfica)
6. [Clave Numérica de 50 dígitos](#6-clave-numérica-de-50-dígitos)
7. [Consecutivo fiscal (sucursal/terminal)](#7-consecutivo-fiscal)
8. [Ingeniería del XML v4.4](#8-ingeniería-del-xml-v44)
9. [Firma XAdES-EPES](#9-firma-xades-epes)
10. [Conexión con Hacienda (OAuth2 + API)](#10-conexión-con-hacienda)
11. [Máquina de estados del documento](#11-máquina-de-estados-del-documento)
12. [Resiliencia y contingencia](#12-resiliencia-y-contingencia)
13. [Casos de uso avanzados v4.4](#13-casos-de-uso-avanzados-v44)
14. [Validaciones frontend (guards)](#14-validaciones-frontend)
15. [Catálogos oficiales](#15-catálogos-oficiales)
16. [Mapa de archivos del módulo](#16-mapa-de-archivos-del-módulo)

---

## 1. Contexto de negocio y modelo multi-emisor

### 1.1 Problema de negocio
El primer cliente de este módulo es un **contador/despacho contable** que lleva la
contabilidad y la facturación electrónica de **múltiples clientes**. Por lo tanto,
la configuración de facturación electrónica NO puede estar atada a la Compañía global
del CMS: debe estar a nivel de **Emisor (Billing Issuer)**.

### 1.2 Concepto clave: Billing Issuer (Emisor Facturador)
Un **Billing Issuer** es una persona física o jurídica en cuyo nombre se emiten
comprobantes electrónicos ante Hacienda. Vive en la **BD operacional de la compañía**
(`{schema}.billing_issuer`).

- Cada compañía puede tener **N emisores**.
- Cada emisor tiene su propio `.p12`, PIN, usuario/clave OAuth de Hacienda y ambiente.
- Existe un emisor marcado como **master** (`is_master = true`) que representa a la
  propia empresa dueña de la compañía (el despacho contable en sí mismo).
- Los demás emisores son los clientes del despacho.

```
Compañía CMS (schema: sinai)
 └── billing_issuer (is_master=true)  → El despacho contable (facturas propias)
 └── billing_issuer  → Cliente A del despacho
 └── billing_issuer  → Cliente B del despacho
 └── billing_issuer  → Cliente C del despacho
```

### 1.3 Relación con entidades CMS existentes
| Concepto Hacienda | Entidad CMS | Notas |
|---|---|---|
| Emisor | `{schema}.billing_issuer` (NUEVO) | Agregado raíz del módulo |
| Receptor (cliente del emisor) | `{schema}.billing_receptor` (NUEVO) | Clientes a quienes se factura |
| Producto/servicio facturable | `{schema}.item` (EXISTE) | Se le añade CAByS + tarifa IVA |
| Datos de la compañía CMS | `admin.company` (EXISTE) | Solo contexto multi-tenant |
| Numeración | `{schema}.fiscal_consecutive` (NUEVO) | Distinto del `Consecutive` interno |

---

## 2. Principios inquebrantables

1. **Zero-Trust criptográfico:** El `.p12` y su PIN NUNCA viajan al cliente ni se
   guardan en texto plano. Se cifran AES-256 en la BD operacional del cliente y solo
   se descifran en memoria RAM volátil durante la firma, limpiando inmediatamente
   (sobrescritura de buffers + `GC.Collect()` forzado).
2. **Resiliencia ante Hacienda:** El sistema NUNCA falla si la API de Hacienda está
   caída. Encola en `einvoice_retry_queue`, marca estado `Pendiente`/`Contingencia`
   y reintenta con backoff exponencial.
3. **Integridad fiscal:** No se factura sin código CAByS (13 díg.) validado, ni se
   emite Nota de Crédito/Débito ni REP sin referencia (`InformacionReferencia`) al
   documento previo mediante su Clave Numérica de 50 díg.
4. **Inmutabilidad de FechaEmision:** En contingencia, se conserva la fecha del
   momento de la venta, NO la del reenvío.
5. **Consecutivo atómico:** Único y secuencial por emisor/sucursal/terminal, con
   bloqueo `Serializable` en BD para evitar duplicados en concurrencia.

---

## 3. Arquitectura sobre el CMS existente

### 3.1 Capas Clean Architecture
```
CMS.Entities      → Entidades de dominio (BillingIssuer, ElectronicDocument, ...)
CMS.Application   → DTOs (ElectronicDocumentDtos, BillingIssuerDtos, ...)
CMS.Data          → DbContexts + Servicios (Vault, Auth, ApiClient, XmlBuilder, ...)
CMS.API           → Controllers REST + DI + BackgroundService worker
CMS.UI            → Razor Pages + validadores frontend
CMS.Shared        → Utilidades compartidas
```

### 3.2 Reutilización de infraestructura existente
| Infraestructura CMS | Reutilización en HACIENDA-CORE |
|---|---|
| `CompanyDbContextFactory` + Regla de Oro Dev/Prod | Acceso multi-BD a datos fiscales |
| `ConsecutiveService` (transacción `Serializable`) | Patrón de concurrencia para Clave Numérica |
| `EmailService` + SMTP en `admin.system_config` | Envío de XML+PDF al receptor |
| `AuditService` + triggers SQL | Auditoría de documentos fiscales |
| `FileDocument` (bytea en BD) | Patrón de almacenamiento binario en BD |
| `is_production` + Regla de Oro | Selección Sandbox vs Producción de endpoints Hacienda |

### 3.3 Ubicación de datos
- **BD Central (`cms`, schema `admin`):** SOLO el catálogo CAByS compartido
  (`admin.cabys`), por ser un catálogo gubernamental idéntico para todas las compañías.
- **BD Operacional (`{schema}`):** Todo lo demás — emisores, credenciales cifradas,
  documentos electrónicos, líneas, referencias, impuestos, consecutivos fiscales y
  cola de reintentos. Los XML firmados se almacenan aquí como `text`/`bytea`.

---

## 4. Modelo de datos completo

### 4.1 Catálogo central (BD `cms`, schema `admin`)

**`admin.cabys`** — Catálogo de Bienes y Servicios (13 dígitos)
| Columna | Tipo | Descripción |
|---|---|---|
| id_cabys | SERIAL PK | Identidad |
| code | VARCHAR(13) UNIQUE | Código CAByS de 13 dígitos |
| description | VARCHAR(1000) | Descripción del bien/servicio |
| tax_rate | DECIMAL(5,2) | Tarifa IVA asociada (ej. 13.00, 4.00, 2.00, 1.00, 0.00) |
| tax_rate_code | VARCHAR(2) | Código tarifa (01=0%, 02=1%, 03=2%, 04=4%, 08=13%) |
| category | VARCHAR(500) | Jerarquía/categoría |
| is_active | BOOLEAN | Vigencia |

### 4.2 BD Operacional (`{schema}`)

**`{schema}.billing_issuer`** — Emisor facturador
| Columna | Tipo | Descripción |
|---|---|---|
| id_billing_issuer | SERIAL PK | Identidad |
| code | VARCHAR(30) UNIQUE | Código de negocio |
| is_master | BOOLEAN | TRUE = la empresa misma (despacho) |
| id_type_id | INTEGER | Tipo de identificación Hacienda (01,02,03,04,05) |
| identification | VARCHAR(20) | Cédula/identificación |
| name | VARCHAR(200) | Nombre/razón social |
| commercial_name | VARCHAR(200) | Nombre comercial |
| province, canton, district | VARCHAR | Ubicación (códigos Hacienda) |
| other_signs | VARCHAR(250) | Señas exactas |
| phone_code, phone | VARCHAR | Teléfono |
| email | VARCHAR(160) | Correo (validado regex) |
| economic_activity | VARCHAR(6) | Código actividad económica |
| environment | VARCHAR(10) | 'stag' \| 'prod' |

**`{schema}.billing_credential`** — Vault del emisor (AES-256)
| Columna | Tipo | Descripción |
|---|---|---|
| id_billing_credential | SERIAL PK | Identidad |
| id_billing_issuer | INTEGER FK | Emisor dueño |
| p12_cipher | BYTEA | `.p12` cifrado AES-256 |
| p12_iv | BYTEA | Vector de inicialización |
| pin_cipher | BYTEA | PIN cifrado AES-256 |
| pin_iv | BYTEA | IV del PIN |
| oauth_username | VARCHAR(160) | Usuario OAuth Hacienda |
| oauth_password_cipher | BYTEA | Clave OAuth cifrada |
| oauth_password_iv | BYTEA | IV |
| cert_not_before, cert_not_after | TIMESTAMP | Vigencia del certificado |
| key_version | INTEGER | Versión de la master key usada |

**`{schema}.fiscal_consecutive`** — Consecutivo por emisor/sucursal/terminal
| Columna | Tipo | Descripción |
|---|---|---|
| id_fiscal_consecutive | SERIAL PK | Identidad |
| id_billing_issuer | INTEGER FK | Emisor |
| branch | VARCHAR(3) | Sucursal (casa matriz '001') |
| terminal | VARCHAR(5) | Terminal/POS ('00001') |
| document_type | VARCHAR(2) | 01=FE,02=ND,03=NC,04=TE,08=FEC,09=REP... |
| last_value | BIGINT | Último consecutivo de 10 díg. usado |

**`{schema}.electronic_document`** — Cabecera del comprobante
| Columna | Tipo | Descripción |
|---|---|---|
| id_electronic_document | SERIAL PK | Identidad |
| id_billing_issuer | INTEGER FK | Emisor |
| id_billing_receptor | INTEGER | Receptor (FK lógica) |
| document_type | VARCHAR(2) | 01/02/03/04/08/09 |
| clave | VARCHAR(50) UNIQUE | Clave Numérica de 50 díg. |
| consecutive | VARCHAR(20) | Consecutivo de 20 díg. |
| situation | VARCHAR(2) | 01=normal, 02=contingencia, 03=sin internet |
| status | VARCHAR(20) | Máquina de estados (§11) |
| issue_date | TIMESTAMPTZ | FechaEmision (INMUTABLE) |
| currency | VARCHAR(3) | CRC/USD... |
| exchange_rate | DECIMAL(18,5) | Tipo de cambio |
| sub_total, total_taxes, total | DECIMAL(18,5) | Montos |
| xml_signed | TEXT | XML firmado XAdES-EPES |
| xml_response | TEXT | Respuesta MensajeHacienda |
| hacienda_status | VARCHAR(20) | aceptado/rechazado/procesando |
| pdf_document | BYTEA | Representación PDF |

**`{schema}.electronic_document_line`** — Línea de detalle
| Columna | Tipo | Descripción |
|---|---|---|
| id_electronic_document_line | SERIAL PK | Identidad |
| id_electronic_document | INTEGER FK | Cabecera |
| line_number | INTEGER | Número de línea |
| id_item | INTEGER | Item (FK lógica) |
| cabys_code | VARCHAR(13) | Código CAByS |
| quantity | DECIMAL(16,3) | Cantidad |
| unit_measure | VARCHAR(15) | Unidad |
| unit_price | DECIMAL(18,5) | Precio unitario (base, sin IVA) |
| discount_amount | DECIMAL(18,5) | Monto descuento |
| discount_nature | VARCHAR(2) | 01=Regalía,04=Volumen,05=Temporada,06=Promoción |
| sub_total | DECIMAL(18,5) | Subtotal |
| taxable_base | DECIMAL(18,5) | Base imponible |
| total_line | DECIMAL(18,5) | Total línea |

**`{schema}.electronic_document_tax`** — Impuestos por línea
| Columna | Tipo | Descripción |
|---|---|---|
| id_electronic_document_tax | SERIAL PK | Identidad |
| id_electronic_document_line | INTEGER FK | Línea |
| tax_code | VARCHAR(2) | 01=IVA... |
| tax_rate_code | VARCHAR(2) | Código tarifa |
| tax_rate | DECIMAL(5,2) | % |
| tax_amount | DECIMAL(18,5) | Monto impuesto |

**`{schema}.electronic_document_reference`** — Referencia a documentos previos
| Columna | Tipo | Descripción |
|---|---|---|
| id_electronic_document_reference | SERIAL PK | Identidad |
| id_electronic_document | INTEGER FK | Documento actual (NC/ND/REP) |
| ref_document_type | VARCHAR(2) | Tipo doc referenciado |
| ref_clave | VARCHAR(50) | Clave de 50 díg. referenciada |
| ref_date | TIMESTAMPTZ | Fecha del doc referenciado |
| ref_code | VARCHAR(2) | Código de referencia (01=anula,02=corrige...) |
| ref_reason | VARCHAR(180) | Razón |

**`{schema}.einvoice_retry_queue`** — Cola de reintentos
| Columna | Tipo | Descripción |
|---|---|---|
| id_einvoice_retry_queue | SERIAL PK | Identidad |
| id_electronic_document | INTEGER FK | Documento |
| operation | VARCHAR(20) | 'send' \| 'poll_status' |
| attempt_count | INTEGER | Intentos realizados |
| next_attempt_at | TIMESTAMPTZ | Próximo intento (backoff) |
| last_error | TEXT | Último error |
| is_done | BOOLEAN | Completado |

---

## 5. Seguridad criptográfica

### 5.1 Vault AES-256
- **Algoritmo:** AES-256-CBC (o GCM) con IV aleatorio por secreto.
- **Master Key:** clave de 256 bits derivada (PBKDF2) de un secreto de aplicación
  gestionado fuera de la BD (variable de entorno / Kubernetes Secret / config).
  Nunca se guarda junto a los datos cifrados. `key_version` permite rotación.
- **Almacenamiento:** columnas `bytea` en `{schema}.billing_credential`.

### 5.2 Ciclo de vida del `.p12` durante la firma (Zero-Trust)
```
1. Leer p12_cipher + p12_iv y pin_cipher + pin_iv de la BD.
2. Descifrar en memoria (byte[] / char[]).
3. Cargar X509Certificate2 desde el byte[] con el PIN.
4. Firmar el XML (XAdES-EPES).
5. Sobrescribir buffers (Array.Clear) del p12, PIN y clave.
6. Dispose del certificado. GC.Collect() + WaitForPendingFinalizers().
```

---

## 6. Clave Numérica de 50 dígitos

### 6.1 Estructura exacta (concatenación)
```
[3]  Código de país          → 506
[2]  Día de emisión          → DD
[2]  Mes de emisión          → MM
[2]  Año de emisión          → AA
[12] Identificación emisor   → cédula rellenada a 12 con ceros a la izquierda
[20] Consecutivo             → ver §7 (sucursal+terminal+tipo+secuencia)
[1]  Situación               → 1=normal, 2=contingencia, 3=sin internet
[8]  Código de seguridad     → 8 dígitos aleatorios
------------------------------------------------------------------------
Total = 3+2+2+2+12+20+1+8 = 50 dígitos
```

### 6.2 Regla de oro
El **consecutivo (20 díg.)** debe ser único y secuencial por emisor/sucursal/terminal.
Usar bloqueo `SELECT ... FOR UPDATE` / transacción `Serializable` para evitar duplicados
en concurrencia (mismo patrón que `ConsecutiveService`).

---

## 7. Consecutivo fiscal

### 7.1 Estructura del consecutivo de 20 dígitos
```
[3]  Sucursal        → casa matriz '001'
[5]  Terminal/Pos    → '00001'
[2]  Tipo documento  → 01=FE,02=ND,03=NC,04=TE,08=FEC,09=REP
[10] Secuencia       → numérico incremental por (sucursal,terminal,tipo)
--------------------------------------------------------------
Total = 3+5+2+10 = 20 dígitos
```

### 7.2 Tipos de documento
| Código | Documento |
|---|---|
| 01 | Factura Electrónica (FE) |
| 02 | Nota de Débito Electrónica (ND) |
| 03 | Nota de Crédito Electrónica (NC) |
| 04 | Tiquete Electrónico (TE) |
| 08 | Factura Electrónica de Compra (FEC) |
| 09 | Recibo Electrónico de Pago (REP) |

---

## 8. Ingeniería del XML v4.4

### 8.1 Namespaces mandatorios (Factura Electrónica)
```xml
<FacturaElectronica
  xmlns="https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/facturaElectronica"
  xmlns:ds="http://www.w3.org/2000/09/xmldsig#"
  xmlns:xsd="http://www.w3.org/2001/XMLSchema"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:schemaLocation="https://cdn.comprobanteselectronicos.go.cr/xml-schemas/v4.4/facturaElectronica https://www.hacienda.go.cr/ATV/ComprobanteElectronico/docs/esquemas/2016/v4.4/FacturaElectronica_V4.4.xsd">
```
El namespace raíz cambia según el tipo de documento (facturaElectronica,
notaCreditoElectronica, notaDebitoElectronica, tiqueteElectronico,
facturaElectronicaCompra, reciboElectronicoPago).

### 8.2 Estructura de nodos principales
```
<Clave>                     50 díg.
<ProveedorSistemas>         cédula del proveedor del sistema
<CodigoActividadEmisor>     6 díg.
<NumeroConsecutivo>         20 díg.
<FechaEmision>              ISO 8601 con zona -06:00
<Emisor>                    §4.2 billing_issuer
<Receptor>                  billing_receptor
<CondicionVenta>            01=contado,02=crédito...
<DetalleServicio>           líneas
<ResumenFactura>            totales, desglose IVA
<InformacionReferencia>     obligatorio en NC/ND/REP
<Normativa>                 resolución + fecha
<ds:Signature>             XAdES-EPES
```

---

## 9. Firma XAdES-EPES

- **Perfil:** XAdES-EPES **Enveloped**.
- **Algoritmo:** RSA-SHA256.
- **Signature Policy (OBLIGATORIO):**
  - Identifier: `https://cdn.comprobanteselectronicos.go.cr/xml-schemas/Resolucion_General_sobre_disposiciones_tecnicas_comprobantes_electronicos_para_efectos_tributarios.pdf`
  - Digest: SHA-256 del PDF de la política vigente, incrustado en el nodo firmado.
- **Referencias:** el documento completo + propiedades firmadas XAdES
  (SigningTime, SigningCertificate, SignaturePolicyIdentifier).
- Implementación con `System.Security.Cryptography.Xml.SignedXml` extendido para
  incluir los QualifyingProperties de XAdES.

---

## 10. Conexión con Hacienda

### 10.1 OAuth2 (Resource Owner Password Credentials — mandatorio legacy)
- Token endpoint: `https://idp.comprobanteselectronicos.go.cr/auth/realms/rut/protocol/openid-connect/token`
- client_id: `api-prod` (producción) / `api-stag` (sandbox)
- grant_type: `password`
- Refresh automático 5 minutos antes de expirar el `access_token`.

> ⚠️ **ACTUALIZACIÓN TRIBU (2025+) — CRÍTICO:** El SANDBOX migró a un **realm distinto**.
> Mapeo verificado y funcional (2026):
>
> | Ambiente | Realm (token endpoint) | client_id | API recepción |
> |---|---|---|---|
> | **Producción** | `rut` | `api-prod` | `recepcion/v1` |
> | **Sandbox** | `rut-stag` | `api-stag` | `recepcion-sandbox/v1` |
>
> - Sandbox token endpoint: `.../auth/realms/rut-stag/protocol/openid-connect/token`
> - Producción token endpoint: `.../auth/realms/rut/protocol/openid-connect/token`
> - `grant_type=password` SIGUE vigente en ambos realms.
> - El usuario/clave de API se generan desde **Tribu / Tico Factura** (portal de Hacienda),
>   y son DISTINTOS entre pruebas y producción (cuentas separadas).
> - Enviar `User-Agent` en los requests al API de recepción (evita bloqueos del WAF).

### 10.2 Endpoints de recepción
| Entorno | URL base |
|---|---|
| Producción | `https://api.comprobanteselectronicos.go.cr/recepcion/v1/` |
| Sandbox | `https://api.comprobanteselectronicos.go.cr/recepcion-sandbox/v1/` |

Selección de entorno por `billing_issuer.environment` combinado con la Regla de Oro
del CMS (Environment=Development ⇒ forzar sandbox salvo config explícita).

### 10.3 Manejo de respuestas HTTP
| Código | Acción |
|---|---|
| 202 Accepted | Recibido, encolar `poll_status` |
| 429 Too Many Requests | Leer `X-RateLimit-Reset`, dormir ese tiempo |
| 400 Bad Request (duplicado) | Marcar local como `Enviado`, ir a consultar estado |
| 401 Unauthorized | `RefreshToken()` y reintentar el request original |
| 5xx / timeout | Encolar en RetryQueue con backoff |

---

## 11. Máquina de estados del documento

```
				 ┌────────────┐
				 │  Borrador  │
				 └─────┬──────┘
					   │ generar+firmar
				 ┌─────▼──────┐
				 │  Firmado   │
				 └─────┬──────┘
			  enviar   │           sin internet
		┌──────────────┼────────────────────┐
   ┌────▼─────┐   ┌────▼──────┐        ┌─────▼────────┐
   │ Enviado  │   │ Pendiente │        │ Contingencia │
   └────┬─────┘   └────┬──────┘        └─────┬────────┘
		│ poll         │ reintento           │ worker + internet
   ┌────▼─────┐        └─────────┬───────────┘
   │Procesando│                  │
   └────┬─────┘                  │
   ┌────┴───────────┐            │
┌──▼─────┐     ┌────▼────┐       │
│Aceptado│     │Rechazado│◄──────┘
└────────┘     └─────────┘
```

Estados: `Borrador`, `Firmado`, `Enviado`, `Pendiente`, `Contingencia`,
`Procesando`, `Aceptado`, `Rechazado`, `Anulado`.

---

## 12. Resiliencia y contingencia

### 12.1 Modo sin internet (offline-first)
```
Si Ping(Hacienda) == Fail:
  1. status = Contingencia
  2. situation = 02 (contingencia) o 03 (sin internet) en la Clave Numérica
  3. Generar PDF con leyenda "Comprobante Provisional - Contingencia"
  4. Guardar en einvoice_retry_queue
  5. Al volver internet → worker procesa la cola
	 IMPORTANTE: conservar FechaEmision original.
```

### 12.2 Backoff exponencial
`next_attempt_at = now + base * 2^attempt_count` (con tope máximo, p.ej. 1h).

---

## 13. Casos de uso avanzados v4.4

### 13.1 REP (Recibo Electrónico de Pago) — exclusivo v4.4
Trigger: se cobra una factura emitida a crédito (plazo 90 días o venta al Estado)
con IVA diferido. Genera documento tipo REP con `InformacionReferencia` apuntando
a la Clave de 50 díg. de la factura original. Sin match ⇒ rechazo de Hacienda.

### 13.2 FEC (Factura de Compra) — proveedores extranjeros
Cliente paga a proveedor extranjero (AWS, Facebook Ads). Auto-generar FEC v4.4 con
Tipo de Identificación `05` (Extranjero No Domiciliado) en el nodo Emisor, permitiendo
deducir el gasto sin cédula jurídica nacional.

### 13.3 Naturaleza del descuento (obligatoria)
No basta el monto. Mapeo: `01=Regalía, 04=Volumen, 05=Temporada, 06=Promoción`.

### 13.4 Combos y surtidos
Si los productos del pack tienen distinta tarifa IVA ⇒ descomponer en líneas
individuales. Si comparten tarifa ⇒ usar nodo `DetalleSurtido`.

### 13.5 Cálculo inverso IVI
Si el usuario ingresa precio con impuesto incluido, desglosar hacia atrás:
`Base = Total / (1 + tarifa)`. Nunca enviar el total en la base imponible.

---

## 14. Validaciones frontend

1. **CAByS:** no permitir guardar producto sin código CAByS de 13 dígitos.
2. **Correo:** regex estricto en `CorreoElectronico`.
3. **IVI inverso:** desglose automático al ingresar precio con impuesto incluido.
4. **Referencia obligatoria:** NC/ND/REP requieren seleccionar documento previo.

---

## 15. Catálogos oficiales

| Catálogo | Uso | Ubicación |
|---|---|---|
| CAByS (13 díg.) | Bienes y servicios + tarifa IVA | `admin.cabys` |
| Tipo de identificación | 01,02,03,04,05 | Constantes / `admin.type_id` |
| Condición de venta | 01,02,03... | Constantes |
| Medio de pago | 01,02,03,04... | Constantes |
| Código de impuesto | 01=IVA... | Constantes |
| Código tarifa IVA | 01..08 | Constantes |
| Unidad de medida | Sp, Unid, kg... | `admin.unit_of_measure` (EXISTE) |
| Actividad económica | 6 díg. | Config del emisor |

---

## 16. Mapa de archivos del módulo

```
CMS.Entities/
  Admin/CabysCode.cs
  Operational/BillingIssuer.cs
  Operational/BillingCredential.cs
  Operational/BillingReceptor.cs
  Operational/FiscalConsecutive.cs
  Operational/ElectronicDocument.cs
  Operational/ElectronicDocumentLine.cs
  Operational/ElectronicDocumentTax.cs
  Operational/ElectronicDocumentReference.cs
  Operational/EInvoiceRetryQueue.cs
  EInvoice/EInvoiceEnums.cs

CMS.Data/
  Services/EInvoice/IEInvoiceVaultService.cs + EInvoiceVaultService.cs
  Services/EInvoice/IClaveNumericaGenerator.cs + ClaveNumericaGenerator.cs
  Services/EInvoice/IHaciendaAuthService.cs + HaciendaAuthService.cs
  Services/EInvoice/IHaciendaApiClient.cs + HaciendaApiClient.cs
  Services/EInvoice/IElectronicDocumentXmlBuilder.cs + ElectronicDocumentXmlBuilder.cs
  Services/EInvoice/IXadesSignatureService.cs + XadesSignatureService.cs
  Services/EInvoice/IElectronicDocumentService.cs + ElectronicDocumentService.cs
  Services/EInvoice/EInvoiceRetryWorker.cs
  Scripts/134..14x_*.sql

CMS.Application/
  DTOs/EInvoice/*.cs

CMS.API/
  Controllers/BillingIssuerController.cs
  Controllers/ElectronicInvoiceController.cs
  Controllers/CabysController.cs

CMS.UI/
  (Razor Pages + validadores)
```

---

## 17. Hallazgos VERIFICADOS contra Hacienda (Tribu 2026) — comprobante ACEPTADO

Esta sección documenta el conocimiento verificado empíricamente al lograr el estado
`ind-estado: aceptado` en el sandbox real.

### 17.1 Endpoints correctos (cambiaron con Tribu)
| Ambiente | Token endpoint (realm) | client_id | API recepción |
|---|---|---|---|
| **Sandbox** | `idp.comprobanteselectronicos.go.cr/auth/realms/rut-stag/.../token` | `api-stag` | `https://api-sandbox.comprobanteselectronicos.go.cr/recepcion/v1/` |
| **Producción** | `idp.comprobanteselectronicos.go.cr/auth/realms/rut/.../token` | `api-prod` | `https://api.comprobanteselectronicos.go.cr/recepcion/v1/` |

- `grant_type=password` sigue vigente. Usuario/clave se generan en **Tribu / Tico Factura**.
- Enviar header `User-Agent` en los requests al API (evita bloqueos WAF).

### 17.2 Firma XAdES — usar FirmaXadesNetCore
- La firma manual con `SignedXml` produce firma matemáticamente válida pero Hacienda
  la RECHAZA (namespace por defecto sin prefijo `ds:`).
- **Solución:** `FirmaXadesNetCore` 1.1.0 (genera prefijos `ds:`/`xades:` correctos).
- **Signature Policy v4.4 (2024)** — valores reales:
  - Identifier: `https://atv.hacienda.go.cr/ATV/ComprobanteElectronico/docs/esquemas/2024/v4.4/Resoluci%C3%B3n_General_sobre_disposiciones_t%C3%A9cnicas_comprobantes_electr%C3%B3nicos_para_efectos_tributarios.pdf`
  - SigPolicyHash (HEX): `0D6C629F5C5639E23C3AE5905DACE1E158CB5806822C003DE787A6EC3321D21F`

### 17.3 Estructura XML v4.4 verificada (cambios vs versiones previas)
- **`ProveedorSistemas`** OBLIGATORIO tras `Clave` (cédula del proveedor del software).
  Valor BITI: `2100042005`.
- **`CodigoActividadEmisor`** con formato decimal, p.ej. `6202.0`.
- **`FechaEmision`** formato `yyyy-MM-ddTHH:mm:ss.000` (hora CR, con milisegundos).
- **`Ubicacion`** requiere `OtrasSenas` obligatorio.
- El nodo **`Normativa` fue ELIMINADO** en v4.4 (no incluirlo).
- Orden línea: `...BaseImponible, Impuesto, ImpuestoAsumidoEmisorFabrica, ImpuestoNeto, MontoTotalLinea`.
- `ResumenFactura` separa servicios vs mercancías: `TotalServGravados` /
  `TotalMercanciasGravadas` según naturaleza de cada línea. Incluye `TotalExonerado`,
  `TotalNoSujeto`, `TotalDesgloseImpuesto`, `MedioPago`, `TotalComprobante`.

### 17.4 Reglas de negocio (validaciones de Hacienda)
- La cédula del **certificado debe coincidir con la del Emisor** (error -60 si no).
  Persona física = 9 díg (p.ej. `206190901`), jurídica = 10 díg (`3101896397`).
- La **ubicación del emisor** debe coincidir con el registro en Tributación (error -37,
  es ADVERTENCIA, no rechazo).
- La **cédula del receptor** debe ser un registro válido (error -38).
- Los montos gravados deben cuadrar con la naturaleza (servicio/mercancía) de las
  líneas (errores -110/-111).

### 17.5 Consulta de estado
`GET {apiBase}/recepcion/{clave}` con Bearer token. El JSON incluye `ind-estado`
(`recibido`/`procesando`/`aceptado`/`rechazado`) y `respuesta-xml` (base64 del
MensajeHacienda con `DetalleMensaje`).

---

**FIN DEL DOCUMENTO MAESTRO.**
