# Danh sách bảng sau rút gọn

Cập nhật ngày 13/11/2026. Schema đã áp dụng có 57 bảng trong model EF và 58 bảng vật lý trong schema `public`. Bảng ngoài model là `__EFMigrationsHistory`. Các schema `dcms_test_*` là môi trường kiểm thử tách biệt.

Riêng tài khoản và phân quyền chỉ có `USER_ACCOUNT` và `ACCESS_GROUP`. Riêng đơn thuốc và danh mục thuốc chỉ có `PRESCRIPTION` và `CATALOG_MEDICINE`. Nhật ký và kết quả chống ghi trùng dùng hai kho chung cho toàn hệ thống.

Chi nhánh, lịch làm việc và lịch hẹn đã gộp về ba bảng `SCHEDULING_BRANCH`, `WORKING_SCHEDULE`, `APPOINTMENT`, dùng chung danh mục dịch vụ điều trị hiện có. Xem [hợp đồng lưu trữ và kiểm chứng](../specs/SCHEDULING-STORAGE-REVIEW.vi.md). Danh sách dưới đây phản ánh schema đã áp dụng.

Phần khám đã gộp về `VISIT` và `CLINICAL_DOCUMENT`. Phiếu khám và chỉ định nằm trong lượt khám, các lần đọc tài liệu nằm trong tài liệu. Snapshot bảng tham chiếu cũ nằm trong audit chuyển đổi dùng chung. Xem [thiết kế và kiểm chứng hai bảng khám](../specs/EXAMINATION-TWO-TABLES.vi.md).

