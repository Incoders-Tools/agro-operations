import test from 'node:test';
import assert from 'node:assert/strict';
import { comboboxIdle, comboboxItems, comboboxStep, displayText, moveActive, optionLimit, selectedLabel } from '../app/backoffice/combobox.mjs';

const clients = [
  { value: 'c1', label: 'Agropecuaria Álamos', detail: 'CUIT 30-11111111-1' },
  { value: 'c2', label: 'Estancia La Ñata', detail: 'CUIT 30-22222222-2' },
  { value: 'c3', label: 'Ganadera del Sur' },
];
const labors = [
  { label: 'Aplicación', options: [{ value: 'l1', label: 'Pulverización terrestre (PULV-TE)' }, { value: 'l2', label: 'Pulverización aérea' }] },
  { label: 'Siembra e implantación', options: [{ value: 'l3', label: 'Siembra gruesa (SIEM-G)' }] },
];
const options = items => items.filter(item => item.kind === 'option');
const values = items => options(items).map(item => item.value);
const typed = (term, list = clients, config) => comboboxStep(comboboxIdle, { type: 'input', term }, list, config).state;

test('S2 typing filters by label or secondary text ignoring accents and case', () => {
  assert.deepEqual(values(comboboxItems(clients, 'alamos').items), ['c1']);
  assert.deepEqual(values(comboboxItems(clients, 'ÑATA').items), ['c2']);
  assert.deepEqual(values(comboboxItems(clients, '2222').items), ['c2']);
  assert.deepEqual(values(comboboxItems(clients, '  ').items), ['c1', 'c2', 'c3']);
  assert.deepEqual(values(comboboxItems(clients, 'zzz').items), []);
  assert.equal(comboboxItems(clients, 'zzz').total, 0);
});

test('S2 groups show their header only when one of their options matches; headers are not options', () => {
  const all = comboboxItems(labors, '').items;
  assert.deepEqual(all.map(item => item.kind), ['group', 'option', 'option', 'group', 'option']);
  assert.equal(all[0].label, 'Aplicación');
  const found = comboboxItems(labors, 'siem-g').items;
  assert.deepEqual(found.map(item => item.kind === 'group' ? `# ${item.label}` : item.value), ['# Siembra e implantación', 'l3']);
});

test('S2 typing a group name lists every option of that group (a category, or the client of the fields)', () => {
  const found = comboboxItems(labors, 'aplicacion').items;
  assert.deepEqual(found.map(item => item.kind === 'group' ? `# ${item.label}` : item.value), ['# Aplicación', 'l1', 'l2']);
});

test('S2 the "none" option of an optional field leads the full list and is not filtered in', () => {
  assert.deepEqual(values(comboboxItems(clients, '', { none: 'Sin indicar' }).items), ['', 'c1', 'c2', 'c3']);
  assert.deepEqual(values(comboboxItems(clients, 'sur', { none: 'Sin indicar' }).items), ['c3']);
});

test('S2 long lists render at most the match cap and report the total', () => {
  const plots = Array.from({ length: 470 }, (_, index) => ({ value: `p${index}`, label: `Lote ${index}` }));
  const view = comboboxItems(plots, '');
  assert.equal(optionLimit, 50);
  assert.equal(options(view.items).length, 50);
  assert.equal(view.total, 470);
  assert.equal(view.truncated, true);
  const narrow = comboboxItems(plots, 'lote 46');
  assert.deepEqual(values(narrow.items), ['p46', 'p460', 'p461', 'p462', 'p463', 'p464', 'p465', 'p466', 'p467', 'p468', 'p469']);
  assert.equal(narrow.truncated, false);
});

test('S2 ArrowDown and ArrowUp move between options, skip group headers and wrap', () => {
  const items = comboboxItems(labors, '').items;
  assert.equal(moveActive(items, -1, 1), 1);
  assert.equal(moveActive(items, 1, 1), 2);
  assert.equal(moveActive(items, 2, 1), 4);
  assert.equal(moveActive(items, 4, 1), 1);
  assert.equal(moveActive(items, 1, -1), 4);
  assert.equal(moveActive(items, -1, -1), 4);
  assert.equal(moveActive([], -1, 1), -1);
});

