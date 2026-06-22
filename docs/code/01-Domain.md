# 01 — Tầng Domain

Domain là **trái tim**: chứa business thuần, không biết gì về database, API hay EF Core.
Mọi file ở đây chỉ dùng thư viện chuẩn .NET (BCL).

---

## A. Common — nền tảng dùng chung (`Domain/Common/`)

### `EntityStatus.cs`
Enum trạng thái vòng đời của mọi entity, phục vụ **xóa mềm**.
```
Active = 1, Inactive = 2, Deleted = 3
```
💡 Xóa mềm: không bao giờ `DELETE` khỏi DB. Khi "xóa", ta đặt `Status = Deleted` rồi tầng
Infrastructure tự lọc bỏ khi đọc. Nhờ vậy dữ liệu lịch sử vẫn còn, có thể khôi phục.

### `DomainException.cs`
Exception **riêng** của domain, ném khi vi phạm quy tắc nghiệp vụ (vd xác nhận đơn rỗng).
- `sealed` để không ai kế thừa lung tung.
- Tách riêng để tầng API biết: "đây là lỗi nghiệp vụ" → trả 400, khác với lỗi hệ thống (500).
💡 Không dùng `Exception` trơn hay `ArgumentException` cho lỗi nghiệp vụ — tách loại lỗi giúp
xử lý đúng ở tầng ngoài.

### `NotFoundException.cs`
Exception riêng cho trường hợp **entity liên quan không tồn tại** (vd tạo Product với `CategoryId` sai,
thanh toán cho Order không có). Domain Service ném loại này; API map sang **404** (khác `DomainException`
→ 400). `sealed`, chỉ mang message.

### `PagedResult.cs` — `PagedResult<T>` + `PageRequest`
Thuộc read side nhưng đặt ở Common: `PagedResult<T>` = 1 trang dữ liệu + `TotalCount`/`TotalPages`/
`HasNext`/`HasPrevious`; `PageRequest` kẹp `page`/`pageSize` về khoảng an toàn ở MỘT chỗ. Xem
[06-Pagination-Search.md](06-Pagination-Search.md).

### `IDomainEvent.cs`
Interface "đánh dấu" (marker) cho **sự kiện nghiệp vụ đã xảy ra**.
- Chỉ có `DateTime OccurredOnUtc { get; }`.
💡 Domain event ghi lại "việc gì đã xảy ra" (vd `OrderConfirmedEvent`). Hiện ta chỉ *phát sinh*
và lưu trong aggregate; sau này có thể thêm bộ xử lý (gửi email, trừ kho...) mà không sửa Order.

### `BaseEntity.cs` — `BaseEntity<TId>`
Lớp cha của **mọi entity**. Generic `TId` để Id linh hoạt (ở đây luôn là `Guid`).

| Thành phần | Ý nghĩa |
|-----------|---------|
| `Id` | Định danh, setter `protected` (chỉ lớp con / rehydrate đặt được) |
| `CreatedAtUtc`, `UpdatedAtUtc` | Dấu thời gian audit |
| `Status` | `EntityStatus` cho xóa mềm |

Method:
- **`BaseEntity(TId theId)`** — constructor chính: gán Id, set `CreatedAtUtc = UtcNow`, `Status = Active`.
- **`BaseEntity()`** (protected, rỗng) — dành cho tầng persistence dựng lại object. Không chạy logic.
- **`Activate()` / `Deactivate()` / `Delete()`** — đổi `Status` rồi gọi `MarkUpdated()`. `Delete()`
  chính là xóa mềm.
- **`MarkUpdated()`** (protected) — cập nhật `UpdatedAtUtc = UtcNow`. Gọi mỗi khi state đổi.
- **`RestoreState(...)`** (protected) — đặt lại Id/Status/timestamps khi đọc từ DB. 💡 Chỉ dùng bởi
  các factory `Rehydrate`, không phải code nghiệp vụ.
- **`Equals` / `GetHashCode`** — hai entity **bằng nhau khi cùng Id** (identity equality), không so
  sánh từng thuộc tính. Đây là bản chất của Entity trong DDD.

