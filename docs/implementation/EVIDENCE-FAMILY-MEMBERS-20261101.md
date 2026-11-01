# Người thân và đặt lịch hộ — kết quả 01/11/2026

## Xem hồ sơ người thân — Modal tạm
Đã thêm nút Xem hồ sơ cho từng dòng/thẻ, component Modal riêng có tên người thân và “Chưa có hồ sơ nào”. Chưa tích hợp nguồn hồ sơ/quyền đọc; không gọi API bệnh án, không ghi dữ liệu, không thay backend/schema. Web build đạt (còn cảnh báo SignalR annotation và kích thước bundle). 7 ca UI hồi quy đạt; ca Modal mới đạt khi chạy lại sau khi chờ focus/animation mở Modal trước Escape. Kiểm đúng tên, đóng bằng Escape/Đóng/×, giữ tìm kiếm, không phát sinh request và responsive 390/768/1440. Dùng Chrome hệ thống vì Chromium Playwright chưa cài. Web localhost5173 HTTP200, health API/database healthy. Chưa chạy Git hoặc thay database.

## Điều chỉnh: giới tính và điện thoại bắt buộc
Đã đổi form và API: tạo/sửa phải đủ gender/contactPhone; không cho lưu rỗng/whitespace. Dữ liệu cũ thiếu trường vẫn đọc được, sửa phải bổ sung. Không migration/backfill, không đổi contact booking. Replay receipt đã commit trước quy định mới vẫn trả kết quả cũ sau auth/owner và không ghi dữ liệu mới; request mới thiếu trường bị400.

Backend/Web build đạt, 5/5 integration test trên database riêng `dcms_family_required_verify_20261101_f61c20a3` đạt (7s), đã dọn DB kiểm thử. Test legacy dùng fixture member/receipt cũ, xác nhận đọc/replay được nhưng cập nhật mới thiếu contact bị chặn. 7/7 UI mock bản cuối đạt (42.3s), kiểm hai lỗi required, retry, CRUD, stale và booking. Lượt đầu có timeout do locator exact name của nút Lưu khi icon loading còn hiện; đổi test sang submit button và chờ class loading kết thúc, không sửa logic save để né lỗi. API đã chạy lại và health api/database healthy. Chưa chạy Git hoặc commit/push. Các mục nullable/clear ở phần lịch sử bên dưới đã được thay thế bằng quy định bắt buộc này.

## Development: đã áp bổ sung contact sau phê duyệt riêng
Đã sao lưu và kiểm archive `C:\Users\khong\AppData\Local\DCMS\backups\dcms-before-family-contact-a6349afa5d714b3db77c039ed85f14a9.dump` (7.656.938 bytes; SHA256 `CD5AC84E926969D557BF0811E47C44F1012155FBF2E4AB6A0BB5CD980706CA3F`). Áp đúng `20261101095748_FamilyMemberContactDetails` thành công. Đọc information_schema/public xác nhận Gender và ContactPhone nullable; không seed hoặc sửa giá trị hồ sơ cũ. API chạy lại port5080, health api/database healthy; Web trang family-members HTTP200. Chưa tạo dữ liệu thử development hoặc nghiệm thu CRUD bằng tài khoản của người dùng; kết quả chức năng vẫn dựa trên integration cách ly và UI mock bên dưới. Chưa chạy Git.

## Bổ sung giới tính và số điện thoại liên hệ
API/model/DTO và form thêm/sửa có gender/contactPhone tùy chọn; danh sách desktop/mobile và đối chiếu stale hiển thị lại. Không suy giới tính theo quan hệ; số người thân không thay số người đặt trong booking. Giữ tương thích DTO/receipt cũ thiếu trường; có thể xóa giá trị bằng null. Migration `20261101095748_FamilyMemberContactDetails` chỉ thêm hai cột nullable, chưa áp dcms_dev tại thời điểm ghi mục này.

Backend solution build đạt 0 warnings/errors; Web build bản cuối đạt (còn warning SignalR/chunk size có sẵn). 5/5 integration test FamilyMemberApiTests đạt trên database riêng `dcms_family_contact_verify_20261101_41b74d8c`, đã dọn database đó. Kiểm round-trip/normalize/clear/validation/request-key mismatch và legacy receipt; các ca owner/concurrency cũ cũng đạt. 7/7 UI mock bản cuối đạt (32.3s), kiểm lưu/mở lại/sửa điện thoại, giới tính, phone validation, form lỗi, booking giữ liên hệ người đặt và responsive. Đã xem lại ảnh mobile synthetic trong `.artifacts`.

Lượt thăm dò phát hiện sự kiện scroll kép có thể tăng trang hai lần; đã thêm khóa ref đồng bộ cho một request trang đang tải, kiểm lại paging đạt. Chưa chạy Git, không seed hoặc tạo dữ liệu thử trên dcms_dev. API đã tạm dừng khi build; cần phê duyệt sao lưu/áp migration mới và chạy lại để test development.

## Điều chỉnh sau khi người dùng test: chỉ chọn người thân đã lưu
Đã bỏ nút/lựa chọn Nhập người thân khác và nút/drawer Thêm người thân trong booking. Dropdown có placeholder, chỉ hiển thị người thân đã lưu; Relative chưa chọn người thân không được Tiếp tục. Thêm/sửa vẫn tại Người thân của tôi, form thông tin booking tiếp tục cho sửa riêng snapshot tên/ngày sinh. Lỗi danh sách có Thử lại, Self không bị chặn. Không thay backend/database hoặc chạy Git.

