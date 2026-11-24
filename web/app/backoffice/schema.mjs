// Entity definitions shared by the forms and the request payloads. Field keys match the API JSON (camelCase).

export const tenures = [
  { value: 'propio', label: 'Propio' },
  { value: 'alquiler_fijo', label: 'Alquiler fijo' },
  { value: 'alquiler_porcentaje', label: 'Alquiler a porcentaje' },
];
export const roles = [{ value: 'renta', label: 'Renta' }, { value: 'servicio', label: 'Servicio' }];
export const categories = [
  ['fertilizante', 'Fertilizante'], ['herbicida', 'Herbicida'], ['insecticida', 'Insecticida'], ['fungicida', 'Fungicida'],
  ['coadyuvante', 'Coadyuvante'], ['curasemilla', 'Curasemilla'], ['bioestimulante', 'Bioestimulante'], ['terapico', 'Terápico'],
].map(([value, label]) => ({ value, label }));

export const optionLabel = (options, value) => options.find(option => option.value === value)?.label ?? value;

const name = { key: 'name', label: 'Nombre', type: 'text', maxLength: 200 };
const taxId = { key: 'taxId', label: 'CUIT', type: 'text', optional: true, inputMode: 'numeric', hint: '11 dígitos, con o sin guiones.' };
const email = { key: 'email', label: 'Email', type: 'email', optional: true };
const phone = { key: 'phone', label: 'Teléfono', type: 'tel', optional: true };
const dose = { type: 'number', optional: true, min: '0.0001', step: '0.0001' };
const joined = parts => parts.filter(Boolean).join(' · ');

export const entities = {
  clients: {
    title: 'Clientes', singular: 'cliente', search: ['name', 'taxId'],
    fields: [
      name, taxId,
      { key: 'producers', label: 'Productores', type: 'textarea', optional: true, hint: 'Una o más personas productoras del cliente.' },
      email, phone,
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
    ],
    detail: row => joined([row.tenure && optionLabel(tenures, row.tenure), row.locality]),
  },
  plots: {
    title: 'Lotes', singular: 'lote', parent: 'fieldId',
    fields: [
      { key: 'code', label: 'Código', type: 'text', optional: true, maxLength: 40, hint: 'Identificador corto cliente-campo-lote.' },
      name,
      { key: 'areaHectares', label: 'Superficie (ha)', type: 'number', min: '0.0001', max: '99999999.9999', step: '0.0001' },
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
    ],
  },
  contractors: {
    title: 'Contratistas', singular: 'contratista', search: ['name', 'taxId'],
    fields: [
      name, taxId, phone, email,
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
    fields: [name, { key: 'abbreviation', label: 'Abreviatura', type: 'text', optional: true, maxLength: 12, hint: 'Ej. PULV-TE' }],
    detail: row => row.abbreviation ?? 'Sin abreviatura',
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
export function payloadFor(entity, form, { revision, parent } = {}) {
  const { fields, parent: parentKey } = entities[entity];
  const editing = revision !== undefined;
  const payload = !editing && parentKey ? { [parentKey]: parent?.[parentKey] } : {};
  for (const field of fields) if (fieldApplies(field, editing)) payload[field.key] = value(field, form[field.key]);
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

const fold = text => String(text ?? '').normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();

export function searchRecords(entity, rows, term) {
  const needle = fold(term).trim();
  if (!needle) return rows;
  const keys = entities[entity].search ?? ['name'];
  return rows.filter(row => keys.some(key => fold(row[key]).includes(needle)));
}
