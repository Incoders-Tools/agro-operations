---
screen: ordenes-de-trabajo
title: Órdenes de trabajo
summary: Alta, búsqueda, edición, cierre e impresión de las órdenes de trabajo (OT), numeradas por firma comercial.
video:
reviewed: 2026-11-25
related: [impresion-ot, clientes, lotes, labores, etapas, contratistas, insumos, personal, campanas, cultivos]
---

# Órdenes de trabajo

## Para qué sirve

Una orden de trabajo (OT) le indica a un contratista qué labor hacer, en qué lotes, con qué insumos y en qué dosis. Desde
esta pantalla emites la OT, la buscas, la corriges mientras está emitida, la marcas como realizada o la cancelas, y
abres su versión imprimible para guardarla en PDF y enviarla al contratista.

## Conceptos

**Orden de trabajo (OT)**: una OT es para **una firma comercial** (y por lo tanto un cliente), **una labor**, **un
contratista** y **uno o más lotes** de uno o más campos de ese cliente. Los insumos son opcionales y se aplican a todos
los lotes de la OT.

| Campo | Significado y reglas |
| --- | --- |
| Cliente | Solo en el alta. Filtra las firmas y los lotes que se pueden elegir. |
| Firma comercial | Nombre con el que se emite la OT. Si el cliente tiene una sola firma, queda elegida. No cambia al editar. |
| Nº | Lo asigna el sistema al emitir: el próximo número de la firma. No se elige ni se cambia. |
| Fecha | Obligatoria. Por defecto, la fecha del día. |
| Labor | Obligatoria y única. La lista se agrupa por categoría (Aplicación, Labranza y preparación, etc.). |
| Etapa | Opcional. Momento del ciclo del cultivo (por ejemplo Presiembra). |
| Contratista | Obligatorio y único. |
| Campos y lotes | Al menos un lote. Puedes marcar lotes de varios campos del mismo cliente. |
| ha a trabajar | Por lote, obligatoria. Empieza con la superficie del lote; puede ser menor o mayor. |
| Campaña | Opcional. |
| Cultivo, Variedad o híbrido | Opcionales. Se completan solos en un caso (ver "Cómo se hace"). |
| Insumos | Cero o más líneas: insumo y dosis por ha. La unidad viene del insumo; los litros se muestran como `L`. |
| Total de un insumo | Dosis por ha × total de ha a trabajar de la OT, con hasta 4 decimales. |
| Observaciones | Opcional, hasta 2000 caracteres. |
| Ingeniero responsable | Se elige entre el personal activo con rol ingeniero (ver Personal). Las OT anteriores conservan el nombre escrito a mano. |

**Estados** (se muestran como una etiqueta de color en el listado y en el detalle):

| Estado | Qué significa |
| --- | --- |
| Emitida | Recién emitida. Es el único estado que se puede editar, marcar realizada o cancelar. |
| Realizada | La labor se hizo. Ya no se puede editar. |
| Cancelada | Anulada con un motivo. Conserva su número y sigue en el listado; el PDF dice ANULADA. |

**Numeración por firma**: cada firma comercial numera sus OT en forma consecutiva. Un número nunca se reutiliza ni se
saltea, aunque la OT se cancele. El próximo número de cada firma se ve y se ajusta en Clientes (firma comercial,
"Próximo Nº de orden"), por ejemplo para continuar la numeración de la planilla.

**Nombre del archivo PDF**: el sistema propone el mismo patrón que la planilla:
`<FIRMA>_ORDEN_<n>_<CAMPO>_(<LOTE>)_..._<ABREVIATURA>_<d-mmm>`, por ejemplo
`CLIENTE EJEMPLO_ORDEN_125_CAMPO NORTE_(Lote 3)_PULV-TE_10-nov`. La firma y el campo van en mayúsculas, se repite
campo y lote por cada lote, la labor usa su abreviatura (o su nombre si no tiene) y los caracteres que Windows no
acepta se reemplazan por `-`.

## Cómo se hace

**Emitir una OT**

1. Pulsa **Nueva orden**.
2. Elige el **Cliente**: escribe para filtrar, muévete con las flechas y confirma con Enter.
3. Revisa la **Firma comercial**. La ayuda debajo indica con qué número se emitirá.
4. Completa **Fecha**, **Labor**, **Etapa** (si corresponde) y **Contratista**.
5. En **Campos y lotes**, marca los lotes. Usa "Filtrar campos y lotes" para buscar por campo, lote o código. Ajusta
   las **ha a trabajar** de cada lote si no es la superficie completa.
6. Si eliges una **Campaña** y marcaste un solo lote, el cultivo y la variedad se completan con el primer cultivo de
   ese lote en esa campaña. Puedes cambiarlos.
7. En **Insumos**, elige el insumo de cada línea; el cursor pasa a **Dosis por ha**. Usa **Agregar insumo** para otra
   línea y **Quitar** para borrarla. Una línea sin insumo no se guarda.
8. Completa **Observaciones** y elige el **Ingeniero responsable**.
9. Pulsa **Emitir orden**. Se abre el detalle con el número asignado.

**Buscar una OT**

1. En el listado, escribe en **Buscar** un número de orden, o parte del nombre de una firma, campo, lote o código de
   lote. La búsqueda se aplica al dejar de escribir.
2. Acota con **Firma** y **Estado** si hace falta.
3. Pulsa el número de la OT para abrir su detalle.

**Editar una OT emitida**

