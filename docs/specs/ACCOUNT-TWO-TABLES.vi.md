# Tài khoản và phân quyền dùng hai bảng

Ngày chốt 12/11/2026. Chủ dự án yêu cầu một bảng lưu tài khoản và thông tin tài khoản, một bảng lưu nhóm quyền.

## Lưu trữ

- `USER_ACCOUNT` chứa thông tin đăng nhập, mật khẩu đã băm, trạng thái, thông tin nhân viên, vai trò, nhóm quyền được gán và phạm vi chi nhánh. Claims, liên kết đăng nhập ngoài, token Identity, phiên đăng nhập, lịch sử refresh token, mã xác minh email, mã khôi phục và nhật ký gửi khôi phục được lưu trong các collection JSONB thuộc tài khoản.
- `ACCESS_GROUP` chứa nhóm quyền động với các grant JSONB. Các vai trò nền tương thích ASP.NET Identity được lưu thành bản ghi hệ thống trong cùng bảng và có loại riêng. Màn quản lý nhóm quyền chỉ thao tác trên bản ghi nhóm quyền động.

`EMPLOYEE_PROFILE`, `ROLE`, `USER_ACCOUNT_ROLE`, bốn bảng phụ `AspNet*`, `STAFF_ACCESS_ASSIGNMENT`, `AUTH_SESSION`, `AUTH_REFRESH_TOKEN`, `EMAIL_VERIFICATION_TOKEN`, `RECOVERY_TOKEN` và `RECOVERY_OUTBOX` được chuyển vào hai bảng trên. Phần còn lại của hệ thống dùng cùng ID tài khoản và ID nhân viên hiện có.

## Hành vi

Giữ các route, DTO, kiểm tra mật khẩu, khóa tài khoản, đăng nhập Google và phiên bản quản trị tài khoản. Group/grant và phạm vi chi nhánh được kiểm tra ở server trước khi đọc dữ liệu hoặc replay kết quả. Quyền thay đổi có hiệu lực với phiên đang đăng nhập.

Refresh token tiếp tục được băm, xoay một lần và phát hiện dùng lại để thu hồi phiên. Khôi phục mật khẩu tiếp tục kiểm soát thời hạn, số lần thử, giới hạn gửi lại, mã xác minh và reset grant dùng một lần. Thông tin proof và token bí mật chỉ có giá trị băm trong lưu trữ.

Các thao tác sửa trạng thái bảo mật của một tài khoản phải khóa hoặc kiểm soát đồng thời trên bản ghi tài khoản, lưu token và thay đổi nghiệp vụ trong cùng giao dịch. ID nhân viên, role, token lịch sử và quan hệ với dữ liệu điều trị/tài chính được bảo toàn khi migration.

## Chuyển dữ liệu và kiểm chứng

Migration chép dữ liệu sang cột/JSONB trước khi thu hồi các bảng nguồn trong cùng giao dịch. Down khôi phục các bảng nguồn và ID. Kiểm tra digest dữ liệu trên bản sao DB phát triển, đăng nhập theo username/email, Google, role/grant, phạm vi chi nhánh, thu hồi phiên, refresh đồng thời, khôi phục mật khẩu và xác minh email.

Mục tiêu physical schema của phần tài khoản/phân quyền là hai bảng. Schema `public` dự kiến giảm từ 95 xuống 82 bảng. Bản sao lưu DB và kiểm thử nâng cấp/rollback được hoàn tất trước khi áp dụng vào DB phát triển.

Kh?a d?ng t?i kho?n ???c l?y b?ng c?u l?nh SQL ch? ??c ID, sau ?? n?p t?i kho?n b?ng truy v?n EF th?ng th??ng. D? li?u JSON ???c c?p nh?t qua ??i t??ng t?i kho?n c? tracking. M?i l?n thay ??i t?i kho?n ho?c ph?n h? s?/quy?n d?ng chung d?ng ??u ??i concurrency stamp; `Version` qu?n tr? t?i kho?n gi? ? ngh?a hi?n c?. Refresh d?ng giao d?ch Read Committed k?t h?p kh?a t?i kho?n ?? hai y?u c?u c?ng token ???c x? l? tu?n t?.

