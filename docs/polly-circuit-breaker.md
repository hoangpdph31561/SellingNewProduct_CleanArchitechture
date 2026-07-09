# Polly.NET & Circuit Breaker — giải thích dễ hiểu + hướng dẫn

Tài liệu này giải thích **Polly** là gì, **circuit breaker** hoạt động ra sao, và dự án đang dùng nó ở
**đúng những chỗ nào**. Đọc xong bạn sẽ hiểu: tại sao một monolith vẫn cần circuit breaker, và tại sao
ta **chỉ** bọc read (đọc) chứ **không** bọc write (ghi) của saga.

> **Dự án này là monolith hay microservice?** → **Monolith** (một API host + nhiều class library).
> KHÔNG phải microservice. Nhưng circuit breaker vẫn có ích: nó bảo vệ mọi lời gọi ra **ngoài tiến
> trình** (database, message broker, SMTP, store khác…) — đó mới là chỗ "có thể chết", không phải code
> in-memory.

---

## 1. Vấn đề: một dependency chậm/chết kéo sập cả hệ thống

Hình dung worker gọi sang **MongoDB** để lấy tên khách. Mongo chết. Điều gì xảy ra nếu **không** có gì
bảo vệ?

- Mỗi request chờ **timeout** (vd 30s) rồi mới lỗi.
- Request dồn lại, **thread pool cạn**, hàng đợi phình to.
- Phần **SQL hoàn toàn khoẻ mạnh** cũng đơ theo, chỉ vì nó phải chờ Mongo.
- → Một dependency chết làm **sập dây chuyền** (cascading failure).

**Circuit breaker** (cầu dao điện) sinh ra để chặn đúng cảnh này.

---

## 2. Polly là gì?

**Polly** là thư viện .NET để thêm **resilience** (khả năng chịu lỗi) quanh một lời gọi:

| Chiến lược (strategy) | Làm gì |
|---|---|
| **Retry** | Thử lại vài lần khi lỗi tạm thời (blip mạng) |
| **Circuit Breaker** | "Ngắt cầu dao" khi lỗi liên tục → fail nhanh, không gọi nữa một lúc |
| **Timeout** | Bỏ cuộc một lần gọi nếu quá lâu |
| **Fallback** | Trả giá trị dự phòng khi lỗi (vd tên = "(unknown)") |
| **Rate limiter / Hedging** | Giới hạn nhịp / gọi song song |

Polly v8 gộp nhiều strategy thành một **`ResiliencePipeline`** (đường ống), gọi qua `ExecuteAsync(...)`.

Package đang dùng: **`Polly.Extensions`** (kéo theo `Polly.Core` + tích hợp DI `AddResiliencePipeline`).

---

## 3. Circuit Breaker — 3 trạng thái (phần cốt lõi)

Giống cầu dao điện trong nhà: quá tải thì **nhảy** để không cháy nhà.

```
        lỗi vượt ngưỡng
 CLOSED ───────────────► OPEN
   ▲                       │  (chờ hết BreakDuration)
   │ thành công            ▼
   └──────── HALF-OPEN ◄────
       (thử 1 phát dò đường)
```

| Trạng thái | Ý nghĩa | Hành vi |
|---|---|---|
| **CLOSED** | Bình thường | Cho gọi thật. Đang **đếm** tỉ lệ lỗi. |
| **OPEN** | Đang "ngắt" | **Không gọi** dependency nữa. Ném `BrokenCircuitException` ngay lập tức (fail-fast). Kéo dài `BreakDuration`. |
| **HALF-OPEN** | Dò thử | Cho **đúng 1** request đi thật. Thành công → CLOSED. Lỗi → OPEN lại. |

**Điểm mấu chốt dễ nhầm:** breaker **không** bypass ngay từ lỗi đầu tiên. Nó phải thấy **đủ nhiều lỗi**
(vd ≥50% trong cửa sổ 10s, tối thiểu N call) mới nhảy sang OPEN. Vài request đầu khi Mongo vừa chết
**vẫn lỗi thật** — đó là lúc breaker đang "học".

---

## 4. Thứ tự "củ hành" (onion) — retry / breaker / timeout

