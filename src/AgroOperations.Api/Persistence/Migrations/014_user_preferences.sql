-- Additive per-user preferences. Runs after 001 and 003-013 in one transaction.
-- Every statement is safe to rerun and keeps existing rows. Nothing is seeded: a missing row reads as the defaults.

-- Preferences of a logged-in identity, keyed by its email in lower case. Today the only identity is the temporary local
-- admin; future invitation-based accounts log in with their email and reuse this table unchanged, so it deliberately has
-- no foreign key to an accounts table. One row per identity; the theme is the only preference for now.
CREATE TABLE IF NOT EXISTS user_preferences (
    identity_email text PRIMARY KEY CHECK (length(btrim(identity_email)) > 0 AND identity_email = lower(btrim(identity_email))),
    theme text NOT NULL DEFAULT 'light' CHECK (theme IN ('light', 'dark')),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0),
    updated_at timestamptz NOT NULL DEFAULT now()
);
