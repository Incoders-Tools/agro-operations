# Align the implemented model with the engineers' domain reference

Owner request of 2026-11-24: anything implemented that differs from the engineers' domain reference document must be
fixed now. The reference is the shared Google document (same text as the workbook folder's general information
document). Public repository: no client values here.

## Specs
- S1: Labors as listed by the reference, with its grouping. `labor_types` gains `category` (one of `aplicacion`,
  `labranza`, `siembra`, `cosecha`, `forrajes`, `infraestructura`; UI: Aplicación, Labranza y preparación, Siembra e
  implantación, Cosecha y poscosecha, Forrajes y ganadería, Mantenimiento de infraestructura) and `description`
  (the reference's explanatory parenthesis). Existing labors keep id, name and abbreviation; only NULL category or
  description are filled. New labors (name | category | description, "-" = none):
  Pulverización aérea con avión | aplicacion | - ; Pulverización aérea con dron | aplicacion | - ;
  Fertilización al voleo proyectada | aplicacion | - ; Fertilización al voleo neumática | aplicacion | - ;
  Disco doble acción | labranza | - ; Acondicionador de suelos | labranza | - ;
  Cincel con rastra, peine o rolo | labranza | - ; Rastra de dientes | labranza | - ;
  Rastra de dientes con rolo | labranza | - ; Disco + rastra de dientes + rolo | labranza | - ;
  Siembra fina con fertilización | siembra | - ; Siembra de gruesa a placa con fertilización simple | siembra | - ;
  Siembra de gruesa neumática con fertilización simple | siembra | - ;
  Siembra de gruesa a placa con fertilización doble | siembra | - ;
  Siembra de gruesa neumática con fertilización doble | siembra | - ;
  Siembra de gruesa a chorrillo sin fertilización | siembra | - ;
  Siembra de gruesa a chorrillo con fertilización | siembra | - ; Cosecha | cosecha | - ;
  Subsolado / Descompactado profundo | labranza | paratill o subsolador de púas ;
  Rastra de disco pesada | labranza | excéntrica o "rastra tiro excéntrico" ;
  Cultivador de campo | labranza | con rolo o rastra de dientes ;
  Vibrocultivador | labranza | con peine o rolo desterronador ;
  Rastra rotativa | labranza | de eje vertical u horizontal / rotovator ;
  Nivelación / Emparejamiento con rabasto | labranza | - ;
  Nivelación láser / Nivelación con pala de arrastre | labranza | sistematización y drenaje ;
  Sistematización de curvas de nivel / Armado de terrazas | labranza | - ;
  Borrado de huellas | labranza | de cosechadora o pulverizadora ;
  Picado / Triturado de rastrojo | labranza | desmalezadora de hélice o trituradora de martillos ;
  Rolado de cultivos de cobertura / Rolado de rastrojos | labranza | rolo faca / rolo picador ;
  Siembra directa fina sin fertilización | siembra | - ;
  Siembra directa de gruesa a placa sin fertilización | siembra | - ;
  Siembra directa de gruesa neumática sin fertilización | siembra | - ;
  Siembra con dosificación variable | siembra | semilla y/o fertilizante ;
  Siembra al voleo | siembra | pasturas o cultivos de cobertura ;
  Interseeding / Siembra aérea de cultivos de servicio | siembra | con avión o dron sobre cultivo en pie ;
  Siembra variable | siembra | - ;
  Inoculación y curado de semillas a campo | siembra | si se factura como labor aparte ;
  Pulverización terrestre selectiva | aplicacion | sensores ópticos tipo WeedSeeker / Weed-It ;
  Escardillado / Carpida mecánica | labranza | cultivador entre surcos ;
  Control de plagas con cebos tóxicos | aplicacion | aplicación al voleo para hormigas, babosas o tucuras ;
  Fertilización al voleo con dosis variable | aplicacion | ambientación ;
  Fertilización líquida chorreada | aplicacion | en superficie ; Fertilización variable | aplicacion | - ;
  Fertilización líquida inyectada / incorporada | aplicacion | ej. UAN o amoníaco anhidro ;
  Aplicación de enmiendas calcáreas / Yeso agrícola | aplicacion | encalado ;
  Aplicación de enmiendas orgánicas | aplicacion | estiércol sólido, cama de pollo o compost con estercolera ;
  Aplicación de efluentes líquidos / Purines | aplicacion | con tanque estercolero e inyector ;
  Fertilización foliar | aplicacion | cuando se realiza como pasada específica sin fitosanitarios ;
  Cosecha con plataforma draper / cabezal maicero / cabezal girasolero | cosecha | si se diferencia tarifa por cabezal ;
  Hilerado / Corte e hilerado | cosecha | cosecha en dos tiempos, ej. colza, pasturas o poroto ;
  Recolección / Trilla de cordón | cosecha | con cabezal recolector ;
  Acarreo en lote / Movimiento de tolva autodescargable | cosecha | logística interna hasta camión o silo ;
  Embolsado de grano seco / húmedo | cosecha | con embolsadora ;
  Extracción de grano de silobolsa | cosecha | con extractora ;
  Cosecha de grano húmedo | cosecha | para quebrado y embolsado ;
  Corte de pasturas | forrajes | segadora de discos o hélice ;
  Corte con acondicionador | forrajes | segadora acondicionadora de rodillos o dedos ;
  Rastrillado / Hilerado de forraje | forrajes | rastrillo estelar o de entrega lateral ;
  Confección de rollos | forrajes | rotoenfardadora ; Confección de megafardos / fardos prismáticos | forrajes | - ;
  Encintado / Plastificado de rollos | forrajes | henolaje ;
  Picado fino de forraje | forrajes | silaje de maíz, sorgo o pasturas ;
  Compactación y tapado de silo | forrajes | silo puente o torta ;
  Desmalezado / Limpieza de potreros | forrajes | - ;
  Mantenimiento de caminos internos / Cuneteo | infraestructura | motoniveladora o disco ;
  Limpieza de canales y desagües | infraestructura | retroexcavadora ;
  Desmonte selectivo / Rolado de renovales | infraestructura | - ;
  Mantenimiento de alambrados y cortafuegos | infraestructura | - .
  Categories for existing labors: Pulverización terrestre, Pulverización aérea, Fertilización al voleo, Fertilización
  incorporada, Fertilización líquida -> aplicacion; Disco, Rolo, Rastra y rolo, Subsolador -> labranza; Siembra fina,
  Siembra gruesa, Resiembra -> siembra; Cosecha fina (description "trigo, cebada, colza, etc."), Cosecha gruesa
  (description "soja, maíz, girasol, sorgo") -> cosecha. The OT labor select groups labors by category.
- S2: "El dato de los cultivos de cada campaña y en cada lote deben quedar registrados en el historial del lote, al
  igual que otros datos como el rendimiento promedio de cosecha tanto de grano como de biomasa de forraje".
  `plot_crops` gains optional `grain_yield_kg_ha` and `forage_biomass_kg_ha` (numeric(12,2), >= 0), editable in the
  plot's crop history.
- S3: "Este lote puede ser nuevo o venir de campañas anteriores del mismo productor o cambiar de productor." A field
  can move to another client and a plot to another field (optional `clientId` on field PUT, optional `fieldId` on
  plot PUT; omitted = unchanged). Existing OTs keep their firm and plots.
- S4: Input categories follow the reference: "Fertilizantes, Herbicidas, Inecticidas, Funguicidas, Coadyuvantes,
  Curasemillas, Bioestimulantes, Terapicos semillas, Inoculantes": add `inoculante`; show `terapico` as "Terápico de
  semillas".
- S5: Crop names as the reference lists them (accents corrected): rename seeded "Pastura de gramíneas" -> "Pasturas
  puras de gramíneas", "Pastura de leguminosas" -> "Pasturas puras de leguminosas", "Raigrás" -> "Raygrass",
  "Poroto mung" -> "Poroto mungo"; only rows still at revision 1 with the seeded name.
- S6: "Listado de proveedores de servicios contratistas ... Los contactos de cada uno, y la web": contractors gain
  optional `website`. Client "celular" is shown as "Celular" (stored phone).

## Tasks
- T1 (S1-S6) worker: migration 010 + API + tests.
- T2 (S1-S6) worker: UI.
- T3: launcher, docs, real DB apply, browser check, push.

## Log
- L1 (owner, 2026-11-24, verbatim): "Excelente guardemos las preguntas luego te las resuelvo cuando me reuna con ellos
  y revisemos si dentro de lo que tenemos para resolver no esta resuelto en este documento que me enviaron:
  [Google document] si algo que implementamos no esta como indica alli, debemos hacerlo de inmediato"
- L2 (evidence): the shared document equals the workbook folder's general information document except bullet marks
  and one line break. The previous round read only its first 7,000 characters; its tail adds labors grouping, crop
  history with yields, input categories, soil analysis, seed analysis and the suppliers database.
- L3 (decision): the reference's section-6 duplicates "Embolsado de grano" and "Extracción de grano" are covered by
  the section-5 entries and not seeded twice. Soil analysis, seed analysis and the suppliers/labs/platforms database
  are new scope, not deviations, and wait for the owner's go-ahead. Q1 and Q13 answered, Q12 and Q15 partly.