Trong Polly, strategy **thêm trước = nằm ngoài cùng** (chạy trước). Thứ tự khuyến nghị:

```
Retry  (ngoài cùng)
  └─ Circuit Breaker (giữa)
       └─ Timeout (trong cùng, quanh 1 lần gọi thật)
            └─ [lời gọi thật: Mongo / SMTP / ...]
```

- **Timeout** trong cùng: chặn 1 lần gọi treo → không ghim thread.
- **Breaker** ở giữa: đếm lỗi, ngắt khi hỏng kéo dài.
- **Retry** ngoài cùng: nuốt blip ngắn. Đặt **ngoài** breaker để (a) retry cũng bị tính vào việc làm
  breaker nhảy, (b) khi breaker đã OPEN thì `BrokenCircuitException` khiến retry **dừng ngay**, không
  thử lại vô ích.

---

## 5. Dự án dùng circuit breaker ở ĐÂU

Ta chỉ đặt breaker ở **ranh giới ra ngoài tiến trình** (out-of-process), là chỗ thật sự có thể "chết":

### 5.1. Gateway hạ nguồn — `Infrastructure.Messaging/Resilience/DownstreamResilience.cs`
Bọc lời gọi của các **RabbitMQ command worker** tới email / invoice / notification / restock. Nếu SMTP
hay dịch vụ ngoài chết, breaker ngắt để consumer không bị treo. Key pipeline: `"downstream-side-effects"`.

### 5.2. Đọc chéo store (cross-store reads) — `Infrastructure.Saga.Core/Resilience/CrossStoreResilience.cs`
Đây là "lời gọi giống remote service" thật nhất trong app: read-model bên **SQL** với sang **Mongo** lấy
tên (`ICrossDbDirectory`), và read-model bên **Mongo** với sang **SQL** lấy số liệu (`ICrossDbOrderStats`).

Hai decorator `ResilientCrossDbDirectory` / `ResilientCrossDbOrderStats` (trong `Saga.Core/CrossDb`) thêm
**fallback nhẹ nhàng** (graceful degradation): khi breaker OPEN hoặc timeout, trả **"(unknown)"** / **0**
thay vì làm hỏng cả request. → Đơn hàng vẫn hiển thị (thiếu tên) thay vì lỗi 500. *Ưu tiên còn-sống hơn
đầy-đủ.*

```csharp
// Chỉ nuốt lỗi khi ĐÚNG là mạch đang mở / timeout — lỗi lẻ tẻ khác vẫn ném ra bình thường.
catch (Exception aEx) when (aEx is BrokenCircuitException or TimeoutRejectedException)
{
    myLogger.LogWarning("Cross-store '{Op}' short-circuited; returning fallback.", theName);
    return theFallback;
}
```

### 5.3. Quy tắc VÀNG: chỉ bọc READ, không bọc WRITE của saga
Write của saga chạy trong **transaction + có compensation** (vd `BeginTransactionAsync`, ghi bước bù
trừ). Nếu bọc **retry** quanh write **không idempotent** → có thể **áp dụng 2 lần** (trừ kho 2 lần!).
→ Saga **tự lo** retry/rollback cho write. Breaker+retry **chỉ** dành cho **read idempotent** (đọc lại
bao nhiêu lần cũng an toàn).

---

## 6. Đi sâu vào CODE — vì sao thiết kế như vậy

Phần này giải thích **lý do tồn tại** của từng class trong giải pháp cross-store, và **"thay vì làm cách
khác thì sao"**. Có **3 mảnh ghép**:

```
CrossStoreResilience   → "nhà máy": dựng pipeline (retry+breaker+timeout) + đăng ký DI (1 lần, dùng chung)
   │  tạo ra
   ▼
CrossStorePipeline     → "hộp đựng": bọc ResiliencePipeline đã dựng, để inject cho rõ ràng
   │  được tiêm vào
   ▼
ResilientCrossDb*      → "decorator": bọc port ICrossDbDirectory / ICrossDbOrderStats,
                          chạy lời gọi qua pipeline + fallback khi mạch mở
```

### 6.1. Vì sao tách riêng `CrossStoreResilience`?

