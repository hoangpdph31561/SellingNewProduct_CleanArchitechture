# Saga — Tham chiếu file & ví dụ Order

Tài liệu liệt kê **toàn bộ file liên quan đến Saga** (provider `DatabaseProvider=Hybrid`), mỗi file
dùng để làm gì, các **method quan trọng**, và một **ví dụ Order** đi từng bước cho cả 3 kịch bản:
thành công, thất bại (rollback in-process), và crash (recovery).

> Bối cảnh: SQL Server giữ `Order/OrderDetail/Payment`; MongoDB giữ `Product/Category/Customer/Employee/User`.
> Một thao tác ghi xuống cả 2 DB không thể là transaction ACID chung → dùng **Saga**: mỗi DB một
> transaction local thật + bù trừ (compensation) cho khe cross-DB + recovery khi crash.

---

## 1. `SellingNewProduct.Infrastructure.Saga.Core` — bộ máy saga (không phụ thuộc DB nào)

### `Saga/SagaContext.cs`
Trạng thái **của một saga** trong 1 request (DI `scoped` → mọi thành phần trong cùng scope dùng chung).
- `Begin()` — mở saga mới: sinh `SagaId`, xoá danh sách compensation, đặt `IsActive=true`.
- `IsActive` — repo saga-aware nhìn cờ này để biết có đang trong saga không.
- `RegisterCompensation(tên, hàm)` — nhét một **hàm hoàn tác** vào danh sách (chỉ khi đang active).
- `Compensations`, `StepNames` — danh sách hoàn tác (chạy ngược) + tên các bước (ghi vào log).
- `MarkCommitted / MarkCompensating / MarkCompensated / MarkFailed` — chuyển trạng thái để ghi log.

### `Saga/SagaStatus.cs`
Enum vòng đời saga: `NotStarted → Started → Committed` hoặc `→ Compensating → Compensated / Failed`.

### `Saga/ISagaParticipant.cs`
Đại diện **một DB tham gia saga**. Kernel điều phối qua interface này, không biết DB cụ thể.
- `IsPivot` — `true` cho store commit **cuối** (ở đây là SQL). Store non-pivot commit **trước**.
- `BeginAsync()` — mở transaction local của store.
- `CommitAsync()` — commit transaction local.
- `RollbackAsync()` — huỷ transaction local (native).

### `Saga/ISagaLog.cs` + `Saga/NullSagaLog.cs`
Nhật ký saga (audit "track commit").
- `StartAsync(sagaId)` — ghi dòng `Started`.
- `CompleteAsync(sagaId, status, steps)` — ghi kết quả cuối (`Committed/Compensated/Failed`).
- `NullSagaLog` — bản rỗng mặc định (khi không có log bền vững); SQL provider thay bằng bản thật.

### `Persistence/SagaUnitOfWork.cs`
Bản cài đặt `IUnitOfWork` của Domain **dạng saga**. Domain gọi y như cũ, không biết bên dưới là saga.
- `BeginTransactionAsync()` — **Begin**: gọi `SagaContext.Begin()`, mở transaction cho **mọi**
  participant (SQL + Mongo), ghi log `Started`, trả về `SagaUnitOfWorkTransaction`.

### `Persistence/SagaUnitOfWorkTransaction.cs`
Handle transaction trả về cho Domain (`IUnitOfWorkTransaction`).
- `CommitAsync()` — **Commit**: commit các participant **non-pivot trước** (Mongo), rồi **pivot sau**
  (SQL). Ghi log `Committed`. Nếu pivot commit ném lỗi → để exception nổi lên cho caller.
- `RollbackAsync()` — **Rollback**: huỷ native mọi transaction còn pending, rồi **chạy ngược các
  compensation** để hoàn tác store đã commit (Mongo). Ghi log `Compensated` (hoặc `Failed`).
- `DisposeAsync()` — **End**: nếu chưa Commit/Rollback thì tự Rollback (qua `await using`).

