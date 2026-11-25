import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { renderToStaticMarkup } from 'react-dom/server';
import { errorMessage } from '../app/backoffice/api.mjs';
import { helpKeyFor, helpKeyPattern, helpScreens } from '../app/backoffice/help.mjs';
import { helpEnabled, helpFile } from '../app/backoffice/help-file.mjs';
import { classifyLink, parseFrontMatter, parseInline, parseMarkdown } from '../app/backoffice/markdown.mjs';
import { renderMarkdown } from '../app/backoffice/markdown-render.mjs';
import { entities, formFor, missingFirmNotice, payloadFor, recipeStages } from '../app/backoffice/schema.mjs';
import { staffDetail, staffQuery, staffRoles } from '../app/backoffice/staff.mjs';
import { fieldError, nounPhrase, validateAll } from '../app/backoffice/validation.mjs';
import { orderFromRecord, orderPayload, printResponsible, responsibleLabel, responsibleNote } from '../app/backoffice/work-orders.mjs';

const helpDir = fileURLToPath(new URL('../../docs/help/', import.meta.url));

// S10: Markdown help renderer

test('S10 front matter gives title, summary, video, reviewed and related keys; the body follows', () => {
  const { meta, body } = parseFrontMatter('---\nscreen: personal\ntitle: Personal\nsummary: Uno: dos.\nvideo:\nreviewed: 2026-11-25\nrelated: [a, b-c]\n---\n\n# Personal\n');
  assert.deepEqual(meta, { screen: 'personal', title: 'Personal', summary: 'Uno: dos.', video: '', reviewed: '2026-11-25', related: ['a', 'b-c'] });
  assert.equal(body.trim(), '# Personal');
  assert.deepEqual(parseFrontMatter('# Sin metadatos').meta, { related: [] });
  assert.equal(parseFrontMatter('\r\n---\r\ntitle: CRLF\r\n---\r\nTexto\r\n'.trimStart()).meta.title, 'CRLF');
});

test('S10 blocks: headings, paragraphs joined across lines, lists with continuation lines and tables', () => {
  const blocks = parseMarkdown([
    '# Título', '', '## Sección', 'Una línea', 'y otra.', '', '- uno', '  sigue', '- dos', '', '1. primero', '   sigue', '2. segundo', '',
    '| A | B |', '| --- | --- |', '| x | `y` |',
  ].join('\n'));
  assert.deepEqual(blocks.map(block => block.type), ['heading', 'heading', 'paragraph', 'list', 'list', 'table']);
  assert.deepEqual([blocks[0].level, blocks[1].level], [1, 2]);
  assert.deepEqual(blocks[2].inline, [{ type: 'text', value: 'Una línea y otra.' }]);
  assert.equal(blocks[3].ordered, false);
  assert.deepEqual(blocks[3].items, [[{ type: 'text', value: 'uno sigue' }], [{ type: 'text', value: 'dos' }]]);
  assert.equal(blocks[4].ordered, true);
  assert.deepEqual(blocks[4].items[0], [{ type: 'text', value: 'primero sigue' }]);
  assert.deepEqual(blocks[5].head, [[{ type: 'text', value: 'A' }], [{ type: 'text', value: 'B' }]]);
  assert.deepEqual(blocks[5].rows, [[[{ type: 'text', value: 'x' }], [{ type: 'code', value: 'y' }]]]);
});

test('S10 inline: bold, italic, inline code (never parsed inside) and links', () => {
  assert.deepEqual(parseInline('a **b** _c_ *d* `**e**` [f](g.md)'), [
    { type: 'text', value: 'a ' }, { type: 'strong', children: [{ type: 'text', value: 'b' }] }, { type: 'text', value: ' ' },
    { type: 'em', children: [{ type: 'text', value: 'c' }] }, { type: 'text', value: ' ' }, { type: 'em', children: [{ type: 'text', value: 'd' }] },
    { type: 'text', value: ' ' }, { type: 'code', value: '**e**' }, { type: 'text', value: ' ' },
    { type: 'link', href: 'g.md', children: [{ type: 'text', value: 'f' }] },
  ]);
  // snake_case words and lone asterisks stay text.
  assert.deepEqual(parseInline('next_work_order_number y 2 * 3'), [{ type: 'text', value: 'next_work_order_number y 2 * 3' }]);
});

