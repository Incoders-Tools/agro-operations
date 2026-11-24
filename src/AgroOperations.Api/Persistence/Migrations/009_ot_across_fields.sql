-- Additive: work orders across fields and a default commercial firm per client. Runs after 001 and 003-008 in one
-- transaction. Every statement is safe to rerun and keeps existing rows.

-- An OT covers plots of one or more fields of the firm's client, so it has no single field: its fields are those of
-- its plots. The column held no data worth keeping (no work order existed when this was written); the index goes with it.
ALTER TABLE work_orders DROP COLUMN IF EXISTS field_id;

-- Each client gets a firm named like it, as creating a client now does. A name already taken by another firm is
-- skipped: firm names are unique across clients, and such a client can receive a firm with another name.
INSERT INTO commercial_firms (id, client_id, name)
SELECT gen_random_uuid(), c.id, btrim(c.name)
FROM clients c
WHERE NOT EXISTS (SELECT 1 FROM commercial_firms f WHERE f.client_id = c.id)
ORDER BY c.name, c.id
ON CONFLICT ((lower(name))) DO NOTHING;
