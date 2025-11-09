# Production Logging Setup - EFK Stack

This directory contains Kubernetes manifests for setting up production-grade logging using Fluent Bit → Elasticsearch → Kibana.

## Architecture

- **Fluent Bit**: DaemonSet running on each node, collecting logs from `/var/log/containers/*.log`
- **Elasticsearch**: External server at `http://192.168.31.156:9200`
- **Kibana**: Access Elasticsearch data through Kibana UI

## Prerequisites

- Kubernetes cluster (minikube/AKS compatible)
- Elasticsearch server accessible from cluster
- Credentials for Elasticsearch (stored in Kubernetes Secret)

## Setup Instructions

### 1. Create Namespace

```bash
kubectl apply -f namespace.yaml
```

### 2. Create Elasticsearch Secret

**IMPORTANT**: Do NOT commit secrets to git. Create the secret using kubectl:

```bash
kubectl -n logging create secret generic es-basic-auth \
  --from-literal=ES_HOST=http://192.168.31.156:9200 \
  --from-literal=ES_USER=elastic \
  --from-literal=ES_PASS='Tuananh123.'
```

Verify the secret was created:

```bash
kubectl -n logging get secret es-basic-auth
```

### 3. Apply Fluent Bit Configuration

```bash
kubectl apply -f configmap-fluent-bit.yaml
```

### 4. Deploy Fluent Bit DaemonSet

```bash
kubectl apply -f daemonset-fluent-bit.yaml
```

### 5. Verify Deployment

Check that Fluent Bit pods are running:

```bash
kubectl -n logging get pods -l app=fluent-bit
```

Expected output: One pod per node, all in `Running` state.

Check logs:

```bash
kubectl -n logging logs -l app=fluent-bit --tail=50
```

## Application Configuration

### .NET API

The API is configured to emit structured JSON logs with the following fields:
- `ts`: ISO timestamp
- `level`: log level (info, warn, error)
- `message`: log message
- `app`: service name (gold-tracker-api)
- `env`: environment (dev/prod)
- `service_version`: build version
- `traceId`: request correlation ID
- `spanId`: distributed tracing span ID
- `correlationId`: correlation ID
- `userId`: user ID (when available)
- `http.method`: HTTP method
- `http.path`: request path
- `http.status_code`: response status
- `duration_ms`: request duration

### Next.js UI

The UI emits structured JSON logs with:
- `ts`: ISO timestamp
- `level`: log level
- `message`: log message
- `app`: service name (gold-tracker-ui)
- `env`: environment
- `service_version`: build version
- `traceId`: request correlation ID
- `route`: API route path
- `status`: HTTP status code
- `duration_ms`: request duration
- `user_agent`: client user agent
- `client_ip`: client IP address

## Testing Logging

### 1. Generate Test Logs

Call the test endpoint to generate INFO and ERROR logs:

```bash
# Get trace ID from response
TRACE_ID=$(curl -sf "http://testcursor.derrick.local/gold/api/test/logging" | jq -r '.traceId')
echo "Trace ID: $TRACE_ID"
```

Or with explicit trace ID:

```bash
curl -sf -H "x-trace-id: test-$(date +%s)" \
  "http://testcursor.derrick.local/gold/api/test/logging" | jq .
```

### 2. Verify Logs in Kibana

1. Open Kibana UI (typically at `http://192.168.31.156:5601`)
2. Go to **Discover**
3. Create a data view:
   - Index pattern: `logs-*` or `logs-dev-*`
   - Time field: `@timestamp`
4. Search for your trace ID:

```
traceId:"<your-trace-id>"
```

### 3. Sample Kibana Queries

**Find all logs for a specific trace:**
```
traceId:"abc123"
```

**Find all errors:**
```
level:"error"
```

**Find slow requests (>1000ms):**
```
duration_ms:>1000
```

**Find logs from API:**
```
app:"gold-tracker-api"
```

**Find logs from UI:**
```
app:"gold-tracker-ui"
```

**Find logs by HTTP path:**
```
http.path:"/api/prices/latest"
```

**Find logs by status code:**
```
http.status_code:500
```

**Find logs by environment:**
```
env:"dev"
```

**Combined query (errors in API with status 500):**
```
app:"gold-tracker-api" AND level:"error" AND http.status_code:500
```

## Index Management

### Index Naming

Logs are indexed with the pattern: `logs-{ENV}-YYYY.MM.DD`

- Default: `logs-dev-2025.11.08`
- Production: `logs-prod-2025.11.08`

The `ENV` variable is set in the Fluent Bit DaemonSet (default: `dev`).

### Index Template (Optional)

To create an index template with proper field mappings, use the Elasticsearch API:

```bash
curl -X PUT "http://192.168.31.156:9200/_index_template/logs-template" \
  -H "Content-Type: application/json" \
  -u elastic:'Tuananh123.' \
  -d @- <<EOF
{
  "index_patterns": ["logs-*"],
  "template": {
    "mappings": {
      "properties": {
        "@timestamp": { "type": "date" },
        "ts": { "type": "date" },
        "level": { "type": "keyword" },
        "app": { "type": "keyword" },
        "env": { "type": "keyword" },
        "traceId": { "type": "keyword" },
        "correlationId": { "type": "keyword" },
        "spanId": { "type": "keyword" },
        "userId": { "type": "keyword" },
        "http.method": { "type": "keyword" },
        "http.path": { "type": "keyword" },
        "http.status_code": { "type": "integer" },
        "duration_ms": { "type": "float" },
        "route": { "type": "keyword" },
        "message": { "type": "text" }
      }
    }
  }
}
EOF
```

