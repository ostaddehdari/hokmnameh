-- HOKMNAMEH / PostgreSQL 18+
-- Canonical data model v1.0 | 2026-11-18
-- Production checklist: run with Flyway/DbUp transactional migrations, revoke DDL from app role,
-- enforce encrypted backups, least privilege, partition match_events/ad_events by month at scale.
-- NOTE: This schema is a reviewed starter design, not a substitute for production migrations and load tests.
BEGIN;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- [Identity | MVP] هویت کاربر، حساب مهمان/ثبت‌شده و وضعیت آن
CREATE TABLE IF NOT EXISTS accounts (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 status text NOT NULL DEFAULT 'active' CHECK(status IN ('active','restricted','banned','deleted')),
 is_guest boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz
);

-- [Identity | MVP] شماره همراه/شناسه بیرونی با هش جست‌وجو و مقدار رمزگذاری‌شده
CREATE TABLE IF NOT EXISTS auth_identities (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 provider text NOT NULL, identifier_hash bytea NOT NULL, identifier_ciphertext bytea,
 verified_at timestamptz, created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(provider,identifier_hash)
);

-- [Identity | MVP] نشست‌ها و توکن‌های تجدید، بدون ذخیره توکن خام
CREATE TABLE IF NOT EXISTS auth_sessions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 refresh_token_hash bytea NOT NULL UNIQUE, device_id uuid,
 expires_at timestamptz NOT NULL, revoked_at timestamptz,
 created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_auth_sessions_account ON auth_sessions(account_id,expires_at);

-- [Identity | MVP] فهرست دستگاه‌ها برای کنترل امنیت و دستگاه هم‌زمان
CREATE TABLE IF NOT EXISTS devices (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 install_id_hash bytea NOT NULL, platform text NOT NULL, app_version text,
 last_seen_at timestamptz, created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(account_id,install_id_hash)
);

-- [Identity | MVP] پروفایل عمومی، نام مستعار و سطح
CREATE TABLE IF NOT EXISTS profiles (
 account_id uuid PRIMARY KEY REFERENCES accounts(id), nickname text NOT NULL,
 nickname_normalized text NOT NULL UNIQUE, avatar_code text NOT NULL DEFAULT 'starter_01',
 level integer NOT NULL DEFAULT 1 CHECK(level >= 1), xp bigint NOT NULL DEFAULT 0 CHECK(xp >= 0),
 bio text, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now()
);

-- [Identity | MVP] تنظیمات زبان، صدا، اعلان، حریم خصوصی و گزینه‌های قابل دسترسی
CREATE TABLE IF NOT EXISTS player_settings (
 account_id uuid PRIMARY KEY REFERENCES accounts(id), language text NOT NULL DEFAULT 'fa',
 sound_enabled boolean NOT NULL DEFAULT true, music_enabled boolean NOT NULL DEFAULT true,
 allow_friend_requests boolean NOT NULL DEFAULT true,
 show_location boolean NOT NULL DEFAULT false,
 settings jsonb NOT NULL DEFAULT '{}'::jsonb, updated_at timestamptz NOT NULL DEFAULT now()
);

-- [Identity | MVP] موقعیت اختیاری و تأییدپذیر برای رتبه‌بندی محلی؛ محله دقیق عمومی نیست
CREATE TABLE IF NOT EXISTS player_geography (
 account_id uuid PRIMARY KEY REFERENCES accounts(id), country_code char(2), province_code text,
 city_code text, district_code text, source text NOT NULL DEFAULT 'user_input',
 verified_at timestamptz, visibility text NOT NULL DEFAULT 'private' CHECK(visibility IN ('private','city','province')),
 updated_at timestamptz NOT NULL DEFAULT now()
);

-- [Identity | MVP] محدودیت زمانی بر مبنای دلیل و قابلیت درخواست تجدیدنظر
CREATE TABLE IF NOT EXISTS account_restrictions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 restriction_type text NOT NULL, reason_code text NOT NULL, starts_at timestamptz NOT NULL DEFAULT now(),
 ends_at timestamptz, appeal_status text NOT NULL DEFAULT 'none', created_at timestamptz NOT NULL DEFAULT now()
);

-- [Social | P1] روابط دوستی با وضعیت درخواست/پذیرش
CREATE TABLE IF NOT EXISTS friendships (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), requester_id uuid NOT NULL REFERENCES accounts(id),
 addressee_id uuid NOT NULL REFERENCES accounts(id), status text NOT NULL DEFAULT 'pending',
 created_at timestamptz NOT NULL DEFAULT now(), accepted_at timestamptz,
 CHECK(requester_id <> addressee_id), UNIQUE(requester_id,addressee_id)
);

