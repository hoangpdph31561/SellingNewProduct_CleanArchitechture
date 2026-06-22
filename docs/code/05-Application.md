# 05 — Tầng Application (CQRS / MediatR)

Application là tầng **điều phối use-case**, nằm giữa API và Domain:

```
API (Controller, ISender) ──► Application (Command/Query + Handler + Validator) ──► Domain (I*ReadService / I*WriteService)
```

API **không** gọi thẳng Domain Service. Nó dựng một **Command** (ghi) hoặc **Query** (đọc) rồi gửi qua
MediatR (`ISender.Send(...)`). MediatR tìm đúng **Handler**, chạy qua **pipeline** (validate trước), và
handler — *mỏng* — chỉ gọi xuống Domain Service. Toàn bộ business rule thật vẫn ở Domain; Application chỉ
là lớp "vào use-case" + validate hình dạng request.

> 💡 Vì sao có tầng này? (1) Tách "đường vào use-case" khỏi controller HTTP — cùng một command có thể được
> gửi từ controller, từ test, từ một job nền… (2) Gom **cross-cutting concern** (validation, sau này có thể
> thêm logging/transaction/caching) vào **pipeline behavior** thay vì rải khắp controller. (3) Theo CQRS:
> Command và Query là hai loại thông điệp tách bạch.

---

## A. Một feature = một file `.cs`

Mỗi use-case gói **chung một file**: record input + handler + validator (nếu có). Không tách nhỏ mỗi class
một file. Ví dụ `Categories/Commands/CreateCategoryCommand.cs`:

```csharp
// 1) Command — record input, là IRequest<T> (T = kết quả trả về)
public sealed record CreateCategoryCommand(string Name, string Description) : IRequest<Category>;

// 2) Validator — chỉ kiểm FORMAT (FluentValidation)
public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

// 3) Handler — MỎNG: chỉ gọi Domain Service, KHÔNG business, KHÔNG repository
public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Category>
{
    private readonly ICategoryWriteService myCategoryWriteService;
    public CreateCategoryCommandHandler(ICategoryWriteService theService) => myCategoryWriteService = theService;

    public Task<Category> Handle(CreateCategoryCommand theCommand, CancellationToken theCancellationToken) =>
        myCategoryWriteService.CreateAsync(theCommand.Name, theCommand.Description, theCancellationToken);
}
```

Query cũng vậy — record `*Query` + handler chung 1 file (validator thường không cần):

```csharp
public sealed record GetCategoryByIdQuery(Guid Id) : IRequest<Category?>;

public sealed class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, Category?>
{
    private readonly ICategoryReadService myCategoryReadService;
    public GetCategoryByIdQueryHandler(ICategoryReadService theService) => myCategoryReadService = theService;

    public Task<Category?> Handle(GetCategoryByIdQuery theQuery, CancellationToken theCancellationToken)
        => myCategoryReadService.GetByIdAsync(theQuery.Id, theCancellationToken);
}
```

> 💡 **Handler mỏng có chủ ý.** Logic "tên category không trùng" KHÔNG ở handler mà ở
> `CategoryWriteService.CreateAsync` (Domain) — vì đó là business rule. Handler chỉ là adapter
> command → service. Nhờ vậy đổi cách "vào" (MediatR ↔ gọi trực tiếp) không đụng business.

**File/record dùng chung nhiều feature** tách riêng (không thuộc một feature nào):
- `Products/Commands/ProductCommandMapping.cs` — `ToNewProduct()` dùng bởi cả `CreateProduct` lẫn `CreateManyProducts`.
- `Orders/Commands/OrderItemCommand.cs` — record line-item dùng trong `PlaceOrderCommand`.

---

## B. Command vs Query (CQRS)

