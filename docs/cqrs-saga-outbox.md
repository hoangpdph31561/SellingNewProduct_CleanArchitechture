# CQRS + Saga + Outbox — đọc code theo đúng thứ tự luồng chạy

Tài liệu này đi **xuyên qua code thật**, theo **thứ tự thực thi** của **một** request duy nhất chạm đủ
cả ba pattern. Mục tiêu: đọc xong bạn biết **mảnh code nào chạy lúc nào**, **vì sao nó nằm ở lớp đó**,
và **ba pattern khớp vào nhau ở đâu**.

Bổ trợ (không trùng lặp): [saga-hybrid.md](saga-hybrid.md) giải thích *vì sao* chọn saga;
[outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md) giải thích *vì sao* Kafka + RabbitMQ;
[polly-circuit-breaker.md](polly-circuit-breaker.md) + [polly-bulkhead.md](polly-bulkhead.md) lo phần
đuôi (gọi gateway). Tài liệu này lo phần **ghép nối**.

---

## 0. Chọn luồng nào để đọc? → `POST /api/orders/{id}/confirm`

Đây là điểm dễ nhầm nhất khi đọc code dự án này:

| Luồng | Chạm store nào | Có saga? | Có outbox? |
|---|---|---|---|
| `POST /api/orders` (**Place**) | chỉ **SQL** (thêm Order) | ❌ **không** — 1 store thì local transaction là đủ | ✅ (1 fact `OrderPlaced`) |
| `POST /api/orders/{id}/confirm` (**Confirm**) | **Mongo** (trừ kho) **+ SQL** (đổi trạng thái) | ✅ **có** — 2 DB, không có distributed transaction | ✅ (cả 2 phía) |

→ **Confirm** là request **duy nhất** gộp đủ **CQRS + Saga + Outbox**. Cả tài liệu này bám theo nó.

**Bản đồ ba pattern — mỗi cái giải một bài toán khác nhau:**

```
CQRS   : "request đi đường nào?"        → HTTP → ISender → Handler mỏng → Domain Service
Saga   : "2 DB làm sao nhất quán?"      → commit-per-step + compensation + pivot
Outbox : "đổi DB & phát event sao cho   → ghi event CÙNG transaction với business write,
          không mất, không cần 2PC?"       rồi relay ra broker sau
```

Chúng **không chồng lấn**: CQRS ở tầng **Application**, Saga + Outbox ở tầng **Infrastructure**, và
**Domain không biết gì về cả hai** — đó chính là điều làm nó "clean".

---

## 1. CQRS — từ HTTP xuống Domain (tầng Application)

### 1.1. Controller — mỏng, chỉ dịch HTTP ↔ command

`API/Controllers/OrdersController.cs`: không có business logic, chỉ `ISender.Send(...)`.

```csharp
var aOrder = await mySender.Send(new ConfirmOrderCommand(theId), theCancellationToken);
return Ok(aOrder.ToResponse());
```

### 1.2. `ValidationBehavior` — chặn trước, ngoài cùng pipeline

`Application/Common/Behaviors/ValidationBehavior.cs`, đăng ký trong `Application/DependencyInjection.cs`:

```csharp
theServices.AddMediatR(c => c.RegisterServicesFromAssembly(aAssembly));
theServices.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)); // chạy TRƯỚC handler
theServices.AddValidatorsFromAssembly(aAssembly);
```

Validate **hình dạng request** (rỗng? âm? quá dài?) — **không** phải business rule. Rule nghiệp vụ
(“còn đủ hàng không?”) nằm ở Domain, vì nó cần đọc DB và thuộc về **miền**, không thuộc về **form**.

### 1.3. Command + Handler — handler là adapter mỏng

`Application/Orders/Commands/ConfirmOrderCommand.cs` — cả command lẫn handler chung **một file** (quy
ước one-file-per-feature):

```csharp
public sealed record ConfirmOrderCommand(Guid Id) : IRequest<Order>;

public sealed class ConfirmOrderCommandHandler : IRequestHandler<ConfirmOrderCommand, Order>
{
    public Task<Order> Handle(ConfirmOrderCommand theCommand, CancellationToken theCancellationToken) =>
        myOrderWriteService.ConfirmAsync(theCommand.Id, theCancellationToken);   // ← 1 dòng, hết
}
```

> **Điểm mấu chốt của CQRS ở đây:** handler **không chứa** logic. Nó chỉ chuyển từ *thế giới MediatR*
> sang **inbound port** `IOrderWriteService` của Domain. Nhờ vậy **Domain không hề tham chiếu MediatR** —
> muốn bỏ MediatR chỉ cần thay tầng Application, Domain không suy suyển.

So sánh với `PlaceOrderCommand.cs`: handler ở đó "dày" hơn chút vì phải dựng value object
(`Address.Create`) và map `OrderItemCommand` → `OrderLine`. Đó vẫn là **dịch dữ liệu**, không phải rule.

| Mảnh | File | Vai trò |
|---|---|---|
| Controller | `API/Controllers/OrdersController.cs` | HTTP → command, bọc `ApiResponse` |
| Behavior | `Application/Common/Behaviors/ValidationBehavior.cs` | validate shape, ngoài cùng |
| Command + Handler | `Application/Orders/Commands/ConfirmOrderCommand.cs` | adapter mỏng → inbound port |
| Inbound port | `Domain/Interfaces/Inbound/IOrderWriteService.cs` | ranh giới vào Domain |

---

## 2. Domain — business logic thuần, **KHÔNG biết saga tồn tại**

`Domain/Services/OrderWriteService.cs` → `ConfirmAsync`:

```csharp
var aOrder    = await LoadOrderAsync(theOrderId, ct);          // đọc SQL (qua port)
var aProducts = await LoadOrderProductsAsync(aOrder, ct);      // đọc Mongo (qua port)

foreach (var aDetail in aOrder.Details)                        // RULE: re-check tồn kho
    EnsureEnoughStock(GetProductOrThrow(aProducts, aDetail.ProductId), aDetail.Quantity);

aOrder.Confirm();                                              // → raise OrderConfirmedEvent

foreach (var aDetail in aOrder.Details)
    aProducts[aDetail.ProductId].DecreaseStock(aDetail.Quantity);  // → raise ProductStockChangedEvent

await PersistInTransactionAsync(aOrder, aProducts.Values, ct);     // ← seam quan trọng nhất
```

Và `PersistInTransactionAsync`:

