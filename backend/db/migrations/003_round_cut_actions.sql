-- Forward-only S02-W05: one accepted cut (human or fallback) per match/round.
CREATE TABLE round_cut_actions_v1 (
  match_id UUID NOT NULL REFERENCES recorded_matches(id) ON DELETE CASCADE,
  round_number BIGINT NOT NULL CHECK (round_number > 0),
  action_id UUID NOT NULL,
  kind TEXT NOT NULL CHECK (kind IN ('cut-selected', 'cut-fallback')),
  actor_seat SMALLINT NOT NULL CHECK (actor_seat BETWEEN -1 AND 3),
  cut_index SMALLINT NOT NULL CHECK (cut_index BETWEEN 1 AND 51),
  state_version BIGINT NOT NULL CHECK (state_version > 0),
  happened_at TIMESTAMPTZ NOT NULL,
  PRIMARY KEY (match_id, round_number),
  UNIQUE (match_id, action_id),
  FOREIGN KEY (match_id, state_version) REFERENCES match_events_v1(match_id, sequence)
);

-- Preserve existing accepted cuts before the uniqueness gate becomes authoritative.
INSERT INTO round_cut_actions_v1(match_id,round_number,action_id,kind,actor_seat,cut_index,state_version,happened_at)
SELECT e.match_id, 1 + (
  SELECT count(*) FROM match_events_v1 previous
  WHERE previous.match_id=e.match_id AND previous.kind='round-started' AND previous.sequence<e.sequence
), e.action_id,e.kind,e.actor_seat,e.public_value,e.sequence,e.happened_at
FROM match_events_v1 e WHERE e.kind IN ('cut-selected','cut-fallback');
