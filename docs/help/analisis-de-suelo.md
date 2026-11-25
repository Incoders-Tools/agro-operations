---
screen: analisis-de-suelo
title: Análisis de suelo
summary: Análisis de suelo de un lote por campaña, con sus resultados y el historial de cada parámetro.
video:
reviewed: 2026-11-25
related: [lotes, proveedores, campanas]
---

# Análisis de suelo

## Para qué sirve

Registra los análisis de suelo de un lote: cuándo se muestreó, qué laboratorio lo analizó, cómo se tomaron las muestras
y los valores de cada parámetro. Todos los análisis del lote quedan a la vista, de todas las campañas, y un historial
por parámetro muestra cómo evolucionó cada valor.

El panel está en la ficha del lote (**Clientes → cliente → campo → lote**).

## Conceptos

**Análisis de suelo**

| Campo | Significado y reglas |
| --- | --- |
| Campaña | Obligatoria. |
| Fecha de muestreo | Obligatoria. |
| Fecha de análisis | Opcional. |
| Laboratorio | Opcional. Solo proveedores de tipo "Laboratorio de suelos". |
| Método de muestreo | **Promedio del lote** (un valor por parámetro para todo el lote) o **Estaciones de muestreo** (cada resultado indica su estación). |
| Detalle del muestreo | Opcional. Cantidad de muestras, ubicación, criterio. |
| Enlace al shape o KML | Opcional. Dirección del archivo con la zona de muestreo, sin espacios, hasta 300 caracteres. |
| Observaciones | Opcional. |

**Resultado**: una línea por parámetro medido. Al menos uno por análisis.

| Campo | Significado y reglas |
| --- | --- |
| Parámetro | Obligatorio, hasta 100 caracteres. Texto libre con sugerencias (pH, Materia orgánica, Fósforo extractable (P Bray), Nitratos (N-NO3), Azufre (S-SO4), Zinc, etc.). |
| Valor | Obligatorio. Número, hasta 4 decimales. |
| Unidad | Opcional, hasta 30 caracteres. Sugerencias: %, ppm, mg/kg, dS/m, cmol/kg. El pH no lleva unidad. |
| Profundidad | Opcional, hasta 30 caracteres, por ejemplo `0-20 cm`. |
| Estación | Obligatoria con muestreo por estaciones, hasta 60 caracteres. |

**Historial por parámetro**: agrupa los resultados de todos los análisis del lote por parámetro (sin distinguir
mayúsculas ni acentos) y los ordena del más antiguo al más reciente.

## Cómo se hace

**Cargar un análisis**

1. Abre el lote y, en **Análisis de suelo**, pulsa **Nuevo análisis**.
2. Elige campaña, fechas, laboratorio y método de muestreo; describe el muestreo y pega el enlace al shape o KML si
   lo tienes.
3. En **Resultados**, completa parámetro, valor, unidad y profundidad. Con estaciones, completa también la estación.
4. Pulsa **Agregar resultado** para otra línea; el cursor pasa al parámetro nuevo. **Quitar** borra una línea.
5. Pulsa **Crear análisis**.

**Corregir un análisis**

1. Pulsa **Editar** en la fila del análisis.
2. Cambia lo necesario y pulsa **Guardar cambios**. Los resultados se reemplazan por los del formulario.

**Ver la zona de muestreo**

1. En la fila del análisis, pulsa **Ver zona de muestreo** (aparece cuando hay enlace).

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Ningún resultado | Carga al menos un resultado del análisis. |
| Muestreo por estaciones con un resultado sin estación | Cada resultado necesita su estación cuando el muestreo es por estaciones. |
| Enlace con espacios | El enlace al shape o KML no puede tener espacios. |
| Método de muestreo inválido | Elige un método de muestreo válido. |
| Laboratorio de otro tipo | Elige un laboratorio de suelos. |
| Fecha de muestreo vacía | Indica la fecha de muestreo. |
| Parámetro vacío en un resultado | Indica el parámetro. |
| Parámetro, unidad, profundidad o estación demasiado largos | El parámetro no puede superar los 100 caracteres. (La unidad y la profundidad, 30; la estación, 60.) |
| Enlace de más de 300 caracteres | El enlace al shape o KML no puede superar los 300 caracteres. |
| Faltan campañas | Primero crea campañas en su catálogo. |

- Las líneas de resultado totalmente vacías se ignoran al guardar.
- Un análisis nunca cambia de lote.

## Preguntas abiertas

- Q17: qué parámetros, unidades y profundidades usan los ingenieros. Hoy el parámetro es texto libre con una lista
  sugerida ([preguntas](../engineer-questions.md)).

## Notas técnicas

- Rutas: `GET /soil-analyses?plotId=` (todas las campañas, muestreo más reciente primero), `GET /soil-analyses/{id}`,
  `POST /soil-analyses`, `PUT /soil-analyses/{id}` (reemplazo completo con resultados; un `plotId` enviado se ignora).
- Tablas: `soil_analyses`, `soil_analysis_results`. Migración `012_analyses.sql`.
- Código: `web/app/backoffice/soil-analyses.jsx`, `analyses.mjs`; dominio `SoilAnalysisService.cs`.
