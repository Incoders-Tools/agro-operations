-- Additive operational attributes: client, field and plot details, commercial firms, the reference crop
-- list and the crops of a plot per campaign. Runs after 001, 003, 004, 005 and 006 in one transaction.
-- Every statement is safe to rerun; new columns are nullable and existing rows keep their data.

-- tax_id is the CUIT stored as its 11 digits, as for contractors; producers is free text.
ALTER TABLE clients
    ADD COLUMN IF NOT EXISTS tax_id text CHECK (tax_id ~ '^[0-9]{11}$'),
    ADD COLUMN IF NOT EXISTS producers text CHECK (length(btrim(producers)) > 0),
    ADD COLUMN IF NOT EXISTS email text CHECK (email ~ '^[^@]+@[^@]+$'),
    ADD COLUMN IF NOT EXISTS phone text CHECK (length(btrim(phone)) > 0),
    ADD COLUMN IF NOT EXISTS address text CHECK (length(btrim(address)) > 0);

ALTER TABLE fields
    ADD COLUMN IF NOT EXISTS tenure text CHECK (tenure IN ('propio', 'alquiler_fijo', 'alquiler_porcentaje')),
    ADD COLUMN IF NOT EXISTS locality text CHECK (length(btrim(locality)) > 0);

-- Short plot ID such as "CLI - CAMPO - LOTE"; unique without regard to case only when present.
ALTER TABLE plots
    ADD COLUMN IF NOT EXISTS code text CHECK (code = btrim(code) AND length(code) BETWEEN 1 AND 40);
CREATE UNIQUE INDEX IF NOT EXISTS plots_code_ci_idx ON plots (lower(code)) WHERE code IS NOT NULL;

-- The commercial firms of a client; a firm name is unique across every client.
CREATE TABLE IF NOT EXISTS commercial_firms (
    id uuid PRIMARY KEY,
    client_id uuid NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    tax_id text CHECK (tax_id ~ '^[0-9]{11}$'),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS commercial_firms_name_ci_idx ON commercial_firms (lower(name));
CREATE INDEX IF NOT EXISTS commercial_firms_client_id_idx ON commercial_firms (client_id);

-- Generic reference crops; the catalog stays editable and a same-name crop is never replaced.
INSERT INTO crops (id, name)
SELECT gen_random_uuid(), seed.name
FROM (VALUES
    ('Trigo'), ('Cebada'), ('Avena'), ('Garbanzo'), ('Lenteja'), ('Colza'), ('Camelina'), ('Carinata'),
    ('Arveja verde'), ('Arveja amarilla'), ('Centeno'), ('Triticale'), ('Vicia'), ('Nabo'),
    ('Pastura de gramíneas'), ('Pastura de leguminosas'), ('Pastura consociada'), ('Raigrás'),
    ('Soja'), ('Maíz'), ('Maíz pisingallo'), ('Sorgo granífero'), ('Sorgo forrajero'), ('Sorgo silero'),
    ('Maíz silo'), ('Girasol'), ('Poroto mung'), ('Moha')
) AS seed (name)
ON CONFLICT ((lower(name))) DO NOTHING;

-- Up to two crops per plot and campaign. Renta or servicio is how the crop is used on that plot in
-- that campaign, not a property of the crop; variety is the optional hybrid or variety.
CREATE TABLE IF NOT EXISTS plot_crops (
    id uuid PRIMARY KEY,
    plot_id uuid NOT NULL REFERENCES plots(id) ON DELETE RESTRICT,
    campaign_id uuid NOT NULL REFERENCES campaigns(id) ON DELETE RESTRICT,
    crop_id uuid NOT NULL REFERENCES crops(id) ON DELETE RESTRICT,
    position smallint NOT NULL CHECK (position IN (1, 2)),
    role text NOT NULL CHECK (role IN ('renta', 'servicio')),
    variety text CHECK (length(btrim(variety)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS plot_crops_position_idx ON plot_crops (plot_id, campaign_id, position);
CREATE INDEX IF NOT EXISTS plot_crops_campaign_id_idx ON plot_crops (campaign_id);
CREATE INDEX IF NOT EXISTS plot_crops_crop_id_idx ON plot_crops (crop_id);
