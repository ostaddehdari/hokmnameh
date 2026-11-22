# Engineering Architecture Decision Record — 2026-11-19

## Decision

**Single C# rules engine** embedded in Unity as `Packages/com.hokmnameh.core` and compiled into the ASP.NET API via linked source. Independent of Unity APIs. Rules execute on the authoritative online server OR on the Android LAN host. Client UI is not a source of truth.

```
             Unity Android 4 players
                     |
              Internet / HTTPS
                     |
            srun.ir/hokmnameh
                     |
             Nginx reverse proxy
                     |
             ASP.NET Core 10 API
                 |        |
               Rooms     PostgreSQL 18
            (in memory)  accounts & sessions
                 |
           Shared Rules Engine

  No Internet: same Unity Android app
          Host handset (TCP port 37991)
              + Shared Rules Engine
              /     |       \
          handset handset handset
```

## Exactly what scales today vs later

The current v0.1 API can handle a small number of rooms; each room is in-memory and all matches on one process. NO load-test evidence exists for 1000s of connected players. There are no durability guarantees for ongoing games across process crashes. The server's Timer uses a room-by-room tick, which must be profiled before any large-scale launch.

**Phase 2 for thousands of concurrent users:** stateless auth/API nodes; a match-placement directory and per-match single-writer worker ownership; process-local room actors under bounded scheduling; atomic checkpoint/event log into PostgreSQL; Valkey for ephemeral presence, queues and matchmaking; sticky WS connection routing or event pub/sub; bounded backpressure; reconnect tokens; metrics for simultaneous connections, turn p95 latency, match CPU, heap, DB pool; chaos testing.

Target planning example: 4,000 connected players -> 1,000 simultaneous four-player rooms (assuming all currently in matches). THIS IS A PLANNING INPUT, NOT a benchmark/capacity claim. The target should be proven at e.g. 100, 500, 1,000, 4,000 synthetic clients with adversarial disconnects and 4x expected message rates.

## Database

PostgreSQL 18 source of truth for identity, eventual match records, reputation, economy and commerce. Schema in `backend/db/migrations/001_core.sql` intentionally contains only the minimum tables in use. The earlier 74-table model is archived as `docs/extended_schema_74_tables_REFERENCE_ONLY.sql`, not auto-applied. New modules are introduced by reviewed versioned migrations, not applying a giant unverified schema.

No Redis/Valkey dependency is needed until a service actually uses it. Avoids operational complexity during MVP.

## Rules specifics

Classic 4-player Hokm is frozen as `iranian-hokm-classic-v1`: seats (0,2) vs (1,3), 52 cards, five cards per seat before Hakem selects trump, eight remaining each, must follow suit, first to seven tricks ends the round, and first team at or above seven match points wins. A normal round awards one point; a 7–0 sweep by Hakem's team awards two; a 7–0 sweep by the non-Hakem team awards three. Hakem stays when Hakem's team wins; otherwise authority rotates to the next clockwise seat. Every match records the immutable ruleset ID. Any behavioral change requires a new ruleset ID and Change Request.

## Product constraints preserved

- Unlimited free practice, casual and ranked entry. No coin admission fee.
- Practice bots never mint coins. XP awards are future economy work, not yet implemented.
- No free text inside a match; later allow vetted preset emotes subject to anti-signal design.
- Ranked allows solo/duo; prefer same-type matchmaking. No ranked engine in v0.1.
- Ranked seasonal structure must support three-month seasons.
- Sponsor skins never influence rules/MMR.
- Offline LAN never generates authoritative online rank, achievements or paid currency while offline; any future offline synchronization must explicitly prevent replay abuse.
- Hokm Arena tactical powers belong to separate later-phase ruleset; do not mix with Classic core.

## Dependency minimization

Use .NET SDK APIs where possible, Npgsql only for DB, Unity built-in UI/socket networking for the prototype. Do not integrate a paid networking asset or an AI inference API before functional LAN/online smoke tests.
