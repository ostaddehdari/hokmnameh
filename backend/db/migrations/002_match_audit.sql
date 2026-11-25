-- S02 versioned durable event stream and encrypted E-04 round proof custody.
-- migrate.sh owns the transaction and checksummed ledger; never edit this after apply.
ALTER TABLE recorded_matches ADD COLUMN audit_sequence BIGINT NOT NULL DEFAULT 0 CHECK (audit_sequence >= 0);

CREATE TABLE match_events_v1 (
  match_id UUID NOT NULL REFERENCES recorded_matches(id) ON DELETE CASCADE,
  sequence BIGINT NOT NULL CHECK (sequence > 0),
  action_id UUID NOT NULL,
  actor_seat SMALLINT NOT NULL CHECK (actor_seat BETWEEN -1 AND 3),
  ruleset_version TEXT NOT NULL,
  happened_at TIMESTAMPTZ NOT NULL,
  state_version BIGINT NOT NULL CHECK (state_version = sequence),
  kind TEXT NOT NULL,
  public_value INTEGER,
  PRIMARY KEY (match_id, sequence),
  UNIQUE (match_id, action_id)
);
CREATE INDEX idx_match_events_action ON match_events_v1(action_id);

CREATE TABLE round_proofs_v1 (
  match_id UUID NOT NULL REFERENCES recorded_matches(id) ON DELETE CASCADE,
  round_number BIGINT NOT NULL CHECK (round_number > 0),
  shuffle_algorithm_id TEXT NOT NULL,
  commitment_hex CHAR(64) NOT NULL,
  cut_index SMALLINT NOT NULL CHECK (cut_index BETWEEN 1 AND 51),
  encryption_nonce BYTEA NOT NULL CHECK (octet_length(encryption_nonce) = 12),
  ciphertext BYTEA NOT NULL CHECK (octet_length(ciphertext) = 48),
  auth_tag BYTEA NOT NULL CHECK (octet_length(auth_tag) = 16),
  expires_at TIMESTAMPTZ NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (match_id, round_number)
);
CREATE INDEX idx_round_proofs_expiry ON round_proofs_v1(expires_at);