test('S10 links: help files open in the panel, other repository files are references, web links open outside', () => {
  assert.deepEqual(classifyLink('clientes.md'), { kind: 'help', key: 'clientes' });
  assert.deepEqual(classifyLink('analisis-de-suelo.md#reglas'), { kind: 'help', key: 'analisis-de-suelo' });
  assert.deepEqual(classifyLink('../engineer-questions.md'), { kind: 'reference', path: 'docs/engineer-questions.md' });
  assert.deepEqual(classifyLink('README.md'), { kind: 'reference', path: 'docs/help/README.md' });
  assert.deepEqual(classifyLink('https://example.com/a'), { kind: 'external', href: 'https://example.com/a' });
  assert.deepEqual(classifyLink('javascript:alert(1)'), { kind: 'text' });
  assert.deepEqual(classifyLink('../../etc/passwd'), { kind: 'text' });
});

test('S10 rendering builds elements: help links are buttons, references text with the questions label, web links new tab', () => {
  const opened = [];
  const html = renderToStaticMarkup(renderMarkdown(parseMarkdown(
    '# Oculto\n\n## Reglas\n\nVer [clientes](clientes.md), Q7 ([preguntas](../engineer-questions.md)) y [sitio](https://example.com).\n\n| A |\n| --- |\n| **x** |'),
  { onHelpLink: key => opened.push(key) }));
  assert.ok(!html.includes('Oculto'), 'the leading h1 repeats the panel title');
  assert.match(html, /<h3>Reglas<\/h3>/);
  assert.match(html, /<button type="button" class="bo-help-link">clientes<\/button>/);
  assert.match(html, /<span class="bo-help-ref" title="docs\/engineer-questions.md">preguntas \(preguntas abiertas de los ingenieros\)<\/span>/);
  assert.match(html, /<a href="https:\/\/example.com" target="_blank" rel="noopener">sitio<\/a>/);
  assert.match(html, /<table class="bo-help-table"><thead><tr><th>A<\/th><\/tr><\/thead><tbody><tr><td><strong>x<\/strong><\/td><\/tr><\/tbody><\/table>/);
});

test('S10 HTML in the Markdown is shown as text, never passed through', () => {
  const html = renderToStaticMarkup(renderMarkdown(parseMarkdown('<script>alert(1)</script>\n\n- <b>x</b> `<i>`\n\n[a](<x>)')));
  assert.ok(!html.includes('<script>') && !html.includes('<b>') && !html.includes('<i>'));
  assert.match(html, /&lt;script&gt;alert\(1\)&lt;\/script&gt;/);
});

// S10: help keys and files

test('S10 every section, view and nested context maps to its help key', () => {
  const expected = {
    'work-orders': 'ordenes-de-trabajo', clients: 'clientes', 'seed-analyses': 'analisis-de-semillas', contractors: 'contratistas',
    providers: 'proveedores', 'quote-comparisons': 'comparacion-de-presupuestos', inputs: 'insumos', 'labor-types': 'labores', stages: 'etapas',
    crops: 'cultivos', campaigns: 'campanas', staff: 'personal', 'plot-campaign-areas': 'superficie-por-campana', recipes: 'recetas',
    'recipe-lines': 'recetas', 'import-records': 'procedencia',
  };
  for (const [section, key] of Object.entries(expected)) assert.equal(helpKeyFor(section), key, section);
  assert.equal(helpKeyFor('unknown'), null);
});

