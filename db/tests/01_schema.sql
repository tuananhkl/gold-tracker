SET search_path = public, gold;

SELECT plan(11);

-- Schema exists
SELECT has_schema('gold', 'gold schema exists');

-- Tables exist
SELECT has_table('gold', 'source', 'gold.source exists');
SELECT has_table('gold', 'product', 'gold.product exists');
SELECT has_table('gold', 'price_tick', 'gold.price_tick exists');
SELECT has_table('gold', 'daily_snapshot', 'gold.daily_snapshot exists');

-- Function exists and is immutable
SELECT has_function('gold', 'fn_local_date', ARRAY['timestamptz'], 'fn_local_date exists');
SELECT results_eq(
  $$ SELECT EXISTS (
         SELECT 1 FROM pg_proc p
         JOIN pg_namespace n ON n.oid=p.pronamespace
         WHERE n.nspname='gold' AND p.proname='fn_local_date' AND p.provolatile='i'
     ) $$,
  $$ VALUES (true) $$,
  'fn_local_date is IMMUTABLE'
);

-- Views exist
SELECT has_view('gold', 'v_latest_price_per_product', 'view v_latest_price_per_product exists');
SELECT has_view('gold', 'v_day_over_day', 'view v_day_over_day exists');

-- Columns basic checks
SELECT results_eq(
  $$ SELECT EXISTS (
         SELECT 1 FROM pg_attribute a
         JOIN pg_class c ON c.oid=a.attrelid
         JOIN pg_namespace n ON n.oid=c.relnamespace
         WHERE n.nspname='gold' AND c.relname='price_tick' AND a.attname='price_sell'
     ) $$,
  $$ VALUES (true) $$,
  'price_sell column present'
);
SELECT results_eq(
  $$ SELECT EXISTS (
         SELECT 1 FROM pg_attribute a
         JOIN pg_class c ON c.oid=a.attrelid
         JOIN pg_namespace n ON n.oid=c.relnamespace
         WHERE n.nspname='gold' AND c.relname='price_tick' AND a.attname='effective_at'
     ) $$,
  $$ VALUES (true) $$,
  'effective_at column present'
);

SELECT finish();

