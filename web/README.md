# Cuaderno de campo · demostración local

La ruta `/` es un cuaderno de campo visual con datos enteramente inventados. No contiene clientes reales, documentos ni recomendaciones agronómicas; no consulta la API ni crea órdenes. `/backoffice` es una ruta distinta para iniciar sesión contra la API privada y administrar clientes, campos, lotes y cultivos guardados en PostgreSQL local. Aún no gestiona campañas, recetas, labores ni importaciones reales.

## Inicio local

En Windows, ejecute `C:\shortcuts\run-agro-operations-local.bat` (o busque **Agro Operations Local** en el menú Inicio). Ambos ejecutan `tools/start-local.ps1` de este repositorio: requieren Node.js y pnpm (si el Explorador no los encuentra en `PATH`, usan la versión predeterminada de fnm y el pnpm de este usuario), instalan dependencias solo si faltan y abren `http://127.0.0.1:3000` cuando el servidor está listo. Mantenga abierta la consola y use **Ctrl+C** para detenerlo. Si ya está ejecutándose esta misma demostración local, el BAT vuelve a abrirla sin iniciar otro servidor. Si el puerto 3000 pertenece a otro servicio o la identidad de la demostración no puede verificarse, el BAT rechaza la apertura; cierre ese servicio antes de reintentarlo. Ninguno inicia con Windows. Si el repositorio cambia de ubicación, actualice la ruta del archivo `.bat`.

Sin el acceso directo, desde `web/` ejecute `LOCAL_PREVIEW=1 NODE_ENV=development pnpm dev` (en PowerShell: `$env:LOCAL_PREVIEW='1'; $env:NODE_ENV='development'; pnpm dev`). El comando liga únicamente a `127.0.0.1`; fuera de modo desarrollo/local, el servidor de desarrollo rechaza el arranque y las rutas `/` y `/backoffice` responden como no encontradas. No desplegar este prototipo.

Para usar `/backoffice`, inicie **en otra consola** `tools/start-backoffice-api.ps1` desde la raíz del repositorio; se necesita Docker, .NET y la credencial local generada. Ejecute `tools/show-backoffice-login.ps1` solo en una consola interactiva para ver la contraseña, no la redirija a un archivo. La ruta web solo usa un proxy a `127.0.0.1:5081` en desarrollo; la API exige cookie y CSRF, incluso cuando el cuaderno de demostración sigue visible. El BAT existente abre `/` y **no** inicia la API ni importa datos reales.

Pruebas: `npm test`. Compilación: `npm run build`. La compilación no habilita la vista en producción.