test('S10 every help key has its file with that screen key and title, and every screen file is known', () => {
  // README.md (format) and index.md (the list of screens) are not screens.
  const files = readdirSync(helpDir).filter(name => name.endsWith('.md') && !['README.md', 'index.md'].includes(name)).map(name => name.slice(0, -3)).sort();
  assert.deepEqual(Object.keys(helpScreens).sort(), files);
  for (const key of files) {
    const { meta } = parseFrontMatter(readFileSync(path.join(helpDir, `${key}.md`), 'utf8'));
    assert.equal(meta.screen, key);
    assert.equal(helpScreens[key], meta.title, key);
    for (const related of meta.related) assert.ok(helpScreens[related], `${key} relates to unknown ${related}`);
  }
});

test('S10 the help route reads only docs/help/<key>.md for a valid key, and only in the local preview', () => {
  const cwd = fileURLToPath(new URL('../', import.meta.url));
  assert.equal(helpFile('ordenes-de-trabajo', cwd), path.join(cwd, '..', 'docs', 'help', 'ordenes-de-trabajo.md'));
  assert.ok(existsSync(helpFile('ordenes-de-trabajo', cwd)));
  for (const key of ['../secret', '..%2Fsecret', 'README', 'a/b', '', 'a.md', 'Personal']) assert.equal(helpFile(key, cwd), null, key);
  assert.ok(helpKeyPattern.test('acceso-local'));
  assert.equal(helpEnabled({ NODE_ENV: 'development', LOCAL_PREVIEW: '1' }), true);
  assert.equal(helpEnabled({ NODE_ENV: 'production', LOCAL_PREVIEW: '1' }), false);
  assert.equal(helpEnabled({ NODE_ENV: 'development' }), false);
});

// S12: validation engine

test('S12 required messages derive from the label: choices are chosen, other values indicated', () => {
  assert.equal(fieldError({ label: 'Firma comercial', kind: 'choice', required: true }, { value: '' }), 'Elige una firma comercial.');
  assert.equal(fieldError({ label: 'Contratista', kind: 'choice', required: true }, { value: '' }), 'Elige un contratista.');
  assert.equal(fieldError({ label: 'Fecha', noun: 'la fecha de la orden', kind: 'date', required: true }, { value: '' }), 'Indica la fecha de la orden.');
  assert.equal(fieldError({ label: 'Nombre', kind: 'text', required: true }, { value: '   ' }), 'Indica el nombre.');
  assert.equal(fieldError({ label: 'Nombre', kind: 'text', required: false }, { value: '' }), null);
  assert.equal(fieldError({ label: 'Nombre', kind: 'text', required: true }, { value: 'Ana' }), null);
});

test('S12 noun phrases take the article of the label, plural and feminine included', () => {
  assert.equal(nounPhrase('Superficie (ha)'), 'la superficie');
  assert.equal(nounPhrase('Dosis por ha'), 'la dosis por ha');
  assert.equal(nounPhrase('Observaciones'), 'las observaciones');
  assert.equal(nounPhrase('Productores'), 'los productores');
  assert.equal(nounPhrase('Labor'), 'la labor');
  assert.equal(nounPhrase('CUIT'), 'el CUIT');
  assert.equal(nounPhrase('Ingeniero responsable'), 'el ingeniero responsable');
});