-- [Social | P1] مسدودسازی کاربران
CREATE TABLE IF NOT EXISTS blocked_accounts (
 blocker_id uuid NOT NULL REFERENCES accounts(id), blocked_id uuid NOT NULL REFERENCES accounts(id),
 created_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(blocker_id,blocked_id),
 CHECK(blocker_id <> blocked_id)
);

-- [Social | P1] باشگاه‌ها، قوانین عضویت و ظرفیت
CREATE TABLE IF NOT EXISTS clubs (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), owner_id uuid NOT NULL REFERENCES accounts(id),
 name text NOT NULL, tag text UNIQUE, description text, required_level integer NOT NULL DEFAULT 1,
 required_credibility integer NOT NULL DEFAULT 0,
 max_members integer NOT NULL DEFAULT 50, created_at timestamptz NOT NULL DEFAULT now()
);

-- [Social | P1] نقش‌ها و وضعیت عضویت
CREATE TABLE IF NOT EXISTS club_members (
 club_id uuid NOT NULL REFERENCES clubs(id), account_id uuid NOT NULL REFERENCES accounts(id),
 member_role text NOT NULL DEFAULT 'member', status text NOT NULL DEFAULT 'active',
 joined_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(club_id,account_id)
);

-- [Social | P1] گفت‌وگوی مستقیم/تیمی/باشگاهی
CREATE TABLE IF NOT EXISTS chat_rooms (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), room_type text NOT NULL,
 club_id uuid REFERENCES clubs(id), created_at timestamptz NOT NULL DEFAULT now()
);

-- [Social | P1] عضویت و زمان آخرین خواندن چت
CREATE TABLE IF NOT EXISTS chat_members (
 room_id uuid NOT NULL REFERENCES chat_rooms(id), account_id uuid NOT NULL REFERENCES accounts(id),
 joined_at timestamptz NOT NULL DEFAULT now(), last_read_at timestamptz,
 PRIMARY KEY(room_id,account_id)
);

-- [Social | P1] پیام متنی قابل گزارش/حذف نرم
CREATE TABLE IF NOT EXISTS chat_messages (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, room_id uuid NOT NULL REFERENCES chat_rooms(id),
 sender_id uuid NOT NULL REFERENCES accounts(id), content text NOT NULL CHECK(length(content)<=1000),
 moderation_status text NOT NULL DEFAULT 'visible', created_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz
);
CREATE INDEX ix_chat_room_timeline ON chat_messages(room_id,created_at DESC);

-- [Social | P1] گزارش تخلف بازی/چت/پروفایل
CREATE TABLE IF NOT EXISTS moderation_reports (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), reporter_id uuid REFERENCES accounts(id),
 target_account_id uuid REFERENCES accounts(id), match_id uuid, category text NOT NULL,
 evidence jsonb NOT NULL DEFAULT '{}'::jsonb, status text NOT NULL DEFAULT 'open',
 created_at timestamptz NOT NULL DEFAULT now()
);

-- [Game | MVP] زمین‌ها و سیاست رنک/آفلاین
CREATE TABLE IF NOT EXISTS game_modes (
 code text PRIMARY KEY, title text NOT NULL, ranked boolean NOT NULL DEFAULT false,
 online_required boolean NOT NULL DEFAULT true, bot_allowed boolean NOT NULL DEFAULT false,
 enabled boolean NOT NULL DEFAULT true, settings jsonb NOT NULL DEFAULT '{}'::jsonb
);

-- [Game | MVP] نسخه‌بندی قانون قابل بازپخش و تست
CREATE TABLE IF NOT EXISTS rule_sets (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL, version integer NOT NULL,
 definition jsonb NOT NULL, checksum text NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(), UNIQUE(code,version)
);

-- [Game | MVP] فصل‌های لیگ؛ برنامه مصوب سه‌ماهه
CREATE TABLE IF NOT EXISTS seasons (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL UNIQUE, starts_at timestamptz NOT NULL,
 ends_at timestamptz NOT NULL, state text NOT NULL DEFAULT 'planned',
 CHECK(ends_at > starts_at)
);

-- [Game | MVP] طبقات برنز تا اسطوره و آستانه کاپ
CREATE TABLE IF NOT EXISTS leagues (
 code text PRIMARY KEY, display_name text NOT NULL,
 sort_order integer NOT NULL UNIQUE, min_cups integer NOT NULL DEFAULT 0,
 max_cups integer, emblem_asset text
);

