# Gateway Email — gửi mail thật qua SMTP (MailKit)

Tài liệu này hướng dẫn cấu hình gửi **email thật** thay cho bản log giả (`LoggingEmailSender`).

- **Port**: `IEmailSender` (`Infrastructure.Messaging/Abstractions/Ports.cs`)
- **Adapter thật**: `SmtpEmailSender` (`Infrastructure.Messaging/Services/SmtpEmailSender.cs`) — dùng **MailKit**
- **Adapter giả**: `LoggingEmailSender` — chỉ log ra console (mặc định, để chạy không cần credential)

> Email chạy như một **command** qua RabbitMQ: `OutboxDispatcher` → queue `email.send` →
> `EmailCommandWorker` → `IEmailSender`. Toàn bộ lời gọi này **nằm sau circuit breaker** (xem
> [polly-circuit-breaker.md](polly-circuit-breaker.md)), nên SMTP chậm/chết sẽ bị ngắt, không treo worker.

---

## 1. Bật adapter thật

Trong `appsettings.json`, đặt cờ `Gateways:UseReal = true` và điền section `Gateways:Smtp`:

```jsonc
"Gateways": {
  "UseReal": true,                 // false → dùng LoggingEmailSender (không gửi thật)
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseStartTls": true,           // true = 587 (STARTTLS); false = 465 (SSL ngầm)
    "User": "you@gmail.com",
    "Password": "",                // KHÔNG commit — dùng user-secrets / biến môi trường
    "FromAddress": "you@gmail.com",
    "FromName": "SellingNewProduct"
  }
}
```

Cơ chế chọn adapter nằm ở `RegisterSideEffects()` trong
`Infrastructure.Messaging/DependencyInjection.cs`:

```csharp
if (aUseReal) theServices.AddScoped<IEmailSender, SmtpEmailSender>();
else          theServices.AddScoped<IEmailSender, LoggingEmailSender>();
```

---

## 2. KHÔNG commit mật khẩu — dùng user-secrets

Mật khẩu SMTP là **bí mật**. Đừng để trong `appsettings.json`. Cách chuẩn khi dev:

```bash
cd SellingNewProduct.API
dotnet user-secrets init
dotnet user-secrets set "Gateways:Smtp:Password" "app-password-cua-ban"
dotnet user-secrets set "Gateways:Smtp:User" "you@gmail.com"
```

Khi deploy: đặt qua **biến môi trường** (ASP.NET Core tự map dấu `:` thành `__`):

```
Gateways__Smtp__Password=...
Gateways__UseReal=true
```

---

## 3. Cấu hình theo nhà cung cấp

| Nhà cung cấp | Host | Port | UseStartTls | Ghi chú |
|---|---|---|---|---|
| **Gmail** | smtp.gmail.com | 587 | true | Cần **App Password** (bật 2FA rồi tạo), không dùng mật khẩu thường |
| **Outlook/Office365** | smtp.office365.com | 587 | true | |
| **SendGrid (SMTP)** | smtp.sendgrid.net | 587 | true | User = `apikey`, Password = API key |
| **Mailtrap** (test) | sandbox.smtp.mailtrap.io | 587 | true | Hộp thư ảo để **test** — mail không đi ra ngoài |
| **MailHog / Papercut** (local) | localhost | 1025 | false | SMTP giả chạy máy bạn, không cần auth |

> **Khuyến nghị khi học/test:** dùng **Mailtrap** hoặc **MailHog** để thấy mail "gửi" mà không spam thật.

---

## 4. Adapter làm gì (tóm tắt code)

`SmtpEmailSender.SendAsync` dựng `MimeMessage` (From/To/Subject/Body HTML), kết nối, xác thực rồi gửi:

```csharp
var aSocketOptions = myOptions.UseStartTls
    ? SecureSocketOptions.StartTls        // cổng 587
    : SecureSocketOptions.SslOnConnect;   // cổng 465
await aClient.ConnectAsync(myOptions.Host, myOptions.Port, aSocketOptions, ct);
if (!string.IsNullOrEmpty(myOptions.User))
    await aClient.AuthenticateAsync(myOptions.User, myOptions.Password, ct);
await aClient.SendAsync(aMime, ct);
await aClient.DisconnectAsync(true, ct);
```

Body đang set là `TextPart("html")` → nội dung là **HTML**. Muốn plain-text thì đổi thành `"plain"`.

---

## 5. Cách test nhanh

1. Bật MailHog: `docker run -p 1025:1025 -p 8025:8025 mailhog/mailhog` (UI ở http://localhost:8025).
2. Đặt config: `UseReal=true`, `Host=localhost`, `Port=1025`, `UseStartTls=false`, `User=""`.
3. Kích hoạt một luồng gửi mail (vd đặt/confirm đơn → sinh `SendEmailCommand`).
4. Mở http://localhost:8025 xem mail. Log app in `📧 SMTP ▶ sent '...' to ...`.

---

## 6. Sự cố thường gặp

| Triệu chứng | Nguyên nhân / cách xử lý |
|---|---|
| `AuthenticationException` với Gmail | Chưa dùng **App Password**, hoặc chưa bật 2FA |
| Treo rồi timeout | Sai `Port`/`UseStartTls` (587↔true, 465↔false). Breaker sẽ ngắt sau vài lần lỗi |
| Mail vào Spam | `FromAddress` không khớp domain đã xác thực (SPF/DKIM) — bình thường khi test |
| Cảnh báo `NU1902` (MailKit/MimeKit) | Advisory mức moderate, **chưa** có bản vá mới hơn — chỉ là warning, build vẫn xanh |

Liên quan: [gateway-invoice-pdf.md](gateway-invoice-pdf.md), [polly-circuit-breaker.md](polly-circuit-breaker.md).
