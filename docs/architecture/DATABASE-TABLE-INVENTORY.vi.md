# Danh sách bảng sau rút gọn

Cập nhật ngày 13/11/2026. Schema đã áp dụng có 31 bảng trong model EF và 32 bảng vật lý trong schema `public`. Bảng ngoài model là `__EFMigrationsHistory`. Các schema `dcms_test_*` là môi trường kiểm thử tách biệt.

Riêng tài khoản và phân quyền chỉ có `USER_ACCOUNT` và `ACCESS_GROUP`. Riêng đơn thuốc và danh mục thuốc chỉ có `PRESCRIPTION` và `CATALOG_MEDICINE`. Nhật ký và kết quả chống ghi trùng dùng hai kho chung cho toàn hệ thống.

Chi nhánh, lịch làm việc và lịch hẹn đã gộp về ba bảng `SCHEDULING_BRANCH`, `WORKING_SCHEDULE`, `APPOINTMENT`, dùng chung danh mục dịch vụ điều trị hiện có. Xem [hợp đồng lưu trữ và kiểm chứng](../specs/SCHEDULING-STORAGE-REVIEW.vi.md). Danh sách dưới đây phản ánh schema đã áp dụng.

Phần khám đã gộp về `VISIT` và `CLINICAL_DOCUMENT`. Phiếu khám và chỉ định nằm trong lượt khám, các lần đọc tài liệu nằm trong tài liệu. Snapshot bảng tham chiếu cũ nằm trong audit chuyển đổi dùng chung. Xem [thiết kế và kiểm chứng hai bảng khám](../specs/EXAMINATION-TWO-TABLES.vi.md).

Phần điều trị đã gộp về `TREATMENT_DOSSIER`, `TREATMENT_PLAN_ITEM`, `CLINICAL_ACTIVITY` và `TREATMENT_TEMPLATE`. Bản nháp nằm trong phiếu, các bước nằm trong hạng mục, hạng mục và phiên bản mẫu nằm trong mẫu; đánh giá, ghi chú và hướng dẫn tái khám dùng chung bảng quá trình. Sự kiện điều trị và tiến độ nằm trong `AUDIT_LOG`. Xem [thiết kế và kiểm chứng bốn bảng điều trị](../specs/TREATMENT-FOUR-TABLES.vi.md).

Chăm sóc bệnh nhân chỉ còn `CARE_TASK`; labo còn phiếu labo `LAB_ORDER` và danh mục labo `LAB_WORK_TYPE`. Phân hệ tài chính đã được gỡ cùng 18 bảng, sẽ đặc tả và thêm lại sau. Xem [chăm sóc, labo và tài chính](../specs/CARE-LAB-FINANCE-STORAGE.vi.md).

Thiết lập và danh mục còn `CLINIC_SETTINGS_VERSION`, `REFERENCE_ENTRY`, `CATALOG_PROVIDER`; lịch sử thay đổi danh mục nằm trong kho receipt dùng chung. Xem [thiết lập và danh mục trong ba bảng](../specs/SETTINGS-CATALOG-STORAGE.vi.md).

Thông báo chỉ còn `IN_APP_NOTIFICATION`; lượt gửi và việc nhân viên xử lý là các loại dòng trong bảng. Nhóm bệnh nhân và gia đình giữ bốn bảng. Xem [thông báo trong một bảng](../specs/NOTIFICATION-STORAGE.vi.md).

