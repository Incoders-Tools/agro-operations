// Company staff (S3, L2): list request and row summary. The form and payload come from the `staff` entity in schema.mjs.

import { optionLabel, staffRoles } from './schema.mjs';

export { staffRoles };

/** The list request with the role and active filters ('true' or 'false'), each only when set. */
export function staffQuery({ role, active } = {}) {
  const params = new URLSearchParams();
  if (role) params.set('role', role);
  if (active) params.set('active', active);
  const query = params.toString();
  return query ? `staff?${query}` : 'staff';
}

/** One staff row: title, role, licence and phone, and "Inactivo" for a member who left. */
export const staffDetail = row => [
  row.title, optionLabel(staffRoles, row.role), row.licenseNumber && `Matrícula ${row.licenseNumber}`, row.phone, !row.active && 'Inactivo',
].filter(Boolean).join(' · ');