test('S12 number rules: type, minimum, maximum and decimals, with the agreement of the noun', () => {
  const dose = { label: 'Dosis por ha', noun: 'la dosis', kind: 'number', min: '0.0001', step: '0.0001', max: '99999999.9999' };
  assert.equal(fieldError(dose, { value: '', badInput: true }), 'La dosis debe ser un número.');
  assert.equal(fieldError(dose, { value: '0' }), 'La dosis debe ser mayor que 0.');
  assert.equal(fieldError(dose, { value: '100000000' }), 'La dosis no puede superar 99.999.999,9999.');
  assert.equal(fieldError(dose, { value: '1.00001' }), 'La dosis admite hasta 4 decimales.');
  assert.equal(fieldError(dose, { value: '2.5' }), null);
  assert.equal(fieldError(dose, { value: '' }), null, 'an empty optional number is valid');
  const worked = { label: 'ha a trabajar', noun: 'las ha a trabajar', kind: 'number', min: '0.0001', step: '0.0001' };
  assert.equal(fieldError(worked, { value: '-1' }), 'Las ha a trabajar deben ser mayores que 0.');
  assert.equal(fieldError({ label: 'Vigor (%)', kind: 'number', min: '0', max: '100', step: '0.01' }, { value: '-1' }), 'El vigor no puede ser menor que 0.');
  assert.equal(fieldError({ label: 'Vigor (%)', kind: 'number', min: '0', max: '100', step: '0.01' }, { value: '100.5' }), 'El vigor no puede superar 100.');
  assert.equal(fieldError({ label: 'Vigor (%)', kind: 'number', min: '0', max: '100', step: '0.01' }, { value: '9.555' }), 'El vigor admite hasta 2 decimales.');
  assert.equal(fieldError({ label: 'Orden', kind: 'number', step: '1' }, { value: '1.5' }), 'El orden debe ser un número entero.');
  assert.equal(fieldError({ label: 'Valor', kind: 'number', step: 'any' }, { value: '1.123456' }), null);
});

test('S12 maximum length and email format', () => {
  assert.equal(fieldError({ label: 'Nombre', kind: 'text', maxLength: 200 }, { value: 'x'.repeat(201) }), 'El nombre no puede superar los 200 caracteres.');
  assert.equal(fieldError({ label: 'Nombre', kind: 'text', maxLength: 200 }, { value: 'x'.repeat(200) }), null);
  assert.equal(fieldError({ label: 'Email', kind: 'email' }, { value: 'a@@b' }), 'El email debe tener un único @ con texto a ambos lados.');
  assert.equal(fieldError({ label: 'Email', kind: 'email' }, { value: 'a@b.com' }), null);
});

test('S12 a per-field message replaces the derived one for its rule only', () => {
  const rules = { label: 'Motivo de la cancelación', kind: 'text', required: true, maxLength: 5, messages: { required: 'Indica por qué se cancela la orden.' } };
  assert.equal(fieldError(rules, { value: '' }), 'Indica por qué se cancela la orden.');
  assert.equal(fieldError(rules, { value: 'demasiado' }), 'El motivo de la cancelación no puede superar los 5 caracteres.');
});

test('S12 validating a form gives each invalid field its message and the first invalid one in form order', () => {
  const result = validateAll([
    { id: 'a', rules: { label: 'Nombre', kind: 'text', required: true }, input: { value: 'ok' } },
    { id: 'b', rules: { label: 'Labor', kind: 'choice', required: true }, input: { value: '' } },
    { id: 'c', rules: { label: 'Fecha', kind: 'date', required: true }, input: { value: '' } },
  ]);
  assert.deepEqual(result, { errors: { b: 'Elige una labor.', c: 'Indica la fecha.' }, first: 'b', count: 2 });
  assert.deepEqual(validateAll([{ id: 'a', rules: { label: 'Nombre', kind: 'text' }, input: { value: '' } }]), { errors: {}, first: null, count: 0 });
});

// S3: staff

test('S3 staff roles, list query and form defaults: a new member is an active engineer', () => {
  assert.deepEqual(staffRoles.map(role => role.value), ['ingeniero', 'administrativo', 'otro']);
  assert.deepEqual(staffRoles.map(role => role.label), ['Ingeniero', 'Administrativo', 'Otro']);
  assert.equal(staffQuery({}), 'staff');
  assert.equal(staffQuery({ role: 'ingeniero', active: 'true' }), 'staff?role=ingeniero&active=true');
  assert.equal(staffQuery({ role: '', active: 'false' }), 'staff?active=false');
  const form = formFor('staff', null);
  assert.equal(form.role, 'ingeniero');
  assert.equal(form.active, true);
  assert.equal(formFor('staff', { fullName: 'A', role: 'otro', active: false }).active, false);
});

