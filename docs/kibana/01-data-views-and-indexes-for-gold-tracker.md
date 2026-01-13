# 01 — Data views & index pattern cho Gold Tracker (repo-specific)

Tài liệu này bám đúng cách logs đang được ingest trong repo `gold-tracker`.

## Logs được lưu ở những index nào?

Filebeat đang route log theo `event.dataset` (set từ `message_type`):

- **Application logs**: `gold-tracker-api-application-YYYY.MM.dd`
- **Integration logs**: `gold-tracker-api-integration-YYYY.MM.dd`
- **Security logs**: `gold-tracker-api-security-YYYY.MM.dd`

Gợi ý: nếu bạn muốn query “tất cả”, dùng pattern tổng:

- `gold-tracker-api-*-*`

## Các field quan trọng (bạn sẽ filter/visualize suốt)

### Log event core

- **`@t`**: timestamp (UTC)
- **`@m`**: message (ví dụ `RequestCompleted`)
- **`@l`**: level token (`trace|debug|info|warn|error|fatal`)
- **`log.level`**: được Filebeat map từ `@l` (đây là field bạn nên dùng trong Kibana)

### Trace & correlation (rất hữu dụng khi debug theo request)

Middleware đẩy các field này vào log:

- **`trace_id`**
- **`correlation_id`**
- **`context_id`**
- **`span_id`**

Bạn sẽ dùng chúng để “gom toàn bộ log của 1 request”, hoặc nối các log liên quan.

### HTTP request fields (trong `TraceContextMiddleware`)

- **`endpoint`** (ví dụ `/healthz`)
- **`method`** (GET/POST…)
- **`status`** (HTTP status code)
- **`duration_ms`** (thời gian xử lý request)

### Business / categorization

- **`message_type`**: `Application` | `Integration` | `SecurityAudit`
- **`event.dataset`**: `application` | `integration` | `security` (do Filebeat set)
- **`biz_keys`**: object chứa các key nghiệp vụ (tùy request)
- **`app`**, **`env`**, **`region`**: Filebeat add_fields (repo đang set `gold-tracker-api`, `dev`, `vn`)

## Tạo Data View (index pattern) trong Kibana

Vào: **Stack Management → Data Views → Create data view**

Gợi ý tạo 2 data view:

- **`gold-tracker-logs-*`**:
  - **Index pattern**: `gold-tracker-api-*-*`
  - **Time field**: `@t` (nếu không thấy, thử `@timestamp` rồi quay lại phần Troubleshooting)
- **`gold-tracker-application-*`**:
  - **Index pattern**: `gold-tracker-api-application-*`
  - **Time field**: `@t`

Khi bạn đã có data view tổng (`gold-tracker-api-*-*`), Discover/Lens sẽ dễ làm hơn vì bạn không phải đổi view liên tục.

## Thực hành nhanh (5 phút)

1) Mở Discover với data view `gold-tracker-logs-*`
2) Chọn time range “Last 24 hours”
3) Query:
   - `app: "gold-tracker-api"`
   - `log.level: error`
4) Thêm cột: `endpoint`, `status`, `duration_ms`, `trace_id`

