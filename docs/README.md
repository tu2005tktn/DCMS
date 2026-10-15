# Documentation

- [Mobile scaffold](specs/MOBILE-SCAFFOLD.md): route mapping, folder responsibilities, and implementation scope; [team guide](../mobile/README.md).
- [UC-33 and S02 Mobile registration](specs/UC33-S02-MOBILE.md): 12 October 2026 decision, workflow, validation, API contract, and account boundary.

- [Screen specifications (English)](specs/DCMS_Screen_Specifications_English%20%281%29.docx): supplied screen design reference.
- [Screen Specification v2.0](specs/DCMS_Screen_Specifications_v2.0.docx) and [Use Case/Business Rule/Message v2.0](specs/DCMS_Use_Cases_Business_Rules_Messages_v2.0.docx) (15 October 2026): current baseline; replaces conflicting values in the earlier pair.

- [Hướng dẫn cài đặt và chạy dự án cho team (Windows/PowerShell)](SETUP.vi.md): công cụ cần cài, cấu hình local, chạy API/web/PostgreSQL, kiểm tra và xử lý lỗi.
- [Đối chiếu triển khai UC-01/UC-31](implementation/UC01-UC31.md): quy tắc, thông báo, API, màn hình, kiểm thử và phần hoãn.
- [Chạy thử đăng nhập và quản lý tài khoản](implementation/RUN-ACCOUNTS.vi.md): migration, demo và kiểm tra tự động.
- [UC-36 S07/S08 liên kết hồ sơ tự thân](implementation/UC36-S07-S08.md): quy tắc, API contract, dữ liệu, kiểm thử, phần hoãn.
- [UC-08/09/38 S05/S06 tìm kiếm và hồ sơ bệnh nhân](implementation/UC08-09-38-S05-S06.md): quyền, dữ liệu, nghi trùng, API, màn hình, phần hoãn.

Architecture: `Domain` contains business rules, `Application` defines use cases and contracts, `Infrastructure` supplies PostgreSQL/EF Core implementations, and `Api` configures HTTP endpoints and dependency injection. Add feature folders within each layer when implementing business features.

The supplied [37-table logical ERD](architecture/DCMS_Logical_ERD_37_Tables.mmd) is the database design reference. Only account, employee, session, account-audit, patient master with administrative audit (UC-08/09/38) and patient account link (UC-36) entities are implemented and migrated; the other business features remain pending.
