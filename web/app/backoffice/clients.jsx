'use client';

import { useCallback, useEffect, useState } from 'react';
import { api } from './api.mjs';
import { RecordDetail, RecordForm, RecordList, SectionHeader } from './records.jsx';
import { optionLabel, roles, rowLabel, tenures } from './schema.mjs';

const children = { clients: 'fields', fields: 'plots', plots: 'plot-crops' };

// Client -> fields -> plots -> crops per campaign (S4). Children are always created inside the selected parent.
export default function ClientsSection({ identity }) {
  const [rows, setRows] = useState({ clients: [], 'commercial-firms': [], fields: [], plots: [], 'plot-crops': [], crops: [], campaigns: [] });
  const [path, setPath] = useState({ clientId: null, fieldId: null, plotId: null });
  // editor: { entity, record } for an explicit edit or "new" target; null shows the default "new child" form.
  const [editor, setEditor] = useState(null);
  const [formKey, setFormKey] = useState(0);
  const [error, setError] = useState('');

  const load = useCallback(async (entity, query) => {
    try {
      const list = await api(query ? `${entity}?${query}` : entity);
      setRows(current => ({ ...current, [entity]: list }));
    } catch (cause) { setError(cause.message); }
  }, []);
  useEffect(() => { load('clients'); load('crops'); load('campaigns'); }, [load]);
  useEffect(() => {
    if (!path.clientId) return;
    load('commercial-firms', `clientId=${path.clientId}`); load('fields', `clientId=${path.clientId}`);
  }, [path.clientId, load]);
  useEffect(() => { if (path.fieldId) load('plots', `fieldId=${path.fieldId}`); }, [path.fieldId, load]);
  useEffect(() => { if (path.plotId) load('plot-crops', `plotId=${path.plotId}`); }, [path.plotId, load]);

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
    setPath(next); setEditor(null); setFormKey(key => key + 1); setError('');
  }
  function open(entity, record = null) { setEditor({ entity, record }); setFormKey(key => key + 1); setError(''); }
  function reloadFor(entity) {
    if (entity === 'clients') return load('clients');
    if (entity === 'commercial-firms' || entity === 'fields') return load(entity, `clientId=${path.clientId}`);
    if (entity === 'plots') return load('plots', `fieldId=${path.fieldId}`);
    return load('plot-crops', `plotId=${path.plotId}`);
  }
  function saved(entity) { setEditor(null); setFormKey(key => key + 1); reloadFor(entity); }
  const refresh = entity => () => { setError(''); reloadFor(entity); };

  const target = editor ?? { entity: level ? children[level] : 'clients', record: null };
  const parent = { clientId: path.clientId, fieldId: path.fieldId, plotId: path.plotId };
  const optionSets = {
    crops: rows.crops.map(row => ({ value: row.id, label: row.name })),
    campaigns: rows.campaigns.map(row => ({ value: row.id, label: row.name })),
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
    <SectionHeader identity={identity} title="Clientes" count={level ? undefined : rows.clients.length}
      description="Cada cliente tiene sus firmas comerciales y sus campos; cada campo, sus lotes; cada lote, sus cultivos por campaña." />
    <nav className="bo-breadcrumb" aria-label="Ubicación"><ol>
      <li><button type="button" onClick={() => go({ clientId: null, fieldId: null, plotId: null })} aria-current={!level ? 'page' : undefined}>Clientes</button></li>
      {client && <li><button type="button" onClick={() => go({ clientId: client.id, fieldId: null, plotId: null })} aria-current={level === 'clients' ? 'page' : undefined}>{client.name}</button></li>}
      {field && <li><button type="button" onClick={() => go({ ...path, plotId: null })} aria-current={level === 'fields' ? 'page' : undefined}>{field.name}</button></li>}
      {plot && <li><button type="button" aria-current="page" onClick={() => go(path)}>{rowLabel('plots', plot)}</button></li>}
    </ol></nav>
    {error && <p className="bo-error" role="alert">{error}</p>}
    <div className="bo-columns"><div className="bo-stack">
      {!level && <RecordList entity="clients" rows={rows.clients} searchable onRefresh={refresh('clients')}
        onSelect={row => go({ clientId: row.id, fieldId: null, plotId: null })} onEdit={row => open('clients', row)}
        empty="Aún no hay clientes. Crea el primero desde el formulario." />}
      {level === 'clients' && <>
        <RecordDetail title="Datos del cliente" onEdit={() => open('clients', client)} items={[
          ['Nombre', client.name], ['CUIT', client.taxId], ['Productores', client.producers], ['Email', client.email],
          ['Teléfono', client.phone], ['Dirección', client.address]]} />
        <RecordList entity="commercial-firms" rows={rows['commercial-firms']} onRefresh={refresh('commercial-firms')}
          onNew={() => open('commercial-firms')} newLabel="Nueva firma" onEdit={row => open('commercial-firms', row)}
          empty="Este cliente aún no tiene firmas comerciales." />
        <RecordList entity="fields" rows={rows.fields} onRefresh={refresh('fields')}
          onSelect={row => go({ clientId: client.id, fieldId: row.id, plotId: null })} onEdit={row => open('fields', row)}
          onNew={() => open('fields')} newLabel="Nuevo campo" empty="Este cliente aún no tiene campos." />
      </>}
      {level === 'fields' && <>
        <RecordDetail title="Datos del campo" onEdit={() => open('fields', field)} items={[
          ['Nombre', field.name], ['Tenencia', field.tenure && optionLabel(tenures, field.tenure)], ['Localidad', field.locality]]} />
        <RecordList entity="plots" rows={rows.plots} onRefresh={refresh('plots')}
          onSelect={row => go({ ...path, plotId: row.id })} onEdit={row => open('plots', row)}
          onNew={() => open('plots')} newLabel="Nuevo lote" empty="Este campo aún no tiene lotes." />
      </>}
      {level === 'plots' && <>
        <RecordDetail title="Datos del lote" onEdit={() => open('plots', plot)} items={[
          ['Código', plot.code], ['Nombre', plot.name], ['Superficie', `${plot.areaHectares} ha`]]} />
        <RecordList entity="plot-crops" rows={plotCrops} onRefresh={refresh('plot-crops')}
          onEdit={row => open('plot-crops', row)} onNew={() => open('plot-crops')} newLabel="Nuevo cultivo"
          label={row => `${nameOf(rows.campaigns, row.campaignId)} · ${row.position} · ${nameOf(rows.crops, row.cropId)}`}
          detail={row => [optionLabel(roles, row.role), row.variety].filter(Boolean).join(' · ')}
          empty="Este lote aún no tiene cultivos cargados por campaña." />
      </>}
    </div>
      <RecordForm key={`${target.entity}-${target.record?.id ?? 'new'}-${formKey}`} entity={target.entity} record={target.record}
        parent={parent} optionSets={optionSets} csrfToken={identity.csrfToken} missing={missing} context={context}
        onCancel={editor ? () => { setEditor(null); setFormKey(key => key + 1); } : undefined} onSaved={() => saved(target.entity)} />
    </div>
  </>;
}
