-- V2: Core tables, constraints, and indexes
SET search_path = public, gold;

-- gold.source
CREATE TABLE IF NOT EXISTS gold.source (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name        text NOT NULL UNIQUE,
    kind        text NOT NULL CHECK (kind IN ('retailer','exchange','news','bank')),
    base_url    text,
    active      boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now()
);

-- gold.product
CREATE TABLE IF NOT EXISTS gold.product (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    brand       text NOT NULL,
    form        text NOT NULL CHECK (form IN ('ring','bar','jewelry','other')),
    karat       integer,
    region      text,
    sku_hint    text,
    active      boolean NOT NULL DEFAULT true
);

-- Option B (strict) uniqueness for product dedup
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes WHERE schemaname = 'gold' AND indexname = 'ux_product_brand_form_karat_region'
    ) THEN
        EXECUTE 'CREATE UNIQUE INDEX ux_product_brand_form_karat_region
                 ON gold.product (brand, form, COALESCE(karat, -1), COALESCE(region, ''''))';
    END IF;
END$$;

-- gold.price_tick
CREATE TABLE IF NOT EXISTS gold.price_tick (
    id              bigserial PRIMARY KEY,
    product_id      uuid NOT NULL REFERENCES gold.product(id) ON DELETE RESTRICT,
    source_id       uuid NOT NULL REFERENCES gold.source(id) ON DELETE RESTRICT,
    price_buy       numeric(18,2) NOT NULL CHECK (price_buy > 0),
    price_sell      numeric(18,2) NOT NULL CHECK (price_sell > 0 AND price_sell >= price_buy),
    currency        char(3) NOT NULL DEFAULT 'VND' CHECK (currency ~ '^[A-Z]{3}$'),
    collected_at    timestamptz NOT NULL,
    effective_at    timestamptz NOT NULL,
    raw_hash        char(64),
    CONSTRAINT ux_price_tick_unique_time UNIQUE (product_id, source_id, effective_at)
);

-- Indexes on price_tick
CREATE INDEX IF NOT EXISTS idx_price_tick_product_time ON gold.price_tick (product_id, effective_at DESC);
CREATE INDEX IF NOT EXISTS idx_price_tick_source_time  ON gold.price_tick (source_id,  effective_at DESC);
CREATE UNIQUE INDEX IF NOT EXISTS idx_price_tick_raw_hash ON gold.price_tick (raw_hash) WHERE raw_hash IS NOT NULL;

-- gold.daily_snapshot
CREATE TABLE IF NOT EXISTS gold.daily_snapshot (
    id                  bigserial PRIMARY KEY,
    product_id          uuid NOT NULL REFERENCES gold.product(id) ON DELETE RESTRICT,
    source_id           uuid NOT NULL REFERENCES gold.source(id) ON DELETE RESTRICT,
    date                date NOT NULL,
    price_buy_close     numeric(18,2) NOT NULL,
    price_sell_close    numeric(18,2) NOT NULL,
    CONSTRAINT ux_daily_snapshot UNIQUE (product_id, source_id, date)
);


