# Issue 3 — ES TLS warning + scraper anomaly noise + kustomize apply failures (2026-01-13)

This write-up documents the “second wave” of issues we fixed today:

- `ScraperHealthReader` warning: **Elasticsearch TLS PartialChain**
- Scraper warnings: **SJC jewelry out-of-bounds**, **BTMC out-of-bounds**
- Deploy pipeline blocked by **kustomize/yaml errors** and **immutable selector** errors

## Context / symptoms

### Symptom A — Elasticsearch TLS warning in logs

Observed warning:

```json
{
  "@m":"Failed to check scraper errors from Elasticsearch",
  "@l":"warn",
  "@x":"... AuthenticationException ... PartialChain ...",
  "SourceContext":"GoldTracker.Infrastructure.Alerts.ScraperHealthReader"
}
```

### Symptom B — SJC out-of-bounds spam

Observed warnings:

```json
{
  "@m":"Skipping SJC record due to anomaly: \"price out of bounds\" ... Form: \"jewelry\" ...",
  "@l":"warn",
  "SourceContext":"GoldTracker.Infrastructure.Scrapers.Sjc.SjcScraper"
}
```

### Symptom C — BTMC out-of-bounds

Example warning:

```json
{
  "@m":"Skipping BTMC record due to anomaly: \"buy price out of bounds (84373122)\" ...",
  "@l":"warn",
  "SourceContext":"GoldTracker.Infrastructure.Scrapers.Btmc.BtmcScraper"
}
```

### Symptom D — Deploy blocked

1) kustomize apply failing with YAML unmarshal errors:

```
yaml: unmarshal errors:
  mapping key "apiVersion" already defined ...
```

2) `kubectl apply -k` failing with immutable selector:

```
The Deployment "gold-tracker-api" is invalid: spec.selector: Invalid value ... field is immutable
```

## Debugging mindset (what to check first)

### For Elasticsearch TLS warnings

Mindset:

1) Is the warning coming from **our code** (app is calling ES directly), or from the log shipper?
2) If it’s our code: is the target endpoint using a **self-signed / private CA** cert?
3) Decide the safest approach per environment:
   - **Dev**: allow a controlled bypass (feature-flagged)
   - **Prod**: install CA certs properly (do not skip verification)

### For “out of bounds” scraper warnings

Mindset:

1) Out-of-bounds is a **data hygiene gate**. Occasional warns are OK; constant warns mean:
   - parsing unit mismatch
   - new feed format
   - wrong product categories included
2) For SJC, the warnings specifically referenced `Form: "jewelry"`:
   - If the product isn’t used in our UI/API, filter it at the parser or at the normalizer boundary.

### For kustomize / deployment errors

Mindset:

1) When `kubectl apply -k` fails:
   - first fix YAML parse errors (syntax / multi-doc)
2) When “field is immutable” appears:
   - it’s almost always the Deployment `spec.selector` or Service selector mismatch
   - fix the source manifests and re-apply, or recreate the deployment if needed

## Investigation steps & commands

### A) Locate the failing code path (ES TLS)

Commands:

```bash
rg "ScraperHealthReader|Failed to check scraper errors" src/GoldTracker.Infrastructure
sed -n '1,120p' src/GoldTracker.Infrastructure/Alerts/ScraperHealthReader.cs
```

Finding:

- `ScraperHealthReader` queries Elasticsearch via named `HttpClient("elasticsearch")`.

### B) Find where the Elasticsearch HttpClient is configured

Commands:

```bash
rg "AddHttpClient\\(\"elasticsearch\"" src/GoldTracker.Infrastructure
```

Finding:

- `src/GoldTracker.Infrastructure/DI/ServiceCollectionExtensions.cs` configures it.

### C) Verify whether log shipper already skips TLS

We checked Filebeat config and noticed:

- It already uses `ssl.verification_mode: none` for output.elasticsearch (shipper side),
  so the warning was **not** from Filebeat — it was from the app.

### D) Identify why SJC shows jewelry

Commands:

```bash
sed -n '1,120p' src/GoldTracker.Infrastructure/Scrapers/Sjc/SjcParser.cs
```

Finding:

- `NormalizeForm()` explicitly maps “nữ trang / jewelry” to `Form = "jewelry"`.
- Our app primarily displays `ring` / `bar`. `jewelry` rows were not useful and were noisy.

### E) Unblock `kubectl apply -k`

Commands:

```bash
kubectl apply -k k8s/overlays/dev
```

We then scanned for invalid YAML (multiple `apiVersion:` blocks in one doc):

```bash
python3 - <<'PY'
import os,re
root='k8s'
bad=[]
for d,_,fs in os.walk(root):
  for f in fs:
    if not f.endswith(('.yml','.yaml')): continue
    p=os.path.join(d,f)
    t=open(p,'r',encoding='utf-8').read()
    idx=[m.start() for m in re.finditer(r'(?m)^apiVersion:\\s*',t)]
    if len(idx)>1 and re.search(r'(?m)^---\\s*$',t) is None:
      bad.append(p)
print('\\n'.join(bad))
PY
```