Nó là **một nơi duy nhất** định nghĩa chính sách chịu lỗi cho **cả hai** hướng đọc chéo (SQL→Mongo và
Mongo→SQL).

- **Đặt ở `Saga.Core`** vì cả `Infrastructure.SqlServer.Saga` lẫn `Infrastructure.MongoDB.Saga` đều
  tham chiếu `Saga.Core`. → Viết 1 lần, hai bên dùng chung, **không lệch cấu hình** (tránh cảnh SQL đặt
  breaker 50% còn Mongo 80%).
- Là **extension method** `AddCrossStoreResilience()` → composition root chỉ gọi **1 dòng** (đặt trong
  `AddSagaCore`), không rải chi tiết Polly khắp nơi.
- Có `CrossStoreResilienceOptions` (record tham số) → chỉnh ngưỡng **không phải sửa code**:
  ```csharp
  services.AddCrossStoreResilience(o => { o.BreakDuration = TimeSpan.FromSeconds(30); });
  ```

> **Thay vì vậy:** nếu nhét cấu hình Polly thẳng vào mỗi project store → **lặp code** + dễ lệch tham số.

### 6.2. Vì sao có `CrossStorePipeline` (lớp bọc) — sao không inject thẳng `ResiliencePipeline`?

Đây là chi tiết dễ bỏ qua nhưng quan trọng. `ResiliencePipeline` là **kiểu chung**. App có **nhiều**
pipeline (còn 1 cái cho downstream gateway). Nếu đăng ký nhiều `ResiliencePipeline` vào DI rồi inject
thẳng → **mơ hồ**: DI không biết trả cái nào (cái sau đè cái trước).

Có 2 cách né:
1. Inject `ResiliencePipelineProvider<string>` rồi mỗi decorator tự `.GetPipeline("cross-store-reads")`
   — nhưng decorator phải **nhớ key**, lookup **mỗi lần**.
2. **(đang dùng)** Bọc pipeline đã dựng vào một **type riêng** `CrossStorePipeline`. Giờ inject
   `CrossStorePipeline` là **rõ ràng, không đụng** pipeline khác, decorator **không cần biết key**.

```csharp
public sealed class CrossStorePipeline            // chỉ là "hộp" gắn tên cho 1 pipeline cụ thể
{
    public CrossStorePipeline(ResiliencePipeline thePipeline) => Pipeline = thePipeline;
    public ResiliencePipeline Pipeline { get; }
}
```

Và cách nó được tạo trong `AddCrossStoreResilience`:
```csharp
theServices.AddResiliencePipeline(PipelineKey, (builder, ctx) => { /* retry+breaker+timeout */ });

// Lấy pipeline từ registry của Polly (theo key) MỘT lần, gói vào hộp để tiêm trực tiếp.
theServices.AddSingleton(sp => new CrossStorePipeline(
    sp.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline(PipelineKey)));
```

- `AddResiliencePipeline(key, …)` → đăng ký pipeline vào **registry** của Polly (resolve qua provider + key).
- `AddSingleton(new CrossStorePipeline(…))` → rút pipeline đó ra, đóng hộp, để decorator inject gọn gàng.

### 6.3. Vì sao dùng DECORATOR (`ResilientCrossDbAdapters`) mà không sửa thẳng adapter gốc?

Lớp gốc `MongoCrossDbDirectory` **chỉ nên** lo một việc: "đọc Mongo". `SqlCrossDbOrderStats` **chỉ** lo
"đọc SQL". Nhét retry/breaker/fallback **vào trong** chúng = trộn 2 trách nhiệm (vi phạm **Single
Responsibility**), và làm chúng phụ thuộc Polly → khó test.

**Decorator** = một lớp **cùng interface**, **bọc** lớp gốc, thêm hành vi mới mà **không đụng** lớp gốc:

```csharp
public sealed class ResilientCrossDbDirectory : ICrossDbDirectory   // cùng interface với lớp gốc
{
    private readonly ICrossDbDirectory myInner;      // ← lớp gốc thật (đọc Mongo)
    private readonly ResiliencePipeline myPipeline;  // ← từ CrossStorePipeline

    public Task<string?> GetCustomerNameAsync(Guid id, CancellationToken ct = default) =>
        RunAsync(t => myInner.GetCustomerNameAsync(id, t), fallback: null, nameof(GetCustomerNameAsync), ct);
    // ... các method khác đều đi qua RunAsync
}
```

