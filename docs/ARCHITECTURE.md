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
┌───────────────────────────────────────────────────────┐
│                         API                            │
│  Controller (ISender) · DTO · ApiResponse · DI setup   │
└──────┬──────────────────┬───────────────────┬──────────┘
       │                  │                   │
       ▼                  │                   ▼
┌──────────────────────┐  │   ┌──────────────────────┐ ┌──────────────────────┐
│     Application       │  │   │ Infrastructure.       │ │ Infrastructure.       │
│  Command/Query +      │  │   │ SqlServer             │ │ MongoDB               │
│  Handler + Validator  │  │   │ DbContext · UnitOfWork│ │ DbContext · UnitOfWork│
│  (MediatR pipeline)   │  │   │ Record · Repo · Mapper│ │ Document · Repo·Mapper │
└──────────┬───────────┘  │   └───────────┬──────────┘ └───────────┬──────────┘
           │              │               │                        │
           ▼              ▼               └───────────┬────────────┘
        ┌────────────────────────────────────────────▼────────────┐
        │                          Domain                          │
        │  Entity · ValueObject · AggregateRoot · DomainEvent      │
        │  Domain Service (*ReadService / *WriteService)           │
        │  Interfaces/Inbound  (I*ReadService, I*WriteService)     │
        │  Interfaces/Outbound (I*ReadRepo, I*WriteRepo,           │
        │                       IUnitOfWork, IPasswordHasher)      │
        │  ReadModels (*View) · Queries (*SearchQuery)             │
        └──────────────────────────────────────────────────────────┘
```

**Quy tắc vàng:** Mũi tên chỉ vào trong. Domain ở trung tâm, không có mũi tên nào đi ra khỏi nó.
Có **4 tầng**: API → Application → Domain ← Infrastructure. API **không** gọi thẳng Domain Service —
nó gửi **Command/Query** qua MediatR (`ISender`) tới **Application**; handler ở Application (mỏng) điều
phối rồi gọi Domain Service (`I*WriteService`/`I*ReadService` — port vào ở `Interfaces/Inbound`); service
gọi repository (port ra ở `Interfaces/Outbound`). Đọc (read-model `*View`) và ghi (aggregate) tách thành
service/repository **read** và **write** riêng (xem §7b). API cũng ref thẳng Domain để dùng các kiểu
read-model/`*SearchQuery`/`PagedResult<T>` khi khai báo chữ ký controller.

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
HTTP POST /api/orders   (đặt cả đơn + toàn bộ item trong 1 call)
   │
   ▼
[API] OrdersController map PlaceOrderRequest (DTO) → PlaceOrderCommand
   │  → mySender.Send(command)   (MediatR — KHÔNG gọi service/repository trực tiếp)
   ▼
[Application] MediatR pipeline:
   │  → ValidationBehavior chạy PlaceOrderCommandValidator (FORMAT) — lỗi → ValidationException → 400
   │  → PlaceOrderCommandHandler (mỏng): gọi IOrderWriteService.PlaceAsync(...)
   ▼
[Domain] OrderWriteService: business logic (Customer/Employee phải tồn tại; mỗi Product
   │  phải Active + đủ tồn kho); Order.Create(...) + AddDetail từng dòng (snapshot giá)
   │  → Order ở trạng thái Draft, sinh Domain Event khi Confirm sau này
   │  → gọi IOrderWriteRepository.AddAsync(order)
   ▼
[Infrastructure] IOrderWriteRepository.AddAsync(order)
   │  → Mapper: Order (domain) → OrderRecord/OrderDocument
   │  → DbContext.SaveChanges()  (EF Core: SQL Server hoặc Mongo provider)
   ▼
[Domain] service trả Order → [Application] handler trả Order → [API]
   ▼
[API] Map Order → OrderResponse (DTO) → return CreatedAtAction(...)
   │  → ApiResponseWrapperFilter bọc thành envelope ApiResponse → HTTP 201
```

> 💡 Nghiệp vụ ghi nhiều aggregate cùng lúc (vd `ConfirmAsync` trừ kho + đổi trạng thái Order) được gói
> trong **một transaction** qua `IUnitOfWork` — xem §7d. Xem thêm mục **9. Domain Service** bên dưới.

## 5. Interface nằm ở đâu? (Dependency Inversion)

Interface repository **được khai báo trong Domain**, được **triển khai trong Infrastructure**.
Đây là điểm cốt lõi của Dependency Inversion Principle:

