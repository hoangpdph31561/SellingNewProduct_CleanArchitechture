# Saga — Giải thích từng class + luồng chạy (kèm code)

Tài liệu này đi **theo đúng thứ tự một saga chạy** (Begin → bước Mongo → bước SQL → Commit / Rollback →
Recovery). Mỗi class được giới thiệu **ngay nơi nó xuất hiện trong luồng**, kèm code và giải thích từng
property/method để khỏi phải trượt qua lại. Đọc một mạch từ trên xuống là hiểu cả cơ chế.

> Bối cảnh: provider `DatabaseProvider=Hybrid`. SQL Server giữ `Order/OrderDetail/Payment`; MongoDB giữ
> `Product/Category/Customer/Employee/User` **và** sổ cái saga `saga_instances`. Một thao tác ghi xuống cả
> 2 DB không thể là transaction ACID chung → dùng **Saga orchestration**: mỗi bước **commit ngay** vào DB
> của nó, ghi cách hoàn tác vào **một sổ cái duy nhất**; bước sau lỗi thì replay compensation; crash thì
> recovery đọc lại sổ cái mà hoàn tác.

---

## 0. Sơ đồ gọi (ai gọi ai)

```
OrderWriteService.PersistInTransactionAsync         (Domain — KHÔNG đổi)
  │  await using tx = uow.BeginTransactionAsync()
  ▼
SagaUnitOfWork.BeginTransactionAsync                 → SagaContext.Begin() + ISagaStore.StartAsync()
  │  returns SagaUnitOfWorkTransaction (tx)
  │
  ├─ productRepo.UpdateRangeAsync()  ── bước Mongo (Compensatable) ─► MongoSagaStore.StageStepAsync()
  │                                                                    (trừ kho + enroll, 1 Mongo tx)
  ├─ orderRepo.UpdateAsync()         ── bước SQL (Pivot) ───────────► ISagaStore.EnrollStepAsync() (marker Pivot)
  │
  ├─ tx.CommitAsync()   (thành công) ─► ISagaStore.RemoveAsync()      (xoá sổ cái)
  └─ tx.RollbackAsync() (lỗi)        ─► SagaCompensator.CompensateAsync()
                                          ├─ ISagaCompensationRegistry.Resolve("RevertStock")
                                          └─ MongoStockCompensationHandler.CompensateAsync()  (cộng trả kho)

Khi khởi động lại:
SagaRecoveryService ─► ISagaStore.GetUnfinishedAsync() ─► SagaCompensator.CompensateAsync() (mỗi saga dở dang)
```

Mọi sợi dây buộc 2 DB là **`SagaId`**; nó nằm trong `SagaContext` lúc chạy và trong document `saga_instances`
để sống sót qua crash.

---

## 1. Điểm vào (Domain — KHÔNG đổi): `OrderWriteService.PersistInTransactionAsync`

Đây là chỗ nghiệp vụ gọi saga. Quan trọng: nó **không biết** mình đang chạy saga — chỉ thấy port
`IUnitOfWork` với bộ verb Begin/Commit/Rollback như mọi provider khác.

```csharp
private async Task PersistInTransactionAsync(Order theOrder, IEnumerable<Product> theProducts, CancellationToken ct)
{
    await using var aTransaction = await myUnitOfWork.BeginTransactionAsync(ct);   // (A) Begin
    try
    {
        await myProductRepository.UpdateRangeAsync(theProducts, ct);              // (B) bước Mongo: trừ/trả kho
        await myOrderRepository.UpdateAsync(theOrder, ct);                        // (C) bước SQL: đổi trạng thái order
        await aTransaction.CommitAsync(ct);                                       // (D) Commit
    }
    catch
    {
        await aTransaction.RollbackAsync(ct);                                     // (E) Rollback
        throw;
    }
}
```

Bên dưới `IUnitOfWork`, provider Hybrid cắm bản cài **saga**. Các mục sau bóc tách từng bước (A)→(E).

---

## 2. (A) Begin — `SagaUnitOfWork` + `SagaContext`

