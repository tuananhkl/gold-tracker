-- V4: Minimal seed data for sources, products, ticks over 2 local days, and daily snapshots
SET search_path = public, gold;

-- Sources
INSERT INTO gold.source (name, kind, base_url)
VALUES
    ('DOJI', 'retailer', 'https://doji.vn'),
    ('BTMC', 'retailer', 'https://btmc.vn'),
    ('SJC',  'retailer', 'https://sjc.com.vn')
ON CONFLICT (name) DO NOTHING;

-- Products: one 24K ring in Hanoi, plus a couple bars
INSERT INTO gold.product (brand, form, karat, region, sku_hint)
VALUES
    ('DOJI', 'ring', 24, 'Hanoi', 'Nhẫn tròn trơn 24K'),
    ('SJC',  'bar',  24, 'HCMC',  'SJC 1L'),
    ('BTMC', 'bar',  24, 'Hanoi', 'BTMC 1L')
ON CONFLICT DO NOTHING;

-- Capture IDs for ease of seeding deterministic associations
WITH p AS (
    SELECT
        (SELECT id FROM gold.product WHERE brand='DOJI' AND form='ring' AND karat=24 AND region='Hanoi') AS prod_ring_doji,
        (SELECT id FROM gold.product WHERE brand='SJC'  AND form='bar'  AND karat=24 AND region='HCMC')  AS prod_bar_sjc,
        (SELECT id FROM gold.product WHERE brand='BTMC' AND form='bar'  AND karat=24 AND region='Hanoi') AS prod_bar_btmc
), s AS (
    SELECT
        (SELECT id FROM gold.source WHERE name='DOJI') AS src_doji,
        (SELECT id FROM gold.source WHERE name='SJC')  AS src_sjc,
        (SELECT id FROM gold.source WHERE name='BTMC') AS src_btmc
)
-- Day 1 and Day 2 in Vietnam local time
-- Use two recent dates to make results relatable; adjust here if desired
INSERT INTO gold.price_tick (product_id, source_id, price_buy, price_sell, currency, collected_at, effective_at, raw_hash)
SELECT
    p.prod_ring_doji, s.src_doji, v.buy, v.sell, 'VND', v.collected_at, v.effective_at, NULL
FROM p, s, (
    VALUES
        -- Day 1 (VN local): 2025-11-01
        (('2025-11-01 09:00'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh'), ('2025-11-01 09:00'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh'), 7350000::numeric, 7450000::numeric),
        (('2025-11-01 16:30'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh'), ('2025-11-01 16:30'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh'), 7380000::numeric, 7480000::numeric),
        -- Day 2 (VN local): 2025-11-02
        (('2025-11-02 09:00'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh'), ('2025-11-02 09:00'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh'), 7400000::numeric, 7500000::numeric),
        (('2025-11-02 16:30'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh'), ('2025-11-02 16:30'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh'), 7420000::numeric, 7520000::numeric)
) v(effective_at, collected_at, buy, sell)
ON CONFLICT DO NOTHING;

-- Also seed a couple of ticks for bars (not essential for tests, but useful)
INSERT INTO gold.price_tick (product_id, source_id, price_buy, price_sell, currency, collected_at, effective_at)
SELECT 
    (SELECT id FROM gold.product WHERE brand='SJC' AND form='bar' AND karat=24 AND region='HCMC') AS product_id,
    (SELECT id FROM gold.source  WHERE name='SJC') AS source_id,
    7450000, 7550000, 'VND', ts, ts
FROM (
    SELECT ('2025-11-01 10:00'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh') AS ts
    UNION ALL
    SELECT ('2025-11-02 15:00'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')
) t
ON CONFLICT DO NOTHING;

INSERT INTO gold.price_tick (product_id, source_id, price_buy, price_sell, currency, collected_at, effective_at)
SELECT 
    (SELECT id FROM gold.product WHERE brand='BTMC' AND form='bar' AND karat=24 AND region='Hanoi') AS product_id,
    (SELECT id FROM gold.source  WHERE name='BTMC') AS source_id,
    7440000, 7540000, 'VND', ts, ts
FROM (
    SELECT ('2025-11-01 11:00'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh') AS ts
    UNION ALL
    SELECT ('2025-11-02 14:00'::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')
) t
ON CONFLICT DO NOTHING;

-- Seed daily snapshots for the DOJI ring (using last tick of each local day)
WITH ring AS (
    SELECT
        pt.product_id,
        pt.source_id,
        gold.fn_local_date(pt.effective_at) AS local_date,
        pt.price_buy,
        pt.price_sell,
        ROW_NUMBER() OVER (
            PARTITION BY pt.product_id, pt.source_id, gold.fn_local_date(pt.effective_at)
            ORDER BY pt.effective_at DESC, pt.collected_at DESC, pt.id DESC
        ) AS rn
    FROM gold.price_tick pt
    JOIN gold.product pr ON pr.id = pt.product_id AND pr.brand='DOJI' AND pr.form='ring' AND pr.karat=24 AND pr.region='Hanoi'
)
INSERT INTO gold.daily_snapshot (product_id, source_id, date, price_buy_close, price_sell_close)
SELECT product_id, source_id, local_date, price_buy, price_sell
FROM ring
WHERE rn = 1
ON CONFLICT DO NOTHING;

