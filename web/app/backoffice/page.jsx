import { notFound } from 'next/navigation';
import { brandTitle } from './brand.mjs';
import { themeBootScript, themeRootId } from './theme.mjs';
import Backoffice from './workspace.jsx';
import './brand.css';
import './help.css';
import './style.css';
import './business.css';

export const dynamic = 'force-dynamic';
export const metadata = { title: brandTitle(), robots: { index: false, follow: false } };
// S2: the inline script marks the root with the cached theme before the first paint; the client changes data-theme after
// hydration, so the root does not compare it with the server render.
export default function Page() {
  if (process.env.NODE_ENV !== 'development' || process.env.LOCAL_PREVIEW !== '1') notFound();
  return <div id={themeRootId} className="bo-root" suppressHydrationWarning>
    <script dangerouslySetInnerHTML={{ __html: themeBootScript() }} />
    <Backoffice />
  </div>;
}
