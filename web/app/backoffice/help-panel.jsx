'use client';

import { createContext, useCallback, useContext, useEffect, useId, useMemo, useRef, useState } from 'react';
import { currentHelpKey, helpScreens } from './help.mjs';
import { classifyLink, parseFrontMatter, parseMarkdown } from './markdown.mjs';
import { renderMarkdown } from './markdown-render.mjs';
import { displayDate } from './work-orders.mjs';

// Contextual help (S10, L3; S1 of the shell round): one "Ayuda" button at the top right of the screen opens a side panel
// with the docs/help file of the screen in view. The panel is not modal: the screen stays usable while it is open, and
// it follows the screen the user moves to.

const HelpContext = createContext(null);
const cache = new Map();

/** Context levels (help.mjs currentHelpKey): the section, a nested screen of it, a panel inside that screen. */
export const helpLevels = { section: 0, screen: 1, panel: 2 };

/**
 * Holds which help file the panel shows. `sectionKey` is the help key of the current section. Screens and panels
 * register their key (usePageHelp); the top button opens the most specific one and an open panel follows it.
 */
export function HelpProvider({ sectionKey = null, children }) {
  const [state, setState] = useState({ open: false, key: null, focus: 0 });
  const [contexts, setContexts] = useState([]);
  const invoker = useRef(null);
  const show = useCallback((key, button) => {
    invoker.current = button ?? null;
    setState(current => ({ open: true, key, focus: current.focus + 1 }));
  }, []);
  // A related screen opens in the same panel; the focus moves to its title.
  const go = useCallback(key => setState(current => ({ ...current, key, focus: current.focus + 1 })), []);
  const close = useCallback(() => {
    setState(current => ({ ...current, open: false }));
    const target = invoker.current?.isConnected ? invoker.current : document.getElementById('bo-help-open');
    target?.focus();
  }, []);
  const register = useCallback((id, key, level) => {
    setContexts(current => [...current.filter(context => context.id !== id), { id, key, level }]);
    return () => setContexts(current => current.filter(context => context.id !== id));
  }, []);
  const page = currentHelpKey(contexts, sectionKey);
  // Following the page never takes the focus from the screen the user works in.
  useEffect(() => { if (page) setState(current => current.open && current.key !== page ? { ...current, key: page } : current); }, [page]);
  const value = useMemo(() => ({ ...state, sectionKey, page, contexts, show, go, close, register }),
    [state, sectionKey, page, contexts, show, go, close, register]);
  return <HelpContext.Provider value={value}>{children}</HelpContext.Provider>;
}

function useHelpContext(key, level) {
  const register = useContext(HelpContext)?.register;
  const id = useId();
  useEffect(() => key && register ? register(id, key, level) : undefined, [id, key, level, register]);
}

/**
 * Registers the help key of the screen in view: by default the section's own; a nested screen (a plot inside Clientes)
 * passes its key, and a panel inside it (soil analyses) passes `helpLevels.panel`.
 */
export function usePageHelp(helpKey, level = helpKey ? helpLevels.screen : helpLevels.section) {
  const sectionKey = useContext(HelpContext)?.sectionKey;
  useHelpContext(helpKey ?? sectionKey, level);
}

const helpIcon = <svg className="bo-help-icon" viewBox="0 0 20 20" width="18" height="18" aria-hidden="true" focusable="false">
  <circle cx="10" cy="10" r="8.25" fill="none" stroke="currentColor" strokeWidth="1.5" />
  <path d="M7.6 7.7a2.5 2.5 0 1 1 3.4 2.3c-.6.3-1 .8-1 1.5v.4" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
  <circle cx="10" cy="14.4" r="1" fill="currentColor" /></svg>;

/**
 * The "Ayuda" button (icon and text) of the top bar and of the print toolbar: it opens the most specific registered
 * context, and a second press closes the panel. `variant="link"` is the login screen's text link. A panel that still
 * passes `helpKey` (the former "?" button next to its title) only registers its context and renders nothing.
 */
export function HelpButton({ helpKey, id = 'bo-help-open', variant, label = 'Ayuda' }) {
  const help = useContext(HelpContext);
  useHelpContext(helpKey, helpLevels.panel);
  if (!help || helpKey) return null;
  const key = help.page;
  const showing = help.open && help.key === key;
  const className = variant === 'link' ? 'bo-help-text-link' : 'bo-help-button';
  if (!key) {
    return <button type="button" id={id} className={className} aria-disabled="true" title="Esta pantalla todavía no tiene ayuda">
      {variant !== 'link' && helpIcon}<span>{label}</span></button>;
  }
  return <button type="button" id={id} className={className} aria-label={variant === 'link' ? undefined : 'Ayuda de esta pantalla'}
    aria-controls="bo-help-panel" aria-expanded={showing} onClick={event => showing ? help.close() : help.show(key, event.currentTarget)}>
    {variant !== 'link' && helpIcon}<span>{label}</span></button>;
}

