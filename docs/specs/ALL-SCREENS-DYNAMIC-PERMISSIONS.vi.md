# Phân quyền động cho toàn bộ màn hình nhân viên

Yêu cầu 02/11/2026: mở rộng phân quyền động cho tất cả màn hình hiện có, gồm phần điều trị. Thay thế các giới hạn phạm vi ở bản đầu S58-DYNAMIC-ACCESS-GROUPS, không chỉ thêm checkbox hoặc ẩn menu.

## Nguyên tắc

- Mỗi màn nghiệp vụ có quyền đọc; thao tác ghi, xuất/in đã triển khai có quyền con. Kiểm cả route/menu/nút trên web và endpoint API. Endpoint nghiệp vụ chưa được ánh xạ phải bị phát hiện bằng kiểm tra inventory; không có đường dẫn trực tiếp vượt kiểm tra.
- Quyền động là điều kiện bổ sung cho vai trò chuyên môn, nhân viên phụ trách, membership chi nhánh, trạng thái dữ liệu, version/requestKey/audit hiện có. Không cấp nghề nha sĩ bằng checkbox, không xóa posted ledger hoặc bỏ các điều kiện hoàn tất lâm sàng.
- Owner luôn có các quyền đúng vai trò hiện có để quản trị; không tự kế thừa Admin/Assistant. Staff chưa gán nhóm giữ hành vi nghiệp vụ hiện hành. Nhân viên đã gán nhóm chỉ có grant được thiết lập; các module trước đây lọt qua kiểm tra role-only nay phải được cấp rõ ràng.
- Phiên xác thực và hồ sơ tài khoản của chính mình luôn dùng được để đăng xuất/cập nhật phiên. Đăng nhập, khôi phục mật khẩu, health và cổng khách hàng giữ hợp đồng xác thực/ownership riêng, không bị nhóm quyền nhân viên chi phối.
- Không tạo checkbox cho màn báo cáo hoặc khả năng xuất dữ liệu chưa tồn tại. Các API chức năng nền đã có (CSKH, thông báo) cũng được kiểm quyền riêng.

## Ma trận màn hình và thao tác

| Màn hình | Quyền đọc | Quyền thao tác |
| --- | --- | --- |
| Lịch hẹn / chi tiết lịch | appointments.read | create, reschedule, cancel, no-show |
| Lịch làm việc / lịch lặp | working-schedules.read | manage (preview/xuất bản) |
| Cài đặt đặt lịch | booking-settings.read | manage |
| Tiếp nhận / check-in | reception.read | check-in |
| Bệnh nhân / hồ sơ | patients.read | create, update, archive; alias manage cũ giữ tương thích |
| Xác minh liên kết | patient-links.read | verify (quyết định/thu hồi) |
| Khám / kết luận / lịch sử | examinations.read | manage; các trạng thái nghiệp vụ không đổi |
| Tiếp nhận kết quả cận lâm sàng | examinations.read | investigations.results.receive; bác sĩ đọc kết quả vẫn cần examinations.manage |
| Hình ảnh/tài liệu | documents.read | upload, review |
| Đơn thuốc theo lượt | prescriptions.read | manage |
| Hồ sơ điều trị | treatment.read | dossiers.create/update/delete; plan.manage; revisions.manage; visits.manage |
| Dòng kế hoạch điều trị | treatment.read | items.create/update/delete/price |
| Quá trình thực hiện | treatment.read | actuals.create/update/delete; progress.manage |
| Kế hoạch điều trị dùng lại / API mẫu cũ | treatment.templates.read | manage |
| Giá điều trị | treatment.prices.read | manage |
| Duyệt thương mại điều trị | treatment.commercial.read | approve |
| Thanh toán / phiếu thu | finance.read | payments.create/confirm/cancel, receipts.print, receipt-sequence.manage; quotes.manage, investigations.perform, costs.read/manage giữ hiện có |
| Danh mục dịch vụ khám/CLS | services.read | manage |
| Hộp thư hỗ trợ lịch hẹn | appointments.support.handle | xử lý theo contract hiện có |
| Công việc CSKH / chuyển bác sĩ | care-tasks.read | manage, escalate |
| Thông báo của chính nhân viên | notifications.read | mark-read |
| Tài khoản nhân viên | accounts.read | create/update/state/audit; alias manage cũ giữ tương thích |
| Thiết lập chung / lịch sử / mã bệnh nhân | settings.general.read | manage; cho phép ủy quyền Admin phù hợp |
| Danh mục hồ sơ/nghiệp vụ, gồm nhóm khách hàng | catalog.{kind}.read | create/update/deactivate |
| Dụng cụ/vật liệu và danh mục | catalog.materials.read | create/update/deactivate |
| Nhà cung cấp, loại công việc labo, thuốc | catalog.{kind}.read | create/update/deactivate; medicines.export cho Excel đã có |
| Phân quyền / gán nhóm / lịch sử | access-groups.read | manage, staff.access.assign; settings.audit.read |

