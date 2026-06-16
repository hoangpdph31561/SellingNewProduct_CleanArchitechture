# Kiến trúc — Clean Architecture + DDD

## 1. Triết lý

Mục tiêu duy nhất cần ghi nhớ: **Business rule không được biết nó đang được lưu ở đâu.**

Nếu một ngày đổi từ SQL Server sang MongoDB (hoặc Postgres, hoặc file), thì:
- Project `Domain` **không đổi một dòng nào**.
- Chỉ thêm/đổi một project `Infrastructure.*` và đổi đăng ký DI ở `API`.

Đây chính là phép thử để chứng minh kiến trúc đúng.

## 1b. Hiểu lầm phổ biến: "Luồng gọi" vs "Chiều reference"

Người mới hay nghĩ: *"API gọi Domain, Domain gọi Infrastructure, vì Infra có DB nên
Infra ở trong cùng."* → Sai một nửa. Cần tách 2 khái niệm:

| | Luồng gọi lúc chạy (runtime) | Chiều phụ thuộc / reference (compile-time) |
|---|---|---|
| Hướng | `API → Domain → IRepository → DB` | `API → Infrastructure → Domain` |
| Ý nghĩa | Dữ liệu đi tới DB **sau cùng** (đúng) | Project nào `reference`/`using` project nào |
| Domain | Dùng `IRepository` của chính nó | **KHÔNG reference Infra** — Infra reference Domain |

→ Domain **định nghĩa interface**, Infrastructure **implement**. Nhờ Dependency Inversion,
mũi tên reference đi **ngược** luồng gọi. Vì vậy **DB là tầng NGOÀI CÙNG** (thay thế được),
không phải trong cùng. "Trong cùng" = business = Domain. Đây chính là điều cho phép
"bê Domain đi đâu cũng được".

## 2. Sơ đồ phụ thuộc

```
┌─────────────────────────────────────────────┐
│                    API                        │
│  Controller · DTO · Validation · DI setup     │
└───────────────┬───────────────┬───────────────┘
                │               │
                ▼               ▼
┌──────────────────────┐ ┌──────────────────────┐
│ Infrastructure.       │ │ Infrastructure.       │
│ SqlServer             │ │ MongoDB               │
│ DbContext · Record ·  │ │ DbContext · Document ·│
│ Repository · Mapper · │ │ Repository · Mapper · │
│ Queries (JOIN)        │ │ Queries (stitch)      │
└───────────┬───────────┘ └───────────┬──────────┘
            │                         │
            └────────────┬────────────┘
                         ▼
            ┌──────────────────────────────────────┐
            │              Domain                    │
            │  Entity · ValueObject · AggregateRoot │
            │  Event · IRepository (interface)      │
            │  Domain Service (I*Service) ← write   │
            │  IQueries + ReadModel (View) ← read   │
            └──────────────────────────────────────┘
```

**Quy tắc vàng:** Mũi tên chỉ vào trong. Domain ở trung tâm, không có mũi tên nào đi ra khỏi nó.
Chỉ có **3 tầng**: API → (Domain) ← Infrastructure. Cả write side (Domain Service) lẫn read side
(IQueries + ReadModel) đều nằm trong Domain; Infrastructure implement cả hai.

## 3. Vì sao tách persistence model khỏi domain entity?

Domain entity được thiết kế cho **nghiệp vụ** (encapsulation, invariant, behavior).
Persistence model được thiết kế cho **database** (khoá ngoại, kiểu cột, index, schema).

Hai mục đích khác nhau → tách riêng. Mỗi Infrastructure tự định nghĩa model của mình:

| Khái niệm | Domain (chung) | SqlServer | MongoDB |
|-----------|----------------|-----------|---------|
| Đơn hàng | `Order` (aggregate root) | `OrderRecord` + `OrderItemRecord` (2 bảng, FK) | `OrderDocument` (1 document lồng `Items[]`) |
| Khách hàng | `Customer` | `CustomerRecord` | `CustomerDocument` |
| Sản phẩm | `Product` | `ProductRecord` | `ProductDocument` |

→ SQL chuẩn hoá thành nhiều bảng; Mongo nhúng (embed) trong một document. Domain không quan tâm.

## 4. Luồng một request (ví dụ: tạo Order)

