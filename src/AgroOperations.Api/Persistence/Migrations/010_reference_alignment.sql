-- Additive alignment with the engineers' domain reference: labor grouping and descriptions, plot crop yields, the
-- inoculante input category, crop spellings and contractor websites. Runs after 001 and 003-009 in one transaction.
-- Every statement is safe to rerun and keeps existing rows: seeds only fill a missing category or description, and a
-- crop is renamed only while it still holds its untouched seeded name.

-- Category is the reference grouping of a labor; description is the reference's explanatory note.
ALTER TABLE labor_types
    ADD COLUMN IF NOT EXISTS category text
        CHECK (category IN ('aplicacion', 'labranza', 'siembra', 'cosecha', 'forrajes', 'infraestructura')),
    ADD COLUMN IF NOT EXISTS description text
        CHECK (description IS NULL OR (description = btrim(description) AND length(description) BETWEEN 1 AND 300));

-- The 006 labors keep id, name and abbreviation; only a missing category or description is filled.
UPDATE labor_types l
SET category = coalesce(l.category, seed.category), description = coalesce(l.description, seed.description), revision = l.revision + 1
FROM (VALUES
    ('Pulverización terrestre', 'aplicacion', NULL), ('Pulverización aérea', 'aplicacion', NULL),
    ('Fertilización al voleo', 'aplicacion', NULL), ('Fertilización incorporada', 'aplicacion', NULL),
    ('Fertilización líquida', 'aplicacion', NULL), ('Disco', 'labranza', NULL), ('Rolo', 'labranza', NULL),
    ('Rastra y rolo', 'labranza', NULL), ('Subsolador', 'labranza', NULL), ('Siembra fina', 'siembra', NULL),
    ('Siembra gruesa', 'siembra', NULL), ('Resiembra', 'siembra', NULL),
    ('Cosecha fina', 'cosecha', 'trigo, cebada, colza, etc.'), ('Cosecha gruesa', 'cosecha', 'soja, maíz, girasol, sorgo')
) AS seed (name, category, description)
WHERE lower(l.name) = lower(seed.name)
  AND (l.category IS NULL OR (l.description IS NULL AND seed.description IS NOT NULL));

