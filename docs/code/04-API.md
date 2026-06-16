# 04 — Tầng API

API là tầng ngoài cùng: nhận HTTP request, gọi **Domain Service** (ghi) hoặc **read query** (đọc), trả HTTP
response. Nó **mỏng** — không chứa business logic, KHÔNG inject repository, KHÔNG tự `new` entity.

Luồng một request ghi:
`HTTP → Controller → (validate format) → map Request → Command → I*Service (Domain) → Repository lưu →
service trả entity → map sang Response DTO → **bọc ApiResponse** → HTTP`.

---

## A. DTO — `Contracts/*Dtos.cs`
DTO là object "biên giới" giữa thế giới HTTP và Domain. Dùng `record` (bất biến, gọn).
- `*Request` — dữ liệu **vào** (client gửi lên). Vd `CreateProductRequest`.
- `*Response` — dữ liệu **ra** (trả về client). Vd `ProductResponse`.
- `CommonDtos.cs` chứa `AddressDto` dùng chung.
- `ApiResponse.cs` — **envelope chuẩn** bọc mọi response (xem mục E). Có bản `ApiResponse` (lỗi, `result` là
  `object?`) và `ApiResponse<T>` (typed, để OpenAPI mô tả payload).

💡 **Vì sao không trả thẳng Domain entity ra ngoài?**
- Domain entity có method, có invariant, setter private — không hợp để serialize.
- Tách DTO giúp đổi Domain mà không vỡ hợp đồng API (và ngược lại).
- Không lộ field nhạy cảm (vd `PasswordHash` không nằm trong `UserResponse`).

---

## B. Validator — `Validators/RequestValidators.cs`
Dùng **FluentValidation**. Mỗi request có một class `: AbstractValidator<TRequest>`.
- 💡 Chỉ kiểm **format/shape**: bắt buộc, độ dài, khoảng giá trị, email đúng dạng. **KHÔNG** kiểm
  business (vd "đủ tồn kho không") — việc đó là của Domain.
- Vd `CreateProductRequestValidator`: `Name` không rỗng, `Price > 0`, `Currency` đúng 3 ký tự...
- `AddressDtoValidator` được tái dùng qua `SetValidator(...)` trong các request có địa chỉ.

💡 Validator chặn **rác đầu vào sớm** (ném `ValidationException` → 400 trước khi đụng Domain); Domain mới
là nơi giữ **luật nghiệp vụ thật**.

---

## C. Input objects — Command & Query (ở Domain)
Port của Domain **không** nhận danh sách tham số dài; chúng nhận **một object**:
- **Command** (`Domain/Commands/Create*Command`) — input cho `I*Service.CreateAsync`.
- **Query** (`Domain/Queries/*SearchQuery`) — input cho `I*Queries.SearchAsync` (filter + paging + sort).

Controller dịch DTO của HTTP sang các object này:
- **Write**: `theRequest.ToCommand()` (map tay, xem mục D) rồi `service.CreateAsync(command, ct)`.
- **Read**: bind thẳng `[FromQuery] XxxSearchQuery theQuery` — ASP.NET tự nhồi từ query-string theo **tên
  property** (PascalCase), vd `?Name=ao&Page=2&SortBy=price`.

💡 Lợi: thêm filter mới chỉ sửa record, **không đổi chữ ký** port; an toàn compile-time; không cần AutoMapper.

---

## D. Mapping — `Mapping/ApiMappings.cs`
Các extension method map tay tại biên giới API:
- **Request → Command**: `ToCommand()`. Vd `CreateCustomerRequest.ToCommand()` trải `Address.Street/Ward/...`
  thành các field phẳng của `CreateCustomerCommand` (Command **không** tham chiếu DTO của API).
- **Domain → Response**: `ToResponse()`. Vd `theProduct.ToResponse()` đọc `Sku.Value`, `Price.Amount`... thành DTO phẳng.

💡 Giữ map tay (không AutoMapper): tường minh, lỗi mapping lộ lúc **compile**, bảo toàn encapsulation.

---

## E. ApiResponse envelope — vì sao Controller trả `Ok(dto)`/`CreatedAtAction(...)` mà client lại nhận envelope?