| | Command (ghi) | Query (đọc) |
|---|---|---|
| Thư mục | `Application/<Aggregate>/Commands` | `Application/<Aggregate>/Queries` |
| Gọi xuống | `I*WriteService` (Domain inbound) | `I*ReadService` (Domain inbound) |
| Trả về | aggregate (vd `Category`, `Order`) | read-model `*View` / `PagedResult<T>` / aggregate đơn lẻ |
| Ví dụ | `CreateCategoryCommand`, `PlaceOrderCommand`, `ConfirmOrderCommand` | `GetCategoryByIdQuery`, `SearchProductsQuery`, `GetSalesByCategoryQuery` |

Search có dạng riêng: query của Application bọc `*SearchQuery` (record filter của Domain) rồi forward:

```csharp
public sealed record SearchCategoriesQuery(CategorySearchQuery Filter) : IRequest<PagedResult<CategorySummaryView>>;
// Handler: => myCategoryReadService.SearchAsync(theQuery.Filter, ct);
```

---

## C. ValidationBehavior — validate trong pipeline MediatR

`Common/Behaviors/ValidationBehavior.cs` là một `IPipelineBehavior<TRequest,TResponse>` đăng ký làm
**bước ngoài cùng** của pipeline. Trước khi handler chạy, nó gom mọi `IValidator<TRequest>` đã đăng ký,
chạy hết, gộp lỗi; có lỗi → ném `ValidationException`.

```csharp
public async Task<TResponse> Handle(TRequest theRequest, RequestHandlerDelegate<TResponse> theNext, CancellationToken ct)
{
    if (!myValidators.Any()) return await theNext();          // không có validator → đi thẳng
    var aContext = new ValidationContext<TRequest>(theRequest);
    var aFailures = (await Task.WhenAll(myValidators.Select(v => v.ValidateAsync(aContext, ct))))
        .SelectMany(r => r.Errors).Where(f => f is not null).ToList();
    if (aFailures.Count != 0) throw new ValidationException(aFailures);   // → middleware → 400
    return await theNext();
}
```

💡 So với cách cũ (action filter ở API): validation giờ là concern của **pipeline CQRS** — một chỗ duy
nhất, áp cho mọi command/query, không phụ thuộc ASP.NET. Controller chỉ `mySender.Send(...)`. `ValidationException`
nổi lên `ExceptionHandlingMiddleware` của API → **400** (xem [04-API.md](04-API.md) mục E).

---

## D. DI — `Application/DependencyInjection.cs`

```csharp
public static IServiceCollection AddApplicationServices(this IServiceCollection theServices)
{
    var aAssembly = typeof(DependencyInjection).Assembly;
    theServices.AddDomainServices();                                            // business rule (Domain)
    theServices.AddMediatR(c => c.RegisterServicesFromAssembly(aAssembly));      // handler + ISender
    theServices.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));  // validate trước handler
    theServices.AddValidatorsFromAssembly(aAssembly);                           // mọi *Validator
    return theServices;
}
```

💡 `AddApplicationServices()` **gọi luôn** `AddDomainServices()` — vì handler phụ thuộc Domain Service.
`Program.cs` chỉ cần gọi `AddApplicationServices()` cho cụm business + CQRS (không gọi `AddDomainServices`
riêng nữa).

---

## E. Read side — read-model & JOIN nhiều bảng (SQL vs Mongo)

Phần ĐỌC vẫn cần dữ liệu ghép nhiều bảng (vd "đơn này của khách *Nguyễn Văn A*, do *Trần Thị B* bán"),
mà aggregate (phía ghi) chỉ giữ id, không hợp để hiển thị. Nên dự án tách **read-model** (`*View`) ở
`Domain/ReadModels`, do **read repository** trả về. Query handler chỉ forward xuống `I*ReadService` →
`I*ReadRepository`.

> 💡 Vẫn giữ tinh thần **tách model đọc/ghi (CQRS)**: ghi đi qua aggregate (Order), đọc trả read-model
> phẳng (`OrderDetailView`). Read và write có service/repository **riêng** ở Domain (xem ARCHITECTURE §7b).

