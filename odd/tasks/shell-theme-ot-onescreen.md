# Global help button, user settings with dark theme, quick discard, business document and one-screen OT

Owner round of 2026-11-28. Public repository: no client values.

## Specs
- S1: "Debemos corregir el boton de ayuda no me gusto del todo donde quedo ubicado al lado de cada entidad. Lo haria
  mas alejado quiza arriba a la derecha de toda la pantalla y con un boton mas intuitivo directamente que diga
  'Ayuda'". One "Ayuda" button (icon + text) in a top bar at the top right of the whole screen, opening the existing
  help panel for the current context (section, client hierarchy level, soil panel, print view); the per-section "?"
  buttons are removed.
- S2: "Debemos tener asociado al usuario logueado una seccion donde va a contar con configuraciones propias y una
  accion rapida hacia multi-theme donde manejemos 2 themes un light que es el actual por defecto y un dark." Top-right
  user menu (name/email) with a quick light/dark toggle, "Mi cuenta" and "Cerrar sesión". "Mi cuenta" shows the
  logged-in identity and its preferences (theme for now). Preferences are stored per logged-in user in the API and
  cached locally so the page renders in the right theme without a flash. Light is the default; print stays light.
- S3: "Cuando entro a editar una entidad, ejemplo OT que tiene muchos campos, no puedo volver rapidamente hacia atras
  hacia el listado, hasta tanto no baje abajo de todo el form a cancelar. Deberia tener un cancelar tambien arriba o
  descartar ... Este caso es cuando se abre el form completo." Every full-page form (OT, quote comparison/quote, soil
  and seed analyses, generic record forms) has a sticky action bar at the top with "Descartar" (back to where the
  user came from; asks for confirmation only when there are unsaved changes) and the primary save action; Escape
  outside an open combobox does the same as Descartar.
- S4: "La informacion que nos definieron de la operatoria del negocio [documento] me gustaria manterla actualziada
  dentro del sistema en alguna seccion ... de ayuda o de documentacion de la operacion o del negocio, para que quede
  documentado y exportable a PDF ... con el estilo del sitio y separando bien titulos, subtitulos, descripciones ...
  con ortografia sintaxis corregida en caso de ser necesario ... bien fluido limpio y facil de digerir." A
  "Documentación › Operación del negocio" section renders `docs/business/operacion-del-negocio.md` (edited, corrected,
  well-structured Spanish version of the engineers' reference: same facts, nothing invented) with a table of contents,
  in the site style, and "Descargar PDF" through a print view styled like the OT print. The file is the single source
  kept up to date in the repository (like help); the help README links to it.
- S5: "El alta de OT actualmente tengo que scrollear, la idea es que de un solo pantallazo el usuario pueda ver todo lo
  que involucra. Tal como lo tienen en su excel actual, pero mejorado a sistema." The OT create/edit form is a dense
  sheet-like layout mirroring the workbook `Orden` sheet: header band (cliente/firma, Nº, fecha, labor, etapa,
  contratista, campaña, cultivo/variedad, responsable), plots block (campo, lote, has, has tarea, total) and inputs
  block (producto, dosis/ha, unidad, total) side by side, plus a right "Chequeos" panel like the workbook's helper
  columns: dose check per input (mínimo / máximo, coloured within/out of range), estimated cost per input and total
  from the input's reference cost (labelled as reference, currency per Q19), and the crop/variety. Observaciones at
  the bottom. On a 1440x900 viewport the whole form (with a 2-field client, 5 selected plots, 4 inputs) fits without
  page scroll; long plot lists scroll inside their block. Narrow screens stack.

## Tasks
- T1 (S2) worker: migration 014 user preferences + /api/v1/me/preferences + tests.
- T2 (S4) worker: business document Markdown (content only), parallel.
- T3 (S1, S2, S3 generic) worker: top bar with Ayuda and user menu, Mi cuenta, dark theme via variables, shared
  top action bar for generic forms, business document section + print, parallel with T1-T2.
- T4 (S3 OT/quotes/analyses, S5) worker: one-screen OT form with Chequeos panel; top action bar on the remaining
  full forms.
- T5: launcher 014, docs/help updates, real DB apply, browser verification at 1440x900 and 390 px, push.

## Log
- L1 (owner, 2026-11-28, verbatim): "1. Debemos corregir el boton de ayuda no me gusto del todo donde quedo ubicado al
  lado de cada entidad. Lo haria mas alejado quiza arriba a la derecha de toda la pantalla y con un boton mas intuitivo
  directamente que diga 'Ayuda' 2. Debemos tener asociado al usuario logueado una seccion donde va a contar con
  configuraciones propias y una accion rapida hacia multi-theme donde manejemos 2 themes un light que es el actual por
  defecto y un dark. 3. Cuando entro a editar una entidad, ejemplo OT que tiene muchos campos, no puedo volver
  rapidamente hacia atras hacia el listado, hasta tanto no baje abajo de todo el form a cancelar. Deberia tener un
  cancelar tambien arriba o descartar o lo que consideres para tener mas rapido el acceso de volver si me equivoque o
  no deseo finalmente editar. Este caso es cuando se abre el form completo. 4. La informacion que nos definieron de la
  operatoria del negocio [Google document] me gustaria manterla actualziada dentro del sistema en alguna seccion que
  consideres correspondiente o de ayuda o de documentacion de la operacion o del negocio, para que quede documentado y
  exportable a PDF, eso indica la actividad y sirve tanto para alguien de soporte, como para personal de la compania
  en un futuro, y hagamos que sea con el estilo del sitio y separando bien titulos, subtitulos, descripciones. Que sea
  un documento con estilo adecuado al sitio y bien ordenado con ortografia sintaxis corregida en caso de ser
  necesario. Busquemos la forma de hacerlo bien fluido limpio y facil de digerir. 5. El alta de OT actualmente tengo
  que scrollear, la idea es que de un solo pantallazo el usuario pueda ver todo lo que involucra. Tal como lo tienen
  en su excel actual, pero mejorado a sistema fijate como es una OT estructuralmente al dia de hoy: [captura]"
- L2 (evidence, workbook screenshot): left block is the OT (firma | Nº/fecha; labor; campo/lotes/has/has tarea with
  total; contratista | cultivo/rotación; insumos producto/dosis por ha/total); right helper block: lot finder, crop
  check, hybrid characteristics, dose check mínimo/máximo coloured green/red, cost per hectare and total.
- L3 (decision): the business document lives as Markdown in the repository and is rendered in-app (same pipeline as
  help), not edited in the UI; in-app editing can follow if the engineers need to change it themselves. Preferences
  are keyed by the logged-in identity email (today the temporary local admin), ready for invitation-based accounts.
