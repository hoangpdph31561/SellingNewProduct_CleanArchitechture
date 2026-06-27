# Hybrid provider + Saga (SQL Server ⇄ MongoDB)

Tài liệu này mô tả provider thứ 3 (`DatabaseProvider=Hybrid`): **tách dữ liệu** giữa SQL Server và
MongoDB và dùng **Saga orchestration** để một thao tác ghi xuống cả hai DB vẫn nhất quán.

> Đây là trang **tổng quan + vì sao**. Muốn đi **từng class, property, method kèm code theo đúng luồng
> chạy** (Begin → bước Mongo → bước SQL → Commit/Rollback → Recovery), xem
> [saga-files-reference.md](saga-files-reference.md).

## Vì sao là Saga, không phải transaction ACID xuyên 2 DB?

Không tồn tại 2-phase-commit/XA dùng chung cho SQL Server **và** MongoDB. Saga thay thế transaction
phân tán bằng **chuỗi commit cục bộ theo từng bước (commit-per-step)**: mỗi bước commit ngay vào DB
của nó tại thời điểm thành công, và ghi lại cách hoàn tác (compensation). Nếu một bước sau thất bại,
orchestrator **replay compensation theo thứ tự ngược** để hoàn tác các bước đã commit.

> Đây **không** phải kiểu "giữ 2 transaction mở rồi commit ở cuối" (pivot/2PC). Mỗi DB commit độc lập
> ngay tại bước của nó; cái duy nhất được giữ xuyên suốt là **sổ cái saga** (saga ledger).

## Phân loại bước (để scale thêm step về sau)

Orchestrator không gắn cứng với Order/Product. Mỗi bước khai báo `SagaStepKind`:

| Kind | Ví dụ | Khi rollback |
|---|---|---|
| **Compensatable** | trừ kho (Mongo), ghi history | chạy compensation để hoàn tác |
| **Pivot** | ghi Order/Payment (SQL) | điểm không quay lại — commit xong là saga thành công |
| **RetryableForward** | gửi email, push notification | đặt **sau** pivot, không rollback; retry tiến tới |

Thêm một bước compensatable mới = viết một `ISagaCompensationHandler` + đăng ký vào DI. **Orchestrator,
recovery, và Domain/Application không đổi.** Quy tắc thứ tự: compensatable **trước** pivot, forward
**sau** pivot — nhờ vậy không bao giờ phải hoàn tác một việc không hoàn tác được (như email đã gửi).

## Tách dữ liệu

| DB | Aggregate |
|----|-----------|
| **SQL Server** | `Order`, `OrderDetail`, `Payment` |
| **MongoDB** | `Product`, `Category`, `Customer`, `Employee`, `User` + **sổ cái saga** `saga_instances` |

Sổ cái saga nằm **một chỗ duy nhất** ở MongoDB (`saga_instances`) — thay cho 3 marker rải rác của
thiết kế cũ (bảng `SagaTransactions` + `SagaCommits` ở SQL, `saga_effects` ở Mongo).

## Các project

- `Infrastructure.Saga.Core` — kernel điều phối: `SagaContext`, `ISagaStore` (port sổ cái),
  `SagaStepKind`/`SagaStepInfo`/`SagaSnapshot`, `ISagaCompensationHandler` + `ISagaCompensationRegistry`,
  `SagaCompensator` (replay + retry + `NeedsManualReview`), `SagaUnitOfWork`/`SagaUnitOfWorkTransaction`
  (implement `IUnitOfWork` của Domain), `SagaRecoveryService`, và 2 port "lá" đọc xuyên DB
  (`ICrossDbDirectory`, `ICrossDbOrderStats`).
- `Infrastructure.MongoDB.Saga` — CQRS cho 5 aggregate, `MongoProductWriteRepository` **saga-aware**
  (commit-per-step + enroll bước nguyên tử), `MongoSagaStore` (sổ cái `saga_instances`),
  `MongoStockCompensationHandler` (`RevertStock`), `MongoCrossDbDirectory`.
- `Infrastructure.SqlServer.Saga` — CQRS cho Order/Payment (commit ngay = bước **pivot**), report +
  order/payment read enrich tên từ Mongo qua `ICrossDbDirectory`, `SqlCrossDbOrderStats`. **Không còn**
  saga participant/log/marker nào ở SQL.

2 project gốc (`Infrastructure.SqlServer`, `Infrastructure.MongoDB`) **không bị thay đổi** — vẫn là 2
provider single-DB chạy độc lập.

