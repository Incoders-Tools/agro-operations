import test from 'node:test';
import assert from 'node:assert/strict';
import { errorMessage } from '../app/backoffice/api.mjs';
import { entities, formFor, payloadFor, searchRecords } from '../app/backoffice/schema.mjs';
import {
  comparisonPayload, comparisonQuery, comparisonTable, contactRoles, currencies, decidePayload, emptyPrice, emptyQuote, filterProviders,
  formatMoney, latestPricesByProvider, newQuoteLine, priceFromRecord, pricePayload, providerKinds, quoteFromRecord, quoteLineTotal,
  quotePayload, quoteTotal, sortPrices,
} from '../app/backoffice/providers.mjs';

const generic = 'Revisa los datos e intenta de nuevo.';

test('S3 S4 provider kinds, contact roles and currencies follow the API values', () => {
  assert.deepEqual(providerKinds.map(kind => kind.value), ['insumos', 'laboratorio_suelos', 'laboratorio_semillas', 'plataforma_virtual', 'otro']);
  assert.deepEqual(providerKinds.map(kind => kind.label), ['Insumos', 'Laboratorio de suelos', 'Laboratorio de semillas', 'Plataforma virtual', 'Otro']);
  assert.deepEqual(contactRoles.map(role => [role.value, role.label]), [['ventas', 'Ventas'], ['tecnico', 'Técnico'], ['otro', 'Otro']]);
  assert.deepEqual(currencies.map(currency => currency.value), ['ARS', 'USD']);
});

test('S3 S4 provider payload sends every field, empty optionals as null, and the revision on edit', () => {
  const form = { ...formFor('providers', null), name: ' Agro Insumos SA ', kind: 'insumos', taxId: '', website: ' www.agro.com.ar ', phone: '', email: '', notes: '' };
  assert.deepEqual(payloadFor('providers', form),
    { name: 'Agro Insumos SA', kind: 'insumos', taxId: null, website: 'www.agro.com.ar', phone: null, email: null, notes: null });
  assert.deepEqual(payloadFor('providers', { ...form, kind: 'laboratorio_suelos' }, { revision: 4 }),
    { name: 'Agro Insumos SA', kind: 'laboratorio_suelos', taxId: null, website: 'www.agro.com.ar', phone: null, email: null, notes: null, revision: 4 });
  assert.equal(formFor('providers', null).kind, 'insumos');
});

test('S3 S4 providers filter by kind and search by name, CUIT and email', () => {
  const rows = [
    { id: 'a', name: 'Agro Norte', kind: 'insumos', taxId: '30111111118', email: 'ventas@norte.com' },
    { id: 'b', name: 'Laboratorio Sur', kind: 'laboratorio_suelos', taxId: null, email: null },
    { id: 'c', name: 'Semillas Lab', kind: 'laboratorio_semillas', taxId: null, email: 'lab@semillas.com' },
  ];
  assert.deepEqual(filterProviders(rows, '').map(row => row.id), ['a', 'b', 'c']);
  assert.deepEqual(filterProviders(rows, 'laboratorio_suelos').map(row => row.id), ['b']);
  assert.deepEqual(searchRecords('providers', rows, 'laboratorio').map(row => row.id), ['b', 'c']);
  assert.deepEqual(searchRecords('providers', rows, 'insumos').map(row => row.id), ['a']);
  assert.deepEqual(searchRecords('providers', rows, '30111').map(row => row.id), ['a']);
  assert.deepEqual(searchRecords('providers', rows, 'semillas.com').map(row => row.id), ['c']);
  assert.equal(entities.providers.detail(rows[1]), 'Laboratorio de suelos');
});

test('S3 S4 contact payload carries the provider on create only and the role', () => {
  const form = { ...formFor('provider-contacts', null), name: ' Ana ', phone: '', email: 'ana@agro.com', notes: '' };
  assert.equal(form.role, 'ventas');
  assert.deepEqual(payloadFor('provider-contacts', form, { parent: { providerId: 'p1' } }),
    { providerId: 'p1', name: 'Ana', role: 'ventas', phone: null, email: 'ana@agro.com', notes: null });
  assert.deepEqual(payloadFor('provider-contacts', { ...form, role: 'tecnico' }, { revision: 2, parent: { providerId: 'p1' } }),
    { name: 'Ana', role: 'tecnico', phone: null, email: 'ana@agro.com', notes: null, revision: 2 });
});

