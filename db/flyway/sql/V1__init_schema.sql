-- V1: Initialize schema and extensions

CREATE SCHEMA IF NOT EXISTS gold;

-- Required extensions
CREATE EXTENSION IF NOT EXISTS pgcrypto; -- gen_random_uuid
-- pgtap extension (optional, for tests only)
-- CREATE EXTENSION IF NOT EXISTS pgtap;    -- for tests (extension files provided in server image)

COMMENT ON SCHEMA gold IS 'Time-series prices; candidate for future RANGE partition on effective_at';


