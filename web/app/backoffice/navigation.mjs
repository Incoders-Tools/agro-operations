// Collapsible sidebar groups (S6): which groups are open, remembered per browser. State maps a group key to true
// (open) or false (closed); a group without an entry is closed. The group of the active section is always open.

export const menuStorageKey = 'terra-ia.backoffice.menu';

/** The stored state from localStorage text; anything but an object of booleans is dropped. */
export function readMenuState(raw) {
  let value;
  try { value = JSON.parse(raw); } catch { return {}; }
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  return Object.fromEntries(Object.entries(value).filter(([, open]) => typeof open === 'boolean'));
}

/** True when `group` shows its items: the active section's group, or one the user left open. */
export const groupOpen = (state, group, active) => group === active || state[group] === true;

/**
 * { type: 'toggle', group, active } opens or closes a group; the active group stays open, so its toggle changes nothing.
 * { type: 'navigate', group } keeps the new section's group open after the user leaves it. { type: 'load', state }
 * replaces the state with the stored one. Unchanged state is returned as is.
 */
export function menuReducer(state, action) {
  if (action.type === 'load') return action.state;
  if (action.type === 'navigate') return state[action.group] === true ? state : { ...state, [action.group]: true };
  if (action.type === 'toggle') {
    if (action.group === action.active) return state;
    return { ...state, [action.group]: !groupOpen(state, action.group, action.active) };
  }
  return state;
}
