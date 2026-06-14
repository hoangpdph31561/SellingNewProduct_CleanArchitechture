# 06 — Phân trang, Tìm kiếm, Lọc & Sắp xếp (Read side)

Tài liệu này nối tiếp [05-Application.md](05-Application.md). Nó giải thích cách dự án thêm
**phân trang (pagination)**, **tìm kiếm theo tên**, **lọc nhiều tiêu chí** và **sắp xếp (sort)**
vào phía ĐỌC — và vì sao mỗi tính năng lại đặt ở đúng tầng của nó.

> 💡 Luận điểm xuyên suốt vẫn không đổi: **một hợp đồng (interface) duy nhất, hai cách thực thi**
> (SQL đẩy xuống DB; Mongo làm phần còn lại trong bộ nhớ). Phân trang/lọc/sort chỉ là *cách đọc*,
> không phải *business rule*, nên chúng sống ở **Application + Infrastructure**, tuyệt đối không vào Domain.

---

## A. Vấn đề: trả về cả bảng là một cái bẫy

Trước đây `SearchAsync` trả `IReadOnlyList<OrderSummaryView>` — **toàn bộ** dòng khớp. Với 10 đơn
thì ổn; với 100.000 đơn thì:

- Tốn RAM (nạp hết về app), tốn băng thông (serialize hết ra JSON).
- Màn hình danh sách chỉ hiển thị ~20 dòng/trang → 99% dữ liệu tải về là lãng phí.
- Không có cách nào nói cho UI biết "có tổng cộng bao nhiêu dòng" để vẽ nút *Trang sau*.

→ Giải pháp: chỉ lấy **một trang**, kèm **tổng số dòng**. Đó là `PagedResult<T>`.

---

## B. `PagedResult<T>` và `PageRequest` — `Application/Common/PagedResult.cs`

Hai kiểu nhỏ, đặt ở **Application** vì cả Infra (tạo ra) lẫn API (trả về) đều phải thấy.

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

## D. Tìm kiếm theo tên + Lọc nhiều tiêu chí

### Quy ước "filter tuỳ chọn": `null = không lọc`

Mọi tham số lọc đều nullable và mặc định `null`. Code chỉ thêm `.Where(...)` khi tham số có giá trị:

```csharp
if (theCategoryId is not null)              // chỉ lọc khi người dùng truyền
    aQuery = aQuery.Where(x => x.p.CategoryId == theCategoryId);

if (thePriceFrom is not null)               // khoảng giá: cận dưới
    aQuery = aQuery.Where(x => x.p.PriceAmount >= thePriceFrom);

if (theMaxStock is not null)                // khoảng tồn kho: cận trên (vd hàng sắp hết)
    aQuery = aQuery.Where(x => x.p.StockQuantity <= theMaxStock);
```

→ `aQuery` được **bồi (compose)** dần. Vì là `IQueryable`, **chưa chạy** DB cho tới khi gọi
`CountAsync`/`ToListAsync`. EF gộp tất cả `.Where` thành **một** mệnh đề `WHERE` duy nhất.

### Tìm theo tên: `Contains`

```csharp
if (!string.IsNullOrWhiteSpace(theName))
{
    var aName = theName.Trim();
    aQuery = aQuery.Where(x => x.p.Name.Contains(aName));   // SQL → LIKE '%aName%'
}
```

### Lọc Product (catalogue) — `IProductQueries.SearchAsync`

| Tham số | Ý nghĩa | Dịch ra SQL |
|---------|---------|-------------|
| `theName` | tên chứa chuỗi | `Name LIKE '%..%'` |
| `theCategoryId` | loại hàng | `CategoryId = @id` |
| `thePriceFrom` / `thePriceTo` | khoảng giá | `PriceAmount BETWEEN` |
| `theMinStock` / `theMaxStock` | khoảng tồn kho | `StockQuantity BETWEEN` |
| `theStatus` | Active/Inactive | `Status = @s` (đã xoá mềm luôn bị loại) |

### Lọc Order (mở rộng) — `IOrderQueries.SearchAsync`

Thêm `theCustomerName` / `theEmployeeName` (contains) bên cạnh các filter cũ (id, status, khoảng ngày).

---

## E. Sắp xếp (sort) — cái "query người dùng truyền vào" cần được xử lý

Phân trang ban đầu chỉ xử lý *số trang*; còn khi người dùng muốn "sắp theo giá giảm dần" thì sao?
Ta nhận **tên cột** (`theSortBy`) + **chiều** (`theSortDescending`).

> ⚠️ **Tuyệt đối không** dịch thẳng chuỗi người dùng thành cột/biểu thức (nguy cơ lỗi & injection).
> Ta **whitelist**: chỉ chấp nhận một số cột đã biết, còn lại rơi về mặc định an toàn.

```csharp
aQuery = (theSortBy?.Trim().ToLowerInvariant()) switch
{
    "price" => theSortDescending ? aQuery.OrderByDescending(x => x.p.PriceAmount)
                                 : aQuery.OrderBy(x => x.p.PriceAmount),
    "stock" => theSortDescending ? aQuery.OrderByDescending(x => x.p.StockQuantity)
                                 : aQuery.OrderBy(x => x.p.StockQuantity),
    _       => theSortDescending ? aQuery.OrderByDescending(x => x.p.Name)   // mặc định: name
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
var aCustomerIdFilter = await ResolveCustomerIdsByNameAsync(theCustomerName, ct);
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

| Method | Tham số chính | Trả về |
|--------|---------------|--------|
| `GET /api/products/search` | `theName, theCategoryId, thePriceFrom/To, theMinStock/MaxStock, theStatus, theSortBy, theSortDescending, thePage, thePageSize` | `PagedResult<ProductSummaryView>` |
| `GET /api/orders` | `theCustomerId/Name, theEmployeeId/Name, theStatus, theFromUtc/ToUtc, theSortBy, theSortDescending, thePage, thePageSize` | `PagedResult<OrderSummaryView>` |
| `GET /api/reports/best-selling-products` | `thePage, thePageSize` | `PagedResult<BestSellingProductView>` |

Ví dụ:
```
GET /api/products/search?theName=áo&thePriceFrom=100000&theMaxStock=5&theSortBy=price&theSortDescending=true&thePage=1&thePageSize=20
GET /api/orders?theCustomerName=an&theStatus=Confirmed&theSortBy=totalAmount&theSortDescending=true&thePage=2
```

> ⚠️ **Breaking change về hợp đồng HTTP:** 2 endpoint `GET /api/orders` và `best-selling-products`
> nay trả **object** `{ items, page, pageSize, totalCount, totalPages, hasNext, hasPrevious }`
> thay vì mảng thuần. Client phải đọc `response.items`.

---

## H. Nguyên tắc rút ra (ghi nhớ)

1. **Phân trang/lọc/sort là *cách đọc*, không phải business** → nằm ở Application + Infrastructure,
   không bao giờ ở Domain.
2. **Luôn trả `TotalCount`** cùng dữ liệu trang — thiếu nó UI không phân trang được.
3. **Kẹp (clamp) input một chỗ** (`PageRequest`): chặn page 0 và pageSize khổng lồ.
4. **Whitelist cột sort** — không dịch thẳng chuỗi người dùng thành cột.
5. **Thứ tự**: WHERE → COUNT → ORDER BY → SKIP/TAKE.
6. **Cùng hợp đồng, khác thực thi**: SQL đẩy xuống DB; Mongo đẩy phần làm được, phần cross-collection
   xử lý trong bộ nhớ (và chấp nhận giới hạn sort — một bài học, không phải lỗi).
