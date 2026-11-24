'use client';

import { useEffect, useState } from 'react';
import { api, sessionEndedMessage } from './api.mjs';
import CatalogSection from './catalogs.jsx';
import ClientsSection from './clients.jsx';
import GenericSection, { genericGroups } from './generic.jsx';
import WorkOrdersSection from './orders.jsx';
import { entities } from './schema.mjs';

// Sidebar navigation. To add a section, add its key here and a case in renderSection.
const navigation = [
  { title: 'Operación', items: [{ key: 'work-orders', title: 'Órdenes de trabajo' }, { key: 'clients', title: 'Clientes' }] },
  { title: 'Catálogos', items: ['contractors', 'inputs', 'labor-types', 'stages', 'crops', 'campaigns'].map(key => ({ key, title: entities[key].title })) },
  { title: 'Planificación', items: ['plot-campaign-areas', 'recipes', 'recipe-lines'].map(key => ({ key, title: genericGroups[key].title })) },
  { title: 'Auditoría', items: [{ key: 'import-records', title: genericGroups['import-records'].title }] },
];

function renderSection(key, identity) {
  if (key === 'work-orders') return <WorkOrdersSection identity={identity} />;
  if (key === 'clients') return <ClientsSection identity={identity} />;
  if (entities[key]) return <CatalogSection key={key} entity={key} identity={identity} />;
  return <GenericSection key={key} group={key} identity={identity} />;
}

export default function Backoffice() {
  const [identity, setIdentity] = useState(null);
  const [loading, setLoading] = useState(true);
  const [password, setPassword] = useState('');
  const [section, setSection] = useState('clients');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

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
    <div className="bo-login-panel"><span className="bo-symbol" aria-hidden="true">✳</span><p className="bo-kicker">Agro · Operaciones</p>
      <h1>Tu espacio de trabajo</h1><p>Este registro es privado. Usa el administrador local temporal para consultar o cargar información.</p>
      <form onSubmit={login}><label htmlFor="bo-password">Contraseña local</label>
        <input id="bo-password" type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} required />
        <button type="submit" disabled={busy}>{busy ? 'Ingresando…' : 'Ingresar al backoffice'}</button></form>
      {error && <p className="bo-error" role="alert">{error}</p>}
      <small>La demostración de cuaderno de campo no usa estos datos.</small>
    </div></main>;

  return <div className="bo-app">
    <aside className="bo-sidebar"><div className="bo-mark"><span aria-hidden="true">✳</span><strong>Agro</strong><small>Operaciones</small></div>
      <nav aria-label="Registros operativos">{navigation.map(group => [
        <p key={group.title} className="bo-nav-group">{group.title}</p>,
        ...group.items.map(item => <button key={item.key} type="button" className={item.key === section ? 'bo-selected' : ''}
          onClick={() => { setSection(item.key); setError(''); }} aria-current={item.key === section ? 'page' : undefined}>{item.title}</button>),
      ])}</nav><p>Una receta es un plan interno de tratamiento: no es una prescripción oficial emitida ni prueba de que la labor se haya realizado.</p>
      <button type="button" className="bo-logout" disabled={busy} onClick={logout}>Cerrar sesión</button>
    </aside>
    <main className="bo-main">
      {error && <p className="bo-error" role="alert">{error}</p>}
      {renderSection(section, identity)}
    </main>
  </div>;
}
