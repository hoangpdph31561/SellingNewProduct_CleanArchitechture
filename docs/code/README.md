# Giải thích code chi tiết (Code Walkthrough)

Bộ tài liệu này giải thích **từng file `.cs`** và **từng method** trong dự án, dành cho người
đang học. Đọc theo thứ tự từ trong ra ngoài (Domain trước, API sau) sẽ dễ hiểu nhất.

| File | Nội dung |
|------|----------|
| [01-Domain.md](01-Domain.md) | Tầng Domain — base class, value object, 8 aggregate, repository interface |
| [02-Infrastructure-SqlServer.md](02-Infrastructure-SqlServer.md) | EF Core + SQL Server: record, config Fluent API, mapper, repository, migration |
| [03-Infrastructure-MongoDB.md](03-Infrastructure-MongoDB.md) | EF Core + MongoDB: khác biệt so với SQL (nhúng document, lọc xóa mềm) |
| [04-API.md](04-API.md) | DTO, validator, controller, middleware, Program.cs |
| [05-Application.md](05-Application.md) | Read side / CQRS-lite: read-model + query JOIN nhiều bảng (SQL vs Mongo) |
| [06-Pagination-Search.md](06-Pagination-Search.md) | Phân trang, tìm kiếm theo tên, lọc nhiều tiêu chí, sắp xếp (SQL vs Mongo) |

## Quy ước đọc

- Mỗi file `.cs` có một mục riêng, đường dẫn bấm vào mở được.
- Method giải thích theo dạng: *làm gì → vì sao → lưu ý*.
- Ký hiệu 💡 = điểm học quan trọng (khái niệm Clean Architecture / DDD).
- Tên biến theo quy ước dự án: `my…` (field), `the…` (tham số), `a…` (biến local).
