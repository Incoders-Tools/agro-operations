'use client';

import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { helpScreens } from './help.mjs';
import { classifyLink, parseFrontMatter, parseMarkdown } from './markdown.mjs';
import { renderMarkdown } from './markdown-render.mjs';
import { displayDate } from './work-orders.mjs';

// Contextual help (S10, L3): a "?" button in each screen opens a side panel with the screen's docs/help file. The panel
// is not modal: the screen stays usable while it is open, and it follows the screen the user moves to.

const HelpContext = createContext(null);
const cache = new Map();

/**
 * Holds which help file the panel shows. `sectionKey` is the help key of the current section, the default of its
 * header's "?" button. A header registers its key as the page (usePageHelp); an open panel switches to a new page.
 */
export function HelpProvider({ sectionKey = null, children }) {
  const [state, setState] = useState({ open: false, key: null, focus: 0 });
  const [page, setPage] = useState(null);
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
  // Following the page never takes the focus from the screen the user works in.
  useEffect(() => { if (page) setState(current => current.open && current.key !== page ? { ...current, key: page } : current); }, [page]);
  return <HelpContext.Provider value={{ ...state, sectionKey, show, go, close, setPage }}>{children}</HelpContext.Provider>;
}

/** Registers the help key of the screen in view (by default the section's), so an open panel follows it. */
export function usePageHelp(helpKey) {
  const help = useContext(HelpContext);
  const key = helpKey ?? help?.sectionKey;
  const setPage = help?.setPage;
  useEffect(() => { if (key && setPage) setPage(key); }, [key, setPage]);
}

/** The "?" button of a screen or of a nested context; a second press closes the panel. */
export function HelpButton({ helpKey, id = 'bo-help-open' }) {
  const help = useContext(HelpContext);
  const key = helpKey ?? help?.sectionKey;
  if (!help || !key) return null;
  const showing = help.open && help.key === key;
  return <button type="button" id={id} className="bo-help-button" aria-label="Ayuda de esta pantalla" title="Ayuda de esta pantalla"
    aria-controls="bo-help-panel" aria-expanded={showing} onClick={event => showing ? help.close() : help.show(key, event.currentTarget)}>?</button>;
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

/** The side panel; it renders nothing while closed. Escape or "Cerrar" close it and return the focus to its "?" button. */
export function HelpPanel() {
  const help = useContext(HelpContext);
  const title = useRef(null);
  const file = useHelpFile(help?.open ? help.key : null);
  const focus = help?.focus;
  useEffect(() => { if (help?.open) title.current?.focus(); }, [focus, help?.open]);
  if (!help?.open) return null;
  const meta = file.status === 'ready' ? file.meta : {};
  const video = meta.video ? classifyLink(meta.video) : null;
  return <aside id="bo-help-panel" className="bo-help-panel" role="complementary" aria-labelledby="bo-help-title"
    onKeyDown={event => { if (event.key === 'Escape') { event.stopPropagation(); help.close(); } }}>
    <div className="bo-help-head">
      <div><p className="bo-help-kicker">Ayuda</p>
        <h2 id="bo-help-title" ref={title} tabIndex={-1}>{meta.title ?? helpScreens[help.key] ?? 'Ayuda'}</h2></div>
      <button type="button" className="bo-help-close" aria-label="Cerrar ayuda" onClick={help.close}>Cerrar</button>
    </div>
    <div className="bo-help-body">
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