-- [Game | MVP] MMR پنهان، کاپ آشکار و آمار فصل
CREATE TABLE IF NOT EXISTS rank_profiles (
 season_id uuid NOT NULL REFERENCES seasons(id), account_id uuid NOT NULL REFERENCES accounts(id),
 mmr integer NOT NULL DEFAULT 1500, mmr_uncertainty numeric(8,2) NOT NULL DEFAULT 350,
 cups integer NOT NULL DEFAULT 0, wins integer NOT NULL DEFAULT 0,
 losses integer NOT NULL DEFAULT 0, league_code text REFERENCES leagues(code),
 updated_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(season_id,account_id)
);
CREATE INDEX ix_rank_season_cups ON rank_profiles(season_id,cups DESC);

-- [Game | MVP] اعتبار رفتاری مستقل از مهارت و سکه
CREATE TABLE IF NOT EXISTS player_credibility (
 account_id uuid PRIMARY KEY REFERENCES accounts(id), score integer NOT NULL DEFAULT 1000,
 completed_matches integer NOT NULL DEFAULT 0, abandons integer NOT NULL DEFAULT 0,
 updated_at timestamptz NOT NULL DEFAULT now()
);

-- [Game | MVP] دفتر حساب تغییر اعتبار و دلیل
CREATE TABLE IF NOT EXISTS credibility_events (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 match_id uuid, delta integer NOT NULL, reason_code text NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(), UNIQUE(account_id,match_id,reason_code)
);

-- [Game | MVP] تیم موقت دو نفره جهت جست‌وجوی رنک
CREATE TABLE IF NOT EXISTS parties (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), leader_id uuid NOT NULL REFERENCES accounts(id),
 status text NOT NULL DEFAULT 'open', created_at timestamptz NOT NULL DEFAULT now()
);

-- [Game | MVP] اعضای تیم از پیش تشکیل‌شده
CREATE TABLE IF NOT EXISTS party_members (
 party_id uuid NOT NULL REFERENCES parties(id), account_id uuid NOT NULL REFERENCES accounts(id),
 joined_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(party_id,account_id)
);

-- [Game | MVP] تیکت صف همتاسازی با اطلاعات دائمی حداقلی؛ صف زنده در Valkey
CREATE TABLE IF NOT EXISTS matchmaking_tickets (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 party_id uuid REFERENCES parties(id), mode_code text NOT NULL REFERENCES game_modes(code),
 season_id uuid REFERENCES seasons(id), region text,
 mmr_snapshot integer, credibility_snapshot integer,
 status text NOT NULL DEFAULT 'queued', created_at timestamptz NOT NULL DEFAULT now(),
 completed_at timestamptz
);
CREATE INDEX ix_tickets_queue ON matchmaking_tickets(mode_code,status,created_at);

-- [Game | MVP] سرورهای اجرا و تعداد میزهای تخصیص‌یافته
CREATE TABLE IF NOT EXISTS match_nodes (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), region text NOT NULL,
 endpoint text NOT NULL, state text NOT NULL DEFAULT 'healthy', capacity_rooms integer NOT NULL,
 active_rooms integer NOT NULL DEFAULT 0, heartbeat_at timestamptz NOT NULL DEFAULT now()
);

-- [Game | MVP] میز مسابقه مرجع؛ بدون واردکردن LAN به مسابقات رنک
CREATE TABLE IF NOT EXISTS matches (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), mode_code text NOT NULL REFERENCES game_modes(code),
 rule_set_id uuid NOT NULL REFERENCES rule_sets(id), season_id uuid REFERENCES seasons(id),
 node_id uuid REFERENCES match_nodes(id), state text NOT NULL DEFAULT 'created',
 team_a_score integer NOT NULL DEFAULT 0, team_b_score integer NOT NULL DEFAULT 0,
 started_at timestamptz, ended_at timestamptz, created_at timestamptz NOT NULL DEFAULT now(),
 CHECK(team_a_score>=0 AND team_b_score>=0)
);
CREATE INDEX ix_matches_running ON matches(state,created_at);

