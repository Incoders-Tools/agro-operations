-- Additive company staff and the work order's responsible staff member. Runs after 001 and 003-012 in one transaction.
-- Every statement is safe to rerun and keeps existing rows. No staff is seeded: real people are entered privately.

-- Company personnel, separate from login accounts so a future account can link to a staff member. Staff is never
-- deleted, only deactivated; email is a single @ with text on both sides, as for contractors.
CREATE TABLE IF NOT EXISTS staff (
    id uuid PRIMARY KEY,
    full_name text NOT NULL CHECK (length(btrim(full_name)) > 0 AND length(full_name) <= 200),
    title text CHECK (length(btrim(title)) > 0 AND length(title) <= 100),
    role text NOT NULL CHECK (role IN ('ingeniero', 'administrativo', 'otro')),
    license_number text CHECK (length(btrim(license_number)) > 0 AND length(license_number) <= 50),
    email text CHECK (email ~ '^[^@]+@[^@]+$'),
    phone text CHECK (length(btrim(phone)) > 0),
    active boolean NOT NULL DEFAULT true,
    notes text CHECK (length(btrim(notes)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS staff_full_name_ci_idx ON staff (lower(full_name));

-- The responsible engineer picked from staff. responsible_engineer keeps the free text of older orders and, when a staff
-- member is set, a snapshot of the name at the time of the write, so issued prints keep it.
ALTER TABLE work_orders
    ADD COLUMN IF NOT EXISTS responsible_staff_id uuid
        CONSTRAINT work_orders_responsible_staff_id_fkey REFERENCES staff(id) ON DELETE RESTRICT;
CREATE INDEX IF NOT EXISTS work_orders_responsible_staff_id_idx ON work_orders (responsible_staff_id);
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conrelid = 'work_orders'::regclass AND conname = 'work_orders_responsible_staff_snapshot_check') THEN
        ALTER TABLE work_orders ADD CONSTRAINT work_orders_responsible_staff_snapshot_check
            CHECK (responsible_staff_id IS NULL OR responsible_engineer IS NOT NULL);
    END IF;
END $$;
