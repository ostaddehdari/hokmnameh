# Hokmnameh real-time protocol v1 (wire contract draft)

Both Online and LAN use the same logical commands, event types and rule version. The transport may be UDP ReliableOrdered (LiteNetLib) or another validated transport.

## Envelope (illustrative JSON; production may switch to MessagePack)

```json
{"v":1,"type":"play_card","roomId":"UUID","commandId":"UUID","lastSeenSeq":128,"clientTimeMs":0,"payload":{"card":"spades:14"}}
```

Server -> player 0:

```json
{"v":1,"type":"state_delta","roomId":"UUID","seq":129,"commandId":"UUID","payload":{"seat":0,"playedCard":"spades:14","nextSeat":1,"timerEndsInMs":30000}}
```

## Commands
- JoinRoom, Ready, ChooseTrump, PlayCard, UseEmote, LeaveRoom, Reconnect, RequestSnapshot.
- `PlayCard`: validate authenticated seat, active phase, turn ownership, card ownership, follow-suit, timer; apply exactly once by commandId.
- `ChooseTrump`: only Hakem, only in TrumpSelection, exactly one suit.
- If duplicated commandId: return the same server result; never replay game effects.

## Events
RoomJoined, GameStarted, FiveCardsDealtPrivate, TrumpSelected, RemainingCardsDealtPrivate, CardPlayed, TrickWon, RoundWon, MatchEnded, SeatDisconnected, BotSubstituted, PlayerRejoined, Snapshot.

## Information filtering
- Public state: seats, trump, played cards, trick totals, timer, rule version.
- Player-private state: **only their own hand** and permissible private inferences.
- Server-only state: complete deck, RNG seed, full hands, connection/session authorization.
- A spectator/admin stream must not expose unrevealed hands.

## Recovery
- Reconnect token is short-lived and bound to the original account/session.
- Client sends lastSeenSeq. Server returns ordered missing deltas or full public+private snapshot.
- Host crashes: Online node recovers from persistent event log/snapshot. LAN cannot guarantee migration; inform users.

## Compatibility
- Reject major protocol mismatch; preserve backward-compatible optional fields for minor revisions.
- Commands/event body schema is generated from a versioned contract directory.