```csharp
// Trong Domain (Domain/Interfaces/Outbound/IOrderWriteRepository.cs)
public interface IOrderWriteRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
}

// Trong Infrastructure.SqlServer/Repositories/Write (implement) — Domain không biết file này tồn tại
internal sealed class SqlServerOrderWriteRepository : IOrderWriteRepository { ... }

// Trong Infrastructure.MongoDB/Repositories/Write (implement)
internal sealed class MongoOrderWriteRepository : IOrderWriteRepository { ... }
```

> 💡 Đọc và ghi tách cổng riêng: `IOrderWriteRepository` (trả aggregate, phục vụ ghi) và
> `IOrderReadRepository` (trả read-model `*View`, phục vụ hiển thị) — xem §7b. Cả hai đặt ở
> `Domain/Interfaces/Outbound`, implement trong `Infrastructure.*/Repositories/Write` và `/Read`.

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

## 7b. Read side — CQRS: tách Read service/repository khỏi Write

Việc ghi trả về **aggregate** `Order` để **thực thi nghiệp vụ** (Confirm, Ship…). Aggregate chỉ tham
chiếu Customer/Employee **bằng id**, nên nó KHÔNG chứa `CustomerName`. Đúng DDD: một aggregate không ôm
dữ liệu của aggregate khác (tránh dữ liệu lỗi thời).

Khi cần **hiển thị** "đơn này của khách nào, ai bán, đã mua bao nhiêu đơn", ta cần dữ liệu ghép nhiều
bảng. Dự án **tách hẳn đọc/ghi (CQRS)** ở cả tầng Domain lẫn Infrastructure:

| | Ghi (Command) | Đọc (Query) |
|---|---|---|
| Application | `*Command` + Handler | `*Query` + Handler |
| Inbound port (Domain) | `I*WriteService` | `I*ReadService` |
| Domain service | `*WriteService` (business rule) | `*ReadService` (forward read) |
| Outbound port (Domain) | `I*WriteRepository` trả aggregate | `I*ReadRepository` trả `*View` |
| Ghép nhiều bảng | ❌ (vỡ ranh giới aggregate) | ✅ JOIN/GROUP BY trong read repository |

**Vì sao tách read/write?** Hai bên có *động cơ* khác nhau: ghi cần aggregate đầy đủ + invariant; đọc cần
read-model phẳng tối ưu hiển thị. Tách giao diện giúp mỗi bên thay đổi độc lập và đọc không "mượn" được
method ghi. Method đọc thuộc dữ liệu của aggregate nào thì nằm ở read service của aggregate đó (vd lịch
sử đơn của khách = dữ liệu Order → `IOrderReadService.GetCustomerHistoryAsync`).

**Báo cáo (Reports) là ca đặc biệt:** không có aggregate "Report", nên chỉ có `IReportReadService` +
`IReportReadRepository` (không có write side). Input đọc gói trong record `*SearchQuery` ở `Domain/Queries`;
read-model `*View` ở `Domain/ReadModels`; `PagedResult<T>` ở `Domain/Common`.

**Mỗi Infrastructure tự chọn chiến lược thực thi cùng một read repository interface:**
- SQL Server: LINQ `join` → EF Core dịch thành **một câu SQL JOIN** chạy trên DB.
- MongoDB: không JOIN quan hệ → **nạp document rồi ghép (stitch) trong bộ nhớ** (hoặc denormalize/`$lookup`).

Đây lại là một lần nữa chứng minh luận điểm cốt lõi: đổi cơ chế lưu trữ, hợp đồng không đổi.

> 💡 **Tách read/write còn xuống tới kết nối DB (MongoDB):** write repository + `IUnitOfWork` dùng
> `MongoAppDbContext` (kết nối **primary**), read repository dùng `MongoReadDbContext`
> (`readPreference=secondaryPreferred`) → đọc đi vào **secondary** trên cụm replica set nhiều node,
> giảm tải primary. Tùy chọn (config `MongoDBRead`), đánh đổi eventual consistency. Xem
> [03-Infrastructure-MongoDB.md](code/03-Infrastructure-MongoDB.md) §C và ARCHITECTURE §11.

## 7d. Unit of Work — gói nhiều aggregate vào MỘT transaction

`DbContext.SaveChanges()` của EF Core đã atomic, nhưng **chỉ trong phạm vi một lần gọi**. Khi một
use-case ghi **nhiều aggregate qua nhiều repository** (vd `ConfirmAsync`: trừ kho `Product` **và** đổi
trạng thái `Order` = 2 lần `SaveChanges` ở 2 repository), ta cần một transaction bao ngoài để cả hai
"tất cả-hoặc-không".

