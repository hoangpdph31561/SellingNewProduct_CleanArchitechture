# Observability — OpenTelemetry + Prometheus (đã tích hợp)

Ba trụ cột: **Traces** (request đi đâu), **Metrics** (nhanh/chậm, bao nhiêu), **Logs** (chuyện gì). Dự án đã cắm **OpenTelemetry (OTel)** cho traces + metrics, export metrics qua **Prometheus**.

## Nguyên tắc: chỉ API biết OTel

Các tầng trong (Domain/Application/Messaging) **không** phụ thuộc SDK nào — chúng chỉ dùng **BCL**: `ActivitySource` (trace) + `Meter` (metric). API subscribe theo **tên** rồi export. Nếu không ai subscribe, các lời gọi gần như miễn phí. Đây là cùng tinh thần Clean Arch: vendor telemetry là chi tiết ngoài cùng.

| Thành phần | File | Vai trò |
|---|---|---|
| Wiring OTel (traces+metrics+Prometheus+health) | [ObservabilitySetup.cs](../SellingNewProduct.API/Observability/ObservabilitySetup.cs) | Chỗ DUY NHẤT ref OTel SDK |
| Span + RED metric mỗi request | [TelemetryBehavior.cs](../SellingNewProduct.Application/Common/Behaviors/TelemetryBehavior.cs) | MediatR pipeline (ngoài cùng) |
| Nguồn trace/metric của app | [AppTelemetry.cs](../SellingNewProduct.Application/Common/Telemetry/AppTelemetry.cs) | `ActivitySource` + `Meter` (BCL) |
| Metric outbox | [OutboxMetrics.cs](../SellingNewProduct.Infrastructure.Messaging/Outbox/OutboxMetrics.cs) | published/failed/duration |
| Logs (Serilog → console + file) | [LoggingSetup.cs](../SellingNewProduct.API/Observability/LoggingSetup.cs) | ILogger routing + template |
| Correlation TraceId/SpanId trong log | [TraceContextEnricher.cs](../SellingNewProduct.API/Observability/TraceContextEnricher.cs) | gắn trace id vào mỗi dòng log |

## Traces (distributed tracing)

Một request có **một TraceId** xuyên suốt, chia thành các **Span**. Đã bật auto-instrument:
- **ASP.NET Core** — span cho request HTTP vào (bỏ qua `/health`, `/metrics`).
- **HttpClient** — span cho call HTTP ra (VNPay, SMTP nếu qua HTTP).
- **EF Core** — span cho mỗi query (cả SQL lẫn Mongo provider).
- **Custom** — `TelemetryBehavior` mở span `MediatR <RequestName>` cho mỗi command/query, gắn `request.type`, ghi exception khi lỗi.

Luồng ví dụ đọc đơn: `HTTP GET /api/orders/{id}` → span controller → span `MediatR GetOrderByIdQuery` → span EF Core (SQL) → (đọc chéo Mongo). Nhìn 1 trace thấy ngay **chậm ở đâu**.

## Metrics (RED + runtime)

- **Runtime** (`AddRuntimeInstrumentation`): GC, heap, thread pool — bắt đúng các nút thắt async đã học (thread pool starvation).
- **ASP.NET Core**: `http.server.request.duration` (histogram) → tính p95/p99, RPS, error rate.
- **App (RED)** — từ `TelemetryBehavior`, cho mỗi request type + outcome:
  - `app.request.total` (Counter) → **R**ate + **E**rrors.
  - `app.request.duration` (Histogram, ms) → **D**uration.
- **Outbox**: `outbox.published.total`, `outbox.failed.total`, `outbox.publish.duration` (theo destination Kafka/RabbitMQ + type).

Endpoint scrape: **`GET /metrics`** (Prometheus exporter). Prometheus kéo mỗi 15s.

## Chạy stack

```bash
# 1) API trên HTTP profile (để Prometheus scrape không vướng dev cert)
dotnet run --project SellingNewProduct.API --launch-profile http    # http://localhost:5044

# 2) Prometheus + Grafana + Alertmanager
docker compose -f deploy/observability/docker-compose.observability.yml up -d
#   Prometheus  http://localhost:9090   (Status > Targets: thấy sellingnewproduct-api UP)
#   Grafana     http://localhost:3000   (admin/admin123) — add datasource Prometheus http://prometheus:9090
```

Mở `http://localhost:5044/metrics` để xem tên metric **thực tế** trong build của bạn (OTel đổi `.`→`_`, thêm hậu tố unit, histogram có `_bucket/_sum/_count`) rồi chỉnh PromQL nếu cần.

## PromQL cho dự án (Four Golden Signals / RED)

