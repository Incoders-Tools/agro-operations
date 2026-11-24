# Backoffice operational model — labors, contractors, inputs, hierarchy and work orders

Owner round of 2026-11-24 after first use of the private local backoffice. Public repository: specs quote the owner
verbatim but never contain real client, contractor or product values; those exist only in the private local database.

## Specs
- S1: "mira como se superpone la seleccion del campo con el nombre. Eso seguro pasa en todo el sitio." No backoffice
  label is covered by its control, focus ring or neighbour, on any form.
- S2: "En tipos de labor quiza podemos llamarlo simplemente labor o labores y cargar los datos que desde los excel ya
  conocemos, fertilizacion al voleo, pulverizacion terrestre, PULV TERRESTRE POSTEMERGENCIA, PULV TERRESTRE BARBECHO,
  SIEMBRA GRUESA, PRESIEMBRA, POSTEMERGENCIA, PREEMERGENCIA todos normalizados." Per L2, labor (what is done) and
  stage (when) are separate catalogs. The UI says "Labores". Labors carry a name and a short abbreviation (as in the
  workbook, e.g. `PULV-TE`) and are preloaded with the workbook labor catalog, normalized ("Pulverización terrestre",
  "Fertilización al voleo", "Siembra gruesa", ...). Stages are preloaded with "Barbecho", "Presiembra",
  "Preemergencia", "Postemergencia" plus the other workbook moments ("Postsiembra", "Prefloración", "Postfloración").
- S3: "Creo que no tenemos pero deberiamos tener un ABM de contratistas. Es un dato que se carga en las OT". Contractor
  ABM with name, CUIT, phone, email, notes and an "own equipment" flag (the workbook value `PROPIO`).
- S4: "Los campos deberian estar asociado a los Lotes, es decir un campo tiene muchos lotes." The schema already holds
  field 1-N plots; the UI must make it visible: client → fields → plots, creating a plot from its field.
- S5: "Aun no visualizo el alta de OT y todos sus asociados, clientes, campo, lote fecha, labores a realizar, insumos
  (para esto abm de insumos) etc etc." Work order (OT) as in the workbook `Orden` sheet: consecutive number, date,
  client (commercial firm), labor, stage, contractor, field, one or more plots of that field each with its area and
  worked area ("HAS TAREA"), crop/hybrid, input lines (input, dose per ha, unit, total = dose × worked area),
  observations and responsible engineer. Numbers are consecutive and never reused; a cancelled OT keeps its number
  and stays queryable. A dose outside the input's min/max range is warned, never silently changed.
- S6: Inputs ABM (needed by S5): commercial name, active ingredient and concentration, formulation abbreviation and
  mixing order (e.g. "(3 SC)"), category (fertilizante, herbicida, insecticida, fungicida, coadyuvante, curasemilla,
  bioestimulante, terápico), unit, dose min/max, reference cost and "requires hollow cone" flag.
- S7: "Los ABMS que visualizo en el sistema muchos son solamente de nombre de entidades siendo que ya hay mucha info que
  te fui pasando de campos, relaciones entre entidades, etc que ya debemos empezar a ir modelando." Model the known
  attributes: client (one or more commercial firms with CUIT, producers, email, phone, address), field (tenure:
  propio, alquiler fijo, alquiler a porcentaje), plot (short ID code client-field-plot), crop (kind: renta, servicio,
  pastura; customizable list).
- S8: Per L4, contractors and inputs are preloaded from the workbook (`ACTIV-INSUMOS-MOMENTOS`), marked imported and
  unverified with provenance, only into the private local database by a tool that reads the workbook at run time.

## Tasks
- T1 (S1, S2 rename) inline: form field wrapper grid; "Tipos de labor" → "Labores". | done (commit: this one; visual check in T8)
- T2 (S2, S3, S6) worker: migration 006 (labor abbreviation, stages, contractors, inputs) + seeds, domain, HTTP, tests.
- T3 (S4, S7) worker: migration 007 (client/firm/field/plot/crop attributes), domain, HTTP, tests.
- T4 (S5) worker: migration 008 work orders, numbering, cancel, lines, totals, dose warnings, HTTP, tests.
- T5 (S1, S3, S4, S6, S7) worker: backoffice UI for hierarchy and attribute forms, contractors, inputs.
- T6 (S5) worker: OT create/list/detail/cancel UI.
- T7 (S8) worker: local workbook import for contractors and inputs with provenance; run against private DB.
- T8: launcher applies 006-008, docs, end-to-end verification.

## Log
- L1 (owner, 2026-11-24, verbatim): "Ya pude acceder y ya comence encontrando errores de estilos: [captura] mira como se
  superpone la seleccion del campo con el nombre. Eso seguro pasa en todo el sitio. 2. En tipos de labor quiza podemos
  llamarlo simplemente labor o labores y cargar los datos que desde los excel ya conocemos, fertilizacion al voleo,
  pulverizacion terrestre, PULV TERRESTRE POSTEMERGENCIA, PULV TERRESTRE BARBECHO, SIEMBRA GRUESA, PRESIEMBRA,
  POSTEMERGENCIA, PREEMERGENCIA todos normalizados. 3. Creo que no tenemos pero deberiamos tener un ABM de contratistas.
  Es un dato que se carga en las OT 4. Los campos deberian estar asociado a los Lotes, es decir un campo tiene muchos
  lotes. 5. Aun no visualizo el alta de OT y todos sus asociados, clientes, campo, lote fecha, labores a realizar,
  insumos (para esto abm de insumos) etc etc. Los ABMS que visualizo en el sistema muchos son solamente de nombre de
  entidades siendo que ya hay mucha info que te fui pasando de campos, relaciones entre entidades, etc que ya debemos
  empezar a ir modelando."
- L2 (owner choice): "Separar labor y etapa (Recomendado)".
- L3 (owner): agents may always read the source workbook folder on D: with full privileges; it is a copy, the original
  is in the client's secure repository.
- L4 (owner choice): "Precargar desde la planilla".
- L5 (evidence): CSS cause of S1 — relation selects sat in a `<div>` outside the form grid gap, so the 3px+3px focus
  ring overlapped the label. Workbook `Registros` has 11,207 OT lines (6 product slots each); `ACTIV-INSUMOS-MOMENTOS`
  holds 16 labors with abbreviations, ~370 inputs with unit/dose range/cost, 28 contractors, 23 crops and 8 moments.
  The existing `recipes` table keeps its own three-stage check; recipes are out of scope for this round.
