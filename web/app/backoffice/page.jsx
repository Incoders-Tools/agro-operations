import { notFound } from 'next/navigation';
import { brandTitle } from './brand.mjs';
import Backoffice from './workspace.jsx';
import './brand.css';
import './help.css';
import './style.css';

export const dynamic = 'force-dynamic';
export const metadata = { title: brandTitle(), robots: { index: false, follow: false } };
export default function Page() {
  if (process.env.NODE_ENV !== 'development' || process.env.LOCAL_PREVIEW !== '1') notFound();
  return <Backoffice />;
}
