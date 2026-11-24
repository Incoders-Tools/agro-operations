# Soil and seed analyses, providers, reference prices and quote comparison

Owner go-ahead of 2026-11-24 ("arranca") for the four modules of the engineers' domain reference that did not exist
yet. Public repository: no client values. The API contract below is binding for the API and UI workers.

## Specs
- S1 Soil analysis (reference point 9): "Periódicamente en los lotes realizamos recomendacion de extraccion y análisis
  de suelos de diferentes nutrientes y/o parámetros de suelos. Ese resultado de análisis debe quedar registrado en el
  lote correspondiente y campaña ... deberiamos poder tener un fácil acceso a esa información incluso en otras
  campañas y que podamos ir cargando mas datos de análisis de suelos en campañas nuevas. Estos análisis de suelos
  puede realizarse en el promedio del lote o por estaciones de muestreo pre definido, por lo tanto debe quedar el
  detalle de como se realizo ese muestreo, y puede llegar a tener un enlace al archivo shape o kml que muestre la zona
  de muestreo. Que se registre la fecha de análisis de suelo." Per plot and campaign; all campaigns visible from the
  plot, with a per-parameter history across campaigns.
- S2 Seed analysis (reference point 10): "Todos los cultivos a implantar tienen un dato especifico de calidad de
  semilla que engloba peso de 1000, poder germinativo con curasemilla; detallando plántulas anormales, muertas y
  semillas duras. poder germinativo sin curasemilla; detallando plántulas anormales, muertas y semillas duras. Vigor.
  Estos datos deberían quedar registrados según cultivo y variedad o hibrido de cada cultivo según la procedencia de
  cada productor. Aveces no tenemos esa información. En base a estos datos podemos definir la población de semillas en
  kg/ha o semillas/ha a sembrar para enviar la información a los clientes". Every measured value optional; a seeding
  calculator derives seeds/ha, kg/ha and seeds per linear metre from a target stand.
- S3 Client input suppliers (reference): "Nuestros clientes consultan y compran en diferentes proveedores de los
  cuales deberiamos registrar las diferentes firmas comerciales, con sus representantes de ventas y representantes
  técnicos, y los precios de referencia de productos que tenemos. aveces los clientes nos mandan diferentes
  presupuestos de diferentes proveedores y productos y tenemos que evaluar cual conviene y los motivos." Providers of
  kind insumos, their contacts, which clients buy from them, reference prices per input, and quote comparisons with
  the chosen quote and the reason.
- S4 Service providers (reference): "Listado de proveedores de servicios contratistas, laboratorios de suelos,
  semillas, plataformas virtuales. Los contactos de cada uno, y la web, etc". Providers of kinds
  laboratorio_suelos, laboratorio_semillas, plataforma_virtual, otro, with contacts and website; contractors stay
  their own entity (work orders use them) and already have contacts and website. Analyses may reference a lab.

## API contract (JSON camelCase; every write is revision-guarded except link create/delete; CSRF as today)
- Provider {id, name (unique ci), kind (insumos|laboratorio_suelos|laboratorio_semillas|plataforma_virtual|otro),
  taxId (CUIT rules), website (contractor rules), phone, email (rules), notes, revision}. GET /providers?kind=,
  POST, PUT (full replace).
- ProviderContact {id, providerId, name, role (ventas|tecnico|otro), phone, email, notes, revision}.
  GET /provider-contacts?providerId=, POST, PUT (cannot change provider).
- ClientProvider {id, clientId, providerId} unique pair. GET /client-providers?clientId=|providerId=, POST (409
  "Duplicate link"), DELETE /client-providers/{id} (204; 404 if missing).
- ProviderPrice {id, providerId, inputId, price (> 0, numeric(14,4)), currency (ARS|USD), unit (default the input's
  unit), validOn (date), notes, revision}. GET /provider-prices?providerId=|inputId=, POST, PUT.