```csharp
await using var aTransaction = await myUnitOfWork.BeginTransactionAsync(ct);
try
{
    await myProductRepository.UpdateRangeAsync(theProducts, ct);   // bước 1
    await myOrderRepository.UpdateAsync(theOrder, ct);             // bước 2
    await aTransaction.CommitAsync(ct);
}
catch { await aTransaction.RollbackAsync(ct); throw; }
```

> ### 🔑 Đây là seam quyết định của cả kiến trúc
> Domain **tưởng** nó đang mở **một transaction** bình thường. Nó chỉ thấy `IUnitOfWork` — một
> **outbound port** thuần Domain. Nhưng ở provider Saga/Hybrid, cái được tiêm vào **không phải** một DB
> transaction mà là **một SAGA orchestration** (§3).
>
> Hệ quả: đổi provider SQL-only → Hybrid/Saga **không sửa một dòng Domain nào**. Đây chính là chỗ
> "Dependency Inversion" trả công: Domain định nghĩa *hợp đồng*, Infrastructure chọn *ý nghĩa*.

Cũng lưu ý **2 domain event** đã được raise **trong bộ nhớ** ở bước này (`OrderConfirmedEvent`,
`ProductStockChangedEvent`) nhưng **chưa** ai publish — chúng nằm chờ trong aggregate. Outbox (§4) sẽ
nhặt chúng.

---

## 3. Saga — Infrastructure "tráo" ý nghĩa của `IUnitOfWork`

Mô hình: **commit-per-step + compensation**, ledger **duy nhất** ở Mongo (`saga_instances`).

### 3.1. `SagaUnitOfWork.BeginTransactionAsync` — mở saga, KHÔNG mở transaction

`Saga.Core/Persistence/SagaUnitOfWork.cs`:

```csharp
public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
{
    myContext.Begin("OrderWrite");                                  // sinh SagaId, Status=Started
    await myStore.StartAsync(myContext.SagaId, myContext.Name, ct); // ghi ledger (durable)
    return new SagaUnitOfWorkTransaction(myContext, myStore, myCompensator);
}
```

**Không có transaction nào được giữ mở.** Đây là khác biệt cốt lõi so với thiết kế "2PC giả": mỗi bước
sẽ **tự commit ngay** vào DB của nó.

### 3.2. `SagaContext` — scoped, "sổ tay chung" của request

`Saga.Core/Saga/SagaContext.cs`. Đăng ký **scoped** ⇒ unit of work, các repository saga-aware và store
**cùng một instance** ⇒ tất cả đồng ý "saga nào đang chạy".

```csharp
public bool IsActive => Status == SagaStatus.Started;   // ← repository dựa vào cờ này
```

Nó **không** giữ danh sách compensation trong RAM — undo info được **ghi xuống ledger ngay** khi bước
commit, nên crash không làm mất.

### 3.3. `SagaStepKind` — cái seam cho phép saga mở rộng

`Saga.Core/Saga/SagaStep.cs`:

| Kind | Chạy khi nào | Khi hỏng thì? |
|---|---|---|
| **`Compensatable`** | **trước** pivot | replay compensation (undo) |
| **`Pivot`** | **điểm không quay đầu** | đã qua đây ⇒ saga coi như **thành công**, không bao giờ undo |
| **`RetryableForward`** | **sau** pivot | **không** undo — chỉ retry tiến tới |

> Thêm bước mới (vd update-user-history) chỉ cần **chọn đúng Kind** — orchestrator không phải sửa.

### 3.4. Bước 1 — Mongo (`Compensatable`): trừ kho + ghi undo, **atomic**

`MongoDB.Saga/Repositories/Write/MongoProductWriteRepository.cs` → `UpdateRangeAsync`:

```csharp
// (a) Chụp delta TRƯỚC khi mutate — nếu không, không còn gì để undo.
var aRemoved = aDocument.StockQuantity - aProduct.StockQuantity;
if (aRemoved != 0) aDeltas.Add(new StockDelta(aProduct.Id, aRemoved));
ProductMapper.MapInto(aDocument, aProduct);

// (b) Ngoài saga (hoặc không đổi kho) → save thường, không enroll gì cả.
if (!mySagaContext.IsActive || aDeltas.Count == 0) { await ...SaveChangesAsync(ct); return; }

// (c) Trong saga: mô tả bước + cách undo nó.
var aStep = new SagaStepInfo("DecreaseStock", SagaStepKind.Compensatable,
    MongoStockCompensationHandler.Type,            // "RevertStock" — key để tra handler
    JsonSerializer.Serialize(aDeltas),             // payload để undo
    Compensated: false);

// (d) MỘT transaction Mongo gói: [đổi kho] + [ghi bước vào ledger] + [ghi outbox rows]
await using var aTransaction = await ...BeginTransactionAsync(ct);
await mySagaStore.StageStepAsync(mySagaContext.SagaId, aStep, ct);  // stage, KHÔNG save
myOutboxWriter.Stage(aProducts);                                    // stage, KHÔNG save
await ...SaveChangesAsync(ct);
await aTransaction.CommitAsync(ct);                                 // ← COMMIT NGAY
```

> **Vì sao `StageStepAsync` chứ không `EnrollStepAsync`?**
> `MongoSagaStore.EnrollStepAsync` = `StageStepAsync` **+ `SaveChangesAsync`**. Ở đây repository **tự**
> quản transaction, nên nó cần store **chỉ stage** rồi để `SaveChanges` **của chính nó** commit tất cả
> **cùng lúc**. Nếu store tự save → tách thành 2 commit → có cửa sổ crash "kho đã trừ nhưng ledger chưa
> ghi" ⇒ **mất khả năng undo**. Đây chính là lý do `MongoSagaStore` **dùng chung** `MongoAppDbContext`
> scoped với repository.

Sau (d): **kho đã trừ thật**, **undo info đã durable**, **outbox rows đã nằm sẵn** — cả ba là **một**
commit nguyên tử. Đây là chỗ **Saga và Outbox giao nhau lần thứ nhất**.

### 3.5. Bước 2 — SQL (`Pivot`): điểm không quay đầu

`SqlServer.Saga/Repositories/Write/SqlOrderWriteRepository.cs` → `UpdateAsync`:

```csharp
OrderMapper.MapInto(aRecord, theOrder);
var aPublished = myOutboxWriter.Stage(theOrder);   // stage outbox (fact + commands)
await myAppDbContext.SaveChangesAsync(ct);         // ← [order đổi] + [outbox rows] commit CÙNG nhau

await MarkPivotCommittedAsync(ct);                 // ledger += Pivot   ← qua đây là không quay đầu
await RecordPublishStepAsync(aPublished, ct);      // ledger += RetryableForward (audit)
```

