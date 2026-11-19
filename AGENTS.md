# AGENTS.md — Hokmnameh coding contract

- Read `README.md`, `docs/ARCHITECTURE.md`, `docs/planning/CHANGE_IMPACT.md`, and the specific Stage/Work/Task before editing code.
- Treat `docs/planning/STAGES_WORKS_TASKS.md` as **backlog**, not a claim that features are implemented.
- A private, owner-edited Master Document was not accessible when this backlog was authored. Do not invent approval; flag ambiguous rules.
- Preserve C# pure rules core as authority for both online ASP.NET and Unity Android LAN. No Unity-dependent code inside Core.
- Do not change Classic Ruleset without version and fixture tests; Arena belongs to a separate Phase-2 ruleset.
- Never expose other players' hidden hands or RNG seed in snapshots, logs, analytics, or bot observations.
- Ranked: Solo/Duo prioritized parity; 3-month calendar seasons; 6 leagues; no coin entry fee, no VIP, no pay-to-win.
- Practice vs bots never grants coins or resources convertible into repeatable coins; legitimate one-time onboarding bonus must be idempotent.
- In-match free text is forbidden. Vetted preset messages/emotes only. Outside-match chat needs server-side Persian moderation and appeals.
- Bot replacement on disconnect must complete games; sanctions need evidence and escalation.
- AI decision reports must use ex-ante information and be displayed only after match; do not call LLM APIs per card move.
- Current GameRoom is in-memory and not production durable; do not claim thousands CCU until load/chaos tests.
- Deployment: existing `srun.ir` host must remain operational, API bind only to localhost, secrets never commit, backups/rollback first.
- For every task: minimal diffs, targeted tests, update migration/docs/API contracts, and report the task ID + actual test evidence.