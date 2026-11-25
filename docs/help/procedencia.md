---
screen: procedencia
title: Procedencia
summary: Registro de solo lectura del origen de cada dato importado de la planilla y de si ya fue verificado.
video:
reviewed: 2026-11-25
related: [clientes, lotes, contratistas, insumos, acceso-local]
---

# Procedencia

## Para qué sirve

Muestra de dónde vino cada registro que se cargó por importación desde la planilla de los ingenieros (clientes, campos,
lotes, contratistas e insumos) y si alguien ya lo verificó. Sirve para auditar los datos importados y saber cuáles
conviene revisar antes de confiar en ellos.

## Conceptos

| Dato | Significado |
| --- | --- |
| Tipo de entidad | client (cliente), field (campo), plot (lote), contractor (contratista) o input (insumo). |
| Referencia de origen | Hoja y fila de la planilla de donde salió el registro. |
| Estado | **Importado, sin verificar** o **Verificado**. |

Un registro de procedencia no reemplaza las validaciones del sistema: el dato importado pasó las mismas reglas que una
carga manual. Solo indica su origen.

## Cómo se hace

**Consultar el origen de un dato**

1. Entra en **Procedencia** (Auditoría).
2. Recorre el listado: cada fila muestra el tipo de entidad, la referencia de origen y el estado. Esta pantalla no
   tiene buscador.

**Importar datos de la planilla (sysadmin)**

1. Con el sistema local en marcha, ejecuta `tools/import-workbook.ps1 -Workbook <ruta> -DryRun` para ver los conteos
   sin cambiar nada.
2. Ejecuta el mismo comando sin `-DryRun` para importar. Los nombres que ya existen se saltean; cada registro creado
   queda como "Importado, sin verificar".
3. Repetir la importación no duplica datos.

## Reglas y validaciones

- La pantalla es de solo lectura: no se crean ni se editan registros desde aquí.
- La importación nunca carga macros de la planilla y solo muestra conteos y números de fila, no valores.
- Los datos reales se guardan solo en la base de datos privada local.

## Preguntas abiertas

- Q1: dos firmas de la planilla que no coinciden con ningún cliente ([preguntas](../engineer-questions.md)).

## Notas técnicas

- Ruta: `GET /import-records`.
- Tabla: `import_records` (único por tipo y entidad, con `raw_payload` jsonb y `verified`). Migraciones
  `005_import_records.sql` y `006_catalogs.sql` (contratistas e insumos).
- Importador: `tools/import-workbook.ps1` y `tools/AgroOperations.WorkbookImport`. Lee la hoja de catálogos
  (contratistas, insumos), `BASEDATOS` (clientes, campos y lotes con código y superficie) y `Registros` (próximo número
  de OT de cada firma).
- Pantalla con el editor genérico en modo lectura: `web/app/backoffice/generic.jsx`; dominio `ImportProvenanceService.cs`.
