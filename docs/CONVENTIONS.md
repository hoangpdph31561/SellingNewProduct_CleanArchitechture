# Quy ước (Conventions)

## 1. Ngôn ngữ
- Giao tiếp / tài liệu: **tiếng Việt**.
- Code, tên class/biến/method, comment trong code: **tiếng Anh**.

## 2. Đặt tên project & namespace
```
SellingNewProduct.Domain
SellingNewProduct.Infrastructure.SqlServer
SellingNewProduct.Infrastructure.MongoDB
SellingNewProduct.API
```
Namespace trùng cấu trúc thư mục.

## 3. Quy ước đặt tên BIẾN (theo yêu cầu của dự án)

> Đây là quy ước RIÊNG của dự án (không phải chuẩn .NET mặc định). Áp dụng nhất quán toàn bộ.
> Dạng tổng quát: `<prefix><Type><Name>` — prefix + tên kiểu + tên ý nghĩa, mỗi phần PascalCase.

| Loại biến | Prefix | Ví dụ |
|-----------|--------|-------|
| **private field** | `my` | `myStringName`, `myMoneyTotal`, `myListItems` |
| **tham số method** | `the` | `theStringName`, `theOrderOrder` → rút gọn `theOrder` |
| **biến local trong method** | `a` | `aIntCount`, `aOrderConfirmed`, `aDecimalSubtotal` |

**Token kiểu (`<Type>`):**
- Kiểu nguyên thủy: `String`, `Int`, `Long`, `Decimal`, `Bool`, `Guid`, `DateTime`.
- Collection: `List` (vd `myListItems`, `theListProducts`).
- Value Object / Entity / enum: dùng **tên class/enum** (vd `myMoneyPrice`, `aOrderStatusNext`).

**Quy tắc rút gọn (tránh lặp):** khi tên ý nghĩa trùng tên kiểu, bỏ phần Name.
- param `Order order` → `theOrder` (không viết `theOrderOrder`).
- local `Customer customer` → `aCustomer`.

**Giữ NGUYÊN chuẩn .NET cho phần public** (không áp prefix):
| Thành phần | Quy ước | Ví dụ |
|------------|---------|-------|
| class, method, property public | PascalCase | `Order`, `Confirm()`, `TotalAmount` |
| interface | `I` + PascalCase | `IOrderRepository` |
| hằng số / enum member | PascalCase | `OrderStatus.Confirmed` |

> Lý do field không dùng `_`: cả nhóm thống nhất prefix `my` đã đủ phân biệt field với
> param/local; tên biến có thể refactor đổi hàng loạt khi cần.

## 4. Quy ước Domain
- Entity/Aggregate: constructor `private`, tạo qua factory `Create(...)`.
- Setter `private`; thay đổi state chỉ qua method có nghĩa nghiệp vụ.
- Collection expose dạng `IReadOnlyList<>`, field thật là `private readonly List<>`.
- Vi phạm invariant → ném `DomainException` (không dùng exception .NET thô).
- Value Object kế thừa `ValueObject`, override `GetEqualityComponents()`.
- Mọi entity kế thừa `BaseEntity<TId>` (có `Status` để xóa mềm — xem DOMAIN_MODEL).
- **Domain Service (tách Read/Write — CQRS)**: logic ghi cần phối hợp (truy vấn DB để check trùng, kiểm
  tra entity liên quan tồn tại…) đặt ở `I*WriteService` + `*WriteService`; method đọc đặt ở `I*ReadService`
  + `*ReadService`. Interface ở `Domain/Interfaces/Inbound`, impl `internal` ở `Domain/Services`. Đăng ký
  qua `AddDomainServices()`. Tầng Application (handler) gọi service; **không** gọi repository trực tiếp,
  **không** tự `new`/`Create` entity.
- **Tổ chức folder Domain theo LOẠI**: `Interfaces/Inbound/` (port vào — `I*ReadService`/`I*WriteService`
  + base `IService`/`IReadService`/`IWriteService`), `Interfaces/Outbound/` (port ra — `I*ReadRepository`/
  `I*WriteRepository` + `IUnitOfWork` + `IPasswordHasher`), `Services/` (impl), `ReadModels/`, `Queries/`,
  `Common/`; folder theo aggregate (Orders/, Products/…) chỉ chứa entity/enum/event/input record
  (`NewProduct`, `OrderLine`…). (KHÔNG nhét service/interface vào folder aggregate.)
- **Read side (CQRS)**: method đọc (Search/Summary/history…) nằm trên `I*ReadService` rồi delegate xuống
  `I*ReadRepository` — repository read trả **read-model** (`*View`, `PagedResult<T>`). Input đọc gói trong
  record `*SearchQuery` (`Domain/Queries`); read-model `*View` ở `Domain/ReadModels`; `PagedResult<T>`/
  `PageRequest` ở `Domain/Common`. Báo cáo không có aggregate → chỉ `IReportReadService` + `IReportReadRepository`.
