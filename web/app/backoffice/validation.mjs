// Form validation (S12): the backoffice's own checks and Spanish messages instead of the browser's bubbles. The rules
// come from each control (form-validation.jsx reads them from the DOM); this module only decides and words the message.

import { fold } from './schema.mjs';

// Feminine and masculine words of the labels that the ending rule below would get wrong.
const feminine = new Set(['labor', 'superficie', 'clave', 'parte', 'contrasena']);
const masculine = new Set(['dia', 'mapa', 'sistema', 'problema', 'tema', 'contratista']);
const amount = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 4 });

/**
 * A label as a noun phrase with its article: "Superficie (ha)" is "la superficie", "Observaciones" "las observaciones".
 * The units in parentheses are dropped; gender and number come from the first word.
 */
export function nounPhrase(label) {
  const phrase = String(label ?? '').replace(/\s*\([^)]*\)/g, '').replace(/\s+/g, ' ').trim();
  const [first = ''] = phrase.split(' ');
  const acronym = first.length > 1 && first === first.toUpperCase() && /[A-Z]/.test(first);
  const word = fold(first);
  const plural = !acronym && /s$/.test(word) && !/is$/.test(word);
  const stem = plural ? (/[nrldz]es$/.test(word) ? word.slice(0, -2) : word.slice(0, -1)) : word;
  const isFeminine = !acronym && !masculine.has(stem) && (feminine.has(stem) || /(a|ion|dad|tad|tud|sis|umbre)$/.test(stem));
  const article = plural ? (isFeminine ? 'las' : 'los') : (isFeminine ? 'la' : 'el');
  return `${article} ${acronym ? phrase : phrase.charAt(0).toLowerCase() + phrase.slice(1)}`;
}

const indefinite = { el: 'un', la: 'una' };

function grammar(noun) {
  const [article, ...rest] = noun.split(' ');
  const plural = article === 'los' || article === 'las';
  const subject = noun.charAt(0).toUpperCase() + noun.slice(1);
  return {
    noun, subject, plural,
    // "Elige una firma comercial"; a plural or an unknown article keeps the noun as written.
    some: indefinite[article] ? `${indefinite[article]} ${rest.join(' ')}` : noun,
    must: plural ? 'deben' : 'debe', can: plural ? 'pueden' : 'puede', allows: plural ? 'admiten' : 'admite',
    greater: plural ? 'mayores' : 'mayor', number: plural ? 'números' : 'un número', integer: plural ? 'números enteros' : 'un número entero',
  };
}

const decimals = text => (String(text).trim().split('.')[1] ?? '').length;

function derived(rule, words, rules) {
  const { subject, must, can, allows } = words;
  if (rule === 'required') {
    if (rules.kind === 'choice') return `Elige ${words.some}.`;
    if (rules.kind === 'checkbox') return `Marca ${words.noun}.`;
    return `Indica ${words.noun}.`;
  }
  if (rule === 'type') return `${subject} ${must} ser ${words.number}.`;
  if (rule === 'integer') return `${subject} ${must} ser ${words.integer}.`;
  if (rule === 'positive') return `${subject} ${must} ser ${words.greater} que 0.`;
  if (rule === 'min') return `${subject} no ${can} ser menor que ${amount.format(Number(rules.min))}.`;
  if (rule === 'max') return `${subject} no ${can} superar ${amount.format(Number(rules.max))}.`;
  if (rule === 'step') {
    const places = decimals(rules.step);
    return `${subject} ${allows} hasta ${places} ${places === 1 ? 'decimal' : 'decimales'}.`;
  }
  if (rule === 'maxLength') return `${subject} no ${can} superar los ${rules.maxLength} caracteres.`;
  return `${subject} ${must} tener un único @ con texto a ambos lados.`;
}

// A minimum equal to the step (0.0001 with step 0.0001) means "more than zero" for the reader.
function minimumRule(value, rules) {
  const min = Number(rules.min);
  if (rules.min === undefined || rules.min === null || rules.min === '' || Number.isNaN(min) || value >= min) return null;
  const step = Number(rules.step);
  return min > 0 && min < 1 && (Number.isNaN(step) || step >= min) ? 'positive' : 'min';
}

function brokenRule(rules, { value = '', checked, badInput }) {
  const text = String(value ?? '');
  if (rules.kind === 'checkbox') return rules.required && !checked ? 'required' : null;
  if (!text.trim()) return badInput ? 'type' : rules.required ? 'required' : null;
  if (rules.kind === 'number') {
    const number = Number(text.trim());
    if (badInput || Number.isNaN(number)) return 'type';
    const below = minimumRule(number, rules);
    if (below) return below;
    if (rules.max !== undefined && rules.max !== null && rules.max !== '' && number > Number(rules.max)) return 'max';
    if (rules.step && rules.step !== 'any') {
      const step = Number(rules.step);
      if (step >= 1 && !Number.isInteger(number)) return 'integer';
      if (step < 1 && decimals(text) > decimals(rules.step)) return 'step';
    }
    return null;
  }
  if (rules.maxLength && text.length > Number(rules.maxLength)) return 'maxLength';
  if (rules.kind === 'email' && !/^[^@\s]+@[^@\s]+$/.test(text.trim())) return 'email';
  return null;
}

/**
 * The message for one control, or null when its value passes. `rules`: { label, noun?, kind ('text' | 'number' | 'date' |
 * 'email' | 'choice' | 'checkbox'), required, min, max, step, maxLength, messages? }; `input`: { value, checked, badInput }.
 * `messages` overrides the derived text of one rule (required, type, integer, positive, min, max, step, maxLength, email).
 */
export function fieldError(rules, input) {
  const rule = brokenRule(rules, input);
  if (!rule) return null;
  const override = rules.messages?.[rule] ?? (rule === 'positive' || rule === 'integer' ? rules.messages?.[rule === 'positive' ? 'min' : 'step'] : undefined);
  return override ?? derived(rule, grammar(rules.noun || nounPhrase(rules.label)), rules);
}

/** Every entry ({ id, rules, input }) in form order: the messages by id, the first invalid id and how many failed. */
export function validateAll(entries) {
  const errors = {};
  let first = null;
  for (const { id, rules, input } of entries) {
    const message = fieldError(rules, input);
    if (!message) continue;
    errors[id] = message;
    first ??= id;
  }
  return { errors, first, count: Object.keys(errors).length };
}