### Index Lifecycle Management (Optional)

To set up automatic index rollover and retention:

```bash
curl -X PUT "http://192.168.31.156:9200/_ilm/policy/logs-policy" \
  -H "Content-Type: application/json" \
  -u elastic:'Tuananh123.' \
  -d '{
    "policy": {
      "phases": {
        "hot": {
          "actions": {
            "rollover": {
              "max_size": "10GB",
              "max_age": "7d"
            }
          }
        },
        "delete": {
          "min_age": "30d",
          "actions": {
            "delete": {}
          }
        }
      }
    }
  }'
```

## Troubleshooting

### Fluent Bit Pods Not Starting

**Check pod status:**
```bash
kubectl -n logging describe pod -l app=fluent-bit
```

**Check logs:**
```bash
kubectl -n logging logs -l app=fluent-bit
```

**Common issues:**
- Secret not created: `Error: secret "es-basic-auth" not found`
- RBAC issues: Check ClusterRoleBinding
- Host path mount issues: Verify `/var/log/containers` exists on nodes

### Logs Not Appearing in Elasticsearch

**Verify Fluent Bit can reach Elasticsearch:**
```bash
kubectl -n logging exec -it $(kubectl -n logging get pod -l app=fluent-bit -o jsonpath='{.items[0].metadata.name}') -- \
  curl -u elastic:'Tuananh123.' http://192.168.31.156:9200/_cluster/health
```

**Check Fluent Bit metrics:**
```bash
kubectl -n logging port-forward $(kubectl -n logging get pod -l app=fluent-bit -o jsonpath='{.items[0].metadata.name}') 2020:2020
curl http://localhost:2020/api/v1/metrics
```

**Verify logs are being collected:**
```bash
kubectl -n logging exec -it $(kubectl -n logging get pod -l app=fluent-bit -o jsonpath='{.items[0].metadata.name}') -- \
  ls -la /var/log/containers/ | head -20
```

### Index Naming Issues

**Check current index pattern:**
```bash
curl -u elastic:'Tuananh123.' "http://192.168.31.156:9200/_cat/indices/logs-*?v"
```

**Verify ENV variable in DaemonSet:**
```bash
kubectl -n logging get daemonset fluent-bit -o yaml | grep -A 2 "ENV"
```

### No Logs from Applications

**Verify application pods have logs:**
```bash
kubectl -n gold-dev logs deploy/gold-tracker-api --tail=10
kubectl -n gold-dev logs deploy/gold-tracker-ui --tail=10
```

**Check if logs are JSON format:**
```bash
kubectl -n gold-dev logs deploy/gold-tracker-api --tail=1 | jq .
```

**Verify environment variables are set:**
```bash
kubectl -n gold-dev get deployment gold-tracker-api -o jsonpath='{.spec.template.spec.containers[0].env[*]}'
```

### RBAC Issues

**Check ServiceAccount:**
```bash
kubectl -n logging get serviceaccount fluent-bit
```

**Check ClusterRoleBinding:**
```bash
kubectl get clusterrolebinding fluent-bit-read
```

**Verify permissions:**
```bash
kubectl auth can-i get pods --as=system:serviceaccount:logging:fluent-bit
```

## Verification Checklist

- [ ] Fluent Bit DaemonSet pods are `Running` on all nodes
- [ ] Secret `es-basic-auth` exists in `logging` namespace
- [ ] Fluent Bit can connect to Elasticsearch (check logs)
- [ ] Test endpoint `/api/test/logging` generates logs
- [ ] Logs appear in Kibana within 5 seconds
- [ ] Can search by `traceId` and see both FE and BE logs
- [ ] Can filter by `app`, `env`, `http.path`, `status`
- [ ] `duration_ms` appears as numeric field in Kibana
- [ ] Error logs include stack traces in `error.stacktrace` field

## Maintenance

### Update Fluent Bit Configuration

After modifying `configmap-fluent-bit.yaml`:

```bash
kubectl apply -f configmap-fluent-bit.yaml
kubectl -n logging rollout restart daemonset/fluent-bit
```

### Update Elasticsearch Credentials

```bash
kubectl -n logging delete secret es-basic-auth
kubectl -n logging create secret generic es-basic-auth \
  --from-literal=ES_HOST=http://192.168.31.156:9200 \
  --from-literal=ES_USER=elastic \
  --from-literal=ES_PASS='NewPassword.'
kubectl -n logging rollout restart daemonset/fluent-bit
```

### Clean Up

To remove all logging resources:

```bash
kubectl delete -f daemonset-fluent-bit.yaml
kubectl delete -f configmap-fluent-bit.yaml
kubectl delete secret -n logging es-basic-auth
kubectl delete -f namespace.yaml
```

## Security Notes

- Secrets are stored in Kubernetes Secret (not in git)
- Fluent Bit uses RBAC with minimal permissions (read-only for pods/logs)
- Elasticsearch connection uses Basic Auth over HTTPS (TLS verify disabled for dev)
- In production, enable TLS verification and use certificates

## Performance Considerations

- Fluent Bit resource limits: 200m CPU, 256Mi memory per node
- Log buffer limit: 50MB per input
- Index rollover: 10GB or 7 days (configurable)
- Log retention: 30 days (configurable)

## Support

For issues or questions:
1. Check Fluent Bit logs: `kubectl -n logging logs -l app=fluent-bit`
2. Verify Elasticsearch connectivity
3. Check application logs format (must be JSON)
4. Review Kibana index patterns and field mappings