### `CrossDb/CrossDbContracts.cs`
Hai "port lá" để **đọc xuyên DB mà không tạo vòng phụ thuộc** (mỗi bản cài chỉ phụ thuộc context DB của
chính nó):
- `ICrossDbDirectory` — Mongo cấp **tên** (customer/employee/product/category) cho read model bên SQL.
- `ICrossDbOrderStats` — SQL cấp **thống kê order** (đếm theo nhân viên, tổng theo khách) cho read model
  bên Mongo.

### `Recovery/ISagaEffectStore.cs`
Kho **hiệu ứng bền vững** mà saga đã ghi xuống Mongo (delta kho). Dùng để hoàn tác.
- `GetPendingSagaIdsAsync()` — các saga có effect chưa revert (ứng viên recovery).
- `RevertAsync(sagaId)` — hoàn tác delta kho + đánh dấu đã revert. **Idempotent**.
- `RemoveAsync(sagaId)` — xoá effect khi saga đã xác nhận nhất quán.

### `Recovery/ISagaCommitStore.cs`
Kho **bằng chứng pivot (SQL) đã commit**.
- `ExistsAsync(sagaId)` — có marker không (⇔ SQL đã commit cho saga này).
- `RemoveAsync(sagaId)` — dọn marker.

### `Recovery/SagaRecoveryService.cs`
`BackgroundService` chạy **một lần lúc khởi động** (chỉ provider Hybrid) để khép khe crash-giữa-2-commit.
- `ExecuteAsync()` — tạo scope, lấy 2 store + log, duyệt `GetPendingSagaIdsAsync()`.
- `ReconcileAsync(sagaId)` — đối chiếu:
  - SQL commit marker **có** → cả 2 đã commit → xoá 2 marker + log `Committed`.
  - SQL commit marker **không** → Mongo commit mà pivot không → `RevertAsync` (trả kho) + log `Compensated`.

### `DependencyInjection.cs`
`AddSagaCore()` — đăng ký `SagaContext`, `IUnitOfWork→SagaUnitOfWork`, `NullSagaLog` mặc định, và
`SagaRecoveryService`.

---

## 1b. `SagaId` — sợi dây buộc 2 DB (qua `SagaContext`)

Cả hai DB **không** bị ép vào một transaction ACID chung; thay vào đó chúng cùng mang **một `SagaId`**.
Vật giữ id đó là `SagaContext`, đăng ký **`scoped`** nên mọi thành phần trong cùng request dùng **chung
một instance** → chung một id.

```csharp
// Saga.Core/DependencyInjection.cs
theServices.AddScoped<SagaContext>();              // 1 instance / request
theServices.AddScoped<IUnitOfWork, SagaUnitOfWork>();
```

**SagaId sinh ra một lần khi Begin:**

```csharp
// SagaUnitOfWork.BeginTransactionAsync
myContext.Begin();                                  // SagaContext.Begin(): SagaId = Guid.NewGuid()
foreach (var p in myParticipants) await p.BeginAsync(ct);
await myLog.StartAsync(myContext.SagaId, ct);       // log Started mang SagaId
```

**Phía Mongo đóng dấu SagaId** (cùng `SagaContext` được inject vào repo):

```csharp
// MongoProductWriteRepository (ctor: MongoAppDbContext, SagaContext, MongoSagaEffectStore)
myEffectStore.Record(mySagaContext.SagaId, aStockRemoved);          // SagaEffect.Id = SagaId
var aSagaId = mySagaContext.SagaId;
mySagaContext.RegisterCompensation($"RevertStock:{aSagaId}",
    ct => myEffectStore.RevertAsync(aSagaId, ct));
// → MongoSagaEffectStore.Record: new SagaEffectDocument { Id = theSagaId, ... }
```

**Phía SQL đóng dấu CÙNG SagaId** (cùng `SagaContext` được inject vào participant):

```csharp
// SqlSagaParticipant (ctor: AppDbContext, SagaContext) — trong CommitAsync
myAppDbContext.SagaCommits.Add(new SagaCommitRecord
{
    SagaId = mySagaContext.SagaId,                  // ← ĐÚNG cùng id với SagaEffect bên Mongo
    CommittedUtc = DateTime.UtcNow
});
await myAppDbContext.SaveChangesAsync(ct);          // ghi trong transaction SQL
await myTransaction.CommitAsync(ct);
```

