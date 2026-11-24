import test from 'node:test';
import assert from 'node:assert/strict';
import { errorMessage } from '../app/backoffice/api.mjs';
import {
  categories, entities, fieldMoveGroups, formFor, laborCategories, laborGroups, moveNotice, optionLabel, payloadFor, plotCropDetail,
  searchRecords, websiteHref,
} from '../app/backoffice/schema.mjs';
import { laborLabel, laborOptionGroups } from '../app/backoffice/work-orders.mjs';

const labors = [
  { id: 'l1', name: 'Siembra gruesa', abbreviation: 'SIEM-G', category: 'siembra', description: null },
  { id: 'l2', name: 'Cosecha fina', abbreviation: null, category: 'cosecha', description: 'trigo, cebada, colza, etc.' },
  { id: 'l3', name: 'Labor sin grupo', abbreviation: null, category: null, description: null },
  { id: 'l4', name: 'Pulverización terrestre', abbreviation: 'PULV-TE', category: 'aplicacion', description: null },
  { id: 'l5', name: 'Disco', abbreviation: null, category: 'labranza', description: null },
  { id: 'l6', name: 'Corte de pasturas', abbreviation: null, category: 'forrajes', description: 'segadora de discos o hélice' },
  { id: 'l7', name: 'Cincel', abbreviation: null, category: 'labranza', description: null },
];

test('S1 labor categories carry the reference labels in its order', () => {
  assert.deepEqual(laborCategories, [
    { value: 'aplicacion', label: 'Aplicación' },
    { value: 'labranza', label: 'Labranza y preparación' },
    { value: 'siembra', label: 'Siembra e implantación' },
    { value: 'cosecha', label: 'Cosecha y poscosecha' },
    { value: 'forrajes', label: 'Forrajes y ganadería' },
    { value: 'infraestructura', label: 'Mantenimiento de infraestructura' },
  ]);
});

test('S1 labor payload sends category and description, empty as null', () => {
  assert.deepEqual(payloadFor('labor-types', { name: ' Cosecha ', abbreviation: '', category: 'cosecha', description: ' granos ' }),
    { name: 'Cosecha', abbreviation: null, category: 'cosecha', description: 'granos' });
  assert.deepEqual(payloadFor('labor-types', { name: 'Rolo', abbreviation: 'ROLO', category: '', description: '' }, { revision: 3 }),
    { name: 'Rolo', abbreviation: 'ROLO', category: null, description: null, revision: 3 });
  const form = formFor('labor-types', labors[1]);
  assert.equal(form.category, 'cosecha');
  assert.equal(form.description, 'trigo, cebada, colza, etc.');
  assert.equal(formFor('labor-types', labors[2]).category, '');
});

test('S1 labors group by category in reference order, uncategorized last as "Otras", names sorted', () => {
  const groups = laborGroups(labors);
  assert.deepEqual(groups.map(group => group.label),
    ['Aplicación', 'Labranza y preparación', 'Siembra e implantación', 'Cosecha y poscosecha', 'Forrajes y ganadería', 'Otras']);
  assert.deepEqual(groups[1].labors.map(row => row.id), ['l7', 'l5']);
  assert.deepEqual(laborGroups([]), []);
  // The catalog list is ordered the same way.
  assert.deepEqual(entities['labor-types'].sort(labors).map(row => row.id), ['l4', 'l7', 'l5', 'l1', 'l2', 'l6', 'l3']);
});

test('S1 labor list is labelled by category and searchable by name, abbreviation, category or description', () => {
  assert.equal(entities['labor-types'].detail(labors[0]), 'Siembra e implantación · SIEM-G');
  assert.equal(entities['labor-types'].detail(labors[1]), 'Cosecha y poscosecha · trigo, cebada, colza, etc.');
  assert.equal(entities['labor-types'].detail(labors[2]), 'Sin categoría');
  assert.deepEqual(searchRecords('labor-types', labors, 'pulv').map(row => row.id), ['l4']);
  assert.deepEqual(searchRecords('labor-types', labors, 'siem-g').map(row => row.id), ['l1']);
  assert.deepEqual(searchRecords('labor-types', labors, 'labranza').map(row => row.id), ['l5', 'l7']);
  assert.deepEqual(searchRecords('labor-types', labors, 'helice').map(row => row.id), ['l6']);
});