Thứ tự **rất có chủ ý**: commit SQL **trước**, ghi pivot marker **ngay sau**. Nếu chết **giữa** hai
dòng này → xem §3.8 (pivot-guard xử lý).

### 3.6. Kết thúc happy path — "xoá ledger = thành công"

`Saga.Core/Persistence/SagaUnitOfWorkTransaction.cs`:

```csharp
public async Task CommitAsync(CancellationToken ct = default)
{
    myFinished = true;
    myContext.MarkCommitted();
    await myStore.RemoveAsync(myContext.SagaId, ct);   // mọi bước đã commit rồi → chỉ xoá sổ
}
```

> **Đảo ngược trực giác:** ở đây `Commit` **không commit gì cả** — mọi thứ đã commit từ trước. Nó chỉ
> **xoá ledger**. Vì vậy: **ledger còn sót lại ⇔ saga CHƯA hoàn tất** — đó chính là tín hiệu cho
> recovery (§3.10). Rất gọn: không cần cột "IsCompleted".

Và `DisposeAsync` → nếu scope thoát mà **chưa** finish → **tự Rollback**. Không có đường nào lọt.

### 3.7 → 3.9. Đường hỏng: Rollback → Compensator → Handler

**`RollbackAsync`** (cùng file): load ledger → nếu `null` (chưa bước nào commit) → **không có gì để
undo**; ngược lại giao cho `SagaCompensator`.

**`SagaCompensator.CompensateAsync`** (`Saga.Core/Saga/SagaCompensator.cs`) — đọc theo đúng thứ tự:

```csharp
// (1) PIVOT-GUARD: đã qua điểm không quay đầu thì TUYỆT ĐỐI không undo — chỉ chốt sổ.
if (theSaga.Steps.Any(s => s.Kind == SagaStepKind.Pivot))
{
    await myStore.SetStatusAsync(theSaga.SagaId, SagaStatus.Committed, ct);
    await myStore.RemoveAsync(theSaga.SagaId, ct);
    return true;
}

// (2) Replay NGƯỢC: bước commit gần nhất undo trước.
for (var i = theSaga.Steps.Count - 1; i >= 0; i--) { ... TryCompensateStepAsync ... }

// (3) Undo mãi không được → park NeedsManualReview (KHÔNG im lặng bỏ qua).
```

`TryCompensateStepAsync`: tra handler qua **registry** theo `CompensationType` (`"RevertStock"`), retry
**5 lần** với backoff — đúng tinh thần saga *"roll back cho tới khi ăn"*.

**`MongoStockCompensationHandler`** (`MongoDB.Saga/Saga/MongoStockCompensationHandler.cs`) — idempotent
nhờ gói **cờ + dữ liệu** vào **một** transaction:

```csharp
if (aStep is null || aStep.Compensated) return;      // đã undo rồi → no-op (chạy 2 lần vẫn an toàn)
foreach (var aDelta in aDeltas) aProduct.StockQuantity += aDelta.Removed;  // cộng lại ĐÚNG phần đã trừ
aStep.Compensated = true;                            // cờ + kho commit CÙNG nhau ⇒ không double-revert
await aTransaction.CommitAsync(ct);
```

### 3.10. `SagaRecoveryService` — vá cửa sổ crash

`Saga.Core/Recovery/SagaRecoveryService.cs`, chạy **lúc khởi động**: đọc `GetUnfinishedAsync()` → giao
cho **đúng cái `SagaCompensator`** mà đường rollback live dùng.

> Dùng **chung** compensator là có chủ ý: saga bị crash được undo **y hệt** saga hỏng lúc đang chạy —
> một đường code, không lệch hành vi. Và vì pivot-guard nằm **trong** compensator, crash **sau** pivot
> sẽ được **finalize** (chốt thành công) chứ không bị undo nhầm.

### Sơ đồ trạng thái saga

```
Begin ──► Started ──(mọi bước OK)──► CommitAsync ──► ledger bị XOÁ  = thành công
             │
             ├─(hỏng TRƯỚC pivot)──► Compensating ──► Compensated ──► ledger XOÁ
             │                            └─(undo 5 lần vẫn fail)──► NeedsManualReview  (giữ lại)
             │
             └─(hỏng/crash SAU pivot)──► pivot-guard ──► Committed ──► ledger XOÁ (finalize, KHÔNG undo)
```

---

## 4. Outbox — phát event tin cậy mà không cần distributed transaction

### 4.1. Vấn đề outbox giải

"Đổi DB **và** publish event" là **2 hệ thống**. Không có 2PC thì luôn có cửa sổ chết:
- Publish **trước** commit → commit fail ⇒ đã bắn event **sai sự thật**.
- Commit **trước** publish → chết giữa chừng ⇒ **mất event**.

**Cách giải:** biến publish thành **một dòng trong CÙNG DB**, commit **chung transaction** với business
write ⇒ nguyên tử. Rồi một relay đọc bảng đó phát ra broker **sau**.

### 4.2. `OutboxRouter` — chính sách "route theo bản chất", đặt tại NGUỒN

`Saga.Core/Outbox/OutboxRouter.cs` — **một** nơi duy nhất quyết định event nào đi đâu:

```csharp
OrderConfirmedEvent e => new[]
{
    Fact(new OrderConfirmedIntegrationEvent(...), e.OrderId.ToString()),   // → Kafka  (sự thật)
    Command(new SendEmailCommand(...)),                                    // → RabbitMQ (việc cần làm)
    Command(new SendNotificationCommand(...))                              // → RabbitMQ
},

ProductStockChangedEvent e => e.NewStock == 0
    ? new[] { Fact(...), Command(new RestockAlertCommand(...)) }           // hết hàng → thêm việc
    : new[] { Fact(...) },                                                 // còn hàng → chỉ báo sự thật
```

- `Fact(...)` → `OutboxEntry(Kafka, topic, ...)` — **sự thật đã xảy ra**, nhiều consumer group đọc.
- `Command(...)` → `OutboxEntry(RabbitMq, queue, ...)` — **việc cụ thể**, per-message ack/retry/DLQ.

> Router **dùng chung** cho **cả** SQL lẫn Mongo writer ⇒ hai DB route **giống hệt nhau**, không lệch
> chính sách. Và **không** có chuyện đẩy message qua Kafka *chỉ để* sang RabbitMQ — quyết định ngay tại
> nguồn.

### 4.3. Hai `OutboxWriter` — chỉ **Stage**, cố tình **không** Save

`SqlServer.Saga/Outbox/OutboxWriter.cs` và `MongoDB.Saga/Outbox/MongoOutboxWriter.cs` — cùng khuôn:

