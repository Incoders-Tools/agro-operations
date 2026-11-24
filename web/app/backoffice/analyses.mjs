// Pure soil and seed analysis helpers (S1, S2): form state, request payloads, client-side checks, the per-parameter
// history and the seeding calculator. Keys match the API JSON (camelCase).

import { fold } from './schema.mjs';

export const samplingMethods = [
  { value: 'promedio', label: 'Promedio del lote' },
  { value: 'estaciones', label: 'Estaciones de muestreo' },
];
// Suggestions only: the parameter and unit stay free text until the engineers confirm the list.
export const soilParameters = [
  'pH', 'Materia orgánica', 'Fósforo extractable (P Bray)', 'Nitratos (N-NO3)', 'Azufre (S-SO4)', 'Zinc', 'Boro',
  'Conductividad eléctrica', 'CIC', 'Calcio', 'Magnesio', 'Potasio', 'Sodio', 'Textura',
];
export const soilUnits = ['%', 'ppm', 'mg/kg', 'dS/m', 'cmol/kg'];

const text = value => value == null ? '' : String(value);
const optional = value => {
  const trimmed = text(value).trim();
  return trimmed === '' ? null : trimmed;
};
const number = value => {
  const trimmed = text(value).trim();
  return trimmed === '' ? null : Number(trimmed);
};

const amount = new Map();
/** es-AR number: dot for thousands, decimal comma, up to `decimals` decimals. */
export function formatNumber(value, decimals = 2) {
  if (!amount.has(decimals)) amount.set(decimals, new Intl.NumberFormat('es-AR', { maximumFractionDigits: decimals }));
  return amount.get(decimals).format(value);
}

// Soil analysis (S1)

let resultKey = 0;
export const newResult = () => ({ key: ++resultKey, parameter: '', value: '', unit: '', depth: '', station: '' });
const resultFields = ['parameter', 'value', 'unit', 'depth', 'station'];
const blank = row => resultFields.every(key => text(row[key]).trim() === '');

export const emptySoilAnalysis = () => ({
  campaignId: '', sampledOn: '', analyzedOn: '', laboratoryId: '', samplingMethod: 'promedio', samplingDetail: '', mapLink: '', notes: '',
  results: [newResult()],
});

const soilHeader = ['campaignId', 'sampledOn', 'analyzedOn', 'laboratoryId', 'samplingMethod', 'samplingDetail', 'mapLink', 'notes'];

export function soilFromRecord(record) {
  const results = (record.results ?? []).map(row => ({ key: ++resultKey, ...Object.fromEntries(resultFields.map(key => [key, text(row[key])])) }));
  return { ...Object.fromEntries(soilHeader.map(key => [key, text(record[key])])), results: results.length ? results : [newResult()] };
}

/** Create and edit send the whole analysis with its results (full replace); blank lines are skipped. */
export function soilPayload(form, { plotId, revision } = {}) {
  const stations = form.samplingMethod === 'estaciones';
  const payload = {
    plotId, campaignId: form.campaignId, sampledOn: optional(form.sampledOn), analyzedOn: optional(form.analyzedOn),
    laboratoryId: optional(form.laboratoryId), samplingMethod: form.samplingMethod, samplingDetail: optional(form.samplingDetail),
    mapLink: optional(form.mapLink), notes: optional(form.notes),
    results: form.results.filter(row => !blank(row)).map(row => ({
      parameter: text(row.parameter).trim(), value: number(row.value), unit: optional(row.unit), depth: optional(row.depth),
      station: stations ? optional(row.station) : null,
    })),
  };
  if (revision !== undefined) payload.revision = revision;
  return payload;
}

/** The rules the API also enforces, checked before saving; null when the form can be sent. */
export function soilProblem(form) {
  const results = form.results.filter(row => !blank(row));
  if (!results.length) return 'Carga al menos un resultado del análisis.';
  if (form.samplingMethod === 'estaciones' && results.some(row => !optional(row.station))) {
    return 'Cada resultado necesita su estación cuando el muestreo es por estaciones.';
  }
  if (/\s/.test(text(form.mapLink).trim())) return 'El enlace al shape o KML no puede tener espacios.';
  return null;
}

/** Newest sampling first, without changing the given list. */
export const sortSoilAnalyses = rows => [...rows].sort((a, b) => text(b.sampledOn).localeCompare(text(a.sampledOn)));

const canonical = new Map(soilParameters.map(name => [fold(name), name]));

/**
 * Every result of the plot's analyses grouped by parameter (case and accents ignored), groups by name and each one's
 * values oldest first, so a parameter that barely changes reads as one row of values across campaigns.
 */
export function parameterHistory(analyses) {
  const groups = new Map();
  for (const analysis of analyses) {
    for (const row of analysis.results ?? []) {
      const key = fold(row.parameter).trim();
      if (!key) continue;
      if (!groups.has(key)) groups.set(key, { parameter: canonical.get(key) ?? text(row.parameter).trim(), entries: [] });
      groups.get(key).entries.push({
        analysisId: analysis.id, campaignId: analysis.campaignId, sampledOn: analysis.sampledOn,
        value: row.value, unit: row.unit ?? null, depth: row.depth ?? null, station: row.station ?? null,
      });
    }
  }
  return [...groups.values()]
    .sort((a, b) => a.parameter.localeCompare(b.parameter, 'es', { sensitivity: 'base' }))
    .map(group => ({ ...group, entries: group.entries.sort((a, b) => text(a.sampledOn).localeCompare(text(b.sampledOn))) }));
}

// Seed analysis (S2)

