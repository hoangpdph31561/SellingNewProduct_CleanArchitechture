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
            ┌──────────────────────────┐
            │       Application         │   ← read side (CQRS-lite)
            │  ReadModel (View) ·       │
            │  IOrderQueries ·          │
            │  IReportQueries           │
            └────────────┬─────────────┘
                         ▼
            ┌──────────────────────────┐
            │          Domain           │
            │  Entity · ValueObject ·   │
            │  AggregateRoot · Event ·  │
            │  IRepository (interface)  │
            └──────────────────────────┘
```

**Quy tắc vàng:** Mũi tên chỉ vào trong. Domain ở trung tâm, không có mũi tên nào đi ra khỏi nó.
Application nằm sát Domain (chỉ ref Domain); Infrastructure & API đều thấy được Application.

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
   │  → FluentValidation kiểm tra format request
   ▼
[API] Map DTO → gọi use case / repository
   │
   ▼
[Domain] Order.Create(...) chạy business rule (vd: phải có ít nhất 1 item)
   │  → sinh Domain Event nếu cần
   ▼
[Infrastructure] IOrderRepository.AddAsync(order)
   │  → Mapper: Order (domain) → OrderRecord/OrderDocument
   │  → DbContext.SaveChanges()  (EF Core: SQL Server hoặc Mongo provider)
   ▼
[API] Map Order → OrderResponse (DTO) → trả 201 Created
```

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
| Hợp đồng | `IOrderRepository` (Domain) | `IOrderQueries`, `IReportQueries` (Application) |
| Trả về | Aggregate `Order` | Read-model phẳng (`OrderDetailView`…) |
| Mục đích | Chạy business rule | Hiển thị / báo cáo nhanh |
| Ghép nhiều bảng | ❌ (vỡ ranh giới aggregate) | ✅ JOIN/GROUP BY |

**Vì sao read-model nằm ở Application, không ở Domain hay API?**
- Không ở **Domain**: read-model không phải business rule, đưa vào sẽ làm Domain phình & bẩn.
- Không ở **API**: Infrastructure phải *return* kiểu này khi implement query, mà Infra **không** ref API
  (ngược chiều mũi tên). Application là tầng cả Infra lẫn API đều thấy → đặt ở đây là hợp lệ.

**Mỗi Infrastructure tự chọn chiến lược thực thi cùng một interface:**
- SQL Server: LINQ `join` → EF Core dịch thành **một câu SQL JOIN** chạy trên DB.
- MongoDB: không JOIN quan hệ → **nạp document rồi ghép (stitch) trong bộ nhớ** (hoặc denormalize/`$lookup`).

Đây lại là một lần nữa chứng minh luận điểm cốt lõi: đổi cơ chế lưu trữ, hợp đồng không đổi.

## 7c. Phân trang, tìm kiếm, lọc & sắp xếp (cũng là read side)

Danh sách thật luôn cần **phân trang** (đừng trả cả bảng), **tìm kiếm/lọc** nhiều tiêu chí và
**sắp xếp**. Đây vẫn là *cách đọc*, không phải business → đặt ở **Application** (`PagedResult<T>`,
`PageRequest`, các tham số lọc trên interface query) + **Infrastructure** (cách thực thi).

- **Application**: `PagedResult<T>` (dữ liệu 1 trang + `TotalCount`/`TotalPages`/`HasNext`), `PageRequest`
  (kẹp page/pageSize ở một chỗ), và interface `IProductQueries`/`IOrderQueries` nhận filter + `sortBy`/`sortDescending`.
- **SQL Server**: đẩy hết `WHERE`/`ORDER BY`/`COUNT`/`OFFSET-FETCH` xuống DB; tên hiển thị lấy bằng JOIN.
- **MongoDB**: đẩy phần làm được xuống DB; tìm theo tên cross-collection thì *resolve Id trước rồi `$in`*;
  phần không JOIN được (vd sort theo tên ghép) xử lý/giới hạn ở bộ nhớ — một minh hoạ cho khác biệt thực thi.

→ Chi tiết: [docs/code/06-Pagination-Search.md](code/06-Pagination-Search.md).

## 8. Xử lý lỗi nghiệp vụ

Domain ném `DomainException` (hoặc dùng Result pattern) khi vi phạm invariant.
API bắt và chuyển thành response chuẩn (ProblemDetails / 400). Xem ROADMAP để biết
ta chọn cách nào (mặc định đề xuất: **DomainException + middleware** cho đơn giản khi học).
