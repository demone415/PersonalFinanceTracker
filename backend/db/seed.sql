-- ============================================================================
-- Finance Tracker — demo seed data (Docker-only).
--
-- Run by the one-shot `db-seed` compose service AFTER:
--   • the backend has applied EF Core migrations (public.* tables exist), and
--   • GoTrue has migrated the auth.* schema (auth.users / auth.identities exist).
--
-- Idempotent: the whole seed is skipped when public.user_profiles already has
-- rows, so repeated `docker compose up` runs are no-ops.
--
-- Creates:
--   • 3 login users (user@ / family@ / admin@, password "Password123!")
--   • 300 accruals total — 150 for each regular user, ~50/month over
--     April / May / June 2026
--   • receipts with line items for the "checkable" categories
--     (Продукты, Кафе и рестораны, Одежда)
--   • monthly budgets for both regular users across all three months
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;          -- crypt() / gen_salt()
SET search_path = public, auth, extensions, pg_temp;

-- ── Temp helpers (live only for this psql session) ──────────────────────────

-- Random monetary value in [min,max], 2 decimals.
CREATE OR REPLACE FUNCTION pg_temp.rnd(p_min numeric, p_max numeric)
RETURNS numeric LANGUAGE sql AS $$
  SELECT round((p_min + (p_max - p_min) * random())::numeric, 2);
$$;

-- A random timestamp within month p_month (first of month), day in 1..p_max_day,
-- at a plausible day-time. Used so June stays within the current date.
CREATE OR REPLACE FUNCTION pg_temp.rand_day(p_month date, p_max_day int)
RETURNS timestamptz LANGUAGE sql AS $$
  SELECT (p_month
          + (floor(random() * p_max_day))::int * interval '1 day'
          + (8 + floor(random() * 12))::int   * interval '1 hour'
          + (floor(random() * 60))::int       * interval '1 minute')::timestamptz;
$$;

-- Pick a random element from a text array.
CREATE OR REPLACE FUNCTION pg_temp.pick(p_arr text[])
RETURNS text LANGUAGE sql AS $$
  SELECT p_arr[1 + floor(random() * array_length(p_arr, 1))::int];
$$;

-- Insert one accrual.
CREATE OR REPLACE FUNCTION pg_temp.add_accrual(
  p_user uuid, p_amount numeric, p_date timestamptz, p_type int,
  p_cat uuid, p_desc text, p_currency text DEFAULT 'RUB',
  p_rate numeric DEFAULT NULL, p_receipt uuid DEFAULT NULL)
RETURNS void LANGUAGE sql AS $$
  INSERT INTO public.accruals
    ("Id","UserId","Amount","Date","Type","Currency","ExchangeRate",
     "CategoryId","Description","IncludeInStats","GroupId","ReceiptId","CreatedAt")
  VALUES
    (gen_random_uuid(), p_user, p_amount, p_date, p_type, p_currency, p_rate,
     p_cat, p_desc, true, NULL, p_receipt, now());
$$;

-- Insert a receipt (Fetched) with N line items, then an accrual linked to it.
-- The accrual amount equals the sum of the items, keeping aggregates consistent.
CREATE OR REPLACE FUNCTION pg_temp.add_receipt_accrual(
  p_user uuid, p_date timestamptz, p_cat uuid,
  p_org text, p_items text[],
  p_price_min numeric, p_price_max numeric, p_qty_max numeric,
  p_min_items int, p_max_items int)
RETURNS void LANGUAGE plpgsql AS $$
DECLARE
  v_receipt uuid := gen_random_uuid();
  v_n     int := p_min_items + floor(random() * (p_max_items - p_min_items + 1))::int;
  v_total numeric := 0;
  v_price numeric;
  v_qty   numeric;
  v_sum   numeric;
  i int;
