---
screen: personal
title: Personal
summary: Personal de la empresa (ingenieros, administrativos y otros); los ingenieros activos son los responsables elegibles en las OT.
video:
reviewed: 2026-11-25
related: [ordenes-de-trabajo, impresion-ot, acceso-local]
---

# Personal

## Para qué sirve

Registra a las personas de la empresa que intervienen en el trabajo. Su uso principal es la orden de trabajo: el
**Ingeniero responsable** de una OT se elige de esta lista, y la OT impresa muestra su nombre, título, matrícula y
teléfono. Hoy los únicos usuarios del sistema son ingenieros, pero en el futuro otra persona podrá cargar datos.

Una persona del personal **no es una cuenta de acceso**: no inicia sesión. Cuando existan cuentas con invitación, cada
cuenta podrá vincularse con su persona del personal.

## Conceptos

| Campo | Significado y reglas |
| --- | --- |
| Nombre completo | Obligatorio y único, sin distinguir mayúsculas; hasta 200 caracteres. Nombre y apellido como deben salir en la OT. |
| Título | Opcional, hasta 100 caracteres. Título profesional; al escribir se sugiere "Ingeniero agrónomo". |
| Rol | Obligatorio: Ingeniero, Administrativo u Otro. |
| Matrícula | Opcional, hasta 50 caracteres. Número de matrícula profesional; sale en la OT impresa. |
| Email | Opcional. Un único `@` con texto a ambos lados. |
| Teléfono | Opcional. Sale en la OT impresa como contacto. |
| Activo | Marcado por defecto. Solo el personal activo aparece para elegir. Desactiva en lugar de borrar a quien deja la empresa. |
| Notas | Opcional. |

**Quién aparece como responsable de una OT**: solo el personal **activo** con rol **Ingeniero**. Desactivar a una
persona no cambia las OT ya emitidas con ella como responsable.

**Qué guarda la OT**: el responsable elegido y una copia de su nombre al guardar la OT. La OT impresa muestra el
título, la matrícula y el teléfono actuales de esa persona.

**OT anteriores**: las OT emitidas antes de este catálogo conservan el nombre del responsable escrito a mano. Al editar
una de ellas, elige al responsable de la lista.

## Cómo se hace

**Abrir la pantalla**

1. En el menú lateral, abre el grupo **Administración** y elige **Personal**.

**Buscar a una persona**

1. Escribe en **Buscar** parte del nombre, el título, la matrícula o el email.
2. Acota con **Rol** y **Activo** (Activos o Inactivos) si hace falta.

**Dar de alta a una persona**

1. Completa el formulario "Nueva persona": nombre completo, título, rol, matrícula y datos de contacto.
2. Deja marcado **Activo**.
3. Pulsa **Crear persona**. Si el rol es Ingeniero, ya aparece en el selector **Ingeniero responsable** de las OT.

**Dar de baja a una persona**

1. Pulsa **Editar** en la persona.
2. Desmarca **Activo** y guarda. Deja de aparecer en el selector; sus OT no cambian.

**Corregir datos que salen en la OT**

1. Edita la persona y corrige título, matrícula o teléfono.
2. Vuelve a abrir la impresión de la OT para ver los datos actualizados.

## Reglas y validaciones

Los mensajes de un campo aparecen debajo de él al guardar, y un resumen arriba del formulario.

| Situación | Mensaje |
| --- | --- |
| Nombre completo vacío | Indica el nombre completo. |
| Nombre ya usado por otra persona | Ya existe un registro con ese nombre. |
| Texto más largo que el máximo | El nombre completo no puede superar los 200 caracteres. (El título, 100; la matrícula, 50.) |
| Una OT con un responsable inactivo o que no es ingeniero | El ingeniero responsable debe ser una persona activa con rol Ingeniero. Revisa Personal. |
| Email inválido | El email debe tener un único @ con texto a ambos lados. |
| Rol inválido | Elige el rol: Ingeniero, Administrativo u Otro. |
| Otra persona guardó antes | Otra persona modificó este registro. Actualiza antes de guardar. |

- Los nombres reales del personal se cargan solo en la base de datos privada local; nunca en el repositorio, en
  pruebas ni en ejemplos.

## Preguntas abiertas

- Q7: qué ingenieros firman la OT y si se guarda la firma escaneada de cada uno ([preguntas](../engineer-questions.md)).
- Q9: si los teléfonos del pie de la OT son los de la empresa o los del ingeniero de cada OT.

## Notas técnicas

- Entidad agregada en esta ronda (decisión L2 de `odd/tasks/staff-help-polish.md`), migración `013_staff.sql`: tabla
  `staff` (`full_name` único sin distinguir mayúsculas, `title`, `role`, `license_number`, `email`, `phone`, `active`,
  `notes`, `revision`); nunca se borra ni se precarga.
- Rutas: `GET /staff?role=&active=` (filtros opcionales; un rol desconocido se rechaza), `GET /staff/{id}`,
  `POST /staff` (sin `active`, queda activo), `PUT /staff/{id}` con `revision`. Escrituras con `X-Agro-CSRF`.
- OT: `work_orders.responsible_staff_id` referencia al personal; `responsible_engineer` guarda el texto libre de las OT
  anteriores o la copia del nombre del responsable. La API exige que el responsable sea un ingeniero activo
  ("Responsible must be an active engineer") y bloquea sus datos hasta confirmar la OT.
- Dominio: `src/AgroOperations.Api/Domain/StaffCatalog.cs`. Pantalla: `web/app/backoffice/staff.jsx` y `staff.mjs`; campos en la
  entidad `staff` de `schema.mjs`.
- Las cuentas de acceso (identidad por invitación) siguen separadas: ver `docs/identity-contract.md`.
