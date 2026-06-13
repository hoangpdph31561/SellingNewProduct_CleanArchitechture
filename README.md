# SellingNewProduct — Clean Architecture + DDD (Bán quần áo)

Dự án học tập minh hoạ **Clean Architecture** và **Domain-Driven Design** trên .NET 10.
Mục tiêu cốt lõi: chứng minh rằng **Domain (business) độc lập hoàn toàn với database** — cùng một
Domain có thể chạy trên **SQL Server** hoặc **MongoDB** mà không sửa một dòng code nghiệp vụ nào.

> Ngôn ngữ giao tiếp: tiếng Việt. Code & comment: tiếng Anh.

---

## 1. Cấu trúc giải pháp

```
SellingNewProduct.sln
├── SellingNewProduct.Domain                    # Tầng trong cùng — business thuần, KHÔNG ref ai
├── SellingNewProduct.Application               # Read side: read-model + query interface → ref Domain
├── SellingNewProduct.Infrastructure.SqlServer  # EF Core + SQL Server   → ref Domain + Application
├── SellingNewProduct.Infrastructure.MongoDB    # EF Core + MongoDB       → ref Domain + Application
└── SellingNewProduct.API                        # Controller, DTO, Validation → ref Domain + Application + Infra
```

Quy tắc phụ thuộc (Dependency Rule — mũi tên luôn hướng vào trong):

```
API ─────► Infrastructure.* ─────► Application ─────► Domain
 │              │                                       ▲
 └──────────────┴────────────────────────────────────── ┘   (API/Infra biết Domain & Application)
```

- **Domain** không reference bất kỳ project nào, không reference EF Core, không biết SQL/Mongo.
- **Application** chứa **read side** (CQRS-lite): read-model phẳng + interface truy vấn (`IOrderQueries`,
  `IReportQueries`) để JOIN/báo cáo nhiều bảng. Chỉ ref Domain.
- **Infrastructure** triển khai (implement) interface repository của Domain **và** query của Application
  (SQL bằng JOIN thật; Mongo ghép trong bộ nhớ).
- **API** là *composition root*: chọn đăng ký Infra nào (SqlServer hay Mongo) lúc khởi động.

> 💡 **Vì sao read-model không nằm ở Domain hay API?** Nó không phải business rule (nên không vào Domain),
> nhưng Infrastructure phải *nhìn thấy* kiểu trả về để implement query — mà Infra không ref API. Tầng
> **Application** là nơi cả Infra lẫn API đều thấy được. Xem [docs/code/05-Application.md](docs/code/05-Application.md).

## 2. Trách nhiệm từng tầng

| Tầng | Chứa gì | Không được chứa |
|------|---------|-----------------|
| **Domain** | Entity nghiệp vụ (Order, Customer, Product), Value Object (Money, Email, Address), Aggregate Root, Domain Event, business rule, **interface repository** | EF Core, attribute DB, DTO, JSON |
| **Application** | **Read-model** phẳng (`OrderDetailView`, `BestSellingProductView`…), **interface query** (`IOrderQueries`, `IReportQueries`) cho việc đọc/báo cáo nhiều bảng | Business rule, EF Core, truy cập DB |
| **Infrastructure.SqlServer** | `DbContext`, persistence model riêng (`*Record`), `IEntityTypeConfiguration`, Repository impl, Mapper Domain↔Record | Logic nghiệp vụ |
| **Infrastructure.MongoDB** | `DbContext` (provider Mongo), persistence model riêng (`*Document`), Repository impl, Mapper Domain↔Document | Logic nghiệp vụ |
| **API** | Controller, Request/Response DTO, Validation (FluentValidation), DI/config, mapping DTO↔Domain | Truy cập DB trực tiếp |

## 3. Yêu cầu môi trường

- .NET SDK **10.0.x** (`dotnet --version`)
- SQL Server (LocalDB / container) cho Infra SqlServer
- MongoDB (local / container) cho Infra MongoDB
- EF Core CLI: `dotnet tool install --global dotnet-ef`

## 4. Chạy dự án

```bash
# Chọn database qua appsettings: "DatabaseProvider": "SqlServer" hoặc "MongoDB"
dotnet run --project SellingNewProduct.API
```

## 5. Tài liệu (đọc để continue ở chat mới)

| File | Nội dung |
|------|----------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Nguyên tắc kiến trúc, luồng dữ liệu, cách map Domain↔Persistence |
| [docs/DOMAIN_MODEL.md](docs/DOMAIN_MODEL.md) | Mô hình nghiệp vụ: Aggregate, Entity, Value Object, business rule |
| [docs/ROADMAP.md](docs/ROADMAP.md) | **Checklist tiến độ** — nguồn sự thật để tiếp tục công việc |
| [docs/CONVENTIONS.md](docs/CONVENTIONS.md) | Quy ước đặt tên, code style, package |
| [docs/code/](docs/code/README.md) | **Giải thích từng file `.cs` và từng method** (cho người học) |
| [docs/code/05-Application.md](docs/code/05-Application.md) | **Read side / CQRS-lite**: read-model, query JOIN nhiều bảng (SQL vs Mongo) |

> **Khi mở chat mới:** bảo Claude đọc `docs/ROADMAP.md` trước để biết đã làm tới đâu.
