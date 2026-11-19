# Prioritized implementation backlog (after this bootstrap)

## P0 — Get first 4 devices working

- [ ] Install .NET 10 SDK on a build machine, restore and compile API + tests (not possible in generating environment).
- [ ] Start PostgreSQL 18+Docker Compose. Verify guest auth / practice endpoint.
- [ ] Open Unity project in 6.3 LTS, compile and generate boot scene.
- [ ] Verify four devices can play a 13-trick round via hotspot/router. Debug Android INTERNET permission and hotspot isolation.
- [ ] Confirm business-rule variants for Hakem rotation, kôt/sweep points and timeouts; freeze ruleset.
- [ ] Add focused card legality tests: void suit, trump winner, partner behavior and every turn order.

## P1 — Closed beta on the server

- [ ] Replace polling Unity online UI client with robust WebSocket transport + reconnect.
- [ ] Durable match state and event log (idempotency / process restart).
- [ ] Valkey match placement, reconnect lease, matchmaking and worker sharding.
- [ ] Guest-to-phone upgrade with Iran SMS provider, account recovery, bot penalty audit.
- [ ] Matchmaking and ranked Solo/Duo parity, season ledger & reputational sanctions.
- [ ] Instrument OpenTelemetry/Prometheus/Grafana; production PostgreSQL backup and restore drills.
- [ ] 1,000 concurrent room synthetic-load evaluation, not theoretical estimates.

## P2 — Full Hokmnameh experience

- [ ] Unity visual design system and Persian UX, animation and graphical card assets.
- [ ] Clubs, chat filtering, approved emotes, reputation moderation and appeals.
- [ ] Economy ledger for free-to-play currency, anti-inflation dashboards, cosmetics.
- [ ] Sponsor skins, impression tracking and per-competition sponsorship packages.
- [ ] One-time puzzle shards, forgotten kings hall and broken crown narrative.
- [ ] Stronger bots with card-memory tracking and Information Set Monte Carlo Tree Search.
- [ ] Seasonal analytics and skill evaluation with partner-adjusted metrics.
- [ ] Hokm Arena alternate ruleset in second development phase.