### `AggregateRoot.cs` — `AggregateRoot<TId> : BaseEntity<TId>`
Lớp cha của **aggregate root** (entity là "cổng vào" của một cụm). Thêm khả năng phát sự kiện.
- `myListDomainEvents` (private field) — danh sách event chưa xử lý.
- **`DomainEvents`** — lộ ra dạng `IReadOnlyList` (bên ngoài đọc, không sửa được).
- **`Raise(theDomainEvent)`** (protected) — aggregate gọi để ghi nhận một event.
- **`ClearDomainEvents()`** — xóa list sau khi đã xử lý (tầng ngoài gọi sau khi lưu).
💡 Chỉ aggregate root mới phát event, vì nó là nơi duy nhất kiểm soát thay đổi của cả cụm.

### `ValueObject.cs`
Lớp cha của **value object** — so sánh **theo giá trị**, không có Id.
- **`GetEqualityComponents()`** (abstract) — lớp con trả về danh sách thành phần để so sánh.
- **`Equals`** — hai VO bằng nhau khi mọi thành phần bằng nhau (`SequenceEqual`).
- **`GetHashCode`** — gộp hash của các thành phần.
💡 Khác Entity: hai tờ 50k bất kỳ là như nhau (value), nhưng hai khách hàng trùng tên vẫn là hai
người khác (entity, phân biệt bằng Id).

---

## B. Value Objects (`Domain/ValueObjects/`)

Tất cả VO: constructor `private` + factory `Create(...)` validate. 💡 Hệ quả: **không thể tạo VO
sai luật** — vd không có `Email` nào sai định dạng tồn tại trong hệ thống.

### `Money.cs`
Số tiền = `Amount` (decimal ≥ 0) + `Currency` (mặc định "VND").
- **`Create(amount, currency)`** — chặn số âm, chặn currency rỗng, chuẩn hóa currency in hoa.
- **`Zero(currency)`** — tạo số tiền 0 (dùng làm điểm khởi đầu khi cộng tổng).
- **`Add(theOther)`** — cộng hai Money; **bắt buộc cùng currency** (`EnsureSameCurrency`), khác thì ném lỗi.
- **`Multiply(theQuantity)`** — nhân với số lượng (tính `LineTotal`).
- **`GetEqualityComponents`** — so sánh theo `Amount` + `Currency`.
💡 Dùng `Money` thay cho `decimal` trơn để không bao giờ "cộng nhầm VND với USD", và gom luật tiền tệ một chỗ.

### `Email.cs`
Bọc một chuỗi email hợp lệ. `partial` vì dùng `[GeneratedRegex]` (regex biên dịch sẵn, nhanh).
- **`Create(value)`** — chặn rỗng, chuẩn hóa về chữ thường, kiểm regex; sai thì ném `DomainException`.
- **`EmailRegex()`** — regex sinh tự động lúc biên dịch.

### `Address.cs`
Địa chỉ bất biến: `Street, Ward, District, City, Country`.
- **`Create(...)`** — bắt buộc có `Street` và `City`; `Country` mặc định "Vietnam".
- 💡 Bất biến (immutable): muốn đổi địa chỉ thì tạo Address mới và thay cả cục, không sửa từng field.

### `Sku.cs`
Mã sản phẩm (vd `TSHIRT-RED-M`). `Create` chặn rỗng, chuẩn hóa in hoa.

---

## C. Các Aggregate (8 entity)

### `Products/Size.cs`
Enum size áo quần: `S, M, L, XL, XXL`.

### `Users/UserRole.cs` & `Users/User.cs`
`UserRole`: `Admin, Manager, Sales`.

`User : AggregateRoot<Guid>` — tài khoản đăng nhập.
- Thuộc tính: `Username`, `PasswordHash` (💡 chỉ lưu hash, domain không bao giờ thấy mật khẩu thật),
  `Email`, `Role`. Tất cả setter `private`.
- **`Create(username, passwordHash, email, role)`** — factory: chặn username/hash rỗng, sinh `Guid` mới.
- **`ChangePassword` / `ChangeEmail` / `ChangeRole`** — đổi từng phần, mỗi method gọi `MarkUpdated()`.
- **`Rehydrate(...)`** — dựng lại User từ dữ liệu DB (dùng object initializer cho field + `RestoreState`
  cho audit). 💡 Không gọi `Create` khi đọc DB vì không muốn sinh Id mới / chạy lại validate.

