# 📚 Bản đồ tài liệu — SellingNewProduct

Index toàn bộ docs, nhóm theo chủ đề, để **tìm kiến thức nhanh**. Mỗi dòng: doc → nó trả lời câu hỏi gì → đọc khi nào.

> **Bắt đầu từ đâu?** Nếu mới vào: đọc theo **Lộ trình** ngay dưới. Muốn tra nhanh 1 khái niệm/pattern: nhảy tới bảng nhóm tương ứng. Muốn 1 file gom tất cả: [TONG-HOP-KIEN-THUC.md](TONG-HOP-KIEN-THUC.md).

---

## 🧭 Lộ trình đọc (từ nền tảng → nâng cao)

1. [ARCHITECTURE.md](ARCHITECTURE.md) — hiểu 4 tầng + Dependency Rule (nền tảng, đọc trước tiên).
2. [DOMAIN_MODEL.md](DOMAIN_MODEL.md) — nghiệp vụ: aggregate, value object, business rule.
3. [code/README.md](code/README.md) — đi từng file `.cs`, từng method (Domain → API).
4. [cqrs-saga-outbox.md](cqrs-saga-outbox.md) — đọc code theo đúng thứ tự luồng chạy.
5. [saga-hybrid.md](saga-hybrid.md) → [outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md) — phần nâng cao (polyglot + event-driven).
6. [TONG-HOP-KIEN-THUC.md](TONG-HOP-KIEN-THUC.md) — tổng hợp mọi pattern + so sánh công nghệ (đọc để ôn/tra).

---

## 🏛️ Kiến trúc nền tảng

| Doc | Trả lời câu hỏi | Đọc khi nào |
|---|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Clean Arch 4 tầng, Dependency Inversion, luồng 1 request, map Domain↔Persistence | Đầu tiên, luôn |
| [DOMAIN_MODEL.md](DOMAIN_MODEL.md) | Aggregate/Entity/Value Object/business rule của shop | Khi cần hiểu nghiệp vụ |
| [CONVENTIONS.md](CONVENTIONS.md) | Quy ước đặt tên (`my/the/a`), code style, 1 file/feature | Trước khi viết code |
| [ROADMAP.md](ROADMAP.md) | Checklist tiến độ — đã làm tới đâu | Mở chat mới / tiếp tục việc |

## 🔀 CQRS · Saga · Outbox · Messaging

| Doc | Trả lời câu hỏi | Đọc khi nào |
|---|---|---|
| [cqrs-saga-outbox.md](cqrs-saga-outbox.md) | Đọc code CQRS+Saga+Outbox theo thứ tự luồng chạy | Muốn nắm luồng tổng |
| [saga-hybrid.md](saga-hybrid.md) | Polyglot SQL⇄Mongo, commit-per-step, pivot, compensation | Hiểu giao dịch xuyên store |
| [saga-files-reference.md](saga-files-reference.md) | Giải thích từng class saga + code | Tra chi tiết class saga |
| [outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md) | Outbox, vì sao dùng CẢ Kafka + RabbitMQ, định tuyến tại nguồn | Hiểu event-driven |
| [outbox-kafka-rabbitmq-walkthrough.md](outbox-kafka-rabbitmq-walkthrough.md) | Walkthrough theo từng method | Đọc code messaging |
| [phase12-summary.md](phase12-summary.md) | Tóm tắt thay đổi phase Outbox/Kafka/Rabbit | Xem đã thêm gì |

## 🛡️ Resilience (chịu lỗi)

| Doc | Trả lời câu hỏi | Đọc khi nào |
|---|---|---|
| [polly-circuit-breaker.md](polly-circuit-breaker.md) | Circuit breaker 3 trạng thái, chặn cascade, chỉ bọc read | Dependency chết |
| [polly-bulkhead.md](polly-bulkhead.md) | Bulkhead giới hạn đồng thời, chặn resource exhaustion | Dependency chậm |

## ⚡ Bất đồng bộ & Đồng thời

| Doc | Trả lời câu hỏi | Đọc khi nào |
|---|---|---|
| [async-concepts.md](async-concepts.md) | Định nghĩa: thread/Task/async/await, connection pool, vì sao dùng được | Học nền async |
| [async-programming.md](async-programming.md) | Áp dụng thật: Task.WhenAll, SemaphoreSlim, Parallel, bulk/chunk, Channel, ConcurrentQueue | Áp dụng vào code |

## 📊 Observability · NFR · Performance (vận hành)

| Doc | Trả lời câu hỏi | Đọc khi nào |
|---|---|---|
| [observability.md](observability.md) | OTel traces+metrics+Prometheus, logs Serilog, dev vs prod (không localhost) | Giám sát hệ thống |
| [logging-coverage.md](logging-coverage.md) | Log phủ tới đâu — nhìn log biết chạy đâu, bao lâu, lỗi gì | Debug/soi luồng |
| [nfrs.md](nfrs.md) | NFR (-ilities) → map vào feature đã có; CAP; SLA | Thiết kế hệ thống |
| [performance-testing.md](performance-testing.md) | Latency/Throughput/Error, k6, tìm breaking point, bottleneck | Load test |

## 🔌 Gateway (dịch vụ ngoài)

| Doc | Trả lời câu hỏi | Đọc khi nào |
|---|---|---|
| [gateway-email-smtp.md](gateway-email-smtp.md) | Gửi email thật qua SMTP (MailKit) | Tích hợp email |
| [gateway-invoice-pdf.md](gateway-invoice-pdf.md) | Xuất hoá đơn PDF (QuestPDF) | Tích hợp PDF |
| [gateway-vnpay.md](gateway-vnpay.md) | Thanh toán online VNPay (ký URL + verify IPN) | Tích hợp thanh toán |

## 🧩 Code walkthrough (từng file `.cs`)

→ [code/README.md](code/README.md): [01-Domain](code/01-Domain.md) · [02-SqlServer](code/02-Infrastructure-SqlServer.md) · [03-MongoDB](code/03-Infrastructure-MongoDB.md) · [04-API](code/04-API.md) · [05-Application](code/05-Application.md) · [06-Pagination-Search](code/06-Pagination-Search.md) · [mongo-replica-set](code/mongo-replica-set.md)

## ⭐ Tổng hợp

| Doc | Nội dung |
|---|---|
| [TONG-HOP-KIEN-THUC.md](TONG-HOP-KIEN-THUC.md) | **Gom tất cả**: async, 13 design pattern, so sánh Kafka/RabbitMQ/Redis + SQL/Mongo + monolith/micro, NFR/Observability/Perf. Có bảng tra "bài toán → pattern" |

---

### Tìm nhanh theo câu hỏi

| Bạn muốn… | Đọc |
|---|---|
| Hiểu tại sao Domain không phụ thuộc DB | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Đổi SQL↔Mongo hoạt động thế nào | [ARCHITECTURE.md](ARCHITECTURE.md) §6, [saga-hybrid.md](saga-hybrid.md) |
| Vì sao dùng Kafka VÀ RabbitMQ | [outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md) §2 |
| Song song/throttle/bulk write | [async-programming.md](async-programming.md) |
| Nhìn log biết chương trình chạy tới đâu | [logging-coverage.md](logging-coverage.md) |
| Giám sát prod (không localhost) | [observability.md](observability.md) |
| Load test + tìm bottleneck | [performance-testing.md](performance-testing.md) |
| So sánh mọi pattern/công nghệ | [TONG-HOP-KIEN-THUC.md](TONG-HOP-KIEN-THUC.md) |