### `SagaUnitOfWork` — bản cài `IUnitOfWork` dạng saga
`BeginTransactionAsync` mở một saga: sinh `SagaId`, ghi dòng `Started` vào sổ cái, rồi trả về handle
transaction.

```csharp
internal sealed class SagaUnitOfWork : IUnitOfWork
{
    private readonly SagaContext myContext;
    private readonly ISagaStore myStore;
    private readonly SagaCompensator myCompensator;
    // ...ctor inject 3 cái trên...

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        myContext.Begin("OrderWrite");                            // (1) sinh SagaId, đặt IsActive=true
        await myStore.StartAsync(myContext.SagaId, myContext.Name, ct);  // (2) ghi saga_instances{Started}
        return new SagaUnitOfWorkTransaction(myContext, myStore, myCompensator);  // (3) handle cho Commit/Rollback
    }
}
```

### `SagaContext` — trạng thái của MỘT saga trong 1 request
Đăng ký **`scoped`** → repo Mongo, repo SQL, store, unit-of-work trong **cùng** request dùng **chung một
instance** ⇒ chung một `SagaId`. Nó **không** giữ danh sách compensation trong RAM nữa (đã chuyển xuống sổ
cái) — chỉ giữ id + cờ "đang chạy saga không".

```csharp
public sealed class SagaContext
{
    public Guid SagaId { get; private set; }                 // id buộc 2 DB
    public string Name { get; private set; } = "Saga";       // loại saga, ghi vào sổ cái để audit
    public SagaStatus Status { get; private set; } = SagaStatus.NotStarted;

    public bool IsActive => Status == SagaStatus.Started;     // repo nhìn cờ này để biết có commit-and-enroll không

    public void Begin(string theName)                        // mở saga mới
    {
        SagaId = Guid.NewGuid();
        Name = theName;
        Status = SagaStatus.Started;
    }

    public void MarkCommitted()        => Status = SagaStatus.Committed;
    public void MarkCompensating()     => Status = SagaStatus.Compensating;
    public void MarkCompensated()      => Status = SagaStatus.Compensated;
    public void MarkNeedsManualReview()=> Status = SagaStatus.NeedsManualReview;
}
```

- **`SagaId`** — GUID duy nhất; xuất hiện ở cả document Mongo lẫn các log.
- **`IsActive`** — chỉ `true` giữa `Begin()` và lúc kết thúc. Repo dùng nó để phân biệt "đang trong saga"
  (phải enroll bước) với ghi thường.
- **`Begin/Mark*`** — máy trạng thái nhỏ; `Status` đổi theo vòng đời (xem `SagaStatus` dưới).

### `SagaStatus` — vòng đời saga

```csharp
public enum SagaStatus
{
    NotStarted = 0,         // chưa có saga
    Started = 1,            // đang chạy, các bước đang commit
    Committed = 2,          // thành công (happy path)
    Compensating = 3,       // đang hoàn tác
    Compensated = 4,        // đã hoàn tác xong
    NeedsManualReview = 5,  // compensation thất bại quá số lần retry → cần can thiệp tay
}
```

### `ISagaStore.StartAsync` + `MongoSagaStore.StartAsync` — ghi dòng `Started`
`ISagaStore` là **port sổ cái** (mục 3 mô tả đủ). `StartAsync` tạo document saga rỗng:

```csharp
// MongoSagaStore : ISagaStore  (dùng chung MongoAppDbContext với các repo)
public async Task StartAsync(Guid theSagaId, string theName, CancellationToken ct = default)
{
    myContext.SagaInstances.Add(new SagaInstanceDocument
    {
        Id = theSagaId,                       // _id = SagaId
        Name = theName,
        Status = (int)SagaStatus.Started,
        RetryCount = 0,
        StartedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow,
        Steps = new List<SagaStepDocument>()  // chưa bước nào
    });
    await myContext.SaveChangesAsync(ct);     // commit ngay (không transaction): saga đã tồn tại bền vững
}
```

Sau (A): MongoDB có `saga_instances{_id=SagaId, Status=Started, Steps=[]}`.

---

