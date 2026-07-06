# Outbox + Kafka + RabbitMQ (event-driven side effects)

Tài liệu này mô tả phần **event-driven** thêm vào provider `DatabaseProvider=Hybrid` (Saga): mỗi thay
đổi nghiệp vụ phát ra **domain event**, event được ghi vào **Outbox** trong cùng transaction, một
dispatcher đẩy lên **Kafka**, rồi các consumer biến sự kiện thành **lệnh (command)** chạy qua
**RabbitMQ** (gửi mail, phát hành hoá đơn, thông báo) và cập nhật **analytics** độc lập.

> Chỉ bật cho provider **Hybrid/Saga** (nơi các pattern nâng cao đang sống). Provider `SqlServer` /
> `MongoDB` thuần **không** đăng ký messaging.

## 1. Vì sao cần Outbox? — bài toán dual-write

Nếu vừa `SaveChanges` (đổi DB) vừa `producer.Publish` (bắn event) là **hai** thao tác trên **hai** hệ
thống, không có transaction chung → có thể:

- Commit DB xong, publish lỗi → **mất event** (khách đã đặt hàng nhưng không ai gửi mail).
- Publish xong, commit DB lỗi/rollback → **event ma** (gửi mail cho đơn không tồn tại).

**Outbox pattern** giải quyết bằng cách biến "publish" thành một **dòng ghi trong cùng DB**: event
được `INSERT` vào bảng `OutboxMessages` **trong đúng transaction** với lệnh ghi Order/Payment. Một
commit cục bộ, nguyên tử. Sau đó tiến trình nền (`OutboxDispatcher`) đọc các dòng chưa publish và đẩy
lên Kafka → giao **at-least-once** (nên consumer phải **idempotent**). Broker chết? Dòng vẫn nằm đó,
tick sau retry — không mất gì.

```
OrderWriteService.Confirm()
   └─ Order.Confirm() → Raise(OrderConfirmedEvent)          (domain event, nội bộ)
      └─ SqlOrderWriteRepository.UpdateAsync()
           ├─ MapInto(record, order)
           ├─ OutboxWriter.Stage(order)   → INSERT OutboxMessages  ┐ CÙNG
           └─ SaveChangesAsync()          → UPDATE Orders          ┘ 1 transaction
```

## 2. Vì sao dùng CẢ Kafka VÀ RabbitMQ? — mỗi thằng một thế mạnh

Đây **không** phải chọn-một-qua-config. Cả hai chạy đồng thời, mỗi cái làm đúng việc nó giỏi:

| | **Kafka** — *event backbone* | **RabbitMQ** — *work/command queue* |
|---|---|---|
| Bản chất message | **Fact**: "việc đã xảy ra" (OrderConfirmed) | **Command**: "hãy làm việc này" (SendEmail) |
| Thế mạnh | log **lưu & replay được**, throughput cao, **nhiều consumer group đọc độc lập**, ordering theo partition | **ack từng message**, **retry + Dead-Letter Queue**, routing qua exchange, competing workers |
| Ai đọc | N consumer (analytics, process-manager, audit…) — mỗi cái như 1 microservice | đúng **1 worker** làm xong việc rồi ack |
| Trong dự án | Outbox publish mọi domain event → topic `orders.events`, `payments.events` | lệnh cần chắc chắn + retry: `email.send`, `invoice.issue`, `notification.send` |

**Định tuyến TẠI NGUỒN (không có cầu Kafka→RabbitMQ).** `OutboxRouter` quyết định ngay khi ghi: một
domain event sinh ra **fact → Kafka** và/hoặc **command → RabbitMQ**, mỗi dòng outbox mang một
`Destination`. `OutboxDispatcher` đọc và bắn **thẳng** tới broker tương ứng. Kafka **không** làm phễu.

