import test from 'node:test';
import assert from 'node:assert/strict';
import { searchPlots, plots } from '../src/demo.mjs';

test('public demo search finds client, field and plot without exposing private data', () => {
  assert.ok(plots.length >= 3);
  for (const term of ['cooperativa', 'cañada', 'lote 03']) {
    assert.ok(searchPlots(term).length > 0, term);
  }
  assert.equal(searchPlots('sin coincidencias').length, 0);
  assert.equal(searchPlots('').length, plots.length);
  assert.ok(plots.every(plot => plot.demo === true && plot.history.length && plot.order));
});
