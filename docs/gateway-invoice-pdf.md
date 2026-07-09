# Gateway Invoice — xuất hoá đơn PDF thật (QuestPDF)

Tài liệu này hướng dẫn cấu hình xuất **hoá đơn PDF thật** thay cho bản log giả (`LoggingInvoiceIssuer`).

- **Port**: `IInvoiceIssuer` (`Infrastructure.Messaging/Abstractions/Ports.cs`)
- **Adapter thật**: `PdfInvoiceIssuer` (`Infrastructure.Messaging/Services/PdfInvoiceIssuer.cs`) — dùng **QuestPDF**
- **Adapter giả**: `LoggingInvoiceIssuer` — chỉ log (mặc định)

> Cũng như email, invoice chạy như **command** qua RabbitMQ: queue `invoice.issue` →
> `InvoiceCommandWorker` → `IInvoiceIssuer`, **nằm sau circuit breaker**.

---

## 1. Bật adapter thật

Cùng cờ `Gateways:UseReal = true` với email; thêm section `Gateways:Invoice`:

```jsonc
"Gateways": {
  "UseReal": true,
  "Invoice": {
    "OutputDirectory": "invoices",                        // thư mục lưu PDF (tự tạo nếu chưa có)
    "SellerName": "SellingNewProduct JSC",
    "SellerTaxCode": "0100000000",
    "SellerAddress": "123 Le Loi, District 1, Ho Chi Minh City"
  }
}
```

`OutputDirectory` là đường dẫn tương đối → tính từ **content root** của app (thư mục chạy). Đặt đường
dẫn tuyệt đối nếu muốn cố định (vd `"D:\\invoices"`).

---

## 2. QuestPDF & giấy phép (QUAN TRỌNG)

QuestPDF miễn phí theo giấy phép **Community** cho cá nhân & doanh nghiệp nhỏ. Bắt buộc khai báo **một
lần** khi khởi động, nếu không sẽ ném exception. Đã set sẵn trong static constructor của adapter:

```csharp
static PdfInvoiceIssuer()
{
    QuestPDF.Settings.License = LicenseType.Community;
}
```

> Nếu công ty vượt ngưỡng doanh thu của Community, cần mua license Professional — xem trang QuestPDF.

---

## 3. Adapter làm gì

`PdfInvoiceIssuer.IssueAsync`:
1. Tạo thư mục output nếu chưa có.
2. Sinh **số hoá đơn** từ `PaymentId`: `INV-<paymentId>` → tên file `INV-....pdf`.
3. Dựng PDF bằng QuestPDF (header người bán + INVOICE, phần thân: số HĐ, ngày, mã đơn, mã thanh toán,
   số tiền, footer cảm ơn).
4. Ghi file ra đĩa, log đường dẫn: `🧾 PDF invoice INV-... written to ...`.

```csharp
var aInvoiceNumber = $"INV-{theRequest.PaymentId:N}".ToUpperInvariant();
var aPath = Path.Combine(myOptions.OutputDirectory, $"{aInvoiceNumber}.pdf");
var aBytes = BuildPdf(theRequest, aInvoiceNumber);
await File.WriteAllBytesAsync(aPath, aBytes, ct);
```

---

## 4. Dữ liệu invoice hiện có (và giới hạn)

Port `InvoiceRequest` chỉ mang: `OrderId`, `PaymentId`, `Amount`, `Currency`. Vì vậy PDF hiện tại là
hoá đơn **tổng tiền** (không có dòng sản phẩm chi tiết). Đây là chủ ý để **không đổi contract** của
outbox/command.

**Muốn có bảng dòng sản phẩm (line items)?** Cần:
1. Mở rộng `IssueInvoiceCommand` + `InvoiceRequest` thêm danh sách item (SKU, tên, SL, đơn giá).
2. Nơi phát command (khi thanh toán hoàn tất) nạp các dòng đơn hàng vào command.
3. Vẽ thêm `Table` trong `BuildPdf`.

> Giữ nguyên nếu chỉ cần chứng từ tổng; mở rộng khi cần hoá đơn đầy đủ theo quy định.

---

## 5. Tuỳ biến giao diện PDF nhanh

Trong `BuildPdf(...)` — vài chỗ hay chỉnh:

| Muốn | Sửa |
|---|---|
| Khổ giấy | `thePage.Size(PageSizes.A4)` → `A5`, `Letter`… |
| Lề | `thePage.Margin(40)` |
| Logo | thêm `theCol.Item().Image(bytes)` trong header (nhúng byte ảnh) |
| Màu tiêu đề | `.FontColor(Colors.Blue.Darken2)` |
| Thêm dòng | thêm `theCol.Item().Text(...)` trong `Content()` |

---

## 6. Cách test nhanh

1. Đặt `UseReal=true`, `Gateways:Invoice:OutputDirectory` = thư mục dễ tìm.
2. Kích hoạt luồng phát hành hoá đơn (vd thanh toán hoàn tất → `IssueInvoiceCommand`).
3. Mở thư mục output → có file `INV-....pdf`. Log in đường dẫn.

---

## 7. Sự cố thường gặp

| Triệu chứng | Xử lý |
|---|---|
| Exception về license khi sinh PDF | Thiếu `QuestPDF.Settings.License = LicenseType.Community` (đã set sẵn — kiểm tra nếu tự copy code) |
| `UnauthorizedAccessException` khi ghi file | App không có quyền ghi vào `OutputDirectory`; đổi thư mục hoặc cấp quyền |
| PDF trống/thiếu chữ | Kiểm tra dữ liệu trong `InvoiceRequest` truyền vào |

Liên quan: [gateway-email-smtp.md](gateway-email-smtp.md), [polly-circuit-breaker.md](polly-circuit-breaker.md).
