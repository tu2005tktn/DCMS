# Kiểm tra sau khi tích hợp main — 02/11/2026

Commit được kiểm tra: `dd5077754ab7cb098ce91a494cf36c8b27c35ac2` (PR #38 của Vinh), nhánh local `tư`.

## Môi trường và phạm vi

- .NET SDK 10.0.401; Chrome headless/Playwright; PostgreSQL 17 qua Docker Compose.
- Tất cả API có ghi dữ liệu sử dụng schema synthetic riêng `dcms_reception_e2e_84a40bda31554e048d55d2fba6b2419d`; integration fixture tự tạo schema `dcms_test_*`.
- Integration dùng pipeline ASP.NET và PostgreSQL thật, nhưng contact/email delivery và realtime notifier được thay bằng fake adapter; không phải kiểm thử gửi email/SignalR ra ngoài.
- API kiểm thử lắng nghe 5084 và 5080 (cùng một instance/schema riêng để hỗ trợ test cũ hardcode 5080); Vite 5173 trỏ tới API 5084. Không migrate/seed database development mặc định.
- PostgreSQL khởi đầu đang recovery sau lần tắt trước; chỉ chạy integration sau khi `pg_isready` thành công. API `/health` trên cả hai cổng trả API/database healthy.
- Lượt kiểm tra ban đầu không sửa implementation hoặc test đã theo dõi trong Git; bản sửa F01 tiếp theo được ghi riêng bên dưới. File local `REALISTIC-DEMO-DATA.vi.md` giữ nguyên SHA-256 `D1815FCDD8EBFBEDA7F0658B4DC7EBFB52C6ED7706F19EFC0EB167DB67FF7B28`.

## Kết quả

| Kiểm tra | Kết quả |
| --- | --- |
| Restore + build backend | Đạt, 0 lỗi; còn NU1902/NU1903 cho `System.Security.Cryptography.Pkcs` 9.0.0 |
| `npm install` + production build web | Đạt; còn cảnh báo SignalR annotation và bundle lớn |
| Unit test | 79/79 đạt |
| Integration PostgreSQL, lượt cuối | 360/362 đạt; 2 lỗi assertion auth, không còn lỗi cạn kết nối |
| Browser mock, 29 ca | 23 đạt, 6 lỗi |
| Browser API thật, 12 ca phân biệt sau recheck | 8 đạt, 4 lỗi |
| Probe bổ sung cho chức năng mới, 11 nhóm | 10 đạt, 1 lỗi |

Không cộng các nhóm thành một tỷ lệ chung: probe gồm nhiều assertion và một số luồng được kiểm tra lại sau khi xử lý giới hạn kết nối.

## Chức năng đã kiểm tra trực tiếp

- Route thiết lập mới và danh mục chẩn đoán: tạo bằng Owner, Dentist lấy được options cùng version; route legacy trả 404.
- Dentist tạo provider/labo và loại công việc labo; không được quản lý thuốc; Assistant không có quyền provider.
- Đọc/preview sequence không tiêu số, không cho lùi, tăng mốc và retry không tăng lần hai.
- Bốn yêu cầu tạo bệnh nhân cùng key đồng thời trả cùng hồ sơ; key cũ khác payload bị 409. Số hồ sơ của nhóm tăng lên 1 và giảm về 0 sau lưu trữ.
- Đặt lịch → check-in → bắt đầu khám qua API thật.
- Đơn thuốc mua ngoài: chọn thuốc active/eligible không quản lý tồn kho, thêm/replay/sửa, stale version 409, số lượng 0 bị 400; sửa lời dặn không thay đổi catalog; trang đơn thuốc tải được với Dentist.
- Phiếu thu: preview không tiêu sequence; tạo khoản phí và thu tiền mặt; API in trả số tiền 50.000/còn lại 150.000; retry ghi nhận in chỉ tạo một `RECEIPT_PRINT_EVENT` và không cấp thêm số.
- Idle: polling không đổi thời điểm tương tác; touch/replay có version, touch sau 30 giây tăng version; hết hạn khiến access và refresh đều 401, không còn refresh ACTIVE. Test này dịch timestamp của **một session synthetic trong schema riêng**, không chờ thật 121 phút; không phải bằng chứng idle tự nhiên trên thiết bị thật.
- UI Owner/Dentist với API thật, sidebar ở 390/768/1440, không tràn ngang toàn trang. Đã xem ảnh mobile và phiếu thu.
- Browser thật: CLS → báo giá → thực hiện → tiền mặt một phần → chuyển khoản Pending → xác nhận; cấu hình giá khám → kết thúc khám → thu một phần → xác nhận chuyển khoản đều đạt khi chạy lại.

## Lỗi và giới hạn còn lại

### F01 — Phiếu thu không áp dụng tùy chọn hiển thị phương thức thanh toán (đã sửa)

Đã tái hiện bằng API và browser thật:

1. Lưu General Settings với `receiptPrint.paymentMethod=false`.
2. API in phiếu thu trả `settings.paymentMethod=false`, cùng version thiết lập.
3. Mở `/payments/{id}/print`: vẫn có dòng **Phương thức: Tiền mặt**.

Nguyên nhân trước khi sửa: `ReceiptPrintPage.tsx` chưa đọc object `settings` của DTO để quyết định render các trường. Bằng chứng lỗi ban đầu: `.artifacts/verification-20261102/receipt-settings-evidence.json`, `receipt-setting-false.png`, `feature-probes.json`.

Bản sửa ngày 02/11/2026, theo yêu cầu sửa phần in phiếu thu:

- Đã cập nhật đặc tả `S58-GENERAL-SETTINGS.md` trước khi sửa implementation.
- Trang in dùng các cờ `paymentMethod`, `showReceiptNumber`, `previouslyPaid`, `remaining`, `paymentNote`, `internalUseNote`, `signatureLines` từ DTO. Tắt trường thì cả nhãn và giá trị không có trong nội dung màn hình/in.
- Khi in, commit DTO mới nhất từ POST trước khi gọi `window.print()`; retry lỗi mạng giữ request key, chặn in trùng lúc đang gửi, lỗi giữ phiếu để thử lại. Điều khiển thao tác ẩn trong print media.
- Mặc định người nộp/người thu không thay tên đã ghi trên phiếu cũ. Không bổ sung dữ liệu mà DTO chưa có (địa chỉ/điện thoại, bác sĩ/trợ thủ, tổng phí/giảm giá/tổng công nợ); các toggle đó vẫn chưa được nghiệm thu đầy đủ.
- Web production build đạt. Ba ca hồi quy `receipt-print.spec.ts` đạt **3/3**: bật/tắt và print media; Transfer/các thông tin bắt buộc; lỗi mạng/retry cùng key và mẫu đổi sau lúc mở trang. Nhóm sidebar đạt **5/5** trong lượt chạy cùng thay đổi này.
- Browser với API/PostgreSQL thật trên schema riêng đạt: bảy cờ cùng tắt/cùng bật, thay mẫu sau mở trang rồi in, ẩn nút/sidebar khi in. Đối chiếu bill và sequence trước/sau thao tác in bằng nhau; số tiền và số phiếu giữ nguyên. Có tạo thêm phiếu synthetic trong schema riêng để kiểm tra ghi chú. Sau kiểm tra khôi phục nội dung cấu hình ban đầu bằng một version mới.
- Hộp thoại in native được thay bằng hàm ghi nhận DOM trong browser test; đã kiểm CSS print và ảnh, chưa thử máy in vật lý.

Bằng chứng sau sửa: `receipt-fix-browser.json`, `receipt-fix-real.json`, `receipt-fixed-off.png`, `receipt-fixed-on.png`, `receipt-fixed-paper.png` trong `.artifacts/verification-20261102/`.

### F02 — Ca browser chọn hai vật liệu cùng tên khác nhóm vẫn timeout

`patient-treatment-ui.spec.ts:287` lỗi trong cả lượt 29 ca và lượt chạy riêng. Lượt đầy đủ dừng khi click `Composite A2 (Vật liệu khác)` ở dòng thứ hai: option không ổn định rồi không còn visible. Đây là **browser mock**, chưa xác nhận lỗi lưu dữ liệu với API thật và chưa xác định nguyên nhân nằm ở automation hay thao tác chuột của UI.

### F03 — Các bài browser chưa cập nhật theo contract hiện tại

- Năm ca `system-settings.spec.ts` không mock `/api/system-settings/settings/capabilities`, nên trang nhận lỗi “Không tìm thấy”; các assertion CRUD phía sau không được kiểm tra.
- `clinical-diagnoses.integration.spec.ts` tạo danh mục qua `/api/settings/catalogs/diagnoses` đã bị gỡ. Probe mới xác nhận đường dẫn mới tạo và đọc options được; chưa chạy lại toàn bộ thao tác chọn/lưu snapshot chẩn đoán trên UI bằng test đã cập nhật.
- Một ca `staff-idle.spec.ts` gọi `/api/settings/staff-idle` và `/staff-idle-settings` cũ; một ca khác đòi tab `Bảng kê` không còn trong trang hiện tại. Ca cảnh báo idle mock đạt; chưa xác nhận lại việc dọn receipt draft qua toàn bộ UI khi đổi actor.
- `reception.integration.spec.ts:40` chọn `TreatmentContinuation` mà không liên kết hồ sơ điều trị, rồi kỳ vọng màn khám. UI hiện hiển thị cảnh báo cần liên kết hồ sơ, phù hợp nhánh điều trị tiếp hiện tại. Check-in/hàng chờ trước assertion này thành công; 4 ca reception còn lại đạt.

### F04 — Hai ca integration auth chưa đạt

- `GoogleApiTests.ExistingLocalEmailCreatesSeparateGoogleAccount`: kỳ vọng 200 OK, thực tế 409 Conflict.
- `RegistrationApiTests.RepeatedAndConcurrentRequestsCreateOneAccount`: kỳ vọng một yêu cầu tạo thành công, hai phản hồi đồng thời đều 409 Conflict.

Hai assertion tái hiện trong cả lượt đầu và lượt chạy lại đã bỏ lỗi kết nối. Chưa sửa code và chưa chạy so sánh baseline trước pull, nên không quy nguyên nhân cho PR của Vinh.

## Lượt chạy lại và bằng chứng

Lượt integration đầu: 333/362 đạt, 29 lỗi; 27 lỗi do thiếu kết nối PostgreSQL (26 lỗi 53300 trực tiếp, một lỗi transient có nguyên nhân 53300), cùng hai assertion auth. Một số browser chạy đồng thời gặp `MSG144` vì cùng giới hạn kết nối. Lượt integration cuối dùng `Pooling=false`, `xUnit.MaxParallelThreads=1`, `xUnit.ParallelizeTestCollections=false` trong **môi trường kiểm thử**, không thay cấu hình sản phẩm. Các browser bị ảnh hưởng đã được chạy lại.

Lượt cuối thực thi đủ 362/362 ca, 360 Passed, 2 Failed, 0 Aborted/NotExecuted. Browser mock gồm sidebar 5/5, session renewal 5/5, patient treatment 13/14 và material catalogs 0/5. Browser API thật sau recheck gồm finance 2/2, examination billing 1/1, reception 4/5, clinical diagnoses 0/1 và staff idle 1/3 (trong đó ca cảnh báo idle dùng mock).

Các file bằng chứng trong `.artifacts/verification-20261102/` (ignored): `unit.trx`, `integration.trx`, `integration-recheck.trx`, `browser-mock.json`, `browser-real.json`, `browser-real-recheck.json`, `exam-billing-recheck.json`, `material-recheck.json`, `feature-probes.json`, log và screenshot tương ứng. Không lưu password/token vào báo cáo hoặc artifact.