| Bảng | Lưu trữ |
| --- | --- |
| `USER_ACCOUNT` | Tài khoản, mật khẩu băm, thông tin nhân viên, vai trò, nhóm được gán, chi nhánh, claims, Google, phiên đăng nhập, refresh token, xác minh email và khôi phục. |
| `ACCESS_GROUP` | Nhóm quyền động và danh sách quyền; bản ghi vai trò hệ thống dùng chung bảng. |
| `PATIENT_PROFILE` | Thông tin hồ sơ bệnh nhân, liên hệ, tiền sử và trạng thái lưu trữ. |
| `PATIENT_ACCOUNT_LINK` | Yêu cầu liên kết tài khoản với hồ sơ bệnh nhân và kết quả duyệt. |
| `CUSTOMER_FAMILY_MEMBER` | Người thân do tài khoản khai báo. |
| `FAMILY_RECORD_ACCESS` | Quyền xem hồ sơ giữa tài khoản và người thân. |
| `SCHEDULING_BRANCH` | Chi nhánh, thông tin liên hệ, nhân sự, ghế, metadata logo, cấu hình đặt lịch và ngân hàng. |
| `SCHEDULING_SERVICE` | Danh mục dịch vụ điều trị dùng chung cho điều trị và đặt lịch; gồm thời lượng, giá tham chiếu, chi nhánh và điều kiện cho khách tự đặt. |
| `WORKING_SCHEDULE` | Lịch tuần cũ và các lần công bố lịch lặp/ngoại lệ; giữ ngày hiệu lực, phiên bản và các khoảng giờ JSONB. |
| `APPOINTMENT` | Lịch hẹn và giữ chỗ dùng chung dòng; gồm người đặt/người khám, nhân sự, ghế, dịch vụ, thời gian, trạng thái, yêu cầu hỗ trợ và kế hoạch nhắc lịch. |
| `IN_APP_NOTIFICATION` | Thông báo cho tài khoản, sự kiện chờ gửi của lịch hẹn, từng lượt gửi theo kênh (trạng thái, số lần thử, lỗi) và việc nhân viên cần xử lý khi gửi thất bại; phân biệt bằng loại dòng. |
| `VISIT` | Lượt đến khám, tiếp nhận, bác sĩ phụ trách, trạng thái, nội dung khám và kết luận; các chỉ định, kết quả và snapshot báo giá trong JSONB. |
| `CLINICAL_DOCUMENT` | Metadata tài liệu lâm sàng, liên kết tệp và trạng thái xử lý; các lần bác sĩ đọc tài liệu theo lượt khám trong JSONB. |
| `PRESCRIPTION` | Đơn thuốc, mẫu đơn và phiên bản đơn; danh sách thuốc nằm trong JSONB. |
| `CATALOG_MEDICINE` | Danh mục thuốc dùng chung, đơn vị và thông tin kê đơn. |
| `TREATMENT_DOSSIER` | Hồ sơ điều trị, bệnh nhân, chi nhánh, thông tin và phiên bản kế hoạch, trạng thái tổng thể; bản nháp phiếu và bản nháp chỉnh sửa kế hoạch trong JSONB. |
| `TREATMENT_PLAN_ITEM` | Các mục dịch vụ trong kế hoạch điều trị với giá đã thỏa thuận; các bước thực hiện và trạng thái tiến độ trong JSONB. |
| `TREATMENT_TEMPLATE` | Mẫu kế hoạch điều trị dùng lại; dòng dịch vụ, nhân sự và các phiên bản mẫu cũ trong JSONB. |
| `CLINICAL_ACTIVITY` | Quá trình điều trị phân theo loại bản ghi: hoạt động điều trị thực tế, phiếu đánh giá lâm sàng, ghi chú chăm sóc và hướng dẫn tái khám kèm liên kết lịch hẹn. |
| `CARE_TASK` | Công việc chăm sóc hoặc theo dõi bệnh nhân; các lần liên hệ và các lần chuyển cấp xử lý trong JSONB. |
| `LAB_ORDER` | Phiếu labo theo hoạt động điều trị: xưởng, loại công việc, răng, màu, ngày gửi/nhận/giao, trạng thái và phí labo; lịch sử trạng thái trong JSONB. |
| `LAB_WORK_TYPE` | Danh mục labo: loại công việc theo xưởng, đơn vị, giá tham khảo và bảo hành. |
| `CATALOG_PROVIDER` | Danh mục nhà cung cấp dùng trong thiết lập danh mục hệ thống. |
| `MATERIAL_CATEGORY` | Nhóm vật tư. |
| `MATERIAL_CATALOG_ITEM` | Danh mục vật tư, đơn vị và định mức liên quan. |
| `MATERIAL_PURCHASE` | Lần nhập hoặc mua vật tư và chi phí. |
| `MATERIAL_USAGE` | Lượng vật tư đã sử dụng cho nghiệp vụ. |
| `CLINIC_SETTINGS_VERSION` | Các phiên bản thiết lập chung của phòng khám; phiên bản mới nhất là thiết lập đang áp dụng. |
| `REFERENCE_ENTRY` | Các mục danh mục tham chiếu hệ thống, phân loại theo loại danh mục. |
| `AUDIT_LOG` | Nhật ký dùng chung cho các module, gồm người thao tác, thời điểm, thay đổi trước/sau, đổi/hủy lịch, xử lý yêu cầu hỗ trợ, sự kiện hồ sơ điều trị và sự kiện tiến độ điều trị. Giữ cả snapshot cấu trúc và dữ liệu của hai bảng thiết lập cũ `SETTINGS_VERSION`, `OPERATIONAL_REFERENCE_ENTRY` để rollback. |
| `COMMAND_RECEIPT` | Kết quả lệnh dùng chung để xử lý gửi lại và chống ghi trùng. Gồm cả lịch sử thay đổi danh mục, thiết lập chung và vật tư (`SystemCatalogChange`). |
| `__EFMigrationsHistory` | Các phiên bản migration đã áp dụng vào database. |
