# 0000 — کنترل ساخت و شواهد آغاز پروژه حکمنامه

این پوشه نقطه ورود انسانی/ماشینی برای ساخت پروژه است؛ کد runtime را کپی نمی‌کند و مسیر واقعی هر قابلیت را به فایل منبع آن ارجاع می‌دهد.

- `V01_GAP_MATRIX.md`: گزارش خواندنی وضعیت واقعی v0.1.
- `v01-gap-matrix.json`: قرارداد ماشین‌خوان ۳۳ الزام → مسیر کد → وضعیت → شکاف → وابستگی.
- `generate-gap-matrix.mjs`: تولید deterministic دو خروجی بالا از پروپوزال و ارزیابی مخزن.
- `verify-gap-matrix.mjs`: کنترل پوشش کامل الزام‌ها، یکتایی IDها، وضعیت‌ها و وجود مسیرهای شاهد.
- `change-control/`: فرایند رسمی Change Request، schema، template، registry و verifier برای Rules/Economy/API/Database/UX.
- `../docs/product_domains_v1.json`: قرارداد ماشین‌خوان چهار زمین، مرز Arena و سیاست عدم فروش برتری.
- `../docs/e04_decision_set_v1.json`: قرارداد مصوب Dealer/Cutter/Deal/Hokm/Crypto/Reveal و گیت fail-closed رنک.

اجرا:

```bash
node 0000/generate-gap-matrix.mjs
node 0000/verify-gap-matrix.mjs
node 0000/change-control/verify-change-control.mjs --self-test
```

اسکریپت‌های نصب، تست و rollback هر Task طبق قرارداد در `deployment-center/projects/hokmnameh/stages/` باقی می‌مانند. هیچ secret، دیتابیس، asset پولی یا لاگ خام در این پوشه قرار نمی‌گیرد.
