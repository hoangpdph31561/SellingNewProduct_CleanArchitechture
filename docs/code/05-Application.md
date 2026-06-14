# 05 — Tầng Application (Read side / CQRS-lite)

Tầng này sinh ra để trả lời một nhu cầu rất thực tế: **hiển thị dữ liệu ghép từ nhiều bảng** —
ví dụ "đơn hàng này của khách *Nguyễn Văn A*, do nhân viên *Trần Thị B* bán", hay "khách này đã đặt
12 đơn, tổng 5.400.000đ". Repository (phía GHI) không hợp để làm việc này, nên ta tách **phía ĐỌC**.

> 💡 Đây là **CQRS-lite**: *Command* (ghi) và *Query* (đọc) đi hai đường khác nhau.
> Write đi qua aggregate; Read đi thẳng JOIN. Chúng không dùng chung một model.

---

## A. Vì sao cần tầng Application? (3 lựa chọn đã cân nhắc)

| Cách | Vấn đề |
|------|--------|
| Nhồi `CustomerName` vào aggregate `Order` | ❌ Vỡ ranh giới aggregate; khách đổi tên → dữ liệu trong Order lỗi thời |
| Để read-model trong project **API** | ❌ Infrastructure phải `return` read-model khi implement query, mà Infra **không ref API** (ngược chiều Dependency Rule) → không compile |
| Để read-model + query interface trong **Application** | ✅ Cả Infra lẫn API đều thấy; Domain vẫn sạch business |

**Chiều phụ thuộc:** `API → Infrastructure → Application → Domain`. Application chỉ ref Domain.

---

## B. Read-model — `Application/ReadModels/*.cs`

Các `record` phẳng, **chỉ để hiển thị** (không có method, không invariant). Khác `*Response` DTO ở API:
read-model là *kết quả truy vấn* (Infra tạo ra), DTO là *hợp đồng HTTP* (API tạo ra). Ở dự án này
controller trả thẳng read-model cho gọn — vì chúng đã đúng hình dạng cần hiển thị.

- `OrderDetailView` — 1 đơn + `CustomerName` + `EmployeeName` + danh sách `OrderLineView` + `AmountPaid`.
- `CustomerOrderHistoryView` — `TotalOrders`, `TotalSpent` + danh sách `CustomerOrderItemView` (kèm tên NV).
- `OrderSummaryView` — dòng tóm tắt cho màn hình danh sách/tìm kiếm.
- `ProductSummaryView` — dòng tóm tắt cho màn hình catalogue (kèm `CategoryName` qua JOIN).
- `BestSellingProductView`, `EmployeeSalesView` — kết quả báo cáo GROUP BY.

> 📄 Phân trang, tìm kiếm theo tên, lọc nhiều tiêu chí và sắp xếp được giải thích riêng ở
> [06-Pagination-Search.md](06-Pagination-Search.md).

---

## C. Query interface — `Application/Queries/*.cs`

- `IOrderQueries`: `GetOrderDetailAsync`, `GetCustomerHistoryAsync`, `SearchAsync` (lọc đa tham số + tìm theo tên + sort + phân trang).
- `IProductQueries`: `SearchAsync` cho catalogue (tên/loại hàng/khoảng giá/khoảng tồn kho/status + sort + phân trang).
- `IReportQueries`: `GetBestSellingProductsAsync` (phân trang), `GetEmployeeSalesLeaderboardAsync`.

💡 Tách khỏi `IOrderRepository`: repository = ghi (trả aggregate), queries = đọc (trả read-model).
Cùng một thực thể "Order" nhưng **hai mô hình khác nhau cho hai mục đích khác nhau**.

---

## D. Hai cách implement CÙNG một interface

Đây là phần thú vị nhất — chứng minh lại luận điểm "đổi DB, business/hợp đồng không đổi".

### SQL Server — JOIN thật (`Infrastructure.SqlServer/Queries/*.cs`)
LINQ `join ... on ... equals ...` được EF Core dịch thành **một câu SQL có JOIN**, chạy trên DB:

```csharp
from o in myAppDbContext.Orders.AsNoTracking()
join c in myAppDbContext.Customers on o.CustomerId equals c.Id
join e in myAppDbContext.Employees on o.EmployeeId equals e.Id
where o.Id == theOrderId
select new { ..., CustomerName = c.FullName, EmployeeName = e.FullName };
```
Báo cáo dùng `group ... by ... into g` → `SUM`/`COUNT` đẩy xuống database. Lấy đúng dữ liệu cần,
không kéo cả bảng về app. Lớp query nằm **trong** assembly SqlServer nên đọc được `internal DbSet`.

### MongoDB — ghép trong bộ nhớ (`Infrastructure.MongoDB/Queries/*.cs`)
Mongo **không có JOIN quan hệ** giữa các collection. Nên ta nạp document cần thiết rồi ghép bằng
LINQ-to-objects (hoặc, "đúng Mongo" hơn cho dữ liệu lớn: aggregation `$lookup`/`$group`, hoặc
denormalize sẵn tên vào document):

```csharp
var aOrder = await Orders.FirstOrDefaultAsync(o => o.Id == theOrderId && o.Status != DeletedStatus);
var aCustomer = await Customers.FirstOrDefaultAsync(c => c.Id == aOrder.CustomerId);
var aEmployee = await Employees.FirstOrDefaultAsync(e => e.Id == aOrder.EmployeeId);
// ... rồi tạo OrderDetailView. Details đã NHÚNG sẵn trong aOrder.Details.
```

> Lưu ý soft-delete: SQL có **Global Query Filter** (`Status != Deleted`) tự áp dụng; Mongo **không có**,
> nên mỗi query phải tự thêm điều kiện `Status != DeletedStatus`.

---

## E. Endpoint dùng read side

| Method | Trả về | Câu hỏi nó trả lời |
|--------|--------|---------------------|
| `GET /api/orders/{id}/view` | `OrderDetailView` | Đơn này của ai, ai bán, gồm món gì, đã trả bao nhiêu |
| `GET /api/orders?theCustomerName=&theStatus=&theSortBy=&thePage=…` | `PagedResult<OrderSummaryView>` | Danh sách/tìm đơn (kèm tên), có phân trang & sort |
| `GET /api/products/search?theName=&theCategoryId=&theSortBy=&thePage=…` | `PagedResult<ProductSummaryView>` | Tìm/lọc sản phẩm trong catalogue |
| `GET /api/customers/{id}/orders` | `CustomerOrderHistoryView` | Khách này đã đặt mấy đơn, là những đơn nào |
| `GET /api/reports/best-selling-products?thePage=&thePageSize=` | `PagedResult<BestSellingProductView>` | Sản phẩm nào bán chạy nhất |
| `GET /api/reports/employee-sales` | `EmployeeSalesView[]` | Nhân viên nào bán nhiều nhất |

> Chi tiết tham số lọc/sort/phân trang: xem [06-Pagination-Search.md](06-Pagination-Search.md).

---

## F. Nguyên tắc rút ra (ghi nhớ)

1. **Write đi qua aggregate (Repository), Read đi thẳng JOIN (Queries).**
2. Aggregate chỉ giữ id của aggregate khác — KHÔNG ôm tên/dữ liệu của nó.
3. Read-model thuộc tầng **Application**, không thuộc Domain (không phải business) cũng không thuộc API
   (Infra phải thấy được nó).
4. Đọc lẻ 1 bản ghi → có thể enrich đơn giản; **danh sách/thống kê → dùng query JOIN** để tránh N+1.