```promql
# Traffic (RPS)
sum(rate(app_request_total[5m]))

# Errors (%)
sum(rate(app_request_total{outcome="error"}[5m])) / sum(rate(app_request_total[5m]))

# Latency p99 (HTTP, giây)
histogram_quantile(0.99, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le))

# Latency p95 theo từng command/query (ms)
histogram_quantile(0.95, sum(rate(app_request_duration_milliseconds_bucket[5m])) by (le, request_type))

# Outbox lỗi publish
sum(rate(outbox_failed_total[5m]))
```

Alert rules mẫu: [deploy/observability/alerts/api-alerts.yml](../deploy/observability/alerts/api-alerts.yml) (HighApplicationErrorRate, SlowHttpResponses, OutboxPublishFailing).

## Logs — ILogger → Serilog (console + file), correlation với trace

App vẫn viết log qua **`ILogger<T>`** như thường; Serilog **nhận** và ghi ra **console + file xoay vòng theo ngày** (`logs/log-YYYYMMDD.txt`, giữ 14 file — chỉnh qua `FileLogging:*`). Mỗi dòng có **TraceId/SpanId** (từ `TraceContextEnricher` đọc `Activity.Current`) → click từ log nhảy sang trace và ngược lại (Logs ↔ Traces correlation).

**Khác nhau theo môi trường (không đổi code, chỉ đổi `ASPNETCORE_ENVIRONMENT`):**

| | Development | Production |
|---|---|---|
| Level | `Debug` (chi tiết) | `Information` |
| Định dạng | text dễ đọc (`[HH:mm:ss INF] TraceId=... ...`) | **JSON** (CompactJsonFormatter) — 1 object/dòng |
| Đích | console + file `logs/` | console (stdout container) + file `/var/log/...` (mount volume) |
| Ai gom | đọc trực tiếp | log shipper (Promtail/Filebeat) → Loki/ELK |

Framework noise (`Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`) ép về `Warning` ở cả hai. `UseSerilogRequestLogging()` ghi 1 dòng tóm tắt mỗi request (method/path/status/elapsed + TraceId).

> Bạn hỏi "ILogger + logfile cho dev": đây chính là nó — dev dùng file text dễ đọc; prod tự chuyển JSON để máy đọc. Không cần cấu hình 2 lần.

## Dev vs Prod — KHÔNG hardcode localhost

Phần OTel ban đầu mặc định phù hợp **local**; giờ đã tách theo **config/môi trường** nên chạy được cả prod / không-localhost:

| | Dev (local) | Prod (cluster) |
|---|---|---|
| Traces đi đâu | in ra **Console** (xem span ngay) | **push OTLP** tới Collector |
| Metrics | scrape `/metrics` | scrape `/metrics` **và/hoặc** push OTLP |
| Cấu hình | `OpenTelemetry:OtlpEndpoint` để **trống** | đặt `OtlpEndpoint` = `http://otel-collector:4317` |

Cách đặt (không sửa code):
- `appsettings.json`: `OtlpEndpoint: ""` (dev).
- [appsettings.Production.json](../SellingNewProduct.API/appsettings.Production.json): `OtlpEndpoint: "http://otel-collector:4317"`.
- Hoặc **env var** (ưu tiên cho prod/secret): `OpenTelemetry__OtlpEndpoint=http://otel-collector:4317` (dấu `__` = cấp lồng).

`ObservabilitySetup` đọc `OtlpEndpoint`: có → thêm OTLP exporter (traces+metrics); dev → thêm Console exporter cho trace. `/metrics` **luôn** bật.

**Prometheus scrape khi KHÔNG localhost:** file [prometheus.yml](../deploy/observability/prometheus.yml) hiện trỏ `host.docker.internal:5044` cho máy dev. Trong prod:
- Đổi target sang **service DNS** (vd `sellingnewproduct-api:8080`) hoặc dùng **Kubernetes service discovery** (`kubernetes_sd_configs` role: pod + annotation `prometheus.io/scrape`).
- App bind theo `ASPNETCORE_URLS` / sau reverse proxy — không phụ thuộc localhost.
- Hoặc bỏ scrape trực tiếp, để **OTel Collector** nhận OTLP rồi Collector expose cho Prometheus (mô hình push, xem docker-compose observability của tài liệu gốc).

## Health checks (cho load balancer / K8s)

- **`GET /health/live`** — liveness: process còn sống không (không check dependency → không fail vì DB chậm).
- **`GET /health/ready`** — readiness: sẵn sàng nhận traffic không, gồm check **SQL store** (`AddDbContextCheck<AppDbContext>`, tag `ready`).

> Đã có đủ **3 trụ cột**: Traces (OTel) + Metrics (Prometheus) + Logs (Serilog file/JSON, correlate TraceId). Mở rộng được: cắm **Jaeger/Tempo** cho trace UI, **Loki** gom log JSON, **sampling** trace ở prod (`ParentBasedSampler(TraceIdRatioBasedSampler(0.1))`).
