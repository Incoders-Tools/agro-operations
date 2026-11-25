---
screen: comparacion-de-presupuestos
title: Comparación de presupuestos
summary: Compara presupuestos de varios proveedores para un cliente, producto por producto, y registra cuál conviene y por qué.
video:
reviewed: 2026-11-25
related: [clientes, proveedores, insumos]
---

# Comparación de presupuestos

## Para qué sirve

Cuando un cliente recibe presupuestos de distintos proveedores, aquí los cargas, los comparas producto por producto y
dejas registrada la elección con su motivo. La comparación queda guardada para consultarla después desde el cliente.

## Conceptos

**Comparación**

| Campo | Significado y reglas |
| --- | --- |
| Cliente | Obligatorio. Solo al crear. |
| Título | Obligatorio, hasta 200 caracteres. Qué se cotiza, por ejemplo "Herbicidas barbecho 2026". |
| Fecha de solicitud | Obligatoria. |
| Estado | **Abierta** (se puede cambiar) o **Decidida** (solo lectura). |
| Presupuesto elegido y motivo | Se completan al decidir. |

**Presupuesto**: lo que cotizó un proveedor.

| Campo | Significado y reglas |
| --- | --- |
| Proveedor | Obligatorio. Solo proveedores de tipo Insumos. |
| Fecha de recepción | Obligatoria. |
| Moneda | ARS o USD. Un presupuesto tiene una sola moneda. |
| Notas | Opcional. Condiciones, plazo de pago, flete, IVA. |
| Productos cotizados | Líneas con un insumo del catálogo o un texto libre, cantidad, unidad y precio unitario. |

**Línea**: cantidad mayor que cero; precio unitario cero o positivo; total de la línea = cantidad × precio unitario,
hasta 4 decimales. Con un insumo del catálogo la unidad es opcional (toma la del insumo); con texto libre es
obligatoria.

**Tabla de comparación**: una fila por producto y una columna por presupuesto. Un mismo insumo del catálogo es el mismo
producto en todos los presupuestos; un texto libre se agrupa por el texto, sin distinguir mayúsculas. En cada moneda se
marca "Más barato en ARS" o "Más barato en USD" el precio unitario más bajo; ante un empate gana el presupuesto recibido
antes. Las monedas nunca se convierten: si hay presupuestos en ARS y USD, la pantalla lo avisa.

## Cómo se hace

**Crear una comparación**

1. Pulsa **Nueva comparación**.
2. Elige el cliente, escribe el título y la fecha de solicitud.
3. Pulsa **Crear comparación**. Se abre su detalle.

**Cargar un presupuesto**

1. En el detalle, pulsa **Agregar presupuesto**.
2. Elige proveedor, fecha de recepción y moneda.
3. En cada línea elige **Insumo del catálogo** o **Texto libre**, y completa cantidad, unidad y precio unitario.
4. Pulsa **Agregar producto** para otra línea; el cursor pasa a la búsqueda del insumo.
5. Revisa el **Total del presupuesto** y pulsa **Agregar presupuesto**.

**Decidir**

1. Revisa la **Comparación por producto**.
2. En **Decidir**, elige el **Presupuesto elegido** y escribe el **Motivo de la elección** (precio, plazo, entrega,
   financiación).
3. Pulsa **Decidir**. La comparación pasa a Decidida.

**Reabrir una comparación decidida**

1. Pulsa **Reabrir**.
2. Cambia los presupuestos y vuelve a decidir. La elección y el motivo anteriores se proponen hasta la nueva decisión.

**Ver las comparaciones de un cliente**

1. Filtra por **Cliente** y **Estado** en el listado, o entra desde la ficha del cliente.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Título vacío | El título es obligatorio. |
| Sin fecha de solicitud o de recepción | Indica la fecha de solicitud. / Indica la fecha de recepción del presupuesto. |
| Moneda inválida | Elige la moneda: ARS o USD. |
| Cantidad en cero o negativa | La cantidad de cada línea debe ser mayor que cero. |
| Precio unitario negativo | El precio unitario debe ser cero o positivo. |
| Línea sin insumo ni texto | Cada línea necesita un insumo o un producto. |
| Decidir sin motivo | Indica el motivo de la elección. |
| Cambiar una comparación decidida | Esta comparación ya está decidida. Reábrela para cambiarla. |
| Reabrir una que ya está abierta | Esta comparación ya está abierta. Actualiza la vista. |

- Las líneas totalmente vacías se ignoran al guardar.
- Un presupuesto decidido queda de solo lectura hasta reabrir la comparación.

## Preguntas abiertas

- Q19: si se compara con o sin IVA, en ARS o USD, y si hace falta el tipo de cambio del día para comparar monedas
  distintas ([preguntas](../engineer-questions.md)).

## Notas técnicas

- Rutas: `GET /quote-comparisons?clientId=&status=`, `GET /quote-comparisons/{id}` (comparación, presupuestos y
  productos con `cheapestQuoteIdByCurrency`), `POST /quote-comparisons`, `PUT /quote-comparisons/{id}` (solo abierta),
  `POST /quote-comparisons/{id}/decide`, `POST /quote-comparisons/{id}/reopen`,
  `POST /quote-comparisons/{id}/quotes`, `PUT /quotes/{id}` (reemplazo completo con líneas, solo abierta).
- Clave de producto: `input:<inputId>` o `text:<texto en minúsculas>`.
- Tablas: `quote_comparisons`, `quotes`, `quote_lines`. Migración `011_providers_and_quotes.sql`.
- Código: `web/app/backoffice/quotes.jsx`, `providers.mjs` (`comparisonTable`); dominio `QuoteComparisonService.cs`.