`IUnitOfWork` (`Domain/Interfaces/Outbound`) là port cho việc đó:

```csharp
await using var tx = await myUnitOfWork.BeginTransactionAsync(ct);
try {
    await myProductWriteRepository.UpdateRangeAsync(products, ct);
    await myOrderWriteRepository.UpdateAsync(order, ct);
    await tx.CommitAsync(ct);          // 2 SaveChanges enlist chung 1 transaction
} catch { await tx.RollbackAsync(ct); throw; }
```

- Mọi repository trong cùng scope dùng **chung một `DbContext`**, nên các `SaveChanges` của chúng enlist
  vào transaction đang mở thay vì tự commit.
- `SqlServerUnitOfWork` map sang EF Core transaction; `MongoUnitOfWork` map sang **multi-document
  transaction** của MongoDB — chỉ chạy được khi server là **replica set** (xem §11). Cùng một port
  `IUnitOfWork`, Domain Service viết một lần, chạy được cả hai backend.
- Use-case chỉ ghi **một** aggregate (vd `ShipAsync`) thì **không** dùng UnitOfWork — một `SaveChanges`
  đã đủ atomic. UnitOfWork chỉ xuất hiện khi cần ghi ≥2 aggregate.

## 7c. Phân trang, tìm kiếm, lọc & sắp xếp (cũng là read side)

Danh sách thật luôn cần **phân trang** (đừng trả cả bảng), **tìm kiếm/lọc** nhiều tiêu chí và
**sắp xếp**. Đây vẫn là *cách đọc* → hợp đồng đặt ở **Domain** (`PagedResult<T>`, `PageRequest`,
interface query) + cách thực thi ở **Infrastructure**.

- **Domain**: `PagedResult<T>` (dữ liệu 1 trang + `TotalCount`/`TotalPages`/`HasNext`) và `PageRequest`
  nằm trong `Domain/Common`; record input `*SearchQuery` (`Domain/Queries`) gói filter + `sortBy`/`sortDescending`,
  được nhận bởi method `SearchAsync` trên `I*ReadService` (đẩy xuống `I*ReadRepository`). API gửi
  `Search*Query` (Application) qua `ISender`; handler bọc `*SearchQuery` rồi gọi read service.
- **SQL Server**: đẩy hết `WHERE`/`ORDER BY`/`COUNT`/`OFFSET-FETCH` xuống DB; tên hiển thị lấy bằng JOIN.
- **MongoDB**: đẩy phần làm được xuống DB; tìm theo tên cross-collection thì *resolve Id trước rồi `$in`*;
  phần không JOIN được (vd sort theo tên ghép) xử lý/giới hạn ở bộ nhớ — một minh hoạ cho khác biệt thực thi.

→ Chi tiết: [docs/code/06-Pagination-Search.md](code/06-Pagination-Search.md).

## 8. Xử lý lỗi nghiệp vụ

Domain ném exception, API có middleware chuyển thành ProblemDetails chuẩn:

| Exception (Domain) | HTTP | Khi nào |
|---|---|---|
| `DomainException` | 400 | Vi phạm invariant / business rule (vd: xác nhận đơn rỗng) |
| `ConflictException` | 409 | Xung đột/trùng/đụng trạng thái (vd: tên category trùng, SKU trùng, không đủ tồn kho, trả vượt nợ) |
| `NotFoundException` | 404 | Tham chiếu tới entity không tồn tại (vd: tạo Product với CategoryId sai) |
| `ValidationException` (FluentValidation) | 400 | Sai FORMAT request (trống, độ dài…) |

→ `ExceptionHandlingMiddleware` bắt theo thứ tự này. Controller không cần `try/catch` hay tự trả 404
cho entity liên quan — service ném, middleware lo phần còn lại.

**Validation đầu vào chạy TỰ ĐỘNG trong pipeline MediatR:** validator (`*CommandValidator`/`*QueryValidator`)
sống cạnh command/query ở **tầng Application**. `ValidationBehavior<TRequest,TResponse>` (một
`IPipelineBehavior` của MediatR, đăng ký làm bước **ngoài cùng**) gom mọi `IValidator<TRequest>`, chạy
trước handler; lỗi → ném `ValidationException` (đi vào bảng trên → 400). Nhờ vậy controller chỉ
`mySender.Send(...)`, không inject validator. Request không có validator thì pipeline cho đi thẳng. Đây là
cách "đúng chuẩn": validate *hình dạng* request là concern cắt ngang → gom về một pipeline behavior.

