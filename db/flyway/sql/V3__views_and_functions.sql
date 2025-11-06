-- V3: Functions and Views
SET search_path = public, gold;

-- Function: convert UTC timestamptz to Vietnam local date
CREATE OR REPLACE FUNCTION gold.fn_local_date(ts timestamptz)
RETURNS date
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT (ts AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;
$$;

-- Latest price per (product, source) by effective_at, collected_at, id
CREATE OR REPLACE VIEW gold.v_latest_price_per_product AS
WITH ranked AS (
    SELECT
        pt.*,
        ROW_NUMBER() OVER (
            PARTITION BY pt.product_id, pt.source_id
            ORDER BY pt.effective_at DESC, pt.collected_at DESC, pt.id DESC
        ) AS rn
    FROM gold.price_tick pt
)
SELECT
    id,
    product_id,
    source_id,
    price_buy,
    price_sell,
    currency,
    collected_at,
    effective_at,
    raw_hash
FROM ranked
WHERE rn = 1;

-- Day-over-day view: compare today's close vs yesterday close per (product, source)
CREATE OR REPLACE VIEW gold.v_day_over_day AS
WITH d AS (
    SELECT
        ds.product_id,
        ds.source_id,
        ds.date,
        ds.price_sell_close,
        LAG(ds.price_sell_close) OVER (
            PARTITION BY ds.product_id, ds.source_id
            ORDER BY ds.date
        ) AS prev_close
    FROM gold.daily_snapshot ds
)
SELECT
    product_id,
    source_id,
    date,
    price_sell_close,
    COALESCE(price_sell_close - prev_close, 0)::numeric(18,2) AS delta_vs_yesterday,
    CASE
        WHEN prev_close IS NULL THEN 'flat'
        WHEN price_sell_close - prev_close > 0 THEN 'up'
        WHEN price_sell_close - prev_close < 0 THEN 'down'
        ELSE 'flat'
    END AS direction
FROM d;


