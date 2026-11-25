// Contextual help (S10, L3): which docs/help/<key>.md file each screen shows; the help route (help-file.mjs) accepts
// only keys matching helpKeyPattern. The mapping is the table in docs/help/README.md; a test keeps both in step with the files.

/** A help key is a file name without extension: lowercase letters, digits and hyphens, so it can never leave docs/help. */
export const helpKeyPattern = /^[a-z0-9-]+$/;

/** Title of every screen's help file, as in its front matter; related screens are listed by these titles. */
export const helpScreens = {
  'ordenes-de-trabajo': 'Órdenes de trabajo',
  'impresion-ot': 'Impresión de la OT',
  clientes: 'Clientes',
  lotes: 'Lotes',
  'analisis-de-suelo': 'Análisis de suelo',
  'analisis-de-semillas': 'Análisis de semillas',
  contratistas: 'Contratistas',
  proveedores: 'Proveedores',
  'comparacion-de-presupuestos': 'Comparación de presupuestos',
  insumos: 'Insumos',
  labores: 'Labores',
  etapas: 'Etapas',
  cultivos: 'Cultivos',
  campanas: 'Campañas',
  personal: 'Personal',
  'superficie-por-campana': 'Superficie por campaña',
  recetas: 'Recetas',
  procedencia: 'Procedencia',
  'acceso-local': 'Acceso local y administración',
};

// Backoffice section key -> help key. Nested contexts (a plot inside Clientes, its soil analyses, the print view and the
// login screen) pass their own key to the help button.
const sections = {
  'work-orders': 'ordenes-de-trabajo', clients: 'clientes', 'seed-analyses': 'analisis-de-semillas', contractors: 'contratistas',
  providers: 'proveedores', 'quote-comparisons': 'comparacion-de-presupuestos', inputs: 'insumos', 'labor-types': 'labores',
  stages: 'etapas', crops: 'cultivos', campaigns: 'campanas', staff: 'personal', 'plot-campaign-areas': 'superficie-por-campana',
  recipes: 'recetas', 'recipe-lines': 'recetas', 'import-records': 'procedencia',
};

/** The help key of a backoffice section; null for a section without help. */
export const helpKeyFor = section => sections[section] ?? null;
