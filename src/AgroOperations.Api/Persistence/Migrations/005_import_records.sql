-- Additive provenance log for bounded real-data imports. A record here never
-- substitutes domain validation; it only traces an entity back to its source row
-- and marks it unverified until a human reviews it.
CREATE TABLE import_records (
    id uuid PRIMARY KEY,
    entity_type text NOT NULL CHECK (entity_type IN ('client', 'field', 'plot')),
    entity_id uuid NOT NULL,
    source_reference text NOT NULL CHECK (length(btrim(source_reference)) > 0),
    verified boolean NOT NULL DEFAULT false,
    raw_payload jsonb NOT NULL
);
CREATE UNIQUE INDEX import_records_entity_idx ON import_records (entity_type, entity_id);
