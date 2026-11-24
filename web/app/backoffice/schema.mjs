// Entity definitions shared by the forms and the request payloads. Field keys match the API JSON (camelCase).

export const tenures = [
  { value: 'propio', label: 'Propio' },
  { value: 'alquiler_fijo', label: 'Alquiler fijo' },
  { value: 'alquiler_porcentaje', label: 'Alquiler a porcentaje' },
];
export const roles = [{ value: 'renta', label: 'Renta' }, { value: 'servicio', label: 'Servicio' }];
export const categories = [
  ['fertilizante', 'Fertilizante'], ['herbicida', 'Herbicida'], ['insecticida', 'Insecticida'], ['fungicida', 'Fungicida'],
  ['coadyuvante', 'Coadyuvante'], ['curasemilla', 'Curasemilla'], ['bioestimulante', 'Bioestimulante'],
  ['terapico', 'Terápico de semillas'], ['inoculante', 'Inoculante'],
].map(([value, label]) => ({ value, label }));
// Labor groups in the order of the engineers' domain reference.
export const laborCategories = [
  ['aplicacion', 'Aplicación'], ['labranza', 'Labranza y preparación'], ['siembra', 'Siembra e implantación'],
  ['cosecha', 'Cosecha y poscosecha'], ['forrajes', 'Forrajes y ganadería'], ['infraestructura', 'Mantenimiento de infraestructura'],
].map(([value, label]) => ({ value, label }));

export const optionLabel = (options, value) => options.find(option => option.value === value)?.label ?? value;

const name = { key: 'name', label: 'Nombre', type: 'text', maxLength: 200 };
const taxId = { key: 'taxId', label: 'CUIT', type: 'text', optional: true, inputMode: 'numeric', hint: '11 dígitos, con o sin guiones.' };
const email = { key: 'email', label: 'Email', type: 'email', optional: true };
const phone = { key: 'phone', label: 'Teléfono', type: 'tel', optional: true };
const yieldKg = { type: 'number', optional: true, min: '0', max: '9999999999.99', step: '0.01' };
const dose = { type: 'number', optional: true, min: '0.0001', step: '0.0001' };
const joined = parts => parts.filter(Boolean).join(' · ');

