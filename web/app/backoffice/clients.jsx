'use client';

import { useCallback, useEffect, useState } from 'react';
import { api } from './api.mjs';
import { Busy, RecordDetail, RecordForm, RecordList, SectionHeader, usePending } from './records.jsx';
import SoilAnalysesPanel from './soil-analyses.jsx';
import { ClientProviderLinks, providerLabel } from './providers.jsx';
import { ClientComparisons } from './quotes.jsx';
import { clientCounts, entities, fieldMoveGroups, fieldPlotCount, moveNotice, optionLabel, plotCropDetail, rowLabel, tenures } from './schema.mjs';

const children = { clients: 'fields', fields: 'plots', plots: 'plot-crops' };

// Client -> fields -> plots -> crops per campaign (S4). Children are always created inside the selected parent.
export default function ClientsSection({ identity, navigate }) {
  const [rows, setRows] = useState({ clients: [], 'commercial-firms': [], fields: [], plots: [], 'plot-crops': [], crops: [], campaigns: [], providers: [] });
  const [path, setPath] = useState({ clientId: null, fieldId: null, plotId: null });
  // editor: { entity, record } for an explicit edit or "new" target; null shows the default "new child" form.
  const [editor, setEditor] = useState(null);
  const [formKey, setFormKey] = useState(0);
  // Every field and plot, only to count them on the client and field rows (S1).
  const [totals, setTotals] = useState({ fields: [], plots: [] });
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  // Which level finished its first load (S5): the catalogs, and the client, field and plot whose children are listed.
  const [loaded, setLoaded] = useState({ base: false, clientId: null, fieldId: null, plotId: null });
  const [loading, track] = usePending();

  const load = useCallback((entity, query) => track((async () => {
    try {
      const list = await api(query ? `${entity}?${query}` : entity);
      setRows(current => ({ ...current, [entity]: list }));
    } catch (cause) { setError(cause.message); }
  })()), [track]);
  const loadTotals = useCallback(() => track((async () => {
    try {
      const [fields, plots] = await Promise.all([api('fields'), api('plots')]);
      setTotals({ fields, plots });
    } catch (cause) { setError(cause.message); }
  })()), [track]);
  const settle = (key, id) => () => setLoaded(current => ({ ...current, [key]: id }));
  useEffect(() => {
    Promise.all([load('clients'), load('crops'), load('campaigns'), load('providers'), loadTotals()]).then(settle('base', true));
  }, [load, loadTotals]);
  useEffect(() => {
    if (!path.clientId) return;
    Promise.all([load('commercial-firms', `clientId=${path.clientId}`), load('fields', `clientId=${path.clientId}`)])
      .then(settle('clientId', path.clientId));
  }, [path.clientId, load]);
  useEffect(() => { if (path.fieldId) load('plots', `fieldId=${path.fieldId}`).then(settle('fieldId', path.fieldId)); }, [path.fieldId, load]);
  useEffect(() => { if (path.plotId) load('plot-crops', `plotId=${path.plotId}`).then(settle('plotId', path.plotId)); }, [path.plotId, load]);
  const ready = loaded.base && ['clientId', 'fieldId', 'plotId'].every(key => !path[key] || loaded[key] === path[key]);

  const client = rows.clients.find(row => row.id === path.clientId);
  const field = path.clientId && rows.fields.find(row => row.id === path.fieldId && row.clientId === path.clientId);
  const plot = field && rows.plots.find(row => row.id === path.plotId && row.fieldId === field.id);
  const level = plot ? 'plots' : field ? 'fields' : client ? 'clients' : null;

  function go(next) {
    const cleared = {};
    if (next.clientId !== path.clientId) Object.assign(cleared, { 'commercial-firms': [], fields: [] });
    if (next.fieldId !== path.fieldId) cleared.plots = [];
    if (next.plotId !== path.plotId) cleared['plot-crops'] = [];
    setRows(current => ({ ...current, ...cleared }));
    setPath(next); setEditor(null); setFormKey(key => key + 1); setError(''); setNotice('');
  }
  function open(entity, record = null) { setEditor({ entity, record }); setFormKey(key => key + 1); setError(''); setNotice(''); }
  function reloadFor(entity) {
    if (entity === 'clients' || entity === 'fields' || entity === 'plots') loadTotals();
    if (entity === 'clients') return load('clients');
    if (entity === 'commercial-firms' || entity === 'fields') return load(entity, `clientId=${path.clientId}`);
    if (entity === 'plots') return load('plots', `fieldId=${path.fieldId}`);
    return load('plot-crops', `plotId=${path.plotId}`);
  }
  // A field moved to another client or a plot moved to another field opens its new parent (S3).
  function saved(entity, record, result) {
    const moved = record && result && moveNotice(entity, record, result, { clients: rows.clients, fields: totals.fields });
    if (!moved) { setEditor(null); setFormKey(key => key + 1); setNotice(''); reloadFor(entity); return; }
    loadTotals();
    if (entity === 'fields') go({ clientId: result.clientId, fieldId: null, plotId: null });
    else go({ clientId: totals.fields.find(row => row.id === result.fieldId)?.clientId ?? path.clientId, fieldId: result.fieldId, plotId: null });
    setNotice(moved);
  }
  const refresh = entity => () => { setError(''); reloadFor(entity); };

  const target = editor ?? { entity: level ? children[level] : 'clients', record: null };
  const parent = { clientId: path.clientId, fieldId: path.fieldId, plotId: path.plotId };
  const optionSets = {
    crops: rows.crops.map(row => ({ value: row.id, label: row.name })),
    campaigns: rows.campaigns.map(row => ({ value: row.id, label: row.name })),
    clients: rows.clients.map(row => ({ value: row.id, label: row.name, detail: row.taxId ? `CUIT ${row.taxId}` : undefined })),
    fieldsByClient: fieldMoveGroups(rows.clients, totals.fields),
  };
  const missing = target.entity === 'plot-crops' && !target.record && (!rows.crops.length || !rows.campaigns.length)
    ? 'Primero crea cultivos y campañas en sus catálogos.' : undefined;
  const within = { 'commercial-firms': client, fields: client, plots: field, 'plot-crops': plot }[target.entity];
  const context = within && !target.record ? {
    'commercial-firms': `Se crea en el cliente ${client.name}.`, fields: `Se crea en el cliente ${client.name}.`,
    plots: `Se crea en el campo ${field?.name}.`, 'plot-crops': `Se crea en el lote ${plot && rowLabel('plots', plot)}.`,
  }[target.entity] : undefined;
  const nameOf = (list, id) => list.find(row => row.id === id)?.name ?? 'Sin referencia';
  const plotCrops = [...rows['plot-crops']].sort((a, b) =>
    nameOf(rows.campaigns, b.campaignId).localeCompare(nameOf(rows.campaigns, a.campaignId)) || a.position - b.position);

  return <>
    <SectionHeader identity={identity} title="Clientes" count={level || !loaded.base ? undefined : rows.clients.length}
      description="Aquí administras cada cliente con sus firmas comerciales y sus campos; cada campo, con sus lotes; cada lote, con sus cultivos por campaña y sus análisis de suelo." />
    <nav className="bo-breadcrumb" aria-label="Ubicación"><ol>
      <li><button type="button" onClick={() => go({ clientId: null, fieldId: null, plotId: null })} aria-current={!level ? 'page' : undefined}>Clientes</button></li>
      {client && <li><button type="button" onClick={() => go({ clientId: client.id, fieldId: null, plotId: null })} aria-current={level === 'clients' ? 'page' : undefined}>{client.name}</button></li>}
      {field && <li><button type="button" onClick={() => go({ ...path, plotId: null })} aria-current={level === 'fields' ? 'page' : undefined}>{field.name}</button></li>}
      {plot && <li><button type="button" aria-current="page" onClick={() => go(path)}>{rowLabel('plots', plot)}</button></li>}
    </ol></nav>
    {notice && <p className="bo-notice" role="status">{notice}</p>}
    {error && <p className="bo-error" role="alert">{error}</p>}
    <Busy ready={ready} busy={loading} className="bo-columns"
      label={path.plotId ? 'Cargando lote…' : path.fieldId ? 'Cargando campo…' : path.clientId ? 'Cargando cliente…' : 'Cargando clientes…'}>
      <div className="bo-stack">
      {!level && <RecordList entity="clients" rows={rows.clients} searchable onRefresh={refresh('clients')}
        onSelect={row => go({ clientId: row.id, fieldId: null, plotId: null })} selectLabel="Ver campos y lotes" onEdit={row => open('clients', row)}
        detail={row => [clientCounts(totals.fields, totals.plots, row.id), entities.clients.detail(row)].filter(Boolean).join(' · ')}
        description="Abre un cliente para ver y cargar sus firmas comerciales, sus campos y los lotes de cada campo."
        empty="Aún no hay clientes. Crea el primero desde el formulario." />}
      {level === 'clients' && <>
        <RecordDetail title="Datos del cliente" onEdit={() => open('clients', client)} items={[
          ['Nombre', client.name], ['CUIT', client.taxId], ['Productores', client.producers], ['Email', client.email],
          ['Celular', client.phone], ['Dirección', client.address]]} />
        <RecordList entity="commercial-firms" rows={rows['commercial-firms']} onRefresh={refresh('commercial-firms')}
          onNew={() => open('commercial-firms')} newLabel="Nueva firma" onEdit={row => open('commercial-firms', row)}
          empty="Este cliente aún no tiene firmas comerciales." />
        <RecordList entity="fields" rows={rows.fields} onRefresh={refresh('fields')}
          onSelect={row => go({ clientId: client.id, fieldId: row.id, plotId: null })} selectLabel="Ver lotes" onEdit={row => open('fields', row)}
          detail={row => [entities.fields.detail(row), fieldPlotCount(totals.plots, row.id)].filter(Boolean).join(' · ')}
          description={`Los campos de ${client.name} y sus lotes se administran aquí: crea un campo con “Nuevo campo” y ábrelo para cargar sus lotes.`}
          onNew={() => open('fields')} newLabel="Nuevo campo" empty="Este cliente aún no tiene campos." />
        <ClientProviderLinks key={`providers-${client.id}`} side="client" ownerId={client.id} options={rows.providers} describe={providerLabel}
          csrfToken={identity.csrfToken} title="Proveedores del cliente" empty={`${client.name} aún no tiene proveedores vinculados.`} />
        <ClientComparisons key={`quotes-${client.id}`} client={client} navigate={navigate} />
      </>}
      {level === 'fields' && <>
        <RecordDetail title="Datos del campo" onEdit={() => open('fields', field)} items={[
          ['Nombre', field.name], ['Tenencia', field.tenure && optionLabel(tenures, field.tenure)], ['Localidad', field.locality]]} />
        <RecordList entity="plots" rows={rows.plots} onRefresh={refresh('plots')}
          description={`Los lotes del campo ${field.name}. Cada lote se puede elegir en una orden de trabajo del cliente.`}
          onSelect={row => go({ ...path, plotId: row.id })} onEdit={row => open('plots', row)}
          onNew={() => open('plots')} newLabel="Nuevo lote" empty="Este campo aún no tiene lotes." />
      </>}
      {level === 'plots' && <>
        <RecordDetail title="Datos del lote" onEdit={() => open('plots', plot)} items={[
          ['Código', plot.code], ['Nombre', plot.name], ['Superficie', `${plot.areaHectares} ha`]]} />
        <RecordList entity="plot-crops" rows={plotCrops} onRefresh={refresh('plot-crops')}
          onEdit={row => open('plot-crops', row)} onNew={() => open('plot-crops')} newLabel="Nuevo cultivo"
          label={row => `${nameOf(rows.campaigns, row.campaignId)} · ${row.position} · ${nameOf(rows.crops, row.cropId)}`}
          detail={plotCropDetail}
          empty="Este lote aún no tiene cultivos cargados por campaña." />
        <SoilAnalysesPanel key={plot.id} plot={plot} campaigns={rows.campaigns} csrfToken={identity.csrfToken} />
      </>}
    </div>
      <RecordForm key={`${target.entity}-${target.record?.id ?? 'new'}-${formKey}`} entity={target.entity} record={target.record}
        parent={parent} optionSets={optionSets} csrfToken={identity.csrfToken} missing={missing} context={context}
        onCancel={editor ? () => { setEditor(null); setFormKey(key => key + 1); } : undefined} onSaved={result => saved(target.entity, target.record, result)} />
    </Busy>
  </>;
}
