# Documentation

- [S04 / UC-31](specs/S04-UC31.md): đặc tả quản lý tài khoản Web; [DCMS-75](implementation/S04-DCMS-75.md): phối hợp và bằng chứng nghiệm thu.

- [Mobile scaffold](specs/MOBILE-SCAFFOLD.md): route mapping, folder responsibilities, and implementation scope; [team guide](../mobile/README.md).

- [Screen specifications (English)](specs/DCMS_Screen_Specifications_English%20%281%29.docx): supplied screen design reference.

- [Hướng dẫn cài đặt và chạy dự án cho team (Windows/PowerShell)](SETUP.vi.md): công cụ cần cài, cấu hình local, chạy API/web/PostgreSQL, kiểm tra và xử lý lỗi.
- [Đối chiếu triển khai UC-01/UC-31](implementation/UC01-UC31.md): quy tắc, thông báo, API, màn hình, kiểm thử và phần hoãn.
- [Chạy thử đăng nhập và quản lý tài khoản](implementation/RUN-ACCOUNTS.vi.md): migration, demo và kiểm tra tự động.

Architecture: `Domain` contains business rules, `Application` defines use cases and contracts, `Infrastructure` supplies PostgreSQL/EF Core implementations, and `Api` configures HTTP endpoints and dependency injection. Add feature folders within each layer when implementing business features.

The supplied [37-table logical ERD](architecture/DCMS_Logical_ERD_37_Tables.mmd) is the database design reference. Only account, employee, session and account-audit entities are implemented and migrated; the other business features remain pending.
