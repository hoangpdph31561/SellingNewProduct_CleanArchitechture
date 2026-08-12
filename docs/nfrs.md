# Non-Functional Requirements (NFRs) — ánh xạ vào dự án

NFR = hệ thống hoạt động **thế nào** (chất lượng, "-ilities"), khác Functional = hệ thống **làm gì**. Bảng dưới map từng NFR về **thứ đã có thật** trong SellingNewProduct + chỗ còn thiếu.

## Bảng NFR → hiện trạng dự án

| NFR | Kỹ thuật chuẩn | Trong dự án (đã có) | Còn thiếu (mở rộng) |
|---|---|---|---|
| **Reliability / Consistency** | Saga, Outbox, Idempotency, UnitOfWork | Saga hybrid + compensation + pivot; Outbox (SQL+Mongo) atomic; UnitOfWork đa-aggregate | dedup idempotency key ở consumer |
| **Availability** | Circuit Breaker, Bulkhead, Graceful Degradation, Health check, Failover | Polly breaker + bulkhead (gateway + cross-store); fallback "(unknown)"/0; `/health/live` + `/health/ready`; app chạy khi broker tắt | multi-instance/region, Alertmanager routing thật |
| **Scalability** | Stateless, Load Balancing, CQRS, Caching, MQ | API stateless (JWT, không session); CQRS read/write tách; Mongo read = secondary; Kafka/RabbitMQ async | **Redis cache** (chưa có), read replica SQL, HPA/K8s |
| **Performance** | async, batch, pooling, index | async all-the-way; `Task.WhenAll` cross-store; batch/chunk bulk write; connection pool | cache report, index tuning |
| **Observability** | Metrics/Logs/Traces, RED, Golden Signals | OTel traces (ASP.NET/HttpClient/EFCore/custom) + metrics (RED + runtime + outbox) + Prometheus `/metrics` | Logs structured + correlation, Jaeger UI |
| **Security** | AuthN/AuthZ, HTTPS, mã hoá | JWT bearer HS256; `[Authorize]` role-based; password hash + verify | mTLS, PCI cho card, secret manager |
| **Maintainability** | Clean Arch, DDD, tách tầng, test | Clean Arch + DDD + CQRS + Provider swap; 1 file/feature | test coverage |

## Availability — số & khái niệm

| SLA | Downtime/năm |
|---|---|
| 99% | ~87.6 giờ |
| 99.9% | ~8.7 giờ |
| 99.99% | ~52.6 phút |
| 99.999% | ~5.26 phút |

- **MTTR** (phục hồi trung bình), **MTBF** (giữa 2 lỗi), **RPO** (mất tối đa bao nhiêu data), **RTO** (khôi phục trong bao lâu).
- Kỹ thuật đã áp: **Circuit Breaker** (chặn cascade), **Bulkhead** (chặn resource exhaustion), **Graceful Degradation** (fallback), **Health Checks** (auto-restart/readiness). Xem [polly-circuit-breaker.md](polly-circuit-breaker.md), [polly-bulkhead.md](polly-bulkhead.md), [observability.md](observability.md).

## Scalability — 2 hướng

- **Vertical (scale up):** thêm CPU/RAM cho 1 máy — dễ, có trần.
- **Horizontal (scale out):** thêm instance — cần **stateless** (dự án đạt: JWT, không session tại service) + load balancer.
- Kỹ thuật: Load Balancing, **Caching (Redis — chưa có)**, DB Sharding, **Message Queue (Kafka/RabbitMQ — đã có)**, CDN, Stateless.

## CAP (hệ phân tán)

Chỉ chọn tối đa 2/3: **C**onsistency, **A**vailability, **P**artition tolerance. Dự án polyglot (SQL+Mongo) chọn nhất quán **cuối cùng** qua Saga (ưu tiên A+P giữa 2 store, C đạt eventually) thay vì distributed transaction (thứ đắt/khó). Cross-store read khi store kia chết → fallback (ưu A hơn C tạm thời).

## Monitoring strategies (đã có metric để làm)

- **RED** (service): **R**ate, **E**rrors, **D**uration → có từ `app.request.*`.
- **USE** (resource): **U**tilization, **S**aturation, **E**rrors → runtime metrics (GC, threadpool).
- **Four Golden Signals** (Google SRE): Latency, Traffic, Errors, Saturation.
- Alert tiering: Critical→page, Warning→Slack, Info→log. Rules: [api-alerts.yml](../deploy/observability/alerts/api-alerts.yml).

## System-level decisions (đã quyết trong dự án)

| Quyết định | Chọn | Lý do |
|---|---|---|
| Monolith vs Microservices | **Modular monolith** | đơn giản; Outbox+Kafka chừa đường tách sau |
| Sync vs Async | **Async** (Kafka/RabbitMQ) cho side-effect | không block checkout, chịu spike |
| SQL vs NoSQL | **Cả hai** (Order/Payment=SQL, catalog=Mongo) | ACID cho tiền, schema linh hoạt cho catalog |
| Distributed txn | **Saga** (không 2PC) | 2PC đắt/khó across store |
| Read model | **CQRS** + Mongo secondary read | tối ưu đọc riêng |

→ Chi tiết pattern + so sánh công nghệ: [TONG-HOP-KIEN-THUC.md](TONG-HOP-KIEN-THUC.md).
