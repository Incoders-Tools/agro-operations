---
screen: labores
title: Labores
summary: Catálogo de lo que se hace en un lote, agrupado por categoría, con su abreviatura para las órdenes de trabajo.
video:
reviewed: 2026-11-25
related: [ordenes-de-trabajo, etapas, recetas]
---

# Labores

## Para qué sirve

Una labor es **qué** se hace en el lote, por ejemplo "Pulverización terrestre" o "Siembra gruesa". Cada OT lleva una
sola labor. El **cuándo** del ciclo del cultivo (barbecho, presiembra, postemergencia…) es aparte: es la etapa.

## Conceptos

| Campo | Significado y reglas |
| --- | --- |
| Nombre | Obligatorio y único, sin distinguir mayúsculas. |
| Abreviatura | Opcional, hasta 12 caracteres, por ejemplo `PULV-TE`. Se usa en el nombre del archivo PDF de la OT. |
| Categoría | Opcional (ver tabla). Sin categoría, la labor aparece en "Otras". |
| Descripción | Opcional, hasta 300 caracteres. Aclaración, por ejemplo el equipo o los cultivos. |

**Categorías**, en el orden de la referencia de los ingenieros:

| Categoría | Ejemplos |
| --- | --- |
| Aplicación | Pulverización terrestre, Fertilización al voleo, Pulverización aérea con dron |
| Labranza y preparación | Disco doble acción, Subsolado / Descompactado profundo |
| Siembra e implantación | Siembra gruesa, Siembra fina con fertilización |
| Cosecha y poscosecha | Cosecha gruesa, Embolsado de grano seco / húmedo |
| Forrajes y ganadería | Confección de rollos, Picado fino de forraje |
| Mantenimiento de infraestructura | Limpieza de canales y desagües |

El catálogo viene precargado con las labores de la planilla y de la referencia de los ingenieros. El listado y el
selector de la OT muestran las labores agrupadas por categoría y ordenadas por nombre.

## Cómo se hace

**Crear una labor**

1. Completa el formulario "Nueva labor": nombre, abreviatura, categoría y descripción.
2. Pulsa **Crear labor**.

**Buscar y editar**

1. Escribe en **Buscar** parte del nombre, la abreviatura, la categoría o la descripción.
2. Pulsa **Editar**, cambia los datos y pulsa **Guardar cambios**.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Nombre vacío | Indica el nombre. |
| Nombre ya usado | Ya existe un registro con ese nombre. |
| Abreviatura de más de 12 caracteres | La abreviatura admite hasta 12 caracteres. |
| Descripción de más de 300 caracteres | La descripción no puede superar los 300 caracteres. |
| Categoría inválida | La categoría no es válida. |

- Cambiar el nombre o la abreviatura cambia cómo se muestran las OT existentes y el nombre de archivo propuesto de sus
  PDF, porque la OT guarda la referencia a la labor, no una copia del texto.

## Preguntas abiertas

- Q3: OT con dos labores en la planilla. Hoy, una labor por OT ([preguntas](../engineer-questions.md)).

## Notas técnicas

- Rutas: `GET/POST /labor-types`, `PUT /labor-types/{id}` (con `revision`).
- Tabla: `labor_types` (nombre único `lower(name)`, `abbreviation`, `category`, `description`). Migraciones
  `004_agronomy.sql`, `006_catalogs.sql` (abreviatura y precarga), `010_reference_alignment.sql` (categorías y labores
  de la referencia).
- Código: `web/app/backoffice/catalogs.jsx`, `schema.mjs` (`laborGroups`); dominio `LaborTypeCatalog.cs`.