Đây là phần hay nhầm. Controller **không tự dựng** envelope. Có **2 nơi** dựng nó, ở 2 đường khác nhau:

```
                         ┌──────────────────────── ExceptionHandlingMiddleware (ngoài cùng) ─┐
   HTTP request ───────► │  try {                                                            │
                         │     ... MVC pipeline ...                                          │
                         │        Controller action  ──(thành công)──►  IActionResult        │
                         │            │                                     │                │
                         │            │                          ApiResponseWrapperFilter    │ ◄── ĐƯỜNG THÀNH CÔNG
                         │            │                          (Result Filter) bọc lại     │
                         │            │                                     │                │
                         │            └──(ném Exception)──────────────┐     ▼                │
                         │  } catch (Domain/Conflict/NotFound/...) {   │  ApiResponse JSON    │
                         │     map type → HTTP status + ApiResponse ◄──┘                     │ ◄── ĐƯỜNG LỖI
                         │  }                                                                │
                         └───────────────────────────────────────────────────────────────────┘
```

### E.1 — Đường THÀNH CÔNG: `ApiResponseWrapperFilter` (`Filters/`)
Controller `return Ok(dto)` / `CreatedAtAction(...)` / `NotFound()` như bình thường. Những thứ này chỉ là
**mô tả** kết quả (một `IActionResult`), **chưa** phải JSON đã gửi đi. Trước khi MVC serialize, nó chạy
**result filter**. Filter của ta bắt lấy kết quả đó và **viết lại**:

```csharp
public void OnResultExecuting(ResultExecutingContext theContext)
{
    switch (theContext.Result)
    {
        case ObjectResult aObjectResult:                       // Ok(dto), CreatedAtAction(dto)...
            var aStatusCode = aObjectResult.StatusCode ?? 200; // 200, hoặc 201 nếu là Created
            aObjectResult.Value = Wrap(aStatusCode, aObjectResult.Value); // .Value: dto → ApiResponse{...,result=dto}
            break;
        case StatusCodeResult aStatusCodeResult:               // NotFound(), NoContent()... (không có body)
            ... // thay bằng ObjectResult chứa ApiResponse; 204 → 200 (vì 204 không được có body)
    }
}
```

Cụ thể với `CreatedAtAction(nameof(GetById), new { theId = aProduct.Id }, aProduct.ToResponse())`:
- `CreatedAtActionResult` **là** một `ObjectResult`, `StatusCode = 201`, `Value = ProductResponse`.
- Filter lấy `Value` (DTO) nhét vào `ApiResponse.Result`, đặt `StatusCode = 201`, `IsSuccess = true`.
- HTTP vẫn **201 Created** (giữ cả header `Location`), nhưng **body** giờ là:
  ```json
  { "statusCode": 201, "isSuccess": true, "errorMessages": [], "result": { /* ProductResponse */ } }
  ```
- `Ok(dto)` → 200 y hệt; `NotFound()` → 404 với `isSuccess=false`, `errorMessages=["Resource not found."]`, `result=null`.

👉 Nhờ vậy controller **không phải sửa gì**: vẫn `Ok`/`CreatedAtAction`/`NotFound` quen thuộc, filter lo việc bọc đồng nhất.

### E.2 — Đường LỖI: `Middleware/ExceptionHandlingMiddleware.cs`
Khi Domain ném exception (vd `CategoryService` thấy trùng tên → `ConflictException`), action **không** trả
`IActionResult` nào cả → **result filter KHÔNG chạy**. Exception "nổi" lên tới middleware ngoài cùng. Middleware
`try/catch` map **kiểu exception → HTTP status** rồi tự dựng `ApiResponse` lỗi:

| Exception (Domain — HTTP-free) | HTTP | Ý nghĩa |
|---|---|---|
| `ValidationException` (FluentValidation) | **400** | sai format đầu vào (gộp mọi lỗi vào `errorMessages`) |
| `DomainException` | **400** | vi phạm luật nghiệp vụ |
| `ConflictException` | **409** | xung đột/trùng (vd tên đã tồn tại) |
| `NotFoundException` | **404** | bản ghi liên quan không tồn tại |
| còn lại | **500** | lỗi ngoài dự kiến (có ghi log) |

