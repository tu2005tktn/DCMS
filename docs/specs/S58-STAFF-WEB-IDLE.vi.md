# S58 11 — Tự đăng xuất staff web

Trạng thái: In Progress. Mở rộng AuthSession và refresh chain hiện có. Nguồn
S58/UC51 v2.5, DCMS-119 và DCMS-128 chưa hiện diện trong checkout; phạm vi này
là idle policy riêng, chờ nối vào general settings chung.

## Chính sách

- Staff web luôn có idle timeout 5/10/20/30/60/90/120 phút, mặc định 20.
  Patient/Public giữ chính sách session hiện tại. ClientKind do luồng đăng nhập
  xác định, client không được tự đổi kind qua activity.
- `AuthSession.LastInteractiveActivityAt` khởi tạo lúc đăng nhập, chỉ cập nhật
  qua `POST /api/auth/session/activity` với bearer session hợp lệ. Server dùng
  UTC của mình; tối đa một ghi/30 giây/session. Hai tab dùng cùng mốc DB.
  Polling, SignalR, background load và refresh chỉ cập nhật LastSeenAt, không
  gia hạn idle. Activity là tín hiệu UI, không chứng minh con người có mặt.
- Middleware xác thực kiểm `now < lastInteractiveActivityAt + timeout` trước
  mỗi request staff web. Sau deadline, revoke session và refresh active trong
  cùng transaction; access/refresh cũ trả 401 MSG07. Refresh không hồi sinh.
  Đổi policy áp ở request sau theo mốc activity cũ. Giảm timeout có preview
  số phiên đang bị ảnh hưởng; tăng timeout không khôi phục session revoked.
- UI staff web gửi activity có throttle 30 giây khi có click/keyboard/touch
  đáng tin cậy, không gửi từ focus, timer, polling, SignalR hay refresh.
  Hiện deadline server, cảnh báo sắp hết và lối đăng nhập lại. Không tự lưu
  clinical draft. Draft chỉ ở memory cùng actor/ngữ cảnh, dọn khi session mất;
  không ghi hồ sơ vào localStorage.

## API và lỗi

- `GET /api/settings/staff-idle`: owner đọc `{version,effectiveAt,staffIdleTimeoutMinutes}`.
- `POST /api/settings/staff-idle/preview`: owner gửi `{staffIdleTimeoutMinutes}`,
  server trả `affectedActiveSessions`, không ghi.
- `PUT /api/settings/staff-idle`: owner gửi
  `{expectedVersion,requestKey,reason,staffIdleTimeoutMinutes}`; commit
  policy/history/command receipt nguyên tử; retry cùng key/payload trả cùng
  kết quả, thay payload/stale trả 409 MSG111.
- `GET /api/auth/session/idle`: staff web đọc deadline server và timeout.
- `POST /api/auth/session/activity`: staff web, body rỗng; server chỉ lấy actor
  và session từ bearer, trả deadline/giờ server. Patient/Public trả 403 MSG06.

Không nhận timestamp/clientKind/actor/session ID từ body activity. Không chấp
nhận timeout ngoài enum. Endpoint owner đòi role hiện tại; sẽ nối action grant
và clinic scope của DCMS-119 khi resource chung sẵn sàng.

## Nghiệm thu

- SET-AT-23: qua 5 phút dù poll/refresh/hai tab vẫn 401; refresh revoked;
  Patient vẫn hoạt động theo policy riêng.
- SET-AT-24: activity trước hạn gia hạn bằng server time; activity sau hạn bị
  chặn; giảm không reset, tăng không hồi sinh.
- SET-AT-25: hết hạn khi nhập, không commit; đăng nhập actor khác không thấy
  draft trước; UI báo đăng nhập lại rõ.