Lợi ích:
- **Consumer không đổi.** `SqlOrderReadRepository` vẫn chỉ biết `ICrossDbDirectory`. Bật/tắt/đổi
  resilience = đổi **1 dòng DI**, không sờ vào repo (nguyên tắc **Open/Closed**).
- **Lớp gốc vẫn thuần** — test được độc lập, không dính Polly.
- Đúng tinh thần dự án: *"đổi adapter mà không đụng consumer"*.

> **Thay vì vậy:** sửa thẳng repo đọc (8 file, hàng chục method) hoặc sửa adapter gốc → đụng nhiều code,
> trộn trách nhiệm, khó gỡ ra sau này.

### 6.4. Vì sao fallback đặt trong `try/catch` của decorator, không dùng Polly `AddFallback`?

Polly **có** strategy `AddFallback`, nhưng nó **generic theo kiểu `<T>`**. Ở đây mỗi method trả **kiểu
khác nhau** và cần **giá trị dự phòng khác nhau**: `null` (tên lẻ), `dict rỗng` (map tên), `list rỗng`
(danh sách), `0` (đếm). Nhét tất cả vào **một** pipeline chung (non-generic) rất rối.

Nên chia vai:
- **Pipeline chung** lo retry + breaker + timeout, và **ném lỗi** khi mạch mở / quá giờ.
- **Decorator** bắt **đúng** loại lỗi đó rồi trả default hợp ngữ cảnh cho **từng** method:

```csharp
private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> op, T fallback, string name, CancellationToken ct)
{
    try
    {
        return await myPipeline.ExecuteAsync(async token => await op(token), ct);
    }
    // Chỉ nuốt khi ĐÚNG là mạch đang mở / timeout. Lỗi khác vẫn ném ra → không giấu bug thật.
    catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException)
    {
        myLogger.LogWarning("Cross-store '{Op}' short-circuited; returning fallback.", name);
        return fallback;
    }
}
```

Điểm cốt: **`when (...)` là catch hẹp** — chỉ fallback khi breaker OPEN hoặc timeout. Một lỗi Mongo lẻ
tẻ (mạch còn CLOSED) **vẫn ném ra**, vì một blip đơn không nên âm thầm giấu dữ liệu.

### 6.5. Vì sao đăng ký decorator ở DI của từng project store, không ở `Saga.Core`?

Vì lớp **gốc** (`MongoCrossDbDirectory`) là **`internal`** của project Mongo — chỉ project đó mới
`new` được. Nên factory nằm trong DI của chính project đó: dựng lớp gốc, **bọc** bằng decorator:

```csharp
// MongoDB.Saga/DependencyInjection.cs
theServices.AddScoped<MongoCrossDbDirectory>();                    // lớp gốc (internal, chỉ ở đây dựng được)
theServices.AddScoped<ICrossDbDirectory>(sp => new ResilientCrossDbDirectory(
    sp.GetRequiredService<MongoCrossDbDirectory>(),                // ← inner
    sp.GetRequiredService<CrossStorePipeline>(),                   // ← pipeline (từ Saga.Core)
    sp.GetRequiredService<ILogger<ResilientCrossDbDirectory>>()));
```

Decorator để **`public`** trong `Saga.Core` (cả 2 project dùng được), nhưng nó chỉ nhận
`ICrossDbDirectory` nên **không cần thấy** lớp `internal` — factory bên project store lo phần dựng.

### 6.6. Ráp lại — luồng 1 request đọc đơn hàng

```
GET /api/orders/{id}
  └─ SqlOrderReadRepository.GetOrderDetailAsync()          (chỉ biết ICrossDbDirectory)
       └─ ResilientCrossDbDirectory.GetCustomerNameAsync() (DECORATOR)
            └─ myPipeline.ExecuteAsync(...)                 (retry → breaker → timeout)
                 └─ MongoCrossDbDirectory.GetCustomerNameAsync()  (lớp GỐC: đọc Mongo thật)

  Mongo khoẻ  → trả tên thật.
  Mongo chết  → vài call đầu lỗi (breaker đếm) → OPEN → BrokenCircuitException
                → decorator catch → trả null → read model hiển thị "(unknown)", đơn vẫn ra.
```

