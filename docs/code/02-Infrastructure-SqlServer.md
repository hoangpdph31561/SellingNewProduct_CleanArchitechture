# 02 — Infrastructure.SqlServer

Tầng này hiện thực các interface repository của Domain bằng **EF Core + SQL Server**. Nó *biết* về
database; Domain thì không. Luồng: `Domain entity ⇄ (mapper tay) ⇄ *Record ⇄ (EF Core) ⇄ bảng SQL`.

---

## A. Persistence models — `Models/*Record.cs`

Đây là **POCO trần** (chỉ property, không method, không attribute). Mỗi cái ứng với một bảng SQL.
- `internal sealed` — chỉ dùng nội bộ tầng này, bên ngoài không thấy.
- 💡 **Không có `[Key]`, `[Required]`...** — mọi cấu hình schema để ở file Configuration (Fluent API).
- Value Object bị "làm phẳng" thành cột nguyên thủy:
  - `Money` → `PriceAmount` (decimal) + `PriceCurrency` (string).
  - `Address` → `Street, Ward, District, City, Country`.
  - `Email`/`Sku` → string.
  - Enum → `int` (vd `OrderStatus`, `Status`).

Danh sách: `UserRecord, CustomerRecord, EmployeeRecord, CategoryRecord, ProductRecord, OrderRecord,
OrderDetailRecord, PaymentRecord`.

### 💡 Lớp cơ sở chung — `Models/BaseRecord.cs`
Mọi record đều có chung 4 cột (đúng bộ field của `BaseEntity<TId>` ở Domain), nên được gom vào một
lớp `internal abstract class BaseRecord`:

| Cột | Kiểu | Ý nghĩa |
|-----|------|---------|
| `Id` | `Guid` | Khóa chính |
| `Status` | `int` | Vòng đời / xóa mềm (map từ enum `EntityStatus`) |
| `CreatedAtUtc` | `DateTime` | Mốc tạo (audit) |
| `UpdatedAtUtc` | `DateTime?` | Mốc cập nhật gần nhất (audit) |

Mỗi record `: BaseRecord` rồi chỉ khai báo phần **riêng** của mình → bớt lặp, đồng nhất với Domain.
- 💡 `BaseRecord` **không phải entity**: không có `DbSet`, không bị navigation nào trỏ tới. Nên EF Core
  **không** coi đây là kế thừa TPH — nó chỉ "rải" 4 cột này vào từng bảng riêng. Schema y hệt trước,
  **không phát sinh migration mới**.
- Bên Mongo có lớp song song `BaseDocument` (xem file 03).

💡 Đáng chú ý: **`OrderRecord` có `List<OrderDetailRecord> Details`** — trong SQL, Order tách thành
2 bảng `Orders` + `OrderDetails` (quan hệ 1-n). (Mongo sẽ làm khác — xem file 03.)

---

## B. Cấu hình schema — `Configurations/*Configuration.cs`

Mỗi bảng một class hiện thực `IEntityTypeConfiguration<TRecord>`, có 1 method `Configure(theBuilder)`
dùng **Fluent API**. Ví dụ điển hình `OrderConfiguration`:

| Lệnh | Ý nghĩa |
|------|---------|
| `ToTable("Orders")` | Đặt tên bảng |
| `HasKey(x => x.Id)` | Khóa chính |
| `Property(x => x.TotalAmount).HasColumnType("decimal(18,2)")` | Kiểu cột |
| `Property(x => x.ShippingCity).HasMaxLength(100).IsRequired()` | Độ dài + NOT NULL |
| `HasMany(x => x.Details).WithOne().HasForeignKey(d => d.OrderId).OnDelete(Cascade)` | 💡 Order 1-n OrderDetail, xóa Order thì xóa luôn dòng (cùng aggregate) |
| `HasOne<CustomerRecord>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(Restrict)` | 💡 Khóa ngoại sang aggregate khác, KHÔNG cascade |
| `HasQueryFilter(x => x.Status != (int)EntityStatus.Deleted)` | 💡 **Xóa mềm**: tự ẩn record đã xóa khỏi mọi truy vấn |

