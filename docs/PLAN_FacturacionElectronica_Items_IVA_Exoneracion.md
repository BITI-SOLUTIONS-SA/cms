# PLAN DE EJECUCIÓN — Facturación Electrónica v4.4: Ítems, Códigos IVA, Actividad Comercial, Régimen Especial y Exoneración

> **Estado**: EN EJECUCIÓN — Fase A ✅, Fase B ✅, R2 ✅, R3 ✅, R4 ✅ completadas (documento de requerimiento persistente)

## 🔖 BITÁCORA DE EJECUCIÓN

- ✅ **FIX REP — validación backend de saldo pendiente (bloquea sobre-pago)** — Corrige que el sistema permitiera emitir un REP (ej: `00100001100000000004` sobre factura `00100001010000000031`) con cantidad/monto **mayor al saldo pendiente**. La UI ya mostraba el saldo, pero el backend no validaba el REP (solo N/C y N/D vía `ValidateReversalLinesAsync`), permitiendo enviar a Hacienda un pago excedido si se manipulaba la cantidad.
  - **Causa raíz**: En `ElectronicDocumentService.EmitAsync`, solo `NotaCredito` y `NotaDebito` invocaban validación de saldos. El `ReciboElectronicoPago` no tenía ninguna validación de cantidades contra la factura referenciada.
  - **Solución**: Nuevo método `ValidateReceiptLinesAsync` (análogo a `ValidateReversalLinesAsync`) invocado cuando `DocumentType == ReciboElectronicoPago`. Valida: (1) referencia de clave presente, (2) documento origen existe y está ACEPTADO, (3) mismo emisor, (4) al menos una línea, (5) cada CAByS existe en la factura, y (6) la cantidad acumulada por CAByS **no supera** el saldo pendiente (`original − REP previos aceptados/pendientes`). Lanza `InvalidOperationException` con detalle (original/pagado/disponible) si se excede. Build de solución OK.
- ✅ **FIX REP — botón deshabilitado si factura totalmente pagada + precarga con saldo pendiente**
  - **Causa raíz**: El botón REP en `Index.cshtml` solo evaluaba tipo/estado/`saleCondition==='02'`, sin considerar los REP previos. La precarga en `Emit.cshtml` cargaba siempre la cantidad completa para el REP (no descontaba pagos previos).
  - **Solución (análoga a N/C)**:
    1. **DTO** — Nuevo flag `FullyPaid` en `ElectronicDocumentSummaryDto`.
    2. **API (lista)** — `GetList` calcula `FullyPaid` para facturas a crédito (01/08, `SaleCondition=="02"`) comparando cantidad original por CAByS contra lo ya documentado por REP (tipo 10) aceptados/pendientes que la referencian.
    3. **API (detalle)** — `GetDetail` calcula el saldo pendiente de pago por CAByS (`alreadyPaidByCabys`, `availableRepByLineNumber`) y expone `AvailableQuantityRep` por línea.
    4. **UI (lista)** — El botón REP se oculta si `d.fullyPaid` y en su lugar se muestra el badge **"Pagada"**; se mantiene visible mientras haya saldo (pago parcial), igual que la N/C.
    5. **UI (emisión)** — La precarga del REP usa `AvailableQuantityRep`, omite líneas ya pagadas (saldo ≤ 0) y muestra aviso si la factura ya fue totalmente pagada. Build de solución OK.
- ✅ **FIX N/C — precarga con saldo disponible (descuenta N/C previas)**
  - **Causa raíz**: El loop de precarga en `Emit.cshtml` cargaba `srcLines` directamente desde la factura de origen usando `l.Quantity` (cantidad original), sin considerar lo ya acreditado por N/C previas. El backend (`ValidateReversalLinesAsync`) ya rechazaba el sobre-crédito, pero la UI no reflejaba esa regla → mala UX y riesgo de envíos fallidos.
  - **Solución (3 capas)**:
    1. **API** — `ElectronicInvoiceController.GetDetail(int id)` calcula las cantidades ya acreditadas por N/C previas no rechazadas/anuladas que referencian el documento, agrupadas por CAByS (`alreadyCreditedByCabys`), deriva el saldo disponible (`remainingByCabys = original − acreditado`), lo distribuye por línea (`availableByLineNumber`) y expone `AvailableQuantity` en cada línea del payload JSON.
    2. **UI** — En `Emit.cshtml`, la precarga de N/C ahora usa `AvailableQuantity` en vez de `Quantity`; las líneas totalmente acreditadas (saldo ≤ 0) se **omiten**; si la factura ya fue **totalmente acreditada** (`loadedLines === 0`) se muestra un aviso claro. El REP conserva la cantidad completa (documenta el pago, no acredita).
    3. **Backend (ya existía)** — `ValidateReversalLinesAsync(...)` sigue bloqueando cantidades que superen el saldo; ahora la UI **refleja** la misma regla. Build de solución OK.
- ✅ **FIX REP `TotalComprobante`/`TotalMedioPago` coherentes con `TotalVentaNeta`**
  - **Causa raíz**: Para el REP forzamos `TotalVentaNeta = TotalVenta` (bruto), pero `TotalComprobante` y `TotalMedioPago` seguían usando `d.Total`, calculado sobre el neto con descuento → inconsistencia de `210.00` (el descuento).
  - **Solución**: En `ElectronicDocumentXmlBuilder.BuildResumen`, para REP `totalComprobante = ventaNeta + totalImpuestoNeto`; `MedioPago`/`TotalComprobante` usan ese valor. Los demás tipos conservan `d.Total`. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ✅ **FIX REP código de tipo `09` → `10` en la UI (selectores y JS)**
  - **Causa raíz**: El código fiscal del REP estaba hardcodeado como `09` en múltiples puntos de la UI: selector `#documentType` y `#refDocType` (`Emit.cshtml`), mapeo `TYPE` (`Index.cshtml`), `toggleReference()` (`['02','03','09']`) y `prefillNoteCredit()` (`targetType = isRep ? '09' : ...`).
  - **Solución**: Todos los puntos anteriores ahora usan `"10"` para REP, alineados con `EInvoiceDocumentType.ReciboElectronicoPago = "10"`. Build de solución OK.
- ✅ **FIX REP código fiscal de tipo de documento `09` → `10`** — Corrige rechazo de **regla de negocio `-78`** de Hacienda (*"La numeración consecutiva no cumple con la estructura en el 'artículo 4 numeración consecutiva' de la resolución 48-2016..."*):
  - **Causa raíz**: La constante `EInvoiceDocumentType.ReciboElectronicoPago` estaba definida como `"09"`, pero según la resolución 48-2016 el código de tipo de documento del **Recibo Electrónico de Pago es `10`**; el `09` corresponde a **Factura Electrónica de Exportación**. Por eso el consecutivo generado (`001 00001 09 0000000014`) incluía el segmento de tipo `09` en las posiciones 9-10, y Hacienda rechazaba la estructura consecutiva del REP.
  - **Solución**: En `CMS.Entities/EInvoice/EInvoiceEnums.cs`, `ReciboElectronicoPago = "10"`. Como `ClaveNumericaGenerator.BuildConsecutive`, `ElectronicDocumentXmlBuilder` (detección `isRep`, `DocMeta`), PDF y demás usos referencian la constante (no el literal `"09"`), el fix se propaga automáticamente. Nuevos consecutivos usan el segmento `10`. El cambio es un `const`, por lo que requiere **detener la depuración y recompilar** para aplicar (Hot Reload no soporta cambios de inicializador de campo `const`).
