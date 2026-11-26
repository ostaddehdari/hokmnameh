// CI-only isolated PostgreSQL/API drill: no credentials, private hands or proof bytes are printed.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { createDecipheriv, createHash } from 'node:crypto';

const base = 'http://127.0.0.1:5080/hokmnameh';
async function request(path, token, method = 'GET', body) {
  const response = await fetch(base + path, {
    method,
    headers: { ...(token ? { authorization: `Bearer ${token}` } : {}),
      ...(body ? { 'content-type': 'application/json' } : {}) },
    body: body ? JSON.stringify(body) : undefined
  });
  assert.ok(response.ok, `${method} ${path} returned HTTP ${response.status}`);
  return response.json();
}
const identities = [];
for (let i = 0; i < 4; i++) identities.push(await request('/api/v1/identity/guest', null, 'POST'));
const tokens = identities.map(x => x.accessToken);
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
const replay = () => {
  const cmd = spawnSync('docker', ['compose', 'exec', '-T', 'api',
    'dotnet', 'Hokmnameh.Api.dll', '--verify-replay', room.id], { encoding: 'utf8' });
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
recovered = replay();
assert.equal(recovered.phase, state.game.phase);
assert.equal(recovered.round, state.game.round);
assert.deepEqual(recovered.score, state.game.matchPoints);
assert.equal(recovered.commitment, state.game.shuffleCommitment);
assert.equal(recovered.closedRounds, 1);

const db = sql => {
  const cmd = spawnSync('docker', ['compose', 'exec', '-T', 'postgres', 'sh', '-c',
    'exec psql -X -Atq -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "$1"', 'sh', sql],
  { encoding: 'utf8' });
  assert.equal(cmd.status, 0, 'isolated PostgreSQL query failed');
  return cmd.stdout.trim();
};
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
console.log(JSON.stringify({ work: 'S02-W01/S02-W02', eventCount: Number(rows[1]),
  moves, encryptedProof: true, closedRoundVerified: true, replayVerified: true, status: 'PASS' }));