## 3. (B) Bước Mongo (Compensatable) — `MongoProductWriteRepository.UpdateRangeAsync`

Đây là nơi "phép thuật" commit-per-step xảy ra phía Mongo. Code (rút gọn phần đọc/ghi quen thuộc):

```csharp
public async Task UpdateRangeAsync(IEnumerable<Product> theProducts, CancellationToken ct = default)
{
    var aProducts = theProducts.ToList();
    var aIds = aProducts.Select(p => p.Id).ToList();

    var aDocuments = (await myMongoAppDbContext.Products.Where(r => aIds.Contains(r.Id)).ToListAsync(ct))
        .ToDictionary(r => r.Id);

    // (1) Tính delta TRƯỚC khi sửa, để biết phải trả lại bao nhiêu khi rollback.
    var aDeltas = new List<StockDelta>();
    foreach (var aProduct in aProducts)
        if (aDocuments.TryGetValue(aProduct.Id, out var aDocument))
        {
            var aRemoved = aDocument.StockQuantity - aProduct.StockQuantity;   // dương = trừ; âm = trả
            if (aRemoved != 0) aDeltas.Add(new StockDelta(aProduct.Id, aRemoved));
            ProductMapper.MapInto(aDocument, aProduct);                        // ghi kho mới vào document
        }

    // (2) Ngoài saga (hoặc không đổi kho): commit thường, không enroll gì.
    if (!mySagaContext.IsActive || aDeltas.Count == 0)
    {
        await myMongoAppDbContext.SaveChangesAsync(ct);
        return;
    }

    // (3) Trong saga: mô tả bước + cách hoàn tác (nhãn "RevertStock" + data deltas).
    var aStep = new SagaStepInfo(
        "DecreaseStock",
        SagaStepKind.Compensatable,
        MongoStockCompensationHandler.Type,          // = "RevertStock"
        JsonSerializer.Serialize(aDeltas),           // data để undo
        Compensated: false);

    // (4) Commit kho mới + ghi bước vào sổ cái NGUYÊN TỬ, rồi COMMIT NGAY.
    await using var aTx = await myMongoAppDbContext.Database.BeginTransactionAsync(ct);
    await mySagaStore.StageStepAsync(mySagaContext.SagaId, aStep, ct);   // enroll (CHƯA save)
    await myMongoAppDbContext.SaveChangesAsync(ct);                      // products + saga_instances cùng 1 lần
    await aTx.CommitAsync(ct);                                           // hai thứ durable cùng nhau
}
```

Mấu chốt: **bước (4)** mở một Mongo transaction để việc trừ kho và việc ghi bước (có data hoàn tác) **cùng
thành công hoặc cùng thất bại** — không bao giờ có "kho đã trừ mà không biết cách trả lại". Và nó **commit
ngay**, không chờ tới cuối request.

### `StockDelta` — data hoàn tác của bước kho

```csharp
internal sealed record StockDelta(Guid ProductId, int Removed);   // Removed = kho_cũ − kho_mới
```

Serialize list này thành chuỗi JSON, nhét vào `CompensationData` của bước. Khi rollback, handler đọc
ngược ra để biết cộng trả bao nhiêu cho từng product.

### `SagaStepKind` + `SagaStepInfo` — mô tả một bước (định nghĩa ở Core)

```csharp
public enum SagaStepKind
{
    Compensatable = 0,    // có undo, chạy TRƯỚC pivot  (vd trừ kho, ghi history)
    Pivot = 1,            // điểm không quay lại         (vd ghi Order/Payment)
    RetryableForward = 2, // không undo, chạy SAU pivot  (vd gửi email — retry tiến tới)
}

public sealed record SagaStepInfo(
    string Name,               // tên bước, vd "DecreaseStock"
    SagaStepKind Kind,         // loại bước → orchestrator xử lý đúng cách khi rollback
    string? CompensationType,  // NHÃN tra handler hoàn tác (null nếu không cần undo)
    string CompensationData,   // payload để undo (vd deltas JSON)
    bool Compensated);         // true sau khi bước đã được hoàn tác
```

