# S01 monitoring baseline

- Liveness `/hokmnameh/health/live` indicates the API process is serving requests.
- Readiness `/hokmnameh/health/ready` checks the configured PostgreSQL connection.
- `deploy/scripts/watch-health.sh` exits nonzero and logs a standard marker to journald. Optional systemd timer in `deploy/systemd/` runs every minute.
- ASP.NET guest registration includes a per-IP prototype limiter; distributed account/device throttling is S12. Proxies must have trusted X-Forwarded-* settings.
- Avoid logging access tokens, raw connection strings, hidden cards or seeds. For S04/S12 add OpenTelemetry, Prometheus/Grafana, alert routing, latency histograms, queue/match capacity and incident escalation.
- Service alerts require an **actual external receiver and a failure drill**; a timer alone is not an alert delivery system.
