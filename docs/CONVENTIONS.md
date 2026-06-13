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
- Domain **cấm** reference: EF Core, MongoDB, AutoMapper, System.Text.Json attribute, ASP.NET.

## 5. Mapping — MAP TAY TOÀN BỘ (không dùng AutoMapper)
- DTO ↔ Domain (ở API): viết method/extension map tay.
- Domain ↔ Persistence (ở Infra): map tay, dùng `Rehydrate(...)` để dựng lại entity khi đọc DB
  (không chạy lại business rule).
- Lý do bỏ AutoMapper: giữ encapsulation của Domain (setter private + factory), thấy rõ luồng
  dữ liệu khi học, tránh license thương mại.

## 6. Validation theo TẦNG (mỗi tầng tự lo phần của mình)
| Tầng | Validate gì | Cách |
|------|-------------|------|
| API | Format request (required, length, range, email dạng đúng) | FluentValidation → 400 |
| Domain | Quy tắc nghiệp vụ (invariant) | factory/method ném `DomainException` |
| Infrastructure | Ràng buộc lưu trữ (unique, NOT NULL, index) | EF config + schema DB |

> Luật business CHỈ viết một lần ở Domain. API/Infra không lặp lại luật business.

## 7. Quy ước Infrastructure
- Persistence model đặt hậu tố theo DB: `*Record` (SQL), `*Document` (Mongo).
- Repository: `internal sealed`, implement interface của Domain.
- Mỗi Infra có `DependencyInjection.cs` với `AddXxxInfrastructure(IServiceCollection, IConfiguration)`.
- **Cấu hình schema bằng Fluent API trong class `IEntityTypeConfiguration<T>` riêng — TUYỆT ĐỐI
  không dùng data annotation** (`[Key]`, `[Required]`, `[Column]`, `[Table]`...). Persistence model
  là POCO trần. Mỗi bảng một file config; `DbContext` gom bằng `ApplyConfigurationsFromAssembly(...)`.
- SQL: config `ToTable`, `HasKey`, `HasMany/WithOne`, index... + Migrations.
- Mongo: config `ToCollection("...")` (nhúng `Details[]` trong Order document).
- Xóa mềm: Global Query Filter `HasQueryFilter(e => e.Status != EntityStatus.Deleted)`.

## 8. Quy ước API
- Controller mỏng: nhận DTO → gọi domain/repository → trả DTO. Không chứa business.
- DTO: `*Request` (vào), `*Response` (ra). Không trả Domain entity ra ngoài.
- Lỗi nghiệp vụ → middleware đổi `DomainException` thành `ProblemDetails` 400.

## 9. Packages dự kiến
| Project | Package |
|---------|---------|
| Domain | (không package ngoài — chỉ BCL) |
| Infrastructure.SqlServer | `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design` |
| Infrastructure.MongoDB | `MongoDB.EntityFrameworkCore` |
| API | `FluentValidation.AspNetCore`, OpenAPI/Swagger, ref 2 Infra |

> KHÔNG dùng AutoMapper (đã chốt map tay).

## 10. Cấu trúc thư mục
```
Domain/
  Common/        (BaseEntity, AggregateRoot, ValueObject, EntityStatus, DomainException, IDomainEvent)
  ValueObjects/  (Money, Email, Address, Sku)
  Users/         (User.cs, UserRole.cs)
  Customers/     (Customer.cs)
  Employees/     (Employee.cs)
  Categories/    (Category.cs)
  Products/      (Product.cs, Size.cs)
  Orders/        (Order.cs, OrderDetail.cs, OrderStatus.cs, OrderConfirmedEvent.cs)
  Payments/      (Payment.cs, PaymentMethod.cs)
  Repositories/  (IUserRepository, ICustomerRepository, ... IOrderRepository)

Infrastructure.SqlServer/
  Persistence/   (AppDbContext.cs)
  Models/        (OrderRecord.cs, ...)         -> *Record
  Configurations/(OrderConfiguration.cs : IEntityTypeConfiguration)
  Repositories/  (SqlServerOrderRepository.cs, ...)
  Mapping/       (OrderMapper.cs)              -> map tay
  DependencyInjection.cs

Infrastructure.MongoDB/
  Persistence/   (MongoAppDbContext.cs)
  Models/        (OrderDocument.cs, ...)        -> *Document
  Repositories/  (MongoOrderRepository.cs, ...)
  Mapping/       (OrderMapper.cs)
  DependencyInjection.cs

API/
  Controllers/
  Contracts/     (DTO: *Request, *Response)
  Validators/    (FluentValidation)
  Mapping/       (DTO <-> Domain map tay)
  Middleware/    (ExceptionHandlingMiddleware)
  Program.cs
```
