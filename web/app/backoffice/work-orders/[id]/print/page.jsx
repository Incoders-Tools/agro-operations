import { notFound } from 'next/navigation';
import { brandTitle } from '../../../brand.mjs';
import WorkOrderPrint from '../../../print.jsx';
import '../../../brand.css';
import '../../../print.css';

export const dynamic = 'force-dynamic';
export const metadata = { title: brandTitle('Orden de trabajo'), robots: { index: false, follow: false } };
// Gated like the backoffice page: only the loopback development preview serves it.
export default async function Page({ params }) {
  if (process.env.NODE_ENV !== 'development' || process.env.LOCAL_PREVIEW !== '1') notFound();
  const { id } = await params;
  return <WorkOrderPrint id={id} />;
}
