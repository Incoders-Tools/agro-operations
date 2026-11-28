// Business documentation (S4): which docs/business/<key>.md files the backoffice shows, and the table of contents built
// from their H2 and H3 headings. The route (help-file.mjs businessDocFile) accepts only these keys.

/** A document key is a file name without extension, like a help key, so it can never leave docs/business. */
export const businessDocKeyPattern = /^[a-z0-9-]+$/;

/** Every document the backoffice shows, with its menu title. README.md describes the folder and is not one. */
export const businessDocs = { 'operacion-del-negocio': 'Operación del negocio' };

/** The text of inline nodes (markdown.mjs parseInline), without markup. */
export const plainText = nodes => nodes.map(node => node.type === 'text' || node.type === 'code' ? node.value : plainText(node.children ?? [])).join('');

/** An anchor id from a heading: lowercase ASCII words joined by hyphens. */
export function slugify(text) {
  const slug = String(text ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase()
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
  return slug || 'seccion';
}

/**
 * [{ level, text, id }] for every H2 and H3 block, in order. A repeated heading gets -2, -3… so every id is unique;
 * the document title (H1) and deeper headings are left out.
 */
export function tableOfContents(blocks) {
  const used = new Set();
  return blocks.filter(block => block.type === 'heading' && (block.level === 2 || block.level === 3)).map(block => {
    const text = plainText(block.inline).trim();
    const base = slugify(text);
    let id = base;
    for (let count = 2; used.has(id); count += 1) id = `${base}-${count}`;
    used.add(id);
    return { level: block.level, text, id };
  });
}
