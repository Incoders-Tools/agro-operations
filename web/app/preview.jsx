'use client';

import { useState } from 'react';
import { plots, searchPlots } from '../src/demo.mjs';

export default function Notebook() {
  const [query, setQuery] = useState('');
  const [selected, setSelected] = useState(plots[0].id);
  const results = searchPlots(query);
  const active = results.find(plot => plot.id === selected) ?? results[0];
  return <div className="shell">
    <header className="topbar"><div className="brand"><span className="brandmark" aria-hidden="true">✳</span><span>CUADERNO <strong>DE CAMPO</strong></span></div><span className="badge">● DEMOSTRACIÓN LOCAL</span></header>
    <main className="workspace">
      <aside className="index" aria-label="Índice de lotes">
        <div className="index-head"><span className="eyebrow">EXPLORAR · DATOS INVENTADOS</span><h1>Lotes de muestra<span className="count">{results.length.toString().padStart(2, '0')}</span></h1><p>Un recorrido visual por fichas ficticias. Ningún dato representa una explotación real.</p></div>
        <label htmlFor="search">Buscar cliente, campo o lote</label><div className="search"><span aria-hidden="true">⌕</span><input id="search" type="search" placeholder="Ej. La Cañada" value={query} onChange={event => setQuery(event.target.value)} /></div>
        <nav aria-label="Lotes de demostración"><ul className="plot-list">{results.map(plot => <li key={`${plot.client}-${plot.id}`}><button type="button" className={`plot-link ${active === plot ? 'active' : ''}`} aria-current={active === plot ? 'page' : undefined} onClick={() => setSelected(plot.id)}><span className="plot-number">{plot.id}</span><span className="plot-title"><strong>{plot.name}</strong><small>{plot.field} · {plot.client}</small></span><span aria-hidden="true">↗</span></button></li>)}</ul></nav>
        {results.length === 0 && <p role="status" className="empty">Sin lotes de muestra para esta búsqueda.</p>}
        <div className="index-foot">Archivo de demostración <span>·</span> Sin conexión a datos reales</div>
      </aside>
      <section className="detail" aria-label="Ficha del lote" aria-live="polite">{active ? <>
        <div className="detail-top"><span className="eyebrow">FICHA DE LOTE / DEMO — {active.id}</span><span className="folio">CUADERNO / 2025</span></div>
        <div className="hero"><div><p className="overline">{active.client} <span> / </span> {active.field}</p><h2>{active.name}</h2><p className="hero-caption">Una vista de trabajo, no un registro operativo.</p></div><div className="seal" aria-label="Información ficticia">DATO<br/>FICTICIO<span>✳</span></div></div>
        <div className="facts"><div><span>CULTIVO DE MUESTRA</span><strong>{active.crop}</strong></div><div><span>SUPERFICIE DE MUESTRA</span><strong>{active.area}</strong></div><div><span>ESTADO ILUSTRATIVO</span><strong>{active.status}</strong></div></div>
        <div className="sections"><section className="history"><div className="section-heading"><span className="eyebrow">01 / BITÁCORA</span><h3>Historial ficticio</h3></div><ol>{active.history.map((entry, i) => <li key={entry}><span className="step">0{i + 1}</span><p>{entry}</p></li>)}</ol></section><section className="order"><div className="section-heading"><span className="eyebrow">02 / VISTA PREVIA</span><h3>Orden de trabajo</h3></div><div className="order-paper"><span className="stamp">EJEMPLO · NO EJECUTAR</span><p>{active.order}</p><small>Solo visualización. No crea, modifica ni confirma trabajos.</small></div></section></div>
        <footer className="detail-foot">PROTOTIPO LOCAL <span>✳</span> INFORMACIÓN ENTERAMENTE INVENTADA</footer>
      </> : <div className="no-detail">Selecciona otro lote o cambia la búsqueda para continuar.</div>}</section>
    </main>
  </div>;
}
