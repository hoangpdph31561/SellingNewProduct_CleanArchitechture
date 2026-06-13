# 04 — Tầng API

API là tầng ngoài cùng: nhận HTTP request, gọi Domain/Repository, trả HTTP response. Nó **mỏng** —
không chứa business logic (business nằm ở Domain).

Luồng một request: `HTTP → Controller → (validate format) → tạo/gọi Domain entity → Repository lưu →
map sang Response DTO → HTTP`.

---

## A. DTO — `Contracts/*Dtos.cs`
DTO là object "biên giới" giữa thế giới HTTP và Domain. Dùng `record` (bất biến, gọn).
- `*Request` — dữ liệu **vào** (client gửi lên). Vd `CreateProductRequest`.
- `*Response` — dữ liệu **ra** (trả về client). Vd `ProductResponse`.
- `CommonDtos.cs` chứa `AddressDto` dùng chung.

💡 **Vì sao không trả thẳng Domain entity ra ngoài?**
- Domain entity có method, có invariant, setter private — không hợp để serialize.
- Tách DTO giúp đổi Domain mà không vỡ hợp đồng API (và ngược lại).
- Không lộ field nhạy cảm (vd `PasswordHash` không nằm trong `UserResponse`).

`OrderDtos.cs` đáng chú ý: có `CreateOrderRequest` (tạo đơn rỗng), `AddOrderDetailRequest` (thêm dòng),
`OrderResponse` chứa danh sách `OrderDetailResponse` — phản ánh cả aggregate ra ngoài.

---

## B. Validator — `Validators/RequestValidators.cs`
Dùng **FluentValidation**. Mỗi request có một class `: AbstractValidator<TRequest>`.
- 💡 Chỉ kiểm **format/shape**: bắt buộc, độ dài, khoảng giá trị, email đúng dạng. **KHÔNG** kiểm
  business (vd "đủ tồn kho không") — việc đó là của Domain.
- Vd `CreateProductRequestValidator`: `Name` không rỗng, `Price > 0`, `Currency` đúng 3 ký tự...
- `AddressDtoValidator` được tái dùng qua `SetValidator(...)` trong các request có địa chỉ.

💡 Ranh giới rõ ràng: validator chặn **rác đầu vào sớm** (trả 400 trước khi đụng Domain); Domain mới
là nơi giữ **luật nghiệp vụ thật**.

---

## C. Mapping — `Mapping/ApiMappings.cs`
Các extension method `ToResponse()` map **Domain → Response DTO** (map tay).
- Vd `theProduct.ToResponse()` đọc `Sku.Value`, `Price.Amount`, `Price.Currency`... thành DTO phẳng.
- `ToDto()` cho `Address`. `Order.ToResponse()` map cả danh sách `Details`.
- Chiều ngược lại (Request → Domain) **không** ở đây — nó nằm trong controller, vì cần gọi factory
  `Create(...)` có validate (vd `Money.Create`, `Address.Create`).

---

## D. Security — `Security/PasswordHasher.cs`
Hàm `Hash(thePassword)` băm SHA256 → base64.
- 💡 **Chỉ cho demo.** App thật phải dùng KDF có salt + chậm (PBKDF2/bcrypt/Argon2). Comment trong
  file ghi rõ điều này.

---

## E. Middleware — `Middleware/ExceptionHandlingMiddleware.cs`
Bắt exception toàn cục, đổi thành response chuẩn **ProblemDetails** (RFC 7807):
- `ValidationException` (FluentValidation) → **400**, gộp các lỗi format.
- `DomainException` (vi phạm business) → **400**, kèm message nghiệp vụ.
- Exception khác → **500** + ghi log.
- **`InvokeAsync(theContext)`** — bọc `myRequestDelegate` trong try/catch.
- **`WriteProblemAsync(...)`** — ghi JSON `application/problem+json`.