### Read-model — `Domain/ReadModels/*.cs`
Các `record` phẳng, **chỉ để hiển thị** (không method, không invariant): `OrderDetailView`,
`CustomerOrderHistoryView`, `OrderSummaryView`, `ProductSummaryView`, `CustomerSummaryView`,
`TopCustomerView`, `EmployeeSummaryView`, `CategorySummaryView`, `PaymentSummaryView`, `OutstandingOrderView`,
và nhóm báo cáo (GROUP BY): `BestSellingProductView`, `EmployeeSalesView`, `CategorySalesView`,
`DailySalesView`, `LowStockProductView`.

> 📄 Phân trang, tìm kiếm theo tên, lọc & sắp xếp: xem [06-Pagination-Search.md](06-Pagination-Search.md).

### Hai cách implement CÙNG một read repository
Đây là phần chứng minh "đổi DB, business/hợp đồng không đổi":

**SQL Server — JOIN thật** (`Infrastructure.SqlServer/Repositories/Read/*.cs`): LINQ
`join ... on ... equals ...` được EF Core dịch thành **một câu SQL có JOIN** chạy trên DB; báo cáo dùng
`group ... by ... into g` → `SUM`/`COUNT` đẩy xuống database.

**MongoDB — ghép trong bộ nhớ** (`Infrastructure.MongoDB/Repositories/Read/*.cs`): Mongo không có JOIN
quan hệ → nạp document cần thiết rồi ghép bằng LINQ-to-objects (hoặc, "đúng Mongo" hơn cho dữ liệu lớn:
`$lookup`/`$group`, hoặc denormalize sẵn tên vào document).

> Lưu ý soft-delete: SQL có **Global Query Filter** (`Status != Deleted`) tự áp dụng; Mongo **không có**,
> nên mỗi read query phải tự thêm `Status != DeletedStatus`.

---

## F. Endpoint dùng Application (qua `ISender`)

| Method | Command/Query gửi đi | Trả về |
|--------|----------------------|--------|
| `POST /api/categories` | `CreateCategoryCommand` | `CategoryResponse` |
| `GET /api/categories/{id}` | `GetCategoryByIdQuery` | `CategoryResponse` |
| `GET /api/categories/search` | `SearchCategoriesQuery` | `PagedResult<CategorySummaryView>` |
| `POST /api/orders` | `PlaceOrderCommand` | `OrderResponse` |
| `POST /api/orders/{id}/confirm` | `ConfirmOrderCommand` | `OrderResponse` |
| `GET /api/orders/{id}/view` | `GetOrderDetailViewQuery` | `OrderDetailView` |
| `GET /api/reports/sales-by-category` | `GetSalesByCategoryQuery` | `CategorySalesView[]` |

> Controller chỉ inject `ISender` và `mySender.Send(...)`. Chi tiết tham số lọc/sort/phân trang: xem
> [06-Pagination-Search.md](06-Pagination-Search.md); envelope `ApiResponse` + map lỗi: [04-API.md](04-API.md).

---

## G. Nguyên tắc rút ra (ghi nhớ)

1. **API gửi Command/Query qua `ISender`** — không gọi service/repository trực tiếp.
2. **Handler mỏng**: chỉ map + gọi Domain Service. Business rule thật ở Domain.
3. **Một feature = một file**: command/query + handler + validator chung 1 `.cs`.
4. **Validation = pipeline behavior** (`ValidationBehavior`), một chỗ cho mọi request → 400.
5. **CQRS**: Command (ghi, trả aggregate) và Query (đọc, trả read-model) tách bạch, gọi xuống
   `I*WriteService`/`I*ReadService` tương ứng.
6. **Read-model**: ghi qua aggregate, đọc trả `*View` phẳng; SQL JOIN ở DB, Mongo ghép bộ nhớ.