```csharp
var aEntries = myRouter.Route(theAggregate.DomainEvents);   // domain event → outbox entries
foreach (var aEntry in aEntries) myContext.OutboxMessages.Add(new OutboxMessageRecord { ... });
theAggregate.ClearDomainEvents();                           // xoá để không enqueue 2 lần
return aEntries.Count;
```

> **Vì sao không `SaveChanges` ở đây?** Vì đó **chính là** cả cái hay của transactional outbox: writer
> chỉ **đặt hàng lên bàn**, `SaveChanges` **của repository** mới bưng đi — nên **business write và
> outbox rows vào chung MỘT commit**. Writer mà tự save là **phá vỡ** tính nguyên tử.

Mỗi store **một outbox riêng** (SQL giữ Order/Payment, Mongo giữ catalogue) — bắt buộc, vì outbox row
phải nằm **cùng DB** với business write thì mới chung transaction được.

### 4.4. `OutboxDispatcher` — relay, at-least-once

`Messaging/Outbox/OutboxDispatcher.cs`, `BackgroundService`, poll **2s**, batch **50**:

```csharp
var aStores = aScope.ServiceProvider.GetServices<IOutboxStore>();  // ← SỐ NHIỀU: drain cả 2 outbox
foreach (var aStore in aStores) await DrainStoreAsync(aStore, ct);
...
try   { await PublishAsync(aMessage, ct); await theStore.MarkPublishedAsync(aMessage.Id, ct); }
catch { await theStore.MarkFailedAsync(aMessage.Id, ex.Message, ct); }   // để nguyên → tick sau retry
```

Và điểm route cuối cùng:

```csharp
theMessage.Destination switch
{
    OutboxDestination.Kafka    => myEventBus.PublishAsync(route, partitionKey, type, payload, ct),
    OutboxDestination.RabbitMq => myCommandPublisher.PublishAsync(route, type, payload, ct),
};
```

**Hệ quả phải nhớ:** publish là **at-least-once** (commit rồi mới publish; publish xong mới mark →
crash giữa 2 bước ⇒ **gửi lại**). ⇒ **Consumer bắt buộc idempotent**. Broker chết cũng **không mất gì**:
row nằm im, tick sau gửi lại.

---

## 5. Kafka & RabbitMQ — luồng chạy, và **vì sao vào đúng Handler**

Dispatcher (§4.4) mới chỉ **đẩy message ra broker**. Từ đó tới lúc `AnalyticsProjectionHandler` hay
`EmailCommandWorker` **thật sự chạy** là một chặng riêng. Câu hỏi cốt lõi:

> Trên dây chỉ có **một chuỗi JSON** và **một cái tên dạng string**. Làm sao nó vào **đúng** handler,
> đúng kiểu, mà **không** dùng reflection?

Đáp án là **registry + closure sinh sẵn lúc khởi động** — cùng một ý tưởng, nhưng Kafka và RabbitMQ
dùng **hai loại "chìa khoá" khác nhau**. Đây là điểm phân biệt quan trọng nhất của cả mục này:

| | **Kafka** (fact) | **RabbitMQ** (command) |
|---|---|---|
| Chìa khoá định danh | **header `eventType`** | **chính cái QUEUE** |
| Vì sao cần chìa đó | 1 topic chở **NHIỀU** loại event (`orders.events` chở Placed/Confirmed/Shipped/Cancelled) ⇒ **phải** có header mới phân biệt được | **1 queue = 1 loại command** (`email.send` chỉ có `SendEmailCommand`) ⇒ queue **đã là** định danh, không cần header |
| Registry | `IntegrationEventRegistry` | `CommandRegistry` |
| Tra bằng | `Resolve(eventType)` → `myByName` | `ByQueue(queue)` → `myByQueue` |
| Resolve handler | `GetServices<...>` (**số nhiều**) | `GetRequiredService<...>` (**số ít**) |
| Số handler chạy | **N** — fan-out | **đúng 1** — một worker làm việc |

### 5.1. Mấu chốt: `Register<T>` "đóng băng" kiểu vào một closure

`Messaging/Routing/MessagingRegistry.cs` — đọc kỹ đoạn này, **toàn bộ phép màu nằm ở đây**:

```csharp
public void Register<TEvent>(string theTopic) where TEvent : IntegrationEvent
{
    var aName = typeof(TEvent).Name;                    // "OrderConfirmedIntegrationEvent" ← discriminator
    var aRegistration = new EventRegistration(aName, theTopic, typeof(TEvent),

        // ↓↓↓ CLOSURE: TEvent bị "đóng băng" vào đây LÚC ĐĂNG KÝ (compile-time generic)
        async (theProvider, thePayload, theCancellationToken) =>
        {
            var anEvent = JsonSerializer.Deserialize<TEvent>(thePayload, MessagingJson.Options) ?? throw ...;

            foreach (var aHandler in theProvider.GetServices<IIntegrationEventHandler<TEvent>>())
                await aHandler.HandleAsync(anEvent, theCancellationToken);   // fan-out: MỌI handler
        });

    myByName[aName] = aRegistration;        // string  → registration   (dùng lúc NHẬN)
    myByType[typeof(TEvent)] = aRegistration; // Type  → registration   (dùng lúc GỬI)
}
```

> **Vì sao không cần reflection?** Vì `Register<TEvent>` là **generic method** — lúc gọi
> `Register<OrderConfirmedIntegrationEvent>("orders.events")` ở startup, compiler đã sinh ra một closure
> mà trong đó `TEvent` **là** `OrderConfirmedIntegrationEvent` **cứng**. `Deserialize<TEvent>` và
> `GetServices<IIntegrationEventHandler<TEvent>>` đều **typed thật**. Lúc nhận message, ta **không** phải
> đổi string → `Type`, **không** `Activator.CreateInstance`, **không** `MakeGenericMethod`. Chỉ là **tra
> từ điển lấy ra closure rồi gọi**. Kiểu an toàn, hot path sạch.

Và **hai** từ điển là có chủ ý — chúng phục vụ **hai đầu** của đường ống:

```
myByType  : dùng lúc GỬI  → OutboxRouter.Fact() gọi Describe(theEvent.GetType())
                            → lấy Topic + EventType để ghi vào outbox row
myByName  : dùng lúc NHẬN → KafkaConsumer đọc header "eventType" (string)
                            → Resolve(name) → lấy đúng closure
```

