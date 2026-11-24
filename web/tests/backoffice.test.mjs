import test from 'node:test';
import assert from 'node:assert/strict';
import { errorMessage } from '../app/backoffice/api.mjs';
import { clientCounts, fieldPlotCount, payloadFor, formFor, searchRecords } from '../app/backoffice/schema.mjs';

const stale = 'Otra persona modificó este registro. Actualiza antes de guardar.';
const generic = 'Revisa los datos e intenta de nuevo.';

test('409 responses map the API error body to Spanish', () => {
  assert.equal(errorMessage(409, { error: 'Duplicate name' }), 'Ya existe un registro con ese nombre.');
  assert.equal(errorMessage(409, { error: 'Duplicate code' }), 'Ya existe un lote con ese código.');
  assert.equal(errorMessage(409, { error: 'Duplicate position' }), 'Ese lote ya tiene un cultivo en esa posición para la campaña.');
  assert.equal(errorMessage(409, { error: 'Stale revision' }), stale);
  // An unknown or missing body keeps the previous stale-revision wording.
  assert.equal(errorMessage(409, null), stale);
  assert.equal(errorMessage(409, { error: 'Something else' }), stale);
});

test('400 responses show Spanish text only for known API messages', () => {
  assert.equal(errorMessage(400, { error: 'CUIT must have 11 digits' }), 'El CUIT debe tener 11 dígitos.');
  assert.equal(errorMessage(400, { error: 'Name is required' }), 'El nombre es obligatorio.');
  assert.equal(errorMessage(400, { error: 'Commercial name is required' }), 'El nombre comercial es obligatorio.');
  assert.equal(errorMessage(400, { error: 'Minimum dose must not exceed maximum dose' }), 'La dosis mínima no puede superar la máxima.');
  assert.equal(errorMessage(400, { error: 'Mix order must be between 1 and 20' }), 'El orden de mezcla debe estar entre 1 y 20.');
  assert.equal(errorMessage(400, { error: 'Tenure must be one of: propio, alquiler_fijo, alquiler_porcentaje' }), 'Elige una tenencia válida.');
  assert.equal(errorMessage(400, { error: 'Unmapped english text' }), generic);
  assert.equal(errorMessage(400, null), generic);
  assert.equal(errorMessage(422, { error: 'Name is required' }), generic);
});

test('session, CSRF, missing record and server errors keep their existing wording', () => {
  assert.equal(errorMessage(401, null, { login: true }), 'Contraseña incorrecta. Inténtalo de nuevo.');
  assert.equal(errorMessage(401, null), 'La sesión terminó. Ingresa de nuevo.');
  assert.equal(errorMessage(403, null), 'No se pudo validar la solicitud. Actualiza la sesión.');
  assert.equal(errorMessage(404, { error: 'Record not found' }), 'El registro relacionado ya no existe. Actualiza los listados.');
  assert.equal(errorMessage(500, null), 'La API privada no responde. Inicia el servidor local.');
  assert.equal(errorMessage(502, null), 'La API privada no responde. Inicia el servidor local.');
});

test('client payload trims text, sends empty optional values as null and passes the CUIT through', () => {
  const form = { name: '  Cliente Demo ', taxId: '20-12345678-9', producers: '', email: '   ', phone: '351 555', address: '' };
  assert.deepEqual(payloadFor('clients', form), {
    name: 'Cliente Demo', taxId: '20-12345678-9', producers: null, email: null, phone: '351 555', address: null,
  });
  assert.deepEqual(payloadFor('clients', { ...form, taxId: '20123456789' }, { revision: 4 }), {
    name: 'Cliente Demo', taxId: '20123456789', producers: null, email: null, phone: '351 555', address: null, revision: 4,
  });
});

test('child payloads carry their parent only on create', () => {
  assert.deepEqual(payloadFor('fields', { name: 'Campo', tenure: '', locality: '' }, { parent: { clientId: 'c1' } }),
    { clientId: 'c1', name: 'Campo', tenure: null, locality: null });
  assert.deepEqual(payloadFor('fields', { name: 'Campo', tenure: 'alquiler_fijo', locality: 'Loc' }, { revision: 2, parent: { clientId: 'c1' } }),
    { name: 'Campo', tenure: 'alquiler_fijo', locality: 'Loc', revision: 2 });
  assert.deepEqual(payloadFor('plots', { code: '', name: 'Lote 1', areaHectares: '12.5' }, { parent: { fieldId: 'f1' } }),
    { fieldId: 'f1', code: null, name: 'Lote 1', areaHectares: 12.5 });
  assert.deepEqual(payloadFor('commercial-firms', { name: 'Firma', taxId: '' }, { parent: { clientId: 'c1' } }),
    { clientId: 'c1', name: 'Firma', taxId: null });
});

