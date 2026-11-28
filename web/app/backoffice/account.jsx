'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { api, errorMessage, send } from './api.mjs';
import { Field } from './field.jsx';
import { usePageHelp } from './help-panel.jsx';
import {
  applyTheme, lastIdentityKey, readTheme, resolveTheme, themeLabel, themeStorageKey, toggleTheme,
} from './theme.mjs';

// The logged-in user's own settings (S2): the top-right user menu, the quick theme toggle and the "Mi cuenta" section.
// Accounts are the temporary local administrator until invitation-based accounts are wired (L3).

export const accountType = 'Administrador local temporal';
const staleMessage = errorMessage(409, { error: 'Stale revision' });

function readLocal(key) {
  try { return key ? window.localStorage.getItem(key) : null; } catch { return null; }
}
function writeLocal(key, value) {
  try { if (key) window.localStorage.setItem(key, value); } catch { /* storage blocked */ }
}

/**
 * The identity's theme: the cached choice applies at once, then the value stored in the API wins (theme.mjs
 * resolveTheme). `choose(theme)` applies and caches it, then saves it with the current revision; a stale revision
 * reloads the stored preference and says so.
 */
export function useThemePreference(identity) {
  const email = identity?.email ?? null;
  const csrfToken = identity?.csrfToken;
  const storageKey = themeStorageKey(email);
  const [state, setState] = useState({ theme: null, revision: 0, status: 'idle', saving: false, error: '', notice: '' });
  const show = useCallback(theme => { applyTheme(theme); writeLocal(storageKey, theme); }, [storageKey]);

  useEffect(() => {
    if (!storageKey) return undefined;
    let active = true;
    writeLocal(lastIdentityKey, email.trim().toLowerCase());
    const local = readTheme(readLocal(storageKey));
    applyTheme(resolveTheme({ local }));
    setState(current => ({ ...current, theme: resolveTheme({ local }), status: 'loading', error: '', notice: '' }));
    api('me/preferences').then(stored => {
      if (!active) return;
      const theme = resolveTheme({ stored, local });
      show(theme);
      setState(current => ({ ...current, theme, revision: stored.revision ?? 0, status: 'ready' }));
    }).catch(cause => { if (active) setState(current => ({ ...current, status: 'ready', error: cause.message })); });
    return () => { active = false; };
  }, [email, storageKey, show]);

  const choose = useCallback(async theme => {
    const previous = state.theme;
    show(theme);
    setState(current => ({ ...current, theme, saving: true, error: '', notice: '' }));
    try {
      const saved = await send('me/preferences', 'PUT', { theme, revision: state.revision }, csrfToken);
      setState(current => ({ ...current, theme: readTheme(saved.theme) ?? theme, revision: saved.revision, saving: false, notice: `Tema ${themeLabel(theme).toLowerCase()} guardado.` }));
    } catch (cause) {
      if (cause.message === staleMessage) {
        const stored = await api('me/preferences').catch(() => null);
        const current = stored ? resolveTheme({ stored, local: previous }) : previous;
        show(current);
        setState(value => ({ ...value, theme: current, revision: stored?.revision ?? value.revision, saving: false,
          error: 'Tus preferencias cambiaron en otra sesión. Se aplicó lo último guardado; vuelve a elegir el tema si quieres cambiarlo.' }));
        return;
      }
      show(previous);
      setState(value => ({ ...value, theme: previous, saving: false, error: `No se pudo guardar el tema. ${cause.message}` }));
    }
  }, [csrfToken, show, state.revision, state.theme]);

  return { ...state, theme: state.theme ?? 'light', choose };
}

/** Up to two capitals from the email's name part: "ana.perez@…" is AP, "admin@…" is AD. */
export function initialsOf(email) {
  const name = String(email ?? '').split('@')[0];
  const words = name.split(/[._-]+/).filter(Boolean);
  const letters = words.length > 1 ? words[0][0] + words[1][0] : name.slice(0, 2);
  return (letters || '?').toUpperCase();
}

const moon = <svg viewBox="0 0 20 20" width="18" height="18" aria-hidden="true" focusable="false">
  <path d="M15.5 12.6A6.5 6.5 0 0 1 7.4 4.5a6.5 6.5 0 1 0 8.1 8.1Z" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinejoin="round" /></svg>;
const sun = <svg viewBox="0 0 20 20" width="18" height="18" aria-hidden="true" focusable="false">
  <circle cx="10" cy="10" r="3.4" fill="none" stroke="currentColor" strokeWidth="1.6" />
  <path d="M10 2.2v2M10 15.8v2M2.2 10h2M15.8 10h2M4.5 4.5l1.4 1.4M14.1 14.1l1.4 1.4M4.5 15.5l1.4-1.4M14.1 5.9l1.4-1.4" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" /></svg>;