test('S3 price payload: unit empty is the input unit (null), validOn date, revision on edit keeps the provider', () => {
  const form = { ...emptyPrice('2026-11-24'), inputId: 'i1', price: '1250.5', currency: 'USD', unit: ' ', notes: '' };
  assert.equal(emptyPrice('2026-11-24').validOn, '2026-11-24');
  assert.equal(emptyPrice('2026-11-24').currency, 'ARS');
  assert.deepEqual(pricePayload(form, { providerId: 'p1' }),
    { providerId: 'p1', inputId: 'i1', price: 1250.5, currency: 'USD', unit: null, validOn: '2026-11-24', notes: null });
  assert.deepEqual(pricePayload({ ...form, unit: 'l' }, { providerId: 'p1', revision: 3 }),
    { inputId: 'i1', price: 1250.5, currency: 'USD', unit: 'l', validOn: '2026-11-24', notes: null, revision: 3 });
  const record = { id: 'x', providerId: 'p1', inputId: 'i1', price: 10, currency: 'ARS', unit: 'kg', validOn: '2026-01-02', notes: null, revision: 1 };
  assert.deepEqual(priceFromRecord(record), { inputId: 'i1', price: '10', currency: 'ARS', unit: 'kg', validOn: '2026-01-02', notes: '' });
});

test('S3 prices list newest first and the latest price per provider', () => {
  const prices = [
    { id: 'old', providerId: 'p1', validOn: '2026-01-10' },
    { id: 'new', providerId: 'p1', validOn: '2026-06-01' },
    { id: 'p2', providerId: 'p2', validOn: '2026-03-01' },
  ];
  assert.deepEqual(sortPrices(prices).map(row => row.id), ['new', 'p2', 'old']);
  assert.deepEqual(prices.map(row => row.id), ['old', 'new', 'p2']);
  assert.deepEqual(latestPricesByProvider(prices).map(row => row.id), ['new', 'p2']);
});

test('S3 comparison payload and list query', () => {
  const form = { title: ' Herbicidas barbecho ', requestedOn: '2026-11-20' };
  assert.deepEqual(comparisonPayload(form, { clientId: 'c1' }), { clientId: 'c1', title: 'Herbicidas barbecho', requestedOn: '2026-11-20' });
  assert.deepEqual(comparisonPayload(form, { revision: 5 }), { title: 'Herbicidas barbecho', requestedOn: '2026-11-20', revision: 5 });
  assert.equal(comparisonPayload({ title: 'x', requestedOn: '' }, { clientId: 'c1' }).requestedOn, null);
  assert.equal(comparisonQuery({}), 'quote-comparisons');
  assert.equal(comparisonQuery({ clientId: 'c1', status: 'decidida' }), 'quote-comparisons?clientId=c1&status=decidida');
  assert.equal(comparisonQuery({ status: 'abierta' }), 'quote-comparisons?status=abierta');
  assert.deepEqual(decidePayload({ quoteId: 'q1', reason: ' Mejor precio ' }, 7), { quoteId: 'q1', reason: 'Mejor precio', revision: 7 });
});

test('S3 live line total and quote total round half away from zero to 4 decimals like the API', () => {
  assert.equal(quoteLineTotal('3', '1250.5'), '3751.5');
  assert.equal(quoteLineTotal('0.3333', '0.0015'), '0.0005');
  assert.equal(quoteLineTotal('0.3333', '0.0014'), '0.0005');
  assert.equal(quoteLineTotal('0.1', '0.2'), '0.02');
  assert.equal(quoteLineTotal('10', '0'), '0');
  assert.equal(quoteLineTotal('', '5'), null);
  assert.equal(quoteLineTotal('2', 'abc'), null);
  assert.equal(quoteLineTotal('0', '5'), null);
  // The quote total rounds the sum of the exact products, as the API does, not the sum of rounded lines.
  const lines = [{ ...newQuoteLine(), quantity: '0.0001', unitPrice: '0.5' }, { ...newQuoteLine(), quantity: '0.0001', unitPrice: '0.5' }];
  assert.equal(quoteLineTotal('0.0001', '0.5'), '0.0001');
  assert.equal(quoteTotal(lines), '0.0001');
  assert.equal(quoteTotal([{ ...newQuoteLine(), quantity: '2', unitPrice: '10.25' }, { ...newQuoteLine(), quantity: 'x', unitPrice: '1' }]), '20.5');
  assert.equal(formatMoney('3751.5', 'ARS'), 'ARS 3.751,5');
  assert.equal(formatMoney(1234567.125, 'USD'), 'USD 1.234.567,125');
});