- **Unit of Work**: nghiệp vụ ghi ≥2 aggregate (vd Confirm/Cancel order) gói trong `IUnitOfWork`
  (`Domain/Interfaces/Outbound`) → 1 transaction. Mongo cần replica set (xem ARCHITECTURE §11).
- Service ném `DomainException` (vi phạm invariant → 400), `ConflictException` (trùng/đụng trạng thái/không
  đủ tồn kho → 409) hoặc `NotFoundException` (entity liên quan thiếu → 404).
- Domain **cấm** reference: EF Core, MongoDB, MediatR, AutoMapper, System.Text.Json attribute, ASP.NET.
  (Được phép: `Microsoft.Extensions.DependencyInjection.Abstractions` — chỉ là contract DI, không hạ tầng.)

## 5. Mapping — MAP TAY TOÀN BỘ (không dùng AutoMapper)
- DTO ↔ Domain (ở API): viết method/extension map tay.
- Domain ↔ Persistence (ở Infra): map tay, dùng `Rehydrate(...)` để dựng lại entity khi đọc DB
  (không chạy lại business rule).
- Lý do bỏ AutoMapper: giữ encapsulation của Domain (setter private + factory), thấy rõ luồng
  dữ liệu khi học, tránh license thương mại.

## 6. Validation theo TẦNG (mỗi tầng tự lo phần của mình)
| Tầng | Validate gì | Cách |
|------|-------------|------|
| Application | Format request (required, length, range, email dạng đúng) | FluentValidation, chạy TỰ ĐỘNG qua `ValidationBehavior` (MediatR pipeline) → 400 |
| Domain | Quy tắc nghiệp vụ (invariant) | factory/method ném `DomainException` |
| Infrastructure | Ràng buộc lưu trữ (unique, NOT NULL, index) | EF config + schema DB |

> Luật business CHỈ viết một lần ở Domain. Application/Infra không lặp lại luật business.

## 6b. Quy ước Application (CQRS / MediatR)
- **Một feature = một file `.cs`** trong `Application/<Aggregate>/Commands` hoặc `/Queries`, gói cả:
  `*Command`/`*Query` (record `IRequest<T>`) + `*Handler` (`IRequestHandler<,>`) + `*Validator`
  (`AbstractValidator<>`, nếu cần). KHÔNG tách handler/validator ra file riêng.
- **Handler mỏng**: chỉ map + gọi `I*WriteService`/`I*ReadService` của Domain. KHÔNG chứa business rule,
  KHÔNG gọi repository, KHÔNG `new` entity.
- Validator chỉ kiểm **format/shape** (required, length, range). Luật nghiệp vụ ở Domain.
- File/record dùng chung nhiều feature (helper map, line-item record) tách file riêng: vd
  `ProductCommandMapping`, `OrderItemCommand`.
- Application **cấm** reference: EF Core, MongoDB, ASP.NET. Được phép: MediatR, FluentValidation, Domain.

## 7. Quy ước Infrastructure
- Persistence model đặt hậu tố theo DB: `*Record` (SQL), `*Document` (Mongo).
- Field chung (`Id, Status, CreatedAtUtc, UpdatedAtUtc`) gom vào lớp cơ sở `abstract`: `BaseRecord`
  (SQL) / `BaseDocument` (Mongo). Mỗi model kế thừa rồi chỉ khai báo phần riêng. Lớp base KHÔNG là
  entity (không `DbSet`) nên EF không tạo mapping kế thừa — chỉ rải cột chung vào từng bảng.
- Repository: `internal sealed`, implement interface của Domain.
- Mỗi Infra có `DependencyInjection.cs` với `AddXxxInfrastructure(IServiceCollection, IConfiguration)`.
- **Cấu hình schema bằng Fluent API trong class `IEntityTypeConfiguration<T>` riêng — TUYỆT ĐỐI
  không dùng data annotation** (`[Key]`, `[Required]`, `[Column]`, `[Table]`...). Persistence model
  là POCO trần. Mỗi bảng một file config; `DbContext` gom bằng `ApplyConfigurationsFromAssembly(...)`.
- SQL: config `ToTable`, `HasKey`, `HasMany/WithOne`, index... + Migrations.
- Mongo: config `ToCollection("...")` (nhúng `Details[]` trong Order document).
- Xóa mềm: Global Query Filter `HasQueryFilter(e => e.Status != EntityStatus.Deleted)`.

## 8. Quy ước API
- Controller mỏng: chỉ inject **`ISender`** (MediatR) → map Request → Command/Query → `mySender.Send(...)`
  → map kết quả sang DTO. KHÔNG inject service/repository/validator, KHÔNG chứa business.
- Validate format request chạy **tự động** qua `ValidationBehavior` (MediatR pipeline ở Application) —
  controller không gọi `ValidateAndThrowAsync`.
