'use client';

import { useCallback, useEffect, useState } from 'react';
import { api } from './api.mjs';
import { InputPrices } from './providers.jsx';
import { RecordForm, RecordList, SectionHeader } from './records.jsx';
import { entities, websiteHref } from './schema.mjs';

const descriptions = {
  contractors: 'Quienes realizan las labores de una orden de trabajo. "Equipo propio" marca la maquinaria del cliente.',
  inputs: 'Productos que se aplican en una labor. El rango de dosis es una referencia para advertir, nunca se aplica solo. Al editar un insumo ves el último precio de cada proveedor.',
  'labor-types': 'Qué se hace en el lote, por ejemplo "Pulverización terrestre", agrupado por categoría. El momento del ciclo es una etapa aparte.',
  stages: 'Cuándo ocurre una labor en el ciclo del cultivo, ordenadas por su número de orden.',
};

// A contractor row: its summary plus the website as an external link when it is a web address.
function contractorDetail(row) {
  const summary = entities.contractors.detail(row);
  const href = websiteHref(row.website);
  if (!row.website) return summary;
  const site = href ? <a href={href} target="_blank" rel="noopener">{row.website}</a> : row.website;
  return <>{summary && `${summary} · `}{site}</>;
}
const details = { contractors: contractorDetail };

// A flat catalog (contractors, inputs, labors, stages, crops, campaigns): list plus create/edit form.
export default function CatalogSection({ entity, identity }) {
  const meta = entities[entity];
  const [rows, setRows] = useState([]);
  const [editing, setEditing] = useState(null);
  const [formKey, setFormKey] = useState(0);
  const [error, setError] = useState('');

  const load = useCallback(() => {
    setError('');
    return api(entity).then(setRows).catch(cause => setError(cause.message));
  }, [entity]);
  useEffect(() => { load(); }, [load]);

  function reset() { setEditing(null); setFormKey(key => key + 1); }
  const form = <RecordForm key={`${editing?.id ?? 'new'}-${formKey}`} entity={entity} record={editing} csrfToken={identity.csrfToken}
    onCancel={editing ? reset : undefined} onSaved={() => { reset(); load(); }} />;
  // An edited input also shows its latest reference price per provider (S3), under the form.
  return <>
    <SectionHeader identity={identity} title={meta.title} description={descriptions[entity]} count={rows.length} />
    {error && <p className="bo-error" role="alert">{error}</p>}
    <div className="bo-columns">
      <RecordList entity={entity} rows={meta.sort ? meta.sort(rows) : rows} searchable={Boolean(meta.search)} onRefresh={load} detail={details[entity]}
        onEdit={row => { setEditing(row); setFormKey(key => key + 1); }} selectedId={editing?.id}
        empty={`Aún no hay ${meta.title.toLowerCase()}. Crea el primer registro desde el formulario.`} />
      {entity === 'inputs' && editing ? <div className="bo-stack bo-side">{form}<InputPrices key={editing.id} input={editing} /></div> : form}
    </div>
  </>;
}