⇒ **Cùng một `Register<T>`** đảm bảo bên gửi và bên nhận **không thể lệch nhau**: cái tên ghi lên header
chính là cái tên dùng để tra lúc nhận. Muốn thêm event mới → thêm **một dòng** `Register<T>` trong
`BuildEventRegistry()`, cả 2 đầu tự khớp.

### 5.2. Kafka — luồng đầy đủ, từng chặng

**Đăng ký lúc startup** (`Messaging/DependencyInjection.cs`):

```csharp
aRegistry.Register<OrderPlacedIntegrationEvent>(MessagingTopics.OrderEvents);      // "orders.events"
aRegistry.Register<OrderConfirmedIntegrationEvent>(MessagingTopics.OrderEvents);   // ← cùng topic!
aRegistry.Register<OrderShippedIntegrationEvent>(MessagingTopics.OrderEvents);     // ← cùng topic!
aRegistry.Register<OrderCancelledIntegrationEvent>(MessagingTopics.OrderEvents);   // ← cùng topic!
aRegistry.Register<PaymentCompletedIntegrationEvent>(MessagingTopics.PaymentEvents);   // "payments.events"
aRegistry.Register<ProductStockChangedIntegrationEvent>(MessagingTopics.ProductEvents);// "products.events"

theServices.AddScoped<IIntegrationEventHandler<OrderConfirmedIntegrationEvent>, AnalyticsProjectionHandler>();
// ... 5 dòng tương tự: handler ĐĂNG KÝ THEO TỪNG KIỂU EVENT
```

**Nhìn kỹ: 4 event khác nhau dùng CHUNG topic `orders.events`.** Đây chính là lý do Kafka **bắt buộc**
phải có header `eventType` — nếu không, consumer đọc được JSON nhưng **không biết** nó là `Placed` hay
`Cancelled` để deserialize.

**GỬI** — `Kafka/KafkaEventBus.cs`:

```csharp
var aMessage = new Message<string, string>
{
    Key     = thePartitionKey,   // = OrderId → mọi event CÙNG order vào CÙNG partition ⇒ giữ THỨ TỰ
    Value   = thePayloadJson,
    Headers = new Headers { { EventTypeHeader, Encoding.UTF8.GetBytes(theEventType) } }  // "eventType"
};
await myProducer.ProduceAsync(theTopic, aMessage, ct);
```

Producer cấu hình `Acks.All` + `EnableIdempotence = true` ⇒ retry của producer **không** sinh bản sao.

**NHẬN** — `Kafka/KafkaConsumerHostedService.cs`, đây là 5 bước dẫn tới đúng handler:

```csharp
aConsumer.Subscribe(myRegistry.Topics);          // ① sub MỌI topic registry biết (distinct)
var aResult = aConsumer.Consume(ct);             // ② lấy 1 message

var aEventType = ReadEventType(theResult.Message.Headers);   // ③ đọc header → "OrderConfirmedIntegrationEvent"
if (aEventType is null) { ...skip... }

var aRegistration = myRegistry.Resolve(aEventType);          // ④ string → closure (tra myByName)
if (aRegistration is null) { ...skip, KHÔNG fail... }        //    lạ → bỏ qua (service khác sở hữu)

await using var aScope = myScopeFactory.CreateAsyncScope();  // ⑤ scope MỚI cho mỗi message
await aRegistration.Dispatch(aScope.ServiceProvider, theResult.Message.Value, ct);
//                  └─ closure: Deserialize<OrderConfirmedIntegrationEvent>
//                             → GetServices<IIntegrationEventHandler<OrderConfirmedIntegrationEvent>>()
//                             → [AnalyticsProjectionHandler] → HandleAsync(...)

aConsumer.Commit(aResult);                       // ⑥ commit offset CHỈ SAU KHI handler xong
```

Ba điểm đáng nhớ:
- **④ là chỗ "vào đúng handler"**: string trên header → closure → DI resolve typed → handler. Không có
  bước nào đoán mò.
- **Không biết event ⇒ bỏ qua, không lỗi.** Đúng tinh thần backbone: topic có thể chở event mà deployment
  này không quan tâm (service khác đọc).
- **⑥ `EnableAutoCommit = false` + commit sau handler ⇒ at-least-once.** Chết giữa handler và commit ⇒
  đọc lại ⇒ **handler phải idempotent**.

### 5.3. RabbitMQ — luồng đầy đủ, từng chặng

**Đăng ký lúc startup:**

```csharp
aRegistry.Register<SendEmailCommand>(MessagingQueues.SendEmail);            // "email.send"
aRegistry.Register<IssueInvoiceCommand>(MessagingQueues.IssueInvoice);      // "invoice.issue"
aRegistry.Register<SendNotificationCommand>(MessagingQueues.SendNotification); // "notification.send"
aRegistry.Register<RestockAlertCommand>(MessagingQueues.RestockAlert);      // "restock.alert"

theServices.AddScoped<ICommandHandler<SendEmailCommand>, EmailCommandWorker>();   // ĐÚNG 1 handler/command
```

**Mỗi command một queue riêng** — khác hẳn Kafka (4 event chung 1 topic).

**GỬI** — `RabbitMq/RabbitMqCommandPublisher.cs`:

```csharp
await aChannel.ExchangeDeclareAsync(MessagingQueues.Exchange, ExchangeType.Direct, durable: true, ...);
var aProperties = new BasicProperties { Persistent = true, ContentType = "application/json", Type = theCommandType };
await aChannel.BasicPublishAsync(MessagingQueues.Exchange, routingKey: theQueue, ..., body: aBody, ...);
//                               exchange "selling.commands"    ↑ routingKey = TÊN QUEUE
```

> **Direct exchange + routingKey = tên queue** là cách message vào đúng queue. Consumer đã bind
> `QueueBindAsync(queue, Exchange, routingKey: queue)` ⇒ Rabbit khớp routingKey với binding key ⇒ thả
> vào **đúng một** queue.
>
> ⚠️ Lưu ý dễ hiểu nhầm: `aProperties.Type = theCommandType` **KHÔNG** dùng để dispatch. Nó chỉ để
> **chẩn đoán/đọc log**. Việc định tuyến do **queue** lo hoàn toàn.

**NHẬN** — `RabbitMq/RabbitMqConsumerHostedService.cs`:

```csharp
foreach (var aQueue in myRegistry.Queues)             // ① MỖI queue một channel + một consumer RIÊNG
{
    var aChannel = await aConnection.CreateChannelAsync(ct);
    await DeclareQueueTopologyAsync(aChannel, aQueue, ct);          // ② khai queue + DLQ + binding
    await aChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, ct);  // ③ 1 message/lúc
    var aConsumer = new AsyncEventingBasicConsumer(aChannel);
    aConsumer.ReceivedAsync += (s, e) => OnMessageAsync(aChannel, aQueue, e, ct);  // ← aQueue "đóng băng" vào closure
    await aChannel.BasicConsumeAsync(aQueue, autoAck: false, aConsumer, ct);
}
```