```
Order.Confirm()
  └─ OutboxRouter (1 transaction, gắn Destination cho từng dòng):
       ├─ Fact  OrderConfirmed   → Kafka  orders.events  ──▶ AnalyticsProjectionHandler (đọc log độc lập)
       ├─ Cmd   SendEmail        → RabbitMQ  email.send        ──▶ EmailCommandWorker    → IEmailSender
       └─ Cmd   SendNotification → RabbitMQ  notification.send ──▶ NotificationWorker     → INotificationSender

Payment.MarkCompleted()
  └─ OutboxRouter:
       ├─ Fact  PaymentCompleted → Kafka  payments.events ──▶ analytics (doanh thu)
       └─ Cmd   IssueInvoice     → RabbitMQ  invoice.issue ──▶ InvoiceCommandWorker → IInvoiceIssuer
```

Kafka trả lời **"cái gì đã xảy ra"** (phát cho nhiều bên, replay được, cho analytics/service khác);
RabbitMQ trả lời **"giờ phải làm gì"** (một worker, ack, retry, dead-letter). Mỗi broker nhận đúng loại
message hợp với nó, **trực tiếp** — không đẩy qua Kafka rồi mới sang RabbitMQ.

## 3. Gắn với Saga

Saga đã chừa sẵn `SagaStepKind.RetryableForward` — "side effect chạy **sau** pivot, không rollback,
retry tiến tới". Outbox chính là cơ chế đó: dòng outbox commit **cùng** pivot (SQL Order), nên khi đã
qua điểm không quay lại thì event chắc chắn có mặt và sẽ được publish. Repo còn ghi một step
`PublishOrderEvents` (RetryableForward) vào sổ cái saga để **truy vết**: past-pivot chỉ tiến tới
(publish), không bao giờ hoàn tác.

**Hai outbox, cùng cơ chế.** SQL (Order/Payment) gắn outbox vào **pivot**; MongoDB (Product) gắn outbox
vào **bước trừ kho** (compensatable, TRƯỚC pivot) — dòng outbox ghi cùng 1 transaction Mongo với thay
đổi tồn kho + bước saga (nên Mongo cần **replica set**). Cùng `OutboxRouter`, cùng `OutboxDispatcher`
drain cả hai. ⚠️ Vì bước Mongo chạy trước pivot, fact `ProductStockChanged` có thể được publish trước
khi biết saga thành công; nếu saga rollback, compensation hoàn kho lại **không** phát event bù → analytics
lệch tạm thời trong ca hiếm này. Chi tiết + cách vá: mục **J** trong
[walkthrough](outbox-kafka-rabbitmq-walkthrough.md).

## 4. Event catalog & luồng

| Domain event | Integration event | Topic | Hệ quả |
|---|---|---|---|
| `OrderPlacedEvent` | `OrderPlacedIntegrationEvent` | `orders.events` | analytics `orders.placed` |
| `OrderConfirmedEvent` | `OrderConfirmedIntegrationEvent` | `orders.events` | mail xác nhận + push + analytics |
| `OrderShippedEvent` | `OrderShippedIntegrationEvent` | `orders.events` | notify SMS "đang giao" |
| `OrderCancelledEvent` | `OrderCancelledIntegrationEvent` | `orders.events` | notify "đã huỷ" + analytics |
| `PaymentCompletedEvent` | Kafka fact + `IssueInvoiceCommand` (RabbitMQ) | `payments.events` / `invoice.issue` | fact cho analytics + lệnh phát hành hoá đơn |
| `ProductStockChangedEvent` (Mongo) | Kafka fact (+ `RestockAlertCommand` nếu hết hàng) | `products.events` / `restock.alert` | analytics tồn kho + cảnh báo nhập lại |

> **Cả 2 DB đều tham gia:** Order/Payment (SQL) và Product (MongoDB) đều có outbox riêng, đều route qua
> `OutboxRouter` chung. Dispatcher drain **cả hai** outbox.

**Domain event** (nội bộ, giàu ngữ nghĩa) ≠ **integration event/command** (phẳng, versionable, đi qua
dây). `OutboxRouter` (ở `Infrastructure.Saga.Core`) là ranh giới dịch + định tuyến — service khác chỉ
thấy integration event/command.

## 5. Chạy thử

