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

## D. Repository interfaces (`Domain/Repositories/`)

7 interface, mỗi aggregate root một cái: `IUserRepository`, `ICustomerRepository`,
`IEmployeeRepository`, `ICategoryRepository`, `IProductRepository`, `IOrderRepository`, `IPaymentRepository`.

💡 **Điểm cốt lõi (Dependency Inversion):** interface khai báo Ở ĐÂY (Domain) nhưng **được hiện thực ở
Infrastructure**. Domain nói "tôi cần lưu/đọc Order" mà không biết lưu vào SQL hay Mongo.

Mẫu chung mỗi interface: `GetByIdAsync`, `GetAllAsync` (hoặc query riêng), `AddAsync`, `UpdateAsync`.

Một vài method truy vấn/nghiệp vụ đặc thù:
- `ICategoryRepository.ExistsByNameAsync` — phục vụ rule "tên category không trùng" (Domain Service gọi).
- `IUserRepository.GetByUsernameAsync` — tìm theo username (đăng nhập).
- `IProductRepository.GetByCategoryAsync` — sản phẩm theo danh mục.
- `IOrderRepository.GetByCustomerAsync`, `GetByDateRangeAsync` — phục vụ tra cứu/báo cáo đơn giản.
- `IPaymentRepository.GetByOrderAsync` — thanh toán của một đơn.
- `ICustomerRepository.DeleteAsync` — xóa mềm khách theo Id.

> Tham số đều có `CancellationToken theCancellationToken = default` để hủy tác vụ async khi cần.

---

## E. Domain Service (`Domain/Abstractions` + `Domain/Services`)

💡 **Logic ghi sống ở đây, không ở controller.** Những gì cần phối hợp aggregate + repository (đặc biệt
các kiểm tra phải truy vấn DB) đặt trong Domain Service. Controller chỉ gọi service.

📁 **Tổ chức theo loại** (không nhét vào folder aggregate): interface ở `Domain/Abstractions/`
(I*Service + I*Queries + IPasswordHasher), implementation ở `Domain/Services/`. Folder theo aggregate
(`Categories/`, `Orders/`…) giờ chỉ chứa entity/enum/event.

- Mỗi module một cặp: `ICategoryService` (public, `Abstractions/` — API thấy) + `CategoryService` (`internal sealed`, `Services/` — chứa logic).
- Ví dụ `CategoryService.CreateAsync`: tạo qua `Category.Create` (validate + trim), gọi `ExistsByNameAsync`
  để chặn trùng tên (ném `DomainException`), rồi `AddAsync`. Trả `Category` cho API.
- `ProductService`/`OrderService`/`PaymentService`/`EmployeeService`: kiểm entity liên quan tồn tại,
  ném `NotFoundException` nếu thiếu → API trả 404.
- `UserService`: băm mật khẩu qua **`IPasswordHasher`** (interface khai báo ở `Domain/Users`, API implement)
  rồi `User.Create`. 💡 Domain định nghĩa hợp đồng, tầng ngoài cắm cách làm vào — vẫn đúng chiều phụ thuộc.
- Đăng ký: `Domain/DependencyInjection.cs` → `AddDomainServices()`; `Program.cs` gọi nó. Nhờ vậy `*Service`
  để `internal` mà API vẫn dùng được qua interface.

> Domain ref `Microsoft.Extensions.DependencyInjection.Abstractions` chỉ để có `IServiceCollection` cho
> `AddDomainServices()` — đây là contract DI thuần, không phải hạ tầng, nên Domain vẫn "sạch".

---

## F. Read side trong Domain (`Domain/Abstractions`, `Domain/ReadModels`)

Vì dự án chỉ còn **3 tầng** (đã bỏ Application), interface query (`IProductQueries`, `IOrderQueries`,
`IReportQueries` — nằm chung trong `Domain/Abstractions`) và read-model (`*View` ở `Domain/ReadModels`)
nằm trong Domain; Infrastructure implement. Đây là phần ĐỌC (JOIN/GROUP BY nhiều bảng), tách bạch với
write side. Chi tiết: [05-Application.md](05-Application.md).
