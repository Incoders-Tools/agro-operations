// Server side of the help route (S10): where a help file lives and when the route answers. Kept apart from help.mjs,
// which the browser also loads, because it uses node:path.

import path from 'node:path';
import { businessDocKeyPattern, businessDocs } from './business/document.mjs';
import { helpKeyPattern } from './help.mjs';

/** The help route answers only in the loopback development preview, like the backoffice page. */
export const helpEnabled = env => env.NODE_ENV === 'development' && env.LOCAL_PREVIEW === '1';

// docs/<folder>/<key>.md from the web app's working directory; the key is checked before it names a path.
const docFile = (folder, pattern, key, cwd) => typeof key === 'string' && pattern.test(key) ? path.join(cwd, '..', 'docs', folder, `${key}.md`) : null;

/** Absolute path of docs/help/<key>.md from the web app's working directory; null for an invalid key. */
export const helpFile = (key, cwd) => docFile('help', helpKeyPattern, key, cwd);

/** Absolute path of docs/business/<key>.md (S4); null for a key that is not a listed business document. */
export const businessDocFile = (key, cwd) => Object.hasOwn(businessDocs, String(key)) ? docFile('business', businessDocKeyPattern, key, cwd) : null;
