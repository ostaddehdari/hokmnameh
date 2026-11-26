// CI-only isolated PostgreSQL/API drill: no credentials, private hands or proof bytes are printed.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { createDecipheriv, createHash, randomUUID } from 'node:crypto';

const base = 'http://127.0.0.1:5080/hokmnameh';
async function request(path, token, method = 'GET', body) {
  for (let attempt = 0; attempt < 3; attempt++) {
    const response = await fetch(base + path, {
      method,
      headers: { ...(token ? { authorization: `Bearer ${token}` } : {}),
        ...(body ? { 'content-type': 'application/json' } : {}) },
      body: body ? JSON.stringify(body) : undefined
    });
    if (response.status === 429 && attempt < 2) {
      const header = response.headers.get('retry-after');
      const seconds = header == null ? 61 : Number(header);
      assert.ok(Number.isFinite(seconds) && seconds >= 1 && seconds <= 300,
        'rate limiter supplied an invalid Retry-After');
      await new Promise(resolve => setTimeout(resolve, seconds * 1000 + 250));
      continue; // The middleware rejected this request before invoking a game command.
    }
    assert.ok(response.ok, `${method} ${path} returned HTTP ${response.status}`);
    return response.json();
  }
  throw new Error('Unreachable request retry state');
}
const identities = [];
for (let i = 0; i < 4; i++) identities.push(await request('/api/v1/identity/guest', null, 'POST'));
const tokens = identities.map(x => x.accessToken);
const cutCreated = await request('/api/v2/rooms', tokens[0], 'POST', { mode: 'free' });
const cutRoom = cutCreated.room;
for (let seat = 1; seat < 4; seat++)
  await request(`/api/v1/rooms/join/${cutRoom.code}`, tokens[seat], 'POST');
const cutPath = `/api/v2/rooms/${cutRoom.id}/cut`;
const cutPending = await request(`/api/v1/rooms/${cutRoom.id}`, tokens[3]);
assert.equal(cutPending.room.cutPhase, 'CUT_PENDING');
assert.equal(cutPending.cutStateVersion, 0);
assert.equal(cutPending.isYourCut, true);
assert.equal(cutPending.game, null);
const cutCommand = { cutIndex: 17, actionId: randomUUID(), stateVersion: cutPending.cutStateVersion };
async function expectCutError(token, body, status, code) {
  const response = await fetch(base + cutPath, { method: 'POST',
    headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
    body: JSON.stringify(body) });
  assert.equal(response.status, status);
  assert.match(response.headers.get('content-type') ?? '', /application\/problem\+json/);
  assert.equal((await response.json()).code, code);
}
await expectCutError(tokens[0], cutCommand, 403, 'wrong_cutter');
await expectCutError(tokens[3], { ...cutCommand, stateVersion: 999 }, 409, 'stale_cut_state');
const concurrent = await Promise.all([0, 1].map(() => fetch(base + cutPath, {
  method: 'POST', headers: { authorization: `Bearer ${tokens[3]}`, 'content-type': 'application/json' },
  body: JSON.stringify(cutCommand)
})));
assert.deepEqual(concurrent.map(x => x.status).sort(), [200, 409]);
const acceptedCut = await concurrent.find(x => x.ok).json();
assert.equal(acceptedCut.game.cutIndex, 17);
assert.equal(acceptedCut.game.phase, 1);
assert.equal(acceptedCut.game.dealCursor, 20);
await expectCutError(tokens[3], cutCommand, 409, 'duplicate_cut_action');
const otherCutState = await request(`/api/v1/rooms/${cutRoom.id}`, tokens[1]);
assert.equal(otherCutState.game.hand.length, 5);
assert.equal(JSON.stringify(otherCutState).toLowerCase().includes('seed'), false);
const created = await request('/api/v1/rooms', tokens[0], 'POST', { mode: 'free' });
const room = created.room;
assert.match(room.id, /^[0-9a-f-]{36}$/i);
for (let seat = 1; seat < 4; seat++) {
  const joined = await request(`/api/v1/rooms/join/${room.code}`, tokens[seat], 'POST');
  assert.equal(joined.seat, seat);
}
let state = await request(`/api/v1/rooms/${room.id}`, tokens[0]);
assert.equal(state.game.phase, 1);
assert.equal(state.game.hand.length, 5);
assert.equal(state.game.shuffleCommitment.length, 64);
const replay = (matchId = room.id) => {
  const cmd = spawnSync('docker', ['compose', 'exec', '-T', 'api',
    'dotnet', 'Hokmnameh.Api.dll', '--verify-replay', matchId], { encoding: 'utf8' });
  assert.equal(cmd.status, 0, 'isolated authority replay failed');
  return JSON.parse(cmd.stdout.trim());
};
let recovered = replay();
assert.equal(recovered.phase, state.game.phase);
assert.equal(recovered.commitment, state.game.shuffleCommitment);
state = await request(`/api/v1/rooms/${room.id}/trump`, tokens[0], 'POST',
  { suit: Math.floor(state.game.hand[0] / 13) });