Phần điều trị đã gộp về `TREATMENT_DOSSIER`, `TREATMENT_PLAN_ITEM`, `CLINICAL_ACTIVITY` và `TREATMENT_TEMPLATE`. Bản nháp nằm trong phiếu, các bước nằm trong hạng mục, hạng mục và phiên bản mẫu nằm trong mẫu; đánh giá, ghi chú và hướng dẫn tái khám dùng chung bảng quá trình. Sự kiện điều trị và tiến độ nằm trong `AUDIT_LOG`. Xem [thiết kế và kiểm chứng bốn bảng điều trị](../specs/TREATMENT-FOUR-TABLES.vi.md).

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
| `IN_APP_NOTIFICATION` | Thông báo hiển thị cho tài khoản và sự kiện chờ xử lý; phân biệt loại bản ghi để xử lý/gửi lại. |
| `NOTIFICATION_DELIVERY` | Lần gửi thông báo, số lần thử và kết quả gửi. |
| `STAFF_HANDLING_ITEM` | Việc cần nhân viên xử lý từ lịch hẹn và thông báo. |
| `VISIT` | Lượt đến khám, tiếp nhận, bác sĩ phụ trách, trạng thái, nội dung khám và kết luận; các chỉ định, kết quả và snapshot báo giá trong JSONB. |
| `CLINICAL_DOCUMENT` | Metadata tài liệu lâm sàng, liên kết tệp và trạng thái xử lý; các lần bác sĩ đọc tài liệu theo lượt khám trong JSONB. |
| `PRESCRIPTION` | Đơn thuốc, mẫu đơn và phiên bản đơn; danh sách thuốc nằm trong JSONB. |
| `CATALOG_MEDICINE` | Danh mục thuốc dùng chung, đơn vị và thông tin kê đơn. |
| `TREATMENT_DOSSIER` | Hồ sơ điều trị, bệnh nhân, chi nhánh, thông tin và phiên bản kế hoạch, trạng thái tổng thể; bản nháp phiếu và bản nháp chỉnh sửa kế hoạch trong JSONB. |
| `TREATMENT_PLAN_ITEM` | Các mục dịch vụ trong kế hoạch điều trị với giá đã thỏa thuận; các bước thực hiện và trạng thái tiến độ trong JSONB. |
| `TREATMENT_TEMPLATE` | Mẫu kế hoạch điều trị dùng lại; dòng dịch vụ, nhân sự và các phiên bản mẫu cũ trong JSONB. |
| `CLINICAL_ACTIVITY` | Quá trình điều trị phân theo loại bản ghi: hoạt động điều trị thực tế, phiếu đánh giá lâm sàng, ghi chú chăm sóc và hướng dẫn tái khám kèm liên kết lịch hẹn. |
| `CARE_TASK` | Công việc chăm sóc hoặc theo dõi bệnh nhân. |
| `CARE_CONTACT_ATTEMPT` | Các lần liên hệ bệnh nhân và kết quả. |
| `CARE_ESCALATION` | Chuyển cấp xử lý công việc chăm sóc. |
| `LAB_ORDER` | Phiếu labo theo hồ sơ điều trị. |
| `LAB_ORDER_STATUS_LOG` | Lịch sử trạng thái phiếu labo. |
| `LAB_WORK_TYPE` | Danh mục loại công việc labo. |
| `CATALOG_PROVIDER` | Danh mục nhà cung cấp dùng trong thiết lập danh mục hệ thống. |
| `PROVIDER` | Nhà cung cấp phục vụ nghiệp vụ chi phí và công nợ hiện có. |
| `PROVIDER_PAYABLE` | Khoản phải trả nhà cung cấp. |
| `FINANCIAL_ACCOUNT` | Tài khoản hoặc quỹ ghi nhận tài chính. |
| `FINANCE_PAYMENT_METHOD` | Danh mục phương thức thanh toán trong phân hệ tài chính. |
| `FINANCE_CHARGE` | Khoản tính phí dịch vụ hoặc phát sinh. |
| `EXAMINATION_FEE` | Phí khám gắn với lượt khám. |
| `FINANCE_INVOICE` | Hóa đơn và tổng tiền. |
| `FINANCE_INVOICE_LINE` | Các dòng dịch vụ hoặc khoản phí trên hóa đơn. |
| `FINANCE_ADVANCE` | Khoản tiền bệnh nhân ứng trước. |
| `FINANCE_ADVANCE_ALLOCATION` | Phân bổ khoản ứng trước vào nghĩa vụ thanh toán. |
| `FINANCE_REFUND` | Khoản hoàn tiền và lý do. |
| `PAYMENT_RECEIPT` | Phiếu thu tiền và thông tin xác nhận thu. |
| `PAYMENT_ALLOCATION` | Phân bổ tiền thu vào các khoản phí hoặc hóa đơn. |
| `RECEIPT_PRINT_EVENT` | Lịch sử in và in lại phiếu thu. |
| `BRANCH_RECEIPT_SEQUENCE` | Bộ đếm số phiếu thu theo chi nhánh. |
| `TREATMENT_DOSSIER_PAYMENT` | Khoản thanh toán ghi theo hồ sơ điều trị. |
| `TREATMENT_DOSSIER_PAYMENT_LINE` | Phân bổ khoản thanh toán hồ sơ vào các dòng điều trị. |
| `TREATMENT_PAYOS_ORDER` | Đơn thanh toán PayOS; lựa chọn dòng thanh toán nằm trong JSONB. |
| `MATERIAL_CATEGORY` | Nhóm vật tư. |
| `MATERIAL_CATALOG_ITEM` | Danh mục vật tư, đơn vị và định mức liên quan. |
| `MATERIAL_PURCHASE` | Lần nhập hoặc mua vật tư và chi phí. |
| `MATERIAL_USAGE` | Lượng vật tư đã sử dụng cho nghiệp vụ. |
| `CLINIC_SETTINGS_VERSION` | Các phiên bản cấu hình chung của phòng khám. |
| `REFERENCE_ENTRY` | Các mục danh mục tham chiếu hệ thống, phân loại theo loại danh mục. |
| `SYSTEM_CATALOG_CHANGE` | Lịch sử thay đổi danh mục hệ thống. |
| `SETTINGS_VERSION` | Kho phiên bản thiết lập vận hành cũ được giữ để tương thích dữ liệu. |
| `OPERATIONAL_REFERENCE_ENTRY` | Kho danh mục vận hành cũ được giữ để tương thích dữ liệu. |
| `AUDIT_LOG` | Nhật ký dùng chung cho các module, gồm người thao tác, thời điểm, thay đổi trước/sau, đổi/hủy lịch, xử lý yêu cầu hỗ trợ, sự kiện hồ sơ điều trị và sự kiện tiến độ điều trị. |
| `COMMAND_RECEIPT` | Kết quả lệnh dùng chung để xử lý gửi lại và chống ghi trùng. |
| `__EFMigrationsHistory` | Các phiên bản migration đã áp dụng vào database. |
