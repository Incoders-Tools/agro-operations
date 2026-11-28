// Fetch wrapper and API error mapping for the private backoffice. The API answers in English; the UI shows Spanish.

const stale = 'Otra persona modificó este registro. Actualiza antes de guardar.';
const generic = 'Revisa los datos e intenta de nuevo.';

const conflicts = {
  'Duplicate name': 'Ya existe un registro con ese nombre.',
  'Duplicate code': 'Ya existe un lote con ese código.',
  'Duplicate position': 'Ese lote ya tiene un cultivo en esa posición para la campaña.',
  'Stale revision': stale,
  'Work order is not editable': 'Esta orden ya no se puede editar.',
  'Duplicate number': 'Otra orden tomó ese número. Actualiza e intenta de nuevo.',
  'Duplicate link': 'Ese proveedor ya está vinculado a este cliente.',
  'Comparison is not open': 'Esta comparación ya está decidida. Reábrela para cambiarla.',
  'Comparison is not decided': 'Esta comparación ya está abierta. Actualiza la vista.',
};

const required = {
  Name: 'El nombre es obligatorio.',
  'Commercial name': 'El nombre comercial es obligatorio.',
  Unit: 'La unidad es obligatoria.',
  'Target use': 'El uso objetivo es obligatorio.',
  'Responsible engineer': 'El ingeniero responsable es obligatorio.',
  'Product name': 'El producto es obligatorio.',
  'Dose unit': 'La unidad de dosis es obligatoria.',
  Reason: 'Indica el motivo de la cancelación.',
  'Issued on': 'La fecha es obligatoria.',
  'Sampled on': 'La fecha de muestreo es obligatoria.',
  'Analyzed on': 'La fecha de análisis es obligatoria.',
  Parameter: 'Cada resultado necesita su parámetro.',
  Provider: 'Elige un proveedor.',
  Input: 'Elige un insumo.',
  'Valid on': 'Indica desde cuándo rige el precio.',
  'Received on': 'Indica la fecha de recepción del presupuesto.',
  'Requested on': 'Indica la fecha de solicitud.',
  Title: 'El título es obligatorio.',
  'Full name': 'El nombre completo es obligatorio.',
};

// "<Field> must be at most N characters": the field as the subject of the Spanish message.
const limitedFields = {
  'Full name': 'El nombre completo', Title: 'El título', 'License number': 'La matrícula', Description: 'La descripción',
  Parameter: 'El parámetro', Unit: 'La unidad', Depth: 'La profundidad', Station: 'La estación',
};
const limited = /^(.+) must be at most (\d+) characters$/;

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
  'At least one plot is required': 'Selecciona al menos un lote.',
  'Each plot can appear only once': 'Cada lote puede aparecer una sola vez.',
  'Worked area must be positive and fit numeric(12,4)': 'Las ha a trabajar deben ser mayores que cero y tener hasta 4 decimales.',
  'Line total must be positive and fit numeric(14,4)': 'El total de cada insumo debe ser mayor que cero y tener hasta 4 decimales.',
  "Every plot must belong to the firm's client": 'Todos los lotes deben ser de campos del cliente de la firma.',
  'Yield must be zero or positive and fit numeric(12,2)': 'El rendimiento debe ser cero o positivo.',
  'Website must be a single address without spaces': 'El sitio web no puede tener espacios.',
  // Soil and seed analyses (S1, S2).
  'Sampling method must be promedio or estaciones': 'Elige un método de muestreo válido.',
  'At least one result is required': 'Carga al menos un resultado del análisis.',
  'Each result needs a station when sampling by stations': 'Cada resultado necesita su estación cuando el muestreo es por estaciones.',
  'Percentages must be between 0 and 100': 'Los porcentajes deben estar entre 0 y 100.',
  'Seed fractions add up to more than 100': 'Germinadas, anormales, muertas y duras suman más de 100 % en un tratamiento.',
  'Laboratory must be a soil laboratory': 'Elige un laboratorio de suelos.',
  'Laboratory must be a seed laboratory': 'Elige un laboratorio de semillas.',
  'Map link must be a single address without spaces': 'El enlace al shape o KML no puede tener espacios.',
  // Providers, prices and quote comparisons (S3, S4). The contact role matches exactly, ahead of the plot crop "Role must be one of".
  'Role must be one of: ventas, tecnico, otro': 'Elige el rol del contacto: Ventas, Técnico u Otro.',
  'Currency must be ARS or USD': 'Elige la moneda: ARS o USD.',
  'Price must be positive and fit numeric(14,4)': 'El precio debe ser mayor que cero y tener hasta 4 decimales.',
  'Quantity must be positive': 'La cantidad de cada línea debe ser mayor que cero.',
  'Unit price must be zero or positive': 'El precio unitario debe ser cero o positivo.',
  'Each line needs an input or a product': 'Cada línea necesita un insumo o un producto.',
  // Staff and the OT responsible (S3); messages the help review found untranslated.
  'Role must be one of: ingeniero, administrativo, otro': 'Elige el rol: Ingeniero, Administrativo u Otro.',
  'Responsible must be an active engineer': 'El ingeniero responsable debe ser una persona activa con rol Ingeniero. Revisa Personal.',
  'Area already recorded for this plot and campaign': 'Ese lote ya tiene superficie registrada para esa campaña. Edita el registro existente.',
  'Percentages must have at most two decimals': 'Los porcentajes admiten hasta 2 decimales.',
  'Thousand seed weight must be positive and fit numeric(10,4)': 'El peso de 1000 semillas debe ser mayor que 0 y tener hasta 4 decimales.',
  // User preferences (S2 of the shell round).
  'Theme must be light or dark': 'Elige el tema claro u oscuro.',
};