`SagaStepKind` chính là cơ chế để **scale thêm bước** mà orchestrator không đổi: thêm bước chỉ việc chọn
đúng `Kind`. Quy tắc: `Compensatable` trước `Pivot`, `RetryableForward` sau `Pivot` → không bao giờ phải
hoàn tác việc không hoàn tác được.

### `MongoSagaStore.StageStepAsync` — enroll bước KHÔNG save (để repo commit chung 1 lần)

```csharp
// Nạp document saga (đang được track trong cùng context từ StartAsync) rồi thêm bước — KHÔNG SaveChanges.
public async Task StageStepAsync(Guid theSagaId, SagaStepInfo theStep, CancellationToken ct = default)
{
    var aDocument = await LoadTrackedAsync(theSagaId, ct);
    if (aDocument is null) return;
    aDocument.Steps.Add(ToDocument(theStep));   // map SagaStepInfo → SagaStepDocument
    aDocument.UpdatedUtc = DateTime.UtcNow;
}

// Bản TỰ save (dùng cho enroll không cần gộp transaction, vd marker Pivot bên SQL):
public async Task EnrollStepAsync(Guid theSagaId, SagaStepInfo theStep, CancellationToken ct = default)
{
    await StageStepAsync(theSagaId, theStep, ct);
    await myContext.SaveChangesAsync(ct);
}
```

Vì sao có 2 bản? **`StageStepAsync`** để repo Mongo gộp việc ghi bước **vào chung** transaction trừ kho
(atomic). **`EnrollStepAsync`** (stage + save) cho nơi không cần gộp — như marker Pivot bên SQL (mục 4).

### `SagaInstanceDocument` — hình dạng một dòng sổ cái (collection `saga_instances`)

```csharp
internal sealed class SagaInstanceDocument
{
    public Guid Id { get; set; }                 // = SagaId
    public string Name { get; set; } = "";
    public int Status { get; set; }              // (int)SagaStatus
    public int RetryCount { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<SagaStepDocument> Steps { get; set; } = new();   // các bước đã commit
}

internal sealed class SagaStepDocument
{
    public string Name { get; set; } = "";
    public int Kind { get; set; }                // (int)SagaStepKind
    public string? CompensationType { get; set; }// nhãn handler
    public string CompensationData { get; set; } = "";
    public bool Compensated { get; set; }
}
```

Sau (B): `saga_instances{Status=Started, Steps=[ DecreaseStock(Compensatable, "RevertStock", deltas, Compensated=false) ]}`,
và kho ở Mongo **đã giảm thật**.

---

## 4. (C) Bước SQL (Pivot) — `SqlOrderWriteRepository.UpdateAsync`

SQL **commit ngay** (không còn participant giữ transaction treo). Ngay sau khi order đổi trạng thái, repo
ghi một **marker Pivot** vào sổ cái = đánh dấu "đã qua điểm không quay lại".

```csharp
public async Task UpdateAsync(Order theOrder, CancellationToken ct = default)
{
    var aRecord = await myAppDbContext.Orders.Include(r => r.Details)
        .FirstOrDefaultAsync(r => r.Id == theOrder.Id, ct);
    if (aRecord is null) return;

    OrderMapper.MapInto(aRecord, theOrder);
    await myAppDbContext.SaveChangesAsync(ct);    // ★ order Confirmed COMMIT NGAY (Pivot)

    await MarkPivotCommittedAsync(ct);            // ghi marker Pivot vào sổ cái
}

private async Task MarkPivotCommittedAsync(CancellationToken ct)
{
    if (!mySagaContext.IsActive) return;          // Ship / cancel-draft chạy ngoài saga → bỏ qua
    var aPivot = new SagaStepInfo("ConfirmOrder", SagaStepKind.Pivot,
        CompensationType: null, CompensationData: "", Compensated: false);
    await mySagaStore.EnrollStepAsync(mySagaContext.SagaId, aPivot, ct);
}
```

