# Phase 12 — Tóm tắt thay đổi: Outbox + Kafka + RabbitMQ

> ⚠️ **ĐÃ CẬP NHẬT Ở PHASE 13 (2026-07-05).** File này mô tả thiết kế Phase 12 ban đầu (có cầu
> Kafka→RabbitMQ qua process manager, outbox chỉ ở SQL). Phase 13 đã **sửa**: định tuyến TẠI NGUỒN
> (`OutboxRouter`: fact→Kafka, command→RabbitMQ **trực tiếp**, bỏ process manager) và **thêm Mongo
> outbox** cho Product. Xem [outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md) +
> [outbox-kafka-rabbitmq-walkthrough.md](outbox-kafka-rabbitmq-walkthrough.md) cho thiết kế HIỆN TẠI.
> Các tên cũ dưới đây (`RabbitMqCommandBus`, `OutboxEventTranslator`, `*ProcessManager`, field
> `Topic/EventType`) đã bị thay bằng `RabbitMqCommandPublisher`, `OutboxRouter`, `Destination/Route/MessageType`.

Ngày: 2026-07-04 · Build: **PASS (0 error)** · Phạm vi: chỉ bật cho provider `DatabaseProvider=Hybrid` (Saga).

> Tài liệu chi tiết kèm sơ đồ luồng: [outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md).

## 1. Mục tiêu

Thêm **event-driven side effects** vào dự án: mỗi thay đổi nghiệp vụ phát domain event → ghi **Outbox**
(nguyên tử với transaction ghi DB) → **Kafka** → consumer biến sự kiện thành **lệnh** chạy qua
**RabbitMQ** (gửi mail, phát hành hoá đơn, thông báo) + cập nhật **analytics** độc lập.

Nguyên tắc: **dùng CẢ 2 broker theo thế mạnh** (không toggle config):
- **Kafka** = event backbone (fact "đã xảy ra"; log replay, fan-out nhiều consumer group).
- **RabbitMQ** = command/work queue (việc "hãy làm"; ack từng message, retry, dead-letter).

## 2. Project mới

| Project | Vai trò |
|---|---|
| `SellingNewProduct.Infrastructure.Messaging` | Kernel messaging: contracts, Kafka bus + consumer, RabbitMQ bus + consumer (DLQ), Outbox dispatcher, process managers, workers, gateway giả lập. Packages: `Confluent.Kafka` 2.6.1, `RabbitMQ.Client` 7.0.0. |

Đã thêm vào `SellingNewProduct.slnx`.

## 3. File THÊM MỚI

### Domain
- `Domain/Orders/OrderPlacedEvent.cs`
- `Domain/Orders/OrderShippedEvent.cs`
- `Domain/Orders/OrderCancelledEvent.cs`
- `Domain/Payments/PaymentCompletedEvent.cs`

### Infrastructure.Messaging (project mới)
- `Contracts/IntegrationEvents.cs` — 5 integration event + `MessagingTopics`
- `Contracts/MessagingCommands.cs` — 3 command + `MessagingQueues`
- `Abstractions/Ports.cs` — `IEventBus`, `ICommandBus`, `IOutboxStore`, `IIntegrationEventHandler<>`, `ICommandHandler<>`, `IEmailSender`/`IInvoiceIssuer`/`INotificationSender`
- `Routing/MessagingRegistry.cs` — `IntegrationEventRegistry` + `CommandRegistry` (map type ↔ topic/queue + dispatch)
- `Outbox/OutboxDispatcher.cs` — background poll outbox → publish Kafka
- `Kafka/KafkaSettings.cs`, `Kafka/KafkaEventBus.cs`, `Kafka/KafkaConsumerHostedService.cs`
- `RabbitMq/RabbitMqSettings.cs`, `RabbitMqConnectionProvider.cs`, `RabbitMqCommandBus.cs`, `RabbitMqConsumerHostedService.cs`
- `Consumers/OrderNotificationProcessManager.cs` — Kafka fact → RabbitMQ command
- `Consumers/InvoiceProcessManager.cs` — PaymentCompleted → issue invoice
- `Consumers/AnalyticsProjectionHandler.cs` — đọc cùng log, độc lập
- `Workers/CommandWorkers.cs` — Email/Invoice/Notification worker
- `Services/FakeSideEffects.cs` — gateway giả lập (log) + analytics in-memory
- `DependencyInjection.cs` — `AddMessaging()`

### Infrastructure.SqlServer.Saga (outbox lưu ở SQL — cùng nơi pivot)
- `Models/OutboxMessageRecord.cs`
- `Configurations/OutboxMessageConfiguration.cs`
- `Outbox/OutboxEventTranslator.cs` — dịch domain event → integration event
- `Outbox/OutboxWriter.cs` — stage outbox vào CÙNG transaction pivot
- `Outbox/SqlOutboxStore.cs` — `IOutboxStore` cho dispatcher poll
- `Migrations/*_AddOutboxMessages.cs` — bảng `OutboxMessages`

