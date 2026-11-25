'use client';

import { useCallback, useEffect, useState } from 'react';
import { api, send } from './api.mjs';
import { ComboField, Field, optionNodes } from './field.jsx';
import { Busy, RecordDetail, RecordForm, RecordList, SectionHeader, usePending } from './records.jsx';
import {
  currencies, emptyPrice, filterProviders, formatMoney, latestPricesByProvider, priceFromRecord, pricePayload, providerKinds, sortPrices,
} from './providers.mjs';
import { contactRoles, entities, optionLabel, websiteHref } from './schema.mjs';
import { displayUnit } from './units.mjs';
import { displayDate } from './work-orders.mjs';

const nameOf = (rows, id) => rows.find(row => row.id === id)?.name ?? 'Sin referencia';
export const providerLabel = provider => `${provider.name} · ${optionLabel(providerKinds, provider.kind)}`;

// Native select, only for small fixed lists (kind, currency, line mode, status); entities use ComboField (S2).
export function Select({ id, label, value, onChange, options, placeholder, optional, hint, required = !optional }) {
  return <Field id={id} label={label} optional={optional} hint={hint}>{hintId =>
    <select id={id} value={value} required={required} aria-describedby={hintId} onChange={event => onChange(event.target.value)}>
      {placeholder !== undefined && <option value="">{placeholder}</option>}
      {optionNodes(options)}
    </select>}</Field>;
}

/** A catalog input chosen in one search field (S2), by commercial name or active ingredient. */
export function InputPicker({ id, label, inputs, value, onChange }) {
  return <ComboField id={id} label={label} value={value} onChange={onChange} placeholder="Busca por nombre o principio activo"
    options={inputs.map(input => ({ value: input.id, label: `${input.commercialName} (${displayUnit(input.unit)})`, detail: input.activeIngredient ?? undefined }))} />;
}

/**
 * Which clients buy from which providers (S3), from either side: `side` 'client' lists the providers of client `ownerId`,
 * 'provider' the clients of provider `ownerId`. `options` are the records that can be linked.
 */
export function ClientProviderLinks({ side, ownerId, options, csrfToken, title, empty, describe = row => row.name }) {
  const own = side === 'client' ? 'clientId' : 'providerId';
  const other = side === 'client' ? 'providerId' : 'clientId';
  const [links, setLinks] = useState([]);
  const [choice, setChoice] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [loaded, setLoaded] = useState(false);
  const [loading, track] = usePending();
  const load = useCallback(() => track(api(`client-providers?${own}=${ownerId}`).then(setLinks).catch(cause => setError(cause.message)))
    .finally(() => setLoaded(true)), [own, ownerId, track]);
  useEffect(() => { load(); }, [load]);

  const linked = new Set(links.map(link => link[other]));
  const available = options.filter(option => !linked.has(option.id));
  const label = id => {
    const record = options.find(option => option.id === id);
    return record ? describe(record) : 'Sin referencia';
  };
  async function change(action) {
    setBusy(true); setError('');
    try { await action(); setChoice(''); await load(); } catch (cause) { setError(cause.message); } finally { setBusy(false); }
  }
  const add = event => {
    event.preventDefault();
    change(() => send('client-providers', 'POST', { [own]: ownerId, [other]: choice }, csrfToken));
  };
  const id = `bo-links-${side}`;
  return <section className="bo-list" aria-label={title}>
    <div className="bo-section-head"><h2>{title}</h2><div><button type="button" disabled={loading} onClick={() => { setError(''); load(); }}>Actualizar</button></div></div>
    {error && <p className="bo-error" role="alert">{error}</p>}
    <Busy ready={loaded} busy={loading} label="Cargando vínculos…">
    {links.length === 0 ? <p className="bo-empty">{empty}</p> : <ul>{[...links].sort((a, b) => label(a[other]).localeCompare(label(b[other]), 'es'))
      .map(link => <li key={link.id}><div className="bo-row"><strong>{label(link[other])}</strong></div>
        <button type="button" disabled={busy} onClick={() => change(() => send(`client-providers/${link.id}`, 'DELETE', undefined, csrfToken))}
          aria-label={`Quitar ${label(link[other])}`}>Quitar</button></li>)}</ul>}
    <form className="bo-inline-form" onSubmit={add}>
      <ComboField id={id} label={side === 'client' ? 'Agregar proveedor' : 'Agregar cliente'} value={choice} onChange={setChoice}
        disabled={!available.length} placeholder={available.length ? 'Busca y elige' : 'No quedan registros para vincular'}
        options={available.map(row => ({ value: row.id, label: describe(row), detail: row.taxId ? `CUIT ${row.taxId}` : undefined }))} />
      <div className="bo-actions"><button type="submit" disabled={busy || !choice}>{busy ? 'Guardando…' : 'Vincular'}</button></div>
    </form>
    </Busy>
  </section>;
}

