# Backoffice design fixes: header spacing, single-field search, focus, fixed menu, loading state

Owner round of 2026-11-24 after using the new modules. UI only (web/app/backoffice).

## Specs
- S1: "Casi se pisan el acento y la indicacion del usuario" — the page title's accents (e.g. "Órdenes") must not
  touch the "Registro privado · email" line above it, on any section, at 1280 and 390 px.
- S2: "Porque usamos dos campos para buscadores, ejemplo el de clientes ... usemos un unico campo que sea buscador y
  que permita al usuario con flecha hacia abajo o hacia arriba desplazarse entre los resultados de busqueda para el
  input y poder hacer enter o click y que quede seleccionado el cliene (o entidad que sea, tambien vi que pasa lo
  mismo con lotes e insumos y seguro con todas las entidades del sistema)". One accessible combobox for every entity
  selection (clients, firms, fields, plots, inputs, labors, contractors, crops, campaigns, stages, providers, quotes,
  recipes, ...): type to filter (accent/case-insensitive), ArrowDown/ArrowUp move the active option, Enter or click
  selects, Escape closes, the selected entity's label shows in the same field. Small fixed enumerations (tenure,
  role, currency, status, sampling method, category, position) may stay native selects.
- S3: "Cuando un usuario agrega item de insumo inmediatamente despues debe haber focus en campo busqueda." After
  "Agregar insumo" (and any add-line action with a searchable entity: quote lines, soil results' first field), focus
  moves to the new line's search field.
- S4: "No proyectemos el menu por tan grande que sea el form ... siempre menu completo a la izquierda." The desktop
  sidebar stays fixed to the viewport (full height, own scroll if needed, logout always visible) while the content
  scrolls.
- S5: "Cuando seleccione la tool de insumos tardo en cargarse y no hubo loader ni nada que impida al usuario ejecutar
  acciones mientras cargamos." Every section and detail load shows a visible loading state and blocks actions on that
  content until loaded (no clickable stale form/list), including Insumos.

## Tasks
- T1 (S1-S5) worker: combobox component + adoption everywhere, focus, sticky sidebar, loading states, tests. | done 7dd109c
- T2: browser verification at 1280/390, push to main. | done

## Log
- L1 (owner, 2026-11-24, verbatim): "Perfecto dejamos ahi las preguntas y algunos ajustes de diseo. 1. Casi se pisan
  el acento y la indicacion del usuario [captura] 2. Porque usamos dos campos para buscadores, ejemplo el de clientes
  [captura] usemos un unico campo que sea buscador y que permita al usuario con flecha hacia abajo o hacia arriba
  desplazarse entre los resultados de busqueda para el input y poder hacer enter o click y que quede seleccionado el
  cliene (o entidad que sea, tambien vi que pasa lo mismo con lotes e insumos y seguro con todas las entidades del
  sistema) 3. Cuando un usuario agrega item de insumo inmediatamente despues debe haber focus en campo busqueda. 4. No
  proyectemos el menu por tan grande que sea el form [captura] siempre menu completo a la izquierda. 5. Cuando
  seleccione la tool de insumos tardo en cargarse y no hubo loader ni nada que impida al usuario ejecutar acciones
  mientras cargamos."
- L2 (evidence): screenshots show the h1 with line-height 1 so "Ó" rises into the kicker; "Buscar cliente" text box
  plus a separate "Cliente" select; the sidebar ending mid-page with the form scrolled. The OT plots list is already
  one filter field plus checkboxes (multi-select) and stays that way.
- L3 (T2 evidence, browser on real data, read-only, 1280 and 390 px): Insumos shows a "Cargando" status right after
  the switch; title glyphs sit 18-19 px below the kicker; OT client combobox: typing filters, ArrowDown moves
  aria-activedescendant, Enter chooses and closes (aria-expanded false), Escape restores the chosen label, no "Buscar
  cliente" field left; labor arrows skip category headers; "Agregar insumo" focuses bo-wo-line-<key>-input; desktop
  sidebar stays at top 0 with logout visible after scrolling 2,000 px; 0 label overlaps, no overflow, no page errors.
