SET search_path = public, gold;

SELECT plan(4);

-- v_latest_price_per_product returns one row per (product,source)
SELECT results_eq(
  $$ WITH v AS (SELECT * FROM gold.v_latest_price_per_product)
     SELECT COUNT(*) FROM v $$,
  $$ SELECT COUNT(*) FROM (SELECT DISTINCT product_id, source_id FROM gold.price_tick) q $$,
  'v_latest has one row per (product,source)'
);

-- Seeded DOJI ring has latest and expected last sell price of 7,520,000 VND (from 2025-11-02 16:30)
SELECT results_eq(
  $$ SELECT price_sell FROM gold.v_latest_price_per_product v
     JOIN gold.product p ON p.id=v.product_id
     JOIN gold.source s ON s.id=v.source_id
     WHERE p.brand='DOJI' AND p.form='ring' AND p.karat=24 AND p.region='Hanoi' AND s.name='DOJI' $$,
  $$ VALUES (7520000::numeric) $$,
  'DOJI ring latest sell price matches last tick'
);

-- v_day_over_day computes correct delta & direction: day 1 flat, day 2 up
-- Day 1 (2025-11-01) should be flat with delta 0
SELECT results_eq(
  $$ SELECT direction, delta_vs_yesterday FROM gold.v_day_over_day v
     JOIN gold.product p ON p.id=v.product_id
     JOIN gold.source s ON s.id=v.source_id
     WHERE p.brand='DOJI' AND p.form='ring' AND p.karat=24 AND p.region='Hanoi' AND s.name='DOJI'
       AND v.date = DATE '2025-11-01' $$,
  $$ VALUES ('flat'::text, 0::numeric) $$,
  'Day 1 is flat with zero delta'
);

-- Day 2 (2025-11-02): last close 7,520,000 vs prior 7,480,000 => +40,000 up
SELECT results_eq(
  $$ SELECT direction, delta_vs_yesterday FROM gold.v_day_over_day v
     JOIN gold.product p ON p.id=v.product_id
     JOIN gold.source s ON s.id=v.source_id
     WHERE p.brand='DOJI' AND p.form='ring' AND p.karat=24 AND p.region='Hanoi' AND s.name='DOJI'
       AND v.date = DATE '2025-11-02' $$,
  $$ VALUES ('up'::text, 40000::numeric) $$,
  'Day 2 is up with +40,000 delta'
);

SELECT finish();

