# Bất đồng bộ trong .NET — Định nghĩa & Vì sao dùng được

Tài liệu khái niệm, đi kèm `async-programming.md` (phần đã áp dụng vào code). Ở đây trả lời **cái gì** và **tại sao nó chạy được**, để hiểu gốc chứ không chỉ copy syntax.

---

## 1. Process → Thread → ThreadPool → Task

**Định nghĩa (từ thấp lên cao):**

- **Process**: một chương trình đang chạy, có vùng nhớ riêng.
- **Thread**: đơn vị thực thi nhỏ nhất trong process. OS lập lịch (schedule) theo thread. Mỗi thread có stack riêng (~1MB) → tạo thread **đắt**.
- **ThreadPool**: CLR giữ sẵn một "hồ" thread tái sử dụng. Xin việc bằng `ThreadPool.QueueUserWorkItem` / `Task.Run`; xong thì thread **trả về hồ**, không hủy. Tránh chi phí tạo/hủy thread liên tục.
- **Task**: lớp trừu tượng trên ThreadPool. Đại diện "một công việc sẽ hoàn thành trong tương lai" — có thể trả giá trị (`Task<T>`), hủy (`CancellationToken`), nối tiếp (`await`).

**Vì sao `Task` hơn `Thread` thủ công:**

| | `Thread` | `Task` |
|---|---|---|
| Chi phí tạo | Cao (stack riêng) | Thấp (mượn ThreadPool) |
| Trả giá trị | Không | `Task<T>` |
| Hủy | Thủ công | `CancellationToken` |
| Nối tiếp | `Join` thủ công | `await` |

> Trong project **không** thấy `new Thread(...)` — luôn dùng `Task`/`async`. Đó là chuẩn hiện đại.

---

## 2. `async` / `await` — cơ chế thật sự

**Hiểu sai phổ biến:** "async = chạy trên thread khác". **Sai.** `await` một tác vụ I/O **không** tốn thread nào trong lúc chờ.

**Cơ chế:** compiler biến method `async` thành một **state machine**. Khi gặp `await` một tác vụ chưa xong (VD chờ DB trả kết quả):

1. Method **trả điều khiển** về caller ngay tại đó.
2. Thread hiện tại được **giải phóng** về ThreadPool — đi phục vụ request khác.
3. Khi I/O xong (OS báo qua IO completion port), một thread bất kỳ trong pool **tiếp tục** method từ chỗ dừng.

**Vì sao dùng được / vì sao quan trọng:** một web server có ThreadPool giới hạn. Nếu mỗi request **block** thread để chờ DB (`.Result`/`.Wait()`), pool cạn thread → request mới phải xếp hàng → *thread starvation*. Với `await`, thread được trả lại trong lúc chờ → cùng số thread phục vụ được **nhiều** request hơn.

```csharp
// XẤU: block thread suốt thời gian chờ DB. Còn nguy cơ deadlock (sync-over-async).
var order = GetOrderAsync(id).Result;

// TỐT: await -> thread được trả về pool trong lúc chờ I/O
var order = await GetOrderAsync(id);
```

> **Quy tắc vàng:** async xuyên suốt (async all the way). Đừng trộn `.Result`/`.Wait()` giữa chừng.

---

## 3. I/O-bound vs CPU-bound — chọn công cụ

Hai loại "chậm" khác nhau, cách xử lý khác nhau:

- **I/O-bound** (chờ DB, mạng, file): thread **không làm gì**, chỉ chờ. → Dùng `async/await` + `Task.WhenAll` để chờ **nhiều** thứ cùng lúc mà không tốn thread.
- **CPU-bound** (tính toán, map/build hàng loạt): thread **bận** chạy. → Dùng nhiều thread thật: `Parallel.For` / `Task.Run` để chia cho nhiều nhân CPU.