Finding:

- `k8s/base/namespace.yaml` had duplicate Namespace blocks without `---`.

## Root causes

### Root cause A — Elasticsearch TLS PartialChain

The app called `ElasticsearchOptions.BaseUrl` via HTTPS with a certificate chain
that the container did not trust (private CA / self-signed / missing intermediate).

### Root cause B — SJC “jewelry” records not compatible with our bounds

The SJC feed includes jewelry pricing which can be outside our typical range checks
for ring/bar products and created warning noise.

### Root cause C — kustomize YAML invalid

`k8s/base/namespace.yaml` contained multiple top-level YAML docs without separators.

### Root cause D — immutable selector (app label mismatch)

`k8s/base/api-deployment.yaml` and `k8s/base/api-service.yaml` used `app: gold-api`
while the other base manifest set used `app: gold-tracker-api`.

Changing a Deployment selector is immutable, so apply failed.

## Fixes implemented

### Fix A — Add config-gated ES TLS bypass (dev-only)

Code changes:

- `src/GoldTracker.Infrastructure/Config/ElasticsearchOptions.cs`
  - Added `SkipTlsVerify` boolean option
- `src/GoldTracker.Infrastructure/DI/ServiceCollectionExtensions.cs`
  - Configured `HttpClientHandler.ServerCertificateCustomValidationCallback` when enabled

K8s change (dev):

- `k8s/overlays/dev/patch-api-env.yaml`
  - Added `Elasticsearch__SkipTlsVerify=true`

### Fix B — Align health query with normalized `log.level`

Code change:

- `src/GoldTracker.Infrastructure/Alerts/ScraperHealthReader.cs`
  - Replaced `term log.level = "Error"` with `terms ["error", "Error"]`
  - Keeps compatibility with older indexed docs.

### Fix C — Reduce SJC jewelry warning noise

Code change:

- `src/GoldTracker.Infrastructure/Scrapers/Sjc/SjcParser.cs`
  - Filtered out `Form == "jewelry"` records at the end of parse.

This keeps the ring/bar data (what UI/API needs) and removes warning noise.

### Fix D — Fix kustomize YAML and selector mismatch

K8s changes:

- `k8s/base/namespace.yaml`
  - Removed duplicate Namespace block (invalid YAML)
- `k8s/base/api-deployment.yaml`
  - Updated `selector.matchLabels.app` and pod template labels to `gold-tracker-api`
- `k8s/base/api-service.yaml`
  - Updated `spec.selector.app` to `gold-tracker-api`

## Deploy & verification

### Build + deploy

```bash
eval "$(minikube -p minikube docker-env)"
docker build -t gold-tracker-api:dev -f src/GoldTracker.Api/Dockerfile .

kubectl apply -k k8s/overlays/dev
kubectl -n gold-dev rollout restart deployment/gold-tracker-api
kubectl -n gold-dev rollout status deployment/gold-tracker-api --timeout=180s
```

### Validate warnings stopped

```bash
kubectl -n gold-dev logs deployment/gold-tracker-api --since=10m \
  | rg "Failed to check scraper errors from Elasticsearch|PartialChain|Skipping SJC record due to anomaly"
```

We observed:

- No more Elasticsearch TLS PartialChain warnings
- No more SJC jewelry out-of-bounds spam

### Trigger SJC scrape and verify inserted

We triggered from within cluster:

```bash
kubectl -n gold-dev run curl-tmp --rm -i --restart=Never --image=curlimages/curl:8.5.0 --command -- \
  curl -sS -X POST "http://gold-tracker-api:8080/admin/scrape/sjc?mode=once"
```

Expected/observed output:

```json
{"inserted":16}
```

## Notes about BTMC out-of-bounds warnings

BTMC out-of-bounds warnings can be acceptable if **rare**:

- They indicate we are actively preventing bad/outlier data from being stored.
- If they spike (high anomaly ratio), it likely means BTMC changed format/units and
  we should revisit parsing or bounds.

## Final conclusion

We restored deploy reliability (kustomize works) and reduced noisy warnings:

- Elasticsearch health checks no longer fail due to TLS chain issues in dev
- Health reader queries log levels correctly after level normalization
- SJC jewelry noise removed without affecting ring/bar functionality

## Lessons learned

- **Treat Kibana “missing fields” as an emitter problem first**, then ingest.
- **Kustomize errors often hide in small YAML mistakes** (duplicate docs, missing separators).
- **Selectors are immutable** — keep label conventions consistent across base manifests.
- **Warn noise** is a signal: if it’s not actionable or not used by product, filter it early.

