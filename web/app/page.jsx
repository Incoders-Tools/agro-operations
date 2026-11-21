import { notFound } from 'next/navigation';
import Notebook from './preview.jsx';

export const dynamic = 'force-dynamic';
export default function Page() {
  if (process.env.NODE_ENV !== 'development' || process.env.LOCAL_PREVIEW !== '1') notFound();
  return <Notebook />;
}
