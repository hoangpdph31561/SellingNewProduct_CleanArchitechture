# 05 — Read side (CQRS-lite) trong Domain

> ⚠️ **Cập nhật kiến trúc:** dự án đã gộp về **3 tầng** (API · Domain · Infrastructure) — KHÔNG còn
> project `Application`. Toàn bộ read side đã chuyển vào **Domain**. Nơi đặt code hiện tại:
> - **Query interface** (`I*Queries`) → `Domain/Abstractions/`
> - **Query input object** (`*SearchQuery`) → `Domain/Queries/`
> - **Read-model output** (`*View`, `PagedResult`) → `Domain/ReadModels/`, `Domain/Common/`
> - **Cách thực thi** → `Infrastructure.SqlServer|MongoDB/Queries/`
>
> (Tên file vẫn là `05-Application.md` cho khỏi vỡ link; nội dung là "read side trong Domain".)

Read side sinh ra để trả lời nhu cầu rất thực tế: **hiển thị dữ liệu ghép từ nhiều bảng** — ví dụ
"đơn hàng này của khách *Nguyễn Văn A*, do nhân viên *Trần Thị B* bán", hay "khách này đã đặt 12 đơn,
tổng 5.400.000đ". Repository (phía GHI) không hợp để làm việc này, nên ta tách **phía ĐỌC**.

> 💡 Đây là **CQRS-lite**: *Command* (ghi) và *Query* (đọc) đi hai đường khác nhau.
> Write đi qua aggregate (+ Domain Service); Read đi thẳng JOIN. Chúng không dùng chung một model.

---

## A. Read-model đặt ở đâu? (3 lựa chọn đã cân nhắc)

| Cách | Đánh giá |
|------|--------|
| Nhồi `CustomerName` vào aggregate `Order` | ❌ Vỡ ranh giới aggregate; khách đổi tên → dữ liệu trong Order lỗi thời |
| Để read-model trong project **API** | ❌ Infrastructure phải `return` read-model khi implement query, mà Infra **không ref API** (ngược chiều Dependency Rule) → không compile |
| Để read-model + query interface trong **Domain** | ✅ Cả Infra lẫn API đều thấy (đều ref Domain); giữ đúng 3 tầng |

**Chiều phụ thuộc:** `API → Infrastructure → Domain`. Đánh đổi khi bỏ Application: Domain "rộng" hơn
(ôm cả read-model hiển thị). Bù lại chỉ còn 3 tầng, đơn giản hơn cho mục tiêu học. Read-model vẫn là
kiểu chỉ-đọc, tách bạch hoàn toàn với aggregate nghiệp vụ.

---

## B. Read-model — `Domain/ReadModels/*.cs`

Các `record` phẳng, **chỉ để hiển thị** (không có method, không invariant). Khác `*Response` DTO ở API:
read-model là *kết quả truy vấn* (Infra tạo ra), DTO là *hợp đồng HTTP* (API tạo ra). Ở dự án này
controller trả thẳng read-model cho gọn — vì chúng đã đúng hình dạng cần hiển thị.

- `OrderDetailView` — 1 đơn + `CustomerName` + `EmployeeName` + danh sách `OrderLineView` + `AmountPaid`.
- `CustomerOrderHistoryView` — `TotalOrders`, `TotalSpent` + danh sách `CustomerOrderItemView` (kèm tên NV).
- `OrderSummaryView`, `OrderStatusCountView` — dòng tóm tắt danh sách / thống kê theo trạng thái.
- `ProductSummaryView` — dòng tóm tắt catalogue (kèm `CategoryName` qua JOIN).
- `CustomerSummaryView`, `TopCustomerView` — danh sách khách / xếp hạng chi tiêu.
- `EmployeeSummaryView` — danh sách nhân viên (kèm số đơn đã bán).
- `CategorySummaryView` — danh mục kèm số sản phẩm + giá trị tồn.
- `PaymentSummaryView`, `OutstandingOrderView` — danh sách thanh toán / đơn còn nợ.
- Báo cáo (GROUP BY): `BestSellingProductView`, `EmployeeSalesView`, `CategorySalesView`, `DailySalesView`, `LowStockProductView`.

> 📄 Phân trang, tìm kiếm theo tên, lọc nhiều tiêu chí và sắp xếp được giải thích riêng ở
> [06-Pagination-Search.md](06-Pagination-Search.md).

---

## C. Query interface — `Domain/Abstractions/*Queries.cs` (input ở `Domain/Queries/`)