### `Customers/Customer.cs`
`Customer : AggregateRoot<Guid>` — khách mua.
- Thuộc tính: `FullName`, `Email`, `PhoneNumber`, `DefaultAddress`, `UserId?` (liên kết tài khoản, tùy chọn).
- 💡 `UserId` là **Guid**, không phải object `User` — vì User là aggregate khác (tham chiếu bằng Id).
- **`Create(...)`** — chặn tên/điện thoại rỗng.
- **`Rename` / `ChangeEmail` / `UpdateAddress`** — hành vi nghiệp vụ, mỗi cái `MarkUpdated()`.
- **`Rehydrate(...)`** — dựng lại từ DB.

### `Employees/Employee.cs`
`Employee : AggregateRoot<Guid>` — nhân viên. Có `UserId` (bắt buộc), `Position`, `HireDate`.
- **`Create(...)`** — chặn tên/chức vụ rỗng, chặn `UserId` rỗng (nhân viên phải có tài khoản).
- **`ChangePosition`**, **`Rehydrate`**.

### `Categories/Category.cs`
`Category : AggregateRoot<Guid>` — danh mục. `Name`, `Description`.
- **`Create`**, **`Update`**, **`Rehydrate`**.

### `Products/Product.cs`
`Product : AggregateRoot<Guid>` — sản phẩm, thuộc `CategoryId`.
- Thuộc tính: `Name, Sku, Color, Size, Price (Money), StockQuantity, CategoryId`.
- **`Create(...)`** — validate: giá > 0, kho ≥ 0, phải có category.
- **`ChangePrice(theNewPrice)`** — chặn giá ≤ 0.
- **`IncreaseStock(theQuantity)`** — chặn số ≤ 0.
- **`DecreaseStock(theQuantity)`** — 💡 chặn **bán quá tồn kho** (`theQuantity > StockQuantity` → ném lỗi).
  Đây là invariant quan trọng: kho không bao giờ âm.
- **`Rehydrate(...)`**.

### `Orders/` — aggregate phức tạp nhất

**`OrderStatus.cs`**: `Draft, Confirmed, Shipped, Cancelled`.

**`OrderConfirmedEvent.cs`** (`: IDomainEvent`): event phát khi đơn được xác nhận; mang `OrderId`,
`CustomerId`, `OccurredOnUtc`.

**`OrderDetail.cs`** (`: BaseEntity<Guid>`) — dòng hàng, là **entity con** của Order.
- 💡 Constructor và `Create`/`IncreaseQuantity`/`ChangeQuantity` đều `internal` → **chỉ Order trong
  cùng project mới tạo/sửa được**, bên ngoài không thể tạo `OrderDetail` rời.
- `ProductId` + `ProductName` + `UnitPrice` (Money) là **snapshot** lúc đặt. `LineTotal => UnitPrice.Multiply(Quantity)`.
- **`Rehydrate(...)`** là `public` vì tầng Infra (project khác) cần dựng lại nó khi đọc DB.

**`Order.cs`** (`: AggregateRoot<Guid>`) — đơn hàng.
- `myListDetails` (private list) — chỉ lộ ra `Details` dạng `IReadOnlyList`. 💡 Bên ngoài **không thể**
  `order.Details.Add(...)` để chèn dòng "chui" — phải đi qua method.
- `CustomerId`, `EmployeeId` (Guid — aggregate khác), `OrderStatus`, `OrderDate`, `ShippingAddress`, `TotalAmount`.
- **`Create(customerId, employeeId, shippingAddress)`** — tạo đơn rỗng trạng thái `Draft`.
- **`AddDetail(theProduct, theQuantity)`** — chỉ khi `Draft` (`EnsureDraft`); nếu sản phẩm đã có thì
  cộng dồn số lượng, chưa có thì tạo dòng mới (snapshot giá từ `theProduct.Price`); cuối cùng `Recalculate()`.
