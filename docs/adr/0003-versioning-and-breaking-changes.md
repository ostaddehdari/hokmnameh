# ADR-0003 — Version contracts and breaking changes

Status: **ACCEPTED** · 2026-11-24 · Stage `S01-W01-T03`.

## Context

API، Shared Core، RuleSet، E‑04، Database و Event payload چرخه انتشار یکسان ندارند. یک شماره مبهم برای کل سامانه نمی‌تواند سازگاری مسابقه فعال یا کلاینت قدیمی را تضمین کند.

## Decision

- نسخه انتشار مخزن از Semantic Versioning 2.0.0 و tagهای `vMAJOR.MINOR.PATCH` استفاده می‌کند.
- API ناسازگار فقط با route major جدید، contract test و سیاست حداقل نسخه کلاینت منتشر می‌شود؛ `/api/v1` درجا شکسته نمی‌شود.
- Shared Core یک Unity package مستقل با SemVer است. تغییر public API ناسازگار نیازمند major جدید و راهنمای مهاجرت مصرف‌کننده است.
- RuleSet و قرارداد E‑04 immutable ID دارند. مسابقه فعال ID ثبت‌شده هنگام ایجاد را تا پایان حفظ می‌کند؛ شناسه ناشناخته fail-closed است.
- Migration دیتابیس forward-only، شماره‌دار و immutable است. فایل اعمال‌شده ویرایش یا rename نمی‌شود؛ اصلاح با migration بعدی است.
- Event payload ناسازگار major جدید می‌گیرد و تا پایان migration، upcaster یا consumer موازی دارد.

مرجع ماشین‌خوان [`version-contract.json`](../../version-contract.json) و رجیستری ساختار [`repository.contract.json`](../../repository.contract.json) هستند.

## Required pull-request evidence

هر تغییر نسخه باید Task/CR، نسخه قبل و بعد، compatibility impact، تست مصرف‌کننده و rollback/forward-recovery را ثبت کند. ادعای Breaking نبودن بدون contract test پذیرفته نیست.

## Consequences

نسخه‌های API، Core، Rules و Schema عمداً می‌توانند مستقل حرکت کنند. هزینه مدیریت چند نسخه افزایش می‌یابد، اما مسابقه جاری، کلاینت قدیمی و داده اعمال‌شده به‌صورت silent نمی‌شکنند.
