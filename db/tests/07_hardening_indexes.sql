SET search_path = public, gold;

SELECT plan(7);

-- New indexes on daily_snapshot
SELECT results_eq(
  $$ SELECT EXISTS (
         SELECT 1 FROM pg_indexes
         WHERE schemaname='gold' AND tablename='daily_snapshot' AND indexname='idx_daily_snapshot_source_date'
     ) $$,
  $$ VALUES (true) $$,
  'idx_daily_snapshot_source_date exists'
);

SELECT results_eq(
  $$ SELECT EXISTS (
         SELECT 1 FROM pg_indexes
         WHERE schemaname='gold' AND tablename='daily_snapshot' AND indexname='idx_daily_snapshot_product_date'
     ) $$,
  $$ VALUES (true) $$,
  'idx_daily_snapshot_product_date exists'
);

-- uq_product_dedup_expr exists (if created) and is unique
SELECT results_eq(
  $$ SELECT EXISTS (
         SELECT 1 FROM pg_indexes WHERE schemaname='gold' AND indexname='uq_product_dedup_expr'
     ) $$,
  $$ VALUES (true) $$,
  'uq_product_dedup_expr exists'
);

SELECT results_eq(
  $$ SELECT (SELECT i.indisunique FROM pg_index i JOIN pg_class c ON c.oid=i.indexrelid JOIN pg_namespace n ON n.oid=c.relnamespace
             WHERE n.nspname='gold' AND c.relname='uq_product_dedup_expr') $$,
  $$ VALUES (true) $$,
  'uq_product_dedup_expr is unique'
);

-- Existing critical indexes still there
SELECT results_eq(
  $$ SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='gold' AND tablename='price_tick' AND indexname='idx_price_tick_product_time') $$,
  $$ VALUES (true) $$,
  'idx_price_tick_product_time exists'
);

SELECT results_eq(
  $$ SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='gold' AND tablename='price_tick' AND indexname='idx_price_tick_source_time') $$,
  $$ VALUES (true) $$,
  'idx_price_tick_source_time exists'
);

-- raw hash unique
SELECT results_eq(
  $$ SELECT (SELECT i.indisunique FROM pg_index i JOIN pg_class c ON c.oid=i.indexrelid JOIN pg_namespace n ON n.oid=c.relnamespace
             WHERE n.nspname='gold' AND c.relname='idx_price_tick_raw_hash') $$,
  $$ VALUES (true) $$,
  'idx_price_tick_raw_hash is unique'
);

SELECT finish();

