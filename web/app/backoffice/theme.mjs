// Light and dark themes (S2). The choice is stored per logged-in identity in the API (/me/preferences) and cached in
// localStorage per email, so the boot script in page.jsx paints the right theme before React loads. brand.css holds the
// colours of both themes; the print views always use the light set. The theme is an attribute of the page's own root
// element (themeRootId), not of <html>, which React hydrates from the shared layout.

export const themes = ['light', 'dark'];
export const defaultTheme = 'light';
/** Id of the backoffice root element (page.jsx) that carries data-theme. */
export const themeRootId = 'bo-root';

/** The email of the last identity that loaded the backoffice, so the boot script knows whose cache to read. */
export const lastIdentityKey = 'terra-ia.backoffice.identity';
const themeKeyPrefix = 'terra-ia.backoffice.theme:';

/** A theme name from stored or received text; null for anything else. */
export function readTheme(raw) {
  if (typeof raw !== 'string') return null;
  const theme = raw.trim().toLowerCase();
  return themes.includes(theme) ? theme : null;
}

/** The localStorage key of one identity's cached theme; null without an email. */
export function themeStorageKey(email) {
  const identity = typeof email === 'string' ? email.trim().toLowerCase() : '';
  return identity ? `${themeKeyPrefix}${identity}` : null;
}

/**
 * The theme to show: the one stored in the API, else the local cache, else light. `stored` is the API answer
 * ({ theme, revision }); revision 0 means nothing is stored, so its light is the default rather than a choice.
 */
export function resolveTheme({ stored, local } = {}) {
  const server = stored && stored.revision > 0 ? readTheme(stored.theme) : null;
  return server ?? readTheme(local) ?? defaultTheme;
}

export const toggleTheme = theme => theme === 'dark' ? 'light' : 'dark';
export const themeLabel = theme => theme === 'dark' ? 'Oscuro' : 'Claro';

/** Sets or removes data-theme="dark" on the backoffice root, where brand.css switches every themed colour. */
export function applyTheme(theme, root = globalThis.document?.getElementById(themeRootId)) {
  if (!root) return;
  if (theme === 'dark') root.dataset.theme = 'dark';
  else delete root.dataset.theme;
}

/**
 * Inline script at the start of the backoffice root: reads the last identity's cached theme and marks the root before
 * the first paint.
 * Storage may be blocked; the page then starts light and the API value applies after login.
 */
export function themeBootScript() {
  return `try{var e=localStorage.getItem(${JSON.stringify(lastIdentityKey)});`
    + `if(e&&localStorage.getItem(${JSON.stringify(themeKeyPrefix)}+e)==='dark')document.getElementById(${JSON.stringify(themeRootId)}).dataset.theme='dark'}catch(_){}`;
}
