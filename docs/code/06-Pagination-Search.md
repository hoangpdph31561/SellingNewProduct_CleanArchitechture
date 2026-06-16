# 06 — Phân trang, Tìm kiếm, Lọc & Sắp xếp (Read side)

Tài liệu này nối tiếp [05-Application.md](05-Application.md). Nó giải thích cách dự án thêm
**phân trang (pagination)**, **tìm kiếm theo tên**, **lọc nhiều tiêu chí** và **sắp xếp (sort)**
vào phía ĐỌC — và vì sao mỗi tính năng lại đặt ở đúng tầng của nó.

> 💡 Luận điểm xuyên suốt vẫn không đổi: **một hợp đồng (interface) duy nhất, hai cách thực thi**
> (SQL đẩy xuống DB; Mongo làm phần còn lại trong bộ nhớ). Phân trang/lọc/sort là *cách đọc* (read side).
>
> ⚠️ **Cập nhật:** dự án đã bỏ tầng Application → hợp đồng read side (`PagedResult<T>`, `PageRequest`,
> `I*Queries`, read-model) nay nằm trong **Domain**; cách thực thi vẫn ở **Infrastructure**. Đọc các
> đường dẫn "Application/..." bên dưới như "Domain/..." (`Domain/Common`, `Domain/Queries`, `Domain/ReadModels`).

---

## A. Vấn đề: trả về cả bảng là một cái bẫy

Trước đây `SearchAsync` trả `IReadOnlyList<OrderSummaryView>` — **toàn bộ** dòng khớp. Với 10 đơn
thì ổn; với 100.000 đơn thì:

- Tốn RAM (nạp hết về app), tốn băng thông (serialize hết ra JSON).
- Màn hình danh sách chỉ hiển thị ~20 dòng/trang → 99% dữ liệu tải về là lãng phí.
- Không có cách nào nói cho UI biết "có tổng cộng bao nhiêu dòng" để vẽ nút *Trang sau*.

→ Giải pháp: chỉ lấy **một trang**, kèm **tổng số dòng**. Đó là `PagedResult<T>`.

---

## B. `PagedResult<T>` và `PageRequest` — `Domain/Common/PagedResult.cs`

Hai kiểu nhỏ, đặt ở **Domain/Common** vì cả Infra (tạo ra) lẫn API (trả về) đều ref Domain.

### `PagedResult<T>` — kết quả một trang + metadata

```csharp
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,   // các dòng của TRANG hiện tại
    int Page,                 // số trang hiện tại (1-based)
    int PageSize,             // số dòng mỗi trang
    int TotalCount)           // TỔNG số dòng khớp (mọi trang)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
```

> 💡 **Vì sao cần `TotalCount`?** Một `List` không thể nói "có 4.380 đơn khớp, bạn đang xem dòng 21–40".
> Có `TotalCount` thì UI tính được `TotalPages`, bật/tắt nút *Next*, hiện "Trang 2 / 219".

### `PageRequest` — chuẩn hoá & kẹp (clamp) input ở MỘT chỗ

```csharp
public readonly record struct PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    public PageRequest(int thePage, int thePageSize)
    {
        Page = thePage < 1 ? 1 : thePage;                       // không cho trang 0 / âm
        PageSize = thePageSize < 1 ? DefaultPageSize            // 0 → mặc định
                 : thePageSize > MaxPageSize ? MaxPageSize       // chặn xin 1.000.000 dòng
                 : thePageSize;
    }

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (Page - 1) * PageSize;                   // số dòng cần bỏ qua
}
```

> 💡 **Vì sao gom luật vào một struct?** Có **4 implementation** (SQL/Mongo × Order/Product) đều cần
> cùng quy tắc "page ≥ 1, pageSize 1..200". Nếu copy-paste 4 lần, sửa luật sẽ sót. Một `PageRequest`
> = một nguồn sự thật, đồng thời chặn người dùng gửi tham số nguy hiểm (DoS bằng `pageSize=999999999`).

---

## C. Công thức phân trang giống nhau ở mọi query

Dù SQL hay Mongo, mọi `SearchAsync` đều theo đúng 3 nhịp:

