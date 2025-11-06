SET search_path = public, gold;

SELECT plan(6);

-- Indexes existence
SELECT has_index('gold', 'price_tick', 'idx_price_tick_product_time', 'product-time index exists');
SELECT has_index('gold', 'price_tick', 'idx_price_tick_source_time',  'source-time index exists');
SELECT has_index('gold', 'price_tick', 'idx_price_tick_raw_hash',     'raw_hash unique partial index exists');

-- Product uniqueness index exists
SELECT has_index('gold', 'product', 'ux_product_brand_form_karat_region', 'product uniqueness index exists');

-- raw_hash index is unique
SELECT results_eq(
  $$ SELECT (SELECT i.indisunique FROM pg_index i JOIN pg_class c ON c.oid=i.indexrelid JOIN pg_namespace n ON n.oid=c.relnamespace
             WHERE n.nspname='gold' AND c.relname='idx_price_tick_raw_hash') $$,
  $$ VALUES (true) $$,
  'idx_price_tick_raw_hash is unique'
);

-- unique time constraint exists too
SELECT results_eq(
  $$ SELECT EXISTS (
         SELECT 1 FROM pg_constraint con
         JOIN pg_class c ON c.oid=con.conrelid
         JOIN pg_namespace n ON n.oid=c.relnamespace
         WHERE n.nspname='gold' AND c.relname='price_tick' AND con.contype='u'
     ) $$,
  $$ VALUES (true) $$,
  'unique time constraint present'
);

SELECT finish();

