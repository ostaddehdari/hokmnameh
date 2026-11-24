import fs from 'node:fs';

const contract = JSON.parse(fs.readFileSync(new URL('./product_domains_v1.json', import.meta.url)));
const rooms = fs.readFileSync(new URL('../backend/src/Hokmnameh.Api/Rooms.cs', import.meta.url), 'utf8');
const failures = [];
const expect = (value, message) => { if (!value) failures.push(message); };
const byId = new Map((contract.mainGrounds ?? []).map(x => [x.id, x]));
const required = ['academy', 'free', 'ranked', 'sponsored_tournament'];

expect(contract.status === 'approved', 'contract must be approved');
expect(contract.decisionId === 'DEC-2026-0005', 'unexpected decision ID');
expect(contract.classicRuleset === 'iranian-hokm-classic-v1', 'Classic ruleset must be immutable v1');
expect(byId.size === required.length && required.every(id => byId.has(id)), 'exactly four canonical main grounds are required');
for (const id of required) {
  const ground = byId.get(id);
  expect(ground.permission?.authenticated === true, `${id}: authenticated permission is required`);
  expect(ground.conditions?.entryCoin === 0, `${id}: entry must not consume coins`);
  expect(ground.inScope?.length > 0 && ground.outOfScope?.length > 0, `${id}: scope boundaries are required`);
}
for (const id of ['free', 'ranked']) {
  expect(byId.get(id).conditions.playLimit === null, `${id}: play must be unlimited`);
  expect(contract.fairCompetition.freeEntryModes.includes(id), `${id}: missing from free-entry policy`);
  expect(contract.fairCompetition.unlimitedModes.includes(id), `${id}: missing from unlimited policy`);
}
expect(byId.get('academy').conditions.coinAwarded === false, 'academy must not mint coins');
expect(byId.get('ranked').conditions.affectsMmr === true, 'ranked must affect MMR');
expect(contract.fairCompetition.vipCompetitivePrivilege === false, 'VIP competitive privilege must be forbidden');
expect(contract.fairCompetition.paidCompetitiveAdvantage === false, 'paid competitive advantage must be forbidden');
expect(contract.fairCompetition.buyableMmrOrStars === false, 'buyable MMR/stars must be forbidden');
expect(contract.arena?.isMainGround === false && contract.arena?.phase === 2, 'Arena must remain outside phase-one main grounds');
expect(contract.arena?.rulesetId !== contract.classicRuleset && contract.arena?.classicCompatibility === 'forbidden', 'Arena needs a separate ruleset');
for (const capability of ['trump-veto', 'suit-break', 'turn-transfer', 'trump-delegation']) expect(contract.arena.capabilities.includes(capability), `Arena capability missing: ${capability}`);
expect(rooms.includes('mode != "free" && mode != "practice"'), 'runtime foundation must remain limited to free/practice');
expect(!rooms.includes('entryCoin') && !rooms.includes('EntryCoin'), 'room runtime must not introduce paid entry');

if (failures.length) { console.error(failures.join('\n')); process.exit(1); }
console.log(JSON.stringify({work:'S00-W03',grounds:[...byId.keys()],arena:contract.arena.status,freeEntry:contract.fairCompetition.freeEntryModes,status:'PASS'}));
