import fs from 'node:fs';
import path from 'node:path';

const here = new URL('.', import.meta.url);
const config = JSON.parse(fs.readFileSync(new URL('./config.json', here)));
const registry = JSON.parse(fs.readFileSync(new URL('./registry.json', here)));
const proposal = JSON.parse(fs.readFileSync(new URL('../../docs/proposal.json', here)));
const requirementIds = new Set([...proposal.requirements, ...proposal.nonfunctional_requirements].map(x=>x.id));
const eligible = new Set(config.merge_eligible_statuses);
const failures = [];
const ids = new Set();
const decisions = new Set();

function covered(file, entry) {
  return entry.affected_paths.some(prefix => file === prefix || file.startsWith(prefix));
}
function validateEntry(e) {
  if (!new RegExp(config.id_pattern).test(e.id)) failures.push(`${e.id}: invalid id`);
  if (!new RegExp(config.decision_id_pattern).test(e.decision_id)) failures.push(`${e.id}: invalid decision_id`);
  if (ids.has(e.id)) failures.push(`${e.id}: duplicate id`); ids.add(e.id);
  if (decisions.has(e.decision_id)) failures.push(`${e.id}: duplicate decision_id`); decisions.add(e.decision_id);
  for (const field of ['title','requester','created_at','rollback_plan']) if (!e[field]) failures.push(`${e.id}: missing ${field}`);
  for (const field of ['category','source_refs','affected_paths','requirement_ids','risk_flags','approvals','acceptance_criteria','test_plan']) if (!Array.isArray(e[field])) failures.push(`${e.id}: ${field} must be array`);
  for (const r of e.requirement_ids ?? []) if (!requirementIds.has(r)) failures.push(`${e.id}: unknown requirement ${r}`);
  if ((e.affected_paths ?? []).some(p => !p || p==='/' || p==='.' || p==='backend/' || p==='unity/')) failures.push(`${e.id}: overly broad affected path`);
  if (eligible.has(e.status)) {
    const approvedRoles = new Set((e.approvals ?? []).filter(a=>a.decision==='APPROVE' && a.evidence).map(a=>a.role));
    for (const category of e.category ?? []) for (const role of config.required_approvals[category] ?? []) if (!approvedRoles.has(role)) failures.push(`${e.id}: missing approval ${role} for ${category}`);
    if ((e.approvals ?? []).some(a=>a.decision==='REJECT')) failures.push(`${e.id}: eligible status conflicts with rejection`);
  }
  if ((e.risk_flags ?? []).some(x=>config.high_risk_flags.includes(x)) && !e.security_review) failures.push(`${e.id}: high-risk change needs security_review`);
  if ((e.category ?? []).includes('database') && !e.migration_plan) failures.push(`${e.id}: database change needs migration_plan`);
  if (e.emergency?.enabled && (!e.emergency.reason || !e.emergency.post_review_due_at)) failures.push(`${e.id}: incomplete emergency record`);
}

for (const e of registry.entries) validateEntry(e);

const args = process.argv.slice(2);
const changedIndex = args.indexOf('--changed-files');
if (changedIndex >= 0) {
  const filename = args[changedIndex+1];
  if (!filename) failures.push('--changed-files requires a filename');
  else {
    const changed = fs.readFileSync(path.resolve(filename),'utf8').split(/\r?\n/).map(x=>x.trim()).filter(Boolean);
    const protectedPrefixes = Object.values(config.protected_domains).flat();
    for (const file of changed) {
      if (config.exempt_prefixes.some(p=>file.startsWith(p))) continue;
      if (!protectedPrefixes.some(p=>file.startsWith(p) || file===p)) continue;
      const matches = registry.entries.filter(e=>eligible.has(e.status) && covered(file,e));
      if (matches.length===0) failures.push(`${file}: protected change has no eligible covering CR`);
    }
  }
}

if (args.includes('--self-test')) {
  const base = registry.entries[0];
  if (!covered('0000/change-control/config.json',base)) failures.push('self-test: approved prefix should cover governance config');
  if (covered('backend/src/Hokmnameh.Api/Program.cs',base)) failures.push('self-test: governance CR must not cover API runtime');
  if (!Object.values(config.protected_domains).flat().some(p=>'backend/src/Hokmnameh.Api/Program.cs'.startsWith(p))) failures.push('self-test: API runtime must be protected');
}

if (failures.length) { console.error(failures.join('\n')); process.exit(1); }
console.log(JSON.stringify({task:config.task_id,entries:registry.entries.length,requirements:requirementIds.size,status:'PASS'}));
