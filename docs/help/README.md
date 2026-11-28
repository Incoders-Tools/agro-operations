# Contextual help content

One Markdown file per backoffice screen. The backoffice shows the file of the current screen in a help side panel that
opens with the "Ayuda" button at the top right of the screen and can stay open while the user works (odd/tasks/staff-help-polish.md, S10 and L3). Humans
and AI agents read the same files, so this folder is also the functional reference for each screen.

Help files describe screens. The business-level reference, which describes the operation those screens support
(clients, plots, campaigns, crops, labors, work orders, analyses and providers), is
[docs/business/operacion-del-negocio.md](../business/operacion-del-negocio.md); its format and update rule are in
[docs/business/README.md](../business/README.md).

## Audience

- **Sysadmin**: how the local stack starts, where credentials and backups live, which API routes and tables back each
  screen (sections "Notas técnicas" and the `acceso-local` file).
- **Engineers**: reminders of rules, validations and less frequent tasks.
- **New staff**: training material, step by step ("Cómo se hace").
- **AI agents**: what each screen does, its entities, rules and open questions, without reading the code first.

## File format

- File name: the screen key in kebab-case plus `.md`, for example `ordenes-de-trabajo.md`.
- Language: neutral Spanish, the same imperative form as the UI ("Elige", "Busca").
- Front matter: simple `key: value` lines between two `---` lines, in this order:

| Key | Meaning |
| --- | --- |
| `screen` | Screen key; equals the file name without `.md`. |
| `title` | Title shown in the panel header. |
| `summary` | One line; also used by `index.md`. |
| `video` | Optional video URL; empty until a video exists. |
| `reviewed` | Date of the last content review, `yyyy-mm-dd`. |
| `related` | Other screen keys, written as `[a, b]`. |

- Body sections, in this order: "Para qué sirve", "Conceptos", "Cómo se hace", "Reglas y validaciones",
  "Preguntas abiertas", "Notas técnicas".
- Allowed Markdown: headings, paragraphs, lists, bold, inline code, tables and links. The panel renderer supports only
  these: no HTML, images, code blocks or footnotes.
- No real client, contractor, product or person names. Examples use invented values ("Cliente Ejemplo", "Campo Norte").
- Open engineer questions link to `docs/engineer-questions.md` by their `Q#`.

## How the UI loads a file

The UI resolves the current screen to its key and loads `docs/help/<key>.md`. Screens inside another screen (a plot
inside Clientes, the soil analysis panel inside a plot) use their own key when they are open. Section keys of the
backoffice navigation map to help keys as follows:

| UI section or view | Help key |
| --- | --- |
| `work-orders` | `ordenes-de-trabajo` |
| `/backoffice/work-orders/{id}/print` | `impresion-ot` |
| `clients` (client and field level) | `clientes` |
| `clients` (plot level) | `lotes` |
| Soil analysis panel of a plot | `analisis-de-suelo` |
| `seed-analyses` | `analisis-de-semillas` |
| `contractors` | `contratistas` |
| `providers` | `proveedores` |
| `quote-comparisons` | `comparacion-de-presupuestos` |
| `inputs` | `insumos` |
| `labor-types` | `labores` |
| `stages` | `etapas` |
| `crops` | `cultivos` |
| `campaigns` | `campanas` |
| `staff` (Administración › Personal) | `personal` |
| `plot-campaign-areas` | `superficie-por-campana` |
| `recipes`, `recipe-lines` | `recetas` |
| `import-records` | `procedencia` |
| Login screen and local stack | `acceso-local` |

`index.md` lists every screen with its title and summary.

One "Ayuda" button (accessible name "Ayuda de esta pantalla") sits in the top bar, at the right of the whole screen
(odd/tasks/shell-theme-ot-onescreen.md, S1). It opens the help of the most specific context on screen: the section,
the plot level of a client, or the soil analysis panel. The print view toolbar has its own "Ayuda" button and the
login screen a help link. The panel loads the file from
`/backoffice/help/<key>` (`web/app/backoffice/help/[key]/route.js`, served only in the local preview, keys matching
`^[a-z0-9-]+$`), renders it with `markdown.mjs` and `markdown-render.mjs` (no HTML passthrough) and follows the screen
the user moves to. Links to another help file open in the panel; `../engineer-questions.md` shows as text; web links
open in a new tab. The section-to-key mapping above lives in `web/app/backoffice/help.mjs` with the title of each file;
a test checks both against these files.

## Adding a video later

Set `video:` to the video URL (for example an unlisted video on the company's account) and update `reviewed:`. The
panel shows the link or player only when the value is not empty. Describe in the body what the video covers only if it
adds something the text does not already say. Never record real client data in a public video.

## Review rule

Update the screen's help file in the same commit that changes the screen's behaviour, fields, labels, messages or
routes, and set `reviewed:` to that day. A new screen ships with its help file and a row in `index.md` and in the table
above. A reviewer rejects a screen change whose help file contradicts it.