- ✅ **FIX REP `ResumenFactura` — `TotalVentaNeta` = `TotalVenta`** — Corrige rechazo de **regla de negocio `-53`** de Hacienda (*"El monto de venta neta no coincide con el monto total"*):
  - **Causa raíz**: Para el REP, Hacienda exige que `TotalVentaNeta` coincida con `TotalVenta` (no se restan descuentos, ya que el REP no lleva descuentos de línea).
  - **Solución**: En `ElectronicDocumentXmlBuilder.BuildResumen`, `ventaNeta = isRep ? totalBruto : totalBruto - totalDescuentos`. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ✅ **FIX REP `ResumenFactura` — omitir `TotalDescuentos`**
  - **Causa raíz**: El resumen del REP tampoco admite `<TotalDescuentos>`: tras `<TotalVenta>` espera directamente `<TotalVentaNeta>`.
  - **Solución**: En `ElectronicDocumentXmlBuilder.BuildResumen`, `<TotalDescuentos>` ahora se emite solo cuando `!isRep`. `TotalVentaNeta` sigue reflejando el neto (bruto − descuentos). Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ✅ **FIX REP `ResumenFactura` reducido (sin totales de clasificación)**
  - **Causa raíz**: El `ResumenFactura` del REP es reducido: tras `<CodigoTipoMoneda>` va directo a `<TotalVenta>`, omitiendo TODOS los totales de clasificación (`TotalServGravados`, `TotalServExentos`, `TotalServExonerado`, `TotalMercancias*`, `TotalGravado`, `TotalExento`, `TotalExonerado`, `TotalNoSujeto`). El builder los emitía (como en FE) y Hacienda rechazaba.
  - **Solución**: En `ElectronicDocumentXmlBuilder.BuildResumen`, el bloque de totales de clasificación ahora se emite solo cuando `!isRep`. Los totales que sí conserva el REP (`TotalVenta → [TotalDescuentos] → TotalVentaNeta → TotalDesgloseImpuesto → TotalImpuesto → MedioPago → TotalComprobante`) se mantienen para todos los tipos. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ✅ **FIX REP `LineaDetalle` — omitir `ImpuestoAsumidoEmisorFabrica`**
  - **Causa raíz**: El XSD del REP tampoco admite `<ImpuestoAsumidoEmisorFabrica>`: tras `<Impuesto>` espera directamente `<ImpuestoNeto>`. El builder lo emitía y Hacienda rechazaba.
  - **Solución**: En `ElectronicDocumentXmlBuilder.BuildDetalle`, la bandera `allowImpuestoAsumido` ahora también excluye `ReciboElectronicoPago` (además de `FacturaCompra`). Estructura final de línea REP: `NumeroLinea → Detalle → MontoTotal → SubTotal → Impuesto → ImpuestoNeto → MontoTotalLinea`. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ✅ **FIX REP `LineaDetalle` — omitir `BaseImponible`**
  - **Causa raíz**: El XSD del REP tampoco admite `<BaseImponible>` en la línea: tras `<SubTotal>` espera directamente `<Impuesto>` o `<ImpuestoNeto>`. El loop unificado lo emitía y Hacienda rechazaba.
  - **Solución**: En `ElectronicDocumentXmlBuilder.BuildDetalle`, `<BaseImponible>` ahora se emite solo cuando `!isRep`. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ✅ **FIX REP `LineaDetalle` — omitir `Descuento`**
  - **Causa raíz**: Aunque la línea del REP comparte casi toda la secuencia con los demás comprobantes, su XSD **no admite `<Descuento>`**: va directo de `<MontoTotal>` a `<SubTotal>`. El loop unificado emitía el descuento (heredado de la factura referenciada) y Hacienda rechazaba.
  - **Solución**: En `ElectronicDocumentXmlBuilder.BuildDetalle`, la condición de emisión del `<Descuento>` ahora es `!isRep && line.DiscountAmount > 0`. El `SubTotal` ya refleja el neto tras descuento, por lo que el importe se conserva. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ✅ **FIX REP `LineaDetalle` completa (NO reducida) — unificación con la línea estándar**
  - **Causa raíz / corrección de hipótesis previa**: Los rechazos incrementales (`MontoTotal` esperado, luego `SubTotal` esperado) demostraron que la hipótesis de una "línea REP reducida" era **incorrecta**. El XSD del REP usa la **misma secuencia de `LineaDetalle` que los demás comprobantes** (`NumeroLinea → Detalle → MontoTotal → [Descuento] → SubTotal → BaseImponible → [Impuesto] → ImpuestoNeto → MontoTotalLinea`); la única diferencia real es que **omite** `CodigoCABYS`, `Cantidad`, `UnidadMedida` y `PrecioUnitario` (esos datos ya viven en la factura referenciada).
  - **Solución**: Se eliminó la rama REP separada de `ElectronicDocumentXmlBuilder.BuildDetalle` (que hacía `return` temprano con solo `NumeroLinea` + `Detalle` + `MontoTotal`). Ahora el loop es **único** para todos los tipos: con `bool isRep`, se omiten `CodigoCABYS`, `Cantidad`, `UnidadMedida` y `PrecioUnitario` cuando es REP, y se emiten `Descuento/SubTotal/BaseImponible/Impuesto/ImpuestoNeto/MontoTotalLinea` igual que el resto. Esto agrega el `SubTotal` reclamado y los elementos subsecuentes en orden XSD. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ⚠️ *(Obsoleto — superado por el fix anterior)* **FIX REP `LineaDetalle` — agregado `MontoTotal`** — Corrige rechazo de schema **cvc-complex-type.2.4.b** en Recibo Electrónico de Pago (*"content of element 'LineaDetalle' is not complete. One of 'MontoTotal' is expected"*, fila 1 col 1338):
  - **Causa raíz**: Tras aceptar `<NumeroLinea>` + `<Detalle>`, el validador de Hacienda exige `<MontoTotal>` como siguiente elemento obligatorio de la `LineaDetalle` del REP. La estructura de la línea REP es: `NumeroLinea → Detalle → MontoTotal → ...` (descubierta paso a paso por los rechazos del validador, al no disponer del XSD oficial).
  - **Solución**: `ElectronicDocumentXmlBuilder.BuildDetalle` (rama REP) ahora emite `<MontoTotal>` con el monto total de la línea de la factura referenciada. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ⚠️ *(Obsoleto — superado por el fix de línea completa)* **FIX REP `LineaDetalle` reducida (solo `NumeroLinea` + `Detalle`)** — Corrige rechazo de schema **cvc-complex-type.2.4.a** en Recibo Electrónico de Pago (*"Invalid content... element 'CodigoCABYS'. One of 'Detalle' is expected"*, fila 1 col 1247):
  - **Causa raíz**: El REP usa una `<LineaDetalle>` **mínima**: su XSD solo admite `<NumeroLinea>` y `<Detalle>`. No lleva CAByS, cantidad, unidad, precio, impuestos, descuentos ni totales de línea (esos datos ya viven en la factura referenciada; el REP solo describe el pago). El builder emitía la línea completa (como FE), por lo que Hacienda rechazaba al encontrar `<CodigoCABYS>`.
  - **Solución**: `ElectronicDocumentXmlBuilder.BuildDetalle` ahora tiene una rama para `ReciboElectronicoPago` que emite solo `<NumeroLinea>` + `<Detalle>` por línea. Los demás tipos conservan la línea completa. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ✅ **FIX REP `CondicionVenta` restringida a [09, 11]** — Corrige rechazo de schema **cvc-enumeration-valid** en Recibo Electrónico de Pago (*"Value '01' is not facet-valid with respect to enumeration '[09, 11]'"*, fila 1 col 1175):
  - **Causa raíz**: El REP restringe `<CondicionVenta>` a la enumeración **[09, 11]** (09 = pago de servicios al Estado; 11 = pago de venta a crédito en IVA hasta 90 días), a diferencia del resto de comprobantes que aceptan 01/02/etc. El flujo emitía `01` (Contado) y Hacienda rechazaba.
  - **Solución**: `ElectronicDocumentXmlBuilder.BuildXml` fuerza `<CondicionVenta>` a **`11`** cuando `DocumentType == ReciboElectronicoPago` (documenta el pago de una factura a crédito). Los demás tipos conservan `document.SaleCondition`. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ✅ **FIX REP `Emisor` reducido (sin `Ubicacion`/`Telefono`)** — Corrige rechazo de schema **cvc-complex-type.2.4.a** en Recibo Electrónico de Pago (*"Invalid content... element 'Ubicacion'. One of 'CorreoElectronico' is expected"*, fila 1 col 876):
  - **Causa raíz**: El nodo `<Emisor>` del REP es **reducido** en su XSD: va `Nombre → Identificacion → NombreComercial (opcional) → CorreoElectronico`, sin `<Ubicacion>` ni `<Telefono>`. El builder emitía ambos nodos (como en FE/FEC/NC/ND), por lo que Hacienda rechazaba al encontrar `<Ubicacion>`.
  - **Solución**: `ElectronicDocumentXmlBuilder.BuildEmisor` ahora recibe `documentType` y **omite `Ubicacion` y `Telefono` cuando el tipo es `ReciboElectronicoPago`**. El `Receptor` del REP ya era compatible. Sin errores de compilación (app en depuración; requiere reiniciar para aplicar).
