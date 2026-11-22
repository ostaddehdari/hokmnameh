# فرایند رسمی Change Request حکمنامه

## اصل غیرقابل‌دورزدن

هیچ تغییر در قوانین بازی، اقتصاد، API، دیتابیس یا UX بدون یک `CR-YYYY-NNNN` ثبت‌شده، `decision_id` یکتا، تحلیل اثر، تست، rollback و تأیید نقش‌های لازم وارد کد نمی‌شود. وضعیت‌های `DRAFT`، `TRIAGE` و `IMPACT_ASSESSED` اجازه merge نمی‌دهند.

## چرخه وضعیت

`DRAFT → TRIAGE → IMPACT_ASSESSED → APPROVED → IMPLEMENTING → VERIFIED → RELEASED`

مسیرهای پایان جایگزین: `REJECTED` یا `SUPERSEDED`. تغییر وضعیت باید با evidence در همان entry ثبت شود؛ حذف تاریخچه ممنوع است.

## حداقل بررسی برحسب دامنه

| دامنه | تأییدهای الزامی | کنترل اختصاصی |
|---|---|---|
| Rules | Product Owner، Game Architect، QA | ruleset version، golden fixtures، عدم نشت اطلاعات |
| Economy | Product Owner، Economy Owner، Security | double-entry، idempotency، عدم Pay-to-Win/تورم |
| API | Architecture Owner، API Consumer Owner | OpenAPI، compatibility، status/error، auth |
| Database | Data Owner، Operations Owner | migration/backfill/lock، backup، rollback/restore |
| UX | Product Owner، UX Owner، Accessibility Owner | journey، RTL/device matrix، accessibility |

هر `risk_flag` حساس مانند `pii`، `currency`، `destructive_migration` یا `hidden_information` بررسی امنیتی صریح می‌خواهد. تغییر production gateway یا migration برگشت‌ناپذیر علاوه بر CR به تأیید اجرای تولید نیاز دارد.

## مسیر اضطراری

Emergency مجوز حذف ثبت نیست. CR اضطراری باید پیش از اجرا ID، علت، دامنه، rollback و مسئول داشته باشد؛ فقط حداقل patch ایمن اجرا شود و بازبینی کامل حداکثر تا ۲۴ ساعت ثبت گردد. نبود post-review به معنی blocker انتشار بعدی است.

## روش استفاده

1. `change-request.template.json` را کپی و IDهای واقعی اختصاص دهید.
2. تحلیل اثر و مسیرهای درگیر را کامل کنید؛ wildcard مبهم یا ریشه مخزن مجاز نیست.
3. تأییدهای نقش‌های لازم را با evidence ثبت کنید.
4. entry را بدون حذف تاریخچه به `registry.json` بیفزایید.
5. پیش از PR اجرا کنید:

```bash
node 0000/change-control/verify-change-control.mjs
node 0000/change-control/verify-change-control.mjs --changed-files changed-files.txt
```

فایل `changed-files.txt` باید یک مسیر Git در هر خط داشته باشد. verifier برای هر مسیر protected یک CR واجد شرایط و پوشش مسیر می‌خواهد. پوشش بر پایه prefix دقیق است؛ `backend/` عمومی برای دورزدن review توصیه/پذیرفته نمی‌شود.

## Definition of Done تغییر

CR تأییدشده، commit/PR ردیابی‌شده، معیارهای پذیرش پاس، migration/API/docs هماهنگ، security/observability بررسی، rollback قابل اجرا و state مرکزی دارای evidence واقعی باشد. `APPROVED` به‌تنهایی به معنی اجرا یا انتشار موفق نیست.
