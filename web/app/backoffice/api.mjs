// Fetch wrapper and API error mapping for the private backoffice. The API answers in English; the UI shows Spanish.

const stale = 'Otra persona modificó este registro. Actualiza antes de guardar.';
const generic = 'Revisa los datos e intenta de nuevo.';

const conflicts = {
  'Duplicate name': 'Ya existe un registro con ese nombre.',
  'Duplicate code': 'Ya existe un lote con ese código.',
  'Duplicate position': 'Ese lote ya tiene un cultivo en esa posición para la campaña.',
  'Stale revision': stale,
};

const required = {
  Name: 'El nombre es obligatorio.',
  'Commercial name': 'El nombre comercial es obligatorio.',
  Unit: 'La unidad es obligatoria.',
  'Target use': 'El uso objetivo es obligatorio.',
  'Responsible engineer': 'El ingeniero responsable es obligatorio.',
  'Product name': 'El producto es obligatorio.',
  'Dose unit': 'La unidad de dosis es obligatoria.',
};

const validations = {
  'CUIT must have 11 digits': 'El CUIT debe tener 11 dígitos.',
  'Email must contain a single @ with text on both sides': 'El email debe tener un único @ con texto a ambos lados.',
  'Area must be positive and fit numeric(12,4)': 'La superficie debe ser mayor que cero y tener hasta 4 decimales.',
  'Code must be at most 40 characters': 'El código admite hasta 40 caracteres.',
  'Position must be 1 or 2': 'La posición debe ser 1 o 2.',
  'Abbreviation must be at most 12 characters': 'La abreviatura admite hasta 12 caracteres.',
  'Mix order must be between 1 and 20': 'El orden de mezcla debe estar entre 1 y 20.',
  'Minimum dose must be positive and fit numeric(12,4)': 'La dosis mínima debe ser mayor que cero y tener hasta 4 decimales.',
  'Maximum dose must be positive and fit numeric(12,4)': 'La dosis máxima debe ser mayor que cero y tener hasta 4 decimales.',
  'Minimum dose must not exceed maximum dose': 'La dosis mínima no puede superar la máxima.',
  'Reference cost must be zero or positive and fit numeric(14,4)': 'El costo de referencia debe ser cero o positivo, con hasta 4 decimales.',
  'Dose must be positive and fit numeric(12,4)': 'La dosis debe ser mayor que cero y tener hasta 4 decimales.',
};

// Messages that list the accepted values end with that list, so they match by prefix.
const choices = [
  ['Tenure must be one of', 'Elige una tenencia válida.'],
  ['Role must be one of', 'Elige renta o servicio.'],
  ['Category must be one of', 'Elige una categoría válida.'],
  ['Stage must be', 'Elige una etapa válida.'],
];

function validationMessage(text) {
  if (typeof text !== 'string') return generic;
  if (validations[text]) return validations[text];
  const field = text.endsWith(' is required') ? text.slice(0, -' is required'.length) : null;
  if (field && required[field]) return required[field];
  return choices.find(([prefix]) => text.startsWith(prefix))?.[1] ?? generic;
}

export function errorMessage(status, body, { login = false } = {}) {
  const text = body && typeof body === 'object' ? body.error : undefined;
  if (status === 401) return login ? 'Contraseña incorrecta. Inténtalo de nuevo.' : 'La sesión terminó. Ingresa de nuevo.';
  if (status === 403) return 'No se pudo validar la solicitud. Actualiza la sesión.';
  if (status === 409) return conflicts[text] ?? stale;
  if (status === 404) return 'El registro relacionado ya no existe. Actualiza los listados.';
  if (status >= 500) return 'La API privada no responde. Inicia el servidor local.';
  if (status === 400) return validationMessage(text);
  return generic;
}

export const sessionEndedMessage = errorMessage(401, null);

export async function api(path, options = {}) {
  const response = await fetch(`/api/v1/${path}`, { cache: 'no-store', credentials: 'same-origin', ...options });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(errorMessage(response.status, body, { login: path === 'session' && options.method === 'POST' }));
  }
  return response.status === 204 ? null : response.json();
}

// Writes carry the session CSRF token; PUT bodies include the record revision (see schema.mjs payloadFor).
export function send(path, method, value, csrfToken) {
  return api(path, {
    method,
    headers: { 'Content-Type': 'application/json', 'X-Agro-CSRF': csrfToken },
    body: JSON.stringify(value),
  });
}
