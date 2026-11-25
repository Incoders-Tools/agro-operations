---
screen: campanas
title: Campañas
summary: Catálogo de campañas agrícolas que ordena el historial de lotes, análisis, superficies y órdenes de trabajo.
video:
reviewed: 2026-11-25
related: [lotes, analisis-de-suelo, analisis-de-semillas, superficie-por-campana, recetas, ordenes-de-trabajo]
---

# Campañas

## Para qué sirve

Una campaña es el ciclo agrícola, por ejemplo "2025/26". Agrupa los cultivos de cada lote, los análisis de suelo, la
superficie por campaña y las recetas, y se puede indicar en una OT y en un análisis de semillas.

## Conceptos

| Campo | Significado y reglas |
| --- | --- |
| Nombre | Obligatorio y único, sin distinguir mayúsculas. Usa siempre el mismo formato, por ejemplo `2025/26`, para que los listados se ordenen bien. |

Dónde se usa:

| Registro | ¿Obligatoria? |
| --- | --- |
| Cultivo por campaña de un lote | Sí |
| Análisis de suelo | Sí |
| Superficie por campaña | Sí |
| Receta | Sí |
| Orden de trabajo | No |
| Análisis de semillas | No |

## Cómo se hace

**Crear una campaña**

1. Escribe el nombre en "Nueva campaña".
2. Pulsa **Crear campaña**.

**Corregir un nombre**

1. Pulsa **Editar**, corrige y pulsa **Guardar cambios**.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Nombre vacío | El nombre es obligatorio. |
| Nombre ya usado | Ya existe un registro con ese nombre. |
| Otra persona guardó antes | Otra persona modificó este registro. Actualiza antes de guardar. |

- El historial de cultivos de un lote se ordena por el nombre de la campaña, de la más reciente a la más antigua; un
  formato distinto entre campañas desordena ese historial.

## Preguntas abiertas

Ninguna por ahora.

## Notas técnicas

- Rutas: `GET/POST /campaigns`, `PUT /campaigns/{id}` (con `revision`).
- Tabla: `campaigns` (nombre único `lower(name)`). Migración `004_agronomy.sql`.
- Código: `web/app/backoffice/catalogs.jsx`, `schema.mjs`; dominio `CampaignCatalog.cs`.