test('S1 OT labor select has one optgroup per category with the abbreviation in the label', () => {
  const groups = laborOptionGroups(labors);
  assert.equal(groups.at(-1).label, 'Otras');
  assert.deepEqual(groups[0], { label: 'Aplicación', options: [{ value: 'l4', label: 'Pulverización terrestre (PULV-TE)' }] });
  assert.deepEqual(groups[3].options, [{ value: 'l2', label: 'Cosecha fina' }]);
  assert.equal(laborLabel(labors[0]), 'Siembra gruesa (SIEM-G)');
  assert.equal(laborLabel(undefined), 'Sin referencia');
});

test('S2 plot crop payload carries both yields, empty as null and zero kept', () => {
  const form = { campaignId: 'k1', position: '1', cropId: 'r1', role: 'renta', variety: '', grainYieldKgHa: '3500.5', forageBiomassKgHa: '' };
  assert.deepEqual(payloadFor('plot-crops', form, { parent: { plotId: 'p1' } }), {
    plotId: 'p1', campaignId: 'k1', position: 1, cropId: 'r1', role: 'renta', variety: null, grainYieldKgHa: 3500.5, forageBiomassKgHa: null,
  });
  assert.deepEqual(payloadFor('plot-crops', { ...form, grainYieldKgHa: '0', forageBiomassKgHa: '12000' }, { revision: 2 }),
    { cropId: 'r1', role: 'renta', variety: null, grainYieldKgHa: 0, forageBiomassKgHa: 12000, revision: 2 });
  const labels = entities['plot-crops'].fields.map(field => field.label);
  assert.ok(labels.includes('Rendimiento de grano (kg/ha)'));
  assert.ok(labels.includes('Biomasa de forraje (kg/ha)'));
  assert.equal(formFor('plot-crops', { grainYieldKgHa: null, forageBiomassKgHa: 0 }).forageBiomassKgHa, '0');
});

test('S2 plot crop rows show the yields only when recorded', () => {
  assert.equal(plotCropDetail({ role: 'renta', variety: 'DM 46', grainYieldKgHa: 3500, forageBiomassKgHa: null }),
    'Renta · DM 46 · Rendimiento de grano (kg/ha): 3500');
  assert.equal(plotCropDetail({ role: 'servicio', variety: null, grainYieldKgHa: 0, forageBiomassKgHa: 8000.25 }),
    'Servicio · Rendimiento de grano (kg/ha): 0 · Biomasa de forraje (kg/ha): 8000.25');
  assert.equal(plotCropDetail({ role: 'renta', variety: null }), 'Renta');
});

test('S3 field edit sends clientId only when it changed; create never sends a move', () => {
  const record = { id: 'f1', clientId: 'c1', name: 'Campo', tenure: null, locality: null, revision: 4 };
  const form = formFor('fields', record);
  assert.equal(form.clientId, 'c1');
  assert.deepEqual(payloadFor('fields', form, { revision: 4, record }), { name: 'Campo', tenure: null, locality: null, revision: 4 });
  assert.deepEqual(payloadFor('fields', { ...form, clientId: 'c2' }, { revision: 4, record }),
    { name: 'Campo', tenure: null, locality: null, clientId: 'c2', revision: 4 });
  assert.deepEqual(payloadFor('fields', { name: 'Nuevo', tenure: '', locality: '', clientId: '' }, { parent: { clientId: 'c1' } }),
    { clientId: 'c1', name: 'Nuevo', tenure: null, locality: null });
  assert.equal(entities.fields.fields.find(field => field.key === 'clientId').label, 'Mover a otro cliente');
});

