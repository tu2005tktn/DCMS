# Phân quyền động cho toàn bộ màn hình — 03/11/2026

## Kết quả triển khai

Mở rộng theo [đặc tả toàn bộ màn hình](../specs/ALL-SCREENS-DYNAMIC-PERMISSIONS.vi.md), thay giới hạn phạm vi của bản phân quyền đầu tiên. Catalog hiện có 121 quyền hiển thị, 16 nhóm cha; hai alias cũ `patients.manage` và `accounts.manage` tiếp tục tương thích trong dữ liệu nhưng không tạo checkbox trùng.

- Bao phủ 24 route nghiệp vụ nhân viên và menu tương ứng: lịch hẹn/chi tiết, lịch làm việc, cấu hình đặt lịch, tiếp nhận, bệnh nhân/hồ sơ, xác minh liên kết, khám, tài liệu, đơn thuốc, điều trị/kế hoạch/điều chỉnh/mẫu/giá/duyệt, thanh toán/in phiếu, dịch vụ, tài khoản, thiết lập và hỗ trợ lịch hẹn.
- Thao tác điều trị tách thêm/sửa/xóa dòng, thực hiện, sửa giá, đóng/mở hồ sơ, điều chỉnh kế hoạch và buổi điều trị. Lưu cả kế hoạch không vượt được quyền thêm/xóa/sửa giá. Context chỉ đọc không trả action ghi hay danh sách tài liệu khi thiếu quyền tài liệu.
- Middleware kiểm route template đã chọn và HTTP method; endpoint chưa khai báo bị từ chối. Kiểm inventory toàn bộ endpoint, bao gồm hợp đồng baseline cho xác thực, ownership khách hàng, health, realtime và các probe chỉ được fixture test đăng ký. OpenAPI hiện có 304 operation, gồm cả staff và public.
- JWT resolve lại quyền từ DB trong request; thu hồi có hiệu lực với token cũ. Web cập nhật quyền khi focus hoặc mỗi 30 giây. Menu, route, form và nút cùng dùng quyền đã resolve, giữ các ràng buộc chuyên môn, membership chi nhánh, phụ trách, trạng thái và version/requestKey.
- Scope assignment được bổ sung cho lịch, đặt lịch, điều trị, giá, template/tái khám và CSKH. Cấu hình hành chính toàn phòng khám giữ phạm vi dùng chung theo đặc tả.
- Admin có thể được ủy quyền cấu hình chung, đọc tài chính/chi phí và quản lý số phiếu thu; không tự thêm quyền mặc định cho Admin chưa gán nhóm. Owner giữ override trong vai trò hiện có, không biến thành Admin/Assistant.
- Giữ thu ngân chốt phí khám đang chờ bằng đúng giá catalog khi có quyền lập phiếu thu. Thay giá vẫn kiểm quyền riêng. Quyền tiếp nhận không bị buộc quyền đọc khám; đọc đơn thuốc không bị buộc quyền ghi khám/tiếp nhận.

## Kiểm chứng và bằng chứng

- .NET 10 restore, build solution Release và web `npm install`/`npm run build`: thành công.
- Unit tests: **79/79**.
- Browser mock: **53/53**. Gồm 24 ca chặn truy cập trực tiếp, menu theo quyền, form chỉ đọc, gán/thu hồi quyền và hồi quy giá khám/CLS, đơn thuốc tùy chọn, lịch sử khám, in phiếu thu.
- API integration cuối: **388/388**; có **22 ca phân quyền toàn màn hình**, kiểm inventory endpoint, chỉ đọc/thu hồi cùng token, scope chi nhánh, lưu cả kế hoạch không vượt quyền giá/thêm/xóa và phân tách header tiếp nhận với dữ liệu khám. Các đợt theo luồng trước đó 191/191 và 199/199 có phần giao nhau, không cộng thành tổng số ca độc lập.
- Browser API thật: **3/3**, dùng hai tài khoản synthetic mới trên schema riêng: nha sĩ đọc lịch/cấu hình/điều trị rồi được cấp quyền tạo, cùng token bị thu hồi; Admin được ủy quyền xem/sửa preview cấu hình rồi thu hồi; editor thật kiểm quyền cha/con sửa giá điều trị và bố cục desktop/mobile. Đã xem lại screenshot desktop/mobile.

Artifacts nằm ở `.artifacts/verification-20261102/` (ignored): `all-screens-*.trx`, log build/test, JSON Playwright, inventory và screenshot. Không chứa mật khẩu trong tài liệu này. Fixture dùng API để tạo tài khoản/nhóm/hồ sơ; SQL chỉ bootstrap membership cho tài khoản synthetic mới vì chưa có API quản lý membership, không sửa lịch/branch hoặc tài khoản demo hiện có.

Lượt API mở rộng loại `GoogleApiTests` và `RegistrationApiTests`, nằm ngoài thay đổi phân quyền màn hình. Không coi các đợt kiểm tra trong tài liệu này là xác nhận lại toàn bộ OAuth/đăng ký. Khi chạy nhiều schema, PostgreSQL local chạm `53300` vì pool fixture giữ kết nối; lần xác minh cuối dùng pool tối đa 4, idle lifetime 2 giây, pruning interval 1 giây và `xUnit.MaxParallelThreads=4` chỉ trong process test, không thay cấu hình sản phẩm.

Build còn cảnh báo package `System.Security.Cryptography.Pkcs` hiện có và bundle web lớn. Không phát sinh migration hoặc sửa dữ liệu bệnh nhân thật; tài liệu demo đang có được giữ nguyên nội dung.

## Giới hạn phạm vi

Chỉ tạo quyền cho màn hình và khả năng đã có consumer. Không bổ sung báo cáo hoặc nút xuất Excel chưa tồn tại; `medicines.export` kiểm khả năng xuất thuốc đã có. Các API CSKH/thông báo được kiểm quyền dù chưa có trang riêng. Không thay nghiệp vụ điều trị của Vinh ngoài các điểm kiểm quyền.
