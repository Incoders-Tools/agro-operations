'use client';

import { useCallback, useEffect, useState } from 'react';
import { api, send } from './api.mjs';
import { ComboField, Field, optionNodes } from './field.jsx';
import { FormTopBar, useDirtyState } from './form-actions.jsx';
import { ValidatedForm } from './form-validation.jsx';
import { Busy, RecordList, SectionHeader, usePending } from './records.jsx';
import { optionLabel, recipeStages, rowLabel } from './schema.mjs';
import { displayUnit } from './units.mjs';

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
    primaryLabel: row => `${optionLabel(recipeStages, row.stage)} · ${row.targetUse}`,
  },
  'recipe-lines': {
    title: 'Líneas de receta', singular: 'línea de receta', feminine: true, dose: true,
    parents: [{ group: 'recipes', key: 'recipeId', label: 'Receta' }],
    primaryLabel: row => `${row.productName} · ${row.doseValue} ${displayUnit(row.doseUnit)}`,
  },
  'import-records': {
    title: 'Procedencia', singular: 'registro de procedencia', readOnly: true, parents: [],
    primaryLabel: row => `${row.entityType} · ${row.sourceReference}`,
    secondaryLabel: row => row.verified ? 'Verificado' : 'Importado, sin verificar',
  },
};
const emptyForm = { parents: {}, area: '', stage: 'presiembra', targetUse: '', responsibleEngineer: '', productName: '', doseValue: '', doseUnit: '' };
const labelOf = (group, row) => genericGroups[group]?.primaryLabel(row) ?? rowLabel(group, row);

export default function GenericSection({ group, identity }) {
  const meta = genericGroups[group];
  const [rows, setRows] = useState({});
  const [form, setForm] = useState(emptyForm);
  const [editing, setEditing] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [loading, track] = usePending();
  const [round, setRound] = useState(0);
  const { dirty, rebase } = useDirtyState(form);
  // Opens `next` in the form as its new starting point; a new round clears the validation messages.
  function start(next, record) { setEditing(record); setForm(next); rebase(next); setRound(value => value + 1); }

  const refresh = useCallback(() => track((async () => {
    setError('');
    try {
      const keys = [group, ...meta.parents.map(parent => parent.group)];
      setRows(Object.fromEntries(await Promise.all(keys.map(async key => [key, await api(key)]))));
    } catch (cause) { setError(cause.message); }
    finally { setLoaded(true); }
  })()), [group, meta, track]);
  useEffect(() => { refresh(); }, [refresh]);

  function edit(row) {
    start({
      ...emptyForm,
      area: meta.area ? String(row.areaHectares) : '',
      stage: meta.stage ? row.stage : 'presiembra',
      targetUse: meta.stage ? row.targetUse : '',
      responsibleEngineer: meta.stage ? row.responsibleEngineer : '',
      productName: meta.dose ? row.productName : '',
      doseValue: meta.dose ? String(row.doseValue) : '',
      doseUnit: meta.dose ? row.doseUnit : '',
    }, row);
    setError('');
  }
  async function save() {
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
      start(emptyForm, null);
    } catch (cause) { setError(cause.message); }
    finally { setBusy(false); }
  }

  const list = rows[group] ?? [];
  const parentName = (parent, id) => {
    const match = rows[parent.group]?.find(row => row.id === id);
    return match ? labelOf(parent.group, match) : 'Sin referencia';
  };
  const parentsReady = meta.parents.every(parent => rows[parent.group]?.length);
  const text = (id, label, key, props = {}, words = {}) => <Field id={id} label={label} {...words}>
    <input id={id} value={form[key]} onChange={event => setForm({ ...form, [key]: event.target.value })} required {...props} /></Field>;
  return <>
    <SectionHeader identity={identity} title={meta.title} count={loaded ? list.length : undefined} />
    {error && <p className="bo-error" role="alert">{error}</p>}
    <Busy ready={loaded} busy={loading} label={`Cargando ${meta.title.toLowerCase()}…`} className="bo-columns">
      <RecordList entity={group} title="Registros" rows={list} onRefresh={refresh} label={meta.primaryLabel} selectedId={editing?.id}
        detail={meta.secondaryLabel ?? (row => `${meta.parents.map(parent => parentName(parent, row[parent.key])).join(' · ')} · Revisión ${row.revision}`)}
        onEdit={meta.readOnly ? undefined : edit} empty={`Aún no hay ${meta.title.toLowerCase()}. Crea el primer registro desde el formulario.`} />
      {!meta.readOnly && <section className="bo-editor"><h2>{editing ? `Editar ${meta.singular}` : `${meta.feminine ? 'Nueva' : 'Nuevo'} ${meta.singular}`}</h2>
        <ValidatedForm key={round} onSubmit={save}>
          <FormTopBar id={`bo-${group}-form`} title={editing ? `Editar ${meta.singular}` : `${meta.feminine ? 'Nueva' : 'Nuevo'} ${meta.singular}`}
            dirty={dirty} canDiscard={Boolean(editing)} onDiscard={() => { start(emptyForm, null); setError(''); }} busy={busy}
            disabled={!editing && !parentsReady} submitLabel={busy ? 'Guardando…' : editing ? 'Guardar cambios' : `Crear ${meta.singular}`} />
          {!editing && meta.parents.map(parent => <ComboField key={parent.key} id={`bo-parent-${parent.key}`} label={parent.label}
            value={form.parents[parent.key] ?? ''} placeholder="Busca y elige un registro"
            options={(rows[parent.group] ?? []).map(option => ({ value: option.id, label: labelOf(parent.group, option) }))}
            onChange={value => setForm(current => ({ ...current, parents: { ...current.parents, [parent.key]: value } }))} />)}
          {meta.area && text('bo-area', 'Superficie (ha)', 'area', { type: 'number', min: '0.0001', max: '99999999.9999', step: '0.0001' })}
          {meta.stage && <>
            <Field id="bo-stage" label="Etapa del ciclo"><select id="bo-stage" value={form.stage} onChange={event => setForm({ ...form, stage: event.target.value })}>
              {optionNodes(recipeStages)}</select></Field>
            {text('bo-target', 'Uso objetivo', 'targetUse', { placeholder: 'Ej. Barbecho', maxLength: 200 })}
            {text('bo-engineer', 'Ingeniero responsable', 'responsibleEngineer', { maxLength: 200 })}
          </>}
          {meta.dose && <>
            {text('bo-product', 'Producto (ficticio por ahora)', 'productName', { maxLength: 200 }, { noun: 'el producto' })}
            {text('bo-dose', 'Dosis', 'doseValue', { type: 'number', min: '0.0001', max: '99999999.9999', step: '0.0001' })}
            {text('bo-dose-unit', 'Unidad', 'doseUnit', { placeholder: 'Ej. l/ha', maxLength: 50 })}
          </>}
          <div className="bo-actions"><button type="submit" disabled={busy || (!editing && !parentsReady)}>{busy ? 'Guardando…' : editing ? 'Guardar cambios' : `Crear ${meta.singular}`}</button>
            {editing && <button type="button" onClick={() => start(emptyForm, null)}>Cancelar</button>}</div>
          {!editing && !parentsReady && <p>Primero crea los registros relacionados: {meta.parents.map(parent => parent.label.toLowerCase()).join(', ')}.</p>}
        </ValidatedForm></section>}
    </Busy>
  </>;
}
