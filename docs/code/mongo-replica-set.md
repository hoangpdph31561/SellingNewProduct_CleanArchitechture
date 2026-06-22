# MongoDB Replica Set — tại sao và cách chạy

## Vì sao dự án cần replica set?

Phiên bản CQRS này dùng `IUnitOfWork` để gói nhiều thao tác ghi vào **một transaction**
(ví dụ confirm đơn hàng: trừ tồn kho `Products` + cập nhật trạng thái `Orders`). Trên
MongoDB, **multi-document transaction chỉ chạy được khi server ở chế độ replica set**
(hoặc sharded cluster). Chạy `mongod` standalone sẽ ném lỗi đại loại:

```
Transaction numbers are only allowed on a replica set member or mongos
```

Lý do kỹ thuật: cơ chế transaction của Mongo dựa trên **oplog** + **snapshot isolation**
(WiredTiger). Oplog chỉ tồn tại khi node là thành viên của một replica set. Standalone
không có oplog ⇒ không có commit/rollback nguyên tử cho nhiều document.

## Ưu / nhược điểm

**Ưu điểm**
- Mở khóa **transaction ACID** nhiều document/collection (cái dự án đang cần).
- **High availability**: primary chết thì bầu primary mới, app không sập.
- Đọc co giãn: có thể đọc từ secondary (`readPreference=secondary`) để giảm tải primary.
- **Change Streams** (`watch()`) để bắt sự kiện thay đổi — hữu ích cho đồng bộ về sau.
- `writeConcern: majority` đảm bảo độ bền dữ liệu trước khi xác nhận ghi.

**Nhược điểm**
- Vận hành phức tạp hơn (nhiều node, election, giám sát).
- **Replication lag**: đọc từ secondary có thể ra dữ liệu cũ (eventual consistency).
- Tốn tài nguyên hơn (gấp ~số node lần dữ liệu/RAM).
- Transaction trong Mongo đắt hơn SQL — nên giữ transaction ngắn, tránh lạm dụng.

## Cách chạy nhanh (1-node, đủ để thử transaction)

```bash
docker compose -f docker-compose.mongo-rs.yml up -d
```

Container `mongo-init` sẽ tự chạy `rs.initiate()`. Connection string trong
`appsettings.json` đã trỏ tới replica set:

```
mongodb://localhost:27017/?replicaSet=rs0&readPreference=primary
```

Đổi provider sang Mongo bằng cách đặt trong `appsettings.json` (hoặc biến môi trường):

```json
"DatabaseProvider": "MongoDB"
```

### Chạy thủ công không cần Docker

```bash
mongod --replSet rs0 --dbpath /data/db
# rồi trong mongosh:
rs.initiate({ _id: "rs0", members: [{ _id: 0, host: "localhost:27017" }] })
```

## Thử nghiệm (đúng yêu cầu của trainer)

1. **Standalone (KHÔNG replica set)** + `DatabaseProvider=MongoDB`: gọi
   `POST /api/orders/{id}/confirm` ⇒ kỳ vọng **lỗi transaction** → chứng minh standalone
   không làm transaction nhiều document được.
2. **Replica set bật**: gọi confirm ⇒ chạy được. Ép một lỗi giữa chừng (vd tồn kho không
   đủ) ⇒ kiểm tra **không collection nào bị ghi một nửa** (rollback toàn phần).

## Replica set 3 node (HA thật) — `docker-compose.mongo-rs-3node.yml`

Cho môi trường giống production hơn (1 PRIMARY + 2 SECONDARY, có **failover** tự động),
dùng file compose riêng ở thư mục gốc repo:

```bash
docker compose -f docker-compose.mongo-rs-3node.yml up -d
```

File này dựng 3 container `mongo1`/`mongo2`/`mongo3` (mỗi node một port **27017/27018/27019**)
và một container `mongo-init` chạy `rs.initiate()` với 3 thành viên, `mongo1` ưu tiên làm primary:

