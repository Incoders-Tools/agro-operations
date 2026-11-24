import test from 'node:test';
import assert from 'node:assert/strict';
import { errorMessage } from '../app/backoffice/api.mjs';
import {
  emptySeedAnalysis, emptySoilAnalysis, formatNumber, newResult, parameterHistory, seedFromRecord, seedPayload, seedProblem, seedQuery,
  seedingRate, searchSeeds, soilFromRecord, soilParameters, soilPayload, soilProblem, soilUnits, sortSoilAnalyses,
} from '../app/backoffice/analyses.mjs';

const generic = 'Revisa los datos e intenta de nuevo.';
const line = changes => ({ ...newResult(), ...changes });

const soilForm = {
  ...emptySoilAnalysis(), campaignId: 'k1', sampledOn: '2026-05-10', analyzedOn: '', laboratoryId: '', samplingMethod: 'promedio',
  samplingDetail: '  ', mapLink: ' https://example.com/zona.kml ', notes: '',
  results: [
    line({ parameter: ' pH ', value: '6.2', unit: '', depth: '0-20 cm', station: 'E1' }),
    line({ parameter: '', value: '', unit: '', depth: '', station: '' }),
    line({ parameter: 'Fósforo extractable (P Bray)', value: '18', unit: 'ppm', depth: '', station: '' }),
  ],
};

test('S1 soil payload trims text, sends empty optionals as null, skips blank lines and drops stations when averaging', () => {
  assert.deepEqual(soilPayload(soilForm, { plotId: 'p1' }), {
    plotId: 'p1', campaignId: 'k1', sampledOn: '2026-05-10', analyzedOn: null, laboratoryId: null, samplingMethod: 'promedio',
    samplingDetail: null, mapLink: 'https://example.com/zona.kml', notes: null,
    results: [
      { parameter: 'pH', value: 6.2, unit: null, depth: '0-20 cm', station: null },
      { parameter: 'Fósforo extractable (P Bray)', value: 18, unit: 'ppm', depth: null, station: null },
    ],
  });
});

test('S1 soil payload keeps stations when sampling by stations and sends the revision on edit', () => {
  const form = { ...soilForm, samplingMethod: 'estaciones', analyzedOn: '2026-05-20', laboratoryId: 'lab1', samplingDetail: '4 estaciones georreferenciadas' };
  const payload = soilPayload(form, { plotId: 'p1', revision: 3 });
  assert.equal(payload.revision, 3);
  assert.equal(payload.analyzedOn, '2026-05-20');
  assert.equal(payload.laboratoryId, 'lab1');
  assert.equal(payload.samplingDetail, '4 estaciones georreferenciadas');
  assert.deepEqual(payload.results.map(row => row.station), ['E1', null]);
  assert.equal('revision' in soilPayload(soilForm, { plotId: 'p1' }), false);
});

test('S1 soil check requires one result, a station on every result by stations and a map link without spaces', () => {
  assert.equal(soilProblem(soilForm), null);
  assert.equal(soilProblem({ ...soilForm, results: [line({})] }), 'Carga al menos un resultado del análisis.');
  assert.equal(soilProblem({ ...soilForm, samplingMethod: 'estaciones' }),
    'Cada resultado necesita su estación cuando el muestreo es por estaciones.');
  const stations = { ...soilForm, samplingMethod: 'estaciones', results: soilForm.results.map(row => ({ ...row, station: row.parameter ? 'E2' : '' })) };
  assert.equal(soilProblem(stations), null);
  assert.equal(soilProblem({ ...soilForm, mapLink: 'zona de muestreo.kml' }), 'El enlace al shape o KML no puede tener espacios.');
});