let moves = 0;
while (state.game.phase === 2 && moves < 52) {
  const seat = state.game.turn;
  const player = await request(`/api/v1/rooms/${room.id}`, tokens[seat]);
  const hand = player.game.hand;
  const led = player.game.ledSuit;
  const legal = led < 0 ? hand : hand.filter(card => Math.floor(card / 13) === led);
  state = await request(`/api/v1/rooms/${room.id}/play`, tokens[seat], 'POST',
    { card: (legal.length ? legal : hand)[0] });
  moves++;
}
assert.ok(moves >= 28 && (state.game.phase === 0 || state.game.phase === 3));
// The ticker may start round 2 immediately after the round-closed response.
// Compare two fresh reads of the same authority, retrying only the boundary race.
let matchingLiveState = false;
for (let attempt = 0; attempt < 4; attempt++) {
  recovered = replay();
  const live = (await request(`/api/v1/rooms/${room.id}`, tokens[0])).game;
  matchingLiveState = recovered.phase === live.phase && recovered.round === live.round &&
    recovered.commitment === live.shuffleCommitment &&
    JSON.stringify(recovered.score) === JSON.stringify(live.matchPoints);
  if (matchingLiveState) break;
}
assert.ok(matchingLiveState, 'replay and live room must converge at a stable boundary');
assert.equal(recovered.closedRounds, 1);

const db = sql => {
  const cmd = spawnSync('docker', ['compose', 'exec', '-T', 'postgres', 'sh', '-c',
    'exec psql -X -Atq -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "$1"', 'sh', sql],
  { encoding: 'utf8' });
  assert.equal(cmd.status, 0, 'isolated PostgreSQL query failed');
  return cmd.stdout.trim();
};
assert.equal(db(`SELECT count(*) FROM match_participants WHERE match_id='${cutRoom.id}'`), '4');
assert.equal(db(`SELECT count(*) FROM match_participants WHERE match_id='${room.id}'`), '4');
const xpMatch = randomUUID();
db(`INSERT INTO recorded_matches(id,ruleset_version,game_mode,started_at)
 VALUES ('${xpMatch}','iranian-hokm-classic-v1','practice',now());
 INSERT INTO match_participants(match_id,account_id,seat) VALUES ('${xpMatch}','${identities[0].accountId}',0);
 INSERT INTO match_events_v1(match_id,sequence,action_id,actor_seat,ruleset_version,happened_at,state_version,kind,public_value)
 VALUES ('${xpMatch}',1,'${randomUUID()}',-1,'iranian-hokm-classic-v1',now(),1,'round-closed',1);
 INSERT INTO xp_awards_v1(match_id,event_sequence,account_id,amount)
 VALUES ('${xpMatch}',1,'${identities[0].accountId}',10) ON CONFLICT DO NOTHING;
 INSERT INTO xp_awards_v1(match_id,event_sequence,account_id,amount)
 VALUES ('${xpMatch}',1,'${identities[0].accountId}',10) ON CONFLICT DO NOTHING;`);
assert.equal(db(`SELECT count(*) FROM xp_awards_v1 WHERE match_id='${xpMatch}'`), '1');
const rejectedAward = spawnSync('docker', ['compose', 'exec', '-T', 'postgres', 'sh', '-c',
  'exec psql -X -v ON_ERROR_STOP=1 -Atq -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "$1"',
  'sh', `INSERT INTO xp_awards_v1(match_id,event_sequence,account_id,amount)
    VALUES ('${xpMatch}',1,'${identities[1].accountId}',10)`], { encoding: 'utf8' });
assert.notEqual(rejectedAward.status, 0, 'nonparticipant must never receive XP');
assert.equal(db(`SELECT count(*) FROM round_cut_actions_v1 WHERE match_id='${cutRoom.id}'`), '1');
assert.equal(db(`SELECT count(*) FROM round_proofs_v1 WHERE match_id='${cutRoom.id}'`), '1');
assert.equal(db(`SELECT string_agg(kind || ':' || coalesce(public_value::text,''),',' ORDER BY sequence)
 FROM match_events_v1 WHERE match_id='${cutRoom.id}'`),
 'cut-selected:17,round-started:1,deal-initial:20');
assert.equal(replay(cutRoom.id).phase, acceptedCut.game.phase);
const stagedRoom = await request(`/api/v1/rooms/${cutRoom.id}/trump`, tokens[0], 'POST', { suit: 2 });
assert.equal(stagedRoom.game.dealCursor, 52);
assert.equal(stagedRoom.game.hand.length, 13);
assert.equal(db(`SELECT string_agg(kind || ':' || coalesce(public_value::text,''),',' ORDER BY sequence)
 FROM match_events_v1 WHERE match_id='${cutRoom.id}'`),
 'cut-selected:17,round-started:1,deal-initial:20,trump-staged:2,deal-second:36,deal-final:52');
