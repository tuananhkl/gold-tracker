## Gold Price Tracker - Sprint 1 (DB only)

PostgreSQL 16 schema with Flyway migrations, containerized dev, and pgTAP tests. Locale for reporting is Vietnam (Asia/Ho_Chi_Minh); DB timezone remains UTC.

### Quickstart

```bash
make db:up
make db:migrate
make db:test
```

All tests should pass. Connect to DB:

```bash
make db:psql
```

### Expected output (sample)

Run after migration:

```sql
SELECT * FROM gold.v_day_over_day ORDER BY date, product_id, source_id;
```

Example rows (DOJI ring 24K Hanoi):

```text
 product_id | source_id |    date    | price_sell_close | delta_vs_yesterday | direction
------------+-----------+------------+-------------------+--------------------+-----------
   ...      |   ...     | 2025-11-01 |          7480000  |                   0 | flat
   ...      |   ...     | 2025-11-02 |          7520000  |               40000 | up
```

### What’s inside

- Schema `gold` with tables: `source`, `product`, `price_tick`, `daily_snapshot`
- Function `gold.fn_local_date(ts timestamptz)` for Vietnam local date extraction
- Views: `gold.v_latest_price_per_product`, `gold.v_day_over_day`
- Flyway migrations under `db/flyway/sql` (V1..V4)
- Seed data: 3 sources (DOJI, BTMC, SJC), products (ring + bars), ticks over 2 local days, snapshots
- pgTAP test suite under `db/tests`
- Docker Compose services: Postgres (with pgTAP installed), Flyway, pgTAP runner

### Notes

- Product uniqueness uses strict option: unique index on `(brand, form, COALESCE(karat,-1), COALESCE(region,''))`.
- `v_day_over_day` first day shows direction `flat` with zero delta by design.
- Times are stored as UTC; local date calculations use `Asia/Ho_Chi_Minh`.


