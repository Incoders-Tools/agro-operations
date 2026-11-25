'use client';

import { useEffect, useRef, useState } from 'react';
import { comboboxIdle, comboboxItems, comboboxStep, displayText, optionLimit } from './combobox.mjs';
import { useClearError, useFieldError } from './form-validation.jsx';

/**
 * One search field for choosing an entity (S2), the WAI-ARIA combobox with a listbox popup. `value` and `onChange` work
 * like a select: the chosen option's value, '' for none. Typing filters (see combobox.mjs); ArrowDown/ArrowUp move,
 * Enter or a click choose, Escape and blur restore the chosen label. `none` labels the empty choice of an optional field.
 * A required choice is checked by the form validation (S12) from aria-required and data-value; `label`, `noun` and
 * `messages` word its message, as in Field.
 */
export function Combobox({ id, value, onChange, options, placeholder, none, required, disabled, describedBy, label, noun, messages }) {
  const [state, setState] = useState(comboboxIdle);
  // Focus selects the whole label so typing replaces it; the mouseup of the focusing click would undo that selection.
  const focusing = useRef(false);
  const config = { value, none };
  const view = comboboxItems(options, state.editing ? state.term : '', { none });
  const listId = `${id}-listbox`;
  const optionId = index => `${id}-option-${index}`;
  const open = state.open && !disabled;
  const error = useFieldError(id);
  const clearError = useClearError();

  function run(event) {
    const result = comboboxStep(state, event, options, config);
    setState(result.state);
    if (result.choose !== undefined && result.choose !== value) { clearError(id); onChange(result.choose); }
    return result;
  }
  useEffect(() => {
    if (open && state.active >= 0) document.getElementById(`${id}-option-${state.active}`)?.scrollIntoView?.({ block: 'nearest' });
  }, [id, open, state.active]);

  // Group headers wrap the options that follow them; options before any header (the "none" option) stay top level.
  const rows = [];
  let group = null;
  view.items.forEach((item, index) => {
    if (item.kind === 'group') { group = { ...item, index, options: [] }; rows.push(group); }
    else (group ? group.options : rows).push({ ...item, index });
  });
  const option = item => <li key={item.index} id={optionId(item.index)} role="option" aria-selected={item.index === state.active}
    className={[item.index === state.active && 'bo-active', item.value === (value ?? '') && 'bo-chosen-option', item.none && 'bo-none'].filter(Boolean).join(' ') || undefined}
    onMouseDown={event => event.preventDefault()} onClick={() => run({ type: 'pick', index: item.index })}
    onMouseMove={() => { if (state.active !== item.index) setState(current => ({ ...current, active: item.index })); }}>
    <span>{item.label}</span>{item.detail && <small>{item.detail}</small>}</li>;
  const shown = view.items.filter(item => item.kind === 'option').length;

  return <div className="bo-combobox">
    <input id={id} type="text" role="combobox" autoComplete="off" spellCheck={false} aria-autocomplete="list" aria-expanded={open}
      aria-controls={listId} aria-activedescendant={open && state.active >= 0 ? optionId(state.active) : undefined}
      aria-required={required || undefined} aria-describedby={describedBy} aria-invalid={error ? true : undefined} disabled={disabled}
      placeholder={placeholder ?? none} data-value={value ?? ''} data-label={typeof label === 'string' ? label : undefined} data-noun={noun}
      data-messages={messages && JSON.stringify(messages)}
      value={displayText(state, options, value)} onChange={event => run({ type: 'input', term: event.target.value })}
      onKeyDown={event => { if (run({ type: 'key', key: event.key }).handled) event.preventDefault(); }}
      onClick={() => { if (!state.open) run({ type: 'open' }); }}
      onFocus={event => { event.target.select(); focusing.current = true; }}
      onMouseUp={event => { if (focusing.current) event.preventDefault(); focusing.current = false; }}
      onKeyUp={() => { focusing.current = false; }} onBlur={() => run({ type: 'blur' })} />
    <div className="bo-combobox-popup" hidden={!open} onMouseDown={event => event.preventDefault()}>
      <ul id={listId} role="listbox" aria-label="Resultados">{open && rows.map(row => row.kind === 'group'
        ? <li key={`g${row.index}`} role="group" aria-labelledby={optionId(row.index)}>
          <span id={optionId(row.index)} className="bo-combobox-group" role="presentation">{row.label}</span>
          <ul role="none">{row.options.map(option)}</ul></li>
        : option(row))}</ul>
      {open && !view.total && <p className="bo-combobox-note">Sin coincidencias para “{state.term.trim()}”.</p>}
      {open && view.truncated && <p className="bo-combobox-note">Se muestran {optionLimit} de {view.total}: refina la búsqueda.</p>}
    </div>
    <span className="bo-sr" role="status">{open ? `${view.total || 'Sin'} ${view.total === 1 ? 'resultado' : 'resultados'}${shown < view.total ? `, se muestran ${shown}` : ''}` : ''}</span>
  </div>;
}

