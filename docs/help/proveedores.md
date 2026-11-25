---
screen: proveedores
title: Proveedores
summary: Proveedores de insumos y de servicios, con sus contactos, los clientes que les compran y sus precios de referencia.
video:
reviewed: 2026-11-25
related: [clientes, insumos, comparacion-de-presupuestos, analisis-de-suelo, analisis-de-semillas, contratistas]
---

# Proveedores

## Para qué sirve

Reúne a las firmas donde los clientes compran insumos y a los proveedores de servicios: laboratorios de suelos y de
semillas, plataformas virtuales y otros. De cada uno guardas sus contactos de ventas y técnicos, qué clientes le
compran y, para los de insumos, sus precios de referencia por insumo. Los contratistas siguen en su propio catálogo.

## Conceptos

**Proveedor**

| Campo | Significado y reglas |
| --- | --- |
| Nombre | Obligatorio y único, sin distinguir mayúsculas. |
| Tipo | Insumos, Laboratorio de suelos, Laboratorio de semillas, Plataforma virtual u Otro. Por defecto Insumos. |
| CUIT | Opcional. 11 dígitos, con o sin guiones. |
| Sitio web | Opcional. Una dirección sin espacios, hasta 200 caracteres. |
| Teléfono, Email, Notas | Opcionales. El email lleva un único `@`. |

El tipo decide dónde aparece el proveedor: los laboratorios de suelos en Análisis de suelo, los de semillas en Análisis
de semillas y los de insumos en los presupuestos y los precios de referencia.

**Contacto**: una persona del proveedor.

| Campo | Significado y reglas |
| --- | --- |
| Nombre | Obligatorio. |
| Rol | Ventas, Técnico u Otro. Por defecto Ventas. |
| Teléfono, Email, Notas | Opcionales. |

Un contacto no cambia de proveedor.

**Cliente que le compra**: vínculo entre un cliente y un proveedor. Cada par se vincula una sola vez. Se ve desde los
dos lados: en el proveedor ("Clientes que le compran") y en el cliente ("Proveedores del cliente").

**Precio de referencia**: solo para proveedores de tipo Insumos.

| Campo | Significado y reglas |
| --- | --- |
| Insumo | Obligatorio, del catálogo de insumos. |
| Precio | Obligatorio. Mayor que cero, hasta 4 decimales. |
| Moneda | ARS (pesos) o USD (dólares). |
| Unidad | Opcional. Vacía toma la unidad del insumo; sigue al insumo mientras no la cambies a mano. |
| Vigente desde | Obligatoria. Fecha desde la que rige el precio. |
| Notas | Opcional. |

Los precios se listan del más reciente al más antiguo. En Insumos, al editar un insumo ves el último precio de cada
proveedor.

## Cómo se hace

**Crear un proveedor**

1. Completa el formulario "Nuevo proveedor" y elige el **Tipo**.
2. Pulsa **Crear proveedor**.

**Filtrar y abrir un proveedor**

1. Elige un **Tipo** para filtrar, o escribe en **Buscar** (nombre, CUIT, email o tipo).
2. Pulsa **Ver detalle**.

**Agregar un contacto**

1. Abre el proveedor; el formulario de la derecha pasa a "Nuevo contacto".
2. Completa nombre, rol y datos, y pulsa **Crear contacto**.

**Vincular un cliente**

1. Abre el proveedor y busca **Clientes que le compran**.
2. Elige el cliente en **Agregar cliente** y pulsa **Vincular**. **Quitar** deshace el vínculo.

**Cargar un precio de referencia**

1. Abre un proveedor de tipo Insumos y pulsa **Nuevo precio**.
2. Elige el insumo, escribe el precio, la moneda y la fecha de vigencia.
3. Pulsa **Crear precio**. Para un precio nuevo del mismo insumo, crea otro con otra fecha en lugar de editar el
   anterior: así queda el historial.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Nombre vacío | Indica el nombre. |
| Nombre ya usado | Ya existe un registro con ese nombre. |
| Tipo inválido | Elige un tipo de proveedor válido. |
| Rol de contacto inválido | Elige el rol del contacto: Ventas, Técnico u Otro. |
| CUIT, email o sitio web inválidos | El CUIT debe tener 11 dígitos. / El email debe tener un único @ con texto a ambos lados. / El sitio web no puede tener espacios. |
| Cliente ya vinculado | Ese proveedor ya está vinculado a este cliente. |
| Precio en cero o negativo | El precio debe ser mayor que cero y tener hasta 4 decimales. |
| Moneda inválida | Elige la moneda: ARS o USD. |
| Sin fecha de vigencia | Indica desde cuándo rige el precio. |
| Sin insumo | Elige un insumo. |
| Faltan insumos | Primero crea insumos en su catálogo. |

## Preguntas abiertas

- Q19: si los precios se comparan con o sin IVA y en qué moneda ([preguntas](../engineer-questions.md)).
- Q20: si los "precios de referencia" son propios por insumo, de cada proveedor o ambos. Hoy existen los dos: el costo
  de referencia del insumo y los precios fechados de cada proveedor.

## Notas técnicas

- Rutas: `GET /providers?kind=`, `POST /providers`, `PUT /providers/{id}`; `GET /provider-contacts?providerId=`,
  `POST /provider-contacts`, `PUT /provider-contacts/{id}`; `GET /client-providers?clientId=|providerId=`,
  `POST /client-providers` (409 "Duplicate link"), `DELETE /client-providers/{id}` (204);
  `GET /provider-prices?providerId=|inputId=`, `POST /provider-prices`, `PUT /provider-prices/{id}`.
- Tablas: `providers`, `provider_contacts`, `client_providers` (único por cliente y proveedor), `provider_prices`
  (`numeric(14,4)`). Migración `011_providers_and_quotes.sql`.
- Un proveedor reúne proveedores de insumos y de servicios; los contratistas quedan aparte (decisión L2 de
  `odd/tasks/analyses-and-providers.md`).
- Código: `web/app/backoffice/providers.jsx`, `providers.mjs`; dominio `ProviderService.cs`.