**Vì sao phân biệt:** `Task.WhenAll` trên việc CPU-bound **không** tăng tốc nếu chỉ chạy trên 1 thread — nó chỉ chồng lấn thời gian **chờ**. Việc CPU cần thread thật (Parallel). Ngược lại, `Parallel.For` gọi I/O sẽ lãng phí thread ngồi chờ.

Trong project: reports = I/O-bound (`Task.WhenAll` cross-store); build sản phẩm lô lớn = CPU-bound (`Task.Run` + `SemaphoreSlim`).

---

## 4. `Task.WhenAll` — vì sao chồng lấn độ trễ

`Task.WhenAll(t1, t2)` **khởi động cả hai** rồi chờ cả hai xong. Nếu t1 mất 100ms (SQL) và t2 mất 80ms (Mongo) mà **độc lập**: tuần tự = 180ms, WhenAll ≈ max(100, 80) = 100ms.

**Điều kiện dùng được:** hai tác vụ phải **thật sự độc lập** và không chia sẻ tài nguyên không thread-safe (xem mục 7). Đó là lý do trong project chỉ `WhenAll` khi hai nhánh ở **hai DbContext khác nhau**.

---

## 5. `SemaphoreSlim` — vì sao cần throttle

`SemaphoreSlim(n)` = "vé vào cửa", tối đa `n` người vào cùng lúc. `WaitAsync()` xin vé (chờ nếu hết), `Release()` trả vé.

**Vì sao cần:** bắn 10.000 `Task` cùng lúc → ngập ThreadPool, cạn connection pool, hoặc dịch vụ ngoài (SMTP, API) chặn vì quá tải. Semaphore giới hạn số việc **đồng thời** ở mức an toàn.

```csharp
var throttle = new SemaphoreSlim(5); // tối đa 5 việc song song
await throttle.WaitAsync();
try     { await GoiApiNgoaiAsync(); }
finally { throttle.Release(); }      // LUÔN release, kể cả khi lỗi -> tránh rò rỉ vé/deadlock
```

> `SemaphoreSlim.WaitAsync()` **không block thread** (khác `Semaphore` cũ) → hợp với async. Đây là "bulkhead" — cùng ý tưởng với `ConcurrencyLimiter` đã dùng ở gateway pipeline (Polly).

---

## 6. Connection pooling & DbContext — "mở 1 connection, làm nhiều CRUD, rồi đóng"

Đây là phần bạn hỏi. Nó gắn với **Unit of Work** (`IUnitOfWork` trong project).

### 6.1 Vì sao có connection pool

Mở một connection tới DB **đắt**: bắt tay TCP, xác thực, TLS... (hàng chục ms). Nếu mỗi câu lệnh mở/đóng connection riêng → cực chậm. Nên ADO.NET/EF Core dùng **connection pool**: connection "đóng" thực chất được **trả về hồ** để tái dùng, không đóng vật lý.

### 6.2 DbContext = 1 connection (mượn) + 1 Unit of Work

Một `DbContext` (VD `AppDbContext`) khi cần sẽ **mượn 1 connection** từ pool, và giữ một **change tracker** — sổ ghi mọi thay đổi (Added/Modified/Deleted). Bạn có thể làm **nhiều thao tác CRUD** trên nó, tất cả gom vào sổ, **chưa** gửi DB. Đến khi `SaveChanges` mới **gửi một loạt** trong **một** round-trip/transaction.

```csharp
// Mượn 1 connection (khi cần), làm ĐẦY ĐỦ CRUD trên cùng context...
var order   = await db.Orders.FindAsync(id);   // Read
order.Confirm();                                // Update (ghi vào change tracker)
db.OrderDetails.Add(newDetail);                 // Create
db.OrderDetails.Remove(oldDetail);              // Delete

// ...rồi GỬI TẤT CẢ trong 1 lần. Batch -> ít round-trip, atomic.
await db.SaveChangesAsync();
// Hết scope request -> Dispose -> connection TRẢ về pool (không đóng vật lý).
```