test('S2 ArrowDown opens a closed list on the selected option, or the first one', () => {
  const opened = comboboxStep(comboboxIdle, { type: 'key', key: 'ArrowDown' }, clients, { value: 'c2' });
  assert.equal(opened.handled, true);
  assert.equal(opened.state.open, true);
  assert.equal(opened.state.active, 1);
  assert.equal(comboboxStep(comboboxIdle, { type: 'key', key: 'ArrowDown' }, labors, { value: '' }).state.active, 1);
  assert.equal(comboboxStep(comboboxIdle, { type: 'key', key: 'ArrowUp' }, labors, { value: '' }).state.active, 4);
  const next = comboboxStep(opened.state, { type: 'key', key: 'ArrowDown' }, clients, { value: 'c2' });
  assert.equal(next.state.active, 2);
});

test('S2 typing opens the list with the first match active; Enter selects it and shows its label', () => {
  const state = typed('estancia');
  assert.deepEqual(state, { open: true, editing: true, term: 'estancia', active: 0 });
  const chosen = comboboxStep(state, { type: 'key', key: 'Enter' }, clients, { value: '' });
  assert.equal(chosen.choose, 'c2');
  assert.equal(chosen.handled, true);
  assert.deepEqual(chosen.state, comboboxIdle);
  assert.equal(displayText(chosen.state, clients, 'c2'), 'Estancia La Ñata');
});

test('S2 Enter with the list closed is left to the form; with no match it is swallowed and selects nothing', () => {
  const closed = comboboxStep(comboboxIdle, { type: 'key', key: 'Enter' }, clients, { value: 'c1' });
  assert.equal(closed.handled, false);
  assert.equal(closed.choose, undefined);
  const empty = comboboxStep(typed('zzz'), { type: 'key', key: 'Enter' }, clients, { value: 'c1' });
  assert.equal(empty.handled, true);
  assert.equal(empty.choose, undefined);
});

test('S2 a click picks that option; a click on a group header picks nothing', () => {
  const state = comboboxStep(comboboxIdle, { type: 'open' }, labors, { value: '' }).state;
  assert.equal(comboboxStep(state, { type: 'pick', index: 2 }, labors, { value: '' }).choose, 'l2');
  const header = comboboxStep(state, { type: 'pick', index: 0 }, labors, { value: '' });
  assert.equal(header.choose, undefined);
  assert.equal(header.state.open, true);
});

test('S2 the "none" option chooses the empty value', () => {
  const state = comboboxStep(comboboxIdle, { type: 'open' }, clients, { value: 'c1', none: 'Sin indicar' }).state;
  assert.equal(state.active, 1);
  assert.equal(comboboxStep(state, { type: 'pick', index: 0 }, clients, { value: 'c1', none: 'Sin indicar' }).choose, '');
});

test('S2 Escape and blur close the list and restore the selected label, never the half-typed text', () => {
  const half = typed('gana');
  assert.equal(displayText(half, clients, 'c1'), 'gana');
  const escaped = comboboxStep(half, { type: 'key', key: 'Escape' }, clients, { value: 'c1' });
  assert.equal(escaped.handled, true);
  assert.equal(escaped.choose, undefined);
  assert.equal(displayText(escaped.state, clients, 'c1'), 'Agropecuaria Álamos');
  const blurred = comboboxStep(half, { type: 'blur' }, clients, { value: '' });
  assert.equal(blurred.choose, undefined);
  assert.equal(displayText(blurred.state, clients, ''), '');
  assert.equal(comboboxStep(comboboxIdle, { type: 'key', key: 'Escape' }, clients, { value: '' }).handled, false);
});

test('S2 Tab is never handled, so focus moves on normally', () => {
  assert.equal(comboboxStep(typed('ala'), { type: 'key', key: 'Tab' }, clients, { value: '' }).handled, false);
});

test('S2 the selected label is found inside groups and is empty for an unknown value', () => {
  assert.equal(selectedLabel(labors, 'l3'), 'Siembra gruesa (SIEM-G)');
  assert.equal(selectedLabel(labors, 'missing'), '');
  assert.equal(selectedLabel(labors, ''), '');
});
