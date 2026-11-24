// Pure work order (OT) helpers: form state, request payloads and the live totals shown before the server stores them.

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
    firmId: '', issuedOn: today, laborTypeId: '', stageId: '', contractorId: '', fieldId: '', plots: [],
    campaignId: '', cropId: '', variety: '', observations: '', responsibleEngineer: '', lines: [newLine()],
  };
}

const text = value => value == null ? '' : String(value);

export function orderFromRecord(record) {
  const header = ['firmId', 'issuedOn', 'laborTypeId', 'stageId', 'contractorId', 'fieldId', 'campaignId', 'cropId', 'variety',
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
export function orderPayload(form, { revision } = {}) {
  const payload = revision === undefined ? { firmId: form.firmId } : {};
  Object.assign(payload, {
    issuedOn: optional(form.issuedOn), laborTypeId: form.laborTypeId, stageId: optional(form.stageId), contractorId: form.contractorId,
    fieldId: form.fieldId, campaignId: optional(form.campaignId), cropId: optional(form.cropId), variety: optional(form.variety),
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

export const orderLabel = (firm, number) => firm ? `${firm.name} Nº ${number}` : `Nº ${number}`;

/** ISO date (yyyy-mm-dd) as dd/mm/yyyy. */
export const displayDate = iso => /^\d{4}-\d{2}-\d{2}$/.test(text(iso)) ? iso.split('-').reverse().join('/') : text(iso);
