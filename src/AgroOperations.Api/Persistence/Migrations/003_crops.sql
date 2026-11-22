-- Additive catalog for manually reviewed crop names; seasonal source strings are not validated crops.
CREATE TABLE crops (
    id uuid PRIMARY KEY,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE UNIQUE INDEX crops_name_ci_idx ON crops (lower(name));
