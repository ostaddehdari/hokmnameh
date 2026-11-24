import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const read = (path) => readFileSync(path, 'utf8');
const staticOnly = process.argv.includes('--static-only');
const contract = JSON.parse(read('deploy/security-contract.json'));

assert.equal(contract.schema_version, '1.0');
assert.deepEqual(contract.task_ids, ['S01-W02-T01', 'S01-W02-T02', 'S01-W02-T03']);
assert.equal(contract.public_route.base_path, '/hokmnameh');
assert.equal(contract.public_route.upstream, 'http://127.0.0.1:5080');
assert.equal(contract.public_route.preserve_base_path, true);
assert.deepEqual(contract.containers.postgres.published_ports, []);
assert.deepEqual(contract.containers.valkey.published_ports, []);
assert.equal(contract.containers.valkey.future_requirement, 'internal-network-only');
assert.equal(contract.host.deploy_user, 'hokmdeploy');
assert.equal(contract.host.root_login, 'forbidden');
assert.equal(contract.host.ssh_authentication, 'public-key-only');
assert.equal(contract.host.docker_engine, 'rootless-required');
assert.equal(contract.secrets.mode, '0600');
assert.equal(contract.rollback.explicit_confirmation, 'CONFIRM_ROLLBACK=YES');

const composeSource = read('docker-compose.yml');
assert.match(composeSource, /user: "10001:10001"/);
assert.match(composeSource, /ports: \["127\.0\.0\.1:5080:8080"\]/);
assert.match(composeSource, /cap_drop: \["ALL"\]/);
assert.match(composeSource, /read_only: true/);
assert.match(composeSource, /networks:\n  internal:\n    internal: true/);

if (!staticOnly) {
  const composeResult = spawnSync('docker', ['compose', 'config', '--format', 'json'], {
    encoding: 'utf8',
    env: { ...process.env, POSTGRES_PASSWORD: 'contract-validation-only' }
  });
  assert.equal(composeResult.status, 0, composeResult.stderr || 'docker compose config failed');
  const compose = JSON.parse(composeResult.stdout);
  const api = compose.services.api;
  const postgres = compose.services.postgres;
  assert.equal(api.user, '10001:10001');
  assert.equal(api.read_only, true);
  assert.ok(api.cap_drop.includes('ALL'));
  assert.ok(api.security_opt.includes('no-new-privileges:true'));
  assert.equal(api.ports.length, 1);
  assert.equal(api.ports[0].host_ip, '127.0.0.1');
  assert.equal(Number(api.ports[0].published), 5080);
  assert.equal(Number(api.ports[0].target), 8080);
  assert.ok(!('ports' in postgres) || postgres.ports.length === 0, 'PostgreSQL must not publish a host port');
  assert.equal(compose.networks.internal.internal, true);
}

const dockerfile = read('backend/Dockerfile');
assert.match(dockerfile, /^USER 10001:10001$/m);
assert.doesNotMatch(dockerfile, /^USER (root|0)(:0)?$/m);

const nginx = read('deploy/nginx/hokmnameh-location.conf');
assert.match(nginx, /location = \/hokmnameh/);
assert.match(nginx, /location \^~ \/hokmnameh\//);
assert.match(nginx, /proxy_pass http:\/\/127\.0\.0\.1:5080;/);
assert.doesNotMatch(nginx.replace(/^\s*#.*$/gm, ''), /\bserver\s*\{/);

const preflight = read('deploy/scripts/preflight-host.sh');
assert.match(preflight, /Refusing deployment as root/);
assert.match(preflight, /Deployment must run as hokmdeploy/);
assert.match(preflight, /rootless/);
assert.match(preflight, /\.env mode must be 0600/);
assert.match(preflight, /\.env owner must be hokmdeploy/);

const deploy = read('deploy/scripts/deploy.sh');
assert.match(deploy, /CONFIRM_PRODUCTION_DEPLOY/);
assert.match(deploy, /\.\/deploy\/scripts\/backup\.sh/);
assert.match(deploy, /\.\/deploy\/scripts\/preflight-host\.sh/);
assert.match(deploy, /\.deploy\/rollback-image/);

const rollback = read('deploy/scripts/rollback.sh');
assert.match(rollback, /CONFIRM_ROLLBACK/);
assert.match(rollback, /--no-build --pull never api/);
assert.match(rollback, /\.\/deploy\/scripts\/check\.sh/);
assert.match(rollback, /restoring the previously running image/);
assert.match(rollback, /No database migration was reversed/);

const ssh = read('deploy/ssh/sshd_config.hokmnameh.example');
assert.match(ssh, /^PermitRootLogin no$/m);
assert.match(ssh, /^PasswordAuthentication no$/m);
assert.match(ssh, /^PubkeyAuthentication yes$/m);
assert.match(ssh, /^AuthenticationMethods publickey$/m);

for (const unit of ['deploy/systemd/hokmnameh-health.service']) {
  const systemd = read(unit);
  assert.match(systemd, /^User=hokmdeploy$/m);
  assert.match(systemd, /^Group=hokmdeploy$/m);
  assert.doesNotMatch(systemd, /^User=root$/m);
}

const trackedSecrets = spawnSync('git', ['ls-files'], { encoding: 'utf8' });
assert.equal(trackedSecrets.status, 0, trackedSecrets.stderr);
for (const path of trackedSecrets.stdout.trim().split('\n').filter(Boolean)) {
  assert.ok(!/(^|\/)(\.env|id_rsa|.*\.(pem|pfx|key))$/.test(path), `Tracked secret-like file: ${path}`);
}

console.log('VPS security contract verified.');