test('S3 staff payload trims text, sends empty optionals as null, the active flag and the revision on edit', () => {
  const form = { fullName: ' Persona Ejemplo ', title: 'Ingeniero agrónomo', role: 'ingeniero', licenseNumber: '', email: '', phone: ' 351 ', active: true, notes: '' };
  assert.deepEqual(payloadFor('staff', form), {
    fullName: 'Persona Ejemplo', title: 'Ingeniero agrónomo', role: 'ingeniero', licenseNumber: null, email: null, phone: '351', active: true, notes: null,
  });
  assert.equal(payloadFor('staff', { ...form, active: false }, { revision: 3 }).revision, 3);
  assert.equal(payloadFor('staff', { ...form, active: false }, { revision: 3 }).active, false);
  const limits = Object.fromEntries(entities.staff.fields.map(field => [field.key, field.maxLength]));
  assert.deepEqual([limits.fullName, limits.title, limits.licenseNumber], [200, 100, 50]);
  assert.deepEqual(entities.staff.fields.find(field => field.key === 'title').suggestions, ['Ingeniero agrónomo']);
});

test('S3 staff rows describe title, role, licence, phone and an inactive member', () => {
  assert.equal(staffDetail({ title: 'Ingeniero agrónomo', role: 'ingeniero', licenseNumber: '123', phone: '351', active: true }),
    'Ingeniero agrónomo · Ingeniero · Matrícula 123 · 351');
  assert.equal(staffDetail({ role: 'otro', active: false }), 'Otro · Inactivo');
});

// S3: OT responsible

const order = {
  firmId: 'f', issuedOn: '2026-11-25', laborTypeId: 'l', stageId: null, contractorId: 'c', campaignId: null, cropId: null, variety: null,
  observations: null, responsibleEngineer: 'Texto anterior', responsibleStaffId: null, plots: [], lines: [], revision: 2,
};
const engineers = [{ id: 's1', fullName: 'Persona Ejemplo', title: 'Ingeniero agrónomo', licenseNumber: '77', active: true }];

test('S3 the OT sends the chosen staff member and no free text; without one, an edit keeps the old text', () => {
  const chosen = { ...orderFromRecord(order, { engineers }), responsibleStaffId: 's1' };
  const sent = orderPayload(chosen, { revision: 2 });
  assert.equal(sent.responsibleStaffId, 's1');
  assert.equal(sent.responsibleEngineer, null);
  const kept = orderPayload(orderFromRecord(order, { engineers }), { revision: 2 });
  assert.equal(kept.responsibleStaffId, null);
  assert.equal(kept.responsibleEngineer, 'Texto anterior');
});

test('S3 an old free-text responsible is shown as a note; an inactive one is dropped so the user picks another', () => {
  assert.deepEqual(responsibleNote(order, engineers), { legacy: 'Texto anterior', inactive: null });
  const staffed = { ...order, responsibleStaffId: 's1', responsibleEngineer: 'Persona Ejemplo' };
  assert.deepEqual(responsibleNote(staffed, engineers), { legacy: null, inactive: null });
  assert.equal(orderFromRecord(staffed, { engineers }).responsibleStaffId, 's1');
  const gone = { ...order, responsibleStaffId: 's9', responsibleEngineer: 'Otra Persona' };
  assert.deepEqual(responsibleNote(gone, engineers), { legacy: null, inactive: 'Otra Persona' });
  assert.equal(orderFromRecord(gone, { engineers }).responsibleStaffId, '');
  assert.deepEqual(responsibleNote(null, engineers), { legacy: null, inactive: null });
});

