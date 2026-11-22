import fs from 'node:fs';

const proposal = JSON.parse(fs.readFileSync(new URL('../docs/proposal.json', import.meta.url)));
const matrix = JSON.parse(fs.readFileSync(new URL('./v01-gap-matrix.json', import.meta.url)));
const required = [...proposal.requirements, ...proposal.nonfunctional_requirements].map(x=>x.id).sort();
const actual = matrix.entries.map(x=>x.id).sort();
const allowed = new Set(matrix.allowed_statuses);
const unique = new Set(actual);

const failures = [];
if (JSON.stringify(required) !== JSON.stringify(actual)) failures.push('matrix IDs do not exactly cover proposal requirements');
if (unique.size !== actual.length) failures.push('duplicate requirement IDs');
for (const e of matrix.entries) {
  if (!allowed.has(e.status)) failures.push(`${e.id}: invalid status`);
  if (!Array.isArray(e.code_paths) || e.code_paths.length === 0) failures.push(`${e.id}: missing code_paths`);
  if (!e.evidence) failures.push(`${e.id}: missing evidence`);
  if (!Array.isArray(e.gaps) || e.gaps.length === 0) failures.push(`${e.id}: missing gaps`);
  if (!Array.isArray(e.dependencies) || e.dependencies.length === 0) failures.push(`${e.id}: missing dependencies`);
  for (const p of e.code_paths) if (!fs.existsSync(new URL('../'+p, import.meta.url))) failures.push(`${e.id}: path not found: ${p}`);
}
if (failures.length) {
  console.error(failures.join('\n'));
  process.exit(1);
}
console.log(JSON.stringify({task:matrix.task_id,requirements:actual.length,unique:unique.size,summary:matrix.summary,status:'PASS'}));
