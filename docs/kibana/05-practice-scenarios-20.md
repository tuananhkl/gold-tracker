# 05 — Thực chiến Kibana: 20 tình huống (đề trước, đáp án sau)

Bạn hãy **chỉ đọc phần “Đề bài” trước**, tự làm trong Kibana 10–20 phút, rồi mới kéo xuống phần “Mindset + lời giải” để đối chiếu.

---

## A) Đề bài (KHÔNG SPOIL)

### 1) Error spike sau deploy

Bạn nhận alert: “Error tăng đột biến trong 10 phút gần đây”. Làm sao xác định:

- error đến từ endpoint nào?
- pod nào là thủ phạm?
- có liên quan deploy/restart không?

### 2) Latency tăng nhưng error không tăng

Người dùng report: “API chậm hẳn từ 14:05”. Cần tìm:

- endpoint nào chậm?
- p95/p99 tăng ở đâu?
- có phải chỉ 1 pod bị chậm không?

### 3) 5xx chỉ xảy ra ở 1 endpoint

SRE nói: “5xx có nhưng chỉ ở vài request”. Cần:

- lọc 5xx
- tìm top endpoint theo 5xx
- lấy 1 trace_id tiêu biểu để đọc chain log

### 4) 5xx tăng nhưng app log không có error

Dashboard cho thấy `status >= 500` tăng, nhưng `log.level:error` gần như không có. Vì sao?

### 5) Health check làm “bẩn” biểu đồ

`/healthz` spam quá nhiều, làm chart request count khó đọc. Làm sao lọc đúng cách để:

- chart vẫn chuẩn
- nhưng khi cần vẫn drilldown thấy health check

### 6) Tìm request chậm nhất hôm nay

Bạn muốn tìm top 20 request chậm nhất hôm nay (theo `duration_ms`). Làm sao?

### 7) Trace theo một request từ client

Client gửi bạn `X-Trace-Id = ABCDE12345`. Bạn cần:

- tìm toàn bộ log của request đó
- xác định endpoint, status, duration

### 8) Một pod “ồn ào” (log volume cao bất thường)

Log volume tăng mạnh nhưng traffic không tăng. Cần tìm:

- pod nào log nhiều nhất?
- message nào spam?

### 9) Integration bị fail ngắt quãng

Team nói: “Integration đôi lúc fail, đôi lúc OK”. Bạn cần:

- lọc đúng integration logs
- xác định tỷ lệ fail theo thời gian
- lấy ví dụ fail và trace ngược về request gốc (nếu có)

### 10) Security audit: truy vết các action nhạy cảm

Bạn cần list các event `SecurityAudit` trong 24h qua và trả lời:

- ai/đâu/endpoint nào thường xuyên xảy ra?
- có spike thời gian nào bất thường?

### 11) 401/403 spike

Một dịch vụ upstream báo “nhiều 401/403”. Bạn cần:

- xác định endpoint nào trả 401/403 nhiều
- correlate với user agent / client ip (nếu có)

### 12) Một loại lỗi “mất field log.level”

Lens slice theo `log.level` thấy nhiều “(missing)”. Bạn cần xác định:

- log nào thiếu?
- thuộc dataset nào?
- root cause là app hay ingest?

### 13) Field `duration_ms` bị thiếu một phần

Bạn muốn làm chart latency nhưng nhiều doc thiếu `duration_ms`. Tìm nguyên nhân và scope ảnh hưởng.

### 14) “Không thấy log” trong Kibana nhưng `kubectl logs` có

Bạn nhìn `kubectl logs` thấy rõ, nhưng Kibana query không ra. Bạn cần check gì theo thứ tự?

### 15) Log vào sai index (application vs integration vs security)

Bạn nghi có log “Integration” nhưng lại rơi vào index application. Làm sao verify và fix?

### 16) Sai time (lệch timezone) làm hiểu nhầm incident

Một incident nhìn như xảy ra lúc 07:00 nhưng thực tế là 14:00 (VN). Làm sao phát hiện và tránh?

### 17) Một endpoint trả 200 nhưng duration_ms cực cao

Không có error, status=200, nhưng khách complain. Làm sao:

- tìm endpoint
- đo p95/p99
- chia theo pod

### 18) Một message cụ thể biến mất sau một thay đổi

Trước đây có message `RequestStarted`, giờ không thấy nữa. Là do code hay do pipeline? Làm sao check nhanh?

### 19) Nghi ngờ Elasticsearch ingest chậm (lag)

Bạn nghi log vào ES trễ vài phút. Làm sao xác minh bằng `@t` và thời gian nhận?

### 20) “Mapping conflict” làm Lens không vẽ được

Lens báo field type conflict hoặc không aggregate được. Làm sao tìm index/field gây conflict và hướng xử lý?

---

## B) Mindset của expert DevOps (đọc trước khi xem lời giải)

### Nguyên tắc 1: Luôn khóa đúng phạm vi trước

