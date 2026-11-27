-- S03: policy versions are append-only; an admin panel can publish a new version later.
CREATE TABLE xp_policy_versions_v1 (
  version INTEGER PRIMARY KEY CHECK (version > 0),
  active_from TIMESTAMPTZ NOT NULL UNIQUE,
  daily_total_cap INTEGER NOT NULL CHECK (daily_total_cap > 0),
  timezone TEXT NOT NULL CHECK (timezone = 'Asia/Tehran'),
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE xp_policy_modes_v1 (
  version INTEGER NOT NULL REFERENCES xp_policy_versions_v1(version),
  game_mode TEXT NOT NULL CHECK (game_mode IN ('practice','free','ranked')),
  base_xp INTEGER NOT NULL CHECK (base_xp >= 0),
  win_xp INTEGER NOT NULL CHECK (win_xp >= 0),
  daily_mode_cap INTEGER NOT NULL CHECK (daily_mode_cap > 0),
  enabled BOOLEAN NOT NULL DEFAULT false,
  PRIMARY KEY(version,game_mode)
);
CREATE TABLE level_thresholds_v1 (
  version INTEGER NOT NULL REFERENCES xp_policy_versions_v1(version),
  level INTEGER NOT NULL CHECK (level > 0),
  cumulative_xp BIGINT NOT NULL CHECK (cumulative_xp >= 0),
  PRIMARY KEY(version,level),
  UNIQUE(version,cumulative_xp)
);
INSERT INTO xp_policy_versions_v1(version,active_from,daily_total_cap,timezone)
  VALUES (1,'2026-01-01 00:00:00+00',300,'Asia/Tehran');
INSERT INTO xp_policy_modes_v1(version,game_mode,base_xp,win_xp,daily_mode_cap,enabled) VALUES
  (1,'practice',8,4,120,true), (1,'free',12,6,240,true),
  (1,'ranked',16,8,240,false);
INSERT INTO level_thresholds_v1(version,level,cumulative_xp) VALUES
  (1,1,0),(1,2,100),(1,3,250),(1,4,450),(1,5,700),(1,6,1000);
INSERT INTO level_thresholds_v1(version,level,cumulative_xp)
  SELECT 1,l,1000 + 350 * (l-6) + 50 * (l-6) * (l-7) / 2
  FROM generate_series(7,100) AS l;
-- History remains attributed to its policy and the Tehran calendar date of the event.
ALTER TABLE xp_awards_v1 ADD COLUMN policy_version INTEGER REFERENCES xp_policy_versions_v1(version);
ALTER TABLE xp_awards_v1 ADD COLUMN policy_day DATE;
CREATE INDEX idx_xp_awards_daily_v1 ON xp_awards_v1(account_id,policy_day,policy_version);
CREATE TABLE round_results_v1 (
  match_id UUID NOT NULL,
  event_sequence BIGINT NOT NULL,
  winning_team SMALLINT NOT NULL CHECK (winning_team IN (0,1)),
  eligible_seats SMALLINT NOT NULL CHECK (eligible_seats BETWEEN 0 AND 15),
  PRIMARY KEY(match_id,event_sequence),
  FOREIGN KEY(match_id,event_sequence) REFERENCES match_events_v1(match_id,sequence)
);

-- Identity and cosmetics share one optimistic profile version.
INSERT INTO player_profiles_v1(account_id,avatar_id)
  SELECT account_id,'avatar-01' FROM profiles ON CONFLICT(account_id) DO NOTHING;
ALTER TABLE player_profiles_v1 ALTER COLUMN avatar_id SET DEFAULT 'avatar-01';
ALTER TABLE player_profiles_v1 ADD CONSTRAINT chk_player_avatar_v1
  CHECK (avatar_id ~ '^avatar-0[1-8]$');
