---
screen: recetas
title: Recetas
summary: Planes internos de tratamiento por lote y campaña con sus líneas de producto; no son prescripciones oficiales.
video:
reviewed: 2026-11-25
related: [lotes, campanas, labores, ordenes-de-trabajo]
---

# Recetas

## Para qué sirve

Una receta es un **plan interno de tratamiento** para un lote en una campaña: qué labor, en qué etapa, con qué uso
objetivo y qué productos y dosis. Sirve para planificar.

Una receta **no es una prescripción oficial emitida** (no es una receta agronómica digital) y **no prueba que la labor
se haya realizado**. Lo que se ejecuta en el campo se registra con una orden de trabajo.

El menú tiene dos entradas: **Recetas** (la cabecera) y **Líneas de receta** (los productos de cada receta).

## Conceptos

**Receta**

| Campo | Significado y reglas |
| --- | --- |
| Lote | Obligatorio. Solo al crear. |
| Campaña | Obligatoria. Solo al crear. |
| Labor | Obligatoria. Solo al crear. |
| Etapa del ciclo | Presiembra, Preemergencia o Postemergencia. Es una lista fija propia, no el catálogo de Etapas. |
| Uso objetivo | Obligatorio, hasta 200 caracteres, por ejemplo "Barbecho". |
| Ingeniero responsable | Obligatorio, texto libre hasta 200 caracteres. |

**Línea de receta**

| Campo | Significado y reglas |
| --- | --- |
| Receta | Obligatoria. Solo al crear. |
| Producto | Obligatorio, texto libre (no usa el catálogo de Insumos por ahora). |
| Dosis | Obligatoria. Mayor que cero, hasta 4 decimales. |
| Unidad | Obligatoria, por ejemplo `L/ha`. |

## Cómo se hace

**Crear una receta**

1. Entra en **Recetas**.
2. Elige lote, campaña y labor.
3. Elige la etapa del ciclo, escribe el uso objetivo y el ingeniero responsable.
4. Pulsa **Crear receta**.

**Agregar productos a una receta**

1. Entra en **Líneas de receta**.
2. Elige la **Receta** y completa producto, dosis y unidad.
3. Pulsa **Crear línea de receta**. Repite por cada producto.

**Corregir**

1. Pulsa **Editar** en el registro. En una receta cambian etapa, uso objetivo y responsable; en una línea, producto,
   dosis y unidad.
2. Pulsa **Guardar cambios**.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Uso objetivo vacío | Indica el uso objetivo. |
| Ingeniero responsable vacío | Indica el ingeniero responsable. |
| Etapa fuera de la lista | Elige una etapa válida. |
| Producto vacío | Indica el producto. |
| Dosis en cero, negativa o con más de 4 decimales | La dosis debe ser mayor que cero y tener hasta 4 decimales. |
| Unidad vacía | Indica la unidad. |
| Faltan registros relacionados | Primero crea los registros relacionados: lote, campaña, labor. |

## Preguntas abiertas

- Q11: cómo se relaciona una OT con la receta agronómica digital, si toda OT necesita una y quién la emite
  ([preguntas](../engineer-questions.md)).

## Notas técnicas

- Rutas: `GET/POST /recipes`, `PUT /recipes/{id}`; `GET/POST /recipe-lines`, `PUT /recipe-lines/{id}` (con
  `revision`).
- Tablas: `recipes` (etapa con lista fija propia), `recipe_lines`. Migración `004_agronomy.sql`.
- Pantallas con el editor genérico: `web/app/backoffice/generic.jsx`; dominio `RecipeService.cs`.
