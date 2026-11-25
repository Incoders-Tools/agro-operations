---
screen: clientes
title: Clientes
summary: Clientes con sus firmas comerciales, campos y proveedores; punto de entrada a los lotes de cada campo.
video:
reviewed: 2026-11-25
related: [lotes, ordenes-de-trabajo, proveedores, comparacion-de-presupuestos, procedencia]
---

# Clientes

## Para qué sirve

Administra los clientes y todo lo que cuelga de ellos: sus firmas comerciales, sus campos y, dentro de cada campo, sus
lotes. Desde la ficha de un cliente también ves y vinculas sus proveedores y sus comparaciones de presupuestos.

La navegación es en niveles: **Clientes → cliente → campo → lote**. La línea de ubicación arriba de los listados
muestra dónde estás y permite volver a cualquier nivel.

## Conceptos

**Cliente**: la persona o empresa para la que se trabaja, como la conocen los ingenieros.

| Campo | Significado y reglas |
| --- | --- |
| Nombre | Obligatorio. |
| CUIT | Opcional. 11 dígitos, con o sin guiones; se guarda solo con los dígitos. |
| Productores | Opcional. Una o más personas productoras del cliente. |
| Email | Opcional. Un único `@` con texto a ambos lados. |
| Celular | Opcional. |
| Dirección | Opcional. |

**Firma comercial**: el nombre con el que se emiten las OT del cliente. Un cliente puede tener varias. Al crear un
cliente se crea su firma con el mismo nombre, salvo que ya exista una firma con ese nombre: en ese caso el cliente se
crea sin firma y la agregas con otro nombre.

| Campo | Significado y reglas |
| --- | --- |
| Nombre | Obligatorio y único entre todas las firmas, sin distinguir mayúsculas. |
| CUIT | Opcional, mismas reglas que el cliente. |
| Próximo Nº de orden | Solo al editar. Número con el que se emitirá la próxima OT de la firma. Debe ser mayor que la última OT emitida; vacío mantiene el actual. |

**Campo**: un establecimiento del cliente. Tiene muchos lotes.

| Campo | Significado y reglas |
| --- | --- |
| Nombre | Obligatorio. |
| Tenencia | Opcional: Propio, Alquiler fijo o Alquiler a porcentaje. |
| Localidad | Opcional. |
| Mover a otro cliente | Solo al editar. Úsalo solo si el campo cambió de productor. |

## Cómo se hace

**Crear un cliente**

1. En el nivel Clientes, completa el formulario "Nuevo cliente".
2. Pulsa **Crear cliente**. Se crea también su firma comercial con el mismo nombre.

**Buscar y abrir un cliente**

1. Escribe en **Buscar** parte del nombre o del CUIT.
2. Pulsa **Ver campos y lotes** en la fila del cliente.

**Agregar o editar una firma comercial**

1. Abre el cliente.
2. Pulsa **Nueva firma**, o **Editar** en una firma existente.
3. Para continuar la numeración de la planilla, edita la firma y completa **Próximo Nº de orden**.
4. Guarda.

**Crear un campo**

1. Abre el cliente y pulsa **Nuevo campo**.
2. Completa nombre, tenencia y localidad, y pulsa **Crear campo**.
3. Pulsa **Ver lotes** en el campo para cargar sus lotes (ver la ayuda de Lotes).

**Mover un campo a otro cliente**

1. Abre el cliente, pulsa **Editar** en el campo.
2. Elige el nuevo cliente en **Mover a otro cliente** y guarda.
3. La pantalla abre el nuevo cliente y avisa "Se movió el campo a …". Las OT ya emitidas no cambian.

**Vincular un proveedor al cliente**

1. Abre el cliente y busca la sección **Proveedores del cliente**.
2. Elige el proveedor en **Agregar proveedor** y pulsa **Vincular**. **Quitar** deshace el vínculo.

**Ver las comparaciones de presupuestos del cliente**

1. Abre el cliente; la sección **Presupuestos** muestra las 5 más recientes.
2. Pulsa una para abrirla, o **Ver comparaciones del cliente** para el listado completo.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Nombre vacío | El nombre es obligatorio. |
| CUIT sin 11 dígitos | El CUIT debe tener 11 dígitos. |
| Email inválido | El email debe tener un único @ con texto a ambos lados. |
| Nombre de firma ya usado | Ya existe un registro con ese nombre. |
| Próximo Nº menor o igual que la última OT | El próximo número debe ser mayor que N. |
| Proveedor ya vinculado | Ese proveedor ya está vinculado a este cliente. |
| Otra persona guardó antes | Otra persona modificó este registro. Actualiza antes de guardar. |

- Nada se borra desde esta pantalla: un cliente, campo o lote con registros asociados no se elimina como efecto
  secundario de otra acción.
- Mover un campo no cambia las OT ya emitidas: conservan su firma y sus lotes.

## Preguntas abiertas

- Q1: dos firmas de la planilla que no coinciden con ningún cliente ([preguntas](../engineer-questions.md)).
- Q14: stock de productos de cada cliente.
- Q15: si la tenencia del campo afecta algún documento o informe.

## Notas técnicas

- Rutas: `GET/POST /clients`, `GET/PUT /clients/{id}`; `GET /commercial-firms?clientId=`, `POST /commercial-firms`,
  `PUT /commercial-firms/{id}`; `GET /fields?clientId=`, `POST /fields`, `GET/PUT /fields/{id}` (un `clientId` en el
  `PUT` mueve el campo; omitido, lo mantiene); `GET/POST /client-providers`, `DELETE /client-providers/{id}`.
- `PUT` reemplaza todo: un atributo opcional omitido queda vacío. Excepción: `nextWorkOrderNumber` omitido mantiene el
  número guardado.
- Tablas: `clients`, `commercial_firms`, `fields`, `client_providers`. Migraciones `001_operations.sql`,
  `007_attributes.sql`, `009_ot_across_fields.sql` (firma por defecto para cada cliente), `010_reference_alignment.sql`
  (mover campos y lotes), `011_providers_and_quotes.sql`.
- Clientes, campos y lotes de la planilla se cargan con `tools/import-workbook.ps1` (ver Procedencia).
- Código: `web/app/backoffice/clients.jsx`, `schema.mjs`; dominio `OperationsService.cs`, `CommercialFirmCatalog.cs`.