Các điểm hay theo từng file:
- **`UserConfiguration`**: `HasIndex(x => x.Username).IsUnique()` — username không trùng (đây là việc
  của tầng DB, không phải business).
- **`ProductConfiguration`**: unique `Sku`, FK sang `Category`.
- **`OrderConfiguration`**: như bảng trên — minh họa rõ "trong aggregate dùng cascade, ngoài aggregate
  dùng restrict".
- **`OrderDetailConfiguration`**: bảng con, có index `OrderId`, FK sang `Product` (restrict).
- **`PaymentConfiguration`**: FK sang `Order`.

💡 Câu chốt: **cùng một `Order.CustomerId` (Guid) trong Domain**, ở đây nó thành **khóa ngoại thật**
trong DB. DB lo việc nối bảng; Domain không hề biết.

---

## C. `Persistence/AppDbContext.cs`
"Cửa" của EF Core tới SQL Server.
- Có các `DbSet<*Record>` (internal) cho từng bảng.
- **`OnModelCreating`** — gọi `ApplyConfigurationsFromAssembly(...)`: tự nạp **mọi** file Configuration
  trong assembly. 💡 Thêm bảng mới chỉ cần thêm 1 file Configuration, không phải sửa DbContext.
- **`OnConfiguring`** — tắt một cảnh báo của EF về việc query filter trên cả hai đầu quan hệ (ta cố ý
  đặt filter xóa mềm ở mọi bảng).

## D. `Persistence/AppDbContextFactory.cs`
Factory `IDesignTimeDbContextFactory<AppDbContext>` — chỉ dùng khi chạy **lệnh EF** (`migrations`,
`database update`) ở design-time, vì lúc đó chưa có DI của API.
- **`CreateDbContext(theArgs)`** — dựng `AppDbContext` với connection string LocalDB cứng.
- 💡 Runtime (khi chạy app) KHÔNG dùng factory này — runtime đọc connection từ `appsettings` qua DI.

---

## E. Mapper tay — `Mapping/*Mapper.cs`

💡 Ta **không dùng AutoMapper** (đã chốt). Mỗi mapper là `internal static`, có 3 method:

| Method | Hướng | Dùng khi |
|--------|-------|----------|
| `ToRecord(domain)` | Domain → Record | **Thêm mới** (insert) |
| `MapInto(target, source)` | Domain → Record (ghi đè) | **Cập nhật** record đang được EF theo dõi |
| `ToDomain(record)` | Record → Domain | **Đọc** từ DB (gọi `Entity.Rehydrate(...)`) |

Vì sao có cả `ToRecord` và `MapInto`?
- Khi **insert**, tạo record mới hoàn toàn → `ToRecord`.
- Khi **update**, ta nạp record cũ (EF đang theo dõi) rồi chỉ ghi đè field → `MapInto`. Nếu thay bằng
  record mới, EF sẽ rối change-tracking. 💡 Đây là lý do tách hai method.

`ToDomain` gọi `Rehydrate` chứ không gọi `Create` — để **không sinh Id mới và không chạy lại validate**
khi chỉ đang đọc dữ liệu có sẵn.

### `OrderMapper.cs` (phức tạp nhất)
- **`ToRecord`** — tạo `OrderRecord` + duyệt `Details` tạo từng `OrderDetailRecord`.
- **`MapInto`** — cập nhật field rồi gọi **`SyncDetails`** để đồng bộ danh sách con:
  - **`SyncDetails`** — xóa record con không còn trong domain, cập nhật cái đang có, thêm cái mới.
    💡 Đây là cách "hòa giải" (reconcile) collection con khi update ở SQL chuẩn hóa (nhiều bảng).
- **`ToDomain`** — dựng lại từng `OrderDetail` rồi `Order.Rehydrate(...)`.

---

## F. Repository — `Repositories/Write/*` + `Repositories/Read/*`

