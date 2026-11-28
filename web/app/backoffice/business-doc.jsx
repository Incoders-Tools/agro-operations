'use client';

import { Fragment, useEffect, useState } from 'react';
import { brandName, brandTagline } from './brand.mjs';
import { businessDocs, plainText, tableOfContents } from './business/document.mjs';
import { parseFrontMatter, parseMarkdown } from './markdown.mjs';
import { renderMarkdown } from './markdown-render.mjs';
import { displayDate } from './work-orders.mjs';

// Business documentation (S4): docs/business/<key>.md rendered in the site style with a table of contents, and its
// printable view. The Markdown goes through the help pipeline (markdown.mjs, markdown-render.mjs: no HTML passthrough);
// only the headings are drawn here, so each H2 and H3 carries the anchor id of its table of contents entry.

export const businessDocHref = key => `/backoffice/business/${encodeURIComponent(key)}`;
export const businessPrintHref = key => `${businessDocHref(key)}/print`;

/** { status: 'loading' | 'ready' | 'missing' | 'error', meta, blocks, toc } of one document. */
function useBusinessDoc(key) {
  const [doc, setDoc] = useState({ key, status: 'loading' });
  useEffect(() => {
    let active = true;
    setDoc({ key, status: 'loading' });
    fetch(businessDocHref(key), { cache: 'no-store' }).then(async response => {
      if (!active) return;
      if (response.status === 404) { setDoc({ key, status: 'missing' }); return; }
      if (!response.ok) throw new Error(String(response.status));
      const { meta, body } = parseFrontMatter(await response.text());
      const all = parseMarkdown(body);
      // The leading H1 repeats the title, which the page shows from the front matter.
      const blocks = all[0]?.type === 'heading' && all[0].level === 1 ? all.slice(1) : all;
      if (active) setDoc({ key, status: 'ready', meta, blocks, toc: tableOfContents(blocks) });
    }).catch(() => { if (active) setDoc({ key, status: 'error' }); });
    return () => { active = false; };
  }, [key]);
  return doc.key === key ? doc : { key, status: 'loading' };
}

/** The document body: runs of paragraphs, lists and tables from the shared renderer; headings with their anchor ids. */
function DocBody({ blocks, toc }) {
  const nodes = [];
  let run = [];
  let entry = 0;
  const flush = () => { if (run.length) nodes.push(<Fragment key={`r${nodes.length}`}>{renderMarkdown(run)}</Fragment>); run = []; };
  blocks.forEach(block => {
    if (block.type !== 'heading') { run.push(block); return; }
    flush();
    const text = plainText(block.inline);
    const listed = block.level === 2 || block.level === 3;
    const id = listed ? toc[entry++]?.id : undefined;
    const Tag = block.level <= 2 ? 'h2' : block.level === 3 ? 'h3' : 'h4';
    nodes.push(<Tag key={`h${nodes.length}`} id={id} tabIndex={id ? -1 : undefined}>{text}</Tag>);
  });
  flush();
  return <div className="bd-body">{nodes}</div>;
}

function jump(event, id) {
  const target = document.getElementById(id);
  if (!target) return;
  event.preventDefault();
  const smooth = !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
  target.scrollIntoView({ behavior: smooth ? 'smooth' : 'auto', block: 'start' });
  target.focus({ preventScroll: true });
}

function Contents({ toc, interactive = true }) {
  if (!toc.length) return null;
  return <ol className="bd-toc-list">{toc.map(item => <li key={item.id} className={item.level === 3 ? 'bd-toc-sub' : undefined}>
    {interactive ? <a href={`#${item.id}`} onClick={event => jump(event, item.id)}>{item.text}</a> : <span>{item.text}</span>}</li>)}</ol>;
}

const facts = meta => [meta.reviewed && `Revisado el ${displayDate(meta.reviewed)}`, meta.version && `Versión ${meta.version}`].filter(Boolean).join(' · ');