// The same API text can mean different things per request: "Reason is required" cancels an order or decides a comparison.
const byPath = [[/^quote-comparisons\/[^/]+\/decide$/, { 'Reason is required': 'Indica el motivo de la elección.' }]];

// Messages that list the accepted values end with that list, so they match by prefix.
const choices = [
  ['Tenure must be one of', 'Elige una tenencia válida.'],
  ['Role must be one of', 'Elige renta o servicio.'],
  ['Category must be one of', 'La categoría no es válida.'],
  ['Stage must be', 'Elige una etapa válida.'],
  ['Status must be one of', 'Elige un estado válido.'],
  ['Kind must be one of', 'Elige un tipo de proveedor válido.'],
];

// A firm's next work order number must exceed its highest issued one, which the message names.
const nextNumber = /^Next number must be greater than (\d+)$/;

function validationMessage(text, path) {
  if (typeof text !== 'string') return generic;
  const specific = path && byPath.find(([pattern]) => pattern.test(path))?.[1][text];
  if (specific) return specific;
  if (validations[text]) return validations[text];
  const issued = nextNumber.exec(text)?.[1];
  if (issued) return `El próximo número debe ser mayor que ${issued}.`;
  const [, limitedField, length] = limited.exec(text) ?? [];
  if (limitedFields[limitedField]) return `${limitedFields[limitedField]} no puede superar los ${length} caracteres.`;
  const field = text.endsWith(' is required') ? text.slice(0, -' is required'.length) : null;
  if (field && required[field]) return required[field];
  return choices.find(([prefix]) => text.startsWith(prefix))?.[1] ?? generic;
}

export function errorMessage(status, body, { login = false, path } = {}) {
  const text = body && typeof body === 'object' ? body.error : undefined;
  if (status === 401) return login ? 'Contraseña incorrecta. Inténtalo de nuevo.' : 'La sesión terminó. Ingresa de nuevo.';
  if (status === 403) return 'No se pudo validar la solicitud. Actualiza la sesión.';
  if (status === 409) return conflicts[text] ?? stale;
  if (status === 404) return 'El registro relacionado ya no existe. Actualiza los listados.';
  if (status >= 500) return 'La API privada no responde. Inicia el servidor local.';
  if (status === 400) return validationMessage(text, path);
  return generic;
}

export const sessionEndedMessage = errorMessage(401, null);

export async function api(path, options = {}) {
  const response = await fetch(`/api/v1/${path}`, { cache: 'no-store', credentials: 'same-origin', ...options });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(errorMessage(response.status, body, { login: path === 'session' && options.method === 'POST', path }));
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
