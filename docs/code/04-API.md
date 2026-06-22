# 04 — Tầng API

API là tầng ngoài cùng: nhận HTTP request, **gửi Command/Query qua MediatR (`ISender`)** tới tầng
Application, trả HTTP response. Nó **mỏng** — không chứa business logic, KHÔNG inject service/repository,
KHÔNG inject validator, KHÔNG tự `new` entity.

Luồng một request ghi:
`HTTP → Controller → map Request → Command → mySender.Send(command) → [Application: ValidationBehavior →
Handler → Domain Service → Repository lưu] → trả entity → map sang Response DTO → **bọc ApiResponse** → HTTP`.

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

## B. Validator — ở tầng Application (chạy TỰ ĐỘNG qua MediatR pipeline)
Dùng **FluentValidation**, nhưng validator **không** ở API nữa — nó nằm cạnh command/query trong tầng
Application (cùng file `.cs`), vd `CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>`.
- 💡 Chỉ kiểm **format/shape**: bắt buộc, độ dài, khoảng giá trị, email đúng dạng. **KHÔNG** kiểm
  business (vd "đủ tồn kho không") — việc đó là của Domain.
- `PlaceOrderCommandValidator` dùng `RuleForEach(x => x.Items)` validate từng dòng đặt hàng;
  `CreateManyProductsCommandValidator` tương tự cho tạo nhiều sản phẩm.

💡 **Controller KHÔNG inject validator.** `ValidationBehavior<,>` (một `IPipelineBehavior` của MediatR,
đăng ký làm bước **ngoài cùng** trong `AddApplicationServices`) chạy trước mọi handler: gom mọi
`IValidator<TRequest>`, validate; lỗi → ném `ValidationException` → `ExceptionHandlingMiddleware` đổi 400.
Request không có validator thì đi thẳng. Chi tiết: [05-Application.md](05-Application.md) mục C.

---

## C. Input objects — Command & Query (ở Application)
Controller **không** truyền danh sách tham số dài; nó gửi **một message** qua `ISender`:
- **Command** (`Application/<Aggregate>/Commands/*Command`, là `IRequest<T>`) — vd `CreateCategoryCommand`,
  `PlaceOrderCommand` (gồm danh sách `OrderItemCommand`), `ConfirmOrderCommand`.
- **Query** (`Application/<Aggregate>/Queries/*Query`) — vd `GetCategoryByIdQuery`, `SearchProductsQuery`
  (bọc `*SearchQuery` của Domain: filter + paging + sort).

Controller dịch DTO của HTTP sang message rồi gửi đi:
- **Write**: `mySender.Send(theRequest.ToCommand(), ct)` (map tay Request→Command, xem mục D).
- **Read theo id**: `mySender.Send(new GetXByIdQuery(theId), ct)`.
- **Search**: bind `[FromQuery] XxxSearchQuery theQuery` (ASP.NET nhồi từ query-string theo **tên property**
  PascalCase, vd `?Name=ao&Page=2&SortBy=price`) rồi `mySender.Send(new SearchXQuery(theQuery), ct)`.

💡 Lợi: thêm filter mới chỉ sửa record, **không đổi chữ ký**; an toàn compile-time; không cần AutoMapper.

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
`Domain/Interfaces/Outbound`), đăng ký DI trong `AddApiServices()`.
- 💡 **Ví dụ hướng phụ thuộc:** Domain định nghĩa hợp đồng `IPasswordHasher`, tầng API cung cấp cách làm.
  `UserService` (Domain) băm mật khẩu qua interface, không biết thuật toán cụ thể.
- 💡 **Chỉ cho demo.** App thật phải dùng KDF có salt + chậm (PBKDF2/bcrypt/Argon2).

---

## G. Controllers — `Controllers/*Controller.cs`
`[ApiController]`, route `api/[controller]`. Inject **DUY NHẤT** `ISender` (MediatR). **Không** inject
service/repository/validator. Mỏng và theo một khuôn:

```csharp
private readonly ISender mySender;

[HttpPost]
public async Task<ActionResult<XResponse>> Create(CreateXRequest theRequest, CancellationToken ct)
{
    // ValidationBehavior (Application) kiểm format TRƯỚC khi handler chạy (sai → 400).
    var aEntity = await mySender.Send(theRequest.ToCommand(), ct);          // → Handler → Domain Service
    return CreatedAtAction(nameof(GetById), new { theId = aEntity.Id }, aEntity.ToResponse()); // filter bọc → 201 + envelope
}

[HttpGet("search")]
public async Task<ActionResult<PagedResult<XSummaryView>>> Search([FromQuery] XSearchQuery theQuery, CancellationToken ct)
    => Ok(await mySender.Send(new SearchXQuery(theQuery), ct));             // Query → Handler → I*ReadService
```