```bash
# 1) Bật brokers (Kafka + RabbitMQ + UI)
docker compose -f docker-compose.messaging.yml up -d
#    Kafka-UI      http://localhost:8085
#    RabbitMQ UI   http://localhost:15672   (guest/guest)

# 2) Bật provider Hybrid + cập nhật schema outbox (LocalDB)
#    appsettings: "DatabaseProvider": "Hybrid"
dotnet ef database update \
  --project SellingNewProduct.Infrastructure.SqlServer.Saga \
  --startup-project SellingNewProduct.Infrastructure.SqlServer.Saga

# 3) Chạy API (Mongo cần replica set — xem docs/mongo-replica-set.md)
dotnet run --project SellingNewProduct.API
```

Sau đó gọi `POST /api/orders/{id}/confirm` rồi xem log:

```
Kafka ▶ published OrderConfirmedIntegrationEvent to orders.events [p0@3].
RabbitMQ ▶ queued SendEmailCommand to 'email.send'.
RabbitMQ ▶ queued SendNotificationCommand to 'notification.send'.
📧 EMAIL → customer-...@example.com | Đơn hàng ... đã được xác nhận | ...
📊 ANALYTICS orders.confirmed = 1 ...
```

> **App vẫn chạy khi broker tắt.** Outbox giữ event và retry publish; consumer tự reconnect. Đây là
> tính chịu lỗi cố ý — không để hạ tầng messaging làm sập API.

## 6. Bản đồ file

| Thành phần | File |
|---|---|
| Domain events | `Domain/Orders/Order*Event.cs`, `Domain/Payments/PaymentCompletedEvent.cs`, `Domain/Products/ProductStockChangedEvent.cs` |
| **Router (fact→Kafka / command→RabbitMQ)** | `Infrastructure.Saga.Core/Outbox/OutboxRouter.cs` (dùng chung 2 DB) |
| Outbox SQL (Order/Payment) | `Infrastructure.SqlServer.Saga/Models/OutboxMessageRecord.cs`, `Outbox/OutboxWriter.cs`, `Outbox/SqlOutboxStore.cs` |
| Outbox Mongo (Product) | `Infrastructure.MongoDB.Saga/Models/OutboxMessageDocument.cs`, `Outbox/MongoOutboxWriter.cs`, `Outbox/MongoOutboxStore.cs` |
| Dispatcher (drain 2 outbox, route) | `Infrastructure.Messaging/Outbox/OutboxDispatcher.cs` |
| Kafka (bus + consumer) | `Infrastructure.Messaging/Kafka/*` |
| RabbitMQ (publisher + consumer + DLQ) | `Infrastructure.Messaging/RabbitMq/*` |
| Kafka consumers (analytics) | `Infrastructure.Messaging/Consumers/AnalyticsProjectionHandler.cs` |
| Workers (làm việc thật) | `Infrastructure.Messaging/Workers/CommandWorkers.cs` |
| Gateway giả lập | `Infrastructure.Messaging/Services/FakeSideEffects.cs` |
| Đăng ký DI | `Infrastructure.Messaging/DependencyInjection.cs` (`AddMessaging`) + router ở `AddSagaCore` |

## 7. Hướng microservice (khi cần)

Kiến trúc hiện tại là **modular monolith**: mọi consumer chạy in-process. Nhưng Outbox + Kafka đã là
**xương sống tích hợp** để tách service sau này mà không đổi Domain:

- Tách "Notification Service": bê các **worker** (email/notification) + queue RabbitMQ ra process riêng;
  nó vẫn nhận command y như cũ. Muốn phản ứng theo event log thì cắm thêm **consumer group Kafka mới** —
  replay từ đầu log, không ảnh hưởng consumer cũ.
- Tách "Analytics Service": tương tự, group riêng, DB riêng.
- Integration event chính là **hợp đồng** giữa các service (đã tách khỏi domain event).

Chưa cần tách bây giờ — nhưng khi cần, chỉ là "đem một consumer group ra chạy riêng", không phải viết
lại. Đó là lý do dựng messaging theo kiểu này ngay từ trong monolith.
