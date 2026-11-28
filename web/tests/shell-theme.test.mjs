import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { businessDocKeyPattern, businessDocs, plainText, slugify, tableOfContents } from '../app/backoffice/business/document.mjs';
import { isDirty, sameFormValue } from '../app/backoffice/form-actions.mjs';
import { businessDocFile } from '../app/backoffice/help-file.mjs';
import { currentHelpKey, helpKeyFor } from '../app/backoffice/help.mjs';
import { parseMarkdown } from '../app/backoffice/markdown.mjs';
import {
  defaultTheme, lastIdentityKey, readTheme, resolveTheme, themeBootScript, themeLabel, themeRootId, themeStorageKey, themes, toggleTheme,
} from '../app/backoffice/theme.mjs';

// S2: theme preference

test('S2 light is the default theme and only light and dark are themes', () => {
  assert.deepEqual(themes, ['light', 'dark']);
  assert.equal(defaultTheme, 'light');
  assert.equal(readTheme('dark'), 'dark');
  assert.equal(readTheme(' Light '), 'light');
  for (const raw of [null, undefined, '', 'blue', '{"theme":"dark"}', 42]) assert.equal(readTheme(raw), null, String(raw));
});

test('S2 the stored server preference wins, then the local cache, then light', () => {
  assert.equal(resolveTheme({ stored: { theme: 'dark', revision: 3 }, local: 'light' }), 'dark');
  assert.equal(resolveTheme({ stored: { theme: 'light', revision: 1 }, local: 'dark' }), 'light');
  // Revision 0 means the server stored nothing: its "light" is the default, not a choice.
  assert.equal(resolveTheme({ stored: { theme: 'light', revision: 0 }, local: 'dark' }), 'dark');
  assert.equal(resolveTheme({ stored: null, local: 'dark' }), 'dark');
  assert.equal(resolveTheme({ stored: null, local: 'sepia' }), 'light');
  assert.equal(resolveTheme({}), 'light');
  assert.equal(resolveTheme({ stored: { theme: 'sepia', revision: 2 }, local: 'dark' }), 'dark');
});

test('S2 the quick toggle switches between light and dark; labels are Spanish', () => {
  assert.equal(toggleTheme('light'), 'dark');
  assert.equal(toggleTheme('dark'), 'light');
  assert.equal(toggleTheme('sepia'), 'dark');
  assert.equal(themeLabel('light'), 'Claro');
  assert.equal(themeLabel('dark'), 'Oscuro');
});

test('S2 the local cache is kept per email, case-insensitively, and the boot script reads it before the first paint', () => {
  assert.equal(themeStorageKey('Admin@Local.Test '), 'terra-ia.backoffice.theme:admin@local.test');
  assert.notEqual(themeStorageKey('a@x.test'), themeStorageKey('b@x.test'));
  assert.equal(themeStorageKey(''), null);
  assert.equal(themeStorageKey(undefined), null);
  assert.equal(lastIdentityKey, 'terra-ia.backoffice.identity');
  const script = themeBootScript();
  assert.ok(script.includes(JSON.stringify(lastIdentityKey)) && script.includes('terra-ia.backoffice.theme:'));
  // The script runs on its own: it sets data-theme="dark" on the backoffice root only for a cached dark choice.
  const run = (getItem, root) => new Function('localStorage', 'document', script)({ getItem },
    { getElementById: id => id === themeRootId ? root : null });
  const storage = new Map([[lastIdentityKey, 'admin@local.test'], [themeStorageKey('admin@local.test'), 'dark']]);
  const root = { dataset: {} };
  run(key => storage.get(key) ?? null, root);
  assert.equal(root.dataset.theme, 'dark');
  const light = { dataset: {} };
  run(() => null, light);
  assert.equal(light.dataset.theme, undefined);
  assert.doesNotThrow(() => run(() => { throw new Error('blocked'); }, {}));
});

// S3: dirty state of a full form

test('S3 a form is dirty only when a value differs from the one it opened with', () => {
  const initial = { name: 'Lote 1', area: '12', active: true, lines: [{ id: 'a', dose: '1' }], note: null };
  assert.equal(isDirty(initial, { ...initial }), false);
  assert.equal(isDirty(initial, { ...initial, lines: [{ id: 'a', dose: '1' }] }), false);
  assert.equal(isDirty(initial, { ...initial, name: 'Lote 2' }), true);
  assert.equal(isDirty(initial, { ...initial, active: false }), true);
  assert.equal(isDirty(initial, { ...initial, lines: [{ id: 'a', dose: '2' }] }), true);
  assert.equal(isDirty(initial, { ...initial, lines: [] }), true);
  // An empty value is the same whether it is '', null or missing.
  assert.equal(isDirty(initial, { ...initial, note: '' }), false);
  assert.equal(sameFormValue({ a: undefined }, {}), true);
  assert.equal(sameFormValue({ a: '' }, { a: '0' }), false);
  assert.equal(sameFormValue([1, 2], [2, 1]), false);
});