test('S3 quote payload: input or free text lines, empty unit as null, blank lines skipped, revision on edit', () => {
  const form = {
    ...emptyQuote('2026-11-24'), providerId: 'p1', currency: 'USD', notes: ' ',
    lines: [
      { ...newQuoteLine(), mode: 'input', inputId: 'i1', productText: 'ignored', quantity: '100', unit: '', unitPrice: '4.5' },
      { ...newQuoteLine() },
      { ...newQuoteLine(), mode: 'text', inputId: 'i9', productText: ' Glifosato genérico ', quantity: '20', unit: 'l', unitPrice: '0' },
    ],
  };
  assert.equal(emptyQuote('2026-11-24').receivedOn, '2026-11-24');
  assert.deepEqual(quotePayload(form), {
    providerId: 'p1', receivedOn: '2026-11-24', currency: 'USD', notes: null,
    lines: [
      { inputId: 'i1', productText: null, quantity: 100, unit: null, unitPrice: 4.5 },
      { inputId: null, productText: 'Glifosato genérico', quantity: 20, unit: 'l', unitPrice: 0 },
    ],
  });
  assert.equal(quotePayload(form, { revision: 2 }).revision, 2);
  const record = {
    id: 'q1', providerId: 'p1', receivedOn: '2026-11-01', currency: 'ARS', notes: null, revision: 2, total: 10,
    lines: [{ id: 'l1', position: 1, inputId: null, productText: 'Urea', quantity: 2, unit: 'kg', unitPrice: 5 }],
  };
  const restored = quoteFromRecord(record);
  assert.deepEqual({ ...restored, lines: restored.lines.map(({ key, ...line }) => line) }, {
    providerId: 'p1', receivedOn: '2026-11-01', currency: 'ARS', notes: '',
    lines: [{ mode: 'text', inputId: '', productText: 'Urea', quantity: '2', unit: 'kg', unitPrice: '5' }],
  });
});

const detail = {
  comparison: { id: 'c1', clientId: 'k1', title: 'Barbecho', requestedOn: '2026-11-01', status: 'decidida', chosenQuoteId: 'q2', decisionReason: 'Entrega', revision: 3 },
  quotes: [
    { id: 'q1', providerId: 'p1', receivedOn: '2026-11-02', currency: 'ARS', total: 5000, lines: [] },
    { id: 'q2', providerId: 'p2', receivedOn: '2026-11-03', currency: 'ARS', total: 4500, lines: [] },
    { id: 'q3', providerId: 'p3', receivedOn: '2026-11-04', currency: 'USD', total: 5, lines: [] },
  ],
  products: [
    {
      key: 'input:i1', label: 'Glifosato',
      offers: [
        { quoteId: 'q1', providerId: 'p1', unitPrice: 50, currency: 'ARS', quantity: 100, unit: 'l', lineTotal: 5000 },
        { quoteId: 'q2', providerId: 'p2', unitPrice: 45, currency: 'ARS', quantity: 100, unit: 'l', lineTotal: 4500 },
        { quoteId: 'q3', providerId: 'p3', unitPrice: 0.05, currency: 'USD', quantity: 100, unit: 'l', lineTotal: 5 },
      ],
      cheapestQuoteIdByCurrency: { ARS: 'q2', USD: 'q3' },
    },
    {
      key: 'text:urea', label: 'Urea',
      offers: [{ quoteId: 'q1', providerId: 'p1', unitPrice: 10, currency: 'ARS', quantity: 1, unit: 'kg', lineTotal: 10 }],
      cheapestQuoteIdByCurrency: { ARS: 'q1' },
    },
  ],
};
const providers = [{ id: 'p1', name: 'Norte' }, { id: 'p2', name: 'Sur' }];

