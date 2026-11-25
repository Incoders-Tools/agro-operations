---
screen: etapas
title: Etapas
summary: Momentos del ciclo del cultivo en que ocurre una labor, ordenados por su número de orden.
video:
reviewed: 2026-11-25
related: [labores, ordenes-de-trabajo]
---

# Etapas

## Para qué sirve

Una etapa es **cuándo** ocurre una labor en el ciclo del cultivo, por ejemplo "Barbecho" o "Postemergencia". Es
opcional en la OT y sale impresa junto a la labor. Separar labor y etapa evita tener una labor por cada combinación
(por ejemplo "Pulverización terrestre" + "Barbecho" en lugar de "Pulverización terrestre barbecho").

## Conceptos

| Campo | Significado y reglas |
| --- | --- |
| Nombre | Obligatorio y único, sin distinguir mayúsculas. |
| Orden | Obligatorio. Número entero: posición de la etapa en el ciclo; el listado se ordena por este número. |

El catálogo viene precargado con Barbecho, Presiembra, Preemergencia, Postemergencia, Postsiembra, Prefloración y
Postfloración.

## Cómo se hace

**Crear una etapa**

1. Completa nombre y orden en "Nueva etapa".
2. Pulsa **Crear etapa**.

**Reordenar**

1. Pulsa **Editar** en la etapa.
2. Cambia el **Orden** y guarda. Para intercalar una etapa, usa un número entre los existentes o renumera las demás.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Nombre vacío | El nombre es obligatorio. |
| Nombre ya usado | Ya existe un registro con ese nombre. |
| Otra persona guardó antes | Otra persona modificó este registro. Actualiza antes de guardar. |

- Las recetas usan su propia lista fija de tres etapas (presiembra, preemergencia, postemergencia), no este catálogo.

## Preguntas abiertas

- Q3: OT con dos etapas en la planilla. Hoy, una etapa por OT ([preguntas](../engineer-questions.md)).

## Notas técnicas

- Rutas: `GET/POST /stages`, `PUT /stages/{id}` (con `revision`).
- Tabla: `stages` (nombre único `lower(name)`, `sort_order`). Migración `006_catalogs.sql`.
- Código: `web/app/backoffice/catalogs.jsx`, `schema.mjs`; dominio `StageCatalog.cs`.