- ✅ **FIX REP `CodigoActividadEmisor`/`CodigoActividadReceptor`** — Corrige rechazo de schema **cvc-complex-type.2.4.a** en Recibo Electrónico de Pago (*"Invalid content... element 'CodigoActividadEmisor'. One of 'NumeroConsecutivo' is expected"*, fila 1 col 646):
  - **Causa raíz**: El builder emitía `<CodigoActividadEmisor>` (y opcionalmente `<CodigoActividadReceptor>`) en la cabecera de TODOS los tipos. Pero el XSD de **ReciboElectronicoPago (REP) v4.4 NO admite** esos nodos: la cabecera del REP va directo de `<ProveedorSistemas>` a `<NumeroConsecutivo>` (misma restricción que el TE con `CodigoActividadReceptor`).
  - **Solución**: `ElectronicDocumentXmlBuilder.BuildXml` ahora omite `<CodigoActividadEmisor>` cuando `DocumentType == ReciboElectronicoPago`, y omite `<CodigoActividadReceptor>` tanto para TE como para REP. Los demás tipos (FE/FEC/NC/ND) mantienen su comportamiento. Build de solución OK.
- ✅ **FIX Precarga REP receptor/moneda/condición** — El botón "Generar recibo" abría la pantalla sin el receptor. Ahora la precarga (`Emit.cshtml` `prefillNoteCredit`) usa los IDs directos `IdCustomerIssuer`/`IdCustomerReceptor` del documento origen (vía `setParty`), con búsqueda por identificación solo como respaldo; además hereda moneda y condición de venta de la factura referenciada (REP forzado a `01` Contado). Build OK.

- ✅ **Fase A** — Catálogo central de códigos IVA.
- ✅ **Fase B** — Campos en `sinai.item`. Script `CMS.Data/Scripts/015_alter_item_add_customer_iva_cabys.sql`. Agrega `id_customer` (NOT NULL DEFAULT 1, FK → sinai.customer), `tax_rate_code` (NOT NULL DEFAULT '08', relación lógica cross-DB → cms.admin.tax_rate_code.code), `cabys_code` (NOT NULL DEFAULT '9799000000000'). Ejecutado OK.
- ✅ **R2** — Actividad económica obligatoria + default global. Script `CMS.Data/Scripts/016_economic_activity_default_and_required.sql`:
  - Parámetro global `default_economic_activity` = `0000.1` en `sinai.global_parameter` (id_menu=204, `/Settings/GlobalParameters`).
  - Backfill de `sinai.supplier.economic_activity` NULL/vacío → `0000.1`.
  - `sinai.supplier.economic_activity` ahora **NOT NULL DEFAULT '0000.1'**.
  - Capa app: `CustomerBillingCredentialController` inyecta `GlobalParameterService` y aplica el default global en Create/Update cuando `EconomicActivity` viene vacío.
  - `ElectronicDocumentXmlBuilder`: fallback de `CodigoActividadEmisor` cambiado de `000000` → `0000.1`. Build OK.
- ✅ **R3** — Régimen especial en emisores. Script `CMS.Data/Scripts/017_add_special_regime_to_billing_credential.sql`:
  - `sinai.customer_billing_credential` + `is_special_regime` (BOOLEAN NOT NULL DEFAULT FALSE) y `special_regime_code` (VARCHAR(20) nullable).
  - Entidad `CustomerBillingCredential`: props `IsSpecialRegime` y `SpecialRegimeCode`.
  - DTOs `BillingIssuerDto` / `UpsertBillingIssuerDto`: `IsSpecialRegime`, `SpecialRegimeCode`.
  - `CustomerBillingCredentialController`: valida en Create/Update que `special_regime_code` sea obligatorio cuando `is_special_regime = true`; persiste ambos campos (limpia el código si se desactiva el régimen).
  - `ElectronicDocumentXmlBuilder.BuildEmisor`: emite `<Registrofiscal8707>` tras `Identificacion` y antes de `NombreComercial` (orden XSD v4.4) cuando el emisor tiene régimen especial.
  - UI `Issuers.cshtml`: checkbox de régimen especial + input condicional del código con toggle y validación cliente. Build OK.
- ✅ **R4** — Múltiples `TotalDesgloseImpuesto` por tarifa. `ElectronicDocumentXmlBuilder`:
  - `BuildResumen` ahora recibe `taxesByLine` y emite **un `TotalDesgloseImpuesto` por cada combinación distinta `(Codigo, CodigoTarifaIVA)`** presente en las líneas, sumando `TaxAmount` por grupo (orden estable por Codigo → CodigoTarifaIVA).
  - Fallback al desglose fijo `01/08` solo si no hay filas de impuesto pero `TotalTaxes > 0`.
  - Call site en `BuildXml` actualizado para pasar `taxesByLine` a `BuildResumen`.
  - `ElectronicDocumentService` ya persiste `TaxRateCode` por línea (desde el input), por lo que el agrupamiento multi-tarifa funciona end-to-end. Build de solución OK.
- ✅ **R6 (Exoneración)** — Documento y líneas exonerables. Script `CMS.Data/Scripts/018_add_exoneration_to_electronic_document.sql`:
  - `sinai.electronic_document` + `is_exonerated`. `sinai.electronic_document_line` + `is_exonerated`, `exon_document_type`, `exon_document_number`, `exon_institution`, `exon_date`, `exon_percent`, `exon_amount`. Ejecutado OK.
  - Entidades `ElectronicDocument` / `ElectronicDocumentLine`: nuevas props de exoneración.
  - DTOs `EmitLineDto` / `EmitDocumentDto` + inputs `EmitLineInput` / `EmitDocumentInput`: `IsExonerated` y campos de exoneración por línea.
  - `ElectronicDocumentService`: calcula `ExonAmount = TaxAmount * ExonPercent/100`, `ImpuestoNeto` y `TotalLine` netos; totales de cabecera (`TotalExonerado`, `TotalServExonerado`, `TotalMercExonerada`) y `TotalTaxes` = suma de IVA neto. Documento exonerado fuerza todas las líneas.
  - `ElectronicDocumentXmlBuilder`: emite bloque `<Exoneracion>` por línea (orden XSD v4.4) y `TotalServExonerado`/`TotalMercExonerada`/`TotalExonerado` en el resumen; desglose multi-tarifa ahora usa el IVA **neto**.
  - `Emit.cshtml`: check global "Documento exonerado", checkbox de exoneración por línea, fila resaltada, total "Exonerado (IVA)", `mapTaxRateCode()` para enviar el `CodigoTarifaIVA` real según el %. Build de solución OK.
- ✅ **Opción 2 (Modal de selección de ítems)** — Completado y build OK:
  - `CMS.Entities/Operational/Item.cs`: mapeadas las columnas de Fase B — `IdCustomer` (`id_customer`), `TaxRateCode` (`tax_rate_code`), `CabysCode` (`cabys_code`).
  - `CMS.API/Controllers/ItemController.cs`: nuevo endpoint `GET api/item/for-billing?customerId=&search=` que devuelve `BillingItemDto` (código, nombre, descripción, precio, IVA %, `TaxRateCode`, `CabysCode`), filtrado por `IsSellable` y opcionalmente por `IdCustomer`.
  - `Emit.cshtml`: botón "Seleccionar ítem", modal `#itemModal` con búsqueda, funciones `searchItems()`/`pickItem()` que prellenan una línea nueva con CAByS, detalle, precio e IVA del ítem; modal movido al `<body>` y auto-búsqueda al abrir.
- ✅ **UI de mantenimiento de ítems** — Completado y build OK:
  - `CMS.API/Controllers/ItemController.cs`: `CreateItemRequest`/`UpdateItemRequest`, `ItemDto` y `MapToDto` extendidos con `IdCustomer`, `TaxRateCode`, `CabysCode`; `CreateItem`/`UpdateItem` aplican defaults (`1`/`08`/`9799000000000`) cuando vienen vacíos.
  - `CMS.Data/Services/ItemService.cs`: `UpdateItemAsync` ahora persiste `IdCustomer`, `TaxRateCode`, `CabysCode`.
  - `CMS.UI/Controllers/InventoryController.cs`: `CreateItemViewModel` (heredado por `EditItemViewModel`) extendido con los 3 campos; se serializan al API automáticamente y el GET los deserializa desde el `ItemDto`.
  - `CreateItem.cshtml` y `EditItem.cshtml`: nueva tarjeta "Facturación Electrónica" con CAByS (13 díg.), selector de Código Tarifa IVA (01–11, default 08) e id_customer; estilo de textos de ayuda para tema oscuro.
