# Gateway Payment — VNPay (thanh toán online)

Tài liệu này hướng dẫn tích hợp **VNPay** — cổng thanh toán theo mô hình **redirect** (chuyển hướng
trình duyệt khách sang trang VNPay để trả tiền, rồi VNPay gọi ngược lại app).

- **Port (Domain)**: `IPaymentGateway` (`Domain/Interfaces/Outbound/IPaymentGateway.cs`)
- **Adapter**: `VnPayPaymentGateway` (`Infrastructure.Payments/VnPay/VnPayPaymentGateway.cs`)
- **Project mới**: `SellingNewProduct.Infrastructure.Payments`
- **API**: `PaymentsController` — `POST api/payments/vnpay/create` và `GET api/payments/vnpay-return`

> **Không có circuit breaker ở đây.** `CreatePayment` và `VerifyCallback` chỉ là **tính toán cục bộ**
> (dựng URL đã ký, kiểm tra chữ ký) — không gọi HTTP ra ngoài. Nếu sau này thêm gọi API refund/query
> của VNPay (qua HTTP) thì chỗ đó mới cần breaker.

---

## 1. Luồng thanh toán VNPay (toàn cảnh)

```
[Khách bấm "Thanh toán"]
        │  POST /api/payments/vnpay/create   (orderId, amount, orderInfo)
        ▼
[API] VnPayPaymentGateway.CreatePayment()
        │  dựng tham số → sắp xếp → ký HMAC-SHA512 → trả PaymentUrl
        ▼
[Trình duyệt khách] redirect sang VNPay ──► [Khách nhập thẻ/OTP trên trang VNPay]
                                                     │
        ┌────────────────────────────────────────────┘
        ▼
VNPay gọi ngược lại 2 kênh:
  (a) Return URL  → GET /api/payments/vnpay-return   (trình duyệt khách quay về, để HIỂN THỊ)
  (b) IPN URL     → server-to-server                 (để GHI NHẬN kết quả — tin cậy hơn)
        │
        ▼
[API] VerifyCallback()  → kiểm tra chữ ký → nếu hợp lệ & thành công → đánh dấu đơn đã trả
```

> **Quan trọng:** Return URL do **trình duyệt** gọi (khách có thể đóng tab → không nhận được). IPN do
> **server VNPay** gọi thẳng → **đây mới là nguồn sự thật** để cập nhật trạng thái đơn. Xem mục 7.

---

## 2. Cấu hình

Section `VnPay` trong `appsettings.json`:

```jsonc
"VnPay": {
  "TmnCode": "",        // Mã website (merchant) VNPay cấp — KHÔNG commit
  "HashSecret": "",     // Chuỗi bí mật để ký/kiểm tra — KHÔNG commit
  "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",  // sandbox; đổi sang live khi chạy thật
  "ReturnUrl": "https://localhost:5001/api/payments/vnpay-return",
  "Version": "2.1.0",
  "Command": "pay",
  "CurrCode": "VND",
  "Locale": "vn",       // "vn" hoặc "en"
  "ExpireMinutes": 15   // link hết hạn sau 15 phút
}
```

Bí mật để trong **user-secrets** khi dev:

```bash
cd SellingNewProduct.API
dotnet user-secrets set "VnPay:TmnCode" "xxxx"
dotnet user-secrets set "VnPay:HashSecret" "xxxx"
```

Đăng ký DI (đã có trong `Program.cs`): `builder.Services.AddVnPayPayments(builder.Configuration);`

---

## 3. Tạo URL thanh toán — `CreatePayment`

Các tham số VNPay bắt buộc và cách adapter dựng:

| Tham số | Giá trị trong dự án |
|---|---|
| `vnp_Version` / `vnp_Command` | `2.1.0` / `pay` |
| `vnp_TmnCode` | từ config |
| `vnp_Amount` | **số tiền × 100** (VND không có phần lẻ) — vd 100.000đ → `10000000` |
| `vnp_CurrCode` | `VND` |
| `vnp_TxnRef` | `OrderId` (định dạng `N` — 32 ký tự hex) — mã tham chiếu giao dịch, **duy nhất** |
| `vnp_OrderInfo` | mô tả đơn |
| `vnp_ReturnUrl` | từ config |
| `vnp_IpAddr` | IP khách |
| `vnp_CreateDate` / `vnp_ExpireDate` | giờ **GMT+7**, định dạng `yyyyMMddHHmmss` |

