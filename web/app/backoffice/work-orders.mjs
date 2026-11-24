// Pure work order (OT) helpers: form state, request payloads and the live totals shown before the server stores them.

import { fold, laborGroups } from './schema.mjs';

export const statuses = [
  { value: 'emitida', label: 'Emitida' },
  { value: 'realizada', label: 'Realizada' },
  { value: 'cancelada', label: 'Cancelada' },
];

let lineKey = 0;
export const newLine = () => ({ key: ++lineKey, inputId: '', dose: '', term: '' });

const pad = number => String(number).padStart(2, '0');
// Local calendar date, so an order created late in the evening is not dated tomorrow.
export const todayIso = (date = new Date()) => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;

export function emptyOrder(today = todayIso()) {
  return {
    clientId: '', firmId: '', issuedOn: today, laborTypeId: '', stageId: '', contractorId: '', plots: [],
    campaignId: '', cropId: '', variety: '', observations: '', responsibleEngineer: '', lines: [newLine()],
  };
}

const text = value => value == null ? '' : String(value);

export function orderFromRecord(record) {
  const header = ['firmId', 'issuedOn', 'laborTypeId', 'stageId', 'contractorId', 'campaignId', 'cropId', 'variety',
    'observations', 'responsibleEngineer'];
  return {
    ...Object.fromEntries(header.map(key => [key, text(record[key])])),
    plots: record.plots.map(plot => ({ plotId: plot.plotId, worked: text(plot.workedAreaHectares) })),
    lines: record.lines.length ? record.lines.map(line => ({ ...newLine(), inputId: line.inputId, dose: text(line.dosePerHectare) })) : [newLine()],
  };
}

const optional = value => {
  const trimmed = text(value).trim();
  return trimmed === '' ? null : trimmed;
};

// Create sends the firm (the server takes the number from it); update sends the revision and never the firm.
// The client only narrows the form and there is no field: each plot belongs to its own field.
export function orderPayload(form, { revision } = {}) {
  const payload = revision === undefined ? { firmId: form.firmId } : {};
  Object.assign(payload, {
    issuedOn: optional(form.issuedOn), laborTypeId: form.laborTypeId, stageId: optional(form.stageId), contractorId: form.contractorId,
    campaignId: optional(form.campaignId), cropId: optional(form.cropId), variety: optional(form.variety),
    observations: optional(form.observations), responsibleEngineer: optional(form.responsibleEngineer),
    plots: form.plots.map(plot => ({ plotId: plot.plotId, workedAreaHectares: Number(text(plot.worked).trim()) })),
    lines: form.lines.filter(line => line.inputId).map(line => ({ inputId: line.inputId, dosePerHectare: Number(text(line.dose).trim()) })),
  });
  if (revision !== undefined) payload.revision = revision;
  return payload;
}

// Exact decimal arithmetic at the API's 4 decimals: values are BigInt ten-thousandths, so 0.1 + 0.2 is 0.3.
const scale = 10000n;
function parse(value) {
  const match = /^(\d+)(?:\.(\d{1,4}))?$/.exec(text(value).trim());
  if (!match) return null;
  return BigInt(match[1]) * scale + BigInt((match[2] ?? '').padEnd(4, '0'));
}
function format(units) {
  const whole = units / scale;
  const fraction = (units % scale).toString().padStart(4, '0').replace(/0+$/, '');
  return fraction ? `${whole}.${fraction}` : String(whole);
}
// Shared with the quote line totals (providers.mjs).
export { parse as parseDecimal, format as formatDecimal, scale as decimalScale };

/** Sum of the ha a trabajar; blank or invalid entries count as zero (the form flags them). */
export const workedTotal = plots => format(plots.reduce((sum, plot) => sum + (parse(plot.worked) ?? 0n), 0n));

/** dose × total worked area, rounded half away from zero to 4 decimals as the server does; null when not computable. */
export function lineTotal(dose, worked) {
  const d = parse(dose);
  const area = parse(worked);
  if (!d || !area) return null;
  return format((d * area + scale / 2n) / scale);
}

/** 'below' or 'above' when the dose leaves the input's reference range; the dose itself is never changed. */
export function doseRange(dose, input) {
  const value = parse(dose);
  if (value === null || !input) return null;
  const min = input.doseMin == null ? null : parse(input.doseMin);
  const max = input.doseMax == null ? null : parse(input.doseMax);
  if (min !== null && value < min) return 'below';
  if (max !== null && value > max) return 'above';
  return null;
}

