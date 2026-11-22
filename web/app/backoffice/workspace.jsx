'use client';

import { useEffect, useState } from 'react';

const groups = {
  clients: { title: 'Clientes', singular: 'Cliente', parents: [] },
  fields: { title: 'Campos', singular: 'Campo', parents: [{ group: 'clients', key: 'clientId', label: 'Cliente' }] },
  plots: { title: 'Lotes', singular: 'Lote', parents: [{ group: 'fields', key: 'fieldId', label: 'Campo' }], area: true },
  crops: { title: 'Cultivos', singular: 'Cultivo', parents: [] },
  campaigns: { title: 'Campañas', singular: 'Campaña', feminine: true, parents: [] },
  'labor-types': { title: 'Tipos de labor', singular: 'Tipo de labor', parents: [] },
  'plot-campaign-areas': {
    title: 'Superficie por campaña', singular: 'Registro de superficie', nameField: false, area: true,
    parents: [{ group: 'plots', key: 'plotId', label: 'Lote' }, { group: 'campaigns', key: 'campaignId', label: 'Campaña' }],
    primaryLabel: row => `${row.areaHectares} ha`,
  },
  recipes: {
    title: 'Recetas', singular: 'Receta', feminine: true, nameField: false, stage: true,
    parents: [
      { group: 'plots', key: 'plotId', label: 'Lote' },
      { group: 'campaigns', key: 'campaignId', label: 'Campaña' },
      { group: 'labor-types', key: 'laborTypeId', label: 'Tipo de labor' },
    ],
    primaryLabel: row => `${row.stage} · ${row.targetUse}`,
  },
  'recipe-lines': {
    title: 'Líneas de receta', singular: 'Línea de receta', feminine: true, nameField: false, dose: true,
    parents: [{ group: 'recipes', key: 'recipeId', label: 'Receta' }],
    primaryLabel: row => `${row.productName} · ${row.doseValue} ${row.doseUnit}`,
  },
};
const emptyForm = { name: '', parents: {}, area: '', stage: 'presiembra', targetUse: '', responsibleEngineer: '', productName: '', doseValue: '', doseUnit: '' };
const empty = Object.fromEntries(Object.keys(groups).map(key => [key, []]));

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
  const [form, setForm] = useState(emptyForm);
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
    setGroup(key); setForm(emptyForm); setEditing(null); setError('');
  }
  function edit(row) {
    const meta = groups[group];
    setEditing(row);
    setForm({
      ...emptyForm,
      name: meta.nameField === false ? '' : row.name,
      parents: Object.fromEntries(meta.parents.map(parent => [parent.key, row[parent.key]])),
      area: meta.area ? row.areaHectares : '',
      stage: meta.stage ? row.stage : 'presiembra',
      targetUse: meta.stage ? row.targetUse : '',
      responsibleEngineer: meta.stage ? row.responsibleEngineer : '',
      productName: meta.dose ? row.productName : '',
      doseValue: meta.dose ? row.doseValue : '',
      doseUnit: meta.dose ? row.doseUnit : '',
    });
    setError('');
  }
  async function save(event) {
    event.preventDefault();
    setBusy(true); setError('');
    const meta = groups[group];
    const value = {};
    if (meta.nameField !== false) value.name = form.name.trim();
    if (meta.area) value.areaHectares = Number(form.area);
    if (meta.stage) { value.stage = form.stage; value.targetUse = form.targetUse.trim(); value.responsibleEngineer = form.responsibleEngineer.trim(); }
    if (meta.dose) { value.productName = form.productName.trim(); value.doseValue = Number(form.doseValue); value.doseUnit = form.doseUnit.trim(); }
    if (!editing) for (const parent of meta.parents) value[parent.key] = form.parents[parent.key];
    if (editing) value.revision = editing.revision;
    try {
      await api(editing ? `${group}/${editing.id}` : group, {
        method: editing ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Agro-CSRF': identity.csrfToken },
        body: JSON.stringify(value),
      });
      await refresh();
      setForm(emptyForm); setEditing(null);
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
  const rowLabel = (groupKey, row) => groups[groupKey].primaryLabel ? groups[groupKey].primaryLabel(row) : row.name;
  const label = row => rowLabel(group, row);
  const parentName = (parent, id) => {
    const match = rows[parent.group]?.find(row => row.id === id);
    return match ? rowLabel(parent.group, match) : 'Sin referencia';
  };
  const parentsReady = meta.parents.every(parent => rows[parent.group]?.length);
  const stages = ['presiembra', 'preemergencia', 'postemergencia'];
  return <div className="bo-app">
    <aside className="bo-sidebar"><div className="bo-mark"><span aria-hidden="true">✳</span><strong>Agro</strong><small>Operaciones</small></div>
      <nav aria-label="Registros operativos">{Object.entries(groups).map(([key, item]) =>
        <button key={key} type="button" className={key === group ? 'bo-selected' : ''} onClick={() => switchGroup(key)} aria-current={key === group ? 'page' : undefined}>{item.title}<span>{rows[key].length}</span></button>
      )}</nav><p>Una receta es un plan interno de tratamiento: no es una prescripción oficial emitida ni prueba de que la labor se haya realizado.</p>
      <button type="button" className="bo-logout" disabled={busy} onClick={logout}>Cerrar sesión</button>
    </aside>
    <main className="bo-main"><header><div><p className="bo-kicker">Registro privado · {identity.email}</p><h1>{meta.title}</h1><p>Consulta y administra los datos guardados en PostgreSQL local.</p></div><span className="bo-count">{rows[group].length} {rows[group].length === 1 ? 'registro' : 'registros'}</span></header>
      {error && <p className="bo-error" role="alert">{error}</p>}
      <div className="bo-columns"><section className="bo-list" aria-label={`Listado de ${meta.title.toLowerCase()}`}><div className="bo-section-head"><h2>Registros</h2><button type="button" onClick={() => { setError(''); refresh().catch(cause => setError(cause.message)); }}>Actualizar</button></div>
        {rows[group].length === 0 ? <p className="bo-empty">Aún no hay {meta.title.toLowerCase()}. Crea el primer registro desde el formulario.</p> :
          <ul>{rows[group].map(row => <li key={row.id}><div><strong>{label(row)}</strong><small>{meta.parents.map(parent => parentName(parent, row[parent.key])).join(' · ')}{meta.parents.length ? ' · ' : ''}Revisión {row.revision}</small></div><button type="button" onClick={() => edit(row)}>Editar</button></li>)}</ul>}
      </section><section className="bo-editor"><h2>{editing ? `Editar ${meta.singular.toLowerCase()}` : `${meta.feminine ? 'Nueva' : 'Nuevo'} ${meta.singular.toLowerCase()}`}</h2>
        <form onSubmit={save}>
          {meta.nameField !== false && <><label htmlFor="bo-name">Nombre</label><input id="bo-name" value={form.name} onChange={event => setForm({ ...form, name: event.target.value })} required maxLength={200} /></>}
          {!editing && meta.parents.map(parent => <div key={parent.key}><label htmlFor={`bo-parent-${parent.key}`}>{parent.label}</label>
            <select id={`bo-parent-${parent.key}`} value={form.parents[parent.key] ?? ''} onChange={event => setForm({ ...form, parents: { ...form.parents, [parent.key]: event.target.value } })} required>
              <option value="">Selecciona un registro</option>{(rows[parent.group] ?? []).map(option => <option key={option.id} value={option.id}>{rowLabel(parent.group, option)}</option>)}
            </select></div>)}
          {meta.area && <><label htmlFor="bo-area">Superficie (ha)</label><input id="bo-area" type="number" min="0.0001" max="99999999.9999" step="0.0001" value={form.area} onChange={event => setForm({ ...form, area: event.target.value })} required /></>}
          {meta.stage && <>
            <label htmlFor="bo-stage">Etapa del ciclo</label>
            <select id="bo-stage" value={form.stage} onChange={event => setForm({ ...form, stage: event.target.value })}>{stages.map(stage => <option key={stage} value={stage}>{stage}</option>)}</select>
            <label htmlFor="bo-target">Uso objetivo</label><input id="bo-target" value={form.targetUse} onChange={event => setForm({ ...form, targetUse: event.target.value })} placeholder="Ej. Barbecho" required maxLength={200} />
            <label htmlFor="bo-engineer">Ingeniero responsable</label><input id="bo-engineer" value={form.responsibleEngineer} onChange={event => setForm({ ...form, responsibleEngineer: event.target.value })} required maxLength={200} />
          </>}
          {meta.dose && <>
            <label htmlFor="bo-product">Producto (ficticio por ahora)</label><input id="bo-product" value={form.productName} onChange={event => setForm({ ...form, productName: event.target.value })} required maxLength={200} />
            <label htmlFor="bo-dose">Dosis</label><input id="bo-dose" type="number" min="0.0001" max="99999999.9999" step="0.0001" value={form.doseValue} onChange={event => setForm({ ...form, doseValue: event.target.value })} required />
            <label htmlFor="bo-dose-unit">Unidad</label><input id="bo-dose-unit" value={form.doseUnit} onChange={event => setForm({ ...form, doseUnit: event.target.value })} placeholder="Ej. l/ha" required maxLength={50} />
          </>}
          <div className="bo-actions"><button type="submit" disabled={busy || (!editing && !parentsReady)}>{busy ? 'Guardando…' : editing ? 'Guardar cambios' : `Crear ${meta.singular.toLowerCase()}`}</button>{editing && <button type="button" onClick={() => { setEditing(null); setForm(emptyForm); }}>Cancelar</button>}</div>
          {!editing && !parentsReady && <p>Primero crea los registros relacionados: {meta.parents.map(parent => groups[parent.group].title.toLowerCase()).join(', ')}.</p>}
        </form></section></div>
    </main>
  </div>;
}
