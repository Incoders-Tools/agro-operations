# Cuaderno de campo · demostración local

Prototipo visual con datos enteramente inventados. No contiene clientes reales, documentos, recomendaciones agronómicas ni conexiones a servicios o API. No crea ni modifica órdenes.

## Inicio local

Con Node.js y dependencias instaladas localmente, desde `web/` ejecuta `LOCAL_PREVIEW=1 NODE_ENV=development npm run dev` (en PowerShell: `$env:LOCAL_PREVIEW='1'; $env:NODE_ENV='development'; npm run dev`). Abre `http://127.0.0.1:3000`. Detén el servidor con Ctrl+C. El comando liga únicamente a `127.0.0.1`; fuera de modo desarrollo/local, el servidor de desarrollo rechaza el arranque y la página responde como no encontrada. No desplegar este prototipo.

Pruebas: `npm test`. Compilación: `npm run build`. La compilación no habilita la vista en producción.
