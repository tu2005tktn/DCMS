# Dữ liệu mẫu: công ty labo và loại công việc labo

Ngày: 06/11/2026. Liên quan: [LAB-ORDER.md](LAB-ORDER.md), [TREATMENT-CATALOG-SEED.md](TREATMENT-CATALOG-SEED.md).

## Mục tiêu

Ô "Cty cung cấp" và "Loại công việc labo" của Phiếu labo (trong Quá trình điều trị/dịch vụ) có sẵn lựa chọn cho các kế hoạch điều trị mẫu cần gửi xưởng. Dữ liệu mẫu cho môi trường phát triển, không phải bảng giá.

## Phạm vi nạp

Dùng chung lệnh `--seed-clinic-catalog` (chỉ Development), nạp sau dịch vụ/kế hoạch và danh mục vật liệu:

1. **Công ty labo** (`CatalogProvider`, loại `Lab`): `LAB-01` "Xưởng labo mẫu 1", `LAB-02` "Xưởng labo mẫu 2" (không dùng tên doanh nghiệp thật).
2. **Loại công việc labo** (`LabWorkType`), 7 loại mà kế hoạch điều trị mẫu cần, mã `LW-01..LW-07`: Mão sứ kim loại (Tooth), Mão toàn sứ Zirconia (Tooth), Mão trên Implant (Tooth), Cầu răng sứ kim loại (Tooth), Hàm tháo lắp nhựa (Jaw), Mặt dán sứ Veneer (Tooth), Hàm duy trì sau niềng (Jaw). Công ty mặc định luân phiên giữa hai xưởng mẫu.
3. **Đơn giá tham khảo và bảo hành để trống** (phòng khám tự nhập theo xưởng thật).

## Quy tắc

- Không ghi đè, chạy lại không nhân đôi: công ty khớp theo mã, loại công việc khớp theo tên (không phân biệt hoa thường). Mục đã có (kể cả đã sửa) thì bỏ qua.
- Tạo bằng tài khoản `dentist.demo`. Không đổi lược đồ, không migration, không sửa API.
- Không nạp phiếu labo, công nợ xưởng hay dữ liệu nhập/xuất.

## Kiểm thử

Test tích hợp: sau nạp có 2 công ty Lab và 7 loại công việc với đơn vị hợp lệ (Jaw/HalfJaw/Tooth/Piece/Set) và công ty mặc định là công ty Lab; chạy lần hai không thêm gì; đổi tên công ty không bị ghi đè.
