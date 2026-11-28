import { notFound } from 'next/navigation';
import { brandTitle } from '../../../brand.mjs';
import { BusinessDocPrint } from '../../../business-doc.jsx';
import { businessDocs } from '../../document.mjs';
import '../../../brand.css';
import '../../../help.css';
import '../../../print.css';
import '../../../business.css';

export const dynamic = 'force-dynamic';
export const metadata = { title: brandTitle('Documentación'), robots: { index: false, follow: false } };
// The printable business document (S4), gated like the backoffice page; an unknown key is not found.
export default async function Page({ params }) {
  if (process.env.NODE_ENV !== 'development' || process.env.LOCAL_PREVIEW !== '1') notFound();
  const { doc } = await params;
  if (!Object.hasOwn(businessDocs, doc)) notFound();
  return <BusinessDocPrint docKey={doc} />;
}
