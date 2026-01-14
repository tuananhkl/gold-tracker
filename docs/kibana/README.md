# Kibana learning path (Gold Tracker repo)

Mục tiêu: giúp bạn dùng Kibana **thực chiến** trên logs của `gold-tracker-api` trong repo này (Kubernetes + Filebeat đẩy về Elasticsearch).

## Bạn sẽ học gì?

- Discover: xem log, cột/field, filter nhanh, pin filter, time range.
- Query: **KQL** (chính), Lucene (khi cần regex / cú pháp đặc biệt).
- Lens: vẽ biểu đồ từ logs (error rate, latency p95, top endpoints, breakdown theo env/pod…).
- Dashboard: ghép panel, drilldown, controls.
- Alerting: rule theo ngưỡng (error spike, latency tăng, 5xx tăng…).
- Troubleshooting: vì sao “missing field”, vì sao query không ra, mapping sai, dữ liệu không vào.

## Bắt đầu từ đâu?

- Nếu bạn mới hoàn toàn: đọc theo thứ tự từ `00-` đến `11-`.
- Nếu bạn đã biết cơ bản: nhảy vào `03-kql-master.md` và `06-lens-quickstart.md`.

## Repo-specific: logs đang đi như thế nào?

Luồng logs hiện tại (tóm tắt):

- Pod logs (stdout) của app là **JSON** (Serilog compact tokens như `@t`, `@m`, `@l`).
- Filebeat (DaemonSet) đọc `/var/log/containers/*.log`, rồi:
  - decode JSON từ field `message`
  - map `@l` → `log.level`
  - set `event.dataset` theo `message_type`
  - output sang Elasticsearch theo index:
    - `gold-tracker-api-application-YYYY.MM.dd`
    - `gold-tracker-api-integration-YYYY.MM.dd`
    - `gold-tracker-api-security-YYYY.MM.dd`

Chi tiết xem `11-k8s-filebeat-pipeline.md`.

## Danh sách bài học

- `00-kibana-overview.md`
- `01-data-views-and-indexes-for-gold-tracker.md`
- `02-discover-workflow.md`
- `03-kql-master.md`
- `04-kql-vs-lucene.md`
- `05-practice-scenarios-20.md`
- `06-lens-quickstart.md`
- `07-lens-advanced-formulas-latency.md`
- `08-dashboards-and-drilldowns.md`
- `09-alerting-rules.md`
- `10-troubleshooting-common-issues.md`
- `11-k8s-filebeat-pipeline.md`

