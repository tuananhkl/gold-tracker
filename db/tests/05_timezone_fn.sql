SET search_path = public, gold;

SELECT plan(3);

-- Around local midnight: 2025-11-01 18:30 UTC is 2025-11-02 01:30 VN
SELECT results_eq(
  $$ SELECT gold.fn_local_date('2025-11-01 18:30+00'::timestamptz) $$,
  $$ VALUES (DATE '2025-11-02') $$,
  'UTC evening rolls to next local day'
);

-- 2025-11-01 00:30 UTC is 2025-11-01 07:30 VN => same local date
SELECT results_eq(
  $$ SELECT gold.fn_local_date('2025-11-01 00:30+00'::timestamptz) $$,
  $$ VALUES (DATE '2025-11-01') $$,
  'Early UTC stays same local day'
);

-- Function is immutable per pg_proc
SELECT results_eq(
  $$ SELECT EXISTS (
         SELECT 1 FROM pg_proc p
         JOIN pg_namespace n ON n.oid=p.pronamespace
         WHERE n.nspname='gold' AND p.proname='fn_local_date' AND p.provolatile='i'
     ) $$,
  $$ VALUES (true) $$,
  'fn_local_date is IMMUTABLE'
);

SELECT finish();

