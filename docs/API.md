# Hokmnameh HTTP / WebSocket API v0.1

Public base path: `/hokmnameh` (`https://srun.ir/hokmnameh` AFTER server deployment). Service currently runs only in local/offline build context.

## Authentication

- `POST /api/v1/identity/guest` -> `201` `{ accountId, nickname, accessToken, expiresAt }`. Token is generated using 256-bit CSPRNG and only SHA-256 is stored in PostgreSQL.
- `GET /api/v1/identity/me` with header `Authorization: Bearer <accessToken>` -> identity.
- Guest tokens are long-lived (30 days) for the prototype. Store in Android keystore/secure storage in a production client, provide rotation/revocation and abuse controls before launch.
- `POST /api/v1/identity/logout` revokes the current bearer session (204); a revoked or unknown token returns 401.
- `GET /api/v1/profile/progress` requires a valid bearer and returns only that account's `accountId`, `xp`, and `level`. XP is issued in the same transaction as an audited `round-closed` event for eligible human participants. Mode and total daily caps use the Tehran calendar day and the active versioned policy; ranked is disabled. No phone challenge route is active.
- `GET /api/v1/profile` returns the current nickname, starter avatar ID and optimistic `ETag` (`"1"` etc.) for the bearer. `PUT /api/v1/profile` accepts `{ "nickname": "بازیکن نو", "avatarId": "avatar-03" }` with `If-Match: "1"`; it returns a new ETag, 412 on a stale edit, 428 when missing, 409 for an occupied nickname, and 400 for invalid fields. Avatar IDs are `avatar-01` through `avatar-08`. This changes no XP, currency, league or reputation.

## Rooms

- `POST /api/v1/rooms`, header `Authorization: Bearer <token>`, JSON `{ "mode": "free" }` -> `{ room, seat }` (seat 0). Every room includes immutable `rulesetVersion`; it is captured at match creation and cannot change for that active match. Other supported mode: `practice` automatically assigns three heuristic bots.
- `POST /api/v1/rooms/join/{8-char-room-code}`, bearer -> `{ room, seat }`. Guest may rejoin while server process remains active.
- `GET /api/v1/rooms/{uuid}`, bearer -> seat-specific `{ room, game, turnDeadline, isYourTurn }`.
- `POST /api/v1/rooms/{uuid}/trump`, bearer + JSON `{ "suit": 0 }` (Spades=0 Hearts=1 Diamonds=2 Clubs=3). Only hakem can call.
- `POST /api/v1/rooms/{uuid}/play`, bearer + JSON `{ "card": 0 }` (card 0..51: suit=floor(card/13), rank=(card mod 13)+2). Must follow led suit.
- `GET /health/live`, `GET /health/ready`.

### Opt-in E-04 Cut room (v2 creation)

- `POST /api/v2/rooms`, bearer + `{ "mode": "free" }` creates a room using the Cut protocol. Existing v1 room creation continues to deal immediately for prototype compatibility. Once all four seats join, the v2 room exposes `room.cutPhase: "CUT_PENDING"`, an eight-second `room.cutDeadline`, and `cutStateVersion` in the authenticated seat state. `game` is null until Cut completes. For this incremental version, join/state/trump/play remain on the existing `/api/v1/rooms/...` routes.
- The Cutter is `(hakemSeat + 3) % 4`. Only that authenticated room member may call `POST /api/v2/rooms/{uuid}/cut` with `{ "cutIndex": 17, "actionId": "<new UUID>", "stateVersion": 0 }`. The value of `stateVersion` must be read from the current pending state. Submit an index in `1..51` once, before the deadline. No Deck or Seed is accepted or exposed. A successful command returns the caller's seat-specific state after the authority records the cut event, sealed proof and round start.
- Typed `application/problem+json` responses include `code`: `cut_not_member` / `wrong_cutter` (403), `invalid_cut_action_id` / `invalid_cut_index` (400), `duplicate_cut_action` / `cut_not_pending` / `stale_cut_state` (409), and `cut_deadline_expired` (410). At timeout, the authority records a deterministic HMAC-domain fallback and deals; disconnect does not extend the deadline. An unavailable room remains 404, and missing authentication remains 401.
- Pending Cut state and Seed currently live in process memory; the cut event and encrypted proof become durable when Cut is accepted or fallback runs. Migration 003 enforces at most one accepted Cut per match/round even across concurrent commands. Restart recovery during `CUT_PENDING` is a later task and is **not** guaranteed here. No v2 room is approved for Ranked or production use yet.
- After the initial five cards, the Hakem has 20 seconds to choose Hokm. Late commands return `trump_deadline_expired` (410); fallback picks the most frequent suit in those five cards with suit order as the tie breaker. V2 commits staged cursors 20, 36 and 52 as separate audit events; the seat-specific snapshot exposes `dealPlanVersion` and `dealCursor` without the undealt deck.

### WebSocket

Connect to `wss://srun.ir/hokmnameh/ws/rooms/{uuid}` using the same bearer header. This is **server-to-client state push only**; mutations go through authenticated HTTP endpoints.

Each `state` message includes only the connecting player's `hand`, other seats' `handCounts`, public trick, public scoreboard and deadline. NEVER send other players' hidden cards, undealt cards, or the random shuffle source to clients. No spectator API exists yet.

The room code is an invitation label, not a credential. All room APIs verify membership.

## Error responses

- 401 unauthenticated, 403 room non-member (WS), 400 illegal moves, 404 missing rooms, 429 guest creation throttling.
- Other uncaught database errors surface as 500 and should be sent to structured logging with sanitized output.

## Intentionally not implemented

Ranked economy, resume after instance restart, OAuth/SMS, anti-fraud policies, dynamic skill match, durable timers, public player discovery, mobile online UI. Do NOT connect players to a ranked room using this prototype without those capabilities.

## Unity HTTP fallback

`OnlineClient.cs` implements minimal guest creation, room create/join and authenticated polling via UnityWebRequest. It is **not** production realtime transport and must be migrated to WebSocket push + reconnect.

## S02 private state extension (CR-2026-0010)

Room state responses retain `game` and WebSocket `state` and add `snapshotProof: { stateVersion, signature }`. The signature covers room ID, seat, state version and the exact JSON snapshot, using an authority-only per-room HMAC key. The API sends only the authenticated player's hand; hand counts, current/last trick, score, trump, E04 commitment and cut index are public. The signature is only verifiable by the current process. No seed, nonce or other player's hidden hand may appear in HTTP, WebSocket or logs. CR-2026-0010 was verified for code merge; production operation remains gated.

Online room creation requires PostgreSQL migration `002_match_audit` and a protected `HOKM_AUDIT_KEY_BASE64` containing 32 random bytes encoded as base64. Versioned match events and sealed E04 round proofs are persisted before the corresponding state is served. A storage failure quarantines the match. Internal deterministic replay is available through an operator-only container CLI: `dotnet Hokmnameh.Api.dll --verify-replay <match-uuid>`; it prints only format, state version, closed rounds, phase, round, score and commitment. It is not an HTTP route and returns no secret or hand. Live room rejoin after restart and participant proof delivery remain separate work; callers cannot request private seed/nonce through this API.