```csharp
catch (ConflictException aConflictException)
{
    await WriteAsync(theContext, HttpStatusCode.Conflict, new[] { aConflictException.Message });
}
// → body:
// { "statusCode": 409, "isSuccess": false, "errorMessages": ["Category 'Áo' already exists."], "result": null }
```

💡 **Điểm mấu chốt về "trả lỗi HTTP":** Domain **không biết HTTP** — nó chỉ ném exception mang *ý nghĩa nghiệp
vụ* (`ConflictException`, `NotFoundException`...). Việc quy ra **mã HTTP** là quyết định của **tầng API**, gói gọn
ở middleware. Muốn thêm mã mới (vd 422, 403) → thêm một `catch` ở đây + (nếu cần) một exception HTTP-free mới ở
`Domain/Common`. Đây là lý do `HttpStatusCode` **không** được nhét vào exception của Domain (giữ Domain độc lập transport).

💡 Vì có 2 cơ chế này, controller **không cần try/catch** và **không tự new `ApiResponse`** — code rất sạch.

---

## F. Security — `Security/PasswordHasher.cs`
Hàm `Hash(thePassword)` băm SHA256 → base64. Implement interface `IPasswordHasher` (khai báo ở
`Domain/Abstractions`), đăng ký DI trong `AddApiServices()`.
- 💡 **Ví dụ hướng phụ thuộc:** Domain định nghĩa hợp đồng `IPasswordHasher`, tầng API cung cấp cách làm.
  `UserService` (Domain) băm mật khẩu qua interface, không biết thuật toán cụ thể.
- 💡 **Chỉ cho demo.** App thật phải dùng KDF có salt + chậm (PBKDF2/bcrypt/Argon2).

---

## G. Controllers — `Controllers/*Controller.cs`
`[ApiController]`, route `api/[controller]`. Inject **Domain Service** (`I*Service`) + (nếu có endpoint đọc)
**read query** (`I*Queries`) + validator qua constructor. Mỏng và theo một khuôn:

```csharp
[HttpPost]
public async Task<ActionResult<XResponse>> Create(CreateXRequest theRequest, CancellationToken ct)
{
    await myCreateValidator.ValidateAndThrowAsync(theRequest, ct);          // 1) format sai → ném → 400
    var aEntity = await myXService.CreateAsync(theRequest.ToCommand(), ct); // 2) Domain lo business + lưu
    return CreatedAtAction(nameof(GetById), new { theId = aEntity.Id }, aEntity.ToResponse()); // 3) filter bọc → 201 + envelope
}

[HttpGet("search")]
public async Task<ActionResult<PagedResult<XSummaryView>>> Search([FromQuery] XSearchQuery theQuery, CancellationToken ct)
    => Ok(await myXQueries.SearchAsync(theQuery, ct));                      // read side, filter bọc → 200 + envelope
```

💡 Controller **không** còn `X.Create(...)`, `repository.AddAsync(...)`, cũng **không** dựng `ApiResponse`. Mọi
logic ghi (gồm kiểm tồn tại bản ghi liên quan → ném `NotFoundException` → 404; trùng → `ConflictException` → 409)
nằm trong Domain Service.

Theo từng controller:
- **`CategoriesController`** — đọc trước. `CategoryService.CreateAsync` check trùng tên → **`ConflictException` (409)**.
  Endpoint đọc: `summaries`, `search` (`ICategoryQueries`).
- **`ProductsController`** — `ProductService` kiểm `CategoryId` tồn tại. `search` + `{id}/summary` dùng `IProductQueries`.
- **`UsersController`** — `UserService` băm mật khẩu qua `IPasswordHasher` rồi `User.Create`.
- **`EmployeesController`** — `EmployeeService` kiểm `UserId` tồn tại. `search` dùng `IEmployeeQueries` (kèm số đơn đã bán).
- **`CustomersController`** — `CustomerService.CreateAsync` dựng `Address`/`Email`; `search`, `top` (`ICustomerQueries`),
  `{id}/orders` (`IOrderQueries`).