BEGIN
  INSERT INTO public.receipts
    ("Id","UserId","AmountInKopecks","Date","Organization","FetchStatus","FetchAttempts")
  VALUES (v_receipt, p_user, 0, p_date, p_org, 1, 0);   -- FetchStatus 1 = Fetched

  FOR i IN 1..v_n LOOP
    v_price := pg_temp.rnd(p_price_min, p_price_max);
    v_qty   := CASE WHEN p_qty_max <= 1 THEN 1
                    ELSE round((1 + random() * (p_qty_max - 1))::numeric, 3) END;
    v_sum   := round(v_price * v_qty, 2);
    v_total := v_total + v_sum;
    INSERT INTO public.receipt_items ("Id","ReceiptId","Name","Price","Quantity","Sum")
    VALUES (gen_random_uuid(), v_receipt, pg_temp.pick(p_items), v_price, v_qty, v_sum);
  END LOOP;

  UPDATE public.receipts
     SET "AmountInKopecks" = round(v_total * 100)::bigint
   WHERE "Id" = v_receipt;

  PERFORM pg_temp.add_accrual(p_user, v_total, p_date, 3 /* Expense */, p_cat, p_org,
                              'RUB', NULL, v_receipt);
END;
$$;

-- Create a GoTrue user (auth.users + auth.identities) with an email/password login.
CREATE OR REPLACE FUNCTION pg_temp.add_user(
  p_id uuid, p_email text, p_name text, p_role text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
  -- The token/email-change columns have no DB default; GoTrue scans them into Go
  -- strings, so leaving them NULL breaks login ("Database error querying schema").
  -- Set them to '' explicitly.
  INSERT INTO auth.users (
    instance_id, id, aud, role, email, encrypted_password, email_confirmed_at,
    created_at, updated_at, raw_app_meta_data, raw_user_meta_data,
    is_sso_user, is_anonymous,
    confirmation_token, recovery_token, email_change, email_change_token_new,
    email_change_token_current, phone_change, phone_change_token, reauthentication_token)
  VALUES (
    '00000000-0000-0000-0000-000000000000', p_id, 'authenticated', 'authenticated',
    p_email, crypt('Password123!', gen_salt('bf')), now(),
    now(), now(),
    jsonb_build_object('provider', 'email',
                       'providers', jsonb_build_array('email'),
                       'role', p_role),
    jsonb_build_object('display_name', p_name),
    false, false,
    '', '', '', '', '', '', '', '')
  ON CONFLICT (id) DO NOTHING;

  INSERT INTO auth.identities (
    id, provider_id, user_id, identity_data, provider,
    last_sign_in_at, created_at, updated_at)
  VALUES (
    gen_random_uuid(), p_id::text, p_id,
    jsonb_build_object('sub', p_id::text, 'email', p_email,
                       'email_verified', true, 'phone_verified', false),
    'email', now(), now(), now())
  ON CONFLICT DO NOTHING;
END;
$$;

-- ── Seed body ───────────────────────────────────────────────────────────────
DO $$
DECLARE
  -- System category ids (must match CategoryConfiguration.cs / AddCategories migration)
  c_groceries uuid := 'a1c00000-0000-7000-8000-000000000001';
  c_rest      uuid := 'a1c00000-0000-7000-8000-000000000002';
  c_transport uuid := 'a1c00000-0000-7000-8000-000000000003';
  c_housing   uuid := 'a1c00000-0000-7000-8000-000000000004';
  c_health    uuid := 'a1c00000-0000-7000-8000-000000000005';
  c_enter     uuid := 'a1c00000-0000-7000-8000-000000000006';
  c_clothing  uuid := 'a1c00000-0000-7000-8000-000000000007';
  c_comms     uuid := 'a1c00000-0000-7000-8000-000000000008';
  c_salary    uuid := 'a1c00000-0000-7000-8000-00000000000b';
  c_other     uuid := 'a1c00000-0000-7000-8000-00000000000c';

  -- Fixed user ids (enumerable, never secret — see CLAUDE.md)
  u_ivan  uuid := '11111111-1111-7111-8111-111111111111';
  u_anna  uuid := '22222222-2222-7222-8222-222222222222';
  u_admin uuid := '33333333-3333-7333-8333-333333333333';

  -- Name pools
  groceries_stores text[] := ARRAY['Магнит','Перекрёсток','ВкусВилл','Пятёрочка','Лента','Ашан','Дикси'];
  groceries_items  text[] := ARRAY['Молоко','Хлеб','Яйца','Сыр','Куриное филе','Овощи','Фрукты','Йогурт','Макароны','Кофе','Масло','Творог','Печенье','Сок','Рыба'];
  rest_places      text[] := ARRAY['Кафе «Уют»','Шаурмячная','Суши-бар','Пицца Хаус','Burger King','KFC','Теремок'];
  rest_items       text[] := ARRAY['Капучино','Цезарь','Паста Карбонара','Пицца Маргарита','Бургер','Ролл Филадельфия','Суп дня','Десерт','Лимонад','Стейк'];
  clothing_stores  text[] := ARRAY['Zara','H&M','Lamoda','Wildberries','O''STIN','Befree','Gloria Jeans'];
  clothing_items   text[] := ARRAY['Футболка','Джинсы','Кроссовки','Куртка','Рубашка','Свитер','Носки','Платье','Шарф','Ремень'];
  transport_desc   text[] := ARRAY['Яндекс.Такси','Метро','Каршеринг','Автобус','Ситимобил','Электричка'];
  health_desc      text[] := ARRAY['Аптека','Клиника','Стоматология','Анализы','Очки / линзы','Массаж'];
  enter_desc       text[] := ARRAY['Кино','Концерт','Спортзал','Steam','Netflix','Зоопарк','Боулинг'];

  v_users   uuid[]    := ARRAY[u_ivan, u_anna];
  v_salary  numeric[] := ARRAY[85000, 120000];
  v_rent    numeric[] := ARRAY[35000, 50000];
  v_months  date[]    := ARRAY['2026-04-01','2026-05-01','2026-06-01']::date[];

  ui int; v_user uuid; v_mon date; v_maxday int; v_y int; v_m int;
  k int; f numeric;
BEGIN
  IF EXISTS (SELECT 1 FROM public.user_profiles) THEN
    RAISE NOTICE 'Seed: data already present — skipping.';
    RETURN;
  END IF;

  -- 1. Users (auth) + profiles
  PERFORM pg_temp.add_user(u_ivan,  'user@example.com',   'Иван Петров',   'user');
  PERFORM pg_temp.add_user(u_anna,  'family@example.com', 'Анна Петрова',  'user');
  PERFORM pg_temp.add_user(u_admin, 'admin@example.com',  'Администратор', 'admin');

  INSERT INTO public.user_profiles ("Id","DisplayName","Currency","CreatedAt","UpdatedAt")
  VALUES
    (u_ivan,  'Иван Петров',   'RUB', now(), now()),
    (u_anna,  'Анна Петрова',  'RUB', now(), now()),
    (u_admin, 'Администратор', 'RUB', now(), now());

  -- 2. Accruals + receipts — 50 per user per month → 150/user, 300 total
  FOR ui IN 1..array_length(v_users, 1) LOOP
    v_user := v_users[ui];

    FOREACH v_mon IN ARRAY v_months LOOP
      v_maxday := CASE WHEN v_mon = DATE '2026-06-01' THEN 19 ELSE 28 END;  -- today = 2026-06-19

      -- Income (salary, day 4) + fixed monthly expenses
      PERFORM pg_temp.add_accrual(v_user, v_salary[ui],
        (v_mon + interval '3 day' + interval '10 hour')::timestamptz,
        1 /* Income */, c_salary, 'Зарплата');
      PERFORM pg_temp.add_accrual(v_user, v_rent[ui],
        (v_mon + interval '9 hour')::timestamptz,
        3, c_housing, 'Аренда квартиры');
      PERFORM pg_temp.add_accrual(v_user, pg_temp.rnd(700, 1200),
        pg_temp.rand_day(v_mon, v_maxday), 3, c_comms, 'Мобильная связь и интернет');

      -- Groceries ×12 (receipts with items). One June row is a foreign-currency
      -- purchase (Epic 8 multi-currency demo); the rest carry receipts.
      FOR k IN 1..12 LOOP
        IF v_mon = DATE '2026-06-01' AND k = 1 THEN
          PERFORM pg_temp.add_accrual(v_user, pg_temp.rnd(50, 120),
            pg_temp.rand_day(v_mon, v_maxday), 3, c_groceries,
            'Продукты (оплата картой за рубежом)', 'USD', 92);
        ELSE
          PERFORM pg_temp.add_receipt_accrual(v_user, pg_temp.rand_day(v_mon, v_maxday),
            c_groceries, pg_temp.pick(groceries_stores), groceries_items,
            40, 600, 3, 3, 8);
        END IF;
      END LOOP;

      -- Restaurants ×6 (receipts with items)
      FOR k IN 1..6 LOOP
        PERFORM pg_temp.add_receipt_accrual(v_user, pg_temp.rand_day(v_mon, v_maxday),
          c_rest, pg_temp.pick(rest_places), rest_items, 200, 900, 2, 2, 6);
      END LOOP;

      -- Clothing ×3 (receipts with items)
      FOR k IN 1..3 LOOP
        PERFORM pg_temp.add_receipt_accrual(v_user, pg_temp.rand_day(v_mon, v_maxday),
          c_clothing, pg_temp.pick(clothing_stores), clothing_items, 800, 6000, 2, 2, 5);
      END LOOP;

      -- Transport ×10
      FOR k IN 1..10 LOOP
        PERFORM pg_temp.add_accrual(v_user, pg_temp.rnd(150, 1200),
          pg_temp.rand_day(v_mon, v_maxday), 3, c_transport, pg_temp.pick(transport_desc));
      END LOOP;

      -- Health ×3
      FOR k IN 1..3 LOOP
        PERFORM pg_temp.add_accrual(v_user, pg_temp.rnd(1000, 6000),
          pg_temp.rand_day(v_mon, v_maxday), 3, c_health, pg_temp.pick(health_desc));
      END LOOP;

      -- Entertainment ×6
      FOR k IN 1..6 LOOP
        PERFORM pg_temp.add_accrual(v_user, pg_temp.rnd(500, 3000),
          pg_temp.rand_day(v_mon, v_maxday), 3, c_enter, pg_temp.pick(enter_desc));
      END LOOP;

      -- Other ×7
      FOR k IN 1..7 LOOP
        PERFORM pg_temp.add_accrual(v_user, pg_temp.rnd(200, 2500),
          pg_temp.rand_day(v_mon, v_maxday), 3, c_other, 'Разные расходы');
      END LOOP;
    END LOOP;

    -- 3. Monthly budgets — all three months × 6 categories. Limits sit a bit
    -- above typical monthly spend; scaled up for the higher-income user.
    f := CASE WHEN ui = 2 THEN 1.3 ELSE 1.0 END;
    FOREACH v_mon IN ARRAY v_months LOOP
      v_y := extract(year  FROM v_mon)::int;
      v_m := extract(month FROM v_mon)::int;
      INSERT INTO public.monthly_budgets
        ("Id","UserId","CategoryId","Year","Month","LimitAmount","Currency")
      VALUES
        (gen_random_uuid(), v_user, c_groceries, v_y, v_m, round(45000 * f), 'RUB'),
        (gen_random_uuid(), v_user, c_rest,      v_y, v_m, round(18000 * f), 'RUB'),
        (gen_random_uuid(), v_user, c_transport, v_y, v_m, round(12000 * f), 'RUB'),
        (gen_random_uuid(), v_user, c_enter,     v_y, v_m, round( 9000 * f), 'RUB'),
        (gen_random_uuid(), v_user, c_clothing,  v_y, v_m, round(12000 * f), 'RUB'),
        (gen_random_uuid(), v_user, c_comms,     v_y, v_m, round( 1500 * f), 'RUB');
    END LOOP;
  END LOOP;

  RAISE NOTICE 'Seed: done — 3 users, 300 accruals, receipts with items, budgets.';
END $$;
