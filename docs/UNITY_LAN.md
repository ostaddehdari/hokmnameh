# Unity Android and LAN testing (offline)

## Basic setup

1. Install Unity **6.3 LTS** through Unity Hub, including Android Build Support (SDK/NDK, OpenJDK).
2. Open `unity` folder as the project, wait for Package Manager resolution.
3. From menu **Hokmnameh > Setup Build Scene**, click Play to exercise the same shared rules source with three heuristic bots.
4. Change build target to Android, set IL2CPP, ARM64, package name, and confirm Android INTERNET permission. Build & Run.
5. On phone A enable a personal hotspot (no mobile data required for LAN) or connect four phones to the same router. Start `Host local game` on A.
6. On phones B/C/D join A's hotspot. Read host handset's local IPv4 in the app (or Android hotspot settings); enter that address and click `Join local game`.
7. After all four join, first 5 cards are distributed, Hakem selects trump, remaining 8 dealt, and play proceeds with follow-suit enforcement.

**Hotspot note:** Not all Android devices permit peer-to-peer access between hotspot clients. Some isolate peers or assign gateways differently. Test target handset brands and Android releases before committing to this method. Use a normal Wi-Fi router as a deterministic fallback.

## Threat model and missing parts

This version is a LAN engineering proof-of-concept with plaintext TCP, manual IP, no device PIN, no reconnect handshake, no anti-DoS or encrypted framing. Use only within trusted local groups. For production: pairing PIN with host approval, signed sequence-numbered commands, local network permissions on Android, retry/ack protocol (or audited reliable-transport library), disconnect state transfer, Android background/foreground service policy and session migration when the host exits.

Host authority must retain all hidden cards; clients receive only seat-specific state. Local matches never increase global rank or coin balances.

## Visuals

Current `Launcher.cs` renders a basic prototype with Unity `OnGUI`, which is deliberately NOT the target art. Once gameplay passes physical-device testing, replace with UGUI Canvas, card prefab pooling, portrait/landscape layout decisions, 2D URP, DOTween (license and platform review), card animations, haptics, audio and Persian-safe fonts. The 3D arena can be a later phase. Do not spend months on shaders before stabilizing core gameplay.

## Internet test in Unity

Use **Online server (beta HTTP)** in the prototype menu only after the backend is deployed. Create a guest identity, create a free room on phone A, then other phones enter the 8-character invitation code. The prototype polls authenticated HTTP at 750-ms intervals; replace with WebSocket state streaming before a high-concurrency rollout. No coin entry fees.
