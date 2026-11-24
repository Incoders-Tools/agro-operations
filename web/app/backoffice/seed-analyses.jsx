'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { api, send } from './api.mjs';
import { ComboField, Field, optionNodes } from './field.jsx';
import { Busy, RecordDetail, SectionHeader, usePending } from './records.jsx';
import {
  emptySeedAnalysis, formatNumber, fractions, searchSeeds, seedFromRecord, seedPayload, seedProblem, seedQuery, seedingRate, treatments,
} from './analyses.mjs';
import { displayDate } from './work-orders.mjs';

const references = ['clients', 'crops', 'campaigns'];
const nameOf = (rows, id) => rows.find(row => row.id === id)?.name;
const percent = value => value == null ? null : `${formatNumber(Number(value), 2)} %`;
const options = rows => rows.map(row => ({ value: row.id, label: row.name, detail: row.taxId ? `CUIT ${row.taxId}` : undefined }));
const title = (row, refs) => [nameOf(refs.crops, row.cropId) ?? 'Sin referencia', row.variety].filter(Boolean).join(' · ');

// Native select, only for the calculator's fixed unit; entities use ComboField (S2).
function Select({ id, label, value, onChange, options: list, placeholder, optional, hint, required = !optional }) {
  return <Field id={id} label={label} optional={optional} hint={hint}>{hintId =>
    <select id={id} value={value} required={required} aria-describedby={hintId} onChange={event => onChange(event.target.value)}>
      <option value="">{placeholder}</option>
      {optionNodes(list)}
    </select>}</Field>;
}

function NumberInput({ id, label, value, onChange, hint, optional = true, min = '0', max, step = 'any' }) {
  return <Field id={id} label={label} optional={optional} hint={hint}>{hintId =>
    <input id={id} type="number" inputMode="decimal" min={min} max={max} step={step} value={value} required={!optional}
      aria-describedby={hintId} onChange={event => onChange(event.target.value)} />}</Field>;
}

