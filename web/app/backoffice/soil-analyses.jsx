'use client';

import { useCallback, useEffect, useState } from 'react';
import { api, send } from './api.mjs';
import { ComboField, Field, optionNodes, useFocusAfterRender } from './field.jsx';
import { Busy, usePending } from './records.jsx';
import {
  emptySoilAnalysis, formatNumber, newResult, parameterHistory, samplingMethods, soilFromRecord, soilParameters, soilPayload, soilProblem,
  soilUnits, sortSoilAnalyses,
} from './analyses.mjs';
import { optionLabel, rowLabel, websiteHref } from './schema.mjs';
import { displayUnit } from './units.mjs';
import { displayDate } from './work-orders.mjs';

const nameOf = (rows, id) => rows.find(row => row.id === id)?.name;
const count = n => `${n} ${n === 1 ? 'resultado' : 'resultados'}`;
const measure = row => [formatNumber(Number(row.value), 4), displayUnit(row.unit)].filter(Boolean).join(' ');

// Native select, only for the fixed sampling methods; campaigns and laboratories use ComboField (S2).
function Select({ id, label, value, onChange, options, placeholder, optional, hint }) {
  return <Field id={id} label={label} optional={optional} hint={hint}>{hintId =>
    <select id={id} value={value} required={!optional} aria-describedby={hintId} onChange={event => onChange(event.target.value)}>
      {placeholder !== undefined && <option value="">{placeholder}</option>}
      {optionNodes(options)}
    </select>}</Field>;
}

function TextInput({ id, label, value, onChange, optional = true, hint, type = 'text', ...rest }) {
  return <Field id={id} label={label} optional={optional} hint={hint}>{hintId =>
    <input id={id} type={type} value={value} required={!optional} aria-describedby={hintId} maxLength={type === 'text' ? 200 : undefined}
      onChange={event => onChange(event.target.value)} {...rest} />}</Field>;
}