- **Time range**: chọn “Absolute” khi điều tra incident.
- **Data view**: ưu tiên `gold-tracker-api-*-*` để không miss dataset.
- **Noise removal**: loại `/healthz` và các request không liên quan bằng filter (dễ bật/tắt).

### Nguyên tắc 2: Chia nhỏ câu hỏi thành 3 lớp

- **What**: chuyện gì đang xảy ra? (error/latency/volume)
- **Where**: ở đâu? (endpoint/pod/dataset)
- **Why**: vì sao? (root cause: code vs infra vs ingest)

### Nguyên tắc 3: Điều tra phải có “đường quay về log raw”

Khi Lens/Dashboard cho thấy metric bất thường, bạn phải drilldown về Discover để xem doc raw:

- `trace_id` để gom theo request
- `@m` để nhìn message type (ví dụ `RequestCompleted`)
- fields: `endpoint`, `status`, `duration_ms`, `kubernetes.pod.name`

### Nguyên tắc 4: “Missing field” luôn có 2 bucket

1) **App không emit field** (source problem)
2) **Ingest parse/mapping fail** (pipeline problem)

Trong repo này, ingest chính nằm ở `k8s/efk-remote/filebeat-configmap.yaml`:

- `decode_json_fields` từ `message`
- map `@l` → `log.level`
- route index theo `event.dataset` (set từ `message_type`)

---

## C) Lời giải / hướng dẫn chi tiết (SPOILERS)

Gợi ý: khi làm, luôn bắt đầu bằng query baseline:

- `app: "gold-tracker-api"`

Và (tuỳ bài) thêm:

- `event.dataset: "application"` hoặc `"integration"` hoặc `"security"`

### 1) Error spike sau deploy

1) Discover (data view `gold-tracker-api-*-*`), time range “Last 30 minutes”.
2) KQL:
   - `log.level: (error OR fatal) AND app: "gold-tracker-api"`
3) Add columns: `endpoint`, `status`, `kubernetes.pod.name`, `trace_id`.
4) Quick pivot:
   - Lens: metric “Count” + breakdown Top values `endpoint` (hoặc trong Discover dùng sidebar field top values).
   - Tiếp theo breakdown theo `kubernetes.pod.name`.
5) Verify deploy/restart:
   - filter theo `kubernetes.pod.name` nghi ngờ và xem thời điểm bắt đầu error.
   - nếu có log “pod restarted”/gap logs: check thời điểm log bắt đầu lại.

### 2) Latency tăng nhưng error không tăng

1) Discover query:
   - `event.dataset: application AND app:"gold-tracker-api" AND NOT endpoint:"/healthz"`
2) Chuyển Lens:
   - Visualization: Line chart
   - Metric: Percentile `duration_ms` (p95, p99)
   - Break down: `endpoint` (Top values 5) để xem endpoint nào kéo p95.
3) Split by `kubernetes.pod.name` để xem có 1 pod outlier không.

### 3) 5xx chỉ xảy ra ở 1 endpoint

1) Discover KQL:
   - `status >= 500 AND event.dataset: application`
2) Add column `endpoint`, sort theo `@t` desc.
3) Tìm endpoint top bằng Lens (Top values `endpoint` by Count).
4) Chọn 1 log 5xx, copy `trace_id`, query `trace_id:"..."` để đọc chain.

### 4) 5xx tăng nhưng app log không có error

Khả năng thường gặp:

- app trả status 5xx nhưng code chỉ log `Information` (`RequestCompleted`) và không log `Error`.
- hoặc status 5xx phát sinh ở reverse-proxy/ingress (log không nằm trong app index).

Check:

1) Discover: `status >= 500 AND event.dataset: application`
2) Inspect 1 doc:
   - xem `@m` là gì (có phải `RequestCompleted`?)
   - xem `log.level` có phải `info`?
3) Nếu đúng: cần bổ sung log error ở app hoặc enrich thêm fields (ngoài scope Kibana).

### 5) Health check làm “bẩn” biểu đồ

Khuyến nghị: dùng filter chip (pinnable) thay vì hardcode query.

1) Add filter: `endpoint` is not `/healthz`
2) **Pin** filter đó.
3) Khi drilldown cần xem health check: disable filter tạm.

### 6) Tìm request chậm nhất hôm nay

1) Time range: “Today” (hoặc absolute).
2) Discover KQL:
   - `event.dataset: application AND duration_ms: * AND NOT endpoint:"/healthz"`
3) Sort theo `duration_ms` desc (nếu UI hỗ trợ sort numeric column).
4) Nếu không sort được trực tiếp: dùng Lens “Top values”:
   - Metric: Max `duration_ms`
   - Breakdown: `trace_id` (Top 20)
   - rồi click vào 1 trace_id để drilldown Discover.

### 7) Trace theo một request từ client

1) Discover query:
   - `trace_id: "ABCDE12345"`
2) Add columns: `endpoint`, `status`, `duration_ms`, `@m`, `log.level`
3) Sort asc để thấy flow.

### 8) Một pod “ồn ào” (log volume cao bất thường)

1) Lens:
   - Metric: Count
   - Break down: Top values `kubernetes.pod.name`
