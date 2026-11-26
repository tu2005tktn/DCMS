# Dữ liệu mẫu dạng SQL cho Docker: dịch vụ, kế hoạch điều trị, vật liệu, labo

Ngày: 10/11/2026. Liên quan: [TREATMENT-CATALOG-SEED.md](TREATMENT-CATALOG-SEED.md), [MATERIAL-CATALOG-SEED.md](MATERIAL-CATALOG-SEED.md), [LAB-CATALOG-SEED.md](LAB-CATALOG-SEED.md).

## Mục tiêu

Người chỉ chạy PostgreSQL bằng Docker (không muốn chạy lệnh `--seed-clinic-catalog` của API) có thể nạp **đúng bộ dữ liệu mẫu** đã mô tả ở ba tài liệu trên bằng một tệp SQL: danh mục dịch vụ, 17 kế hoạch điều trị mẫu, nhóm và mục dụng cụ/vật liệu, công ty và loại công việc labo. Đây là dữ liệu mẫu cho môi trường phát triển, không phải bảng giá hay khuyến cáo y khoa.

## Phạm vi

Tệp `infrastructure/seed/clinic-catalog.seed.sql` (sinh từ cùng dữ liệu của các seeder C#, nội dung trùng khớp):

| Bảng | Nội dung |
|---|---|
| `MATERIAL_CATEGORY`, `MATERIAL_CATALOG_ITEM` | 10 nhóm, 46 mục dụng cụ/vật liệu |
| `SCHEDULING_SERVICE` | 49 dịch vụ của chi nhánh demo |
| `TREATMENT_TEMPLATE`, `TREATMENT_TEMPLATE_ITEM` | 17 kế hoạch `ItemPlan` và các dòng |
| `CATALOG_PROVIDER`, `LAB_WORK_TYPE` | 2 công ty labo `LAB-01/02`, 7 loại công việc `LW-01..07` |

Không nạp nhập hàng, tồn kho, xuất dùng, phiếu labo hay dữ liệu bệnh nhân.

## Quy tắc

- **Điều kiện trước:** cơ sở dữ liệu đã có schema (`start-api.ps1 -Migrate`) và dữ liệu demo cơ bản (`-SeedDemo`: chi nhánh "S09 - Chi nhánh thử nghiệm", tài khoản `dentist.demo`). Thiếu một trong hai thì tệp dừng bằng lỗi rõ ràng và không ghi gì.
- **Không phụ thuộc ID:** chi nhánh và người tạo được tìm theo tên; dịch vụ trong dòng kế hoạch tìm theo (chi nhánh, tên); nhóm theo tên; công ty theo mã.
- **Idempotent, không ghi đè:** khóa tự nhiên giống các seeder (dịch vụ theo chi nhánh+tên; kế hoạch theo chi nhánh+tên chưa xóa; nhóm theo tên không phân biệt hoa thường; mục theo nhóm+tên; công ty theo mã; loại công việc theo tên không phân biệt hoa thường). Mục đã có thì bỏ qua. Chạy lại không nhân đôi.
- Một giao dịch duy nhất: lỗi giữa chừng thì không nạp gì.
- Chỉ dùng cho Development; không chạy trên dữ liệu thật.
- Không đổi lược đồ, không migration, không sửa API.

## Cách chạy

Từ thư mục gốc, với container `postgres` của `infrastructure/docker/compose.yaml`:

```powershell
& .\infrastructure\scripts\import-clinic-catalog.ps1
```

Script đọc `POSTGRES_USER`/`POSTGRES_DB` từ `.env` và chạy tệp SQL qua `docker compose exec` (không in mật khẩu). Có thể chạy tay: `docker compose --env-file .env -f infrastructure/docker/compose.yaml exec -T postgres psql -U <user> -d <db> -v ON_ERROR_STOP=1 < infrastructure/seed/clinic-catalog.seed.sql`.

## Kiểm thử

Trên cơ sở dữ liệu sạch đã `-Migrate -SeedDemo`: sau khi chạy tệp, số bản ghi đúng (10/46/49/17/2/7) và mỗi kế hoạch có ít nhất một dòng; chạy lần hai không thêm gì; kết quả giống lệnh `--seed-clinic-catalog`; thiếu chi nhánh demo thì báo lỗi và không ghi gì.
