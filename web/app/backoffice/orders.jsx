'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { api, send } from './api.mjs';
import { ComboField, Field, optionNodes, useFocusAfterRender } from './field.jsx';
import { Busy, Loading, RecordDetail, SectionHeader, usePending } from './records.jsx';
import { fieldPlotCount, optionLabel, rowLabel } from './schema.mjs';
import {
  clientFirms, displayDate, doseRange, emptyOrder, exceedsPlot, fieldNames, laborLabel, laborOptionGroups, lineTotal, newLine, orderFromRecord,
  orderLabel, orderPayload, orderQuery, plotCropPrefill, plotGroups, statuses, workedTotal,
} from './work-orders.mjs';

// Catalogs the list, form and detail resolve ids against; firms come back with their next work order number.
const references = ['commercial-firms', 'clients', 'fields', 'labor-types', 'stages', 'contractors', 'campaigns', 'crops', 'inputs'];
const byId = (rows, id) => rows.find(row => row.id === id);
const nameOf = (rows, id) => byId(rows, id)?.name;
const printHref = id => `/backoffice/work-orders/${encodeURIComponent(id)}/print`;

// Opens the print view in a new tab, so the list or detail keeps its state.
function PrintLink({ id, label }) {
  return <a className="bo-print-link" href={printHref(id)} target="_blank" rel="noopener" aria-label={label}>Imprimir / PDF</a>;
}

function Readout({ label, children }) {
  return <div className="bo-readout"><span>{label}</span><output>{children}</output></div>;
}

// Native select, only for the fixed status list; entities use ComboField (S2).
function Select({ id, label, value, onChange, options, placeholder, optional, hint, required = !optional }) {
  return <Field id={id} label={label} optional={optional} hint={hint}>{hintId =>
    <select id={id} value={value} required={required} aria-describedby={hintId} onChange={event => onChange(event.target.value)}>
      <option value="">{placeholder}</option>
      {optionNodes(options)}
    </select>}</Field>;
}

function doseWarning(range, input) {
  if (!range) return null;
  const unit = `${input.unit}/ha`;
  return range === 'below' ? `La dosis está por debajo de la mínima del insumo (${input.doseMin} ${unit}).`
    : `La dosis está por encima de la máxima del insumo (${input.doseMax} ${unit}).`;
}