-- The labors of the reference, without abbreviation. A same-name labor is kept and only its missing values are filled.
INSERT INTO labor_types (id, name, category, description)
SELECT gen_random_uuid(), seed.name, seed.category, seed.description
FROM (VALUES
    ('Pulverización aérea con avión', 'aplicacion', NULL), ('Pulverización aérea con dron', 'aplicacion', NULL),
    ('Fertilización al voleo proyectada', 'aplicacion', NULL), ('Fertilización al voleo neumática', 'aplicacion', NULL),
    ('Disco doble acción', 'labranza', NULL), ('Acondicionador de suelos', 'labranza', NULL),
    ('Cincel con rastra, peine o rolo', 'labranza', NULL), ('Rastra de dientes', 'labranza', NULL),
    ('Rastra de dientes con rolo', 'labranza', NULL), ('Disco + rastra de dientes + rolo', 'labranza', NULL),
    ('Siembra fina con fertilización', 'siembra', NULL), ('Siembra de gruesa a placa con fertilización simple', 'siembra', NULL),
    ('Siembra de gruesa neumática con fertilización simple', 'siembra', NULL),
    ('Siembra de gruesa a placa con fertilización doble', 'siembra', NULL),
    ('Siembra de gruesa neumática con fertilización doble', 'siembra', NULL),
    ('Siembra de gruesa a chorrillo sin fertilización', 'siembra', NULL),
    ('Siembra de gruesa a chorrillo con fertilización', 'siembra', NULL), ('Cosecha', 'cosecha', NULL),
    ('Subsolado / Descompactado profundo', 'labranza', 'paratill o subsolador de púas'),
    ('Rastra de disco pesada', 'labranza', 'excéntrica o "rastra tiro excéntrico"'),
    ('Cultivador de campo', 'labranza', 'con rolo o rastra de dientes'),
    ('Vibrocultivador', 'labranza', 'con peine o rolo desterronador'),
    ('Rastra rotativa', 'labranza', 'de eje vertical u horizontal / rotovator'),
    ('Nivelación / Emparejamiento con rabasto', 'labranza', NULL),
    ('Nivelación láser / Nivelación con pala de arrastre', 'labranza', 'sistematización y drenaje'),
    ('Sistematización de curvas de nivel / Armado de terrazas', 'labranza', NULL),
    ('Borrado de huellas', 'labranza', 'de cosechadora o pulverizadora'),
    ('Picado / Triturado de rastrojo', 'labranza', 'desmalezadora de hélice o trituradora de martillos'),
    ('Rolado de cultivos de cobertura / Rolado de rastrojos', 'labranza', 'rolo faca / rolo picador'),
    ('Siembra directa fina sin fertilización', 'siembra', NULL),
    ('Siembra directa de gruesa a placa sin fertilización', 'siembra', NULL),
    ('Siembra directa de gruesa neumática sin fertilización', 'siembra', NULL),
    ('Siembra con dosificación variable', 'siembra', 'semilla y/o fertilizante'),
    ('Siembra al voleo', 'siembra', 'pasturas o cultivos de cobertura'),
    ('Interseeding / Siembra aérea de cultivos de servicio', 'siembra', 'con avión o dron sobre cultivo en pie'),
    ('Siembra variable', 'siembra', NULL),
    ('Inoculación y curado de semillas a campo', 'siembra', 'si se factura como labor aparte'),
    ('Pulverización terrestre selectiva', 'aplicacion', 'sensores ópticos tipo WeedSeeker / Weed-It'),
    ('Escardillado / Carpida mecánica', 'labranza', 'cultivador entre surcos'),
    ('Control de plagas con cebos tóxicos', 'aplicacion', 'aplicación al voleo para hormigas, babosas o tucuras'),
    ('Fertilización al voleo con dosis variable', 'aplicacion', 'ambientación'),
    ('Fertilización líquida chorreada', 'aplicacion', 'en superficie'), ('Fertilización variable', 'aplicacion', NULL),
    ('Fertilización líquida inyectada / incorporada', 'aplicacion', 'ej. UAN o amoníaco anhidro'),
    ('Aplicación de enmiendas calcáreas / Yeso agrícola', 'aplicacion', 'encalado'),
    ('Aplicación de enmiendas orgánicas', 'aplicacion', 'estiércol sólido, cama de pollo o compost con estercolera'),
    ('Aplicación de efluentes líquidos / Purines', 'aplicacion', 'con tanque estercolero e inyector'),
    ('Fertilización foliar', 'aplicacion', 'cuando se realiza como pasada específica sin fitosanitarios'),
    ('Cosecha con plataforma draper / cabezal maicero / cabezal girasolero', 'cosecha', 'si se diferencia tarifa por cabezal'),
    ('Hilerado / Corte e hilerado', 'cosecha', 'cosecha en dos tiempos, ej. colza, pasturas o poroto'),
    ('Recolección / Trilla de cordón', 'cosecha', 'con cabezal recolector'),
    ('Acarreo en lote / Movimiento de tolva autodescargable', 'cosecha', 'logística interna hasta camión o silo'),
    ('Embolsado de grano seco / húmedo', 'cosecha', 'con embolsadora'),
    ('Extracción de grano de silobolsa', 'cosecha', 'con extractora'),
    ('Cosecha de grano húmedo', 'cosecha', 'para quebrado y embolsado'),
    ('Corte de pasturas', 'forrajes', 'segadora de discos o hélice'),
    ('Corte con acondicionador', 'forrajes', 'segadora acondicionadora de rodillos o dedos'),
    ('Rastrillado / Hilerado de forraje', 'forrajes', 'rastrillo estelar o de entrega lateral'),
    ('Confección de rollos', 'forrajes', 'rotoenfardadora'), ('Confección de megafardos / fardos prismáticos', 'forrajes', NULL),
    ('Encintado / Plastificado de rollos', 'forrajes', 'henolaje'),
    ('Picado fino de forraje', 'forrajes', 'silaje de maíz, sorgo o pasturas'),
    ('Compactación y tapado de silo', 'forrajes', 'silo puente o torta'),
    ('Desmalezado / Limpieza de potreros', 'forrajes', NULL),
    ('Mantenimiento de caminos internos / Cuneteo', 'infraestructura', 'motoniveladora o disco'),
    ('Limpieza de canales y desagües', 'infraestructura', 'retroexcavadora'),
    ('Desmonte selectivo / Rolado de renovales', 'infraestructura', NULL),
    ('Mantenimiento de alambrados y cortafuegos', 'infraestructura', NULL)
) AS seed (name, category, description)
ON CONFLICT ((lower(name))) DO UPDATE
    SET category = coalesce(labor_types.category, EXCLUDED.category),
        description = coalesce(labor_types.description, EXCLUDED.description),
        revision = labor_types.revision + 1
    WHERE labor_types.category IS NULL OR (labor_types.description IS NULL AND EXCLUDED.description IS NOT NULL);

-- Average harvest yield of a plot crop: grain and forage biomass, in kg/ha.
ALTER TABLE plot_crops
    ADD COLUMN IF NOT EXISTS grain_yield_kg_ha numeric(12,2) CHECK (grain_yield_kg_ha >= 0),
    ADD COLUMN IF NOT EXISTS forage_biomass_kg_ha numeric(12,2) CHECK (forage_biomass_kg_ha >= 0);

-- The reference lists inoculants among the input categories.
ALTER TABLE inputs DROP CONSTRAINT IF EXISTS inputs_category_check;
ALTER TABLE inputs ADD CONSTRAINT inputs_category_check
    CHECK (category IN ('fertilizante', 'herbicida', 'insecticida', 'fungicida', 'coadyuvante',
        'curasemilla', 'bioestimulante', 'terapico', 'inoculante'));

-- Reference spellings for four 007 crops. A crop the user already edited, or whose new name is taken, keeps its name.
UPDATE crops c
SET name = rename.new_name, revision = c.revision + 1
FROM (VALUES
    ('Pastura de gramíneas', 'Pasturas puras de gramíneas'), ('Pastura de leguminosas', 'Pasturas puras de leguminosas'),
    ('Raigrás', 'Raygrass'), ('Poroto mung', 'Poroto mungo')
) AS rename (old_name, new_name)
WHERE c.name = rename.old_name AND c.revision = 1
  AND NOT EXISTS (SELECT 1 FROM crops other WHERE lower(other.name) = lower(rename.new_name));

-- A single web address, without spaces.
ALTER TABLE contractors
    ADD COLUMN IF NOT EXISTS website text CHECK (website ~ '^\S{1,200}$');
