# 02 — Discover workflow (xem log, filter, pin, table view)

Mục tiêu: bạn mở Discover là **điều tra được ngay**: lọc theo `log.level`, `endpoint`, `status`, `trace_id`, và biết cách “pin filter” để đi qua lại giữa nhiều view/panel.

## 1) Chọn đúng Data View và Time range

1) Vào **Analytics → Discover**
2) Chọn data view: `gold-tracker-logs-*` (index pattern `gold-tracker-api-*-*`)
3) Chọn time range:
   - Bắt đầu bằng “Last 24 hours”
   - Khi debug incident: dùng “Absolute” (từ giờ A đến giờ B) để tránh lệch

Nếu bạn “không thấy log”, 80% là do bước này.

## 2) Hiểu 3 khu vực chính trong Discover

- **Query bar**: bạn gõ KQL/Lucene (mặc định KQL)
- **Filters**: các filter “chip” (Add filter / pin / disable / invert)
- **Table**: danh sách documents; bạn có thể add column theo field

## 3) Thực hành: setup view chuẩn cho Gold Tracker

### Query baseline

Gõ KQL:

- `app: "gold-tracker-api"`

Nếu bạn muốn “chỉ app logs”:

- `event.dataset: "application"`

### Add các cột hay dùng

Ở panel fields, add column:

- `log.level`
- `event.dataset`
- `endpoint`
- `method`
- `status`
- `duration_ms`
- `trace_id`
- `correlation_id`
- `kubernetes.pod.name` (nếu có)

Mục tiêu: nhìn 1 dòng log là đoán được request nào, endpoint nào, lỗi mức nào, pod nào.

## 4) Filter đúng cách (đừng chỉ gõ query)

Trong Kibana, filter “chip” có lợi vì:

- bạn có thể **pin** để mang filter qua Dashboard/Lens
- bạn có thể disable tạm để so sánh
- bạn có thể invert để “loại trừ” nhanh

### Ví dụ 1: chỉ xem error

- Add filter: `log.level` **is** `error`

### Ví dụ 2: loại trừ health check

Bạn sẽ thấy `/healthz` rất nhiều. Loại trừ:

- Add filter: `endpoint` **is not** `/healthz`

Hoặc KQL:

- `NOT endpoint: "/healthz"`

### Ví dụ 3: chỉ xem 5xx

KQL:

- `status >= 500`

## 5) Điều tra theo trace_id (playbook siêu quan trọng)

Khi bạn có `trace_id` (từ response header hoặc từ 1 log error):

1) Copy `trace_id`
2) Query:
   - `trace_id: "T5VMASNOT5"` (ví dụ)
3) Sort theo time ascending
4) Add columns: `@m`, `log.level`, `endpoint`, `status`, `duration_ms`

Bạn sẽ thấy chuỗi:

- `RequestStarted`
- log business/integration khác (nếu có)
- `RequestCompleted` hoặc error

## 6) Dùng “Surrounding documents” khi cần ngữ cảnh

Khi 1 log error không đủ ngữ cảnh:

- Click vào 1 document → chọn **View surrounding documents**

Trick này giúp bạn xem các log “trước/sau” cùng thời điểm, đôi khi tìm ra nguyên nhân (spike, deploy, pod restart…).

## 7) Lưu search (Saved search)

Nếu bạn có một “view chuẩn”:

- Query: `app: "gold-tracker-api" AND NOT endpoint:"/healthz"`
- Filters: `env=dev`, `region=vn`
- Columns: chuẩn

Thì hãy **Save** để lần sau mở là dùng ngay (và có thể add vào Dashboard).