- ✅ **FIX Clasificación Bien/Servicio por CAByS** — Corrige rechazos Hacienda **-110** ("carece del monto total de mercancías gravados, pero cuenta con mercancías gravados") y **-111** ("El monto total de servicios gravados no coincide con la suma de los servicios gravados"):
  - **Causa raíz**: la UI no enviaba `isService`, por lo que TODAS las líneas quedaban con el default `IsService=true` y el `ResumenFactura` reportaba todo como `TotalServGravados`, sin `TotalMercanciasGravadas`. Hacienda clasifica cada línea por su CAByS y detectaba la discrepancia.
  - **Solución (backend, fuente de verdad)**: `ElectronicDocumentService` deriva `IsService` del código CAByS con el helper `IsServiceByCabys(cabys, fallback)` según el estándar CAByS-CR: **primer dígito 1-6 = mercancía/bien, 7-9 = servicio**. El valor recibido solo se usa como respaldo si el CAByS es inválido.
  - **Alcance**: aplica a **TODOS** los tipos de comprobante (FE, FEC, NC, ND, TE, REP) porque todos construyen sus líneas por la misma ruta `EmitAsync`. Tanto el desglose de cabecera (`TotalServGravados`/`TotalMercanciasGravadas`) como el `ResumenFactura` del `ElectronicDocumentXmlBuilder` ya leen `line.IsService`, por lo que ambos quedan correctos automáticamente. Build de solución OK.
- ✅ **FIX FEC `ImpuestoAsumidoEmisorFabrica`** — Corrige rechazo de schema **cvc-complex-type.2.4.a** en Factura Electrónica de Compra:
  - **Causa raíz**: `ElectronicDocumentXmlBuilder.BuildDetalle` emitía `<ImpuestoAsumidoEmisorFabrica>` en TODA línea. Ese elemento es válido en FE/TE/NC/ND/REP, pero el XSD de **FacturaElectronicaCompra (FEC) v4.4 NO lo permite**: en FEC la línea pasa directamente de `<Impuesto>` a `<ImpuestoNeto>`. Hacienda rechazaba con *"Invalid content... element 'ImpuestoAsumidoEmisorFabrica'. One of 'Impuesto, ImpuestoNeto' is expected"*.
  - **Solución**: `BuildDetalle` ahora recibe `documentType` y solo agrega `<ImpuestoAsumidoEmisorFabrica>` cuando el tipo **no** es `FacturaCompra` (`allowImpuestoAsumido = documentType != EInvoiceDocumentType.FacturaCompra`). Call site en `BuildXml` actualizado. Build OK.
- ✅ **FIX FEC `InformacionReferencia` obligatoria** — Corrige rechazo de schema **cvc-complex-type.2.4.a** en Factura Electrónica de Compra:
  - **Causa raíz**: El XSD de **FacturaElectronicaCompra (FEC) v4.4** exige **al menos un** bloque `<InformacionReferencia>` (mínimo 1), a diferencia de la FE normal donde es opcional. `BuildReferencias` devolvía `null` cuando no había referencias explícitas, por lo que el XML pasaba directo de `</ResumenFactura>` a `<ds:Signature>`. Hacienda rechazaba con *"Invalid content... element 'Signature'. One of '...InformacionReferencia' is expected"*.
  - **Solución**: `BuildReferencias` ahora recibe el `document` y, cuando el tipo es `FacturaCompra` y no hay referencias explícitas, autogenera una **autorreferencia** apuntando a la propia Clave del comprobante: `TipoDocIR=99` + `TipoDocRefOTRO`, `Numero`=Clave, `FechaEmisionIR`, `Codigo=99` + `CodigoReferenciaOTRO`, `Razon`. Se respeta el orden estricto del XSD (`TipoDocIR → TipoDocRefOTRO → Numero → FechaEmisionIR → Codigo → CodigoReferenciaOTRO → Razon`). Los demás tipos (FE/NC/ND/TE/REP) mantienen el comportamiento original (`null` si no hay referencias). Build OK.
- ✅ **FIX TE `CodigoActividadReceptor`** — Corrige rechazo de schema **cvc-complex-type.2.4.a** en Tiquete Electrónico:
  - **Causa raíz**: El builder emitía `<CodigoActividadReceptor>` cuando el receptor tenía actividad económica, en TODOS los tipos. Pero el XSD de **TiqueteElectronico (TE) v4.4 NO admite** ese nodo: en TE la cabecera va directo de `<CodigoActividadEmisor>` a `<NumeroConsecutivo>`. Hacienda rechazaba con *"Invalid content... element 'CodigoActividadReceptor'. One of 'NumeroConsecutivo' is expected"*.
  - **Solución**: El nodo `<CodigoActividadReceptor>` ahora se omite cuando `document.DocumentType == TiqueteElectronico`. Los demás tipos (FE/FEC/NC/ND/REP) mantienen la emisión cuando el receptor tiene actividad económica. Build OK.
- ✅ **TE sin Receptor (definitivo)** — El Tiquete Electrónico es para consumidor final y su XSD no requiere Receptor. Se eliminó por completo el receptor para TE en toda la ruta:
  - **Backend** (`ElectronicDocumentService.EmitAsync`): `receptorCredential` se fuerza a `null` cuando el tipo es `TiqueteElectronico`, aunque venga un `ReceptorId` en el input. Como consecuencia el `ElectronicDocumentXmlBuilder` no emite `<Receptor>` (ya condicionado a `receptorCredential is null ? null : BuildReceptor(...)`).
  - **UI** (`Emit.cshtml`): nueva función `toggleReceptor()` que oculta el campo Receptor (`#receptorField`) y limpia `#receptorId`/`#receptorDisplay` cuando el tipo es `04` (TE). Se invoca desde `toggleReference()` (al cambiar el tipo) y en el arranque. Además, en `emit()` el `receptorId` se fuerza a `null` para TE. Build OK.
- ✅ **Botón REP (Recibo Electrónico de Pago) en la lista de documentos** — Permite generar un REP a partir de una FE o FEC aceptada:
  - **Contexto fiscal**: El REP (v4.4, obligatorio desde 01-sep-2025) documenta la recepción de un pago (total o parcial) de una factura emitida a crédito bajo régimen de IVA diferido (profesionales/PYMES) o servicios al Estado. No reemplaza la factura, la complementa. Debe referenciar la clave de la factura original. Solo aplica a FE y FEC.
  - **UI Lista** (`Index.cshtml`): nuevo botón `ei-rep-btn` (icono `bi-cash-coin`, "REP") visible **solo** cuando el documento está aceptado por Hacienda y es tipo `01` (FE) u `08` (FEC) con clave. Su handler navega a `Emit?repFrom={id}&refClave=...&refType=...&refDate=...`.
  - **Restricción a crédito (IVA diferido)**: el botón REP ahora exige además que la factura origen sea **a crédito** (`saleCondition === '02'`). Como el REP solo aplica al régimen de IVA diferido (pago diferido de facturas a crédito), no tiene sentido para ventas de contado. Para habilitar esta validación en la lista:
    - Se agregó la propiedad `SaleCondition` a `ElectronicDocumentSummaryDto` (`CMS.Application/DTOs/EInvoice/EInvoiceDtos.cs`).
    - Se incluyó `d.SaleCondition` en la proyección de `GetAll(...)` en `ElectronicInvoiceController.cs` (antes solo lo exponía el endpoint de detalle).
    - La condición de render del botón en `Index.cshtml` requiere ahora: aceptado **+** tipo `01`/`08` **+** clave **+** `saleCondition === '02'`. Build OK.
  - **UI Emit** (`Emit.cshtml`): la función de precarga ahora maneja `repFrom`: selecciona tipo `09` (REP), muestra y precarga la tarjeta de referencia (clave/tipo/fecha de la factura origen), fija `refCode=12` (Comprobante de pago) y razón "Pago de factura a crédito", auto-selecciona emisor/receptor de la factura origen y precarga sus líneas (bloqueadas; ajustables para pagos parciales). Se agregó la opción `12 - Comprobante de pago (REP)` al select `#refCode`.
  - **Backend**: el REP ya estaba soportado en `ElectronicDocumentXmlBuilder` (`DocMeta` → `ReciboElectronicoPago_V4.4.xsd`) y `ValidateBusinessRules` exige referencia obligatoria para REP. No pasa por `ValidateReversalLinesAsync` (exclusivo de NC/ND). Build OK.

