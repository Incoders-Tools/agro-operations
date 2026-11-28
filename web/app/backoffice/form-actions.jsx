'use client';

import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { isDirty } from './form-actions.mjs';

// S3: a full form keeps its actions at hand. FormTopBar is the first child of the form (a ValidatedForm): a sticky bar
// with "Descartar" and the primary save action, so leaving or saving never needs a scroll to the bottom buttons, which
// stay as they were. "Descartar" asks first only when something would be lost; Escape does the same unless an open
// combobox, the help panel or the user menu takes it.

/**
 * { dirty, initial, rebase }: whether `value` differs from the value the form opened with (form-actions.mjs isDirty).
 * `rebase()` makes the current value the new starting point, after a reset.
 */
export function useDirtyState(value) {
  const [initial, setInitial] = useState(value);
  const rebase = useCallback(next => setInitial(next), []);
  return { dirty: isDirty(initial, value), initial, rebase };
}

// Escape belongs to whatever consumed it first: an open combobox (it prevents the default), the help panel, a menu.
const consumed = event => event.defaultPrevented || Boolean(event.target?.closest?.('.bo-help-panel, .bo-user-menu, [role=dialog], [role=alertdialog]'));

/**
 * The sticky top action bar of a full form. `onDiscard()` leaves the form (back to where the user came from) or resets
 * it; it is called at once when the form is clean and after "¿Descartar los cambios?" when `dirty`. Without
 * `canDiscard` (a clean form with nowhere to go back to) the button is disabled. `submitLabel` names the primary
 * action, a submit button of the form; `extra` adds actions between both.
 */
export function FormTopBar({ title, dirty, onDiscard, canDiscard = true, submitLabel, busy, disabled, extra, id }) {
  const generated = useId();
  const base = id ?? `bo-form${generated.replace(/[^a-zA-Z0-9-]/g, '')}`;
  const [confirming, setConfirming] = useState(false);
  const bar = useRef(null);
  const keep = useRef(null);
  const discardButton = useRef(null);
  const enabled = canDiscard || dirty;

  const discard = useCallback(() => {
    if (!enabled) return;
    if (dirty) setConfirming(true);
    else onDiscard?.();
  }, [dirty, enabled, onDiscard]);

  useEffect(() => { if (confirming) keep.current?.focus(); }, [confirming]);
  // Escape inside the form (or with nothing focused, when this is the only form bar) discards like the button.
  useEffect(() => {
    function onKey(event) {
      if (event.key !== 'Escape' || confirming || consumed(event)) return;
      const form = bar.current?.closest('form');
      const target = event.target;
      const inside = form && target instanceof Node && form.contains(target);
      const idle = (target === document.body || target === document.documentElement) && document.querySelectorAll('.bo-form-topbar').length === 1;
      if (!inside && !idle) return;
      event.preventDefault();
      discard();
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [confirming, discard]);

  function stay() {
    setConfirming(false);
    discardButton.current?.focus();
  }
  return <div ref={bar} className="bo-form-topbar" role="group" aria-label="Acciones del formulario">
    {title && <span className="bo-form-topbar-title" aria-hidden="true">{title}</span>}
    <div className="bo-form-topbar-actions">
      <button ref={discardButton} type="button" id={`${base}-discard`} className="bo-discard" disabled={!enabled || busy}
        title={enabled ? 'Descartar (Esc)' : undefined} onClick={discard}>Descartar</button>
      {extra}
      <button type="submit" id={`${base}-submit`} className="bo-primary" disabled={busy || disabled}>{submitLabel}</button>
    </div>
    {confirming && <div className="bo-confirm" role="alertdialog" aria-modal="false" aria-labelledby={`${base}-confirm-title`}
      aria-describedby={`${base}-confirm-text`} onKeyDown={event => { if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); stay(); } }}>
      <p id={`${base}-confirm-title`} className="bo-confirm-title">¿Descartar los cambios?</p>
      <p id={`${base}-confirm-text`}>Se pierde lo que cambiaste desde que abriste el formulario.</p>
      <div className="bo-confirm-actions">
        <button type="button" id={`${base}-confirm-discard`} className="bo-danger" onClick={() => { setConfirming(false); onDiscard?.(); }}>Descartar cambios</button>
        <button ref={keep} type="button" id={`${base}-confirm-keep`} onClick={stay}>Seguir editando</button>
      </div>
    </div>}
  </div>;
}
