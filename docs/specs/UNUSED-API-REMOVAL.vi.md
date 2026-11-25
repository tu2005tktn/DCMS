# Gỡ API không có giao diện sử dụng (21/11/2026)

Yêu cầu người dùng ngày 21/11/2026: chỉ giữ API mà giao diện web (`web/src`) hoặc mobile (`mobile/src`) thực sự gọi; API sinh thừa thì xóa. Không đổi schema database, không thêm hoặc xóa bảng.

## Cách rà soát

1. Liệt kê toàn bộ route minimal API trong `backend/src/DCMS.Api` (335 route, tính cả route ghép qua hàm phụ và nhóm bí danh).
2. Thu thập mọi đường dẫn `/api/...` trong code web và mobile (bỏ test, mock), kể cả đường dẫn ghép từ biến như `` `${base}/${id}` ``.
3. Đối chiếu từng route; các route chỉ khớp nhờ đoạn đường dẫn động được kiểm tra thủ công tại chỗ gọi.

Kết quả: 34 route không có màn hình nào gọi. Được giữ dù không có giao diện gọi trực tiếp:

| Route | Lý do giữ |
| --- | --- |
| `POST /api/payments/payos/webhook` | PayOS gọi khi khách chuyển khoản. |
| `GET /health` | Kiểm tra sức khỏe khi triển khai và theo `AGENTS.md`. |
| `/hubs/notifications` | SignalR, web và mobile kết nối tới. |
| `GET /api/dev/recovery/proof/{accountId}`, `/api/test/realtime/*` | Chỉ đăng ký ở môi trường Development, phục vụ kiểm thử cục bộ. |

## Route bị gỡ (34)

### Tài khoản và xác thực (11)

| Route | Ghi chú |
| --- | --- |
| `POST /api/auth/session/idle-preview` | Thiết lập chung dùng `POST /api/system-settings/settings/general/preview`. |
| `POST /api/auth/sign-out-all` | Không có nút "đăng xuất mọi thiết bị". |
| `GET /api/auth/sessions` | Không có màn danh sách phiên. |
| `GET /api/me/email-verification`, `POST .../request`, `POST .../confirm` | Màn xác minh e-mail đã gỡ theo [S03-PUBLIC-EMAIL-RECOVERY.md](S03-PUBLIC-EMAIL-RECOVERY.md); e-mail đăng ký dùng thẳng cho khôi phục mật khẩu. |
| `POST /api/admin/recovery/request/{accountId}`, `.../verify/{accountId}`, `.../reset/{accountId}`, `GET .../status/{accountId}` | Màn quản lý tài khoản không có thao tác "Admin hỗ trợ khôi phục". Nhân viên tự khôi phục qua `/api/auth/recovery/*`. |
| `GET /api/accounts/roles` | Màn tài khoản lấy vai trò từ `GET /api/accounts/catalog`. |

### Đặt lịch và khám (5)

| Route | Ghi chú |
| --- | --- |
| `GET /api/working-schedules?branchId&dentistId&weekStart`, `POST /api/working-schedules/preview`, `POST /api/working-schedules/publish`, `POST /api/working-schedules/preview-availability` | Lịch làm việc kiểu tuần cũ. Màn `/working-schedules` chỉ dùng lịch lặp `/api/working-schedules/recurring/*` ([S09-UC03.md](S09-UC03.md)). Dữ liệu tuần cũ đã lưu (`WorkingWeek`) vẫn được đọc khi tính slot, không xóa. |
| `GET /api/visits/{visitId}` | Màn phiếu khám đọc lượt khám qua `GET /api/visits/{visitId}/examination`. Phương thức `IReceptionService.VisitAsync` vẫn giữ vì phiếu khám dùng nội bộ. |

### Thiết lập (12)

| Route | Ghi chú |
| --- | --- |
| `/api/system-settings/branches/*` (11 route) | Bí danh trùng của `/api/settings/branches/*`, cùng code xử lý. Màn Chi nhánh & Ghế chỉ gọi `/api/settings/branches`. |
| `GET /api/system-settings/settings/catalogs/services/export` | Không có nút xuất danh mục dịch vụ. |

### Hồ sơ và điều trị (6)

| Route | Ghi chú |
| --- | --- |
| `GET /api/patient-links/me/requests/{id}` | Màn liên kết hồ sơ đọc yêu cầu hiện tại từ `GET /api/patient-links/me`. |
| `GET /api/prescriptions/patients/{patientId}/{id}` | Tab đơn thuốc dùng dữ liệu từ danh sách `GET /api/prescriptions/patients/{patientId}`. |
| `GET /api/patients/{patientId}/dossiers/summary` | Thanh tóm tắt phiếu điều trị đã bỏ theo [PATIENT-TREATMENT-PAYMENT-UI.md](PATIENT-TREATMENT-PAYMENT-UI.md). |
| `PATCH /api/dossiers/{dossierId}/plan/items/{itemId}/schedule-note` | Không có ô nhập ghi chú lịch cho hạng mục. |
| `GET /api/dossiers/{dossierId}/plan/preview`, `GET /api/dossiers/{dossierId}/plan/history` | Phiếu điều trị không có màn xem trước/lịch sử phiên bản kế hoạch. |

## Phạm vi code

- Xóa dòng map route, hợp đồng request/response và phương thức service chỉ phục vụ các route trên.
- Xóa toàn bộ phân hệ xác minh e-mail (`IEmailVerificationService`, `EmailVerificationService`, `IEmailVerificationDelivery`, `UnavailableEmailVerificationDelivery`). Cột `EmailConfirmed` của tài khoản giữ nguyên.
- Xóa hoặc chuyển các test tích hợp đang gọi route bị gỡ; test lịch làm việc dựng dữ liệu bằng lịch lặp.
- Các tài liệu cũ nhắc tới route trên (ví dụ `AUTH-TOKEN-STRATEGY.vi.md`, `s03-account-recovery-evidence.md`, `S21-S43-TREATMENT-DOSSIER.md`, `S62-TREATMENT-PLAN-REVISION.md`, `UC36-S07-S08.md`, `S58-DCMS-132-EXPORT-AUDIT.vi.md`) là lịch sử; tài liệu này thay thế phần mô tả các route đó.
