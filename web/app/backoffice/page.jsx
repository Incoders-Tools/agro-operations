import { notFound } from 'next/navigation';
import Backoffice from './workspace.jsx';
import './style.css';

export const dynamic = 'force-dynamic';
export const metadata = { title: 'Agro · Operaciones privadas', robots: { index: false, follow: false } };
export default function Page() {
  if (process.env.NODE_ENV !== 'development' || process.env.LOCAL_PREVIEW !== '1') notFound();
  return <Backoffice />;
}
