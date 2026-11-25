# Staff, contextual help, validation messages and backoffice polish

Owner round of 2026-11-25. Public repository: staff names are entered only in the private local database.

## Specs
- S1: "Desalineado" — the OT list filter row (Buscar / Firma / Estado) is misaligned: controls of one row share
  height and top line; hints do not push their control out of line. Same for every filter row.
- S2: "En unidad si es Litros usemos L mayusculas porque sino la l miniscula parece una letra cortada" — litres are
  shown as "L" (and "L/ha") everywhere units are displayed (forms, lists, detail, print); stored values unchanged.
- S3: "El campo Ingeniero responsable deberia recuperarse desde un ABM de usuarios del sistema (que de momento los
  unicos dos son rol Ingenieros) ... a futuro lo puede cargar otra persona ... deberiamos traer de usuarios-personal
  de la compania, busquemos la mejor forma." Per L2: a Staff ("Personal") entity; the OT's responsible engineer is
  picked from active staff with role ingeniero; the printed OT shows name, title, licence and phone.
- S4: "La firma del cliente para el que estamos construyendo este proudcto se llama Terra_IA asi que ya empezamos a
  utilizar su nombre donde corresponda." Product brand shown as "Terra IA" (login, sidebar mark, page titles, print
  header), from one brand constant next to brand.css.
- S5: "A logica de que mostramos esto en el menu lateral?" — remove the recipe explanation from the sidebar (it moves
  to the help content).
- S6: "No me gusta el scrollbar en el menu ... quizas agrupadores para minimizar-colapsar items de menu" — collapsible
  menu groups (Operación, Catálogos, Planificación, Auditoría, Administración); the active section's group is open,
  open/closed state remembered per browser; no visible scrollbar in normal desktop heights.
- S7: "Una vez que se establece una orden como realizada el estado debe ser mas intuitivo con algun pill de color tal
  como lo hacemos en el listado" — the OT detail shows the status as the same coloured pill, next to the title.
- S8: "Despues de seleccionar un insumo debemos posicionar el cursor en dosis por ha automaticamente" — choosing an
  input in an OT line (and a quote line) focuses its dose/quantity field.
- S9: "En la vision de la OT ya generada, aprovechemos el ancho de la pantalla o usemos 2 columnas para mostrar todos
  los datos asi tambien entra el lote en una pantalla grande en la visual ... y si se ven los detalles de los insumos
  mucho mejor. Una OT es para una unica labor o tarea, eso puede ser una validacion del alta" — OT detail in a wide
  multi-column layout: header data in 2-3 columns, plots table and inputs table (product, formulation, mix order,
  dose, unit, total, hollow cone, out-of-range warnings) visible on a 1280x800 screen without scrolling past the
  header. One labor per OT is already enforced (single required labor).
- S10: "Este sistema merece la pena tener un boton de ayuda en cada interface, que deje documentada la herramienta y
  las entidades y la info necesaria ... para mi como sysadmin ... para los propios Ingenieros ... o bien para nuevo
  personal como forma de capacitacion ... a futuro podriamos incluir videos ilustrativos ... Quiza lo mejor sea
  desplegar un modal con la ayuda (segun te parezca mejor y manejemosno segun la industria maneja este tipo de
  cosas) Tambien la informacion deberia servir para algun agente IA ... podria tranquilamente quedar documentado en
  archivos markdown esto y luego lo levantamos en la UI." Per L3: one Markdown file per screen under `docs/help/`,
  shown in a contextual help side panel opened by a "?" button on every screen.
- S11: "En las OT o impresiones no mostremos datos internos del sistema como: 127.0.0.1:3000/backoffice/..." — the
  print/PDF shows no browser header or footer (URL, date, title, page numbers).
- S12: "Cuando tengamos mensajes de error no mostremos el generico 'please fill out this field' que sea personalizado
  segun el campo" — forms validate with Spanish, field-specific messages shown under the field; no browser bubbles.
- S13: "De momento los usuarios serian [ingeniero 1], Ingeniero Agronomo y [ingeniero 2] tambien Ing Agronomo." —
  entered in the private DB after T5 (never in the repository).