> **Đây là lý do RabbitMQ không cần header.** Vì mỗi consumer được **treo riêng lên một queue**, nên khi
> `OnMessageAsync` chạy, tham số `theQueue` **đã cho biết** đây là message gì — nó được bake vào lambda
> lúc dựng consumer. **Ngữ cảnh thay cho chìa khoá.**

```csharp
private async Task OnMessageAsync(IChannel theChannel, string theQueue, BasicDeliverEventArgs theArgs, ...)
{
    var aRegistration = myRegistry.ByQueue(theQueue);   // ④ queue → closure (tra myByQueue)
    var aPayload = Encoding.UTF8.GetString(theArgs.Body.Span);

    for (var anAttempt = 1; anAttempt <= mySettings.MaxRetries; anAttempt++)
    {
        try
        {
            await using var aScope = myScopeFactory.CreateAsyncScope();   // ⑤ scope mới MỖI LẦN THỬ
            await aRegistration.Dispatch(aScope.ServiceProvider, aPayload, ct);
            //                  └─ Deserialize<SendEmailCommand>
            //                     → GetRequiredService<ICommandHandler<SendEmailCommand>>()  ← SỐ ÍT
            //                     → EmailCommandWorker.HandleAsync(...)
            await theChannel.BasicAckAsync(theArgs.DeliveryTag, multiple: false, ct);  // ⑥ ACK sau khi xong
            return;
        }
        catch when (anAttempt < mySettings.MaxRetries) { ...delay 300ms*n, thử lại... }   // ⑦ retry in-process
        catch { await theChannel.BasicNackAsync(..., requeue: false, ct); return; }       // ⑧ hết cửa → DLQ
    }
}
```

**Ba tầng an toàn của RabbitMQ ở đây (đừng nhầm lẫn với Polly):**
- **⑦ retry in-process** (`MaxRetries`, mặc định 3) — của **consumer host**.
- **⑧ Nack(requeue:false)** → Rabbit đẩy sang **DLX** → `email.send.dlq` để mổ xẻ sau. **Không mất**,
  cũng **không** kẹt vòng lặp vô hạn.
- Bên **trong** handler còn một tầng nữa: `CommandWorkers` gọi gateway qua Polly
  **bulkhead → retry → breaker → timeout** ([polly-bulkhead.md](polly-bulkhead.md)). Tức là một message
  hỏng đi qua: *Polly retry (gọi gateway)* → *consumer retry (cả handler)* → *DLQ*.

### 5.4. Ráp lại: từ 1 outbox row tới đúng 1 dòng code

```
OUTBOX ROW (Mongo/SQL)                       BROKER                      TỚI ĐÚNG HANDLER
─────────────────────────────────────────────────────────────────────────────────────────
Destination = Kafka                    ┌── topic "orders.events" ──┐
Route       = "orders.events"          │  Key    = <OrderId>       │  header eventType
MessageType = "OrderConfirmed…Event" ──┤  Header = eventType:      ├─► Resolve("OrderConfirmed…")
Payload     = {…}                      │           OrderConfirmed… │   → closure<OrderConfirmed…>
PartitionKey= <OrderId>                └── Value  = {…} ───────────┘   → GetServices<IHandler<T>>()
                                                                       → AnalyticsProjectionHandler ✔
                                                                         (N handler đều chạy)

Destination = RabbitMq                 ┌ exchange "selling.commands" (direct)
Route       = "email.send"             │  routingKey = "email.send"
MessageType = "SendEmailCommand"     ──┤     └─► binding → queue "email.send"   ← QUEUE là định danh
Payload     = {…}                      │            └─► consumer treo riêng queue này
PartitionKey= ""  (không dùng)         └─────────────► ByQueue("email.send")
                                                       → closure<SendEmailCommand>
                                                       → GetRequiredService<ICommandHandler<T>>()
                                                       → EmailCommandWorker ✔  (ĐÚNG 1)
                                                          └─► Polly bulkhead→retry→breaker→timeout
                                                              └─► IEmailSender (SMTP thật / logging)
```

### 5.5. Tóm gọn "vì sao vào đúng handler"

1. **Lúc startup**, `Register<T>(route)` sinh **closure đã biết `T`** + nhét vào **từ điển** theo cả
   `string` lẫn `Type`. Không reflection về sau.
2. **Lúc gửi**, `OutboxRouter` tra `myByType`/`myQueueByType` → ghi **route + tên** vào outbox row ⇒
   bên gửi dùng **chính** cái tên bên nhận sẽ tra.
3. **Lúc nhận**, lấy chìa khoá — Kafka: **header `eventType`**; Rabbit: **tên queue đang treo** — tra từ
   điển → **ra closure**.
4. **Closure** deserialize đúng kiểu rồi hỏi **DI**: `GetServices<IIntegrationEventHandler<T>>()` (N,
   fan-out) hoặc `GetRequiredService<ICommandHandler<T>>()` (1, work queue).
5. **DI** trả về đúng class đã đăng ký ở `RegisterEventHandlers` / `RegisterCommandWorkers`.

⇒ Chuỗi khép kín: **`Register<T>` + `AddScoped<IHandler<T>, XHandler>()`** là **hai** dòng duy nhất
quyết định "message này chạy vào class nào". Thiếu dòng thứ hai ⇒ Kafka **im lặng bỏ qua** (`GetServices`
trả list rỗng), Rabbit **ném lỗi** (`GetRequiredService`) → retry → DLQ.

---

## 6. Ba pattern khớp vào nhau ở đâu (phần đáng giá nhất)

### 6.1. Giao điểm ① — Outbox row nằm CÙNG transaction với saga step

Đây là mấu chốt làm cả hệ thống đứng vững:

```
Mongo transaction (§3.4):  [ trừ kho ]  +  [ ledger: bước + undo info ]  +  [ outbox rows ]   → 1 commit
SQL   transaction (§3.5):  [ đổi order ]                                 +  [ outbox rows ]   → 1 commit
```

Nếu outbox tách khỏi transaction của step → có thể "kho đã trừ mà event mất", hoặc "event đã bắn mà kho
chưa trừ". Gộp chung ⇒ **mỗi bước saga là một đơn vị nguyên tử tự mô tả**: *đã làm gì*, *undo thế nào*,
*cần báo cho ai*.