test('S3 comparison table: one row per product, one column per quote, cheapest per currency from the API', () => {
  const table = comparisonTable(detail, providers);
  assert.deepEqual(table.columns.map(column => [column.quoteId, column.provider, column.currency, column.total, column.chosen]),
    [['q1', 'Norte', 'ARS', 5000, false], ['q2', 'Sur', 'ARS', 4500, true], ['q3', 'Sin referencia', 'USD', 5, false]]);
  assert.deepEqual(table.rows.map(row => row.label), ['Glifosato', 'Urea']);
  assert.deepEqual(table.rows[0].cells.map(cell => [cell.quoteId, cell.offers.map(offer => offer.unitPrice), cell.cheapest]),
    [['q1', [50], false], ['q2', [45], true], ['q3', [0.05], true]]);
  // A quote without the product has an empty cell; an offer alone in its currency is still the cheapest of it.
  assert.deepEqual(table.rows[1].cells.map(cell => [cell.offers.length, cell.cheapest]), [[1, true], [0, false], [0, false]]);
  assert.deepEqual(table.currencies, ['ARS', 'USD']);
  assert.equal(table.mixedCurrencies, true);
  assert.equal(table.note, 'Hay presupuestos en ARS y USD: los precios no se convierten y el más barato se marca dentro de cada moneda (Q19).');
});

test('S3 comparison table in one currency has no note, and a currency without offers is not highlighted', () => {
  const single = {
    ...detail,
    quotes: detail.quotes.slice(0, 2),
    products: [{ ...detail.products[0], offers: detail.products[0].offers.slice(0, 2), cheapestQuoteIdByCurrency: { ARS: 'q2' } }],
  };
  const table = comparisonTable(single, providers);
  assert.equal(table.mixedCurrencies, false);
  assert.equal(table.note, null);
  assert.deepEqual(table.rows[0].cells.map(cell => cell.cheapest), [false, true]);
  assert.deepEqual(comparisonTable({ ...single, quotes: [], products: [] }, providers), { columns: [], rows: [], currencies: [], mixedCurrencies: false, note: null });
});

test('S3 S4 API messages map to Spanish, the contact role by exact text', () => {
  const cases = {
    'Kind must be one of: insumos, laboratorio_suelos, laboratorio_semillas, plataforma_virtual, otro': 'Elige un tipo de proveedor válido.',
    'Role must be one of: ventas, tecnico, otro': 'Elige el rol del contacto: Ventas, Técnico u Otro.',
    'Currency must be ARS or USD': 'Elige la moneda: ARS o USD.',
    'Price must be positive and fit numeric(14,4)': 'El precio debe ser mayor que cero y tener hasta 4 decimales.',
    'Quantity must be positive': 'La cantidad de cada línea debe ser mayor que cero.',
    'Unit price must be zero or positive': 'El precio unitario debe ser cero o positivo.',
    'Each line needs an input or a product': 'Cada línea necesita un insumo o un producto.',
    'Provider is required': 'Elige un proveedor.',
    'Input is required': 'Elige un insumo.',
    'Valid on is required': 'Indica desde cuándo rige el precio.',
    'Received on is required': 'Indica la fecha de recepción del presupuesto.',
    'Requested on is required': 'Indica la fecha de solicitud.',
    'Title is required': 'El título es obligatorio.',
  };
  for (const [english, spanish] of Object.entries(cases)) assert.equal(errorMessage(400, { error: english }), spanish);
  assert.equal(errorMessage(409, { error: 'Comparison is not open' }), 'Esta comparación ya está decidida. Reábrela para cambiarla.');
  assert.equal(errorMessage(409, { error: 'Comparison is not decided' }), 'Esta comparación ya está abierta. Actualiza la vista.');
  assert.equal(errorMessage(409, { error: 'Duplicate link' }), 'Ese proveedor ya está vinculado a este cliente.');
  // "Reason is required" keeps the work order cancellation message, except when deciding a comparison.
  assert.equal(errorMessage(400, { error: 'Reason is required' }), 'Indica el motivo de la cancelación.');
  assert.equal(errorMessage(400, { error: 'Reason is required' }, { path: 'quote-comparisons/c1/decide' }), 'Indica el motivo de la elección.');
  // Existing mappings are unchanged.
  assert.equal(errorMessage(400, { error: 'Role must be one of: renta, servicio' }), 'Elige renta o servicio.');
  assert.equal(errorMessage(400, { error: 'Unit is required' }), 'La unidad es obligatoria.');
  assert.equal(errorMessage(409, { error: 'Stale revision' }), 'Otra persona modificó este registro. Actualiza antes de guardar.');
  assert.equal(errorMessage(400, { error: 'Unmapped english text' }), generic);
});
