import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync } from 'node:fs';

const read = path => readFileSync(path, 'utf8');
const json = path => JSON.parse(read(path));
const sha256 = value => createHash('sha256').update(value).digest('hex');

const manifest = json('backend/db/migrations/manifest.json');
const contract = json('deploy/operations-contract.json');
assert.equal(manifest.schema_version, '1.0');
assert.equal(manifest.runner_contract, 'hokmnameh-forward-only-v1');
assert.deepEqual(contract.task_ids, ['S01-W03-T01', 'S01-W03-T02', 'S01-W03-T03']);

const sqlFiles = readdirSync('backend/db/migrations').filter(name => name.endsWith('.sql')).sort();
assert.equal(sqlFiles.length, manifest.migrations.length, 'manifest must cover every SQL migration');
const seen = new Set();
for (let index = 0; index < manifest.migrations.length; index++) {
  const entry = manifest.migrations[index];
  const expectedPrefix = String(index + 1).padStart(3, '0');
  assert.match(entry.version, /^[0-9]{3}_[a-z0-9_]+$/);
  assert.equal(entry.file, `${entry.version}.sql`);
  assert.equal(entry.version.slice(0, 3), expectedPrefix, 'migration versions must be contiguous');
  assert.ok(!seen.has(entry.version), `duplicate migration version ${entry.version}`);
  seen.add(entry.version);
  assert.equal(sqlFiles[index], entry.file, 'manifest and filesystem order differ');
  const sql = read(`backend/db/migrations/${entry.file}`);
  assert.equal(sha256(sql), entry.sha256, `checksum mismatch for ${entry.file}`);
  assert.doesNotMatch(sql, /\b(DROP\s+DATABASE|CREATE\s+DATABASE|\\connect)\b/i);
  assert.doesNotMatch(sql, /^\s*(BEGIN|COMMIT)\s*;/im, 'runner owns the transaction');
}

assert.equal(contract.migrations.immutable_after_apply, true);
assert.equal(contract.migrations.database_recovery, 'forward-only');
assert.equal(contract.backup_restore.plaintext_dump_on_disk, false);
assert.equal(contract.backup_restore.restore_network, 'none');
assert.equal(contract.backup_restore.duration_recorded, true);
assert.equal(contract.environments.development.data_class, 'synthetic-only');
assert.notEqual(contract.environments.staging.credentials, contract.environments.production.credentials);
assert.notEqual(contract.environments.staging.database, contract.environments.production.database);

for (const [name, expectedLimit, expectedWindow] of [
  ['guest_identity', 10, 300],
  ['authenticated_commands', 120, 60],
  ['websocket_connect', 20, 60]
]) {
  const policy = contract.rate_limits[name];
  assert.equal(policy.permit_limit, expectedLimit);
  assert.equal(policy.window_seconds, expectedWindow);
  assert.equal(policy.queue_limit, 0);
}
assert.equal(contract.rate_limits.rejection_status, 429);
assert.equal(contract.rate_limits.retry_after_header, true);
assert.equal(contract.health.ready_dependency, 'postgresql');
assert.equal(contract.alerts.delivery_failure_is_nonzero, true);

const program = read('backend/src/Hokmnameh.Api/Program.cs');
for (const policy of ['auth-guest', 'authenticated-command', 'websocket-connect']) {
  assert.ok(program.includes(`"${policy}"`), `API is missing rate policy ${policy}`);
}
assert.match(program, /StatusCodes\.Status429TooManyRequests/);
assert.match(program, /MetadataName\.RetryAfter/);
assert.doesNotMatch(program, /Request\.Headers\.Authorization/, 'unverified bearer values must not create limiter partitions');
assert.match(program, /\/health\/live/);
assert.match(program, /\/health\/ready/);

const migrate = read('deploy/scripts/migrate.sh');
assert.match(migrate, /pg_advisory_xact_lock/);
assert.match(migrate, /Migration checksum mismatch/);
assert.match(migrate, /Migration sequence gap/);
const backup = read('deploy/scripts/backup.sh');
assert.match(backup, /--format=custom/);
assert.match(backup, /age -r/);
assert.match(backup, /metadata\.json/);
const restore = read('deploy/scripts/restore-drill.sh');
assert.match(restore, /--network none/);
assert.match(restore, /sha256sum -c/);
assert.match(restore, /duration_seconds/);
assert.match(restore, /schema_migrations/);
const monitor = read('deploy/scripts/watch-health.sh');
assert.match(monitor, /api-liveness/);
assert.match(monitor, /database-readiness/);
assert.match(monitor, /HOKM_ALERT_WEBHOOK_URL/);
assert.doesNotMatch(monitor, /echo .*HOKM_ALERT_WEBHOOK_URL/);

const workflow = read('.github/workflows/backend.yml');
for (const evidence of ['verify-s01-operations.mjs', 'Restore encrypted backup into a separate PostgreSQL', 'Verify runtime rate limit', 'test-health-monitor.mjs']) {
  assert.ok(workflow.includes(evidence), `CI evidence missing: ${evidence}`);
}

console.log(JSON.stringify({work: 'S01-W03', migrations: manifest.migrations.length, ratePolicies: 3, status: 'PASS'}));