test('plot crop update omits the identifying campaign and position', () => {
  const form = { campaignId: 'k1', position: '2', cropId: 'r1', role: 'servicio', variety: '' };
  assert.deepEqual(payloadFor('plot-crops', form, { parent: { plotId: 'p1' } }),
    { plotId: 'p1', campaignId: 'k1', position: 2, cropId: 'r1', role: 'servicio', variety: null });
  assert.deepEqual(payloadFor('plot-crops', form, { revision: 7, parent: { plotId: 'p1' } }),
    { cropId: 'r1', role: 'servicio', variety: null, revision: 7 });
});

test('input payload converts numbers, keeps empty numbers null and sends booleans', () => {
  const form = {
    commercialName: ' Producto ', activeIngredient: '', concentration: '', formulation: 'SC', mixOrder: '3',
    category: '', unit: 'l', doseMin: '0.5', doseMax: '', referenceCost: '0', hollowCone: true,
  };
  assert.deepEqual(payloadFor('inputs', form), {
    commercialName: 'Producto', activeIngredient: null, concentration: null, formulation: 'SC', mixOrder: 3,
    category: null, unit: 'l', doseMin: 0.5, doseMax: null, referenceCost: 0, hollowCone: true,
  });
});

test('contractor, labor and stage payloads', () => {
  assert.deepEqual(payloadFor('contractors', { name: 'Propio', taxId: '', phone: '', email: '', notes: '', ownEquipment: true }),
    { name: 'Propio', taxId: null, phone: null, email: null, notes: null, ownEquipment: true });
  assert.deepEqual(payloadFor('contractors', { name: 'X', ownEquipment: false }),
    { name: 'X', taxId: null, phone: null, email: null, notes: null, ownEquipment: false });
  assert.deepEqual(payloadFor('labor-types', { name: 'Siembra gruesa', abbreviation: '' }), { name: 'Siembra gruesa', abbreviation: null });
  assert.deepEqual(payloadFor('stages', { name: 'Barbecho', sortOrder: '10' }, { revision: 1 }), { name: 'Barbecho', sortOrder: 10, revision: 1 });
  assert.deepEqual(payloadFor('crops', { name: ' Soja ' }), { name: 'Soja' });
});

test('form state from a record turns null into empty strings and keeps booleans', () => {
  const record = { id: 'i', commercialName: 'P', activeIngredient: null, mixOrder: 3, doseMin: 0.5, hollowCone: false, unit: 'l', revision: 2 };
  const form = formFor('inputs', record);
  assert.equal(form.activeIngredient, '');
  assert.equal(form.mixOrder, '3');
  assert.equal(form.doseMin, '0.5');
  assert.equal(form.hollowCone, false);
  assert.equal(formFor('contractors', null).ownEquipment, false);
  assert.equal(formFor('clients', null).name, '');
});

test('search matches name or active ingredient ignoring case and accents', () => {
  const rows = [
    { id: 1, commercialName: 'Herbicida Uno', activeIngredient: 'Glifosato' },
    { id: 2, commercialName: 'Fungicida Dos', activeIngredient: 'Azoxistrobina' },
    { id: 3, commercialName: 'Terápico', activeIngredient: null },
  ];
  assert.deepEqual(searchRecords('inputs', rows, 'glifo').map(row => row.id), [1]);
  assert.deepEqual(searchRecords('inputs', rows, 'DOS').map(row => row.id), [2]);
  assert.deepEqual(searchRecords('inputs', rows, 'terapico').map(row => row.id), [3]);
  assert.equal(searchRecords('inputs', rows, '  ').length, 3);
});

test('client rows count their fields and plots, field rows their plots', () => {
  const fields = [{ id: 'f1', clientId: 'c1' }, { id: 'f2', clientId: 'c1' }, { id: 'f3', clientId: 'c2' }];
  const plots = [{ fieldId: 'f1' }, { fieldId: 'f1' }, { fieldId: 'f2' }, { fieldId: 'f3' }];
  assert.equal(clientCounts(fields, plots, 'c1'), '2 campos · 3 lotes');
  assert.equal(clientCounts(fields, plots, 'c2'), '1 campo · 1 lote');
  assert.equal(clientCounts(fields, plots, 'c9'), '0 campos · 0 lotes');
  assert.equal(fieldPlotCount(plots, 'f1'), '2 lotes');
  assert.equal(fieldPlotCount(plots, 'f3'), '1 lote');
  assert.equal(fieldPlotCount(plots, 'f9'), '0 lotes');
});
