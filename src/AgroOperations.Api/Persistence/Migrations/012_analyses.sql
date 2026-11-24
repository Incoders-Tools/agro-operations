-- Additive soil and seed analyses. Runs after 001 and 003-011 in one transaction. Every statement is safe to rerun and
-- keeps existing rows. The rules that span rows (at least one result, a station on every result when sampling by
-- stations, the laboratory kind) are enforced by the API in the same transaction as the write.

-- A soil analysis of a plot in a campaign, sampled over the plot average or at predefined sampling stations; map_link
-- is a single address of the shape or KML file of the sampled area.
CREATE TABLE IF NOT EXISTS soil_analyses (
    id uuid PRIMARY KEY,
    plot_id uuid NOT NULL REFERENCES plots(id) ON DELETE RESTRICT,
    campaign_id uuid NOT NULL REFERENCES campaigns(id) ON DELETE RESTRICT,
    sampled_on date NOT NULL,
    analyzed_on date,
    laboratory_id uuid REFERENCES providers(id) ON DELETE RESTRICT,
    sampling_method text NOT NULL CHECK (sampling_method IN ('promedio', 'estaciones')),
    sampling_detail text CHECK (length(btrim(sampling_detail)) > 0),
    -- PostgreSQL limits a regular expression repetition to 255, so the length is checked apart.
    map_link text CHECK (length(map_link) BETWEEN 1 AND 300 AND map_link !~ '\s'),
    notes text CHECK (length(btrim(notes)) > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE INDEX IF NOT EXISTS soil_analyses_plot_id_idx ON soil_analyses (plot_id);
CREATE INDEX IF NOT EXISTS soil_analyses_campaign_id_idx ON soil_analyses (campaign_id);
CREATE INDEX IF NOT EXISTS soil_analyses_laboratory_id_idx ON soil_analyses (laboratory_id);

-- One measured parameter of an analysis; the unit is absent for unitless parameters such as pH. The parameter list is
-- free text until the engineers confirm it.
CREATE TABLE IF NOT EXISTS soil_analysis_results (
    id uuid PRIMARY KEY,
    analysis_id uuid NOT NULL REFERENCES soil_analyses(id) ON DELETE RESTRICT,
    position smallint NOT NULL CHECK (position > 0),
    parameter text NOT NULL CHECK (length(btrim(parameter)) > 0 AND length(parameter) <= 100),
    value numeric(14,4) NOT NULL,
    unit text CHECK (length(btrim(unit)) > 0 AND length(unit) <= 30),
    depth text CHECK (length(btrim(depth)) > 0 AND length(depth) <= 30),
    station text CHECK (length(btrim(station)) > 0 AND length(station) <= 60),
    UNIQUE (analysis_id, position)
);

-- Seed quality of a client's crop and variety or hybrid: thousand seed weight, germination with and without seed
-- treatment detailing abnormal seedlings, dead and hard seeds, and vigor. Every measured value is optional; per treatment
-- the present fractions add up to at most 100.
CREATE TABLE IF NOT EXISTS seed_analyses (
    id uuid PRIMARY KEY,
    client_id uuid NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    crop_id uuid NOT NULL REFERENCES crops(id) ON DELETE RESTRICT,
    variety text CHECK (length(btrim(variety)) > 0),
    campaign_id uuid REFERENCES campaigns(id) ON DELETE RESTRICT,
    analyzed_on date NOT NULL,
    laboratory_id uuid REFERENCES providers(id) ON DELETE RESTRICT,
    thousand_seed_weight_g numeric(10,4) CHECK (thousand_seed_weight_g > 0),
    germination_treated_pct numeric(5,2) CHECK (germination_treated_pct BETWEEN 0 AND 100),
    abnormal_treated_pct numeric(5,2) CHECK (abnormal_treated_pct BETWEEN 0 AND 100),
    dead_treated_pct numeric(5,2) CHECK (dead_treated_pct BETWEEN 0 AND 100),
    hard_treated_pct numeric(5,2) CHECK (hard_treated_pct BETWEEN 0 AND 100),
    germination_untreated_pct numeric(5,2) CHECK (germination_untreated_pct BETWEEN 0 AND 100),
    abnormal_untreated_pct numeric(5,2) CHECK (abnormal_untreated_pct BETWEEN 0 AND 100),
    dead_untreated_pct numeric(5,2) CHECK (dead_untreated_pct BETWEEN 0 AND 100),
    hard_untreated_pct numeric(5,2) CHECK (hard_untreated_pct BETWEEN 0 AND 100),
    vigor_pct numeric(5,2) CHECK (vigor_pct BETWEEN 0 AND 100),
    notes text CHECK (length(btrim(notes)) > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0),
    CHECK (coalesce(germination_treated_pct, 0) + coalesce(abnormal_treated_pct, 0) + coalesce(dead_treated_pct, 0)
           + coalesce(hard_treated_pct, 0) <= 100),
    CHECK (coalesce(germination_untreated_pct, 0) + coalesce(abnormal_untreated_pct, 0) + coalesce(dead_untreated_pct, 0)
           + coalesce(hard_untreated_pct, 0) <= 100)
);
CREATE INDEX IF NOT EXISTS seed_analyses_client_id_idx ON seed_analyses (client_id);
CREATE INDEX IF NOT EXISTS seed_analyses_crop_id_idx ON seed_analyses (crop_id);
CREATE INDEX IF NOT EXISTS seed_analyses_campaign_id_idx ON seed_analyses (campaign_id);
CREATE INDEX IF NOT EXISTS seed_analyses_laboratory_id_idx ON seed_analyses (laboratory_id);
