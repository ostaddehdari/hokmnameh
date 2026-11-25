# S02-W01 + S02-W02 — implementation evidence and remaining gates

This branch advances both Works together under verified `CR-2026-0010`. The owner instructed completion, and ProjectMaker recorded technical role reviews with actual CI evidence. CODEOWNERS currently names only the repository owner; no distinct human reviewer has been appointed. The approved cryptographic parameters are in `DEC-2026-0006`; `docs/e04_test_vector_v1.json` is the cross-language fixture.

## Implemented

- E04 canonical context, HKDF-SHA256, HMAC counter stream, rejection-sampled Fisher–Yates, domain-separated commitment, cut rotation and HMAC fallback. OS CSPRNG supplies 32-byte seed and 16-byte nonce; failures stop round creation. The source game publishes only commitment, algorithm ID and cut index, and deals 5 + 4 + 4 packets counter-clockwise from Hakem. Trump is chosen after the first five cards. Cursors are 20, 36, 52.
- `CommandV1` / `MatchEventV1` require sequence, action ID, actor seat, ruleset version, timestamp and state version. The room appends public actions and signs each seat-specific state with an in-memory HMAC key, returning `snapshotProof` alongside the existing `game` / WebSocket `state` fields.
- Shared smoke test compares the .NET implementation with E04-TV-001, exercises all 51 cuts, checks deal cursors, rejects duplicate/gapped actions and tampered or cross-seat snapshots, and checks serialized snapshots/events have no secret field names.
- A simulated four-player room now checks every authenticated HTTP state and every WebSocket broadcast against that seat's five private cards. It rejects a non-member requesting state or registering a socket. An entropy failure cannot partially advance the round because the random deck is prepared before any state mutation, and the temporary seed/nonce are cleared even when CSPRNG fails.

## Durable custody and fail stop (candidate)

- Migration `002_match_audit.sql` adds a per-match monotonic event ledger with a unique `(match_id, action_id)` and an encrypted round-proof table. `PostgresMatchStore` commits the `round-started` event and AES-256-GCM sealed seed/nonce together before any card is dealt. Each later command appends through a row lock and version check. On storage failure the room is quarantined and cannot serve uncommitted state.
- `HOKM_AUDIT_KEY_BASE64` is a unique 32-byte key in the protected operator `.env`; it is never stored in Git, proof ciphertext, event rows or API responses. Round proof can be decrypted by the internal auditor operation only after a committed `round-closed` event and before its 30-day expiry. The CI integration drill plays a full synthetic round against PostgreSQL, checks event sequence/count and recomputes the commitment from the decrypted proof. No public proof endpoint exists yet.

## Gates before closure

- Match events and sealed proofs are persisted, but active room state is not rehydrated from the ledger after a restart. Deterministic replay/rejoin is S02-W03. Backups and purge must respect the 30-day secret lifetime before production activation.
- The full player cut/deadline ceremony is scheduled in S02-W05; this prototype uses the HMAC fallback cut. End-user post-close proof delivery is not enabled; only the internal auditor can decrypt closed proofs.
- A server-side HMAC signature can only be verified by the running authority; the API does not supply clients with verification keys. Never log or return HMAC keys, seed, nonce or hidden decks.
- GitHub Actions run 45694139070 passed all five jobs: .NET shared smoke, static checks, checksummed migration/tamper rejection, Docker image and encrypted backup/restore. The isolated runtime drill played 36 moves, committed 39 events, decrypted the sealed proof after close and recomputed its commitment. Protected-path verifier and final PR CI must pass before merge. Real-device/rejoin and production retention remain future gates.

## Commands

```sh
node docs/verify-e04-decision-v1.mjs
node 0000/change-control/verify-change-control.mjs
dotnet build Hokmnameh.sln -c Release -warnaserror -p:ContinuousIntegrationBuild=true
dotnet run --project backend/tests/Hokmnameh.SmokeTests/Hokmnameh.SmokeTests.csproj -c Release
```

No VPS deployment was performed by this branch.
