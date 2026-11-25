import { readFile } from 'node:fs/promises';
import { helpEnabled, helpFile } from '../../help-file.mjs';

export const dynamic = 'force-dynamic';

const notFound = () => new Response('Not found', { status: 404, headers: { 'Content-Type': 'text/plain; charset=utf-8' } });

// The help Markdown of one screen (S10), gated like the backoffice page: only the loopback development preview serves it.
// The key is checked before any file access, so no request can read outside docs/help.
export async function GET(_request, { params }) {
  if (!helpEnabled(process.env)) return notFound();
  const { key } = await params;
  const file = helpFile(key, process.cwd());
  if (!file) return notFound();
  try {
    const text = await readFile(file, 'utf8');
    return new Response(text, { headers: { 'Content-Type': 'text/markdown; charset=utf-8', 'Cache-Control': 'no-store' } });
  } catch {
    return notFound();
  }
}
