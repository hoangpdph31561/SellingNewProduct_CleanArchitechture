# Giải thích code chi tiết (Code Walkthrough)

Bộ tài liệu này giải thích **từng file `.cs`** và **từng method** trong dự án, dành cho người
đang học. Đọc theo thứ tự từ trong ra ngoài (Domain trước, API sau) sẽ dễ hiểu nhất.

| File | Nội dung |
|------|----------|
| [01-Domain.md](01-Domain.md) | Tầng Domain — base class, value object, 8 aggregate, port In/Out (read/write), **Domain Service** (tách read/write), `IUnitOfWork`, read side |
| [02-Infrastructure-SqlServer.md](02-Infrastructure-SqlServer.md) | EF Core + SQL Server: record, config Fluent API, mapper, repository (Read/Write), `SqlServerUnitOfWork`, migration |
| [03-Infrastructure-MongoDB.md](03-Infrastructure-MongoDB.md) | EF Core + MongoDB: khác biệt so với SQL (nhúng document, lọc xóa mềm), `MongoUnitOfWork` + replica set |
| [04-API.md](04-API.md) | DTO, controller inject `ISender`, **ApiResponse envelope + xử lý lỗi HTTP**, DI từng tầng |
| [05-Application.md](05-Application.md) | **Tầng Application (CQRS)**: Command/Query + Handler + Validator (1 file/feature), MediatR `ISender`, `ValidationBehavior`; read-model + JOIN nhiều bảng (SQL vs Mongo) |
| [06-Pagination-Search.md](06-Pagination-Search.md) | Query object, phân trang, tìm kiếm theo tên, lọc nhiều tiêu chí, sắp xếp (SQL vs Mongo) |

> Kiến trúc hiện tại: **4 tầng** (API · Application · Domain · Infrastructure). API gửi **Command/Query**
> qua MediatR (`ISender`) tới Application; handler mỏng gọi **Domain Service** (tách `I*ReadService`/
> `I*WriteService`); service gọi repository (tách Read/Write) + `IUnitOfWork`. Validate đầu vào chạy tự
> động qua `ValidationBehavior` trong pipeline MediatR.

## Quy ước đọc

- Mỗi file `.cs` có một mục riêng, đường dẫn bấm vào mở được.
- Method giải thích theo dạng: *làm gì → vì sao → lưu ý*.
- Ký hiệu 💡 = điểm học quan trọng (khái niệm Clean Architecture / DDD).
- Tên biến theo quy ước dự án: `my…` (field), `the…` (tham số), `a…` (biến local).
