# Business reference

`operacion-del-negocio.md` describes how the company operates: clients, fields and plots, campaigns, crops, labors and
stages, work orders, agrochemicals, soil and seed analyses, and providers. It is the edited, corrected Spanish version of
the engineers' reference document (Google Docs) and keeps every fact and list of that document; nothing is added
(odd/tasks/shell-theme-ot-onescreen.md, S4 and L3).

Help files in `docs/help/` describe screens; this folder describes the operation those screens support.

## Ownership

The engineers own the content: their reference document is the source of truth. The development team maintains the
Markdown file in this repository and keeps it in sync with that reference. The file is not edited in the app.

## How the app shows it

The backoffice section "Documentación › Operación del negocio" renders the file with the same Markdown pipeline as the
help panel (`markdown.mjs` and `markdown-render.mjs`, no HTML passthrough), with a table of contents built from its
headings. "Descargar PDF" opens a print view styled like the work order print, which the browser saves as PDF.

## File format

- Language: neutral, professional Spanish.
- Front matter: simple `key: value` lines between two `---` lines, in this order:

| Key | Meaning |
| --- | --- |
| `title` | Title shown in the section and the print view. |
| `summary` | One line describing the document. |
| `source` | Where the content comes from. |
| `reviewed` | Date of the last content review, `yyyy-mm-dd`. |
| `version` | Integer; increases by one with every content change. |

- Allowed Markdown: one H1, H2 and H3 headings, paragraphs, lists, bold, italic, inline code, tables and links. No
  HTML, images, code blocks, footnotes or nested lists.
- No real client, contractor or person names.

## Update rule

When the engineers change their reference, apply the change in the same session: edit the file, set `reviewed:` to
that day, increase `version:` by one and add the new spelling or structure decisions to the list below. Do not add
rules, numbers or names that the reference does not contain; open questions go to `docs/engineer-questions.md`.

## Changes from the source

Version 1, 2026-11-28. Every change below is editorial; reviewers can check fidelity against this list.

**Structure**

- The source's numbering (1 to 10, with two items numbered 7 and labor groups numbered 2, 5, 6 and 7) is replaced by
  headings. Order: "Bases de datos generales" (Clientes, Campos y lotes, Campañas, Cultivos, Labores y etapas, Órdenes
  de trabajo, Agroquímicos), "Análisis de suelos", "Análisis de semillas", "Proveedores".
- Source item 2 is split into "Campos y lotes" and "Campañas". The crop list (source item 7) and the plot history
  paragraph sit under "Cultivos" with source item 3. The labor list (source item 6) sits under "Labores y etapas" with
  source item 4, before "Órdenes de trabajo" (source item 5).
- Field tenure, stages and seed quality data are shown as tables; client data, crop and agrochemical categories as
  lists; long paragraphs are split into short paragraphs and bullet lists.
- Labor groups are bold labels, not headings, so the table of contents stays short. The source has no title for the
  first list and for three groups; their titles are descriptive and the document says so: "Lista principal" (first
  list), "Labranza y preparación del suelo" (group before source group 2), "Protección de cultivos" and "Fertilización
  y enmiendas" (the untitled block between source groups 2 and 5, split where the missing groups 3 and 4 would be).
- Within each group, labors keep the source order. Items that appear twice in the source stay where they are
  ("Embolsado de grano" and "Extracción de grano" under Forrajes y ganadería, next to the more specific items under
  Cosecha).
- "Proveedores" has two subsections, "Proveedores de insumos de los clientes" and "Proveedores de servicios".
- Added: "Para qué sirve este documento" and a glossary. Glossary definitions use only statements of the source.

**Wording**

- First person plural ("nuestro trabajo", "nuestros clientes", "deberíamos") becomes third person ("los ingenieros",
  "los clientes", "debe").
- "deberían ser customizables" becomes "debe ser personalizable".
- "realizamos recomendación de extracción y análisis de suelos de diferentes nutrientes y/o parámetros" becomes "los
  ingenieros recomiendan la extracción de muestras y el análisis de suelos para distintos nutrientes o parámetros del
  suelo".
- "ter un fácil acceso a esa información información" becomes "debe ser fácil acceder a esa información".
- "semilla y/o fertilizante" becomes "semilla o fertilizante, o ambos".
- "si diferencias tarifa por cabezal" becomes "si se diferencia la tarifa por cabezal".
- "ej." becomes "por ejemplo".
- "laboratorios de suelos, semillas" becomes "laboratorios de suelos y de semillas", as in the provider types of the
  app (`docs/help/proveedores.md`).
- "Peso de 1000" keeps its name; its table detail reads "Peso de mil semillas".
- "Siembra de gruesa a placa fertilización simple" and the three similar seeding labors gain "con" ("a placa con
  fertilización simple").
- "Cincel con Rastra o Peine o Rolo" becomes "Cincel con rastra, peine o rolo"; "Rastra disco pesada (Excéntrica o
  "Rastra tiro excéntrico")" becomes "Rastra de discos pesada (excéntrica o "rastra de tiro excéntrico")".
- "Acarreo en lote ... hasta camión o silo" becomes "Acarreo en el lote ... hasta el camión o el silo"; "segadora de
  discos o hélice" and "rodillos o dedos" gain "de" before the second option.
- "Pulverización aérea/avión" and "aérea/dron" gain spaces around the slash.

**Spelling and accents**

- Inecticidas → Insecticidas; Funguicidas → Fungicidas; Terapicos semillas → Terápicos de semillas.
- Raygras → Raigrás; Maiz → Maíz; Maiz pizingallo → Maíz pisingallo; Maiz silo → Maíz silo; Pasturas consociada →
  Pasturas consociadas.
- Pulverizacion → Pulverización; Fertilizacion → Fertilización; liquida → líquida; neumatica → neumática.
- deberiamso → deberíamos (then third person); recomendaciond e extracicon → recomendación de extracción; ene l →
  en el; mese → meses; mas → más; limite → límite; Analisis → Análisis; especifico →
  específico; hibrido → híbrido; Aveces → A veces; pre definido → predefinidas; kml → KML.
- Double spaces removed ("rastra  de dientes", "Rastra  disco").
