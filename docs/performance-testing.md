# Performance Testing — Latency / Throughput / Error Rate

Đo 3 chỉ số cốt lõi cho API, tìm **breaking point**, và map dấu hiệu bottleneck về đúng đặc điểm của dự án.

## 3 chỉ số

| Chỉ số | Định nghĩa | Ngưỡng dùng trong dự án |
|---|---|---|
| **Latency** | thời gian request→response; xem **p95/p99** (không phải average — average giấu outlier) | p95 < 500ms, p99 < 1000ms |
| **Throughput** | request thành công / giây (RPS) | càng cao càng tốt; giảm khi tăng tải = bottleneck |
| **Error Rate** | % request lỗi | < 1% ổn; 1–5% điều tra; > 5% nghiêm trọng |

## Công cụ

- **k6** (dùng ở đây) — script JS, nhẹ, hợp CI. Script sẵn: [perf/k6/load-test.js](../perf/k6/load-test.js).
- **NBomber** — load test viết bằng C#, hợp hệ .NET (thêm project console riêng nếu cần).
- **JMeter** — GUI, nhiều protocol.

### Chạy k6

```bash
# Cài (Windows)
winget install k6 --source winget

# Chạy (API đang chạy http://localhost:5044)
k6 run perf/k6/load-test.js
k6 run -e BASE_URL=http://localhost:5044 -e EMAIL=admin@shop.local -e PASSWORD=Secret123! perf/k6/load-test.js

# Xuất kết quả
k6 run --out json=perf/k6/results.json --summary-export=perf/k6/summary.json perf/k6/load-test.js
```

Script: login lấy JWT → ramp tải đọc (products list + report best-selling) → có `thresholds` (p95<500, p99<1000, error<1%) nên **fail run** nếu vượt (dùng làm cổng CI). Chỉnh `stages.rate` để đẩy tới breaking point.

## Tìm breaking point (quy tắc vàng)

Tăng dần tải, ghi p95 + error rate mỗi mức; điểm latency tăng **phi tuyến** hoặc error > 1% = ngưỡng.

```
RPS   | p95    | Error  | Trạng thái
------|--------|--------|------------
100   |  45ms  | 0.00%  | ✅
500   |  98ms  | 0.00%  | ✅
800   | 320ms  | 0.80%  | ⚠️
1000  |1840ms  | 9.67%  | ❌ bottleneck
```

## Bottleneck — map về ĐẶC ĐIỂM dự án này

| Dấu hiệu | Nguyên nhân chung | Cụ thể trong dự án |
|---|---|---|
| Latency vọt khi tăng tải | thread pool cạn, DB connection pool đầy | `.Result`/`.Wait()` (sync-over-async) — dự án tránh; DbContext pool nhỏ |
| Throughput chạm trần | CPU/RAM bão hoà | build lô lớn CPU-bound → `Parallel` ([BuildManyAsync](../SellingNewProduct.Domain/Services/ProductWriteService.cs)) |
| p99 ≫ p95 (tail cao) | GC pause, lock contention, slow query | đọc chéo store (SQL↔Mongo) — đã bọc breaker/bulkhead |
| Error rate tăng theo tải | timeout, 503 | circuit breaker (Polly) fail-fast + fallback |
| N+1 query | loop query | đã sửa: SKU check gộp 1 query (`GetExistingSkusAsync`); report dùng `Task.WhenAll` cross-store |

### Checklist điều tra (.NET)

```
[ ] CPU > 90%?        -> tối ưu async/parallel, scale
[ ] RAM đầy?          -> memory leak / tăng heap; DbContext giữ nhiều entity (AsNoTracking cho read, chunk cho write)
[ ] DB slow query?    -> index, connection pool (MaxPoolSize); tránh N+1 (batch/Include)
[ ] Thread pool cạn?  -> ThreadPool.GetAvailableThreads()==0; bỏ .Result; SetMinThreads
[ ] External chậm?    -> đo riêng; timeout + circuit breaker (Polly) — đã có ở gateway + cross-store
[ ] DbContext?        -> KHÔNG thread-safe: đừng fan-out cùng context; batch (xem async-programming.md)
```

## Quan sát trong lúc test

Chạy k6 **song song** với observability stack ([observability.md](observability.md)): xem Grafana/Prometheus lúc tải cao:
- `app.request.duration` p95/p99 theo `request.type` → command/query nào chậm.
- runtime metrics: GC, **threadpool queue length** → bắt đúng starvation.
- `outbox.publish.duration` → messaging có nghẽn không.

→ Kết hợp: k6 tạo tải, OTel/Prometheus **chỉ ra chỗ gãy**.