-- [Game | MVP] مالکیت صندلی، تیم، حضور و ربات جایگزین
CREATE TABLE IF NOT EXISTS match_seats (
 match_id uuid NOT NULL REFERENCES matches(id), seat smallint NOT NULL CHECK(seat BETWEEN 0 AND 3),
 account_id uuid REFERENCES accounts(id), team smallint NOT NULL CHECK(team IN (0,1)),
 joined_at timestamptz, left_at timestamptz, is_bot_substitute boolean NOT NULL DEFAULT false,
 PRIMARY KEY(match_id,seat), UNIQUE(match_id,account_id)
);

-- [Game | MVP] تعهد هش بذر برزدن؛ افشا فقط پس از پایان برای ممیزی
CREATE TABLE IF NOT EXISTS match_seed_commitments (
 match_id uuid PRIMARY KEY REFERENCES matches(id), seed_commitment char(64) NOT NULL,
 encrypted_seed bytea NOT NULL, revealed_seed text, revealed_at timestamptz
);

-- [Game | MVP] رخدادهای اقتدارگرایانه با ترتیب و شناسه درخواست یکتا؛ قابل بازپخش
CREATE TABLE IF NOT EXISTS match_events (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 match_id uuid NOT NULL REFERENCES matches(id), sequence_no integer NOT NULL,
 actor_seat smallint, event_type text NOT NULL, payload jsonb NOT NULL,
 command_id uuid, occurred_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(match_id,sequence_no), UNIQUE(match_id,command_id)
);
CREATE INDEX ix_match_events_match_id ON match_events(match_id,id);

-- [Game | MVP] چک‌پوینت وضعیت محرمانه برای بازیابی فرایند پس از خرابی
CREATE TABLE IF NOT EXISTS match_snapshots (
 match_id uuid NOT NULL REFERENCES matches(id), sequence_no integer NOT NULL,
 state_ciphertext bytea NOT NULL, checksum text NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(match_id,sequence_no)
);

-- [Game | MVP] نتیجه نهایی قطعی و بدون تکرار
CREATE TABLE IF NOT EXISTS match_results (
 match_id uuid PRIMARY KEY REFERENCES matches(id), winner_team smallint CHECK(winner_team IN (0,1)),
 result_code text NOT NULL, score_a integer NOT NULL, score_b integer NOT NULL,
 duration_seconds integer, finalized_at timestamptz NOT NULL DEFAULT now()
);

-- [Game | MVP] نتیجه و آمار هر بازیکن به‌ازای مسابقه
CREATE TABLE IF NOT EXISTS player_match_stats (
 match_id uuid NOT NULL REFERENCES matches(id), account_id uuid NOT NULL REFERENCES accounts(id),
 team smallint NOT NULL, won boolean NOT NULL,
 tricks_won integer NOT NULL DEFAULT 0, cards_played integer NOT NULL DEFAULT 0,
 disconnected_seconds integer NOT NULL DEFAULT 0, PRIMARY KEY(match_id,account_id)
);
CREATE INDEX ix_player_match_stats_account ON player_match_stats(account_id,match_id);

-- [Game | MVP] دفتر تغییر MMR و کاپ با نسخه الگوریتم
CREATE TABLE IF NOT EXISTS rating_changes (
 match_id uuid NOT NULL REFERENCES matches(id), account_id uuid NOT NULL REFERENCES accounts(id),
 old_mmr integer NOT NULL, new_mmr integer NOT NULL,
 old_cups integer NOT NULL, new_cups integer NOT NULL,
 algorithm_version text NOT NULL, created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(match_id,account_id)
);

-- [Game | P1] خلاصه امن بازپخش؛ دست‌های مخفی در زمان مسابقه فاش نشوند
CREATE TABLE IF NOT EXISTS match_replays (
 match_id uuid PRIMARY KEY REFERENCES matches(id), public_replay_uri text,
 private_replay_uri text, available_at timestamptz,
 created_at timestamptz NOT NULL DEFAULT now()
);

-- [Game | P1] نسخه‌های ثابت و قابل کش لیدربوردهای جغرافیایی
CREATE TABLE IF NOT EXISTS leaderboard_snapshots (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), season_id uuid REFERENCES seasons(id),
 metric text NOT NULL, scope text NOT NULL, scope_code text,
 snapshot jsonb NOT NULL, created_at timestamptz NOT NULL DEFAULT now()
);

-- [Economy | MVP] موجودی مادی‌شده سکه/الماس؛ فقط از طریق تراکنش سرور
CREATE TABLE IF NOT EXISTS wallets (
 account_id uuid NOT NULL REFERENCES accounts(id), currency text NOT NULL CHECK(currency IN ('coin','gem')),
 balance bigint NOT NULL DEFAULT 0 CHECK(balance >= 0), version bigint NOT NULL DEFAULT 0,
 updated_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(account_id,currency)
);