`EmployeeId` gi? c?c ID nh?n vi?n c?. T?i kho?n m?i nh?n m?t ID n?i b? t? sequence; ch? d?ng c? m? v? t?n nh?n vi?n m?i xu?t hi?n nh? h? s? nh?n vi?n. C?t l?ch s? `VISIT.PriorityDentistId` ti?p t?c li?n k?t t?i ID nh?n vi?n ?? gi? l?i.

## K?t qu? migration

`20261112095901_ConsolidateAccountStorage` ?? ?p d?ng v?o DB ph?t tri?n. `public` c? 82 b?ng, trong ?? c?n `USER_ACCOUNT` v? `ACCESS_GROUP` v? kh?ng c?n c? 13 b?ng ngu?n ?? g?p. Model EF c? 80 b?ng. B?n sao l?u schema `public` n?m t?i `.tools/account-collapse/before-account-collapse.dump` (768.703 byte; SHA-256 `cf703e1730d9bff7be82927e952e160299763ca1db802027dc18cd698eb20e57`), ngo?i Git.

B?n sao DB ?? ch?y Up/Down v? ??i chi?u 1.276 d?ng c?a 15 b?ng ngu?n: s? l??ng v? digest kh?p ho?n to?n. Digest logic t?i kho?n, vai tr?, g?n vai tr?, nh?n vi?n, phi?n v? nh?t k? t?i kho?n c?ng kh?p tr??c/sau khi ?p d?ng v?o DB ph?t tri?n.

## Ki?m ch?ng ho?n t?t

- Restore/build .NET 10 v? build web th?nh c?ng; model kh?ng c? thay ??i ch?a t?o migration.
- 102 unit test ??t. T?ng h?p theo t?ng test case t? l??t h?i quy to?n b? v? l??t ch?y l?i `SystemCatalogApiTests`: 556 integration test ??t, hai NFR t?y ch?n skip theo c?u h?nh. Test danh m?c c?p nh?t t?i kho?n t? b?n ghi m?i v? x?c nh?n IdentityResult th?nh c?ng tr??c khi ki?m tra m?t quy?n; b?n ghi c? b? t? ch?i b?i concurrency stamp sau khi ??ng nh?p.
- M?t test l?i c? s?n `ClinicalWorkApiTests.ReviewSaveDoesNotPostActivityAndActivityRetryUsesOneId` ???c lo?i kh?i l??t h?i quy n?y. L?i 503 ?? ???c t?i hi?n tr?n HEAD `1febcd7` tr??c thay ??i l?u tr?, nh? ghi trong t?i li?u r?t g?n DB.
- C?c ki?m tra m?i bao ph? Identity claims/logins/tokens/role claims, gi? d? li?u qua Up/Down, x?a g?n nh?m v?n gi? t?i kho?n v? nh?n vi?n, t? ch?i ghi ?? t?i kho?n c?, refresh ??ng th?i ch? xoay m?t l?n, ph?t hi?n d?ng l?i thu h?i m?i phi?n, heartbeat idempotent v? thu h?i khi h?t h?n kh?ng ho?t ??ng.
- API ch?y tr?n c?ng 5080. `/health` x?c nh?n API v? database healthy. ??ng nh?p t?i kho?n demo hi?n c?, ??c `/api/auth/me`, ??ng xu?t v? x?c nh?n token c? b? t? ch?i ?? th?nh c?ng tr?n DB ph?t tri?n; kh?ng l?u token ho?c m?t kh?u trong b?o c?o.
- N?m database b?n sao t?m ?? ???c x?a; gi? b?n sao l?u v? manifest ki?m ch?ng ngo?i Git.

## Cập nhật rút gọn ngày 18/11/2026 (`USER_ACCOUNT`: 37 -> 18 cột)