**Ký (sign):** sắp xếp tham số theo **thứ tự ordinal của key**, nối thành chuỗi `k=v&k=v` (URL-encode),
ký **HMAC-SHA512** bằng `HashSecret`, rồi gắn `&vnp_SecureHash=<hash>` vào cuối URL.

```csharp
var aSignData = BuildEncodedQuery(aParameters);            // đã sort + url-encode
var aSecureHash = HmacSha512(myOptions.HashSecret, aSignData);
var aPayUrl = $"{myOptions.BaseUrl}?{aSignData}&vnp_SecureHash={aSecureHash}";
```

> **Bẫy hay gặp:** encode ở lúc **ký** và lúc **ghép URL** phải **giống hệt nhau**, nếu lệch một ký tự
> thì hash không khớp → VNPay báo sai chữ ký. Adapter dùng chung `BuildEncodedQuery` cho cả hai để tránh.

Gọi API:
```
POST /api/payments/vnpay/create
{ "orderId": "…", "amount": 100000, "orderInfo": "Thanh toan don DH123" }

→ { "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_Amount=...&vnp_SecureHash=..." }
```
Frontend chỉ việc `window.location = paymentUrl`.

---

## 4. Kiểm tra callback — `VerifyCallback`

Khi VNPay gọi lại (return/IPN), app **phải xác minh chữ ký trước**, rồi mới tin kết quả:

1. Lấy `vnp_SecureHash` khách gửi lên.
2. Lấy **mọi** tham số `vnp_*` **trừ** `vnp_SecureHash` và `vnp_SecureHashType`.
3. Sort ordinal → dựng chuỗi giống lúc ký → tính lại HMAC-SHA512.
4. So sánh với hash nhận được (không phân biệt hoa/thường).

```csharp
var aIsValid = string.Equals(aExpectedHash, aReceivedHash, StringComparison.OrdinalIgnoreCase);
var aIsSuccessful = aIsValid
    && theParams["vnp_ResponseCode"] == "00"
    && theParams["vnp_TransactionStatus"] == "00";
```

- `IsValid` = chữ ký hợp lệ (đúng là VNPay gửi, không bị sửa).
- `IsSuccessful` = **đã** hợp lệ **và** thanh toán thành công (`00`/`00`).

> **Bảo mật:** tuyệt đối **không** tin `vnp_ResponseCode` nếu chữ ký sai. `IsValid=false` → từ chối ngay.

---

## 5. Endpoint trong `PaymentsController`

```csharp
[HttpPost("vnpay/create")]  // trả về paymentUrl để redirect
[HttpGet ("vnpay-return")]  // VNPay redirect khách về đây; verify rồi báo kết quả
```

`vnpay-return` đọc `Request.Query` thành dictionary, gọi `VerifyCallback`, và:
- Chữ ký sai → `400 BadRequest`, **không** đụng vào đơn.
- Hợp lệ **và** thành công → gửi `CompletePaymentByOrderCommand(OrderId)` để **hoàn tất payment** của đơn
  (idempotent), rồi `200 OK` kèm kết quả + cờ `PaymentCompleted`.

```csharp
if (!aResult.IsValid) return BadRequest(Map(aResult, thePaymentCompleted: false));

var aPaymentCompleted = false;
if (aResult.IsSuccessful)
{
    var aPayment = await mySender.Send(new CompletePaymentByOrderCommand(aResult.OrderId), ct);
    aPaymentCompleted = aPayment is not null;   // null = đơn không có payment nào để hoàn tất
}
return Ok(Map(aResult, aPaymentCompleted));
```

---

## 6. Mã kết quả VNPay (`vnp_ResponseCode`) hay gặp

| Mã | Ý nghĩa |
|---|---|
| `00` | Thành công |
| `07` | Trừ tiền thành công nhưng nghi ngờ gian lận |
| `09` | Thẻ/tài khoản chưa đăng ký Internet Banking |
| `24` | Khách **huỷ** giao dịch |
| `51` | Không đủ số dư |
| `65` | Vượt hạn mức trong ngày |
| `75` | Ngân hàng bảo trì |
| `99` | Lỗi khác |