-- [Economy | MVP] دفتر غیرقابل‌ویرایش تمام صدور/مصرف‌ها و کلید idempotency
CREATE TABLE IF NOT EXISTS wallet_ledger (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 account_id uuid NOT NULL REFERENCES accounts(id), currency text NOT NULL CHECK(currency IN ('coin','gem')),
 delta bigint NOT NULL CHECK(delta <> 0), balance_after bigint NOT NULL CHECK(balance_after>=0),
 reason_code text NOT NULL, ref_type text NOT NULL, ref_id text NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(account_id,currency,ref_type,ref_id,reason_code)
);
CREATE INDEX ix_wallet_ledger_account_time ON wallet_ledger(account_id,created_at DESC);

-- [Economy | MVP] قواعد عددی پاداش/محدودیت با نسخه و بازه اعتبار
CREATE TABLE IF NOT EXISTS economy_configs (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL, version integer NOT NULL,
 config jsonb NOT NULL, valid_from timestamptz NOT NULL, valid_to timestamptz,
 UNIQUE(code,version)
);

-- [Economy | MVP] کاتالوگ آیتم‌های ظاهری/مدت‌دار/مصرفی، بدون pay-to-win
CREATE TABLE IF NOT EXISTS store_items (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), sku text NOT NULL UNIQUE, item_type text NOT NULL,
 name text NOT NULL, asset_key text, rarity text, expires_after_hours integer,
 is_active boolean NOT NULL DEFAULT true, metadata jsonb NOT NULL DEFAULT '{}'::jsonb
);

-- [Economy | MVP] قیمت نسخه‌دار به ارزهای داخل بازی
CREATE TABLE IF NOT EXISTS store_prices (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), item_id uuid NOT NULL REFERENCES store_items(id),
 currency text NOT NULL CHECK(currency IN ('coin','gem')),
 amount bigint NOT NULL CHECK(amount>=0), starts_at timestamptz NOT NULL DEFAULT now(),
 ends_at timestamptz
);

-- [Economy | MVP] مالکیت دارایی و انقضا
CREATE TABLE IF NOT EXISTS player_inventory (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 item_id uuid NOT NULL REFERENCES store_items(id), source_ref text NOT NULL,
 acquired_at timestamptz NOT NULL DEFAULT now(), expires_at timestamptz,
 state text NOT NULL DEFAULT 'active', UNIQUE(account_id,item_id,source_ref)
);
CREATE INDEX ix_inventory_active ON player_inventory(account_id,state,expires_at);

-- [Economy | MVP] تجهیز اسکین کارت/میز/قاب در هر اسلات
CREATE TABLE IF NOT EXISTS equipped_items (
 account_id uuid NOT NULL REFERENCES accounts(id), slot_code text NOT NULL,
 inventory_id uuid NOT NULL REFERENCES player_inventory(id),
 updated_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(account_id,slot_code)
);

-- [Economy | MVP] سفارش خرید با کلید یکتای کلاینت/سرور
CREATE TABLE IF NOT EXISTS store_orders (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 sku text NOT NULL, order_kind text NOT NULL, currency text,
 amount bigint NOT NULL DEFAULT 0, status text NOT NULL DEFAULT 'created',
 idempotency_key text NOT NULL UNIQUE, created_at timestamptz NOT NULL DEFAULT now()
);

-- [Economy | MVP] رسید درگاه؛ فقط شناسه مرجع، هرگز داده کارت بانکی
CREATE TABLE IF NOT EXISTS payment_transactions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), order_id uuid NOT NULL REFERENCES store_orders(id),
 gateway text NOT NULL, gateway_reference text,
 amount_minor bigint NOT NULL CHECK(amount_minor>=0),
 currency_code text NOT NULL, status text NOT NULL DEFAULT 'pending',
 verified_at timestamptz, created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(gateway,gateway_reference)
);

-- [Economy | MVP] حفاظت از صدور تکراری جایزه (بازی/چالش/آموزش/تبلیغ)
CREATE TABLE IF NOT EXISTS reward_grants (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 source_type text NOT NULL, source_id text NOT NULL, reward_code text NOT NULL,
 payload jsonb NOT NULL, granted_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(account_id,source_type,source_id,reward_code)
);

