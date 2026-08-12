# TỔNG HỢP KIẾN THỨC — SellingNewProduct

Tài liệu **học sâu**, gom toàn bộ kiến thức của dự án (đọc từ code thật + các doc lẻ). Mỗi khái niệm trình bày theo khung:

> **Định nghĩa (kỹ) → Vấn đề nó giải → Cơ chế/Ví dụ → Áp dụng trong project → Ưu/Nhược (sâu) → Khi nào dùng / KHÔNG dùng**

Đọc theo thứ tự cũng được, mà tra từng mục cũng được. File này **dài có chủ đích** — mục tiêu là hiểu *tại sao*, không chỉ biết *tên gọi*.

**Mục lục**
- [Phần A — Bất đồng bộ & Đồng thời](#phần-a)
- [Phần B — Kiến trúc & Design Patterns](#phần-b)
- [Phần C — So sánh công nghệ](#phần-c)
- [Phần D — Bảng tra bài toán → pattern](#phần-d)

Doc chuyên sâu từng phần: [async-concepts.md](async-concepts.md), [async-programming.md](async-programming.md), [ARCHITECTURE.md](ARCHITECTURE.md), [saga-hybrid.md](saga-hybrid.md), [outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md), [polly-circuit-breaker.md](polly-circuit-breaker.md), [polly-bulkhead.md](polly-bulkhead.md).

---

<a name="phần-a"></a>
## Phần A — Bất đồng bộ & Đồng thời

Trước khi vào từng công cụ, cần phân biệt hai loại "chậm", vì **chọn sai công cụ = không nhanh hơn, thậm chí chậm đi**:

- **I/O-bound**: thread **không làm gì**, chỉ **chờ** kết quả từ DB/mạng/đĩa. Ví dụ: query SQL mất 80ms — CPU rảnh suốt 80ms đó. → Giải bằng `async/await` + `Task.WhenAll` (chờ nhiều thứ cùng lúc mà **không tốn thread**).
- **CPU-bound**: thread **bận** chạy tính toán (map 100k dòng, hash, nén). CPU cháy 100%. → Giải bằng **nhiều thread thật** (`Parallel`/`Task.Run`) để chia cho nhiều nhân.

`Task.WhenAll` một đống việc CPU-bound trên **một** thread **không** nhanh hơn — nó chỉ chồng lấn thời gian *chờ*, mà CPU-bound thì không có lúc chờ. Ngược lại, `Parallel.For` gọi I/O = lãng phí cả đống thread ngồi chờ. **Nhớ kỹ khác biệt này.**

---

### A0. Nền tảng: Process → Thread → ThreadPool → Task

**Định nghĩa từ dưới lên:**

- **Process**: một chương trình đang chạy, có không gian nhớ riêng, cô lập với process khác.
- **Thread**: đơn vị **thực thi** nhỏ nhất mà hệ điều hành **lập lịch** (schedule). Mỗi thread có **stack riêng** (mặc định ~1MB) + con trỏ lệnh. Vì tốn ~1MB stack + cần OS cấp phát, **tạo một thread là đắt** (cỡ vài chục µs–ms + tốn RAM). Có 4.000 thread = ~4GB chỉ riêng stack.
- **ThreadPool**: CLR giữ sẵn một "hồ bơi" thread tái sử dụng. Bạn quăng việc vào (`ThreadPool.QueueUserWorkItem`, `Task.Run`); pool lấy một thread rảnh chạy, xong **trả thread về hồ** (không hủy). Tránh chi phí tạo/hủy liên tục. Pool còn **tự co giãn** số thread theo tải (thuật toán hill-climbing).
- **Task**: lớp trừu tượng **trên** ThreadPool, đại diện "một công việc sẽ hoàn tất trong tương lai". Không phải "một thread" — một `Task` I/O có thể **chẳng chiếm thread nào** trong lúc chờ (xem A1).

**Vì sao `Task` thay cho `new Thread` thủ công:**

| | `new Thread(...)` | `Task` |
|---|---|---|
| Chi phí tạo | Cao (stack riêng, OS cấp) | Thấp (mượn ThreadPool) |
| Trả giá trị | Không (phải tự chia sẻ biến) | `Task<T>` |
| Hủy | Thủ công (cờ tự chế) | `CancellationToken` |
| Nối tiếp khi xong | `Join()` (block) | `await` / `ContinueWith` |
| Chờ nhiều cái | Tự quản lý | `Task.WhenAll/WhenAny` |

> Trong dự án **không** có `new Thread(...)` chỗ nào — luôn `Task`/`async`. Đó là mặc định hiện đại của .NET.

---

### A1. `async` / `await` — trái tim của mọi thứ

**Định nghĩa:** `async`/`await` là cú pháp để viết code bất đồng bộ **trông như tuần tự**. Compiler biến một method `async` thành một **state machine** (máy trạng thái) — một class ẩn có các "điểm dừng" tại mỗi `await`.

**Hiểu lầm #1 cần đập bỏ:** *"async = chạy trên thread khác"*. **SAI.** `await` một tác vụ **I/O** không dùng thread nào trong lúc chờ cả.

**Cơ chế thật (chi tiết):** khi thực thi gặp `await someIoTask` mà tác vụ **chưa xong**:

1. Method **đăng ký một callback** ("khi I/O xong thì chạy tiếp từ đây") rồi **return ngay** về hàm gọi.
2. Thread đang chạy được **trả về ThreadPool** — nó đi phục vụ request/việc khác. **Không thread nào ngồi chờ.**
3. Ổ đĩa/network card hoàn tất I/O ở tầng OS → báo qua **IO Completion Port**.
4. ThreadPool lấy **một thread bất kỳ** (không nhất thiết thread cũ) chạy tiếp method từ đúng chỗ `await` dừng, với biến cục bộ được khôi phục từ state machine.

**Vì sao điều này quan trọng sống còn với server:** một web server có ThreadPool **giới hạn**. Hình dung 1.000 request đồng thời, mỗi cái query DB 100ms:

- **Kiểu block** (`.Result`): mỗi request **giữ chặt 1 thread** suốt 100ms chờ DB → cần 1.000 thread → pool cạn → request thứ 1.001 phải **xếp hàng** → độ trễ tăng vọt, rồi sập. Đây là **thread pool starvation**.
- **Kiểu `await`**: trong 100ms chờ DB, thread được **trả về pool** phục vụ request khác → có thể chỉ ~vài chục thread phục vụ cả 1.000 request. Cùng phần cứng, **throughput cao hơn nhiều lần**.

**Ví dụ đối chiếu:**
```csharp
// XẤU: block thread suốt thời gian chờ DB. Còn nguy cơ DEADLOCK (xem A6).
var order = GetOrderAsync(id).Result;      // hoặc .Wait(), .GetAwaiter().GetResult()

// TỐT: await -> thread được trả về pool trong lúc chờ I/O
var order = await GetOrderAsync(id);
```

**Hiểu lầm #2:** *"thêm `async` là code chạy nhanh hơn"*. **SAI.** Một request lẻ **không** nhanh hơn (vẫn chờ DB 100ms). Cái được là **khả năng phục vụ đồng thời** (scalability), không phải tốc độ một request.

**Quy tắc vàng — "async all the way":** đã `async` thì `await` xuyên suốt, **đừng** chèn `.Result`/`.Wait()` giữa chừng.

- **Ưu:** scale tốt dưới tải cao; ít thread hơn; `CancellationToken` hủy sạch.
- **Nhược:** lan `async` khắp call stack (một chỗ async kéo cả chuỗi phải async); dễ mắc `sync-over-async` gây deadlock; debug stack trace khó đọc hơn; `async void` (trừ event handler) nuốt exception — tránh.
- **Khi dùng:** mọi I/O. **Khi KHÔNG:** việc thuần CPU ngắn (async chỉ thêm overhead state machine, không lợi).

---

### A2. `Task.WhenAll` — chạy song song nhiều tác vụ độc lập

**Định nghĩa:** `Task.WhenAll(t1, t2, ...)` nhận các Task **đã khởi động**, trả về một Task hoàn tất khi **tất cả** xong. Nếu các tác vụ độc lập, tổng thời gian ≈ **max** (cái lâu nhất) thay vì **tổng**.

**Điểm mấu chốt dễ sai:** `WhenAll` **không tự khởi động** gì cả — nó chỉ **chờ**. Việc "chạy song song" xảy ra vì bạn **đã gọi** các method async (chúng bắt đầu I/O) *trước khi* `await`. So sánh:

```csharp
// TUẦN TỰ: await ngay -> t2 chỉ bắt đầu sau khi t1 xong = 100 + 80 = 180ms
var a = await CallSqlAsync();     // 100ms
var b = await CallMongoAsync();   // 80ms

// SONG SONG: khởi động CẢ HAI rồi mới chờ = max(100, 80) = 100ms
var t1 = CallSqlAsync();          // bắt đầu ngay, chưa await
var t2 = CallMongoAsync();        // bắt đầu ngay
await Task.WhenAll(t1, t2);
var a = await t1;  var b = await t2;   // đã xong, lấy kết quả (không chờ thêm)
```

**ĐIỀU KIỆN dùng được (cực quan trọng trong dự án này):** các tác vụ phải **thật sự độc lập** VÀ **không chia sẻ tài nguyên không thread-safe**. `DbContext` của EF Core **KHÔNG** thread-safe — chạy 2 query song song trên **cùng một** context ném:
> *"A second operation was started on this context before a previous operation completed."*

**Áp dụng thực tế:** [`SqlReportReadRepository.cs`](../SellingNewProduct.Infrastructure.SqlServer.Saga/Repositories/Read/SqlReportReadRepository.cs) — báo cáo cần dữ liệu bán hàng (SQL, `AppDbContext`) + tên sản phẩm/danh mục (Mongo, `MongoReadDbContext`). Đây là **hai context khác nhau** → an toàn chạy song song:
```csharp
var aLinesTask = SalesLinesQuery().Select(...).ToListAsync(ct);   // nhánh SQL
var aDimensionsTask = LoadDimensionsAsync();                       // nhánh Mongo (2 call NỘI BỘ tuần tự)
await Task.WhenAll(aLinesTask, aDimensionsTask);                   // SQL || Mongo
```
Chú ý: bên trong nhánh Mongo, 2 call `GetProductsAsync` + `GetCategoryNamesAsync` **chung** `MongoReadDbContext` nên phải **tuần tự** — chỉ nhánh-với-nhánh mới song song.

**Xử lý exception:** nếu nhiều Task ném lỗi, `await Task.WhenAll(...)` chỉ ném **một** exception đầu tiên (các cái khác nằm trong `task.Exception`). Muốn gom hết phải đọc từng task.

- **Ưu:** chồng lấn độ trễ I/O độc lập → giảm tổng thời gian; code gọn.
- **Nhược:** **nguy hiểm** nếu share tài nguyên không thread-safe (DbContext, `HttpClient` xài sai, biến chung); khó bắt hết lỗi; bung quá nhiều Task cùng lúc = quá tải (cần throttle — xem A3).
- **Khi dùng:** nhiều I/O độc lập trên **tài nguyên khác nhau** (SQL vs Mongo, nhiều HTTP endpoint khác nhau). **Khi KHÔNG:** cùng một `DbContext` → batch 1 query thay thế (xem A5); việc CPU-bound → Parallel.

---

### A3. `SemaphoreSlim` — giới hạn số việc đồng thời (throttle)

**Định nghĩa:** semaphore = "quầy phát vé", tối đa **N vé**. Muốn chạy phải `WaitAsync()` xin một vé (hết vé thì **chờ**, nhưng chờ **không block thread** — đây là điểm khác `Semaphore` cũ), làm xong `Release()` trả vé. → Bảo đảm **không quá N** việc chạy đồng thời.

**Vấn đề nó giải:** `Task.WhenAll` trên **10.000** item = bung 10.000 Task cùng lúc → ngập ThreadPool, cạn connection pool DB, hoặc dịch vụ ngoài (SMTP, API) trả 429/timeout vì quá tải. Semaphore ép trần an toàn (vd 5–20 việc đồng thời).

**Cơ chế + ví dụ (mẫu chuẩn, luôn `Release` trong `finally`):**
```csharp
var throttle = new SemaphoreSlim(5);   // tối đa 5 việc song song
var tasks = items.Select(async item =>
{
    await throttle.WaitAsync(ct);      // xin vé (chờ nếu hết, không block thread)
    try     { return await GoiApiNgoaiAsync(item, ct); }
    finally { throttle.Release(); }    // TRẢ vé — kể cả khi lỗi, nếu không sẽ rò rỉ vé -> deadlock dần
});
await Task.WhenAll(tasks);
```

**Áp dụng thực tế:** helper tái dùng [`Domain/Common/AsyncParallel.cs`](../SellingNewProduct.Domain/Common/AsyncParallel.cs) — `ForEachAsync` chạy có giới hạn, **giữ nguyên thứ tự** kết quả. Dùng ở `ProductWriteService.BuildManyAsync` khi build lô sản phẩm lớn.

**Liên hệ:** đây chính là **bulkhead thủ công** — cùng ý tưởng với `ConcurrencyLimiter` của Polly (B12). Khác biệt: `SemaphoreSlim` bạn tự quản; Polly bọc thành pipeline có thêm queue-limit + tích hợp DI.

- **Ưu:** chặn quá tải; đơn giản; `WaitAsync` không block thread; chọn N theo giới hạn thật (số connection, rate-limit của API).
- **Nhược:** **quên `Release()` trong `finally`** là bug kinh điển → hết vé vĩnh viễn, treo; chọn N sai (thấp = chậm, cao = quá tải); không cấp thread riêng nên không cô lập theo pool.
- **Khi dùng:** gọi N dịch vụ ngoài/query đồng thời cần chặn trần; giới hạn concurrency downstream. **Khi KHÔNG:** chỉ 2–3 việc cố định (dùng `WhenAll` thẳng); việc chung DbContext (không parallel được, phải batch).

---

### A4. Parallelism cho CPU — `Parallel.For` / `Task.Run`

**Định nghĩa:** chạy việc **thuần CPU** trên **nhiều thread thật đồng thời** để chia tải cho nhiều nhân CPU. Khác hẳn `async` (async là để **không** tốn thread khi chờ I/O; parallel là để **dùng nhiều** thread cho tính toán).

**Vì sao khác `Task.WhenAll`:** `WhenAll` giỏi chồng lấn *thời gian chờ*. Việc CPU-bound **không có lúc chờ** — nó cần **nhiều core chạy cùng lúc**. `Parallel.For` phân mảnh vòng lặp cho các thread ThreadPool, mỗi thread ăn một core.

**Ví dụ (giữ thread-safe: mỗi index ghi slot riêng, không share state ghi):**
```csharp
var results = new Product[requests.Count];
Parallel.For(0, requests.Count, i =>
{
    results[i] = Build(requests[i]);   // Build thuần CPU, độc lập từng item
});
```

**Áp dụng thực tế:** `ProductWriteService.BuildManyAsync` — lô ≥100 sản phẩm thì build (map + validate + tạo Value Object) offload qua `Task.Run` có giới hạn bằng semaphore; lô nhỏ build inline (overhead song song không đáng cho vài chục item).

- **Ưu:** rút ngắn việc CPU nặng theo số core (map/nén/hash hàng loạt).
- **Nhược:** overhead tạo/điều phối thread + context-switch (lô nhỏ **chậm hơn** tuần tự); **race condition** nếu ghi chung biến (phải khoá/ghi slot riêng); **vô ích cho I/O** (thread ngồi chờ, không tận dụng core); có thể tranh core với phần còn lại của app.
- **Khi dùng:** tính toán/biến đổi **lượng lớn**, không đụng tài nguyên chung không thread-safe (không `DbContext`). **Khi KHÔNG:** việc I/O (dùng async), dữ liệu nhỏ, có share state phức tạp.

---

### A5. Bulk write, Chunk, Connection Pool & "mở 1 connection làm nhiều CRUD"

**Định nghĩa (phần bạn hỏi trước đây):**

- **Connection pool:** mở một kết nối tới DB **rất đắt** (bắt tay TCP + xác thực + TLS, hàng chục ms). Nên ADO.NET/EF Core dùng **pool**: connection "đóng" thực ra được **trả về hồ** để tái dùng, **không** đóng vật lý. Vì vậy đừng mở/đóng connection cho từng câu lệnh.
- **DbContext = 1 connection (mượn) + 1 Unit of Work:** một `DbContext` khi cần sẽ mượn 1 connection từ pool và giữ một **change tracker** (sổ theo dõi Added/Modified/Deleted). Bạn làm **nhiều CRUD** trên nó (đọc, sửa, thêm, xoá) — tất cả ghi vào sổ, **chưa** gửi DB. Đến `SaveChanges` mới **gửi một loạt** trong **một** round-trip + transaction ngầm.

**Ví dụ "mở 1 lần, dồn CRUD, gửi 1 lần":**
```csharp
var order   = await db.Orders.FindAsync(id);   // Read  (mượn connection)
order.Confirm();                                // Update (ghi vào change tracker)
db.OrderDetails.Add(newDetail);                 // Create
db.OrderDetails.Remove(oldDetail);              // Delete
await db.SaveChangesAsync();                     // GỬI TẤT CẢ 1 lần, atomic
// hết scope -> Dispose -> connection TRẢ VỀ POOL (không đóng vật lý)
```
→ Ít round-trip, atomic (một transaction ngầm), rẻ (tái dùng pool).

- **Bulk write theo Chunk:** insert/update số lượng **lớn** thì cắt thành lô `Enumerable.Chunk(N)` (vd 500), mỗi lô một `AddRange`+`SaveChanges`. Tránh dựng **một** command/transaction khổng lồ (ngốn RAM, khoá lâu, dễ timeout).
  - Áp dụng: `ProductWriteService.CreateManyAsync` — `foreach (var chunk in products.Chunk(500)) AddRangeAsync(chunk)`.
- **Batch thay vì fan-out:** N việc trên **cùng** context thì gộp **1 query** (`WHERE Id IN (...)`), đừng bắn N query song song (không parallel được vì chung context). Áp dụng: đổi N lần `ExistsBySkuAsync` thành 1 `GetExistingSkusAsync`.

- **Ưu:** ít round-trip, atomic, tái dùng connection; chunk kiểm soát kích thước transaction.
- **Nhược:** `DbContext` không thread-safe (phải tuần tự/batch, không fan-out); change tracker giữ nhiều entity = tốn RAM (lô lớn nên `AsNoTracking` cho đọc / chia chunk cho ghi); lô quá to = khoá lâu.
- **Khi dùng:** import/cập nhật nhiều bản ghi; ghi ≥2 aggregate atomic (dùng `IUnitOfWork`).

---

### A6. Deadlock kinh điển — vì sao `.Result` / `.Wait()` nguy hiểm

**Định nghĩa:** deadlock = hai bên chờ nhau vòng tròn, không ai đi tiếp. `sync-over-async` (`GetAsync().Result`) block thread hiện tại **chờ** tác vụ async xong; nhưng phần tiếp sau `await` (continuation) lại cần **chính thread bị block đó** để chạy (khi có `SynchronizationContext` như UI / ASP.NET cũ) → chờ nhau → **treo vĩnh viễn**.

ASP.NET Core hiện đại **không** còn `SynchronizationContext` nên bớt rủi ro deadlock, **nhưng** `.Result`/`.Wait()` vẫn **block thread** → góp phần **thread starvation** dưới tải. → Luôn `await`, đừng chặn.

---

### A7. Bảng chọn công cụ async (chốt Phần A)

| Tình huống | Công cụ | Vì sao |
|---|---|---|
| Chờ I/O (DB/mạng/file) | `async/await` | Trả thread về pool khi chờ |
| Nhiều I/O độc lập, **khác** tài nguyên | `Task.WhenAll` | Chồng lấn độ trễ |
| Nhiều việc trên **cùng** DbContext | **Batch 1 query** | Context không thread-safe |
| Giới hạn số việc đồng thời | `SemaphoreSlim` | Chặn quá tải downstream |
| Tính toán CPU hàng loạt | `Parallel.For`/`Task.Run` | Dùng nhiều core |
| Ghi/đọc nhiều bản ghi | Bulk + Chunk + pool | Ít round-trip, atomic |
| Ghi ≥2 aggregate atomic | `IUnitOfWork` | Một transaction bao ngoài |
| Đánh thức consumer tức thì (producer/consumer) | `Channel<T>` (coalescing signal) | `OutboxSignal` đánh thức dispatcher, poll làm lưới |
| Buffer quan sát lock-free (multi-writer) | `ConcurrentQueue<T>` (ring) | `OutboxActivityLog` + `/api/diagnostics/outbox-activity` |
| **Tránh** | `.Result`/`.Wait()` | Deadlock / starvation |

> **Channel vs ConcurrentQueue:** Channel = **hand-off** producer/consumer (mỗi item consume 1 lần, có backpressure/await); ConcurrentQueue = **shared buffer** lock-free (giữ, trim, đọc nhiều lần, không có consumer async). Hai công cụ bổ sung — xem [async-programming.md](async-programming.md).

---

<a name="phần-b"></a>
## Phần B — Kiến trúc & Design Patterns

### B1. Clean Architecture (+ Dependency Inversion)

**Định nghĩa:** một cách sắp xếp code thành các **vòng đồng tâm**, với một luật duy nhất: **mọi phụ thuộc (mũi tên reference) chỉ được chỉ VÀO TRONG**. Trung tâm là **business rule** (Domain) — nó **không biết** mình đang được lưu ở đâu, gọi bởi framework nào. Vòng ngoài (API, DB, UI) là **chi tiết thay thế được**.

**Hiểu lầm phải đập bỏ — "luồng gọi" ≠ "chiều reference":**
- **Luồng gọi lúc chạy:** `API → Domain → IRepository → DB` (dữ liệu tới DB **sau cùng**).
- **Chiều reference lúc biên dịch:** `API → Application → Infrastructure → Domain`. Tức Infrastructure **reference** Domain, **không** ngược lại.
- Người mới tưởng "Infra có DB nên Infra ở trong cùng" → SAI. **DB là tầng NGOÀI CÙNG** (thay được); "trong cùng" là business = Domain.

**Cơ chế làm được điều đó — Dependency Inversion:** Domain **định nghĩa interface** (`IOrderWriteRepository`), Infrastructure **implement**. Nhờ vậy mũi tên phụ thuộc đi **ngược** luồng gọi:
```csharp
// Domain/Interfaces/Outbound/IOrderWriteRepository.cs  — Domain SỞ HỮU hợp đồng
public interface IOrderWriteRepository { Task AddAsync(Order o, CancellationToken ct = default); ... }

// Infrastructure.*/Repositories/Write  — Infra implement, Domain KHÔNG biết file này tồn tại
internal sealed class SqlOrderWriteRepository : IOrderWriteRepository { ... }
```
Phép thử đúng/sai kiến trúc: *"đổi SQL Server → MongoDB, project Domain có đổi dòng nào không?"* — Đúng thì **không**, chỉ thêm một `Infrastructure.*` và đổi 1 dòng DI ở `Program.cs`.

**Áp dụng:** project `Domain` không ref project nào (chỉ ref `DI.Abstractions` để tự đăng ký service). 4 tầng: `API → Application → Domain ← Infrastructure`. Composition root `API/Program.cs` chọn provider.

- **Ưu:** đổi hạ tầng/DB không lan vào nghiệp vụ; test Domain **không cần** DB/web; nghiệp vụ sống lâu độc lập framework.
- **Nhược:** **nhiều interface + boilerplate** (mỗi cổng một interface + impl); overkill cho app CRUD nhỏ; người mới nhầm hướng phụ thuộc; đổi *chữ ký* command vẫn lan ra ngoài (Clean Arch chỉ cô lập *chi tiết hạ tầng*, không phải mọi thay đổi).
- **Khi dùng:** hệ nghiệp vụ phức tạp, sống lâu, nhiều backend thay thế được. **Khi KHÔNG:** script/CRUD nhỏ, prototype.

---

### B2. DDD tactical — Aggregate, Value Object, Domain Event

**Định nghĩa:**
- **Aggregate (Aggregate Root):** một **cụm** entity được coi là **một đơn vị nhất quán**, có một "gốc" (root) là cửa vào duy nhất. Vd `Order` là root, ôm `OrderDetail[]`. Luật vàng: aggregate khác **chỉ tham chiếu nhau bằng Id**, không ôm object của nhau → tránh dữ liệu lỗi thời và ranh giới transaction rõ ràng. Vì thế `Order` **không** chứa `CustomerName`, chỉ `CustomerId`.
- **Value Object:** object **bất biến**, **không có Id**, so sánh theo **giá trị** (hai `Money(100, "VND")` là bằng nhau). Vd `Money`, `Sku`, `Address`, `Size`. Nó **tự bảo vệ tính hợp lệ** khi tạo (`Sku.Create` chuẩn hoá + validate).
- **Domain Event:** một object ghi "**việc nghiệp vụ đã xảy ra**" (`OrderConfirmedEvent`), phát ra **bên trong** Domain khi trạng thái đổi. Là quá khứ, bất biến.

**Vấn đề nó giải:** gom invariant (bất biến nghiệp vụ) vào **một chỗ** để không bị phá vỡ rải rác. Vd "đơn phải có ≥1 dòng mới `Confirm`", "không `Ship` đơn `Draft`" — đặt **trong** `Order`, không cho code ngoài sửa trạng thái tùy tiện.

**Cơ chế/Ví dụ:**
```csharp
public sealed class Order : AggregateRoot
{
    private readonly List<OrderDetail> _details = new();
    public OrderStatus Status { get; private set; }          // set PRIVATE -> chỉ Order tự đổi
    public void AddDetail(Product p, int qty) { /* snapshot giá tại thời điểm mua */ }
    public void Confirm() {                                    // invariant tập trung ở đây
        if (Status != OrderStatus.Draft) throw new DomainException(...);
        Status = OrderStatus.Confirmed;
        Raise(new OrderConfirmedEvent(Id));                    // domain event
    }
}
```

**Áp dụng:** `Domain/Orders/Order.cs`, `Domain/ValueObjects/*` (`Money`, `Sku`, `Address`), `Domain/Orders/Order*Event.cs`.

- **Ưu:** invariant không thể bị lách; Value Object diệt cả lớp bug (SKU/tiền sai định dạng); tham chiếu bằng Id → dữ liệu không lỗi thời.
- **Nhược:** cần **read-model riêng** để hiển thị (aggregate thiếu `CustomerName` → sinh ra CQRS ở B3); học DDD tốn thời gian; dễ vẽ sai ranh giới aggregate (quá to = khoá nhiều, quá nhỏ = mất nhất quán).
- **Khi dùng:** nghiệp vụ nhiều quy tắc + trạng thái. **Khi KHÔNG:** dữ liệu phẳng gần như CRUD.

---

### B3. CQRS — tách đường Đọc khỏi đường Ghi

**Định nghĩa:** **C**ommand **Q**uery **R**esponsibility **S**egregation — tách **mô hình ghi** khỏi **mô hình đọc** vì chúng có **động cơ khác nhau**:
- **Ghi (Command):** trả **aggregate** đầy đủ để **thực thi nghiệp vụ** + enforce invariant. Không ghép bảng (vỡ ranh giới aggregate).
- **Đọc (Query):** trả **read-model phẳng** (`*View`) tối ưu hiển thị — JOIN/GROUP BY thoải mái, không cần invariant.

**Vấn đề nó giải:** `Order` (aggregate) chỉ có `CustomerId`. Màn hình cần "đơn của **ai**, **ai** bán, khách đã mua **bao nhiêu** đơn" = ghép nhiều bảng. Nếu ép aggregate ôm hết → phá DDD. → Tạo **đường đọc riêng**.

**Cơ chế (các cặp interface trong dự án):**
| | Ghi | Đọc |
|---|---|---|
| Application | `*Command` + Handler | `*Query` + Handler |
| Inbound port | `I*WriteService` | `I*ReadService` |
| Domain service | `*WriteService` (business rule) | `*ReadService` (forward) |
| Outbound port | `I*WriteRepository` → aggregate | `I*ReadRepository` → `*View` |
| Ghép nhiều bảng | ❌ | ✅ JOIN/GROUP BY |

**Áp dụng:** `Domain/ReadModels/*View`, `I*ReadService`/`I*ReadRepository`. Mongo còn tách tới **kết nối**: ghi dùng `MongoAppDbContext` (primary), đọc dùng `MongoReadDbContext` (`readPreference=secondaryPreferred`) → đọc vào **secondary** của replica set, giảm tải primary (đánh đổi eventual consistency).

- **Ưu:** mỗi bên tối ưu & tiến hoá độc lập; đọc không "mượn" được method ghi; read-model có thể denormalize cho nhanh.
- **Nhược:** nhiều interface/class; hai mô hình phải đồng bộ ý nghĩa; đọc chéo store phức tạp (in-memory join — xem C2); nếu tách tới read-DB riêng thì có độ trễ đồng bộ.
- **Khi dùng:** đọc và ghi khác hình dạng rõ rệt; báo cáo, dashboard. **Khi KHÔNG:** CRUD 1-1 đơn giản (tách chỉ thêm phiền).

---

### B4. Repository + Unit of Work

**Định nghĩa:**
- **Repository:** cổng trừu tượng "bộ sưu tập aggregate", che DB thật. Domain thấy `AddAsync/GetByIdAsync/UpdateAsync`, không thấy EF/Mongo.
- **Unit of Work:** gói **nhiều thao tác ghi** (qua nhiều repository) vào **một transaction** — tất-cả-hoặc-không.

**Vấn đề UoW giải:** `SaveChanges` chỉ atomic **trong một lần gọi**. Use-case `ConfirmAsync` ghi **2 aggregate** (`Product` trừ kho **và** `Order` đổi trạng thái = 2 `SaveChanges` ở 2 repo). Cần transaction bao ngoài để cả hai cùng thành/bại.

**Cơ chế (Begin → CRUD → Commit → Rollback → End):**
```csharp
await using var tx = await myUnitOfWork.BeginTransactionAsync(ct);   // Begin
try {
    await myProductWriteRepository.UpdateRangeAsync(products, ct);   // các repo CHUNG 1 DbContext
    await myOrderWriteRepository.UpdateAsync(order, ct);             // -> enlist chung transaction
    await tx.CommitAsync(ct);                                        // Commit: làm bền
} catch { await tx.RollbackAsync(ct); throw; }                       // Rollback: hoàn tác
// await using -> DisposeAsync ("End"): tự rollback nếu chưa commit
```
`SqlServerUnitOfWork` map sang EF transaction; `MongoUnitOfWork` map sang **multi-document transaction** (Mongo cần **replica set**). Cùng port `IUnitOfWork`, Domain viết **một lần**, chạy cả hai backend. Use-case ghi **1** aggregate (vd `ShipAsync`) thì **không** cần UoW.

- **Ưu:** Domain không dính EF/Mongo; transaction đa-aggregate gọn; đổi store dễ.
- **Nhược:** repo generic dễ phình thành "God object"; **leaky abstraction** nếu lộ `IQueryable`; UoW che mất chi phí transaction (dễ mở transaction quá rộng); Mongo bắt buộc replica set.
- **Khi dùng:** cần trừu tượng lưu trữ + ghi nhiều aggregate atomic. **Khi KHÔNG:** app nhỏ dùng thẳng ORM cũng được.

---

### B5. Mediator (MediatR) + Pipeline Behavior

**Định nghĩa:** **Mediator** = một trung gian; thay vì Controller gọi thẳng service/repo, nó **gửi một object Command/Query** qua `ISender.Send(...)`, MediatR tìm đúng **Handler** xử lý. **Pipeline Behavior** = các lớp bọc quanh handler cho concern cắt ngang (validation, logging, transaction) — như middleware nhưng cho request nội bộ.

**Vấn đề nó giải:** (1) tách người gửi khỏi người xử lý (Controller mỏng, không biết service nào); (2) gom **validation** vào **một** chỗ chạy tự động, khỏi rải `if` khắp controller.

**Cơ chế:**
```csharp
// Controller: KHÔNG new entity, KHÔNG gọi repo, KHÔNG gọi thẳng service
var result = await mySender.Send(new PlaceOrderCommand(...), ct);

// Pipeline: ValidationBehavior chạy TRƯỚC handler, gom mọi IValidator<TRequest>
public sealed class ValidationBehavior<TReq,TRes> : IPipelineBehavior<TReq,TRes> {
    public async Task<TRes> Handle(TReq req, RequestHandlerDelegate<TRes> next, CancellationToken ct) {
        // chạy validators -> lỗi thì throw ValidationException (middleware map 400)
        return await next();   // hợp lệ -> đi tiếp tới handler
    }
}
```
Quy ước dự án: **một file cho một feature** — `*Command` + `*Validator` + `*Handler` chung một `.cs`. Handler **mỏng**, chỉ điều phối rồi gọi Domain Service.

**Áp dụng:** mọi Controller dùng `ISender`; `Application/Common/Behaviors/ValidationBehavior`; đăng ký ở `AddApplicationServices()`. Domain **không** ref MediatR (chỉ Application ref).

- **Ưu:** Controller mỏng, decoupled; validation/logging/transaction một chỗ đồng nhất; thêm behavior mới không sửa handler.
- **Nhược:** thêm **một lớp gián tiếp** (khó lần "ai xử lý cái này" — phải tìm handler); over-engineering cho CRUD nhỏ; lạm dụng behavior làm luồng ẩn khó debug; "một request một handler" đôi khi gò bó.
- **Khi dùng:** nhiều use-case, muốn pipeline đồng nhất. **Khi KHÔNG:** ít endpoint, gọi service thẳng đơn giản hơn.

---

### B6. Strategy / Provider — hoán đổi hạ tầng lúc chạy

**Định nghĩa:** cùng **một tập interface**, có **nhiều bộ implement**; chọn bộ nào lúc chạy qua cấu hình. (Là **Strategy pattern** ở quy mô cả tầng hạ tầng.)

**Cơ chế:**
```csharp
// Program.cs — chỗ DUY NHẤT biết đang dùng DB nào
var provider = builder.Configuration["DatabaseProvider"]; // "SqlServer" | "MongoDB" | "Hybrid"
if (provider == "SqlServer") services.AddSqlServerInfrastructure(config);
else if (provider == "Hybrid") services.AddSqlServerSagaInfrastructure(config).AddMongoSaga...(config);
else services.AddMongoInfrastructure(config);
```
Mỗi provider gói mình trong một `AddXxxInfrastructure()`.

**Áp dụng:** 3 provider — `SqlServer` thuần, `MongoDB` thuần, `Hybrid` (Saga: Order/Payment ở SQL + phần còn lại ở Mongo).

- **Ưu:** đổi/thêm backend không đụng Domain/Application; test A-B; migration dần.
- **Nhược:** phải giữ **nhiều impl đồng bộ** cùng hợp đồng (thêm method interface = sửa mọi provider); dễ lệch hành vi tinh vi giữa các provider.
- **Khi dùng:** cần hỗ trợ nhiều DB/nhà cung cấp. **Khi KHÔNG:** chắc chắn một backend duy nhất mãi mãi.

---

### B7. Adapter / Gateway — bọc dịch vụ ngoài

**Định nghĩa:** **Adapter** biến API của một hệ ngoài về đúng interface Domain mong đợi. **Gateway** là adapter cho hệ ngoài (email, PDF, cổng thanh toán). Thường có **bản fake** (dev/test) và **bản thật**.

**Vấn đề nó giải:** Domain không nên biết MailKit/QuestPDF/VNPay; nếu đổi nhà cung cấp email thì nghiệp vụ không được đổi. → Định nghĩa `IEmailSender` ở Domain, cắm impl ở ngoài.

**Cơ chế:**
```csharp
// Domain: hợp đồng
public interface IEmailSender { Task SendAsync(EmailMessage m, CancellationToken ct); }
// Infra: bản thật (SMTP) và bản fake; chọn qua config Gateways:UseReal
```

**Áp dụng:** `IEmailSender` (SMTP/MailKit), `IInvoiceIssuer` (PDF/QuestPDF), `IPaymentGateway` (VNPay, project `Infrastructure.Payments`). Toggle `Gateways:UseReal`.

- **Ưu:** test bằng fake không cần dịch vụ thật; đổi nhà cung cấp không đụng nghiệp vụ; cô lập lỗi/định dạng bên ngoài.
- **Nhược:** phải **map lỗi/format** của bên ngoài về Domain (dễ rò rỉ khái niệm lạ); giữ fake và real đồng hành vi; nhà cung cấp đổi API thì adapter phải theo.
- **Khi dùng:** mọi tích hợp ngoài (email, SMS, PDF, thanh toán). **Khi KHÔNG:** logic thuần nội bộ.

---

### B8. Outbox Pattern — an toàn "dual-write"

**Định nghĩa:** khi một use-case vừa muốn **đổi DB** vừa muốn **phát event ra broker**, đó là **hai** thao tác trên **hai** hệ không có transaction chung → rủi ro. **Outbox** biến "publish" thành một **dòng INSERT vào bảng trong CÙNG DB**, cùng transaction với thay đổi nghiệp vụ. Sau đó một tiến trình nền đọc bảng và đẩy lên broker.

**Vấn đề nó giải (bài toán dual-write):**
- Commit DB xong, publish **lỗi** → **mất event** (khách đặt hàng nhưng không ai gửi mail).
- Publish xong, commit DB **rollback** → **event ma** (gửi mail cho đơn không tồn tại).

**Cơ chế:**
```
OrderWriteService.Confirm()
  └─ Order.Confirm() -> Raise(OrderConfirmedEvent)
     └─ SqlOrderWriteRepository.UpdateAsync()
          ├─ OutboxWriter.Stage(order)  -> INSERT OutboxMessages  ┐ CÙNG
          └─ SaveChangesAsync()         -> UPDATE Orders          ┘ 1 transaction (atomic)
   ... sau đó ...
OutboxDispatcher (nền) đọc dòng chưa publish -> đẩy Kafka/RabbitMQ -> đánh dấu đã publish
```
Giao **at-least-once** (broker chết thì dòng còn đó, tick sau retry — không mất). Vì có thể **publish trùng**, consumer phải **idempotent** (xử lý lại vẫn đúng).

**Áp dụng:** Outbox SQL (Order/Payment) + Outbox Mongo (Product), `OutboxRouter` gắn `Destination` cho từng dòng, `OutboxDispatcher` drain **cả hai**. Chi tiết [outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md).

- **Ưu:** **không mất event** dù broker chết; event atomic với thay đổi nghiệp vụ; app vẫn chạy khi broker tắt.
- **Nhược:** có **độ trễ** (poll theo tick); phải **dọn** bảng outbox; consumer **bắt buộc idempotent**; at-least-once = có thể trùng; thêm cơ sở hạ tầng (bảng + dispatcher).
- **Khi dùng:** **mọi lúc** phát event/side-effect gắn với thay đổi DB. **Khi KHÔNG:** không có event ra ngoài.

---

### B9. Saga — giao dịch dài trải nhiều store (+ Compensation + Pivot)

**Định nghĩa:** khi một nghiệp vụ phải ghi vào **nhiều DB/service** mà **không có** distributed transaction (2PC) — vd SQL Server + MongoDB — ta chia thành **chuỗi bước**, **mỗi bước commit cục bộ**. Nếu một bước sau **lỗi**, saga chạy **compensation** (hành động bù) để **hoàn tác** các bước đã commit trước đó. Đây là nhất quán **cuối cùng** (eventual), không phải tức thời.

**Khái niệm cốt lõi:**
- **Compensatable step:** bước có thể **hoàn tác** (vd trừ kho → bù bằng cộng kho lại).
- **Pivot:** **điểm không quay lại**. Trước pivot = compensatable; **sau** pivot chỉ được **tiến tới** (retryable-forward), không hoàn tác nữa.
- **Sổ cái (ledger):** ghi mọi bước + dữ liệu bù để **khôi phục sau sự cố** (crash recovery).

**Cơ chế trong dự án (provider Hybrid):**
```
1. Mongo: trừ kho Product        (COMPENSATABLE, trước pivot) — bù = cộng kho lại
2. SQL:   commit Order + Payment  (**PIVOT** — điểm không quay lại)
3.        publish event           (RETRYABLE-FORWARD, sau pivot — chỉ retry tiến tới)

Lỗi ở bước 2? -> replay compensation bước 1 (hoàn kho).
Qua pivot rồi lỗi bước 3? -> KHÔNG hoàn tác, chỉ retry publish.
```
**Một sổ cái** `saga_instances` đặt ở **Mongo**; rollback có retry + **pivot-guard**. Chi tiết [saga-hybrid.md](saga-hybrid.md).

- **Ưu:** đạt nhất quán across-store **không cần** distributed transaction (thứ vừa khó vừa chậm); chịu lỗi + khôi phục sau crash.
- **Nhược:** **phức tạp cao** (mỗi bước cần compensation đúng); **nhất quán tạm thời lệch** (vd fact `ProductStockChanged` publish trước pivot, nếu rollback thì analytics lệch trong ca hiếm); khó test/khó suy luận; compensation không phải lúc nào cũng "hoàn tác sạch" (đã gửi mail thì không thu hồi được).
- **Khi dùng:** ghi trải **nhiều DB/service** bắt buộc nhất quán. **Khi KHÔNG:** một store duy nhất (dùng `IUnitOfWork` — B4 — đơn giản hơn nhiều).

---

### B10. Process Manager — "sự kiện → lệnh"

**Định nghĩa:** một thành phần **nghe fact** ("việc đã xảy ra", từ Kafka) rồi **phát command** ("giờ hãy làm việc này", sang RabbitMQ) để đẩy quy trình nhiều bước đi tiếp. Khác Saga ở chỗ Saga điều phối **một** giao dịch; Process Manager điều phối **quy trình dài** phản ứng theo sự kiện.

**Cơ chế:**
```
Kafka fact OrderConfirmed  ──▶  [Process Manager / Consumer]  ──▶  RabbitMQ command SendEmail
                                                                ──▶  RabbitMQ command SendNotification
```

**Áp dụng:** consumer Kafka → worker RabbitMQ (email/invoice/notification/restock) trong `Infrastructure.Messaging`.

- **Ưu:** tách bạch "đã xảy ra" khỏi "phải làm gì"; dễ tách microservice sau; thêm phản ứng mới = thêm consumer, không sửa nguồn.
- **Nhược:** thêm bậc gián tiếp; phải quản lý **trạng thái quy trình** (đang ở bước nào); khó theo dõi end-to-end.
- **Khi dùng:** quy trình nhiều bước phản ứng theo event. **Khi KHÔNG:** side-effect đơn giản một nhịp.

---

### B11. Decorator — thêm hành vi mà không sửa lớp gốc

**Định nghĩa:** một lớp **cùng interface** với lớp gốc, **bọc** lớp gốc và **thêm** hành vi (resilience, cache, log) **trước/sau** khi gọi gốc. Giữ **Single Responsibility** (gốc chỉ lo việc gốc) + **Open/Closed** (thêm tính năng không sửa gốc).

**Vấn đề nó giải:** muốn thêm retry/circuit-breaker/fallback vào việc đọc chéo store, nhưng **không** muốn nhét Polly vào `MongoCrossDbDirectory` (làm nó trộn 2 trách nhiệm + khó test) và **không** muốn sửa 8 repo đang gọi nó.

**Cơ chế:**
```csharp
public sealed class ResilientCrossDbDirectory : ICrossDbDirectory   // CÙNG interface
{
    private readonly ICrossDbDirectory myInner;      // lớp GỐC (đọc Mongo thật)
    private readonly ResiliencePipeline myPipeline;  // retry+breaker+timeout
    public Task<string?> GetCustomerNameAsync(Guid id, CancellationToken ct = default) =>
        RunAsync(t => myInner.GetCustomerNameAsync(id, t), fallback: null, ...);  // bọc + fallback
}
// DI: consumer vẫn chỉ biết ICrossDbDirectory. Bật/tắt resilience = đổi 1 dòng đăng ký.
```

**Áp dụng:** `ResilientCrossDbDirectory` / `ResilientCrossDbOrderStats` (trong `Saga.Core/CrossDb`) bọc adapter đọc chéo store + fallback "(unknown)"/0.

- **Ưu:** lớp gốc thuần, test độc lập; consumer không đổi (đổi adapter không đụng người gọi); xếp chồng nhiều decorator được.
- **Nhược:** nhiều lớp bọc = nhiều class; xếp chồng sâu khó lần thứ tự; phải cùng interface (interface rộng thì bọc mệt).
- **Khi dùng:** thêm cross-cutting (retry/cache/log/metrics) quanh một port. **Khi KHÔNG:** chỉ một chỗ dùng, sửa thẳng đơn giản hơn.

---

### B12. Resilience — Retry / Circuit Breaker / Timeout / Bulkhead (Polly)

**Định nghĩa:** **Polly** là thư viện .NET thêm **khả năng chịu lỗi** quanh lời gọi ra ngoài. Bốn chiến lược chính, ghép thành **một `ResiliencePipeline`** (kiểu "củ hành", thêm trước = ngoài cùng):

| Chiến lược | Làm gì | Chống cái gì |
|---|---|---|
| **Retry** | Thử lại vài lần (backoff+jitter) | Blip mạng tạm thời |
| **Circuit Breaker** | Ngắt cầu dao theo **tỉ lệ lỗi** | Dependency **chết** kéo sập dây chuyền |
| **Timeout** | Bỏ một call quá lâu | Call treo ghim thread |
| **Bulkhead** | Giới hạn **số call đồng thời** | Dependency **chậm** làm cạn pool |

**Circuit Breaker — 3 trạng thái (cốt lõi):**
```
        lỗi vượt ngưỡng
 CLOSED ───────────────► OPEN ──(hết BreakDuration)──► HALF-OPEN ──thành công──► CLOSED
 (đếm lỗi)              (fail-fast,                    (thử 1 phát dò)   ──lỗi──► OPEN
                        không gọi thật)
```
Điểm dễ nhầm: breaker **không** ngắt từ lỗi đầu tiên — cần **đủ lỗi** trong cửa sổ (vd ≥50% trong 10s, tối thiểu N call) mới OPEN. OPEN thì ném `BrokenCircuitException` **ngay** (fail-fast, không chờ timeout).

**Bulkhead vì sao cần dù đã có breaker:** breaker đếm **lỗi**; nhưng dependency **vẫn trả lời, chỉ RẤT CHẬM** (mỗi mail 8–10s) thì breaker **không nhảy** (call vẫn "thành công"), số call đồng thời phình lên → cạn thread/connection pool → phần khoẻ cũng đơ. Bulkhead (`ConcurrencyLimiter` = semaphore) chặn theo **số lượng đồng thời** → cô lập. **Breaker = trục tỉ lệ lỗi; Bulkhead = trục số đồng thời → bổ sung nhau.**

**Thứ tự pipeline:** `bulkhead → retry → breaker → timeout` (retry **ngoài** breaker để retry cũng bị tính vào breaker, và khi breaker OPEN thì retry dừng ngay).

**Áp dụng:** `Infrastructure.Messaging/Resilience/DownstreamResilience.cs` (gateway email/invoice/notification) + `Infrastructure.Saga.Core/Resilience/CrossStoreResilience.cs` (đọc chéo store). **QUY TẮC VÀNG: chỉ bọc READ idempotent, KHÔNG bọc WRITE của saga** — retry một write không idempotent = trừ kho **2 lần**. Write của saga để **saga** tự lo retry/rollback.

- **Ưu:** chặn **cascading failure** (breaker) + **resource exhaustion** (bulkhead); fallback graceful (đơn vẫn hiện, tên "(unknown)"); monolith vẫn cần (chỗ ra ngoài tiến trình mới "chết").
- **Nhược:** **chỉnh tham số khó** (ngưỡng sai = ngắt oan hoặc không ngắt kịp); retry sai chỗ (write không idempotent) = nhân đôi tác dụng phụ; thêm độ phức tạp + latency nhỏ; fallback che dữ liệu (phải catch **hẹp** đúng `BrokenCircuit`/`Timeout`, đừng nuốt mọi lỗi).
- **Khi dùng:** **mọi** lời gọi ra ngoài tiến trình (DB store khác, broker, SMTP, cổng thanh toán), **idempotent**. **Khi KHÔNG:** call in-memory; write không idempotent (để saga lo).

---

### B13. Options Pattern

**Định nghĩa:** gói tham số cấu hình vào một record/class (`*Options`), nạp từ `appsettings`/env, inject qua `IOptions<T>` → **đổi cấu hình không sửa code**.

**Áp dụng:** `CrossStoreResilienceOptions` (ngưỡng breaker chỉnh qua `AddCrossStoreResilience(o => ...)`), `Gateways:UseReal`, connection strings, `DatabaseProvider`.

- **Ưu:** tinh chỉnh không build lại; một nơi khai báo mặc định; test dễ (truyền options giả).
- **Nhược:** cần **validate** cấu hình (sai key = lỗi lúc chạy); tản mát cấu hình nếu lạm dụng.

---

<a name="phần-c"></a>
## Phần C — So sánh công nghệ

### C1. Kafka vs RabbitMQ vs Redis

Dự án dùng **cả Kafka + RabbitMQ** (không phải chọn-một — mỗi cái một thế mạnh, chạy đồng thời). **Redis KHÔNG có trong code** (grep = 0 file) — liệt kê để so sánh và gợi ý chỗ hợp nếu sau này thêm.

**Ý niệm gốc phân biệt Kafka vs RabbitMQ:**
- **Kafka = cuốn nhật ký (log) của "những việc đã xảy ra".** Ghi **fact** bất biến, giữ lại lâu, **nhiều bên đọc độc lập**, tua lại (replay) được. Trả lời câu hỏi *"chuyện gì đã xảy ra?"*.
- **RabbitMQ = hàng đợi công việc.** Mang **command** "hãy làm việc này", giao cho **đúng một** worker, worker **ack** khi xong, lỗi thì **retry / Dead-Letter Queue**. Trả lời *"giờ phải làm gì?"*.

| Tiêu chí | **Kafka** *(đang dùng)* | **RabbitMQ** *(đang dùng)* | **Redis** *(chưa dùng)* |
|---|---|---|---|
| Bản chất | Distributed **log** / event streaming | **Message broker** (queue) | In-memory data store (+pub/sub, streams) |
| Loại message | **Fact**: OrderConfirmed | **Command**: SendEmail | value/lock/counter/pub-sub nhẹ |
| Lưu & tua lại | Log **bền, replay được** | Xoá sau ack; có DLQ | RAM (tùy chọn bền); pub/sub **không** replay |
| Nhiều người đọc | **N consumer group** đọc độc lập cùng log | Cạnh tranh — **1 worker** lấy 1 message | client bất kỳ |
| Giao hàng | at-least-once, ordering theo **partition** | ack từng cái, **retry + Dead-Letter** | fire-and-forget (pub/sub) |
| Throughput | Rất cao (log tuần tự) | Cao | Rất cao (RAM) — nhưng không phải broker bền |
| Trong dự án | `orders.events`, `payments.events`, `products.events` | `email.send`, `invoice.issue`, `notification.send`, `restock.alert` | — |

**Redis hợp chỗ nào (nếu thêm):** **cache** read-model/report (giảm tải DB); **distributed lock** (khoá idempotency cho outbox/consumer); **rate limiting**; **session store**; **pub/sub thời gian thực nhẹ** (không cần bền/replay). Redis **không** thay được Kafka (không có log replay + consumer group) hay RabbitMQ (không ack/retry/DLQ bền) cho nghiệp vụ này — nó **bổ sung** (tốc độ/cache/khóa), không thay thế.

> **Định tuyến tại nguồn:** `OutboxRouter` gắn `Destination` cho từng dòng outbox → fact bắn **thẳng** Kafka, command bắn **thẳng** RabbitMQ. **Không** có cầu Kafka→RabbitMQ. Kafka không làm phễu trung chuyển.

### C2. SQL Server vs MongoDB (EF Core cho cả hai)

| | **SQL Server** | **MongoDB** |
|---|---|---|
| Mô hình dữ liệu | Chuẩn hoá nhiều bảng + khoá ngoại: `OrderRecord` + `OrderItemRecord` | Nhúng 1 document: `OrderDocument` lồng `Items[]` |
| Đọc ghép nhiều thực thể | LINQ `join` → **một câu SQL JOIN** chạy trên DB | Không JOIN quan hệ → **nạp rồi ghép in-memory** (hoặc `$lookup`/denormalize) |
| Transaction đa document | Chạy ngay trên instance thường | **Cần replica set** (standalone `mongod` throw khi `BeginTransaction`) |
| Migration schema | EF Migrations | Provider Mongo **không** migration — schema theo nhu cầu, map bằng `ToCollection` |
| Điểm mạnh | Quan hệ phức tạp, JOIN, giao dịch mạnh | Ghi/đọc document nhanh, linh hoạt schema, scale ngang |
| Vai trò trong Hybrid | **Pivot**: Order/Payment | Compensatable: Product/Customer/Employee + sổ cái saga |

Ý nghĩa lớn: **cùng interface repository**, mỗi store tự chọn chiến lược thực thi → minh hoạ luận điểm Clean Arch "đổi lưu trữ, hợp đồng không đổi".

### C3. Monolith vs Microservice

- Dự án = **modular monolith**: một API host + nhiều class library; mọi consumer chạy **in-process**.
- **Đường lên microservice đã chừa sẵn:** Outbox + Kafka là **xương sống tích hợp**; **integration event** (phẳng, versionable) là **hợp đồng** giữa service, đã tách khỏi domain event. Tách "Notification/Analytics Service" = bê worker/consumer-group ra process riêng — **không** viết lại Domain, chỉ "đem một consumer group đi chạy chỗ khác".
- **Ưu monolith:** deploy/debug đơn giản, transaction cục bộ dễ, một codebase. **Nhược:** scale theo cụm khó, một lỗi có thể ảnh hưởng cả host (nên cần circuit breaker + bulkhead ở ranh giới ra ngoài).
- **Khi tách micro:** khi các phần có nhịp scale/deploy khác nhau rõ rệt, đội tách biệt. Chưa cần thì **đừng** tách sớm (gánh phức tạp phân tán mà không lợi).

---

<a name="phần-d"></a>
## Phần D — Bảng tra: bài toán → pattern

| Bài toán | Pattern / công nghệ | Chỗ trong dự án |
|---|---|---|
| Đổi DB không đụng nghiệp vụ | Clean Arch + Dependency Inversion + Provider | `Domain` interfaces + `AddXxxInfrastructure` |
| Quy tắc nghiệp vụ phức tạp, nhiều trạng thái | DDD Aggregate + Domain Service | `Order`, `*WriteService` |
| Hiển thị ghép nhiều bảng | CQRS read-model | `*ReadService` / `*View` |
| Ghi ≥2 aggregate atomic (1 store) | Unit of Work + transaction | `OrderWriteService.ConfirmAsync` |
| Ghi trải **nhiều store** | Saga + compensation + pivot | provider Hybrid, `saga_instances` |
| Phát event không mất khi broker chết | Outbox | Outbox SQL + Mongo, `OutboxDispatcher` |
| "Đã xảy ra" cho nhiều bên, tua lại | Kafka | `*.events` topics |
| "Phải làm việc này", ack + retry + DLQ | RabbitMQ | `email.send`, `invoice.issue`... |
| Phản ứng nhiều bước theo sự kiện | Process Manager | consumer Kafka → worker RabbitMQ |
| Dependency **chết** kéo sập dây chuyền | Circuit Breaker (Polly) | cross-store reads, gateway |
| Dependency **chậm** làm cạn pool | Bulkhead (ConcurrencyLimiter) | pipeline gateway + cross-store |
| Thêm resilience không sửa adapter gốc | Decorator | `ResilientCrossDb*` |
| Validate request tự động một chỗ | Mediator + Pipeline Behavior | `ValidationBehavior` |
| Gọi email / PDF / thanh toán | Adapter/Gateway (fake↔real) | `IEmailSender`, `IInvoiceIssuer`, `IPaymentGateway` |
| Chờ nhiều I/O độc lập (khác store) | `Task.WhenAll` | `SqlReportReadRepository` |
| Giới hạn số việc đồng thời | `SemaphoreSlim` | `AsyncParallel` |
| Việc CPU hàng loạt | `Parallel` / `Task.Run` | `ProductWriteService.BuildManyAsync` |
| Ghi/đọc nhiều bản ghi hiệu quả | Bulk + Chunk + connection pool | `CreateManyAsync` |
| Đổi cấu hình không sửa code | Options Pattern | `CrossStoreResilienceOptions`, `Gateways:UseReal` |
| (Nếu cần) cache / khóa / đếm nhanh | **Redis** *(chưa có)* | — gợi ý: cache report, idempotency lock |

---

## Phần E — Vận hành: NFR · Observability · Performance

Ba tài liệu chuyên sâu (đã tích hợp code + infra thật):

| Chủ đề | Định nghĩa ngắn | Đã có trong dự án | Doc |
|---|---|---|---|
| **NFRs** | yêu cầu "hệ thống hoạt động thế nào" (-ilities) | map Availability→breaker/bulkhead/health, Consistency→saga/outbox, Scalability→stateless/CQRS | [nfrs.md](nfrs.md) |
| **Observability** | Traces + Metrics + Logs | OTel traces (ASP.NET/HttpClient/EFCore/custom) + metrics (RED + runtime + outbox) + Prometheus `/metrics`; health `/health/{live,ready}` | [observability.md](observability.md) |
| **Performance testing** | Latency (p95/p99) · Throughput (RPS) · Error Rate | k6 script + thresholds; map bottleneck→đặc điểm dự án | [performance-testing.md](performance-testing.md) |

Chỉ **API** ref OTel SDK; tầng trong dùng BCL `ActivitySource`/`Meter` → không coupling vendor (đúng tinh thần Clean Arch). RED metrics tự động cho mọi command/query qua `TelemetryBehavior` (MediatR pipeline). Redis vẫn **chưa dùng** — chỗ hợp: cache report + distributed lock idempotency.

Infra: [deploy/observability/](../deploy/observability/) (Prometheus + Grafana + Alertmanager), [perf/k6/](../perf/k6/).

---

### Nguồn
Tổng hợp từ code (`SellingNewProduct.*`) + docs: ARCHITECTURE, CONVENTIONS, DOMAIN_MODEL, cqrs-saga-outbox, saga-hybrid, saga-files-reference, outbox-kafka-rabbitmq(+walkthrough), phase12-summary, polly-circuit-breaker, polly-bulkhead, gateway-email-smtp/invoice-pdf/vnpay, mongo-replica-set, async-concepts, async-programming.

