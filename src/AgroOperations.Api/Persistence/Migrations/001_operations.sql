-- Apply transactionally with the deployment migration runner before enabling persistence.
CREATE TABLE clients (
    id uuid PRIMARY KEY,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE TABLE fields (
    id uuid PRIMARY KEY,
    client_id uuid NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE INDEX fields_client_id_idx ON fields(client_id);
CREATE TABLE plots (
    id uuid PRIMARY KEY,
    field_id uuid NOT NULL REFERENCES fields(id) ON DELETE RESTRICT,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    area_hectares numeric(12,4) NOT NULL CHECK (area_hectares > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE INDEX plots_field_id_idx ON plots(field_id);
-- Updates must compare the expected revision atomically and increment it:
-- UPDATE ... SET ..., revision = revision + 1 WHERE id = @id AND revision = @expected
-- A zero-row update must distinguish a missing row from a stale revision.