-- [Economy | MVP] تعریف مأموریت‌های روزانه، هفتگی و آموزشی
CREATE TABLE IF NOT EXISTS missions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL UNIQUE, mission_type text NOT NULL,
 criteria jsonb NOT NULL, reward jsonb NOT NULL, starts_at timestamptz, ends_at timestamptz,
 active boolean NOT NULL DEFAULT true
);

-- [Economy | MVP] پیشرفت مأموریت و ادعای جایزه
CREATE TABLE IF NOT EXISTS mission_progress (
 account_id uuid NOT NULL REFERENCES accounts(id), mission_id uuid NOT NULL REFERENCES missions(id),
 progress integer NOT NULL DEFAULT 0, completed_at timestamptz, claimed_at timestamptz,
 PRIMARY KEY(account_id,mission_id)
);

-- [Economy | MVP] استخر جوایز کارت شانسی، سقف و شرط شارژ با فعالیت
CREATE TABLE IF NOT EXISTS daily_luck_configs (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), active_on date NOT NULL,
 pool jsonb NOT NULL, max_draws integer NOT NULL, earn_rule jsonb NOT NULL,
 UNIQUE(active_on)
);

-- [Economy | MVP] قرعه‌کشی سروری با جایزه مدت‌دار و محدودیت تعداد
CREATE TABLE IF NOT EXISTS daily_luck_draws (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 config_id uuid NOT NULL REFERENCES daily_luck_configs(id),
 draw_no integer NOT NULL, reward jsonb NOT NULL,
 awarded_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(account_id,config_id,draw_no)
);

-- [Legacy | MVP] مجموعه تاج شکسته و پرتره‌های تالار پادشاهان
CREATE TABLE IF NOT EXISTS legacy_collections (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL UNIQUE, title text NOT NULL,
 collection_type text NOT NULL, total_pieces integer NOT NULL,
 reward_sku text, is_active boolean NOT NULL DEFAULT true, created_at timestamptz NOT NULL DEFAULT now()
);

-- [Legacy | MVP] قطعات ثابت و غیرتکراری هر مجموعه
CREATE TABLE IF NOT EXISTS legacy_piece_catalog (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), collection_id uuid NOT NULL REFERENCES legacy_collections(id),
 piece_index integer NOT NULL, asset_key text NOT NULL,
 UNIQUE(collection_id,piece_index)
);

-- [Legacy | MVP] مجموعه فعال با زمان قفل‌شدن آن در شروع مسابقه رنک
CREATE TABLE IF NOT EXISTS player_legacy_state (
 account_id uuid PRIMARY KEY REFERENCES accounts(id),
 active_collection_id uuid REFERENCES legacy_collections(id), updated_at timestamptz NOT NULL DEFAULT now()
);

-- [Legacy | MVP] قطعات قطعی دریافت‌شده از برد معتبر و فقط یک‌بار
CREATE TABLE IF NOT EXISTS player_legacy_pieces (
 account_id uuid NOT NULL REFERENCES accounts(id), piece_id uuid NOT NULL REFERENCES legacy_piece_catalog(id),
 source_match_id uuid NOT NULL REFERENCES matches(id),
 earned_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(account_id,piece_id), UNIQUE(account_id,source_match_id)
);

-- [Legacy | MVP] دریافت پاداش دائمی زیبایی و جلوگیری از Claim دوباره
CREATE TABLE IF NOT EXISTS legacy_claims (
 account_id uuid NOT NULL REFERENCES accounts(id), collection_id uuid NOT NULL REFERENCES legacy_collections(id),
 reward_grant_id uuid REFERENCES reward_grants(id), claimed_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(account_id,collection_id)
);

-- [Legacy | P2] سرنخ‌ها و معمای ریشه→موج→باد؛ توسعه فاز دوم
CREATE TABLE IF NOT EXISTS legacy_clue_progress (
 account_id uuid NOT NULL REFERENCES accounts(id), clue_code text NOT NULL,
 unlocked_at timestamptz NOT NULL DEFAULT now(), source_match_id uuid REFERENCES matches(id),
 PRIMARY KEY(account_id,clue_code)
);

-- [Ads | P1] حساب برندها و وضعیت قرارداد
CREATE TABLE IF NOT EXISTS advertisers (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), legal_name text NOT NULL,
 brand_name text NOT NULL, status text NOT NULL DEFAULT 'active',
 contact_ciphertext bytea, created_at timestamptz NOT NULL DEFAULT now()
);

-- [Ads | P1] جایگاه‌های مجاز؛ ممنوعیت تبلیغ وسط تصمیم‌گیری
CREATE TABLE IF NOT EXISTS ad_placements (
 code text PRIMARY KEY, title text NOT NULL,
 context text NOT NULL, user_interrupting boolean NOT NULL DEFAULT false,
 enabled boolean NOT NULL DEFAULT true
);