- ✅ **FIX PlazoCredito faltante en ventas a crédito** — Corrige rechazo **error -58** de Hacienda (*"El campo denominado 'Plazo del crédito' no posee la estructura establecida para el mismo"*):
  - **Causa raíz**: al emitir una factura con `CondicionVenta = 02` (Crédito), el XSD v4.4 exige el nodo `<PlazoCredito>` justo después de `<CondicionVenta>`. El builder ya lo soportaba (`document.SaleCondition == "02" && document.CreditTerm.HasValue`), pero la UI de emisión (`Emit.cshtml`) **no tenía campo** para capturar el plazo, por lo que `CreditTerm` llegaba `null` y el XML se emitía sin `<PlazoCredito>`.
  - **Solución UI** (`Emit.cshtml`): nuevo campo `#creditTerm` (número de días, default 30) dentro de `#creditTermWrapper`, que se muestra/oculta con `toggleCreditTerm()` según la condición de venta (visible solo para `02`). El payload de `emit()` incluye `creditTerm` y valida que sea > 0 cuando la venta es a crédito.
  - **Solución Backend** (`ElectronicDocumentService.ValidateBusinessRules`): validación de respaldo que lanza excepción si `SaleCondition == "02"` y `CreditTerm` es null o < 1.

- ✅ **FIX cálculo de descuentos en líneas y resumen** — Corrige el bloque de rechazos **-44, -454, -46, -111, -51, -488** (líneas con descuento) y **-518, -476, -45** (Regalía parcial):
  - **Causa raíz #1 (SubTotal de línea)**: el `SubTotal` de cada línea se emitía con el **bruto** (`cantidad × precio`) en lugar del neto. Hacienda v4.4 exige `SubTotal = MontoTotal − MontoDescuento`. Al no restar el descuento, fallaban -44 (subtotal), -454 (base imponible), -46 (total de línea) y en cascada -45/-488.
  - **Solución #1** (`ElectronicDocumentService`): `SubTotal = bd.UnitPriceBase * Quantity − DiscountAmount` (== `BaseImponible` al no haber impuesto selectivo de consumo).
  - **Causa raíz #2 (totales del resumen)**: `TotalServGravados`, `TotalGravado` y `TotalVenta` usaban la base imponible **neta** (post-descuento). Hacienda los calcula sobre el **bruto** (`MontoTotal`), reflejando el descuento solo en `TotalDescuentos`/`TotalVentaNeta`. Por eso -111 esperaba 10419.14 y -51 no cuadraba.
  - **Solución #2** (`ElectronicDocumentXmlBuilder.BuildResumen`): los totales de clasificación se calculan sobre `l.TotalAmount` (bruto). Se agregaron `totalBruto` y `totalDescuentos`; `TotalVenta = totalBruto`, `TotalVentaNeta = totalBruto − totalDescuentos`.
  - **Causa raíz #3 (Regalía/Bonificación)**: los códigos de descuento **01 (Regalía)** y **03 (Bonificación)** exigen que el descuento sea el 100% del `MontoTotal` con tratamiento especial de `ImpuestoAsumidoEmisorFabrica` (-518/-476). No se soportan como descuento parcial.
  - **Solución #3**: se quitó la opción "Regalía (01)" del dropdown de naturaleza en `Emit.cshtml` (quedan 04 Volumen, 05 Temporada, 06 Promoción) y se agregó validación de respaldo en `ValidateBusinessRules` que rechaza códigos `01`/`03`. Sin errores de compilación.

- ✅ **Botón "Consultar" para documentos en estado Pendiente/Contingencia** — Permite verificar el estado real en Hacienda y actualizar el comprobante desde la lista:
  - **Contexto**: cuando el envío queda encolado (respuesta HTTP 429/timeout/duplicado o error transitorio), el documento queda en estado **`Pendiente`** — aún **NO** llegó a Hacienda. Antes solo había un botón "Reprocesar", y el botón "Consultar" (`ei-poll-btn`) aparecía únicamente para `Procesando`/`Enviado`.
  - **Backend** (`ElectronicDocumentService.PollStatusAsync`): ahora maneja también `Pendiente`/`Contingencia`. Si el documento está en esos estados, primero lo **reenvía** vía `SendAndTrackAsync`; si pasa a `Procesando` continúa con la consulta del `ind-estado` a Hacienda; si Hacienda resuelve (aceptado/rechazado) actualiza el documento, parsea la respuesta y cierra reintentos. Todo queda registrado en la bitácora (`CONSULTA`).
  - **UI Lista** (`Index.cshtml`): el botón `ei-poll-btn` ahora también se muestra para `Pendiente`/`Contingencia` (icono `bi-cloud-arrow-up`, texto "Consultar"). El handler llama a `POST /api/electronicinvoice/{id}/poll-status`, muestra spinner, y refresca la lista tanto si se resuelve como si cambia de estado (Pendiente → Procesando). Sin errores de compilación.

- ✅ **Polling en segundo plano (reconciliación de documentos huérfanos)** — Garantiza que TODO documento en estado no terminal se reprocese automáticamente sin intervención del usuario:
  - **Infraestructura existente**: `EInvoiceRetryWorker` (BackgroundService registrado en `CMS.API/Program.cs` vía `AddHostedService`) corre cada **30 s**, recorre las compañías tenant con connection string operacional y procesa su cola `sinai.einvoice_retry_queue`. Para operaciones `poll_status` consulta el `ind-estado` a Hacienda y resuelve aceptado/rechazado; para `send` llama a `ProcessPendingAsync` (que reencola `poll_status` al enviarse). Backoff exponencial (`ScheduleNextAttempt`: 30 s → cap 1 h).
  - **Hueco detectado**: un documento podía quedar en `Pendiente`/`Contingencia`/`Procesando`/`Enviado` **sin** un ítem activo en la cola (reinicio del servidor antes de encolar, fallo en el enqueue, o documentos previos a esta lógica). Esos documentos "huérfanos" **nunca** se reprocesaban automáticamente — solo con el botón manual "Consultar".
  - **Solución** (`EInvoiceRetryWorker.ReconcileOrphanDocumentsAsync`): nueva **barredora de reconciliación** que corre al inicio de cada ciclo por compañía. Busca hasta 50 documentos en estado no terminal (`Pendiente`/`Contingencia`/`Procesando`/`Enviado`) con más de **1 min** de antigüedad (`RecordDate <= now-1min`, para no competir con la emisión sincrónica en curso) que **no** tengan ítem activo en la cola, y los **reencola**: `poll_status` si ya fueron enviados (`Procesando`/`Enviado`, o con `SubmittedAt`+`Clave`), o `send` si aún no llegaron a Hacienda. Con `NextAttemptAt = now` para reproceso inmediato en el mismo ciclo. Build de solución OK.

- ✅ **FIX bloque `<Exoneracion>` con nombres de elementos v4.4** — Corrige rechazo de schema **cvc-complex-type.2.4.a** (*"Invalid content... element 'TipoDocumento'. One of 'TipoDocumentoEX1' is expected"*):
  - **Causa raíz**: `ElectronicDocumentXmlBuilder` emitía el bloque `<Exoneracion>` con los nombres de elementos de **v4.3** (`TipoDocumento`, `FechaEmision`, `PorcentajeExoneracion`) y con `NumeroDocumento`/`NombreInstitucion` **vacíos**. El XSD **v4.4** renombró esos elementos y exige valores no vacíos.
  - **Solución**: el bloque ahora emite el orden y nombres correctos de v4.4: `TipoDocumentoEX1` (+ `TipoDocumentoOTRO` cuando = 99) → `NumeroDocumento` (nunca vacío, default `0`) → `NombreInstitucion` como **código de catálogo** 01–99 (+ `NombreInstitucionOtros` cuando = 99) → `FechaEmisionEX` → `TarifaExonerada` (entero %) → `MontoExoneracion`. Build de solución OK.

- ✅ **UI de captura de datos de exoneración** — Permite ingresar la autorización de exoneración real del cliente desde el formulario de emisión:
  - **UI Emit** (`Emit.cshtml`): nueva tarjeta `#exonCard` "Datos de la exoneración" (oculta por defecto) con: `#exonDocType` (catálogo tipo documento 01–11, 99), `#exonDocNumber` (número de autorización), `#exonInstitution` (catálogo institución 01–12, 99), `#exonDate` (fecha emisión) y `#exonPercent` (% exoneración, default 100). Incluye alerta advirtiendo que los datos deben corresponder a la autorización real.
  - **Visibilidad automática**: `recalc()` muestra la tarjeta **solo cuando hay al menos una línea exonerada** (checkbox de línea o "Documento exonerado"). Al mostrarse por primera vez, `ensureExonDefaults()` preselecciona valores por defecto (tipo `99`, institución `99`, número `0`, fecha de hoy, 100 %), garantizando que **siempre** haya datos válidos cuando el documento lleva exoneración.
  - **Cálculo**: `recalc()` ahora aplica el `#exonPercent` configurado al monto exonerado por línea (antes fijo en 100 %). El total "Exonerado (IVA)" refleja el porcentaje real.
  - **Payload**: `emit()` envía `exonDocumentType`, `exonDocumentNumber`, `exonInstitution`, `exonDate` y `exonPercent` por cada línea exonerada. El backend (`ElectronicDocumentService` → `ElectronicDocumentXmlBuilder`) ya persistía y emitía estos campos en el bloque `<Exoneracion>` v4.4. Build de solución OK.

