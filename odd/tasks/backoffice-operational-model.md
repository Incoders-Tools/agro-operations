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
  propio, alquiler fijo, alquiler a porcentaje), plot (short ID code client-field-plot), crop catalog (customizable,
  seeded with the reference list) and, per L6, the plot's crops per campaign (up to two, each renta or servicio,
  with optional hybrid/variety) as the plot history the OT "cultivo" comes from.
- S8: Per L4, contractors and inputs are preloaded from the workbook (`ACTIV-INSUMOS-MOMENTOS`), marked imported and
  unverified with provenance, only into the private local database by a tool that reads the workbook at run time.

## Tasks
- T1 (S1, S2 rename) inline: form field wrapper grid; "Tipos de labor" → "Labores". | done (commit: this one; visual check in T8)
- T2 (S2, S3, S6) worker: migration 006 (labor abbreviation, stages, contractors, inputs) + seeds, domain, HTTP, tests. | done (commit: catalogs)
- T3 (S4, S7) worker: migration 007 (client/firm/field/plot/crop attributes), domain, HTTP, tests. | done (commit: attributes)
- T4 (S5) worker: migration 008 work orders, numbering, cancel, lines, totals, dose warnings, HTTP, tests. | done (commit: work orders API)
- T5 (S1, S3, S4, S6, S7) worker: backoffice UI for hierarchy and attribute forms, contractors, inputs. | done (commit: masters UI; browser check in T8)
- T6 (S5) worker: OT create/list/detail/cancel UI. | done (commit: work orders UI; browser check in T8)
- T7 (S8) worker: local workbook import for contractors and inputs with provenance; run against private DB. | done (commit: import tool; real run in T8)
- T8: launcher applies 006-008, docs, end-to-end verification. | done (commits 81e56bd, 3c5b6fe, docs)

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
- L6 (decision from the domain reference): renta/servicio describes how a crop is used on a plot in a campaign, not
  the crop itself ("con dos cultivos donde los dos pueden ser de renta o uno de servicio"), so it lives on the
  plot-campaign crop record, not on the crop catalog.
- L7 (T2 evidence): 006_catalogs.sql adds labor abbreviations + 14 seeded labors, 7 stages, contractors (seed
  "Propio"), inputs; duplicate names now return 409 "Duplicate name" on every name catalog (crops and campaigns
  returned 400 before). Private DB had 0 labors, so no accent collision with the seed.
- L8 (evidence and decision): issued OT file names are `<FIRMA>_ORDEN_<n>_...` and `Registros` pairs `N° ORDEN` with
  `FIRMA COMERCIAL`, so OT numbers are consecutive per commercial firm. Each firm has an editable next number so the
  numbering can continue from the workbook. The owner's ordering rules say numbers must not be skipped and a
  cancelled OT keeps its number, so numbers are allocated in the creating transaction (no sequence gaps).
- L9 (verify, T8): independent verifier found B1 — Windows PowerShell 5.1 pipes migrations to psql as US-ASCII, so
  accented seed names (006 labors/stages, 007 crops) would be stored with '?'; caught before the private DB got
  006-008. Fixed with $OutputEncoding = UTF-8 in the launcher and a Windows smoke regression (RED: missing setting;
  ASCII probe shows 3F). B2 — a real commercial hybrid and a real-looking variety in tests replaced with invented
  values. Migrations: no row loss, rerunnable, partial schemas refused. Numbering: gapless and race-safe under probes.
- L10 (T8 evidence): private DB backed up to ignored `.local/backups/before-006-008.dump`, then 006-008 applied by the
  launcher: 0 names with '?', 14 labors, 7 stages, 28 crops; masters intact (1 client, 7 fields, 10 plots). Workbook
  import created 27 contractors and 367 inputs (147 with mix order, 181 with dose range), all unverified with
  provenance; a second dry run creates nothing. 36 dose rows did not match an input name exactly (9 differ only in
  the trailing parenthetical) and those inputs came in without doses. Browser check at 1280 and 390 px over 8
  screens: 0 label overlaps, 0 UI errors; found and fixed a phone-width overflow (page ~1400px wide) in 3c5b6fe.
  No OT was created in the private DB (no firm exists yet).