// Create or edit one reference price of the provider. The unit follows the chosen input until it is changed by hand.
function PriceForm({ provider, record, inputs, csrfToken, onSaved, onCancel }) {
  const [form, setForm] = useState(() => record ? priceFromRecord(record) : emptyPrice());
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const set = changes => setForm(current => ({ ...current, ...changes }));
  const unitOf = id => inputs.find(input => input.id === id)?.unit ?? '';
  function chooseInput(inputId) {
    const followsInput = !form.unit.trim() || form.unit === unitOf(form.inputId);
    set(followsInput ? { inputId, unit: unitOf(inputId) } : { inputId });
  }
  async function save(event) {
    event.preventDefault();
    setBusy(true); setError('');
    try {
      await send(record ? `provider-prices/${record.id}` : 'provider-prices', record ? 'PUT' : 'POST',
        pricePayload(form, { providerId: provider.id, revision: record?.revision }), csrfToken);
      onSaved();
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }
  return <section className="bo-editor bo-analysis-form"><h2>{record ? 'Editar precio de referencia' : 'Nuevo precio de referencia'}</h2>
    <form onSubmit={save}>
      <div className="bo-pair">
        <InputPicker id="bo-price-input" label="Insumo" inputs={inputs} value={form.inputId} onChange={chooseInput} />
        <Field id="bo-price-value" label="Precio">
          <input id="bo-price-value" type="number" inputMode="decimal" min="0.0001" step="0.0001" required value={form.price}
            onChange={event => set({ price: event.target.value })} /></Field>
        <Select id="bo-price-currency" label="Moneda" value={form.currency} onChange={currency => set({ currency })} options={currencies} />
        <Field id="bo-price-unit" label="Unidad" optional hint="Por defecto, la unidad del insumo.">{hintId =>
          <input id="bo-price-unit" type="text" maxLength={40} value={form.unit} aria-describedby={hintId} onChange={event => set({ unit: event.target.value })} />}</Field>
        <Field id="bo-price-valid" label="Vigente desde">
          <input id="bo-price-valid" type="date" required value={form.validOn} onChange={event => set({ validOn: event.target.value })} /></Field>
      </div>
      <Field id="bo-price-notes" label="Notas" optional>
        <textarea id="bo-price-notes" rows={2} maxLength={2000} value={form.notes} onChange={event => set({ notes: event.target.value })} /></Field>
      {error && <p className="bo-error" role="alert">{error}</p>}
      {!inputs.length && <p>Primero crea insumos en su catálogo.</p>}
      <div className="bo-actions"><button type="submit" disabled={busy || !inputs.length}>{busy ? 'Guardando…' : record ? 'Guardar cambios' : 'Crear precio'}</button>
        <button type="button" onClick={onCancel}>Cancelar</button></div>
    </form></section>;
}

// Reference prices of an input supplier (S3), newest first.
function PricesPanel({ provider, csrfToken }) {
  const [prices, setPrices] = useState([]);
  const [inputs, setInputs] = useState([]);
  const [editing, setEditing] = useState(null);
  const [error, setError] = useState('');
  const [loaded, setLoaded] = useState({ prices: false, inputs: false });
  const [loading, track] = usePending();
  const load = useCallback(() => track(api(`provider-prices?providerId=${provider.id}`).then(setPrices).catch(cause => setError(cause.message)))
    .finally(() => setLoaded(current => ({ ...current, prices: true }))), [provider.id, track]);
  useEffect(() => {
    load();
    track(api('inputs').then(setInputs).catch(cause => setError(cause.message))).finally(() => setLoaded(current => ({ ...current, inputs: true })));
  }, [load, track]);
  const ready = loaded.prices && loaded.inputs;
  const inputName = id => inputs.find(input => input.id === id)?.commercialName ?? 'Sin referencia';
  return <>
    <section className="bo-list" aria-label="Precios de referencia">
      <div className="bo-section-head"><h2>Precios de referencia</h2><div>
        <button type="button" disabled={!ready || loading} onClick={() => setEditing({ record: null })}>Nuevo precio</button>
        <button type="button" disabled={loading} onClick={() => { setError(''); load(); }}>Actualizar</button></div></div>
      {error && <p className="bo-error" role="alert">{error}</p>}
      <Busy ready={ready} busy={loading} label="Cargando precios…">
      {!prices.length ? <p className="bo-empty">Este proveedor aún no tiene precios de referencia.</p> :
        <table className="bo-table"><thead><tr><th>Insumo</th><th>Precio</th><th>Unidad</th><th>Vigente desde</th><th>Notas</th><th><span className="bo-sr">Acciones</span></th></tr></thead>
          <tbody>{sortPrices(prices).map(price => <tr key={price.id}>
            <td data-label="Insumo">{inputName(price.inputId)}</td>
            <td data-label="Precio">{formatMoney(price.price, price.currency)}</td>
            <td data-label="Unidad">{displayUnit(price.unit)}</td>
            <td data-label="Vigente desde">{displayDate(price.validOn)}</td>
            <td data-label="Notas">{price.notes ?? <span className="bo-muted">Sin notas</span>}</td>
            <td data-label="Acciones"><button type="button" className="bo-link" onClick={() => setEditing({ record: price })}
              aria-label={`Editar precio de ${inputName(price.inputId)} del ${displayDate(price.validOn)}`}>Editar</button></td>
          </tr>)}</tbody></table>}
      </Busy>
    </section>
    {editing && <PriceForm key={editing.record?.id ?? 'new'} provider={provider} record={editing.record} inputs={inputs} csrfToken={csrfToken}
      onCancel={() => setEditing(null)} onSaved={() => { setEditing(null); load(); }} />}
  </>;
}

/** Latest reference price per provider of one input (S3), for the input catalog. */
export function InputPrices({ input }) {
  const [prices, setPrices] = useState(null);
  const [providers, setProviders] = useState([]);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    Promise.all([api(`provider-prices?inputId=${input.id}`), api('providers')])
      .then(([list, all]) => { if (active) { setPrices(list); setProviders(all); } })
      .catch(cause => { if (active) setError(cause.message); });
    return () => { active = false; };
  }, [input.id]);
  const latest = prices ? latestPricesByProvider(prices) : [];
  return <section className="bo-list" aria-label={`Precios de referencia de ${input.commercialName}`}>
    <h2>Precios de referencia</h2>
    <p className="bo-list-note">Último precio de cada proveedor para {input.commercialName}. Se cargan desde Proveedores.</p>
    {error ? <p className="bo-error" role="alert">{error}</p> : !prices ? <p role="status">Cargando precios…</p> :
      !latest.length ? <p className="bo-empty">Ningún proveedor tiene precios de este insumo.</p> :
        <table className="bo-table"><thead><tr><th>Proveedor</th><th>Precio</th><th>Vigente desde</th></tr></thead>
          <tbody>{latest.map(price => <tr key={price.id}>
            <td data-label="Proveedor">{nameOf(providers, price.providerId)}</td>
            <td data-label="Precio">{formatMoney(price.price, price.currency)} / {displayUnit(price.unit)}</td>
            <td data-label="Vigente desde">{displayDate(price.validOn)}</td></tr>)}</tbody></table>}
  </section>;
}

