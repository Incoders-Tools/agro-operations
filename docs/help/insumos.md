---
screen: insumos
title: Insumos
summary: Catálogo de productos que se aplican en las labores, con categoría, formulación, orden de mezcla y rango de dosis.
video:
reviewed: 2026-11-25
related: [ordenes-de-trabajo, proveedores, comparacion-de-presupuestos, procedencia]
---

# Insumos

## Para qué sirve

Es el catálogo de productos que se aplican en una labor: fertilizantes, fitosanitarios, coadyuvantes, curasemillas y
otros. Cada línea de insumo de una OT sale de aquí, y con este catálogo el sistema advierte dosis fuera de rango y marca
los productos que requieren cono hueco.

## Conceptos

| Campo | Significado y reglas |
| --- | --- |
| Nombre comercial | Obligatorio y único, sin distinguir mayúsculas. |
| Principio activo | Opcional. |
| Concentración | Opcional, por ejemplo `48 %`. |
| Formulación | Opcional. Abreviatura, por ejemplo `SC`, `EC`, `WG`. |
| Orden de mezcla | Opcional. Entero de 1 a 20: en qué orden se agrega el producto al tanque. |
| Categoría | Opcional (ver tabla). |
| Unidad | Obligatoria, por ejemplo `L`, `kg`, `cc`. Los litros se muestran como `L` y `L/ha`. |
| Dosis mínima por ha | Opcional. Mayor que cero, hasta 4 decimales. |
| Dosis máxima por ha | Opcional. Mayor que cero, hasta 4 decimales; no menor que la mínima. |
| Costo de referencia | Opcional. Cero o positivo, hasta 4 decimales. |
| Requiere cono hueco | Marca los productos que deben aplicarse con pastillas de cono hueco. |

**Categorías**: Fertilizante, Herbicida, Insecticida, Fungicida, Coadyuvante, Curasemilla, Bioestimulante, Terápico de
semillas e Inoculante.

**Formulación y orden de mezcla**: en la OT impresa el producto sale con su formulación y "orden de mezcla N". Los
nombres importados de la planilla ya traen ambos entre paréntesis, por ejemplo "Producto A (3 SC)"; en ese caso no se
repiten.

**Rango de dosis**: es una referencia. Si una OT usa una dosis fuera del rango, la OT lo advierte pero la guarda tal
cual: el sistema nunca cambia una dosis solo.

**Cono hueco**: la OT impresa marca esos productos con "Cono hueco" y agrega una nota.

**Insumos importados**: los insumos cargados desde la planilla quedan como "Importado, sin verificar" en Procedencia.
Algunos llegaron sin rango de dosis porque el nombre de la planilla de dosis no coincidía exactamente; revisa el rango
antes de confiar en las advertencias.

**Precios de referencia**: al editar un insumo, debajo del formulario se ve el último precio de cada proveedor. Los
precios se cargan desde Proveedores.

## Cómo se hace

**Crear un insumo**

1. Completa el formulario "Nuevo insumo". Solo el nombre comercial y la unidad son obligatorios.
2. Pulsa **Crear insumo**.

**Buscar y corregir un insumo**

1. Escribe en **Buscar** parte del nombre comercial o del principio activo.
2. Pulsa **Editar**, corrige y pulsa **Guardar cambios**.
3. Para verificar un insumo importado, revisa principio activo, unidad, rango de dosis y orden de mezcla contra la
   etiqueta del producto.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Nombre comercial vacío | Indica el nombre comercial. |
| Nombre ya usado | Ya existe un registro con ese nombre. |
| Unidad vacía | Indica la unidad. |
| Orden de mezcla fuera de 1 a 20 | El orden de mezcla debe estar entre 1 y 20. |
| Dosis mínima o máxima en cero, negativa o con más de 4 decimales | La dosis mínima (o máxima) debe ser mayor que cero y tener hasta 4 decimales. |
| Mínima mayor que la máxima | La dosis mínima no puede superar la máxima. |
| Costo de referencia negativo | El costo de referencia debe ser cero o positivo, con hasta 4 decimales. |
| Categoría inválida | La categoría no es válida. |

- Cambiar la unidad de un insumo no cambia las OT emitidas: cada línea guarda la unidad del momento en que se emitió.

## Preguntas abiertas

- Q10: si se debe advertir cuando un insumo choca con el evento genético del cultivo ([preguntas](../engineer-questions.md)).
- Q14: stock de productos por cliente.
- Q20: precios de referencia propios frente a precios de cada proveedor.

## Notas técnicas

- Rutas: `GET/POST /inputs`, `PUT /inputs/{id}` (con `revision`); precios por insumo en
  `GET /provider-prices?inputId=`.
- Tabla: `inputs` (nombre único `lower(commercial_name)`). Migraciones `006_catalogs.sql`, `010_reference_alignment.sql`
  (categoría inoculante).
- Carga desde la planilla: `tools/import-workbook.ps1` (ver Procedencia).
- Código: `web/app/backoffice/catalogs.jsx`, `schema.mjs`; dominio `InputCatalog.cs`.
