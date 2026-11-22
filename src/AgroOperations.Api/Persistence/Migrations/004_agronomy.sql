-- Additive agronomy domain: campaigns, per-campaign plot area, labor catalog and internal recipes.
-- A recipe is an internal planning record. It is never an officially issued government
-- prescription and never proves that a labor was executed on the field.
CREATE TABLE campaigns (
    id uuid PRIMARY KEY,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE UNIQUE INDEX campaigns_name_ci_idx ON campaigns (lower(name));

-- Plot area can change between campaigns; plots.area_hectares stays as the current reference value.
CREATE TABLE plot_campaign_areas (
    id uuid PRIMARY KEY,
    plot_id uuid NOT NULL REFERENCES plots(id) ON DELETE RESTRICT,
    campaign_id uuid NOT NULL REFERENCES campaigns(id) ON DELETE RESTRICT,
    area_hectares numeric(12,4) NOT NULL CHECK (area_hectares > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE UNIQUE INDEX plot_campaign_areas_unique_idx ON plot_campaign_areas (plot_id, campaign_id);

CREATE TABLE labor_types (
    id uuid PRIMARY KEY,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE UNIQUE INDEX labor_types_name_ci_idx ON labor_types (lower(name));

-- stage is the crop-cycle timing, independent from the labor type performed.
CREATE TABLE recipes (
    id uuid PRIMARY KEY,
    plot_id uuid NOT NULL REFERENCES plots(id) ON DELETE RESTRICT,
    campaign_id uuid NOT NULL REFERENCES campaigns(id) ON DELETE RESTRICT,
    labor_type_id uuid NOT NULL REFERENCES labor_types(id) ON DELETE RESTRICT,
    stage text NOT NULL CHECK (stage IN ('presiembra', 'preemergencia', 'postemergencia')),
    target_use text NOT NULL CHECK (length(btrim(target_use)) > 0),
    responsible_engineer text NOT NULL CHECK (length(btrim(responsible_engineer)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE INDEX recipes_plot_id_idx ON recipes(plot_id);

-- Fictitious/free-text product identity until a real product catalog is authorized.
CREATE TABLE recipe_lines (
    id uuid PRIMARY KEY,
    recipe_id uuid NOT NULL REFERENCES recipes(id) ON DELETE RESTRICT,
    product_name text NOT NULL CHECK (length(btrim(product_name)) > 0),
    dose_value numeric(12,4) NOT NULL CHECK (dose_value > 0),
    dose_unit text NOT NULL CHECK (length(btrim(dose_unit)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE INDEX recipe_lines_recipe_id_idx ON recipe_lines(recipe_id);