### 6.2. Giao điểm ② — Pivot + Outbox = vì sao `RetryableForward` an toàn

Qua pivot rồi thì saga **không undo**, chỉ **tiến tới**. Nhưng "tiến tới" (gửi email, xuất hoá đơn) là
gọi **hệ thống ngoài** — dễ hỏng. **Outbox làm cho lời hứa đó khả thi**: các command **đã** được commit
nguyên tử cùng pivot, nên dù app **chết ngay sau** pivot, `OutboxDispatcher` lúc khởi động lại vẫn tìm
thấy row và **đẩy tiếp**. Việc **không thể mất**.

⇒ Bước `PublishOrderEvents` mới chỉ cần đánh dấu `RetryableForward` "để audit" — vì **outbox**, chứ
không phải saga, mới là thứ **bảo đảm** nó xảy ra.

### 6.3. Giao điểm ③ — CQRS giữ cho Domain sạch để ② khả thi

Vì handler **mỏng** và business rule nằm trong **Domain service + aggregate**, mà **domain event** lại
do **aggregate** raise (`aOrder.Confirm()`), nên outbox có thể "nhặt" event **ở tầng repository** —
đúng chỗ transaction đang mở. Nếu logic nằm rải ở controller/handler, sẽ **không có** aggregate nào
mang event để nhặt, và outbox buộc phải gọi thủ công → dễ quên → mất event.

### 6.4. Bảng tổng: ai lo gì

| | CQRS | Saga | Outbox |
|---|---|---|---|
| Bài toán | request đi đường nào; tách read/write | 2 DB nhất quán không 2PC | đổi DB + phát event, không mất |
| Tầng | Application | Infrastructure | Infrastructure |
| Domain biết? | biết **port** (`IOrderWriteService`) | ❌ chỉ thấy `IUnitOfWork` | ❌ chỉ raise domain event |
| Vũ khí | MediatR, behavior, command/query | commit-per-step, ledger, compensation, pivot | row cùng transaction + relay |
| Hỏng thì | ném exception → middleware map status | undo ngược / finalize sau pivot | không mất; retry tick sau |
| File lõi | `ConfirmOrderCommand.cs` | `SagaUnitOfWork*.cs`, `SagaCompensator.cs` | `OutboxRouter.cs`, `OutboxDispatcher.cs` |

---

## 7. Luồng đầy đủ một request (ráp lại)

```
POST /api/orders/{id}/confirm
│
├─[CQRS] OrdersController → ISender.Send(ConfirmOrderCommand)
│        └─ ValidationBehavior (shape) → ConfirmOrderCommandHandler → IOrderWriteService.ConfirmAsync
│
├─[DOMAIN] OrderWriteService.ConfirmAsync
│        ├─ LoadOrder (SQL) + LoadProducts (Mongo)   ← qua outbound ports
│        ├─ EnsureEnoughStock                        ← RULE
│        ├─ aOrder.Confirm()                         → raise OrderConfirmedEvent      (in-memory)
│        ├─ product.DecreaseStock()                  → raise ProductStockChangedEvent (in-memory)
│        └─ PersistInTransactionAsync → IUnitOfWork.BeginTransactionAsync
│
├─[SAGA] SagaUnitOfWork.Begin → SagaContext.Begin("OrderWrite") + store.StartAsync
│        │                                                        → ledger: Started
│        ├─ BƯỚC 1  MongoProductWriteRepository.UpdateRangeAsync   (Compensatable)
│        │    └─ Mongo txn: [kho−] + [ledger: DecreaseStock + deltas] + [outbox: fact(+restock?)] ✔ commit
│        │
│        ├─ BƯỚC 2  SqlOrderWriteRepository.UpdateAsync            (Pivot)
│        │    ├─ SQL txn: [order=Confirmed] + [outbox: fact + email + push] ✔ commit
│        │    ├─ ledger += Pivot            ← ĐIỂM KHÔNG QUAY ĐẦU
│        │    └─ ledger += RetryableForward (audit)
│        │
│        └─ CommitAsync → ledger bị XOÁ  = saga xong
│
└─► 200 OK (ApiResponse<OrderResponse>)          ← người dùng KHÔNG phải chờ email/Kafka

   ┄┄┄ bất đồng bộ, 2s/tick ┄┄┄
[OUTBOX] OutboxDispatcher → drain CẢ HAI store (SQL + Mongo)
        ├─ Kafka    ← facts    : OrderConfirmed, ProductStockChanged   → AnalyticsProjectionHandler…
        └─ RabbitMQ ← commands : SendEmail, SendNotification, RestockAlert
                                  └─ CommandWorkers → bulkhead→retry→breaker→timeout → gateway

   ┄┄┄ nếu BƯỚC 2 ném lỗi ┄┄┄
catch → aTransaction.RollbackAsync → store.LoadAsync → SagaCompensator
        ├─ có Pivot?  → KHÔNG có (mới bước 1) → đi tiếp
        └─ replay ngược → MongoStockCompensationHandler → kho cộng lại (idempotent) → ledger XOÁ
```

---

## 8. Bảng tra file — đọc theo đúng thứ tự này