test('S1 soil form state from a stored analysis turns null into empty strings and keeps its results', () => {
  const record = {
    id: 's1', plotId: 'p1', campaignId: 'k1', sampledOn: '2026-05-10', analyzedOn: null, laboratoryId: null, samplingMethod: 'estaciones',
    samplingDetail: null, mapLink: null, notes: 'Obs', revision: 2,
    results: [{ id: 'r1', parameter: 'pH', value: 6.2, unit: null, depth: null, station: 'E1' }],
  };
  const form = soilFromRecord(record);
  assert.equal(form.analyzedOn, '');
  assert.equal(form.notes, 'Obs');
  assert.equal(form.samplingMethod, 'estaciones');
  assert.equal(form.results.length, 1);
  assert.deepEqual({ ...form.results[0], key: undefined }, { key: undefined, parameter: 'pH', value: '6.2', unit: '', depth: '', station: 'E1' });
  assert.equal(emptySoilAnalysis().samplingMethod, 'promedio');
  assert.equal(emptySoilAnalysis().results.length, 1);
  assert.ok(soilParameters.includes('Fósforo extractable (P Bray)'));
  assert.equal(soilParameters.length, 14);
  assert.deepEqual(soilUnits, ['%', 'ppm', 'mg/kg', 'dS/m', 'cmol/kg']);
});

test('S1 soil analyses sort newest sampling first', () => {
  const rows = [{ id: 'a', sampledOn: '2024-05-01' }, { id: 'b', sampledOn: '2026-05-01' }, { id: 'c', sampledOn: '2025-05-01' }];
  assert.deepEqual(sortSoilAnalyses(rows).map(row => row.id), ['b', 'c', 'a']);
  assert.deepEqual(rows.map(row => row.id), ['a', 'b', 'c']);
});

test('S1 per-parameter history groups values across campaigns, oldest first, ignoring case and accents', () => {
  const analyses = [
    { id: 'n', campaignId: 'k26', sampledOn: '2026-05-10', results: [
      { id: 'r3', parameter: 'ph', value: 6.4, unit: null, depth: '0-20 cm', station: null },
      { id: 'r4', parameter: 'Fosforo extractable (P Bray)', value: 21, unit: 'ppm', depth: '0-20 cm', station: null }] },
    { id: 'o', campaignId: 'k24', sampledOn: '2024-05-12', results: [
      { id: 'r1', parameter: 'pH', value: 6.1, unit: null, depth: '0-20 cm', station: 'E1' },
      { id: 'r2', parameter: 'Zinc', value: 0.8, unit: 'ppm', depth: null, station: 'E1' }] },
    { id: 'x', campaignId: 'k25', sampledOn: '2025-05-12' },
  ];
  assert.deepEqual(parameterHistory(analyses), [
    { parameter: 'Fósforo extractable (P Bray)', entries: [
      { analysisId: 'n', campaignId: 'k26', sampledOn: '2026-05-10', value: 21, unit: 'ppm', depth: '0-20 cm', station: null }] },
    { parameter: 'pH', entries: [
      { analysisId: 'o', campaignId: 'k24', sampledOn: '2024-05-12', value: 6.1, unit: null, depth: '0-20 cm', station: 'E1' },
      { analysisId: 'n', campaignId: 'k26', sampledOn: '2026-05-10', value: 6.4, unit: null, depth: '0-20 cm', station: null }] },
    { parameter: 'Zinc', entries: [
      { analysisId: 'o', campaignId: 'k24', sampledOn: '2024-05-12', value: 0.8, unit: 'ppm', depth: null, station: 'E1' }] },
  ]);
  assert.deepEqual(parameterHistory([]), []);
});

const seedForm = {
  ...emptySeedAnalysis(), clientId: 'c1', cropId: 'r1', variety: ' DM 46 ', campaignId: '', analyzedOn: '2026-04-02', laboratoryId: '',
  thousandSeedWeightG: '150', germinationTreatedPct: '85', abnormalTreatedPct: '5', deadTreatedPct: '7', hardTreatedPct: '3',
  germinationUntreatedPct: '', abnormalUntreatedPct: '', deadUntreatedPct: '', hardUntreatedPct: '', vigorPct: '80', notes: ' ',
};

