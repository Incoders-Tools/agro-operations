'use client';

import { useState } from 'react';
import { send } from './api.mjs';
import { Field, SchemaControl } from './field.jsx';
import { entities, fieldApplies, formFor, payloadFor, rowLabel, searchRecords } from './schema.mjs';

export function SectionHeader({ identity, title, description, count }) {
  return <header><div><p className="bo-kicker">Registro privado · {identity.email}</p><h1>{title}</h1>
    <p>{description ?? 'Consulta y administra los datos guardados en PostgreSQL local.'}</p></div>
    {count !== undefined && <span className="bo-count">{count} {count === 1 ? 'registro' : 'registros'}</span>}</header>;
}

export const editorTitle = (entity, record) => {
  const { singular, feminine } = entities[entity];
  return record ? `Editar ${singular}` : `${feminine ? 'Nueva' : 'Nuevo'} ${singular}`;
};

// Create or edit one record of a schema entity. The parent re-mounts it (key) when the target record changes.
export function RecordForm({ entity, record, parent, optionSets, csrfToken, onSaved, onCancel, missing, context }) {
  const meta = entities[entity];
  const [form, setForm] = useState(() => formFor(entity, record));
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const fields = meta.fields.filter(field => fieldApplies(field, Boolean(record)));

  async function save(event) {
    event.preventDefault();
    setBusy(true); setError('');
    try {
      const saved = await send(record ? `${entity}/${record.id}` : entity, record ? 'PUT' : 'POST',
        payloadFor(entity, form, { revision: record?.revision, parent }), csrfToken);
      onSaved(saved, Boolean(record));
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }

  const label = meta.singular;
  return <section className="bo-editor"><h2>{editorTitle(entity, record)}</h2>
    {context && <p className="bo-context">{context}</p>}
    <form onSubmit={save}>
      {fields.map(field => <SchemaControl key={field.key} field={field} id={`bo-${entity}-${field.key}`} value={form[field.key]}
        optionSets={optionSets} onChange={value => setForm(current => ({ ...current, [field.key]: value }))} />)}
      {error && <p className="bo-error" role="alert">{error}</p>}
      {missing && <p>{missing}</p>}
      <div className="bo-actions"><button type="submit" disabled={busy || Boolean(missing)}>{busy ? 'Guardando…' : record ? 'Guardar cambios' : `Crear ${label}`}</button>
        {(record || onCancel) && <button type="button" onClick={onCancel}>Cancelar</button>}</div>
    </form></section>;
}

// A panel listing records. `onSelect` makes the row open its children and `selectLabel` adds that as a visible action;
// `onEdit` adds an "Editar" action.
export function RecordList({
  entity, title, rows, selectedId, onSelect, selectLabel, onEdit, onRefresh, onNew, newLabel, searchable, empty, label, detail, description,
}) {
  const [term, setTerm] = useState('');
  const meta = entities[entity] ?? {};
  const shown = searchable ? searchRecords(entity, rows, term) : rows;
  const primary = label ?? (row => rowLabel(entity, row));
  const secondary = detail ?? meta.detail ?? (() => '');
  return <section className="bo-list" aria-label={`Listado de ${(title ?? meta.title).toLowerCase()}`}>
    <div className="bo-section-head"><h2>{title ?? meta.title}</h2><div>
      {onNew && <button type="button" onClick={onNew}>{newLabel}</button>}
      {onRefresh && <button type="button" onClick={onRefresh}>Actualizar</button>}</div></div>
    {description && <p className="bo-list-note">{description}</p>}
    {searchable && rows.length > 0 && <div className="bo-search"><Field id={`bo-search-${entity}`} label="Buscar"
      hint={`${shown.length} de ${rows.length}`}>{hintId =>
        <input id={`bo-search-${entity}`} type="search" value={term} aria-describedby={hintId} onChange={event => setTerm(event.target.value)} />}</Field></div>}
    {rows.length === 0 ? <p className="bo-empty">{empty}</p> : shown.length === 0 ? <p className="bo-empty">Sin coincidencias para “{term}”.</p> :
      <ul className="bo-scroll">{shown.map(row => {
        const text = <><strong>{primary(row)}</strong><small>{secondary(row) || `Revisión ${row.revision}`}</small></>;
        return <li key={row.id} className={row.id === selectedId ? 'bo-current' : undefined}>
          {onSelect ? <button type="button" className="bo-row" aria-current={row.id === selectedId ? 'true' : undefined} onClick={() => onSelect(row)}>{text}</button>
            : <div className="bo-row">{text}</div>}
          {onSelect && selectLabel && <button type="button" className="bo-open" onClick={() => onSelect(row)}
            aria-label={`${selectLabel}: ${primary(row)}`}>{selectLabel}</button>}
          {onEdit && <button type="button" onClick={() => onEdit(row)} aria-label={`Editar ${primary(row)}`}>Editar</button>}</li>;
      })}</ul>}
  </section>;
}

// Attribute summary of the selected record; empty optional attributes read "Sin indicar".
export function RecordDetail({ title, items, onEdit }) {
  return <section className="bo-list bo-detail"><div className="bo-section-head"><h2>{title}</h2>
    {onEdit && <button type="button" onClick={onEdit}>Editar</button>}</div>
    <dl>{items.map(([term, value]) => <div key={term}><dt>{term}</dt><dd>{value === null || value === undefined || value === '' ? <span className="bo-muted">Sin indicar</span> : value}</dd></div>)}</dl>
  </section>;
}
