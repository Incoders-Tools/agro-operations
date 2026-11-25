---
screen: superficie-por-campana
title: Superficie por campaña
summary: Superficie de un lote en una campaña determinada, cuando difiere de la superficie de referencia del lote.
video:
reviewed: 2026-11-25
related: [lotes, campanas, ordenes-de-trabajo]
---

# Superficie por campaña

## Para qué sirve

La superficie de un lote puede cambiar entre campañas (por ejemplo, por una división o una corrección de mensura). Aquí
registras la superficie de un lote en una campaña, sin modificar la superficie de referencia del lote.

## Conceptos

| Campo | Significado y reglas |
| --- | --- |
| Lote | Obligatorio. Solo al crear. |
| Campaña | Obligatoria. Solo al crear. |
| Superficie (ha) | Obligatoria. Mayor que cero, hasta 4 decimales. |

Hay como máximo un registro por lote y campaña. La superficie del lote (en Lotes) sigue siendo el valor de referencia
actual; las OT hoy proponen la superficie del lote, no la de la campaña.

## Cómo se hace

**Registrar la superficie de una campaña**

1. Elige el **Lote** y la **Campaña**: escribe para filtrar y confirma con Enter.
2. Escribe la **Superficie (ha)**.
3. Pulsa **Crear registro de superficie**.

**Corregir la superficie**

1. Pulsa **Editar** en el registro.
2. Cambia la superficie y pulsa **Guardar cambios**. Lote y campaña no cambian.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Superficie en cero, negativa o con más de 4 decimales | La superficie debe ser mayor que cero y tener hasta 4 decimales. |
| Ya existe un registro para ese lote y campaña | Se rechaza; edita el registro existente. |
| Faltan lotes o campañas | Primero crea los registros relacionados: lote, campaña. |
| Otra persona guardó antes | Otra persona modificó este registro. Actualiza antes de guardar. |

## Preguntas abiertas

- Q12: qué superficie debe proponer una OT cuando la de la campaña y la del lote difieren
  ([preguntas](../engineer-questions.md)).

## Notas técnicas

- Rutas: `GET/POST /plot-campaign-areas`, `PUT /plot-campaign-areas/{id}` (solo `areaHectares` y `revision`).
- Tabla: `plot_campaign_areas` (única por `plot_id` y `campaign_id`). Migración `004_agronomy.sql`.
- Pantalla con el editor genérico: `web/app/backoffice/generic.jsx`; dominio `PlotCampaignAreaService.cs`.
