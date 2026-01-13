# Issue 2 — Duplicate app running in `default` and `gold-dev` namespaces

## Context

After fixing log level emission, Kibana still showed log records from a “pod cũ”:

- `gold-tracker-api-76449db76-g7rvl`

Even after waiting, logs continued to appear with fresh timestamps.

## Debugging mindset

When you still see “old pod” logs:

1) Don’t assume the pod is gone — **prove it** with cluster state.
2) In Kubernetes, “old” often just means “older deployment”, but it can still be running:
   - in a different namespace
   - as a leftover deployment/statefulset
   - or recreated by a controller (Deployment/CronJob)

So the first question becomes:

> “Is that pod still Running anywhere in the cluster?”

## Investigation steps & commands

### Step A — List all pods in all namespaces

Command:

```bash
kubectl get pods -A -o wide | grep -F "gold-tracker-api" || true
```

Output interpretation:

- If you see the same app pods in multiple namespaces, Kibana will ingest logs from both.

We found:

- `default` had `gold-tracker-api-76449db76-g7rvl` Running
- `gold-dev` had the current API pod Running

### Step B — Confirm duplicate deployments exist

Command:

```bash
kubectl get deploy -A | grep -F "gold-tracker-api" || true
```

We found:

- `default/gold-tracker-api`
- `gold-dev/gold-tracker-api`

This explains why logs appeared from the “old” pod: it was not old — it was a separate environment.

## Root cause

There were **two environments** deployed simultaneously:

- An old environment in namespace `default` (API/UI/Postgres/CronJobs)
- The intended environment in namespace `gold-dev`

Since Filebeat/Elastic were ingesting cluster logs across namespaces, Kibana mixed both.

## Fix strategy

Goal:

- Keep **only `gold-dev`** as the active environment
- Remove/stop all Gold Tracker resources in `default`

### Part 1 — Stop the duplicate API immediately

Command:

```bash
kubectl -n default scale deployment/gold-tracker-api --replicas=0
```

Mindset:

- This is a safe immediate mitigation (stops log emission and load) while planning full cleanup.

### Part 2 — Full cleanup of `default` namespace resources

**Important**: this deletes the old `default` database PVC, i.e. old data.

Commands executed:

```bash
# stop future job creation in default
kubectl -n default delete cronjob \
  cron-scrape-btmc \
  cron-scrape-doji \
  cron-scrape-phucthanh \
  cron-scrape-sjc \
  cron-snapshot-daily \
  --ignore-not-found

# delete accumulated jobs (also removes their pods)
kubectl -n default get job -o name \
  | grep -E 'job.batch/cron-(scrape|snapshot)-' \
  | xargs -r kubectl -n default delete --ignore-not-found

# delete app workloads
kubectl -n default delete deployment gold-tracker-ui gold-tracker-api --ignore-not-found
kubectl -n default delete statefulset postgres --ignore-not-found

# delete services/config
kubectl -n default delete service gold-tracker-ui gold-tracker-api postgres --ignore-not-found
kubectl -n default delete configmap gold-app-config --ignore-not-found
kubectl -n default delete secret postgres-secret telegram-secret --ignore-not-found

# delete postgres storage in default (deletes old default DB data)
kubectl -n default delete pvc pgdata-postgres-0 --ignore-not-found
```

### Part 3 — Reduce noise in `gold-dev`

We kept the `gold-dev` CronJobs, but deleted accumulated Completed/Failed Jobs:

```bash
kubectl -n gold-dev get job -o name \
  | grep -E 'job.batch/cron-(scrape|snapshot)-' \
  | xargs -r kubectl -n gold-dev delete --ignore-not-found
```

### Part 4 — Verify cluster is clean

Commands:

```bash
kubectl -n default get pods,deploy,sts,svc,cronjob,job,pvc
kubectl get pods -A -o wide | grep -E "gold-tracker-(api|ui)" || true
```

Expected:

- `default` contains no Gold Tracker workloads
- Only `gold-dev` contains `gold-tracker-api` and `gold-tracker-ui`

## Conclusion

The “old pod logs” symptom was not a logging issue — it was an **environment duplication** issue.

Once `default` was cleaned, Kibana and operations became predictable:

- one namespace (`gold-dev`)
- one set of deployments/services/postgres

## Recommendation (prevent recurrence)

- In Kibana, add a permanent filter: `kubernetes.namespace: "gold-dev"`
- Consider enforcing a namespace naming convention and blocking `default` deployments for app workloads.