function useHelpFile(key) {
  const [file, setFile] = useState(() => cache.get(key) ?? { key, status: 'loading' });
  useEffect(() => {
    if (!key) return undefined;
    if (cache.has(key)) { setFile(cache.get(key)); return undefined; }
    let active = true;
    setFile({ key, status: 'loading' });
    fetch(`/backoffice/help/${encodeURIComponent(key)}`, { cache: 'no-store' })
      .then(response => response.ok ? response.text() : Promise.reject(new Error(String(response.status))))
      .then(text => {
        const { meta, body } = parseFrontMatter(text);
        const loaded = { key, status: 'ready', meta, blocks: parseMarkdown(body) };
        cache.set(key, loaded);
        if (active) setFile(loaded);
      })
      .catch(() => { if (active) setFile({ key, status: 'error' }); });
    return () => { active = false; };
  }, [key]);
  return file.key === key ? file : { key, status: 'loading' };
}

/**
 * The side panel; it renders nothing while closed. Escape or "Cerrar" close it and return the focus to the "Ayuda"
 * button. When the screen has more than one context (a plot and its soil analyses), it offers each of them.
 */
export function HelpPanel() {
  const help = useContext(HelpContext);
  const title = useRef(null);
  const file = useHelpFile(help?.open ? help.key : null);
  const focus = help?.focus;
  useEffect(() => { if (help?.open) title.current?.focus(); }, [focus, help?.open]);
  if (!help?.open) return null;
  const meta = file.status === 'ready' ? file.meta : {};
  const screens = [...new Set([...help.contexts].sort((a, b) => a.level - b.level).map(context => context.key))];
  const video = meta.video ? classifyLink(meta.video) : null;
  return <aside id="bo-help-panel" className="bo-help-panel" role="complementary" aria-labelledby="bo-help-title"
    onKeyDown={event => { if (event.key === 'Escape') { event.stopPropagation(); help.close(); } }}>
    <div className="bo-help-head">
      <div><p className="bo-help-kicker">Ayuda</p>
        <h2 id="bo-help-title" ref={title} tabIndex={-1}>{meta.title ?? helpScreens[help.key] ?? 'Ayuda'}</h2></div>
      <button type="button" className="bo-help-close" aria-label="Cerrar ayuda" onClick={help.close}>Cerrar</button>
    </div>
    <div className="bo-help-body">
      {screens.length > 1 && <nav className="bo-help-screens" aria-label="Ayudas de esta pantalla"><ul>{screens.map(key =>
        <li key={key}><button type="button" className="bo-help-chip" aria-current={key === help.key ? 'true' : undefined}
          onClick={() => help.go(key)}>{helpScreens[key] ?? key}</button></li>)}</ul></nav>}
      {file.status === 'loading' && <p className="bo-loading" role="status">Cargando la ayuda…</p>}
      {file.status === 'error' && <p className="bo-error" role="alert">No se pudo cargar la ayuda de esta pantalla. Actualiza la página e intenta de nuevo.</p>}
      {file.status === 'ready' && <>
        {meta.summary && <p className="bo-help-summary">{meta.summary}</p>}
        {video && <section className="bo-help-video" aria-label="Video">
          {video.kind === 'external' ? <a href={video.href} target="_blank" rel="noopener">Ver el video de esta pantalla</a>
            : <p>El video de esta pantalla todavía no está disponible.</p>}</section>}
        <div className="bo-help-content">{renderMarkdown(file.blocks, { onHelpLink: help.go })}</div>
        {meta.related?.length > 0 && <section className="bo-help-related" aria-labelledby="bo-help-related">
          <h3 id="bo-help-related">Pantallas relacionadas</h3>
          <ul>{meta.related.map(key => <li key={key}><button type="button" className="bo-help-link" onClick={() => help.go(key)}>{helpScreens[key] ?? key}</button></li>)}</ul>
        </section>}
        {meta.reviewed && <p className="bo-help-reviewed">Revisado el {displayDate(meta.reviewed)}.</p>}
      </>}
    </div>
  </aside>;
}
