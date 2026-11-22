# ماتریس شکاف واقعی v0.1 حکمنامه

- Task: `S00-W01-T02`
- Source baseline: `e8c91a31ce347d423d7402dd51e05b9938be18bc`
- Requirements: 33
- این سند ادعای نصب یا تکمیل runtime نیست.

| ID | وضعیت واقعی | مسیرهای شاهد | شکاف اصلی | وابستگی |
|---|---|---|---|---|
| FR-001 | partial | `unity/Packages/com.hokmnameh.core/Runtime/HokmGame.cs`<br>`docs/ruleset_hokm_classic_v1.json`<br>`backend/tests/Hokmnameh.SmokeTests/Program.cs` | تصویب و freeze قواعد؛ Golden/property tests کامل | S00-W02, S02-W01 |
| FR-002 | planned | `unity/Packages/com.hokmnameh.core/Runtime/HokmGame.cs`<br>`docs/planning/CHANGE_IMPACT.md` | پیاده‌سازی E-04؛ test vector و audit/reveal | S00-W04, S02-W04, S02-W05, S02-W06 |
| FR-003 | partial | `backend/src/Hokmnameh.Api/AuthService.cs`<br>`backend/db/migrations/001_core.sql`<br>`docs/API.md` | OTP موبایل؛ rotation/recovery/revocation API؛ abuse controls کامل | S03-W01 |
| FR-004 | partial | `backend/src/Hokmnameh.Api/Rooms.cs`<br>`unity/Packages/com.hokmnameh.core/Runtime/HokmBot.cs`<br>`unity/Assets/Hokmnameh/Runtime/Launcher.cs` | XP ledger idempotent؛ اثبات coin delta=0؛ انتخاب پنج سطح سختی | S03-W02, S05-W02 |
| FR-005 | partial | `backend/src/Hokmnameh.Api/Rooms.cs`<br>`backend/src/Hokmnameh.Api/Program.cs` | ready check؛ event log پایدار؛ reconnect پس از restart؛ bot replacement کامل | S04-W01, S04-W02, S04-W03 |
| FR-006 | partial | `unity/Packages/com.hokmnameh.core/Runtime/HokmGame.cs`<br>`backend/src/Hokmnameh.Api/Rooms.cs`<br>`docs/API.md` | contract/security test خصمانه؛ redaction سراسری log/analytics | S02-W02, S12-W02 |
| FR-007 | planned | `docs/planning/STAGES_WORKS_TASKS.md` | party consent؛ queue policy؛ atomic reservation؛ چهار Ready انسانی | S07-W01, S07-W04, S07-W05 |
| FR-008 | planned | `docs/planning/STAGES_WORKS_TASKS.md` | season ledger؛ شش لیگ؛ rollover سه‌ماهه؛ idempotent adjudication | S07-W02, S07-W03 |
| FR-009 | planned | `backend/src/Hokmnameh.Api/Rooms.cs`<br>`docs/NEXT_STEPS.md` | disconnect evidence؛ bot completion؛ penalty escalation؛ appeal | S04-W03, S07-W06, S09-W03 |
| FR-010 | planned | `docs/ARCHITECTURE.md`<br>`docs/planning/STAGES_WORKS_TASKS.md` | double-entry ledger؛ inventory؛ free-entry invariant؛ anti-inflation tests | S08-W01, S08-W02 |
| FR-011 | planned | `docs/planning/STAGES_WORKS_TASKS.md` | charge policy؛ daily pool؛ bounded claim؛ idempotency | S08-W02 |
| FR-012 | planned | `docs/planning/CHANGE_IMPACT.md` | registry/focus lock؛ ranked win grant؛ outbox/claim؛ Unity screens | S08-W03, S08-W04, S08-W05, S08-W06 |
| FR-013 | planned | `docs/ARCHITECTURE.md`<br>`docs/planning/STAGES_WORKS_TASKS.md` | campaign/tournament model؛ impression audit؛ billing/reporting | S10-W01, S10-W02, S10-W03 |
| FR-014 | planned | `AGENTS.md`<br>`docs/ARCHITECTURE.md` | preset emotes؛ Persian moderation؛ report/appeal/audit | S09-W01, S09-W02, S09-W03 |
| FR-015 | planned | `docs/planning/STAGES_WORKS_TASKS.md` | legal feature store؛ ex-ante evaluator؛ CI/confidence؛ post-match report | S11-W01, S11-W02 |
| FR-016 | partial | `unity/Packages/com.hokmnameh.core/Runtime/HokmBot.cs` | difficulty contract؛ belief sampling/ISMCTS؛ no-omniscience tests | S05-W02, S11-W03 |
| FR-017 | partial | `unity/Assets/Hokmnameh/Runtime/LanHost.cs`<br>`unity/Assets/Hokmnameh/Runtime/LanClient.cs`<br>`docs/UNITY_LAN.md` | تست واقعی چهار دستگاه؛ auth/integrity LAN؛ reward isolation evidence | S05-W01, S05-W03, S13-W02 |
| FR-018 | planned | `backend/src/Hokmnameh.Api/Program.cs`<br>`backend/src/Hokmnameh.Api/Hokmnameh.Api.csproj`<br>`docs/API.md` | OpenAPI generation؛ Swagger UI زیر base path؛ public HTTP verification | S01-W02, S12-W04 |
| FR-019 | partial | `unity/Assets/Hokmnameh/Runtime/Launcher.cs`<br>`docs/NEXT_STEPS.md` | RTL/design system؛ responsive device matrix؛ accessibility/reduced motion | S06-W01, S06-W02, S06-W03, S06-W04 |
| FR-020 | planned | `docs/ARCHITECTURE.md`<br>`docs/planning/STAGES_WORKS_TASKS.md` | config registry؛ audited flags؛ kill-switch drill | S00-W01, S12-W05 |
| NFR-001 | operational_unverified | `docs/ARCHITECTURE.md`<br>`docs/NEXT_STEPS.md` | load harness؛ 100/500/1000/4000 reports؛ chaos profile | S12-W03, S13-W01 |
| NFR-002 | partial | `docker-compose.yml`<br>`deploy/nginx/hokmnameh-location.conf`<br>`docs/DEPLOYMENT.md` | VPS socket/firewall evidence؛ TLS/public route verification | S01-W02, S12-W02 |
| NFR-003 | partial | `backend/src/Hokmnameh.Api/Program.cs`<br>`backend/db/migrations/001_core.sql` | ProblemDetails typed errors؛ bounded pagination؛ contract consistency tests | S04-W04, S12-W04 |
| NFR-004 | planned | `docs/proposal.md`<br>`docs/NEXT_STEPS.md` | contrast/safe-area/reduced-motion/non-color tests | S06-W04, S13-W02 |
| NFR-005 | partial | `deploy/scripts/watch-health.sh`<br>`deploy/systemd/hokmnameh-health.service`<br>`docs/operations/MONITORING.md` | structured logs؛ metrics/traces؛ dashboard/alerts؛ redaction tests | S01-W03, S12-W01 |
| SEC-001 | partial | `backend/src/Hokmnameh.Api/AuthService.cs`<br>`backend/src/Hokmnameh.Api/Program.cs`<br>`.github/workflows/backend.yml` | object auth suite؛ CORS/security headers؛ SAST/dependency policy؛ session rotation | S03-W01, S12-W02 |
| SEC-002 | partial | `backend/src/Hokmnameh.Api/Rooms.cs`<br>`AGENTS.md` | automated leak tests برای payload/log/analytics؛ seed retention policy | S02-W02, S12-W02 |
| SEC-003 | planned | `docs/proposal.md`<br>`docs/planning/DECISIONS_TO_CONFIRM.md` | cheating/replay/collusion/admin-abuse model؛ control-to-test trace | S00-W01, S12-W02 |
| DATA-001 | partial | `backend/db/migrations/001_core.sql`<br>`deploy/scripts/migrate.sh`<br>`docs/adr/0002-deployment-safety-and-migrations.md` | production apply evidence؛ backfill/rollback per migration؛ isolated restore proof | S01-W03, S12-W06 |
| DATA-002 | planned | `backend/db/migrations/001_core.sql`<br>`docs/planning/STAGES_WORKS_TASKS.md` | append-only event ledger؛ idempotency/optimistic concurrency؛ race tests | S02-W02, S04-W02, S08-W01 |
| OPS-001 | operational_unverified | `deploy/scripts/backup.sh`<br>`deploy/scripts/restore-drill.sh`<br>`docs/operations/RESTORE_DRILL.md` | separate staging؛ encrypted off-host backup؛ real restore evidence | S01-W03, S12-W06 |
| OPS-002 | operational_unverified | `deploy/scripts/deploy.sh`<br>`deploy/scripts/check.sh`<br>`deploy/nginx/hokmnameh-location.conf` | server inventory؛ nginx backup/test/reload؛ immutable release and measured rollback | S01-W02, S13-W03 |
| OPS-003 | partial | `.github/workflows/backend.yml`<br>`.github/CODEOWNERS`<br>`.github/pull_request_template.md` | required branch checks؛ SAST/dependency scan؛ Unity build/device lane | S01-W01, S12-W02, S13-W02 |

## تفسیر وضعیت

- `implemented`: معیار v0.1 با تست کافی محقق شده است.
- `partial`: بخشی از runtime موجود است ولی معیار کامل نیست.
- `planned`: در runtime وجود ندارد و فقط طراحی/backlog است.
- `pending_decision`: پیاده‌سازی به تصمیم مالک وابسته است.
- `operational_unverified`: ابزار موجود است اما اجرای واقعی محیط هدف evidence ندارد.
