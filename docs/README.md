# Documentation

- [DCMS-112 S19/S20](specs/S19-S20-DCMS-112.md): khám theo Visit, lịch sử, sơ đồ răng và file private; [contract, vận hành và bằng chứng](implementation/DCMS-112-EXAMINATION.md).

- [Shared password policy](specs/SHARED-PASSWORD-POLICY.md): decision 18 October 2026, 8–256 characters without character-class requirements for patient and staff accounts; [implementation and validation](implementation/SHARED-PASSWORD-POLICY.md). This decision supersedes the older password baseline in the Word references below.

- [S04 / UC-31](specs/S04-UC31.md): đặc tả quản lý tài khoản Web; [DCMS-75](implementation/S04-DCMS-75.md): phối hợp và bằng chứng nghiệm thu.

- [Mobile scaffold](specs/MOBILE-SCAFFOLD.md): route mapping, folder responsibilities, and implementation scope; [team guide](../mobile/README.md).
- [UC-33 and S02 Mobile registration](specs/UC33-S02-MOBILE.md): 12 October 2026 decision, workflow, validation, API contract, and account boundary.

- [Bộ đặc tả DCMS v2.4](specs/README.md): nguồn hiện hành, ngày 25/10/2026; thay thế các bản Word cũ và bản trùng trong `odental-v2/`. Gồm [Screen Specifications](specs/DCMS_Screen_Specifications_v2.4.docx) và [Use Cases / Business Rules / Messages](specs/DCMS_Use_Cases_Business_Rules_Messages_v2.4.docx). Trang hướng dẫn nêu các thay đổi chính, quyết định riêng còn hiệu lực và ranh giới giữa đặc tả với bằng chứng triển khai cũ.
- [Luồng phòng khám DCMS-110](specs/CLINIC-FLOW-DCMS-110.vi.md): các bước, dữ liệu bàn giao, phạm vi và điểm tính phí gói còn cần quyết định; [DCMS-111 S17/S18](specs/S17-S18-RECEPTION.md): xác minh người đến, check-in một lần, hàng chờ và contract Visit; [bằng chứng triển khai DCMS-111](implementation/DCMS-111-RECEPTION.md). Bắt đầu/lưu khám ở DCMS-112 chưa thuộc phần nghiệm thu của DCMS-111.

- [Hướng dẫn cài đặt và chạy dự án cho team (Windows/PowerShell)](SETUP.vi.md): công cụ cần cài, cấu hình local, chạy API/web/PostgreSQL, kiểm tra và xử lý lỗi.
- [Đối chiếu triển khai UC-01/UC-31](implementation/UC01-UC31.md): quy tắc, thông báo, API, màn hình, kiểm thử và phần hoãn.
- [Chạy thử đăng nhập và quản lý tài khoản](implementation/RUN-ACCOUNTS.vi.md): migration, demo và kiểm tra tự động.
- [UC-36 S07/S08 liên kết hồ sơ tự thân](implementation/UC36-S07-S08.md): quy tắc, API contract, dữ liệu, kiểm thử, phần hoãn.
- [UC-08/09/38 S05/S06 tìm kiếm và hồ sơ bệnh nhân](implementation/UC08-09-38-S05-S06.md): quyền, dữ liệu, nghi trùng, API, màn hình, phần hoãn.
- [UC-05/06 S14/S16 lịch hẹn của tôi, đổi/hủy, yêu cầu hỗ trợ](implementation/UC05-06-S14-S16.md): hợp đồng API dùng chung với TV2/TV3/TV5, quy tắc 12 giờ, mock DTO, phần chờ.
- [UC-06 API đổi/hủy lịch](specs/UC06-APPOINTMENT-CHANGES.md): quyền tự phục vụ/staff, transaction, reservation, lịch sử, receipt và outbox; [bằng chứng triển khai](implementation/UC06-APPOINTMENT-CHANGES.md).
- [Web khách hàng xem/đổi/hủy lịch](specs/CUSTOMER-APPOINTMENTS-WEB.md): màn S14/S16 tại `/customer/appointments`, dùng API thật, review/xác nhận và xử lý lỗi.
- [UC-04/05 S10/S11 lịch hẹn web, đặt lịch tại quầy, API đọc lịch](implementation/UC04-05-S10-S11.md): phạm vi xem, API đọc dùng chung, lưới 15 phút, form quầy dùng lệnh đặt lịch chung.
- [Runbook chạy demo DCMS](implementation/RUN-DEMO.vi.md): khởi động nhanh 3 terminal, kịch bản demo, cách đọc mã xác minh, lỗi đã sửa 13/10/2026 và các việc cần chuẩn bị để deploy thật.

Architecture: `Domain` contains business rules, `Application` defines use cases and contracts, `Infrastructure` supplies PostgreSQL/EF Core implementations, and `Api` configures HTTP endpoints and dependency injection. Add feature folders within each layer when implementing business features.

The supplied [37-table logical ERD](architecture/DCMS_Logical_ERD_37_Tables.mmd) is the database design reference. Only account, employee, session, account-audit, patient master with administrative audit (UC-08/09/38) and patient account link (UC-36) entities are implemented and migrated; the other business features remain pending.
