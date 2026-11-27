-- The four profile domains have independent versions and distinct write authority.
ALTER TABLE profiles ADD COLUMN nickname_version BIGINT NOT NULL DEFAULT 1
  CHECK(nickname_version > 0);
INSERT INTO player_profiles_v1(account_id,avatar_id)
  SELECT account_id,'avatar-01' FROM profiles ON CONFLICT(account_id) DO NOTHING;
UPDATE player_profiles_v1 SET avatar_id='avatar-01' WHERE avatar_id IS NULL;
ALTER TABLE player_profiles_v1 ALTER COLUMN avatar_id SET NOT NULL;
INSERT INTO player_reputation_v1(account_id)
  SELECT account_id FROM profiles ON CONFLICT(account_id) DO NOTHING;
INSERT INTO player_league_v1(account_id)
  SELECT account_id FROM profiles ON CONFLICT(account_id) DO NOTHING;
-- Do not tie these versions to XP/Level, or make client mutations to reputation/league.