assert.equal(replay(cutRoom.id).phase, stagedRoom.game.phase);
const id = room.id;
const rows = db(`SELECT r.audit_sequence,(SELECT count(*) FROM match_events_v1 e WHERE e.match_id=r.id),
  (SELECT count(*) FROM match_events_v1 e WHERE e.match_id=r.id AND e.kind='round-closed'),
  encode(p.encryption_nonce,'hex'),encode(p.ciphertext,'hex'),encode(p.auth_tag,'hex'),
  p.commitment_hex,p.cut_index
  FROM recorded_matches r JOIN round_proofs_v1 p ON p.match_id=r.id AND p.round_number=1
  WHERE r.id='${id}'`).split('|');
assert.equal(rows.length, 8);
assert.equal(Number(rows[0]), Number(rows[1]));
assert.equal(Number(rows[2]), 1);
assert.equal(rows[6].trim(), state.game.shuffleCommitment);
assert.ok(Number(rows[7]) >= 1 && Number(rows[7]) <= 51);
const keyText = readFileSync('.env', 'utf8').split('\n').find(x => x.startsWith('HOKM_AUDIT_KEY_BASE64='));
assert.ok(keyText);
const key = Buffer.from(keyText.split('=').slice(1).join('='), 'base64');
assert.equal(key.length, 32);
const cipher = Buffer.from(rows[4], 'hex');
const decipher = createDecipheriv('aes-256-gcm', key, Buffer.from(rows[3], 'hex'));
decipher.setAAD(Buffer.from(`HOKM-E04-PROOF-v1\0${id.replaceAll('-', '')}:1:${rows[6].trim()}`));
decipher.setAuthTag(Buffer.from(rows[5], 'hex'));
const proof = Buffer.concat([decipher.update(cipher), decipher.final()]);
assert.equal(proof.length, 48);
assert.notDeepEqual(proof, cipher);
const u16 = n => { const b = Buffer.alloc(2); b.writeUInt16BE(n); return b; };
const u64 = n => { const b = Buffer.alloc(8); b.writeBigUInt64BE(BigInt(n)); return b; };
const ruleset = Buffer.from('iranian-hokm-classic-v1');
const algorithm = Buffer.from('E04-HKDF-SHA256-HMACCTR-RS-FY-v1');
const context = Buffer.concat([Buffer.from('HOKM-E04-CONTEXT\0'), Buffer.from([1]),
  Buffer.from(id.replaceAll('-', ''), 'hex'), u64(1), u16(ruleset.length), ruleset,
  u16(algorithm.length), algorithm]);
const recomputed = createHash('sha256').update(Buffer.concat([
  Buffer.from('hokmnameh/e04/commit/v1\0'), context, proof.subarray(32), proof.subarray(0, 32)
])).digest('hex');
assert.equal(recomputed, rows[6].trim());
proof.fill(0); key.fill(0);
// Isolate identity checks from the many game commands within the same IP rate window.
const restart = spawnSync('docker', ['compose', 'restart', 'api'], { encoding: 'utf8' });
assert.equal(restart.status, 0, 'isolated API restart failed');
let ready = false;
for (let attempt = 0; attempt < 30; attempt++) {
  try { ready = (await fetch(base + '/health/ready')).ok; } catch { /* container starting */ }
  if (ready) break;
  await new Promise(resolve => setTimeout(resolve, 200));
}
assert.ok(ready, 'API did not recover after isolated restart');
const initialProgress = await request('/api/v1/profile/progress', tokens[0]);
assert.equal(initialProgress.accountId, identities[0].accountId);
assert.equal(initialProgress.xp, 0);
assert.equal(initialProgress.level, 1);
const temporary = await request('/api/v1/identity/guest', null, 'POST');
const signedOut = await fetch(base + '/api/v1/identity/logout', {
  method: 'POST', headers: { authorization: `Bearer ${temporary.accessToken}` }
});
assert.equal(signedOut.status, 204);
const revoked = await fetch(base + '/api/v1/identity/me', {
  headers: { authorization: `Bearer ${temporary.accessToken}` }
});
assert.equal(revoked.status, 401);
assert.equal(db(`SELECT revoked_reason FROM auth_sessions WHERE account_id='${temporary.accountId}'`), 'user-logout');
console.log(JSON.stringify({ work: 'S02-W03/S02-W04', eventCount: Number(rows[1]),
  moves, encryptedProof: true, closedRoundVerified: true, replayVerified: true, status: 'PASS' }));
