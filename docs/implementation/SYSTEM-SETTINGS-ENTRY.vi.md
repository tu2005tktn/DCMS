# Một điểm vào thiết lập hệ thống

Ngày 28/10/2026. Theo yêu cầu bỏ trang `/settings` cũ và dùng URL `/system-settings` cho tab/menu Thiết lập hệ thống.

- Chỉ một menu Thiết lập hệ thống, liên kết `/system-settings`. Trang dùng SystemSettingsPage từ phần main đã tích hợp, có các tab theo capabilities: general, catalogs, supplies và materials. Chủ phòng khám thấy đủ các mục; bác sĩ chỉ thấy vật liệu theo quyền hiện hành.
- Xóa import SettingsPage, route `/settings` và `/settings/:section`, menu cũ, cùng module giao diện `web/src/features/settings`. Không giữ alias/redirect cho URL cũ; URL cũ trả trang không tìm thấy của app.
- Chuyển hook lấy lựa chọn chẩn đoán và kiểu snapshot đang dùng bởi S19 về module examinations; chỉ giữ kiểu dữ liệu cần cho khám, xóa DTO giao diện cũ. Không thay API nghiệp vụ, quyền backend, bảng dữ liệu hoặc ID/snapshot chẩn đoán trên các phiếu đã lưu.
- Các API `/api/settings`, `/api/reference-options` và `/api/access-groups` là hợp đồng backend riêng đang phục vụ phân quyền/danh mục vận hành và kiểm thử tích hợp; không xóa cơ chế này chỉ vì bỏ route giao diện `/settings`. Phạm vi này không hợp nhất hai schema danh mục hoặc xóa dữ liệu lịch sử.
- Bỏ các ca browser thao tác trang cũ đã bị xóa; giữ regression chọn chẩn đoán từ danh mục trong phần khám dưới tên phù hợp. Kiểm browser: một menu, Owner vào đúng trang và thấy Thiết lập chung; Dentist thấy vật liệu theo quyền; `/settings` và deep link cũ không còn trang; khám vẫn tải được danh mục chẩn đoán. Build web.

## Kết quả

Đã xóa SettingsPage và DTO giao diện cũ, chuyển hook/snapshot sang examinations, chỉ còn một menu `/system-settings`. Regression chẩn đoán được giữ tại `web/tests/clinical-diagnoses.integration.spec.ts`, cấu hình `web/playwright.clinical-diagnoses.config.ts`; hai ca browser chỉ dành cho UI cũ được bỏ, backend tests/phân quyền không thay đổi. Web production build đạt. Browser/API thật ở viewport desktop 1920 xác nhận một menu và click đúng URL, Owner thấy các tab quản trị, Dentist chỉ có vật liệu, `/settings` và `/settings/diagnoses` hiển thị trang không tìm thấy, danh mục chẩn đoán tại phiếu khám vẫn tải HTTP 200 và có dữ liệu. Lần thử viewport 1280 bị menu tràn/che bởi header có sẵn; không mở rộng thay đổi bố cục menu trong phạm vi này. Không thay dữ liệu nghiệp vụ.
