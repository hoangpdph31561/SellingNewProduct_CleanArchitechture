# Mô hình nghiệp vụ (Domain Model)

Bối cảnh: cửa hàng **bán quần áo**. 8 bảng, nhóm theo aggregate.

## 1. Khối xây dựng DDD dùng trong dự án

| Khái niệm | Ý nghĩa | Ví dụ |
|-----------|---------|-------|
| **Entity** | Có định danh (Id), thay đổi theo thời gian | `Customer`, `Product`, `Order` |
| **Value Object** | Không Id, so sánh theo giá trị, bất biến | `Money`, `Email`, `Address`, `Sku` |
| **Aggregate Root** | Cổng vào duy nhất để thao tác một cụm entity | `Order` (chứa `OrderDetail`) |
| **Domain Event** | Việc nghiệp vụ đã xảy ra | `OrderConfirmedEvent` |
| **Repository (interface)** | Hợp đồng lưu/đọc aggregate (kể cả truy vấn báo cáo) | `IOrderRepository` |
| **Domain Exception** | Vi phạm quy tắc nghiệp vụ | `DomainException` |

> **Quy tắc aggregate quan trọng:** tham chiếu giữa các aggregate **CHỈ bằng Id**, không bằng
> object. Vd `Order.CustomerId` (Guid), KHÔNG phải `Order.Customer`. Mỗi aggregate có đúng một
> repository; entity con (như `OrderDetail`) không có repository riêng.

## 2. Base classes (Domain/Common)

```
BaseEntity<TId>
  - Id: TId
  - CreatedAtUtc: DateTime
  - UpdatedAtUtc: DateTime
  - Status: EntityStatus          // dùng cho xóa mềm
  + MarkUpdated()                 // cập nhật UpdatedAtUtc
  + Deactivate() / Activate()
  + Delete()                      // đặt Status = Deleted (XÓA MỀM, không xóa cứng)

AggregateRoot<TId> : BaseEntity<TId>
  - _domainEvents (private)
  + IReadOnlyList<IDomainEvent> DomainEvents
  + protected Raise(IDomainEvent)
  + ClearDomainEvents()

ValueObject                       // equality theo GetEqualityComponents()
IDomainEvent                      // marker
DomainException : Exception       // ném khi vi phạm invariant
```

### EntityStatus (enum) — xóa mềm
```
enum EntityStatus { Active = 1, Inactive = 2, Deleted = 3 }
```
- **Không bao giờ xóa cứng (DELETE).** Gọi `entity.Delete()` → `Status = Deleted`.
- Infrastructure dùng **EF Core Global Query Filter** để tự ẩn record `Deleted` khi đọc:
  `HasQueryFilter(e => e.Status != EntityStatus.Deleted)`.
- → Domain chỉ biết "có trạng thái"; việc lọc khi query là của Infra (tách tầng đúng).

## 3. Value Objects
- **Money**: `Amount` (decimal ≥ 0), `Currency` (mặc định "VND"). Hành vi `Add`, `Multiply(qty)`.
- **Email**: bọc chuỗi email hợp lệ, validate trong constructor.
- **Address**: `Street`, `Ward`, `District`, `City`, `Country`. Bất biến.
- **Sku**: mã sản phẩm theo quy ước (vd `TSHIRT-RED-M`).
- **Size**: `enum Size { S, M, L, XL, XXL }`.

## 4. Tám aggregate / bảng

| # | Bảng | Root? | Trường chính | Tham chiếu (bằng Id) |
|---|------|-------|--------------|----------------------|
| 1 | **User** | ✔ | Username, PasswordHash, Email, Role | — |
| 2 | **Customer** | ✔ | FullName, Email, Phone, DefaultAddress | UserId (tùy chọn) |
| 3 | **Employee** | ✔ | FullName, Position, HireDate | UserId |
| 4 | **Category** | ✔ | Name, Description | — |
| 5 | **Product** | ✔ | Name, Sku, Color, Size, Price, StockQuantity | CategoryId |
| 6 | **Order** | ✔ | Status, OrderDate, ShippingAddress, TotalAmount | CustomerId, EmployeeId |
| 7 | **OrderDetail** | ✱ con của Order | ProductId, ProductName, UnitPrice, Quantity, LineTotal | ProductId |
| 8 | **Payment** | ✔ | Amount, Method, PaidAtUtc, Status | OrderId |

