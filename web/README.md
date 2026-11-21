# Cuaderno de campo · demostración local

Prototipo visual con datos enteramente inventados. No contiene clientes reales, documentos, recomendaciones agronómicas ni conexiones a servicios o API. No crea ni modifica órdenes.

## Inicio local

En Windows, ejecute `C:\shortcuts\run-agro-operations-local.bat` (o busque **Agro Operations Local** en el menú Inicio). Ambos ejecutan `tools/start-local.ps1` de este repositorio: requieren Node.js y pnpm, instalan dependencias solo si faltan y abren `http://127.0.0.1:3000` cuando el servidor está listo. Mantenga abierta la consola y use **Ctrl+C** para detenerlo. Si el puerto 3000 está ocupado, cierre el otro servidor antes de volver a abrirlo; no se conectará a ese servicio. Ninguno inicia con Windows. Si el repositorio cambia de ubicación, actualice la ruta del archivo `.bat`.

Sin el acceso directo, desde `web/` ejecute `LOCAL_PREVIEW=1 NODE_ENV=development pnpm dev` (en PowerShell: `$env:LOCAL_PREVIEW='1'; $env:NODE_ENV='development'; pnpm dev`). El comando liga únicamente a `127.0.0.1`; fuera de modo desarrollo/local, el servidor de desarrollo rechaza el arranque y la página responde como no encontrada. No desplegar este prototipo.

Pruebas: `npm test`. Compilación: `npm run build`. La compilación no habilita la vista en producción.
