# 03 — Infrastructure.MongoDB

Tầng này hiện thực **đúng 7 interface repository** giống hệt bản SQL, nhưng lưu vào **MongoDB** (cũng
qua EF Core, dùng provider `MongoDB.EntityFrameworkCore`). Cấu trúc song song với file 02, nên ở đây
tôi nhấn mạnh **những chỗ KHÁC** — và chính sự khác đó là điều chứng minh "Domain độc lập DB".

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

## C. `Persistence/MongoAppDbContext.cs`
Giống `AppDbContext` của SQL: các `DbSet<*Document>` + `OnModelCreating` gọi
`ApplyConfigurationsFromAssembly`. Không cần `OnConfiguring` (không có query filter để chỉnh cảnh báo).

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

## E. Repository — `Repositories/Mongo*Repository.cs`
Giống bản SQL, nhưng vì **không có global query filter**, mỗi truy vấn đọc phải **tự lọc xóa mềm**:
```csharp
private const int DeletedStatus = (int)EntityStatus.Deleted;   // = 3
...
.Where(r => r.Status != DeletedStatus)            // ẩn record đã xóa mềm
```
- 💡 Cùng một mục tiêu "ẩn dữ liệu đã xóa", nhưng cách làm khác tầng SQL: SQL để EF tự lọc, Mongo lọc tay.
  Đây là ví dụ điển hình "chi tiết hạ tầng khác nhau, hợp đồng (interface) như nhau".
- **`MongoOrderRepository`** — **không cần `.Include(Details)`** vì Details đã nằm sẵn trong document
  (đọc document là có luôn). Đối lập với SQL phải Include bảng con.

---

## F. `DependencyInjection.cs`
Extension method **`AddMongoInfrastructure(theServices, theConfiguration)`**:
1. Đọc connection string `"MongoDB"` + tên database (`MongoDatabaseName`, mặc định `SellingNewProduct`).
2. `AddDbContext<MongoAppDbContext>(o => o.UseMongoDB(connString, dbName))`.
3. Đăng ký **đúng 7 interface giống bản SQL**, nhưng trỏ tới `Mongo*Repository`.

💡 So sánh hai file `DependencyInjection`:
- SQL đăng ký `IOrderRepository → SqlServerOrderRepository`.
- Mongo đăng ký `IOrderRepository → MongoOrderRepository`.

Cùng một interface, hai implementation. API chọn bên nào tuỳ `DatabaseProvider`. **Đó là toàn bộ "phép
màu" của Clean Architecture trong dự án này.**