```
1) COUNT  → đếm tổng số dòng KHỚP BỘ LỌC (để có TotalCount)
2) SKIP/TAKE → lấy đúng 1 trang  (Skip = (Page-1)*PageSize, Take = PageSize)
3) Bọc lại thành PagedResult<T>
```

**Thứ tự bắt buộc:** lọc (WHERE) → đếm (COUNT) → sắp xếp (ORDER BY) → phân trang (SKIP/TAKE).
Sai thứ tự (vd Take trước Where) sẽ ra kết quả sai.

---

## D. Gói tham số vào Query object + Tìm kiếm/Lọc

### Tất cả filter gói trong MỘT `*SearchQuery` (record)

`SearchAsync` **không** nhận danh sách tham số dài; nó nhận **một** object input (đặt ở `Domain/Queries/`):

```csharp
public sealed record ProductSearchQuery
{
    public string? Name { get; init; }
    public Guid? CategoryId { get; init; }
    public decimal? PriceFrom { get; init; }
    public decimal? PriceTo { get; init; }
    public int? MinStock { get; init; }
    public int? MaxStock { get; init; }
    public EntityStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}

Task<PagedResult<ProductSummaryView>> SearchAsync(ProductSearchQuery theQuery, CancellationToken ct = default);
```

Controller bind thẳng từ query-string (không cần liệt kê từng `[FromQuery]`):

```csharp
[HttpGet("search")]
public async Task<ActionResult<PagedResult<ProductSummaryView>>> Search(
    [FromQuery] ProductSearchQuery theQuery, CancellationToken ct)
    => Ok(await myProductQueries.SearchAsync(theQuery, ct));
```

> 💡 ASP.NET bind theo **tên property** (PascalCase) → query-string là `?Name=ao&Page=2&SortBy=price`.
> ⚠️ **Breaking change** so với bản cũ: tên tham số URL đổi `theName→Name`, `thePage→Page`, `theSortBy→SortBy`...
> 💡 Lợi: thêm filter mới chỉ sửa record, **không đổi chữ ký** port; an toàn compile-time.

### Quy ước "filter tuỳ chọn": `null = không lọc`

Mọi property lọc đều nullable. Code chỉ thêm `.Where(...)` khi nó có giá trị:

```csharp
if (theQuery.CategoryId is not null)              // chỉ lọc khi người dùng truyền
    aQuery = aQuery.Where(x => x.p.CategoryId == theQuery.CategoryId);

if (theQuery.PriceFrom is not null)               // khoảng giá: cận dưới
    aQuery = aQuery.Where(x => x.p.PriceAmount >= theQuery.PriceFrom);

if (theQuery.MaxStock is not null)                // khoảng tồn kho: cận trên (vd hàng sắp hết)
    aQuery = aQuery.Where(x => x.p.StockQuantity <= theQuery.MaxStock);
```

→ `aQuery` được **bồi (compose)** dần. Vì là `IQueryable`, **chưa chạy** DB cho tới khi gọi
`CountAsync`/`ToListAsync`. EF gộp tất cả `.Where` thành **một** mệnh đề `WHERE` duy nhất.

### Tìm theo tên: `Contains`

```csharp
if (!string.IsNullOrWhiteSpace(theQuery.Name))
{
    var aName = theQuery.Name.Trim();
    aQuery = aQuery.Where(x => x.p.Name.Contains(aName));   // SQL → LIKE '%aName%'
}
```

### Lọc Product (catalogue) — `IProductQueries.SearchAsync(ProductSearchQuery)`

| Property | Ý nghĩa | Dịch ra SQL |
|---------|---------|-------------|
| `Name` | tên chứa chuỗi | `Name LIKE '%..%'` |
| `CategoryId` | loại hàng | `CategoryId = @id` |
| `PriceFrom` / `PriceTo` | khoảng giá | `PriceAmount BETWEEN` |
| `MinStock` / `MaxStock` | khoảng tồn kho | `StockQuantity BETWEEN` |
| `Status` | Active/Inactive | `Status = @s` (đã xoá mềm luôn bị loại) |

### Các Search khác (cùng khuôn)

