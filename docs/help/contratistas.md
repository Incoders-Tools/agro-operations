---
screen: contratistas
title: Contratistas
summary: Catálogo de quienes realizan las labores de las órdenes de trabajo, con sus datos de contacto.
video:
reviewed: 2026-11-25
related: [ordenes-de-trabajo, proveedores, procedencia]
---

# Contratistas

## Para qué sirve

Lista a quienes ejecutan las labores en el campo. Cada OT lleva un contratista, y su nombre sale impreso en la OT. Los
contratistas son un catálogo propio, separado de Proveedores, porque las OT los referencian.

## Conceptos

| Campo | Significado y reglas |
| --- | --- |
| Nombre | Obligatorio y único, sin distinguir mayúsculas. |
| CUIT | Opcional. 11 dígitos, con o sin guiones. |
| Teléfono | Opcional. |
| Email | Opcional. Un único `@` con texto a ambos lados. |
| Sitio web | Opcional. Una dirección sin espacios, hasta 200 caracteres, por ejemplo `www.ejemplo.com.ar`. En el listado es un enlace. |
| Notas | Opcional. |
| Equipo propio | Marca que la maquinaria es del propio cliente (`PROPIO` en la planilla). La OT impresa lo indica. |

El catálogo trae precargado el contratista "Propio". Los contratistas importados de la planilla quedan como
"Importado, sin verificar" en Procedencia.

## Cómo se hace

**Crear un contratista**

1. Completa el formulario "Nuevo contratista".
2. Pulsa **Crear contratista**.

**Buscar y editar**

1. Escribe en **Buscar** parte del nombre o del CUIT.
2. Pulsa **Editar** en la fila, cambia los datos y pulsa **Guardar cambios**.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Nombre vacío | Indica el nombre. |
| Nombre ya usado | Ya existe un registro con ese nombre. |
| CUIT sin 11 dígitos | El CUIT debe tener 11 dígitos. |
| Email inválido | El email debe tener un único @ con texto a ambos lados. |
| Sitio web con espacios | El sitio web no puede tener espacios. |
| Otra persona guardó antes | Otra persona modificó este registro. Actualiza antes de guardar. |

Un sitio web que empieza con otro esquema (no `http` ni `https`) se guarda pero no se muestra como enlace.

## Preguntas abiertas

- Q4: si una OT puede necesitar más de un contratista. Hoy, uno por OT ([preguntas](../engineer-questions.md)).

## Notas técnicas

- Rutas: `GET/POST /contractors`, `PUT /contractors/{id}` (con `revision`).
- Tabla: `contractors` (nombre único `lower(name)`). Migraciones `006_catalogs.sql`, `010_reference_alignment.sql`
  (sitio web).
- Código: `web/app/backoffice/catalogs.jsx`, `schema.mjs`; dominio `ContractorCatalog.cs`.