Web build đạt; 8/8 Playwright mock đạt (33.2s): 7 ca người thân đã cập nhật theo hành vi mới và 1 hồi quy booking Self/slot conflict. Các bằng chứng bên dưới mô tả lượt triển khai trước, nhánh inline create/manual đã được thay thế bằng quyết định mới này.

## Đã triển khai
- API `/api/me/family-members`: list/search/page/detail/create/update/soft-delete; Public active và owner scoping, 404 với ID ngoài tài khoản, optimistic version, receipts owner+requestKey và transaction lock theo owner.
- Hai bảng CUSTOMER_FAMILY_MEMBER / CUSTOMER_FAMILY_RECEIPT, migration `20261101092227_CustomerFamilyMembers` và model snapshot đồng bộ. Không thay bảng Appointment hoặc Patient.
- Web Người thân của tôi: bảng/thẻ responsive, tìm kiếm/paging20, drawer CRUD, xác nhận xóa/dirty, giữ dữ liệu khi lỗi, đối chiếu stale có lựa chọn giữ bản nhập hoặc dùng dữ liệu mới.
- Nav/shortcut và booking Relative: tìm/chọn danh sách nhiều trang, thêm độc lập trong drawer, deep link GET lại theo owner, sao chép tên/ngày sinh, sửa riêng trên lịch, giữ booker/contact. Tạo lịch dùng attendee.birthDate và Relative hiện có, không gửi familyMemberId hoặc PatientId mới.
- Kiểm race deep-link response ghi đè lựa chọn người dùng và debounce ban đầu đưa trang dropdown về 1. Không sửa check-in, liên kết hồ sơ hoặc quy tắc availability/booking.

## Bằng chứng
- .NET10 restore + solution build đạt; sau sửa style assertion xUnit, integration test build lại không còn warning mới.
- Web `npm run build` bản cuối đạt; còn warning SignalR annotation/chunk size có sẵn.
- 79/79 unit test backend đạt.
- 4/4 integration test FamilyMemberApiTests đạt trên database riêng `dcms_family_verify_20261101_785f3e24`; ApiFixture tạo thêm schema riêng, migrate toàn bộ và seed chỉ trong database này. Test CRUD/search/duplicate names, roles/owner/inactive, validation, concurrent create/replay/key mismatch, concurrent edits/receipt replay sau xóa, no side effects và HasPendingModelChanges=false. Database kiểm thử đã được drop sau khi fixture dọn xong; không dùng dcms_dev tạo dữ liệu thử.
- 7/7 Playwright family-members.spec.ts bản cuối đạt (29.9s), API mock. Kiểm CRUD/retry key/dirty, stale đối chiếu, deep link/dữ liệu snapshot và slot retry, inline create/Self/manual fallback, tìm kiếm/paging, guard, dropdown remote paging và bàn phím.
- 5/5 Dashboard mock và 8/8 hồi quy customer session/Self booking/verified self-link/staff shell đạt trong các lượt kiểm tra riêng; không tuyên bố một lượt 20 test hoặc toàn bộ E2E dự án.
- Các lượt thăm dò có lỗi fixture addInitScript tự cấp lại phiên sau reload, thao tác click input Ant Select bị label che, timeout khi build đồng thời; đã sửa fixture/cách thao tác, chạy lại riêng đủ 7 ca đạt. Race pagination được sửa trong implementation, test lại đạt.
- Đã xem ảnh mock mobile tại `.artifacts/family-members-390.png`; test chụp 390/768/1440 và kiểm không tràn ngang.

## Localhost và phần chờ duyệt
API/Web đã chạy lại, `/health` xác nhận api/database healthy. Đọc danh sách migration xác nhận chỉ `20261101092227_CustomerFamilyMembers (Pending)` trên dcms_dev. Chưa áp migration development nên chưa nghiệm thu CRUD/browser bằng API development thật. Muốn test tính năng ở localhost cần sao lưu, áp migration này và khởi động lại API sau khi người dùng duyệt riêng. Không seed hoặc tạo người thân thử trong dcms_dev.

Chưa chạy Git, commit/push/PR trong lượt triển khai. Giữ các sửa đổi đăng ký/Google/customer có sẵn, chỉ chỉnh các đoạn tích hợp người thân trong tệp dùng chung. Handoff check-in/liên kết: danh sách này chưa xác minh danh tính, không phải Patient và không cấp quyền bệnh án.

## Development sau phê duyệt riêng
Người dùng đã duyệt sao lưu, áp migration và chạy API. Đã kiểm `pg_restore --list` thành công cho bản dump ngoài repo `C:\Users\khong\AppData\Local\DCMS\backups\dcms-before-family-members-e224e52bbcfe4927a31444bdbbd8c326.dump` (7.232.660 bytes; SHA256 `8F51BF484EB513E775D3073AC9277A87E20DF25720AC2CEEC0668F31BBEF753F`). Áp đúng `20261101092227_CustomerFamilyMembers` thành công; lịch sử migration có bản ghi, hai bảng mới đều 0 dòng. Không seed hoặc tạo người thân thử.

API đã khởi động lại tại 5080; health api/database healthy, Web `/customer/family-members` HTTP200, GET API người thân không có phiên trả401. Đây là kiểm startup/schema/auth guard, không thay thế nghiệm thu browser CRUD bằng tài khoản thật; các kiểm thử chức năng phía trên vẫn dùng database cách ly/API mock. Chưa chạy Git hoặc commit/push.