Mỗi nhóm có `*SearchQuery` riêng: `OrderSearchQuery` (CustomerId/EmployeeId, CustomerName/EmployeeName contains,
Status, FromUtc/ToUtc), `CustomerSearchQuery` (Name/Email/PhoneNumber/City, Status), `EmployeeSearchQuery`
(Name/Position, Status), `PaymentSearchQuery` (OrderId, Method, Status, FromUtc/ToUtc), `CategorySearchQuery` (Name).

---

## E. Sắp xếp (sort) — cái "query người dùng truyền vào" cần được xử lý

Phân trang ban đầu chỉ xử lý *số trang*; còn khi người dùng muốn "sắp theo giá giảm dần" thì sao?
Ta nhận **tên cột** (`theQuery.SortBy`) + **chiều** (`theQuery.SortDescending`).

> ⚠️ **Tuyệt đối không** dịch thẳng chuỗi người dùng thành cột/biểu thức (nguy cơ lỗi & injection).
> Ta **whitelist**: chỉ chấp nhận một số cột đã biết, còn lại rơi về mặc định an toàn.

```csharp
aQuery = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
{
    "price" => theQuery.SortDescending ? aQuery.OrderByDescending(x => x.p.PriceAmount)
                                       : aQuery.OrderBy(x => x.p.PriceAmount),
    "stock" => theQuery.SortDescending ? aQuery.OrderByDescending(x => x.p.StockQuantity)
                                       : aQuery.OrderBy(x => x.p.StockQuantity),
    _       => theQuery.SortDescending ? aQuery.OrderByDescending(x => x.p.Name)   // mặc định: name
                                       : aQuery.OrderBy(x => x.p.Name)
};
```

- Product: `name` (mặc định), `price`, `stock`.
- Order: `orderDate` (mặc định, mới nhất trước), `totalAmount`, `customerName`, `employeeName`.

---

## F. SQL vs Mongo — CÙNG hợp đồng, KHÁC cách chạy

Đây là phần học cốt lõi. Cùng `IProductQueries`/`IOrderQueries`, nhưng:

### SQL Server — đẩy HẾT xuống database
`WHERE` + `ORDER BY` + `COUNT(*)` + `OFFSET/FETCH` đều do EF Core dịch và chạy trên DB. App chỉ nhận
về đúng 1 trang. Tên khách/loại hàng lấy bằng **JOIN** ngay trong câu lệnh.

```csharp
var aTotalCount = await aQuery.CountAsync(ct);          // SELECT COUNT(*) ...
var aRows = await aQuery
    .Skip(aPage.Skip).Take(aPage.PageSize)              // OFFSET .. FETCH NEXT ..
    .Select(...).ToListAsync(ct);
```

### MongoDB — đẩy phần làm được, còn lại làm trong bộ nhớ
Mongo **không JOIN** giữa collection và **dịch text hạn chế**. Chiến lược:

**Product:** các so sánh field (category, khoảng giá, khoảng tồn, status) đẩy xuống DB; còn
`name`-contains + sort + phân trang làm **in-memory**; cuối cùng mới ghép `CategoryName` cho riêng
trang đó. Đây đúng là "tradeoff Mongo" đã nêu ở 05.

**Order — mẹo hay khi lọc theo tên mà không có JOIN:** muốn lọc theo *tên khách* nhưng tên nằm ở
collection khác → **tìm ID trước, rồi lọc theo ID**:

```csharp
// 1. Tìm các customer có tên khớp → danh sách Id
var aCustomerIdFilter = await ResolveCustomerIdsByNameAsync(theQuery.CustomerName, ct);
// 2. Lọc orders theo tập Id đó ($in) — paging vẫn chạy ở DB
if (aCustomerIdFilter is not null)
    aQuery = aQuery.Where(o => aCustomerIdFilter.Contains(o.CustomerId));
```

> 💡 Nhờ vậy `Skip/Take/Count` của Order **vẫn chạy trên DB** dù điều kiện là "tên khách".

