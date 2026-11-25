// Small Markdown parser for the help files (S10). It supports only what docs/help/README.md allows: front matter,
// headings, paragraphs, ordered and unordered lists, tables, bold, italic, inline code and links. Everything else,
// HTML included, stays plain text; markdown-render.mjs turns the result into React elements.

/**
 * `key: value` lines between two `---` lines at the start; `related: [a, b]` becomes a list. Without front matter
 * the whole text is the body.
 */
export function parseFrontMatter(source) {
  const text = String(source ?? '').replace(/\r\n?/g, '\n');
  const match = /^---\n([\s\S]*?)\n---\n?/.exec(text);
  const meta = { related: [] };
  if (!match) return { meta, body: text };
  for (const line of match[1].split('\n')) {
    const at = line.indexOf(':');
    if (at <= 0) continue;
    const key = line.slice(0, at).trim();
    const value = line.slice(at + 1).trim();
    meta[key] = key === 'related' ? value.replace(/^\[|\]$/g, '').split(',').map(item => item.trim()).filter(Boolean) : value;
  }
  return { meta, body: text.slice(match[0].length) };
}

const helpLink = /^([a-z0-9-]+)\.md(?:#[\w-]*)?$/;

/**
 * What a link opens: another help file in the panel ({ kind: 'help', key }), a web page in a new tab
 * ({ kind: 'external', href }), a repository file shown as text ({ kind: 'reference', path }, relative to docs/help/),
 * or nothing ({ kind: 'text' }) for any other scheme or a path outside docs/.
 */
export function classifyLink(href) {
  const target = String(href ?? '').trim();
  if (/^https?:\/\/[^\s]+$/i.test(target)) return { kind: 'external', href: target };
  const help = helpLink.exec(target);
  if (help) return { kind: 'help', key: help[1] };
  if (/^[a-z][a-z0-9+.-]*:/i.test(target) || target.startsWith('/')) return { kind: 'text' };
  const parts = ['docs', 'help'];
  for (const part of target.split('#')[0].split('/')) {
    if (part === '..') { if (parts.length <= 1) return { kind: 'text' }; parts.pop(); }
    else if (part && part !== '.') parts.push(part);
  }
  return parts.length > 1 && /\.md$/.test(parts[parts.length - 1]) ? { kind: 'reference', path: parts.join('/') } : { kind: 'text' };
}

// Inline markup, in priority order: code spans are never parsed inside; links, then bold, then italic. An emphasis
// marker must touch its text (`*a*`, not `2 * 3`) and `_` only counts at word edges, so snake_case stays text.
const inlinePatterns = [
  { type: 'code', pattern: /`([^`]+)`/ },
  { type: 'link', pattern: /\[([^\]]+)\]\(([^)\s]*)\)/ },
  { type: 'strong', pattern: /\*\*(?=\S)([\s\S]*?\S)\*\*/ },
  { type: 'em', pattern: /(?<![\w*])\*(?=[^\s*])([^*]*?[^\s*])\*(?![\w*])|(?<!\w)_(?=\S)([^_]*?\S)_(?!\w)/ },
];

/** Inline nodes: { type: 'text', value } | { type: 'code', value } | { type: 'strong' | 'em', children } | { type: 'link', href, children }. */
export function parseInline(text) {
  const source = String(text ?? '');
  let best = null;
  for (const { type, pattern } of inlinePatterns) {
    const match = pattern.exec(source);
    if (match && (!best || match.index < best.match.index)) best = { type, match };
  }
  if (!best) return source ? [{ type: 'text', value: source }] : [];
  const { type, match } = best;
  const before = source.slice(0, match.index);
  const after = source.slice(match.index + match[0].length);
  let node;
  if (type === 'code') node = { type, value: match[1] };
  else if (type === 'link') node = { type, href: match[2], children: parseInline(match[1]) };
  else node = { type, children: parseInline(match[1] ?? match[2]) };
  return [...(before ? [{ type: 'text', value: before }] : []), node, ...parseInline(after)];
}

const heading = /^(#{1,6})\s+(.*)$/;
const bullet = /^[-*]\s+(.*)$/;
const numbered = /^\d+[.)]\s+(.*)$/;
const tableRow = /^\|.*\|\s*$/;
const separator = /^\|(\s*:?-{3,}:?\s*\|)+\s*$/;
const cells = line => line.trim().replace(/^\||\|$/g, '').split('|').map(cell => parseInline(cell.trim()));

/**
 * Blocks: { type: 'heading', level, inline } | { type: 'paragraph', inline } | { type: 'list', ordered, items: inline[] } |
 * { type: 'table', head: inline[], rows: inline[][] }. Lines of a paragraph join with a space; an indented line continues
 * the list item above it.
 */
export function parseMarkdown(body) {
  const lines = String(body ?? '').replace(/\r\n?/g, '\n').split('\n');
  const blocks = [];
  let index = 0;
  while (index < lines.length) {
    const line = lines[index];
    const trimmed = line.trim();
    if (!trimmed) { index += 1; continue; }
    const title = heading.exec(trimmed);
    if (title) { blocks.push({ type: 'heading', level: title[1].length, inline: parseInline(title[2].trim()) }); index += 1; continue; }
    if (tableRow.test(trimmed) && separator.test(lines[index + 1]?.trim() ?? '')) {
      const head = cells(trimmed);
      const rows = [];
      index += 2;
      while (index < lines.length && tableRow.test(lines[index].trim())) { rows.push(cells(lines[index])); index += 1; }
      blocks.push({ type: 'table', head, rows });
      continue;
    }
    const marker = bullet.test(trimmed) ? bullet : numbered.test(trimmed) ? numbered : null;
    if (marker && !/^\s/.test(line)) {
      const items = [];
      while (index < lines.length) {
        const current = lines[index];
        const item = marker.exec(current.trim());
        if (item && !/^\s/.test(current)) items.push(item[1].trim());
        else if (current.trim() && /^\s/.test(current) && items.length) items[items.length - 1] += ` ${current.trim()}`;
        else break;
        index += 1;
      }
      blocks.push({ type: 'list', ordered: marker === numbered, items: items.map(parseInline) });
      continue;
    }
    const paragraph = [];
    while (index < lines.length) {
      const current = lines[index].trim();
      if (!current || heading.test(current) || (bullet.test(current) || numbered.test(current)) && !/^\s/.test(lines[index])
        || tableRow.test(current) && separator.test(lines[index + 1]?.trim() ?? '')) break;
      paragraph.push(current);
      index += 1;
    }
    blocks.push({ type: 'paragraph', inline: parseInline(paragraph.join(' ')) });
  }
  return blocks;
}
