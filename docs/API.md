# Hokmnameh HTTP / WebSocket API v0.1

Public base path: `/hokmnameh` (`https://srun.ir/hokmnameh` AFTER server deployment). Service currently runs only in local/offline build context.

## Authentication

- `POST /api/v1/identity/guest` -> `201` `{ accountId, nickname, accessToken, expiresAt }`. Token is generated using 256-bit CSPRNG and only SHA-256 is stored in PostgreSQL.
- `GET /api/v1/identity/me` with header `Authorization: Bearer <accessToken>` -> identity.
- Guest tokens are long-lived (30 days) for the prototype. Store in Android keystore/secure storage in a production client, provide rotation/revocation and abuse controls before launch.

## Rooms

- `POST /api/v1/rooms`, header `Authorization: Bearer <token>`, JSON `{ "mode": "free" }` -> `{ room, seat }` (seat 0). Every room includes immutable `rulesetVersion`; it is captured at match creation and cannot change for that active match. Other supported mode: `practice` automatically assigns three heuristic bots.
- `POST /api/v1/rooms/join/{8-char-room-code}`, bearer -> `{ room, seat }`. Guest may rejoin while server process remains active.
- `GET /api/v1/rooms/{uuid}`, bearer -> seat-specific `{ room, game, turnDeadline, isYourTurn }`.
- `POST /api/v1/rooms/{uuid}/trump`, bearer + JSON `{ "suit": 0 }` (Spades=0 Hearts=1 Diamonds=2 Clubs=3). Only hakem can call.
- `POST /api/v1/rooms/{uuid}/play`, bearer + JSON `{ "card": 0 }` (card 0..51: suit=floor(card/13), rank=(card mod 13)+2). Must follow led suit.
- `GET /health/live`, `GET /health/ready`.

### WebSocket

Connect to `wss://srun.ir/hokmnameh/ws/rooms/{uuid}` using the same bearer header. This is **server-to-client state push only**; mutations go through authenticated HTTP endpoints.

Each `state` message includes only the connecting player's `hand`, other seats' `handCounts`, public trick, public scoreboard and deadline. NEVER send other players' hidden cards, undealt cards, or the random shuffle source to clients. No spectator API exists yet.

The room code is an invitation label, not a credential. All room APIs verify membership.

## Error responses

- 401 unauthenticated, 403 room non-member (WS), 400 illegal moves, 404 missing rooms, 429 guest creation throttling.
- Other uncaught database errors surface as 500 and should be sent to structured logging with sanitized output.

## Intentionally not implemented

Ranked economy, durable match log, resume after instance restart, OAuth/SMS, anti-fraud policies, dynamic skill match, durable timers, public player discovery, mobile online UI. Do NOT connect players to a ranked room using this prototype without those capabilities.

## Unity HTTP fallback

`OnlineClient.cs` implements minimal guest creation, room create/join and authenticated polling via UnityWebRequest. It is **not** production realtime transport and must be migrated to WebSocket push + reconnect.
