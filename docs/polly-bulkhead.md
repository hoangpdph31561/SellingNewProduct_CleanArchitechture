# Bulkhead Pattern (Polly) — giải thích chi tiết + luồng chạy + phối hợp với các pattern khác

Tài liệu này giải thích **bulkhead** (vách ngăn) là gì, **tại sao** cần nó khi đã có circuit breaker,
nó **chạy ra sao** (luồng permit), và **phối hợp** thế nào với retry / breaker / timeout trong cùng một
`ResiliencePipeline`. Đọc kèm [polly-circuit-breaker.md](polly-circuit-breaker.md).

> **Đặt ở đâu trong dự án?** Có **hai** chỗ, đều là lớp **ngoài cùng** của pipeline:
> 1. **Gateway hạ nguồn** — `Infrastructure.Messaging/Resilience/DownstreamResilience.cs`: bọc các
>    RabbitMQ command worker gọi ra email / invoice / notification / restock (chạy trên thread nền).
> 2. **Cross-store reads** — `Infrastructure.Saga.Core/Resilience/CrossStoreResilience.cs`: bọc các
>    read model với sang store kia (chạy trên **thread request** của API). Xem [§9](#9-áp-dụng-thứ-hai-bulkhead-cho-cross-store-reads-request-path).
>
> Package dùng chung: **`Polly.RateLimiting`** (bổ sung cạnh `Polly.Extensions`).

---

## 1. Vấn đề bulkhead giải quyết: dependency **CHẬM** (không phải chết) làm cạn pool

Circuit breaker giỏi xử lý dependency **chết hẳn** (lỗi liên tục → ngắt). Nhưng có một cảnh **khác**
mà breaker **không** chặn kịp: dependency **vẫn trả lời, chỉ là RẤT CHẬM**.

Hình dung worker gọi SMTP. SMTP không chết — nó nhận kết nối nhưng mỗi mail mất 8–10s mới xong.

- Message dồn về liên tục, mỗi cái giữ **1 thread + 1 connection** trong 8–10s.
- Vì các call **vẫn thành công** (chỉ chậm), **circuit breaker KHÔNG nhảy** — nó chỉ đếm *lỗi*, không
  đếm *độ chậm*.
- Số call đang chạy đồng thời phình lên: 50, 100, 200… **thread pool / connection pool cạn sạch**.
- Các worker **khác** (invoice, notification) — vốn khoẻ — cũng **không xin nổi thread** để chạy.
- → Một dependency **chậm** kéo sập **toàn bộ** host. Đây là **resource exhaustion** (cạn tài nguyên).

**Bulkhead** sinh ra để chặn đúng cảnh này: **giới hạn số call được chạy đồng thời**, cô lập thiệt hại.

---

## 2. Bulkhead là gì? — vách ngăn khoang tàu

Tên lấy từ **đóng tàu**: thân tàu chia thành nhiều **khoang kín (bulkhead)**. Một khoang thủng ngập
nước thì **nước không tràn** sang khoang khác → tàu **không chìm**.

Trong phần mềm: chia tài nguyên (số call đồng thời) thành **hạn mức cố định**. Một dependency "ngập"
(chậm) chỉ được chiếm **tối đa N slot** — hết N là **thôi**, không lấn sang phần tài nguyên của phần
còn lại. Phần khoẻ vẫn có tài nguyên để chạy.

> **Một câu:** breaker chặn theo **tỉ lệ lỗi**; bulkhead chặn theo **số lượng đồng thời**. Hai trục
> hoàn toàn khác nhau → chúng **bổ sung** nhau, không thay thế.

### Hai kiểu bulkhead (chỉ để biết)
| Kiểu | Cơ chế | Ghi chú |
|---|---|---|
| **Thread-pool isolation** | mỗi dependency 1 pool thread riêng | kiểu Hystrix cũ; tốn thread, context-switch |
| **Concurrency limiter (semaphore)** | 1 bộ đếm permit, không cấp thread riêng | **Polly v8 dùng kiểu này** — nhẹ, async-friendly |

Polly v8 làm bulkhead bằng **`ConcurrencyLimiter`** (semaphore bất đồng bộ) trong package
`Polly.RateLimiting`. Không tạo thread riêng — chỉ **đếm số call đang chạy**.

---

## 3. Hai tham số cốt lõi: `PermitLimit` và `QueueLimit`

```csharp
new ConcurrencyLimiter(new ConcurrencyLimiterOptions
{
    PermitLimit = 8,   // tối đa 8 call CHẠY đồng thời
    QueueLimit  = 4,   // + tối đa 4 call ĐỨNG CHỜ xin permit
});
```

| Tham số | Ý nghĩa | Nếu vượt |
|---|---|---|
| **`PermitLimit`** | Bao nhiêu call được **chạy cùng lúc**. Mỗi call chạy giữ **1 permit**, xong thì **trả** lại. | Không có permit → sang hàng đợi |
| **`QueueLimit`** | Bao nhiêu call được **xếp hàng chờ** một permit trống. | Hàng đợi đầy → **từ chối NGAY** (`RateLimiterRejectedException`) |

Sức chứa tối đa = `PermitLimit + QueueLimit` = **12** call "được chấp nhận" tại một thời điểm. Call thứ
13 bị **fail-fast** — thà lỗi ngay còn hơn dồn ứ vô tận rồi làm cạn pool.

**Vì sao cần cả QueueLimit > 0?** Cho một khoảng đệm nhỏ để nuốt các đợt burst ngắn (call vừa xong,
permit vừa trả) mà không từ chối vội. Đặt `QueueLimit = 0` → cực gắt: hết 8 permit là từ chối luôn.

---

## 4. Luồng chạy — một call đi qua bulkhead

```
Call tới bulkhead
      │
      ▼
Còn permit trống? ──Có──► LẤY 1 permit ──► chạy phần còn lại của pipeline (retry/breaker/timeout → gateway)
      │                                          │
      │ Không                                    ▼ (xong / lỗi / timeout)
      ▼                                     TRẢ permit lại  ◄── luôn trả, kể cả khi lỗi
Còn chỗ trong queue? ──Có──► ĐỨNG CHỜ ──► (có permit trống) ──► lấy permit ──► chạy
      │
      │ Không (queue đầy)
      ▼
TỪ CHỐI NGAY → ném RateLimiterRejectedException (fail-fast, KHÔNG gọi gateway)
```

Điểm mấu chốt:
- **Giữ permit suốt thời gian call chạy**, trả lại khi kết thúc (dù thành công, lỗi, hay timeout). Nhờ
  `try/finally` bên trong `ConcurrencyLimiter` → **không rò rỉ permit**.
- Call bị từ chối **không chạm** tới gateway — nó lỗi *trước khi* vào lõi pipeline. Rẻ và nhanh.

---

## 5. Phối hợp với retry / breaker / timeout — thứ tự "củ hành"

Trong dự án pipeline giờ có **4 lớp**. Nhắc lại: trong Polly, **thêm trước = nằm ngoài cùng**.

```
Bulkhead  (NGOÀI CÙNG)  ── giới hạn SỐ call đồng thời
  └─ Retry               ── thử lại blip ngắn
       └─ Circuit Breaker ── ngắt khi tỉ lệ lỗi cao
            └─ Timeout     ── cắt 1 lần gọi treo
                 └─ [gọi gateway thật: SMTP / invoice / ...]
```

Mỗi lớp chặn **một loại sự cố khác nhau** — đó là lý do dùng **cả bốn**:

| Lớp | Bảo vệ khỏi | Trục đo | Hành vi khi kích hoạt |
|---|---|---|---|
| **Bulkhead** | dependency **chậm** làm cạn pool | **số call đồng thời** | từ chối call thừa (`RateLimiterRejectedException`) |
| **Retry** | **blip** thoáng qua (mạng rung) | số lần thử | thử lại có backoff + jitter |
| **Circuit Breaker** | dependency **chết kéo dài** | **tỉ lệ lỗi** trong cửa sổ | fail-fast (`BrokenCircuitException`) khi OPEN |
| **Timeout** | **một** call treo vô hạn | thời gian mỗi call | huỷ call (`TimeoutRejectedException`) |

### 5.1. Vì sao bulkhead nằm NGOÀI CÙNG (bọc cả retry)?

Đây là quyết định thiết kế quan trọng nhất. Đặt bulkhead ngoài cùng nghĩa là **1 permit bị giữ cho
TRỌN cả chuỗi retry**, không phải mỗi lần thử một permit.

- **Đúng bản chất "isolation":** hạn mức phải là *"tối đa 8 thao tác đang xử lý"*, mà một thao tác gồm
  cả các lần retry của nó. Nếu bulkhead nằm *trong* retry → mỗi lần retry lại xin permit mới, 1 thao tác
  chậm-hay-retry có thể ngốn nhiều permit → sai ý nghĩa cô lập.
- **Chặn bão retry:** khi gateway chậm, retry làm **tăng** tải (mỗi call thành 3–4 lần thử). Bulkhead
  bọc ngoài đặt trần cứng cho **tổng** số thao tác đồng thời, nên retry **không** thể thổi bùng tải vượt
  hạn mức.
- **Từ chối là quyết định sớm nhất:** nếu hệ thống đã quá tải, ta muốn nói "không" **ngay**, *trước khi*
  tốn công retry/đo breaker/đặt timer. Ngoài cùng = rẻ nhất.

### 5.2. Retry KHÔNG thử lại khi bị bulkhead từ chối

Vì bulkhead ở **ngoài** retry, khi bulkhead ném `RateLimiterRejectedException` thì lỗi đó nằm **ngoài**
tầm với của retry (retry ở trong) → **không** có chuyện retry cố thử lại một call vừa bị từ chối vì quá
tải (thử lại lúc quá tải là đổ thêm dầu vào lửa). Ngược lại, `ShouldHandle` của retry cũng đã loại
`BrokenCircuitException` để không thử lại khi breaker đang OPEN.

### 5.3. Bốn lớp nhìn theo tình huống thực tế

| Tình huống gateway | Lớp nào ra tay | Kết quả |
|---|---|---|
| Rung mạng 1 nhịp | Retry | thử lại → thành công, người dùng không thấy gì |
| Treo 1 call (không phản hồi) | Timeout → (rồi Retry) | cắt sau 3s, thử lại |
| Chết hẳn, lỗi liên tục | Breaker | sau vài lỗi → OPEN → fail-fast 15s |
| **Sống nhưng chậm**, tải dồn | **Bulkhead** | giữ 8 chạy + 4 chờ, **từ chối phần thừa** → pool không cạn |

---

## 6. Code thật trong dự án

`Infrastructure.Messaging/Resilience/DownstreamResilience.cs` — lớp bulkhead được **thêm đầu tiên** nên
nằm ngoài cùng:

```csharp
// 4) Bulkhead (ngoài cùng): MỘT ConcurrencyLimiter dùng chung, cô lập số call đang chạy.
//    Dựng MỘT lần ở đây (pipeline là singleton) để mọi worker chia sẻ cùng một pool permit.
var aBulkhead = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
{
    PermitLimit = 8,
    QueueLimit = 4
});

theBuilder.AddRateLimiter(new RateLimiterStrategyOptions
{
    RateLimiter = theArgs => aBulkhead.AcquireAsync(1, theArgs.Context.CancellationToken),
    OnRejected = theArgs =>
    {
        aLogger.LogWarning(
            "Polly 🚧 bulkhead FULL — call rejected; {Permits} permits + {Queue} queue slots all in use.",
            8, 4);
        return ValueTask.CompletedTask;
    }
});

// 3) Retry → 2) Circuit Breaker → 1) Timeout được thêm sau (nằm trong dần).
```

### 6.1. Giải phẫu từng mảnh (mỗi dòng làm gì)

Hình dung một **bãi giữ xe**: bãi có số chỗ cố định, xe vào lấy vé, ra thì trả vé; hết chỗ thì đuổi về.

| Mảnh | Của ai | Vai trò | Ví von |
|---|---|---|---|
| **`ConcurrencyLimiter`** + `ConcurrencyLimiterOptions` | **.NET BCL** (`System.Threading.RateLimiting`) | primitive **semaphore đếm** — thứ *thật sự* giữ trạng thái "đang có mấy call chạy". `PermitLimit`=số chỗ đỗ, `QueueLimit`=chỗ chờ ngoài cổng | **cái bãi xe** |
| **`AddRateLimiter`** + `RateLimiterStrategyOptions` | **Polly** (`Polly.RateLimiting`) | cầu nối cắm cái bãi trên thành **một lớp** trong pipeline; bọc mỗi `ExecuteAsync`: xin vé trước, chạy xong **tự trả vé** | **lắp barrier** vào đường ống |
| **`RateLimiter`** (delegate) | Polly gọi cho **mỗi** call | luật "mỗi call → xin **1** permit ở **đúng** bãi `aBulkhead`", kèm truyền `CancellationToken` của call | **luật xin vé** |
| **`OnRejected`** (callback) | Polly gọi **khi hết vé** | hook **thông báo** (ở đây: log cảnh báo). *Không* quyết định từ chối — cái bãi quyết | **còi báo bãi đầy** |

**① `ConcurrencyLimiter` — "cái bãi xe" thật sự.** Là primitive của .NET, không phải Polly. Gọi
`AcquireAsync()` → còn chỗ thì trả một **lease** (cái vé) đã acquired; xong việc **dispose lease** → trả
permit lại bãi. Đây là thứ duy nhất giữ trạng thái, nên phải là **một instance dùng chung** cho mọi call.

**② `AddRateLimiter` — "lắp barrier vào đường ống".** `ConcurrencyLimiter` đứng một mình chưa chặn gì cả;
`AddRateLimiter` mới biến nó thành một **strategy** trong "củ hành". Nó bọc mỗi lần chạy: **trước** → xin
vé; **sau** (dù thành công / lỗi / timeout) → **tự dispose lease** để trả vé → **permit không rò rỉ**.
Thêm **đầu tiên** nên nằm **ngoài cùng** (bulkhead phải bọc cả retry).

**③ `RateLimiter` (delegate) — "luật xin vé".** Là **hàm** Polly gọi cho từng call: `theArgs =>
aBulkhead.AcquireAsync(1, theArgs.Context.CancellationToken)`. Vì sao là delegate chứ không truyền thẳng
limiter? Để (a) trỏ mọi call về **cùng một** bãi dùng chung, (b) truyền **token của call hiện tại** vào
→ request bị huỷ lúc đang chờ vé thì thoát ngay, (c) [nâng cao] có thể chọn bãi khác nhau theo key/tenant.

**④ `OnRejected` — "còi báo bãi đầy".** Chạy khi lease **không** acquire được (hết permit **và** hàng chờ
đầy). Điểm mấu chốt: nó **không** ném lỗi — ngay sau khi nó chạy xong, **Polly ném
`RateLimiterRejectedException`**. Đó chính là exception mà `catch` fallback trong decorator phải bắt (để
degrade về `"(unknown)"`/`0` thay vì 500). Trả `ValueTask.CompletedTask` vì hook không làm gì async.

```
ExecuteAsync → RateLimiter xin vé ở aBulkhead
   ├─ còn chỗ  → lease acquired → chạy retry→breaker→timeout→gateway → xong → dispose lease (trả vé)
   └─ bãi đầy  → lease KHÔNG acquired → OnRejected (log) → Polly ném RateLimiterRejectedException
```

### 6.2. Vì sao `AddRateLimiter` + `ConcurrencyLimiter` dựng SẴN, không phải `AddConcurrencyLimiter(8, 4)`?

Polly có sẵn overload tiện lợi `AddConcurrencyLimiter(permitLimit, queueLimit)`, nhưng nó **không** cho
móc `OnRejected`. Ở đây ta muốn **log mỗi lần từ chối** (tín hiệu quá tải rất đáng chú ý), nên dùng
`AddRateLimiter(RateLimiterStrategyOptions)` và **tự dựng một `ConcurrencyLimiter` dùng chung**.

> **Bẫy dễ mắc:** phải dựng `ConcurrencyLimiter` **MỘT lần** ở ngoài closure (như trên) rồi tham chiếu
> vào. Nếu `new ConcurrencyLimiter(...)` **bên trong** lambda `RateLimiter =>` thì mỗi call tạo một
> limiter mới với đủ 8 permit → **vô hiệu hoá** bulkhead hoàn toàn. Vì pipeline được đăng ký như
> **singleton** (qua `ResiliencePipelineProvider`), biến `aBulkhead` được dựng đúng một lần và **mọi
> worker chia sẻ chung** một pool.

---

## 7. Tinh chỉnh `PermitLimit` / `QueueLimit`

| Muốn… | Chỉnh |
|---|---|
| Cho gateway nhiều thông lượng hơn | tăng `PermitLimit` |
| Chịu burst ngắn tốt hơn (ít từ chối oan) | tăng `QueueLimit` |
| Bảo vệ pool gắt hơn (fail-fast sớm) | giảm `QueueLimit` (thậm chí = 0) |
| Cô lập chặt từng gateway | tách **mỗi gateway một bulkhead riêng** (xem dưới) |

**Đặt bao nhiêu?** Quy tắc ngón tay cái: `PermitLimit` ≈ số call đồng thời mà gateway **khoẻ** xử lý
gọn, và tổng permit của mọi bulkhead **nhỏ hơn** số thread/connection mà host có. Mục tiêu là khi một
gateway ngập, nó **không** mượn hết tài nguyên của phần còn lại.

> **Nâng cấp có thể làm sau:** hiện 4 worker (email/invoice/notification/restock) **dùng chung một**
> bulkhead. Cô lập thật sự hơn là cho **mỗi loại một `ConcurrencyLimiter` riêng** — email chậm thì chỉ
> khoang email đầy, invoice vẫn có permit. Chung một pool vẫn tốt hơn không có gì, nhưng khoang riêng
> mới đúng tinh thần "vách ngăn".

---

## 8. Cách thử (demo) nhìn thấy bulkhead hoạt động

1. Tạm cho gateway **chậm giả**: trong stand-in email sender thêm `await Task.Delay(TimeSpan.FromSeconds(10))`.
2. Đẩy **> 12** `SendEmailCommand` vào RabbitMQ trong thời gian ngắn (hoặc đặt hàng liên tục để phát nhiều sự kiện).
3. Quan sát log:
   - 8 call đầu: chạy (giữ permit), đứng chờ 10s.
   - 4 call kế: đứng trong queue.
   - Từ call thứ 13: `Polly 🚧 bulkhead FULL — call rejected …` **ngay lập tức**, không chờ 10s.
4. Điểm cần thấy: các worker **khác** vẫn phản hồi nhanh — chúng không bị đám email chậm ăn hết thread.

Muốn thấy **phối hợp** với breaker: để gateway vừa chậm vừa lỗi → bulkhead chặn số đồng thời trong khi
breaker đếm tỉ lệ lỗi để ngắt; hai cơ chế chạy song song trên cùng pipeline.

---

## 9. Áp dụng thứ hai: bulkhead cho cross-store reads (request path)

Ngoài gateway, dự án còn đặt bulkhead cho **cross-store reads** — chỗ read model một store với sang
store kia (SQL→Mongo lấy tên; Mongo→SQL lấy số liệu đơn). Xem thêm
[polly-circuit-breaker.md §5.2](polly-circuit-breaker.md).

### 9.1. Vì sao đây mới là chỗ bulkhead "đúng bài" nhất

Khác gateway (chạy nền), cross-store reads chạy **trên chính thread request của API** và **cạnh tranh
cùng connection pool** với read/write chính của store. → Một store kia chậm mà không có bulkhead thì:

- Request dồn lại, mỗi cái giữ 1 thread + 1 connection tới store kia.
- Ăn hết pool → **mọi endpoint** đơ, không chỉ phần enrichment.
- Read/write **chính** của store cũng đói connection theo.

Bulkhead cô lập đúng "cú với sang store kia" (hop rủi ro nhất) để nó không bao giờ nuốt hết pool. Và vì
path này **đã có fallback** (trả `"(unknown)"` / `0`), khi bulkhead từ chối ta degrade mượt sang partial
data — đúng tinh thần *availability over completeness*.

### 9.2. Điểm mấu chốt: TÁCH theo store (2 pipeline riêng), không dùng chung 1

Cross-store có **hai dependency khác nhau**: hướng SQL→Mongo (đọc Mongo) và hướng Mongo→SQL (đọc SQL).
Nếu dùng **một** bulkhead chung → Mongo chậm chiếm hết permit, chặn luôn read **SQL** đang khoẻ = đặt
hai khoang tàu sau **chung một vách**. Vì vậy đăng ký **hai** pipeline tách biệt:

```
CrossStoreResilience.AddCrossStoreResilience()
  ├─ pipeline "cross-store-to-mongo"  → CrossStoreToMongoPipeline → ResilientCrossDbDirectory (đọc Mongo)
  └─ pipeline "cross-store-to-sql"    → CrossStoreToSqlPipeline   → ResilientCrossDbOrderStats (đọc SQL)
```

Hai decorator sẵn có đã chia đúng theo hướng store, nên mỗi cái chỉ việc inject **wrapper type riêng**
của nó (`CrossStoreToMongoPipeline` / `CrossStoreToSqlPipeline`) — DI không còn mơ hồ.

> **Một công đôi việc:** tách pipeline sửa LUÔN một lỗ hổng có sẵn ở **circuit breaker** — trước đây một
> breaker chung khiến Mongo lỗi làm short-circuit oan cả read SQL. Giờ mỗi store có breaker **và**
> bulkhead **và** timeout riêng → cô lập trọn vẹn cả tỉ lệ lỗi lẫn số đồng thời.

### 9.3. ⚠️ Bắt buộc: cho fallback nuốt luôn `RateLimiterRejectedException`

Đây là chi tiết dễ quên nhất. Khi bulkhead từ chối, nó ném `RateLimiterRejectedException`. Nếu `catch`
fallback của decorator **không** bắt loại này thì call bị từ chối sẽ **lọt ra → 500 nguyên request**,
phá vỡ đúng thiết kế graceful degradation. Vì vậy `catch` (trong `ResilientCrossDbAdapters.cs`) phải mở
rộng:

```csharp
// Degrade khi breaker OPEN, HOẶC call timeout, HOẶC bulkhead đầy → trả partial data.
// Lỗi khác vẫn ném ra để không giấu bug thật.
catch (Exception aEx) when (aEx is BrokenCircuitException or TimeoutRejectedException or RateLimiterRejectedException)
{
    myLogger.LogWarning("Cross-store '{Op}' short-circuited ({Reason}); returning fallback.", theName, aEx.GetType().Name);
    return theFallback;
}
```

### 9.4. Sizing — vì sao permit ở đây nhỏ hơn

`CrossStoreResilienceOptions` thêm 2 tham số (mặc định **`MaxConcurrency = 10`**, **`QueueLimit = 5`**),
chỉnh được lúc đăng ký:

```csharp
services.AddCrossStoreResilience(o =>
{
    o.MaxConcurrency = 10;   // tối đa 10 cross-store read tới MỖI store chạy đồng thời
    o.QueueLimit     = 5;    // + 5 đứng chờ; vượt nữa → RateLimiterRejectedException
});
```

Permit đặt **thấp hơn nhiều** so với `maxPoolSize` mặc định (~100) của driver Mongo/SQL — để các read
enrichment (thứ yếu) **không bao giờ** giành hết connection của read/write **chính**. Mỗi store có một
`ConcurrencyLimiter` riêng nên hạn mức là "10 đồng thời tới Mongo" và "10 đồng thời tới SQL" tách bạch.

### 9.5. So với gateway — cùng pattern, khác ngữ cảnh

| | Gateway (§6) | Cross-store (§9) |
|---|---|---|
| Chạy trên | thread worker nền | **thread request API** |
| Số bulkhead | 1 (4 worker dùng chung) | **2** (tách theo store) |
| Permit / Queue | 8 / 4 | 10 / 5 (mỗi store) |
| Khi từ chối | message fail/requeue | **fallback** → partial data (không 500) |
| Vá kèm | — | tách breaker riêng theo store |

### 9.6. Vì sao phải có **pipeline key** (và thêm một tầng **type wrapper**)?

Đăng ký cross-store thực chất là:

```csharp
theServices.AddResiliencePipeline(ToMongoPipelineKey, (b, ctx) => ConfigurePipeline(...)); // "cross-store-to-mongo"
theServices.AddResiliencePipeline(ToSqlPipelineKey,   (b, ctx) => ConfigurePipeline(...)); // "cross-store-to-sql"

theServices.AddSingleton(sp => new CrossStoreToMongoPipeline(
    sp.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline(ToMongoPipelineKey)));
theServices.AddSingleton(sp => new CrossStoreToSqlPipeline(
    sp.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline(ToSqlPipelineKey)));
```

**Tầng 1 — key để Polly phân biệt các pipeline.** Polly không cất pipeline theo *kiểu* mà cất trong một
**registry dạng từ điển** `ResiliencePipelineProvider<string>` — **key kiểu `string`** (đó là lý do có
`<string>`). App có **nhiều** pipeline **cùng kiểu** `ResiliencePipeline` (to-mongo, to-sql, và downstream
gateway). Cùng kiểu ⇒ nếu không đặt tên thì không tài nào phân biệt (đăng ký nhiều cái cùng kiểu → cái
sau đè cái trước, lấy nhầm bãi xe). Vì vậy mỗi pipeline một **key riêng**, rồi `GetPipeline(key)` rút
**đúng** cái ra. Chính hai key khác nhau này **là** cái làm nên Phương án B: Mongo và SQL ⇒ hai bulkhead
+ hai breaker **tách biệt**.

**Tầng 2 — type wrapper để tầng tiêu dùng inject an toàn.** Key chỉ là `string` → decorator mà tự cầm
string đi tra thì **dễ gõ nhầm** và phải nhớ key. Nên sau khi `GetPipeline(key)` lấy ra, ta **bọc vào
type riêng** (`CrossStoreToMongoPipeline` / `CrossStoreToSqlPipeline`). Giờ decorator chỉ khai báo type
trong constructor → DI biết chính xác đưa cái nào, **kiểm ở compile-time**, và decorator **không cần
biết key** tồn tại. Key sống gọn trong `CrossStoreResilience`.

> Tóm lại: **key** để *Polly* phân biệt pipeline trong registry (bắt buộc khi có >1 pipeline); **type
> wrapper** để *decorator* inject đúng cái mà không đụng tới key.

---

## 10. Tóm tắt 30 giây

- **Bulkhead = vách ngăn**: giới hạn **số call đồng thời** để một dependency **chậm** không làm **cạn
  pool** và kéo sập phần khoẻ.
- Polly v8 làm bằng **`ConcurrencyLimiter`** (semaphore) trong `Polly.RateLimiting`: `PermitLimit` (số
  chạy) + `QueueLimit` (số chờ); vượt cả hai → **từ chối ngay** `RateLimiterRejectedException`.
- Đặt **ngoài cùng** pipeline → 1 permit giữ cho **cả chuỗi retry**, chặn bão retry, từ chối sớm nhất.
- **Bổ sung** (không thay thế) breaker: breaker chặn theo **tỉ lệ lỗi**, bulkhead chặn theo **số đồng
  thời**. Cộng thêm retry (blip) + timeout (call treo) = 4 lớp, mỗi lớp một loại sự cố.
- Dựng `ConcurrencyLimiter` **một lần** (singleton) để mọi worker **chia sẻ chung** pool — đừng `new`
  trong lambda.
- Dự án đặt bulkhead ở **2 chỗ**: gateway hạ nguồn (1 pool chung, 8/4) và **cross-store reads** (tách
  **2 pool theo store**, 10/5) — chỗ thứ hai là request path nên phải cho fallback nuốt thêm
  `RateLimiterRejectedException`, và tách pipeline vá luôn breaker-dùng-chung.
- Giải phẫu (§6.1): `ConcurrencyLimiter` = cái bãi xe (BCL); `AddRateLimiter` = lắp barrier vào pipeline
  (Polly); `RateLimiter` = luật xin vé mỗi call; `OnRejected` = còi báo đầy (rồi Polly ném exception).
- **Pipeline key** (§9.6) để Polly phân biệt nhiều pipeline cùng kiểu trong registry; bọc thêm **type
  wrapper** để decorator inject đúng cái, khỏi cầm string.

Liên quan: [polly-circuit-breaker.md](polly-circuit-breaker.md),
[outbox-kafka-rabbitmq.md](outbox-kafka-rabbitmq.md), [gateway-email-smtp.md](gateway-email-smtp.md),
[saga-hybrid.md](saga-hybrid.md).
