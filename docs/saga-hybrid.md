# Hybrid provider + Saga (SQL Server ⇄ MongoDB)

Tài liệu này mô tả provider thứ 3 (`DatabaseProvider=Hybrid`): **tách dữ liệu** giữa SQL Server và
MongoDB và dùng **Saga pattern** để một thao tác ghi xuống cả hai DB vẫn "transaction hoàn chỉnh".

## Vì sao là Saga, không phải transaction ACID xuyên 2 DB?

Không tồn tại 2-phase-commit/XA dùng chung cho SQL Server **và** MongoDB. Saga thay thế transaction
phân tán bằng **chuỗi local transaction**: mỗi DB commit riêng; nếu một bước sau thất bại thì chạy
**compensating action** để hoàn tác bước trước. "Track commit" = ghi nhật ký saga để biết kết quả.

## Tách dữ liệu

| DB | Aggregate |
|----|-----------|
| **SQL Server** | `Order`, `OrderDetail`, `Payment` + bảng ledger `SagaTransactions` |
| **MongoDB** | `Product`, `Category`, `Customer`, `Employee`, `User` |

## Các project

- `Infrastructure.Saga.Core` — kernel: `SagaContext`, `ISagaParticipant`, `SagaUnitOfWork` (implement
  `IUnitOfWork` của Domain), `ISagaLog`, và 2 port "lá" cắt vòng phụ thuộc đọc xuyên DB
  (`ICrossDbDirectory`, `ICrossDbOrderStats`).
- `Infrastructure.SqlServer.Saga` — CQRS cho Order/Payment, `SqlSagaParticipant` (pivot, EF transaction
  thật), `SqlSagaLog` (ghi `SagaTransactions` bằng context riêng), report + order/payment read enrich
  tên từ Mongo qua `ICrossDbDirectory`, và `SqlCrossDbOrderStats` (thống kê order cho Mongo).
- `Infrastructure.MongoDB.Saga` — CQRS cho 5 aggregate, `MongoProductWriteRepository` **saga-aware**
  (snapshot + đăng ký compensation), `MongoSagaParticipant` (no-op), `MongoCrossDbDirectory`.

2 project gốc (`Infrastructure.SqlServer`, `Infrastructure.MongoDB`) **không bị thay đổi** — vẫn là 2
provider single-DB chạy độc lập.

## Luồng saga: ConfirmOrder / CancelOrder (đã confirmed)

`OrderWriteService` **không đổi**. Mỗi DB có **một local transaction thật**; saga chỉ lo cái khe
cross-DB.

1. **Begin** — `BeginTransactionAsync`: mở **transaction SQL** (pivot) + **transaction Mongo** (cần
   replica set).
2. **Mongo** — `Product.UpdateRange`: snapshot + đăng ký compensation **trước**, rồi ghi (pending
   trong transaction Mongo).
3. **SQL** — `Order.Update`: ghi (pending trong transaction SQL).
4. **Commit** — commit Mongo **trước**, rồi commit SQL (pivot) **sau** → ghi log `Committed`.
   - Mongo commit lỗi → SQL còn pending → **rollback native cả hai**, khỏi bù trừ.
   - SQL commit lỗi (sau khi Mongo đã commit) → **Rollback**: rollback SQL + replay compensation
     khôi phục snapshot Mongo → ghi log `Compensated`.

⇒ Mỗi DB atomic nội bộ (transaction thật). Chỉ còn **một khe** duy nhất (SQL hỏng sau khi Mongo đã
commit) cần compensation — đã được khép kín bằng recovery worker (mục dưới).

## Crash-recovery (khép cái khe giữa 2 commit)

Compensation in-process chỉ chạy khi tiến trình còn sống. Nếu **crash đúng giữa Mongo-commit và
SQL-commit**, ta cần đối chiếu lại lúc khởi động. Cơ chế dùng **2 marker bền vững, mỗi cái ghi trong
chính transaction của store đó** nên tồn tại ⇔ store đó đã commit:

- **Mongo `saga_effects`** (delta kho đã trừ) — ghi cùng `SaveChanges` với việc trừ kho.
- **SQL `SagaCommits`** (bằng chứng pivot commit) — ghi trong business transaction của order.

`SagaRecoveryService` (BackgroundService, chạy 1 lần lúc khởi động — chỉ ở provider Hybrid) duyệt mọi
`saga_effect` chưa revert:

| Mongo effect | SQL commit marker | Kết luận | Hành động |
|---|---|---|---|
| có | **có** | cả 2 đã commit → saga thành công | xoá 2 marker, log `Committed` |
| có | **không** | Mongo commit, pivot KHÔNG → dở dang | revert delta kho, log `Compensated` |

Cùng một hàm `RevertAsync` được dùng cho **cả** compensation in-process lẫn recovery; nó **idempotent**
(cờ `Reverted` lật trong 1 Mongo transaction) nên chạy 2 lần vẫn an toàn → bản thân recovery cũng
chịu được crash.

⇒ Đây là điểm "exactly-once theo nghĩa thực dụng": không phải ACID 2PC, nhưng mọi đường đều hội tụ về
trạng thái nhất quán, có nhật ký kiểm chứng.

## Chạy thử

1. `appsettings`: đặt `DatabaseProvider=Hybrid`. Cần cả `ConnectionStrings:SqlServerSaga` (DB riêng
   `SellingNewProduct_Saga`) lẫn `ConnectionStrings:MongoDB`. **Mongo phải là replica set** — saga
   dùng transaction Mongo thật (xem docs/mongo-replica-set.md, docker-compose.mongo-rs.yml).
2. Tạo schema SQL: `dotnet ef database update --context AppDbContext` và
   `dotnet ef database update --context SagaLogDbContext` trong project `Infrastructure.SqlServer.Saga`.
3. Tạo Customer/Employee/Product (rơi xuống Mongo) + Order (rơi xuống SQL).
4. Confirm order → kho giảm ở Mongo, order `Confirmed` ở SQL, 1 dòng `SagaTransactions = Committed`.
5. **Demo compensation**: tạm cho bước SQL ném exception (ví dụ trong `SqlOrderWriteRepository.UpdateAsync`)
   rồi confirm → kho ở Mongo được **khôi phục**, `SagaTransactions = Compensated`, SQL không đổi.

## Lưu ý: đọc xuyên DB (polyglot CQRS)

Read model của Order cần tên Customer/Employee (Mongo); read model của Customer/Employee cần thống kê
order (SQL). Không JOIN xuyên DB được nên hai phía enrich **in-memory** qua 2 port lá. Hai port lá chỉ
phụ thuộc context DB của chính mình ⇒ không tạo vòng phụ thuộc DI. Production nên thay bằng read model
denormalized (materialized view) cập nhật qua event.
