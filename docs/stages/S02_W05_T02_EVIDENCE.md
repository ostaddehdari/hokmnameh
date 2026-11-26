# S02-W05-T02 — authorized Cut command

The v2 room creation route opts into a server-owned `CUT_PENDING` phase before any card is dealt. The server alone owns the random Seed, shuffled Deck, eight-second deadline and fallback. The authenticated cutter `(hakemSeat + 3) % 4` submits one `cutIndex`, `actionId` and current `stateVersion`. Incorrect seat, state, duplicate action, deadline and invalid index produce typed errors without changing the authoritative state. The v1 prototype room creation retains its existing immediate-deal behavior.

On success or timeout, the cut event is persisted before the `round-started` event and encrypted proof, then the authority deals. Replay verifies the public cut event matches the sealed proof; the old v1 golden fixture remains valid. In-memory pending state cannot recover across process restart, so production readiness and persistent pending-cut recovery are not claimed.

Targeted shared-core smoke, isolated API/PostgreSQL drill, and repository CI provide the acceptance evidence. This document will be updated with actual run IDs after CI.
