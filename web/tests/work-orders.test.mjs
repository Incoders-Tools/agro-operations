import test from 'node:test';
import assert from 'node:assert/strict';
import { errorMessage } from '../app/backoffice/api.mjs';
import { formFor, payloadFor } from '../app/backoffice/schema.mjs';
import {
  clientFirms, cropLabel, displayDate, documentTitle, doseRange, emptyOrder, exceedsPlot, fieldNames, lineTotal, orderFromRecord, orderLabel,
  orderPayload, orderQuery, plotCropPrefill, plotGroups, productLabel, todayIso, workedTotal,
} from '../app/backoffice/work-orders.mjs';

const generic = 'Revisa los datos e intenta de nuevo.';

const form = {
  ...emptyOrder('2026-11-23'),
  clientId: 'cl1', firmId: 'f1', laborTypeId: 'l1', stageId: '', contractorId: 'c1', campaignId: '', cropId: 'cr1',
  variety: '  Variedad ficticia 2 ', observations: ' ', responsibleEngineer: ' Ing. Demo ',
  plots: [{ plotId: 'p1', worked: '10.5' }, { plotId: 'p2', worked: '4' }],
  lines: [{ key: 1, inputId: 'i1', dose: '1.25', term: 'gli' }, { key: 2, inputId: '', dose: '', term: '' }],
};

test('an empty order starts today with no plots and one blank input line', () => {
  const order = emptyOrder('2026-11-23');
  assert.equal(order.issuedOn, '2026-11-23');
  assert.deepEqual(order.plots, []);
  assert.equal(order.lines.length, 1);
  assert.equal(order.lines[0].inputId, '');
  assert.equal(order.firmId, '');
  assert.equal(order.clientId, '');
  assert.equal('fieldId' in order, false);
  assert.equal(todayIso(new Date(2026, 0, 5, 23, 30)), '2026-01-05');
});

test('create payload carries the firm, never a field or the client, trims text, nulls empty optionals and skips blank lines', () => {
  assert.deepEqual(orderPayload(form), {
    firmId: 'f1', issuedOn: '2026-11-23', laborTypeId: 'l1', stageId: null, contractorId: 'c1',
    campaignId: null, cropId: 'cr1', variety: 'Variedad ficticia 2', observations: null, responsibleEngineer: 'Ing. Demo',
    plots: [{ plotId: 'p1', workedAreaHectares: 10.5 }, { plotId: 'p2', workedAreaHectares: 4 }],
    lines: [{ inputId: 'i1', dosePerHectare: 1.25 }],
  });
});

test('update payload sends the revision and never the firm or number', () => {
  const payload = orderPayload({ ...form, issuedOn: '' }, { revision: 3 });
  assert.equal(payload.revision, 3);
  assert.equal('firmId' in payload, false);
  assert.equal('number' in payload, false);
  assert.equal('fieldId' in payload, false);
  assert.equal('clientId' in payload, false);
  assert.equal(payload.issuedOn, null);
});

test('form state from a stored order keeps plots, worked areas and lines in order', () => {
  const record = {
    id: 'w1', firmId: 'f1', number: 12, issuedOn: '2026-10-01', laborTypeId: 'l1', stageId: null, contractorId: 'c1',
    campaignId: 'k1', cropId: null, variety: null, observations: 'Obs', responsibleEngineer: null, status: 'emitida', revision: 2,
    plots: [{ plotId: 'p1', fieldId: 'fd1', areaHectares: 10, workedAreaHectares: 9.5, workedAreaExceedsPlot: false }],
    lines: [{ id: 'x', position: 1, inputId: 'i1', dosePerHectare: 0.8, unit: 'l', total: 7.6, doseOutOfRange: false }],
  };
  const state = orderFromRecord(record);
  assert.equal(state.firmId, 'f1');
  assert.equal('fieldId' in state, false);
  assert.equal(state.stageId, '');
  assert.equal(state.campaignId, 'k1');
  assert.equal(state.observations, 'Obs');
  assert.deepEqual(state.plots, [{ plotId: 'p1', worked: '9.5' }]);
  assert.equal(state.lines.length, 1);
  assert.equal(state.lines[0].inputId, 'i1');
  assert.equal(state.lines[0].dose, '0.8');
});

test('worked total sums the ha a trabajar exactly and ignores blank or invalid entries', () => {
  assert.equal(workedTotal([{ worked: '0.1' }, { worked: '0.2' }]), '0.3');
  assert.equal(workedTotal([{ worked: '10.5' }, { worked: '' }, { worked: 'x' }, { worked: '4' }]), '14.5');
  assert.equal(workedTotal([]), '0');
});

test('line total is dose times total worked area rounded half away from zero to 4 decimals', () => {
  assert.equal(lineTotal('1.25', '14.5'), '18.125');
  assert.equal(lineTotal('0.3333', '0.5'), '0.1667'); // 0.16665 rounds up
  assert.equal(lineTotal('0.1', '0.3'), '0.03');
  assert.equal(lineTotal('2', '10'), '20');
  assert.equal(lineTotal('0.00005', '1'), null); // more than 4 decimals is not a valid dose
  assert.equal(lineTotal('', '10'), null);
  assert.equal(lineTotal('1', '0'), null);
  assert.equal(lineTotal('-1', '10'), null);
});