## Luồng saga: ConfirmOrder / CancelOrder (đã confirmed)

`OrderWriteService` **không đổi** — vẫn gọi `Begin → writes → Commit` qua port `IUnitOfWork`.

1. **Begin** — `BeginTransactionAsync`: mở saga, ghi `saga_instances` trạng thái `Started`.
2. **Bước Mongo** (`Product.UpdateRange`, Compensatable): trừ kho **và** enroll bước (`RevertStock` +
   delta kho) trong **một** Mongo transaction → **commit ngay**. Hai thứ durable cùng nhau (atomic).
3. **Bước SQL** (`Order.Update`, Pivot): ghi order → **commit ngay**, rồi enroll một marker
   `ConfirmOrder`(Pivot) vào sổ cái = **điểm không quay lại**.
4. **Commit** — `CommitAsync`: mọi bước đã durable; chỉ **xoá** dòng `saga_instances` (saga thành công).
   - Nếu bước SQL ném exception (trước marker Pivot) → **Rollback**: `SagaCompensator` đọc `saga_instances`,
     replay `RevertStock` để cộng kho lại (retry tới khi được) → xoá sổ cái.

⇒ Mỗi DB commit độc lập tại bước của nó. Nếu crash **sau** khi SQL đã commit, sổ cái đã có marker Pivot →
recovery thấy Pivot thì **finalize, không revert** (khép khe "order Confirmed mà kho bị trả lại"). Khe dư
duy nhất là crash đúng giữa SQL-commit và lúc ghi marker Pivot — cực nhỏ, là cái giá cố hữu của polyglot
không-2PC khi đặt sổ cái ở Mongo.

## Rollback & retry

`SagaCompensator` dùng chung cho cả rollback in-request lẫn recovery:

- Replay từng bước **Compensatable** chưa compensate, **thứ tự ngược**.
- Mỗi compensation **retry tới `MaxRetries`** (backoff). Quá hạn → đặt saga `NeedsManualReview` (không
  nuốt lặng), recovery sẽ thử lại ở lần khởi động sau.
- Compensation **idempotent**: `MongoStockCompensationHandler` cộng kho lại **và** đánh dấu bước
  `Compensated` trong **cùng một** Mongo transaction, nên chạy 2 lần không bao giờ cộng kho gấp đôi.

## Crash-recovery

Nếu tiến trình **crash giữa saga**, các bước đã commit vẫn còn trong `saga_instances`.
`SagaRecoveryService` (BackgroundService, chạy 1 lần lúc khởi động — chỉ ở provider Hybrid) đọc mọi
saga chưa kết thúc (`Started`/`Compensating`/`NeedsManualReview`) và đưa cho **chính** `SagaCompensator`
để hoàn tác y như một lỗi lúc đang chạy. Mọi compensation idempotent nên recovery cũng chịu được crash.

## Chạy thử

1. `appsettings`: đặt `DatabaseProvider=Hybrid`. Cần cả `ConnectionStrings:SqlServerSaga` (DB riêng
   `SellingNewProduct_Saga`) lẫn `ConnectionStrings:MongoDB`. **Mongo phải là replica set** — bước Mongo
   dùng transaction thật (xem docs/mongo-replica-set.md, docker-compose.mongo-rs.yml).
2. Tạo schema SQL: `dotnet ef database update --context AppDbContext` trong project
   `Infrastructure.SqlServer.Saga`. (Không còn context/migration saga-log nào.)
3. Tạo Customer/Employee/Product (rơi xuống Mongo) + Order (rơi xuống SQL).
4. Confirm order → kho giảm ở Mongo, order `Confirmed` ở SQL, dòng `saga_instances` **bị xoá** (thành công).
5. **Demo compensation**: tạm cho bước SQL ném exception (ví dụ trong `SqlOrderWriteRepository.UpdateAsync`)
   rồi confirm → kho ở Mongo được **khôi phục** (RevertStock), SQL không đổi, sổ cái được dọn.

## Lưu ý: đọc xuyên DB (polyglot CQRS)

Read model của Order cần tên Customer/Employee (Mongo); read model của Customer/Employee cần thống kê
order (SQL). Không JOIN xuyên DB được nên hai phía enrich **in-memory** qua 2 port lá. Hai port lá chỉ
phụ thuộc context DB của chính mình ⇒ không tạo vòng phụ thuộc DI. Production nên thay bằng read model
denormalized (materialized view) cập nhật qua event.
