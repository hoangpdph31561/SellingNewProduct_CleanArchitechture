# SellingNewProduct — Clean Architecture + DDD (Bán quần áo)

Dự án học tập minh hoạ **Clean Architecture** và **Domain-Driven Design** trên .NET 10.
Mục tiêu cốt lõi: chứng minh rằng **Domain (business) độc lập hoàn toàn với database** — cùng một
Domain có thể chạy trên **SQL Server** hoặc **MongoDB** mà không sửa một dòng code nghiệp vụ nào.

> Ngôn ngữ giao tiếp: tiếng Việt. Code & comment: tiếng Anh.

---

## 1. Cấu trúc giải pháp

```
SellingNewProduct.sln  (3 tầng)
├── SellingNewProduct.Domain                    # Tầng trong cùng — business + Domain Service + read side, KHÔNG ref ai*
├── SellingNewProduct.Infrastructure.SqlServer  # EF Core + SQL Server   → ref Domain
├── SellingNewProduct.Infrastructure.MongoDB    # EF Core + MongoDB       → ref Domain
└── SellingNewProduct.API                        # Controller, DTO, Validation → ref Domain + 2 Infra
```
<sub>* Domain chỉ ref `Microsoft.Extensions.DependencyInjection.Abstractions` (contract DI thuần để có `AddDomainServices`).</sub>

Quy tắc phụ thuộc (Dependency Rule — mũi tên luôn hướng vào trong):

```
API ─────► Infrastructure.* ─────► Domain
 │                                   ▲
 └───────────────────────────────────┘   (API cũng ref thẳng Domain)
```

- **Domain** không reference project nào, không EF Core, không biết SQL/Mongo. Chứa: entity/aggregate,
  **Domain Service** (`I*Service` — write side, API gọi cái này), **interface repository**, và **read side**
  (`I*Queries` + read-model `*View` + `PagedResult<T>`).
- **Infrastructure** triển khai interface repository **và** query của Domain (SQL bằng JOIN thật; Mongo ghép trong bộ nhớ).
- **API** là *composition root*: validate format request, gọi Domain Service / query, chọn đăng ký Infra nào lúc khởi động.

> 💡 **Chỉ 3 tầng.** Tầng Application cũ (read side) đã gộp vào Domain. Đánh đổi: Domain "rộng" hơn (ôm
> read-model) nhưng kiến trúc đơn giản, đúng mô hình API → Domain ← Infrastructure. Xem [docs/code/05-Application.md](docs/code/05-Application.md).

## 2. Trách nhiệm từng tầng

| Tầng | Chứa gì | Không được chứa |
|------|---------|-----------------|
| **Domain** | Entity nghiệp vụ, Value Object, Aggregate Root, Domain Event, business rule, **interface repository**, **Domain Service** (`I*Service`), **read side** (`I*Queries` + read-model `*View` + `PagedResult<T>`) | EF Core, attribute DB, DTO, JSON |
| **Infrastructure.SqlServer** | `DbContext`, persistence model riêng (`*Record`), `IEntityTypeConfiguration`, Repository impl, Query impl (JOIN), Mapper Domain↔Record | Logic nghiệp vụ |
| **Infrastructure.MongoDB** | `DbContext` (provider Mongo), persistence model riêng (`*Document`), Repository impl, Query impl (stitch), Mapper Domain↔Document | Logic nghiệp vụ |
| **API** | Controller (gọi Domain Service/query), Request/Response DTO, Validation (FluentValidation), `IPasswordHasher` impl, DI/config, mapping DTO↔Domain | Truy cập DB trực tiếp, business rule |

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
| [docs/code/06-Pagination-Search.md](docs/code/06-Pagination-Search.md) | **Phân trang, tìm kiếm, lọc & sắp xếp**: `PagedResult<T>`, filter, sort (SQL vs Mongo) |

> **Khi mở chat mới:** bảo Claude đọc `docs/ROADMAP.md` trước để biết đã làm tới đâu.
