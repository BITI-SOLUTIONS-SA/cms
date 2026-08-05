# 🧪 GUÍA DE TESTING MANUAL - EMISIÓN DE FACTURA ELECTRÓNICA

**Fecha:** 2026-01-24
**Estado de la aplicación:** ✅ Corriendo en https://localhost:5001

---

## 📋 DATOS DE PRUEBA CREADOS

### Emisor (Company Owner)
```
ID: 1
Nombre: BITI SOLUTIONS S.A
Cédula: 3101234567
Tipo: Jurídica (02)
Actividad Económica: 620100
Email: facturacion@biti.cr
Teléfono: 506-22223333
Ambiente: stag (sandbox)
```

### Receptor
```
ID: 2
Nombre: CLIENTE PRUEBA S.A
Cédula: 3102222222
Tipo: Jurídica (02)
Email: cliente@test.cr
Teléfono: 506-88887777
Ambiente: stag (sandbox)
```

---

## 🔐 PASO 1: Autenticación

1. Abre el navegador y navega a: `https://localhost:5001`
2. Inicia sesión con tus credenciales
3. Selecciona la compañía **SINAI** (o la que tenga `company_schema = 'sinai'`)

---

## 📝 PASO 2: Verificar Endpoints de API

### 2.1 Listar Emisores

**Endpoint:** `GET https://localhost:5001/api/CustomerBillingCredential/issuers`

**Respuesta esperada:**
```json
[
  {
	"id": 1,
	"name": "BITI SOLUTIONS S.A",
	"identification": "3101234567",
	"is_issuer": true,
	"is_company_owner": true,
	"environment": "stag",
	"economic_activity": "620100"
  }
]
```

### 2.2 Listar Receptores

**Endpoint:** `GET https://localhost:5001/api/CustomerBillingCredential/receptors`

**Respuesta esperada:**
```json
[
  {
	"id": 2,
	"name": "CLIENTE PRUEBA S.A",
	"identification": "3102222222",
	"is_issuer": false,
	"environment": "stag"
  }
]
```

---

## 🧾 PASO 3: Emitir Factura de Prueba

### 3.1 Navegar a la Pantalla de Emisión

1. Ve a: `https://localhost:5001/ElectronicInvoice/Emit`
2. Verifica que:
   - El dropdown **Emisor** se llena con "BITI SOLUTIONS S.A (3101234567)"
   - El dropdown **Receptor** se llena con "CLIENTE PRUEBA S.A (3102222222)"

### 3.2 Completar el Formulario

```
Emisor: BITI SOLUTIONS S.A (3101234567)
Receptor: CLIENTE PRUEBA S.A (3102222222)
Tipo de documento: Factura Electrónica (FE)
Condición venta: Contado
Moneda: CRC
Correo receptor: cliente@test.cr
```

### 3.3 Agregar Línea de Prueba

```
CAByS: 2118401010109  (Servicios de desarrollo de software)
Descripción: Desarrollo de software personalizado
Cantidad: 1
Precio: 100000.00
IVA %: 13
Descuento: 0
```

### 3.4 Emitir

1. Click en "Emitir y enviar a Hacienda"
2. Esperar respuesta

---

## ✅ RESPUESTA ESPERADA

### Exitosa (202 Accepted o 200 OK)
```json
{
  "documentId": 123,
  "clave": "50624012600031012345670100000010000000001112345678",
  "consecutive": "00100001000000001",
  "status": "Procesando",
  "sentToHacienda": true,
  "message": "Comprobante emitido y enviado a Hacienda."
}
```

### En Contingencia (Hacienda no disponible)
```json
{
  "documentId": 123,
  "clave": "50624012600031012345670100000010000000001112345678",
  "consecutive": "00100001000000001",
  "status": "Contingencia",
  "sentToHacienda": false,
  "message": "Hacienda no disponible. Comprobante en contingencia; se reintentará automáticamente."
}
```

---

## 🔍 PASO 4: Verificar en Base de Datos

### 4.1 Ver el Documento Creado

```sql
SELECT 
	id_electronic_document,
	clave,
	consecutive,
	status,
	hacienda_status,
	submitted_at,
	accepted_at
FROM sinai.electronic_document
ORDER BY createdate DESC
LIMIT 1;
```

