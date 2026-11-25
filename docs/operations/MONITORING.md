# S01 monitoring baseline

- Liveness `/hokmnameh/health/live` indicates the API process is serving requests.
- Readiness `/hokmnameh/health/ready` checks the configured PostgreSQL connection.
- `deploy/scripts/watch-health.sh` checks liveness before readiness, posts a redacted JSON alert to `HOKM_ALERT_WEBHOOK_URL`, and exits nonzero both for a detected outage and for failed delivery. A local HTTP receiver is accepted only for the CI drill; operational receivers must use HTTPS.
- Put `HOKM_ALERT_WEBHOOK_URL` and `HOKM_ENVIRONMENT` in `/opt/hokmnameh/.monitoring.env` with mode `0600`. The hardened systemd timer in `deploy/systemd/` runs every minute after an authorized operator installs it.
- Rate limits are fail-fast with no queue: guest identity `10/5m`, authenticated commands `120/1m`, and WebSocket connects `20/1m`, all per trusted client IP. Unverified bearer values are deliberately not used as partitions because rotating forged tokens would bypass the limit. Rejections use HTTP 429 plus `Retry-After`.
- Nginx must remain the explicitly trusted local proxy. Distributed account/device throttling, shared counters and WAF controls remain S12 scope.
- Avoid logging access tokens, raw connection strings, hidden cards or seeds. For S04/S12 add OpenTelemetry, Prometheus/Grafana, alert routing, latency histograms, queue/match capacity and incident escalation.
- CI proves delivery to a simulated receiver during a database-readiness failure. A real external receiver, paging policy and VPS timer installation remain production gates and must not be inferred from CI.
