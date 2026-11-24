# Work orders across fields, client data import and printable OT

Owner round of 2026-11-24 (second). Public repository: specs quote the owner verbatim but never contain real client,
contractor or product values; real data lives only in the private local database.

## Specs
- S1: "No veo aun que podamos desde el backoffice gestionar los campos de los clientes y los lotes asociados a esos
  campos. ASi cuando quiero dar de alta una OT no hay ninguna firma para la cual seleccionar lotes. Esos los podemos
  tomar inclusive de la info que tenemos en la carpeta del cliente." Import every client, field and plot of the
  workbook `BASEDATOS` sheet (client, establishment = field, plot, plot code, area) and the commercial firms of the
  `Registros` sheet linked to their client, with each firm's next OT number continuing after its highest workbook
  number. Idempotent, unverified provenance, private DB only. The client list makes it obvious how to open a
  client's fields and plots.
- S2: "Vamos guardando preguntas para los Ingenieros en un todolist del proyecto asi antes de reunirme te las puedo
  pedir y las traigo resueltas." `docs/engineer-questions.md`, checklist with evidence and current behaviour.
- S3: "Una OT es para un cliente y un campo y varios lotes de ese campo o como funciona la cardinalidad? ... cada OT
  es para un unico campo? Una OT un cliente un campo? O puede ser 1 OT 1 Cliente varios campos y lotes que se les
  ejecuta lo mismo? En una OT depende la labor puede ejecutar mas de 1 contratista? 1 OT puede tener mas de 1 labor?
  Siempre se utilizan insumos en una OT o hay ocasiones que son labores que no requieren insumos?" Per L2: one OT =
  one commercial firm (one client), one or more plots from one or more fields of that client, one labor, one
  contractor, zero or more inputs applied to every plot.
- S4: "Firma comercial y cliente es lo mismo? desde el punto de vista de la entidad, despues podemos cargar un cliente
  como ellos lo conozcan y firma comercial es un campo mas en el cliente que luego mostramos en el sistema si es asi
  como reconocen las personas o emrpesas para las que trabajan." Client is the main entity; the commercial firm is
  how the client is shown on documents. Creating a client creates its firm with the same name; existing clients
  without a firm get one; more firms per client remain possible (Q1). The OT form starts from the client.
- S5: "Recorda que la OT debemos sacar un PDF una vez generada el usuario la podria buscar y exportar un PDF para
  mandarlo al contratista, debe tener la estetica similar a las que ellos generan pero con el nuevo estilo que vamos a
  generar en el sitio." OT list searchable (number, firm, field, plot); a print view of one OT with the sections of
  the issued PDFs (header firm / Nº / date; labor; field-plot-area-worked area table with total; contractor;
  crop/rotation; inputs product-dose-total; observations; footer "No modificar a mano esta orden", contacts,
  responsible signature line) in the site style, exported with the browser's "Guardar como PDF", with a default file
  name in the workbook pattern `<FIRMA>_ORDEN_<n>_<CAMPO>_(<LOTE>)..._<ABREV>_<d-mmm>`.
- S6: "Otra de las preguntas es el manual de marca si tienen o logos y paletas de colores ... Asi afectamos el sitio
  con ese estilo y todos los documentos que salgan de aqui". Question Q16; colours and fonts of the print view come
  from one set of CSS variables so a future brand is one change.

## Tasks
- T1 (S2) inline: engineer questions document. | done
- T2 (S3, S4) worker: migration 009 (OT without single field, default firm per client + backfill), OT search, tests. | done (commit: OT across fields)
- T3 (S1) worker: workbook import of BASEDATOS clients/fields/plots and Registros firms with next numbers; real run. | done (commit: import hierarchy; real run in T5)
- T4 (S1, S3, S4, S5, S6) worker | done (commit: OT by client + print view): OT form by client with plots grouped by field, client list discoverability, OT
  search, print view and PDF file name.
- T5: launcher 009, docs, browser verification, push to main. | done

## Log
- L1 (owner, 2026-11-24, verbatim): "No veo aun que podamos desde el backoffice gestionar los campos de los clientes y
  los lotes asociados a esos campos. ASi cuando quiero dar de alta una OT no hay ninguna firma para la cual
  seleccionar lotes. Esos los podemos tomar inclusive de la info que tenemos en la carpeta del cliente. Algunos vamos
  a encontrar ahi mismo. Vamos guardando preguntas para los Ingenieros en un todolist del proyecto asi antes de
  reunirme te las puedo pedir y las traigo resueltas. Una OT es para un cliente y un campo y varios lotes de ese campo
  o como funciona la cardinalidad? Firma comercial y cliente es lo mismo? desde el punto de vista de la entidad,
  despues podemos cargar un cliente como ellos lo conozcan y firma comercial es un campo mas en el cliente que luego
  mostramos en el sistema si es asi como reconocen las personas o emrpesas para las que trabajan. Ademas, cada OT es
  para un unico campo? Una OT un cliente un campo? O puede ser 1 OT 1 Cliente varios campos y lotes que se les
  ejecuta lo mismo? En una OT depende la labor puede ejecutar mas de 1 contratista? 1 OT puede tener mas de 1 labor?
  Siempre se utilizan insumos en una OT o hay ocasiones que son labores que no requieren insumos? Recorda que la OT
  debemos sacar un PDF una vez generada el usuario la podria buscar y exportar un PDF para mandarlo al contratista,
  debe tener la estetica similar a las que ellos generan pero con el nuevo estilo que vamos a generar en el sitio.
  Otra de las preguntas es el manual de marca si tienen o logos y paletas de colores de la empresa de ingenieros
  agronomos para los que haceos el proyecto. Asi afectamos el sitio con ese estilo y todos los documentos que salgan
  de aqui"
- L2 (evidence, workbook `Registros`, 6,381 OTs / 11,206 lines): fields per OT 1: 5,555, 2: 578, 3: 160, 4: 65,
  5+: 23; plots per OT up to 5 (template limit); contractors per OT always 1; labors per OT 1 except 32; dates 1 except
  116; 212 OTs without products; plots share identical products in 6,230 OTs (6,204 with identical doses); 31 firms vs
  28 BASEDATOS clients, 29 firm names match a client name; each firm numbers its own OTs. Issued PDF of a 2-field OT
  confirms one header firm, one labor, one contractor, one crop, one input table and a total worked area.
- L3 (decision): open items from L2 (2 firms without client, differing products per plot, double labors/dates,
  signatures, map image, contacts, genetic events, prescriptions, areas, crop sequences, client stock, tenure, brand)
  are recorded as Q1-Q16 in `docs/engineer-questions.md`; behaviour follows the majority case until answered.
- L4 (decision): the PDF is produced from a print view with the browser's "Guardar como PDF" (no new server
  dependency); a server-generated PDF can follow if OTs must be sent automatically.
- L5 (T5 evidence): private DB backed up to `.local/backups/before-009-import.dump`; 009 applied (field_id gone). Real
  import: 27 clients, 167 fields, 458 plots created; the earlier client, 7 fields and 10 plots matched and got their
  10 codes; 28 firms got their next number; 2 workbook firms match no client (Q1); rerun creates nothing. Totals: 28
  clients, 174 fields, 468 plots (all coded), 28 firms, 0 clients without a firm. Browser at 1280/390 px: 28 clients
  with "Ver campos y lotes", OT form for a client with 29 fields, plots from two fields totalled, number hint shown,
  0 label overlaps, no horizontal overflow, no page errors; print view checked with an intercepted invented document
  (no real OT created): title = workbook file name, one A4 page. Polished amounts to the decimal comma and stopped
  repeating the formulation already in the product name.