`CompensationType: null` vì Pivot **không bao giờ bị hoàn tác**. Marker này tồn tại để **recovery** biết
saga đã thành công (xem pivot-guard ở mục 6/7). SQL không giữ bảng saga nào — nó chỉ ghi vào sổ cái Mongo
qua `ISagaStore` (runtime = `MongoSagaStore`, cùng request scope).

Sau (C): sổ cái có thêm `ConfirmOrder(Pivot)`; order ở SQL đã `Confirmed`.

---

## 5. (D) Commit — `SagaUnitOfWorkTransaction.CommitAsync`

Lúc này mọi bước đã commit cục bộ rồi, nên Commit **không commit gì cả** — chỉ xoá dòng sổ cái (saga xong).

```csharp
public async Task CommitAsync(CancellationToken ct = default)
{
    if (myFinished) return;
    myFinished = true;
    myContext.MarkCommitted();

    // Mọi bước đã durable ở DB của nó → saga thành công → bỏ dòng sổ cái.
    // (Một dòng sổ cái còn sót lại CHÍNH LÀ tín hiệu cho recovery biết saga chưa kết thúc.)
    await myStore.RemoveAsync(myContext.SagaId, ct);
}
```

Sau (D) thành công: `saga_instances` **không còn dòng nào** cho saga này. Kho giảm ở Mongo + order
`Confirmed` ở SQL → nhất quán, sạch sẽ.

---

## 6. (E) Rollback — `SagaUnitOfWorkTransaction.RollbackAsync` → `SagaCompensator`

Khi một bước ném exception, `OrderWriteService` gọi `RollbackAsync`. Nó **đọc sổ cái** rồi giao cho
`SagaCompensator`:

```csharp
public async Task RollbackAsync(CancellationToken ct = default)
{
    if (myFinished) return;
    myFinished = true;
    myContext.MarkCompensating();

    var aSaga = await myStore.LoadAsync(myContext.SagaId, ct);   // đọc snapshot từ sổ cái
    if (aSaga is null) return;                                   // chưa bước nào commit → khỏi undo

    var aOk = await myCompensator.CompensateAsync(aSaga, ct);    // replay compensation
    if (aOk) myContext.MarkCompensated();
    else     myContext.MarkNeedsManualReview();
}
```

### `SagaCompensator` — "nửa hoàn tác" của orchestrator (dùng chung cho rollback & recovery)
Đây là trái tim của rollback. Đọc từng bước theo **thứ tự ngược**, bỏ qua Pivot/Forward, replay
Compensatable, **retry** tới khi được; quá hạn → `NeedsManualReview`.

```csharp
public async Task<bool> CompensateAsync(SagaSnapshot theSaga, CancellationToken ct = default)
{
    // (0) Pivot-guard: nếu đã có bước Pivot → saga ĐÃ qua điểm không quay lại → KHÔNG hoàn tác,
    //     chỉ finalize. Đây là cái khép khe "crash sau khi SQL commit nhưng trước khi dọn sổ cái".
    if (theSaga.Steps.Any(s => s.Kind == SagaStepKind.Pivot))
    {
        await myStore.SetStatusAsync(theSaga.SagaId, SagaStatus.Committed, ct);
        await myStore.RemoveAsync(theSaga.SagaId, ct);
        return true;
    }

    await myStore.SetStatusAsync(theSaga.SagaId, SagaStatus.Compensating, ct);

    // (1) Chạy NGƯỢC: bước commit gần nhất được undo trước.
    for (var i = theSaga.Steps.Count - 1; i >= 0; i--)
    {
        var aStep = theSaga.Steps[i];
        if (aStep.Kind != SagaStepKind.Compensatable || aStep.Compensated) continue;  // bỏ qua Pivot/Forward/đã undo

        if (!await TryCompensateStepAsync(theSaga.SagaId, aStep, ct))   // (2) retry bên trong
        {
            await myStore.SetStatusAsync(theSaga.SagaId, SagaStatus.NeedsManualReview, ct);
            return false;                                              // quá hạn → đậu lại chờ người
        }
    }

    await myStore.SetStatusAsync(theSaga.SagaId, SagaStatus.Compensated, ct);
    await myStore.RemoveAsync(theSaga.SagaId, ct);
    return true;
}
```

