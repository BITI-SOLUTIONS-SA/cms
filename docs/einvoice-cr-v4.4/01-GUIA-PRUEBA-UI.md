# Guía de prueba desde la UI — Facturación Electrónica CR v4.4

> Estado del sistema: **verificado end-to-end contra el sandbox de Hacienda
> (comprobante ACEPTADO)**. Esta guía describe cómo probar la emisión desde la
> interfaz web del CMS.

## 0. Requisitos previos (ya configurados)

| Elemento | Estado |
|---|---|
| Catálogo CAByS (admin.cabys) | ✅ 20,506 códigos |
| Tablas fiscales en `sinai` | ✅ 9 tablas + columnas is_service / hacienda_detail |
| Emisor master (id=1) | ✅ MARTINEZ ROJAS ERNESTO ALEJANDRO (206190901) |
| Credencial .p12 + PIN + OAuth (ambiente stag) | ✅ Cifrada AES-256 en BD |
| Master key AES | ✅ `EInvoice:MasterKey` en appsettings.json (dev) |
| Menú + permisos | ✅ E-Invoicing (id 17) |

> **Producción:** definir la variable de entorno `EINVOICE_MASTER_KEY` (Kubernetes
> Secret). NUNCA usar la master key del appsettings en producción.

## 1. Levantar API + UI

```powershell
# Terminal 1 - API
cd CMS.API
dotnet run

# Terminal 2 - UI
cd CMS.UI
dotnet run
```

Abrir la UI (https://localhost:5001) e iniciar sesión con un usuario que tenga los
permisos `EInvoice.*` (el admin id=1 ya los tiene).

## 2. Menú

`E-Invoicing` en el menú lateral, submenús:
- **Comprobantes** → `/ElectronicInvoice`
- **Emisores** → `/ElectronicInvoice/Issuers`
- **Emitir Comprobante** → `/ElectronicInvoice/Emit`

## 3. Verificar el emisor y su certificado

1. Ir a **Emisores**.
2. El emisor `MARTINEZ ROJAS ERNESTO ALEJANDRO` debe aparecer con:
   - Ambiente activo: **Pruebas** (badge gris).
   - Certificado **Pruebas: válido**.
3. (Opcional) Clic en **Certificado** para ver las pestañas Pruebas/Producción.
   - Pestaña **Pruebas**: ya tiene certificado cargado.
   - Pestaña **Producción**: vacía (cargar cuando se tenga el .p12 de producción).

### Cargar un nuevo certificado (flujo real por cliente)
En el modal de Certificado, pestaña correspondiente:
- Seleccionar `.p12`, ingresar PIN, usuario y clave OAuth.
- Clic **Cargar y cifrar**. El archivo se cifra AES-256 en el servidor (nunca viaja de vuelta).

### Promover a Producción
Cuando el cliente apruebe las pruebas ante Hacienda:
- Cargar el certificado de **Producción**.
- Clic **Usar Producción** (exige certificado de producción vigente).

## 4. Emitir una factura de prueba

1. Ir a **Emitir Comprobante**.
2. Seleccionar el **Emisor** (Ernesto Martínez).
3. Tipo de documento: **Factura Electrónica (FE)**.
4. Agregar una línea:
   - CAByS: `8399000000000` (valida 13 díg. y autocompleta tarifa 13%).
   - Detalle: `Servicios profesionales`.
   - Cantidad: `1`, Precio: `5000`.
5. Clic **Emitir y enviar a Hacienda**.

Resultado esperado: mensaje con la **Clave** de 50 díg. y estado
`Procesando` (enviado a Hacienda) o `Contingencia` (si Hacienda no responde).

## 5. Ver el resultado

1. Ir a **Comprobantes**.
2. La factura aparece con su estado. El **worker de reintentos** consulta el estado
   automáticamente cada 30s y lo actualiza a **Aceptado** / **Rechazado**.
3. Iconos por fila:
   - **XML**: descarga el XML firmado XAdES-EPES.
   - **PDF**: descarga la representación gráfica.
   - **ℹ️** junto al estado Hacienda: muestra el `DetalleMensaje` (motivo).

## 6. Validaciones frontend (guards)

- **CAByS**: no permite guardar sin 13 dígitos (borde rojo).
- **Correo**: regex estricto.
- **IVI inverso**: al marcar "Precios con IVA incluido", desglosa la base hacia atrás.
- **Descuento**: exige naturaleza si hay monto de descuento.
- **NC/ND/REP**: exige la clave de referencia (50 díg.).

## 7. Solución de problemas

| Síntoma | Causa / Acción |
|---|---|
| `Emisor sin credenciales para el ambiente 'stag'` | Cargar el .p12 en la pestaña Pruebas |
| Estado `Contingencia` | Hacienda no respondió; el worker reintenta con backoff |
| Estado `Rechazado` + ℹ️ | Ver el DetalleMensaje (datos/estructura) |
| OAuth `invalid_client` | Usuario/clave de API incorrectos (regenerar en Tribu) |

## 8. Mapeo de endpoints Hacienda (Tribu 2026)

| Ambiente | Token (realm) | client_id | API recepción |
|---|---|---|---|
| Sandbox | `rut-stag` | `api-stag` | `api-sandbox.comprobanteselectronicos.go.cr/recepcion/v1` |
| Producción | `rut` | `api-prod` | `api.comprobanteselectronicos.go.cr/recepcion/v1` |
