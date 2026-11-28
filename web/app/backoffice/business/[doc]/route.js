import { readFile } from 'node:fs/promises';
import { businessDocFile, helpEnabled } from '../../help-file.mjs';

export const dynamic = 'force-dynamic';

const notFound = () => new Response('Not found', { status: 404, headers: { 'Content-Type': 'text/plain; charset=utf-8' } });

// The Markdown of one business document (S4), gated like the help route: only the loopback development preview serves
// it, and only for a listed key, so no request can read outside docs/business.
export async function GET(_request, { params }) {
  if (!helpEnabled(process.env)) return notFound();
  const { doc } = await params;
  const file = businessDocFile(doc, process.cwd());
  if (!file) return notFound();
  try {
    const text = await readFile(file, 'utf8');
    return new Response(text, { headers: { 'Content-Type': 'text/markdown; charset=utf-8', 'Cache-Control': 'no-store' } });
  } catch {
    return notFound();
  }
}
