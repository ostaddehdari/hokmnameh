import fs from 'node:fs';

const proposal = JSON.parse(fs.readFileSync(new URL('../docs/proposal.json', import.meta.url)));
const requirements = [...proposal.requirements, ...proposal.nonfunctional_requirements];

const map = {
  'FR-001': ['partial',['unity/Packages/com.hokmnameh.core/Runtime/HokmGame.cs','docs/ruleset_hokm_classic_v1.json','backend/tests/Hokmnameh.SmokeTests/Program.cs'],'قواعد پایه و smoke test موجود است؛ قرارداد فعلی هنوز 5+8 و برخی امتیازها provisional است.',['تصویب و freeze قواعد','Golden/property tests کامل'],['S00-W02','S02-W01']],
  'FR-002': ['planned',['unity/Packages/com.hokmnameh.core/Runtime/HokmGame.cs','docs/planning/CHANGE_IMPACT.md'],'Shuffle فعلی CSPRNG است اما Commit/Cut/HKDF/HMAC و Deal 5+4+4 اجرا نشده‌اند.',['پیاده‌سازی E-04','test vector و audit/reveal'],['S00-W04','S02-W04','S02-W05','S02-W06']],
  'FR-003': ['partial',['backend/src/Hokmnameh.Api/AuthService.cs','backend/db/migrations/001_core.sql','docs/API.md'],'هویت مهمان، token hash و session پایه وجود دارد.',['OTP موبایل','rotation/recovery/revocation API','abuse controls کامل'],['S03-W01']],
  'FR-004': ['partial',['backend/src/Hokmnameh.Api/Rooms.cs','unity/Packages/com.hokmnameh.core/Runtime/HokmBot.cs','unity/Assets/Hokmnameh/Runtime/Launcher.cs'],'practice با سه بات heuristic موجود است.',['XP ledger idempotent','اثبات coin delta=0','انتخاب پنج سطح سختی'],['S03-W02','S05-W02']],
  'FR-005': ['partial',['backend/src/Hokmnameh.Api/Rooms.cs','backend/src/Hokmnameh.Api/Program.cs'],'اتاق آزاد، join code و فرمان‌های HTTP موجودند؛ state حافظه‌ای است.',['ready check','event log پایدار','reconnect پس از restart','bot replacement کامل'],['S04-W01','S04-W02','S04-W03']],
  'FR-006': ['partial',['unity/Packages/com.hokmnameh.core/Runtime/HokmGame.cs','backend/src/Hokmnameh.Api/Rooms.cs','docs/API.md'],'Snapshot صندلی‌محور فقط hand خود بازیکن را هدف می‌گیرد.',['contract/security test خصمانه','redaction سراسری log/analytics'],['S02-W02','S12-W02']],
  'FR-007': ['planned',['docs/planning/STAGES_WORKS_TASKS.md'],'کد matchmaking/ranked در runtime وجود ندارد.',['party consent','queue policy','atomic reservation','چهار Ready انسانی'],['S07-W01','S07-W04','S07-W05']],
  'FR-008': ['planned',['docs/planning/STAGES_WORKS_TASKS.md'],'فصل، لیگ و MMR فقط در برنامه هستند.',['season ledger','شش لیگ','rollover سه‌ماهه','idempotent adjudication'],['S07-W02','S07-W03']],
  'FR-009': ['planned',['backend/src/Hokmnameh.Api/Rooms.cs','docs/NEXT_STEPS.md'],'timeout ساده وجود دارد؛ تشخیص ترک، sanction و appeal وجود ندارد.',['disconnect evidence','bot completion','penalty escalation','appeal'],['S04-W03','S07-W06','S09-W03']],
  'FR-010': ['planned',['docs/ARCHITECTURE.md','docs/planning/STAGES_WORKS_TASKS.md'],'قید non-P2W مستند است؛ ledger اقتصادی runtime وجود ندارد.',['double-entry ledger','inventory','free-entry invariant','anti-inflation tests'],['S08-W01','S08-W02']],
  'FR-011': ['planned',['docs/planning/STAGES_WORKS_TASKS.md'],'کارت شانس runtime ندارد.',['charge policy','daily pool','bounded claim','idempotency'],['S08-W02']],
  'FR-012': ['planned',['docs/planning/CHANGE_IMPACT.md'],'Royal Legacy فقط blueprint/backlog است.',['registry/focus lock','ranked win grant','outbox/claim','Unity screens'],['S08-W03','S08-W04','S08-W05','S08-W06']],
  'FR-013': ['planned',['docs/ARCHITECTURE.md','docs/planning/STAGES_WORKS_TASKS.md'],'اصل عدم اثر اسپانسر بر قواعد مستند است؛ سرویس sponsor وجود ندارد.',['campaign/tournament model','impression audit','billing/reporting'],['S10-W01','S10-W02','S10-W03']],
  'FR-014': ['planned',['AGENTS.md','docs/ARCHITECTURE.md'],'ممنوعیت free-text داخل match مستند است؛ moderation runtime وجود ندارد.',['preset emotes','Persian moderation','report/appeal/audit'],['S09-W01','S09-W02','S09-W03']],
  'FR-015': ['planned',['docs/planning/STAGES_WORKS_TASKS.md'],'AI-01 در backlog است و worker/feature store وجود ندارد.',['legal feature store','ex-ante evaluator','CI/confidence','post-match report'],['S11-W01','S11-W02']],
  'FR-016': ['partial',['unity/Packages/com.hokmnameh.core/Runtime/HokmBot.cs'],'یک heuristic bot وجود دارد؛ پنج سطح و ISMCTS وجود ندارد.',['difficulty contract','belief sampling/ISMCTS','no-omniscience tests'],['S05-W02','S11-W03']],
  'FR-017': ['partial',['unity/Assets/Hokmnameh/Runtime/LanHost.cs','unity/Assets/Hokmnameh/Runtime/LanClient.cs','docs/UNITY_LAN.md'],'prototype میزبان/کلاینت LAN موجود است.',['تست واقعی چهار دستگاه','auth/integrity LAN','reward isolation evidence'],['S05-W01','S05-W03','S13-W02']],
  'FR-018': ['planned',['backend/src/Hokmnameh.Api/Program.cs','backend/src/Hokmnameh.Api/Hokmnameh.Api.csproj','docs/API.md'],'API دستی مستند است؛ Swagger/OpenAPI middleware/package فعال نیست.',['OpenAPI generation','Swagger UI زیر base path','public HTTP verification'],['S01-W02','S12-W04']],
  'FR-019': ['partial',['unity/Assets/Hokmnameh/Runtime/Launcher.cs','docs/NEXT_STEPS.md'],'UI prototype عملکردی است و design system نهایی نیست.',['RTL/design system','responsive device matrix','accessibility/reduced motion'],['S06-W01','S06-W02','S06-W03','S06-W04']],
  'FR-020': ['planned',['docs/ARCHITECTURE.md','docs/planning/STAGES_WORKS_TASKS.md'],'versioning محدود موجود است؛ feature flag/kill switch runtime کامل نیست.',['config registry','audited flags','kill-switch drill'],['S00-W01','S12-W05']],
  'NFR-001': ['operational_unverified',['docs/ARCHITECTURE.md','docs/NEXT_STEPS.md'],'هدف بار و مراحل تست مستندند؛ benchmark واقعی وجود ندارد.',['load harness','100/500/1000/4000 reports','chaos profile'],['S12-W03','S13-W01']],
  'NFR-002': ['partial',['docker-compose.yml','deploy/nginx/hokmnameh-location.conf','docs/DEPLOYMENT.md'],'localhost/private-network intent در config وجود دارد.',['VPS socket/firewall evidence','TLS/public route verification'],['S01-W02','S12-W02']],
  'NFR-003': ['partial',['backend/src/Hokmnameh.Api/Program.cs','backend/db/migrations/001_core.sql'],'UTC timestamps و برخی statusها وجود دارند.',['ProblemDetails typed errors','bounded pagination','contract consistency tests'],['S04-W04','S12-W04']],
  'NFR-004': ['planned',['docs/proposal.md','docs/NEXT_STEPS.md'],'الزامات accessibility ثبت شده‌اند ولی UI نهایی/تست وجود ندارد.',['contrast/safe-area/reduced-motion/non-color tests'],['S06-W04','S13-W02']],
  'NFR-005': ['partial',['deploy/scripts/watch-health.sh','deploy/systemd/hokmnameh-health.service','docs/operations/MONITORING.md'],'health watcher پایه موجود است.',['structured logs','metrics/traces','dashboard/alerts','redaction tests'],['S01-W03','S12-W01']],
  'SEC-001': ['partial',['backend/src/Hokmnameh.Api/AuthService.cs','backend/src/Hokmnameh.Api/Program.cs','.github/workflows/backend.yml'],'bearer validation و rate limit پایه موجود است.',['object auth suite','CORS/security headers','SAST/dependency policy','session rotation'],['S03-W01','S12-W02']],
  'SEC-002': ['partial',['backend/src/Hokmnameh.Api/Rooms.cs','AGENTS.md'],'اصل محرمانگی و snapshot صندلی‌محور موجود است.',['automated leak tests برای payload/log/analytics','seed retention policy'],['S02-W02','S12-W02']],
  'SEC-003': ['planned',['docs/proposal.md','docs/planning/DECISIONS_TO_CONFIRM.md'],'فهرست تهدید در proposal هست؛ threat model اجرایی و trace کنترل‌ها ناقص است.',['cheating/replay/collusion/admin-abuse model','control-to-test trace'],['S00-W01','S12-W02']],
  'DATA-001': ['partial',['backend/db/migrations/001_core.sql','deploy/scripts/migrate.sh','docs/adr/0002-deployment-safety-and-migrations.md'],'migration checksum/lock tooling ایجاد شده است.',['production apply evidence','backfill/rollback per migration','isolated restore proof'],['S01-W03','S12-W06']],
  'DATA-002': ['planned',['backend/db/migrations/001_core.sql','docs/planning/STAGES_WORKS_TASKS.md'],'recorded match پایه هست؛ event/reward ledgers کامل نیستند.',['append-only event ledger','idempotency/optimistic concurrency','race tests'],['S02-W02','S04-W02','S08-W01']],
  'OPS-001': ['operational_unverified',['deploy/scripts/backup.sh','deploy/scripts/restore-drill.sh','docs/operations/RESTORE_DRILL.md'],'ابزار backup/restore نوشته شده ولی اجرای واقعی خارج‌میزبان اثبات نشده است.',['separate staging','encrypted off-host backup','real restore evidence'],['S01-W03','S12-W06']],
  'OPS-002': ['operational_unverified',['deploy/scripts/deploy.sh','deploy/scripts/check.sh','deploy/nginx/hokmnameh-location.conf'],'preflight/health/rollback tooling موجود است؛ نصب VPS تأیید نشده است.',['server inventory','nginx backup/test/reload','immutable release and measured rollback'],['S01-W02','S13-W03']],
  'OPS-003': ['partial',['.github/workflows/backend.yml','.github/CODEOWNERS','.github/pull_request_template.md'],'build/smoke/migration/docker CI قبلاً سبز شده است.',['required branch checks','SAST/dependency scan','Unity build/device lane'],['S01-W01','S12-W02','S13-W02']]
};

