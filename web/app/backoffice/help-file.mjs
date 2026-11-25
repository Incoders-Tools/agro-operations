// Server side of the help route (S10): where a help file lives and when the route answers. Kept apart from help.mjs,
// which the browser also loads, because it uses node:path.

import path from 'node:path';
import { helpKeyPattern } from './help.mjs';

/** The help route answers only in the loopback development preview, like the backoffice page. */
export const helpEnabled = env => env.NODE_ENV === 'development' && env.LOCAL_PREVIEW === '1';

/** Absolute path of docs/help/<key>.md from the web app's working directory; null for an invalid key. */
export function helpFile(key, cwd) {
  if (typeof key !== 'string' || !helpKeyPattern.test(key)) return null;
  return path.join(cwd, '..', 'docs', 'help', `${key}.md`);
}
