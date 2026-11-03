# Kế hoạch điều trị và CRUD hạng mục — 02/11/2026

## Điều chỉnh bác sĩ/phụ tá/ghi chú tùy chọn
Theo yêu cầu mới, item-plan cho dentistId/dentistName null; bác sĩ allowClear, không tự gán tài khoản đang đăng nhập và không kiểm required. Phụ tá và note tiếp tục tùy chọn. Có thể chọn phụ tá độc lập hoặc xóa lựa chọn bác sĩ/phụ tá/ghi chú khi sửa. Nếu ID được cung cấp vẫn kiểm nguồn/branch như trước; không đổi nhân sự actual hoặc luồng phiếu.

Backend solution build đạt 0 warning/error; Web build đạt (warning SignalR/chunk size có sẵn). 6/6 ItemPlanApiTests PostgreSQL cách ly đạt, bổ sung tạo rỗng nhân sự/note, round-trip, gán rồi bỏ chọn độc lập; 8/8 UI mock item-plans đạt (36.2s) gồm ca thêm khi ba trường rỗng và bỏ chọn bác sĩ khi sửa. Database test riêng `dcms_itemplans_optional_20261102_27b1f94a`; không tạo dữ liệu thử dcms_dev. Chưa chạy lại 79 unit test hoặc bộ progression sau thay đổi nullable này; các con số ở mục trước là lượt trước.

Migration mới `20261102070713_ItemPlanOptionalDentist` chỉ đổi DentistId/DentistName của TREATMENT_TEMPLATE_ITEM thành nullable; giữ dữ liệu hiện có, chưa áp development. API development đang tạm dừng để build; cần phê duyệt riêng migration và chạy lại. Chưa chạy Git.

Sau phê duyệt riêng: backup đã kiểm bằng pg_restore --list tại `C:\Users\khong\AppData\Local\DCMS\backups\dcms-before-optional-dentist-2c48e992650f4122b88bb51b97faa329.dump` (9.384.682 bytes; SHA256 `15B515BE688945A95E4D6CF4F611CDC980552BA2DED4CD904793F369D7DE08B1`). Đã áp đúng `20261102070713_ItemPlanOptionalDentist`; history và information_schema/public xác nhận cả hai cột nullable=YES. API đã chạy lại port5080 không seed; không sửa nội dung các hạng mục sẵn có hoặc tạo dữ liệu thử development. Trạng thái chờ migration phía trên là lịch sử trước phê duyệt; database test cách ly đã được dọn. Chưa chạy Git.

## Đã triển khai
- Tái dùng TreatmentTemplate, phân biệt LegacySteps/ItemPlan và xóa mềm; bảng con TreatmentTemplateItem có snapshot danh mục, răng FDI nhiều mã, phút, text note, ngày, nhân sự và chi phí VND nguyên. Không áp dụng vào dossier.
- API `/api/treatment-templates/item-plans` list/search/page/options/create/detail/rename/delete và CRUD items; role/branch active kiểm trước replay, chung transaction advisory lock/receipt progression, namespace riêng, expectedVersion và audit before/after. Giữ nguồn inactive không đổi được phép sửa các trường khác; chọn mới phải active/đúng branch. Chi phí là tổng hàng, không nhân răng.
- Menu/route mới giữ `/treatment-templates`; đặt tên trước, kế hoạch rỗng, bảng nhập tại chỗ ✓ lưu/× hủy, xác nhận xóa, dirty guard khi link/reload/đổi branch, không autosave. Lỗi giữ form, MSG111 có đọc/so sánh bản máy chủ trước retry; chỉ preview giá khi chọn service. Bảng có cuộn ngang nội bộ trên mobile. Mẫu cũ giữ route `/treatment-templates/legacy` và bộ test sao chép vào item Agreed.
- Migration `20261102064519_TreatmentItemPlans` chỉ thêm 2 cột root và bảng con/FK restrict/index. Không seed/backfill/chuyển dữ liệu bệnh nhân. Chỉ migration mới này Pending trên dcms_dev theo EF migrations list.