> `Role` để **enum** `UserRole { Admin, Sales, Manager }` trong User cho gọn.
> `PaymentMethod { Cash, Card, Transfer, COD }`.

## 5. Business rules chính

### Product
- `Create(name, sku, color, size, price, stock)` — factory, validate.
- `ChangePrice(Money)` — giá > 0. `IncreaseStock(int)` / `DecreaseStock(int)` — kho không âm.
- Invariant: `StockQuantity >= 0`, `Price.Amount > 0`.

### Customer / Employee / User
- `Create(...)`, `ChangeEmail`, `UpdateAddress`, `Rename`...; User: `ChangePassword`, `ChangeRole`.

### Order (aggregate root — quan trọng nhất)
```
Order : AggregateRoot<Guid>
  - CustomerId, EmployeeId: Guid
  - Status: OrderStatus { Draft, Confirmed, Shipped, Cancelled }
  - Details: IReadOnlyList<OrderDetail>   // field private readonly List
  - ShippingAddress: Address
  - OrderDate: DateTime
  - TotalAmount: Money                    // = tổng LineTotal

OrderDetail : BaseEntity<Guid>   (con, KHÔNG có repository riêng)
  - ProductId, ProductName (snapshot), UnitPrice (snapshot Money), Quantity, LineTotal
```

| Hành vi | Quy tắc (invariant) |
|---------|---------------------|
| `Create(customerId, employeeId, shippingAddress)` | Tạo order rỗng trạng thái `Draft` |
| `AddDetail(product, quantity)` | Chỉ khi `Draft`; qty > 0; trùng product thì cộng dồn; snapshot giá+tên |
| `RemoveDetail(detailId)` / `ChangeQuantity(...)` | Chỉ khi `Draft`; qty > 0 |
| `Confirm()` | Chỉ khi `Draft` **và** có ≥ 1 detail; → `Confirmed`; raise `OrderConfirmedEvent` |
| `Cancel()` | Không cho huỷ khi đã `Shipped` |
| `MarkShipped()` | Chỉ khi `Confirmed` |
| `TotalAmount` | Luôn tính lại = tổng `LineTotal` khi đổi details |

> Vi phạm → `DomainException`: `Confirm()` đơn rỗng, `AddDetail` khi đã `Confirmed`...

### Snapshot giá — vì sao OrderDetail giữ UnitPrice riêng?
Giá sản phẩm đổi sau khi khách đặt. Đơn phải giữ giá **tại thời điểm đặt** → `OrderDetail`
copy `UnitPrice`/`ProductName` thay vì tham chiếu `Product` hiện tại (chống rò rỉ giữa aggregate).

## 6. Repository interfaces (Domain/Repositories)

Mỗi aggregate root một repository. Read side **đã gộp vào repository**: ngoài các method ghi (trả
aggregate), repository còn có method đọc trả **read-model** (`*View`, `PagedResult<T>`) cho danh sách/
báo cáo. Riêng báo cáo xuyên nhiều bảng có `IReportRepository` (không gắn aggregate). (Trước đây từng
tách cổng `I*Queries` riêng — nay gộp lại để mỗi module một cổng `I*Service`→`I*Repository`.)

```csharp
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);
    Task<IReadOnlyList<Order>> GetByDateRangeAsync(DateTime theFrom, DateTime theTo, CancellationToken theCancellationToken = default); // báo cáo
    Task AddAsync(Order theOrder, CancellationToken theCancellationToken = default);
    Task UpdateAsync(Order theOrder, CancellationToken theCancellationToken = default);
}
```
(Tương tự: `IUserRepository`, `ICustomerRepository`, `IEmployeeRepository`,
`ICategoryRepository`, `IProductRepository`, `IPaymentRepository`.)

> `Order` luôn được nạp đầy đủ kèm `Details` (load cả cụm aggregate) cho phần ghi; phần đọc/báo cáo
> dùng read-model phẳng (JOIN/GROUP BY) trả về trực tiếp, không nạp aggregate.

## 7. Mapping Domain ↔ Persistence (map tay ở Infrastructure)

- `ToRecord(Order)` / `ToDocument(Order)` — Domain → persistence model.
- `ToDomain(OrderRecord)` / `ToDomain(OrderDocument)` — persistence → Domain qua
  `Order.Rehydrate(...)` (internal factory dựng lại state, KHÔNG chạy lại business rule).
- SQL: `Order` + `OrderDetail` thành 2 bảng (FK). Mongo: `OrderDocument` nhúng `Details[]`.