- **`OrdersController`** (quan trọng) — `OrderService` gói luồng nhiều bước:
  - `Create` — kiểm customer + employee tồn tại, tạo đơn `Draft`.
  - `AddDetail` — nạp Order + Product, gọi `AddDetail` (Domain giữ luật: chỉ thêm khi Draft, snapshot giá), lưu.
  - `Confirm` / `Ship` / `Cancel` — nạp Order, gọi method Domain, lưu. 💡 Đơn không tồn tại → `NotFoundException`
    (404); sai trạng thái → `DomainException` (400).
  - Đọc: `{id}/view`, danh sách (`Search`), `status-breakdown` (`IOrderQueries`).
- **`PaymentsController`** — `PaymentService`: `Create` (kiểm order tồn tại), `Complete`. Đọc: `search`, `outstanding-orders`.
- **`ReportsController`** — chỉ đọc: `IReportQueries` (best-selling, employee-sales, sales-by-category, daily-sales, low-stock).

💡 Controller **không bao giờ** đụng `DbContext`, repository, hay biết SQL/Mongo. Nó chỉ nói chuyện với Domain
Service và read query.

---

## H. OpenAPI/Swagger — `OpenApi/ApiResponseOperationTransformer.cs`
Vì filter bọc envelope **lúc runtime** còn controller vẫn khai báo kiểu trả về là DTO gốc, document OpenAPI sẽ
"nói dối" nếu không can thiệp. `ApiResponseOperationTransformer` (`IOpenApiOperationTransformer`) sửa schema mọi
response **2xx** thành hình envelope, với schema DTO gốc lồng dưới `result`. Đăng ký:
`AddOpenApi(o => o.AddOperationTransformer<ApiResponseOperationTransformer>())`.

💡 Nhờ vậy Swagger hiển thị đúng `{ statusCode, isSuccess, errorMessages, result: <DTO> }` cho từng endpoint.

---

## I. DI từng tầng — `DependencyInjection.cs` + `Program.cs`

Mỗi tầng có **một** extension đăng ký dịch vụ của riêng nó (đối xứng nhau):

| Tầng | Extension |
|---|---|
| API | `AddApiServices()` — controllers + `ApiResponseWrapperFilter`, OpenAPI + transformer, validators, `IPasswordHasher` |
| Domain | `AddDomainServices()` |
| Infra SqlServer | `AddSqlServerInfrastructure(config)` |
| Infra MongoDB | `AddMongoInfrastructure(config)` |

`Program.cs` là **Composition Root** — nơi "ráp" và là **chỗ DUY NHẤT** biết đang dùng DB nào:

```csharp
builder.Services.AddApiServices();      // presentation
builder.Services.AddDomainServices();   // business logic

var aProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
if (string.Equals(aProvider, "MongoDB", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddMongoInfrastructure(builder.Configuration);   // chọn adapter Mongo
else
    builder.Services.AddSqlServerInfrastructure(builder.Configuration); // hoặc adapter SqlServer

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();  // đường lỗi (mục E.2) — đặt ngoài cùng
...
```

💡 Phần chọn DB **cố ý** ở `Program.cs`, không nhét vào `AddApiServices` — chọn adapter là việc của composition
root; nếu API tự chọn thì tầng API lại phụ thuộc cả 2 Infra một cách không cần thiet.

💡 Đổi một chữ `"SqlServer"` ↔ `"MongoDB"` trong [appsettings.json](../../SellingNewProduct.API/appsettings.json)
là đổi toàn bộ tầng lưu trữ — Domain và Controller **không sửa gì**. Đây là đích đến của cả dự án.

---

## J. `appsettings.json`
- `DatabaseProvider` — `"SqlServer"` hoặc `"MongoDB"`.
- `ConnectionStrings:SqlServer` — LocalDB (sửa cho khớp máy bạn).
- `ConnectionStrings:MongoDB` + `MongoDatabaseName` — cho Mongo (cần Mongo chạy ở `localhost:27017`).