- QuoteComparison {id, clientId, title, requestedOn, status (abierta|decidida), chosenQuoteId, decisionReason,
  revision}. GET /quote-comparisons?clientId=&status=, GET /quote-comparisons/{id} -> {comparison, quotes:[Quote],
  products:[{key, label, offers:[{quoteId, providerId, unitPrice, currency, quantity, unit, lineTotal}],
  cheapestQuoteIdByCurrency:{ARS?,USD?}}]}, POST, PUT (title, requestedOn; only while abierta),
  POST /quote-comparisons/{id}/decide {quoteId, reason (required), revision} -> decidida;
  POST /quote-comparisons/{id}/reopen {revision} -> abierta (keeps the previous reason until a new decision).
- Quote {id, comparisonId, providerId, receivedOn, currency (ARS|USD), notes, lines:[{id, position, inputId?,
  productText?, quantity (> 0), unit, unitPrice (>= 0)}] (each line has inputId or productText), total, revision}.
  POST /quote-comparisons/{id}/quotes, PUT /quotes/{id} (full replace with lines; only while the comparison is
  abierta). Product key: "input:<inputId>" or "text:<lowercased trimmed productText>".
- SoilAnalysis {id, plotId, campaignId, sampledOn, analyzedOn?, laboratoryId? (provider of kind
  laboratorio_suelos), samplingMethod (promedio|estaciones), samplingDetail, mapLink (single address without
  spaces), notes, results:[{id, parameter, value (numeric(14,4)), unit, depth?, station?}], revision}.
  GET /soil-analyses?plotId= (newest first, all campaigns), GET /soil-analyses/{id}, POST (with results), PUT (full
  replace with results). At least one result; station required on every result when samplingMethod = estaciones.
- SeedAnalysis {id, clientId, cropId, variety?, campaignId?, analyzedOn, laboratoryId? (laboratorio_semillas),
  thousandSeedWeightG?, germinationTreatedPct?, abnormalTreatedPct?, deadTreatedPct?, hardTreatedPct?,
  germinationUntreatedPct?, abnormalUntreatedPct?, deadUntreatedPct?, hardUntreatedPct?, vigorPct?, notes,
  revision}. Percentages 0-100; per treatment the four present values add up to at most 100. GET
  /seed-analyses?clientId=&cropId=, POST, PUT.
- 400 messages (exact, for UI mapping): "Kind must be one of: ...", "Role must be one of: ventas, tecnico, otro",
  "Currency must be ARS or USD", "Price must be positive and fit numeric(14,4)", "Quantity must be positive",
  "Unit price must be zero or positive", "Each line needs an input or a product", "Reason is required",
  "Comparison is not open" (409), "Sampling method must be promedio or estaciones", "At least one result is
  required", "Each result needs a station when sampling by stations", "Percentages must be between 0 and 100",
  "Seed fractions add up to more than 100", "Laboratory must be a soil laboratory" / "Laboratory must be a seed
  laboratory", "Map link must be a single address without spaces".

## Tasks
- T1 (S3, S4) worker: migration 011 providers, contacts, client links, prices, quote comparisons + API + tests. | done
- T2 (S1, S2) worker, parallel with T1: UI for soil analyses (plot level), seed analyses section, calculator. | done (commit: analyses UI; API round trip in T5)
- T3 (S1, S2) worker: migration 012 soil and seed analyses + API + tests. | done
- T4 (S3, S4) worker, parallel with T3: UI for providers, contacts, client links, prices, quote comparison. | done
- T5: launcher 011-012, engineer questions, docs, real DB apply, browser check, push.

## Log
- L1 (owner, 2026-11-24, verbatim): "arranca" — answer to "¿Arranco con alguno, o esperamos a que traigas las
  respuestas de los ingenieros?" after listing soil analysis, seed analysis, client input suppliers and service
  providers.
- L2 (decision): one Provider entity with a kind covers client input suppliers and service providers (labs, virtual
  platforms); contractors stay separate because work orders reference them. The seeding formula, soil parameter list
  and quote taxes/currency go to the engineer questions; the calculator states its assumptions on screen.
