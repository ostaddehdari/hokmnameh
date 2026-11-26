// Independent Node reference fixture. Seed/nonce are public synthetic test bytes only.
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';

const path = new URL('../docs/replay_match_v1.golden.json', import.meta.url);
const fixture = JSON.parse(readFileSync(path));
const text = s => Buffer.from(s);
const label = s => Buffer.concat([text(s), Buffer.from([0])]);
const u16 = n => { const b = Buffer.alloc(2); b.writeUInt16BE(n); return b; };
const u64 = n => { const b = Buffer.alloc(8); b.writeBigUInt64BE(BigInt(n)); return b; };
const lp = s => Buffer.concat([u16(text(s).length), text(s)]);
const hash = b => crypto.createHash('sha256').update(b).digest();
const mac = (k, b) => crypto.createHmac('sha256', k).update(b).digest();
const ruleset = 'iranian-hokm-classic-v1';
const algorithm = 'E04-HKDF-SHA256-HMACCTR-RS-FY-v1';

function shuffled(matchId, round, seed, nonce, cut) {
  const ctx = Buffer.concat([label('HOKM-E04-CONTEXT'), Buffer.from([1]),
    Buffer.from(matchId.replaceAll('-', ''), 'hex'), u64(round), lp(ruleset), lp(algorithm)]);
  const salt = hash(text('hokmnameh/e04/hkdf-salt/v1'));
  const key = Buffer.from(crypto.hkdfSync('sha256', seed, salt,
    Buffer.concat([label('hokmnameh/e04/shuffle-key/v1'), ctx]), 32));
  const commitment = hash(Buffer.concat([label('hokmnameh/e04/commit/v1'), ctx, nonce, seed])).toString('hex');
  let counter = 1, words = [];
  const uniform = n => {
    const limit = Math.floor(0x100000000 / n) * n;
    for (;;) {
      if (!words.length) {
        const block = mac(key, Buffer.concat([label('hokmnameh/e04/stream/v1'), ctx, u64(counter++)]));
        words = Array.from({ length: 8 }, (_, i) => block.readUInt32BE(i * 4));
      }
      const x = words.shift();
      if (x < limit) return x % n;
    }
  };
  const deck = Array.from({ length: 52 }, (_, i) => i);
  for (let i = 51; i > 0; i--) { const j = uniform(i + 1); [deck[i], deck[j]] = [deck[j], deck[i]]; }
  key.fill(0);
  return { deck: [...deck.slice(cut), ...deck.slice(0, cut)], commitment };
}

const hands = Array.from({ length: 4 }, () => []), points = [0, 0];
let hakem = 0, round = 0, winner = -1;
const rounds = [], events = [];
while (winner < 0 && round < 12) {
  round++;
  const seed = Buffer.from(Array.from({ length: 32 }, (_, i) => (i + round) & 255));
  const nonce = Buffer.from(Array.from({ length: 16 }, (_, i) => (240 - i - round) & 255));
  const cut = round % 51 + 1;
  const { deck, commitment } = shuffled(fixture.matchId, round, seed, nonce, cut);
  hands.forEach(hand => hand.splice(0));
  for (let offset = 0; offset < 4; offset++) hands[(hakem - offset + 4) % 4].push(...deck.slice(offset * 5, offset * 5 + 5));
  rounds.push({ round, seedHex: seed.toString('hex'), nonceHex: nonce.toString('hex'), cut,
    commitment, first12: deck.slice(0, 12) });
  events.push({ kind: 'round-started', seat: -1, value: round });
  const trump = Math.floor(hands[hakem][0] / 13);
  events.push({ kind: 'trump-selected', seat: hakem, value: trump });
  for (let packet = 0; packet < 2; packet++)
    for (let offset = 0; offset < 4; offset++)
      hands[(hakem - offset + 4) % 4].push(...deck.slice(20 + packet * 16 + offset * 4, 24 + packet * 16 + offset * 4));
  const tricks = [0, 0]; let turn = hakem;
  while (Math.max(...tricks) < 7) {
    const cards = []; const leadSeat = turn;
    for (let offset = 0; offset < 4; offset++) {
      const seat = (leadSeat + offset) % 4;
      const led = cards.length ? Math.floor(cards[0].card / 13) : -1;
      const follow = hands[seat].filter(c => Math.floor(c / 13) === led);
      const card = (follow.length ? follow : hands[seat])[0];
      assert.ok(card !== undefined);
      hands[seat].splice(hands[seat].indexOf(card), 1);
      cards.push({ seat, card }); events.push({ kind: 'card-played', seat, value: card });
    }
    let best = -1;
    for (const p of cards) {
      const suit = Math.floor(p.card / 13), rank = p.card % 13 + 2;
      const power = suit === trump ? 100 + rank : suit === Math.floor(cards[0].card / 13) ? 20 + rank : 0;
      if (power > best) { best = power; turn = p.seat; }
    }
    tricks[turn % 2]++;
  }
  const team = turn % 2;
  points[team] += tricks[1 - team] ? 1 : team === hakem % 2 ? 2 : 3;
  events.push({ kind: 'round-closed', seat: -1, value: round });
  if (points[team] >= 7) winner = team;
  else if (team !== hakem % 2) hakem = (hakem + 1) % 4;
}
assert.ok(winner >= 0, 'match must terminate');
const expected = { round, points, winner, eventCount: events.length,
  transcriptSha256: hash(text(JSON.stringify(events))).toString('hex') };
if (process.argv.includes('--generate')) {
  fixture.rounds = rounds; fixture.events = events; fixture.expected = expected;
  writeFileSync(path, JSON.stringify(fixture, null, 2) + '\n');
} else {
  assert.deepEqual(fixture.rounds, rounds);
  assert.deepEqual(fixture.events, events);
  assert.deepEqual(fixture.expected, expected);
}
console.log(JSON.stringify({ work: 'S02-W03/S02-W04', rounds: round,
  events: events.length, winner, status: 'PASS' }));
