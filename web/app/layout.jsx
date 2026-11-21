import './style.css';

export const metadata = { title: 'Cuaderno de campo · Demostración local', robots: { index: false, follow: false } };
export default function Layout({ children }) {
  return <html lang="es"><body>{children}</body></html>;
}