test('S2 seed payload converts measured values, keeps empty ones null and sends the revision on edit', () => {
  assert.deepEqual(seedPayload(seedForm), {
    clientId: 'c1', cropId: 'r1', variety: 'DM 46', campaignId: null, analyzedOn: '2026-04-02', laboratoryId: null,
    thousandSeedWeightG: 150, germinationTreatedPct: 85, abnormalTreatedPct: 5, deadTreatedPct: 7, hardTreatedPct: 3,
    germinationUntreatedPct: null, abnormalUntreatedPct: null, deadUntreatedPct: null, hardUntreatedPct: null, vigorPct: 80, notes: null,
  });
  const edited = seedPayload({ ...seedForm, germinationTreatedPct: '0' }, { revision: 4 });
  assert.equal(edited.revision, 4);
  assert.equal(edited.germinationTreatedPct, 0);
  const form = seedFromRecord({ ...seedPayload(seedForm), id: 'a1', revision: 4 });
  assert.equal(form.thousandSeedWeightG, '150');
  assert.equal(form.germinationUntreatedPct, '');
  assert.equal(form.notes, '');
});

test('S2 seed check: each block adds up to at most 100 and every percentage stays between 0 and 100', () => {
  assert.equal(seedProblem(seedForm), null);
  assert.equal(seedProblem(emptySeedAnalysis()), null);
  // Exactly 100 is allowed, also with decimals that do not sum exactly in binary floating point.
  assert.equal(seedProblem({ ...seedForm, germinationUntreatedPct: '90.1', abnormalUntreatedPct: '9.7', deadUntreatedPct: '0.2' }), null);
  assert.equal(seedProblem({ ...seedForm, hardTreatedPct: '3.5' }),
    'Con curasemilla: germinadas, anormales, muertas y duras suman 100,5 %, más de 100 %.');
  assert.equal(seedProblem({ ...seedForm, germinationUntreatedPct: '95', deadUntreatedPct: '6' }),
    'Sin curasemilla: germinadas, anormales, muertas y duras suman 101 %, más de 100 %.');
  assert.equal(seedProblem({ ...seedForm, vigorPct: '101' }), 'Los porcentajes deben estar entre 0 y 100.');
  assert.equal(seedProblem({ ...seedForm, abnormalTreatedPct: '-1' }), 'Los porcentajes deben estar entre 0 y 100.');
});

test('S2 seed list query filters by client and crop; search matches the variety ignoring case and accents', () => {
  assert.equal(seedQuery({ clientId: '', cropId: '' }), 'seed-analyses');
  assert.equal(seedQuery({ clientId: 'c1', cropId: '' }), 'seed-analyses?clientId=c1');
  assert.equal(seedQuery({ clientId: 'c1', cropId: 'r1' }), 'seed-analyses?clientId=c1&cropId=r1');
  const rows = [{ id: 1, variety: 'Híbrido Norte' }, { id: 2, variety: 'DM 46' }, { id: 3, variety: null }];
  assert.deepEqual(searchSeeds(rows, 'hibrido').map(row => row.id), [1]);
  assert.deepEqual(searchSeeds(rows, ' ').map(row => row.id), [1, 2, 3]);
});

const analysis = { thousandSeedWeightG: 150, germinationTreatedPct: 80, germinationUntreatedPct: 90 };

test('S2 seeding calculator uses germination with seed treatment and gives exact seeds/ha, kg/ha and seeds per metre', () => {
  const rate = seedingRate({ plants: '320000', per: 'ha', efficiencyPct: '80', rowSpacingCm: '52.5' }, analysis);
  assert.deepEqual(rate, {
    germination: { pct: 80, source: 'treated' }, plantsPerHa: 320000, seedsPerHa: 500000, kgPerHa: 75, seedsPerMetre: 26.25, missing: [],
  });
  assert.equal(formatNumber(rate.seedsPerHa, 0), '500.000');
  assert.equal(formatNumber(rate.seedsPerMetre, 2), '26,25');
  assert.equal(formatNumber(370370.37037, 0), '370.370');
  // Plants per m2 convert to plants per ha.
  assert.equal(seedingRate({ plants: '32', per: 'm2', efficiencyPct: '80', rowSpacingCm: '' }, analysis).seedsPerHa, 500000);
});

