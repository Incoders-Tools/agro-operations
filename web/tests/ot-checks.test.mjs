import test from 'node:test';
import assert from 'node:assert/strict';
import { costTotals, doseState, lineCost } from '../app/backoffice/work-orders.mjs';

// S5: the "Chequeos" panel of the one-screen OT, like the workbook helper columns.

test('S5 the dose check is below, within or above the input range, and inclusive at both ends', () => {
  const input = { doseMin: 1.5, doseMax: 3, unit: 'L' };
  assert.equal(doseState('1.4999', input), 'below');
  assert.equal(doseState('1.5', input), 'within');
  assert.equal(doseState('2', input), 'within');
  assert.equal(doseState('3', input), 'within');
  assert.equal(doseState('3.0001', input), 'above');
});

test('S5 an input with only one bound checks that bound; with none the state is no range', () => {
  assert.equal(doseState('0.5', { doseMin: 1, doseMax: null }), 'below');
  assert.equal(doseState('99', { doseMin: 1, doseMax: null }), 'within');
  assert.equal(doseState('4', { doseMin: null, doseMax: 3 }), 'above');
  assert.equal(doseState('2', { doseMin: null, doseMax: null }), 'none');
  assert.equal(doseState('2', {}), 'none');
});

test('S5 without an input or a valid dose there is nothing to check', () => {
  const input = { doseMin: 1, doseMax: 3 };
  assert.equal(doseState('', input), null);
  assert.equal(doseState('abc', input), null);
  assert.equal(doseState('2', null), null);
  assert.equal(doseState('2', undefined), null);
});

test('S5 the estimated cost is dose × reference cost per ha and line total × reference cost, at 4 decimals', () => {
  // Line total 1.25 × 14.5 = 18.125; per ha 1.25 × 12.3456 = 15.432; total 18.125 × 12.3456 = 223.764.
  assert.deepEqual(lineCost('1.25', '14.5', { referenceCost: 12.3456 }), { perHectare: '15.432', total: '223.764' });
  assert.deepEqual(lineCost('2', '10', { referenceCost: '7.5' }), { perHectare: '15', total: '150' });
});

test('S5 the cost rounds half away from zero to 4 decimals, like the server line total', () => {
  // 0.5 × 0.0001 = 0.00005 → 0.0001; 0.3333 × 0.0001 = 0.00003333 → 0; line total 0.5 × 1 = 0.5.
  assert.deepEqual(lineCost('0.5', '1', { referenceCost: 0.0001 }), { perHectare: '0.0001', total: '0.0001' });
  assert.deepEqual(lineCost('0.3333', '1', { referenceCost: 0.0001 }), { perHectare: '0', total: '0' });
});

test('S5 a zero reference cost is a cost; a missing one gives null (shown as "Sin costo de referencia")', () => {
  assert.deepEqual(lineCost('2', '10', { referenceCost: 0 }), { perHectare: '0', total: '0' });
  assert.equal(lineCost('2', '10', { referenceCost: null }), null);
  assert.equal(lineCost('2', '10', { referenceCost: undefined }), null);
  assert.equal(lineCost('2', '10', {}), null);
  assert.equal(lineCost('2', '10', null), null);
});

test('S5 a priced input without a valid dose or worked area has no amount yet', () => {
  const input = { referenceCost: 5 };
  assert.deepEqual(lineCost('', '10', input), { perHectare: null, total: null });
  assert.deepEqual(lineCost('2', '0', input), { perHectare: '10', total: null });
  assert.deepEqual(lineCost('2', '', input), { perHectare: '10', total: null });
});

test('S5 the cost totals add the priced lines exactly and count the lines without a reference cost', () => {
  const costs = [{ perHectare: '0.1', total: '1.5' }, { perHectare: '0.2', total: '2.25' }, null, { perHectare: '3', total: null }];
  assert.deepEqual(costTotals(costs), { perHectare: '3.3', total: '3.75', missing: 1 });
});

test('S5 with no priced line the totals are null, and blank lines are not counted as missing', () => {
  assert.deepEqual(costTotals([null, null]), { perHectare: null, total: null, missing: 2 });
  assert.deepEqual(costTotals([]), { perHectare: null, total: null, missing: 0 });
  assert.deepEqual(costTotals([{ perHectare: null, total: null }]), { perHectare: null, total: null, missing: 0 });
});