## Hợp đồng và kiểm quyền

- Đăng ký đầy đủ grant, nhãn, cha và allowedBaseRoles ở server. Màn checkbox hiển thị toàn bộ danh mục quyền có consumer thật. Quyền cha/con được kiểm ở server và cập nhật nhất quán khi chọn/bỏ trên web.
- Lớp kiểm quyền endpoint tập trung ánh xạ route thực tế + HTTP method sang quyền; đọc lại session hiện hành đã resolve từ DB. Áp dụng cả API staff dùng chung với booking khách hàng, nhưng không chặn khách công khai theo quyền staff.
- Các API options dùng chung cho nhiều màn chấp nhận một trong các quyền màn sử dụng hợp lệ; không vô tình buộc quyền quản lý chỉ để đọc picker.
- Thu ngân có finance.payments.create được chốt phí khám còn chờ bằng đúng giá catalog đã cấu hình; API chốt phí khám chấp nhận quyền này hoặc finance.quotes.manage. Quyền thay giá và yêu cầu lý do thay giá vẫn do dịch vụ phí khám kiểm tra riêng.
- Admin có thể được cấp rõ ràng finance.read để làm quyền cha của chi phí thuê ngoài và thiết lập số phiếu thu. Không tự cấp finance.read cho Admin chưa gán nhóm; các quyền ghi lâm sàng và thu tiền giữ giới hạn vai trò hiện có. Mọi quyền con phải có quyền cha tương thích với cùng vai trò.
- Các kiểm tra chi nhánh/phụ trách trong service giữ nguyên và bổ sung scope assignment cho các module mới. Danh mục hành chính bệnh nhân vẫn dùng chung theo contract; các nghiệp vụ theo Visit/Dossier/branch bị giới hạn theo assignment.
- Sửa đơn giá dòng điều trị yêu cầu thêm items.price; sửa nội dung không thay đơn giá không bị buộc quyền giá. Phiếu đã post tiếp tục dùng correction/reversal theo nghiệp vụ, không hard-delete để mô phỏng checkbox tham chiếu.
- Lập kế hoạch chi tiết yêu cầu quyền sửa hạng mục; thêm/bỏ hạng mục qua lưu cả kế hoạch vẫn phải có items.create/items.delete. Điều chỉnh kế hoạch sau chốt có quyền revisions.manage phụ thuộc items.price. Bản nháp chỉ thuộc tác giả, vẫn kiểm đúng loại dòng khi xóa.
- Middleware đọc lại tối đa 4 MiB JSON metadata để kiểm loại thao tác và chi nhánh, kể cả body chunked; tài liệu multipart tiếp tục dùng giới hạn và kiểm scope riêng của dịch vụ tài liệu.
- Kê đơn/tài liệu tách quyền đọc/ghi khỏi quyền ghi kết luận khám; vẫn kiểm vai trò chuyên môn và phạm vi, không làm đơn thuốc thành bắt buộc.
- API capabilities/allowedActions và UI không quảng bá thao tác mà nhân viên không được cấp. Thu hồi quyền có hiệu lực từ request tiếp theo; UI refresh/focus giữ bản nhập khi chỉ mất quyền ghi.
- Không tự cấp các quyền mới cho nhóm hạn chế hiện có. Không thay role/membership/lịch làm việc hoặc dữ liệu lâm sàng chỉ để gán quyền.
- Tích hợp main PR #39/#40 ngày 03/11: áp kế hoạch dùng lại cần items.create; ngày dự kiến cần items.update; thanh toán theo phiếu cần finance.read/payments.create. Các API người thân giữ Public/ownership. Chi tiết và bằng chứng ở `docs/implementation/VINH-MAIN-INTEGRATION-20261103.vi.md`.

## Kiểm chứng

1. Inventory mọi route màn nhân viên và mọi endpoint nghiệp vụ: phải ánh xạ hoặc có lý do baseline/public rõ ràng.
2. Mỗi nhóm màn có ca API cấp quyền đọc, từ chối ghi, cấp ghi rồi thu hồi với cùng token; kiểm role/scope và alias cũ.
3. Hồi quy các bộ lịch hẹn/lịch làm việc, điều trị/phiếu điều trị/giá, thiết lập, tài chính, khám/đơn thuốc/tài liệu và hồ sơ bệnh nhân.
4. Browser mock kiểm route/menu/nút theo quyền; browser API thật trên schema riêng kiểm ít nhất lịch/cấu hình, điều trị và thiết lập được ủy quyền, cùng luồng thu hồi.