test('S2 seeding calculator falls back to germination without seed treatment', () => {
  const rate = seedingRate({ plants: '81000', per: 'ha', efficiencyPct: '90', rowSpacingCm: '' }, { ...analysis, germinationTreatedPct: null });
  assert.deepEqual(rate.germination, { pct: 90, source: 'untreated' });
  assert.equal(rate.seedsPerHa, 100000);
  assert.equal(rate.kgPerHa, 15);
  assert.equal(rate.seedsPerMetre, null);
  assert.deepEqual(rate.missing, ['Indica la distancia entre surcos para calcular las semillas por metro lineal.']);
});

test('S2 seeding calculator explains what is missing', () => {
  const none = seedingRate({ plants: '', per: 'ha', efficiencyPct: '90', rowSpacingCm: '' }, {});
  assert.equal(none.seedsPerHa, null);
  assert.equal(none.kgPerHa, null);
  assert.equal(none.seedsPerMetre, null);
  assert.equal(none.germination, null);
  assert.deepEqual(none.missing, [
    'Indica las plantas objetivo.',
    'El análisis no tiene poder germinativo (ni con ni sin curasemilla).',
    'Falta el peso de 1000 semillas para calcular kg/ha.',
    'Indica la distancia entre surcos para calcular las semillas por metro lineal.',
  ]);
  const zero = seedingRate({ plants: '300000', per: 'ha', efficiencyPct: '0', rowSpacingCm: '52' }, { ...analysis, germinationTreatedPct: 0 });
  assert.equal(zero.seedsPerHa, null);
  assert.deepEqual(zero.missing, [
    'La eficiencia de emergencia debe ser mayor que 0 y hasta 100 %.',
    'El poder germinativo es 0 %: no se pueden calcular las semillas.',
  ]);
  const noWeight = seedingRate({ plants: '320000', per: 'ha', efficiencyPct: '80', rowSpacingCm: '52.5' }, { germinationTreatedPct: 80 });
  assert.equal(noWeight.seedsPerHa, 500000);
  assert.equal(noWeight.kgPerHa, null);
  assert.equal(noWeight.seedsPerMetre, 26.25);
  assert.deepEqual(noWeight.missing, ['Falta el peso de 1000 semillas para calcular kg/ha.']);
});

test('S1 S2 analysis 400 messages map to Spanish', () => {
  const cases = {
    'Sampling method must be promedio or estaciones': 'Elige un método de muestreo válido.',
    'At least one result is required': 'Carga al menos un resultado del análisis.',
    'Each result needs a station when sampling by stations': 'Cada resultado necesita su estación cuando el muestreo es por estaciones.',
    'Percentages must be between 0 and 100': 'Los porcentajes deben estar entre 0 y 100.',
    'Seed fractions add up to more than 100': 'Germinadas, anormales, muertas y duras suman más de 100 % en un tratamiento.',
    'Laboratory must be a soil laboratory': 'Elige un laboratorio de suelos.',
    'Laboratory must be a seed laboratory': 'Elige un laboratorio de semillas.',
    'Map link must be a single address without spaces': 'El enlace al shape o KML no puede tener espacios.',
    'Sampled on is required': 'La fecha de muestreo es obligatoria.',
    'Analyzed on is required': 'La fecha de análisis es obligatoria.',
    'Parameter is required': 'Cada resultado necesita su parámetro.',
  };
  for (const [english, spanish] of Object.entries(cases)) assert.equal(errorMessage(400, { error: english }), spanish);
  // Existing mappings are unchanged.
  assert.equal(errorMessage(400, { error: 'Unit is required' }), 'La unidad es obligatoria.');
  assert.equal(errorMessage(400, { error: 'Role must be one of: renta, servicio' }), 'Elige renta o servicio.');
  assert.equal(errorMessage(400, { error: 'Unmapped english text' }), generic);
});