💡 Controller **không** còn `X.Create(...)`, `service/repository.AddAsync(...)`, `ValidateAndThrowAsync(...)`,
cũng **không** dựng `ApiResponse`. Mọi logic ghi/đọc nghiệp vụ (kiểm tồn tại → `NotFoundException` 404;
trùng/đụng trạng thái/không đủ tồn kho → `ConflictException` 409) nằm trong Domain Service; handler ở
Application chỉ điều phối.

Theo từng controller (command/query gửi đi → handler → Domain Service tương ứng):
- **`CategoriesController`** — `CreateCategoryCommand` → `CategoryWriteService` check trùng tên → **`ConflictException` (409)**.
  Đọc: `GetAllCategoriesQuery`, `GetCategorySummariesQuery`, `SearchCategoriesQuery`, `GetCategoryByIdQuery`.
- **`ProductsController`** — `CreateProductCommand` kiểm `CategoryId` tồn tại + **SKU không trùng**;
  `CreateManyProductsCommand` (AddRange). Đọc: `Search`, `{id}/summary`.
- **`UsersController`** — `CreateUserCommand` → `UserWriteService` băm mật khẩu qua `IPasswordHasher` rồi `User.Create`.
- **`EmployeesController`** — `CreateEmployeeCommand` kiểm `UserId` tồn tại. `SearchEmployeesQuery` (kèm số đơn đã bán).
- **`CustomersController`** — `CreateCustomerCommand`/`DeleteCustomerCommand`; đọc `Search`, `top`,
  `{id}/orders` (`GetCustomerOrderHistoryQuery` — dữ liệu Order, handler gọi `IOrderReadService`).
- **`OrdersController`** (quan trọng):
  - `PlaceOrderCommand` (`POST /api/orders`) — **cả đơn + toàn bộ item** 1 call: kiểm customer/employee tồn tại,
    mỗi product Active + đủ tồn kho, tạo đơn `Draft` kèm các dòng.
  - `ConfirmOrderCommand` — **trừ kho** rồi chuyển `Confirmed` (ghi qua `IUnitOfWork`). `CancelOrderCommand` —
    **hoàn kho** nếu đơn đang `Confirmed`. `ShipOrderCommand`. 💡 Đơn không tồn tại → 404; sai trạng thái → 400; thiếu tồn → 409.
  - Đọc: `GetOrderByIdQuery`, `GetOrderDetailViewQuery`, `SearchOrdersQuery`, `GetOrderStatusBreakdownQuery`.
- **`PaymentsController`** — `CreatePaymentCommand`: order phải `Confirmed/Shipped`, đúng tiền tệ, không trả
  vượt số còn nợ (đều `ConflictException`). `CompletePaymentCommand`. Đọc: `Search`, `outstanding-orders`.
- **`ReportsController`** — chỉ đọc: `GetBestSellingProductsQuery`, `GetEmployeeSalesLeaderboardQuery`,
  `GetSalesByCategoryQuery`, `GetDailySalesQuery`, `GetLowStockProductsQuery` (handler gọi `IReportReadService`).

💡 Controller **không bao giờ** đụng `DbContext`, repository, service, hay biết SQL/Mongo. Nó chỉ gửi message qua `ISender`.

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
| API | `AddApiServices()` — controllers + `ApiResponseWrapperFilter`, OpenAPI + transformer, `IPasswordHasher` |
| Application | `AddApplicationServices()` — MediatR + `ValidationBehavior` + validators (+ gọi `AddDomainServices()`) |
| Infra SqlServer | `AddSqlServerInfrastructure(config)` |
| Infra MongoDB | `AddMongoInfrastructure(config)` |

`Program.cs` là **Composition Root** — nơi "ráp" và là **chỗ DUY NHẤT** biết đang dùng DB nào:

```csharp
builder.Services.AddApiServices();          // presentation
builder.Services.AddApplicationServices();  // CQRS handlers + MediatR + validation (kéo theo AddDomainServices)

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