- ✅ **FIX cálculo de exoneración v4.4 (rechazos -190, -106, -108, -111)** — Corrige la semántica de `MontoExoneracion` y de los totales de clasificación del resumen:
  - **Causa raíz**: se enviaba `MontoExoneracion` = **monto del IVA** (ej. 338.00, 253.50), cuando Hacienda (error **-190**) exige `MontoExoneracion = %exoneración × SubTotal (BaseImponible)` — es decir, la **base exonerada** (2600.00, 1950.00). Además, el resumen clasificaba `TotalServGravados`/`TotalServExonerado`/`TotalExonerado` sobre el **bruto** (`TotalAmount`) en lugar de la **base neta**, provocando la cascada **-106/-108/-111**.
  - **Solución `ElectronicDocumentService.cs`**: se separan dos conceptos antes confundidos en `exonAmount`: `exonBase = BaseImponible × %/100` (→ `MontoExoneracion`) y `exonTax = exonBase × TarifaIVA/100` (IVA realmente exonerado). El IVA neto y el total de línea se derivan de `exonTax`. La entidad `ElectronicDocumentLine.ExonAmount` ahora guarda la **base exonerada**.
  - **Solución `ElectronicDocumentXmlBuilder.BuildResumen`**: las clasificaciones gravadas/exentas usan la **base neta** (`TaxableBase`) y las exoneradas usan `ExonAmount` (base exonerada). Así `TotalGravado + TotalExento + TotalExonerado = TotalVentaNeta`. Verificado con datos reales: 1325 (grav) + 4550 (exon: 2600+1950) = **5875 = TotalVentaNeta**. Build de solución OK.

- ✅ **FIX definitivo exoneración v4.4 (rechazos -290, -46, -54, -487 + -190/-106/-108/-111/-51)** — Corrige la **contradicción** entre dos rechazos consecutivos y establece la fórmula que satisface **todas** las validaciones de Hacienda simultáneamente:
  - **Contradicción detectada**: al poner `MontoExoneracion` = IVA (627.90) fallaba **-190** (Hacienda espera `MontoExoneracion = (TarifaExonerada/100) × SubTotal`); al ponerlo = base exonerada (4830) fallaba **-290** (`ImpuestoNeto = Monto − MontoExoneracion` → 627.90 − 4830 = **−4202.10**, negativo) y **-46** (`MontoTotalLinea ≠ SubTotal + ImpuestoNeto`). El bug de fondo era `TarifaExonerada = 100` (se enviaba el % de exoneración en lugar de la **tarifa efectiva**).
  - **Fórmula correcta (satisface ambas)**: `MontoExoneracion = IVA × %exon` (IVA exonerado) **y** `TarifaExonerada = TarifaIVA% × %exon/100` (tarifa efectiva, p.ej. `13` para exoneración total de un IVA 13%). Verificación: −190 → (13/100)×4830 = 627.90 ✓; −290 → 627.90 − 627.90 = 0 ✓; −46 → 4830 + 0 = 4830 ✓.
  - **Solución `ElectronicDocumentService.cs`**: `exonTax = TaxAmount × %exon/100` (→ `ExonAmount` = IVA exonerado = `MontoExoneracion`); `ImpuestoNeto = TaxAmount − exonTax`; `TotalLine = TotalLine − exonTax`.
  - **Solución `ElectronicDocumentXmlBuilder`**: `TarifaExonerada = tax.TaxRate × %exon/100` (tarifa efectiva, 2 decimales); la clasificación exonerada del `ResumenFactura` vuelve a usar `TaxableBase` (base) porque `ExonAmount` ahora es el IVA. Errores -54/-487 quedan resueltos porque `ImpuestoNeto` de líneas exoneradas vuelve a 0 y `TotalDesgloseImpuesto` solo suma el IVA neto real. Verificado con datos reales: `TotalGravado`(2450) + `TotalExonerado`(11150) = **13600 = TotalVentaNeta** ✓. Build de solución OK.

- ✅ **FIX totales de clasificación = BRUTO (rechazos -111, -106, -108, -51 con descuento + exoneración)** — Regla confirmada contra un XML **ACEPTADO** por Hacienda y los montos exactos que la validación reportó:
  - **Regla definitiva**: los totales de CLASIFICACIÓN del `ResumenFactura` (`TotalServGravados`, `TotalServExonerado`, `TotalMercanciasGravadas`, `TotalExonerado`, `TotalGravado`, `TotalVenta`) se calculan sobre el **BRUTO por línea** (`MontoTotal` = cantidad × precio, **antes** del descuento), **NO** sobre la base neta ni sobre el IVA. El descuento se refleja **únicamente** en `TotalDescuentos`, y `TotalVentaNeta = TotalVenta − TotalDescuentos`.
  - **Evidencia**: Hacienda reportó -111 esperando `TotalServGravados` = **2500** (se envió 2450 = base neta) y -106/-108 esperando exonerado = **6800** = 6400+400 bruto (se envió 6700). -51 esperaba `TotalVenta` = 2500+6800 = **9300**. Todos coinciden con el bruto.
  - **Solución** (`ElectronicDocumentXmlBuilder.BuildResumen` **y** los campos persistidos en `ElectronicDocumentService`): las clasificaciones gravado/exento/exonerado usan `TotalAmount` (bruto). `TotalVenta = Σ MontoTotal`; `TotalVentaNeta = TotalVenta − TotalDescuentos`. El `MontoExoneracion` por línea (IVA exonerado) y el `TarifaExonerada` (tarifa efectiva) se mantienen del fix anterior. Verificado: 2500 (grav) + 6800 (exon) = **9300 = TotalVenta**, 9300 − 150 = **9150 = TotalVentaNeta** ✓. Build de solución OK.
### Decisiones confirmadas por el usuario
- "CodigoComercial" del mensaje = campo **`economic_activity`** (R2). El nodo `<CodigoComercial>` de producto por línea queda **diferido**.
- Código IVA por defecto = **`08`** (13% tarifa general).
- `sinai.item.id_customer` = **NOT NULL DEFAULT 1** (con FK real a `sinai.customer`, que ya tiene `id_customer = 1`).

---

> **Estado histórico inicial**: PENDIENTE DE EJECUCIÓN (documento de requerimiento persistente)
> **Autor original del requerimiento**: Ernesto Martínez (BITI Solutions)
> **Creado**: 2026 — durante la implementación del Tiquete Electrónico
> **Alcance**: CMS.API, CMS.Application, CMS.Data, CMS.Entities, CMS.UI + BD `cms` (central) y BD `sinai` (operacional)
> **Regla de oro del proyecto**: BD central `cms` (schema `admin`) = seguridad/config; BD compañía (`sinai`) = operacional. Ver `.github/copilot-instructions.md`.

---

## 0. Contexto y motivación

Durante la programación del **Tiquete Electrónico** se detectó, comparando contra un XML **ACEPTADO** por Hacienda (Walmart Heredia, clave `50628032600310200722314900003040000068065100000000`), que el generador de XML tenía bugs de orden que ya se corrigieron:

- ✅ **Corregido** — orden nodo `Descuento`: `MontoDescuento → CodigoDescuento → NaturalezaDescuento`.
- ✅ **Corregido** — orden en `ResumenFactura`: `TotalVenta → TotalDescuentos → TotalVentaNeta`.

Quedaron pendientes 5 mejoras estructurales (este documento) que son **indispensables** para emitir correctamente comprobantes con productos reales, tarifas mixtas y exoneración.

### Archivo clave del generador XML
`CMS.Data/Services/EInvoice/ElectronicDocumentXmlBuilder.cs` — métodos `BuildDetalle`, `BuildResumen`, `BuildEmisor`.

---

## 1. Requerimientos (lo que pidió el usuario, textual e interpretado)

### R1 — (ACLARADO) = R2. "CodigoComercial" en el mensaje del usuario se refería al campo **`economic_activity`**. El nodo `<CodigoComercial>` de producto por línea queda **DIFERIDO / fuera de alcance** de este ciclo.

### R2 — Código de Actividad Económica (CodigoActividad) obligatorio + default global
- El campo `economic_activity` debe ser **obligatorio en `sinai.supplier`** (igual que en `sinai.customer_billing_credential`).
- Los registros con `NULL` → poner `0000.1`.
- En los **mantenimientos** (formularios), si el usuario no lo digita → default `0000.1`.
- Ese default debe existir como **parámetro global** en `/Settings/GlobalParameters`.
- Para `sinai` el valor por defecto es **`0000.1`**.
- El sistema debe **leer el default desde el parámetro global**.

