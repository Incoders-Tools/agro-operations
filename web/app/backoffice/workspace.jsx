'use client';

import { useEffect, useReducer, useState } from 'react';
import { api, sessionEndedMessage } from './api.mjs';
import { brandName, brandTagline } from './brand.mjs';
import CatalogSection from './catalogs.jsx';
import ClientsSection from './clients.jsx';
import GenericSection, { genericGroups } from './generic.jsx';
import { groupOpen, menuReducer, menuStorageKey, readMenuState } from './navigation.mjs';
import WorkOrdersSection from './orders.jsx';
import ProvidersSection from './providers.jsx';
import QuoteComparisonsSection from './quotes.jsx';
import SeedAnalysesSection from './seed-analyses.jsx';
import { entities } from './schema.mjs';

// Sidebar navigation, in collapsible groups (S6). To add a section, add its key here and a case in renderSection.
const navigation = [
  {
    key: 'operation', title: 'Operación',
    items: [
      { key: 'work-orders', title: 'Órdenes de trabajo' }, { key: 'clients', title: 'Clientes' },
      { key: 'quote-comparisons', title: 'Comparación de presupuestos' },
    ],
  },
  {
    key: 'catalogs', title: 'Catálogos',
    items: [
      ...['contractors', 'providers', 'inputs', 'labor-types', 'stages', 'crops', 'campaigns'].map(key => ({ key, title: entities[key].title })),
      { key: 'seed-analyses', title: 'Análisis de semillas' },
    ],
  },
  { key: 'planning', title: 'Planificación', items: ['plot-campaign-areas', 'recipes', 'recipe-lines'].map(key => ({ key, title: genericGroups[key].title })) },
  { key: 'audit', title: 'Auditoría', items: [{ key: 'import-records', title: genericGroups['import-records'].title }] },
];
const groupOf = section => navigation.find(group => group.items.some(item => item.key === section))?.key;

// `params` carries the target of a cross-section link, such as the client whose quote comparisons to show.
function renderSection(key, identity, params, navigate) {
  if (key === 'work-orders') return <WorkOrdersSection identity={identity} />;
  if (key === 'clients') return <ClientsSection identity={identity} navigate={navigate} />;
  if (key === 'providers') return <ProvidersSection identity={identity} />;
  if (key === 'quote-comparisons') return <QuoteComparisonsSection key={JSON.stringify(params)} identity={identity} initial={params} />;
  if (key === 'seed-analyses') return <SeedAnalysesSection identity={identity} />;
  if (entities[key]) return <CatalogSection key={key} entity={key} identity={identity} />;
  return <GenericSection key={key} group={key} identity={identity} />;
}

export default function Backoffice() {
  const [identity, setIdentity] = useState(null);
  const [loading, setLoading] = useState(true);
  const [password, setPassword] = useState('');
  const [section, setSection] = useState('clients');
  const [params, setParams] = useState({});
  const navigate = (key, next = {}) => { setSection(key); setParams(next); setError(''); };
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [menu, dispatchMenu] = useReducer(menuReducer, {});
  const [menuLoaded, setMenuLoaded] = useState(false);
  const activeGroup = groupOf(section);

  // The open groups are read after mounting (the server render has no localStorage) and saved on every change.
  useEffect(() => {
    try { dispatchMenu({ type: 'load', state: readMenuState(window.localStorage.getItem(menuStorageKey)) }); } catch { /* storage blocked */ }
    setMenuLoaded(true);
  }, []);
  useEffect(() => {
    if (!menuLoaded) return;
    try { window.localStorage.setItem(menuStorageKey, JSON.stringify(menu)); } catch { /* storage blocked */ }
  }, [menu, menuLoaded]);
  // Leaving a section keeps its group open, so the menu does not jump under the pointer.
  useEffect(() => { if (menuLoaded && activeGroup) dispatchMenu({ type: 'navigate', group: activeGroup }); }, [activeGroup, menuLoaded]);

  useEffect(() => {
    let active = true;
    api('session').then(session => {
      if (active) setIdentity(session);
    }).catch(cause => {
      if (active && cause.message !== sessionEndedMessage) setError(cause.message);
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  async function login(event) {
    event.preventDefault();
    setBusy(true); setError('');
    try {
      const session = await api('session', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ password }),
      });
      setPassword('');
      setIdentity(session);
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }
  async function logout() {
    setBusy(true); setError('');
    try { await api('session/logout', { method: 'POST', headers: { 'X-Agro-CSRF': identity.csrfToken } }); }
    catch (cause) { setError(cause.message); }
    finally { setIdentity(null); setSection('clients'); setBusy(false); }
  }

  if (loading) return <main className="bo-login"><p role="status">Comprobando acceso local…</p></main>;
  if (!identity) return <main className="bo-login">
    <div className="bo-login-panel"><span className="bo-symbol" aria-hidden="true">✳</span><p className="bo-kicker">{brandName} · {brandTagline}</p>
      <h1>Tu espacio de trabajo</h1><p>Este registro es privado. Usa el administrador local temporal para consultar o cargar información.</p>
      <form onSubmit={login}><label htmlFor="bo-password">Contraseña local</label>
        <input id="bo-password" type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} required />
        <button type="submit" disabled={busy}>{busy ? 'Ingresando…' : 'Ingresar al backoffice'}</button></form>
      {error && <p className="bo-error" role="alert">{error}</p>}
      <small>La demostración de cuaderno de campo no usa estos datos.</small>
    </div></main>;

  return <div className="bo-app">
    <aside className="bo-sidebar"><div className="bo-mark"><span aria-hidden="true">✳</span><strong>{brandName}</strong><small>{brandTagline}</small></div>
      <nav aria-label="Registros operativos">{navigation.map(group => {
        const open = groupOpen(menu, group.key, activeGroup);
        const listId = `bo-nav-${group.key}`;
        return <div key={group.key} className="bo-nav-section">
          <button type="button" id={`${listId}-toggle`} className="bo-nav-group" aria-expanded={open} aria-controls={listId}
            aria-disabled={group.key === activeGroup || undefined} title={group.key === activeGroup ? 'Contiene la sección abierta' : undefined}
            onClick={() => dispatchMenu({ type: 'toggle', group: group.key, active: activeGroup })}>{group.title}</button>
          <div id={listId} className="bo-nav-items" role="group" aria-labelledby={`${listId}-toggle`} hidden={!open}>
            {group.items.map(item => <button key={item.key} type="button" className={item.key === section ? 'bo-selected' : ''}
              onClick={() => navigate(item.key)} aria-current={item.key === section ? 'page' : undefined}>{item.title}</button>)}</div>
        </div>;
      })}</nav>
      <button type="button" className="bo-logout" disabled={busy} onClick={logout}>Cerrar sesión</button>
    </aside>
    <main className="bo-main">
      {error && <p className="bo-error" role="alert">{error}</p>}
      {renderSection(section, identity, params, navigate)}
    </main>
  </div>;
}