// Create or edit one soil analysis of the plot with its results (full replace on save).
function SoilForm({ plot, record, campaigns, labs, csrfToken, onSaved, onCancel }) {
  const [form, setForm] = useState(() => record ? soilFromRecord(record) : emptySoilAnalysis());
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const set = changes => setForm(current => ({ ...current, ...changes }));
  const setResult = (key, changes) => set({ results: form.results.map(row => row.key === key ? { ...row, ...changes } : row) });
  const stations = form.samplingMethod === 'estaciones';
  const options = rows => rows.map(row => ({ value: row.id, label: row.name }));
  const focusLater = useFocusAfterRender();
  // The new result's first field gets the focus (S3).
  function addResult() {
    const row = newResult();
    set({ results: [...form.results, row] });
    focusLater(`bo-soil-result-${row.key}-parameter`);
  }

  async function save(event) {
    event.preventDefault();
    const problem = soilProblem(form);
    if (problem) { setError(problem); return; }
    setBusy(true); setError('');
    try {
      const saved = await send(record ? `soil-analyses/${record.id}` : 'soil-analyses', record ? 'PUT' : 'POST',
        soilPayload(form, { plotId: plot.id, revision: record?.revision }), csrfToken);
      onSaved(saved);
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }

  return <section className="bo-editor bo-analysis-form"><h2>{record ? 'Editar análisis de suelo' : 'Nuevo análisis de suelo'}</h2>
    {!record && <p className="bo-context">Se registra en el lote {rowLabel('plots', plot)}.</p>}
    <form onSubmit={save}>
      <div className="bo-pair">
        <ComboField id="bo-soil-campaign" label="Campaña" value={form.campaignId} onChange={campaignId => set({ campaignId })}
          options={options(campaigns)} placeholder="Busca y elige una campaña" />
        <TextInput id="bo-soil-sampled" label="Fecha de muestreo" type="date" optional={false} value={form.sampledOn}
          onChange={sampledOn => set({ sampledOn })} />
        <TextInput id="bo-soil-analyzed" label="Fecha de análisis" type="date" value={form.analyzedOn} onChange={analyzedOn => set({ analyzedOn })} />
        <ComboField id="bo-soil-lab" label="Laboratorio" optional value={form.laboratoryId} onChange={laboratoryId => set({ laboratoryId })}
          options={options(labs)} none="Sin indicar"
          hint={labs.length ? undefined : 'Aún no hay laboratorios de suelos en Proveedores.'} />
        <Select id="bo-soil-method" label="Método de muestreo" value={form.samplingMethod} onChange={samplingMethod => set({ samplingMethod })}
          options={samplingMethods} hint={stations ? 'Cada resultado indica su estación.' : 'Un valor por parámetro para todo el lote.'} />
      </div>
      <Field id="bo-soil-detail" label="Detalle del muestreo" optional hint="Cómo se tomaron las muestras: cantidad, ubicación, criterio.">{hintId =>
        <textarea id="bo-soil-detail" rows={3} maxLength={2000} value={form.samplingDetail} aria-describedby={hintId}
          onChange={event => set({ samplingDetail: event.target.value })} />}</Field>
      <TextInput id="bo-soil-map" label="Enlace al shape o KML" value={form.mapLink} inputMode="url" maxLength={2000}
        hint="Dirección del archivo que muestra la zona de muestreo, sin espacios." onChange={mapLink => set({ mapLink })} />

      <fieldset className="bo-group"><legend>Resultados</legend>
        <datalist id="bo-soil-parameters">{soilParameters.map(name => <option key={name} value={name} />)}</datalist>
        <datalist id="bo-soil-units">{soilUnits.map(unit => <option key={unit} value={unit} />)}</datalist>
        <ol className="bo-lines">{form.results.map((row, index) => {
          const id = `bo-soil-result-${row.key}`;
          const filled = Boolean(row.parameter || row.value);
          return <li key={row.key} className={stations ? 'bo-soil-line bo-soil-stations' : 'bo-soil-line'}>
            <TextInput id={`${id}-parameter`} label={`Parámetro ${index + 1}`} optional={false} list="bo-soil-parameters" value={row.parameter}
              required={filled || index === 0} onChange={parameter => setResult(row.key, { parameter })} />
            <TextInput id={`${id}-value`} label="Valor" type="number" optional={false} step="any" value={row.value}
              required={filled || index === 0} onChange={value => setResult(row.key, { value })} />
            <TextInput id={`${id}-unit`} label="Unidad" list="bo-soil-units" value={row.unit} maxLength={30}
              onChange={unit => setResult(row.key, { unit })} />
            <TextInput id={`${id}-depth`} label="Profundidad" value={row.depth} placeholder="0-20 cm" maxLength={30}
              onChange={depth => setResult(row.key, { depth })} />
            {stations && <TextInput id={`${id}-station`} label="Estación" optional={false} value={row.station} maxLength={60}
              required={filled} onChange={station => setResult(row.key, { station })} />}
            <button type="button" className="bo-link" disabled={form.results.length === 1}
              onClick={() => set({ results: form.results.filter(other => other.key !== row.key) })}
              aria-label={`Quitar resultado ${index + 1}`}>Quitar</button>
          </li>;
        })}</ol>
        <button type="button" className="bo-link" onClick={addResult}>Agregar resultado</button>
      </fieldset>

      <Field id="bo-soil-notes" label="Observaciones" optional>
        <textarea id="bo-soil-notes" rows={3} maxLength={2000} value={form.notes} onChange={event => set({ notes: event.target.value })} /></Field>
      {error && <p className="bo-error" role="alert">{error}</p>}
      {!campaigns.length && <p>Primero crea campañas en su catálogo.</p>}
      <div className="bo-actions"><button type="submit" disabled={busy || !campaigns.length}>{busy ? 'Guardando…' : record ? 'Guardar cambios' : 'Crear análisis'}</button>
        <button type="button" onClick={onCancel}>Cancelar</button></div>
    </form></section>;
}

// Soil analyses of one plot across every campaign (S1): list newest first, form, and the per-parameter history.
export default function SoilAnalysesPanel({ plot, campaigns, csrfToken }) {
  const [rows, setRows] = useState([]);
  const [labs, setLabs] = useState([]);
  const [labError, setLabError] = useState('');
  // editor: null (closed) | { record } where record is null for a new analysis.
  const [editor, setEditor] = useState(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  // S5: the list waits for the analyses and laboratories; opening one to edit blocks the list until it arrives.
  const [loaded, setLoaded] = useState({ rows: false, labs: false });
  const [opening, setOpening] = useState(false);
  const [loading, track] = usePending();

  const load = useCallback(() => track((async () => {
    try { setRows(await api(`soil-analyses?plotId=${encodeURIComponent(plot.id)}`)); setError(''); }
    catch (cause) { setError(cause.message); }
    finally { setLoaded(current => ({ ...current, rows: true })); }
  })()), [plot.id, track]);
  useEffect(() => { load(); }, [load]);
  useEffect(() => {
    track(api('providers?kind=laboratorio_suelos').then(list => { setLabs(list); setLabError(''); }).catch(cause => setLabError(cause.message)))
      .finally(() => setLoaded(current => ({ ...current, labs: true })));
  }, [track]);

  async function edit(row) {
    setError(''); setNotice(''); setOpening(true);
    try { setEditor({ record: await api(`soil-analyses/${row.id}`) }); }
    catch (cause) { setError(cause.message); }
    finally { setOpening(false); }
  }
  function saved() {
    setNotice(editor?.record ? 'Análisis de suelo actualizado.' : 'Análisis de suelo creado.');
    setEditor(null); load();
  }

  const analyses = sortSoilAnalyses(rows);
  const history = parameterHistory(analyses);
  const campaignName = id => nameOf(campaigns, id) ?? 'Sin referencia';
  const ready = loaded.rows && loaded.labs;
  return <>
    {editor && <SoilForm key={editor.record?.id ?? 'new'} plot={plot} record={editor.record} campaigns={campaigns} labs={labs}
      csrfToken={csrfToken} onSaved={saved} onCancel={() => setEditor(null)} />}
    <section className="bo-list" aria-label="Listado de análisis de suelo">
      <div className="bo-section-head"><h2>Análisis de suelo</h2><div>
        <button type="button" disabled={!ready || loading || opening} onClick={() => { setEditor({ record: null }); setNotice(''); setError(''); }}>Nuevo análisis</button>
        <button type="button" disabled={loading} onClick={() => { setNotice(''); load(); }}>Actualizar</button></div></div>
      <p className="bo-list-note">Todos los análisis del lote, de todas las campañas, del más reciente al más antiguo.</p>
      {notice && <p className="bo-notice" role="status">{notice}</p>}
      {error && <p className="bo-error" role="alert">{error}</p>}
      {labError && <p className="bo-warning" role="status">No se pudieron cargar los laboratorios: {labError}</p>}
      <Busy ready={ready} busy={loading || opening} label="Cargando análisis de suelo…">
      {!analyses.length ? <p className="bo-empty">Este lote aún no tiene análisis de suelo.</p> :
        <table className="bo-table"><thead><tr><th>Campaña</th><th>Muestreo</th><th>Análisis</th><th>Laboratorio</th><th>Método</th><th>Resultados</th>
          <th><span className="bo-sr">Acciones</span></th></tr></thead>
          <tbody>{analyses.map(row => {
            const href = websiteHref(row.mapLink);
            return <tr key={row.id}>
              <td data-label="Campaña">{campaignName(row.campaignId)}</td>
              <td data-label="Muestreo">{displayDate(row.sampledOn)}</td>
              <td data-label="Análisis">{row.analyzedOn ? displayDate(row.analyzedOn) : <span className="bo-muted">Sin indicar</span>}</td>
              <td data-label="Laboratorio">{row.laboratoryId ? nameOf(labs, row.laboratoryId) ?? 'Sin referencia' : <span className="bo-muted">Sin indicar</span>}</td>
              <td data-label="Método">{optionLabel(samplingMethods, row.samplingMethod)}
                {row.samplingDetail && <small className="bo-cell-note">{row.samplingDetail}</small>}</td>
              <td data-label="Resultados">{count((row.results ?? []).length)}
                {href && <small className="bo-cell-note"><a href={href} target="_blank" rel="noopener">Ver zona de muestreo</a></small>}</td>
              <td data-label="Acciones"><button type="button" className="bo-link" onClick={() => edit(row)}
                aria-label={`Editar análisis de suelo del ${displayDate(row.sampledOn)}`}>Editar</button></td>
            </tr>;
          })}</tbody></table>}
      </Busy>
    </section>
    {history.length > 0 && <section className="bo-list" aria-label="Historial por parámetro">
      <h2>Historial por parámetro</h2>
      <p className="bo-list-note">Cada parámetro con sus valores por campaña y fecha de muestreo, del más antiguo al más reciente.</p>
      <div className="bo-history">{history.map((group, position) => <section key={group.parameter} className="bo-field-group"
        aria-labelledby={`bo-soil-history-${position}`}>
        <h3 id={`bo-soil-history-${position}`}>{group.parameter} <small>{count(group.entries.length)}</small></h3>
        <table className="bo-table"><thead><tr><th>Campaña</th><th>Muestreo</th><th>Valor</th><th>Profundidad</th><th>Estación</th></tr></thead>
          <tbody>{group.entries.map((entry, index) => <tr key={`${entry.analysisId}-${index}`}>
            <td data-label="Campaña">{campaignName(entry.campaignId)}</td>
            <td data-label="Muestreo">{displayDate(entry.sampledOn)}</td>
            <td data-label="Valor">{measure(entry)}</td>
            <td data-label="Profundidad">{entry.depth ?? <span className="bo-muted">Sin indicar</span>}</td>
            <td data-label="Estación">{entry.station ?? <span className="bo-muted">Promedio</span>}</td>
          </tr>)}</tbody></table>
      </section>)}</div>
    </section>}
  </>;
}