function providerWebsite(provider) {
  if (!provider.website) return null;
  const href = websiteHref(provider.website);
  return href ? <a href={href} target="_blank" rel="noopener">{provider.website}</a> : provider.website;
}

// Providers (S3, S4): list by kind with search; the detail holds contacts, the clients that buy and, for input suppliers, prices.
export default function ProvidersSection({ identity }) {
  const [rows, setRows] = useState([]);
  const [clients, setClients] = useState([]);
  const [kind, setKind] = useState('');
  const [providerId, setProviderId] = useState(null);
  const [contacts, setContacts] = useState([]);
  // editor: { entity, record } for an explicit edit; null shows the default "new" form of the current level.
  const [editor, setEditor] = useState(null);
  const [formKey, setFormKey] = useState(0);
  const [error, setError] = useState('');
  // S5: the list waits for providers and clients; a provider's detail waits for its contacts.
  const [loaded, setLoaded] = useState({ base: false, providerId: null });
  const [loading, track] = usePending();

  const load = useCallback(() => track(api('providers').then(setRows).catch(cause => setError(cause.message))), [track]);
  const loadContacts = useCallback(() => providerId
    ? track(api(`provider-contacts?providerId=${providerId}`).then(setContacts).catch(cause => setError(cause.message)))
      .finally(() => setLoaded(current => ({ ...current, providerId })))
    : null, [providerId, track]);
  useEffect(() => {
    Promise.all([load(), track(api('clients').then(setClients).catch(cause => setError(cause.message)))])
      .finally(() => setLoaded(current => ({ ...current, base: true })));
  }, [load, track]);
  useEffect(() => { setContacts([]); loadContacts(); }, [loadContacts]);
  const ready = loaded.base && (!providerId || loaded.providerId === providerId);

  const provider = rows.find(row => row.id === providerId);
  function open(entity, record = null) { setEditor({ entity, record }); setFormKey(key => key + 1); setError(''); }
  function go(id) { setProviderId(id); setEditor(null); setFormKey(key => key + 1); setError(''); }
  const target = editor ?? { entity: provider ? 'provider-contacts' : 'providers', record: null };
  function saved() {
    if (target.entity === 'providers') load(); else loadContacts();
    setEditor(null); setFormKey(key => key + 1);
  }
  const shown = filterProviders(rows, kind);
  const roleOrder = new Map(contactRoles.map((role, index) => [role.value, index]));
  const sortedContacts = [...contacts].sort((a, b) => roleOrder.get(a.role) - roleOrder.get(b.role) || a.name.localeCompare(b.name, 'es'));

  return <>
    <SectionHeader identity={identity} title="Proveedores" count={provider || !loaded.base ? undefined : shown.length}
      description="Firmas donde los clientes compran insumos y proveedores de servicios: laboratorios de suelos y semillas, plataformas virtuales y otros. Los contratistas siguen en su propio catálogo." />
    {provider && <nav className="bo-breadcrumb" aria-label="Ubicación"><ol>
      <li><button type="button" onClick={() => go(null)}>Proveedores</button></li>
      <li><button type="button" aria-current="page" onClick={() => go(provider.id)}>{provider.name}</button></li></ol></nav>}
    {error && <p className="bo-error" role="alert">{error}</p>}
    <Busy ready={ready} busy={loading} label={providerId ? 'Cargando proveedor…' : 'Cargando proveedores…'} className="bo-columns"><div className="bo-stack">
      {!provider && <>
        <div className="bo-filters bo-filters-single">
          <Select id="bo-provider-kind" label="Tipo" required={false} value={kind} onChange={setKind} placeholder="Todos los tipos" options={providerKinds} />
        </div>
        <RecordList entity="providers" rows={shown} searchable onRefresh={() => { setError(''); load(); }} selectedId={editor?.record?.id}
          onSelect={row => go(row.id)} selectLabel="Ver detalle" onEdit={row => open('providers', row)}
          empty={kind ? `No hay proveedores del tipo ${optionLabel(providerKinds, kind)}.` : 'Aún no hay proveedores. Crea el primero desde el formulario.'} />
      </>}
      {provider && <>
        <RecordDetail title="Datos del proveedor" onEdit={() => open('providers', provider)} items={[
          ['Nombre', provider.name], ['Tipo', optionLabel(providerKinds, provider.kind)], ['CUIT', provider.taxId], ['Sitio web', providerWebsite(provider)],
          ['Teléfono', provider.phone], ['Email', provider.email], ['Notas', provider.notes]]} />
        <RecordList entity="provider-contacts" rows={sortedContacts} onRefresh={() => { setError(''); loadContacts(); }} selectedId={editor?.record?.id}
          onNew={() => open('provider-contacts')} newLabel="Nuevo contacto" onEdit={row => open('provider-contacts', row)}
          detail={row => [entities['provider-contacts'].detail(row), row.notes].filter(Boolean).join(' · ')}
          empty="Este proveedor aún no tiene contactos de ventas ni técnicos." />
        <ClientProviderLinks key={`clients-${provider.id}`} side="provider" ownerId={provider.id} options={clients} csrfToken={identity.csrfToken}
          title="Clientes que le compran" empty="Ningún cliente está vinculado a este proveedor." />
        {provider.kind === 'insumos' && <PricesPanel key={`prices-${provider.id}`} provider={provider} csrfToken={identity.csrfToken} />}
      </>}
    </div>
      <RecordForm key={`${target.entity}-${target.record?.id ?? 'new'}-${formKey}`} entity={target.entity} record={target.record}
        parent={{ providerId }} csrfToken={identity.csrfToken}
        context={target.entity === 'provider-contacts' && !target.record ? `Se crea en el proveedor ${provider?.name}.` : undefined}
        onCancel={editor ? () => { setEditor(null); setFormKey(key => key + 1); } : undefined} onSaved={saved} />
    </Busy>
  </>;
}
