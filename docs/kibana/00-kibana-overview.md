# 00 — Kibana overview (học để làm gì, khi nào dùng cái gì)

## Kibana là gì (ở góc nhìn DevOps)

Kibana là UI để:

- **Search/Filter** dữ liệu trong Elasticsearch (logs, metrics, traces, security events…).
- **Visualize** (Lens) → biểu đồ.
- **Dashboards** → ghép nhiều biểu đồ thành “màn hình vận hành”.
- **Alerting** → cảnh báo dựa trên dữ liệu.

Trong repo này, bạn chủ yếu dùng Kibana để:

- debug lỗi production-like: trace một request qua `trace_id/correlation_id`
- kiểm tra error spike / latency tăng
- quan sát top endpoint bị lỗi, top lỗi theo loại, top pod gây lỗi

## Các màn hình bạn sẽ dùng nhiều nhất

- **Discover**: “mở thô” logs; tốt nhất để điều tra sự cố.
- **Lens**: “biến logs thành biểu đồ”; tốt nhất để làm dashboard và theo dõi xu hướng.
- **Dashboards**: “bảng điều khiển”; cho vận hành và chia sẻ.
- **Stack Management → Data Views**: cấu hình data view (index pattern) để query/visualize.

## Khi nào dùng Discover vs Lens?

- **Discover** dùng khi:
  - bạn cần xem **log raw** (json fields), đọc message/exception
  - bạn cần pivot nhanh theo filter: endpoint, status, pod, trace_id…
  - bạn cần “mò” field nào tồn tại, field nào thiếu

- **Lens** dùng khi:
  - bạn cần trend theo thời gian (error rate, request count)
  - bạn cần top values (top endpoints 5xx, top pod error)
  - bạn cần thống kê (p95 latency, tỉ lệ 5xx)

## Điều quan trọng nhất trong Kibana: Time range + Data View

90% “không thấy log” là do:

- đang chọn **sai time range** (ví dụ “Last 15 minutes” nhưng sự cố xảy ra hôm qua)
- đang chọn **sai data view/index pattern** (ví dụ chỉ chọn `application-*` nhưng log bạn tìm nằm ở `integration-*`)

## Từ khóa bạn sẽ gặp

- **Index**: “bảng” dữ liệu trong Elasticsearch.
- **Index pattern / Data View**: pattern để Kibana đọc nhiều index cùng lúc, ví dụ `gold-tracker-api-application-*`.
- **Field**: cột/thuộc tính; ví dụ `log.level`, `endpoint`, `duration_ms`.
- **KQL**: Kibana Query Language (dễ dùng, default).
- **Lucene query**: query kiểu cũ, mạnh hơn ở một số cú pháp (regex/wildcard đặc biệt).