-- [Ads | P1] کمپین، هدف‌گیری، بودجه و ظرفیت رزرو
CREATE TABLE IF NOT EXISTS ad_campaigns (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), advertiser_id uuid NOT NULL REFERENCES advertisers(id),
 name text NOT NULL, starts_at timestamptz NOT NULL, ends_at timestamptz NOT NULL,
 status text NOT NULL DEFAULT 'draft', targeting jsonb NOT NULL DEFAULT '{}'::jsonb,
 budget_minor bigint NOT NULL DEFAULT 0, reserved_impressions bigint NOT NULL DEFAULT 0,
 CHECK(ends_at > starts_at)
);

-- [Ads | P1] نسخه و دارایی بنر/اسکین با تأیید محتوا
CREATE TABLE IF NOT EXISTS ad_creatives (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), campaign_id uuid NOT NULL REFERENCES ad_campaigns(id),
 placement_code text NOT NULL REFERENCES ad_placements(code),
 asset_uri text NOT NULL, creative_type text NOT NULL,
 review_status text NOT NULL DEFAULT 'pending', created_at timestamptz NOT NULL DEFAULT now()
);

-- [Ads | P1] اسکین رایگان تبلیغاتی که بازیکن می‌تواند تجهیز کند
CREATE TABLE IF NOT EXISTS sponsored_card_skins (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), advertiser_id uuid NOT NULL REFERENCES advertisers(id),
 item_id uuid NOT NULL UNIQUE REFERENCES store_items(id),
 campaign_id uuid REFERENCES ad_campaigns(id), active boolean NOT NULL DEFAULT true
);

-- [Ads | P1] فرصت نمایش/نمایش معتبر/کلیک، مجزا و قابل شمارش
CREATE TABLE IF NOT EXISTS ad_events (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 campaign_id uuid NOT NULL REFERENCES ad_campaigns(id),
 creative_id uuid REFERENCES ad_creatives(id), match_id uuid REFERENCES matches(id),
 account_id uuid REFERENCES accounts(id), placement_code text REFERENCES ad_placements(code),
 event_type text NOT NULL CHECK(event_type IN ('opportunity','impression','click','reward_verified')),
 dedupe_key text NOT NULL UNIQUE, occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_ad_events_reporting ON ad_events(campaign_id,occurred_at,event_type);

-- [Ads | P1] جام/لیگ اسپانسری و برند برگزارکننده
CREATE TABLE IF NOT EXISTS sponsored_events (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), advertiser_id uuid NOT NULL REFERENCES advertisers(id),
 title text NOT NULL, kind text NOT NULL, starts_at timestamptz NOT NULL,
 ends_at timestamptz NOT NULL, eligibility jsonb NOT NULL,
 rules jsonb NOT NULL, status text NOT NULL DEFAULT 'draft',
 CHECK(ends_at > starts_at)
);

-- [Ads | P1] ثبت‌نام تیم/بازیکن بر اساس شرایط سطح/کاپ/اعتبار
CREATE TABLE IF NOT EXISTS tournament_entries (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), event_id uuid NOT NULL REFERENCES sponsored_events(id),
 account_id uuid NOT NULL REFERENCES accounts(id), party_id uuid REFERENCES parties(id),
 status text NOT NULL DEFAULT 'registered', registered_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(event_id,account_id)
);

-- [Ads | P1] اتصال مسابقه واقعی به براکت تورنمنت
CREATE TABLE IF NOT EXISTS tournament_matches (
 event_id uuid NOT NULL REFERENCES sponsored_events(id), match_id uuid NOT NULL REFERENCES matches(id),
 round_index integer NOT NULL, bracket_slot integer NOT NULL,
 PRIMARY KEY(event_id,match_id), UNIQUE(event_id,round_index,bracket_slot)
);

-- [Ops | MVP] خاموش‌کردن قابلیت پرخطر بدون انتشار اپ جدید
CREATE TABLE IF NOT EXISTS feature_flags (
 key text PRIMARY KEY, enabled boolean NOT NULL DEFAULT false,
 rollout_percent smallint NOT NULL DEFAULT 0 CHECK(rollout_percent BETWEEN 0 AND 100),
 conditions jsonb NOT NULL DEFAULT '{}'::jsonb, updated_at timestamptz NOT NULL DEFAULT now()
);

