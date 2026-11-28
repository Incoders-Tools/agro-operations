// Dirty state of a full form (S3): whether its values differ from the ones it opened with, so "Descartar" asks for
// confirmation only when something would be lost. Kept apart from form-actions.jsx so node:test can load it.

const empty = value => value === undefined || value === null || value === '';

/** Deep equality of form values; '', null and a missing value are the same empty value. */
export function sameFormValue(a, b) {
  if (empty(a) && empty(b)) return true;
  if (Array.isArray(a) || Array.isArray(b)) {
    return Array.isArray(a) && Array.isArray(b) && a.length === b.length && a.every((item, index) => sameFormValue(item, b[index]));
  }
  if (a && b && typeof a === 'object' && typeof b === 'object') {
    return [...new Set([...Object.keys(a), ...Object.keys(b)])].every(key => sameFormValue(a[key], b[key]));
  }
  return Object.is(a, b);
}

export const isDirty = (initial, current) => !sameFormValue(initial, current);
