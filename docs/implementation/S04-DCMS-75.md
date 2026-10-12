# DCMS-75 — S04 User Account Management

Chủ màn hình: Tư Nguyễn Văn. [Đặc tả S04/UC-31](../specs/S04-UC31.md), quyết định ngày 12/10/2026.

## Nội dung cập nhật task hiện có

S04 Web: search mã/tên/liên hệ, role/status filters, sort/paging server, detail Drawer, create/update staff, nhiều role, lock/unlock có lý do, audit và duplicate normalized contact. Admin login dùng API/phiên chung S01; S03 recovery hoãn, không reset bypass. Quyết định chưa chốt đánh dấu [PROPOSED].

Không tạo task mới thay DCMS-75. Jira chỉ cập nhật trực tiếp khi có kết nối Atlassian và đọc được task hiện tại; triển khai repository tiếp tục độc lập.

## Chạy và kiểm tra

Xem [hướng dẫn hiện có](RUN-ACCOUNTS.vi.md). Từ PowerShell tại thư mục gốc, sau khi có `.env` riêng và SDK .NET 10:

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
. .\infrastructure\scripts\import-development-env.ps1
& $dotnet restore backend/DCMS.sln
& $dotnet build backend/DCMS.sln --no-restore
& .\infrastructure\scripts\verify-s04-migration.ps1
& .\infrastructure\scripts\test-accounts.ps1
& .\infrastructure\scripts\start-api.ps1
```

Terminal khác:

```powershell
cd web
npm.cmd install
npm.cmd run build
npm.cmd run dev
```

Khi API/Vite đang chạy, terminal ở root:

```powershell
Invoke-RestMethod http://localhost:5080/health
& .\infrastructure\scripts\test-accounts.ps1 -Browser
```

Nếu PowerShell chặn script local, dùng policy cho terminal hiện tại theo hướng dẫn SETUP hoặc `powershell.exe -NoProfile -ExecutionPolicy Bypass -File infrastructure/scripts/test-accounts.ps1` (thêm `-Browser` khi cần). Chromium cần cài một lần từ `web` bằng `npx.cmd playwright install chromium`. API cần dừng trước khi build lại nếu Windows khóa DLL; chạy API/tests dưới tài khoản Windows hiện tại để DPAPI/Event Log hoạt động.

Kiểm tra migration trên development đã có migration nền tảng: `& .\infrastructure\scripts\verify-s04-migration.ps1`. Script so digest dữ liệu trước/sau trong PostgreSQL, không in credential/record; giữ volume và không xóa account/schema. Không chạy đồng thời API/tests ghi dữ liệu trong lúc so digest.

Migration `20261012150749_UniqueAccountContacts` thay EmailIndex bằng unique index và thêm generated stored NormalizedPhone + unique index. Phone của dữ liệu cũ được PostgreSQL tính từ PhoneNumber; không đổi liên hệ gốc, credential, trạng thái hoặc role. Migration chỉ thay index/thêm cột, không drop bảng hoặc delete dữ liệu. Nếu normalized contacts trùng thì migration rollback và cần xử lý nghiệp vụ riêng, không gộp/xóa tài khoản tự động.

## Bằng chứng nghiệm thu ngày 12/10/2026

| Kiểm tra thực tế | Kết quả |
| --- | --- |
| .NET SDK 10.0.401 restore + build `--no-restore` | Đạt, 0 warning / 0 error |
| EF pending model changes | Không còn model changes chưa có migration |
| Migration trên PostgreSQL development | Áp dụng thành công; script so digest xác nhận account (kể cả hash), roles, user roles, employees, sessions và audit cũ không đổi |
| Integration tests PostgreSQL thật | **25 passed, 0 failed, 0 skipped** trong schema riêng; có sentinel Patient/link/bệnh án |
| Browser Chromium | **9 passed**; luồng nghiệp vụ dùng API thật; riêng error/loading/permission states có response fault được chủ động inject rồi bỏ để retry API thật |
| npm install / TypeScript + Vite build | Đạt; npm audit 0 vulnerabilities lúc install; còn cảnh báo JS bundle lớn (~1.15 MB trước gzip) |
| Compose và health | PostgreSQL healthy; `/health` HTTP 200, api/database healthy |
| Giao diện | Đã xem ảnh desktop, detail Drawer và viewport 390 px; bảng cuộn ngang trong Card, trang không tràn ngang |
| Git/secret/artifact | `.env`, dependencies, SDK local, test-results bị ignore; không thêm secret hoặc dữ liệu Patient vào thay đổi |

Ảnh QA được tạo tại `web/test-results/s04-desktop.png`, `s04-detail.png`, `s04-small-screen.png`, nằm ngoài Git. Browser tests giữ account giả `e2e.*`/audit trong development để team xem; integration schemas cũng giữ lại theo quy trình dự án.

| Tiêu chí người dùng | Bằng chứng |
| --- | --- |
| 1. Admin tạo từng staff role; public không quản trị | `AdminCanCreateEachStaffRoleWithoutAffectingPatientRecords` (4 role cases), `EveryManagementEndpointRequiresAdmin` (anonymous/Dentist/Owner/Assistant/Guest, gồm detail/catalog/audit) |
| 2. Owner chỉ kế thừa Dentist | `OwnerInheritanceMultipleRolesAndPublicGuestAreDistinct`, browser Owner access denied |
| 3. Không Patient/bệnh án/link side effects | Sentinel Patient + PATIENT_ACCOUNT_LINK + MEDICAL_RECORD; assertion sau create/role/lock/unlock/contact |
| 4. Lock/unlock và phiên | `LockImmediatelyDeniesExistingTokenAndUnlockDoesNotReviveIt` |
| 5. Role revoke hiệu lực phiên hiện tại | `RevokeAdminRoleAffectsExistingTokenAndCanGrantAgain`, token forged role test |
| 6. Duplicate/quyền/version và audit | normalized email/phone create/update, concurrent contact/create, concurrent writers, stale update/state; audit rollback/actor/time/credential checks |
| 7. Admin login → tạo → role → lock/unlock → logout thật | Browser `admin creates, edits roles, locks, unlocks, searches and reads audit through real APIs`, kèm đăng xuất |
| List/UI states bổ sung S04 | search code/name/email/phone; filters/sort/paging; detail; contact validation/duplicate/double submit; conflict giữ draft; error/retry/access denied; unsaved discard |

## Phần hoãn và trạng thái task

Sau khi mở bản development, người dùng phản hồi “Rồi oke” và yêu cầu cập nhật tài liệu, đẩy thay đổi lên GitHub. Các kết quả từng kịch bản trong bảng trên là bằng chứng kiểm thử tự động đã chạy; không ghi thêm kịch bản kiểm thử thủ công chưa được báo cáo.

Recovery support hoãn vì S03/UC-02 chưa có kênh xác minh và policy; Drawer hiển thị nút chưa khả dụng và lý do. S01 vẫn sở hữu API/phiên chung; không thêm refresh token, mật khẩu recovery hoặc hệ xác thực thứ hai. Quyết định [PROPOSED] cần team/phòng khám chốt trước deployment.

**Jira DCMS-75 chưa được cập nhật trực tiếp:** Atlassian chưa kết nối trong phiên này. Nội dung task và bằng chứng ở đây sẵn sàng dùng để cập nhật task hiện có; không tạo task thay thế hoặc tự đổi trạng thái/assignee trên Jira.

Tham khảo triển khai: [EF indexes](https://learn.microsoft.com/en-us/ef/core/modeling/indexes), [EF concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [Ant Design Table](https://ant.design/components/table/). Quy tắc nghiệp vụ/MSG lấy từ DOCX local, không lấy từ tài liệu kỹ thuật.
