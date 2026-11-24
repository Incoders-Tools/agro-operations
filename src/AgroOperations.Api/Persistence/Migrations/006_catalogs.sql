-- Additive operational catalogs: labor abbreviations, crop-cycle stages, contractors and inputs.
-- Runs after 001, 003, 004 and 005 in one transaction. Every statement is safe to rerun and keeps
-- existing rows: seeds never replace a same-name row, they only fill a missing labor abbreviation.
ALTER TABLE labor_types ADD COLUMN IF NOT EXISTS abbreviation text
    CHECK (abbreviation IS NULL OR (abbreviation = btrim(abbreviation) AND length(abbreviation) BETWEEN 1 AND 12));

INSERT INTO labor_types (id, name, abbreviation)
SELECT gen_random_uuid(), seed.name, seed.abbreviation
FROM (VALUES
    ('Cosecha fina', 'COS-FIN'), ('Cosecha gruesa', 'COS-GRU'), ('Disco', 'DISCO'),
    ('Fertilización al voleo', 'FERT-V'), ('Fertilización incorporada', 'FERT-INC'), ('Fertilización líquida', 'FERT-LIQ'),
    ('Pulverización aérea', 'PULV-AE'), ('Pulverización terrestre', 'PULV-TE'), ('Rolo', 'ROL'), ('Rastra y rolo', 'RAS-ROL'),
    ('Siembra fina', 'SIEM-FIN'), ('Siembra gruesa', 'SIEM-GRU'), ('Resiembra', 'RE-SIEM'), ('Subsolador', 'SUBSO')
) AS seed (name, abbreviation)
ON CONFLICT ((lower(name))) DO UPDATE
    SET abbreviation = EXCLUDED.abbreviation, revision = labor_types.revision + 1
    WHERE labor_types.abbreviation IS NULL;

-- Stage is when a labor happens in the crop cycle; recipes.stage keeps its own older check for now.
CREATE TABLE IF NOT EXISTS stages (
    id uuid PRIMARY KEY,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    sort_order integer NOT NULL,
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS stages_name_ci_idx ON stages (lower(name));
INSERT INTO stages (id, name, sort_order)
SELECT gen_random_uuid(), seed.name, seed.sort_order
FROM (VALUES
    ('Barbecho', 10), ('Presiembra', 20), ('Preemergencia', 30), ('Postsiembra', 40),
    ('Postemergencia', 50), ('Prefloración', 60), ('Postfloración', 70)
) AS seed (name, sort_order)
ON CONFLICT ((lower(name))) DO NOTHING;

-- tax_id is the CUIT stored as its 11 digits, without separators.
CREATE TABLE IF NOT EXISTS contractors (
    id uuid PRIMARY KEY,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    tax_id text CHECK (tax_id ~ '^[0-9]{11}$'),
    phone text CHECK (length(btrim(phone)) > 0),
    email text CHECK (email ~ '^[^@]+@[^@]+$'),
    notes text CHECK (length(btrim(notes)) > 0),
    own_equipment boolean NOT NULL DEFAULT false,
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS contractors_name_ci_idx ON contractors (lower(name));
INSERT INTO contractors (id, name, own_equipment) VALUES (gen_random_uuid(), 'Propio', true)
ON CONFLICT ((lower(name))) DO NOTHING;

-- Real inputs arrive through the private local import; this migration seeds none.
CREATE TABLE IF NOT EXISTS inputs (
    id uuid PRIMARY KEY,
    commercial_name text NOT NULL CHECK (length(btrim(commercial_name)) > 0),
    active_ingredient text CHECK (length(btrim(active_ingredient)) > 0),
    concentration text CHECK (length(btrim(concentration)) > 0),
    formulation text CHECK (length(btrim(formulation)) > 0),
    mix_order smallint CHECK (mix_order BETWEEN 1 AND 20),
    category text CHECK (category IN ('fertilizante', 'herbicida', 'insecticida', 'fungicida', 'coadyuvante',
        'curasemilla', 'bioestimulante', 'terapico')),
    unit text NOT NULL CHECK (length(btrim(unit)) > 0),
    dose_min numeric(12,4) CHECK (dose_min > 0),
    dose_max numeric(12,4) CHECK (dose_max > 0),
    reference_cost numeric(14,4) CHECK (reference_cost >= 0),
    hollow_cone boolean NOT NULL DEFAULT false,
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0),
    CHECK (dose_min IS NULL OR dose_max IS NULL OR dose_min <= dose_max)
);
CREATE UNIQUE INDEX IF NOT EXISTS inputs_commercial_name_ci_idx ON inputs (lower(commercial_name));

-- Provenance can now trace imported contractors and inputs.
ALTER TABLE import_records DROP CONSTRAINT IF EXISTS import_records_entity_type_check;
ALTER TABLE import_records ADD CONSTRAINT import_records_entity_type_check
    CHECK (entity_type IN ('client', 'field', 'plot', 'contractor', 'input'));
