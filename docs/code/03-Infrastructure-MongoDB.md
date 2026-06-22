# 03 — Infrastructure.MongoDB

Tầng này hiện thực **đúng bộ port outbound** (các `I*WriteRepository`/`I*ReadRepository` +
`IUnitOfWork`) giống hệt bản SQL, nhưng lưu vào **MongoDB** (cũng qua EF Core, dùng provider
`MongoDB.EntityFrameworkCore`). Cấu trúc song song với file 02, nên ở đây tôi nhấn mạnh **những chỗ KHÁC**
— và chính sự khác đó là điều chứng minh "Domain độc lập DB".

> 💡 Điểm học lớn nhất: Domain entity, repository interface, mapper *signature* đều như nhau. Chỉ
> phần lưu trữ đổi. Đổi DB = đổi tầng này + một dòng DI ở API.

---

## A. Documents — `Models/*Document.cs`
POCO trần `internal sealed`, tương tự `*Record` của SQL. **Khác biệt cốt lõi nằm ở Order:**

- **SQL**: `OrderRecord` + `OrderDetailRecord` = **2 bảng**, nối bằng khóa ngoại.
- **Mongo**: `OrderDocument` chứa `List<OrderDetailDocument> Details` được **nhúng thẳng vào trong
  một document** (mảng con). `OrderDetailDocument` **không** là collection riêng.

💡 Đây là sự khác biệt tự nhiên giữa hai loại DB: SQL chuẩn hóa thành nhiều bảng; Mongo gom cả cụm
vào một tài liệu. Domain `Order` (giữ `List<OrderDetail>`) không quan tâm bên dưới lưu kiểu nào.

Các document còn lại (`UserDocument, CustomerDocument, ...`) gần như y hệt record tương ứng.

### 💡 Lớp cơ sở chung — `Models/BaseDocument.cs`
Song song với `BaseRecord` bên SQL (file 02): một `internal abstract class BaseDocument` gom 4 field
chung `Id, Status, CreatedAtUtc, UpdatedAtUtc`. Mọi document `: BaseDocument`, kể cả
`OrderDetailDocument` nhúng bên trong order.
- Đặt tên `BaseDocument` (không phải `BaseRecord`) để khớp quy ước hậu tố `*Document` của tầng Mongo.
- Field `Id` vẫn được map sang `_id` theo quy ước, không đổi gì.

---

## B. Cấu hình — `Configurations/MongoConfigurations.cs`
Gom tất cả config vào **một file** (mỗi entity vẫn là một class `IEntityTypeConfiguration`). Khác SQL:

| SQL | Mongo |
|-----|-------|
| `ToTable("orders")` | **`ToCollection("orders")`** |
| `HasMany(...).WithOne().HasForeignKey(...)` | **`OwnsMany(x => x.Details)`** — nhúng mảng con |
| `HasQueryFilter(Status != Deleted)` | ❌ **không có** — provider Mongo chưa hỗ trợ tốt → lọc trong repository |
| `HasIndex`, `HasColumnType`, `HasMaxLength` | không cần (Mongo không ràng buộc schema cứng) |

- **`ToCollection`** — ánh xạ entity vào một collection MongoDB.
- **`OwnsMany(x => x.Details)`** — khai báo `Details` là **owned type nhúng** (embedded). Provider sẽ
  lưu thành mảng con trong document `orders`.
- Khóa: property tên `Id` được map sang `_id` của Mongo theo quy ước.

💡 Vì Mongo **không có migration**, collection được tạo tự động khi ghi dữ liệu lần đầu — không cần
`dotnet ef database update`.

---

## C. Persistence context — TÁCH write/read theo node

Phần mapping (`DbSet<*Document>` + `OnModelCreating` gọi `ApplyConfigurationsFromAssembly`) gom vào
`MongoDbContextBase`. Hai context dẫn xuất, **chỉ khác nhau ở connection string** (DI wiring), không
khác model:

| Context | Connection (`readPreference`) | Ai dùng |
|---|---|---|
| `MongoAppDbContext` | `MongoDB` → **primary** | Write repository + `MongoUnitOfWork` (ghi + transaction **bắt buộc** primary) |
| `MongoReadDbContext` | `MongoDBRead` → **secondaryPreferred** | Read repository (`Repositories/Read/*`) — query đọc từ **secondary** trên cụm nhiều node |

