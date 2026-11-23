import fs from 'node:fs';

const rules = JSON.parse(fs.readFileSync(new URL('./ruleset_hokm_classic_v1.json', import.meta.url)));
const golden = JSON.parse(fs.readFileSync(new URL('./ruleset_hokm_classic_v1.golden.json', import.meta.url)));
const failures = [];
const eq = (actual, expected, label) => { if (actual !== expected) failures.push(`${label}: expected ${expected}, got ${actual}`); };

eq(rules.immutableId, golden.ruleset, 'immutable ID');
eq(rules.status, 'approved', 'status');
eq(rules.deal.beforeTrumpSelect, 5, 'preliminary cards');
eq(rules.trick.tricksNeededToWinRound, 7, 'round target');
eq(rules.match.targetPoints, 7, 'match target');

for (const item of golden.scoring) {
  const expected = item.losingTeamTricks !== 0 ? rules.roundScoring.normal
    : item.winningTeam === item.hakemTeam ? rules.roundScoring.hakemTeamSweep
    : rules.roundScoring.nonHakemTeamSweep;
  eq(expected, item.expectedPoints, `scoring/${item.id}`);
}
for (const item of golden.hakemRotation) {
  const expected = item.currentHakem % 2 === item.winningTeam ? item.currentHakem : (item.currentHakem + 1) % 4;
  eq(expected, item.expectedHakem, `hakem/${item.id}`);
}
for (const item of golden.termination) {
  if ('roundEnds' in item) eq(item.winnerTricks >= rules.trick.tricksNeededToWinRound, item.roundEnds, `termination/${item.id}`);
  if ('finished' in item) eq(Math.max(...item.points) >= rules.match.targetPoints, item.finished, `termination/${item.id}`);
}

if (failures.length) { console.error(failures.join('\n')); process.exit(1); }
console.log(JSON.stringify({task:'S00-W02-T01',ruleset:rules.immutableId,scoring:golden.scoring.length,hakemRotation:golden.hakemRotation.length,termination:golden.termination.length,status:'PASS'}));
