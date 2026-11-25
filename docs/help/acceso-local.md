---
screen: acceso-local
title: Acceso local y administración
summary: Inicio del sistema local, ingreso con el administrador temporal, credenciales, importación y respaldos (para el sysadmin).
video:
reviewed: 2026-11-25
related: [procedencia, personal]
---

# Acceso local y administración

## Para qué sirve

Explica cómo se pone en marcha el sistema en la computadora local, cómo se ingresa y dónde quedan las credenciales, los
datos y los respaldos. Está pensada para el sysadmin; quien solo usa el backoffice necesita los pasos de "Ingresar".

## Conceptos

**Sistema local**: tres piezas que corren solo en esta computadora (dirección `127.0.0.1`, no accesibles desde la red):

| Pieza | Dónde | Qué hace |
| --- | --- | --- |
| Base de datos privada | PostgreSQL en Docker, proyecto `agro-operations-local`, puerto 5433 | Guarda los datos reales. Persiste entre reinicios. |
| API protegida | `http://127.0.0.1:5081` | Atiende al backoffice; exige sesión. |
| Vista web | `http://127.0.0.1:3000/backoffice` | El backoffice. En `/` hay una demostración con datos inventados que nunca usa la base privada. |

**Administrador local temporal**: un único usuario de desarrollo para ingresar mientras no existan las cuentas con
invitación. Solo funciona en modo desarrollo y en esta computadora. No es el personal de la empresa (ver Personal).

**Credenciales**: se generan solas la primera vez y se guardan protegidas para el usuario de Windows que las creó
(DPAPI) en `.local/backoffice-credentials.json`, dentro de la carpeta del repositorio e ignoradas por git.

**Sesión**: dura ocho horas. Hay un límite de intentos de ingreso. Cerrar sesión la termina en el acto.

**Respaldos**: copias completas de la base privada en `.local/backups/`, con un nombre que indica el momento, por
ejemplo `before-011-012.dump` (antes de aplicar las migraciones 011 y 012).

## Cómo se hace

**Iniciar el sistema**

1. Ejecuta `C:\shortcuts\run-agro-operations-local.bat` (o el acceso directo del menú Inicio). Es el único lanzador.
2. El lanzador ejecuta `tools/start-local.ps1`, que inicia Docker Desktop si hace falta, levanta la base de datos,
   aplica solo las migraciones que faltan, abre la API en su propia ventana, inicia la vista web y abre el
   backoffice en el navegador.
3. Deja abierta la ventana de la API mientras trabajas.

**Ingresar**

1. Si no conoces la contraseña, abre PowerShell **como el mismo usuario de Windows** y ejecuta en forma interactiva
   `tools/show-backoffice-login.ps1`. Muestra el usuario y la contraseña en pantalla.
2. Escribe la contraseña en **Contraseña local** y pulsa **Ingresar al backoffice**.
3. Para salir, pulsa **Cerrar sesión** en el menú lateral.

**Hacer un respaldo antes de un cambio de esquema**

1. Con el sistema en marcha y desde la carpeta del repositorio, genera el respaldo dentro del contenedor:
   `docker compose -p agro-operations-local -f compose.yaml exec -T postgres pg_dump -U agro_dev -d agro_operations -Fc -f /tmp/respaldo.dump`
2. Cópialo a `.local/backups/`:
   `docker compose -p agro-operations-local -f compose.yaml cp postgres:/tmp/respaldo.dump .local/backups/before-<migración>.dump`
3. Comprueba que el archivo existe y no está vacío antes de aplicar la migración.

**Importar datos de la planilla**

1. Con el sistema en marcha, ejecuta `tools/import-workbook.ps1 -Workbook <ruta> -DryRun` y revisa los conteos.
2. Ejecuta sin `-DryRun` para importar (ver Procedencia).

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Contraseña incorrecta | Contraseña incorrecta. Inténtalo de nuevo. |
| La sesión venció o se cerró | La sesión terminó. Ingresa de nuevo. |
| La API no está corriendo | La API privada no responde. Inicia el servidor local. |
| El token de seguridad no coincide | No se pudo validar la solicitud. Actualiza la sesión. |
| El puerto 3000 lo usa otro programa | El puerto local 3000 está ocupado por otro servicio o no se pudo verificar la vista. No se abrirá ese servicio. |

- Nunca copies la contraseña en chats, tickets, capturas ni registros; `show-backoffice-login.ps1` se ejecuta solo en
  forma interactiva y se niega si su salida se redirige.
- El lanzador nunca borra ni reinicia datos: si la base existe sin credenciales o con un esquema incompleto, se detiene
  con un error en lugar de resetear.
- No hay borrado de datos desde el backoffice. Restaurar un respaldo reemplaza la base completa: es una acción
  destructiva y requiere una decisión explícita.
- Este acceso es solo para desarrollo local; nunca se habilita en un servidor público.

## Preguntas abiertas

Ninguna por ahora. Las cuentas con invitación por email están en `docs/identity-contract.md`.

## Notas técnicas

- Requisitos de la API: `ASPNETCORE_ENVIRONMENT=Development`, `AGRO_LOCAL_ADMIN_EMAIL`, `AGRO_LOCAL_ADMIN_PASSWORD` (24
  caracteres o más) y `AGRO_LOCAL_DATABASE` apuntando a un PostgreSQL en `127.0.0.1`; los arma
  `tools/start-backoffice-api.ps1`.
- Sesión: `POST /api/v1/session` (contraseña), `GET /api/v1/session` (identidad y token CSRF),
  `POST /api/v1/session/logout` (con `X-Agro-CSRF`). Cookie HttpOnly, SameSite=Strict, ocho horas. Las claves de
  protección de datos viven en `.local/keys`.
- Toda escritura requiere la cabecera `X-Agro-CSRF`; sin sesión, la API responde 401.
- Las migraciones se aplican en orden (`001`, `003` en adelante), solo las que faltan, enviadas a `psql` como UTF-8 para
  conservar los acentos.
- Contratos: `docs/local-access-contract.md`, `docs/local-preview-contract.md`, `docs/platform-contract.md`.