test('dose range check warns below the minimum or above the maximum, never changes the dose', () => {
  const input = { doseMin: 0.5, doseMax: 2 };
  assert.equal(doseRange('0.4', input), 'below');
  assert.equal(doseRange('0.5', input), null);
  assert.equal(doseRange('2', input), null);
  assert.equal(doseRange('2.01', input), 'above');
  assert.equal(doseRange('5', { doseMin: null, doseMax: null }), null);
  assert.equal(doseRange('0.1', { doseMin: 1, doseMax: null }), 'below');
  assert.equal(doseRange('', input), null);
  assert.equal(doseRange('1', undefined), null);
});

test('crop prefill takes position 1 of the chosen campaign', () => {
  const crops = [
    { campaignId: 'k1', position: 2, cropId: 'r2', variety: 'B' },
    { campaignId: 'k1', position: 1, cropId: 'r1', variety: null },
    { campaignId: 'k2', position: 1, cropId: 'r3', variety: 'C' },
  ];
  assert.deepEqual(plotCropPrefill(crops, 'k1'), { cropId: 'r1', variety: '' });
  assert.deepEqual(plotCropPrefill(crops, 'k2'), { cropId: 'r3', variety: 'C' });
  assert.equal(plotCropPrefill(crops, 'k9'), null);
  assert.equal(plotCropPrefill(crops, ''), null);
});

test('order label shows the firm name and the server number', () => {
  assert.equal(orderLabel({ name: 'Firma Demo' }, 17), 'Firma Demo Nº 17');
  assert.equal(orderLabel(undefined, 3), 'Nº 3');
  assert.equal(displayDate('2026-11-03'), '03/11/2026');
});

test('worked area beyond the plot area is flagged, equal is not', () => {
  assert.equal(exceedsPlot('10.0001', 10), true);
  assert.equal(exceedsPlot('10', 10), false);
  assert.equal(exceedsPlot('', 10), false);
});

test('work order API messages map to Spanish', () => {
  assert.equal(errorMessage(409, { error: 'Work order is not editable' }), 'Esta orden ya no se puede editar.');
  assert.equal(errorMessage(400, { error: 'At least one plot is required' }), 'Selecciona al menos un lote.');
  assert.equal(errorMessage(400, { error: 'Each plot can appear only once' }), 'Cada lote puede aparecer una sola vez.');
  assert.equal(errorMessage(400, { error: 'Worked area must be positive and fit numeric(12,4)' }),
    'Las ha a trabajar deben ser mayores que cero y tener hasta 4 decimales.');
  assert.equal(errorMessage(400, { error: 'Dose must be positive and fit numeric(12,4)' }), 'La dosis debe ser mayor que cero y tener hasta 4 decimales.');
  assert.equal(errorMessage(400, { error: 'Line total must be positive and fit numeric(14,4)' }),
    'El total de cada insumo debe ser mayor que cero y tener hasta 4 decimales.');
  assert.equal(errorMessage(400, { error: 'Reason is required' }), 'Indica el motivo de la cancelación.');
  assert.equal(errorMessage(400, { error: 'Issued on is required' }), 'La fecha es obligatoria.');
  assert.equal(errorMessage(400, { error: "Every plot must belong to the firm's client" }), 'Todos los lotes deben ser de campos del cliente de la firma.');
  assert.equal(errorMessage(400, { error: 'Status must be one of: emitida, realizada, cancelada' }), 'Elige un estado válido.');
  assert.equal(errorMessage(400, { error: 'Next number must be greater than 41' }), 'El próximo número debe ser mayor que 41.');
  assert.equal(errorMessage(400, { error: 'Next number must be greater than x' }), generic);
  assert.equal(errorMessage(400, { error: 'Work order is not editable' }), generic);
});

test('firm next work order number is sent only on edit, empty keeps the stored one', () => {
  const firm = { name: ' Firma ', taxId: '', nextWorkOrderNumber: '42' };
  assert.deepEqual(payloadFor('commercial-firms', firm, { parent: { clientId: 'c1' } }), { clientId: 'c1', name: 'Firma', taxId: null });
  assert.deepEqual(payloadFor('commercial-firms', firm, { revision: 3 }), { name: 'Firma', taxId: null, nextWorkOrderNumber: 42, revision: 3 });
  assert.deepEqual(payloadFor('commercial-firms', { ...firm, nextWorkOrderNumber: ' ' }, { revision: 3 }),
    { name: 'Firma', taxId: null, nextWorkOrderNumber: null, revision: 3 });
  assert.equal(formFor('commercial-firms', { name: 'F', taxId: null, nextWorkOrderNumber: 7, revision: 1 }).nextWorkOrderNumber, '7');
});