---

## 7. Cách thêm circuit breaker cho một dependency mới (công thức)

**Bước 1** — thêm package (nếu project chưa có):
```xml
<PackageReference Include="Polly.Extensions" Version="8.5.0" />
```

**Bước 2** — khai báo pipeline (retry → breaker → timeout) và đăng ký DI:
```csharp
theServices.AddResiliencePipeline("my-dependency", (builder, ctx) =>
{
    builder.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = new PredicateBuilder().Handle<Exception>(
            ex => ex is not (BrokenCircuitException or OperationCanceledException)),
    });
    builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,               // ≥50% lỗi
        SamplingDuration = TimeSpan.FromSeconds(10),
        MinimumThroughput = 5,            // cần tối thiểu 5 call mới xét
        BreakDuration = TimeSpan.FromSeconds(20),
    });
    builder.AddTimeout(TimeSpan.FromSeconds(2));
});
```

**Bước 3** — lấy pipeline ra và bọc lời gọi:
```csharp
var pipeline = provider.GetPipeline("my-dependency");   // ResiliencePipelineProvider<string>
var result = await pipeline.ExecuteAsync(
    async ct => await callTheDependency(ct), cancellationToken);
```

> Mẹo: bọc bằng **decorator** quanh port/interface (như `ResilientCrossDbDirectory`) để code gọi
> **không phải sửa** — đúng tinh thần "đổi adapter không đụng consumer".

---

## 8. Tinh chỉnh tham số (đọc bảng này khi cần chỉnh)

| Tham số | Ý nghĩa | Đặt cao lên nếu… | Đặt thấp xuống nếu… |
|---|---|---|---|
| `FailureRatio` | % lỗi để nhảy OPEN | dependency hay có blip lành tính | cần ngắt sớm, nhạy |
| `MinimumThroughput` | số call tối thiểu mới xét | traffic thấp, tránh nhảy vì 1-2 lỗi | traffic cao, muốn phản ứng nhanh |
| `SamplingDuration` | cửa sổ đo tỉ lệ lỗi | muốn nhìn xu hướng dài | muốn phản ứng theo phút hiện tại |
| `BreakDuration` | thời gian OPEN | dependency lâu hồi phục | hồi phục nhanh, muốn thử lại sớm |
| `Timeout` | hạn 1 lần gọi | query nặng, chấp nhận chờ | cần trả nhanh, cắt sớm |

---

## 9. Cách thử (demo) nhìn thấy breaker hoạt động

1. Bật provider Hybrid/Saga, chạy app.
2. **Tắt container MongoDB** (`docker stop <mongo>`).
3. Gọi API đọc đơn hàng (read model SQL cần tên từ Mongo).
4. Quan sát log:
   - Vài call đầu: `Cross-store ↻ retry …` rồi lỗi (breaker CLOSED, đang đếm).
   - Sau đó: `Cross-store ⛔ circuit OPEN …` → các call sau **trả "(unknown)"** ngay (fallback), không chờ.
   - Sau `BreakDuration`: `🔎 HALF-OPEN` → bật lại Mongo → `✅ CLOSED`, tên thật trở lại.

---

## 10. Tóm tắt 30 giây

- Polly = thư viện resilience; circuit breaker = cầu dao chặn cascading failure.
- 3 trạng thái: **CLOSED** (đếm) → **OPEN** (fail-fast) → **HALF-OPEN** (dò) → CLOSED.
- Không nhảy ngay lỗi đầu; cần đủ lỗi trong cửa sổ.
- Thứ tự: **retry → breaker → timeout**.
- Dự án đặt breaker ở: **gateway hạ nguồn** và **đọc chéo store** (có fallback).
- **Chỉ bọc read idempotent**; write của saga để saga tự lo.

Liên quan: [saga-hybrid.md](saga-hybrid.md), [outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md),
[gateway-email-smtp.md](gateway-email-smtp.md), [gateway-vnpay.md](gateway-vnpay.md).
