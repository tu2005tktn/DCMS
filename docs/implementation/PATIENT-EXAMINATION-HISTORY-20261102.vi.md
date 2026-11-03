# Lịch sử khám trong hồ sơ bệnh nhân — 02/11/2026

Đặc tả: [PATIENT-EXAMINATION-HISTORY.vi.md](../specs/PATIENT-EXAMINATION-HISTORY.vi.md).

## Kết quả

- Dùng liên kết bền vững `VisitExamination.VisitId -> Visit.PatientId` hiện có. Kết thúc lượt không cần thêm lần lưu hồ sơ; không tạo bản sao phiếu, không tạo hồ sơ điều trị.
- Màn khám sau khi kết thúc hiển thị xác nhận đã lưu lịch sử và liên kết thẳng tới đúng hồ sơ/tab Khám. Các thao tác thu ngân/handoff hiện có được giữ.
- Tab Khám có tên/mã bệnh nhân, trạng thái kết thúc, thời gian thực tế lấy từ audit `CompleteVisit`, tìm kiếm chẩn đoán tổng hợp, lọc lượt đã kết thúc và bảng xem chi tiết nội dung thích ứng với màn hình. Có liên kết mở phiếu gốc để xem các phần chuyên biệt theo quyền.
- Hồ sơ làm mới khi kích hoạt tab, tải lại hồ sơ và khi nhận sự kiện focus/visibility của trình duyệt. Đính chính hiển thị cùng lượt với phiên bản mới, giữ thời điểm kết thúc.
- API chỉ thêm trường đọc `completedAt` nullable. Không cần migration; thời điểm kết thúc của dữ liệu cũ không có audit vẫn null.

## Kiểm chứng

- .NET 10 restore/build solution thành công. Cảnh báo dependency Pkcs NU1902/NU1903 có sẵn vẫn hiện.
- `npm install` và `npm run build` thành công; Vite còn cảnh báo chunk lớn và annotation SignalR có sẵn.
- `ExaminationApiTests` và `PatientApiTests`: **89/89 qua** trên PostgreSQL với schema riêng của fixture. HTTP/auth/EF thật; contact/email/realtime/scanner dùng adapter kiểm thử theo fixture hiện có.
- Ca vòng đời mở rộng kiểm tra: phiếu xác nhận nhưng lượt đang mở chưa có completedAt; kết thúc và retry chỉ một dòng lịch sử; nội dung đúng; completedAt khớp audit; đính chính giữ completedAt và cập nhật phiên bản; hồ sơ bệnh nhân khác cùng số điện thoại không chứa lượt này.
- Playwright **5/5 qua**: **2 ca API/PostgreSQL thật** trên runtime cách ly và **3 ca UI mock**. Luồng thật: tiếp nhận, bắt đầu, hoàn tất phiếu, kết thúc qua UI, mở đúng hồ sơ, đọc chi tiết, tải lại vẫn còn và mở lại phiếu; hồ sơ đã mở sẵn cập nhật khi trở lại tab Khám sau khi kết thúc qua API. UI mock kiểm tra lọc trạng thái, nội dung, nạp lại tab/hồ sơ, sự kiện focus và lỗi/thử lại.
- Đã xem ảnh desktop và màn hình 390px; phần chi tiết mở trong drawer của cửa sổ, không bị bảng ngang hoặc khung tab cắt nội dung.
- Health API/database tại 5080 đều healthy; web 5173 HTTP 200. API runtime đang dùng Debug mới tại 5084 và 5080 trên cùng schema cách ly, thông tin trong `.artifacts/verification-20261102/runtime.json`.
- `git diff --check` qua; tài liệu demo sửa sẵn của người dùng giữ nguyên SHA-256.

## Giới hạn bằng chứng

- Bộ chạy đầu với override xUnit single-thread/connection pooling gặp timeout ở ca có sẵn `RemovingScopedDiagnosisDoesNotReviveAggregateAliases`; đã chủ động dừng job đó. Ca vòng đời mục tiêu chạy riêng qua 1/1; chạy lại toàn bộ hai nhóm bằng cấu hình mặc định qua 89/89, không sửa nghiệp vụ để né timeout.
- Trình duyệt headless không phát focus khi chỉ gọi `bringToFront` giữa hai page trong lần thử đầu. Ca API thật dùng thao tác chuyển tab Khám của ứng dụng; handler focus/visibility được kiểm bằng sự kiện UI mock. Không tuyên bố đã kiểm chứng thao tác chuyển cửa sổ desktop thật.
- Danh sách giữ giới hạn 100 lượt gần nhất trong phạm vi chi nhánh được cấp quyền và thông báo giới hạn hiện có.
- Các báo cáo máy nằm trong `.artifacts/verification-20261102/`: `patient-examination-history-api-final.trx`, `patient-examination-history-live.json` và ảnh ở `history-live/`.