---

## 7. ✅ Cập nhật đơn khi thành công (idempotent) — ĐÃ LÀM

Khi callback hợp lệ + thành công, app hoàn tất payment của đơn. Cơ chế **idempotent** nằm ở tầng Domain
(`PaymentWriteService.CompleteByOrderAsync`), không phải ở controller:

```csharp
public async Task<Payment?> CompleteByOrderAsync(Guid theOrderId, CancellationToken ct = default)
{
    var aPayments = await myPaymentRepository.GetByOrderAsync(theOrderId, ct);

    // Đã Completed → callback lặp lại; trả về nguyên trạng, KHÔNG cộng tiền lần 2.
    var aCompleted = aPayments.FirstOrDefault(p => p.PaymentStatus == PaymentStatus.Completed);
    if (aCompleted is not null) return aCompleted;

    // Còn Pending → hoàn tất. Không có payment nào → trả null (không coi là lỗi).
    var aPending = aPayments.FirstOrDefault(p => p.PaymentStatus == PaymentStatus.Pending);
    if (aPending is null) return null;

    aPending.MarkCompleted();
    await myPaymentRepository.UpdateAsync(aPending, ct);
    return aPending;
}
```

Chuỗi: `vnpay-return` → `CompletePaymentByOrderCommand(OrderId)` (Application) →
`IPaymentWriteService.CompleteByOrderAsync` (Domain) → repo. Đặt tính idempotent ở **Domain** để mọi lối
gọi (return URL **và** IPN, gọi bao nhiêu lần cũng vậy) đều an toàn.

**Giả định & lưu ý còn lại:**
- Luồng này giả định đơn **đã có một payment `Pending`** (tạo qua luồng tạo payment thường
  `POST /api/payments`). Nếu chưa có → `CompleteByOrderAsync` trả `null`, response có `PaymentCompleted=false`.
  Muốn tự tạo payment Pending ngay ở `vnpay/create` thì gọi thêm `CreatePaymentCommand` ở đó (chưa làm để
  giữ scope gọn — lưu ý order phải ở trạng thái Confirmed/Shipped theo rule hiện có).
- `vnp_TxnRef` đang dùng **OrderId** nên hoàn tất theo đơn. Nếu một đơn có **nhiều** payment online, nên
  đổi `vnp_TxnRef` sang **PaymentId** để callback trỏ đúng payment.
- **Nên gọi IPN** (server-to-server) làm nguồn cập nhật chính, vì khách có thể đóng tab trước khi
  return URL chạy. IPN và return dùng chung `VerifyCallback` + command nên đã idempotent sẵn.

---

## 8. Test trên Sandbox

1. Đăng ký tài khoản **VNPay Sandbox** (https://sandbox.vnpayment.vn) → lấy `TmnCode` + `HashSecret`.
2. Điền vào user-secrets. Giữ `BaseUrl` = URL sandbox.
3. `ReturnUrl` phải là URL VNPay gọi lại được. Chạy local thì dùng **ngrok** để có URL public:
   `ngrok http https://localhost:5001` → lấy URL `https://xxxx.ngrok.io/api/payments/vnpay-return`.
4. Gọi `POST /api/payments/vnpay/create` → mở `paymentUrl`.
5. Dùng **thẻ test** VNPay (NCB) trong tài liệu sandbox để trả.
6. VNPay redirect về `vnpay-return` → xem kết quả verify.

---

## 9. Chuyển sang môi trường THẬT (production)

| Việc | Sandbox → Live |
|---|---|
| `BaseUrl` | đổi sang URL pay chính thức của VNPay |
| `TmnCode` / `HashSecret` | dùng bộ **merchant thật** |
| `ReturnUrl` | domain thật (https), không phải localhost/ngrok |
| IPN | khai báo IPN URL thật với VNPay; xử lý cập nhật đơn ở đó |
| Bí mật | đưa vào biến môi trường / secret manager, **không** vào git |

Liên quan: [gateway-email-smtp.md](gateway-email-smtp.md), [gateway-invoice-pdf.md](gateway-invoice-pdf.md),
[polly-circuit-breaker.md](polly-circuit-breaker.md).
