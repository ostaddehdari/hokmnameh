import fs from 'node:fs';
import crypto from 'node:crypto';

const decision = JSON.parse(fs.readFileSync(new URL('./e04_decision_set_v1.json', import.meta.url)));
const vectorPath = new URL('./e04_test_vector_v1.json', import.meta.url);
const vector = fs.existsSync(vectorPath) ? JSON.parse(fs.readFileSync(vectorPath)) : null;
const failures = [];
const expect = (value, message) => { if (!value) failures.push(message); };
const u16be = n => { const b=Buffer.alloc(2); b.writeUInt16BE(n); return b; };
const u64be = n => { const b=Buffer.alloc(8); b.writeBigUInt64BE(BigInt(n)); return b; };
const utf8 = s => Buffer.from(s, 'utf8');
const lp = s => Buffer.concat([u16be(utf8(s).length), utf8(s)]);
const uuidBytes = s => Buffer.from(s.replaceAll('-', ''), 'hex');

function context(v) {
  return Buffer.concat([
    utf8('HOKM-E04-CONTEXT'), Buffer.from([0,1]),
    uuidBytes(v.matchId), u64be(v.roundNumber), lp(v.rulesetId), lp(v.shuffleAlgorithmId)
  ]);
}
function derive(v) {
  const ctx=context(v), seed=Buffer.from(v.seedHex,'hex'), nonce=Buffer.from(v.nonceHex,'hex');
  const salt=crypto.createHash('sha256').update(utf8('hokmnameh/e04/hkdf-salt/v1')).digest();
  const key=Buffer.from(crypto.hkdfSync('sha256',seed,salt,Buffer.concat([utf8('hokmnameh/e04/shuffle-key/v1'),Buffer.from([0]),ctx]),32));
  const commitment=crypto.createHash('sha256').update(Buffer.concat([utf8('hokmnameh/e04/commit/v1'),Buffer.from([0]),ctx,nonce,seed])).digest();
  let counter=1n, words=[];
  const refill=()=>{const mac=crypto.createHmac('sha256',key).update(Buffer.concat([utf8('hokmnameh/e04/stream/v1'),Buffer.from([0]),ctx,u64be(counter++)])).digest();for(let i=0;i<32;i+=4)words.push(mac.readUInt32BE(i));};
  const uniform=n=>{const limit=Math.floor(0x100000000/n)*n;while(true){if(!words.length)refill();const x=words.shift();if(x<limit)return x%n;}};
  const deck=Array.from({length:52},(_,i)=>i);for(let i=51;i>0;i--){const j=uniform(i+1);[deck[i],deck[j]]=[deck[j],deck[i]];}
  const cut=v.cutIndex, cutDeck=[...deck.slice(cut),...deck.slice(0,cut)];
  const cutMac=crypto.createHmac('sha256',key).update(Buffer.concat([utf8('hokmnameh/e04/cut-fallback/v1'),Buffer.from([0]),ctx])).digest();
  let fallback=null; for(let i=0;i<32&&fallback===null;i+=4){const x=cutMac.readUInt32BE(i),limit=Math.floor(0x100000000/51)*51;if(x<limit)fallback=(x%51)+1;}
  if(fallback===null) throw new Error('test vector unexpectedly exhausted cut fallback block');
  return {contextHex:ctx.toString('hex'),hkdfSaltHex:salt.toString('hex'),shuffleKeyHex:key.toString('hex'),commitmentHex:commitment.toString('hex'),shuffledDeck:deck,cutDeck,first12AfterCut:cutDeck.slice(0,12),fallbackCutIndex:fallback};
}

expect(decision.status==='approved' && decision.decisionId==='DEC-2026-0006','decision must be approved');
expect(decision.deal.firstReceiver==='hakem' && decision.deal.seatDelta===-1,'Hakem/counter-clockwise deal required');
expect(JSON.stringify(decision.deal.stagesPerSeat)==='[5,4,4]' && JSON.stringify(decision.deal.cursorAfterStage)==='[20,36,52]','5+4+4 cursors invalid');
expect(decision.cutter.seatRule==='(hakemSeat + 3) % 4','cutter rule invalid');
expect(decision.cut.minimumInclusive===1 && decision.cut.maximumInclusive===51 && decision.cut.deadlineMilliseconds===8000,'cut policy invalid');
expect(decision.hokmSelection.deadlineMilliseconds===20000,'hokm deadline invalid');
expect(decision.cryptography.shuffleAlgorithmId==='E04-HKDF-SHA256-HMACCTR-RS-FY-v1','algorithm ID invalid');
expect(decision.cryptography.canonicalContext.integers==='unsigned-big-endian','endianness invalid');
expect(decision.revealAndRetention.revealPreconditions.includes('roundState=ROUND_CLOSED'),'early reveal must be forbidden');
expect(decision.revealAndRetention.encryptedSecretRetentionDays===30 && decision.revealAndRetention.nonSecretAuditHashRetentionDays===365,'retention invalid');

if (process.argv.includes('--print-vector')) {
  const input={matchId:'00112233-4455-6677-8899-aabbccddeeff',roundNumber:7,rulesetId:'iranian-hokm-classic-v1',shuffleAlgorithmId:decision.cryptography.shuffleAlgorithmId,seedHex:'000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f',nonceHex:'f0e0d0c0b0a090807060504030201000',cutIndex:17};
  console.log(JSON.stringify({schemaVersion:'1.0',vectorId:'E04-TV-001',contractId:decision.contractId,input,expected:derive(input)},null,2));
  process.exit(0);
}
expect(vector?.vectorId==='E04-TV-001' && vector?.contractId===decision.contractId,'official vector missing');
if(vector){
  const actual=derive(vector.input);for(const [k,v] of Object.entries(vector.expected))expect(JSON.stringify(actual[k])===JSON.stringify(v),`vector mismatch: ${k}`);
  const changedSeed={...vector.input,seedHex:`01${vector.input.seedHex.slice(2)}`}, changedCut={...vector.input,cutIndex:18};
  const seedResult=derive(changedSeed), cutResult=derive(changedCut);
  expect(seedResult.commitmentHex!==actual.commitmentHex && JSON.stringify(seedResult.shuffledDeck)!==JSON.stringify(actual.shuffledDeck),'one-byte seed change must alter commitment and deck');
  expect(JSON.stringify(cutResult.cutDeck)!==JSON.stringify(actual.cutDeck),'cut index change must alter cut deck');
}
if (failures.length) { console.error(failures.join('\n')); process.exit(1); }
console.log(JSON.stringify({work:'S00-W04',decision:decision.decisionId,algorithm:decision.cryptography.shuffleAlgorithmId,vector:vector.vectorId,status:'PASS'}));
