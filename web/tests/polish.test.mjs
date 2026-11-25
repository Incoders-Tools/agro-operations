import test from 'node:test';
import assert from 'node:assert/strict';
import { brandName, brandTagline, brandTitle } from '../app/backoffice/brand.mjs';
import { groupOpen, menuReducer, menuStorageKey, readMenuState } from '../app/backoffice/navigation.mjs';
import { entities, formFor, payloadFor } from '../app/backoffice/schema.mjs';
import { displayUnit } from '../app/backoffice/units.mjs';

test('litres display as L in any case and spelling; other units are unchanged', () => {
  for (const unit of ['l', 'L', 'lt', 'LT', 'lts', 'Lts', 'litro', 'Litros', 'LITROS', ' l ']) assert.equal(displayUnit(unit), 'L', unit);
  for (const unit of ['kg', 'cc', 'g', 'ml', 'ppm', '%', 'dosis', 'bolsa']) assert.equal(displayUnit(unit), unit, unit);
});

test('a composite unit maps only its litre part', () => {
  assert.equal(displayUnit('l/ha'), 'L/ha');
  assert.equal(displayUnit('Lts/ha'), 'L/ha');
  assert.equal(displayUnit('litros / ha'), 'L/ha');
  assert.equal(displayUnit('kg/ha'), 'kg/ha');
  assert.equal(displayUnit('g/l'), 'g/L');
  assert.equal(displayUnit('lote'), 'lote');
});

test('an empty unit displays as an empty string', () => {
  assert.equal(displayUnit(null), '');
  assert.equal(displayUnit(undefined), '');
  assert.equal(displayUnit(''), '');
});

test('the brand comes from one constant used by the page titles', () => {
  assert.equal(brandName, 'Terra IA');
  assert.equal(brandTagline, 'Operaciones agronómicas');
  assert.equal(brandTitle(), 'Terra IA · Operaciones');
  assert.equal(brandTitle('Orden de trabajo'), 'Terra IA · Orden de trabajo');
});

test('the menu is an accordion: only the active section group is open by default', () => {
  const state = readMenuState(null);
  assert.deepEqual(state, {});
  assert.equal(groupOpen(state, 'operation', 'operation'), true);
  assert.equal(groupOpen(state, 'catalogs', 'operation'), false);
});

test('opening a group closes the others, so the menu always fits without a scrollbar', () => {
  const catalogs = menuReducer({}, { type: 'toggle', group: 'catalogs', active: 'operation' });
  assert.deepEqual(catalogs, { open: 'catalogs' });
  assert.equal(groupOpen(catalogs, 'catalogs', 'operation'), true);
  assert.equal(groupOpen(catalogs, 'operation', 'operation'), false);
  const audit = menuReducer(catalogs, { type: 'toggle', group: 'audit', active: 'operation' });
  assert.equal(groupOpen(audit, 'catalogs', 'operation'), false);
  assert.equal(groupOpen(audit, 'audit', 'operation'), true);
  // Closing the open group goes back to the active section's group; the active group cannot close itself.
  assert.deepEqual(menuReducer(audit, { type: 'toggle', group: 'audit', active: 'operation' }), { open: 'operation' });
  const active = { open: 'operation' };
  assert.equal(menuReducer(active, { type: 'toggle', group: 'operation', active: 'operation' }), active);
});

test('navigating opens only the group of the new section', () => {
  const state = menuReducer({ open: 'catalogs' }, { type: 'navigate', group: 'administration' });
  assert.deepEqual(state, { open: 'administration' });
  assert.equal(groupOpen(state, 'catalogs', 'administration'), false);
});

test('the stored menu state round-trips and ignores invalid data', () => {
  assert.equal(menuStorageKey, 'terra-ia.backoffice.menu');
  const stored = JSON.stringify(menuReducer({}, { type: 'toggle', group: 'planning', active: 'operation' }));
  assert.deepEqual(readMenuState(stored), { open: 'planning' });
  assert.deepEqual(menuReducer({ open: 'audit' }, { type: 'load', state: readMenuState(stored) }), { open: 'planning' });
  for (const raw of ['', 'not json', '[1,2]', '"text"', '42', 'null', '{"open":3}', '{"catalogs":true}']) assert.deepEqual(readMenuState(raw), {}, raw);
});

test('the inputs list shows litres as L while the payload keeps the stored unit', () => {
  const row = { commercialName: 'Producto ficticio', activeIngredient: null, category: null, unit: 'l', doseMin: 1, doseMax: 2, hollowCone: false };
  assert.equal(entities.inputs.detail(row), '1–2 L/ha');
  assert.equal(entities.inputs.detail({ ...row, doseMin: null, doseMax: null }), 'L');
  assert.equal(payloadFor('inputs', { ...formFor('inputs'), commercialName: 'P', unit: 'l' }).unit, 'l');
});