-- [Ops | MVP] پیکربندی زمان‌دار اقتصاد/قوانین/تبلیغات
CREATE TABLE IF NOT EXISTS configuration_versions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), namespace text NOT NULL,
 version integer NOT NULL, payload jsonb NOT NULL, checksum text NOT NULL,
 effective_at timestamptz NOT NULL DEFAULT now(), UNIQUE(namespace,version)
);

-- [Ops | MVP] پنل با اتصال به هویت و وضعیت نقش مدیر
CREATE TABLE IF NOT EXISTS admin_users (
 account_id uuid PRIMARY KEY REFERENCES accounts(id),
 status text NOT NULL DEFAULT 'active', mfa_required boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now()
);

-- [Ops | MVP] نقش‌ها: مدیرکل، مالی، تبلیغات، پشتیبانی، نظارت
CREATE TABLE IF NOT EXISTS admin_roles (
 code text PRIMARY KEY, title text NOT NULL, permissions jsonb NOT NULL
);

-- [Ops | MVP] انتساب نقش با قابلیت ابطال
CREATE TABLE IF NOT EXISTS admin_role_assignments (
 account_id uuid NOT NULL REFERENCES admin_users(account_id),
 role_code text NOT NULL REFERENCES admin_roles(code),
 granted_at timestamptz NOT NULL DEFAULT now(), revoked_at timestamptz,
 PRIMARY KEY(account_id,role_code)
);

-- [Ops | MVP] ثبت تغییرات حساس مدیران با داده قبل/بعد حداقلی
CREATE TABLE IF NOT EXISTS audit_logs (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, actor_id uuid REFERENCES accounts(id),
 action text NOT NULL, entity_type text NOT NULL, entity_id text NOT NULL,
 change_data jsonb NOT NULL DEFAULT '{}'::jsonb, ip_hash bytea,
 created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_audit_entity ON audit_logs(entity_type,entity_id,created_at DESC);

-- [Ops | MVP] انتشار قابل اطمینان رویدادهای تجاری بدون دوگانگی DB/صف
CREATE TABLE IF NOT EXISTS outbox_events (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 aggregate_type text NOT NULL, aggregate_id text NOT NULL,
 event_type text NOT NULL, payload jsonb NOT NULL,
 created_at timestamptz NOT NULL DEFAULT now(),
 dispatched_at timestamptz, retry_count integer NOT NULL DEFAULT 0
);
CREATE INDEX ix_outbox_pending ON outbox_events(created_at) WHERE dispatched_at IS NULL;

-- [Ops | P1] ارسال اعلان با صف و جلوگیری از ارسال چندباره
CREATE TABLE IF NOT EXISTS notification_jobs (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), account_id uuid NOT NULL REFERENCES accounts(id),
 template_code text NOT NULL, payload jsonb NOT NULL DEFAULT '{}'::jsonb,
 status text NOT NULL DEFAULT 'pending', dedupe_key text UNIQUE,
 scheduled_at timestamptz NOT NULL DEFAULT now(), sent_at timestamptz
);

-- Deferred foreign keys: source tables precede referenced tables in the bootstrap script.
ALTER TABLE auth_sessions ADD CONSTRAINT fk_sessions_device FOREIGN KEY(device_id) REFERENCES devices(id);
ALTER TABLE moderation_reports ADD CONSTRAINT fk_reports_match FOREIGN KEY(match_id) REFERENCES matches(id);
ALTER TABLE credibility_events ADD CONSTRAINT fk_credibility_match FOREIGN KEY(match_id) REFERENCES matches(id);

-- Seed data / note: monetary rates remain adjustable via configuration_versions.
INSERT INTO game_modes(code,title,ranked,online_required,bot_allowed) VALUES
 ('training','آموزش و ربات',false,false,true),
 ('casual','آزاد',false,true,false),
 ('ranked','رنک',true,true,false),
 ('sponsor_tournament','تورنمنت اسپانسری',false,true,false),
 ('lan','شبکه محلی',false,false,true),
 ('arena','حکم آرنا - فاز دوم',false,true,false)
ON CONFLICT (code) DO NOTHING;
INSERT INTO leagues(code,display_name,sort_order,min_cups) VALUES
 ('bronze','برنز',1,0), ('silver','نقره',2,500), ('gold','طلا',3,1200),
 ('diamond','الماس',4,2200), ('master','استاد',5,3500), ('legend','اسطوره',6,5000)
ON CONFLICT (code) DO NOTHING;
COMMIT;