| # | File | Pattern | Đọc để hiểu |
|---|---|---|---|
| 1 | `API/Controllers/OrdersController.cs` | CQRS | controller mỏng |
| 2 | `Application/DependencyInjection.cs` | CQRS | MediatR + behavior |
| 3 | `Application/Orders/Commands/ConfirmOrderCommand.cs` | CQRS | command+handler 1 file |
| 4 | `Domain/Interfaces/Inbound/IOrderWriteService.cs` | CQRS | inbound port |
| 5 | **`Domain/Services/OrderWriteService.cs`** | Domain | rule + **seam `IUnitOfWork`** |
| 6 | `Saga.Core/Persistence/SagaUnitOfWork.cs` | Saga | mở saga thay vì transaction |
| 7 | `Saga.Core/Saga/SagaContext.cs` | Saga | scoped, `IsActive` |
| 8 | `Saga.Core/Saga/SagaStep.cs` | Saga | 3 Kind |
| 9 | **`MongoDB.Saga/Repositories/Write/MongoProductWriteRepository.cs`** | Saga+Outbox | **giao điểm ①** |
| 10 | `MongoDB.Saga/Saga/MongoSagaStore.cs` | Saga | Stage vs Enroll |
| 11 | **`SqlServer.Saga/Repositories/Write/SqlOrderWriteRepository.cs`** | Saga+Outbox | **pivot** |
| 12 | `Saga.Core/Persistence/SagaUnitOfWorkTransaction.cs` | Saga | commit=xoá sổ; dispose=rollback |
| 13 | `Saga.Core/Saga/SagaCompensator.cs` | Saga | pivot-guard, replay ngược |
| 14 | `MongoDB.Saga/Saga/MongoStockCompensationHandler.cs` | Saga | undo idempotent |
| 15 | `Saga.Core/Recovery/SagaRecoveryService.cs` | Saga | crash recovery |
| 16 | **`Saga.Core/Outbox/OutboxRouter.cs`** | Outbox | route-by-nature |
| 17 | `SqlServer.Saga/Outbox/OutboxWriter.cs` + `MongoDB.Saga/Outbox/MongoOutboxWriter.cs` | Outbox | Stage, không Save |
| 18 | **`Messaging/Outbox/OutboxDispatcher.cs`** | Outbox | relay, at-least-once |
| 19 | **`Messaging/Routing/MessagingRegistry.cs`** | Kafka+Rabbit | **closure `Register<T>`** — mấu chốt "vào đúng handler" |
| 20 | `Messaging/DependencyInjection.cs` | Kafka+Rabbit | `BuildEventRegistry` / `BuildCommandRegistry` + đăng ký handler |
| 21 | `Messaging/Kafka/KafkaEventBus.cs` | Kafka | gửi: header `eventType`, key = aggregate id |
| 22 | `Messaging/Kafka/KafkaConsumerHostedService.cs` | Kafka | nhận: header → Resolve → fan-out, commit sau handler |
| 23 | `Messaging/RabbitMq/RabbitMqCommandPublisher.cs` | Rabbit | gửi: direct exchange, routingKey = tên queue |
| 24 | `Messaging/RabbitMq/RabbitMqConsumerHostedService.cs` | Rabbit | nhận: 1 consumer/queue → ByQueue → 1 worker, retry → DLQ |

---

## 9. Điểm cần lưu ý / hạn chế hiện tại

1. **Compensation không phát fact bù** *(đã biết — hiện KHÔNG gây hại, cố ý giữ nguyên)*.
   `MongoStockCompensationHandler` sửa **thẳng document** (không đi qua aggregate `Product`), nên
   **không** raise `ProductStockChangedEvent` mới ⇒ **không** có outbox row bù. Trong khi đó fact "kho đã
   giảm" của bước 1 **đã commit** và **vẫn sẽ** được dispatcher publish lên Kafka.

   **Vì sao hiện tại không sao:** consumer Kafka duy nhất của fact này là
   `AnalyticsProjectionHandler.RecordStockMovement` → `Bump("stock.movements", 1, ...)` — nó chỉ **đếm số
   lần có biến động kho**, **không** cộng dồn ra một con số tồn kho. Mà biến động đó **đã thật sự xảy
   ra** (rồi mới bị revert), nên đếm 1 không hề sai. Không có state nào bị lệch.

   **Triệu chứng cụ thể duy nhất:** nếu kho tụt về **đúng 0** rồi saga mới rollback, `OutboxRouter` đã
   kèm một `RestockAlertCommand` ⇒ cảnh báo "hết hàng" **bắn oan**. Đó là cảnh báo advisory, không phải
   giao dịch, và chỉ xảy ra trên **đường lỗi** (pivot SQL fail sau khi bước Mongo đã commit).

   **Nó thành bug thật khi nào:** ngay khi có consumer **suy ra state** từ luồng event (vd một analytics
   service thật cộng dồn tồn kho, hay một read-model khác) — mà đó chính là kịch bản Kafka fan-out được
   thiết kế để phục vụ. Lúc đó phải cho compensation stage một fact bù **trong cùng transaction** với
   việc hoàn kho (và cân nhắc cả `RestockAlert` bắn oan).
2. **`IsActive` là cờ mờ.** Repository dựa vào `mySagaContext.IsActive` để biết có enroll hay không. Chạy
   `UpdateRangeAsync` ngoài saga ⇒ commit thẳng, **không** undo được. Đúng ý đồ, nhưng phải nhớ.
3. **Ledger đặt ở Mongo** ⇒ Mongo chết thì saga **không mở được**. Đánh đổi đã biết, xem
   [saga-hybrid.md](saga-hybrid.md).
4. **At-least-once** ⇒ mọi consumer **phải** idempotent. Không có ngoại lệ.

---

## 10. Tóm tắt 30 giây

- Chỉ **Confirm** mới gộp đủ 3 pattern (Place chỉ chạm SQL ⇒ không saga).
- **CQRS**: controller & handler **mỏng**; rule nằm ở **Domain service + aggregate**; Domain **không ref
  MediatR**.
- **Seam quyết định**: Domain chỉ thấy `IUnitOfWork`; infra tráo nó thành **saga** ⇒ đổi provider không
  đụng Domain.
- **Saga**: **commit-per-step** (không giữ transaction mở) + undo info ghi ledger **ngay**; `Pivot` là
  điểm không quay đầu (**pivot-guard** chặn undo nhầm); `Commit` = **xoá ledger**, nên *ledger còn sót =
  chưa xong* ⇒ recovery biết việc phải làm.
- **Outbox**: event ghi **cùng transaction** với business write ⇒ nguyên tử, không cần 2PC;
  `OutboxRouter` route **tại nguồn** (fact→Kafka, command→RabbitMQ); dispatcher relay **at-least-once**.
- **Vào đúng handler** (§5): `Register<T>` lúc startup sinh **closure đã đóng băng `T`** vào từ điển ⇒
  không reflection. Chìa khoá tra khác nhau: **Kafka = header `eventType`** (vì 1 topic chở nhiều loại
  event) → `GetServices` → **N handler**; **Rabbit = chính cái queue** (vì 1 queue = 1 command, consumer
  treo riêng từng queue) → `GetRequiredService` → **đúng 1 worker**. Hai dòng quyết định tất cả:
  `Register<T>(route)` + `AddScoped<IHandler<T>, XHandler>()`.
- **Giao điểm vàng**: *một* commit gói **[business write] + [saga step & undo] + [outbox rows]** → mỗi
  bước saga là đơn vị nguyên tử tự mô tả; và outbox chính là thứ khiến lời hứa "qua pivot chỉ tiến tới"
  **thực sự** giữ được.

Liên quan: [saga-hybrid.md](saga-hybrid.md), [outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md),
[outbox-kafka-rabbitmq-walkthrough.md](outbox-kafka-rabbitmq-walkthrough.md),
[saga-files-reference.md](saga-files-reference.md), [polly-bulkhead.md](polly-bulkhead.md),
[ARCHITECTURE.md](ARCHITECTURE.md).
