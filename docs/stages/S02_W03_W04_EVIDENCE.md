# S02-W03/W04 — replay and E-04 verification

Source baseline: `S02-W01/W02` merged in PR #13. This work adds an internal `hokmnameh-match-replay-v1` verifier. It loads ordered `match_events_v1` and encrypted `round_proofs_v1` from one repeatable-read PostgreSQL snapshot. It validates envelope sequence, unique action IDs, ruleset, E-04 commitment, event legality and round boundaries before it returns a game state. The 32-byte Seed and 16-byte Nonce are zeroed when the bundle is disposed. Unknown format, algorithm or ruleset fails closed; no public replay endpoint exists.

`docs/replay_match_v1.golden.json` contains **synthetic public test secrets only**. An independent Node reference in `tools/verify-replay-fixture.mjs` regenerates the 11-round, 517-event match; the .NET smoke reconstructs the same winner/score, each E-04 commitment and first 12 cut cards. Prefix recovery, duplicate action, sequence gap, changed Seed and unknown replay format are tested. The online PostgreSQL drill also reconstructs an open and closed round through the internal container CLI, comparing its public result with the authoritative room state.

The prior E-04 implementation is reused without changing the approved shuffle contract. Tests cover 256-bit Seed, 128-bit Nonce, versioned HKDF/HMAC rejection-sampled Fisher–Yates, SHA-256 commitment before deal, official E04-TV-001 and independence from MMR/cosmetic/advertisement metadata. A random-source failure stops round creation; no clock-derived seed or product metadata reaches the shuffle context.

This is a code and isolated-CI gate. It does not persist roster/deadline/snapshot HMAC key and does not make active rooms automatically recoverable on restart. No production VPS run or off-host key-custody/30-day backup-erasure proof is claimed.

## Verified isolated run

GitHub Actions run `45796625501` (source PR #14) passed all five jobs: static validation, .NET build and full-match smoke, checksum/idempotent PostgreSQL migration, Docker image and operations drill. The latter printed `RESTORE_DRILL PASS tables=8 migrations=2 duration_seconds=2 isolated=true` and `eventCount=52 moves=48 encryptedProof=true closedRoundVerified=true replayVerified=true status=PASS`. The two prior CI attempts exposed a ticker boundary race and an internal CLI JSON casing mismatch; both were fixed before this successful run. No VPS test was executed.
