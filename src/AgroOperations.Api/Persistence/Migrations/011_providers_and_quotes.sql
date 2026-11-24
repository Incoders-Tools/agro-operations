-- Additive providers and quote comparisons: input suppliers and service providers (laboratories, virtual platforms),
-- their contacts, the clients that buy from them, reference prices per input and comparisons of the quotes clients
-- receive. Runs after 001 and 003-010 in one transaction. Every statement is safe to rerun and keeps existing rows.

-- One provider entity with a kind; contractors stay their own table because work orders reference them.
-- tax_id is the CUIT stored as its 11 digits; website is a single address without spaces, as for contractors.
CREATE TABLE IF NOT EXISTS providers (
    id uuid PRIMARY KEY,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    kind text NOT NULL CHECK (kind IN ('insumos', 'laboratorio_suelos', 'laboratorio_semillas', 'plataforma_virtual', 'otro')),
    tax_id text CHECK (tax_id ~ '^[0-9]{11}$'),
    website text CHECK (website ~ '^\S{1,200}$'),
    phone text CHECK (length(btrim(phone)) > 0),
    email text CHECK (email ~ '^[^@]+@[^@]+$'),
    notes text CHECK (length(btrim(notes)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE UNIQUE INDEX IF NOT EXISTS providers_name_ci_idx ON providers (lower(name));
CREATE INDEX IF NOT EXISTS providers_kind_idx ON providers (kind);

-- Sales and technical representatives of a provider; a contact never moves to another provider.
CREATE TABLE IF NOT EXISTS provider_contacts (
    id uuid PRIMARY KEY,
    provider_id uuid NOT NULL REFERENCES providers(id) ON DELETE RESTRICT,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    role text NOT NULL CHECK (role IN ('ventas', 'tecnico', 'otro')),
    phone text CHECK (length(btrim(phone)) > 0),
    email text CHECK (email ~ '^[^@]+@[^@]+$'),
    notes text CHECK (length(btrim(notes)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE INDEX IF NOT EXISTS provider_contacts_provider_id_idx ON provider_contacts (provider_id);

-- A client buys from a provider; the API deletes a link row, never a client or a provider.
CREATE TABLE IF NOT EXISTS client_providers (
    id uuid PRIMARY KEY,
    client_id uuid NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    provider_id uuid NOT NULL REFERENCES providers(id) ON DELETE RESTRICT,
    UNIQUE (client_id, provider_id)
);
CREATE INDEX IF NOT EXISTS client_providers_provider_id_idx ON client_providers (provider_id);

-- A reference price of an input at a provider, valid on a date; unit defaults to the input's unit in the API.
CREATE TABLE IF NOT EXISTS provider_prices (
    id uuid PRIMARY KEY,
    provider_id uuid NOT NULL REFERENCES providers(id) ON DELETE RESTRICT,
    input_id uuid NOT NULL REFERENCES inputs(id) ON DELETE RESTRICT,
    price numeric(14,4) NOT NULL CHECK (price > 0),
    currency text NOT NULL CHECK (currency IN ('ARS', 'USD')),
    unit text NOT NULL CHECK (length(btrim(unit)) > 0),
    valid_on date NOT NULL,
    notes text CHECK (length(btrim(notes)) > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE INDEX IF NOT EXISTS provider_prices_provider_id_idx ON provider_prices (provider_id);
CREATE INDEX IF NOT EXISTS provider_prices_input_id_idx ON provider_prices (input_id);

-- A client's comparison of quotes. Deciding stores the chosen quote and the reason; reopening keeps both until the
-- next decision, so only a decidida comparison is required to have them.
CREATE TABLE IF NOT EXISTS quote_comparisons (
    id uuid PRIMARY KEY,
    client_id uuid NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    title text NOT NULL CHECK (length(btrim(title)) > 0),
    requested_on date NOT NULL,
    status text NOT NULL DEFAULT 'abierta' CHECK (status IN ('abierta', 'decidida')),
    chosen_quote_id uuid,
    decision_reason text CHECK (length(btrim(decision_reason)) > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0),
    CHECK ((chosen_quote_id IS NULL) = (decision_reason IS NULL)),
    CHECK (status = 'abierta' OR chosen_quote_id IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS quote_comparisons_client_id_idx ON quote_comparisons (client_id);

-- A quote a provider sent for a comparison; its total is computed by the API from the lines.
CREATE TABLE IF NOT EXISTS quotes (
    id uuid PRIMARY KEY,
    comparison_id uuid NOT NULL REFERENCES quote_comparisons(id) ON DELETE RESTRICT,
    provider_id uuid NOT NULL REFERENCES providers(id) ON DELETE RESTRICT,
    received_on date NOT NULL,
    currency text NOT NULL CHECK (currency IN ('ARS', 'USD')),
    notes text CHECK (length(btrim(notes)) > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0),
    UNIQUE (comparison_id, id)
);
CREATE INDEX IF NOT EXISTS quotes_provider_id_idx ON quotes (provider_id);

-- The chosen quote must be a quote of the same comparison.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conname = 'quote_comparisons_chosen_quote_fkey' AND connamespace = current_schema()::regnamespace) THEN
        ALTER TABLE quote_comparisons ADD CONSTRAINT quote_comparisons_chosen_quote_fkey
            FOREIGN KEY (id, chosen_quote_id) REFERENCES quotes (comparison_id, id) ON DELETE RESTRICT;
    END IF;
END $$;

-- A line names an input of the catalog or a free product text (or both); unit_price may be zero for a bonus product.
CREATE TABLE IF NOT EXISTS quote_lines (
    id uuid PRIMARY KEY,
    quote_id uuid NOT NULL REFERENCES quotes(id) ON DELETE RESTRICT,
    position smallint NOT NULL CHECK (position > 0),
    input_id uuid REFERENCES inputs(id) ON DELETE RESTRICT,
    product_text text CHECK (length(btrim(product_text)) > 0),
    quantity numeric(14,4) NOT NULL CHECK (quantity > 0),
    unit text NOT NULL CHECK (length(btrim(unit)) > 0),
    unit_price numeric(14,4) NOT NULL CHECK (unit_price >= 0),
    UNIQUE (quote_id, position),
    CHECK (input_id IS NOT NULL OR product_text IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS quote_lines_input_id_idx ON quote_lines (input_id);
