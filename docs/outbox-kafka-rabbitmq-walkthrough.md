# Outbox + Kafka + RabbitMQ — Walkthrough theo từng method

Tài liệu này đi **từng bước, từng method, ai gọi ai** cho luồng event-driven, kèm giải thích **DI**
(lifetime, scope) và **Docker**. Đọc kèm [outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md) (tổng
quan + vì sao).

Mục lục:
- [A. Docker: 3 container làm gì](#a-docker-3-container-làm-gì)
- [B. DI: cái gì đăng ký lúc khởi động, lifetime nào, vì sao](#b-di-cái-gì-đăng-ký-lúc-khởi-động-lifetime-nào-vì-sao)
- [C. Registry: bộ não định tuyến](#c-registry-bộ-não-định-tuyến)
- [D. Luồng GHI: từ HTTP đến bảng Outbox (nguyên tử)](#d-luồng-ghi-từ-http-đến-bảng-outbox-nguyên-tử)
- [E. Outbox → Kafka: OutboxDispatcher](#e-outbox--kafka-outboxdispatcher)
- [F. Kafka → handler: KafkaConsumerHostedService](#f-kafka--handler-kafkaconsumerhostedservice)
- [G. Handler → RabbitMQ: process manager](#g-handler--rabbitmq-process-manager)
- [H. RabbitMQ → worker: consumer + retry + DLQ](#h-rabbitmq--worker-consumer--retry--dlq)
- [I. Bức tranh tổng: 1 lần confirm đơn](#i-bức-tranh-tổng-1-lần-confirm-đơn)

---

## A. Docker: 3 container làm gì

File `docker-compose.messaging.yml`. Lệnh: `docker compose -f docker-compose.messaging.yml up -d`.

### `kafka` (image `apache/kafka:3.8.0`, KRaft — không cần Zookeeper)
- **Vì sao 2 listener?** App .NET chạy trên **máy host**, còn `kafka-ui` chạy **trong mạng docker**.
  Hai bên cần địa chỉ khác nhau để nối tới cùng một broker:
  - `HOST://localhost:9092` — cho app .NET (ngoài docker). `appsettings.Kafka.BootstrapServers = localhost:9092`.
  - `DOCKER://kafka:29092` — cho container khác (kafka-ui). Tên `kafka` là DNS nội bộ của docker network.
- `KAFKA_ADVERTISED_LISTENERS` là địa chỉ broker **tự khai** cho client kết nối lại. Nếu khai sai,
  client connect được lần đầu rồi bị redirect tới địa chỉ chết → treo. Đây là lỗi Kafka + Docker kinh điển.
- `KAFKA_AUTO_CREATE_TOPICS_ENABLE: true` → topic `orders.events`/`payments.events` tự sinh khi publish
  lần đầu (không phải tạo tay).

### `kafka-ui` (`provectuslabs/kafka-ui`) — cổng http://localhost:8085
- Chỉ để **xem** topic/message/consumer group. Nối tới broker qua `kafka:29092` (listener DOCKER).

### `rabbitmq` (`rabbitmq:3.13-management`) — AMQP :5672, UI http://localhost:15672 (guest/guest)
- `5672` = cổng app nói chuyện (giao thức AMQP). `15672` = UI quản trị (xem queue, message, DLQ).

> **App vẫn chạy khi chưa `up` broker.** Producer/consumer bọc try-catch + retry (xem phần E, F, H),
> nên `dotnet run` không sập; chỉ là event nằm chờ trong Outbox tới khi Kafka sống lại.

---

## B. DI: cái gì đăng ký lúc khởi động, lifetime nào, vì sao

### Điểm vào: `Program.cs`
```csharp
else if (... "Hybrid" || "Saga" ...)
{
    builder.Services.AddSagaCore();
    builder.Services.AddSqlServerSagaInfrastructure(builder.Configuration); // ← đăng ký OutboxWriter + IOutboxStore
    builder.Services.AddMongoSagaInfrastructure(builder.Configuration);
    builder.Services.AddMessaging(builder.Configuration);                   // ← đăng ký toàn bộ messaging
}
```
Messaging **chỉ** được đăng ký ở nhánh Saga. Provider `SqlServer`/`MongoDB` thuần không có gì thay đổi.

### `AddSqlServerSagaInfrastructure` (project SqlServer.Saga) — 2 dòng liên quan Outbox
```csharp
theServices.AddScoped<OutboxWriter>();               // Scoped: dùng chung AppDbContext (scoped) với repo
theServices.AddScoped<IOutboxStore, SqlOutboxStore>();// Scoped: cũng ôm AppDbContext
```
**Vì sao Scoped?** `OutboxWriter` phải ghi vào **đúng** `AppDbContext` mà repository đang dùng, để
`INSERT OutboxMessages` và `UPDATE Orders` nằm trong **cùng một** `SaveChanges` → nguyên tử. Nếu nó là
Singleton nó sẽ ôm một DbContext khác → mất tính nguyên tử. Đây là mấu chốt của Outbox pattern.

### `AddMessaging` (project Messaging) — phân loại theo lifetime

| Đăng ký | Lifetime | Vì sao |
|---|---|---|
| `IntegrationEventRegistry`, `CommandRegistry` | **Singleton** (instance dựng sẵn) | Bảng tra cứu bất biến, dựng 1 lần lúc boot |
| `IEventBus → KafkaEventBus` | **Singleton** | Ôm 1 `IProducer` Kafka dùng lại (mở producer mỗi lần bắn là phản pattern) |
| `RabbitMqConnectionProvider` | **Singleton** | Ôm 1 `IConnection` RabbitMQ (connection đắt, thread-safe) |
| `ICommandBus → RabbitMqCommandBus` | **Singleton** | Chỉ dùng connection provider; channel tạo/huỷ mỗi lần gửi |
| `OutboxDispatcher`, `KafkaConsumerHostedService`, `RabbitMqConsumerHostedService` | **HostedService** (Singleton nền) | 3 vòng lặp nền chạy suốt vòng đời app |
| `IIntegrationEventHandler<T>` (process manager, analytics) | **Scoped** | Mỗi message mở 1 scope riêng để xử lý |
| `ICommandHandler<T>` (worker) | **Scoped** | Tương tự |
| `IEmailSender`/`IInvoiceIssuer`/`INotificationSender` | **Scoped** | Gateway, nằm trong scope xử lý |
| `IAnalyticsStore → InMemoryAnalyticsStore` | **Singleton** | "DB analytics" phải sống xuyên request để cộng dồn |

**Vấn đề Scoped-trong-Singleton:** 3 hosted service là Singleton, nhưng handler là Scoped. Không thể
inject Scoped vào Singleton. Giải pháp: hosted service inject `IServiceScopeFactory`, và **mỗi message
tự mở 1 scope** rồi resolve handler trong scope đó. Xem `CreateAsyncScope()` xuất hiện ở phần E/F/H.

Vì sao handler Scoped? Vì đôi khi nó cần thứ scoped (như DbContext của analytics thật). Mở scope/message
cũng cô lập lỗi giữa các message.

---

## C. Registry: bộ não định tuyến

File `Routing/MessagingRegistry.cs`. Đây là chỗ thay reflection bằng **closure gõ sẵn kiểu**.

### `IntegrationEventRegistry.Register<TEvent>(topic)`
Được gọi trong `AddMessaging.BuildEventRegistry()`:
```csharp
aRegistry.Register<OrderConfirmedIntegrationEvent>(MessagingTopics.OrderEvents);
```
Mỗi lần gọi lưu 3 thứ vào 2 dictionary (`myByName`, `myByType`):
- `EventType` = `typeof(TEvent).Name` (vd `"OrderConfirmedIntegrationEvent"`) — dùng làm discriminator.
- `Topic` — vd `"orders.events"`.
- **`Dispatch`** — một `Func<IServiceProvider, string(json), CancellationToken, Task>` đã "đóng băng"
  kiểu `TEvent`:
  ```csharp
  async (provider, json, ct) =>
  {
      var evt = JsonSerializer.Deserialize<TEvent>(json, ...);           // biết chính xác TEvent
      foreach (var h in provider.GetServices<IIntegrationEventHandler<TEvent>>())  // TẤT CẢ handler
          await h.HandleAsync(evt, ct);                                  // → fan-out
  }
  ```
  `GetServices` (số nhiều) trả **mọi** handler đăng ký cho event đó → đây chính là **fan-out**.

### 3 method tra cứu
- `Describe(Type)` — từ kiểu integration event ra `(Topic, EventType)`. **`OutboxWriter` dùng** để biết
  ghi vào topic nào, discriminator gì.
- `Resolve(string eventType)` — từ tên discriminator ra `EventRegistration`. **Kafka consumer dùng** khi
  đọc header `eventType`.
- `Topics` — danh sách topic distinct để consumer `Subscribe`.

### `CommandRegistry` — y hệt nhưng cho command
- `Register<TCommand>(queue)`: closure resolve **1** handler (`GetRequiredService<ICommandHandler<TCommand>>`)
  — command chỉ có đúng 1 worker (khác event nhiều handler).
- `QueueFor(Type)` — **`RabbitMqCommandBus` dùng** để biết đẩy vào queue nào.
- `ByQueue(string)` — **`RabbitMqConsumer` dùng** khi nhận message từ queue.

---

## D. Luồng GHI: từ HTTP đến bảng Outbox (nguyên tử)

Ví dụ: `POST /api/orders/{id}/confirm`.

**1. Controller → Application → Domain Service** (không đổi so với trước):
`OrdersController.Confirm` → `ISender.Send(ConfirmOrderCommand)` → `ConfirmOrderCommandHandler.Handle`
→ `IOrderWriteService.ConfirmAsync`.

**2. `OrderWriteService.ConfirmAsync`** (`Domain/Services/OrderWriteService.cs`):
```csharp
aOrder.Confirm();                       // ← raise domain event tại đây
...
await PersistInTransactionAsync(aOrder, aProducts.Values, ct);
```

**3. `Order.Confirm()`** (`Domain/Orders/Order.cs`):
```csharp
OrderStatus = OrderStatus.Confirmed;
MarkUpdated();
Raise(new OrderConfirmedEvent(Id, CustomerId, TotalAmount.Amount, TotalAmount.Currency));
```
`Raise` (ở `AggregateRoot`) chỉ **thêm event vào list `DomainEvents`** trong bộ nhớ của aggregate. Chưa
publish gì cả.

**4. `PersistInTransactionAsync`** mở transaction UoW rồi gọi `myOrderRepository.UpdateAsync(aOrder)`.

**5. `SqlOrderWriteRepository.UpdateAsync`** (`SqlServer.Saga/Repositories/Write/`) — **trái tim của Outbox**:
```csharp
OrderMapper.MapInto(aRecord, theOrder);          // domain → record

var aPublished = myOutboxWriter.Stage(theOrder); // (a) stage event vào context, CHƯA save
await myAppDbContext.SaveChangesAsync(ct);        // (b) 1 SaveChanges: UPDATE Orders + INSERT OutboxMessages

await MarkPivotCommittedAsync(ct);                // (c) ghi step Pivot vào sổ cái saga
await RecordPublishStepAsync(aPublished, ct);     // (d) ghi step RetryableForward (audit)
```
Điểm mấu chốt là **(a) rồi (b)**: cả thay đổi Order và dòng outbox vào cùng một `SaveChanges` → **một
transaction SQL** → hoặc cùng thành công, hoặc cùng rollback. Không bao giờ "đổi đơn nhưng mất event".

**6. `OutboxWriter.Stage(aggregate)`** (`SqlServer.Saga/Outbox/OutboxWriter.cs`):
```csharp
var entries = myRouter.Route(theAggregate.DomainEvents);   // router quyết Kafka/RabbitMQ (bước 7)
foreach (var en in entries)
    myAppDbContext.OutboxMessages.Add(new OutboxMessageRecord {  // Add — CHƯA SaveChanges
        Id = Guid.NewGuid(),
        Destination = (int)en.Destination,   // 0 = Kafka, 1 = RabbitMQ
        Route = en.Route,                     // topic hoặc queue
        MessageType = en.MessageType,         // discriminator
        Payload = en.Payload, PartitionKey = en.PartitionKey, CreatedUtc = ...
    });
theAggregate.ClearDomainEvents();     // xoá để không enqueue lần 2
return entries.Count;
```
`Add` chỉ đánh dấu "sẽ insert"; chính `SaveChangesAsync` của repo (bước 5b) mới thực sự commit. **Mongo có
`MongoOutboxWriter` y hệt** (ghi document vào transaction Mongo, dùng chung `OutboxRouter`).

**7. `OutboxRouter.Route`** (`Infrastructure.Saga.Core/Outbox/`, dùng chung cho cả SQL & Mongo): `switch`
domain event → **danh sách `OutboxEntry`**, mỗi entry gắn `Destination` (Kafka hay RabbitMQ):
```csharp
OrderConfirmedEvent e => new[] {
    Fact(new OrderConfirmedIntegrationEvent(...), e.OrderId.ToString()),  // → Kafka orders.events
    Command(new SendEmailCommand(...)),                                   // → RabbitMQ email.send
    Command(new SendNotificationCommand(...))                            // → RabbitMQ notification.send
},
```
`Fact()` tra `IntegrationEventRegistry.Describe` → topic; `Command()` tra `CommandRegistry.QueueFor` →
queue. Đây là chỗ **quyết định định tuyến tại nguồn**: một fact có thể kèm luôn các command — tất cả ghi
vào cùng transaction, KHÔNG đẩy qua Kafka rồi mới sang RabbitMQ.

Kết thúc phần D: trong DB đã có (vd) 3 dòng `OutboxMessages` — 1 Kafka + 2 RabbitMQ — cùng `ProcessedUtc
= NULL`. Chưa gửi đi đâu.

---

## E. Outbox → broker: OutboxDispatcher (route theo Destination)

File `Messaging/Outbox/OutboxDispatcher.cs`. Là `BackgroundService` (Singleton nền), chạy từ lúc app start.

**`ExecuteAsync`** — vòng lặp vô hạn, mỗi 2 giây gọi `DispatchBatchAsync`, 1 tick lỗi KHÔNG giết vòng lặp.

**`DispatchBatchAsync`** — drain **mọi** outbox (SQL + Mongo):
```csharp
await using var scope = myScopeFactory.CreateAsyncScope();
var stores = scope.ServiceProvider.GetServices<IOutboxStore>();  // CẢ SqlOutboxStore VÀ MongoOutboxStore
foreach (var store in stores)
    await DrainStoreAsync(store, ct);
```

**`DrainStoreAsync` + `PublishAsync`** — đọc dòng chưa publish rồi **route theo Destination**:
```csharp
var messages = await store.GetUnpublishedAsync(BatchSize, ct);
foreach (var m in messages)
{
    try {
        await PublishAsync(m, ct);                 // route: Kafka hay RabbitMQ
        await store.MarkPublishedAsync(m.Id, ct);  // stamp ProcessedUtc
    } catch (Exception e) {
        await store.MarkFailedAsync(m.Id, e.Message, ct);   // giữ chưa publish → tick sau thử lại
    }
}
```
- **(1)** Dispatcher là Singleton nhưng `IOutboxStore` là Scoped → phải tự mở scope (đúng như phần B).
- **`PublishAsync(m)` — ĐÂY là chỗ route theo thế mạnh, tại nguồn:**
  ```csharp
  Task PublishAsync(OutboxMessage m, ct) => m.Destination switch {
      OutboxDestination.Kafka    => myEventBus.PublishAsync(m.Route, m.PartitionKey, m.MessageType, m.Payload, ct),
      OutboxDestination.RabbitMq => myCommandPublisher.PublishAsync(m.Route, m.MessageType, m.Payload, ct),
  };
  ```
  Fact → `KafkaEventBus` (producer idempotent, Acks=All, key=OrderId giữ thứ tự partition). Command →
  `RabbitMqCommandPublisher` (publish thẳng vào queue, persistent). **Không** có Kafka→Rabbit.
- Nếu broker chết → `PublishAsync` ném → catch, dòng vẫn `ProcessedUtc = NULL` → tick sau thử lại.
  **At-least-once** → consumer/worker phải idempotent.

---

## F. Kafka → handler: KafkaConsumerHostedService

File `Messaging/Kafka/KafkaConsumerHostedService.cs`. `BackgroundService` nền.

**`ExecuteAsync`** → `Task.Run(RunConsumeLoop)` (vì consumer Confluent chặn luồng → chạy thread riêng).

**`RunConsumeLoop`**:
```csharp
while (!ct.IsCancellationRequested)
{
    try {
        using var consumer = new ConsumerBuilder<string,string>(config).Build(); // EnableAutoCommit=false
        consumer.Subscribe(myRegistry.Topics);          // subscribe mọi topic đã đăng ký
        while (!ct.IsCancellationRequested) {
            var result = consumer.Consume(ct);           // chặn tới khi có message
            await HandleMessageAsync(result, ct);        // xử lý
            consumer.Commit(result);                     // COMMIT OFFSET chỉ sau khi handler xong
        }
    }
    catch (Exception e) { log; await Task.Delay(5s, ct); } // broker chết → reconnect
}
```
`Commit` **sau** handler = at-least-once. Nếu crash giữa handler và commit → message được đọc lại → chạy
lại handler (idempotent lo phần trùng).

**`HandleMessageAsync`** — bám theo MỘT message cụ thể: một `OrderConfirmedIntegrationEvent` vừa tới từ
topic `orders.events`.

```csharp
// result.Message = { Key: "3fa8...OrderId", Value: "{\"orderId\":\"3fa8...\",\"totalAmount\":250000,...}",
//                    Headers: { eventType = "OrderConfirmedIntegrationEvent" } }

var eventType = ReadEventType(result.Message.Headers);   // → "OrderConfirmedIntegrationEvent"
var reg = myRegistry.Resolve(eventType);                 // tra registry → EventRegistration của type này
if (reg is null) return;                                 // không đăng ký (service khác sở hữu) → bỏ qua

await using var scope = myScopeFactory.CreateAsyncScope();
await reg.Dispatch(scope.ServiceProvider, result.Message.Value, ct);   // ← gọi closure
```

**`reg.Dispatch` là gì với ĐÚNG type này?** Lúc khởi động, `Register<OrderConfirmedIntegrationEvent>(...)`
đã "đóng băng" một closure gõ sẵn kiểu `OrderConfirmedIntegrationEvent`. Bung ra, nó chạy chính xác như
sau (không còn `TEvent` chung chung nữa):

```csharp
// closure cho riêng OrderConfirmedIntegrationEvent:
async (provider, json, ct) => {
    // 1. deserialize ĐÚNG kiểu cụ thể
    var evt = JsonSerializer.Deserialize<OrderConfirmedIntegrationEvent>(json, ...);
    //    evt.OrderId = 3fa8..., evt.TotalAmount = 250000, evt.Currency = "VND"

    // 2. lấy MỌI handler đăng ký cho đúng type này
    var handlers = provider.GetServices<IIntegrationEventHandler<OrderConfirmedIntegrationEvent>>();
    //    Sau Phase 13 → chỉ có [ AnalyticsProjectionHandler ]  (email/notify đã đi thẳng RabbitMQ từ outbox)

    // 3. gọi từng handler
    foreach (var h in handlers)
        await h.HandleAsync(evt, ct);     // → AnalyticsProjectionHandler.HandleAsync(evt)
}
```

**Handler cụ thể** — `AnalyticsProjectionHandler.HandleAsync(OrderConfirmedIntegrationEvent)`
(`Messaging/Consumers/AnalyticsProjectionHandler.cs`):
```csharp
public Task HandleAsync(OrderConfirmedIntegrationEvent e, CancellationToken ct) {
    myAnalytics.RecordOrderConfirmed(e.OrderId, e.TotalAmount);   // → InMemoryAnalyticsStore
    return Task.CompletedTask;                                    // → log 📊 ANALYTICS orders.confirmed = 1
}
```

**`RecordOrderConfirmed` thực sự làm gì?** — ⚠️ Đây là bản **GIẢ LẬP**: **không** ghi DB, **không** đẩy đi
đâu. Chỉ **tăng một biến đếm trong RAM + ghi log** (giống `IEmailSender` log thay vì gửi mail thật).
`IAnalyticsStore` → impl `InMemoryAnalyticsStore` (`Messaging/Services/FakeSideEffects.cs`):
```csharp
private readonly ConcurrentDictionary<string, decimal> myCounters = new();   // "DB" = dictionary trong RAM

public void RecordOrderConfirmed(Guid orderId, decimal amount)
    => Bump("orders.confirmed", 1, orderId, amount);

private void Bump(string key, decimal delta, ...) {
    var value = myCounters.AddOrUpdate(key, delta, (_, existing) => existing + delta);  // đếm += 1
    myLogger.LogInformation("📊 ANALYTICS {Key} = {Value} ...", key, value, ...);        // in log
}
```
Vòng đời & vì sao:
- Đăng ký **Singleton** → biến đếm **sống xuyên request/message**, cộng dồn qua nhiều đơn. Nếu Scoped thì
  mỗi message một dictionary mới → luôn = 1 (vô nghĩa).
- **Mất sạch khi app restart** (chỉ ở RAM, không persist) — khác `OutboxMessages` (nằm trong DB, sống qua
  restart). `ConcurrentDictionary` vì consumer chạy trên thread nền, có thể ghi đồng thời.

**Nó ĐẠI DIỆN cho gì trong hệ thật:** một **read-model / projection** nghe event rồi ghi vào **DB analytics
riêng** (hoặc feed dashboard/OLAP). Swap = đổi impl `IAnalyticsStore` trong DI, **consumer/handler không đổi
một dòng** (port/adapter):
```csharp
public sealed class SqlAnalyticsStore : IAnalyticsStore {   // inject DbContext analytics riêng
    public void RecordOrderConfirmed(Guid orderId, decimal amount)
        => /* UPDATE daily_sales SET confirmed += 1, revenue += amount WHERE date = today */;
}
```
Đây chính là điểm mạnh Kafka: tách microservice thì `AnalyticsProjectionHandler` + `SqlAnalyticsStore`
thành **Analytics Service** với **DB riêng**, đọc cùng `orders.events` bằng consumer group riêng.

| | Hiện tại (demo) | Bản thật (gợi ý) |
|---|---|---|
| Lưu ở đâu | `ConcurrentDictionary` (RAM) | DB analytics riêng |
| Đẩy đi đâu | Không, chỉ log 📊 | Có thể feed dashboard/OLAP |
| Mất khi restart | Có | Không (đã persist) |
| Đổi thế nào | thay impl `IAnalyticsStore` trong DI | — |

**Xong handler → về `RunConsumeLoop` → `consumer.Commit(result)`** đánh dấu đã đọc offset này. Nếu handler
ném exception → KHÔNG commit → message được đọc lại ở vòng sau (at-least-once → handler phải idempotent).

> **Lưu ý luồng route-at-source:** message `OrderConfirmed` trên Kafka ở đây **chỉ chạy analytics**. Việc
> gửi mail/notify KHÔNG xảy ra ở consumer này — chúng đã được `OutboxRouter` route thẳng thành command
> RabbitMQ từ lúc ghi outbox (mục D & G), chạy song song ở `RabbitMqConsumerHostedService` (mục H). Kafka
> consumer và RabbitMQ consumer là hai luồng độc lập, không cái nào gọi cái nào.

---

## G. Command tới RabbitMQ: publish thẳng từ dispatcher (KHÔNG qua Kafka)

Command **không** đi qua Kafka. Ở phần D, `OutboxRouter` đã ghi thẳng dòng `SendEmailCommand`
(Destination=RabbitMq, Route=`email.send`) vào outbox. Ở phần E, dispatcher gặp dòng đó gọi:

**`RabbitMqCommandPublisher.PublishAsync(queue, commandType, payloadJson)`** (`Messaging/RabbitMq/RabbitMqCommandPublisher.cs`):
```csharp
var conn = await myConnectionProvider.GetConnectionAsync(ct);   // connection singleton, mở lười
await using var channel = await conn.CreateChannelAsync(ct);    // channel MỖI lần gửi (channel rẻ, không thread-safe)
await channel.ExchangeDeclareAsync(Exchange, Direct, durable:true, ...);  // idempotent
var props = new BasicProperties { Persistent = true, Type = commandType }; // Persistent = sống qua restart
await channel.BasicPublishAsync(Exchange, routingKey: queue, ..., body: payloadJson);
```
- **`RabbitMqConnectionProvider.GetConnectionAsync`**: double-check lock + `SemaphoreSlim` để chỉ mở **1**
  connection. Mở **lười** → app không sập khi RabbitMQ chưa lên.
- `routingKey = queue` + exchange direct → message rơi đúng vào queue `email.send`.
- So với thiết kế cũ: bỏ hẳn `OutboxWriter→Kafka→process manager→RabbitMqCommandBus`. Giờ chỉ
  `OutboxRouter→(outbox)→dispatcher→RabbitMqCommandPublisher`. Ít hop, đúng thế mạnh.

---

## H. RabbitMQ → worker: consumer + retry + DLQ

File `Messaging/RabbitMq/RabbitMqConsumerHostedService.cs`. `BackgroundService` nền (Singleton, chạy suốt
vòng đời app). Nó **nghe** các queue và giao message cho worker — đối xứng với `KafkaConsumerHostedService`.

### Bản đồ 5 method — cái nào làm gì, gọi ai

| Method | Nhóm | Trách nhiệm (1 câu) | Chạy mấy lần |
|---|---|---|---|
| `ExecuteAsync` | vòng đời | Điểm vào từ `BackgroundService`. Vòng lặp bền: gọi `StartConsumersAsync`, canh chừng, lỗi thì dựng lại. | 1 (chạy suốt) |
| `StartConsumersAsync` | khởi động | Mở connection, khai 2 exchange, và với **mỗi queue** dựng topology + gắn 1 consumer. | mỗi lần (re)connect |
| `DeclareQueueTopologyAsync` | khởi động | Khai 1 queue chính + 1 DLQ + các binding (dead-letter). | 1 lần / queue |
| `OnMessageAsync` | mỗi message | Nhận 1 message: deserialize → gọi worker → **ACK**; lỗi thì retry → hết thì **NACK→DLQ**. | mỗi message tới |
| `CloseChannelsAsync` | dọn dẹp | Đóng/huỷ mọi channel (khi shutdown hoặc trước khi dựng lại). | khi tắt / reconnect |

**Quan hệ gọi nhau:**
```
ExecuteAsync (vòng bền)
   └─ StartConsumersAsync              ← dựng 1 lần khi (re)connect
        └─ (mỗi queue) DeclareQueueTopologyAsync + gắn consumer.ReceivedAsync = OnMessageAsync
   └─ ... idle canh channel còn sống ...
   └─ CloseChannelsAsync               ← khi lỗi/tắt, rồi lặp lại StartConsumersAsync
                     ▲
   OnMessageAsync ───┘  ← RabbitMQ tự gọi (event) MỖI khi có message, KHÔNG do ExecuteAsync gọi trực tiếp
```
Điểm hay gây rối: `OnMessageAsync` **không** nằm trong vòng lặp `ExecuteAsync`. Sau khi `StartConsumersAsync`
gắn `consumer.ReceivedAsync += OnMessageAsync`, chính **RabbitMQ client** sẽ gọi `OnMessageAsync` trên
thread callback của nó mỗi khi có message. `ExecuteAsync` lúc đó chỉ **ngồi canh** (idle-wait) xem channel
còn mở không, không đụng gì tới việc xử lý message.

### `ExecuteAsync` — điểm vào + vòng bền (tự hồi phục khi broker chết)
```csharp
while (!stoppingToken.IsCancellationRequested) {
    try {
        await StartConsumersAsync(ct);                       // dựng connection + consumers
        // consumer chạy trên thread callback của RabbitMQ; ở đây chỉ NGỒI CANH:
        while (!ct.IsCancellationRequested && myChannels.All(c => c.IsOpen))
            await Task.Delay(5s, ct);                         // channel còn sống → ngủ, lặp lại
    }
    catch (Exception e) { log; await Task.Delay(5s, ct); }    // broker chưa lên / rớt → thử lại
    await CloseChannelsAsync();                                // dọn trước khi dựng lại vòng sau
}
```
Nếu RabbitMQ chưa chạy, `StartConsumersAsync` ném → catch → 5s sau thử lại. Đây là lý do app **không sập**
khi broker tắt (giống outbox giữ message ở mục E).

### Khái niệm nền: exchange / queue / binding / topology (RabbitMQ khác Kafka)

Ở RabbitMQ, producer **KHÔNG gửi thẳng vào queue** — gửi vào **exchange**, rồi exchange định tuyến. (Kafka
ngược lại: ghi thẳng topic.) Bốn khái niệm:

| Khái niệm | Là gì | Ví dụ trong dự án |
|---|---|---|
| **Queue** | hộp thư, message nằm chờ worker lấy | `email.send`, `email.send.dlq` |
| **Exchange** | "bưu điện" định tuyến message vào queue theo quy tắc | `selling.commands` (chính), `selling.commands.dlx` (DLX) |
| **Routing key** | "địa chỉ trên phong bì" producer gắn khi publish | `"email.send"` |
| **Binding** | quy tắc nối exchange→queue kèm binding key | `email.send` bound vào `selling.commands` key `"email.send"` |

Loại exchange quyết cách match: **direct** (ta dùng) = routing key khớp **chính xác** binding key → vào
đúng queue đó; ngoài ra có **fanout** (phát mọi queue), **topic** (khớp mẫu `order.*`), **headers**.

**Topology** = toàn bộ sơ đồ đấu nối (exchange + queue + binding + tham số dead-letter). `StartConsumersAsync`
khai exchange, `DeclareQueueTopologyAsync` khai queue + binding — cộng lại là "dựng ống". Khai báo
**idempotent** (khai lại y hệt = no-op) nên chạy lại mỗi lần khởi động vẫn an toàn.

```
dispatcher ──publish, routingKey="email.send"──▶ [selling.commands] (direct)
                                                     │ binding "email.send"
                                                     ▼
                                                 [email.send] ──▶ worker (EmailCommandWorker)
                                                     │ NACK requeue=false (hết retry, nhờ arg x-dead-letter-exchange)
                                                     ▼
                                                 [selling.commands.dlx] (direct)
                                                     │ binding "email.send"
                                                     ▼
                                                 [email.send.dlq]  (chờ soi tay)
```
> Vì sao qua exchange chứ không thẳng queue? **Tách rời**: producer chỉ cần biết *exchange + routing key*,
> không cần biết có bao nhiêu queue/consumer. Đổi định tuyến = sửa **binding**, không đụng producer. Đây là
> khả năng route linh hoạt mà Kafka không có (Kafka route = partition theo key).

### Khái niệm nền: DLQ / DLX — để làm gì (bài toán "poison message")

Giả sử queue `email.send` có một message worker xử lý **fail hoài** (payload hỏng, dữ liệu sai). Không có
DLQ, chỉ còn 2 lựa chọn tồi:

| Cách xử lý message hỏng | Hậu quả |
|---|---|
| NACK **requeue=true** (trả lại queue) | quay lại → fail → quay lại → **lặp vô tận**, chặn cả queue (*poison message*) |
| NACK **requeue=false**, không DLX | message bị **vứt, mất luôn**, không rõ vì sao |

**DLQ cho lựa chọn thứ 3**: đẩy message hỏng sang **một hộp thư riêng** → không mất, không lặp, không chặn
message lành. Rảnh thì soi lại, sửa, phát lại.

- **DLQ (Dead Letter Queue)** = "hộp thư chết" — một **queue** bình thường, chỉ khác *công dụng*: chứa
  message thất bại (`email.send.dlq`).
- **DLX (Dead Letter Exchange)** = "quầy phân loại thư chết" — một **exchange**. Vì mọi thứ ở RabbitMQ route
  qua exchange, message chết cũng phải đi qua exchange rồi mới tới DLQ → đó là DLX (`selling.commands.dlx`).
  (Tham số `x-dead-letter-exchange` chỉ nhận tên **exchange**, không nhận thẳng queue — nên bắt buộc có DLX.)

> Nôm na: **DLX = quầy phân loại, DLQ = cái hộp.** Message chết đi qua DLX (quầy) → binding → rơi vào DLQ (hộp).

Một message bị "dead-letter" khi: **(1) bị reject/NACK requeue=false** ← ta dùng cái này (hết 3 retry);
(2) hết TTL; (3) queue đầy. **Kafka không có DLQ/DLX sẵn** — fail thì hoặc kẹt (không commit) hoặc mất
(commit bỏ qua) hoặc tự code "dead-letter topic". Có sẵn cơ chế này là lý do RabbitMQ hợp với *command*.

**`StartConsumersAsync`** — dựng topology + 1 consumer/queue:
```csharp
// 1 channel khai 2 exchange dùng chung:
await setup.ExchangeDeclareAsync("selling.commands", Direct, durable:true);
await setup.ExchangeDeclareAsync("selling.commands.dlx", Direct, durable:true);  // dead-letter exchange

foreach (var queue in myRegistry.Queues) {              // email.send, invoice.issue, notification.send
    var channel = await conn.CreateChannelAsync(ct);   // 1 CHANNEL riêng cho mỗi queue
    await DeclareQueueTopologyAsync(channel, queue, ct);   // dựng queue+DLQ+binding (ở trên)
    await channel.BasicQosAsync(prefetchCount: 1, ...);   // mỗi lúc chỉ nhận 1 message/queue
    var consumer = new AsyncEventingBasicConsumer(channel);
    consumer.ReceivedAsync += (s, ea) => OnMessageAsync(channel, queue, ea, ct);  // TREO callback
    await channel.BasicConsumeAsync(queue, autoAck:false, consumer, ct);  // bắt đầu nghe queue
    myChannels.Add(channel);                           // giữ để canh sống & đóng khi tắt
}
```
Giải thích các dòng dễ gợn:
- **`CreateChannelAsync` mỗi queue** — 1 connection (mở ở `RabbitMqConnectionProvider`) nhưng **nhiều
  channel** (channel là "kênh ảo" nhẹ trên connection). Mỗi queue 1 channel để ack/nack độc lập, không
  giẫm chân nhau (channel **không** thread-safe).
- **`BasicQosAsync(prefetchCount:1)`** — RabbitMQ chỉ giao **1 message/lần** cho consumer này tới khi nó
  ack; nhờ vậy vòng retry trong `OnMessageAsync` **độc chiếm** channel, không bị message thứ 2 chen vào
  giữa lúc đang retry. (Muốn xử lý song song nhiều hơn thì tăng prefetch — đánh đổi với thứ tự.)
- **`ReceivedAsync += OnMessageAsync`** — đây là chỗ **treo callback**: từ giờ RabbitMQ **tự gọi**
  `OnMessageAsync` mỗi khi có message (không phải ta gọi). Khác Kafka phải tự `Consume()` trong vòng lặp.
- **`autoAck:false`** — TẮT tự-ack. Ta **tự ack thủ công** sau khi worker thành công; nếu không, message
  bị coi là "đã nhận xong" ngay khi vừa giao → worker lỗi là **mất việc**. Đây là điều kiện để có retry/DLQ.
- **`myChannels.Add`** — lưu lại để `ExecuteAsync` canh `IsOpen` và `CloseChannelsAsync` đóng khi tắt.

**`DeclareQueueTopologyAsync`** — đấu nối TRỌN VẸN một queue: **đường vào** (nhận việc) + **đường thoát khi
lỗi** (dead-letter). 4 dòng = 2 con đường. Với `queue = "email.send"`:
```csharp
var dlq = "email.send.dlq";

// ── ĐƯỜNG THOÁT (khi lỗi) ──
await channel.QueueDeclareAsync(dlq, durable:true, ...);                             // (1) tạo hộp thư chết
await channel.QueueBindAsync(dlq, "selling.commands.dlx", routingKey:"email.send");  // (2) nối DLQ ↔ DLX

// ── ĐƯỜNG VÀO (nhận việc) ──
var args = {
    ["x-dead-letter-exchange"]    = "selling.commands.dlx",   // luật: bị reject → đẩy sang DLX
    ["x-dead-letter-routing-key"] = "email.send"
};
await channel.QueueDeclareAsync("email.send", durable:true, arguments:args, ...);    // (3) tạo queue chính (+ luật dead-letter)
await channel.QueueBindAsync("email.send", "selling.commands", routingKey:"email.send"); // (4) nối queue ↔ exchange chính
```
Map vào sơ đồ topology ở trên:
- **(4)** `selling.commands ─"email.send"→ email.send` : để message publish routingKey `email.send` **rơi vào** queue.
- **(3) args** : gắn luật "nếu chết thì đi đâu" — reject ⇒ sang DLX với key `email.send`.
- **(2)** `selling.commands.dlx ─"email.send"→ email.send.dlq` : để message chết **rơi vào** DLQ.
- **(1)** : tạo DLQ để (2) có chỗ trỏ tới.

Hai **exchange** KHÔNG khai ở đây — chúng khai 1 lần ở `StartConsumersAsync` (dùng chung mọi queue). Method
này chỉ lo phần **riêng của từng queue**: queue chính + DLQ + 2 binding.

Ý nghĩa tham số `QueueDeclareAsync`:
- `durable: true` → queue **sống qua restart broker** (metadata ghi đĩa). Kết hợp message `Persistent=true`
  (mục G) ⇒ việc không mất khi RabbitMQ khởi động lại.
- `exclusive: false` → queue **không** khoá vào 1 connection → nhiều consumer/instance cùng dùng (cần cho scale).
- `autoDelete: false` → queue **không** tự xoá khi consumer cuối rớt → message vẫn nằm chờ.

**`OnMessageAsync`** — bám theo MỘT message cụ thể: một `SendEmailCommand` vừa rơi vào queue `email.send`
(do dispatcher publish ở mục G). `theQueue = "email.send"`, `ea.Body = {"to":"customer-...@example.com","subject":"Đơn ... đã được xác nhận","body":"..."}`.

```csharp
var reg = myRegistry.ByQueue("email.send");      // → CommandRegistration của email.send
var payload = Encoding.UTF8.GetString(ea.Body.Span);   // JSON của SendEmailCommand
for (int attempt = 1; attempt <= MaxRetries; attempt++) {   // MaxRetries = 3 (RabbitMqSettings)
    try {
        await using var scope = myScopeFactory.CreateAsyncScope();
        await reg.Dispatch(scope.ServiceProvider, payload, ct);   // ← closure của email.send (bung ra dưới)
        await channel.BasicAckAsync(ea.DeliveryTag, ...);         // OK → ACK, xong
        return;
    }
    catch (Exception e) when (attempt < MaxRetries) {   // lỗi & còn lượt → chờ 300ms×attempt, thử lại
        await Task.Delay(300ms * attempt, ct);
    }
    catch (Exception e) {                               // lỗi ở lượt cuối → NACK không requeue
        await channel.BasicNackAsync(ea.DeliveryTag, requeue:false, ...);   // → dead-letter sang email.send.dlq
        return;
    }
}
```

**`reg.Dispatch` là gì với ĐÚNG queue này?** Lúc khởi động, `Register<SendEmailCommand>("email.send")` đã
"đóng băng" một closure gõ sẵn kiểu `SendEmailCommand`. Bung ra, nó chạy chính xác như sau (khác event:
command chỉ có **ĐÚNG 1** handler → `GetRequiredService`, không phải `GetServices`):

```csharp
// closure cho riêng SendEmailCommand:
async (provider, json, ct) => {
    var cmd = JsonSerializer.Deserialize<SendEmailCommand>(json, ...);   // 1. deserialize đúng kiểu
    //    cmd.To = "customer-...@example.com", cmd.Subject = "Đơn ... đã được xác nhận"
    var handler = provider.GetRequiredService<ICommandHandler<SendEmailCommand>>();  // 2. đúng 1 worker
    //    → EmailCommandWorker
    await handler.HandleAsync(cmd, ct);                                  // 3. gọi worker
}
```

**Worker cụ thể** — `EmailCommandWorker.HandleAsync(SendEmailCommand)` (`Messaging/Workers/CommandWorkers.cs`):
```csharp
public Task HandleAsync(SendEmailCommand cmd, CancellationToken ct)
    => myEmailSender.SendAsync(new EmailMessage(cmd.To, cmd.Subject, cmd.Body), ct);   // → IEmailSender
```

**Gateway cụ thể** — `LoggingEmailSender.SendAsync` (`Messaging/Services/FakeSideEffects.cs`) — ⚠️ bản
**giả lập**: chỉ log `📧 EMAIL → customer-...@example.com | Đơn ... đã được xác nhận | ...`, **không** gửi
mail thật. Đổi sang SMTP/SendGrid thật = thay impl `IEmailSender` trong DI, **không đụng** worker hay
consumer (port/adapter, y như `IAnalyticsStore` ở mục F).

**Đối chiếu với mục F (Kafka) — cùng cơ chế registry, khác 2 điểm:**

| | Kafka (mục F) | RabbitMQ (mục H) |
|---|---|---|
| Tra bằng | header `eventType` → `Resolve(name)` | tên queue → `ByQueue(queue)` |
| Số handler | `GetServices` — **nhiều** (fan-out) | `GetRequiredService` — **đúng 1** worker |
| Xác nhận đã xử lý | `consumer.Commit(offset)` | `BasicAckAsync(deliveryTag)` |
| Khi handler lỗi | không commit → đọc lại | retry ≤3 → hết thì NACK → **DLQ** |

Nếu `email.send` xử lý fail 3 lần (vd `IEmailSender` ném) → NACK requeue=false → RabbitMQ đẩy message sang
`email.send.dlq` (nhờ `x-dead-letter-exchange` khai ở topology). Bạn mở RabbitMQ UI (:15672) sẽ thấy
message nằm trong queue `email.send.dlq` để soi/xử lý tay — đây là thứ Kafka không có sẵn.

### `CloseChannelsAsync` — dọn dẹp
```csharp
foreach (var channel in myChannels) { try { await channel.CloseAsync(); await channel.DisposeAsync(); } catch { } }
myChannels.Clear();
```
Đóng mọi channel đã mở (mỗi queue 1 channel, lưu trong `myChannels`). Gọi ở 2 chỗ: khi app **tắt**, và
**trước mỗi lần dựng lại** trong `ExecuteAsync` (để không rò channel cũ khi reconnect). Bọc `try/catch`
rỗng vì lúc này chỉ cần best-effort — channel có thể đã chết sẵn.

> **Gói lại mục H:** 3 method **khởi động** (`ExecuteAsync`→`StartConsumersAsync`→`DeclareQueueTopologyAsync`)
> dựng sẵn "đường ống" (exchange + queue + DLQ + consumer) MỘT lần; sau đó `OnMessageAsync` do RabbitMQ gọi
> **mỗi message** để chạy worker + ack/retry/DLQ; `CloseChannelsAsync` dọn khi cần. Hiểu theo nhóm
> "dựng ống một lần / chảy nước mỗi message" sẽ dễ hơn đọc tuần tự từng method.

---

## I. Bức tranh tổng: 1 lần confirm đơn

```
HTTP POST /orders/{id}/confirm
  │
  ▼ (đồng bộ, trong 1 request)
OrdersController → ISender → ConfirmOrderCommandHandler → OrderWriteService.ConfirmAsync
  → Order.Confirm()  ── Raise(OrderConfirmedEvent) vào list DomainEvents
  → SqlOrderWriteRepository.UpdateAsync
       ├─ OutboxWriter.Stage(order) → OutboxRouter.Route → 3 dòng: 1 Kafka + 2 RabbitMQ (chưa save)
       └─ SaveChangesAsync()  ══ 1 TRANSACTION SQL: UPDATE Orders + INSERT 3×OutboxMessages ══> COMMIT
  ← HTTP 200 trả về NGAY (chưa gửi mail; mail là việc nền)

... (bất đồng bộ, các vòng lặp nền) ...

OutboxDispatcher (mỗi 2s) — drain CẢ SqlOutboxStore + MongoOutboxStore, route theo Destination:
  ├─ dòng Kafka    → KafkaEventBus.PublishAsync        ─▶ Kafka [orders.events]
  │                                                        └─▶ AnalyticsProjectionHandler (📊)
  ├─ dòng RabbitMQ → RabbitMqCommandPublisher.PublishAsync ─▶ RabbitMQ [email.send]
  ├─ dòng RabbitMQ → RabbitMqCommandPublisher.PublishAsync ─▶ RabbitMQ [notification.send]
  └─ MarkPublishedAsync cho từng dòng (ProcessedUtc = now)

RabbitMqConsumerHostedService [email.send]
  → OnMessageAsync (retry ≤3, DLQ nếu fail) → EmailCommandWorker → LoggingEmailSender (📧)
  → BasicAck

(song song) Product bên Mongo: DecreaseStock → ProductStockChangedEvent
  → MongoProductWriteRepository (trong transaction Mongo) → MongoOutboxWriter.Stage
  → MongoOutboxStore → dispatcher → Kafka [products.events] (📊) (+ restock.alert nếu hết hàng)
```

**Đọc theo màu:**
- Phần **đồng bộ** (request của khách) kết thúc ngay sau khi commit SQL — nhanh, không chờ mail.
- Phần **bất đồng bộ** (nền) lo publish + gửi mail/hoá đơn/notify + analytics. Nếu mail lỗi, retry/DLQ ở
  RabbitMQ; nếu Kafka chết, Outbox giữ event. Khách **không** bị ảnh hưởng.

Đó là toàn bộ giá trị của kiến trúc: **nghiệp vụ lõi nhất quán & nhanh (SQL + Outbox 1 transaction)**,
còn **side effect tách rời, chịu lỗi, mở rộng được (Kafka fan-out + RabbitMQ retry/DLQ)**.

---

## J. Phía MongoDB (Product): outbox trong transaction Mongo

SQL không phải nơi duy nhất phát event — **Product bên MongoDB cũng có outbox riêng**, dùng CHUNG
`OutboxRouter` và được CÙNG một `OutboxDispatcher` drain. Khác biệt nằm ở chỗ nó gắn vào **bước saga
"trừ kho"** (compensatable, chạy TRƯỚC pivot SQL), không phải vào pivot.

**1. Product phát event** (`Domain/Products/Product.cs`): `DecreaseStock`/`IncreaseStock` raise
`ProductStockChangedEvent(ProductId, Name, NewStock, ChangeQuantity)` (change âm khi trừ, dương khi cộng).
Được gọi trong `OrderWriteService.ConfirmAsync` (trừ kho) và `CancelAsync` (hoàn kho).

**2. `MongoProductWriteRepository.UpdateRangeAsync`** (`MongoDB.Saga/Repositories/Write/`) — bước saga
Mongo, commit-per-step trong **1 transaction Mongo**:
```csharp
// ... map document, tính aDeltas (tồn cũ - tồn mới) để saga còn undo ...
if (!mySagaContext.IsActive || aDeltas.Count == 0) {
    await ctx.SaveChangesAsync(ct);   // ngoài saga / không đổi kho → ghi thường
    return;
}
var aStep = new SagaStepInfo("DecreaseStock", Compensatable, "RevertStock", json(aDeltas), false);

await using var tx = await ctx.Database.BeginTransactionAsync(ct);   // ← transaction Mongo
await mySagaStore.StageStepAsync(sagaId, aStep, ct);   // (a) ghi bước saga vào ledger (chưa save)
myOutboxWriter.Stage(aProducts);                        // (b) ghi outbox từ ProductStockChangedEvent (chưa save)
await ctx.SaveChangesAsync(ct);                         // (c) 1 SAVE: tồn kho + bước saga + outbox
await tx.CommitAsync(ct);                               // (d) COMMIT nguyên tử cả 3
```
**3 thứ vào 1 transaction Mongo**: thay đổi tồn kho (`products`) + bước saga (`saga_instances`) + dòng
outbox (`outbox_messages`). Đây là lý do **Mongo phải chạy replica set** — transaction đa-document chỉ có
trên replica set (xem `docs/mongo-replica-set.md`, `docker-compose.mongo-rs.yml`).

**3. `MongoOutboxWriter.Stage(products)`** (`MongoDB.Saga/Outbox/`): giống hệt SQL `OutboxWriter` — gọi
`OutboxRouter.Route(product.DomainEvents)` (CHUNG policy), `Add` `OutboxMessageDocument` vào context (chưa
save), rồi `ClearDomainEvents`. Router cho `ProductStockChangedEvent`:
- luôn 1 **Fact** `ProductStockChangedIntegrationEvent` → Kafka `products.events` (analytics tồn kho);
- nếu `NewStock == 0` thêm 1 **Command** `RestockAlertCommand` → RabbitMQ `restock.alert`.

**4. `MongoOutboxStore`** đăng ký như **một `IOutboxStore` NỮA** (bên cạnh `SqlOutboxStore`). Ở phần E,
`scope.GetServices<IOutboxStore>()` trả **cả hai** → dispatcher drain lần lượt, route mỗi dòng theo
`Destination` y như SQL. Không cần dispatcher riêng cho Mongo.

**5. ⚠️ Caveat "publish sớm" (điểm học quan trọng).** Bước trừ kho là **Compensatable, chạy TRƯỚC pivot**.
Dòng outbox `ProductStockChanged(-qty)` commit ngay cùng lần trừ kho → **sẽ được publish** dù saga chưa
chắc thành công. Nếu pivot SQL sau đó lỗi, saga hoàn kho bằng `MongoStockCompensationHandler` — nhưng
handler này sửa **thẳng document** (`StockQuantity += ...`), **không** qua domain nên **không** phát
`ProductStockChanged(+qty)` bù lại. Hệ quả: trong ca rollback hiếm, analytics thấy một chuyển động kho
đã bị revert mà không có event đối ứng (eventually inconsistent).
Cách khắc phục nếu cần chặt chẽ (chưa làm — để đơn giản):
- chỉ enqueue **fact của bước compensatable** SAU pivot (giữ outbox row ở trạng thái "hold" tới khi pivot commit); hoặc
- cho compensation phát một **event bù** (`ProductStockChanged(+qty)`) để analytics tự cân bằng; hoặc
- coi `products.events` là *state hiện tại* (mang `NewStock`) thay vì *delta* — người đọc lấy giá trị mới nhất, chuyển động thừa không sai lệch tồn kho.

> Tóm lại: **cùng cơ chế outbox + router + dispatcher cho cả 2 DB**, chỉ khác điểm gắn (Mongo = bước
> compensatable pre-pivot, SQL = pivot). Caveat trên là ranh giới tinh tế giữa outbox và saga
> compensatable — đáng để hiểu, và dễ vá khi nghiệp vụ đòi hỏi.
