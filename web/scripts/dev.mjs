import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

if (process.env.NODE_ENV && process.env.NODE_ENV !== 'development' || process.env.LOCAL_PREVIEW !== '1') {
  console.error('Vista local no disponible: se requiere NODE_ENV=development y LOCAL_PREVIEW=1.');
  process.exit(1);
}
const child = spawn(process.execPath, ['node_modules/next/dist/bin/next', 'dev', '--hostname', '127.0.0.1'], {
  cwd: fileURLToPath(new URL('../', import.meta.url)),
  env: { ...process.env, NODE_ENV: 'development' },
  stdio: 'inherit',
});
child.on('error', error => { console.error(error.message); process.exitCode = 1; });
child.on('exit', code => { process.exitCode = code ?? 1; });