test('S3 the detail and print show name, title and licence; the print footer adds the phone', () => {
  assert.equal(responsibleLabel(engineers[0], 'x'), 'Persona Ejemplo · Ingeniero agrónomo · Matrícula 77');
  assert.equal(responsibleLabel(null, 'Texto anterior'), 'Texto anterior');
  assert.equal(responsibleLabel(null, null), null);
  assert.deepEqual(printResponsible({ responsibleEngineer: 'Persona Ejemplo', responsible: { fullName: 'Persona Ejemplo', title: 'Ingeniero agrónomo', licenseNumber: '77', phone: '351 555' } }),
    { name: 'Persona Ejemplo', details: ['Ingeniero agrónomo', 'Matrícula 77'], call: 'Persona Ejemplo · 351 555' });
  assert.deepEqual(printResponsible({ responsibleEngineer: 'Texto anterior', responsible: null }), { name: 'Texto anterior', details: [], call: 'Texto anterior' });
  assert.deepEqual(printResponsible({ responsibleEngineer: null }), { name: null, details: [], call: null });
});

// Messages and limits aligned with the API (help review)

test('new API messages map to Spanish', () => {
  const cases = {
    'Responsible must be an active engineer': 'El ingeniero responsable debe ser una persona activa con rol Ingeniero. Revisa Personal.',
    'Area already recorded for this plot and campaign': 'Ese lote ya tiene superficie registrada para esa campaña. Edita el registro existente.',
    'Percentages must have at most two decimals': 'Los porcentajes admiten hasta 2 decimales.',
    'Thousand seed weight must be positive and fit numeric(10,4)': 'El peso de 1000 semillas debe ser mayor que 0 y tener hasta 4 decimales.',
    'Description must be at most 300 characters': 'La descripción no puede superar los 300 caracteres.',
    'Parameter must be at most 100 characters': 'El parámetro no puede superar los 100 caracteres.',
    'Unit must be at most 30 characters': 'La unidad no puede superar los 30 caracteres.',
    'Depth must be at most 30 characters': 'La profundidad no puede superar los 30 caracteres.',
    'Station must be at most 60 characters': 'La estación no puede superar los 60 caracteres.',
    'Full name must be at most 200 characters': 'El nombre completo no puede superar los 200 caracteres.',
    'Title must be at most 100 characters': 'El título no puede superar los 100 caracteres.',
    'License number must be at most 50 characters': 'La matrícula no puede superar los 50 caracteres.',
    'Full name is required': 'El nombre completo es obligatorio.',
    'Role must be one of: ingeniero, administrativo, otro': 'Elige el rol: Ingeniero, Administrativo u Otro.',
  };
  for (const [api, spanish] of Object.entries(cases)) assert.equal(errorMessage(400, { error: api }), spanish, api);
  // Unchanged neighbours: the contact role and the plot crop role still match first.
  assert.equal(errorMessage(400, { error: 'Role must be one of: ventas, tecnico, otro' }), 'Elige el rol del contacto: Ventas, Técnico u Otro.');
  assert.equal(errorMessage(400, { error: 'Role must be one of: renta, servicio' }), 'Elige renta o servicio.');
  assert.equal(errorMessage(400, { error: 'Code must be at most 40 characters' }), 'El código admite hasta 40 caracteres.');
  assert.equal(errorMessage(400, { error: 'Unknown must be at most 9 characters' }), 'Revisa los datos e intenta de nuevo.');
});

test('UI limits follow the API: website 200, labor description 300', () => {
  assert.equal(entities.contractors.fields.find(field => field.key === 'website').maxLength, 200);
  assert.equal(entities.providers.fields.find(field => field.key === 'website').maxLength, 200);
  assert.equal(entities['labor-types'].fields.find(field => field.key === 'description').maxLength, 300);
});

test('a new client without a commercial firm gets the notice to add one', () => {
  assert.equal(missingFirmNotice([]), 'Ya existe una firma con ese nombre; agrega una firma comercial para este cliente.');
  assert.equal(missingFirmNotice([{ id: 'f' }]), null);
});

test('recipe stages show capitalised labels and keep their values', () => {
  assert.deepEqual(recipeStages, [
    { value: 'presiembra', label: 'Presiembra' }, { value: 'preemergencia', label: 'Preemergencia' }, { value: 'postemergencia', label: 'Postemergencia' },
  ]);
});
