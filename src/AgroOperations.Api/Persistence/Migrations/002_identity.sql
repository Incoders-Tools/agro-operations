-- Apply through the deployment migration process before using the internal invitation service.
CREATE TABLE identity_accounts (
    id uuid PRIMARY KEY,
    email text NOT NULL UNIQUE,
    password_hash text NOT NULL,
    email_verified boolean NOT NULL DEFAULT false,
    active boolean NOT NULL DEFAULT false,
    security_stamp text NOT NULL
);
CREATE TABLE identity_invitations (
    id uuid PRIMARY KEY,
    email text NOT NULL,
    token_hash text NOT NULL UNIQUE,
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz
);
CREATE INDEX identity_invitations_email ON identity_invitations(email);
CREATE TABLE identity_password_resets (
    id uuid PRIMARY KEY,
    account_id uuid NOT NULL REFERENCES identity_accounts(id) ON DELETE RESTRICT,
    token_hash text NOT NULL UNIQUE,
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz
);
CREATE INDEX identity_password_resets_account_idx ON identity_password_resets(account_id);