Read side nay phủ **7 nhóm query** (interface ở `Domain/Abstractions`, input gói trong `*SearchQuery` ở `Domain/Queries`):

- `IProductQueries`: `GetByIdAsync`, `SearchAsync(ProductSearchQuery)`.
- `IOrderQueries`: `GetOrderDetailAsync`, `GetCustomerHistoryAsync`, `SearchAsync(OrderSearchQuery)`, `GetStatusBreakdownAsync`.
- `ICustomerQueries`: `GetByIdAsync`, `SearchAsync(CustomerSearchQuery)`, `GetTopCustomersAsync`.
- `IEmployeeQueries`: `GetByIdAsync`, `SearchAsync(EmployeeSearchQuery)` (kèm số đơn đã bán).
- `ICategoryQueries`: `GetCategorySummariesAsync`, `SearchAsync(CategorySearchQuery)`.
- `IPaymentQueries`: `SearchAsync(PaymentSearchQuery)`, `GetOutstandingOrdersAsync`.
- `IReportQueries`: `GetBestSellingProductsAsync`, `GetEmployeeSalesLeaderboardAsync`, `GetSalesByCategoryAsync`, `GetDailySalesAsync`, `GetLowStockProductsAsync`.

💡 Tách khỏi `I*Repository`: repository = ghi (trả aggregate), queries = đọc (trả read-model).
Cùng một thực thể nhưng **hai mô hình cho hai mục đích**. Cả hai interface đều ở Domain; Infrastructure implement.

💡 **Input gói thành object:** `SearchAsync` nhận **một** `*SearchQuery` (record) thay vì danh sách tham số
dài; phía ghi cũng vậy với `Create*Command`. Chi tiết cơ chế + binding `[FromQuery]` xem
[04-API.md](04-API.md) mục C và [06-Pagination-Search.md](06-Pagination-Search.md).

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
| `GET /api/orders?CustomerName=&Status=&SortBy=&Page=…` | `PagedResult<OrderSummaryView>` | Danh sách/tìm đơn (kèm tên), phân trang & sort |
| `GET /api/orders/status-breakdown` | `OrderStatusCountView[]` | Mỗi trạng thái có bao nhiêu đơn, tổng tiền |
| `GET /api/products/search?Name=&CategoryId=&SortBy=&Page=…` | `PagedResult<ProductSummaryView>` | Tìm/lọc sản phẩm trong catalogue |
| `GET /api/products/{id}/summary` | `ProductSummaryView` | 1 sản phẩm kèm tên loại hàng |
| `GET /api/customers/search?Name=&City=&Page=…` | `PagedResult<CustomerSummaryView>` | Tìm/lọc khách |
| `GET /api/customers/top` | `PagedResult<TopCustomerView>` | Khách chi tiêu nhiều nhất |
| `GET /api/customers/{id}/orders` | `CustomerOrderHistoryView` | Khách này đã đặt mấy đơn, là những đơn nào |
| `GET /api/employees/search?Name=&Position=&Page=…` | `PagedResult<EmployeeSummaryView>` | Tìm/lọc nhân viên (kèm số đơn) |
| `GET /api/categories/summaries` · `…/search` | `CategorySummaryView[]` · `PagedResult<…>` | Danh mục kèm số SP + giá trị tồn |
| `GET /api/payments/search` · `…/outstanding-orders` | `PagedResult<PaymentSummaryView>` · `PagedResult<OutstandingOrderView>` | Tìm thanh toán / đơn còn nợ |
| `GET /api/reports/best-selling-products` · `employee-sales` · `sales-by-category` · `daily-sales` · `low-stock-products` | các `*View` / `PagedResult<…>` | Báo cáo GROUP BY |

> Controller chỉ inject `I*Queries` cho các endpoint đọc này (đứng cạnh `I*Service` cho phần ghi).
> Chi tiết tham số lọc/sort/phân trang: xem [06-Pagination-Search.md](06-Pagination-Search.md).

---

## F. Nguyên tắc rút ra (ghi nhớ)

1. **Write đi qua aggregate (Domain Service + Repository), Read đi thẳng JOIN (Queries).**
2. Aggregate chỉ giữ id của aggregate khác — KHÔNG ôm tên/dữ liệu của nó.
3. Read-model + query interface nằm trong **Domain** (vì chỉ còn 3 tầng), nhưng vẫn tách bạch với
   aggregate: read-model chỉ để đọc, không phải business rule.
4. Đọc lẻ 1 bản ghi → có thể enrich đơn giản; **danh sách/thống kê → dùng query JOIN** để tránh N+1.
