'use client';

import { useCallback, useEffect, useState } from 'react';
import { api, send } from './api.mjs';
import { Field } from './field.jsx';
import { RecordList, SectionHeader } from './records.jsx';
import { rowLabel } from './schema.mjs';

// Planning and provenance sections that keep the earlier generic editor.
export const genericGroups = {
  'plot-campaign-areas': {
    title: 'Superficie por campaña', singular: 'registro de superficie', area: true,
    parents: [{ group: 'plots', key: 'plotId', label: 'Lote' }, { group: 'campaigns', key: 'campaignId', label: 'Campaña' }],
    primaryLabel: row => `${row.areaHectares} ha`,
  },
  recipes: {
    title: 'Recetas', singular: 'receta', feminine: true, stage: true,
    parents: [
      { group: 'plots', key: 'plotId', label: 'Lote' },
      { group: 'campaigns', key: 'campaignId', label: 'Campaña' },
      { group: 'labor-types', key: 'laborTypeId', label: 'Labor' },
    ],
    primaryLabel: row => `${row.stage} · ${row.targetUse}`,
  },
  'recipe-lines': {
    title: 'Líneas de receta', singular: 'línea de receta', feminine: true, dose: true,
    parents: [{ group: 'recipes', key: 'recipeId', label: 'Receta' }],
    primaryLabel: row => `${row.productName} · ${row.doseValue} ${row.doseUnit}`,
  },
  'import-records': {
    title: 'Procedencia', singular: 'registro de procedencia', readOnly: true, parents: [],
    primaryLabel: row => `${row.entityType} · ${row.sourceReference}`,
    secondaryLabel: row => row.verified ? 'Verificado' : 'Importado, sin verificar',
  },
};
const emptyForm = { parents: {}, area: '', stage: 'presiembra', targetUse: '', responsibleEngineer: '', productName: '', doseValue: '', doseUnit: '' };
const stages = ['presiembra', 'preemergencia', 'postemergencia'];
const labelOf = (group, row) => genericGroups[group]?.primaryLabel(row) ?? rowLabel(group, row);

export default function GenericSection({ group, identity }) {
  const meta = genericGroups[group];
  const [rows, setRows] = useState({});
  const [form, setForm] = useState(emptyForm);
  const [editing, setEditing] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const refresh = useCallback(async () => {
    setError('');
    try {
      const keys = [group, ...meta.parents.map(parent => parent.group)];
      setRows(Object.fromEntries(await Promise.all(keys.map(async key => [key, await api(key)]))));
    } catch (cause) { setError(cause.message); }
  }, [group, meta]);
  useEffect(() => { refresh(); }, [refresh]);

  function edit(row) {
    setEditing(row);
    setForm({
      ...emptyForm,
      area: meta.area ? String(row.areaHectares) : '',
      stage: meta.stage ? row.stage : 'presiembra',
      targetUse: meta.stage ? row.targetUse : '',
      responsibleEngineer: meta.stage ? row.responsibleEngineer : '',
      productName: meta.dose ? row.productName : '',
      doseValue: meta.dose ? String(row.doseValue) : '',
      doseUnit: meta.dose ? row.doseUnit : '',
    });
    setError('');
  }
  async function save(event) {
    event.preventDefault();
    setBusy(true); setError('');
    const value = {};
    if (meta.area) value.areaHectares = Number(form.area);
    if (meta.stage) { value.stage = form.stage; value.targetUse = form.targetUse.trim(); value.responsibleEngineer = form.responsibleEngineer.trim(); }
    if (meta.dose) { value.productName = form.productName.trim(); value.doseValue = Number(form.doseValue); value.doseUnit = form.doseUnit.trim(); }
    if (!editing) for (const parent of meta.parents) value[parent.key] = form.parents[parent.key];
    if (editing) value.revision = editing.revision;
    try {
      await send(editing ? `${group}/${editing.id}` : group, editing ? 'PUT' : 'POST', value, identity.csrfToken);
      await refresh();
      setForm(emptyForm); setEditing(null);
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }

  const list = rows[group] ?? [];
  const parentName = (parent, id) => {
    const match = rows[parent.group]?.find(row => row.id === id);
    return match ? labelOf(parent.group, match) : 'Sin referencia';
  };
  const parentsReady = meta.parents.every(parent => rows[parent.group]?.length);
  const text = (id, label, key, props = {}) => <Field id={id} label={label}>
    <input id={id} value={form[key]} onChange={event => setForm({ ...form, [key]: event.target.value })} required {...props} /></Field>;
  return <>
    <SectionHeader identity={identity} title={meta.title} count={list.length} />
    {error && <p className="bo-error" role="alert">{error}</p>}
    <div className="bo-columns">
      <RecordList entity={group} title="Registros" rows={list} onRefresh={refresh} label={meta.primaryLabel} selectedId={editing?.id}
        detail={meta.secondaryLabel ?? (row => `${meta.parents.map(parent => parentName(parent, row[parent.key])).join(' · ')} · Revisión ${row.revision}`)}
        onEdit={meta.readOnly ? undefined : edit} empty={`Aún no hay ${meta.title.toLowerCase()}. Crea el primer registro desde el formulario.`} />
      {!meta.readOnly && <section className="bo-editor"><h2>{editing ? `Editar ${meta.singular}` : `${meta.feminine ? 'Nueva' : 'Nuevo'} ${meta.singular}`}</h2>
        <form onSubmit={save}>
          {!editing && meta.parents.map(parent => <Field key={parent.key} id={`bo-parent-${parent.key}`} label={parent.label}>
            <select id={`bo-parent-${parent.key}`} value={form.parents[parent.key] ?? ''} required
              onChange={event => setForm({ ...form, parents: { ...form.parents, [parent.key]: event.target.value } })}>
              <option value="">Selecciona un registro</option>
              {(rows[parent.group] ?? []).map(option => <option key={option.id} value={option.id}>{labelOf(parent.group, option)}</option>)}
            </select></Field>)}
          {meta.area && text('bo-area', 'Superficie (ha)', 'area', { type: 'number', min: '0.0001', max: '99999999.9999', step: '0.0001' })}
          {meta.stage && <>
            <Field id="bo-stage" label="Etapa del ciclo"><select id="bo-stage" value={form.stage} onChange={event => setForm({ ...form, stage: event.target.value })}>
              {stages.map(stage => <option key={stage} value={stage}>{stage}</option>)}</select></Field>
            {text('bo-target', 'Uso objetivo', 'targetUse', { placeholder: 'Ej. Barbecho', maxLength: 200 })}
            {text('bo-engineer', 'Ingeniero responsable', 'responsibleEngineer', { maxLength: 200 })}
          </>}
          {meta.dose && <>
            {text('bo-product', 'Producto (ficticio por ahora)', 'productName', { maxLength: 200 })}
            {text('bo-dose', 'Dosis', 'doseValue', { type: 'number', min: '0.0001', max: '99999999.9999', step: '0.0001' })}
            {text('bo-dose-unit', 'Unidad', 'doseUnit', { placeholder: 'Ej. l/ha', maxLength: 50 })}
          </>}
          <div className="bo-actions"><button type="submit" disabled={busy || (!editing && !parentsReady)}>{busy ? 'Guardando…' : editing ? 'Guardar cambios' : `Crear ${meta.singular}`}</button>
            {editing && <button type="button" onClick={() => { setEditing(null); setForm(emptyForm); }}>Cancelar</button>}</div>
          {!editing && !parentsReady && <p>Primero crea los registros relacionados: {meta.parents.map(parent => parent.label.toLowerCase()).join(', ')}.</p>}
        </form></section>}
    </div>
  </>;
}
