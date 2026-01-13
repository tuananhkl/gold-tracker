# Appendix — Commands cheat sheet used today

This is a curated list of the commands we used, with “what to look for” notes.

## Logging / Kibana debug

### Find Serilog config in the repo

```bash
rg "AddSerilogLogging\\(|WriteTo\\.Console|RenderedCompactJsonFormatter|CompactJsonFormatter" src/GoldTracker.Api
```

- **Look for**: which formatter is being used and where it’s wired.

### Inspect recent API logs

```bash
kubectl -n gold-dev logs deployment/gold-tracker-api --tail=50
```

- **Look for**: presence of `@l` for Info logs; consistent `@l` tokens.

## Kubernetes: find “old pods” still running

### Search for a pod name across all namespaces

```bash
kubectl get pods -A -o wide | grep -F "gold-tracker-api" || true
```

- **Look for**: duplicates across namespaces.

### Find deployments across namespaces

```bash
kubectl get deploy -A | grep -F "gold-tracker-api" || true
```

- **Look for**: multiple deployments with same name in different namespaces.

### Identify who controls a pod

```bash
kubectl -n <ns> describe pod <pod-name> | sed -n '1,80p'
```

- **Look for**: `Controlled By: ReplicaSet/...` and labels/selectors.

## Build & deploy (Minikube)

### Build an image inside Minikube’s Docker daemon

```bash
eval "$(minikube -p minikube docker-env)"
docker build -t gold-tracker-api:dev -f src/GoldTracker.Api/Dockerfile .
```

### Rollout restart and wait

```bash
kubectl -n gold-dev rollout restart deployment/gold-tracker-api
kubectl -n gold-dev rollout status deployment/gold-tracker-api --timeout=180s
```

- **Look for**: “successfully rolled out”.

## Cleanup duplicate environment in `default`

### Immediate mitigation (stop emitting logs)

```bash
kubectl -n default scale deployment/gold-tracker-api --replicas=0
```

### Full cleanup (danger: deletes default DB)

```bash
kubectl -n default delete cronjob \
  cron-scrape-btmc \
  cron-scrape-doji \
  cron-scrape-phucthanh \
  cron-scrape-sjc \
  cron-snapshot-daily \
  --ignore-not-found

kubectl -n default get job -o name \
  | grep -E 'job.batch/cron-(scrape|snapshot)-' \
  | xargs -r kubectl -n default delete --ignore-not-found

kubectl -n default delete deployment gold-tracker-ui gold-tracker-api --ignore-not-found
kubectl -n default delete statefulset postgres --ignore-not-found

kubectl -n default delete service gold-tracker-ui gold-tracker-api postgres --ignore-not-found
kubectl -n default delete configmap gold-app-config --ignore-not-found
kubectl -n default delete secret postgres-secret telegram-secret --ignore-not-found

kubectl -n default delete pvc pgdata-postgres-0 --ignore-not-found
```

### Verify default is clean

```bash
kubectl -n default get pods,deploy,sts,svc,cronjob,job,pvc
```

## Cleanup accumulated Jobs in `gold-dev` (keep CronJobs)

```bash
kubectl -n gold-dev get job -o name \
  | grep -E 'job.batch/cron-(scrape|snapshot)-' \
  | xargs -r kubectl -n gold-dev delete --ignore-not-found
```