## Tasks
- T1 (S3) worker: migration 013 staff + OT responsible staff + document fields, API, tests. | done
- T2 (S1, S2, S4-S9, S11) worker: UI polish, parallel with T1. | done (browser check in T5)
- T3 (S10) worker: help content `docs/help/*.md`, parallel with T1 and T2. | done
- T4 (S3, S10, S12) worker: staff UI, OT responsible picker, help panel + renderer, custom validation.
- T5: launcher 013, real DB apply, staff entry (S13), browser check, push.

## Log
- L1 (owner, 2026-11-25, verbatim): "1. Desalineado [captura] 2. En unidad si es Litros usemos L mayusculas porque
  sino la l miniscula parece una letra cortada [captura] 3. El campo Ingeniero responsable deberia recuperarse desde
  un ABM de usuarios del sistema (que de momento los unicos dos son rol Ingenieros) y son los unicos que usan el
  sistema pero a futuro lo puede cargar otra persona a esto. Asi que deberiamos traer de usuarios-personal de la
  compania, busquemos la mejor forma. 4. La firma del cliente para el que estamos construyendo este proudcto se llama
  Terra_IA asi que ya empezamos a utilizar su nombre donde corresponda. 6. A logica de que mostramos esto en el menu
  lateral? [captura] 7. No me gusta el scrollbar en el menu, que otra opcion podemos encontrara, quizas agrupadores
  para minimizar-colapsar items de menu? no lo se [captura] 8. Una vez que se establece una orden como realizada el
  estado debe ser mas intuitivo con algun pill de color tal como lo hacemos en el listado o alguna estrategia que
  permita ver ese estado rapidamente. [captura] 9. Despues de seleccionar un insumo debemos posicionar el cursor en
  dosis por ha automaticamente para ya cargar el dato. 10. En la vision de la OT ya generada, aprovechemos el ancho de
  la pantalla o usemos 2 columnas para mostrar todos los datos asi tambien entra el lote en una pantalla grande en la
  visual. [captura] y si se ven los detalles de los insumos mucho mejor. Una OT es para una unica labor o tarea, eso
  puede ser una validacion del alta 11. Este sistema merece la pena tener un boton de ayuda en cada interface, que
  deje documentada la herramienta y las entidades y la info necesaria, de manera tal que quede conocimiento tanto para
  mi como sysadmin cuando tenga que entrar a realizar algo, para los propios Ingenieros (aunque ellos ya saben muy
  bien que comprende cada cosa pero pueden olvidar alguna cuestion tecnica de uso) o bien para nuevo personal como
  forma de capacitacion y de ayuda documental. De momento manejamos textos con definiciones de que es que se puede
  hacer y como, pero a futuro podriamos incluir videos ilustrativos de como manejar parte o total de la herramienta.
  Quiza lo mejor sea desplegar un modal con la ayuda (segun te parezca mejor y manejemosno segun la industria maneja
  este tipo de cosas) Tambien la informacion deberia servir para algun agente IA que corra y que sepa que funcion
  cumple cada componente o cada pantalla historicamente, asi que podria tranquilamente quedar documentado en archivos
  markdown esto y luego lo levantamos en la UI. 12. En las OT o impresiones no mostremos datos internos del sistema
  como: 127.0.0.1:3000/backoffice/work-orders/<id>/print 13. Cuando tengamos mensajes de error no mostremos el
  generico "please fill out this field" que sea personalizado segun el campo con errorMessages o lo que corresponda en
  cada campo. 14. De momento los usuarios serian [ingeniero 1], Ingeniero Agronomo y [ingeniero 2] tambien Ing
  Agronomo." (Names redacted in this public file; the private DB holds them and code never seeds them.)
- L2 (decision): login accounts (invitation-only identity) are not wired yet, so staff is a separate company-personnel
  entity (name, title, role ingeniero|administrativo|otro, licence number, email, phone, active, notes) that a future
  login account can link to. OTs keep the free-text responsible engineer of older rows; new and edited OTs pick a
  staff member. The URL on prints is the browser's header/footer, removed with a zero @page margin.
- L3 (decision): help as a non-blocking right-side panel (contextual help pattern used by SaaS backoffices: it can stay
  open while the user works), not a modal; Markdown files per screen with front matter (title, screen key, optional
  video URL, last reviewed date) so humans and AI agents read the same source.
