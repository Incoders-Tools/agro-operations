'use client';

import { useCallback, useEffect, useState } from 'react';
import { api, send } from './api.mjs';
import { Field } from './field.jsx';
import { RecordDetail, SectionHeader } from './records.jsx';
import { optionLabel, rowLabel, searchRecords } from './schema.mjs';
import {
  displayDate, doseRange, emptyOrder, exceedsPlot, lineTotal, newLine, orderFromRecord, orderLabel, orderPayload, plotCropPrefill,
  statuses, workedTotal,
} from './work-orders.mjs';

// Catalogs the list, form and detail resolve ids against; firms come back with their next work order number.
const references = ['commercial-firms', 'clients', 'fields', 'labor-types', 'stages', 'contractors', 'campaigns', 'crops', 'inputs'];
const byId = (rows, id) => rows.find(row => row.id === id);
const laborLabel = labor => labor ? (labor.abbreviation ? `${labor.name} (${labor.abbreviation})` : labor.name) : 'Sin referencia';
const nameOf = (rows, id) => byId(rows, id)?.name;

function Readout({ label, children }) {
  return <div className="bo-readout"><span>{label}</span><output>{children}</output></div>;
}

function Select({ id, label, value, onChange, options, placeholder, optional, hint, required = !optional, disabled }) {
  return <Field id={id} label={label} optional={optional} hint={hint}>{hintId =>
    <select id={id} value={value} required={required} disabled={disabled} aria-describedby={hintId} onChange={event => onChange(event.target.value)}>
      <option value="">{placeholder}</option>
      {options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>}</Field>;
}

function doseWarning(range, input) {
  if (!range) return null;
  const unit = `${input.unit}/ha`;
  return range === 'below' ? `La dosis está por debajo de la mínima del insumo (${input.doseMin} ${unit}).`
    : `La dosis está por encima de la máxima del insumo (${input.doseMax} ${unit}).`;
}

// Alta and edit of one OT, in the order of the workbook `Orden` sheet. The number always comes from the server.
function OrderForm({ record, refs, csrfToken, onSaved, onCancel }) {
  const [form, setForm] = useState(() => record ? orderFromRecord(record) : emptyOrder());
  const [fieldPlots, setFieldPlots] = useState([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const firm = byId(refs['commercial-firms'], form.firmId);
  const client = firm && byId(refs.clients, firm.clientId);
  const set = changes => setForm(current => ({ ...current, ...changes }));

  useEffect(() => {
    let active = true;
    if (!form.fieldId) { setFieldPlots([]); return undefined; }
    api(`plots?fieldId=${form.fieldId}`).then(rows => { if (active) setFieldPlots(rows); }).catch(cause => { if (active) setError(cause.message); });
    return () => { active = false; };
  }, [form.fieldId]);

  // With a campaign and a single plot, the crop and variety start from that plot's first crop of the campaign.
  function prefill(next) {
    if (!next.campaignId || next.plots.length !== 1) return;
    const { plotId } = next.plots[0];
    const { campaignId } = next;
    api(`plot-crops?plotId=${plotId}`).then(rows => {
      const crop = plotCropPrefill(rows, campaignId);
      if (crop) setForm(current => current.campaignId === campaignId && current.plots.length === 1 && current.plots[0].plotId === plotId
        ? { ...current, ...crop } : current);
    }).catch(cause => setError(cause.message));
  }
  function chooseFirm(firmId) {
    const clientId = byId(refs['commercial-firms'], firmId)?.clientId;
    const keepsField = byId(refs.fields, form.fieldId)?.clientId === clientId;
    set(keepsField ? { firmId } : { firmId, fieldId: '', plots: [] });
  }
  function togglePlot(plot, checked) {
    const plots = checked ? [...form.plots, { plotId: plot.id, worked: String(plot.areaHectares) }] : form.plots.filter(row => row.plotId !== plot.id);
    set({ plots }); prefill({ ...form, plots });
  }
  const setWorked = (plotId, worked) => set({ plots: form.plots.map(row => row.plotId === plotId ? { ...row, worked } : row) });
  const setLine = (key, changes) => set({ lines: form.lines.map(line => line.key === key ? { ...line, ...changes } : line) });

  async function save(event) {
    event.preventDefault();
    setError('');
    if (!form.plots.length) { setError('Selecciona al menos un lote.'); return; }
    setBusy(true);
    try {
      const saved = await send(record ? `work-orders/${record.id}` : 'work-orders', record ? 'PUT' : 'POST',
        orderPayload(form, { revision: record?.revision }), csrfToken);
      onSaved(saved, Boolean(record));
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }

  const options = (rows, label = row => row.name) => rows.map(row => ({ value: row.id, label: label(row) }));
  const firmOptions = options(refs['commercial-firms'], row => `${row.name} · ${nameOf(refs.clients, row.clientId) ?? 'Sin cliente'}`);
  const fields = firm ? refs.fields.filter(row => row.clientId === firm.clientId) : [];
  const total = workedTotal(form.plots);
  const ready = refs['commercial-firms'].length && refs['labor-types'].length && refs.contractors.length;

  return <section className="bo-editor bo-order-form"><h2>{record ? `Editar orden ${orderLabel(firm, record.number)}` : 'Nueva orden de trabajo'}</h2>
    <form onSubmit={save}>
      {record ? <p className="bo-context">Firma {firm?.name ?? 'Sin referencia'} · cliente {client?.name ?? 'Sin referencia'}. La firma y el número no cambian.</p>
        : <Select id="bo-wo-firm" label="Firma comercial" value={form.firmId} onChange={chooseFirm} options={firmOptions}
          placeholder="Selecciona una firma" hint={firm ? `Cliente ${client?.name ?? 'sin referencia'}. Se emitirá con el Nº ${firm.nextWorkOrderNumber}.` : undefined} />}
      <Field id="bo-wo-date" label="Fecha">
        <input id="bo-wo-date" type="date" value={form.issuedOn} required onChange={event => set({ issuedOn: event.target.value })} /></Field>
      <Select id="bo-wo-labor" label="Labor" value={form.laborTypeId} onChange={laborTypeId => set({ laborTypeId })}
        options={options(refs['labor-types'], laborLabel)} placeholder="Selecciona una labor" />
      <Select id="bo-wo-stage" label="Etapa" optional value={form.stageId} onChange={stageId => set({ stageId })}
        options={options(refs.stages)} placeholder="Sin indicar" />
      <Select id="bo-wo-contractor" label="Contratista" value={form.contractorId} onChange={contractorId => set({ contractorId })}
        options={options(refs.contractors)} placeholder="Selecciona un contratista" />
      <Select id="bo-wo-field" label="Campo" value={form.fieldId} disabled={!firm} onChange={fieldId => set({ fieldId, plots: [] })}
        options={options(fields)} placeholder={firm ? 'Selecciona un campo' : 'Primero elige la firma'}
        hint={firm && !fields.length ? 'El cliente de esta firma aún no tiene campos.' : undefined} />

      <fieldset className="bo-group"><legend>Lotes</legend>
        {!form.fieldId ? <p className="bo-muted">Elige un campo para ver sus lotes.</p> : !fieldPlots.length ? <p className="bo-muted">Este campo aún no tiene lotes.</p> :
          <div className="bo-plots">{fieldPlots.map(plot => {
            const chosen = form.plots.find(row => row.plotId === plot.id);
            const id = `bo-wo-plot-${plot.id}`;
            return <div key={plot.id} className={chosen ? 'bo-plot bo-plot-on' : 'bo-plot'}>
              <Field id={id} label={rowLabel('plots', plot)} hint={`Superficie ${plot.areaHectares} ha`} check>{hintId =>
                <input id={id} type="checkbox" checked={Boolean(chosen)} aria-describedby={hintId} onChange={event => togglePlot(plot, event.target.checked)} />}</Field>
              {chosen && <>
                <Field id={`${id}-worked`} label="ha a trabajar">
                  <input id={`${id}-worked`} type="number" min="0.0001" max="99999999.9999" step="0.0001" required value={chosen.worked}
                    onChange={event => setWorked(plot.id, event.target.value)} /></Field>
                {exceedsPlot(chosen.worked, plot.areaHectares) && <p className="bo-warning" role="status">⚠ Supera la superficie del lote ({plot.areaHectares} ha).</p>}
              </>}
            </div>;
          })}</div>}
        <p className="bo-total">Total a trabajar: <strong>{total} ha</strong></p>
      </fieldset>

      <Select id="bo-wo-campaign" label="Campaña" optional value={form.campaignId} options={options(refs.campaigns)} placeholder="Sin indicar"
        onChange={campaignId => { set({ campaignId }); prefill({ ...form, campaignId }); }}
        hint="Con una campaña y un solo lote, el cultivo se completa con el primer cultivo del lote." />
      <Select id="bo-wo-crop" label="Cultivo" optional value={form.cropId} onChange={cropId => set({ cropId })}
        options={options(refs.crops)} placeholder="Sin indicar" />
      <Field id="bo-wo-variety" label="Variedad o híbrido" optional>
        <input id="bo-wo-variety" type="text" maxLength={200} value={form.variety} onChange={event => set({ variety: event.target.value })} /></Field>

      <fieldset className="bo-group"><legend>Insumos</legend>
        {!form.lines.length && <p className="bo-muted">Sin insumos.</p>}
        <ol className="bo-lines">{form.lines.map((line, index) => {
          const input = byId(refs.inputs, line.inputId);
          const found = searchRecords('inputs', refs.inputs, line.term);
          const shown = input && !found.includes(input) ? [input, ...found] : found;
          const lineTotalValue = input ? lineTotal(line.dose, total) : null;
          const warning = doseWarning(input && doseRange(line.dose, input), input);
          const id = `bo-wo-line-${line.key}`;
          return <li key={line.key} className="bo-line">
            <Field id={`${id}-search`} label="Buscar insumo" optional>
              <input id={`${id}-search`} type="search" value={line.term} onChange={event => setLine(line.key, { term: event.target.value })} /></Field>
            <Select id={`${id}-input`} label={`Insumo ${index + 1}`} value={line.inputId} onChange={inputId => setLine(line.key, { inputId })}
              options={options(shown, row => rowLabel('inputs', row))} placeholder={found.length ? 'Selecciona un insumo' : 'Sin coincidencias'} />
            <Field id={`${id}-dose`} label="Dosis por ha">
              <input id={`${id}-dose`} type="number" min="0.0001" max="99999999.9999" step="0.0001" required={Boolean(line.inputId)} value={line.dose}
                onChange={event => setLine(line.key, { dose: event.target.value })} /></Field>
            <Readout label="Unidad">{input?.unit ?? '—'}</Readout>
            <Readout label="Total">{lineTotalValue ? `${lineTotalValue} ${input.unit}` : '—'}</Readout>
            <button type="button" className="bo-link" onClick={() => set({ lines: form.lines.filter(row => row.key !== line.key) })}
              aria-label={`Quitar insumo ${index + 1}`}>Quitar</button>
            {warning && <p className="bo-warning" role="status">⚠ {warning}</p>}
          </li>;
        })}</ol>
        <button type="button" className="bo-link" onClick={() => set({ lines: [...form.lines, newLine()] })}>Agregar insumo</button>
      </fieldset>

      <Field id="bo-wo-notes" label="Observaciones" optional>
        <textarea id="bo-wo-notes" rows={3} maxLength={2000} value={form.observations} onChange={event => set({ observations: event.target.value })} /></Field>
      <Field id="bo-wo-engineer" label="Ingeniero responsable" optional>
        <input id="bo-wo-engineer" type="text" maxLength={200} value={form.responsibleEngineer} onChange={event => set({ responsibleEngineer: event.target.value })} /></Field>
      {error && <p className="bo-error" role="alert">{error}</p>}
      {!ready && <p>Primero crea firmas comerciales, labores y contratistas.</p>}
      <div className="bo-actions"><button type="submit" disabled={busy || !ready}>{busy ? 'Guardando…' : record ? 'Guardar cambios' : 'Emitir orden'}</button>
        <button type="button" onClick={onCancel}>Cancelar</button></div>
    </form></section>;
}

// One OT with its plots and lines; emitida orders can be edited, completed or cancelled with a reason.
function OrderDetail({ id, refs, csrfToken, onEdit, onBack, onChanged }) {
  const [order, setOrder] = useState(null);
  const [plots, setPlots] = useState([]);
  const [action, setAction] = useState(null);
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const loaded = await api(`work-orders/${id}`);
      setOrder(loaded);
      setPlots(await api(`plots?fieldId=${loaded.fieldId}`));
    } catch (cause) { setError(cause.message); }
  }, [id]);
  useEffect(() => { load(); }, [load]);

  async function close(event) {
    event.preventDefault();
    setBusy(true); setError('');
    try {
      const body = action === 'cancel' ? { revision: order.revision, reason } : { revision: order.revision };
      const saved = await send(`work-orders/${id}/${action === 'cancel' ? 'cancel' : 'complete'}`, 'POST', body, csrfToken);
      setOrder(saved); setAction(null); setReason(''); onChanged();
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }

  if (!order) return <section className="bo-list"><button type="button" className="bo-link" onClick={onBack}>← Volver al listado</button>
    {error ? <p className="bo-error" role="alert">{error}</p> : <p role="status">Cargando orden…</p>}</section>;
  const firm = byId(refs['commercial-firms'], order.firmId);
  const issued = order.status === 'emitida';
  return <div className="bo-stack">
    <div className="bo-section-head"><button type="button" className="bo-link" onClick={onBack}>← Volver al listado</button>
      {issued && <div><button type="button" onClick={() => onEdit(order)}>Editar</button>
        <button type="button" onClick={() => { setAction('complete'); setError(''); }}>Marcar realizada</button>
        <button type="button" onClick={() => { setAction('cancel'); setError(''); }}>Cancelar orden</button></div>}</div>
    {error && <p className="bo-error" role="alert">{error}</p>}
    {action && <section className="bo-editor"><h2>{action === 'cancel' ? 'Cancelar orden' : 'Marcar como realizada'}</h2>
      <form onSubmit={close}>
        {action === 'cancel' ? <Field id="bo-wo-reason" label="Motivo de la cancelación" hint="La orden conserva su número y sigue en el listado.">{hintId =>
          <textarea id="bo-wo-reason" rows={3} maxLength={2000} required value={reason} aria-describedby={hintId} onChange={event => setReason(event.target.value)} />}</Field>
          : <p>La orden {orderLabel(firm, order.number)} pasará a Realizada y ya no se podrá editar.</p>}
        <div className="bo-actions"><button type="submit" disabled={busy}>{busy ? 'Guardando…' : action === 'cancel' ? 'Confirmar cancelación' : 'Confirmar realizada'}</button>
          <button type="button" onClick={() => setAction(null)}>Volver</button></div>
      </form></section>}
    <RecordDetail title={`Orden ${orderLabel(firm, order.number)}`} items={[
      ['Estado', optionLabel(statuses, order.status)], ['Fecha', displayDate(order.issuedOn)],
      ['Firma comercial', firm?.name], ['Cliente', firm && nameOf(refs.clients, firm.clientId)],
      ['Labor', laborLabel(byId(refs['labor-types'], order.laborTypeId))], ['Etapa', nameOf(refs.stages, order.stageId)],
      ['Contratista', nameOf(refs.contractors, order.contractorId)], ['Campo', nameOf(refs.fields, order.fieldId)],
      ['Campaña', nameOf(refs.campaigns, order.campaignId)], ['Cultivo', nameOf(refs.crops, order.cropId)], ['Variedad o híbrido', order.variety],
      ['Ingeniero responsable', order.responsibleEngineer], ['Observaciones', order.observations],
      ...(order.status === 'cancelada' ? [['Motivo de la cancelación', order.cancelReason]] : []),
    ]} />
    <section className="bo-list"><h2>Lotes</h2>
      <table className="bo-table"><thead><tr><th>Lote</th><th>Superficie</th><th>ha a trabajar</th></tr></thead>
        <tbody>{order.plots.map(row => {
          const plot = byId(plots, row.plotId);
          return <tr key={row.plotId}><td data-label="Lote">{plot ? rowLabel('plots', plot) : 'Sin referencia'}</td>
            <td data-label="Superficie">{row.areaHectares} ha</td>
            <td data-label="ha a trabajar">{row.workedAreaHectares} ha
              {row.workedAreaExceedsPlot && <span className="bo-warning-inline">⚠ Supera la superficie del lote</span>}</td></tr>;
        })}</tbody></table>
      <p className="bo-total">Total a trabajar: <strong>{workedTotal(order.plots.map(row => ({ worked: row.workedAreaHectares })))} ha</strong></p>
    </section>
    <section className="bo-list"><h2>Insumos</h2>
      {!order.lines.length ? <p className="bo-empty">Esta orden no tiene insumos.</p> :
        <table className="bo-table"><thead><tr><th>Nº</th><th>Insumo</th><th>Dosis por ha</th><th>Total</th></tr></thead>
          <tbody>{order.lines.map(line => <tr key={line.id}><td data-label="Nº">{line.position}</td>
            <td data-label="Insumo">{byId(refs.inputs, line.inputId)?.commercialName ?? 'Sin referencia'}</td>
            <td data-label="Dosis por ha">{line.dosePerHectare} {line.unit}/ha
              {line.doseOutOfRange && <span className="bo-warning-inline">⚠ Fuera del rango de dosis del insumo</span>}</td>
            <td data-label="Total">{line.total} {line.unit}</td></tr>)}</tbody></table>}
    </section>
  </div>;
}

// Work orders (S5): list with firm and status filters, alta, detail, edit, complete and cancel.
export default function WorkOrdersSection({ identity }) {
  const [refs, setRefs] = useState(() => Object.fromEntries(references.map(key => [key, []])));
  const [orders, setOrders] = useState([]);
  const [filters, setFilters] = useState({ firmId: '', status: '' });
  // view: { mode: 'list' } | { mode: 'detail', id } | { mode: 'form', record }
  const [view, setView] = useState({ mode: 'list' });
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');

  const loadRefs = useCallback(async () => {
    try { setRefs(Object.fromEntries(await Promise.all(references.map(async key => [key, await api(key)])))); }
    catch (cause) { setError(cause.message); }
  }, []);
  const loadOrders = useCallback(async () => {
    const query = new URLSearchParams(Object.entries(filters).filter(([, value]) => value)).toString();
    try { setOrders(await api(query ? `work-orders?${query}` : 'work-orders')); setError(''); }
    catch (cause) { setError(cause.message); }
  }, [filters]);
  useEffect(() => { loadRefs(); }, [loadRefs]);
  useEffect(() => { loadOrders(); }, [loadOrders]);

  function saved(order, edited) {
    const firm = byId(refs['commercial-firms'], order.firmId);
    setNotice(`Orden ${orderLabel(firm, order.number)} ${edited ? 'actualizada' : 'emitida'}.`);
    setView({ mode: 'detail', id: order.id });
    loadOrders(); loadRefs();
  }
  const show = next => { setView(next); setNotice(''); setError(''); };

  const firmOptions = refs['commercial-firms'].map(row => ({ value: row.id, label: row.name }));
  return <>
    <SectionHeader identity={identity} title="Órdenes de trabajo" count={view.mode === 'list' ? orders.length : undefined}
      description="Cada orden se numera por firma comercial. Una orden cancelada conserva su número y sigue en el listado." />
    {notice && <p className="bo-notice" role="status">{notice}</p>}
    {error && <p className="bo-error" role="alert">{error}</p>}
    {view.mode === 'form' && <OrderForm key={view.record?.id ?? 'new'} record={view.record} refs={refs} csrfToken={identity.csrfToken}
      onSaved={saved} onCancel={() => show(view.record ? { mode: 'detail', id: view.record.id } : { mode: 'list' })} />}
    {view.mode === 'detail' && <OrderDetail key={view.id} id={view.id} refs={refs} csrfToken={identity.csrfToken}
      onBack={() => show({ mode: 'list' })} onEdit={record => show({ mode: 'form', record })} onChanged={loadOrders} />}
    {view.mode === 'list' && <section className="bo-list" aria-label="Listado de órdenes de trabajo">
      <div className="bo-section-head"><h2>Órdenes</h2><div>
        <button type="button" onClick={() => show({ mode: 'form', record: null })}>Nueva orden</button>
        <button type="button" onClick={() => { setNotice(''); loadOrders(); loadRefs(); }}>Actualizar</button></div></div>
      <div className="bo-filters">
        <Select id="bo-wo-filter-firm" label="Firma" required={false} value={filters.firmId} options={firmOptions} placeholder="Todas"
          onChange={firmId => setFilters(current => ({ ...current, firmId }))} />
        <Select id="bo-wo-filter-status" label="Estado" required={false} value={filters.status} options={statuses} placeholder="Todos"
          onChange={status => setFilters(current => ({ ...current, status }))} />
      </div>
      {!orders.length ? <p className="bo-empty">{filters.firmId || filters.status ? 'Ninguna orden coincide con los filtros.' : 'Aún no hay órdenes de trabajo. Emite la primera con “Nueva orden”.'}</p> :
        <table className="bo-table bo-orders"><thead><tr><th>Nº</th><th>Fecha</th><th>Firma</th><th>Labor</th><th>Campo</th><th>Estado</th></tr></thead>
          <tbody>{orders.map(order => {
            const firm = byId(refs['commercial-firms'], order.firmId);
            return <tr key={order.id}>
              <td data-label="Nº"><button type="button" className="bo-link" onClick={() => show({ mode: 'detail', id: order.id })}>{orderLabel(firm, order.number)}</button></td>
              <td data-label="Fecha">{displayDate(order.issuedOn)}</td>
              <td data-label="Firma">{firm?.name ?? 'Sin referencia'}</td>
              <td data-label="Labor">{laborLabel(byId(refs['labor-types'], order.laborTypeId))}</td>
              <td data-label="Campo">{nameOf(refs.fields, order.fieldId) ?? 'Sin referencia'}</td>
              <td data-label="Estado"><span className={`bo-status bo-status-${order.status}`}>{optionLabel(statuses, order.status)}</span></td>
            </tr>;
          })}</tbody></table>}
    </section>}
  </>;
}
