// Pure combobox logic (S2): filtering, the match cap, the active option and what each key or pointer event does.
// Options are { value, label, detail? }; an item with its own `options` is a group whose header cannot be chosen.

import { fold } from './schema.mjs';

/** At most this many matches are rendered, so long lists (470 plots, 370 inputs) stay fast. */
export const optionLimit = 50;

/** Closed, showing the selected label. */
export const comboboxIdle = Object.freeze({ open: false, editing: false, term: '', active: -1 });

const flatten = options => options.flatMap(option => option.options ? option.options : [option]);

/** Label of the option with `value`, also inside groups; '' when nothing (or an unknown value) is selected. */
export function selectedLabel(options, value) {
  if (value === '' || value == null) return '';
  return flatten(options).find(option => option.value === value)?.label ?? '';
}

/**
 * The rendered rows for a term: group headers ({ kind: 'group' }) and options ({ kind: 'option' }) matching the label or
 * the secondary text, ignoring accents and case; a term matching a group's label keeps all of that group's options. `none` names the empty choice of an optional field, listed first while
 * nothing is typed. `total` counts every match; `truncated` says only the first `limit` are listed.
 */
export function comboboxItems(options, term, { none, limit = optionLimit } = {}) {
  const needle = fold(term).trim();
  const matches = option => !needle || fold(option.label).includes(needle) || fold(option.detail).includes(needle);
  const items = [];
  let total = 0;
  if (none !== undefined && !needle) items.push({ kind: 'option', value: '', label: none, none: true });
  const add = (option, kept) => {
    if (!kept && !matches(option)) return false;
    total += 1;
    if (total <= limit) items.push({ kind: 'option', value: option.value, label: option.label, detail: option.detail });
    return total <= limit;
  };
  for (const option of options) {
    if (!option.options) { add(option); continue; }
    const header = { kind: 'group', label: option.label };
    const at = items.length;
    const kept = Boolean(needle) && fold(option.label).includes(needle);
    const shown = option.options.map(child => add(child, kept)).filter(Boolean).length;
    if (shown) items.splice(at, 0, header);
  }
  return { items, total, truncated: total > limit };
}

/** Next option index from `active` by `step` (1 or -1), skipping headers and wrapping; -1 when there is no option. */
export function moveActive(items, active, step) {
  const indexes = items.flatMap((item, index) => item.kind === 'option' ? [index] : []);
  if (!indexes.length) return -1;
  const at = indexes.indexOf(active);
  if (at === -1) return step > 0 ? indexes[0] : indexes[indexes.length - 1];
  return indexes[(at + step + indexes.length) % indexes.length];
}

const itemsFor = (state, options, config) => comboboxItems(options, state.editing ? state.term : '', config).items;

/** The input text: what is being typed, else the selected label. */
export const displayText = (state, options, value) => state.editing ? state.term : selectedLabel(options, value);

/**
 * One event of the combobox: { type: 'input', term } | { type: 'key', key } | { type: 'open' } | { type: 'pick', index } |
 * { type: 'blur' }. `config` carries the selected `value` and the `none` label. Returns the next state, the value to
 * `choose` (undefined when nothing is chosen) and whether the key was `handled` (so the browser default is prevented).
 * Escape and blur restore the selected label: a half-typed text is never left as if it were selected.
 */
export function comboboxStep(state, event, options, config = {}) {
  const { value } = config;
  if (event.type === 'input') {
    const next = { open: true, editing: true, term: event.term, active: -1 };
    return { state: { ...next, active: moveActive(itemsFor(next, options, config), -1, 1) }, handled: false };
  }
  if (event.type === 'blur') return { state: comboboxIdle, handled: false };
  const items = itemsFor(state, options, config);
  // Opening starts on the selected option (the "none" option when nothing is selected), else the first or last one.
  const chosen = items.findIndex(item => item.kind === 'option' && item.value === (value ?? ''));
  const opened = step => ({ ...state, open: true, active: chosen !== -1 ? chosen : moveActive(items, -1, step) });
  if (event.type === 'open') return { state: opened(1), handled: false };
  if (event.type === 'pick') {
    const item = items[event.index];
    return item?.kind === 'option' ? { state: comboboxIdle, choose: item.value, handled: true } : { state, handled: false };
  }
  if (event.type !== 'key') return { state, handled: false };
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    const step = event.key === 'ArrowDown' ? 1 : -1;
    return { state: state.open ? { ...state, active: moveActive(items, state.active, step) } : opened(step), handled: true };
  }
  if (event.key === 'Enter') {
    if (!state.open) return { state, handled: false };
    const item = items[state.active];
    return item?.kind === 'option' ? { state: comboboxIdle, choose: item.value, handled: true } : { state, handled: true };
  }
  if (event.key === 'Escape') return state.open || state.editing ? { state: comboboxIdle, handled: true } : { state, handled: false };
  return { state, handled: false };
}
