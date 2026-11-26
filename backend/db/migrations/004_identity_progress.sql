-- S03: forward-only identity and progress foundation; no OTP codes or raw access tokens at rest.
ALTER TABLE accounts ADD COLUMN phone_e164 TEXT;
ALTER TABLE accounts ADD CONSTRAINT chk_accounts_phone_e164
  CHECK (phone_e164 IS NULL OR phone_e164 ~ '^\+[1-9][0-9]{7,14}$');
ALTER TABLE accounts ADD COLUMN phone_verified_at TIMESTAMPTZ;
CREATE UNIQUE INDEX uq_accounts_verified_phone ON accounts(phone_e164) WHERE phone_verified_at IS NOT NULL;

ALTER TABLE auth_sessions ADD COLUMN device_id UUID;
ALTER TABLE auth_sessions ADD COLUMN last_seen_at TIMESTAMPTZ;
ALTER TABLE auth_sessions ADD COLUMN revoked_reason TEXT;
CREATE INDEX idx_auth_sessions_device ON auth_sessions(account_id,device_id) WHERE revoked_at IS NULL;

CREATE TABLE account_consents_v1 (
  account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
  policy_id TEXT NOT NULL,
  policy_version TEXT NOT NULL,
  accepted_at TIMESTAMPTZ NOT NULL,
  withdrawn_at TIMESTAMPTZ,
  PRIMARY KEY(account_id,policy_id,policy_version)
);

CREATE TABLE phone_challenges_v1 (
  id UUID PRIMARY KEY,
  account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
  phone_e164 TEXT NOT NULL CHECK (phone_e164 ~ '^\+[1-9][0-9]{7,14}$'),
  purpose TEXT NOT NULL CHECK (purpose IN ('upgrade','login')),
  code_mac BYTEA NOT NULL CHECK (octet_length(code_mac)=32),
  attempts SMALLINT NOT NULL DEFAULT 0 CHECK (attempts BETWEEN 0 AND 5),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  expires_at TIMESTAMPTZ NOT NULL,
  consumed_at TIMESTAMPTZ,
  invalidated_at TIMESTAMPTZ
);
CREATE INDEX idx_phone_challenges_account ON phone_challenges_v1(account_id,created_at DESC);
CREATE INDEX idx_phone_challenges_phone ON phone_challenges_v1(phone_e164,created_at DESC);

-- XP is independent of nickname, rank, currency and reputation. Only an audited round closure is eligible.
CREATE TABLE xp_awards_v1 (
  match_id UUID NOT NULL,
  event_sequence BIGINT NOT NULL,
  account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
  amount INTEGER NOT NULL CHECK(amount BETWEEN 1 AND 1000),
  awarded_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY(match_id,event_sequence,account_id),
  FOREIGN KEY(match_id,event_sequence) REFERENCES match_events_v1(match_id,sequence)
);
CREATE INDEX idx_xp_awards_account ON xp_awards_v1(account_id,awarded_at DESC);

CREATE FUNCTION validate_xp_award_v1() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM match_events_v1 e JOIN match_participants p ON p.match_id=e.match_id
    WHERE e.match_id=NEW.match_id AND e.sequence=NEW.event_sequence
      AND e.kind='round-closed' AND p.account_id=NEW.account_id
  ) THEN
    RAISE EXCEPTION 'XP award requires an audited round closure and match participant'
      USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER xp_award_source_guard_v1 BEFORE INSERT ON xp_awards_v1
  FOR EACH ROW EXECUTE FUNCTION validate_xp_award_v1();

CREATE TABLE player_profiles_v1 (
  account_id UUID PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE,
  avatar_id TEXT,
  version BIGINT NOT NULL DEFAULT 1 CHECK(version>0)
);
CREATE TABLE player_reputation_v1 (
  account_id UUID PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE,
  score INTEGER NOT NULL DEFAULT 0,
  version BIGINT NOT NULL DEFAULT 1 CHECK(version>0)
);
CREATE TABLE player_league_v1 (
  account_id UUID PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE,
  league_id TEXT,
  version BIGINT NOT NULL DEFAULT 1 CHECK(version>0)
);