### 4.2 Ver la Cola de Reintentos

```sql
SELECT 
	id_einvoice_retry_queue,
	id_electronic_document,
	operation,
	attempt_count,
	next_attempt_at,
	is_done
FROM sinai.einvoice_retry_queue
WHERE is_done = false
ORDER BY createdate DESC;
```

---

## 🐛 TROUBLESHOOTING

### Error: "Credencial de emisor no encontrada"
**Solución:** Verifica que el emisor existe y tiene certificado .p12 configurado:
```sql
SELECT id_customer_billing_credential, name, identification, p12_cipher IS NOT NULL as has_cert
FROM sinai.customer_billing_credential
WHERE is_issuer = true AND is_active = true;
```

### Error: "Emisor sin credenciales (.p12) configuradas"
**Solución:** El certificado .p12 y PIN deben estar cifrados en la BD. Para pruebas en sandbox, necesitas el certificado de prueba de Hacienda.

### Error: "401 Unauthorized" de Hacienda
**Solución:** Las credenciales OAuth (`oauth_username`, `oauth_password_cipher`) deben estar configuradas correctamente para el ambiente sandbox.

### Dropdown de Emisor/Receptor Vacío
**Solución:** 
1. Abre la consola del navegador (F12)
2. Ve a la pestaña "Network"
3. Verifica que las peticiones a `/api/CustomerBillingCredential/issuers` y `/receptors` devuelvan 200 OK
4. Si devuelve 401, verifica la autenticación

---

## 📊 VALIDACIÓN COMPLETA

### Checklist de Validación

- [ ] Login exitoso
- [ ] Selección de compañía correcta (sinai)
- [ ] Endpoint `/api/CustomerBillingCredential/issuers` devuelve emisores
- [ ] Endpoint `/api/CustomerBillingCredential/receptors` devuelve receptores
- [ ] Pantalla `/ElectronicInvoice/Emit` carga correctamente
- [ ] Dropdowns de Emisor/Receptor se llenan automáticamente
- [ ] Formulario se completa sin errores de validación
- [ ] Click en "Emitir" ejecuta sin errores de JavaScript
- [ ] API `/api/electronicinvoice/emit` devuelve 200 OK
- [ ] Documento se crea en `sinai.electronic_document`
- [ ] Clave numérica tiene 50 dígitos
- [ ] Consecutivo tiene 20 dígitos
- [ ] Estado es "Procesando" o "Contingencia"
- [ ] Worker `EInvoiceRetryWorker` procesa la cola automáticamente

---

## 📁 ARCHIVOS DE CONFIGURACIÓN

### appsettings.json (CMS.UI)
Verifica que tenga:
```json
{
  "ConnectionStrings": {
	"DefaultConnection": "Host=10.0.0.1;Database=cms;Username=postgres;Password=POStgres2026"
  }
}
```

### Configuración de Hacienda (sinai.company)
```sql
SELECT 
	company_name,
	hacienda_environment,
	hacienda_token_url,
	hacienda_reception_url,
	hacienda_schema_version
FROM admin.company
WHERE company_schema = 'sinai';
```

---

## 🎯 RESULTADO ESPERADO FINAL

1. ✅ Factura creada en BD
2. ✅ Clave numérica generada (50 dígitos)
3. ✅ XML firmado con certificado .p12
4. ✅ Enviada a Hacienda sandbox
5. ✅ Respuesta "aceptado" o "procesando"
6. ✅ Cola de reintentos activa si es necesario

---

## 🔗 ENDPOINTS DE DIAGNÓSTICO

### Verificar Conectividad con Hacienda
```
GET https://localhost:5001/api/einvoice/diagnostics/ping-hacienda?environment=stag
```

### Verificar Configuración del Emisor
```
GET https://localhost:5001/api/einvoice/diagnostics/issuer/1/readiness
```

### Generar XML de Muestra (sin enviar)
```
POST https://localhost:5001/api/einvoice/diagnostics/generate-sample?issuerId=1&documentType=01&price=10000
```

---

**FIN DE LA GUÍA DE TESTING**

📌 **NOTA:** Si encuentras algún error durante el testing, revisa los logs de la aplicación en la consola de PowerShell donde se ejecutó `dotnet run`.
