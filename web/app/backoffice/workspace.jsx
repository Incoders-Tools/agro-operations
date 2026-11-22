'use client';

import { useEffect, useState } from 'react';

const groups = {
  clients: { title: 'Clientes', singular: 'Cliente', parent: null },
  fields: { title: 'Campos', singular: 'Campo', parent: 'clients', parentKey: 'clientId' },
  plots: { title: 'Lotes', singular: 'Lote', parent: 'fields', parentKey: 'fieldId' },
  crops: { title: 'Cultivos', singular: 'Cultivo', parent: null },
};
const empty = { clients: [], fields: [], plots: [], crops: [] };

async function api(path, options = {}) {
  const response = await fetch(`/api/v1/${path}`, { cache: 'no-store', credentials: 'same-origin', ...options });
  if (!response.ok) {
    if (response.status === 401) throw new Error(path === 'session' && options.method === 'POST' ? 'Contraseña incorrecta. Inténtalo de nuevo.' : 'La sesión terminó. Ingresa de nuevo.');
    if (response.status === 403) throw new Error('No se pudo validar la solicitud. Actualiza la sesión.');
    if (response.status === 409) throw new Error('Otra persona modificó este registro. Actualiza antes de guardar.');
    if (response.status === 404) throw new Error('El registro relacionado ya no existe. Actualiza los listados.');
    throw new Error(response.status >= 500 ? 'La API privada no responde. Inicia el servidor local.' : 'Revisa los datos e intenta de nuevo.');
  }
  return response.status === 204 ? null : response.json();
}

export default function Backoffice() {
  const [identity, setIdentity] = useState(null);
  const [loading, setLoading] = useState(true);
  const [password, setPassword] = useState('');
  const [group, setGroup] = useState('clients');
  const [rows, setRows] = useState(empty);
  const [form, setForm] = useState({ name: '', parent: '', area: '' });
  const [editing, setEditing] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function refresh() {
    const entries = await Promise.all(Object.keys(groups).map(async key => [key, await api(key)]));
    setRows(Object.fromEntries(entries));
  }
  useEffect(() => {
    let active = true;
    api('session').then(async session => {
      if (!active) return;
      setIdentity(session);
      await refresh();
    }).catch(cause => {
      if (active && !cause.message.includes('sesión terminó')) setError(cause.message);
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
      await refresh();
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }
  async function logout() {
    setBusy(true); setError('');
    try { await api('session/logout', { method: 'POST', headers: { 'X-Agro-CSRF': identity.csrfToken } }); }
    catch (cause) { setError(cause.message); }
    finally { setIdentity(null); setRows(empty); setEditing(null); setBusy(false); }
  }
  function switchGroup(key) {
    setGroup(key); setForm({ name: '', parent: '', area: '' }); setEditing(null); setError('');
  }
  function edit(row) {
    const meta = groups[group];
    setEditing(row);
    setForm({ name: row.name, parent: meta.parentKey ? row[meta.parentKey] : '', area: row.areaHectares ?? '' });
    setError('');
  }
  async function save(event) {
    event.preventDefault();
    setBusy(true); setError('');
    const meta = groups[group];
    const value = { name: form.name.trim() };
    if (!editing && meta.parentKey) value[meta.parentKey] = form.parent;
    if (group === 'plots') value.areaHectares = Number(form.area);
    if (editing) value.revision = editing.revision;
    try {
      await api(editing ? `${group}/${editing.id}` : group, {
        method: editing ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Agro-CSRF': identity.csrfToken },
        body: JSON.stringify(value),
      });
      await refresh();
      setForm({ name: '', parent: '', area: '' }); setEditing(null);
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
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

  const meta = groups[group];
  const parents = meta.parent ? rows[meta.parent] : [];
  const parentName = id => parents.find(row => row.id === id)?.name ?? 'Sin referencia';
  return <div className="bo-app">
    <aside className="bo-sidebar"><div className="bo-mark"><span aria-hidden="true">✳</span><strong>Agro</strong><small>Operaciones</small></div>
      <nav aria-label="Registros operativos">{Object.entries(groups).map(([key, item]) =>
        <button key={key} type="button" className={key === group ? 'bo-selected' : ''} onClick={() => switchGroup(key)} aria-current={key === group ? 'page' : undefined}>{item.title}<span>{rows[key].length}</span></button>
      )}</nav><p>Las campañas, recetas y labores se incorporarán como registros separados. Una tarea planificada no equivale a un trabajo realizado.</p>
      <button type="button" className="bo-logout" disabled={busy} onClick={logout}>Cerrar sesión</button>
    </aside>
    <main className="bo-main"><header><div><p className="bo-kicker">Registro privado · {identity.email}</p><h1>{meta.title}</h1><p>Consulta y administra los datos guardados en PostgreSQL local.</p></div><span className="bo-count">{rows[group].length} {rows[group].length === 1 ? 'registro' : 'registros'}</span></header>
      {error && <p className="bo-error" role="alert">{error}</p>}
      <div className="bo-columns"><section className="bo-list" aria-label={`Listado de ${meta.title.toLowerCase()}`}><div className="bo-section-head"><h2>Registros</h2><button type="button" onClick={() => { setError(''); refresh().catch(cause => setError(cause.message)); }}>Actualizar</button></div>
        {rows[group].length === 0 ? <p className="bo-empty">Aún no hay {meta.title.toLowerCase()}. Crea el primer registro desde el formulario.</p> :
          <ul>{rows[group].map(row => <li key={row.id}><div><strong>{row.name}</strong><small>{meta.parentKey ? `${parentName(row[meta.parentKey])} · ` : ''}{group === 'plots' ? `${row.areaHectares} ha · ` : ''}Revisión {row.revision}</small></div><button type="button" onClick={() => edit(row)}>Editar</button></li>)}</ul>}
      </section><section className="bo-editor"><h2>{editing ? `Editar ${meta.singular.toLowerCase()}` : `Nuevo ${meta.singular.toLowerCase()}`}</h2>
        <form onSubmit={save}><label htmlFor="bo-name">Nombre</label><input id="bo-name" value={form.name} onChange={event => setForm({ ...form, name: event.target.value })} required maxLength={200} />
          {!editing && meta.parent && <><label htmlFor="bo-parent">{groups[meta.parent].singular}</label><select id="bo-parent" value={form.parent} onChange={event => setForm({ ...form, parent: event.target.value })} required><option value="">Selecciona un registro</option>{parents.map(parent => <option key={parent.id} value={parent.id}>{parent.name}</option>)}</select></>}
          {group === 'plots' && <><label htmlFor="bo-area">Superficie (ha)</label><input id="bo-area" type="number" min="0.0001" max="99999999.9999" step="0.0001" value={form.area} onChange={event => setForm({ ...form, area: event.target.value })} required /></>}
          <div className="bo-actions"><button type="submit" disabled={busy || (!editing && meta.parent && !parents.length)}>{busy ? 'Guardando…' : editing ? 'Guardar cambios' : `Crear ${meta.singular.toLowerCase()}`}</button>{editing && <button type="button" onClick={() => { setEditing(null); setForm({ name: '', parent: '', area: '' }); }}>Cancelar</button>}</div>
          {!editing && meta.parent && !parents.length && <p>Primero crea un {groups[meta.parent].singular.toLowerCase()}.</p>}
        </form></section></div>
    </main>
  </div>;
}
