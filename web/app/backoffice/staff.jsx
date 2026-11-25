'use client';

import { useCallback, useEffect, useState } from 'react';
import { api } from './api.mjs';
import { Select } from './providers.jsx';
import { Busy, RecordForm, RecordList, SectionHeader, usePending } from './records.jsx';
import { staffDetail, staffQuery, staffRoles } from './staff.mjs';

const activeOptions = [{ value: 'true', label: 'Activos' }, { value: 'false', label: 'Inactivos' }];

// Company staff (S3, L2): list searched by name and filtered by role and active, with the create and edit form. Staff is
// never deleted; a member who leaves is deactivated.
export default function StaffSection({ identity }) {
  const [rows, setRows] = useState([]);
  const [filters, setFilters] = useState({ role: '', active: '' });
  const [editing, setEditing] = useState(null);
  const [formKey, setFormKey] = useState(0);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [loaded, setLoaded] = useState(false);
  const [loading, track] = usePending();

  const load = useCallback(() => {
    setError('');
    return track(api(staffQuery(filters)).then(setRows).catch(cause => setError(cause.message))).finally(() => setLoaded(true));
  }, [filters, track]);
  useEffect(() => { load(); }, [load]);

  function reset() { setEditing(null); setFormKey(key => key + 1); }
  function saved(member, edited) {
    setNotice(edited ? `Se guardaron los datos de ${member.fullName}.` : `Se agregó a ${member.fullName} al personal.`);
    reset(); load();
  }
  const filtered = filters.role || filters.active;
  return <>
    <SectionHeader identity={identity} title="Personal" count={loaded ? rows.length : undefined}
      description="Personas de la empresa. Los ingenieros activos son los que se eligen como responsables de una orden de trabajo; a quien deja la empresa se lo desactiva, no se lo borra." />
    {notice && <p className="bo-notice" role="status">{notice}</p>}
    {error && <p className="bo-error" role="alert">{error}</p>}
    <Busy ready={loaded} busy={loading} label="Cargando personal…" className="bo-columns">
      <div className="bo-stack">
        <div className="bo-filters">
          <Select id="bo-staff-filter-role" label="Rol" required={false} value={filters.role} options={staffRoles} placeholder="Todos"
            onChange={role => setFilters(current => ({ ...current, role }))} />
          <Select id="bo-staff-filter-active" label="Activo" required={false} value={filters.active} options={activeOptions} placeholder="Todos"
            onChange={active => setFilters(current => ({ ...current, active }))} />
        </div>
        <RecordList entity="staff" rows={rows} searchable onRefresh={load} detail={staffDetail} selectedId={editing?.id}
          onEdit={row => { setEditing(row); setFormKey(key => key + 1); setNotice(''); }}
          empty={filtered ? 'Nadie del personal coincide con los filtros.' : 'Aún no hay personal. Agrega a la primera persona desde el formulario.'} />
      </div>
      <RecordForm key={`${editing?.id ?? 'new'}-${formKey}`} entity="staff" record={editing} csrfToken={identity.csrfToken}
        onCancel={editing ? reset : undefined} onSaved={saved} />
    </Busy>
  </>;
}