test('S3 plot edit sends fieldId only when it changed', () => {
  const record = { id: 'p1', fieldId: 'f1', code: null, name: 'Lote', areaHectares: 10, revision: 1 };
  const form = formFor('plots', record);
  assert.deepEqual(payloadFor('plots', form, { revision: 1, record }), { code: null, name: 'Lote', areaHectares: 10, revision: 1 });
  assert.deepEqual(payloadFor('plots', { ...form, fieldId: 'f9' }, { revision: 1, record }),
    { code: null, name: 'Lote', areaHectares: 10, fieldId: 'f9', revision: 1 });
  assert.equal(entities.plots.fields.find(field => field.key === 'fieldId').label, 'Mover a otro campo');
});

test('S3 plot move options list fields grouped by client, both sorted by name', () => {
  const clients = [{ id: 'c2', name: 'Zeta' }, { id: 'c1', name: 'Alfa' }, { id: 'c3', name: 'Sin campos' }];
  const fields = [{ id: 'f2', clientId: 'c1', name: 'Sur' }, { id: 'f1', clientId: 'c1', name: 'Norte' }, { id: 'f3', clientId: 'c2', name: 'Este' }];
  assert.deepEqual(fieldMoveGroups(clients, fields), [
    { label: 'Alfa', options: [{ value: 'f1', label: 'Norte' }, { value: 'f2', label: 'Sur' }] },
    { label: 'Zeta', options: [{ value: 'f3', label: 'Este' }] },
  ]);
});

test('S3 move notice names the destination, nothing when the parent did not change', () => {
  const clients = [{ id: 'c1', name: 'Alfa' }, { id: 'c2', name: 'Zeta' }];
  const fields = [{ id: 'f1', clientId: 'c1', name: 'Norte' }, { id: 'f3', clientId: 'c2', name: 'Este' }];
  assert.equal(moveNotice('fields', { clientId: 'c1' }, { clientId: 'c2' }, { clients, fields }), 'Se movió el campo a Zeta.');
  assert.equal(moveNotice('plots', { fieldId: 'f1' }, { fieldId: 'f3' }, { clients, fields }), 'Se movió el lote a Este (Zeta).');
  assert.equal(moveNotice('plots', { fieldId: 'f1' }, { fieldId: 'f1' }, { clients, fields }), null);
  assert.equal(moveNotice('clients', { id: 'c1' }, { id: 'c1' }, { clients, fields }), null);
});

test('S4 input categories add Inoculante and name Terápico de semillas', () => {
  assert.equal(optionLabel(categories, 'inoculante'), 'Inoculante');
  assert.equal(optionLabel(categories, 'terapico'), 'Terápico de semillas');
});

test('S6 contractor website is optional, sent trimmed and linked only for web addresses', () => {
  assert.deepEqual(payloadFor('contractors', { name: 'X', website: ' www.ejemplo.com.ar ', ownEquipment: false }),
    { name: 'X', taxId: null, phone: null, email: null, website: 'www.ejemplo.com.ar', notes: null, ownEquipment: false });
  assert.equal(entities.contractors.fields.find(field => field.key === 'website').label, 'Sitio web');
  assert.equal(websiteHref('www.ejemplo.com.ar'), 'https://www.ejemplo.com.ar');
  assert.equal(websiteHref('http://ejemplo.com'), 'http://ejemplo.com');
  assert.equal(websiteHref('HTTPS://Ejemplo.com/x'), 'HTTPS://Ejemplo.com/x');
  assert.equal(websiteHref('javascript:alert(1)'), null);
  assert.equal(websiteHref(''), null);
  assert.equal(websiteHref(null), null);
});

test('S6 client phone is labelled Celular; contractors keep Teléfono', () => {
  assert.equal(entities.clients.fields.find(field => field.key === 'phone').label, 'Celular');
  assert.equal(entities.contractors.fields.find(field => field.key === 'phone').label, 'Teléfono');
});

test('new 400 messages map to Spanish', () => {
  assert.equal(errorMessage(400, { error: 'Category must be one of: aplicacion, labranza' }), 'La categoría no es válida.');
  assert.equal(errorMessage(400, { error: 'Yield must be zero or positive and fit numeric(12,2)' }), 'El rendimiento debe ser cero o positivo.');
  assert.equal(errorMessage(400, { error: 'Website must be a single address without spaces' }), 'El sitio web no puede tener espacios.');
});