- Gom `SecurityStamp`, `PhoneNumberConfirmed`, `TwoFactorEnabled`, `LockoutEnd`, `LockoutEnabled`, `AccessFailedCount` cùng 7 collection (`Claims`, `Logins`, `IdentityTokens`, `Sessions` gồm `RefreshTokens`, `EmailVerifications`, `Recoveries`, `RecoveryDeliveries`) vào cột `AuthData` (`jsonb`).
- Loại bỏ `ConcurrencyStamp`, `NormalizedUserName`, `NormalizedEmail`, `NormalizedPhone`, `GoogleSubject`, `EmployeeFullName`, `AccessVersion`. Tính duy nhất không phân biệt hoa thường cho `UserName`/`Email` và số điện thoại chuẩn hóa được bảo vệ bằng 3 functional unique index (`UserNameIndex`, `EmailIndex`, `IX_USER_ACCOUNT_NormalizedPhone`).
- `EmployeeProfile` chia sẻ cột `FullName` với `ApplicationUser` và giữ `EmployeeCode` làm cột phân biệt; `StaffAccessAssignment` đọc/trả `Version` của tài khoản.
- `Version` trở thành phiên bản nghiệp vụ quản trị thuần túy (`IsConcurrencyToken(false)`); đồng thời ghi ở cấp DB được bảo vệ bởi cột hệ thống ẩn `xmin` (`attnum = -2`, `.IsRowVersion()`) trên cả 3 thực thể dùng chung `USER_ACCOUNT`. Xem [rút gọn 9 bảng còn lại](./REMAINING-NINE-TABLES-SIMPLIFICATION.vi.md).

## Cập nhật rút gọn ngày 18/11/2026 (`ACCESS_GROUP`: 14 -> 10 cột)

- Loại bỏ `NormalizedCode`, `NormalizedName` (thay bằng functional unique index `IX_ACCESS_GROUP_RecordKind_Code_Upper` và `IX_ACCESS_GROUP_RecordKind_Name_Upper`).
- Loại bỏ `RoleConcurrencyStamp` (dùng `Version.ToString()` làm `ConcurrencyStamp` cho `IdentityRole`).
- Gộp `RoleClaims` vào cột `Grants` (`jsonb`) qua ánh xạ value converter/comparer `AccessGroupGrant` (`GroupId`, `Permission`, `ClaimId?`, `ClaimValue?`). Xem [rút gọn 8 bảng ít cột còn lại](./REMAINING-EIGHT-SMALL-TABLES-SIMPLIFICATION.vi.md).

## Sửa dữ liệu ngày 19/11/2026: mốc thời gian vô cực trong `AuthData.Sessions`

- Lỗi: đăng nhập trả 503 vì `System.Text.Json` không đọc được `"-infinity"` thành `DateTimeOffset` (`$.Sessions[n].LastInteractiveActivityAt`). `UserManager.FindByNameAsync` nạp toàn bộ `AuthData` nên một phiên hỏng chặn đăng nhập của cả tài khoản.
- Nguồn gốc: migration `S58StaffIdleRuntime` thêm cột `AUTH_SESSION.LastInteractiveActivityAt` với mặc định `DateTimeOffset.MinValue`, PostgreSQL lưu thành `-infinity` cho các phiên đã có. Migration `ConsolidateAccountStorage` chuyển phiên vào JSON bằng `to_jsonb`, ghi nguyên chuỗi `"-infinity"`. Một số phiên cũ cũng có `LastSeenAt = "-infinity"`.
- Bất biến: mọi mốc thời gian trong `AuthData` (kể cả `Sessions[].RefreshTokens[]`) phải là chuỗi ISO-8601 hữu hạn; không được có `"infinity"`/`"-infinity"`.
- Migration chỉ sửa dữ liệu `RepairInfiniteSessionTimestamps` (không đổi schema) thay thế trong mỗi phiên: `LastSeenAt` vô cực lấy `CreatedAt`; `LastInteractiveActivityAt` vô cực lấy `LastSeenAt` (sau khi sửa), nếu không có thì `CreatedAt`. Các trường thời gian vô cực khác trong phiên/refresh token còn sót lại lấy `CreatedAt` của phiên; nếu chính `CreatedAt` hỏng thì dùng `1970-01-01T00:00:00+00:00` để phiên chắc chắn hết hạn. Phiên hợp lệ giữ nguyên. Migration idempotent, chạy tự động khi deploy (`migrate`). `Down` không khôi phục giá trị vô cực.
- Các phiên bị ảnh hưởng đều đã cũ nên sau khi sửa vẫn hết hạn theo chính sách idle/refresh hiện tại; không phiên nào được hồi sinh.
