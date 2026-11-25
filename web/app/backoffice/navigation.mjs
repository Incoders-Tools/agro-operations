// Collapsible sidebar groups as an accordion (S6): one group is open at a time, so the full menu always fits without
// a scrollbar. State is { open: groupKey } when the user opened a group, or {} to follow the active section's group.

export const menuStorageKey = 'terra-ia.backoffice.menu';

/** The stored state from localStorage text; anything but { open: <group key> } is dropped. */
export function readMenuState(raw) {
  let value;
  try { value = JSON.parse(raw); } catch { return {}; }
  return value && typeof value === 'object' && !Array.isArray(value) && typeof value.open === 'string' ? { open: value.open } : {};
}

/** The single open group: the one the user opened, else the active section's group. */
const openGroup = (state, active) => state.open ?? active;
export const groupOpen = (state, group, active) => openGroup(state, active) === group;

/**
 * { type: 'toggle', group, active } opens a group and closes the others; closing the open group returns to the active
 * section's group, whose own toggle changes nothing. { type: 'navigate', group } opens only the new section's group.
 * { type: 'load', state } replaces the state with the stored one. Unchanged state is returned as is.
 */
export function menuReducer(state, action) {
  if (action.type === 'load') return action.state;
  if (action.type === 'navigate') return state.open === action.group ? state : { open: action.group };
  if (action.type === 'toggle') {
    if (!groupOpen(state, action.group, action.active)) return { open: action.group };
    return action.group === action.active ? state : { open: action.active };
  }
  return state;
}
