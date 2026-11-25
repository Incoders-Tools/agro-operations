'use client';

import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { validateAll } from './validation.mjs';

// S12: every backoffice form is a ValidatedForm. The browser's own validation is off (noValidate), so no "Please fill out
// this field" bubble appears; on submit each control is checked with the rules in its attributes (required or
// aria-required, type, min, max, step, maxLength) and its Spanish message shows under it (see Field in field.jsx).

const FormErrors = createContext({ errors: {}, clear: () => {} });

/** The message shown under the control `id`, if any. */
export const useFieldError = id => useContext(FormErrors).errors[id];
/** `clear(id)` removes the message of a control whose value changed outside a DOM change event (a combobox pick). */
export const useClearError = () => useContext(FormErrors).clear;
export const errorIdFor = id => `${id}-error`;

function kindOf(element) {
  if (element.type === 'checkbox') return 'checkbox';
  if (element.tagName === 'SELECT' || element.getAttribute('role') === 'combobox') return 'choice';
  if (element.type === 'number' || element.type === 'email' || element.type === 'date') return element.type;
  return 'text';
}

function messagesOf(element) {
  try { return element.dataset.messages ? JSON.parse(element.dataset.messages) : undefined; } catch { return undefined; }
}

const attribute = (element, name) => element.getAttribute(name) ?? undefined;

// Field and Combobox write data-label, data-noun and data-messages; a combobox keeps its chosen value in data-value.
function entryOf(element) {
  const kind = kindOf(element);
  return {
    id: element.id,
    rules: {
      label: element.dataset.label ?? element.labels?.[0]?.textContent ?? '', noun: element.dataset.noun, kind, messages: messagesOf(element),
      required: element.required || element.getAttribute('aria-required') === 'true',
      min: attribute(element, 'min'), max: attribute(element, 'max'), step: attribute(element, 'step'),
      maxLength: element.maxLength > 0 ? element.maxLength : undefined,
    },
    input: {
      value: element.getAttribute('role') === 'combobox' ? element.dataset.value ?? '' : element.value,
      checked: element.checked, badInput: Boolean(element.validity?.badInput),
    },
  };
}

const checked = element => element.id && !element.disabled && ['INPUT', 'SELECT', 'TEXTAREA'].includes(element.tagName)
  && !['hidden', 'submit', 'button', 'reset'].includes(element.type) && !element.closest('[inert]');

/**
 * A form with the backoffice's own validation. `onSubmit(event)` runs only when every control passes; otherwise each
 * invalid control gets its message, a short summary shows at the top and the first invalid control takes the focus.
 * A control's message clears as soon as its value changes.
 */
export function ValidatedForm({ onSubmit, className, children }) {
  const [errors, setErrors] = useState({});
  const clear = useCallback(id => setErrors(current => {
    if (!id || !(id in current)) return current;
    const { [id]: _removed, ...rest } = current;
    return rest;
  }), []);
  function submit(event) {
    event.preventDefault();
    const result = validateAll([...event.currentTarget.elements].filter(checked).map(entryOf));
    setErrors(result.errors);
    if (result.first) { document.getElementById(result.first)?.focus(); return; }
    onSubmit(event);
  }
  const value = useMemo(() => ({ errors, clear }), [errors, clear]);
  const count = Object.keys(errors).length;
  return <FormErrors.Provider value={value}>
    <form noValidate className={className} onSubmit={submit} onChange={event => clear(event.target.id)}>
      {count > 0 && <p className="bo-error bo-form-summary" role="alert">
        {count === 1 ? 'Revisa el campo marcado antes de guardar.' : `Revisa los ${count} campos marcados antes de guardar.`}</p>}
      {children}
    </form>
  </FormErrors.Provider>;
}