⇒ Sau một saga, Mongo có `saga_effects._id = SagaId` và SQL có `SagaCommits.SagaId = SagaId` — **cùng một
GUID**. Đó là khoá để ghép hai phía.

**Lưu ý sống còn:** `SagaContext` là `scoped` → **biến mất khi request kết thúc / crash**. Nên `SagaId`
chỉ "sống sót" nhờ đã được ghi xuống các bản ghi **bền vững**. Recovery vì thế đọc id từ DB chứ không
từ `SagaContext`:

```csharp
// SagaRecoveryService
var aPending = await aEffectStore.GetPendingSagaIdsAsync(ct);   // SagaId lấy từ saga_effects (Mongo)
foreach (var aSagaId in aPending)
    if (await aCommitStore.ExistsAsync(aSagaId, ct)) { /* cùng id có trong SagaCommits (SQL)? */ }
```

Tóm lại: **`SagaContext` giữ `SagaId` lúc đang chạy; còn để xuyên qua 2 DB và sống sót qua crash, `SagaId`
được đóng dấu xuống `saga_effects` (Mongo) và `SagaCommits` (SQL)** — hai mặt của cùng một id.

---

## 2. `SellingNewProduct.Infrastructure.SqlServer.Saga` — phía SQL (Order/Payment + pivot)

- **`Persistence/AppDbContext.cs`** — context nghiệp vụ: `Orders`, `OrderDetails`, `Payments`, và
  `SagaCommits` (bảng marker, nằm CHUNG context để atomic với commit nghiệp vụ).
- **`Persistence/SagaLogDbContext.cs`** — context **riêng** chỉ chứa `SagaTransactions` (nhật ký). Tách
  ra để log `Compensated` không bị cuốn theo khi rollback nghiệp vụ.
- **`Models/*Record.cs`** — model bảng: `OrderRecord`, `OrderDetailRecord`, `PaymentRecord`,
  `SagaTransactionRecord` (nhật ký), `SagaCommitRecord` (marker pivot: `SagaId`, `CommittedUtc`).
- **`Mapping/OrderMapper.cs`, `PaymentMapper.cs`** — chuyển Domain ⇄ Record (`ToRecord/MapInto/ToDomain`).
- **`Repositories/Write/SqlOrderWriteRepository.cs`, `SqlPaymentWriteRepository.cs`** — ghi Order/Payment.
  Khi saga active, `SaveChanges` của chúng nằm trong transaction SQL nên **pending** tới khi commit.
- **`Repositories/Read/Sql*ReadRepository.cs`** — đọc Order/Payment/Report; enrich tên từ Mongo qua
  `ICrossDbDirectory` (vì không JOIN xuyên DB được).
- **`Saga/SqlSagaParticipant.cs`** — participant **pivot** (`IsPivot=true`).
  - `BeginAsync()` — `AppDbContext.Database.BeginTransactionAsync()`.
  - `CommitAsync()` — **ghi `SagaCommitRecord` trong cùng transaction** rồi commit (marker durable ⇔ pivot commit).
  - `RollbackAsync()` — rollback transaction SQL.
- **`Saga/SqlSagaLog.cs`** — bản thật của `ISagaLog`, ghi `SagaTransactions` bằng `SagaLogDbContext`.
- **`Saga/SqlSagaCommitStore.cs`** — `ISagaCommitStore`: `ExistsAsync/RemoveAsync` cho recovery.
- **`Saga/SqlCrossDbOrderStats.cs`** — `ICrossDbOrderStats`: đếm/sum order cho read model Mongo.
- **`DependencyInjection.cs`** — `AddSqlServerSagaInfrastructure()` đăng ký tất cả phía SQL.
- **`Migrations/`** — `InitialCreate` (Orders/OrderDetails/Payments), `AddSagaCommits` (bảng marker),
  `SagaLog/InitialSagaLog` (bảng nhật ký).

---

