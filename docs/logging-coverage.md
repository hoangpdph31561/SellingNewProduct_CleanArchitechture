# Độ phủ Logging — "nhìn log biết chạy tới đâu, mất bao lâu, lỗi gì"

Trả lời thẳng câu hỏi: **KHÔNG phải mọi method đều tự viết log** — và **không nên** (spam + boilerplate). Thay vào đó dùng **cross-cutting** ở các ranh giới + **traces** cho chi tiết. Kết hợp lại thì đúng là "nhìn log/trace biết chạy tới đâu, bao lâu, lỗi gì".

## 3 tầng ghi nhận (mỗi request)

| Tầng | Ghi cái gì | Từ đâu | "Tới đâu / bao lâu / lỗi" |
|---|---|---|---|
| **HTTP** | 1 dòng/request: method, path, **status**, **elapsed** | `UseSerilogRequestLogging()` | tới đâu (endpoint) + bao lâu (tổng) |
| **Business op (mọi command/query)** | `→ Handling X`, `✓ Handled X in {ms}`, `✗ X failed/rejected after {ms}` | [TelemetryBehavior](../SellingNewProduct.Application/Common/Behaviors/TelemetryBehavior.cs) (MediatR pipeline) | tới đâu (X nào) + bao lâu (từng op) + lỗi (Warning/Error) |
| **Chi tiết (query DB, HTTP out)** | span + thời gian mỗi EF Core query / HttpClient call | OTel auto-instrument (traces) | tới đâu ở tầng DB + bao lâu mỗi query |

Mọi dòng log mang **TraceId/SpanId** ([TraceContextEnricher](../SellingNewProduct.API/Observability/TraceContextEnricher.cs)) → 1 request nối được log ↔ trace ↔ metric.

**Vì sao KHÔNG log trong từng domain service/method:** `TelemetryBehavior` bọc **mọi** command/query nên tự động có start/end/duration/exception cho toàn bộ nghiệp vụ; OTel EF Core đo **từng query** bên dưới. Thêm log tay vào mỗi method = trùng + nhiễu + bẩn Domain (Domain giữ thuần, không ref logging).

## Phân loại exception trong log

| Loại | Level | Ví dụ |
|---|---|---|
| Nghiệp vụ (mong đợi, 4xx) | **Warning** | `ValidationException`, `NotFoundException`(404), `ConflictException`(409), `DomainException`(400), `UnauthorizedException`(401) |
| Bất ngờ (bug, 5xx) | **Error** (kèm stack trace) | NullRef, DB down không fallback, lỗi lạ |

→ Lọc log `level=Error` = chỉ thấy **bug thật**, không lẫn 404/409 bình thường. `TelemetryBehavior` phân loại; `ExceptionHandlingMiddleware` log 500 ở boundary.

## Độ phủ theo thành phần (hiện trạng)

| Thành phần | Log gì | Trạng thái |
|---|---|---|
| API request in/out | method/path/status/elapsed | ✅ đủ (Serilog request logging) |
| Command/Query (Application → Domain service) | start/end/duration/exception | ✅ đủ (TelemetryBehavior) |
| EF Core query (SQL + Mongo) | mỗi query + thời gian | ✅ trace (không phải log text) |
| Outbox dispatcher | publish ok/fail + retry | ✅ (+ metric + activity ring buffer) |
| Saga (recovery, resilience/breaker) | warning/error khi trip/rollback | ✅ (lỗi + trạng thái) |
| Gateway (email/invoice/VNPay) | gọi + lỗi qua breaker | ✅ một phần |
| **Saga từng bước (progress)** | started → step recorded → committed / compensating → compensated | ✅ **đã thêm** (SagaUnitOfWork(Transaction) + MongoSagaStore + SagaCompensator) |
| **HttpClient ra ngoài** | span | ✅ trace |

## Log mẫu 1 request (dev, text)

```
[10:22:41 INF] TraceId=4bf92f... SellingNewProduct.Application.Requests → Handling PlaceOrderCommand
[10:22:41 DBG] TraceId=4bf92f... ... (EF Core: SELECT ... FROM Customers ...)   # trace, không phải log
[10:22:41 INF] TraceId=4bf92f... SellingNewProduct.Application.Requests ✓ Handled PlaceOrderCommand in 84.2 ms
[10:22:41 INF] TraceId=4bf92f... Serilog.AspNetCore.RequestLoggingMiddleware HTTP POST /api/orders responded 201 in 92.1 ms
```

Lỗi nghiệp vụ:
```
[10:23:05 WRN] TraceId=a1b2... ✗ ConfirmOrderCommand rejected after 12.4 ms: ConflictException — Not enough stock ...
```

Lỗi bất ngờ:
```
[10:24:10 ERR] TraceId=c3d4... ✗ SearchOrdersQuery FAILED after 5001.0 ms (SqlException)
System.Data.SqlClient.SqlException: Timeout expired ...
   at ...
```

## Saga — log từng bước (đã thêm)

Vòng đời saga giờ đọc được qua log (cùng `SagaId` xuyên suốt):

```
INF Saga 7f3a.. 'OrderWrite' started.
INF Saga 7f3a..: step 'DecreaseStock' (Compensatable) recorded.     # Mongo trừ kho (trước pivot)
INF Saga 7f3a..: step 'PublishOrderEvents' (RetryableForward) recorded.
INF Saga 7f3a.. committed (all steps succeeded).                     # qua pivot, xong

# Trường hợp rollback:
WRN Saga 9c1b.. rolling back: a step failed, compensating recorded steps.
INF Saga 9c1b..: step 'DecreaseStock' compensated.                  # hoàn kho
INF Saga 9c1b.. compensated (rolled back cleanly).
# hoặc:
ERR Saga 9c1b.. could NOT be compensated — parked for manual review.
```

Nguồn: `SagaUnitOfWork` (started) · `MongoSagaStore.StageStepAsync` (step recorded) · `SagaUnitOfWorkTransaction` (committed / rolling back / outcome) · `SagaCompensator` (per-step compensated + retry/fail). Kèm metric + sổ cái `saga_instances`.

## Còn thiếu / mở rộng

- **Structured properties**: có thể thêm `orderId`, `customerId` vào log scope (BeginScope) để filter theo entity.
- **Sampling** trace ở prod để giảm noise.

> Tóm lại: **business layer đã phủ đủ** (mọi command/query có start/end/duration/exception tự động). Infra nền phủ **lỗi + metric**; muốn soi tiến trình từng bước saga qua log text thì bổ sung log step (đang có metric + ledger thay thế).
