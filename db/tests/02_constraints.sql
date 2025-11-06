SET search_path = public, gold;

SELECT plan(10);

-- NOT NULLs
SELECT results_eq($$ SELECT attnotnull FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='gold' AND c.relname='price_tick' AND a.attname='price_buy' $$,
                  $$ VALUES (true) $$, 'price_buy NOT NULL');
SELECT results_eq($$ SELECT attnotnull FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='gold' AND c.relname='price_tick' AND a.attname='price_sell' $$,
                  $$ VALUES (true) $$, 'price_sell NOT NULL');
SELECT results_eq($$ SELECT attnotnull FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='gold' AND c.relname='price_tick' AND a.attname='currency' $$,
                  $$ VALUES (true) $$, 'currency NOT NULL');
SELECT results_eq($$ SELECT attnotnull FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='gold' AND c.relname='price_tick' AND a.attname='collected_at' $$,
                  $$ VALUES (true) $$, 'collected_at NOT NULL');
SELECT results_eq($$ SELECT attnotnull FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='gold' AND c.relname='price_tick' AND a.attname='effective_at' $$,
                  $$ VALUES (true) $$, 'effective_at NOT NULL');

-- CHECKs
-- CHECKs enforced (attempt invalid insert)
BEGIN;
  DO $$
  DECLARE v_product uuid; v_source uuid; BEGIN
    SELECT p.id, s.id INTO v_product, v_source
    FROM gold.product p CROSS JOIN gold.source s
    LIMIT 1;
    BEGIN
      INSERT INTO gold.price_tick (product_id, source_id, price_buy, price_sell, currency, collected_at, effective_at)
      VALUES (v_product, v_source, -1, 0, 'VND', now(), now());
      RAISE EXCEPTION 'should have failed';
    EXCEPTION WHEN check_violation THEN
      PERFORM 1; -- expected
    END;
  END$$;
ROLLBACK;
SELECT pass('CHECKs on price_buy/price_sell enforced');

-- FKs restrict
SELECT fk_ok('gold', 'price_tick', 'product_id', 'gold', 'product', 'id');
SELECT fk_ok('gold', 'price_tick', 'source_id',  'gold', 'source',  'id');

-- Uniqueness: (product_id, source_id, effective_at)
SELECT results_eq(
  $$ SELECT EXISTS (
         SELECT 1 FROM pg_constraint con
         JOIN pg_class c ON c.oid=con.conrelid
         JOIN pg_namespace n ON n.oid=c.relnamespace
         WHERE n.nspname='gold' AND c.relname='price_tick' AND con.contype='u'
           AND con.conkey IS NOT NULL
     ) $$,
  $$ VALUES (true) $$,
  'unique tick per time (some unique constraint exists)'
);

-- Duplicate tick should fail
BEGIN;
  SELECT product_id, source_id, effective_at
  INTO TEMP TABLE _t
  FROM gold.price_tick
  LIMIT 1;

  DO $$
  DECLARE v_product uuid; v_source uuid; v_effective timestamptz; BEGIN
    SELECT product_id, source_id, effective_at INTO v_product, v_source, v_effective FROM _t;
    BEGIN
      INSERT INTO gold.price_tick (product_id, source_id, price_buy, price_sell, currency, collected_at, effective_at)
      VALUES (v_product, v_source, 100, 101, 'VND', now(), v_effective);
      RAISE EXCEPTION 'should have failed';
    EXCEPTION WHEN unique_violation THEN
      -- expected
      PERFORM 1;
    END;
  END$$;
ROLLBACK;

SELECT pass('duplicate tick prevented by unique constraint');

SELECT finish();