> ⚠️ **NOTA DE INTERPRETACIÓN**: El usuario escribió "CodigoComercial" pero describió el campo `economic_activity` con formato `0000.1` (que es un **Código de Actividad**, no un código de producto). Por eso este plan trata R1 (CodigoComercial de producto por línea) y R2 (Código de Actividad económica por defecto) como **dos requerimientos distintos**. **VALIDAR con el usuario antes de ejecutar** si ambos son correctos.

### R3 — Régimen Especial / `Registrofiscal8707`
- Agregar a los **emisores** (`sinai.customer_billing_credential`) un campo **boolean** `is_special_regime` (régimen especial).
- Si `is_special_regime = true` → solicitar como **obligatorio** el código de régimen especial (`special_regime_code`, que va en `<Registrofiscal8707>`).
- Al **emitir cualquier documento electrónico**: si el emisor tiene `is_special_regime = true`, incluir siempre `<Registrofiscal8707>{special_regime_code}</Registrofiscal8707>` en el `<Emisor>` (después de `Identificacion`, antes de `NombreComercial` — orden del XSD v4.4).

### R4 — Múltiples `TotalDesgloseImpuesto` por tarifa (INDISPENSABLE)
- Adaptar **todos los documentos** y el sistema para permitir **varios `TotalDesgloseImpuesto`**, uno por cada `CodigoTarifaIVA` presente en las líneas (hoy se emite **uno fijo con código `08`**, lo cual es incorrecto con tarifas mixtas).
- El **código de IVA** (CodigoTarifaIVA) debe estar a nivel de **`sinai.item`**.
- **NO usar `sinai.item.tax_rate`** (ese es el **porcentaje**, no el código).
- Crear una **NUEVA tabla catálogo en la BD central `cms`** (schema `admin`) con el **100% de los códigos de IVA de Hacienda**, referenciada lógicamente (cross-DB, sin FK real) desde `sinai.item`.
- Requisito previo: agregar `id_customer` a `sinai.item` **NOT NULL DEFAULT 1** y FK con `sinai.customer`.
  - ⚠️ **Muchas pantallas/procesos ya usan `sinai.item`** → el DEFAULT 1 garantiza que los INSERT existentes sigan funcionando.
- Agregar columna de **código IVA** a `sinai.item`, **obligatoria**, con **default = `08` (13%)** cuando no se indique al crear el ítem.
- Los documentos electrónicos deben **tomar el CodigoTarifaIVA desde `sinai.item`**.

### R5 — CAByS a nivel de `sinai.item` + modal de selección
- Agregar columna **CAByS** a `sinai.item`.
- Si el registro se ingresa con dato → usarlo; **default = `9799000000000`** para `sinai`.
- Ese default también como **parámetro global** (para que el sistema lo tome de ahí).
- En la **emisión de documentos**: en lugar de digitar el CAByS, abrir un **modal NUEVO** que despliegue los **ítems del cliente previamente seleccionado** (`sinai.item` filtrado por `id_customer`), y el sistema tome el **CAByS asignado a ese ítem**.

### R6 — Exoneración
- Dejar configurado para poder usar exoneración.
- Agregar al emitir la factura un **check "Documento exonerado"**:
  - Si **marcado** → todas las líneas van **sin impuesto** (exoneradas).
  - Si **desmarcado** → el usuario puede exonerar **línea por línea** (solo algunas).
- Emitir correctamente `TotalServExonerado`, `TotalMercExonerada`, `TotalExonerado` y el bloque `<Exoneracion>` por línea en el XML.

### R7 — Catálogo central de códigos IVA (soporte de R4)
- Nueva tabla en `cms.admin` (ej. `admin.tax_rate_code` o `admin.iva_code`) con todos los códigos oficiales v4.4:
  - `01` = 0% (tarifa exenta / bienes de canasta básica según versión)
  - `02` = 1%
  - `03` = 2%
  - `04` = 4%
  - `05` = 0% (transitorio)
  - `06` = 0% (sin derecho a crédito)
  - `07` = 0% (bienes/servicios exentos)
  - `08` = 13% (tarifa general)
  - `10` = 0% (reducido)
  - `11` = 0.5% (transitorio)
  - ⚠️ **VALIDAR la lista completa y los porcentajes contra el Anexo v4.4 vigente** antes de sembrar datos.

---

## 2. Modelo de datos objetivo

### 2.1 BD central `cms` — schema `admin`
**Nueva tabla `admin.tax_rate_code`** (catálogo global de códigos de IVA Hacienda):
```
id_tax_rate_code   SERIAL PK
code               VARCHAR(2)  NOT NULL UNIQUE   -- CodigoTarifaIVA v4.4 (01..13)
name               VARCHAR(100) NOT NULL         -- Descripción
rate_percent       NUMERIC(5,2) NOT NULL         -- Porcentaje (ej. 13.00, 0.00)
is_exempt          BOOLEAN NOT NULL DEFAULT false -- true para tarifas 0%
is_active          BOOLEAN NOT NULL DEFAULT true
sort_order         INTEGER NOT NULL DEFAULT 0
+ columnas de auditoría estándar del proyecto (createdate, record_date, created_by, updated_by, rowpointer)
```
> Sigue el estándar de scripts SQL de `.github/copilot-instructions.md` (bloque START/END, índices, comentarios, permisos, trigger de auditoría).

### 2.2 BD compañía `sinai` — cambios en tablas operacionales

**`sinai.item`** (agregar columnas):
```
id_customer            INTEGER NOT NULL DEFAULT 1   -- backfill = 1; FK real → sinai.customer.id_customer (mismo schema)
tax_rate_code          VARCHAR(2) NOT NULL DEFAULT '08'  -- CodigoTarifaIVA; default = 08 (13% tarifa general). Relación lógica CROSS-DB → cms.admin.tax_rate_code.code
cabys_code             VARCHAR(13) NOT NULL DEFAULT '9799000000000'  -- default desde parámetro global
```
> ⚠️ `tax_rate` (numeric, porcentaje) **se conserva** para no romper pantallas; el nuevo `tax_rate_code` es el que usan los documentos electrónicos.
> ⚠️ El default de `cabys_code` debe leerse del parámetro global al insertar desde la app (no depender solo del DEFAULT SQL).

**`sinai.supplier`** (modificar):
```
economic_activity   VARCHAR(6) NOT NULL DEFAULT '0000.1'  -- volver obligatorio; backfill NULLs → '0000.1'
```

**`sinai.customer_billing_credential`** (agregar):
```
is_special_regime    BOOLEAN NOT NULL DEFAULT false
special_regime_code  VARCHAR(20)   -- Registrofiscal8707; obligatorio en app cuando is_special_regime = true
```

**`sinai.electronic_document_line`** (agregar, para soportar exoneración y desglose por tarifa):
```
is_exonerated        BOOLEAN NOT NULL DEFAULT false
tax_rate_code        VARCHAR(2)     -- código IVA congelado al emitir (copiado del item)
commercial_code      VARCHAR(20)    -- CodigoComercial (código del producto)
commercial_code_type VARCHAR(2)     -- Tipo del CodigoComercial (default '04')
-- (opcional) campos de exoneración: exon_document_type, exon_document_number, exon_institution, exon_date, exon_percent, exon_amount
```

**`sinai.electronic_document`** (agregar):
```
is_exonerated        BOOLEAN NOT NULL DEFAULT false  -- documento exonerado completo
```

### 2.3 Parámetros globales (`sinai.global_parameter`)
> La tabla ya existe. Usa `id_menu` (referencia lógica a `cms.admin.menu.id_menu`), `data_type`, `value_string`/`value_*`, `default_value`. La UI vive en `/Settings/GlobalParameters` (confirmar `id_menu` correcto para "Settings/GlobalParameters" — buscar en `admin.menu`).

Insertar parámetros (category `EInvoice`):
```
code = 'default_economic_activity_code'  data_type='string'  value_string='0000.1'  default_value='0000.1'
code = 'default_cabys_code'              data_type='string'  value_string='9799000000000' default_value='9799000000000'
code = 'default_tax_rate_code'           data_type='string'  value_string='08'  default_value='08'
code = 'default_commercial_code_type'    data_type='string'  value_string='04'  default_value='04'
```

---

## 3. Pasos de ejecución (orden recomendado)