// Create or edit one seed analysis. Every measured value is optional; each treatment block adds up to at most 100 %.
function SeedForm({ record, refs, labs, csrfToken, onSaved, onCancel }) {
  const [form, setForm] = useState(() => record ? seedFromRecord(record) : emptySeedAnalysis());
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const set = changes => setForm(current => ({ ...current, ...changes }));
  const problem = seedProblem(form);

  async function save(event) {
    event.preventDefault();
    if (problem) { setError(problem); return; }
    setBusy(true); setError('');
    try {
      const saved = await send(record ? `seed-analyses/${record.id}` : 'seed-analyses', record ? 'PUT' : 'POST',
        seedPayload(form, { revision: record?.revision }), csrfToken);
      onSaved(saved, Boolean(record));
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }

  const ready = refs.clients.length && refs.crops.length;
  return <section className="bo-editor bo-analysis-form"><h2>{record ? 'Editar análisis de semillas' : 'Nuevo análisis de semillas'}</h2>
    <form onSubmit={save}>
      <div className="bo-pair">
        <ComboField id="bo-seed-client" label="Cliente" value={form.clientId} onChange={clientId => set({ clientId })} options={options(refs.clients)}
          placeholder="Busca por nombre o CUIT" hint="Productor de procedencia de la semilla." />
        <ComboField id="bo-seed-crop" label="Cultivo" value={form.cropId} onChange={cropId => set({ cropId })} options={options(refs.crops)}
          placeholder="Busca y elige un cultivo" />
        <Field id="bo-seed-variety" label="Variedad o híbrido" optional>
          <input id="bo-seed-variety" type="text" maxLength={200} value={form.variety} onChange={event => set({ variety: event.target.value })} /></Field>
        <ComboField id="bo-seed-campaign" label="Campaña" optional value={form.campaignId} onChange={campaignId => set({ campaignId })}
          options={options(refs.campaigns)} none="Sin indicar" />
        <Field id="bo-seed-date" label="Fecha de análisis">
          <input id="bo-seed-date" type="date" required value={form.analyzedOn} onChange={event => set({ analyzedOn: event.target.value })} /></Field>
        <ComboField id="bo-seed-lab" label="Laboratorio" optional value={form.laboratoryId} onChange={laboratoryId => set({ laboratoryId })}
          options={options(labs)} none="Sin indicar" hint={labs.length ? undefined : 'Aún no hay laboratorios de semillas en Proveedores.'} />
        <NumberInput id="bo-seed-weight" label="Peso de 1000 semillas (g)" value={form.thousandSeedWeightG}
          onChange={thousandSeedWeightG => set({ thousandSeedWeightG })} />
        <NumberInput id="bo-seed-vigor" label="Vigor (%)" max="100" value={form.vigorPct} onChange={vigorPct => set({ vigorPct })} />
      </div>
      <p className="bo-hint">Todos los valores medidos son opcionales: carga solo los que tengas.</p>
      <div className="bo-pair">{treatments.map(treatment => <fieldset key={treatment.key} className="bo-group"><legend>{treatment.label}</legend>
        {fractions.map(fraction => {
          const key = `${fraction.key}${treatment.key}Pct`;
          return <NumberInput key={key} id={`bo-seed-${key}`} label={fraction.label} max="100" value={form[key]}
            onChange={value => set({ [key]: value })} />;
        })}
      </fieldset>)}</div>
      <Field id="bo-seed-notes" label="Observaciones" optional>
        <textarea id="bo-seed-notes" rows={3} maxLength={2000} value={form.notes} onChange={event => set({ notes: event.target.value })} /></Field>
      {problem && <p className="bo-warning" role="status">⚠ {problem}</p>}
      {error && <p className="bo-error" role="alert">{error}</p>}
      {!ready && <p>Primero crea clientes y cultivos.</p>}
      <div className="bo-actions"><button type="submit" disabled={busy || !ready}>{busy ? 'Guardando…' : record ? 'Guardar cambios' : 'Crear análisis'}</button>
        <button type="button" onClick={onCancel}>Cancelar</button></div>
    </form></section>;
}

// Seeding rate from a target stand (S2). The formula is an assumption until the engineers confirm it (Q18).
function SeedingCalculator({ analysis }) {
  const [inputs, setInputs] = useState({ plants: '', per: 'ha', efficiencyPct: '90', rowSpacingCm: '' });
  const set = changes => setInputs(current => ({ ...current, ...changes }));
  const rate = seedingRate(inputs, analysis);
  const used = rate.germination && `Se usa el poder germinativo ${rate.germination.source === 'treated' ? 'con' : 'sin'} curasemilla (${percent(rate.germination.pct)}).`;
  const result = (value, decimals, unit) => value === null ? '—' : `${formatNumber(value, decimals)} ${unit}`;
  return <section className="bo-list bo-calculator" aria-label="Calculadora de siembra"><h2>Calculadora de siembra</h2>
    <p className="bo-warning" role="note">Fórmula provisoria: espera la confirmación de los ingenieros (Q18).</p>
    <div className="bo-pair">
      <NumberInput id="bo-calc-plants" label="Plantas objetivo" optional={false} min="0" value={inputs.plants} onChange={plants => set({ plants })} />
      <Select id="bo-calc-per" label="Plantas por" value={inputs.per} onChange={per => set({ per: per || 'ha' })} placeholder="Elige la unidad"
        options={[{ value: 'ha', label: 'Hectárea (plantas/ha)' }, { value: 'm2', label: 'Metro cuadrado (plantas/m²)' }]} />
      <NumberInput id="bo-calc-efficiency" label="Eficiencia de emergencia (%)" optional={false} min="0" max="100" value={inputs.efficiencyPct}
        onChange={efficiencyPct => set({ efficiencyPct })} hint="Plantas logradas sobre semillas germinables. Por defecto 90 %." />
      <NumberInput id="bo-calc-spacing" label="Distancia entre surcos (cm)" value={inputs.rowSpacingCm} onChange={rowSpacingCm => set({ rowSpacingCm })}
        hint="Necesaria para semillas por metro lineal." />
    </div>
    {used && <p className="bo-context">{used}</p>}
    <dl className="bo-results" aria-live="polite">
      <div><dt>Semillas/ha</dt><dd>{result(rate.seedsPerHa === null ? null : Math.round(rate.seedsPerHa), 0, 'semillas/ha')}</dd></div>
      <div><dt>kg/ha</dt><dd>{result(rate.kgPerHa, 2, 'kg/ha')}</dd></div>
      <div><dt>Semillas por metro lineal</dt><dd>{result(rate.seedsPerMetre, 2, 'semillas/m')}</dd></div>
    </dl>
    {rate.missing.length > 0 && <ul className="bo-missing">{rate.missing.map(message => <li key={message}>{message}</li>)}</ul>}
    <div className="bo-formulas"><p>Cómo se calcula (PG y eficiencia como fracción, por ejemplo 90 % = 0,9):</p><ul>
      <li><code>semillas/ha = plantas/ha ÷ (PG × eficiencia)</code></li>
      <li><code>kg/ha = semillas/ha × P1000 ÷ 1.000.000</code></li>
      <li><code>semillas por metro lineal = semillas/ha × distancia (m) ÷ 10.000</code></li>
      <li>Plantas/m² × 10.000 = plantas/ha.</li></ul></div>
  </section>;
}

function SeedDetail({ id, refs, labs, onBack, onEdit }) {
  const [analysis, setAnalysis] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    api(`seed-analyses/${id}`).then(row => { if (active) setAnalysis(row); }).catch(cause => { if (active) setError(cause.message); });
    return () => { active = false; };
  }, [id]);
  if (!analysis) return <section className="bo-list"><button type="button" className="bo-link" onClick={onBack}>← Volver al listado</button>
    {error ? <p className="bo-error" role="alert">{error}</p> : <p role="status">Cargando análisis…</p>}</section>;
  const block = key => fractions.map(fraction => [`${fraction.label.replace(' (%)', '')}`, percent(analysis[`${fraction.key}${key}Pct`])]);
  return <div className="bo-stack">
    <div className="bo-section-head"><button type="button" className="bo-link" onClick={onBack}>← Volver al listado</button>
      <div><button type="button" onClick={() => onEdit(analysis)}>Editar</button></div></div>
    <RecordDetail title={`Análisis de semillas · ${title(analysis, refs)}`} items={[
      ['Cliente', nameOf(refs.clients, analysis.clientId)], ['Cultivo', nameOf(refs.crops, analysis.cropId)], ['Variedad o híbrido', analysis.variety],
      ['Campaña', nameOf(refs.campaigns, analysis.campaignId)], ['Fecha de análisis', displayDate(analysis.analyzedOn)],
      ['Laboratorio', analysis.laboratoryId && (nameOf(labs, analysis.laboratoryId) ?? 'Sin referencia')],
      ['Peso de 1000 semillas', analysis.thousandSeedWeightG == null ? null : `${formatNumber(Number(analysis.thousandSeedWeightG), 2)} g`],
      ['Vigor', percent(analysis.vigorPct)], ['Observaciones', analysis.notes],
    ]} />
    <div className="bo-pair">{treatments.map(treatment =>
      <RecordDetail key={treatment.key} title={treatment.label} items={block(treatment.key)} />)}</div>
    <SeedingCalculator analysis={analysis} />
  </div>;
}

