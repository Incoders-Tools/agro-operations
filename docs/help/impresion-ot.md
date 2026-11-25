---
screen: impresion-ot
title: Impresión de la OT
summary: Hoja imprimible de una orden de trabajo para guardarla como PDF y enviarla al contratista.
video:
reviewed: 2026-11-25
related: [ordenes-de-trabajo, personal, insumos]
---

# Impresión de la OT

## Para qué sirve

Muestra una OT emitida como una hoja A4 con las mismas secciones que los PDF que ya usaban los ingenieros, con el estilo
del sistema. Desde aquí la guardas como PDF con el diálogo de impresión del navegador.

## Conceptos

Secciones de la hoja, de arriba hacia abajo:

| Sección | Contenido |
| --- | --- |
| Encabezado | "ORDEN DE TRABAJO" y la marca del producto. |
| Firma comercial | Nombre, CUIT si lo tiene y el cliente cuando su nombre difiere del de la firma. |
| Orden Nº y Fecha | Número de la OT en su firma y fecha de emisión (dd/mm/aaaa). |
| Labor a realizar y Etapa | Labor con su abreviatura; etapa o "Sin indicar". |
| Tabla de lotes | Campo, lote (con su código), Has (superficie del lote al emitir), Has tarea (ha a trabajar) y el total a trabajar. |
| Contratista | Nombre y "Equipo propio" cuando corresponde. |
| Cultivo / Rotación | Cultivo, variedad o híbrido y campaña. |
| Insumos a utilizar | Producto, dosis por ha y total. Si no hay: "Esta orden no lleva insumos." |
| Observaciones | Texto de la OT o "Sin observaciones." |
| Pie | "NO MODIFICAR A MANO ESTA ORDEN.", el ingeniero responsable (nombre, título, matrícula y teléfono) y la línea "Firma responsable actuante". |

**Producto**: se imprime el nombre comercial con su formulación y "orden de mezcla N". Si el nombre ya termina con el
orden y la formulación entre paréntesis, como en la planilla (por ejemplo "Producto A (3 SC)"), no se repiten.

**Cono hueco**: los insumos que lo requieren llevan la marca "Cono hueco" y una nota al pie de la tabla.

**Cantidades**: con coma decimal y punto de miles, hasta 4 decimales (por ejemplo 1.234,5). Los litros se muestran
como `L` y `L/ha`.

**OT cancelada**: la hoja lleva la marca **ANULADA** y el motivo de la cancelación.

## Cómo se hace

1. En Órdenes de trabajo, pulsa **Imprimir / PDF** en el listado o en el detalle. La hoja se abre en otra pestaña.
2. Pulsa **Descargar PDF**. Se abre el diálogo de impresión del navegador.
3. Elige como destino **Guardar como PDF**. El nombre del archivo ya viene propuesto con el patrón de la planilla.
4. Guarda el archivo y envíalo al contratista.
5. Pulsa **Volver** para regresar al backoffice.

## Reglas y validaciones

- La hoja no muestra el encabezado ni el pie del navegador (dirección de la página, fecha, título ni números de
  página).
- La hoja es de solo lectura. Para corregir algo, edita la OT (solo si está emitida) y vuelve a imprimir.
- Si la sesión terminó: "Tu sesión terminó o aún no ingresaste. Ingresa al backoffice y vuelve a abrir esta orden."
- Si la OT no existe: "El registro relacionado ya no existe. Actualiza los listados."

## Preguntas abiertas

- Q7: firma escaneada del ingeniero responsable ([preguntas](../engineer-questions.md)).
- Q8: imagen del mapa con la zona a tratar.
- Q9: teléfonos de contacto del pie: fijos de la empresa o los del ingeniero de cada OT.
- Q10: advertencia por evento genético del cultivo.
- Q16: manual de marca, logo y paleta para el documento.

## Notas técnicas

- Ruta de la vista: `/backoffice/work-orders/{id}/print`. Datos: `GET /api/v1/work-orders/{id}/document`, que devuelve
  los nombres ya resueltos, el total de ha y `fileName` (el nombre propuesto, usado como título de la página).
- El PDF lo genera el navegador; no hay generación de PDF en el servidor (decisión L4 de
  `odd/tasks/ot-client-data-pdf.md`).
- El margen de página en cero oculta el encabezado y el pie del navegador. Colores y tipografías salen de las variables
  CSS de marca, de modo que un cambio de marca es un solo cambio.
- Código: `web/app/backoffice/print.jsx`, `print.css`; nombre de archivo en `WorkOrderDocument.FileName`
  (`src/AgroOperations.Api/Domain/WorkOrderService.cs`).