1. Abre el detalle y pulsa **Editar**.
2. Cambia lo necesario. La firma y el número no cambian.
3. Pulsa **Guardar cambios**.

**Marcar una OT como realizada**

1. Abre el detalle y pulsa **Marcar realizada**.
2. Confirma con **Confirmar realizada**. Desde ese momento no se puede editar.

**Cancelar una OT**

1. Abre el detalle y pulsa **Cancelar orden**.
2. Escribe el **Motivo de la cancelación** (obligatorio).
3. Pulsa **Confirmar cancelación**. La OT conserva su número y queda en el listado como Cancelada.

**Imprimir o guardar en PDF**

1. Pulsa **Imprimir / PDF** en el listado o en el detalle. Se abre en otra pestaña.
2. Sigue los pasos de la ayuda "Impresión de la OT".

## Reglas y validaciones

Avisos que no impiden guardar (la OT se guarda igual):

- "⚠ Supera la superficie del lote (N ha)." cuando las ha a trabajar son más que la superficie del lote.
- "⚠ La dosis está por debajo de la mínima del insumo (…)" o "… por encima de la máxima …" cuando la dosis sale del
  rango de referencia del insumo. La dosis nunca se cambia sola.

Lo que el sistema rechaza y el mensaje que ves:

| Situación | Mensaje |
| --- | --- |
| Ningún lote marcado | Selecciona al menos un lote. |
| Fecha vacía | La fecha es obligatoria. |
| ha a trabajar en cero, negativas o con más de 4 decimales | Las ha a trabajar deben ser mayores que cero y tener hasta 4 decimales. |
| Dosis en cero, negativa o con más de 4 decimales | La dosis debe ser mayor que cero y tener hasta 4 decimales. |
| Total de un insumo fuera de rango | El total de cada insumo debe ser mayor que cero y tener hasta 4 decimales. |
| El mismo lote dos veces | Cada lote puede aparecer una sola vez. |
| Un lote de otro cliente | Todos los lotes deben ser de campos del cliente de la firma. |
| Editar, cerrar o cancelar una OT realizada o cancelada | Esta orden ya no se puede editar. |
| Cancelar sin motivo | Indica el motivo de la cancelación. |
| Dos emisiones simultáneas chocan por el número | Otra orden tomó ese número. Actualiza e intenta de nuevo. |
| Otra persona guardó la OT antes que tú | Otra persona modificó este registro. Actualiza antes de guardar. |
| Un registro elegido ya no existe | El registro relacionado ya no existe. Actualiza los listados. |
| El responsable elegido ya no está activo o no es ingeniero | Se rechaza: el responsable debe ser un ingeniero activo. |

Si faltan datos base, el formulario lo indica: "Primero crea clientes, labores y contratistas." o "Este cliente aún no
tiene firmas comerciales. Créala en Clientes."

## Preguntas abiertas

Pendientes con los ingenieros ([preguntas](../engineer-questions.md)); el sistema sigue el caso más frecuente mientras
tanto:

- Q2: productos o dosis distintos entre lotes de una misma OT. Hoy los insumos aplican a todos los lotes.
- Q3: OT con dos labores, fechas o etapas. Hoy una labor, una fecha y una etapa por OT.
- Q4: más de un contratista por OT. Hoy uno.
- Q5: pulverizaciones sin productos. Hoy los insumos son opcionales en toda labor.
- Q6: confirmar la numeración por firma y la continuidad con la planilla.
- Q7: firma escaneada del ingeniero en el PDF.
- Q8: imagen del mapa con la zona a tratar.
- Q9: teléfonos de contacto del pie del PDF.
- Q10: advertencias por evento genético del cultivo.
- Q11: relación entre la OT y la receta agronómica digital.
- Q12: qué superficie proponer cuando la de la campaña y la del lote difieren.

## Notas técnicas

- Rutas (API local, prefijo `/api/v1`): `GET /work-orders?firmId=&status=&q=`, `GET /work-orders/{id}`,
  `GET /work-orders/{id}/document`, `POST /work-orders`, `PUT /work-orders/{id}` (con `revision`),
  `POST /work-orders/{id}/complete`, `POST /work-orders/{id}/cancel` (con `revision` y `reason`).
- `q` busca por número exacto cuando es un entero; si no, por texto contenido (sin distinguir mayúsculas) en firma,
  campo, lote o código de lote. Un `status` desconocido se rechaza.
- El número sale de `commercial_firms.next_work_order_number` dentro de la misma transacción del alta: sin huecos y
  seguro ante altas simultáneas.
- Tablas: `work_orders`, `work_order_plots` (guarda la superficie del lote al emitir y las ha a trabajar),
  `work_order_lines` (guarda la unidad del insumo al emitir y el total). Migraciones `008_work_orders.sql`,
  `009_ot_across_fields.sql` (la OT deja de tener un campo propio) y `013_staff.sql` (`responsible_staff_id`; con un
  responsable, `responsible_engineer` guarda la copia de su nombre; sin él, el texto libre de las OT anteriores).
- El total de cada línea se redondea a 4 decimales, la mitad hacia arriba, igual en la vista previa y en la API.
- Las escrituras requieren la cabecera `X-Agro-CSRF`; un `PUT` con revisión vieja responde 409.
- Código de la pantalla: `web/app/backoffice/orders.jsx` y `work-orders.mjs`; dominio en
  `src/AgroOperations.Api/Domain/WorkOrderService.cs`.
