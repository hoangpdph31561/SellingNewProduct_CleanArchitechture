# ROADMAP — Tiến độ & Checklist

> **File này là NGUỒN SỰ THẬT để tiếp tục công việc.**
> Mở chat mới → bảo Claude đọc file này trước. Sau mỗi bước hoàn thành, tick `[x]`.

Cập nhật lần cuối: 2026-06-16
Trạng thái tổng thể: **Phase 1-5 + 7 XONG. Phase 9 (refactor 3 tầng + Domain Service) XONG. Toàn solution build pass (0 error). Còn lại: Phase 6 (user tự chạy demo SQL↔Mongo, seed, unit test).**

> ⚠️ **Cập nhật kiến trúc (2026-06-16) — xem Phase 9 cuối file:** Đã **bỏ project Application**, gộp read
> side vào **Domain**. Đã thêm **Domain Service** (`I*Service`) cho cả 7 module — API gọi service, KHÔNG
> gọi repository trực tiếp. Còn **3 tầng**: API · Domain · Infrastructure. Các mô tả "Application" / "5 project"
> bên dưới (Phase 7) là LỊCH SỬ, không còn đúng hiện trạng.

---

## Phase 0 — Khởi tạo & tài liệu
- [x] Khảo sát hiện trạng (solution `SellingNewProduct`, .NET 10, mới có API template)
- [x] Viết tài liệu: README, ARCHITECTURE, DOMAIN_MODEL, CONVENTIONS, ROADMAP
- [ ] Xác nhận kế hoạch với người dùng

## Phase 1 — Dựng khung 4 project ✅
- [x] Tạo `SellingNewProduct.Domain` (classlib) + add vào solution
- [x] Tạo `SellingNewProduct.Infrastructure.SqlServer` (classlib) → ref Domain
- [x] Tạo `SellingNewProduct.Infrastructure.MongoDB` (classlib) → ref Domain
- [x] API ref Domain + 2 Infra
- [x] Xoá WeatherForecast template khỏi API
- [x] `dotnet build` toàn solution PASS