- **`ChangeDetailQuantity` / `RemoveDetail`** — chỉ khi `Draft`, sau đó tính lại tổng.
- **`Confirm()`** — 💡 chặn nếu **đơn rỗng** (`Details.Count == 0`); chuyển `Confirmed`; `Raise(OrderConfirmedEvent)`.
- **`MarkShipped()`** — chỉ từ `Confirmed`. **`Cancel()`** — chặn nếu đã `Shipped`.
- **`EnsureDraft()`** (private) — gom quy tắc "chỉ sửa khi nháp", ném lỗi nếu không.
- **`FindDetail(id)`** (private) — tìm dòng, không có thì ném lỗi.
- **`Recalculate()`** (private) — `TotalAmount` = tổng mọi `LineTotal` (bắt đầu từ `Money.Zero()`).
  💡 Tổng tiền **luôn được tính lại từ các dòng**, không cho set tay → không bao giờ lệch.
- **`Rehydrate(...)`** — dựng lại Order kèm danh sách `OrderDetail` đã dựng sẵn, không chạy lại business rule.

### `Payments/` — `PaymentMethod`, `PaymentStatus`, `Payment.cs`
- `PaymentMethod`: `Cash, Card, Transfer, Cod`. `PaymentStatus`: `Pending, Completed, Refunded`.
- 💡 `Payment` có **hai** trạng thái khác nhau: `Status` (EntityStatus — xóa mềm, từ BaseEntity) và
  `PaymentStatus` (nghiệp vụ thanh toán). Đừng nhầm.
- **`Create(orderId, amount, method)`** — chặn orderId rỗng, amount ≤ 0; khởi tạo `Pending`.
- **`MarkCompleted()`** — chỉ từ `Pending`; set `PaidAtUtc = UtcNow`, `Completed`.
- **`Refund()`** — chỉ từ `Completed`.
- **`Rehydrate(...)`**.

---

## D. Outbound ports — repository & UnitOfWork (`Domain/Interfaces/Outbound/`)

Theo CQRS, mỗi aggregate có **hai** repository tách biệt:
- `I*WriteRepository` — trả/nhận **aggregate** (vd `IOrderWriteRepository`: `GetByIdAsync`, `AddAsync`, `UpdateAsync`).
- `I*ReadRepository` — trả **read-model** `*View`/`PagedResult<T>` cho hiển thị (vd `IOrderReadRepository`).

Có base generic gọn: `IRepository`, `IReadRepository<TView>`, `IWriteRepository<TAggregate>`. Báo cáo
không gắn aggregate → chỉ có `IReportReadRepository`. Ngoài ra:
- **`IUnitOfWork`** — mở transaction gói nhiều repository write vào một commit (xem §G).
- **`IPasswordHasher`** — hợp đồng băm mật khẩu (API implement); để ở Outbound vì là dịch vụ hạ tầng Domain *cần*.

💡 **Điểm cốt lõi (Dependency Inversion):** mọi interface ở đây khai báo trong Domain nhưng **được hiện
thực ở Infrastructure** (repository) hoặc API (`IPasswordHasher`). Domain nói "tôi cần lưu/đọc Order" mà
không biết lưu vào SQL hay Mongo.

Một vài method truy vấn/nghiệp vụ đặc thù (trên các write repository):
- `ICategoryWriteRepository.ExistsByNameAsync` — rule "tên category không trùng".
- `IProductWriteRepository.ExistsBySkuAsync` — rule "SKU không trùng"; `GetByIdsAsync` (nạp nhiều product
  1 lần cho luồng đặt đơn), `AddRangeAsync`/`UpdateRangeAsync` (ghi hàng loạt).
- `IUserWriteRepository.GetByUsernameAsync` — tìm theo username; `IPaymentWriteRepository.GetByOrderAsync`
  — thanh toán của một đơn (chặn trả vượt nợ).

> Tham số đều có `CancellationToken theCancellationToken = default` để hủy tác vụ async khi cần.

---

## E. Inbound ports & Domain Service (`Domain/Interfaces/Inbound` + `Domain/Services`)

💡 **Logic ghi sống ở đây, không ở controller/handler.** Những gì cần phối hợp aggregate + repository
(đặc biệt các kiểm tra phải truy vấn DB) đặt trong Domain Service. Tầng Application (handler) chỉ gọi service.

📁 **Tổ chức theo loại + tách CQRS:** interface (inbound port) ở `Domain/Interfaces/Inbound/`:
`I*WriteService` (ghi) và `I*ReadService` (đọc) cho mỗi aggregate + `IReportReadService`, kèm base
`IService`/`IReadService`/`IWriteService`. Implementation ở `Domain/Services/` (`*WriteService` +
`*ReadService`, đều `internal sealed`). Folder theo aggregate chỉ chứa entity/enum/event/input record.

