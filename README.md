# SellingNewProduct — Clean Architecture + DDD (Bán quần áo)

Dự án học tập minh hoạ **Clean Architecture** và **Domain-Driven Design** trên .NET 10.
Mục tiêu cốt lõi: chứng minh rằng **Domain (business) độc lập hoàn toàn với database** — cùng một
Domain có thể chạy trên **SQL Server** hoặc **MongoDB** mà không sửa một dòng code nghiệp vụ nào.

> Ngôn ngữ giao tiếp: tiếng Việt. Code & comment: tiếng Anh.

---

## 1. Cấu trúc giải pháp

```
SellingNewProduct.sln  (4 tầng)
├── SellingNewProduct.Domain                    # Tầng trong cùng — business + Domain Service + ports, KHÔNG ref ai*
├── SellingNewProduct.Application               # CQRS: Command/Query + Handler + Validator (MediatR) → ref Domain
├── SellingNewProduct.Infrastructure.SqlServer  # EF Core + SQL Server   → ref Domain
├── SellingNewProduct.Infrastructure.MongoDB    # EF Core + MongoDB       → ref Domain
└── SellingNewProduct.API                        # Controller, DTO → ref Application + Domain + 2 Infra
```
<sub>* Domain chỉ ref `Microsoft.Extensions.DependencyInjection.Abstractions` (contract DI thuần để có `AddDomainServices`).</sub>

Quy tắc phụ thuộc (Dependency Rule — mũi tên luôn hướng vào trong):

```
API ──► Application ──┐
 │                    ▼
 ├──► Infrastructure.* ──► Domain
 └────────────────────────► Domain   (API cũng ref thẳng Domain để dùng read-model/query types)
```

- **Domain** không reference project nghiệp vụ nào, không EF Core, không MediatR, không biết SQL/Mongo.
  Chứa: entity/aggregate, **Domain Service** (tách `I*ReadService`/`I*WriteService`), **các port** vào
  (`Interfaces/Inbound`) & ra (`Interfaces/Outbound`: repository, `IUnitOfWork`, `IPasswordHasher`),
  read-model `*View`, input record `*SearchQuery`, `PagedResult<T>`.
- **Application** là tầng CQRS: mỗi use-case một `*Command`/`*Query` + `*Handler` (mỏng) + `*Validator`
  (gói chung **1 file/feature**). Handler chỉ điều phối, gọi xuống Domain Service. MediatR cho phép API
  gửi qua `ISender`; `ValidationBehavior` validate request trong pipeline.
- **Infrastructure** triển khai các outbound port của Domain (repository tách Read/Write + `IUnitOfWork`).
  SQL đọc bằng JOIN thật; Mongo ghép trong bộ nhớ.
- **API** là *composition root*: controller mỏng inject `ISender` gửi Command/Query, map DTO, chọn đăng ký
  Infra nào lúc khởi động.

> 💡 **4 tầng.** API không gọi thẳng Domain Service nữa mà gửi Command/Query qua MediatR (`ISender`) tới
> tầng Application; handler ở Application điều phối rồi mới gọi Domain Service. Xem
> [docs/code/05-Application.md](docs/code/05-Application.md).

## 2. Trách nhiệm từng tầng

| Tầng | Chứa gì | Không được chứa |
|------|---------|-----------------|
| **Domain** | Entity nghiệp vụ, Value Object, Aggregate Root, Domain Event, business rule, **Domain Service** (`*ReadService`/`*WriteService`), **port** (`Interfaces/Inbound` + `Interfaces/Outbound`: repository, `IUnitOfWork`, `IPasswordHasher`), read-model `*View`, `*SearchQuery`, `PagedResult<T>` | EF Core, MediatR, attribute DB, DTO, JSON |
| **Application** | `*Command`/`*Query` (record input), `*Handler` (mỏng — gọi Domain Service), `*Validator` (FluentValidation), `ValidationBehavior` (MediatR pipeline). Mỗi feature **1 file** gộp cả ba | Business rule thật (ở Domain), truy cập DB, EF Core |
| **Infrastructure.SqlServer** | `DbContext` + `SqlServerUnitOfWork`, persistence model (`*Record`), `IEntityTypeConfiguration`, Repository impl (Read/Write), Mapper Domain↔Record | Logic nghiệp vụ |
| **Infrastructure.MongoDB** | `DbContext` (provider Mongo) + `MongoUnitOfWork`, persistence model (`*Document`), Repository impl (Read/Write), Mapper Domain↔Document | Logic nghiệp vụ |
| **API** | Controller (inject `ISender`, gửi Command/Query), Request/Response DTO, `IPasswordHasher` impl, `ApiResponseWrapperFilter`, `ExceptionHandlingMiddleware`, DI/config, mapping DTO↔Command/Response | Truy cập DB trực tiếp, business rule, gọi repository |

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

## 5. Tài liệu

📚 **[docs/README.md](docs/README.md) — Bản đồ toàn bộ tài liệu** (nhóm theo chủ đề + lộ trình đọc + tìm nhanh theo câu hỏi). Bắt đầu từ đây.

Lối tắt hay dùng:

| File | Nội dung |
|------|----------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Nguyên tắc kiến trúc, luồng dữ liệu, map Domain↔Persistence |
| [docs/TONG-HOP-KIEN-THUC.md](docs/TONG-HOP-KIEN-THUC.md) | **Tổng hợp mọi pattern + so sánh công nghệ** (async, saga, Kafka/RabbitMQ/Redis, NFR/observability) |
| [docs/code/](docs/code/README.md) | **Giải thích từng file `.cs` và từng method** (cho người học) |
| [docs/ROADMAP.md](docs/ROADMAP.md) | **Checklist tiến độ** — nguồn sự thật để tiếp tục công việc |

> **Khi mở chat mới:** bảo Claude đọc `docs/ROADMAP.md` trước để biết đã làm tới đâu; đọc `docs/README.md` để tìm tài liệu.