const fields = [
  { id: 'fb', clientId: 'cl1', name: 'San Benito' },
  { id: 'fa', clientId: 'cl1', name: 'El Álamo' },
  { id: 'fc', clientId: 'cl1', name: 'Vacío' },
];
const plots = [
  { id: 'p3', fieldId: 'fb', code: 'SB-02', name: 'Bajo', areaHectares: 20 },
  { id: 'p1', fieldId: 'fa', code: 'EA-01', name: 'Norte', areaHectares: 40 },
  { id: 'p2', fieldId: 'fa', code: null, name: 'Sur', areaHectares: 35.5 },
  { id: 'p4', fieldId: 'fb', code: 'SB-01', name: 'Loma', areaHectares: 12 },
];
const shape = groups => groups.map(group => [group.field.id, group.plots.map(plot => plot.id)]);

test('plots are grouped by field in field name order, with every field of the client', () => {
  assert.deepEqual(shape(plotGroups(fields, plots, '')), [['fa', ['p1', 'p2']], ['fb', ['p4', 'p3']], ['fc', []]]);
});

test('the plot filter matches field name or plot name or code, ignoring case and accents', () => {
  assert.deepEqual(shape(plotGroups(fields, plots, 'alamo')), [['fa', ['p1', 'p2']]]);
  assert.deepEqual(shape(plotGroups(fields, plots, 'sb-01')), [['fb', ['p4']]]);
  assert.deepEqual(shape(plotGroups(fields, plots, 'SUR')), [['fa', ['p2']]]);
  assert.deepEqual(plotGroups(fields, plots, 'nada'), []);
  assert.equal(plotGroups(fields, plots, '   ').length, 3);
});

test('the total worked area adds plots of different fields', () => {
  const chosen = [{ plotId: 'p1', worked: '40' }, { plotId: 'p3', worked: '19.75' }, { plotId: 'p4', worked: '0.25' }];
  assert.equal(workedTotal(chosen), '60');
  assert.equal(lineTotal('1.5', workedTotal(chosen)), '90');
});

test('a client offers only its firms and preselects the only one', () => {
  const firms = [{ id: 'f1', clientId: 'cl1' }, { id: 'f2', clientId: 'cl2' }, { id: 'f3', clientId: 'cl2' }];
  assert.deepEqual(clientFirms(firms, 'cl1'), { firms: [firms[0]], preselected: 'f1' });
  assert.deepEqual(clientFirms(firms, 'cl2'), { firms: [firms[1], firms[2]], preselected: '' });
  assert.deepEqual(clientFirms(firms, 'cl9'), { firms: [], preselected: '' });
  assert.deepEqual(clientFirms(firms, ''), { firms: [], preselected: '' });
});

test('the list query sends the search with the firm and status filters', () => {
  assert.equal(orderQuery({ firmId: '', status: '', q: '' }), 'work-orders');
  assert.equal(orderQuery({ firmId: 'f1', status: 'emitida', q: '' }), 'work-orders?firmId=f1&status=emitida');
  assert.equal(orderQuery({ firmId: '', status: '', q: '  lote norte ' }), 'work-orders?q=lote+norte');
  assert.equal(orderQuery({ firmId: 'f1', status: '', q: '12' }), 'work-orders?firmId=f1&q=12');
  assert.equal(orderQuery({ firmId: '', status: '', q: 'a&b' }), 'work-orders?q=a%26b');
});

test('list rows name the fields of the order from its field ids', () => {
  assert.equal(fieldNames(fields, ['fa', 'fb']), 'El Álamo, San Benito');
  assert.equal(fieldNames(fields, ['fa', 'gone']), 'El Álamo, Sin referencia');
  assert.equal(fieldNames(fields, []), 'Sin campos');
  assert.equal(fieldNames(fields, undefined), 'Sin campos');
});

test('the print title is the document file name, so Guardar como PDF proposes it', () => {
  assert.equal(documentTitle({ fileName: 'FIRMA DEMO_ORDEN_12_EL ALAMO_(Norte)_PULV-TE_3-nov', number: 12 }),
    'FIRMA DEMO_ORDEN_12_EL ALAMO_(Norte)_PULV-TE_3-nov');
  assert.equal(documentTitle({ fileName: '  ', number: 12 }), 'ORDEN_12');
  assert.equal(documentTitle(null), 'Orden de trabajo');
});

test('print labels join crop and variety, and product, formulation and mix order', () => {
  assert.equal(cropLabel({ cropName: 'Soja', variety: 'Variedad ficticia' }), 'Soja · Variedad ficticia');
  assert.equal(cropLabel({ cropName: 'Soja', variety: null }), 'Soja');
  assert.equal(cropLabel({ cropName: null, variety: null }), 'Sin indicar');
  assert.equal(productLabel({ inputName: 'Producto Uno', formulation: 'SC', mixOrder: 2 }), 'Producto Uno SC · orden de mezcla 2');
  assert.equal(productLabel({ inputName: 'Producto Dos', formulation: null, mixOrder: null }), 'Producto Dos');
});
