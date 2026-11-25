---
screen: analisis-de-semillas
title: Análisis de semillas
summary: Calidad de semilla por cliente, cultivo y variedad, con una calculadora de densidad de siembra.
video:
reviewed: 2026-11-25
related: [clientes, cultivos, campanas, proveedores]
---

# Análisis de semillas

## Para qué sirve

Registra la calidad de la semilla que cada productor va a sembrar, por cultivo y variedad o híbrido: peso de 1000
semillas, poder germinativo con y sin curasemilla y vigor. Con esos datos, la calculadora estima cuántas semillas por
hectárea, kilos por hectárea y semillas por metro lineal hacen falta para lograr un stand de plantas objetivo.

## Conceptos

**Análisis de semillas**: todos los valores medidos son opcionales, porque a veces no se tienen.

| Campo | Significado y reglas |
| --- | --- |
| Cliente | Obligatorio. Productor de procedencia de la semilla. |
| Cultivo | Obligatorio. |
| Variedad o híbrido | Opcional. |
| Campaña | Opcional. |
| Fecha de análisis | Obligatoria. |
| Laboratorio | Opcional. Solo proveedores de tipo "Laboratorio de semillas". |
| Peso de 1000 semillas (g) | Opcional. Mayor que cero y menor que 1.000.000, hasta 4 decimales. |
| Vigor (%) | Opcional. Entre 0 y 100. |
| Con curasemilla / Sin curasemilla | Cuatro porcentajes por tratamiento: poder germinativo, plántulas anormales, semillas muertas y semillas duras. Cada uno entre 0 y 100, hasta 2 decimales. |
| Observaciones | Opcional. |

En cada tratamiento, germinadas + anormales + muertas + duras suman como máximo 100 %.

**Calculadora de siembra** (en el detalle de un análisis). Fórmula **provisoria**, pendiente de confirmación de los
ingenieros (Q18). PG y eficiencia se usan como fracción (90 % = 0,9):

- `semillas/ha = plantas/ha ÷ (PG × eficiencia)`
- `kg/ha = semillas/ha × P1000 ÷ 1.000.000`
- `semillas por metro lineal = semillas/ha × distancia entre surcos (m) ÷ 10.000`
- `plantas/m² × 10.000 = plantas/ha`

| Dato de la calculadora | Significado |
| --- | --- |
| Plantas objetivo | Stand buscado, por hectárea o por metro cuadrado. |
| Eficiencia de emergencia (%) | Plantas logradas sobre semillas germinables. Por defecto 90 %. Mayor que 0 y hasta 100. |
| Distancia entre surcos (cm) | Necesaria solo para las semillas por metro lineal. |
| PG usado | El poder germinativo con curasemilla si existe; si no, el sin curasemilla. La pantalla indica cuál usó. |

Ejemplo: 320.000 plantas/ha, PG 80 %, eficiencia 80 % y P1000 de 150 g dan 500.000 semillas/ha y 75 kg/ha; con surcos
a 52,5 cm, 26,25 semillas por metro lineal.

## Cómo se hace

**Cargar un análisis**

1. Pulsa **Nuevo análisis**.
2. Elige cliente, cultivo y fecha; completa variedad, campaña y laboratorio si los conoces.
3. Carga los valores que tengas; deja vacíos los que no.
4. Pulsa **Crear análisis**. Se abre el detalle.

**Buscar un análisis**

1. Filtra por **Cliente** y **Cultivo**, o escribe en **Buscar variedad**.
2. Pulsa el cultivo y variedad de la fila para abrir el detalle.

**Calcular la densidad de siembra**

1. Abre el detalle del análisis.
2. En **Calculadora de siembra**, indica las plantas objetivo y si son por hectárea o por metro cuadrado.
3. Ajusta la eficiencia de emergencia y, si quieres semillas por metro, la distancia entre surcos.
4. Lee semillas/ha, kg/ha y semillas por metro lineal. Si falta un dato, la calculadora lo indica debajo.

## Reglas y validaciones

| Situación | Mensaje |
| --- | --- |
| Un porcentaje fuera de 0 a 100 | Los porcentajes deben estar entre 0 y 100. Bajo el campo: "El vigor no puede superar 100." o el nombre del porcentaje. |
| Un porcentaje con más de 2 decimales | Los porcentajes admiten hasta 2 decimales. Bajo el campo, por ejemplo: "El vigor admite hasta 2 decimales." |
| Peso de 1000 semillas en cero, negativo o con más de 4 decimales | El peso de 1000 semillas debe ser mayor que 0. / El peso de 1000 semillas admite hasta 4 decimales. |
| Las cuatro fracciones de un tratamiento suman más de 100 | Con curasemilla (o Sin curasemilla): germinadas, anormales, muertas y duras suman N %, más de 100 %. |
| Laboratorio de otro tipo | Elige un laboratorio de semillas. |
| Fecha de análisis vacía | Indica la fecha del análisis. |
| Faltan clientes o cultivos | Primero crea clientes y cultivos. |

Mensajes de la calculadora cuando falta un dato: "Indica las plantas objetivo.", "La eficiencia de emergencia debe ser
mayor que 0 y hasta 100 %.", "El análisis no tiene poder germinativo (ni con ni sin curasemilla).", "Falta el peso de
1000 semillas para calcular kg/ha." e "Indica la distancia entre surcos para calcular las semillas por metro lineal."

- Un valor en cero es un dato medido; vacío significa "sin dato".
- La calculadora no guarda nada: solo calcula en pantalla.

## Preguntas abiertas

- Q18: confirmar la fórmula, qué poder germinativo usar (con o sin curasemilla) y la eficiencia de emergencia habitual
  por cultivo ([preguntas](../engineer-questions.md)).

## Notas técnicas

- Rutas: `GET /seed-analyses?clientId=&cropId=` (más reciente primero), `GET /seed-analyses/{id}`,
  `POST /seed-analyses`, `PUT /seed-analyses/{id}` (reemplazo completo, incluso cliente y cultivo).
- Tabla: `seed_analyses`. Migración `012_analyses.sql`.
- Código: `web/app/backoffice/seed-analyses.jsx`, `analyses.mjs` (`seedingRate`); dominio `SeedAnalysisService.cs`.
