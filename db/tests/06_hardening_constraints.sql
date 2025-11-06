SET search_path = public, gold;

SELECT plan(4);

-- NOT NULL brand/form
SELECT results_eq($$ SELECT attnotnull FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='gold' AND c.relname='product' AND a.attname='brand' $$,
                  $$ VALUES (true) $$, 'product.brand NOT NULL');
SELECT results_eq($$ SELECT attnotnull FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='gold' AND c.relname='product' AND a.attname='form' $$,
                  $$ VALUES (true) $$, 'product.form NOT NULL');

-- future effective_at rejected by CHECK
BEGIN;
  PREPARE q AS
    INSERT INTO gold.price_tick(product_id, source_id, price_buy, price_sell, currency, collected_at, effective_at)
    SELECT p.id, s.id, 1, 1, 'VND', now(), now() + interval '10 days'
    FROM gold.product p, gold.source s LIMIT 1;
  CREATE TEMP TABLE _future_ok(ok boolean);
  DO $$
  BEGIN
    BEGIN
      EXECUTE 'EXECUTE q';
      INSERT INTO _future_ok VALUES (false);
    EXCEPTION WHEN check_violation THEN
      INSERT INTO _future_ok VALUES (true);
    END;
  END$$;
  SELECT results_eq($$ SELECT ok FROM _future_ok $$, $$ VALUES (true) $$, 'future effective_at rejected');
  DEALLOCATE q;
ROLLBACK;

-- optional dedup expr: if present, attempt duplicate insert and expect failure; otherwise skip
DO $$
DECLARE has_idx boolean; v_brand text; v_form text; v_karat integer; v_region text; BEGIN
  SELECT EXISTS (
    SELECT 1 FROM pg_indexes WHERE schemaname='gold' AND indexname='uq_product_dedup_expr'
  ) INTO has_idx;

  IF has_idx THEN
    CREATE TEMP TABLE _dup_ok(ok boolean);
    SELECT brand, form, karat, region INTO v_brand, v_form, v_karat, v_region FROM gold.product LIMIT 1;
    BEGIN
      INSERT INTO gold.product(brand, form, karat, region)
      VALUES (v_brand, v_form, v_karat, v_region);
      INSERT INTO _dup_ok VALUES (false);
    EXCEPTION WHEN unique_violation THEN
      INSERT INTO _dup_ok VALUES (true);
    END;
  END IF;
END$$;

SELECT CASE WHEN EXISTS (
  SELECT 1 FROM pg_indexes WHERE schemaname='gold' AND indexname='uq_product_dedup_expr'
) THEN results_eq($$ SELECT ok FROM _dup_ok $$, $$ VALUES (true) $$, 'uq_product_dedup_expr prevents duplicates')
ELSE ok(true, 'uq_product_dedup_expr absent; skipped') END;

SELECT finish();