/** True when the worked area is larger than the plot's area. */
export function exceedsPlot(worked, area) {
  const value = parse(worked);
  const limit = parse(area);
  return value !== null && limit !== null && value > limit;
}

/** The OT crop comes from the plot's first crop of the chosen campaign. */
export function plotCropPrefill(plotCrops, campaignId) {
  if (!campaignId) return null;
  const first = plotCrops.find(row => row.campaignId === campaignId && Number(row.position) === 1);
  return first ? { cropId: first.cropId, variety: text(first.variety) } : null;
}

export const laborLabel = labor => labor ? (labor.abbreviation ? `${labor.name} (${labor.abbreviation})` : labor.name) : 'Sin referencia';

/** OT labor select: one optgroup per category in reference order, "Otras" last; options show the abbreviation. */
export const laborOptionGroups = labors => laborGroups(labors).map(group => ({
  label: group.label, options: group.labors.map(labor => ({ value: labor.id, label: laborLabel(labor) })),
}));

export const orderLabel = (firm, number) => firm ? `${firm.name} Nº ${number}` : `Nº ${number}`;

/** The firms of a client; with exactly one, it is preselected. */
export function clientFirms(firms, clientId) {
  const own = clientId ? firms.filter(firm => firm.clientId === clientId) : [];
  return { firms: own, preselected: own.length === 1 ? own[0].id : '' };
}

const byName = (a, b) => a.name.localeCompare(b.name, 'es', { sensitivity: 'base' });
// Plots without a code go after the coded ones.
const byCode = (a, b) => Number(!a.code) - Number(!b.code) || text(a.code).localeCompare(text(b.code), 'es', { numeric: true }) || byName(a, b);

/**
 * The client's fields in name order, each with its plots in code order. A term keeps the whole field when it matches
 * the field name, otherwise only the plots whose name or code match; fields left without plots are dropped.
 */
export function plotGroups(fields, plots, term) {
  const needle = fold(term).trim();
  const matches = value => fold(value).includes(needle);
  return [...fields].sort(byName).flatMap(field => {
    const own = plots.filter(plot => plot.fieldId === field.id).sort(byCode);
    if (!needle) return [{ field, plots: own }];
    const shown = matches(field.name) ? own : own.filter(plot => matches(plot.name) || matches(plot.code));
    return shown.length ? [{ field, plots: shown }] : [];
  });
}

/** The list request: search text, firm and status, each only when set. */
export function orderQuery({ firmId, status, q }) {
  const params = new URLSearchParams();
  if (firmId) params.set('firmId', firmId);
  if (status) params.set('status', status);
  if (text(q).trim()) params.set('q', text(q).trim());
  const query = params.toString();
  return query ? `work-orders?${query}` : 'work-orders';
}

/** Names of a summary's fieldIds, in the order the API gives them. */
export function fieldNames(fields, fieldIds) {
  if (!fieldIds?.length) return 'Sin campos';
  return fieldIds.map(id => fields.find(field => field.id === id)?.name ?? 'Sin referencia').join(', ');
}

/** The browser proposes document.title as the PDF name, so the title is the API's workbook-pattern file name. */
export function documentTitle(document) {
  if (!document) return 'Orden de trabajo';
  return text(document.fileName).trim() || `ORDEN_${document.number}`;
}

export const cropLabel = document => [document.cropName, document.variety].filter(Boolean).join(' · ') || 'Sin indicar';

export function productLabel(line) {
  // Workbook names already carry "(<mix order> <formulation>)"; repeating it would print it twice.
  if (/\(\s*\d+\s+[A-Za-z]+\s*\)\s*$/.test(line.inputName ?? '')) return line.inputName;
  const name = line.formulation ? `${line.inputName} ${line.formulation}` : line.inputName;
  return line.mixOrder ? `${name} · orden de mezcla ${line.mixOrder}` : name;
}

const amountFormat = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 4 });
/** Quantity as printed on the issued orders: decimal comma, dot for thousands, up to 4 decimals. */
export const formatAmount = value => value === null || value === undefined || value === '' ? '' : amountFormat.format(Number(value));

/** ISO date (yyyy-mm-dd) as dd/mm/yyyy. */
export const displayDate = iso => /^\d{4}-\d{2}-\d{2}$/.test(text(iso)) ? iso.split('-').reverse().join('/') : text(iso);
