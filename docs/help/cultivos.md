---
screen: cultivos
title: Cultivos
summary: Catálogo de cultivos usado en el historial de los lotes, en las órdenes de trabajo y en los análisis de semillas.
video:
reviewed: 2026-11-25
related: [lotes, ordenes-de-trabajo, analisis-de-semillas, campanas]
---

# Cultivos

## Para qué sirve

Lista los cultivos que se pueden elegir en el historial de un lote (cultivos por campaña), en una OT y en un análisis de
semillas. El catálogo se puede ampliar y viene precargado con la lista de la referencia de los ingenieros.

## Conceptos

| Campo | Significado y reglas |
| --- | --- |
| Nombre | Obligatorio y único, sin distinguir mayúsculas. Por ejemplo "Soja", "Maíz", "Pasturas puras de gramíneas". |

La variedad o el híbrido no son parte del cultivo: se escriben en cada cultivo por campaña, OT o análisis de semillas.
El uso (renta o servicio) tampoco: depende de la campaña y se carga en el lote.

Una secuencia de la planilla como "TRIGO/SOJA" no es un cultivo: son dos cultivos de la misma campaña (posición 1 y
posición 2 en el lote).

## Cómo se hace

**Crear un cultivo**

1. Escribe el nombre en "Nuevo cultivo".
2. Pulsa **Crear cultivo**.

**Corregir un nombre**

1. Pulsa **Editar** en el cultivo, corrige y pulsa **Guardar cambios**. El cambio se ve en todos los registros que lo
   usan.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Nombre vacío | Indica el nombre. |
| Nombre ya usado | Ya existe un registro con ese nombre. |
| Otra persona guardó antes | Otra persona modificó este registro. Actualiza antes de guardar. |

## Preguntas abiertas

- Q10: si cada cultivo o variedad debe llevar su evento genético para advertir conflictos con insumos
  ([preguntas](../engineer-questions.md)).
- Q13 (respondida): uno o dos cultivos por campaña; las pasturas se cargan en cada campaña que ocupan.

## Notas técnicas

- Rutas: `GET/POST /crops`, `PUT /crops/{id}` (con `revision`).
- Tabla: `crops`. Migraciones `003_crops.sql`, `007_attributes.sql` (precarga), `010_reference_alignment.sql`
  (nombres de la referencia).
- Código: `web/app/backoffice/catalogs.jsx`, `schema.mjs`; dominio `CropCatalog.cs`.
