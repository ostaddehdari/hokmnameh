# S02-W01 + S02-W02 — implementation evidence and remaining gates

This branch advances both Works together. Source changes are governed by `CR-2026-0010` (draft until independent approvals). The approved cryptographic parameters are in `DEC-2026-0006`; `docs/e04_test_vector_v1.json` is the cross-language fixture.

## Implemented

- E04 canonical context, HKDF-SHA256, HMAC counter stream, rejection-sampled Fisher–Yates, domain-separated commitment, cut rotation and HMAC fallback. OS CSPRNG supplies 32-byte seed and 16-byte nonce; failures stop round creation. The source game publishes only commitment, algorithm ID and cut index, and deals 5 + 4 + 4 packets counter-clockwise from Hakem. Trump is chosen after the first five cards. Cursors are 20, 36, 52.
- `CommandV1` / `MatchEventV1` require sequence, action ID, actor seat, ruleset version, timestamp and state version. The room appends public actions and signs each seat-specific state with an in-memory HMAC key, returning `snapshotProof` alongside the existing `game` / WebSocket `state` fields.
- Shared smoke test compares the .NET implementation with E04-TV-001, exercises all 51 cuts, checks deal cursors, rejects duplicate/gapped actions and tampered or cross-seat snapshots, and checks serialized snapshots/events have no secret field names.

## Gates before closure

- The current room and event list are in memory; there is no durable authoritative event store or deterministic replay. That work is scheduled in S02-W03. Claims about durable single-source-of-truth, crash recovery or proof retention must wait.
- The random seed/nonce are cleared after shuffle. An auditable, encrypted 30-day post-close proof and player cut/deadline ceremony are scheduled in S02-W04/W05; this branch does **not** implement those contracts. Automatic HMAC fallback cut is used for the prototype.
- A server-side HMAC signature can only be verified by the running authority; the API does not supply clients with verification keys. Never log or return HMAC keys, seed, nonce or hidden decks.
- The source CI, independent role approvals in the Change Request, security review of live HTTP/WS serialization and a suitable per-seat network test are required before marking either Work complete or merging to `main`.

## Commands

```sh
node docs/verify-e04-decision-v1.mjs
node 0000/change-control/verify-change-control.mjs
dotnet build Hokmnameh.sln -c Release -warnaserror -p:ContinuousIntegrationBuild=true
dotnet run --project backend/tests/Hokmnameh.SmokeTests/Hokmnameh.SmokeTests.csproj -c Release
```

No VPS deployment was performed by this branch.