// Seed analyses (S2): list filtered by client and crop and searched by variety, form, detail with the calculator.
export default function SeedAnalysesSection({ identity }) {
  const [refs, setRefs] = useState(() => Object.fromEntries(references.map(key => [key, []])));
  const [labs, setLabs] = useState([]);
  const [labError, setLabError] = useState('');
  const [rows, setRows] = useState([]);
  const [filters, setFilters] = useState({ clientId: '', cropId: '' });
  const [term, setTerm] = useState('');
  const request = useRef(0);
  // view: { mode: 'list' } | { mode: 'detail', id } | { mode: 'form', record }
  const [view, setView] = useState({ mode: 'list' });
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  // S5: the list, the form and the detail wait for their first load and block while they reload.
  const [loaded, setLoaded] = useState({ refs: false, rows: false });
  const [refsLoading, trackRefs] = usePending();
  const [rowsLoading, trackRows] = usePending();

  const loadRefs = useCallback(() => trackRefs(Promise.all([
    (async () => {
      try { setRefs(Object.fromEntries(await Promise.all(references.map(async key => [key, await api(key)])))); }
      catch (cause) { setError(cause.message); }
    })(),
    api('providers?kind=laboratorio_semillas').then(list => { setLabs(list); setLabError(''); }).catch(cause => setLabError(cause.message)),
  ])).finally(() => setLoaded(current => ({ ...current, refs: true }))), [trackRefs]);
  // Only the latest request updates the list, so a slow earlier filter cannot overwrite a newer one.
  const loadRows = useCallback(() => trackRows((async () => {
    const current = ++request.current;
    try {
      const list = await api(seedQuery(filters));
      if (current === request.current) { setRows(list); setError(''); }
    } catch (cause) { if (current === request.current) setError(cause.message); }
    finally { if (current === request.current) setLoaded(state => ({ ...state, rows: true })); }
  })()), [filters, trackRows]);
  useEffect(() => { loadRefs(); }, [loadRefs]);
  useEffect(() => { loadRows(); }, [loadRows]);

  function saved(analysis, edited) {
    setNotice(`Análisis de semillas ${edited ? 'actualizado' : 'creado'}.`);
    setView({ mode: 'detail', id: analysis.id });
    loadRows();
  }
  const show = next => { setView(next); setNotice(''); setError(''); };
  const shown = searchSeeds(rows, term);
  const filtered = filters.clientId || filters.cropId || term.trim();

  return <>
    <SectionHeader identity={identity} title="Análisis de semillas" count={view.mode === 'list' && loaded.rows ? shown.length : undefined}
      description="Calidad de semilla por cultivo y variedad o híbrido según el productor de procedencia, con la calculadora de densidad de siembra." />
    {notice && <p className="bo-notice" role="status">{notice}</p>}
    {error && <p className="bo-error" role="alert">{error}</p>}
    {labError && <p className="bo-warning" role="status">No se pudieron cargar los laboratorios: {labError}</p>}
    {view.mode !== 'list' && <Busy ready={loaded.refs} busy={refsLoading} label="Cargando clientes, cultivos y laboratorios…">
      {view.mode === 'form' && <SeedForm key={view.record?.id ?? 'new'} record={view.record} refs={refs} labs={labs} csrfToken={identity.csrfToken}
        onSaved={saved} onCancel={() => show(view.record ? { mode: 'detail', id: view.record.id } : { mode: 'list' })} />}
      {view.mode === 'detail' && <SeedDetail key={view.id} id={view.id} refs={refs} labs={labs}
        onBack={() => show({ mode: 'list' })} onEdit={record => show({ mode: 'form', record })} />}
    </Busy>}
    {view.mode === 'list' && <section className="bo-list" aria-label="Listado de análisis de semillas">
      <div className="bo-section-head"><h2>Análisis</h2><div>
        <button type="button" disabled={!loaded.refs || refsLoading} onClick={() => show({ mode: 'form', record: null })}>Nuevo análisis</button>
        <button type="button" disabled={refsLoading || rowsLoading} onClick={() => { setNotice(''); loadRows(); loadRefs(); }}>Actualizar</button></div></div>
      <div className="bo-filters">
        <ComboField id="bo-seed-filter-client" label="Cliente" required={false} value={filters.clientId} options={options(refs.clients)} none="Todos"
          onChange={clientId => setFilters(current => ({ ...current, clientId }))} />
        <ComboField id="bo-seed-filter-crop" label="Cultivo" required={false} value={filters.cropId} options={options(refs.crops)} none="Todos"
          onChange={cropId => setFilters(current => ({ ...current, cropId }))} />
        <Field id="bo-seed-filter-variety" label="Buscar variedad" optional hint={`${shown.length} de ${rows.length}`}>{hintId =>
          <input id="bo-seed-filter-variety" type="search" value={term} aria-describedby={hintId} onChange={event => setTerm(event.target.value)} />}</Field>
      </div>
      <Busy ready={loaded.rows && loaded.refs} busy={rowsLoading || refsLoading} label="Cargando análisis de semillas…">
      {!shown.length ? <p className="bo-empty">{filtered ? 'Ningún análisis coincide con los filtros o la búsqueda.' : 'Aún no hay análisis de semillas. Crea el primero con “Nuevo análisis”.'}</p> :
        <table className="bo-table"><thead><tr><th>Cultivo y variedad</th><th>Cliente</th><th>Fecha</th><th>PG con curasemilla</th><th>PG sin curasemilla</th><th>P1000</th></tr></thead>
          <tbody>{shown.map(row => <tr key={row.id}>
            <td data-label="Cultivo"><button type="button" className="bo-link" onClick={() => show({ mode: 'detail', id: row.id })}>{title(row, refs)}</button></td>
            <td data-label="Cliente">{nameOf(refs.clients, row.clientId) ?? 'Sin referencia'}</td>
            <td data-label="Fecha">{displayDate(row.analyzedOn)}</td>
            <td data-label="PG con curasemilla">{percent(row.germinationTreatedPct) ?? <span className="bo-muted">Sin dato</span>}</td>
            <td data-label="PG sin curasemilla">{percent(row.germinationUntreatedPct) ?? <span className="bo-muted">Sin dato</span>}</td>
            <td data-label="P1000">{row.thousandSeedWeightG == null ? <span className="bo-muted">Sin dato</span> : `${formatNumber(Number(row.thousandSeedWeightG), 2)} g`}</td>
          </tr>)}</tbody></table>}
      </Busy>
    </section>}
  </>;
}
