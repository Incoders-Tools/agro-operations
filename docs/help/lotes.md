---
screen: lotes
title: Lotes
summary: Lotes de cada campo con su código y superficie, su historial de cultivos y rendimientos por campaña.
video:
reviewed: 2026-11-25
related: [clientes, analisis-de-suelo, cultivos, campanas, superficie-por-campana, ordenes-de-trabajo]
---

# Lotes

## Para qué sirve

Un lote es la unidad de terreno sobre la que se trabaja. Aquí cargas los lotes de un campo, los mueves si cambian de
campo y llevas el historial de cada lote: qué cultivos tuvo en cada campaña, con qué uso y qué rendimiento dio. La ficha
del lote también muestra sus análisis de suelo.

Llegas desde **Clientes → cliente → Ver lotes (en un campo) → lote**.

## Conceptos

**Lote**

| Campo | Significado y reglas |
| --- | --- |
| Código | Opcional, hasta 40 caracteres. Identificador corto cliente-campo-lote, por ejemplo `CEJ-NORTE-03`. Único sin distinguir mayúsculas. |
| Nombre | Obligatorio. |
| Superficie (ha) | Obligatoria. Mayor que cero, hasta 4 decimales. Es la superficie de referencia actual del lote. |
| Mover a otro campo | Solo al editar. Cualquier campo, también de otro cliente. |

**Cultivo por campaña**: el historial del lote. Un lote admite hasta dos cultivos por campaña (primero y segundo, por
ejemplo trigo y luego soja). Una pastura que dura varias campañas se carga en cada campaña que ocupa.

| Campo | Significado y reglas |
| --- | --- |
| Campaña | Solo al crear. Obligatoria. |
| Posición | Solo al crear. 1 (primer cultivo) o 2 (segundo cultivo). |
| Cultivo | Obligatorio, del catálogo de cultivos. |
| Uso | Renta o Servicio en esta campaña. Por defecto Renta. |
| Variedad o híbrido | Opcional. |
| Rendimiento de grano (kg/ha) | Opcional. Cero o positivo, hasta 2 decimales. Rendimiento promedio de cosecha. |
| Biomasa de forraje (kg/ha) | Opcional. Cero o positivo, hasta 2 decimales. |

El primer cultivo de una campaña es el que la OT propone cuando eliges esa campaña y un solo lote.

## Cómo se hace

**Crear un lote**

1. Abre el cliente y luego el campo con **Ver lotes**.
2. Pulsa **Nuevo lote**, completa código, nombre y superficie.
3. Pulsa **Crear lote**.

**Mover un lote a otro campo**

1. En el campo, pulsa **Editar** en el lote.
2. Elige el destino en **Mover a otro campo**; la lista agrupa los campos por cliente.
3. Guarda. La pantalla abre el nuevo campo y avisa "Se movió el lote a … (cliente)". Las OT ya emitidas no cambian.

**Cargar el cultivo de una campaña**

1. Abre el lote.
2. Pulsa **Nuevo cultivo**.
3. Elige campaña, posición, cultivo y uso; completa variedad si la conoces.
4. Pulsa **Crear cultivo de campaña**.

**Registrar el rendimiento**

1. Abre el lote y pulsa **Editar** en el cultivo de la campaña.
2. Completa **Rendimiento de grano** o **Biomasa de forraje** y guarda. La campaña y la posición no cambian.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Nombre vacío | El nombre es obligatorio. |
| Superficie en cero, negativa o con más de 4 decimales | La superficie debe ser mayor que cero y tener hasta 4 decimales. |
| Código de más de 40 caracteres | El código admite hasta 40 caracteres. |
| Código ya usado por otro lote | Ya existe un lote con ese código. |
| Posición distinta de 1 o 2 | La posición debe ser 1 o 2. |
| La campaña ya tiene un cultivo en esa posición | Ese lote ya tiene un cultivo en esa posición para la campaña. |
| Rendimiento negativo | El rendimiento debe ser cero o positivo. |
| Faltan cultivos o campañas | Primero crea cultivos y campañas en sus catálogos. |

- Un rendimiento en cero es un valor cargado; vacío significa "sin dato".
- La superficie del lote se copia a cada OT al emitirla; cambiarla después no modifica las OT emitidas.

## Preguntas abiertas

- Q12: qué superficie debe proponer la OT cuando la superficie de la campaña y la del lote difieren
  ([preguntas](../engineer-questions.md)).
- Q13 (respondida): uno o dos cultivos por campaña, cada uno de renta o de servicio.

## Notas técnicas

- Rutas: `GET /plots?fieldId=`, `POST /plots`, `GET/PUT /plots/{id}` (un `fieldId` en el `PUT` mueve el lote);
  `GET /plot-crops?plotId=`, `POST /plot-crops`, `PUT /plot-crops/{id}` (no cambia lote, campaña ni posición).
- Tablas: `plots` (código único parcial `lower(code)` cuando existe), `plot_crops` (único por lote, campaña y
  posición). Migraciones `001_operations.sql`, `007_attributes.sql`, `010_reference_alignment.sql` (rendimientos).
- Código: `web/app/backoffice/clients.jsx` (nivel lote), `schema.mjs`; dominio `OperationsService.cs`,
  `PlotCropService.cs`.