Theo CQRS, repository tách **hai nhóm** (mỗi class `internal sealed`, nhận `AppDbContext` qua constructor):
- `Repositories/Write/SqlServer*WriteRepository.cs` — hiện thực `I*WriteRepository` (trả/nhận aggregate).
- `Repositories/Read/SqlServer*ReadRepository.cs` — hiện thực `I*ReadRepository` (đọc bằng JOIN, trả `*View`).

Mẫu chung phía **ghi**:

```csharp
// Đọc: AsNoTracking (không cần theo dõi) + map sang domain
var aRecord = await myAppDbContext.Users.AsNoTracking()
    .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);
return aRecord is null ? null : UserMapper.ToDomain(aRecord);

// Thêm: map sang record + Add + SaveChanges
myAppDbContext.Users.Add(UserMapper.ToRecord(theUser));
await myAppDbContext.SaveChangesAsync(theCancellationToken);

// Sửa: nạp record đang theo dõi + MapInto + SaveChanges
var aRecord = await myAppDbContext.Users.FirstOrDefaultAsync(r => r.Id == theUser.Id, ...);
if (aRecord is null) return;
UserMapper.MapInto(aRecord, theUser);
await myAppDbContext.SaveChangesAsync(...);
```
- 💡 `AsNoTracking()` khi đọc → nhanh hơn, vì không cần EF theo dõi thay đổi.
- 💡 Khi sửa thì **không** dùng `AsNoTracking` (cần EF theo dõi để biết cột nào đổi).
- **`SqlServerOrderWriteRepository`** — khi đọc/sửa Order phải `.Include(r => r.Details)` để nạp kèm bảng con.
- **Read repository** (`Repositories/Read/`) dùng LINQ `join`/`group by` → EF dịch thành SQL JOIN/GROUP BY,
  trả thẳng read-model `*View` (xem [05-Application.md](05-Application.md) §E và [06-Pagination-Search.md](06-Pagination-Search.md)).
- Nhờ `HasQueryFilter`, các bản ghi đã xóa mềm **tự động bị loại** — repository không cần tự lọc.

### `Persistence/SqlServerUnitOfWork.cs`
Hiện thực `IUnitOfWork`: `BeginTransactionAsync` mở `AppDbContext.Database.BeginTransactionAsync()` và bọc
trong `EfCoreUnitOfWorkTransaction` (adapt `IDbContextTransaction` sang hợp đồng Domain). Mọi write
repository trong cùng scope dùng chung `AppDbContext`, nên các `SaveChanges` của chúng enlist vào transaction
này và commit một lần — dùng cho `OrderWriteService.ConfirmAsync/CancelAsync` (ghi Product + Order atomic).

---

## G. `DependencyInjection.cs`
Class `static` với extension method **`AddSqlServerInfrastructure(theServices, theConfiguration)`**:
1. Đọc connection string `"SqlServer"` từ config (không có thì ném lỗi rõ ràng).
2. `AddDbContext<AppDbContext>(o => o.UseSqlServer(...))`.
3. Đăng ký các cặp `interface → implementation` dạng `AddScoped`: mỗi aggregate một `I*WriteRepository`
   + một `I*ReadRepository`, `IReportReadRepository`, và **`IUnitOfWork → SqlServerUnitOfWork`**.

💡 Đây là một trong hai "công tắc" DB. API chỉ gọi 1 dòng `AddSqlServerInfrastructure(...)` là xong.

---

## H. Migration — `Migrations/`
Sinh bởi lệnh `dotnet ef migrations add InitialCreate`. Gồm:
- `*_InitialCreate.cs` — `Up()` tạo bảng, `Down()` xóa bảng.
- `AppDbContextModelSnapshot.cs` — ảnh chụp model hiện tại để EF so sánh cho migration sau.
💡 Migration là cách EF "phiên bản hóa" schema. Chạy `dotnet ef database update` để áp vào DB thật.
(MongoDB **không** có khái niệm này — xem file 03.)
