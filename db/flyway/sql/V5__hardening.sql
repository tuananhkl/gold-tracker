-- V5: Hardening constraints and indexes
-- Rationale:
--  A) Ensure required product fields are NOT NULL for data integrity
--  B) Optional strict dedup index to treat NULLs as values (kept alongside existing unique index)
--  C) Add read-path indexes to speed up daily snapshot charts and widgets
--  D) Guard against absurd future effective_at while allowing next-day announcements

SET search_path = gold, public;

-- A) Tighten product required fields
ALTER TABLE IF EXISTS gold.product
  ALTER COLUMN brand SET NOT NULL,
  ALTER COLUMN form  SET NOT NULL;

-- B) Optional strict dedup for product (treat NULLs as values) — keep both if non-conflicting
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_indexes
     WHERE schemaname='gold' AND indexname='uq_product_dedup_expr'
  ) THEN
    CREATE UNIQUE INDEX uq_product_dedup_expr
      ON gold.product (brand, form, COALESCE(karat,-1), COALESCE(region,''));
  END IF;
END$$;

-- C) Extra read paths for snapshots (speed up charts & widgets)
CREATE INDEX IF NOT EXISTS idx_daily_snapshot_source_date
  ON gold.daily_snapshot (source_id, date DESC);

CREATE INDEX IF NOT EXISTS idx_daily_snapshot_product_date
  ON gold.daily_snapshot (product_id, date DESC);

-- D) Guard against absurd future effective_at (allows announcing next-day prices)
ALTER TABLE IF EXISTS gold.price_tick
  DROP CONSTRAINT IF EXISTS price_tick_effective_at_reasonable,
  ADD  CONSTRAINT price_tick_effective_at_reasonable
  CHECK (effective_at <= now() + interval '1 day');