```
HTTP POST /api/orders
   │
   ▼
[API] OrdersController nhận CreateOrderRequest (DTO)
   │  → FluentValidation kiểm tra FORMAT request (không trống, độ dài…)
   │  → gọi IOrderService.CreateAsync(...) — KHÔNG đụng repository, KHÔNG new entity
   ▼
[Domain] OrderService: business logic (vd: Customer/Employee phải tồn tại)
   │  → Order.Create(...) chạy invariant của aggregate, sinh Domain Event nếu cần
   │  → gọi IOrderRepository.AddAsync(order)
   ▼
[Infrastructure] IOrderRepository.AddAsync(order)
   │  → Mapper: Order (domain) → OrderRecord/OrderDocument
   │  → DbContext.SaveChanges()  (EF Core: SQL Server hoặc Mongo provider)
   ▼
[Domain] service trả Order về lại API
   ▼
[API] Map Order → OrderResponse (DTO) → trả 201 Created
```

→ Xem thêm mục **9. Domain Service** bên dưới.

## 5. Interface nằm ở đâu? (Dependency Inversion)

Interface repository **được khai báo trong Domain**, được **triển khai trong Infrastructure**.
Đây là điểm cốt lõi của Dependency Inversion Principle:

```csharp
// Trong Domain (Domain/Repositories/IOrderRepository.cs)
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
}

// Trong Infrastructure.SqlServer (implement) — Domain không biết file này tồn tại
internal sealed class SqlServerOrderRepository : IOrderRepository { ... }

// Trong Infrastructure.MongoDB (implement)
internal sealed class MongoOrderRepository : IOrderRepository { ... }
```

## 6. Chọn database lúc runtime (Composition Root)

`API/Program.cs` đọc cấu hình và đăng ký Infra tương ứng:

```csharp
var provider = builder.Configuration["DatabaseProvider"]; // "SqlServer" | "MongoDB"

if (provider == "SqlServer")
    builder.Services.AddSqlServerInfrastructure(builder.Configuration);
else
    builder.Services.AddMongoInfrastructure(builder.Configuration);
```

Mỗi Infra cung cấp một extension method `AddXxxInfrastructure(...)` để đăng ký
`DbContext` + các repository. Đây là chỗ DUY NHẤT biết ta đang dùng DB nào.

## 7. EF Core cho cả hai

- SQL Server: `Microsoft.EntityFrameworkCore.SqlServer` + Migrations.
- MongoDB: `MongoDB.EntityFrameworkCore` (provider chính thức của MongoDB cho EF Core).
  Lưu ý: provider Mongo **không hỗ trợ migration** như SQL — schema tạo theo nhu cầu;
  ta cấu hình mapping bằng `OnModelCreating` (ToCollection) thay vì `ToTable`.

## 7b. Read side — Write đi qua aggregate, Read đi thẳng JOIN (CQRS-lite)

Repository (`IOrderRepository`) trả về **aggregate** `Order` để **thực thi nghiệp vụ** (Confirm, Ship…).
Aggregate chỉ tham chiếu Customer/Employee **bằng id**, nên nó KHÔNG chứa `CustomerName`. Đúng theo DDD:
một aggregate không ôm dữ liệu của aggregate khác (tránh dữ liệu lỗi thời).

Vậy khi cần **hiển thị** "đơn này của khách nào, ai bán, đã mua bao nhiêu đơn", ta dùng một đường riêng:

| | Write side | Read side |
|---|---|---|
| Hợp đồng | `IOrderRepository` + `IOrderService` (Domain) | `IOrderQueries`, `IReportQueries` (Domain) |
| Trả về | Aggregate `Order` | Read-model phẳng (`OrderDetailView`…) |
| Mục đích | Chạy business rule | Hiển thị / báo cáo nhanh |
| Ghép nhiều bảng | ❌ (vỡ ranh giới aggregate) | ✅ JOIN/GROUP BY |

**Read-model đặt ở đâu?** Dự án gộp về **3 tầng** (không có Application), nên cả interface query
(`IOrderQueries`) lẫn read-model (`OrderDetailView`) đều nằm trong **Domain** (`Domain/Queries`,
`Domain/ReadModels`); Infrastructure implement query. Đánh đổi: Domain "rộng" hơn (ôm cả read-model
hiển thị) để giữ đúng 3 tầng API → Domain ← Infrastructure. Read-model vẫn là kiểu chỉ-đọc, tách bạch
với aggregate (không trộn vào entity nghiệp vụ).