// Alta and edit of one OT, in the order of the workbook `Orden` sheet. The number always comes from the server.
// The form starts from the client (S4); its plots may come from any of its fields (S3).
function OrderForm({ record, refs, csrfToken, onSaved, onCancel }) {
  const [form, setForm] = useState(() => record ? orderFromRecord(record) : emptyOrder());
  const [plotTerm, setPlotTerm] = useState('');
  const [clientPlots, setClientPlots] = useState([]);
  const [plotsLoading, setPlotsLoading] = useState(false);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const focusLater = useFocusAfterRender();
  const firm = byId(refs['commercial-firms'], form.firmId);
  // Editing keeps the firm, so the client follows from it.
  const clientId = record ? firm?.clientId ?? '' : form.clientId;
  const client = byId(refs.clients, clientId);
  const fields = clientId ? refs.fields.filter(row => row.clientId === clientId) : [];
  const fieldKey = fields.map(row => row.id).join(',');
  const set = changes => setForm(current => ({ ...current, ...changes }));

  useEffect(() => {
    let active = true;
    if (!fieldKey) { setClientPlots([]); setPlotsLoading(false); return undefined; }
    setPlotsLoading(true);
    Promise.all(fieldKey.split(',').map(fieldId => api(`plots?fieldId=${fieldId}`)))
      .then(lists => { if (active) setClientPlots(lists.flat()); }).catch(cause => { if (active) setError(cause.message); })
      .finally(() => { if (active) setPlotsLoading(false); });
    return () => { active = false; };
  }, [fieldKey]);

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
  function chooseClient(next) {
    set({ clientId: next, firmId: clientFirms(refs['commercial-firms'], next).preselected, plots: [] });
    setPlotTerm('');
  }
  function togglePlot(plot, checked) {
    const plots = checked ? [...form.plots, { plotId: plot.id, worked: String(plot.areaHectares) }] : form.plots.filter(row => row.plotId !== plot.id);
    set({ plots }); prefill({ ...form, plots });
  }
  const setWorked = (plotId, worked) => set({ plots: form.plots.map(row => row.plotId === plotId ? { ...row, worked } : row) });
  const setLine = (key, changes) => set({ lines: form.lines.map(line => line.key === key ? { ...line, ...changes } : line) });
  // The new line's input search gets the focus (S3).
  function addLine() {
    const line = newLine();
    set({ lines: [...form.lines, line] });
    focusLater(`bo-wo-line-${line.key}-input`);
  }

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

  const options = (rows, label = row => row.name, detail = row => row.taxId ? `CUIT ${row.taxId}` : undefined) =>
    rows.map(row => ({ value: row.id, label: label(row), detail: detail(row) }));
  const inputOptions = options(refs.inputs, row => rowLabel('inputs', row), row => [row.activeIngredient, row.unit].filter(Boolean).join(' · '));
  const { firms } = clientFirms(refs['commercial-firms'], clientId);
  const groups = plotGroups(fields, clientPlots, plotTerm);
  const shownPlots = groups.reduce((sum, group) => sum + group.plots.length, 0);
  const total = workedTotal(form.plots);
  const ready = refs['commercial-firms'].length && refs['labor-types'].length && refs.contractors.length;
  const firmHint = firm ? `Se emitirá con el Nº ${firm.nextWorkOrderNumber}.`
    : client && !firms.length ? 'Este cliente aún no tiene firmas comerciales. Créala en Clientes.' : undefined;

  return <section className="bo-editor bo-order-form"><h2>{record ? `Editar orden ${orderLabel(firm, record.number)}` : 'Nueva orden de trabajo'}</h2>
    <form onSubmit={save}>
      {record ? <p className="bo-context">Firma {firm?.name ?? 'Sin referencia'} · cliente {client?.name ?? 'Sin referencia'}. La firma y el número no cambian.</p>
        : <div className="bo-pair">
          <ComboField id="bo-wo-client" label="Cliente" value={clientId} onChange={chooseClient} options={options(refs.clients)}
            placeholder="Busca por nombre o CUIT" hint={`${refs.clients.length} clientes. Escribe para filtrar y elige con las flechas y Enter.`} />
          <ComboField id="bo-wo-firm" label="Firma comercial" value={form.firmId} disabled={!client} onChange={firmId => set({ firmId })}
            options={options(firms)} placeholder={client ? 'Busca y elige una firma' : 'Primero elige el cliente'} hint={firmHint} />
        </div>}
      <Field id="bo-wo-date" label="Fecha">
        <input id="bo-wo-date" type="date" value={form.issuedOn} required onChange={event => set({ issuedOn: event.target.value })} /></Field>
      <ComboField id="bo-wo-labor" label="Labor" value={form.laborTypeId} onChange={laborTypeId => set({ laborTypeId })}
        options={laborOptionGroups(refs['labor-types'])} placeholder="Busca por nombre o abreviatura" />
      <ComboField id="bo-wo-stage" label="Etapa" optional value={form.stageId} onChange={stageId => set({ stageId })}
        options={options(refs.stages)} none="Sin indicar" />
      <ComboField id="bo-wo-contractor" label="Contratista" value={form.contractorId} onChange={contractorId => set({ contractorId })}
        options={options(refs.contractors)} placeholder="Busca y elige un contratista" />
      <fieldset className="bo-group"><legend>Campos y lotes</legend>
        {!client ? <p className="bo-muted">Elige un cliente para ver sus campos y lotes.</p>
          : !fields.length ? <p className="bo-muted">Este cliente aún no tiene campos. Cárgalos en Clientes.</p>
          : plotsLoading ? <Loading label="Cargando campos y lotes…" /> : <>
            <Field id="bo-wo-plot-filter" label="Filtrar campos y lotes" optional
              hint={`${shownPlots} de ${clientPlots.length} lotes a la vista · ${form.plots.length} seleccionados. Busca por campo, lote o código.`}>{hintId =>
                <input id="bo-wo-plot-filter" type="search" value={plotTerm} aria-describedby={hintId} onChange={event => setPlotTerm(event.target.value)} />}</Field>
            {!groups.length ? <p className="bo-muted">Sin coincidencias para “{plotTerm}”.</p> : groups.map(({ field, plots }) =>
              <section key={field.id} className="bo-field-group" aria-labelledby={`bo-wo-group-${field.id}`}>
                <h3 id={`bo-wo-group-${field.id}`}>{field.name} <small>{fieldPlotCount(clientPlots, field.id)}</small></h3>
                {!plots.length ? <p className="bo-muted">Este campo aún no tiene lotes.</p> : <div className="bo-plots">{plots.map(plot => {
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
              </section>)}
          </>}
        <p className="bo-total" role="status">Total a trabajar: <strong>{total} ha</strong> en {form.plots.length} {form.plots.length === 1 ? 'lote' : 'lotes'}</p>
      </fieldset>

      <ComboField id="bo-wo-campaign" label="Campaña" optional value={form.campaignId} options={options(refs.campaigns)} none="Sin indicar"
        onChange={campaignId => { set({ campaignId }); prefill({ ...form, campaignId }); }}
        hint="Con una campaña y un solo lote, el cultivo se completa con el primer cultivo del lote." />
      <ComboField id="bo-wo-crop" label="Cultivo" optional value={form.cropId} onChange={cropId => set({ cropId })}
        options={options(refs.crops)} none="Sin indicar" />
      <Field id="bo-wo-variety" label="Variedad o híbrido" optional>
        <input id="bo-wo-variety" type="text" maxLength={200} value={form.variety} onChange={event => set({ variety: event.target.value })} /></Field>

      <fieldset className="bo-group"><legend>Insumos</legend>
        {!form.lines.length && <p className="bo-muted">Sin insumos.</p>}
        <ol className="bo-lines">{form.lines.map((line, index) => {
          const input = byId(refs.inputs, line.inputId);
          const lineTotalValue = input ? lineTotal(line.dose, total) : null;
          const warning = doseWarning(input && doseRange(line.dose, input), input);
          const id = `bo-wo-line-${line.key}`;
          return <li key={line.key} className="bo-line">
            <ComboField id={`${id}-input`} label={`Insumo ${index + 1}`} value={line.inputId}
              onChange={inputId => setLine(line.key, { inputId })} options={inputOptions} placeholder="Busca por nombre o principio activo" />
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
        <button type="button" className="bo-link" onClick={addLine}>Agregar insumo</button>
      </fieldset>

      <Field id="bo-wo-notes" label="Observaciones" optional>
        <textarea id="bo-wo-notes" rows={3} maxLength={2000} value={form.observations} onChange={event => set({ observations: event.target.value })} /></Field>
      <Field id="bo-wo-engineer" label="Ingeniero responsable" optional>
        <input id="bo-wo-engineer" type="text" maxLength={200} value={form.responsibleEngineer} onChange={event => set({ responsibleEngineer: event.target.value })} /></Field>
      {error && <p className="bo-error" role="alert">{error}</p>}
      {!ready && <p>Primero crea clientes, labores y contratistas.</p>}
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
      // The order shows once its plots are in too, so the plot table never lists them as missing.
      const loaded = await api(`work-orders/${id}`);
      const fieldIds = [...new Set(loaded.plots.map(row => row.fieldId))];
      setPlots((await Promise.all(fieldIds.map(fieldId => api(`plots?fieldId=${fieldId}`)))).flat());
      setOrder(loaded);
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
  const fieldIds = [...new Set(order.plots.map(row => row.fieldId))];
  return <div className="bo-stack">
    <div className="bo-section-head"><button type="button" className="bo-link" onClick={onBack}>← Volver al listado</button>
      <div><PrintLink id={order.id} label={`Imprimir o guardar en PDF la orden ${orderLabel(firm, order.number)}`} />
        {issued && <><button type="button" onClick={() => onEdit(order)}>Editar</button>
          <button type="button" onClick={() => { setAction('complete'); setError(''); }}>Marcar realizada</button>
          <button type="button" onClick={() => { setAction('cancel'); setError(''); }}>Cancelar orden</button></>}</div></div>
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
      ['Contratista', nameOf(refs.contractors, order.contractorId)], ['Campos', fieldNames(refs.fields, fieldIds)],
      ['Campaña', nameOf(refs.campaigns, order.campaignId)], ['Cultivo', nameOf(refs.crops, order.cropId)], ['Variedad o híbrido', order.variety],
      ['Ingeniero responsable', order.responsibleEngineer], ['Observaciones', order.observations],
      ...(order.status === 'cancelada' ? [['Motivo de la cancelación', order.cancelReason]] : []),
    ]} />
    <section className="bo-list"><h2>Lotes</h2>
      <table className="bo-table"><thead><tr><th>Campo</th><th>Lote</th><th>Superficie</th><th>ha a trabajar</th></tr></thead>
        <tbody>{order.plots.map(row => {
          const plot = byId(plots, row.plotId);
          return <tr key={row.plotId}><td data-label="Campo">{nameOf(refs.fields, row.fieldId) ?? 'Sin referencia'}</td>
            <td data-label="Lote">{plot ? rowLabel('plots', plot) : 'Sin referencia'}</td>
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

// Work orders (S5): list with search, firm and status filters, alta, detail, edit, complete, cancel and print.
export default function WorkOrdersSection({ identity }) {
  const [refs, setRefs] = useState(() => Object.fromEntries(references.map(key => [key, []])));
  const [orders, setOrders] = useState([]);
  const [filters, setFilters] = useState({ firmId: '', status: '', q: '' });
  const [term, setTerm] = useState('');
  const request = useRef(0);
  // view: { mode: 'list' } | { mode: 'detail', id } | { mode: 'form', record }
  const [view, setView] = useState({ mode: 'list' });
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  // S5: the list and the form wait for their first load and block while they reload.
  const [loaded, setLoaded] = useState({ refs: false, orders: false });
  const [refsLoading, trackRefs] = usePending();
  const [ordersLoading, trackOrders] = usePending();

  const loadRefs = useCallback(() => trackRefs((async () => {
    try { setRefs(Object.fromEntries(await Promise.all(references.map(async key => [key, await api(key)])))); }
    catch (cause) { setError(cause.message); }
    finally { setLoaded(current => ({ ...current, refs: true })); }
  })()), [trackRefs]);
  // Only the latest request updates the list, so a slow earlier search cannot overwrite a newer one.
  const loadOrders = useCallback(() => trackOrders((async () => {
    const current = ++request.current;
    try {
      const rows = await api(orderQuery(filters));
      if (current === request.current) { setOrders(rows); setError(''); }
    } catch (cause) { if (current === request.current) setError(cause.message); }
    finally { if (current === request.current) setLoaded(state => ({ ...state, orders: true })); }
  })()), [filters, trackOrders]);
  useEffect(() => { loadRefs(); }, [loadRefs]);
  useEffect(() => { loadOrders(); }, [loadOrders]);
  // The search is sent 300 ms after the last keystroke.
  useEffect(() => {
    const timer = setTimeout(() => setFilters(current => current.q === term ? current : { ...current, q: term }), 300);
    return () => clearTimeout(timer);
  }, [term]);

  function saved(order, edited) {
    const firm = byId(refs['commercial-firms'], order.firmId);
    setNotice(`Orden ${orderLabel(firm, order.number)} ${edited ? 'actualizada' : 'emitida'}.`);
    setView({ mode: 'detail', id: order.id });
    loadOrders(); loadRefs();
  }
  const show = next => { setView(next); setNotice(''); setError(''); };

  const firmOptions = refs['commercial-firms'].map(row => ({ value: row.id, label: row.name, detail: row.taxId ? `CUIT ${row.taxId}` : undefined }));
  const refsReady = loaded.refs && !refsLoading;
  return <>
    <SectionHeader identity={identity} title="Órdenes de trabajo" count={view.mode === 'list' && loaded.orders ? orders.length : undefined}
      description="Cada orden se numera por firma comercial. Una orden cancelada conserva su número y sigue en el listado." />
    {notice && <p className="bo-notice" role="status">{notice}</p>}
    {error && <p className="bo-error" role="alert">{error}</p>}
    {view.mode === 'form' && <Busy ready={loaded.refs} busy={refsLoading} label="Cargando catálogos de la orden…">
      <OrderForm key={view.record?.id ?? 'new'} record={view.record} refs={refs} csrfToken={identity.csrfToken}
        onSaved={saved} onCancel={() => show(view.record ? { mode: 'detail', id: view.record.id } : { mode: 'list' })} /></Busy>}
    {view.mode === 'detail' && <OrderDetail key={view.id} id={view.id} refs={refs} csrfToken={identity.csrfToken}
      onBack={() => show({ mode: 'list' })} onEdit={record => show({ mode: 'form', record })} onChanged={loadOrders} />}
    {view.mode === 'list' && <section className="bo-list" aria-label="Listado de órdenes de trabajo">
      <div className="bo-section-head"><h2>Órdenes</h2><div>
        <button type="button" disabled={!refsReady} onClick={() => show({ mode: 'form', record: null })}>Nueva orden</button>
        <button type="button" disabled={refsLoading || ordersLoading} onClick={() => { setNotice(''); loadOrders(); loadRefs(); }}>Actualizar</button></div></div>
      <div className="bo-filters">
        <Field id="bo-wo-filter-q" label="Buscar" optional hint="Número, firma, campo, lote o código de lote.">{hintId =>
          <input id="bo-wo-filter-q" type="search" value={term} aria-describedby={hintId} onChange={event => setTerm(event.target.value)} />}</Field>
        <ComboField id="bo-wo-filter-firm" label="Firma" required={false} value={filters.firmId} options={firmOptions} none="Todas"
          onChange={firmId => setFilters(current => ({ ...current, firmId }))} />
        <Select id="bo-wo-filter-status" label="Estado" required={false} value={filters.status} options={statuses} placeholder="Todos"
          onChange={status => setFilters(current => ({ ...current, status }))} />
      </div>
      <Busy ready={loaded.orders && loaded.refs} busy={ordersLoading || refsLoading} label="Cargando órdenes…">
      {!orders.length ? <p className="bo-empty">{filters.firmId || filters.status || filters.q.trim() ? 'Ninguna orden coincide con la búsqueda o los filtros.' : 'Aún no hay órdenes de trabajo. Emite la primera con “Nueva orden”.'}</p> :
        <table className="bo-table bo-orders"><thead><tr><th>Nº</th><th>Fecha</th><th>Firma</th><th>Labor</th><th>Campos</th><th>Estado</th><th><span className="bo-sr">Acciones</span></th></tr></thead>
          <tbody>{orders.map(order => {
            const firm = byId(refs['commercial-firms'], order.firmId);
            return <tr key={order.id}>
              <td data-label="Nº"><button type="button" className="bo-link" onClick={() => show({ mode: 'detail', id: order.id })}>{orderLabel(firm, order.number)}</button></td>
              <td data-label="Fecha">{displayDate(order.issuedOn)}</td>
              <td data-label="Firma">{firm?.name ?? 'Sin referencia'}</td>
              <td data-label="Labor">{laborLabel(byId(refs['labor-types'], order.laborTypeId))}</td>
              <td data-label="Campos">{fieldNames(refs.fields, order.fieldIds)}</td>
              <td data-label="Estado"><span className={`bo-status bo-status-${order.status}`}>{optionLabel(statuses, order.status)}</span></td>
              <td data-label="PDF"><PrintLink id={order.id} label={`Imprimir o guardar en PDF la orden ${orderLabel(firm, order.number)}`} /></td>
            </tr>;
          })}</tbody></table>}
      </Busy>
    </section>}
  </>;
}
