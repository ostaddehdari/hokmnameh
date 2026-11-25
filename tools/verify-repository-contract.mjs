import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const root=path.resolve(new URL('..',import.meta.url).pathname);
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const json=p=>JSON.parse(read(p));
const repo=json('repository.contract.json'), versions=json('version-contract.json');
const codeowners=read('.github/CODEOWNERS'), packageJson=json('unity/Packages/com.hokmnameh.core/package.json');
const rules=json('docs/ruleset_hokm_classic_v1.json'), e04=json('docs/e04_decision_set_v1.json');
const program=read('backend/src/Hokmnameh.Api/Program.cs');
const failures=[]; const expect=(x,m)=>{if(!x)failures.push(m);};

expect(repo.layout==='monorepo','layout must be monorepo');
const ids=new Set();
for(const m of repo.modules??[]){
  expect(!ids.has(m.id),`duplicate module ${m.id}`);ids.add(m.id);
  expect(fs.existsSync(path.join(root,m.path)),`${m.id}: missing ${m.path}`);
  expect(codeowners.includes(`/${m.path}/`)||codeowners.includes(`/${m.path.split('/')[0]}/`),`${m.id}: CODEOWNERS missing path`);
  expect(Boolean(m.owner),`${m.id}: owner required`);
}
for(const p of repo.rootContracts??[])expect(fs.existsSync(path.join(root,p)),`missing root contract ${p}`);

expect(packageJson.version===versions.sharedCore.version,'shared-core version mismatch');
expect(packageJson.name===versions.sharedCore.package,'shared-core package mismatch');
expect(rules.immutableId===versions.rules.classic,'Classic ruleset version mismatch');
expect(e04.contractId===versions.rules.e04,'E-04 contract version mismatch');
expect(program.includes(versions.api.routePrefix),'API major prefix not found in Program.cs');
expect(versions.api.breakingChange.includes('new-major'),'API breaking policy missing');
expect(versions.database.breakingChange.includes('forward-only'),'database forward-only policy missing');

const tracked=execFileSync('git',['ls-files','-z'],{cwd:root}).toString().split('\0').filter(Boolean);
const secret=/^(\.env|.*\.(pem|pfx)|id_rsa.*)$/i;
for(const p of tracked){
  const base=path.basename(p);
  if(secret.test(base))failures.push(`sensitive file tracked: ${p}`);
  if(p.includes('/bin/')||p.includes('/obj/')||p.startsWith('unity/Library/'))failures.push(`generated file tracked: ${p}`);
}
const migrationArtifacts=tracked.filter(p=>p.startsWith('backend/db/migrations/'));
const migrations=migrationArtifacts.filter(p=>p.endsWith('.sql'));
const migrationRe=new RegExp(versions.database.migrationPattern);
for(const p of migrations)expect(migrationRe.test(path.basename(p)),`invalid migration name: ${p}`);
for(const p of migrationArtifacts.filter(p=>!p.endsWith('.sql')))
  expect(p==='backend/db/migrations/manifest.json',`unexpected migration artifact: ${p}`);

if(failures.length){console.error(failures.join('\n'));process.exit(1);}
console.log(JSON.stringify({work:'S01-W01',modules:ids.size,tracked:tracked.length,migrations:migrations.length,repositoryVersion:versions.repositoryVersion,status:'PASS'}));