## 3. `SellingNewProduct.Infrastructure.MongoDB.Saga` — phía Mongo (catalogue/people + non-pivot)

- **`Persistence/MongoDbContextBase.cs`** (+ `MongoAppDbContext` ghi, `MongoReadDbContext` đọc) — map 5
  collection + `saga_effects`.
- **`Models/*Document.cs`** — `Product/Category/Customer/Employee/User`Document, và
  `SagaEffectDocument` (`Id=SagaId`, `Reverted`, `Deltas: [{ProductId, Removed}]`).
- **`Mapping/*Mapper.cs`** — Domain ⇄ Document.
- **`Repositories/Write/MongoProductWriteRepository.cs`** — **saga-aware** (xem method chính dưới).
- **`Repositories/Write/Mongo{Category,Customer,Employee,User}WriteRepository.cs`** — ghi thường.
- **`Repositories/Read/Mongo*ReadRepository.cs`** — đọc 5 aggregate; Customer/Employee read lấy thống kê
  order qua `ICrossDbOrderStats`.
- **`Saga/MongoSagaParticipant.cs`** — participant **non-pivot** (`IsPivot=false`).
  - `BeginAsync()` — `MongoAppDbContext.Database.BeginTransactionAsync()` (cần replica set).
  - `CommitAsync()` / `RollbackAsync()` — commit/huỷ transaction Mongo.
- **`Saga/MongoSagaEffectStore.cs`** — `ISagaEffectStore` + ghi effect.
  - `Record(sagaId, delta)` — **enqueue** effect vào context (KHÔNG save) → repo save chung 1 lần trong tx.
  - `RevertAsync(sagaId)` — mở tx Mongo, cộng trả kho theo delta, đặt `Reverted=true`. **Idempotent**.
  - `GetPendingSagaIdsAsync()` / `RemoveAsync()` — cho recovery.
- **`Saga/MongoCrossDbDirectory.cs`** — `ICrossDbDirectory`: cấp tên cho read model SQL.
- **`DependencyInjection.cs`** — `AddMongoSagaInfrastructure()` đăng ký tất cả phía Mongo.

### Method quan trọng: `MongoProductWriteRepository.UpdateRangeAsync(products)`
Đây là nơi "phép thuật" saga phía Mongo xảy ra (được gọi khi confirm/cancel order):
1. Load document kho hiện tại theo id.
2. Tính **delta** mỗi product: `Removed = kho_cũ − kho_mới` (dương = trừ kho khi confirm; âm = trả kho khi cancel).
3. `MapInto` ghi kho mới vào document.
4. Nếu **đang trong saga**: `effectStore.Record(SagaId, delta)` (enqueue) **và**
   `SagaContext.RegisterCompensation(... ct => effectStore.RevertAsync(SagaId, ct))`.
5. `SaveChangesAsync()` — ghi **kho mới + SagaEffect cùng một lần** trong transaction Mongo (atomic).

---

## 4. File bị sửa (ngoài 3 project mới)

- **`Domain/Interfaces/Outbound/IUnitOfWork.cs`** — chỉ thêm XML-doc bộ verb Begin/Commit/Rollback/End,
  **không đổi chữ ký** → Domain & 2 provider gốc không phải sửa.
- **`API/Program.cs`** — thêm nhánh `DatabaseProvider=Hybrid` gọi `AddSagaCore + AddSqlServerSaga + AddMongoSaga`.
- **`API/appsettings.json`** — thêm `ConnectionStrings:SqlServerSaga`; Mongo phải là replica set.
- **`Domain/Services/OrderWriteService.cs`** — **KHÔNG đổi** (điểm mấu chốt: nghiệp vụ không biết gì về saga).

---

## 5. Ví dụ Order — đi từng bước

Điểm vào nghiệp vụ (không đổi): `OrderWriteService.PersistInTransactionAsync` được gọi bởi `ConfirmAsync`
và `CancelAsync` (đơn đã confirmed). Nội dung:

```csharp
await using var tx = await myUnitOfWork.BeginTransactionAsync(ct);   // (A) Begin
try
{
    await myProductRepository.UpdateRangeAsync(theProducts, ct);     // (B) Mongo: trừ/trả kho
    await myOrderRepository.UpdateAsync(theOrder, ct);               // (C) SQL: đổi trạng thái order
    await tx.CommitAsync(ct);                                        // (D) Commit
}
catch
{
    await tx.RollbackAsync(ct);                                      // (E) Rollback
    throw;
}
```

### Place Order (chỉ SQL — KHÔNG phải saga)
`PlaceAsync` chỉ `myOrderRepository.AddAsync(order)` → ghi thẳng vào SQL (Draft). Đọc
customer/employee/product (Mongo) để kiểm tra tồn tại. Không có bước 2-DB nên không mở saga.

### Confirm Order — kịch bản THÀNH CÔNG
Mục tiêu: trừ kho (Mongo) + chuyển Order sang `Confirmed` (SQL), cả hai cùng có hiệu lực.

| Bước | Việc xảy ra |
|---|---|
| (A) Begin | `SagaContext.Begin()` (SagaId mới, IsActive=true); mở **transaction SQL** + **transaction Mongo**; log `Started`. |
| (B) Mongo | `UpdateRangeAsync`: tính delta (Removed>0), ghi kho mới + `SagaEffect{Reverted=false}` (pending trong tx Mongo); đăng ký compensation `RevertStock`. |
| (C) SQL | `Order.Update` → `Confirmed` (pending trong tx SQL). |
| (D) Commit | commit **Mongo trước** (kho + effect thành durable) → commit **SQL sau**: ghi `SagaCommitRecord` **trong tx** rồi commit (order + marker durable). Log `Committed`. |

Kết quả: kho đã giảm ở Mongo, order `Confirmed` ở SQL, có `SagaCommits[SagaId]` + `SagaTransactions=Committed`.
(Effect & marker còn lại sẽ được recovery dọn ở lần khởi động sau — vô hại.)

### Confirm Order — kịch bản THẤT BẠI (bước SQL lỗi, tiến trình còn sống)
Ví dụ ràng buộc SQL vi phạm ở (C)/(D).

| Bước | Việc xảy ra |
|---|---|
| (A)(B) | Như trên: Mongo trừ kho + ghi effect, đăng ký compensation. |
| (D) Commit | commit Mongo **thành công** (kho durable) → commit SQL (pivot) **ném lỗi**. Exception nổi lên. |
| (E) Rollback | `RollbackAsync`: rollback tx SQL (order chưa từng `Confirmed`) → chạy compensation `RevertStock` = `effectStore.RevertAsync(SagaId)` → **cộng trả kho** + `Reverted=true`. Log `Compensated`. |

Kết quả: kho trở lại như cũ ở Mongo, order vẫn `Draft` ở SQL → **nhất quán**. Không có `SagaCommits[SagaId]`.

> Nếu Mongo commit lỗi (chưa tới SQL): SQL còn pending → `RollbackAsync` huỷ native cả hai, **không cần
> bù trừ**. Kho không đổi, order không đổi.

### Confirm Order — kịch bản CRASH (giải thích bằng code, từ lúc đang commit)

Crash xảy ra **bên trong `CommitAsync`**, sau khi Mongo commit nhưng trước khi SQL commit:

```csharp
public async Task CommitAsync(CancellationToken ct = default)
{
    if (myFinished) return;

    foreach (var p in myParticipants.Where(p => !p.IsPivot))   // (1) Mongo (non-pivot)
        await p.CommitAsync(ct);        // ✅ Mongo COMMIT xong: kho mới + SagaEffect đã DURABLE

    // ★★★ CRASH ở ĐÂY: tắt nguồn sau khi Mongo commit, trước khi SQL commit ★★★

    foreach (var p in myParticipants.Where(p => p.IsPivot))    // (2) SQL (pivot)
        await p.CommitAsync(ct);        // ✖ chưa chạy: SagaCommit + order Confirmed KHÔNG có

    myContext.MarkCommitted();
    await myLog.CompleteAsync(myContext.SagaId, SagaStatus.Committed, ...);  // ✖ không chạy
}
```