`TryCompensateStepAsync` là phần **Resolve handler theo nhãn + retry**:

```csharp
private async Task<bool> TryCompensateStepAsync(Guid theSagaId, SagaStepInfo theStep, CancellationToken ct)
{
    if (theStep.CompensationType is null) return true;                  // không có undo → coi như xong

    var aHandler = myRegistry.Resolve(theStep.CompensationType);        // "RevertStock" → handler instance
    if (aHandler is null) return false;                                 // quên đăng ký handler → cần review

    var aCtx = new SagaCompensationContext(theSagaId, theStep.Name, theStep.CompensationData);

    for (var anAttempt = 1; anAttempt <= MaxRetries; anAttempt++)       // MaxRetries = 5
    {
        try { await aHandler.CompensateAsync(aCtx, ct); return true; }  // chạy code undo thật
        catch when (anAttempt < MaxRetries)
        {
            await myStore.IncrementRetryAsync(theSagaId, ct);
            await Task.Delay(Backoff(anAttempt), ct);                   // backoff lũy thừa, chặn ở vài giây
        }
        catch { /* lần cuối: log, rơi ra dưới */ }
    }
    return false;
}
```

### `ISagaCompensationRegistry.Resolve` — đổi NHÃN string ra handler
`Resolve` chỉ là tra `Dictionary<string, handler>`. Registry được dựng từ **mọi** handler đăng ký trong DI:

```csharp
internal sealed class SagaCompensationRegistry : ISagaCompensationRegistry
{
    private readonly IReadOnlyDictionary<string, ISagaCompensationHandler> myHandlers;

    public SagaCompensationRegistry(IEnumerable<ISagaCompensationHandler> theHandlers)  // DI tiêm cả danh sách
    {
        var aMap = new Dictionary<string, ISagaCompensationHandler>(StringComparer.Ordinal);
        foreach (var h in theHandlers) aMap[h.CompensationType] = h;   // index theo nhãn
        myHandlers = aMap;
    }

    public ISagaCompensationHandler? Resolve(string theType)
        => myHandlers.TryGetValue(theType, out var h) ? h : null;
}
```

### `ISagaCompensationHandler` + `MongoStockCompensationHandler` — code undo thật
Interface: một handler biết hoàn tác **một loại** bước.

```csharp
public interface ISagaCompensationHandler
{
    string CompensationType { get; }                                   // NHÃN nó nhận
    Task CompensateAsync(SagaCompensationContext ctx, CancellationToken ct = default);  // PHẢI idempotent
}
public sealed record SagaCompensationContext(Guid SagaId, string StepName, string CompensationData);
```

Bản cài cho bước kho — cộng trả kho **và** đánh dấu bước `Compensated` trong **một** Mongo transaction nên
**idempotent** (chạy 2 lần không cộng kho gấp đôi):

```csharp
internal sealed class MongoStockCompensationHandler : ISagaCompensationHandler
{
    public const string Type = "RevertStock";                          // ← khớp nhãn repo gắn ở mục 3
    public string CompensationType => Type;

    public async Task CompensateAsync(SagaCompensationContext ctx, CancellationToken ct = default)
    {
        await using var aTx = await myContext.Database.BeginTransactionAsync(ct);

        var aSaga = await myContext.SagaInstances.FirstOrDefaultAsync(s => s.Id == ctx.SagaId, ct);
        var aStep = aSaga?.Steps.FirstOrDefault(s => s.Name == ctx.StepName);
        if (aStep is null || aStep.Compensated) return;                // đã undo / không có → no-op (idempotent)

        var aDeltas = JsonSerializer.Deserialize<List<StockDelta>>(ctx.CompensationData) ?? new();
        var aIds = aDeltas.Select(d => d.ProductId).ToList();
        var aProducts = (await myContext.Products.Where(p => aIds.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id);

        foreach (var d in aDeltas)
            if (aProducts.TryGetValue(d.ProductId, out var p))
                p.StockQuantity += d.Removed;                          // cộng TRẢ đúng phần đã đổi

        aStep.Compensated = true;                                      // đánh dấu — cùng transaction
        await myContext.SaveChangesAsync(ct);
        await aTx.CommitAsync(ct);
    }
}
```

