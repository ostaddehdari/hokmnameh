import assert from 'node:assert/strict';
import http from 'node:http';
import { spawn } from 'node:child_process';

const alerts = [];
const server = http.createServer((req, res) => {
  if (req.url === '/hokmnameh/health/live') {
    res.writeHead(200).end('live');
    return;
  }
  if (req.url === '/hokmnameh/health/ready') {
    res.writeHead(503).end('database unavailable');
    return;
  }
  if (req.url === '/alert' && req.method === 'POST') {
    let body = '';
    req.setEncoding('utf8');
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => {
      alerts.push(JSON.parse(body));
      res.writeHead(202).end('accepted');
    });
    return;
  }
  res.writeHead(404).end();
});

await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const { port } = server.address();
const child = spawn('bash', ['deploy/scripts/watch-health.sh'], {
  env: {
    ...process.env,
    BASE: `http://127.0.0.1:${port}/hokmnameh`,
    HOKM_ALERT_WEBHOOK_URL: `http://127.0.0.1:${port}/alert`,
    HOKM_ENVIRONMENT: 'ci'
  },
  stdio: ['ignore', 'pipe', 'pipe']
});
let stderr = '';
child.stderr.setEncoding('utf8');
child.stderr.on('data', chunk => { stderr += chunk; });
const exitCode = await new Promise(resolve => child.on('close', resolve));
await new Promise(resolve => setTimeout(resolve, 25));
server.close();

assert.equal(exitCode, 1, stderr);
assert.equal(alerts.length, 1);
assert.equal(alerts[0].service, 'hokmnameh-api');
assert.equal(alerts[0].environment, 'ci');
assert.equal(alerts[0].check, 'database-readiness');
assert.equal(alerts[0].status, 'failed');
assert.ok(!JSON.stringify(alerts[0]).includes('/alert'), 'webhook URL must not be reflected');
assert.match(stderr, /HOKMNAMEH_ALERT_DELIVERED/);
console.log('Health monitor failure delivery verified.');