2) Chọn pod top1, add filter `kubernetes.pod.name:"..."`
3) Trong Discover, xem `@m` top values hoặc query:
   - `@m: *` và quan sát message nào lặp lại.

### 9) Integration bị fail ngắt quãng

1) Discover KQL:
   - `event.dataset: integration AND (log.level: error OR status >= 500)`
2) Lens: line chart Count theo time.
3) Lấy 1 doc fail, nếu có `trace_id` thì query theo trace để xem request gốc.

### 10) Security audit: truy vết action nhạy cảm

1) Discover KQL:
   - `event.dataset: security AND app:"gold-tracker-api"`
2) Add columns: `endpoint`, `method`, `client_ip`, `user_agent` (nếu có).
3) Lens:
   - Top values `endpoint`
   - Timeline Count để xem spike.

### 11) 401/403 spike

1) Discover:
   - `(status: 401 OR status: 403) AND event.dataset: application`
2) Lens Top values theo `endpoint`.
3) Add columns `user_agent`, `client_ip` để xem có 1 client spam không.

### 12) Một loại lỗi “mất field log.level”

1) Discover filter:
   - `NOT log.level: *` (hoặc `log.level: *` rồi invert)
2) Inspect doc raw xem có `@l` không.
3) Nếu `@l` không có:
   - app/formatter không emit → fix ở app (repo đã có `AlwaysLevelRenderedCompactJsonFormatter`).
4) Nếu `@l` có nhưng `log.level` thiếu:
   - pipeline mapping/script lỗi → check filebeat processor map `@l` → `log.level`.

### 13) Field `duration_ms` bị thiếu một phần

1) Discover:
   - `event.dataset: application AND NOT duration_ms: *`
2) Inspect doc xem `@m` là gì:
   - nếu không phải request log (`RequestCompleted`) thì duration_ms thiếu là bình thường.
3) Nếu là `RequestCompleted` mà vẫn thiếu:
   - suspect middleware enrich bị bypass/exception path.

### 14) “Không thấy log” trong Kibana nhưng `kubectl logs` có

Checklist theo thứ tự:

1) Time range đúng chưa (UTC vs local)?
2) Data view đúng chưa (`gold-tracker-api-*-*`)?
3) KQL quá chặt không? thử chỉ `app:"gold-tracker-api"`.
4) Inspect ingest:
   - có decode JSON fail không (message không parse)?
   - index name có đúng không?
5) Nếu cần: search theo `kubernetes.pod.name` hoặc một token unique trong message.

### 15) Log vào sai index (application vs integration vs security)

1) Discover tìm doc nghi ngờ (lọc theo `message_type:"Integration"`).
2) Check `event.dataset`:
   - nếu `message_type` là Integration nhưng `event.dataset` lại `application`, thì filebeat script `dataset-map` không match.
3) Kiểm tra field `message_type` có đúng casing không (`Integration` vs `integration`).

### 16) Sai time (lệch timezone) làm hiểu nhầm incident

1) So sánh `@t` (UTC) với `ts_vn` (nếu có).
2) Trong Kibana, đảm bảo “Advanced settings” timezone và timepicker hiểu đúng.
3) Khi điều tra incident: dùng “Absolute time” theo UTC hoặc theo timezone nhất quán.

### 17) Một endpoint trả 200 nhưng duration_ms cực cao

1) Discover:
   - `status: 200 AND duration_ms >= 2000 AND event.dataset: application`
2) Lens:
   - Percentile duration_ms (p95/p99) theo `endpoint`
   - split theo `kubernetes.pod.name` để tìm outlier pod.

### 18) Một message cụ thể biến mất sau một thay đổi

1) Discover query:
   - `@m: "RequestStarted" AND event.dataset: application`
2) Expand time range trước/sau deploy.
3) Nếu trước có sau không:
   - check code change (logging) hoặc pipeline decode (field không còn ở root).
4) Verify raw doc: còn `@m` không? hay bị decode_json overwrite?

### 19) Nghi ngờ Elasticsearch ingest chậm (lag)

Bạn cần 2 timestamps: event time và ingest time.

1) Kiểm tra doc có `@timestamp` / ingest timestamp không.
2) Nếu có cả `@t` (event) và `@timestamp` (ingest):
   - so sánh chênh lệch (ingest - event).
3) Nếu không có ingest time:
   - tạm dùng “khi bạn thấy nó xuất hiện” vs `@t` (kém chuẩn), và cân nhắc add ingest pipeline later.

### 20) “Mapping conflict” làm Lens không vẽ được

1) Trong data view, vào field detail để xem type.
2) Dùng Discover sample docs từ nhiều index ngày khác nhau, inspect field type/value.
3) Xác định index nào “khác kiểu” (ví dụ hôm nay là number, hôm qua là string).
4) Hướng xử lý:
   - fix tại source (emit consistent type)
   - hoặc tách data view theo index pattern hẹp hơn (chỉ ngày đúng)
   - hoặc dùng runtime field / ingest normalize (nâng cao)

