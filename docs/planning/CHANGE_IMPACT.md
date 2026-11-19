# Hokmnameh — Product Change Impact Register

> Baseline: GitHub main `46364593b083` (v0.1). Source caveat: private ChatGPT conversation link inaccessible, so owner edits cannot be directly diffed. Facts below are recoverable prior decisions, NOT a statement that every late edit was viewed.

## Scope reconciliation

### Approved / project-recorded constraints
- **سیاست محصول ثبت‌شده:** ۴ زمین: آکادمی، آزاد، رنک، جام اسپانسر
- **اقتصاد و دسترسی:** ورود آزاد و رنک رایگان و نامحدود
- **اقتصاد:** بازی با بات فقط XP؛ منبع تولید سکه نیست
- **همتاسازی:** رنک Solo و Duo، ترجیح مسابقه هم‌نوع
- **لیگ و فصل:** ۶ لیگ و فصل سه‌ماهه تقویمی
- **اقتصاد:** VIP حذف و هیچ خریدی مزیت رقابتی ندارد
- **Trust & Safety:** روی میز چت آزاد ممنوع؛ پیام آماده تحت کنترل
- **مدیریت مسابقه:** ترک بازیکن: ربات جایگزین + پیامد متناسب
- **روایت و حفظ بازیکن:** تاج شکسته و تالار پادشاهان فراموش‌شده
- **Unity UI:** لابی الهام‌گرفته از چیدمان Mobile Legends با هویت مستقل
- **Unity UI:** داشبورد مجزا و شیشه‌ای روی لابی؛ پروفایل/Level/آواتار/اعتبار مستقل
- **Rulesets:** حکم آرنا در فاز دوم، جدا از کلاسیک
- **AI-01:** هوش کیفیت بازی نباید با اطلاعات مخفی یا در مسابقه زنده تقلب کند

### Needs owner confirmation / not inferred as approval
- **Ruleset v1:** گردش حاکم و فرمول دقیق کُت/امتیاز در همه نوع بازی
- **Trust & Safety:** دقیق‌ترین سیاست اعتراض و تنبیه قطع غیرعمدی
- **Source-of-truth:** ثبت تاریخ و متن نهایی نسخه ویرایش‌شده صاحبکار

## Feature delta against current GitHub source

| Capability | Repo now | Work plan | Layer |
|---|---|---|---|
| موتور اصلی حکم و توزیع امن | موجود پایه؛ چند قاعده نیازمند تأیید | S00/S02 | Core C# |
| ثبت ماندگار رخداد، replay و checkpoint | فاقد پیاده‌سازی کامل | S02/S04 | Backend+DB |
| WebSocket مطمئن، reconnect و resume | سرویس push اولیه؛ Unity polling | S04 | Backend+Unity |
| LAN چهار گوشی و میزبان اندرویدی | نمونه اولیه؛ فاقد تست واقعی دستگاه | S05 | Unity+Core |
| ربات چندسطحی و انصاف دانش | ربات heuristic پایه | S05/S11 | Core/AI |
| هویت تلفنی/امنیت نشست | حساب مهمان پایه | S03 | Backend+DB |
| آکادمی و بازی بدون سکه با بات | تمرین پایه؛ پاداش پیاده نشده | S03/S05 | Backend+Unity |
| لابی و داشبورد شیشه‌ای | UI placeholder IMGUI | S06 | Unity |
| کامپوننت‌های مستقل پروفایل/لول/آواتار/اعتبار | در UI فعلی پیاده نشده | S03/S06 | Unity+API |
| میز ۷۵ درجه و انیمیشن‌های کارت | در UI فعلی پیاده نشده | S06 | Unity |
| رنک Solo/Duo و هم‌نوع سازی | پیاده نشده | S07 | Backend+DB+Unity |
| شش لیگ و فصل سه‌ماهه | پیاده نشده | S07 | Backend+DB+Unity |
| اعتبار رفتاری/ربات جانشین و مجازات | timeout bot پایه؛ رسیدگی ناقص | S04/S07 | Backend+Moderation |
| Economy Ledger/سکه/الماس/فروشگاه | پیاده نشده | S08 | Backend+DB+Unity |
| کارت شانسی روزانه و آیتم زمان‌دار | پیاده نشده | S08 | Backend+DB+Unity |
| تاج شکسته و تالار پادشاهان | پیاده نشده | S08 | Backend+Unity |
| دوستان/باشگاه/لیدربورد محلی | پیاده نشده | S09 | Backend+Unity |
| چت اجتماعی ایمن و استیکر میز | پیاده نشده | S09 | Backend+Unity |
| کارت تبلیغی و سیستم اسپانسر | پیاده نشده | S10 | Backend+Admin+Unity |
| تورنمنت اسپانسری | پیاده نشده | S10 | Backend+Admin+Unity |
| AI-01 ارزیابی کیفیت تصمیم | پیاده نشده | S11 | AI+Backend+Unity |
| مقیاس هزاران اتصال/Valkey/Workers | فاقد benchmark و شاردینگ | S12 | Backend+OPS |
| حکم آرنا مستقل | پیاده نشده؛ فاز دوم | S14 | Core+Backend+Unity |

## Implementation notes

- Prioritize deterministic rules and event log before ranked economy/AI reports; match worker remains authoritative.
- Preserve no-cost free/ranked entry, no VIP, no coin grants from repeated bot play, no free text at the table.
- AI-01 Ex-Ante analysis only from the legal observation at the decision instant; post-hoc replay must not pollute decision score.
- Do not merge Arena abilities into classic rules; independent versioned ruleset.
- LAN host mode must not mint authoritative online progress and needs true four-device testing.
- Avoid adding ORM/networking framework until benchmark/test justifies operational complexity.

## Repo changes made in this planning update

- Added `docs/planning/STAGES_WORKS_TASKS.md` (full 15-stage / 45-work / 135-task backlog).
- Added `docs/planning/CHANGE_IMPACT.md` (traceability and caveats).
- Added `docs/planning/backlog.csv` and `docs/planning/backlog.json` (machine-readable ticket import).
- Added `AGENTS.md` (development contract).
- Added `docs/planning/DECISIONS_TO_CONFIRM.md` (blocking decision checklist).
- Added the Word plan `docs/planning/Hokmnameh_Stage_Work_Task_Development_Plan.docx` if binary upload succeeds.
- No unreviewed game/runtime behavior is changed in this planning commit.