- Ví dụ `CategoryWriteService.CreateAsync`: tạo qua `Category.Create` (validate + trim), gọi
  `ExistsByNameAsync` chặn trùng tên (ném **`ConflictException`** → 409), rồi `AddAsync`. `CategoryReadService`
  lo `GetByIdAsync`/`SearchAsync`/`GetSummariesAsync`.
- `Product*/Order*/Payment*/Employee*Service`: kiểm entity liên quan tồn tại, ném `NotFoundException` → 404.
- **Nghiệp vụ phối hợp nhiều aggregate:**
  - `ProductWriteService.CreateAsync` chặn **SKU trùng** (`ConflictException`); `CreateManyAsync` tạo hàng loạt (AddRange).
  - `OrderWriteService.PlaceAsync` đặt cả đơn + item (Draft): kiểm product Active + đủ tồn kho. `ConfirmAsync`
    **trừ kho** từng dòng rồi ghi qua **`IUnitOfWork`** (1 transaction); `CancelAsync` **hoàn kho** nếu đơn đang Confirmed.
  - `PaymentWriteService.CreateAsync` chặn trả đơn chưa Confirmed/Shipped, sai tiền tệ, hoặc trả vượt số còn nợ.
  - 💡 Đây là chỗ "nghiệp vụ thật" sống — phối hợp Order ↔ Product, Payment ↔ Order. Stock dùng
    `Product.DecreaseStock/IncreaseStock` (invariant kho không âm nằm trong aggregate).
- `UserWriteService`: băm mật khẩu qua **`IPasswordHasher`** (interface ở `Interfaces/Outbound`, API implement)
  rồi `User.Create`. 💡 Domain định nghĩa hợp đồng, tầng ngoài cắm cách làm vào — vẫn đúng chiều phụ thuộc.
- Đăng ký: `Domain/DependencyInjection.cs` → `AddDomainServices()` (bind từng inbound port → `*Service`).
  Hàm này được `AddApplicationServices()` gọi. Nhờ vậy `*Service` để `internal` mà tầng ngoài vẫn dùng qua interface.

> Domain ref `Microsoft.Extensions.DependencyInjection.Abstractions` chỉ để có `IServiceCollection` cho
> `AddDomainServices()` — contract DI thuần, không phải hạ tầng. Domain **không** ref MediatR (chỉ Application có).

---

## F. Read side — read service/repository (`Domain/ReadModels`, `Domain/Queries`)

Method đọc (Search/Summary/history/báo cáo) nằm trên **`I*ReadService`**, delegate xuống **`I*ReadRepository`**
— repository read trả **read-model** (`*View` ở `Domain/ReadModels`; `PagedResult<T>` ở `Domain/Common`).
Input đọc gói trong record `*SearchQuery` (`Domain/Queries`). Báo cáo không gắn aggregate →
`IReportReadService` + `IReportReadRepository`. Đây là phần ĐỌC (JOIN/GROUP BY nhiều bảng), read-model
tách bạch với aggregate. Chi tiết cách thực thi (SQL JOIN vs Mongo stitch): [05-Application.md](05-Application.md) §E.

---

## G. Unit of Work (`Domain/Interfaces/Outbound/IUnitOfWork.cs`)

`DbContext.SaveChanges()` đã atomic nhưng chỉ trong một lần gọi. Khi một use-case ghi **≥2 aggregate qua
≥2 repository** (vd `ConfirmAsync`: trừ kho `Product` + đổi trạng thái `Order`), ta cần transaction bao
ngoài. `IUnitOfWork.BeginTransactionAsync()` trả một `IUnitOfWorkTransaction` (`CommitAsync`/`RollbackAsync`/
`DisposeAsync`). Service gọi nhiều repository rồi `CommitAsync` một lần.

💡 Implementation ở Infrastructure: `SqlServerUnitOfWork` (EF transaction) và `MongoUnitOfWork`
(**multi-document transaction** — cần MongoDB **replica set**, xem [03-Infrastructure-MongoDB.md](03-Infrastructure-MongoDB.md)
và ARCHITECTURE §11). Use-case ghi 1 aggregate (vd `ShipAsync`) thì không dùng UnitOfWork.