/** "Documentación › <document>": the document with a sticky table of contents and "Descargar PDF". */
export function BusinessDocSection({ identity, docKey }) {
  const doc = useBusinessDoc(docKey);
  const meta = doc.meta ?? {};
  const title = meta.title ?? businessDocs[docKey] ?? 'Documento';
  return <>
    <header className="bd-header"><div><p className="bo-kicker">Documentación · {identity.email}</p><h1>{title}</h1>
      <p>{meta.summary ?? 'La referencia de la operación del negocio, mantenida en el repositorio junto con las ayudas.'}</p>
      {doc.status === 'ready' && facts(meta) && <p className="bd-facts">{facts(meta)}</p>}</div>
      {doc.status === 'ready' && <a id="bo-doc-print" className="bd-print-button" href={businessPrintHref(docKey)} target="_blank" rel="noopener"
        aria-label={`Descargar PDF de ${title}`}>Descargar PDF</a>}</header>
    {doc.status === 'loading' && <p className="bo-loading" role="status">Cargando el documento…</p>}
    {doc.status === 'error' && <p className="bo-error" role="alert">No se pudo cargar el documento. Actualiza la página e intenta de nuevo.</p>}
    {doc.status === 'missing' && <section className="bo-list bd-empty" aria-labelledby="bd-empty-title">
      <h2 id="bd-empty-title">El documento todavía no está disponible</h2>
      <p className="bo-empty">Cuando el archivo <code>docs/business/{docKey}.md</code> esté en el repositorio, se mostrará aquí con su índice y la
        opción de descargarlo en PDF.</p></section>}
    {doc.status === 'ready' && <div className="bd-layout">
      {doc.toc.length > 0 && <nav className="bd-toc" aria-labelledby="bd-toc-title"><details open>
        <summary id="bd-toc-title">Contenido</summary><Contents toc={doc.toc} /></details></nav>}
      <article id="bo-doc-article" className="bd-article" aria-label={title}><DocBody blocks={doc.blocks} toc={doc.toc} />
        {meta.source && <p className="bd-source">Fuente: {meta.source}.</p>}</article>
    </div>}
  </>;
}

/**
 * The printable document (S4), like the OT print: A4 sheets with the brand header, a title page (title, summary, source,
 * review date, version and contents) and a new page for each H2. "Descargar PDF" opens the browser's print dialog,
 * where "Guardar como PDF" proposes document.title as the file name.
 */
export function BusinessDocPrint({ docKey }) {
  const doc = useBusinessDoc(docKey);
  const meta = doc.meta ?? {};
  const title = meta.title ?? businessDocs[docKey] ?? 'Documento';
  useEffect(() => {
    if (doc.status === 'ready') document.title = [brandName, title, meta.version && `versión ${meta.version}`].filter(Boolean).join(' - ');
  }, [doc.status, title, meta.version]);
  const ready = doc.status === 'ready';
  return <main className="ot-page bd-print">
    <nav className="ot-toolbar" aria-label="Acciones del documento">
      {ready && <button type="button" id="bo-doc-print-now" onClick={() => window.print()}>Descargar PDF</button>}
      <a href="/backoffice">Volver</a>
      {ready && <small>En el diálogo de impresión elige “Guardar como PDF”; el nombre del archivo ya viene propuesto.</small>}
    </nav>
    {!ready ? <div className="ot-message">
      {doc.status === 'loading' ? <p role="status">Cargando el documento…</p>
        : doc.status === 'missing' ? <p role="alert">El documento todavía no está disponible.</p>
          : <p role="alert">No se pudo cargar el documento. Actualiza la página e intenta de nuevo.</p>}</div>
      : <article className="ot-sheet bd-sheet" aria-label={title}>
        <header className="ot-head"><p className="ot-mark"><span aria-hidden="true">✳</span> {brandName} · {brandTagline}</p><p className="bd-sheet-kind">DOCUMENTACIÓN</p></header>
        <section className="bd-cover" aria-label="Portada">
          <h1>{title}</h1>
          {meta.summary && <p className="bd-cover-summary">{meta.summary}</p>}
          <dl className="bd-cover-facts">
            {meta.source && <div><dt>Fuente</dt><dd>{meta.source}</dd></div>}
            {meta.reviewed && <div><dt>Revisado</dt><dd>{displayDate(meta.reviewed)}</dd></div>}
            {meta.version && <div><dt>Versión</dt><dd>{meta.version}</dd></div>}
          </dl>
          {doc.toc.length > 0 && <section className="bd-cover-toc" aria-label="Contenido"><h2>Contenido</h2><Contents toc={doc.toc} interactive={false} /></section>}
        </section>
        <DocBody blocks={doc.blocks} toc={doc.toc} />
      </article>}
  </main>;
}
