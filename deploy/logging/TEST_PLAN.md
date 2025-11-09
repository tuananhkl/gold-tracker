# Logging Setup Test Plan

## Prerequisites Check

```bash
# 1. Verify Kubernetes cluster is running
kubectl cluster-info

# 2. Verify Elasticsearch is accessible
curl -u elastic:'Tuananh123.' http://192.168.31.156:9200/_cluster/health
```

## Deployment Steps

```bash
# 1. Apply logging namespace
kubectl apply -f deploy/logging/namespace.yaml

# 2. Create Elasticsearch secret
kubectl -n logging create secret generic es-basic-auth \
  --from-literal=ES_HOST=http://192.168.31.156:9200 \
  --from-literal=ES_USER=elastic \
  --from-literal=ES_PASS='Tuananh123.'

# 3. Apply ConfigMap
kubectl apply -f deploy/logging/configmap-fluent-bit.yaml

# 4. Deploy Fluent Bit
kubectl apply -f deploy/logging/daemonset-fluent-bit.yaml

# 5. Wait for pods
kubectl -n logging wait --for=condition=ready pod -l app=fluent-bit --timeout=120s
```

## Verification Checklist

### 1. Fluent Bit Pods Running

```bash
kubectl -n logging get pods -l app=fluent-bit
```

**Expected**: All pods in `Running` state, `READY 1/1`

### 2. Test Logging Endpoint

```bash
# Generate test logs with trace ID
TRACE_ID="test-$(date +%s)"
curl -H "x-trace-id: $TRACE_ID" \
  http://testcursor.derrick.local/gold/api/test/logging | jq .

echo "Trace ID: $TRACE_ID"
```

**Expected**: JSON response with `traceId` field

### 3. Verify Logs in Elasticsearch

Wait 5-10 seconds, then check Elasticsearch:

```bash
# List indices
curl -u elastic:'Tuananh123.' \
  "http://192.168.31.156:9200/_cat/indices/logs-*?v"

# Search for trace ID
curl -u elastic:'Tuananh123.' \
  "http://192.168.31.156:9200/logs-dev-*/_search?q=traceId:$TRACE_ID" | jq '.hits.hits[0:3]'
```

### 4. Verify in Kibana

1. Open Kibana: `http://192.168.31.156:5601`
2. Go to **Discover**
3. Create data view:
   - Index pattern: `logs-dev-*`
   - Time field: `@timestamp`
4. Search for trace ID: `traceId:"$TRACE_ID"`
5. Verify both INFO and ERROR logs appear
6. Verify fields: `app`, `env`, `http.path`, `status`, `duration_ms`

### 5. Verify Log Correlation

```bash
# Make a request that triggers both FE and BE logs
TRACE_ID="corr-$(date +%s)"
curl -H "x-trace-id: $TRACE_ID" \
  "http://testcursor.derrick.local/gold/api/prices/latest?kind=ring" | jq '.items[0] | {brand, form}'

# In Kibana, search for:
# traceId:"$TRACE_ID"
# Should see logs from both gold-tracker-api and gold-tracker-ui
```

## Expected Results

✅ Fluent Bit DaemonSet pods running on all nodes
✅ Test endpoint generates logs visible in Kibana within 5 seconds
✅ Can search by `traceId` and see both FE & BE logs
✅ Can filter by `app`, `env`, `http.path`, `status`
✅ `duration_ms` appears as numeric field
✅ Error logs include stack traces in `error.stacktrace` field

## Troubleshooting

If logs don't appear:

1. Check Fluent Bit logs: `kubectl -n logging logs -l app=fluent-bit --tail=50`
2. Verify Elasticsearch connectivity from Fluent Bit pod
3. Check application logs format (must be JSON)
4. Verify index pattern in Kibana matches actual index names