**Mỗi Infrastructure tự chọn chiến lược thực thi cùng một interface:**
- SQL Server: LINQ `join` → EF Core dịch thành **một câu SQL JOIN** chạy trên DB.
- MongoDB: không JOIN quan hệ → **nạp document rồi ghép (stitch) trong bộ nhớ** (hoặc denormalize/`$lookup`).

Đây lại là một lần nữa chứng minh luận điểm cốt lõi: đổi cơ chế lưu trữ, hợp đồng không đổi.

## 7c. Phân trang, tìm kiếm, lọc & sắp xếp (cũng là read side)

Danh sách thật luôn cần **phân trang** (đừng trả cả bảng), **tìm kiếm/lọc** nhiều tiêu chí và
**sắp xếp**. Đây vẫn là *cách đọc* → hợp đồng đặt ở **Domain** (`PagedResult<T>`, `PageRequest`,
interface query) + cách thực thi ở **Infrastructure**.

- **Domain**: `PagedResult<T>` (dữ liệu 1 trang + `TotalCount`/`TotalPages`/`HasNext`) và `PageRequest`
  nằm trong `Domain/Common`; interface `IProductQueries`/`IOrderQueries` (`Domain/Queries`) nhận filter + `sortBy`/`sortDescending`.
- **SQL Server**: đẩy hết `WHERE`/`ORDER BY`/`COUNT`/`OFFSET-FETCH` xuống DB; tên hiển thị lấy bằng JOIN.
- **MongoDB**: đẩy phần làm được xuống DB; tìm theo tên cross-collection thì *resolve Id trước rồi `$in`*;
  phần không JOIN được (vd sort theo tên ghép) xử lý/giới hạn ở bộ nhớ — một minh hoạ cho khác biệt thực thi.

→ Chi tiết: [docs/code/06-Pagination-Search.md](code/06-Pagination-Search.md).

## 8. Xử lý lỗi nghiệp vụ

Domain ném exception, API có middleware chuyển thành ProblemDetails chuẩn:

| Exception (Domain) | HTTP | Khi nào |
|---|---|---|
| `DomainException` | 400 | Vi phạm invariant / business rule (vd: tên category trùng) |
| `NotFoundException` | 404 | Tham chiếu tới entity không tồn tại (vd: tạo Product với CategoryId sai) |
| `ValidationException` (FluentValidation, ở API) | 400 | Sai FORMAT request (trống, độ dài…) |

→ `ExceptionHandlingMiddleware` bắt theo thứ tự này. Controller không cần `try/catch` hay tự trả 404
cho entity liên quan — service ném, middleware lo phần còn lại.

## 9. Domain Service — API gọi behavior, không gọi repository

Controller **không** được tự `new`/`Entity.Create()` rồi gọi repository. Logic ghi (kể cả các
kiểm tra cần truy vấn DB như "tên không trùng", "entity liên quan phải tồn tại") nằm trong
**Domain Service**.

```
API (validate format)  →  I*Service (Domain: business logic)  →  I*Repository (Domain interface)
                                                                      ↑ Infrastructure implement
```

- Mỗi module có `I*Service` (public — thứ DUY NHẤT API thấy) + `*Service` (internal — chứa logic).
- Service phối hợp aggregate + repository, ném `DomainException`/`NotFoundException` khi vi phạm.
- Domain tự đăng ký service qua `AddDomainServices()` (`Domain/DependencyInjection.cs`); nhờ vậy
  `*Service` để `internal` mà API vẫn dùng qua interface. `Program.cs` gọi `AddDomainServices()`.
- Phụ thuộc kỹ thuật cần thiết: Domain ref `Microsoft.Extensions.DependencyInjection.Abstractions`
  (chỉ là contract `IServiceCollection`, không kéo theo hạ tầng).
- Ví dụ ngoại lệ hướng phụ thuộc: `IPasswordHasher` khai báo ở Domain (`Domain/Users`), API implement
  (`PasswordHasher`) và đăng ký DI — Domain định nghĩa hợp đồng, tầng ngoài cắm vào.

> Lưu ý quan niệm: "API gọi Domain" nghĩa là API **phụ thuộc** vào abstraction của Domain. Đổi *chữ ký
> behavior* thì nơi gọi phải đổi (kiến trúc nào cũng vậy). Clean Architecture chỉ bảo đảm: đổi *chi tiết
> hạ tầng/DB* không lan vào trong — không phải "đổi Domain mà API vô can".
