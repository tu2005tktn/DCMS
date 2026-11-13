# Kế hoạch thực thi System Test NFR — 11/11/2026

Nguồn bố cục: `Report-5.4_System Test_NFRs.xlsx`. Người dùng yêu cầu tự xây dựng tiêu chí NFR phù hợp DCMS và thực thi, không giới hạn 5 dòng mẫu. Giữ ID gốc, thay ví dụ customer/order bằng tài liệu hồ sơ DCMS, bổ sung tiêu chí và worksheet khi cần. Ngưỡng bên dưới là tiêu chí đề xuất cho lần chạy local này, chưa phải SLA được duyệt. Đây là kiểm thử kỹ thuật trên dữ liệu tổng hợp, không phải nghiệm thu lâm sàng.

## Phạm vi và cách đánh giá

- **TC-SEC-001**: mẫu customer/order không có trong DCMS. Chạy ca tương đương trên tài liệu hồ sơ: tạo tệp tổng hợp qua API, chứng minh người có quyền đọc được cùng tệp trước khi thử tài khoản Public/Patient khác, Admin, anonymous và URL tĩnh đoán được. Chỉ Pass trong phạm vi ánh xạ DCMS, nêu rõ khác biệt với order id=2. Kiểm HTTP thực qua Kestrel, PostgreSQL schema riêng, private filesystem và ClamAV thật. Không sử dụng hồ sơ bệnh nhân có sẵn.
- **TC-SEC-002**: cần URL staging HTTPS và định nghĩa BF-01. Local HTTP không chứng minh HTTPS/mixed-content; ghi Blocked nếu chưa có đầu vào.
- **TC-PERF-001**: Chromium, một người dùng, không throttle CPU/network, API thật; đo từ navigationStart tới dashboard có dữ liệu/empty state hoàn tất. Ghi riêng cold browser cache và warm cache, không dùng load event đơn thuần thay cho dashboard sẵn sàng. Target <2 giây. Nêu rõ dev hay production preview, máy local và số mẫu.
- **TC-PERF-002**: tệp PDF tổng hợp đúng 5,000,000 byte (5 MB thập phân), multipart POST API thật qua loopback Kestrel. Thời gian từ trước gửi request tới nhận đủ JSON; yêu cầu HTTP 201, đúng kích thước/hash và đọc lại đúng bytes. ClamAV thật; ghi trạng thái scan. Target <10 giây. Đây là đo upload API, chưa bao gồm thao tác file picker trên UI.
- **TC-PERF-003**: 20 tài khoản Public tổng hợp với 20 session riêng, chạy đồng thời 60 giây; luân phiên GET auth/me và danh sách lịch hẹn upcoming, think time 250 ms/request/user. Error rate <1%, p95 <1 giây, throughput >=10 request/giây. Không tính thời gian tạo tài khoản/sign-in hoặc warmup vào tải đo. Không ngoại suy thành khả năng tải production.
- **TC-PERF-004**: 30 GET auth/me liên tiếp, p95 <500 ms, 100% HTTP 200 và payload hợp lệ.

## Tiêu chí bổ sung trước khi chạy

| ID | Tiêu chí / điều kiện đạt |
|---|---|
| TC-SEC-003 | Anonymous không đọc được auth/me và clinical-documents: HTTP 401. |
| TC-SEC-004 | Bearer token giả bị từ chối 401, không trả dữ liệu người dùng. |
| TC-SEC-005 | Thu hồi quyền chi nhánh có hiệu lực ngay: cùng tài liệu đang đọc được trả 403 sau khi membership bị ngừng; upload cũng bị từ chối. |
| TC-SEC-006 | Nội dung tài liệu trả Cache-Control no-store và X-Content-Type-Options nosniff. |
| TC-SEC-007 | Server từ chối MIME/chữ ký giả, >20 MiB và batch >10 file: 400. |
| TC-SEC-008 | Origin không được phép không nhận Access-Control-Allow-Origin, preflight không cấp quyền. |
| TC-SEC-009 | Dependency audit không có high/critical đã được công cụ báo; ghi riêng .NET và npm. |
| TC-REL-001 | Gửi lại cùng upload requestKey/payload trả cùng documentId, không nhân bản tệp. |
| TC-REL-002 | Ba lần upload/download giữ nguyên 5,000,000 byte và hash SHA256. |
| TC-REL-003 | PendingScan/Rejected không được đọc nội dung, Ready mới đọc được; scanner thiếu/lỗi không tự chuyển Ready. |
| TC-REL-004 | Dashboard khi API 503 hiển thị lỗi và nút Thử lại; sau phục hồi GET lịch hẹn thành công. Chỉ quan sát method của endpoint lịch hẹn, chưa kết luận mọi endpoint không ghi. Fault injection ở browser, không đánh sập database dùng chung. |
| TC-USE-001 | Dashboard tại 390/768/1440 px không tràn ngang >1 px, liên kết đặt lịch chính hiển thị; thao tác Tab/Enter kiểm ở TC-USE-002. |
| TC-USE-002 | Các liên kết đặt lịch/lịch hẹn có accessible name; Tab đưa focus tới control tương tác và Enter mở đúng route. Không suy diễn thành chứng nhận WCAG đầy đủ. |
| TC-USE-003 | Không có uncaught JavaScript exception trên dashboard thật ở các kích thước đã đo. |
| TC-MAINT-001 | .NET 10 restore/build và React TypeScript/Vite build thành công, không có compile error. Cảnh báo được ghi riêng. |