## Phase 2 — Domain (trái tim) ✅
- [x] Common: `BaseEntity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `EntityStatus` (enum), `IDomainEvent`, `DomainException`
- [x] ValueObjects: `Money`, `Email`, `Address`, `Sku`; enum `Size`
- [x] Aggregate `User` (+ `UserRole`) + business
- [x] Aggregate `Customer` + business
- [x] Aggregate `Employee` + business
- [x] Aggregate `Category` + business
- [x] Aggregate `Product` + business (ChangePrice, Increase/DecreaseStock)
- [x] Aggregate `Order` + `OrderDetail` + `OrderStatus` + business rules (AddDetail/Confirm/Cancel/Ship)
- [x] Aggregate `Payment` (+ `PaymentMethod`, `PaymentStatus`)
- [x] `OrderConfirmedEvent`
- [x] Interfaces (7): `IUserRepository`, `ICustomerRepository`, `IEmployeeRepository`, `ICategoryRepository`, `IProductRepository`, `IOrderRepository`, `IPaymentRepository`
- [ ] (Tuỳ chọn) Unit test cho business rule của Order

## Phase 3 — Infrastructure.SqlServer ✅
- [x] Package: EFCore.SqlServer + Design (10.0.9)
- [x] `AppDbContext` (+ `AppDbContextFactory` design-time)
- [x] Persistence models (`*Record`): User, Customer, Employee, Category, Product, Order, OrderDetail, Payment
- [x] `IEntityTypeConfiguration` cho từng model (FK, index unique Sku/Username)
- [x] Global Query Filter xóa mềm (`Status != Deleted`)
- [x] Mapper Domain ↔ Record map tay (kèm `Rehydrate`)
- [x] Repository impl (7 cái)
- [x] `DependencyInjection.AddSqlServerInfrastructure(...)`
- [x] Migration đầu tiên `InitialCreate` (user tự chạy `database update` với LocalDB của mình)

## Phase 4 — Infrastructure.MongoDB ✅
- [x] Package: `MongoDB.EntityFrameworkCore` (10.0.2)
- [x] `MongoAppDbContext`
- [x] Persistence documents (`*Document`): User, Customer, Employee, Category, Product, Order (nhúng `Details[]`), Payment
- [x] Cấu hình `ToCollection(...)` + `OwnsMany(Details)` qua IEntityTypeConfiguration
- [x] Xóa mềm: provider Mongo KHÔNG có global query filter → lọc `Status != Deleted` trong repository
- [x] Mapper Domain ↔ Document map tay
- [x] Repository impl (7 cái)
- [x] `DependencyInjection.AddMongoInfrastructure(...)`

## Phase 5 — API ✅
- [x] DTO (Contracts): Category, Product, Customer, User, Employee, Order (+AddOrderDetail), Payment
- [x] FluentValidation validators (chỉ format request)
- [x] Mapping Domain → Response map tay (`ApiMappings`)
- [x] Controllers (7): Categories, Products, Customers, Users, Employees, Orders, Payments
- [x] `ExceptionHandlingMiddleware` (DomainException + ValidationException → ProblemDetails 400)
- [x] `Program.cs`: đọc `DatabaseProvider` từ config, đăng ký Infra tương ứng
- [x] appsettings: connection string SQL + Mongo, `DatabaseProvider`
- [x] OpenAPI (`MapOpenApi`)
- [x] `PasswordHasher` (SHA256 — demo, không production)

## Phase 6 — Chứng minh "đổi DB không đổi Domain" (user tự chạy)
- [ ] Chạy với `DatabaseProvider=SqlServer`, test luồng tạo Order (xem thứ tự ở README)
- [ ] Đổi sang `DatabaseProvider=MongoDB` (cần Mongo chạy ở localhost:27017), chạy lại CÙNG request
- [ ] So sánh: SQL lưu 2 bảng Orders+OrderDetails; Mongo lưu 1 document có Details nhúng
- [ ] (Tuỳ chọn) Seed dữ liệu mẫu để demo nhanh

### Thứ tự gọi API để tạo 1 đơn hàng hoàn chỉnh
1. `POST /api/categories` → lấy categoryId
2. `POST /api/products` (dùng categoryId) → productId
3. `POST /api/users` → userId
4. `POST /api/employees` (dùng userId) → employeeId
5. `POST /api/customers` → customerId
6. `POST /api/orders` (customerId, employeeId) → orderId (Draft)
7. `POST /api/orders/{orderId}/details` (productId, quantity) → thêm dòng
8. `POST /api/orders/{orderId}/confirm` → Confirmed (chặn nếu rỗng)
9. `POST /api/payments` (orderId, amount) rồi `POST /api/payments/{id}/complete`

### Lệnh chạy
```
# SQL Server (mặc định)
dotnet run --project SellingNewProduct.API
# MongoDB: sửa appsettings DatabaseProvider="MongoDB" (cần MongoDB chạy) rồi chạy lại
```

---

## Phase 7 — Read side / Application (CQRS-lite) ✅
> Lý do thêm: hiển thị đơn cần TÊN khách & TÊN nhân viên (không phải Id); và các truy vấn
> nhiều bảng (lịch sử mua hàng, sản phẩm bán chạy, doanh số NV) gây N+1 nếu enrich từng cái.
- [x] Tạo project `SellingNewProduct.Application` (classlib) → ref Domain; add vào solution
- [x] Read-model phẳng: `OrderDetailView`, `OrderLineView`, `CustomerOrderHistoryView`, `OrderSummaryView`, `BestSellingProductView`, `EmployeeSalesView`
- [x] Query interface: `IOrderQueries` (GetOrderDetail / GetCustomerHistory / Search), `IReportQueries` (BestSellingProducts / EmployeeSalesLeaderboard)
- [x] SqlServer implement bằng **JOIN/GROUP BY thật** (`SqlServerOrderQueries`, `SqlServerReportQueries`)
- [x] MongoDB implement bằng **nạp + ghép trong bộ nhớ** (`MongoOrderQueries`, `MongoReportQueries`)
- [x] Đăng ký DI cho cả 2 Infra; API ref Application
- [x] Endpoint: `GET /api/orders/{id}/view`, `GET /api/orders` (search), `GET /api/customers/{id}/orders`, `GET /api/reports/best-selling-products`, `GET /api/reports/employee-sales`
- [x] `dotnet build` PASS (0 error). Tài liệu cập nhật (README, ARCHITECTURE, code/05-Application)

## Quyết định kiến trúc đã chốt
- 5 project: Domain / **Application** / Infra.SqlServer / Infra.MongoDB / API.
- Repository interface (write side) ở Domain (Dependency Inversion).
- **Read side tách riêng (CQRS-lite)**: read-model + query interface ở **Application**; Infra implement
  (SQL = JOIN, Mongo = ghép bộ nhớ). Aggregate KHÔNG ôm dữ liệu aggregate khác (Order chỉ giữ CustomerId).
- Persistence model tách riêng mỗi Infra (`*Record` / `*Document`).
- **8 bảng:** User, Customer, Employee, Category, Product, Order, OrderDetail (con), Payment.
- **BaseEntity<TId>** + enum `EntityStatus` → **xóa mềm** (không xóa cứng), lọc bằng EF Global Query Filter.
- **Map tay toàn bộ** (KHÔNG AutoMapper) — DTO↔Domain và Domain↔Persistence.
- **Validation theo tầng:** API (FluentValidation, format) · Domain (DomainException, business) · Infra (EF/DB constraint).
- **Đặt tên biến:** field `my<Type><Name>`, param `the<Type><Name>`, local `a<Type><Name>` (không `_`). Public giữ PascalCase. Xem CONVENTIONS.
- Lỗi nghiệp vụ: `DomainException` + middleware → ProblemDetails 400.

## Phase 8 — Read side: việc còn mở (tiếp theo nếu cần)
> Nối tiếp Phase 7. Chưa làm, để dành cho phiên sau.
- [x] **Phân trang (pagination)** cho `IOrderQueries.SearchAsync` và `IReportQueries.GetBestSellingProductsAsync`
      (thêm `thePage`/`thePageSize` + trả `PagedResult<T>` có `TotalCount`/`TotalPages`/`HasNext`/`HasPrevious`).
      - `Application/Common/PagedResult.cs`: record `PagedResult<T>` + struct `PageRequest` (clamp page≥1, pageSize 1..200, mặc định 20) — gom luật phân trang 1 chỗ, dùng chung SQL & Mongo.
      - SQL: `CountAsync()` + `Skip/Take` đẩy hẳn xuống DB (OFFSET/FETCH) cho cả Search lẫn best-selling (đếm số GROUP BY).
      - Mongo: Search dùng `Count`+`Skip/Take` ở DB rồi mới ghép tên (chỉ ghép cho trang hiện tại); best-selling group bộ nhớ → total = cả list, `Skip/Take` in-memory.
      - API: `GET /api/orders?thePage=&thePageSize=` và `GET /api/reports/best-selling-products?thePage=&thePageSize=` trả `PagedResult<T>`.
- [x] **Tìm kiếm + lọc + sắp xếp nâng cao** (read side, cả SQL & Mongo, đều phân trang):
      - **Product search MỚI** `IProductQueries.SearchAsync` → `GET /api/products/search`. Lọc: `theName` (contains),
        `theCategoryId` (loại hàng), `thePriceFrom/thePriceTo` (khoảng giá), `theMinStock/theMaxStock` (khoảng tồn kho),
        `theStatus` (Active/Inactive; soft-deleted luôn bị loại). Sort `theSortBy` ∈ {name, price, stock} + `theSortDescending`.
        Read-model mới `ProductSummaryView` (kèm `CategoryName` qua JOIN). SQL đẩy hết WHERE/ORDER BY/OFFSET-FETCH xuống DB;
        Mongo đẩy các so sánh field xuống DB, còn name-contains + sort + paging làm in-memory (tradeoff đã ghi chú).
      - **Order search MỞ RỘNG** `IOrderQueries.SearchAsync`: thêm `theCustomerName`/`theEmployeeName` (contains) và
        sort `theSortBy` ∈ {orderDate, totalAmount, customerName, employeeName} + `theSortDescending`.
        Mongo: lọc theo tên → resolve ra danh sách Id rồi `$in` (giữ paging chạy ở DB); sort theo tên ghép không khả thi ở Mongo
        nên fallback orderDate desc (minh hoạ khác biệt do Mongo không JOIN). SQL sort được mọi cột.
      - DI: đăng ký `IProductQueries` cho cả 2 Infra.
- [ ] **Ví dụ "snapshot tên khách trên hoá đơn"**: minh hoạ code phân biệt 2 ca — (a) đông cứng tên khách
      vào Order lúc tạo (yêu cầu nghiệp vụ hoá đơn pháp lý) vs (b) JOIN lấy tên hiện tại để hiển thị (read side).
      Đây là ranh giới tinh tế giữa "snapshot hợp lệ trong aggregate" và "live reference của read-side".
- [ ] (Tuỳ chọn) Mongo: thay "nạp + ghép bộ nhớ" bằng aggregation pipeline `$lookup`/`$group` cho báo cáo lớn.
- [ ] (Tuỳ chọn) Cân nhắc tách read-model ra `*Response` DTO riêng ở API nếu hợp đồng HTTP cần khác read-model.

## Phase 9 — Gộp 3 tầng + Domain Service ✅ (2026-06-16)
> Theo review: API không được gọi repository / `new` entity; logic nghiệp vụ phải nằm dưới API; chỉ 3 tầng.
- [x] **Bỏ project `Application`**: chuyển read side vào Domain — `PagedResult`/`PageRequest` → `Domain/Common`;
      `I*Queries` → `Domain/Queries`; read-model `*View` → `Domain/ReadModels`. Gỡ ProjectReference + khỏi `.slnx`.
- [x] **Domain Service cho cả 7 module** (`I*Service` public + `*Service` internal): Category (check trùng tên),
      Product/Employee/Order/Payment (existence check entity liên quan), Customer, User. Đăng ký qua `AddDomainServices()`
      (`Domain/DependencyInjection.cs`); Domain thêm package `Microsoft.Extensions.DependencyInjection.Abstractions`.
- [x] **`NotFoundException`** (`Domain/Common`) → middleware map **404** (tách khỏi `DomainException` → 400).
- [x] **`IPasswordHasher`** khai báo ở `Domain/Users`, `PasswordHasher` (API) implement + đăng ký DI.
- [x] **7 controller** chỉ inject `I*Service` (+ `I*Queries` cho endpoint đọc) + validator; bỏ repository & `new` entity.
- [x] **Tổ chức lại folder Domain theo loại**: `Abstractions/` (I*Service + I*Queries + IPasswordHasher),
      `Services/` (impl internal), `Repositories/`, `ReadModels/`; folder aggregate (Categories/, Orders/…) chỉ còn entity.
- [x] `dotnet build` PASS (0 error, 0 warning). Docs cập nhật (README, ARCHITECTURE, CONVENTIONS, code/01,04,05, README index).
- Ghi nhớ: comment trong code = tiếng Anh; tài liệu Markdown = tiếng Việt.
- **Đã chốt với user (giữ nguyên, KHÔNG đổi):** repository interface ở Domain (không xuống Infra — DIP);
  giữ ReadModels + IQueries (read side); aggregate tham chiếu nhau bằng Id, không ôm List aggregate khác.

## Câu hỏi còn mở (cần người dùng quyết khi tới)
- [ ] Có cần Unit of Work / transaction rõ ràng không? (mặc định: SaveChanges per repo)
- [ ] Seed dữ liệu mẫu để demo nhanh? (đề xuất: có)
- [ ] `Payment` là aggregate riêng hay con của `Order`? (mặc định: aggregate riêng, ref OrderId)
- [ ] Có làm authentication thật cho `User` không, hay chỉ lưu bảng? (mặc định: chỉ lưu bảng, chưa auth)