Với `Removed = kho_cũ − kho_mới`: confirm trừ 5 → `Removed=+5` → revert cộng `+5` (kho trở lại); cancel trả
+5 → `Removed=−5` → revert cộng `−5` (trừ lại đúng phần đã trả).

Sau (E): kho ở Mongo trở về như cũ, order vẫn `Draft` ở SQL, sổ cái đã xoá → nhất quán.

---

## 7. Crash recovery — `SagaRecoveryService`

`SagaContext` (RAM) mất khi crash, nhưng sổ cái `saga_instances` còn. `SagaRecoveryService` chạy **một lần
lúc khởi động**, đọc mọi saga chưa kết thúc và giao cho **chính** `SagaCompensator` — đúng cách rollback
in-request làm.

```csharp
public sealed class SagaRecoveryService : BackgroundService          // chỉ đăng ký ở provider Hybrid
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await using var aScope = myScopeFactory.CreateAsyncScope();
        var aStore = aScope.ServiceProvider.GetRequiredService<ISagaStore>();
        var aCompensator = aScope.ServiceProvider.GetRequiredService<SagaCompensator>();

        var aUnfinished = await aStore.GetUnfinishedAsync(ct);        // Status in (Started, Compensating, NeedsManualReview)
        foreach (var aSaga in aUnfinished)
            await aCompensator.CompensateAsync(aSaga, ct);            // có Pivot → finalize; không → revert
    }
}
```

`GetUnfinishedAsync` lọc theo trạng thái chưa terminal:

```csharp
public async Task<IReadOnlyList<SagaSnapshot>> GetUnfinishedAsync(CancellationToken ct = default)
{
    var started = (int)SagaStatus.Started; var comp = (int)SagaStatus.Compensating; var manual = (int)SagaStatus.NeedsManualReview;
    var docs = await myContext.SagaInstances.AsNoTracking()
        .Where(s => s.Status == started || s.Status == comp || s.Status == manual).ToListAsync(ct);
    return docs.Select(ToSnapshot).ToList();
}
```

**Hai nhánh crash, nhờ pivot-guard ở mục 6:**
- Crash **trước** Pivot (mới trừ kho, order chưa Confirmed): sổ cái chỉ có `DecreaseStock` → `CompensateAsync`
  replay `RevertStock` → trả kho → nhất quán.
- Crash **sau** Pivot (order đã Confirmed): sổ cái có `ConfirmOrder(Pivot)` → pivot-guard → **finalize, KHÔNG
  trả kho** → nhất quán (kho đã trừ + order Confirmed là đúng).

> Khe dư duy nhất: crash **đúng giữa** SQL-commit và lúc enroll Pivot marker (1 write Mongo ngay sau). Cực
> nhỏ; là cái giá cố hữu của polyglot không-2PC khi đặt sổ cái ở Mongo thay vì marker atomic trong tx SQL.

---

## 8. Lắp ráp DI — ai đăng ký cái gì

### `AddSagaCore()` (Saga.Core) — bộ khung điều phối
```csharp
public static IServiceCollection AddSagaCore(this IServiceCollection s)
{
    s.AddScoped<SagaContext>();                                   // 1 instance / request → chung SagaId
    s.AddScoped<IUnitOfWork, SagaUnitOfWork>();                   // cắm port Domain vào saga
    s.AddScoped<ISagaCompensationRegistry, SagaCompensationRegistry>();
    s.AddScoped<SagaCompensator>();
    s.AddHostedService<SagaRecoveryService>();                    // recovery lúc khởi động
    return s;
}
```
`ISagaStore` và các handler **không** đăng ký ở đây — do project DB cấp.

### `AddMongoSagaInfrastructure()` — Mongo cấp sổ cái + handler
```csharp
s.AddScoped<MongoSagaStore>();                                   // concrete (repo cần để gộp transaction)
s.AddScoped<ISagaStore>(sp => sp.GetRequiredService<MongoSagaStore>());  // cùng instance
s.AddSagaCompensationHandlers(typeof(DependencyInjection).Assembly);     // auto-quét MỌI handler trong assembly
```