const entries = requirements.map(r => {
  const v = map[r.id];
  if (!v) throw new Error(`No gap mapping for ${r.id}`);
  return {id:r.id,title:r.title ?? r.description,status:v[0],decision_status:r.decision_status,code_paths:v[1],evidence:v[2],gaps:v[3],dependencies:v[4]};
});

const output = {
  schema_version:'1.0', task_id:'S00-W01-T02', project_slug:'hokmnameh',
  source_commit:'e8c91a31ce347d423d7402dd51e05b9938be18bc', generated_at:'2026-11-22',
  allowed_statuses:['implemented','partial','planned','pending_decision','operational_unverified'],
  summary:Object.fromEntries(['implemented','partial','planned','pending_decision','operational_unverified'].map(s=>[s,entries.filter(e=>e.status===s).length])),
  entries
};
fs.writeFileSync(new URL('./v01-gap-matrix.json', import.meta.url),JSON.stringify(output,null,2)+'\n');

let md = '# ماتریس شکاف واقعی v0.1 حکمنامه\n\n';
md += `- Task: \`${output.task_id}\`\n- Source baseline: \`${output.source_commit}\`\n- Requirements: ${entries.length}\n- این سند ادعای نصب یا تکمیل runtime نیست.\n\n`;
md += '| ID | وضعیت واقعی | مسیرهای شاهد | شکاف اصلی | وابستگی |\n|---|---|---|---|---|\n';
for(const e of entries) md += `| ${e.id} | ${e.status} | ${e.code_paths.map(x=>'`'+x+'`').join('<br>')} | ${e.gaps.join('؛ ')} | ${e.dependencies.join(', ')} |\n`;
md += '\n## تفسیر وضعیت\n\n- `implemented`: معیار v0.1 با تست کافی محقق شده است.\n- `partial`: بخشی از runtime موجود است ولی معیار کامل نیست.\n- `planned`: در runtime وجود ندارد و فقط طراحی/backlog است.\n- `pending_decision`: پیاده‌سازی به تصمیم مالک وابسته است.\n- `operational_unverified`: ابزار موجود است اما اجرای واقعی محیط هدف evidence ندارد.\n';
fs.writeFileSync(new URL('./V01_GAP_MATRIX.md', import.meta.url),md);
