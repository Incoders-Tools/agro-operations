'use client';

import { useEffect, useState } from 'react';
import { Combobox } from './combobox.jsx';

// Every backoffice control sits inside one .bo-field wrapper so its label, control and hint are separate grid rows:
// the grid gap keeps the 3px + 3px focus ring clear of the label (S1).
export function Field({ id, label, optional, hint, check, children }) {
  const hintId = hint ? `${id}-hint` : undefined;
  const control = typeof children === 'function' ? children(hintId) : children;
  return <div className={check ? 'bo-field bo-check' : 'bo-field'}>
    {check && control}
    <label htmlFor={id}>{label}{optional && <span className="bo-optional">(opcional)</span>}</label>
    {!check && control}
    {hint && <small id={hintId} className="bo-hint">{hint}</small>}
  </div>;
}

/** A labelled entity combobox (S2) in the shared Field, so the label and hint rows never overlap the control. */
export function ComboField({ id, label, optional, hint, required = !optional, ...props }) {
  return <Field id={id} label={label} optional={optional} hint={hint}>{hintId =>
    <Combobox id={id} describedBy={hintId} required={required} {...props} />}</Field>;
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
export function SchemaControl({ field, id, value, onChange, optionSets = {} }) {
  const common = { id, required: !field.optional && field.type !== 'checkbox' };
  if (field.type === 'checkbox') {
    return <Field id={id} label={field.label} hint={field.hint} check>{hintId =>
      <input {...common} type="checkbox" checked={value} aria-describedby={hintId} onChange={event => onChange(event.target.checked)} />}</Field>;
  }
  return <Field id={id} label={field.label} optional={field.optional} hint={field.hint}>{hintId => {
    const props = { ...common, 'aria-describedby': hintId, value, onChange: event => onChange(event.target.value) };
    if (field.type === 'select' && typeof field.options === 'string') {
      return <Combobox id={id} value={value} onChange={onChange} options={optionSets[field.options] ?? []} required={common.required}
        describedBy={hintId} placeholder={field.placeholder} none={field.optional ? field.placeholder ?? 'Sin indicar' : undefined} />;
    }
    if (field.type === 'select') {
      return <select {...props}>
        {(field.placeholder || field.optional) && <option value="">{field.placeholder ?? 'Sin indicar'}</option>}
        {optionNodes(field.options)}
      </select>;
    }
    if (field.type === 'textarea') return <textarea {...props} rows={3} maxLength={field.maxLength ?? 2000} />;
    const numeric = field.type === 'number' || field.type === 'integer';
    return <input {...props} type={numeric ? 'number' : field.type} inputMode={field.inputMode}
      min={field.min} max={field.max} step={field.step} maxLength={numeric ? undefined : field.maxLength ?? 200} />;
  }}</Field>;
}