- `MongoSagaParticipant.CommitAsync` (bước 1) đã xong: `await myTransaction.CommitAsync(ct)` ⇒ **kho +
  `SagaEffect{Reverted=false}` durable**.
- `SqlSagaParticipant.CommitAsync` (bước 2) **chưa từng chạy** ⇒ không có `SagaCommits[SagaId]`, order vẫn
  `Draft` (transaction SQL bị server rollback khi mất kết nối). `SagaContext` + delegate compensation trong
  RAM **mất sạch** → đường hoàn tác in-process không chạy được.

Trạng thái ngay sau crash:

| Store | Kết quả |
|---|---|
| Mongo | kho đã trừ + `SagaEffect{Reverted=false}` — durable |
| SQL | order `Draft`, **không** có `SagaCommits[SagaId]` |

**Khởi động lại → `SagaRecoveryService` tự dọn:**

```csharp
// ExecuteAsync
var aPending = await aEffectStore.GetPendingSagaIdsAsync(ct);   // thấy SagaId (effect Reverted=false)
foreach (var aSagaId in aPending) await ReconcileAsync(aSagaId, ...);

// ReconcileAsync — marker SQL làm trọng tài
if (await theCommitStore.ExistsAsync(theSagaId, ct))           // có SagaCommits không?
{
    await theEffectStore.RemoveAsync(theSagaId, ct);           // crash SAU khi SQL commit → dọn rác
    await theCommitStore.RemoveAsync(theSagaId, ct);
    await theLog.CompleteAsync(theSagaId, SagaStatus.Committed, ...);
}
else
{
    await theEffectStore.RevertAsync(theSagaId, ct);           // SQL chưa commit → TRẢ KHO
    await theLog.CompleteAsync(theSagaId, SagaStatus.Compensated, ...);
}
```

- Crash **trước** SQL commit (kịch bản chính): `ExistsAsync=false` → `RevertAsync` trả kho → order vẫn
  `Draft` → **nhất quán**.
- Crash **sau** SQL commit (chỉ chưa kịp dọn marker): `ExistsAsync=true` → **không revert**, chỉ xoá 2
  marker (dữ liệu vốn đã đúng).

Mấu chốt: `SagaCommits[SagaId]` được ghi **trong cùng transaction** với order, nên sự tồn tại của nó là
ranh giới chính xác giữa "saga đã thành công" và "dở dang". `RevertAsync` lại idempotent (`Reverted`) nên
dù in-process đã chạy hay chưa, recovery chạy lại vẫn an toàn.

### Cancel Order (đơn đã Confirmed)
Giống Confirm nhưng ngược chiều kho: `IncreaseStock` → delta `Removed < 0`. Compensation/recovery cộng
`Removed` (số âm) ⇒ **trừ lại** đúng phần đã trả. Logic saga y hệt, chỉ khác dấu.

### Ship / Cancel (Draft) — KHÔNG phải saga
`ShipAsync` và huỷ đơn chưa Confirmed chỉ đụng SQL (1 lần ghi) → không mở saga.

---

## 5b. Chi tiết code: khối đăng ký compensation & cách revert chạy khi lỗi

Trong `MongoProductWriteRepository.UpdateRangeAsync`, sau khi tính delta và ghi kho mới:

```csharp
if (mySagaContext.IsActive && aStockRemoved.Count > 0)
{
    myEffectStore.Record(mySagaContext.SagaId, aStockRemoved);      // (1) bền vững — cho crash recovery
    var aSagaId = mySagaContext.SagaId;
    mySagaContext.RegisterCompensation($"RevertStock:{aSagaId}",
        ct => myEffectStore.RevertAsync(aSagaId, ct));              // (2) in-memory — cho rollback tại chỗ
}
```

Khối này làm **2 việc song song, cùng trỏ về một hàm `RevertAsync`**:

- **(1) `Record(...)`** — enqueue document `SagaEffect{ Reverted=false, Deltas=[{ProductId, Removed}] }`
  vào `MongoAppDbContext`; nó được lưu **chung `SaveChanges`** (cùng transaction Mongo) với việc trừ kho
  ⇒ tồn tại ⇔ Mongo đã commit. Đây là bản ghi **bền vững** để **recovery sau crash** biết trả lại bao nhiêu.
