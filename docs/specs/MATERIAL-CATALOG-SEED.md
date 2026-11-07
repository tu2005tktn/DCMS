# Dữ liệu mẫu: danh mục dụng cụ/vật liệu (thiết lập chung)

Ngày: 06/11/2026. Liên quan: [SYSTEM-SETTINGS-MATERIAL-CATALOG.md](SYSTEM-SETTINGS-MATERIAL-CATALOG.md), [TREATMENT-CATALOG-SEED.md](TREATMENT-CATALOG-SEED.md).

## Mục tiêu

Ai lấy mã về và chạy theo hướng dẫn đều có sẵn **danh sách tên dụng cụ/vật liệu** ở màn thiết lập chung (nhóm và mục). Đây là dữ liệu mẫu cho môi trường phát triển.

## Phạm vi nạp

Dùng chung lệnh với dữ liệu dịch vụ/kế hoạch: `--seed-clinic-catalog` (chỉ Development). Lệnh nạp:

1. **Nhóm** (`MaterialCategory`): Dụng cụ cầm tay; Dụng cụ nội nha và mũi khoan; Vật liệu trám; Vật liệu nội nha; Thuốc tê và kim; Lấy dấu và phục hình; Vô trùng và bảo hộ; Chỉ khâu và phẫu thuật; Implant; Tẩy trắng.
2. **Dụng cụ/vật liệu** (`MaterialCatalogItem`): 46 mục, mỗi mục có nhóm và đơn vị dùng (`usageUnit`: cái, cây, viên, ống, g, mL, sợi...).

## Không nạp (đã bỏ 06/11/2026)

- **Nhập hàng** (`MaterialPurchase`), tồn kho, xuất dùng (`MaterialUsage`) và **công ty cung cấp mẫu**: không nạp. Người dùng tự ghi nhập/xuất khi chạy thử.
- Công ty labo và loại công việc labo: xem [LAB-CATALOG-SEED.md](LAB-CATALOG-SEED.md).

## Quy tắc dữ liệu

- **Không ghi đè, chạy lại không nhân đôi:** nhóm khớp theo tên (không phân biệt hoa thường), mục khớp theo (nhóm, tên). Mục đã có thì bỏ qua, không đổi đơn vị người dùng đã sửa.
- Không đổi lược đồ cơ sở dữ liệu, không migration, không sửa API.

## Cách chạy cho người khác

```powershell
& .\infrastructure\scripts\start-api.ps1 -Migrate -SeedDemo
& .\infrastructure\scripts\start-api.ps1 -SeedClinicCatalog
```

Lệnh in số nhóm và số mục đã thêm. Cần `--seed-demo` đã chạy trước.

## Kiểm thử

Test tích hợp trên schema cách ly: sau nạp có đủ nhóm và 46 mục có đơn vị dùng; không có lần nhập hàng, xuất dùng hay công ty nào được tạo; chạy lần hai không thêm gì và không đổi đơn vị đã sửa.
