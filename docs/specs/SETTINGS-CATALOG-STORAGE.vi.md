# Thiết lập và danh mục trong ba bảng

Ngày 13/11/2026. Yêu cầu của chủ dự án: rút gọn nhóm thiết lập và danh mục. Phương án được chọn giữ ba bảng, nhà cung cấp và xưởng labo vẫn là bảng riêng để khóa ngoại từ danh mục labo và lần nhập vật tư tiếp tục được database kiểm.

| Bảng | Dữ liệu |
| --- | --- |
| `CLINIC_SETTINGS_VERSION` | Các phiên bản thiết lập chung của phòng khám; phiên bản mới nhất là thiết lập đang áp dụng |
| `REFERENCE_ENTRY` | Danh mục phụ dùng chung: chẩn đoán, nguồn khách, nghề nghiệp, tiền sử, loại chăm sóc, loại chi, nhóm khách |
| `CATALOG_PROVIDER` | Nhà cung cấp vật tư và xưởng labo, gồm loại, địa chỉ, điện thoại, email |

## Bảng được bỏ

- `SYSTEM_CATALOG_CHANGE` chuyển vào kho receipt dùng chung `COMMAND_RECEIPT` với `ReceiptType = SystemCatalogChange`. Mỗi dòng giữ khóa gửi lại, người thực hiện, lệnh, ID bản ghi bị đổi, fingerprint, nội dung trước khi đổi, kết quả và thời điểm. Khóa gửi lại vẫn duy nhất trong loại receipt này. Lịch sử thay đổi của danh mục, thiết lập chung, danh mục vật tư và lần nhập vật tư đọc từ cùng các dòng này, theo ID bản ghi và thời điểm.
- `SETTINGS_VERSION` và `OPERATIONAL_REFERENCE_ENTRY` là hai bảng của module `/api/settings` cũ, đã ngừng ghi từ 30/10/2026 theo [S58-SETTINGS-MERGE.vi.md](S58-SETTINGS-MERGE.vi.md). Dữ liệu danh mục cũ đã được chép sang `REFERENCE_ENTRY` khi gộp. Hai bảng được bỏ; cấu trúc (cột, ràng buộc, index) và toàn bộ dòng của mỗi bảng được lưu thành một bản ghi `SettingsLegacyArchive` trong `AUDIT_LOG` để rollback dựng lại đúng như cũ.

Route, DTO, quyền, phân trang, lỗi phiên bản và quy tắc gửi lại của thiết lập chung, danh mục phụ, nhà cung cấp, danh mục vật tư, lần nhập vật tư và nhật ký thiết lập giữ nguyên. Kiểm quyền vẫn diễn ra trước khi trả lại kết quả của lần gửi lại.

## Chuyển dữ liệu

Một migration nối sau `RemoveFinanceStorage`, trong cùng giao dịch PostgreSQL:

1. Chép từng dòng `SYSTEM_CATALOG_CHANGE` vào `COMMAND_RECEIPT` với khóa lưu trữ mới, giữ nguyên các giá trị còn lại. Cột `Command` dùng chung được nới từ 40 lên 48 ký tự cho vừa lệnh danh mục.
2. Lưu snapshot của `SETTINGS_VERSION` và `OPERATIONAL_REFERENCE_ENTRY` vào `AUDIT_LOG`.
3. Kiểm số dòng đã chuyển và đã lưu khớp nguồn, rồi bỏ ba bảng.

Down dựng lại ba bảng và phục hồi dữ liệu với khóa, nội dung và ràng buộc cũ, rồi xóa các dòng receipt và snapshot đã thêm.

Mục tiêu: model EF từ 36 xuống 33 bảng, `public` từ 37 xuống 34 bảng.

## Kiểm chứng

- Up, Down và Up lại trên bản sao DB phát triển; đối chiếu số lượng và digest từng nguồn, kể cả ba bảng bị bỏ đọc lại từ nơi lưu mới.
- Kiểm thử lưu trữ: lịch sử và gửi lại danh mục sau chuyển đổi; snapshot hai bảng cũ phục hồi được qua rollback.
- Backend restore/build bằng .NET 10, bộ kiểm thử thiết lập/danh mục và toàn bộ integration, web build, model khớp migration, API health và đọc thiết lập chung, danh mục, nhà cung cấp, lịch sử trên DB phát triển.

## Kết quả triển khai ngày 13/11/2026

Migration `20261113081111_ConsolidateSettingsStorage` đã áp dụng vào DB phát triển. Model EF còn 33 bảng; `public` còn 34 bảng gồm `__EFMigrationsHistory`, khớp [danh sách bảng](../architecture/DATABASE-TABLE-INVENTORY.vi.md). Bản sao lưu `public` trước khi áp dụng nằm tại `.tools/settings-collapse/before-settings-collapse-20261113.dump`, được Git ignore, SHA-256 `9257d75b107321b0d22eb0f1a26fbdcbfa4212d00a68210b9d6543d70561ab96`.

Trên bản sao phục hồi từ DB phát triển, migration EF thật chạy Up, Down về `RemoveFinanceStorage` rồi Up lại. Cả 36 nguồn với 2.740 dòng khớp số lượng và digest ở cả ba bước, gồm 11 thay đổi danh mục đọc lại từ `COMMAND_RECEIPT`, 36 dòng danh mục cũ và bảng thiết lập cũ (trống) đọc lại từ snapshot. Sau Down, cột, ràng buộc và index của `COMMAND_RECEIPT`, `AUDIT_LOG` và ba bảng được dựng lại giống hệt bản gốc, không còn snapshot. Sau khi áp dụng vào DB phát triển, đối chiếu trực tiếp cả 36 nguồn đều khớp.

Backend restore/build bằng .NET 10 thành công; model EF khớp migration. Có 100 unit test đạt. Lượt integration toàn bộ chạy tuần tự đạt 509/511, 2 NFR tùy chọn skip, không lỗi. Hai test lưu trữ mới kiểm thay đổi danh mục và hai bảng cũ được giữ qua rollback, và khóa gửi lại vẫn duy nhất trong kho receipt chung. Test tương thích S58 đọc danh mục cũ từ snapshot. Web không thay đổi.

API trên DB phát triển: `GET /health` trả API và database healthy. Đăng nhập Owner demo, đọc thiết lập chung và lịch sử, danh mục chẩn đoán, nhà cung cấp, lịch sử thay đổi của một nhà cung cấp và một mục danh mục, nhật ký thiết lập, tùy chọn chẩn đoán và lần nhập vật tư đều thành công; số mục và số dòng lịch sử khớp DB. Phiên kiểm chứng đã đăng xuất, log API không có lỗi.