- **(2) `RegisterCompensation(...)`** — nhét một **delegate** vào danh sách trong `SagaContext` (chỉ trong
  RAM). Đây là đường **hoàn tác tại chỗ** khi tiến trình còn sống (lỗi ở bước SQL) — nhanh, khỏi đợi restart.

> `var aSagaId = mySagaContext.SagaId;` — bắt SagaId ra biến cục bộ rồi mới capture, để delegate không trỏ
> vào `SagaContext` (sẽ bị `Begin()` của saga sau ghi đè).

### Đường đi khi lỗi (revert tại chỗ)

**B1.** `OrderWriteService` bắt exception → gọi `tx.RollbackAsync(ct)`.

**B2.** `SagaUnitOfWorkTransaction.RollbackAsync` (kernel) chạy ngược các compensation:

```csharp
foreach (var aParticipant in myParticipants)                 // huỷ native tx còn pending (SQL rollback)
    await SafeAsync(() => aParticipant.RollbackAsync(ct));

for (var i = myContext.Compensations.Count - 1; i >= 0; i--)  // chạy NGƯỢC thứ tự
{
    var aCompensation = myContext.Compensations[i];
    try { await aCompensation.CompensateAsync(ct); }          // ← chính là delegate (2): RevertAsync(aSagaId)
    catch { aCompensated = false; }
}
```

**B3.** `MongoSagaEffectStore.RevertAsync` làm việc thật, trong **transaction Mongo riêng**, **idempotent**:

```csharp
await using var aTransaction = await myMongoAppDbContext.Database.BeginTransactionAsync(ct);

var aEffect = await myMongoAppDbContext.SagaEffects.FirstOrDefaultAsync(e => e.Id == sagaId, ct);
if (aEffect is null || aEffect.Reverted) return;             // không có / đã revert → bỏ qua (idempotent)

// ... load product theo Deltas ...
foreach (var aDelta in aEffect.Deltas)
    if (aProducts.TryGetValue(aDelta.ProductId, out var p))
        p.StockQuantity += aDelta.Removed;                   // cộng TRẢ đúng phần đã đổi

aEffect.Reverted = true;                                     // đánh dấu để không revert lần 2
await myMongoAppDbContext.SaveChangesAsync(ct);
await aTransaction.CommitAsync(ct);
```

Với `Removed = kho_cũ − kho_mới`:
- **Confirm** đã trừ 5 → `Removed=+5` → revert cộng `+5` → kho trở lại.
- **Cancel** đã trả +5 → `Removed=−5` → revert cộng `−5` → trừ lại đúng phần đã trả.

Vì `Reverted=true` được set **trong cùng transaction** với việc cộng trả kho, nếu **recovery lúc khởi động**
lỡ gọi lại `RevertAsync` (hệ thống không biết đường in-process đã chạy) thì `if (aEffect.Reverted) return;`
chặn lại → **không bao giờ trừ/cộng kho hai lần**. Đó là tính idempotent giúp 2 đường (in-process + recovery)
dùng chung an toàn.

---

## 6. Bảng tổng kết "ai làm gì khi nào"

| Tình huống | Mongo (kho) | SQL (order) | Marker | Hành động hoàn tác |
|---|---|---|---|---|
| Thành công | commit | commit | effect + SagaCommit | (không) |
| SQL lỗi (còn sống) | commit | rollback | effect, **không** SagaCommit | `RevertAsync` ngay (in-process) |
| Mongo lỗi | rollback | rollback (pending) | (không) | không cần |
| Crash giữa 2 commit | commit | chưa commit | effect, **không** SagaCommit | `RevertAsync` lúc khởi động (recovery) |
| Crash sau SQL commit | commit | commit | effect + SagaCommit | recovery xoá marker, xác nhận Committed |

Mọi đường đều hội tụ về trạng thái nhất quán; `RevertAsync` idempotent nên chạy lặp vẫn an toàn.