💡 Đây mới là phần "primary ghi / secondary đọc" thực sự (định tuyến vật lý), bổ sung cho việc tách
interface read/write ở Domain. `MongoDBRead` là **tùy chọn**: thiếu thì DI fallback về connection
primary (single-node / chạy SQL vẫn hoạt động bình thường).
> ⚠️ Secondary có **replication lag** → đọc ngay sau ghi có thể ra dữ liệu cũ (eventual consistency).
> Hợp cho list/report; không hợp "read-your-own-write". Chi tiết bật/tắt: [docs/mongo-replica-set.md](../../../docs/mongo-replica-set.md).
Không cần `OnConfiguring` (không có query filter để chỉnh cảnh báo).

---

## D. Mapper — `Mapping/*Mapper.cs`
Cùng bộ 3 method như SQL, chỉ đổi `*Record` → `*Document`:
- `ToDocument(domain)` (insert), `MapInto(target, source)` (update), `ToDomain(document)` (đọc, gọi `Rehydrate`).

### `OrderMapper.cs` — khác SQL ở chỗ update
- SQL phải `SyncDetails` (hòa giải từng dòng vì là bảng riêng).
- Mongo **đơn giản hơn**: `MapInto` chỉ cần **dựng lại cả mảng** `Details`:
  ```csharp
  theTarget.Details = theSource.Details.Select(ToDetailDocument).ToList();
  ```
  💡 Vì Details là mảng nhúng trong cùng một document, ghi đè cả mảng là tự nhiên và an toàn — không
  có "bản ghi con mồ côi" như mô hình quan hệ.

---

## E. Repository — `Repositories/Write/*` + `Repositories/Read/*`
Tách Read/Write y như bản SQL (`Mongo*WriteRepository`/`Mongo*ReadRepository`), nhưng vì **không có global
query filter**, mỗi truy vấn đọc phải **tự lọc xóa mềm**:
```csharp
private const int DeletedStatus = (int)EntityStatus.Deleted;   // = 3
...
.Where(r => r.Status != DeletedStatus)            // ẩn record đã xóa mềm
```
- 💡 Cùng một mục tiêu "ẩn dữ liệu đã xóa", nhưng cách làm khác tầng SQL: SQL để EF tự lọc, Mongo lọc tay.
  Đây là ví dụ điển hình "chi tiết hạ tầng khác nhau, hợp đồng (interface) như nhau".
- **`MongoOrderWriteRepository`** — **không cần `.Include(Details)`** vì Details đã nằm sẵn trong document.
- **Read repository** — không JOIN quan hệ → nạp document liên quan rồi **ghép (stitch) trong bộ nhớ** để
  dựng `*View` (xem [05-Application.md](05-Application.md) §E).

### `Persistence/MongoUnitOfWork.cs`
Hiện thực `IUnitOfWork`: `BeginTransactionAsync` mở transaction trên `MongoAppDbContext.Database`.
- ⚠️ MongoDB **chỉ cho phép multi-document transaction khi server chạy ở chế độ REPLICA SET** (hoặc
  sharded cluster). Đứng standalone `mongod` → `BeginTransactionAsync` **throw**. Đây là lý do flow
  `ConfirmAsync`/`CancelAsync` (ghi Product + Order atomic) cần replica set khi `DatabaseProvider=MongoDB`.
- Dựng replica set local: [`docker-compose.mongo-rs.yml`](../../../docker-compose.mongo-rs.yml) + connection
  string `...?replicaSet=rs0`. Chi tiết & 3-node: [`docs/mongo-replica-set.md`](../../../docs/mongo-replica-set.md).
- 💡 Đây cũng là một khác biệt hạ tầng "lộ ra": SQL Server transaction chạy ngay trên instance bình thường,
  Mongo cần cấu hình cụm — nhưng port `IUnitOfWork` mà Domain thấy thì y hệt.

---

## F. `DependencyInjection.cs`
Extension method **`AddMongoInfrastructure(theServices, theConfiguration)`**:
1. Đọc connection string `"MongoDB"` + tên database (`MongoDatabaseName`, mặc định `SellingNewProduct`).
2. `AddDbContext<MongoAppDbContext>(o => o.UseMongoDB(connString, dbName))`.
3. Đăng ký **đúng bộ interface giống bản SQL** (mỗi aggregate một `I*WriteRepository` + `I*ReadRepository`,
   `IReportReadRepository`, `IUnitOfWork`), nhưng trỏ tới các class `Mongo*`.

💡 So sánh hai file `DependencyInjection`:
- SQL đăng ký `IOrderWriteRepository → SqlServerOrderWriteRepository`, `IUnitOfWork → SqlServerUnitOfWork`.
- Mongo đăng ký `IOrderWriteRepository → MongoOrderWriteRepository`, `IUnitOfWork → MongoUnitOfWork`.

Cùng một interface, hai implementation. API chọn bên nào tuỳ `DatabaseProvider`. **Đó là toàn bộ "phép
màu" của Clean Architecture trong dự án này.**