## 9. Application Handler + Domain Service — API gửi Command, không gọi repository

Controller **không** tự `new`/`Entity.Create()`, **không** gọi repository, **không** gọi thẳng Domain
Service. Nó **gửi Command/Query qua MediatR**. Handler (Application) là adapter mỏng; logic ghi thật (kể
cả các kiểm tra cần truy vấn DB như "tên không trùng", "entity liên quan phải tồn tại") nằm trong
**Domain Service**.

```
API (Controller, ISender)
  │  mySender.Send(command/query)
  ▼
Application (Handler mỏng + ValidationBehavior)   → I*WriteService / I*ReadService  (Domain inbound port)
                                                          │  business logic
                                                          ▼
                                                    I*WriteRepository / I*ReadRepository (+ IUnitOfWork)
                                                          ↑ Infrastructure implement
```

- Mỗi use-case một file ở Application: `*Command`/`*Query` (record input) + `*Handler` (gọi Domain
  Service) + `*Validator` (FluentValidation) — gói chung 1 `.cs`.
- Domain tách `I*WriteService` (business rule, phối hợp aggregate + repository, ném `DomainException`/
  `ConflictException`/`NotFoundException`) và `I*ReadService` (forward read xuống read repository).
- Đăng ký: `AddApplicationServices()` (Application) gọi `AddDomainServices()` (Domain) + đăng ký MediatR,
  `ValidationBehavior`, validators. `Program.cs` chỉ gọi `AddApplicationServices()` cho cụm này.
- Phụ thuộc kỹ thuật cần thiết: Domain ref `Microsoft.Extensions.DependencyInjection.Abstractions`
  (chỉ là contract `IServiceCollection`). Domain **không** ref MediatR — MediatR chỉ ở Application.
- Ví dụ ngoại lệ hướng phụ thuộc: `IPasswordHasher` khai báo ở Domain (`Interfaces/Outbound`), API
  implement (`PasswordHasher`) và đăng ký DI — Domain định nghĩa hợp đồng, tầng ngoài cắm vào.

> Lưu ý quan niệm: đổi *chữ ký* command/behavior thì nơi gọi phải đổi (kiến trúc nào cũng vậy). Clean
> Architecture chỉ bảo đảm: đổi *chi tiết hạ tầng/DB* không lan vào trong — không phải "đổi Domain mà
> API vô can".

## 11. MongoDB transaction cần REPLICA SET

`IUnitOfWork` (§7d) khi chạy trên Mongo map sang **multi-document transaction**. MongoDB **chỉ cho phép**
transaction nhiều document khi server chạy ở chế độ **replica set** (hoặc sharded cluster) — standalone
`mongod` sẽ throw ngay khi `BeginTransactionAsync`. Đây là lý do flow `ConfirmAsync`/`CancelAsync` (ghi 2
aggregate: kho + Order) bắt buộc cần replica set khi `DatabaseProvider = MongoDB`.

Replica set là **cấu hình hạ tầng**, không phải code C#. Trong dự án nó xuất hiện ở:

| Chỗ | Vai trò |
|---|---|
| [`docker-compose.mongo-rs.yml`](../../docker-compose.mongo-rs.yml) | **1-node** replica set (đủ cho transaction); `mongo-init` chạy `rs.initiate()` 1 lần |
| [`docker-compose.mongo-rs-3node.yml`](../../docker-compose.mongo-rs-3node.yml) | **3-node** (1 primary + 2 secondary) — HA + failover thật (cần sửa hosts file) |
| `appsettings.json` connection string | 1-node: `mongodb://localhost:27017/?replicaSet=rs0...`; 3-node: `mongodb://mongo1:27017,mongo2:27018,mongo3:27019/?replicaSet=rs0...` |
| `MongoUnitOfWork.BeginTransactionAsync` | Nơi dùng tới — mở transaction (đứng standalone sẽ lỗi) |
| [`docs/mongo-replica-set.md`](../../docs/mongo-replica-set.md) | Giải thích + hướng dẫn dựng 1-node & 3-node |

> 💡 SQL Server không cần cấu hình gì thêm: transaction của EF Core chạy ngay trên một instance bình thường.
> Replica set 1-node là tối thiểu vừa đủ để mở khóa transaction; 3-node chỉ cần khi muốn HA/failover thật.