> Cada paso de BD = un script SQL numerado en `CMS.Data/Scripts/` siguiendo el estándar del proyecto. Ejecutar con las credenciales de `.github/copilot-instructions.md` (`postgres`/`POStgres2026`, host `10.0.0.1`). Los cambios en BD central van a `cms`; los operacionales a `sinai`.

### Fase A — Catálogo central de IVA (BD `cms`)
1. **Script** `013_create_tax_rate_code_catalog.sql` (BD `cms`, schema `admin`): crear `admin.tax_rate_code` + sembrar el 100% de códigos v4.4 (validar lista/porcentajes).
2. Crear entidad `CMS.Entities` para el catálogo + configuración EF Core en el DbContext **central** (no el de compañía).
3. Endpoint API de solo lectura para listar códigos IVA (para dropdowns en mantenimiento de ítems).

### Fase B — Parámetros globales
4. **Script** `014_seed_einvoice_global_parameters.sql` (BD `sinai`): insertar los 4 parámetros (default_economic_activity_code, default_cabys_code, default_tax_rate_code, default_commercial_code_type). Resolver `id_menu` de Settings/GlobalParameters.
5. Servicio/helper para **leer parámetros globales** por `code` (si no existe ya). Buscar `GlobalParameter` en `CMS.Data`/`CMS.Application` antes de crear uno nuevo.

### Fase C — Cambios en `sinai.item` (CUIDADO: tabla muy usada)
6. **Script** `015_alter_item_add_customer_iva_cabys.sql` (BD `sinai`):
   - `ADD COLUMN id_customer INTEGER NOT NULL DEFAULT 1;` (backfill automático por el DEFAULT).
   - `ADD COLUMN tax_rate_code VARCHAR(2) NOT NULL DEFAULT '08';`
   - `ADD COLUMN cabys_code VARCHAR(13) NOT NULL DEFAULT '9799000000000';`
   - FK `id_customer → sinai.customer(id_customer)` (verificar que exista customer id=1 antes).
   - Índices: `ix_sinai_item_customer`, `ix_sinai_item_cabys`.
7. Actualizar entidad `CMS.Entities` de `Item` + EF Core config (nuevas columnas).
8. Actualizar **mantenimiento de ítems** (UI + API):
   - Dropdown de **CodigoTarifaIVA** (desde catálogo central); obligatorio; default `01` (0%).
   - Campo **CAByS**; si vacío → tomar `default_cabys_code` del parámetro global.
   - Campo/selección de **cliente** (`id_customer`).
   - ⚠️ Verificar TODAS las pantallas/queries que usan `sinai.item` para no romperlas (buscar referencias en el código).

### Fase D — `sinai.supplier` actividad económica
9. **Script** `016_alter_supplier_economic_activity_required.sql` (BD `sinai`): backfill NULL → `0000.1`; `ALTER COLUMN economic_activity SET NOT NULL SET DEFAULT '0000.1'`.
10. Mantenimiento de supplier (UI + API): campo obligatorio; default desde `default_economic_activity_code`.

### Fase E — Régimen especial en emisores
11. **Script** `017_alter_billing_credential_special_regime.sql` (BD `sinai`): `ADD is_special_regime BOOLEAN NOT NULL DEFAULT false`, `ADD special_regime_code VARCHAR(20)`.
12. Entidad `CustomerBillingCredential` + EF config: nuevos campos.
13. Mantenimiento de emisores (UI + API): checkbox régimen especial; cuando true, `special_regime_code` obligatorio (validación cliente y servidor).

### Fase F — Exoneración (esquema documento)
14. **Script** `018_alter_electronic_document_exoneration.sql` (BD `sinai`): agregar `is_exonerated` a `electronic_document` y a `electronic_document_line` (+ `tax_rate_code`, `commercial_code`, `commercial_code_type`, campos de exoneración por línea).
15. Entidades `ElectronicDocument` / `ElectronicDocumentLine` + EF config.

### Fase G — Generador XML (`ElectronicDocumentXmlBuilder.cs`) — el núcleo
16. **`BuildEmisor`**: emitir `<Registrofiscal8707>` cuando `is_special_regime = true` (orden XSD: tras `Identificacion`, antes de `NombreComercial`).
17. **`BuildDetalle`** (por línea):
	- Emitir `<CodigoComercial><Tipo>{commercial_code_type}</Tipo><Codigo>{commercial_code}</Codigo></CodigoComercial>` (tras `CodigoCABYS`).
	- Usar `CodigoTarifaIVA` real de la línea (desde item), no fijo `08`.
	- Si línea exonerada: `Tarifa`/`Monto` de impuesto en 0 y agregar bloque `<Exoneracion>` (si aplica) + `<MontoTotalLinea>` correcto.
18. **`BuildResumen`** — reescribir el desglose:
	- Calcular montos por naturaleza: `TotalServGravados`, `TotalServExentos`, `TotalServExonerado`, `TotalMercanciasGravadas`, `TotalMercanciasExentas`, `TotalMercExonerada`.
	- `TotalGravado`, `TotalExento`, `TotalExonerado`.
	- **Múltiples `<TotalDesgloseImpuesto>`**: agrupar líneas por `CodigoTarifaIVA` y emitir uno por cada grupo con `Codigo=01`, `CodigoTarifaIVA={grupo}`, `TotalMontoImpuesto={suma}`.
	- Respetar orden XSD v4.4 verificado contra el ejemplo aceptado.

### Fase H — Emisión: modal CAByS por ítem + check exoneración (UI)
19. `Emit.cshtml`: nuevo **modal de selección de ítem** filtrado por el cliente (`id_customer`) previamente seleccionado; al elegir ítem, autocompletar CAByS, CodigoComercial, CodigoTarifaIVA, detalle y precio desde `sinai.item`.
20. `Emit.cshtml`: **check "Documento exonerado"** global; si marcado, todas las líneas exoneradas; si no, permitir exonerar línea por línea.
21. Endpoint API para listar ítems por cliente (para el modal).
22. `ElectronicDocumentService.EmitAsync`: mapear exoneración, `tax_rate_code`, `commercial_code` a las líneas persistidas; validar coherencia.

### Fase I — Validación end-to-end
23. Recompilar solución (`run_build`).
24. Emitir un **Tiquete Electrónico** de prueba con: producto con CAByS/IVA reales, descuento, tarifas mixtas (13% + 0%) y validar contra XSD.
25. Emitir una **Factura exonerada** completa y otra con exoneración parcial (algunas líneas).
26. Confirmar aceptación en sandbox Hacienda.

---

## 4. Decisiones (CONFIRMADAS por el usuario)

1. ✅ **R1 = R2**: cuando el usuario escribió "CodigoComercial" se refería al campo **`economic_activity`**. NO es un requerimiento separado de nodo de producto por línea. El nodo `<CodigoComercial>` de producto por línea queda **fuera de alcance** de este ciclo (diferido; puede retomarse luego si Hacienda lo exige).
2. ✅ **Código IVA por defecto**: **`08`** (13% — tarifa general). Es el default de `sinai.item.tax_rate_code` cuando no se indique.
3. ✅ **`id_customer` en `sinai.item`**: **NOT NULL** con **DEFAULT = 1** a nivel de tabla; backfill de todos los registros existentes a `1`.
4. ⚠️ **Lista oficial de códigos IVA v4.4**: validar contra el anexo vigente de Hacienda antes de sembrar (pendiente técnico, no bloquea).
5. ⚠️ **`id_menu` de Settings/GlobalParameters**: resolver el id real en `cms.admin.menu` al insertar parámetros.
6. ✅ **Ubicación tabla catálogo IVA**: `cms.admin` (central), NO en `sinai`.
7. ✅ **Resto de requerimientos**: aprobados por el usuario.

---

## 5. Estado de avance (marcar conforme se ejecuta)

- [ ] Fase A — Catálogo central IVA (`cms.admin.tax_rate_code`)
- [ ] Fase B — Parámetros globales
- [ ] Fase C — `sinai.item` (id_customer, tax_rate_code, cabys_code) + mantenimiento
- [ ] Fase D — `sinai.supplier` economic_activity obligatorio
- [ ] Fase E — Régimen especial en emisores
- [ ] Fase F — Esquema exoneración en documento/líneas
- [ ] Fase G — Generador XML (CodigoComercial, Registrofiscal8707, múltiples desgloses, exoneración)
- [ ] Fase H — UI emisión (modal CAByS por ítem + check exoneración)
- [ ] Fase I — Validación end-to-end + aceptación Hacienda

> **Ya corregido antes de este plan**: orden `Descuento` y orden `TotalDescuentos` en `ElectronicDocumentXmlBuilder.cs`.
