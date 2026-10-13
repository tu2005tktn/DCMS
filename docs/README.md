# Documentation

- [Mobile scaffold](specs/MOBILE-SCAFFOLD.md): route mapping, folder responsibilities, and implementation scope; [team guide](../mobile/README.md).

- [Screen specifications (English)](specs/DCMS_Screen_Specifications_English%20%281%29.docx): supplied screen design reference.
- [Đặc tả thiết kế UI](specs/UI-DESIGN.vi.md): design system và bố cục mục tiêu cho web (trang xác thực, workspace, trạng thái hệ thống); thay đổi chỉ ở tầng trình bày, không đổi route, phân quyền và nội dung MSG.

- [Hướng dẫn cài đặt và chạy dự án cho team (Windows/PowerShell)](SETUP.vi.md): công cụ cần cài, cấu hình local, chạy API/web/PostgreSQL, kiểm tra và xử lý lỗi.
- [Đối chiếu triển khai UC-01/UC-31](implementation/UC01-UC31.md): quy tắc, thông báo, API, màn hình, kiểm thử và phần hoãn.
- [Chạy thử đăng nhập và quản lý tài khoản](implementation/RUN-ACCOUNTS.vi.md): migration, demo và kiểm tra tự động.
- [Runbook chạy demo DCMS](implementation/RUN-DEMO.vi.md): khởi động nhanh 3 terminal, kịch bản demo, cách đọc mã xác minh, lỗi đã sửa 13/10/2026 và các việc cần chuẩn bị để deploy thật.

Architecture: `Domain` contains business rules, `Application` defines use cases and contracts, `Infrastructure` supplies PostgreSQL/EF Core implementations, and `Api` configures HTTP endpoints and dependency injection. Add feature folders within each layer when implementing business features.

The supplied [37-table logical ERD](architecture/DCMS_Logical_ERD_37_Tables.mmd) is the database design reference. Only account, employee, session and account-audit entities are implemented and migrated; the other business features remain pending.
