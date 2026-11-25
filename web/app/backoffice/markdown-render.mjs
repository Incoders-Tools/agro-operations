// React elements for the blocks of markdown.mjs (S10). Text always goes in as a string child, so React escapes it:
// nothing in a help file can add HTML to the page. Plain .mjs (createElement, no JSX) so node:test can render it.

import { createElement as h, Fragment } from 'react';
import { classifyLink } from './markdown.mjs';

// Repository files a help link may name, as the reader should know them.
const references = { 'docs/engineer-questions.md': 'preguntas abiertas de los ingenieros' };

function inline(nodes, options, prefix = 'i') {
  return nodes.map((node, index) => {
    const key = `${prefix}${index}`;
    if (node.type === 'text') return node.value;
    if (node.type === 'code') return h('code', { key }, node.value);
    if (node.type === 'strong') return h('strong', { key }, inline(node.children, options, key));
    if (node.type === 'em') return h('em', { key }, inline(node.children, options, key));
    const children = inline(node.children, options, key);
    const target = classifyLink(node.href);
    if (target.kind === 'help') {
      return h('button', { key, type: 'button', className: 'bo-help-link', onClick: () => options.onHelpLink?.(target.key) }, children);
    }
    if (target.kind === 'external') return h('a', { key, href: target.href, target: '_blank', rel: 'noopener' }, children);
    if (target.kind === 'reference') {
      return h('span', { key, className: 'bo-help-ref', title: target.path }, children, ` (${references[target.path] ?? target.path})`);
    }
    return h(Fragment, { key }, children);
  });
}

/**
 * The help body as React elements. The leading level-1 heading repeats the panel title and is left out; the other
 * headings move two levels down, under the panel's own h2. `onHelpLink(key)` opens another help file in the panel.
 */
export function renderMarkdown(blocks, options = {}) {
  const shown = blocks[0]?.type === 'heading' && blocks[0].level === 1 ? blocks.slice(1) : blocks;
  return h(Fragment, null, shown.map((block, index) => {
    const key = `b${index}`;
    if (block.type === 'heading') return h(`h${Math.min(block.level + 1, 6)}`, { key }, inline(block.inline, options, key));
    if (block.type === 'paragraph') return h('p', { key }, inline(block.inline, options, key));
    if (block.type === 'list') {
      return h(block.ordered ? 'ol' : 'ul', { key }, block.items.map((item, at) => h('li', { key: at }, inline(item, options, `${key}-${at}-`))));
    }
    return h('table', { key, className: 'bo-help-table' },
      h('thead', null, h('tr', null, block.head.map((cell, at) => h('th', { key: at }, inline(cell, options, `${key}h${at}-`))))),
      h('tbody', null, block.rows.map((row, at) => h('tr', { key: at }, row.map((cell, column) => h('td', { key: column },
        inline(cell, options, `${key}r${at}c${column}-`)))))));
  }));
}