export const entities = {
  clients: {
    title: 'Clientes', singular: 'cliente', search: ['name', 'taxId'],
    fields: [
      name, taxId,
      { key: 'producers', label: 'Productores', type: 'textarea', optional: true, hint: 'Una o más personas productoras del cliente.' },
      email, { ...phone, label: 'Celular' },
      { key: 'address', label: 'Dirección', type: 'text', optional: true },
    ],
    detail: row => joined([row.taxId && `CUIT ${row.taxId}`, row.email, row.phone]),
  },
  'commercial-firms': {
    title: 'Firmas comerciales', singular: 'firma comercial', feminine: true, parent: 'clientId',
    fields: [
      name, taxId,
      {
        key: 'nextWorkOrderNumber', label: 'Próximo Nº de orden', type: 'integer', optional: true, editOnly: true, min: '1', step: '1',
        hint: 'Continúa la numeración de la planilla. Debe ser mayor que la última orden emitida; vacío mantiene el actual.',
      },
    ],
    detail: row => joined([row.taxId ? `CUIT ${row.taxId}` : 'Sin CUIT', row.nextWorkOrderNumber && `Próxima orden Nº ${row.nextWorkOrderNumber}`]),
  },
  fields: {
    title: 'Campos', singular: 'campo', parent: 'clientId',
    fields: [
      name,
      { key: 'tenure', label: 'Tenencia', type: 'select', optional: true, options: tenures, placeholder: 'Sin indicar' },
      { key: 'locality', label: 'Localidad', type: 'text', optional: true },
      {
        key: 'clientId', label: 'Mover a otro cliente', type: 'select', options: 'clients', editOnly: true, move: true,
        hint: 'Elige otro cliente solo si el campo cambió de productor. Las órdenes ya emitidas no cambian.',
      },
    ],
    detail: row => joined([row.tenure && optionLabel(tenures, row.tenure), row.locality]),
  },
  plots: {
    title: 'Lotes', singular: 'lote', parent: 'fieldId',
    fields: [
      { key: 'code', label: 'Código', type: 'text', optional: true, maxLength: 40, hint: 'Identificador corto cliente-campo-lote.' },
      name,
      { key: 'areaHectares', label: 'Superficie (ha)', type: 'number', min: '0.0001', max: '99999999.9999', step: '0.0001' },
      {
        key: 'fieldId', label: 'Mover a otro campo', type: 'select', options: 'fieldsByClient', editOnly: true, move: true,
        hint: 'Elige otro campo, también de otro cliente, solo si el lote cambió de campo. Las órdenes ya emitidas no cambian.',
      },
    ],
    label: row => row.code ? `${row.code} · ${row.name}` : row.name,
    detail: row => `${row.areaHectares} ha`,
  },
  'plot-crops': {
    title: 'Cultivos por campaña', singular: 'cultivo de campaña', parent: 'plotId',
    fields: [
      { key: 'campaignId', label: 'Campaña', type: 'select', options: 'campaigns', createOnly: true, placeholder: 'Selecciona una campaña' },
      {
        key: 'position', label: 'Posición', type: 'select', integer: true, createOnly: true, initial: '1',
        options: [{ value: '1', label: '1 · primer cultivo' }, { value: '2', label: '2 · segundo cultivo' }],
        hint: 'Un lote admite hasta dos cultivos por campaña.',
      },
      { key: 'cropId', label: 'Cultivo', type: 'select', options: 'crops', placeholder: 'Selecciona un cultivo' },
      { key: 'role', label: 'Uso', type: 'select', options: roles, initial: 'renta', hint: 'Renta o servicio en esta campaña.' },
      { key: 'variety', label: 'Variedad o híbrido', type: 'text', optional: true },
      { key: 'grainYieldKgHa', label: 'Rendimiento de grano (kg/ha)', ...yieldKg, hint: 'Rendimiento promedio de cosecha.' },
      { key: 'forageBiomassKgHa', label: 'Biomasa de forraje (kg/ha)', ...yieldKg },
    ],
  },
  contractors: {
    title: 'Contratistas', singular: 'contratista', search: ['name', 'taxId'],
    fields: [
      name, taxId, phone, email,
      { key: 'website', label: 'Sitio web', type: 'text', inputMode: 'url', optional: true, maxLength: 300, hint: 'Ej. www.ejemplo.com.ar' },
      { key: 'notes', label: 'Notas', type: 'textarea', optional: true },
      { key: 'ownEquipment', label: 'Equipo propio', type: 'checkbox', hint: 'Maquinaria del propio cliente (PROPIO en la planilla).' },
    ],
    detail: row => joined([row.ownEquipment && 'Equipo propio', row.taxId && `CUIT ${row.taxId}`, row.phone, row.email]),
  },
  inputs: {
    title: 'Insumos', singular: 'insumo', search: ['commercialName', 'activeIngredient'],
    fields: [
      { key: 'commercialName', label: 'Nombre comercial', type: 'text', maxLength: 200 },
      { key: 'activeIngredient', label: 'Principio activo', type: 'text', optional: true },
      { key: 'concentration', label: 'Concentración', type: 'text', optional: true, hint: 'Ej. 48 %' },
      { key: 'formulation', label: 'Formulación', type: 'text', optional: true, hint: 'Abreviatura, ej. SC.' },
      { key: 'mixOrder', label: 'Orden de mezcla', type: 'integer', optional: true, min: '1', max: '20', step: '1', hint: 'De 1 a 20.' },
      { key: 'category', label: 'Categoría', type: 'select', optional: true, options: categories, placeholder: 'Sin indicar' },
      { key: 'unit', label: 'Unidad', type: 'text', hint: 'Ej. l, kg, cc.' },
      { key: 'doseMin', label: 'Dosis mínima por ha', ...dose },
      { key: 'doseMax', label: 'Dosis máxima por ha', ...dose },
      { key: 'referenceCost', label: 'Costo de referencia', type: 'number', optional: true, min: '0', step: '0.0001' },
      { key: 'hollowCone', label: 'Requiere cono hueco', type: 'checkbox' },
    ],
    label: row => row.commercialName,
    detail: row => joined([
      row.activeIngredient, row.category && optionLabel(categories, row.category),
      (row.doseMin != null || row.doseMax != null) ? `${row.doseMin ?? '…'}–${row.doseMax ?? '…'} ${row.unit}/ha` : row.unit,
      row.hollowCone && 'Cono hueco',
    ]),
  },
  'labor-types': {
    title: 'Labores', singular: 'labor', feminine: true,
    search: ['name', 'abbreviation', row => optionLabel(laborCategories, row.category), 'description'],
    fields: [
      name, { key: 'abbreviation', label: 'Abreviatura', type: 'text', optional: true, maxLength: 12, hint: 'Ej. PULV-TE' },
      { key: 'category', label: 'Categoría', type: 'select', optional: true, options: laborCategories, placeholder: 'Sin categoría' },
      { key: 'description', label: 'Descripción', type: 'textarea', optional: true, hint: 'Aclaración de la labor, ej. equipo o cultivos.' },
    ],
    detail: row => joined([row.category ? optionLabel(laborCategories, row.category) : 'Sin categoría', row.abbreviation, row.description]),
    sort: rows => laborGroups(rows).flatMap(group => group.labors),
  },
  stages: {
    title: 'Etapas', singular: 'etapa', feminine: true,
    fields: [name, { key: 'sortOrder', label: 'Orden', type: 'integer', step: '1', hint: 'Posición de la etapa en el ciclo del cultivo.' }],
    detail: row => `Orden ${row.sortOrder}`,
  },
  crops: { title: 'Cultivos', singular: 'cultivo', fields: [name] },
  campaigns: { title: 'Campañas', singular: 'campaña', feminine: true, fields: [name] },
};