/** The top bar's direct light/dark switch: pressed while the dark theme is on. */
export function ThemeToggle({ preference }) {
  const dark = preference.theme === 'dark';
  return <button type="button" id="bo-theme-toggle" className="bo-icon-button" aria-pressed={dark} aria-label="Tema oscuro"
    title={dark ? 'Cambiar a tema claro' : 'Cambiar a tema oscuro'} disabled={preference.saving}
    onClick={() => preference.choose(toggleTheme(preference.theme))}>{dark ? sun : moon}</button>;
}

/**
 * The identity button (initials and email) and its menu: the quick theme toggle, "Mi cuenta" and "Cerrar sesión".
 * Escape, a click outside or a choice close it; the focus returns to the button.
 */
export function UserMenu({ identity, preference, onAccount, onLogout, busy }) {
  const [open, setOpen] = useState(false);
  const root = useRef(null);
  const button = useRef(null);
  const first = useRef(null);
  const close = useCallback((focus = true) => { setOpen(false); if (focus) button.current?.focus(); }, []);
  useEffect(() => { if (open) first.current?.focus(); }, [open]);
  useEffect(() => {
    if (!open) return undefined;
    const outside = event => { if (!root.current?.contains(event.target)) close(false); };
    document.addEventListener('pointerdown', outside);
    return () => document.removeEventListener('pointerdown', outside);
  }, [open, close]);
  const choose = action => () => { close(); action(); };
  const next = toggleTheme(preference.theme);
  return <div ref={root} className="bo-user" onKeyDown={event => { if (open && event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); close(); } }}>
    <button ref={button} type="button" id="bo-user-menu-button" className="bo-user-button" aria-expanded={open} aria-controls="bo-user-menu"
      aria-label={`Cuenta de ${identity.email}`} onClick={() => setOpen(value => !value)}>
      <span className="bo-avatar" aria-hidden="true">{initialsOf(identity.email)}</span>
      <span className="bo-user-email">{identity.email}</span>
      <span className="bo-chevron" aria-hidden="true" /></button>
    <div id="bo-user-menu" className="bo-user-menu" hidden={!open}>
      <p className="bo-user-menu-head"><strong>{identity.email}</strong><small>{accountType}</small></p>
      <ul>
        <li><button ref={first} type="button" id="bo-menu-theme" disabled={preference.saving}
          onClick={() => preference.choose(next)}>
          <span>Tema: {themeLabel(preference.theme)}</span><small>Cambiar a {themeLabel(next).toLowerCase()}</small></button></li>
        <li><button type="button" id="bo-menu-account" onClick={choose(onAccount)}>Mi cuenta</button></li>
        <li><button type="button" id="bo-menu-logout" disabled={busy} onClick={choose(onLogout)}>Cerrar sesión</button></li>
      </ul>
    </div>
  </div>;
}

/** "Mi cuenta": who is logged in and their preferences, for now the theme (saved at once on change). */
export function AccountSection({ identity, preference }) {
  usePageHelp();
  const busy = preference.status === 'loading' || preference.saving;
  return <>
    <header><div><p className="bo-kicker">Tu cuenta · {identity.email}</p><h1>Mi cuenta</h1>
      <p>Tus datos de acceso y tus preferencias. Se guardan para tu usuario y te siguen en cualquier navegador.</p></div></header>
    <div className="bo-account">
      <section className="bo-list bo-detail" aria-labelledby="bo-account-identity"><h2 id="bo-account-identity">Identidad</h2>
        <dl>
          <div><dt>Email</dt><dd>{identity.email}</dd></div>
          <div><dt>Tipo de cuenta</dt><dd>{accountType}<small className="bo-cell-note">Las cuentas por invitación todavía no están habilitadas.</small></dd></div>
        </dl>
      </section>
      <section className="bo-editor bo-preferences" aria-labelledby="bo-account-preferences"><h2 id="bo-account-preferences">Preferencias</h2>
        <fieldset className="bo-group" aria-describedby="bo-theme-hint" disabled={busy}>
          <legend>Tema</legend>
          {['light', 'dark'].map(theme => <Field key={theme} id={`bo-theme-${theme}`} label={themeLabel(theme)} check>
            <input id={`bo-theme-${theme}`} type="radio" name="bo-theme" value={theme} checked={preference.theme === theme}
              onChange={() => preference.choose(theme)} /></Field>)}
          <small id="bo-theme-hint" className="bo-hint">Claro es el tema por defecto. La impresión y los PDF siempre usan el tema claro.</small>
        </fieldset>
        {preference.status === 'loading' && <p className="bo-loading" role="status">Cargando tus preferencias…</p>}
        {preference.saving && <p className="bo-loading" role="status">Guardando…</p>}
        {preference.notice && <p className="bo-notice" role="status">{preference.notice}</p>}
        {preference.error && <p className="bo-error" role="alert">{preference.error}</p>}
      </section>
    </div>
  </>;
}