### Gốc dự án / docs
- `docker-compose.messaging.yml` — Kafka (KRaft) + Kafka-UI + RabbitMQ (management)
- `docs/outbox-kafka-rabbitmq.md` — tài liệu chi tiết
- `docs/phase12-summary.md` — file này

## 4. File SỬA

| File | Thay đổi |
|---|---|
| `Domain/Orders/Order.cs` | Raise event trong `Confirm`/`MarkShipped`/`Cancel`; thêm `MarkPlaced()` |
| `Domain/Orders/OrderConfirmedEvent.cs` | Thêm `TotalAmount` + `Currency` |
| `Domain/Payments/Payment.cs` | `MarkCompleted()` raise `PaymentCompletedEvent` |
| `Domain/Services/OrderWriteService.cs` | `PlaceAsync` gọi `aOrder.MarkPlaced()` |
| `Infrastructure.SqlServer.Saga/Persistence/AppDbContext.cs` | Thêm `DbSet<OutboxMessageRecord>` + config |
| `.../Repositories/Write/SqlOrderWriteRepository.cs` | `OutboxWriter.Stage` trong Add/Update; ghi saga step `PublishOrderEvents` (RetryableForward) |
| `.../Repositories/Write/SqlPaymentWriteRepository.cs` | `OutboxWriter.Stage` trong Update |
| `.../DependencyInjection.cs` | Đăng ký `OutboxWriter` + `SqlOutboxStore` |
| `.../*.csproj` (SqlServer.Saga, API) | Ref project `Infrastructure.Messaging` |
| `API/Program.cs` | Nhánh Saga gọi `AddMessaging()` |
| `API/appsettings.json` | Thêm section `Kafka` + `RabbitMq` |
| `SellingNewProduct.slnx` | Thêm project Messaging |
| `docs/ROADMAP.md` | Thêm Phase 12 |

## 5. Luồng chính (tóm tắt)

```
Order.Confirm() ─Raise─▶ OrderConfirmedEvent
   └─ SqlOrderWriteRepository.UpdateAsync
        ├─ OutboxWriter.Stage(order) → INSERT OutboxMessages ┐ CÙNG 1
        └─ SaveChanges()             → UPDATE Orders          ┘ transaction (SQL pivot)

OutboxDispatcher (nền) ─▶ Kafka "orders.events"
   ├─▶ OrderNotificationProcessManager ─▶ RabbitMQ "email.send" ─▶ EmailCommandWorker ─▶ 📧
   └─▶ AnalyticsProjectionHandler ─▶ 📊 (đọc cùng log, độc lập)

Payment.MarkCompleted() ─▶ PaymentCompletedEvent ─▶ Kafka "payments.events"
   └─▶ InvoiceProcessManager ─▶ RabbitMQ "invoice.issue" ─▶ InvoiceCommandWorker ─▶ 🧾
```

## 6. Event & Command

| Domain event | → Integration event (Kafka topic) | → Command (RabbitMQ queue) |
|---|---|---|
| OrderPlaced | OrderPlaced (`orders.events`) | — (chỉ analytics) |
| OrderConfirmed | OrderConfirmed (`orders.events`) | `email.send` + `notification.send` |
| OrderShipped | OrderShipped (`orders.events`) | `notification.send` (SMS) |
| OrderCancelled | OrderCancelled (`orders.events`) | `notification.send` |
| PaymentCompleted | PaymentCompleted (`payments.events`) | `invoice.issue` |

## 7. Cách chạy

```bash
docker compose -f docker-compose.messaging.yml up -d     # Kafka+RabbitMQ (UI :8085 / :15672)
# appsettings: "DatabaseProvider": "Hybrid"
dotnet ef database update \
  --project SellingNewProduct.Infrastructure.SqlServer.Saga \
  --startup-project SellingNewProduct.Infrastructure.SqlServer.Saga
dotnet run --project SellingNewProduct.API
# POST /api/orders/{id}/confirm → log: 📧 EMAIL / 🧾 INVOICE / 📊 ANALYTICS
```

> App vẫn khởi động khi broker tắt: outbox retry publish, consumer tự reconnect (chịu lỗi có chủ đích).

## 8. Không đổi

- Domain/Application logic nghiệp vụ, CQRS/MediatR, Saga orchestration, Read side.
- Provider `SqlServer` / `MongoDB` thuần (không đăng ký messaging; domain event chỉ tích luỹ rồi bỏ qua).