export const treatments = [
  { key: 'Treated', label: 'Con curasemilla' },
  { key: 'Untreated', label: 'Sin curasemilla' },
];
export const fractions = [
  { key: 'germination', label: 'Poder germinativo (%)' },
  { key: 'abnormal', label: 'Plántulas anormales (%)' },
  { key: 'dead', label: 'Semillas muertas (%)' },
  { key: 'hard', label: 'Semillas duras (%)' },
];
const percentKeys = [...treatments.flatMap(({ key }) => fractions.map(fraction => `${fraction.key}${key}Pct`)), 'vigorPct'];
const seedText = ['clientId', 'cropId', 'variety', 'campaignId', 'analyzedOn', 'laboratoryId', 'notes'];
const seedNumbers = ['thousandSeedWeightG', ...percentKeys];

export const emptySeedAnalysis = () => Object.fromEntries([...seedText, ...seedNumbers].map(key => [key, '']));
export const seedFromRecord = record => Object.fromEntries([...seedText, ...seedNumbers].map(key => [key, text(record[key])]));

/** Every measured value is optional and goes as null when empty; zero is a measured value. */
export function seedPayload(form, { revision } = {}) {
  const payload = {
    clientId: form.clientId, cropId: form.cropId, variety: optional(form.variety), campaignId: optional(form.campaignId),
    analyzedOn: optional(form.analyzedOn), laboratoryId: optional(form.laboratoryId),
    ...Object.fromEntries(seedNumbers.map(key => [key, number(form[key])])), notes: optional(form.notes),
  };
  if (revision !== undefined) payload.revision = revision;
  return payload;
}

/** Percentages between 0 and 100; per treatment the present fractions add up to at most 100. Null when valid. */
export function seedProblem(form) {
  const values = percentKeys.map(key => number(form[key])).filter(value => value !== null);
  if (values.some(value => !(value >= 0 && value <= 100))) return 'Los porcentajes deben estar entre 0 y 100.';
  for (const { key, label } of treatments) {
    // Ten-thousandths keep decimal sums such as 90.1 + 9.7 + 0.2 exact.
    const units = fractions.reduce((sum, fraction) => sum + Math.round((number(form[`${fraction.key}${key}Pct`]) ?? 0) * 10000), 0);
    if (units > 1000000) return `${label}: germinadas, anormales, muertas y duras suman ${formatNumber(units / 10000, 4)} %, más de 100 %.`;
  }
  return null;
}

/** The list request with the client and crop filters, each only when set. */
export function seedQuery({ clientId, cropId }) {
  const params = new URLSearchParams();
  if (clientId) params.set('clientId', clientId);
  if (cropId) params.set('cropId', cropId);
  const query = params.toString();
  return query ? `seed-analyses?${query}` : 'seed-analyses';
}

export function searchSeeds(rows, term) {
  const needle = fold(term).trim();
  return needle ? rows.filter(row => fold(row.variety).includes(needle)) : rows;
}

/**
 * Seeding rate from a target stand (assumption awaiting the engineers, Q18):
 * semillas/ha = plantas/ha ÷ (PG × eficiencia); kg/ha = semillas/ha × P1000 ÷ 1.000.000;
 * semillas por metro lineal = semillas/ha × distancia (m) ÷ 10.000.
 * Germination with seed treatment is used when present, else without. Each output is null when its data is missing,
 * and `missing` explains why.
 */
export function seedingRate({ plants, per, efficiencyPct, rowSpacingCm }, analysis) {
  const missing = [];
  const target = number(plants);
  const plantsPerHa = target !== null && target > 0 ? (per === 'm2' ? target * 10000 : target) : null;
  if (plantsPerHa === null) missing.push('Indica las plantas objetivo.');
  const efficiency = number(efficiencyPct);
  const efficient = efficiency !== null && efficiency > 0 && efficiency <= 100;
  if (!efficient) missing.push('La eficiencia de emergencia debe ser mayor que 0 y hasta 100 %.');
  const treated = analysis?.germinationTreatedPct;
  const untreated = analysis?.germinationUntreatedPct;
  const germination = treated != null ? { pct: Number(treated), source: 'treated' }
    : untreated != null ? { pct: Number(untreated), source: 'untreated' } : null;
  if (!germination) missing.push('El análisis no tiene poder germinativo (ni con ni sin curasemilla).');
  else if (!(germination.pct > 0)) missing.push('El poder germinativo es 0 %: no se pueden calcular las semillas.');
  const weight = analysis?.thousandSeedWeightG == null ? null : Number(analysis.thousandSeedWeightG);
  if (!(weight > 0)) missing.push('Falta el peso de 1000 semillas para calcular kg/ha.');
  const spacing = number(rowSpacingCm);
  if (!(spacing > 0)) missing.push('Indica la distancia entre surcos para calcular las semillas por metro lineal.');

  // Percentages stay whole numbers in the division, so 320000 ÷ (80 % × 80 %) is exactly 500000.
  const seedsPerHa = plantsPerHa !== null && efficient && germination?.pct > 0 ? plantsPerHa * 10000 / (germination.pct * efficiency) : null;
  return {
    germination, plantsPerHa, seedsPerHa,
    kgPerHa: seedsPerHa !== null && weight > 0 ? seedsPerHa * weight / 1000000 : null,
    // Spacing in cm: × (cm ÷ 100) ÷ 10.000 is ÷ 1.000.000.
    seedsPerMetre: seedsPerHa !== null && spacing > 0 ? seedsPerHa * spacing / 1000000 : null,
    missing,
  };
}
