-- Hokmnameh MVP, intentionally SMALL. Extend with docs/extended_schema_74_tables_REFERENCE_ONLY.sql
-- only via individually reviewed migrations, never run the entire reference blindly.
BEGIN;
CREATE TABLE IF NOT EXISTS accounts (
  id UUID PRIMARY KEY,
  status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active','restricted','banned','deleted')),
  is_guest BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS profiles (
  account_id UUID PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE,
  nickname TEXT NOT NULL,
  nickname_normalized TEXT NOT NULL UNIQUE,
  level INTEGER NOT NULL DEFAULT 1 CHECK (level>=1),
  xp BIGINT NOT NULL DEFAULT 0 CHECK (xp>=0),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS auth_sessions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
  token_hash BYTEA NOT NULL UNIQUE,
  expires_at TIMESTAMPTZ NOT NULL,
  revoked_at TIMESTAMPTZ,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_auth_sessions_active ON auth_sessions(account_id,expires_at)
  WHERE revoked_at IS NULL;
CREATE TABLE IF NOT EXISTS recorded_matches (
  id UUID PRIMARY KEY,
  ruleset_version TEXT NOT NULL,
  game_mode TEXT NOT NULL,
  started_at TIMESTAMPTZ NOT NULL,
  ended_at TIMESTAMPTZ,
  winning_team SMALLINT CHECK (winning_team IN (0,1)),
  scoreboard JSONB NOT NULL DEFAULT '{}'::jsonb,
  audit_checksum TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS match_participants (
  match_id UUID REFERENCES recorded_matches(id) ON DELETE CASCADE,
  account_id UUID REFERENCES accounts(id),
  seat SMALLINT NOT NULL CHECK (seat BETWEEN 0 AND 3),
  PRIMARY KEY (match_id, seat)
);
COMMIT;