`AddSagaCompensationHandlers` quét assembly tìm mọi `ISagaCompensationHandler` rồi tự `AddScoped` — thêm
handler mới **không** phải thêm dòng đăng ký:
```csharp
public static IServiceCollection AddSagaCompensationHandlers(this IServiceCollection s, params Assembly[] asms)
{
    var t = typeof(ISagaCompensationHandler);
    foreach (var impl in asms.SelectMany(a => a.GetTypes())
                             .Where(x => x is { IsAbstract:false, IsInterface:false } && t.IsAssignableFrom(x)))
        s.TryAddEnumerable(ServiceDescriptor.Scoped(t, impl));   // TryAddEnumerable: chống trùng
    return s;
}
```

### `AddSqlServerSagaInfrastructure()` — SQL chỉ có repo (không có saga state)
Đăng ký `AppDbContext` (Orders/Payments) + repo. **Không** participant/log/marker nào. `SqlOrderWriteRepository`
inject `ISagaStore` + `SagaContext` để ghi marker Pivot.

### `Program.cs` — thứ tự gọi
```csharp
builder.Services.AddSagaCore();                                  // gọi TRƯỚC
builder.Services.AddSqlServerSagaInfrastructure(builder.Configuration);
builder.Services.AddMongoSagaInfrastructure(builder.Configuration);
```

---

## 9. Thêm bước rollback mới (history, email) — checklist + code mẫu

Ví dụ "ghi user history" (Compensatable):

1. **Viết handler** (tự được auto-quét, không cần đăng ký tay):
   ```csharp
   internal sealed class DeleteUserHistoryHandler : ISagaCompensationHandler
   {
       public const string Type = "DeleteUserHistory";
       public string CompensationType => Type;
       public async Task CompensateAsync(SagaCompensationContext ctx, CancellationToken ct = default)
       { /* xoá history theo ctx.CompensationData, idempotent */ }
   }
   ```
2. **Enroll bước** tại repo/service làm việc đó (gắn nhãn + data, đặt TRƯỚC Pivot):
   ```csharp
   var step = new SagaStepInfo("WriteUserHistory", SagaStepKind.Compensatable,
       DeleteUserHistoryHandler.Type, JsonSerializer.Serialize(new { historyId }), false);
   await store.EnrollStepAsync(sagaId, step, ct);
   ```

Gửi email = `SagaStepKind.RetryableForward`, đặt **sau** Pivot, không undo (đẩy outbox + retry gửi).

**Không đụng tới:** `SagaCompensator`, `Resolve`, `SagaRecoveryService`, `SagaUnitOfWork`, Domain/Application.
Đó là lý do thiết kế "scale step mà không đập lại".

---

## 10. Bảng tổng kết các kịch bản

| Tình huống | Mongo (kho) | SQL (order) | Sổ cái `saga_instances` | Ai hoàn tác |
|---|---|---|---|---|
| Thành công | commit ngay | commit ngay + marker Pivot | tạo rồi **xoá** (CommitAsync) | (không) |
| Bước SQL lỗi (còn sống) | commit ngay | rollback (chưa Pivot) | còn `Started`+`DecreaseStock` | `SagaCompensator` → RevertStock (in-process) |
| Bước Mongo lỗi | tx Mongo tự rollback | chưa tới | chỉ `Started` | không cần (xoá sổ cái) |
| Crash trước Pivot | commit ngay | chưa commit | `Started`+`DecreaseStock` | recovery → RevertStock |
| Crash sau Pivot | commit ngay | commit ngay | `Started`+`DecreaseStock`+`Pivot` | recovery → **pivot-guard finalize, không revert** |
| Compensation fail quá 5 lần | — | — | `NeedsManualReview` | recovery thử lại lần khởi động sau / người vào tay |

Mọi đường hội tụ về trạng thái nhất quán; compensation idempotent nên chạy lặp vẫn an toàn.