// S1: the top "Ayuda" button follows the most specific registered context

test('S1 the top help button opens the most specific registered context, else the section help', () => {
  assert.equal(currentHelpKey([], 'clientes'), 'clientes');
  assert.equal(currentHelpKey([{ key: 'clientes', level: 0 }], null), 'clientes');
  assert.equal(currentHelpKey([{ key: 'clientes', level: 0 }, { key: 'lotes', level: 1 }], 'clientes'), 'lotes');
  assert.equal(currentHelpKey([{ key: 'analisis-de-suelo', level: 2 }, { key: 'lotes', level: 1 }], 'clientes'), 'analisis-de-suelo');
  // Between contexts of the same level, the last registered wins.
  assert.equal(currentHelpKey([{ key: 'a', level: 1 }, { key: 'b', level: 1 }], null), 'b');
  assert.equal(currentHelpKey([], null), null);
  assert.equal(helpKeyFor('account'), 'acceso-local');
  assert.equal(helpKeyFor('operacion-del-negocio'), null);
});

// S4: business document

test('S4 business documents are known keys under docs/business, never another path', () => {
  const cwd = fileURLToPath(new URL('../', import.meta.url));
  assert.deepEqual(Object.keys(businessDocs), ['operacion-del-negocio']);
  assert.equal(businessDocs['operacion-del-negocio'], 'Operación del negocio');
  assert.equal(businessDocFile('operacion-del-negocio', cwd), path.join(cwd, '..', 'docs', 'business', 'operacion-del-negocio.md'));
  assert.ok(existsSync(businessDocFile('operacion-del-negocio', cwd)));
  for (const key of ['../secret', '..%2Fsecret', 'README', 'readme', 'a/b', '', 'a.md', 'Operacion', 'otro-documento', null]) {
    assert.equal(businessDocFile(key, cwd), null, String(key));
  }
  assert.ok(businessDocKeyPattern.test('operacion-del-negocio'));
  assert.ok(!businessDocKeyPattern.test('../x'));
});

test('S4 heading slugs drop accents and punctuation', () => {
  assert.equal(slugify('Órdenes de trabajo (OT)'), 'ordenes-de-trabajo-ot');
  assert.equal(slugify('  Análisis   de suelo — muestreo  '), 'analisis-de-suelo-muestreo');
  assert.equal(slugify('¿Qué es?'), 'que-es');
  assert.equal(slugify('***'), 'seccion');
  assert.equal(plainText([{ type: 'text', value: 'Uno ' }, { type: 'strong', children: [{ type: 'text', value: 'dos' }] },
    { type: 'code', value: ' tres' }, { type: 'link', href: 'x', children: [{ type: 'em', children: [{ type: 'text', value: ' cuatro' }] }] }]), 'Uno dos tres cuatro');
});

test('S4 the table of contents lists H2 and H3 with unique ids, skipping the title and deeper headings', () => {
  const blocks = parseMarkdown([
    '# Operación del negocio', '', '## Clientes', '', 'Texto.', '', '### Datos', '', '#### Detalle', '', '## Lotes', '', '### Datos', '',
    '## Clientes', '', '### **Campañas** y _cultivos_',
  ].join('\n'));
  assert.deepEqual(tableOfContents(blocks), [
    { level: 2, text: 'Clientes', id: 'clientes' },
    { level: 3, text: 'Datos', id: 'datos' },
    { level: 2, text: 'Lotes', id: 'lotes' },
    { level: 3, text: 'Datos', id: 'datos-2' },
    { level: 2, text: 'Clientes', id: 'clientes-2' },
    { level: 3, text: 'Campañas y cultivos', id: 'campanas-y-cultivos' },
  ]);
  assert.deepEqual(tableOfContents([]), []);
});

test('S2 the API theme validation message reads in Spanish', async () => {
  const { errorMessage } = await import('../app/backoffice/api.mjs');
  assert.equal(errorMessage(400, { error: 'Theme must be light or dark' }), 'Elige el tema claro u oscuro.');
});