```js
rs.initiate({
  _id: "rs0",
  members: [
    { _id: 0, host: "mongo1:27017", priority: 2 },
    { _id: 1, host: "mongo2:27018", priority: 1 },
    { _id: 2, host: "mongo3:27019", priority: 1 }
  ]
})
```

### ⚠️ Bắt buộc: map tên host về localhost

Replica set **quảng bá thành viên theo `host:port`** trong `rs.initiate()`. Driver (app chạy
trên máy host) sau khi kết nối node đầu sẽ tự khám phá topology rồi **kết nối lại tới đúng các
địa chỉ được quảng bá** (`mongo1:27017`, `mongo2:27018`, `mongo3:27019`). Vì vậy host phải phân
giải được các tên đó. Thêm vào hosts file:

```
# Windows: C:\Windows\System32\drivers\etc\hosts  (mở bằng quyền Administrator)
# Linux/macOS: /etc/hosts
127.0.0.1 mongo1 mongo2 mongo3
```

> 💡 **Vì sao mỗi node một port khác nhau?** Địa chỉ quảng bá phải giống hệt nhau khi nhìn từ
> **trong** Docker (node này gọi node kia) và từ **ngoài** host (app gọi vào). Nếu cả 3 cùng
> dùng `27017`, ta không thể publish 3 container ra cùng cổng `27017` của host. Cho mỗi node một
> port riêng (khớp cả trong lẫn ngoài nhờ `--port` + map `port:port`) giải quyết trọn vẹn.

### Connection string (đặt vào `ConnectionStrings:MongoDB` của appsettings.json)

```
mongodb://mongo1:27017,mongo2:27018,mongo3:27019/?replicaSet=rs0&readPreference=primary
```

### Thử failover

```bash
docker stop snp-mongo1                 # hạ primary
docker exec -it snp-mongo2 mongosh --port 27018 --eval "rs.status()"   # mongo2/mongo3 bầu primary mới
```

App vẫn ghi/đọc được sau vài giây (driver tự chuyển sang primary mới) — đây là điều mà bản
single-node KHÔNG có.

## Cho SECONDARY gánh đọc (primary ghi / secondary đọc)

Tầng MongoDB dùng **hai context**: `MongoAppDbContext` (ghi, primary) và `MongoReadDbContext` (đọc,
secondary). Read repository inject context đọc; write repository + `MongoUnitOfWork` giữ primary.
**`appsettings.json` đã bật sẵn** hai connection string (`MongoDB` primary + `MongoDBRead`
secondaryPreferred). Cho cụm 3-node, đổi cả hai sang chuỗi 3-seed:

```jsonc
"ConnectionStrings": {
  "MongoDB":     "mongodb://mongo1:27017,mongo2:27018,mongo3:27019/?replicaSet=rs0&readPreference=primary",
  "MongoDBRead": "mongodb://mongo1:27017,mongo2:27018,mongo3:27019/?replicaSet=rs0&readPreference=secondaryPreferred"
}
```

- Thiếu `MongoDBRead` → đọc **fallback về primary** (nên single-node / chạy SQL không cần khai báo).
- `secondaryPreferred`: ưu tiên secondary, nhưng nếu không có secondary nào sống thì vẫn đọc primary
  (an toàn hơn `secondary` thuần).

**Kiểm chứng đọc đi vào secondary:** bật profiler trên một secondary rồi gọi một endpoint đọc:
```bash
docker exec -it snp-mongo2 mongosh --port 27018 --eval "db.getSiblingDB('SellingNewProduct').setProfilingLevel(2)"
# gọi GET /api/categories/search ... rồi xem:
docker exec -it snp-mongo2 mongosh --port 27018 --eval "db.getSiblingDB('SellingNewProduct').system.profile.find({op:'query'}).sort({ts:-1}).limit(3)"
```

> ⚠️ **Replication lag (eventual consistency):** secondary trễ hơn primary vài mili giây → vừa ghi xong
> đọc lại từ secondary có thể ra dữ liệu cũ. Hợp cho danh sách/báo cáo; với "đọc lại ngay cái vừa ghi"
> thì để `readPreference=primary` (hoặc không set `MongoDBRead`).
