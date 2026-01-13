# 03 — KQL master (gõ query như master, bám đúng logs Gold Tracker)

KQL (Kibana Query Language) là thứ bạn dùng 90% thời gian trong Discover/Lens.

## 1) Các field bạn sẽ query nhiều nhất (repo này)

- `log.level`: `info|warn|error|fatal` (đã normalize từ `@l`)
- `event.dataset`: `application|integration|security`
- `endpoint`, `method`, `status`, `duration_ms`
- `trace_id`, `correlation_id`, `context_id`, `span_id`
- `message_type` (từ app): `Application|Integration|SecurityAudit`
- `@m` (message): ví dụ `RequestCompleted`

## 2) Cú pháp KQL bạn cần thuộc

### Equals / match

- `log.level: error`
- `event.dataset: "application"`
- `endpoint: "/healthz"`

### AND / OR / NOT

- `log.level: error AND event.dataset: application`
- `(status >= 500 OR log.level: error) AND NOT endpoint: "/healthz"`

### Range (số)

- `status >= 500`
- `duration_ms >= 1000`
- `duration_ms >= 200 AND duration_ms < 1000`

### Exists (field có/không có)

- `trace_id: *`
- `NOT duration_ms: *`

Dùng khi bạn nghi ngờ ingest/formatter làm thiếu field.

### Wildcard (chuỗi)

KQL wildcard thường dùng `*`:

- `endpoint: "/api/*"`

Lưu ý: wildcard có thể tốn tài nguyên hơn equals (nhất là prefix không cố định).

## 3) Các query mẫu “thực chiến”

### A) “Có lỗi gì trong 30 phút qua?”

- `app: "gold-tracker-api" AND log.level: (error OR fatal)`

Nếu muốn bỏ noise health check:

- `app: "gold-tracker-api" AND log.level: (error OR fatal) AND NOT endpoint: "/healthz"`

### B) “Endpoint nào đang 5xx nhiều?”

Trong Discover:

- `status >= 500 AND event.dataset: application`

Sau đó bạn có thể dùng Lens (bài 06) để Top values theo `endpoint`.

### C) “Request chậm (p95-ish)”

KQL (lọc thô):

- `event.dataset: application AND duration_ms >= 1000`

Sau đó dùng Lens percentile để đo p95/p99 chuẩn.

### D) “Trace theo 1 request”

- `trace_id: "T5VMASNOT5"`

### E) “Chỉ xem integration logs”

Bạn có 2 lựa chọn (đều đúng trong repo):

- `event.dataset: "integration"`
- hoặc `message_type: "Integration"`

Khuyến nghị: dùng `event.dataset` vì đó là thứ Filebeat route index + consistent.

### F) “Tìm theo message”

Nếu bạn biết message `@m` (Serilog message string):

- `@m: "RequestCompleted"`

Nếu bạn muốn tìm text trong document nhưng không biết field nào:

- dùng search text theo UI (tuỳ config), hoặc chuyển Lucene (bài 04).

## 4) Những lỗi hay gặp khi gõ KQL

### Lỗi 1: nhầm kiểu dữ liệu

`status` là số, nên:

- đúng: `status >= 500`
- sai: `status: "500"`

### Lỗi 2: field không được index (hoặc không tồn tại)

Nếu query `biz_keys.something: "x"` mà không ra:

- kiểm tra document có field đó không (expand 1 doc trong Discover)
- kiểm tra field type trong data view (Stack Management → Data Views)

### Lỗi 3: dấu ngoặc và ưu tiên toán tử

Luôn dùng ngoặc khi có OR:

- đúng: `(log.level: error OR status >= 500) AND NOT endpoint:"/healthz"`
- dễ sai logic: `log.level: error OR status >= 500 AND NOT endpoint:"/healthz"`

## 5) Bài tập (follow để lên tay)

### Bài 1 — Lọc 5xx, group theo endpoint (chuẩn bị cho Lens)

1) Discover query: `status >= 500 AND event.dataset: application`
2) Add columns: `endpoint`, `status`, `trace_id`
3) Pick 1 trace_id bất kỳ và mở chain logs theo trace_id

### Bài 2 — Tìm request chậm

1) Query: `duration_ms >= 1000 AND event.dataset: application`
2) Sort desc theo `@t`
3) Check top 5 slow requests: endpoint nào chiếm nhiều?