HTTP Kestrel fixture dùng cổng 5080 khi chạy browser tùy chọn; Vite 5173 gọi cùng API/schema kiểm thử. Browser đo trước load test để tránh ảnh hưởng lẫn nhau. Security và upload dùng ClamAV thật; các regression file-state hiện có dùng scanner stand-in có kiểm soát và được ghi rõ.

## Bằng chứng và giới hạn

Lưu báo cáo đã điền riêng trong `outputs/`, không ghi đè mẫu nguồn. Ghi commit, thời điểm Asia/Ho_Chi_Minh, môi trường, kích thước, số mẫu, kết quả HTTP và số đo. Không lưu mật khẩu, token, connection string hoặc dữ liệu bệnh nhân. Bài kiểm thử bổ sung tái dùng fixture schema `dcms_test_*`; schema được giữ theo quy ước hiện có, không xóa dữ liệu development. Các adapter email/realtime của fixture là stand-in, không nằm trong luồng đo tệp.

Không sửa mã nghiệp vụ trong lần kiểm thử này. Lỗi thực thi và thiếu điều kiện được ghi đúng trạng thái; không thay bằng kết quả kỳ vọng.

## Kết quả thực thi

Báo cáo `outputs/nfr-20261111/Report-5.4_System Test_NFRs_Executed.xlsx`: **21 tiêu chí, 18 Pass, 2 Fail, 1 Blocked**. Đây là số tiêu chí NFR, không phải số test method xUnit.

- 20 tài khoản/session riêng, 60,26 giây: 4.227 request HTTP 200, 0 lỗi, p95 22,73 ms, 70,15 request/giây. Workload đọc auth/me và lịch hẹn upcoming; không đại diện booking/payment hoặc soak test.
- Ba upload PDF đúng 5.000.000 byte: 476,08 / 110,45 / 104,94 ms; tất cả HTTP 201, ClamAV thật trả Ready, download khớp bytes.
- **NFR-01 / Fail**: audit báo `System.Security.Cryptography.Pkcs 10.0.0` có advisory High và Moderate. npm báo Vite 6.4.4 Moderate. Chưa kiểm chứng exploit; chưa sửa dependency trong nhiệm vụ kiểm thử.
- **NFR-02 / Fail trong dev**: dashboard mẫu cold đầu 4.152 ms >2.000 ms, có chi phí khởi tạo/transform Vite; hai cold tiếp theo 388/451 ms, warm 261/283/267 ms. Đo production trước khi quy nguyên nhân tối ưu.
- **Blocked**: HTTPS staging chưa có môi trường/URL/chứng thư để kiểm chứng. Không suy diễn từ HTTP localhost.
- Năm regression về file-state, chữ ký/dung lượng/batch, quyền bị thu hồi và hash tamper đều Pass; một HTTP NFR test và một scanner-unavailable test cuối đều Pass. Lần chuẩn bị đầu có lỗi generator PDF thiếu 1 byte, đã sửa trước các số đo hợp lệ; không tính thành defect sản phẩm.

## Chạy lại

`SystemNfrReportTests` là opt-in, tránh đưa tải 60 giây và phụ thuộc ClamAV vào mọi lần chạy suite thường:

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml --profile clinical up -d
# Chờ pg_isready và ClamAV healthy trước khi chạy.
. .\infrastructure\scripts\import-development-env.ps1
$env:DCMS_RUN_SYSTEM_NFR = '1'
$env:DCMS_NFR_EVIDENCE_PATH = Join-Path (Get-Location) 'outputs/nfr-20261111/http-evidence.json'
& $dotnet test backend/tests/DCMS.IntegrationTests/DCMS.IntegrationTests.csproj --filter 'FullyQualifiedName~SystemNfrReportTests'
```

Browser là bước tùy chọn trước load test: chạy Vite 5173; đặt `DCMS_NFR_BROWSER_SCRIPT` tới helper `browser.mjs` và `DCMS_NFR_BROWSER_NODE` tới node có Playwright. Khi bật helper, fixture dùng Kestrel 5080 để Web gọi cùng schema, tự truyền token qua environment của process và không ghi token ra tệp. Bộ helper, mã test và bằng chứng được đóng gói cùng báo cáo. Không có biến opt-in thì hai NFR test được Skip.
