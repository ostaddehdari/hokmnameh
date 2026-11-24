# حکمنامه — Hokmnameh

Monorepo for the **Hokmnameh** Android card game: Unity 6, shared C# Hokm rules, .NET 10 API, PostgreSQL 18, and zero-internet local Wi-Fi play.

## Current deliverable (engineering bootstrap, not production)

- **Working source design:** pure C# rules engine with 52-card CSPRNG shuffle, 5+8 deal, follow-suit validation, four-seat tricks, scoring, replayable state transitions and bot heuristics.
- **Backend API source:** guest accounts using PostgreSQL, private bearer sessions, game rooms, free + practice rooms, trump/play validation, per-seat snapshots and websocket state stream.
- **Unity engineering prototype:** menu, HTTP-polling online room prototype, 1 vs 3 bots, one Android phone as TCP LAN match host, three Android phones join through the same hotspot/Wi-Fi; no Internet is required for LAN.
- **Server:** Docker Compose PostgreSQL 18 + ASP.NET Core 10, Nginx reverse proxy **under `/hokmnameh`** without replacing your existing `srun.ir` website.
- **Tests:** pure C# console-based rules smoke checks, GitHub Actions. Unity device testing and production load tests remain mandatory.

## Repository layout

```
backend/src/Hokmnameh.Api/              ASP.NET API + authoritative online rooms
backend/tests/Hokmnameh.SmokeTests/     rules smoke tests
backend/db/migrations/001_core.sql      small bootstrap database
unity/Packages/com.hokmnameh.core/     SINGLE shared C# rules source
unity/Assets/Hokmnameh/Runtime/        Unity offline bots, LAN host/client and proto UI
unity/Assets/Hokmnameh/Editor/         build scene generator
./docker-compose.yml                   PostgreSQL + backend
./deploy/nginx/                        path-based reverse proxy snippet
./docs/                                decisions, APIs, phases, caveats
```

The machine-readable module map and owners live in [`repository.contract.json`](repository.contract.json). Unified .NET build entry is [`Hokmnameh.sln`](Hokmnameh.sln); compatibility rules are defined by [`version-contract.json`](version-contract.json) and [ADR-0003](docs/adr/0003-versioning-and-breaking-changes.md).

## Bootstrapping locally or on the VPS

```bash
cp .env.example .env
# edit .env: use a randomly generated long password
./deploy/scripts/deploy.sh
# local: http://127.0.0.1:5080/hokmnameh/health/live
# reverse proxy setup explained in docs/DEPLOYMENT.md
```

**Do not expose PostgreSQL or port 5080 publicly.** No `.env`, access tokens, SSH private keys or Unity paid assets belong in this repository.

## Unity

Open `unity/` with a Unity **6.3 LTS** Editor with Android Build Support. Click `Hokmnameh > Setup Build Scene`, then Play. Build an Android ARM64 APK after checking Player Settings and Android permissions. The UI is deliberately functional placeholder IMGUI; it is NOT a finished visual game.

## API quickstart

`POST /hokmnameh/api/v1/identity/guest` creates a guest bearer. Then `POST /hokmnameh/api/v1/rooms` with `Authorization: Bearer TOKEN`, JSON `{ "mode": "practice" }` (or `free`). See `docs/API.md`.

## Roadmap and limitations

**Not yet implemented:** user login via SMS, ranked ladders, coins, sponsorship billing, social systems, durable match event log, full rejoin, strong bots/ISMCTS, production hosting telemetry, Redis/Valkey orchestration, 1000+ player load certification, LAN cryptographic authentication, and final UI/UX. This is an honest v0.1 bootstrap, not a completed game.

## Stage S01 implementation (2026-11-20)

CI now builds/tests the .NET core, validates PostgreSQL migrations, and builds a container. Operators can deploy to staging, use checksummed SQL migrations and perform encrypted backups/isolated restore drills. See [`docs/stages/S01_EXECUTION.md`](docs/stages/S01_EXECUTION.md), [`docs/operations/S01_SERVER_RUNBOOK.md`](docs/operations/S01_SERVER_RUNBOOK.md), and the ADRs. None of these files prove VPS deployment or public production readiness.