💡 Nhờ middleware này, controller **không cần try/catch** — cứ để Domain ném `DomainException`, middleware
lo dịch sang HTTP. Code controller nhờ vậy rất sạch.

---

## F. Controllers — `Controllers/*Controller.cs`
`[ApiController]`, route `api/[controller]`. Inject repository + validator qua constructor. Mỏng và
theo một khuôn:

```csharp
[HttpPost]
public async Task<ActionResult<XResponse>> Create(CreateXRequest theRequest, CancellationToken ct)
{
    await myCreateValidator.ValidateAndThrowAsync(theRequest, ct);   // 1) chặn format sai → 400
    // 2) (nếu cần) kiểm tồn tại bản ghi liên quan → 404
    var aEntity = X.Create(...);                                     // 3) tạo qua Domain factory (giữ luật)
    await myRepository.AddAsync(aEntity, ct);                        // 4) lưu (SQL hay Mongo tuỳ DI)
    return CreatedAtAction(nameof(GetById), new { theId = aEntity.Id }, aEntity.ToResponse());
}
```

Theo từng controller:
- **`CategoriesController`** — CRUD đơn giản nhất, đọc trước để nắm khuôn.
- **`ProductsController`** — khi tạo, kiểm `CategoryId` có tồn tại không (404 nếu không).
- **`UsersController`** — 💡 có `using DomainUser = ...User;` vì `ControllerBase` đã có sẵn property tên
  `User` (ClaimsPrincipal); không alias sẽ bị nhầm tên. Mật khẩu băm trước khi vào Domain.
- **`EmployeesController`** — kiểm `UserId` tồn tại.
- **`CustomersController`** — dựng `Address.Create(...)` từ `AddressDto`.
- **`OrdersController`** (quan trọng) — minh họa luồng nghiệp vụ nhiều bước:
  - `Create` — kiểm customer + employee tồn tại, tạo đơn `Draft`.
  - `AddDetail` — nạp Order, nạp Product, gọi `aOrder.AddDetail(aProduct, qty)` (Domain giữ luật:
    chỉ thêm khi Draft, snapshot giá), rồi `UpdateAsync`.
  - `Confirm` / `Ship` / `Cancel` — nạp Order, gọi đúng method Domain, lưu. 💡 Nếu vi phạm (vd confirm
    đơn rỗng) Domain ném `DomainException` → middleware trả 400, controller không phải xử lý gì thêm.
- **`PaymentsController`** — `Create` (kiểm order tồn tại), `Complete` (gọi `MarkCompleted`).

💡 Để ý: controller **không bao giờ** đụng `DbContext` hay biết SQL/Mongo. Nó chỉ nói chuyện với
interface repository và Domain entity.

---

## G. `Program.cs` — Composition Root
Nơi "ráp" mọi thứ lại và là **chỗ DUY NHẤT** biết đang dùng DB nào:
1. `AddControllers()`, `AddOpenApi()`.
2. `AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>()` — tự nạp mọi validator.
3. Đọc `DatabaseProvider` từ config, rồi:
   ```csharp
   if (provider == "MongoDB") AddMongoInfrastructure(config);
   else                        AddSqlServerInfrastructure(config);
   ```
4. `UseMiddleware<ExceptionHandlingMiddleware>()`, `MapControllers()`.

💡 Đổi một chữ `"SqlServer"` ↔ `"MongoDB"` trong [appsettings.json](../../SellingNewProduct.API/appsettings.json)
là đổi toàn bộ tầng lưu trữ — Domain và Controller **không sửa gì**. Đây là đích đến của cả dự án.

---

## H. `appsettings.json`
- `DatabaseProvider` — `"SqlServer"` hoặc `"MongoDB"`.
- `ConnectionStrings:SqlServer` — LocalDB (sửa cho khớp máy bạn).
- `ConnectionStrings:MongoDB` + `MongoDatabaseName` — cho Mongo (cần Mongo chạy ở `localhost:27017`).
