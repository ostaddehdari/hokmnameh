# ADR-0001 — Single C# game core, monorepo, separate authorities

Status: **ACCEPTED AS ENGINEERING ARCHITECTURE** · 2026-11-20 · Stage `S01-W01-T01/T03`.

## Context
Hokmnameh needs real-time online four-player matches and completely offline Android LAN sessions. Gameplay correctness and card confidentiality must not depend on Unity presentation, advertising or economy logic.

## Decision
- Keep the game rules engine in `unity/Packages/com.hokmnameh.core` with no `UnityEngine` dependency, compiled into both Unity Android and authoritative .NET match server.
- Use ASP.NET Core 10 for product API, PostgreSQL 18 for durable truth, and process-local rooms **only for prototype**; no thousands-CCU claims until actor/match-worker + load/chaos tests.
- Keep `backend`, `unity`, `docs`, `deploy` in one repository. All changes are associated with a Stage/Work/Task ID and a ruleset/protocol version where relevant.
- Separate online authority (server) from LAN authority (host handset). Offline results must not mint online ranked rewards or economic assets.

## Compatibility / versioning
- API breaking changes require a new API major prefix, contract tests and a client minimum version/upgrade policy.
- Game ruleset changes require a new immutable ruleset ID and golden tests; an active match keeps its original version.
- Database changes use immutable monotonically numbered migrations. Never edit an already applied migration.
- Unity asset bundles have an explicit content version and cannot alter `CardId` or gameplay authority.

## Consequences
Unity screenshots and animations can evolve independently. Shared engine determinism enables test parity, but network/session security and lifecycle recovery remain separate work items in S02/S04/S05/S12.
