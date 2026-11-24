'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { api, send } from './api.mjs';
import { Field } from './field.jsx';
import { InputPicker, Select } from './providers.jsx';
import { RecordDetail, SectionHeader } from './records.jsx';
import {
  comparisonPayload, comparisonQuery, comparisonStatuses, comparisonTable, currencies, decidePayload, emptyQuote, formatMoney, newQuoteLine,
  quoteFromRecord, quoteLineTotal, quotePayload, quoteTotal,
} from './providers.mjs';
import { optionLabel } from './schema.mjs';
import { displayDate, formatAmount, todayIso } from './work-orders.mjs';

const nameOf = (rows, id) => rows.find(row => row.id === id)?.name ?? 'Sin referencia';
const options = rows => rows.map(row => ({ value: row.id, label: row.name }));
const lineModes = [{ value: 'input', label: 'Insumo del catálogo' }, { value: 'text', label: 'Texto libre' }];
const Status = ({ status }) => <span className={`bo-status bo-status-${status}`}>{optionLabel(comparisonStatuses, status)}</span>;

// Create a comparison for a client, or edit the title and request date of an open one.
function ComparisonForm({ record, clients, clientId, csrfToken, onSaved, onCancel }) {
  const [form, setForm] = useState(() => ({
    clientId: record?.clientId ?? clientId ?? '', title: record?.title ?? '', requestedOn: record?.requestedOn ?? todayIso(),
  }));
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const set = changes => setForm(current => ({ ...current, ...changes }));
  async function save(event) {
    event.preventDefault();
    setBusy(true); setError('');
    try {
      const saved = await send(record ? `quote-comparisons/${record.id}` : 'quote-comparisons', record ? 'PUT' : 'POST',
        comparisonPayload(form, record ? { revision: record.revision } : { clientId: form.clientId }), csrfToken);
      onSaved(saved, Boolean(record));
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }
  return <section className="bo-editor bo-analysis-form"><h2>{record ? 'Editar comparación' : 'Nueva comparación de presupuestos'}</h2>
    <form onSubmit={save}>
      <div className="bo-pair">
        {record ? <Field id="bo-cmp-client" label="Cliente"><output id="bo-cmp-client">{nameOf(clients, record.clientId)}</output></Field>
          : <Select id="bo-cmp-client" label="Cliente" value={form.clientId} onChange={value => set({ clientId: value })} options={options(clients)}
            placeholder="Selecciona un cliente" />}
        <Field id="bo-cmp-title" label="Título" hint="Qué se cotiza, por ejemplo “Herbicidas barbecho 2026”.">{hintId =>
          <input id="bo-cmp-title" type="text" required maxLength={200} value={form.title} aria-describedby={hintId}
            onChange={event => set({ title: event.target.value })} />}</Field>
        <Field id="bo-cmp-requested" label="Fecha de solicitud">
          <input id="bo-cmp-requested" type="date" required value={form.requestedOn} onChange={event => set({ requestedOn: event.target.value })} /></Field>
      </div>
      {error && <p className="bo-error" role="alert">{error}</p>}
      <div className="bo-actions"><button type="submit" disabled={busy || (!record && !clients.length)}>
        {busy ? 'Guardando…' : record ? 'Guardar cambios' : 'Crear comparación'}</button>
        <button type="button" onClick={onCancel}>Cancelar</button></div>
    </form></section>;
}

// Add or replace one quote with its lines. Totals follow the API: 4 decimals, rounded half away from zero.
function QuoteForm({ comparison, record, providers, inputs, csrfToken, onSaved, onCancel }) {
  const [form, setForm] = useState(() => record ? quoteFromRecord(record) : emptyQuote());
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const set = changes => setForm(current => ({ ...current, ...changes }));
  const setLine = (key, changes) => setForm(current => ({ ...current, lines: current.lines.map(line => line.key === key ? { ...line, ...changes } : line) }));
  const unitOf = id => inputs.find(input => input.id === id)?.unit ?? '';
  // Suppliers of inputs, plus the quote's current provider if its kind changed since.
  const choices = providers.filter(provider => provider.kind === 'insumos' || provider.id === record?.providerId);

  async function save(event) {
    event.preventDefault();
    setBusy(true); setError('');
    try {
      await send(record ? `quotes/${record.id}` : `quote-comparisons/${comparison.id}/quotes`, record ? 'PUT' : 'POST',
        quotePayload(form, { revision: record?.revision }), csrfToken);
      onSaved(Boolean(record));
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }
  return <section className="bo-editor bo-analysis-form"><h2>{record ? 'Editar presupuesto' : 'Nuevo presupuesto'}</h2>
    <form onSubmit={save}>
      <div className="bo-pair">
        <Select id="bo-quote-provider" label="Proveedor" value={form.providerId} onChange={providerId => set({ providerId })} options={options(choices)}
          placeholder="Selecciona un proveedor" hint={choices.length ? 'Proveedores de tipo Insumos.' : 'Aún no hay proveedores de tipo Insumos.'} />
        <Field id="bo-quote-received" label="Fecha de recepción">
          <input id="bo-quote-received" type="date" required value={form.receivedOn} onChange={event => set({ receivedOn: event.target.value })} /></Field>
        <Select id="bo-quote-currency" label="Moneda" value={form.currency} onChange={currency => set({ currency })} options={currencies}
          hint="Un presupuesto tiene una sola moneda." />
      </div>
      <Field id="bo-quote-notes" label="Notas" optional hint="Condiciones, plazo de pago, flete, IVA.">{hintId =>
        <textarea id="bo-quote-notes" rows={2} maxLength={2000} value={form.notes} aria-describedby={hintId} onChange={event => set({ notes: event.target.value })} />}</Field>
      <fieldset className="bo-group"><legend>Productos cotizados</legend>
        <ol className="bo-lines">{form.lines.map((line, index) => {
          const id = `bo-quote-line-${line.key}`;
          const total = quoteLineTotal(line.quantity, line.unitPrice);
          const chooseInput = inputId => {
            const followsInput = !line.unit.trim() || line.unit === unitOf(line.inputId);
            setLine(line.key, followsInput ? { inputId, unit: unitOf(inputId) } : { inputId });
          };
          return <li key={line.key} className="bo-quote-line">
            <Select id={`${id}-mode`} label={`Producto ${index + 1}`} value={line.mode} onChange={mode => setLine(line.key, { mode })} options={lineModes} />
            {line.mode === 'input'
              ? <InputPicker id={`${id}-input`} label="Insumo" inputs={inputs} value={line.inputId} onChange={chooseInput} />
              : <Field id={`${id}-text`} label="Producto">
                <input id={`${id}-text`} type="text" required maxLength={200} value={line.productText} onChange={event => setLine(line.key, { productText: event.target.value })} /></Field>}
            <Field id={`${id}-quantity`} label="Cantidad">
              <input id={`${id}-quantity`} type="number" inputMode="decimal" min="0.0001" step="0.0001" required value={line.quantity}
                onChange={event => setLine(line.key, { quantity: event.target.value })} /></Field>
            <Field id={`${id}-unit`} label="Unidad" optional={line.mode === 'input'}>
              <input id={`${id}-unit`} type="text" maxLength={40} required={line.mode !== 'input'} value={line.unit}
                onChange={event => setLine(line.key, { unit: event.target.value })} /></Field>
            <Field id={`${id}-price`} label="Precio unitario">
              <input id={`${id}-price`} type="number" inputMode="decimal" min="0" step="0.0001" required value={line.unitPrice}
                onChange={event => setLine(line.key, { unitPrice: event.target.value })} /></Field>
            <div className="bo-readout"><span id={`${id}-total-label`}>Total de la línea</span>
              <output aria-labelledby={`${id}-total-label`}>{total === null ? '—' : formatMoney(total, form.currency)}</output></div>
            <button type="button" className="bo-link" disabled={form.lines.length === 1}
              onClick={() => set({ lines: form.lines.filter(row => row.key !== line.key) })} aria-label={`Quitar producto ${index + 1}`}>Quitar</button>
          </li>;
        })}</ol>
        <div className="bo-section-head"><button type="button" className="bo-link" onClick={() => set({ lines: [...form.lines, newQuoteLine()] })}>Agregar producto</button>
          <p className="bo-total" aria-live="polite">Total del presupuesto: <strong>{formatMoney(quoteTotal(form.lines), form.currency)}</strong></p></div>
      </fieldset>
      {error && <p className="bo-error" role="alert">{error}</p>}
      <div className="bo-actions"><button type="submit" disabled={busy || !choices.length}>{busy ? 'Guardando…' : record ? 'Guardar cambios' : 'Agregar presupuesto'}</button>
        <button type="button" onClick={onCancel}>Cancelar</button></div>
    </form></section>;
}

// One row per product, one column per quote; the cheapest offer per currency comes from the API (cheapestQuoteIdByCurrency).
function ComparisonTable({ detail, providers }) {
  const table = comparisonTable(detail, providers);
  if (!table.rows.length) return <p className="bo-empty">Los presupuestos todavía no tienen productos para comparar.</p>;
  const heading = column => `${column.provider} · ${column.currency}`;
  return <>
    {table.note && <p className="bo-warning" role="note">{table.note}</p>}
    <div className="bo-table-scroll"><table className="bo-table bo-compare">
      <caption className="bo-sr">Precio unitario de cada producto por presupuesto</caption>
      <thead><tr><th scope="col">Producto</th>{table.columns.map(column => <th key={column.quoteId} scope="col" className={column.chosen ? 'bo-chosen' : undefined}>
        {column.provider}<small>{displayDate(column.receivedOn)} · {column.currency}{column.chosen && ' · elegido'}</small></th>)}</tr></thead>
      <tbody>{table.rows.map(row => <tr key={row.key}>
        <th scope="row" data-label="Producto">{row.label}</th>
        {row.cells.map((cell, index) => {
          const column = table.columns[index];
          return <td key={cell.quoteId} data-label={heading(column)} className={[cell.cheapest && 'bo-cheapest', column.chosen && 'bo-chosen'].filter(Boolean).join(' ') || undefined}>
            {!cell.offers.length ? <span className="bo-muted">No cotiza</span> : <>
              {cell.offers.map((offer, position) => <span key={position} className="bo-offer">
                <strong>{formatMoney(offer.unitPrice, offer.currency)}</strong> / {offer.unit}
                <small>{formatAmount(offer.quantity)} {offer.unit} · {formatMoney(offer.lineTotal, offer.currency)}</small></span>)}
              {cell.cheapest && <span className="bo-badge">Más barato en {column.currency}</span>}</>}
          </td>;
        })}</tr>)}</tbody>
      <tfoot><tr><th scope="row" data-label="Total">Total</th>{table.columns.map(column =>
        <td key={column.quoteId} data-label={heading(column)} className={column.chosen ? 'bo-chosen' : undefined}><strong>{formatMoney(column.total, column.currency)}</strong></td>)}</tr></tfoot>
    </table></div>
  </>;
}

// Pick the chosen quote and say why (S3). After a reopen the previous choice and reason are suggested.
function DecideForm({ detail, providers, csrfToken, onSaved }) {
  const { comparison, quotes } = detail;
  const [form, setForm] = useState(() => ({
    quoteId: quotes.some(quote => quote.id === comparison.chosenQuoteId) ? comparison.chosenQuoteId : '', reason: comparison.decisionReason ?? '',
  }));
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function decide(event) {
    event.preventDefault();
    setBusy(true); setError('');
    try {
      await send(`quote-comparisons/${comparison.id}/decide`, 'POST', decidePayload(form, comparison.revision), csrfToken);
      onSaved();
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }
  return <section className="bo-editor bo-analysis-form" aria-label="Decidir"><h2>Decidir</h2>
    <form onSubmit={decide}>
      <Select id="bo-decide-quote" label="Presupuesto elegido" value={form.quoteId} onChange={quoteId => setForm(current => ({ ...current, quoteId }))}
        placeholder="Selecciona un presupuesto" options={quotes.map(quote => ({
          value: quote.id, label: `${nameOf(providers, quote.providerId)} · ${displayDate(quote.receivedOn)} · ${formatMoney(quote.total, quote.currency)}`,
        }))} />
      <Field id="bo-decide-reason" label="Motivo de la elección" hint="Por qué conviene: precio, plazo, entrega, financiación.">{hintId =>
        <textarea id="bo-decide-reason" rows={3} required maxLength={2000} value={form.reason} aria-describedby={hintId}
          onChange={event => setForm(current => ({ ...current, reason: event.target.value }))} />}</Field>
      {error && <p className="bo-error" role="alert">{error}</p>}
      <div className="bo-actions"><button type="submit" disabled={busy || !form.quoteId || !form.reason.trim()}>{busy ? 'Guardando…' : 'Decidir'}</button></div>
    </form></section>;
}

function ComparisonDetail({ id, refs, csrfToken, onBack, onChanged }) {
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  // editing: null | { kind: 'comparison' } | { kind: 'quote', record }
  const [editing, setEditing] = useState(null);
  const [busy, setBusy] = useState(false);
  const load = useCallback(() => api(`quote-comparisons/${id}`).then(found => { setDetail(found); setError(''); }).catch(cause => setError(cause.message)), [id]);
  useEffect(() => { load(); }, [load]);

  function changed(message) { setEditing(null); setNotice(message); load(); onChanged(); }
  async function reopen() {
    setBusy(true); setError(''); setNotice('');
    try {
      await send(`quote-comparisons/${id}/reopen`, 'POST', { revision: detail.comparison.revision }, csrfToken);
      changed('Comparación reabierta: puedes cambiar los presupuestos y volver a decidir.');
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }

  const back = <button type="button" className="bo-link" onClick={onBack}>← Volver al listado</button>;
  if (!detail) return <section className="bo-list">{back}{error ? <p className="bo-error" role="alert">{error}</p> : <p role="status">Cargando comparación…</p>}</section>;
  const { comparison, quotes } = detail;
  const open = comparison.status === 'abierta';
  const chosen = quotes.find(quote => quote.id === comparison.chosenQuoteId);
  const chosenLabel = chosen && `${nameOf(refs.providers, chosen.providerId)} · ${formatMoney(chosen.total, chosen.currency)}`;
  return <div className="bo-stack">
    <div className="bo-section-head">{back}<div>
      {open && <button type="button" onClick={() => { setNotice(''); setEditing({ kind: 'comparison' }); }}>Editar</button>}
      {!open && <button type="button" disabled={busy} onClick={reopen}>{busy ? 'Reabriendo…' : 'Reabrir'}</button>}
      <button type="button" onClick={() => { setNotice(''); load(); }}>Actualizar</button></div></div>
    {notice && <p className="bo-notice" role="status">{notice}</p>}
    {error && <p className="bo-error" role="alert">{error}</p>}
    <RecordDetail title={comparison.title} items={[
      ['Cliente', nameOf(refs.clients, comparison.clientId)], ['Fecha de solicitud', displayDate(comparison.requestedOn)],
      ['Estado', <Status status={comparison.status} />],
      ...(open ? [] : [['Presupuesto elegido', chosenLabel], ['Motivo de la elección', comparison.decisionReason]]),
    ]} />
    {open && chosen && <p className="bo-context">Decisión anterior: {chosenLabel}. Motivo: {comparison.decisionReason}</p>}
    {editing?.kind === 'comparison' && <ComparisonForm record={comparison} clients={refs.clients} csrfToken={csrfToken}
      onCancel={() => setEditing(null)} onSaved={() => changed('Comparación actualizada.')} />}
    <section className="bo-list" aria-label="Presupuestos recibidos">
      <div className="bo-section-head"><h2>Presupuestos</h2><div>
        {open && <button type="button" onClick={() => { setNotice(''); setEditing({ kind: 'quote', record: null }); }}>Agregar presupuesto</button>}</div></div>
      {!quotes.length ? <p className="bo-empty">Aún no hay presupuestos en esta comparación.{open && ' Agrega el primero con “Agregar presupuesto”.'}</p> :
        <table className="bo-table"><thead><tr><th>Proveedor</th><th>Fecha de recepción</th><th>Moneda</th><th>Total</th><th><span className="bo-sr">Acciones</span></th></tr></thead>
          <tbody>{quotes.map(quote => <tr key={quote.id} className={quote.id === chosen?.id && !open ? 'bo-chosen' : undefined}>
            <td data-label="Proveedor">{nameOf(refs.providers, quote.providerId)}{quote.id === chosen?.id && !open && <span className="bo-badge">Elegido</span>}
              {quote.notes && <span className="bo-cell-note">{quote.notes}</span>}</td>
            <td data-label="Fecha de recepción">{displayDate(quote.receivedOn)}</td>
            <td data-label="Moneda">{quote.currency}</td>
            <td data-label="Total">{formatMoney(quote.total, quote.currency)}</td>
            <td data-label="Acciones">{open ? <button type="button" className="bo-link" onClick={() => { setNotice(''); setEditing({ kind: 'quote', record: quote }); }}
              aria-label={`Editar presupuesto de ${nameOf(refs.providers, quote.providerId)}`}>Editar</button> : <span className="bo-muted">Solo lectura</span>}</td>
          </tr>)}</tbody></table>}
    </section>
    {editing?.kind === 'quote' && <QuoteForm key={editing.record?.id ?? 'new'} comparison={comparison} record={editing.record} providers={refs.providers}
      inputs={refs.inputs} csrfToken={csrfToken} onCancel={() => setEditing(null)}
      onSaved={edited => changed(edited ? 'Presupuesto actualizado.' : 'Presupuesto agregado.')} />}
    <section className="bo-list" aria-label="Comparación por producto"><h2>Comparación por producto</h2>
      <ComparisonTable detail={detail} providers={refs.providers} /></section>
    {open && quotes.length > 0 && <DecideForm key={comparison.revision} detail={detail} providers={refs.providers} csrfToken={csrfToken}
      onSaved={() => changed('Comparación decidida.')} />}
  </div>;
}

/** The client's quote comparisons on the client page, each opening in the comparison section (S3). */
export function ClientComparisons({ client, navigate }) {
  const [rows, setRows] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    api(comparisonQuery({ clientId: client.id })).then(list => { if (active) setRows(list); }).catch(cause => { if (active) setError(cause.message); });
    return () => { active = false; };
  }, [client.id]);
  return <section className="bo-list" aria-label="Presupuestos del cliente">
    <div className="bo-section-head"><h2>Presupuestos</h2><div>
      <button type="button" onClick={() => navigate('quote-comparisons', { clientId: client.id })}>Ver comparaciones del cliente</button></div></div>
    {error ? <p className="bo-error" role="alert">{error}</p> : !rows ? <p role="status">Cargando comparaciones…</p> :
      !rows.length ? <p className="bo-empty">{client.name} aún no tiene comparaciones de presupuestos.</p> :
        <ul>{rows.slice(0, 5).map(row => <li key={row.id}>
          <button type="button" className="bo-row" onClick={() => navigate('quote-comparisons', { clientId: client.id, comparisonId: row.id })}>
            <strong>{row.title}</strong><small>{displayDate(row.requestedOn)} · {optionLabel(comparisonStatuses, row.status)}</small></button></li>)}</ul>}
    {rows?.length > 5 && <p className="bo-list-note">Se muestran las 5 más recientes de {rows.length}.</p>}
  </section>;
}

const references = ['clients', 'providers', 'inputs'];

// Quote comparisons (S3): list by client and status, create, detail with quotes, comparison table and the decision.
export default function QuoteComparisonsSection({ identity, initial = {} }) {
  const [refs, setRefs] = useState(() => Object.fromEntries(references.map(key => [key, []])));
  const [rows, setRows] = useState([]);
  const [filters, setFilters] = useState({ clientId: initial.clientId ?? '', status: '' });
  // view: { mode: 'list' } | { mode: 'new' } | { mode: 'detail', id }
  const [view, setView] = useState(initial.comparisonId ? { mode: 'detail', id: initial.comparisonId } : { mode: 'list' });
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const request = useRef(0);

  const loadRefs = useCallback(async () => {
    try { setRefs(Object.fromEntries(await Promise.all(references.map(async key => [key, await api(key)])))); }
    catch (cause) { setError(cause.message); }
  }, []);
  // Only the latest request updates the list, so a slow earlier filter cannot overwrite a newer one.
  const loadRows = useCallback(async () => {
    const current = ++request.current;
    try {
      const list = await api(comparisonQuery(filters));
      if (current === request.current) { setRows(list); setError(''); }
    } catch (cause) { if (current === request.current) setError(cause.message); }
  }, [filters]);
  useEffect(() => { loadRefs(); }, [loadRefs]);
  useEffect(() => { loadRows(); }, [loadRows]);

  const show = next => { setView(next); setNotice(''); setError(''); };
  return <>
    <SectionHeader identity={identity} title="Comparación de presupuestos" count={view.mode === 'list' ? rows.length : undefined}
      description="Presupuestos de distintos proveedores para un cliente: compara precios por producto, elige uno y deja el motivo." />
    {notice && <p className="bo-notice" role="status">{notice}</p>}
    {error && <p className="bo-error" role="alert">{error}</p>}
    {view.mode === 'new' && <ComparisonForm clients={refs.clients} clientId={filters.clientId} csrfToken={identity.csrfToken}
      onCancel={() => show({ mode: 'list' })} onSaved={saved => { loadRows(); show({ mode: 'detail', id: saved.id }); setNotice('Comparación creada. Agrega los presupuestos recibidos.'); }} />}
    {view.mode === 'detail' && <ComparisonDetail key={view.id} id={view.id} refs={refs} csrfToken={identity.csrfToken}
      onBack={() => show({ mode: 'list' })} onChanged={loadRows} />}
    {view.mode === 'list' && <section className="bo-list" aria-label="Listado de comparaciones de presupuestos">
      <div className="bo-section-head"><h2>Comparaciones</h2><div>
        <button type="button" onClick={() => show({ mode: 'new' })}>Nueva comparación</button>
        <button type="button" onClick={() => { setNotice(''); loadRows(); loadRefs(); }}>Actualizar</button></div></div>
      <div className="bo-filters">
        <Select id="bo-cmp-filter-client" label="Cliente" required={false} value={filters.clientId} options={options(refs.clients)} placeholder="Todos"
          onChange={clientId => setFilters(current => ({ ...current, clientId }))} />
        <Select id="bo-cmp-filter-status" label="Estado" required={false} value={filters.status} options={comparisonStatuses} placeholder="Todos"
          onChange={status => setFilters(current => ({ ...current, status }))} />
      </div>
      {!rows.length ? <p className="bo-empty">{filters.clientId || filters.status ? 'Ninguna comparación coincide con los filtros.' : 'Aún no hay comparaciones. Crea la primera con “Nueva comparación”.'}</p> :
        <table className="bo-table"><thead><tr><th>Título</th><th>Cliente</th><th>Fecha de solicitud</th><th>Estado</th></tr></thead>
          <tbody>{rows.map(row => <tr key={row.id}>
            <td data-label="Título"><button type="button" className="bo-link" onClick={() => show({ mode: 'detail', id: row.id })}>{row.title}</button></td>
            <td data-label="Cliente">{nameOf(refs.clients, row.clientId)}</td>
            <td data-label="Fecha de solicitud">{displayDate(row.requestedOn)}</td>
            <td data-label="Estado"><Status status={row.status} /></td></tr>)}</tbody></table>}
    </section>}
  </>;
}
