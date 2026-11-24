// Pure provider and quote comparison helpers (S3, S4): option sets, request payloads, live quote totals and the
// comparison table. Keys match the API JSON (camelCase).

import { decimalScale, formatDecimal, parseDecimal, todayIso } from './work-orders.mjs';

// Kinds and roles live in schema.mjs, whose provider and contact forms use them.
export { contactRoles, providerKinds } from './schema.mjs';
export const currencies = [{ value: 'ARS', label: 'ARS · pesos' }, { value: 'USD', label: 'USD · dólares' }];
export const comparisonStatuses = [{ value: 'abierta', label: 'Abierta' }, { value: 'decidida', label: 'Decidida' }];

const text = value => value == null ? '' : String(value);
const optional = value => {
  const trimmed = text(value).trim();
  return trimmed === '' ? null : trimmed;
};
const number = value => {
  const trimmed = text(value).trim();
  return trimmed === '' ? null : Number(trimmed);
};
const withRevision = (payload, revision) => revision === undefined ? payload : { ...payload, revision };

/** Every provider without a kind, else only that kind. */
export const filterProviders = (rows, kind) => kind ? rows.filter(row => row.kind === kind) : rows;

// Reference prices (S3)

export const emptyPrice = (today = todayIso()) => ({ inputId: '', price: '', currency: 'ARS', unit: '', validOn: today, notes: '' });
export const priceFromRecord = record => Object.fromEntries(['inputId', 'price', 'currency', 'unit', 'validOn', 'notes'].map(key => [key, text(record[key])]));

/** An empty unit is sent as null so the API takes the input's unit; editing keeps the provider. */
export function pricePayload(form, { providerId, revision } = {}) {
  const payload = {
    inputId: form.inputId, price: number(form.price), currency: form.currency, unit: optional(form.unit), validOn: optional(form.validOn),
    notes: optional(form.notes),
  };
  return revision === undefined ? { providerId, ...payload } : withRevision(payload, revision);
}

/** Newest valid date first, without changing the given list. */
export const sortPrices = rows => [...rows].sort((a, b) => text(b.validOn).localeCompare(text(a.validOn)));

/** The newest price of each provider, newest first. */
export function latestPricesByProvider(prices) {
  const latest = new Map();
  for (const price of sortPrices(prices)) if (!latest.has(price.providerId)) latest.set(price.providerId, price);
  return [...latest.values()];
}

// Quote comparisons (S3)

export function comparisonPayload(form, { clientId, revision } = {}) {
  const payload = { title: text(form.title).trim(), requestedOn: optional(form.requestedOn) };
  return revision === undefined ? { clientId, ...payload } : withRevision(payload, revision);
}

export const decidePayload = (form, revision) => ({ quoteId: form.quoteId, reason: text(form.reason).trim(), revision });

/** The list request with the client and status filters, each only when set. */
export function comparisonQuery({ clientId, status } = {}) {
  const params = new URLSearchParams();
  if (clientId) params.set('clientId', clientId);
  if (status) params.set('status', status);
  const query = params.toString();
  return query ? `quote-comparisons?${query}` : 'quote-comparisons';
}

let lineKey = 0;
// mode 'input' names a catalog input; 'text' a free product description.
export const newQuoteLine = () => ({ key: ++lineKey, mode: 'input', inputId: '', productText: '', quantity: '', unit: '', unitPrice: '' });
const lineFields = ['inputId', 'productText', 'quantity', 'unit', 'unitPrice'];
const blankLine = line => lineFields.every(key => text(line[key]).trim() === '');

export const emptyQuote = (today = todayIso()) => ({ providerId: '', receivedOn: today, currency: 'ARS', notes: '', lines: [newQuoteLine()] });

export function quoteFromRecord(record) {
  const lines = (record.lines ?? []).map(line => ({
    key: ++lineKey, mode: line.inputId ? 'input' : 'text', ...Object.fromEntries(lineFields.map(key => [key, text(line[key])])),
  }));
  return { providerId: text(record.providerId), receivedOn: text(record.receivedOn), currency: text(record.currency), notes: text(record.notes),
    lines: lines.length ? lines : [newQuoteLine()] };
}

/** Create and edit send the whole quote with its lines (full replace); blank lines are skipped, an empty unit is the input's. */
export function quotePayload(form, { revision } = {}) {
  const lines = form.lines.filter(line => !blankLine(line)).map(line => {
    const input = line.mode === 'input';
    return {
      inputId: input ? optional(line.inputId) : null, productText: input ? null : optional(line.productText),
      quantity: number(line.quantity), unit: optional(line.unit), unitPrice: number(line.unitPrice),
    };
  });
  return withRevision({ providerId: form.providerId, receivedOn: optional(form.receivedOn), currency: form.currency, notes: optional(form.notes), lines },
    revision);
}

// Exact products in hundred-millionths (ten-thousandths squared), then rounded half away from zero to 4 decimals.
function product(quantity, unitPrice) {
  const q = parseDecimal(quantity);
  const price = parseDecimal(unitPrice);
  return q && price !== null ? q * price : null;
}
const rounded = units => formatDecimal((units + decimalScale / 2n) / decimalScale);

/** quantity × unit price at the API's 4 decimals; null while the quantity is not positive or a value is invalid. */
export function quoteLineTotal(quantity, unitPrice) {
  const units = product(quantity, unitPrice);
  return units === null ? null : rounded(units);
}

/** The rounded sum of the exact line products, as the API computes Quote.total; incomplete lines count as zero. */
export const quoteTotal = lines => rounded(lines.reduce((sum, line) => sum + (product(line.quantity, line.unitPrice) ?? 0n), 0n));

const moneyFormat = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 4 });
/** "ARS 1.234,5": currency code and es-AR amount, up to 4 decimals. */
export const formatMoney = (value, currency) => `${currency} ${moneyFormat.format(Number(value))}`;

/**
 * The comparison detail as a table: one column per quote in the API order, one row per product. Each cell holds that
 * quote's offers for the product and is marked cheapest when the API names it cheapest in its currency. Currencies are
 * never converted (Q19), so mixed currencies get a note.
 */
export function comparisonTable(detail, providers) {
  const names = new Map(providers.map(provider => [provider.id, provider.name]));
  const quotes = detail.quotes ?? [];
  const columns = quotes.map(quote => ({
    quoteId: quote.id, provider: names.get(quote.providerId) ?? 'Sin referencia', receivedOn: quote.receivedOn, currency: quote.currency,
    total: quote.total, chosen: detail.comparison?.status === 'decidida' && detail.comparison.chosenQuoteId === quote.id,
  }));
  const currency = new Map(quotes.map(quote => [quote.id, quote.currency]));
  const rows = (detail.products ?? []).map(item => ({
    key: item.key, label: item.label,
    cells: quotes.map(quote => ({
      quoteId: quote.id, offers: item.offers.filter(offer => offer.quoteId === quote.id),
      cheapest: item.cheapestQuoteIdByCurrency?.[currency.get(quote.id)] === quote.id,
    })),
  }));
  const used = [...new Set(quotes.map(quote => quote.currency))].sort();
  const mixedCurrencies = used.length > 1;
  return {
    columns, rows, currencies: used, mixedCurrencies,
    note: mixedCurrencies ? `Hay presupuestos en ${used.join(' y ')}: los precios no se convierten y el más barato se marca dentro de cada moneda (Q19).` : null,
  };
}