**Giới hạn được ghi nhận có chủ đích:** Mongo chỉ **sort theo field của chính Order** (orderDate,
totalAmount) ở DB. Sort theo `customerName`/`employeeName` (dữ liệu ghép từ collection khác) sẽ phải
nạp toàn bộ rồi mới sắp → ta **fallback** về `orderDate desc`. SQL thì sort được mọi cột.
**Đây không phải bug — đây là minh hoạ:** cùng một contract, khả năng thực thi *hiệu quả* phụ thuộc
vào việc kho lưu trữ có JOIN hay không.

| Khía cạnh | SQL Server | MongoDB |
|-----------|-----------|---------|
| Lọc field | `WHERE` ở DB | `Where` ở DB |
| Tìm theo tên | LIKE (JOIN sẵn) | resolve tên→Id rồi `$in`; hoặc `Contains` in-memory (Product) |
| COUNT / SKIP / TAKE | ở DB (OFFSET/FETCH) | ở DB (Order) / in-memory (Product) |
| Sort | mọi cột ở DB | field riêng ở DB; cột ghép → fallback |
| Lấy tên hiển thị | JOIN | nạp collection liên quan rồi ghép |

---

## G. Endpoint

| Method | Tham số chính (PascalCase) | Trả về |
|--------|---------------|--------|
| `GET /api/products/search` | `Name, CategoryId, PriceFrom/To, MinStock/MaxStock, Status, SortBy, SortDescending, Page, PageSize` | `PagedResult<ProductSummaryView>` |
| `GET /api/orders` | `CustomerId/Name, EmployeeId/Name, Status, FromUtc/ToUtc, SortBy, SortDescending, Page, PageSize` | `PagedResult<OrderSummaryView>` |
| `GET /api/customers/search` | `Name, Email, PhoneNumber, City, Status, SortBy, SortDescending, Page, PageSize` | `PagedResult<CustomerSummaryView>` |
| `GET /api/employees/search` | `Name, Position, Status, SortBy, SortDescending, Page, PageSize` | `PagedResult<EmployeeSummaryView>` |
| `GET /api/payments/search` | `OrderId, Method, Status, FromUtc/ToUtc, SortBy, SortDescending, Page, PageSize` | `PagedResult<PaymentSummaryView>` |
| `GET /api/categories/search` | `Name, SortDescending, Page, PageSize` | `PagedResult<CategorySummaryView>` |
| `GET /api/reports/best-selling-products`, `low-stock-products` | `Page, PageSize` (+ `Threshold`) | `PagedResult<…>` |

Ví dụ:
```
GET /api/products/search?Name=áo&PriceFrom=100000&MaxStock=5&SortBy=price&SortDescending=true&Page=1&PageSize=20
GET /api/orders?CustomerName=an&Status=Confirmed&SortBy=totalAmount&SortDescending=true&Page=2
```

> ⚠️ **Breaking change về hợp đồng HTTP (2 điểm):**
> 1. Tên tham số query-string đổi sang **PascalCase** (`theName→Name`, `thePage→Page`...) do bind từ `*SearchQuery`.
> 2. Toàn bộ response nay bọc trong **envelope `ApiResponse`** (xem [04-API.md](04-API.md) mục E): dữ liệu trang
>    nằm ở `result` → client đọc `response.result.items`, `response.result.totalCount`...

---

## H. Nguyên tắc rút ra (ghi nhớ)

1. **Phân trang/lọc/sort là *cách đọc*, không phải business** → hợp đồng ở Domain (read side), cách
   thực thi ở Infrastructure. (Trước đây tách ở Application; nay gộp Domain do dự án còn 3 tầng.)
2. **Luôn trả `TotalCount`** cùng dữ liệu trang — thiếu nó UI không phân trang được.
3. **Kẹp (clamp) input một chỗ** (`PageRequest`): chặn page 0 và pageSize khổng lồ.
4. **Whitelist cột sort** — không dịch thẳng chuỗi người dùng thành cột.
5. **Thứ tự**: WHERE → COUNT → ORDER BY → SKIP/TAKE.
6. **Cùng hợp đồng, khác thực thi**: SQL đẩy xuống DB; Mongo đẩy phần làm được, phần cross-collection
   xử lý trong bộ nhớ (và chấp nhận giới hạn sort — một bài học, không phải lỗi).
