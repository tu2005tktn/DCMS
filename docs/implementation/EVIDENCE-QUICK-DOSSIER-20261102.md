# Ngày kế hoạch và tạo phiếu nhanh — 02/11/2026

Đã triển khai theo TREATMENT-QUICK-CREATE.md. TreatmentSheet sắp projection kết hợp items/server drafts theo ngày giảm dần, không ngày cuối, plannedOrder/ID giữ tie; editor ở vị trí ngày đã lưu, hàng mới cuối. Không ghi lại thứ tự nghiệp vụ. Tạo phiếu dùng create-context + quick API, bác sĩ suy từ actor, branch inline một tự chọn/nhiều để trống, không Modal và không navigate. Response được pin/dedupe và mở đúng ID ngay cả khi danh sách cập nhật chậm; lỗi giữ branch/requestKey, khóa bấm lặp. Ghi chú/hàng chưa lưu có cảnh báo bỏ trước tạo. Route dossier detail giữ nguyên; diagnosis trống hiện Chưa ghi nhận khi mở detail.

API tạo đầy đủ/quick dùng chung CreateCore, giữ bắt buộc diagnosis cho create đầy đủ, quick lưu chuỗi rỗng. Namespace QuickCreate riêng, advisory transaction lock trước replay, patient active/branch/role kiểm trước replay; nguồn khám tiếp tục kiểm đúng patient/branch/version và examination confirmed như cũ. Không sửa schema hoặc tạo migration. Không tạo dữ liệu thử trong dcms_dev.

Backend solution build đạt 0 warning/error. 21 integration test TreatmentSheetV25Tests/TreatmentDossierApiTests đạt trên database riêng `dcms_quick_verify_20261102_a7c89102`; đã dọn database. Ca mới kiểm context, doctor đúng actor, phiếu trống, concurrent retry cùng ID, key khác payload, sai branch, create đầy đủ còn chặn diagnosis rỗng, archived bị chặn kể cả replay và không Appointment/actual. 79 unit test đạt.

Web build cuối đạt, còn warning SignalR/chunk size cũ. Lượt UI cuối đủ bộ có 16 ca đạt và một ca retry tạo nhanh timeout do locator exact-name khi icon loading đổi tên truy cập. Đã thêm aria-label ổn định cho nút và cập nhật test; chạy lại riêng ca tạo nhanh 1/1 đạt (7.7s). Không tuyên bố một lượt 17/17. Các ca sort/edit position, áp kế hoạch, nhập tay, actual/menu/material và responsive390/768/1440 đã đạt; lỗi tràn ban đầu do dòng branch/nút tạo đã sửa flex-wrap và các ca responsive chạy lại đạt.

API chạy lại bản build đã kiểm tra, health api/database healthy; Web HTTP200. Không migration hoặc seed development, chưa chạy Git/commit/push. Kiểm chức năng backend dựa PostgreSQL cách ly và Web dùng API mock; chưa tạo phiếu thật bằng tài khoản người dùng để nghiệm thu development.

Test tay: đăng nhập Dentist → bệnh nhân → Điều trị & Thanh toán → chọn Chi nhánh nếu có nhiều → Tạo phiếu mới. Phiếu trống mở ngay trong tab hiện tại. Thêm hạng mục có ngày khác nhau/không ngày để kiểm sắp xếp; sửa ngày và ✓ mới đổi vị trí.