export const rowLabel = (entity, row) => entities[entity].label?.(row) ?? row.name;

function value(field, raw) {
  if (field.type === 'checkbox') return Boolean(raw);
  const text = typeof raw === 'string' ? raw.trim() : raw == null ? '' : String(raw);
  if (field.type === 'number' || field.type === 'integer' || field.integer) return text === '' ? null : Number(text);
  return text === '' && field.optional ? null : text;
}

// Shown and sent only where it applies: create-only fields identify a record, edit-only ones exist after creation.
export const fieldApplies = (field, editing) => editing ? !field.createOnly : !field.editOnly;

// Creating sends the parent key and every field; editing sends the revision and omits create-only fields.
// A move field (new parent) is sent only when it differs from the edited record, so omitting it keeps the parent.
export function payloadFor(entity, form, { revision, parent, record } = {}) {
  const { fields, parent: parentKey } = entities[entity];
  const editing = revision !== undefined;
  const payload = !editing && parentKey ? { [parentKey]: parent?.[parentKey] } : {};
  for (const field of fields) {
    if (!fieldApplies(field, editing)) continue;
    const next = value(field, form[field.key]);
    if (field.move && (!next || next === record?.[field.key])) continue;
    payload[field.key] = next;
  }
  if (editing) payload.revision = revision;
  return payload;
}

export function formFor(entity, record) {
  return Object.fromEntries(entities[entity].fields.map(field => {
    const raw = record?.[field.key];
    if (field.type === 'checkbox') return [field.key, Boolean(raw)];
    return [field.key, raw == null ? field.initial ?? '' : String(raw)];
  }));
}

export const fold = text => String(text ?? '').normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();

export function searchRecords(entity, rows, term) {
  const needle = fold(term).trim();
  if (!needle) return rows;
  const keys = entities[entity].search ?? ['name'];
  return rows.filter(row => keys.some(key => fold(typeof key === 'function' ? key(row) : row[key]).includes(needle)));
}

const count = (n, one, many) => `${n} ${n === 1 ? one : many}`;

/** "N campos · M lotes" of one client, from every field and plot. */
export function clientCounts(fields, plots, clientId) {
  const own = new Set(fields.filter(field => field.clientId === clientId).map(field => field.id));
  return `${count(own.size, 'campo', 'campos')} · ${count(plots.filter(plot => own.has(plot.fieldId)).length, 'lote', 'lotes')}`;
}

export const fieldPlotCount = (plots, fieldId) => count(plots.filter(plot => plot.fieldId === fieldId).length, 'lote', 'lotes');

const byName = (a, b) => a.name.localeCompare(b.name, 'es', { sensitivity: 'base' });

/** Labors per category in reference order, each sorted by name; uncategorized ones go last as "Otras". */
export function laborGroups(labors) {
  const known = new Set(laborCategories.map(category => category.value));
  const groups = [...laborCategories.map(({ value, label }) => ({ category: value, label })), { category: null, label: 'Otras' }];
  return groups.map(group => ({
    ...group,
    labors: labors.filter(row => group.category ? row.category === group.category : !known.has(row.category)).sort(byName),
  })).filter(group => group.labors.length);
}

/** Select groups for moving a plot: every field under its client, both by name; clients without fields are left out. */
export function fieldMoveGroups(clients, fields) {
  return [...clients].sort(byName).map(client => ({
    label: client.name,
    options: fields.filter(field => field.clientId === client.id).sort(byName).map(field => ({ value: field.id, label: field.name })),
  })).filter(group => group.options.length);
}

/** Notice after an edit that moved a field to another client or a plot to another field; null when nothing moved. */
export function moveNotice(entity, before, after, { clients, fields }) {
  const name = (rows, id) => rows.find(row => row.id === id)?.name ?? 'Sin referencia';
  if (entity === 'fields' && after.clientId !== before.clientId) return `Se movió el campo a ${name(clients, after.clientId)}.`;
  if (entity === 'plots' && after.fieldId !== before.fieldId) {
    const field = fields.find(row => row.id === after.fieldId);
    return `Se movió el lote a ${name(fields, after.fieldId)} (${name(clients, field?.clientId)}).`;
  }
  return null;
}

/** One plot crop row: use, variety and the recorded yields (zero is a recorded value). */
export const plotCropDetail = row => joined([
  optionLabel(roles, row.role), row.variety,
  row.grainYieldKgHa != null && `Rendimiento de grano (kg/ha): ${row.grainYieldKgHa}`,
  row.forageBiomassKgHa != null && `Biomasa de forraje (kg/ha): ${row.forageBiomassKgHa}`,
]);

/** Link target for a contractor website: http(s) as stored, a bare address as https, any other scheme not linked. */
export function websiteHref(website) {
  const address = String(website ?? '').trim();
  if (!address) return null;
  if (/^https?:\/\//i.test(address)) return address;
  if (/^[a-z][a-z0-9+.-]*:/i.test(address)) return null;
  return `https://${address}`;
}
