# E‑04 Decision Set v1

وضعیت: **APPROVED** — `DEC-2026-0006`، مصوب مالک پروژه در ۲۴ نوامبر ۲۰۲۶.

مرجع الزام‌آور ماشین‌خوان [`e04_decision_set_v1.json`](./e04_decision_set_v1.json) و بردار رسمی [`e04_test_vector_v1.json`](./e04_test_vector_v1.json) است. مقدار ناشناخته، قرارداد ناشناخته یا Algorithm ID متفاوت باید ایجاد مسابقه رنک را رد کند.

## Dealer، Cutter و Deal

- Dealer در v1 یک نقش بازیکنی نیست؛ Game Authority سرور تنها مرجع Shuffle، Cut و Deal است.
- Cutter بازیکن بعدیِ حاکم در جهت خلاف عقربه‌های ساعت است: `(hakemSeat + 3) % 4`؛ او Deck را نمی‌بیند و فقط یک Cut ثبت می‌کند.
- Cut چرخش دوری `deck[k:] + deck[:k]` با `1 ≤ k ≤ 51` است.
- اولین گیرنده حاکم و ترتیب صندلی‌ها خلاف عقربه‌های ساعت با delta برابر `-1` است.
- توزیع برای هر صندلی سه مرحله `۵، ۴، ۴` دارد؛ Cursorها به‌ترتیب `۲۰، ۳۶، ۵۲` هستند و Shuffle مجدد ممنوع است.
- نخستین دست را حاکم آغاز می‌کند.

## Deadline و fallback

- مهلت Cut برابر ۸ ثانیه است. قطع اتصال زمان را تمدید نمی‌کند؛ fallback یک اندیس یکنواخت `1..51` از دامنه HMAC مستقل و قابل بازتولید است.
- مهلت انتخاب حکم ۲۰ ثانیه است. قطع اتصال زمان را تمدید نمی‌کند؛ fallback خالی با بیشترین تعداد در پنج کارت حاکم را انتخاب می‌کند و ترتیب tie-break برابر پیک، دل، خشت، گشنیز است.
- درخواست دیرهنگام، تکراری یا با state قدیمی رد می‌شود و deadline را از نو شروع نمی‌کند.

## رمزنگاری و Canonical Encoding

Algorithm ID برابر `E04-HKDF-SHA256-HMACCTR-RS-FY-v1` است: Seed تصادفی ۲۵۶بیتی و Nonce تصادفی ۱۲۸بیتی، HKDF-SHA256 مطابق RFC 5869، جریان HMAC-SHA256 شمارنده‌ای، Rejection Sampling بدون modulo bias و Fisher–Yates نزولی. همه اعداد Canonical Context به‌صورت unsigned big-endian ثبت می‌شوند و UUID در ترتیب شبکه/RFC 4122 است.

Commit پیش از نمایش یا توزیع هر کارت ثبت می‌شود. هیچ MMR، پرداخت، تبلیغ، Skin، دستگاه یا Frame Rate ورودی RNG نیست.

## Reveal و retention

Reveal فقط وقتی مجاز است که راند `ROUND_CLOSED`، امکان Resume خاموش و state نهایی پایدار شده باشد. بسته اثبات برای بازیکنان همان راند و Auditor مجاز ۳۰ روز در دسترس است. Seed، Deck و Hand خصوصی رمزگذاری‌شده حداکثر ۳۰ روز نگهداری و سپس با Cryptographic Erasure حذف می‌شوند؛ Hashهای غیرمحرمانه ممیزی ۳۶۵ روز باقی می‌مانند. لاگ عمومی هرگز Secret خام ندارد و Backup همان تاریخ انقضا را رعایت می‌کند.

این Work قرارداد را قفل می‌کند؛ پیاده‌سازی production موتور SecureShuffle/CardCut/DealPlanner در Stage S02 انجام می‌شود.