**Vì sao gộp như vậy tốt:**
- **Ít round-trip**: 4 thao tác trên → EF gộp thành số câu lệnh tối thiểu trong 1 lần gửi.
- **Atomic**: `SaveChanges` chạy trong 1 transaction ngầm → tất cả thành công hoặc tất cả thất bại.
- **Rẻ**: connection mượn/trả từ pool, không mở/đóng vật lý mỗi câu.

### 6.3 Khi cần nhiều write cùng "all-or-nothing" → `IUnitOfWork`

Nếu muốn gộp nhiều repository/nhiều `SaveChanges` vào **một** transaction, project dùng `IUnitOfWork` — đúng mẫu Begin → CRUD → Commit → (Rollback nếu lỗi) → End:

```csharp
await using var tx = await myUnitOfWork.BeginTransactionAsync(ct); // Begin: mở transaction trên connection
try
{
    await myProductRepository.UpdateRangeAsync(products, ct); // nhiều write...
    await myOrderRepository.UpdateAsync(order, ct);
    await tx.CommitAsync(ct);   // Commit: làm bền
}
catch
{
    await tx.RollbackAsync(ct); // Rollback: hoàn tác
    throw;
}
// await using -> DisposeAsync ("End"): kết thúc scope, tự rollback nếu chưa commit
```

(Xem thật ở `OrderWriteService.PersistInTransactionAsync`.) Đây chính xác là ý "mở 1 connection, thực hiện đầy đủ CRUD, rồi đóng": connection/transaction mở một lần, dồn nhiều thao tác, commit, rồi trả về pool.

> Update nhiều bản ghi: ưu tiên `UpdateRangeAsync`/`AddRangeAsync` (1 `SaveChanges`) thay vì vòng lặp update từng cái (mỗi cái 1 round-trip). Lô cực lớn thì **chunk** (xem `async-programming.md` mục 4).

---

## 7. Vì sao DbContext KHÔNG thread-safe

Vì `DbContext` giữ **state có thể thay đổi**: change tracker + **một** connection đang dùng. Hai thao tác chạy **đồng thời** trên cùng context sẽ giẫm lên state đó / dùng chung 1 connection đang bận → EF ném:

> *"A second operation was started on this context before a previous operation completed."*

**Hệ quả (đã áp dụng trong project):**
- KHÔNG `Task.WhenAll` nhiều truy vấn trên **cùng** context → thay bằng **batch 1 query** hoặc tuần tự.
- Muốn song song thật nhiều truy vấn → mỗi nhánh cần **context riêng** (`IDbContextFactory`), hoặc nhánh ở **DB khác** (SQL vs Mongo) như reports.

---

## 8. Deadlock kinh điển — vì sao `.Result` nguy hiểm

`GetAsync().Result` **block** thread hiện tại để chờ. Trong context đồng bộ hóa (UI/ASP.NET cũ), continuation sau `await` cần **chính thread đang bị block** đó để chạy tiếp → chờ nhau vòng tròn → **deadlock**. ASP.NET Core hiện đại không còn SynchronizationContext nên bớt rủi ro, nhưng `.Result` vẫn **block thread** → starvation. → Luôn `await`.

---

## Tóm tắt một dòng

- Thread đắt → ThreadPool tái dùng → `Task` là mặt tiền tiện dụng.
- `await` **giải phóng thread** khi chờ I/O → server phục vụ nhiều request hơn.
- I/O-bound → `Task.WhenAll`; CPU-bound → `Parallel`/`Task.Run`.
- `SemaphoreSlim` giới hạn đồng thời (throttle/bulkhead).
- Connection pool + DbContext: **mở 1 lần, dồn CRUD, `SaveChanges`/Commit gộp, trả pool** — batch & atomic.
- DbContext không thread-safe → batch, đừng fan-out cùng context.
- Đừng `.Result`/`.Wait()` — deadlock/starvation.
