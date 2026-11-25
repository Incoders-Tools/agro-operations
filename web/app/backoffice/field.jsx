'use client';

import { cloneElement, isValidElement, useEffect, useState } from 'react';
import { Combobox } from './combobox.jsx';
import { errorIdFor, useFieldError } from './form-validation.jsx';

const ids = (...lists) => [...new Set(lists.flatMap(list => String(list ?? '').split(' ')).filter(Boolean))].join(' ') || undefined;

/**
 * Every backoffice control sits inside one .bo-field wrapper so its label, control, hint and validation message are
 * separate grid rows: the grid gap keeps the 3px + 3px focus ring clear of the label (S1). A function child receives the
 * ids that describe the control (hint and message). A native control also gets aria-invalid and the data-label,
 * data-noun and data-messages the form validation words its message with (S12): `noun` is the label as a noun phrase
 * ("la fecha de la orden") and `messages` replaces the text of one rule ({ required: '…' }).
 */
export function Field({ id, label, optional, hint, check, noun, messages, children }) {
  const error = useFieldError(id);
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? errorIdFor(id) : undefined;
  const describedBy = ids(hintId, errorId);
  const raw = typeof children === 'function' ? children(describedBy) : children;
  const control = isValidElement(raw) && typeof raw.type === 'string' ? cloneElement(raw, {
    'aria-describedby': ids(raw.props['aria-describedby'], describedBy), 'aria-invalid': error ? true : undefined,
    'data-label': typeof label === 'string' ? label : undefined, 'data-noun': noun, 'data-messages': messages && JSON.stringify(messages),
  }) : raw;
  return <div className={check ? 'bo-field bo-check' : 'bo-field'}>
    {check && control}
    <label htmlFor={id}>{label}{optional && <span className="bo-optional">(opcional)</span>}</label>
    {!check && control}
    {hint && <small id={hintId} className="bo-hint">{hint}</small>}
    {error && <small id={errorId} className="bo-field-error">{error}</small>}
  </div>;
}

/** A labelled entity combobox (S2) in the shared Field, so the label, hint and message rows never overlap the control. */
export function ComboField({ id, label, optional, hint, required = !optional, noun, messages, ...props }) {
  return <Field id={id} label={label} optional={optional} hint={hint}>{describedBy =>
    <Combobox id={id} describedBy={describedBy} required={required} label={label} noun={noun} messages={messages} {...props} />}</Field>;
}

/** Focus the element with an id after the next render, such as the search field of a line just added (S3). */
export function useFocusAfterRender() {
  const [target, setTarget] = useState(null);
  useEffect(() => {
    if (!target) return;
    document.getElementById(target)?.focus();
    setTarget(null);
  }, [target]);
  return setTarget;
}

// Options for a native select of a small fixed enumeration; an item with its own `options` renders as an <optgroup>.
export const optionNodes = options => options.map(option => option.options
  ? <optgroup key={option.label} label={option.label}>{optionNodes(option.options)}</optgroup>
  : <option key={option.value} value={option.value}>{option.label}</option>);

// Renders one schema field (see schema.mjs). `options` resolves named option sets such as 'crops': those are entities and
// render as a combobox; a fixed list of options (tenure, role, category) stays a native select.
// `suggestions` lists values offered while typing (a datalist) without restricting the field.
export function SchemaControl({ field, id, value, onChange, optionSets = {} }) {
  const common = { id, required: !field.optional && field.type !== 'checkbox' };
  const words = { noun: field.noun, messages: field.messages };
  if (field.type === 'checkbox') {
    return <Field id={id} label={field.label} hint={field.hint} check {...words}>{hintId =>
      <input {...common} type="checkbox" checked={value} aria-describedby={hintId} onChange={event => onChange(event.target.checked)} />}</Field>;
  }
  const listId = field.suggestions ? `${id}-suggestions` : undefined;
  const control = <Field id={id} label={field.label} optional={field.optional} hint={field.hint} {...words}>{hintId => {
    const props = { ...common, 'aria-describedby': hintId, value, onChange: event => onChange(event.target.value) };
    if (field.type === 'select' && typeof field.options === 'string') {
      return <Combobox id={id} value={value} onChange={onChange} options={optionSets[field.options] ?? []} required={common.required}
        describedBy={hintId} placeholder={field.placeholder} none={field.optional ? field.placeholder ?? 'Sin indicar' : undefined}
        label={field.label} {...words} />;
    }
    if (field.type === 'select') {
      return <select {...props}>
        {(field.placeholder || field.optional) && <option value="">{field.placeholder ?? 'Sin indicar'}</option>}
        {optionNodes(field.options)}
      </select>;
    }
    if (field.type === 'textarea') return <textarea {...props} rows={3} maxLength={field.maxLength ?? 2000} />;
    const numeric = field.type === 'number' || field.type === 'integer';
    return <input {...props} type={numeric ? 'number' : field.type} inputMode={field.inputMode} list={listId}
      min={field.min} max={field.max} step={field.step} maxLength={numeric ? undefined : field.maxLength ?? 200} />;
  }}</Field>;
  return listId ? <>{control}<datalist id={listId}>{field.suggestions.map(item => <option key={item} value={item} />)}</datalist></> : control;
}
