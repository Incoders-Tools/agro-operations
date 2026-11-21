# Cuaderno de campo · demostración local

Prototipo visual con datos enteramente inventados. No contiene clientes reales, documentos, recomendaciones agronómicas ni conexiones a servicios o API. No crea ni modifica órdenes.

## Inicio local

En Windows, busque **Agro Operations Local** en el menú Inicio. El acceso directo ejecuta `tools/start-local.ps1` de este repositorio: requiere Node.js y pnpm, instala dependencias solo si faltan y abre `http://127.0.0.1:3000` cuando el servidor está listo. Mantenga abierta la consola y use **Ctrl+C** para detenerlo. Si el puerto 3000 está ocupado, cierre el otro servidor antes de volver a abrirlo; no se conectará a ese servicio. El acceso directo no inicia con Windows.

Sin el acceso directo, desde `web/` ejecute `LOCAL_PREVIEW=1 NODE_ENV=development pnpm dev` (en PowerShell: `$env:LOCAL_PREVIEW='1'; $env:NODE_ENV='development'; pnpm dev`). El comando liga únicamente a `127.0.0.1`; fuera de modo desarrollo/local, el servidor de desarrollo rechaza el arranque y la página responde como no encontrada. No desplegar este prototipo.

Pruebas: `npm test`. Compilación: `npm run build`. La compilación no habilita la vista en producción.