## Kiểm chứng
- .NET10 restore + solution build: đạt, 0 warning/error ở lượt cuối. Lượt build đầu API cũ khóa DLL; đã dừng API do chính phiên này khởi động để build lại, không thay database development.
- 79/79 unit test backend đạt.
- 8/8 integration test đạt: 5 ItemPlanApiTests mới và 3 TreatmentProgressionApiTests hồi quy. Chạy database riêng `dcms_itemplans_verify_20261102_83f1a6d2`, mỗi fixture thêm schema riêng. Kiểm CRUD/soft delete/snapshot/không side effect Patient-dossier-actual-Appointment-reservation-charge-payment, retry/cạnh tranh/authorization trước replay, validation, loại định nghĩa và migration có dữ liệu Legacy trước nâng cấp. HasPendingModelChanges=false.
- Web build cuối đạt; còn warning SignalR annotation và bundle >500KB có sẵn.
- 8/8 Playwright mock đạt (29.3s): 7 ca item plans CRUD/validation/network/key/MSG111/responsive/role/list retry và 1 ca legacy map/apply. Kiểm 390/768/1440, bàn phím, không tràn document, chỉ bảng cuộn ngang. Đã xem ảnh synthetic `.artifacts/item-plans-390.png` và `item-plans-1440.png`.
- Các lượt thăm dò phát hiện min-width mặc định của grid card gây tràn mobile (đã sửa CSS), locator Ant Select bị trùng nhãn hoặc bị selection text che (test dùng role/keyboard), fixture stale trả version mới quá sớm và legacy test đổi origin làm mất session (đã sửa fixture/test, không bỏ kiểm tra sản phẩm).

## Trạng thái bàn giao
Chưa áp migration vào dcms_dev; API development đã tạm dừng để build và cần chạy lại sau khi được duyệt migration. Web vẫn chạy Vite. Chưa tạo dữ liệu thử trong dcms_dev, chưa nghiệm thu CRUD browser với API development thật. Bằng chứng giao diện dùng API mock; backend dùng PostgreSQL cách ly. Không chạy Git/commit/push. Các sửa đổi đăng ký/Google/customer có sẵn giữ nguyên.

Test thủ công sau migration: đăng nhập Dentist → Kế hoạch điều trị → chọn chi nhánh → Tạo kế hoạch → nhập tên/Lưu → + Thêm hạng mục → chọn dịch vụ/bác sĩ, nhập răng/phút/text/tiền → ✓ → tải lại/Mở → Sửa/×/Xóa; thử Mẫu tiến trình cũ. Ngày/phút/răng được để trống, không có SL/giảm giá/Thực hiện ở bảng mới.

## Development sau phê duyệt riêng
Người dùng đã duyệt sao lưu, áp migration và chạy API. Đã tạo backup ngoài Git tại `C:\Users\khong\AppData\Local\DCMS\backups\dcms-before-item-plans-2d02f10c89da40d488ab3f45e2479325.dump` (8.947.783 bytes; SHA256 `E74D52CA23B03DE0EFBE975B89E03D5EA0BF7CCEC4B169E87007C757D812DE19`), kiểm archive thành công bằng pg_restore --list trước khi áp.

Đã áp đúng `20261102064519_TreatmentItemPlans` bằng EF database update có target; lịch sử migration xác nhận bản ghi. information_schema/public xác nhận DefinitionKind NOT NULL default LegacySteps và DeletedAt nullable. Bảng TREATMENT_TEMPLATE_ITEM có 0 dòng ngay sau migration; không seed hoặc tạo hạng mục/hồ sơ thử. API được chạy lại bằng bản build đã kiểm tra, không kèm migrate/seed. Chưa chạy Git. Các mục chờ migration/API ở trên là trạng thái trước phê duyệt, đã được thay thế bởi mục này; kiểm CRUD chức năng vẫn dựa trên PostgreSQL cách ly và UI mock, chưa nghiệm thu browser CRUD với tài khoản người dùng trên dcms_dev.
