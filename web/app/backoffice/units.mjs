// Display of measurement units (S2). Stored values and request payloads keep the unit as typed; only the screen and the
// print view show litres as "L", because a lowercase "l" reads like a cut letter.

// One segment of a unit, between slashes: l, lt, lts, litro or litros in any case, with an optional final dot.
const litre = /^(?:l|lts?|litros?)\.?$/i;

/**
 * The unit as shown: litres as "L", also inside a composite unit ("l/ha" is "L/ha", "g/l" is "g/L"); the segments of a
 * composite unit lose the spaces around the slash. Anything else is unchanged; '' when there is no unit.
 */
export function displayUnit(unit) {
  if (unit === null || unit === undefined) return '';
  return String(unit).split('/').map(segment => litre.test(segment.trim()) ? 'L' : segment.trim()).join('/');
}
