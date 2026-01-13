# Issue 1 — Kibana: many logs missing `log.level` / mixed `Information` vs `info`

## Context

We were using Kibana Lens to slice logs by log level for the `gold-tracker` app.

Observed:

- A large portion of records had **missing `log.level`** (“(missing value)” slice).
- Some records had `log.level` like **`Information`**, others like **`info`** (inconsistent casing/format).
- Example record provided (no `@l` / no level field):

```json
{"@t":"2026-01-13T12:25:49.0819540Z","@m":"RequestCompleted","@i":"0831e305","@tr":"e9670f4d0de2793db9d49c680c1d53a3","@sp":"07490ba9d6ff0545","duration_ms":0.3453,"status":200,"biz_keys":{"endpoint":"/healthz","method":"GET","biz_func":"Application","query_keys":{}},"method":"GET","endpoint":"/healthz","message_type":"Application","span_id":"07490ba9d6ff0545","context_id":"GF4ZG","correlation_id":"T5VMASNOT5","trace_id":"T5VMASNOT5","user_agent":"kube-probe/1.34","client_ip":"::ffff:10.244.0.1","hostname":"gold-tracker-api-76449db76-g7rvl","service_version":"1.0.0.0","env":"Docker","app":"gold-tracker-api","RequestId":"0HNI64D5RFBOK:00000001","RequestPath":"/healthz","ConnectionId":"0HNI64D5RFBOK","ts_vn":"2026-01-13T19:25:49.081+07:00"}
```

## Debugging mindset

When a structured field is “missing” in Kibana, there are typically two buckets:

1) **The app never emitted the field** (so ingest can’t map it)
2) The app emitted the field, but **ingest parsing/mapping** failed

Start with (1) because it’s cheaper to verify:

- Pull raw pod logs (`kubectl logs ...`) and confirm whether the field exists.
- If the raw JSON doesn’t contain the field, fix at the source (logging formatter/enricher).

Only then look into ingest pipelines (Filebeat/Elastic ingest rules).

## Investigation steps & commands

### Step A — Find the logging configuration in the API

Mindset: “Where is Serilog configured? Which formatter/sink is used?”

Commands:

```bash
rg "AddSerilogLogging\\(|WriteTo\\.Console|RenderedCompactJsonFormatter|CompactJsonFormatter" src/GoldTracker.Api
```

Finding:

- `src/GoldTracker.Api/Logging/SerilogConfig.cs` used:
  - `WriteTo.Console(new RenderedCompactJsonFormatter())`

### Step B — Validate the hypothesis (Serilog compact JSON omits level)

Mindset: “Is the formatter known to omit level for `Information`?”

Serilog’s compact format convention:

- It **omits** the level token for `Information` events to reduce payload size.
- That means `@l` is absent for most app logs (since `Information` is usually the default).
- If Filebeat/ingest relies on a level token to populate ECS `log.level`, you’ll see “missing”.

## Root cause

`gold-tracker-api` used:

- `RenderedCompactJsonFormatter` which **does not always emit `@l`**
  - For `Information`, it’s usually omitted.

Therefore:

- Kibana sees many docs with no level token
- `log.level` is missing for those docs

## Fix (code)

We added a small wrapper formatter that:

- Uses the built-in compact formatter first
- Ensures `@l` exists for **all** events
- Normalizes `@l` to a consistent set of tokens:
  - `trace`, `debug`, `info`, `warn`, `error`, `fatal`

### File added

- `src/GoldTracker.Api/Logging/AlwaysLevelRenderedCompactJsonFormatter.cs`

Key idea:

```csharp
// 1) Render compact JSON first (Serilog built-in)
// 2) If "@l" exists -> normalize to info/warn/error...
// 3) If "@l" missing -> inject "@l":"info" (or warn/error/etc)
```

### Wiring change

- `src/GoldTracker.Api/Logging/SerilogConfig.cs`

Before:

```csharp
.WriteTo.Console(new RenderedCompactJsonFormatter());
```

After:

```csharp
.WriteTo.Console(new AlwaysLevelRenderedCompactJsonFormatter());
```

## Deploy + test

Mindset: “Confirm new code is running AND verify raw logs before checking Kibana.”

Commands:

```bash
# build image in minikube docker
eval "$(minikube -p minikube docker-env)"
docker build -t gold-tracker-api:dev -f src/GoldTracker.Api/Dockerfile .

# restart and wait
kubectl -n gold-dev rollout restart deployment/gold-tracker-api
kubectl -n gold-dev rollout status deployment/gold-tracker-api --timeout=180s

# verify logs contain @l
kubectl -n gold-dev logs deployment/gold-tracker-api --tail=30
```

Expected output (examples seen after fix):

```json
{"@l":"info","@t":"...","@m":"RequestCompleted", ... }
{"@l":"error","@t":"...","@m":"Telegram polling error ...", ... }
```

## Conclusion

- Missing `log.level` was caused by the **log emitter** (Serilog formatter), not Kibana.
- Ensuring every log line contains a normalized `@l` makes `log.level` stable and filterable.

