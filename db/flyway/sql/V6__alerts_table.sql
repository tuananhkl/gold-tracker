-- V6: Alerts Engine - alert_event table
CREATE SCHEMA IF NOT EXISTS gold;

CREATE TABLE IF NOT EXISTS gold.alert_event (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  created_at    timestamptz NOT NULL DEFAULT now(),
  kind          text NOT NULL,        -- price_jump | no_data | scraper_error
  brand         text NULL,
  region        text NULL,
  product_kind  text NULL,            -- "ring" | "bar" | ...
  message       text NOT NULL,
  severity      text NOT NULL,        -- info | warn | high
  meta          jsonb NOT NULL DEFAULT '{}'::jsonb,
  dedup_key     text NULL             -- dùng để tránh spam (brand+region+kind+window)
);

CREATE INDEX IF NOT EXISTS idx_alert_event_created_at ON gold.alert_event(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_alert_event_dedup ON gold.alert_event(dedup_key);
CREATE INDEX IF NOT EXISTS idx_alert_event_kind ON gold.alert_event(kind);

COMMENT ON TABLE gold.alert_event IS 'Stores alert events for price jumps, no data, and scraper errors';
COMMENT ON COLUMN gold.alert_event.dedup_key IS 'Used to prevent duplicate alerts within a time window';

