# Lập trình bất đồng bộ trong SellingNewProduct

Tài liệu này ghi lại các mẫu (pattern) bất đồng bộ đã áp dụng thật vào code, kèm lý do **vì sao** áp dụng ở chỗ này mà **không** áp dụng ở chỗ kia. Điểm mấu chốt xuyên suốt:

> **EF Core `DbContext` KHÔNG thread-safe.** Cả SQL (`AppDbContext`) lẫn Mongo (`MongoAppDbContext`, `MongoReadDbContext`) đều là EF Core, đăng ký `Scoped` (một instance cho mỗi request). Chạy song song nhiều truy vấn trên **cùng một** context sẽ ném lỗi *"A second operation was started on this context..."*.

Từ đó suy ra 3 quy tắc quyết định:

| Tình huống | Cách đúng | Vì sao |
|---|---|---|
| Nhiều lời gọi độc lập trên **cùng** context | **Gộp thành 1 truy vấn** (batch) | Không thể song song trên context không thread-safe |
| Nhiều lời gọi trên **các context khác nhau** (SQL vs Mongo) | `Task.WhenAll` | Hai context khác nhau, song song an toàn, chồng lấn độ trễ |
| Việc thuần CPU (map/build), không đụng state chung | `Parallel` / `Task.Run` + `SemaphoreSlim` | An toàn thread, tận dụng nhiều nhân |
| Ghi số lượng lớn | **Chunk** rồi `AddRange` từng lô | Tránh 1 command/transaction khổng lồ |

---

## 1. `Task.WhenAll` — chạy song song hai store khác nhau

**File:** `SellingNewProduct.Infrastructure.SqlServer.Saga/Repositories/Read/SqlReportReadRepository.cs`

Báo cáo *best-selling* và *sales-by-category* cần dữ liệu từ **hai kho**: dòng bán hàng ở SQL, tên sản phẩm/danh mục ở MongoDB. Vì là **hai `DbContext` khác nhau**, ta chạy đồng thời:

```csharp
// Nhánh SQL (AppDbContext)
var aLinesTask = SalesLinesQuery()
    .Select(d => new { d.ProductId, d.Quantity, Revenue = d.UnitPriceAmount * d.Quantity })
    .ToListAsync(theCancellationToken);

// Nhánh Mongo: hai lời gọi dùng CHUNG context Mongo -> tuần tự BÊN TRONG nhánh này
async Task<(...)> LoadDimensionsAsync()
{
    var aProducts = await myDirectory.GetProductsAsync(theCancellationToken);
    var aCategoryNameById = await myDirectory.GetCategoryNamesAsync(theCancellationToken);
    return (aProducts, aCategoryNameById);
}
var aDimensionsTask = LoadDimensionsAsync();

// Nhánh SQL || nhánh Mongo (khác context) -> chồng lấn độ trễ, an toàn
await Task.WhenAll(aLinesTask, aDimensionsTask);
```

**Chú ý:** hai lời gọi Mongo (`GetProductsAsync`, `GetCategoryNamesAsync`) **không** được `Task.WhenAll` vì chúng dùng chung `MongoReadDbContext` — phải tuần tự.

**Lý do không áp dụng ở `OrderWriteService.PlaceAsync`:** ở đó kiểm tra customer + employee đều trên Mongo, **chung một** `MongoAppDbContext` -> phải tuần tự, không được `Task.WhenAll`.

---

## 2. Batch thay vì fan-out — trên cùng một context

**File:** `SellingNewProduct.Domain/Services/ProductWriteService.cs` (`CreateManyAsync`)

Trước đây kiểm tra SKU trùng bằng vòng lặp N truy vấn (`ExistsBySkuAsync` mỗi SKU) — vừa N+1, vừa **không** song song được (chung context). Sửa thành **một** round-trip:

```csharp
var aSkuValues = aSkuByIndex.Select(s => s.Value).ToList();
var aExistingSkus = await myProductRepository.GetExistingSkusAsync(aSkuValues, theCancellationToken);
if (aExistingSkus.Count > 0)
    throw new ConflictException($"A product with SKU '{aExistingSkus.First()}' already exists.");
```

Method mới `GetExistingSkusAsync` (repo Mongo) dùng `WHERE Sku IN (...)` một lần rồi diff trong bộ nhớ.

> Bài học: khi các lời gọi chia sẻ context không thread-safe, **gộp query** là cách "song song" đúng — không phải `Task.WhenAll`.

---

## 3. `SemaphoreSlim` + Parallelism — cho việc thuần CPU

**File:** `SellingNewProduct.Domain/Common/AsyncParallel.cs` + `ProductWriteService.BuildManyAsync`

`AsyncParallel.ForEachAsync` là tiện ích tái sử dụng: chạy song song có **giới hạn** bằng `SemaphoreSlim`, giữ nguyên thứ tự kết quả.

```csharp
using var aThrottle = new SemaphoreSlim(theMaxDegreeOfParallelism, theMaxDegreeOfParallelism);
var aTasks = theSource.Select(async aItem =>
{
    await aThrottle.WaitAsync(theCancellationToken);
    try     { return await theBody(aItem, theCancellationToken); }
    finally { aThrottle.Release(); } // luôn Release, kể cả khi lỗi
}).ToList();
return await Task.WhenAll(aTasks);
```

Áp dụng khi build sản phẩm cho lô lớn (map thuần CPU, không đụng state chung):

```csharp
var aBuilt = await AsyncParallel.ForEachAsync(
    Enumerable.Range(0, theRequests.Count),
    Environment.ProcessorCount,
    (aIndex, aCt) => Task.Run(() => Build(theRequests[aIndex], theSkuByIndex[aIndex]), aCt),
    theCancellationToken);
```

Lô nhỏ (`< 100`) thì map inline — overhead song song không đáng. `SemaphoreSlim` chặn trên `Environment.ProcessorCount` để không làm ngập thread pool.

> `SemaphoreSlim.WaitAsync()` **không** block thread (khác `Semaphore`). Dùng nó cho async throttle: giới hạn số HTTP call, số message gửi đồng thời, v.v.

---

## 4. Bulk write theo lô (chunk)

**File:** `ProductWriteService.CreateManyAsync`

Chèn theo lô để lô import khổng lồ không thành một command/transaction duy nhất:

```csharp
foreach (var aChunk in aProducts.Chunk(BulkWriteBatchSize)) // BulkWriteBatchSize = 500
    await myProductRepository.AddRangeAsync(aChunk, theCancellationToken);
```

`Enumerable.Chunk` (.NET 6+) cắt danh sách thành các mảng ≤ 500. Mỗi lô là một `AddRange` + `SaveChanges`.

> Các lô chạy **tuần tự** (chung `DbContext`). Muốn song song nhiều lô thì mỗi lô cần context riêng (`IDbContextFactory`) — chưa làm ở đây để giữ đơn giản.

---

## Tổng kết: chọn công cụ nào?

- **`Task.WhenAll`** — nhiều việc I/O độc lập trên **tài nguyên khác nhau / thread-safe** (SQL vs Mongo, nhiều HTTP endpoint).
- **`SemaphoreSlim`** — giới hạn số việc chạy đồng thời (throttle), tránh làm ngập tài nguyên.
- **`Parallel` / `Task.Run`** — việc **thuần CPU**, chia cho nhiều nhân.
- **Batch / Chunk** — khi các lời gọi chia sẻ tài nguyên không thread-safe (EF `DbContext`).
- **Tránh** `.Result` / `.Wait()` — gây deadlock/starvation; luôn `await` xuyên suốt.