- Response bọc envelope **`ApiResponse`** tự động qua `ApiResponseWrapperFilter` (result filter).
- DTO: `*Request` (vào), `*Response` (ra). Không trả Domain entity ra ngoài.
- Lỗi → `ExceptionHandlingMiddleware` đổi `DomainException`/`ValidationException` → 400, `ConflictException` → 409, `NotFoundException` → 404.

## 9. Packages
| Project | Package |
|---------|---------|
| Domain | `Microsoft.Extensions.DependencyInjection.Abstractions` (chỉ để có `AddDomainServices`) |
| Application | `MediatR`, `FluentValidation` + `FluentValidation.DependencyInjectionExtensions`, `Microsoft.Extensions.DependencyInjection.Abstractions`; ref Domain |
| Infrastructure.SqlServer | `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `*.Configuration/DependencyInjection.Abstractions`; ref Domain |
| Infrastructure.MongoDB | `MongoDB.EntityFrameworkCore`, `*.Configuration/DependencyInjection.Abstractions`; ref Domain |
| API | `Microsoft.AspNetCore.OpenApi`, `FluentValidation(+DI)`; ref Application + Domain + 2 Infra |

> **4 tầng**: API · Application (CQRS/MediatR) · Domain · Infrastructure (SqlServer/MongoDB).

> KHÔNG dùng AutoMapper (đã chốt map tay).

## 10. Cấu trúc thư mục
```
Domain/
  Common/             (BaseEntity, AggregateRoot, ValueObject, EntityStatus, DomainException,
                       ConflictException, NotFoundException, IDomainEvent, PagedResult/PageRequest)
  ValueObjects/       (Money, Email, Address, Sku)
  Interfaces/Inbound/ (I*ReadService, I*WriteService, IReportReadService + base IService/IReadService/IWriteService)
  Interfaces/Outbound/(I*ReadRepository, I*WriteRepository, IReportReadRepository + base IRepository/
                       IReadRepository/IWriteRepository, IUnitOfWork, IPasswordHasher)
  Services/           (*ReadService + *WriteService cho mỗi aggregate, ReportReadService)  -> impl internal
  Queries/            (*SearchQuery record input cho SearchAsync)
  ReadModels/         (ProductViews, OrderViews, ReportViews ...)  -> *View (read-model)
  Users/              (User.cs, UserRole.cs)   <- folder aggregate: CHỈ entity/enum/event/input record
  Customers/          (Customer.cs, NewCustomer.cs)
  Employees/          (Employee.cs)
  Categories/         (Category.cs)
  Products/           (Product.cs, Size.cs, NewProduct.cs)
  Orders/             (Order.cs, OrderDetail.cs, OrderLine.cs, OrderStatus.cs, OrderConfirmedEvent.cs)
  Payments/           (Payment.cs, PaymentMethod.cs, PaymentStatus.cs)
  DependencyInjection.cs   (AddDomainServices — bind inbound port -> *Service)

Application/                              (CQRS/MediatR)
  <Aggregate>/Commands/ (CreateXCommand.cs = Command + Handler + Validator chung 1 file)
  <Aggregate>/Queries/  (GetXQuery.cs      = Query + Handler chung 1 file)
  Common/Behaviors/     (ValidationBehavior.cs — MediatR pipeline)
  DependencyInjection.cs   (AddApplicationServices — MediatR + ValidationBehavior + validators + AddDomainServices)

Infrastructure.SqlServer/
  Persistence/    (AppDbContext.cs, AppDbContextFactory.cs, SqlServerUnitOfWork.cs)
  Models/         (BaseRecord.cs, OrderRecord.cs, ...)  -> *Record
  Configurations/ (OrderConfiguration.cs : IEntityTypeConfiguration)
  Repositories/Write/ (SqlServer*WriteRepository.cs)  -> ghi (trả aggregate)
  Repositories/Read/  (SqlServer*ReadRepository.cs)   -> đọc (JOIN, trả *View)
  Mapping/        (OrderMapper.cs)              -> map tay
  Migrations/     · DependencyInjection.cs

Infrastructure.MongoDB/
  Persistence/    (MongoAppDbContext.cs, MongoUnitOfWork.cs)
  Models/         (BaseDocument.cs, OrderDocument.cs, ...)  -> *Document
  Repositories/Write/ (Mongo*WriteRepository.cs)  -> ghi
  Repositories/Read/  (Mongo*ReadRepository.cs)   -> đọc (stitch)
  Mapping/        (CategoryMapper.cs ...)
  DependencyInjection.cs

API/
  Security/      (PasswordHasher.cs : IPasswordHasher)
  Controllers/   (inject ISender, gửi Command/Query)
  Contracts/     (DTO: *Request, *Response, ApiResponse)
  Mapping/       (ApiMappings.cs — Request->Command, Domain->Response map tay)
  Filters/       (ApiResponseWrapperFilter.cs)
  Middleware/    (ExceptionHandlingMiddleware.cs)
  OpenApi/       (ApiResponseOperationTransformer.cs)
  Program.cs
```
