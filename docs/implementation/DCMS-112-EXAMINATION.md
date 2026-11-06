# DCMS-112 — Contract và bàn giao khám theo Visit

## Gộp nhập kết quả toàn hàm vào vùng khám — 28/10/2026

Đã bỏ hai ô riêng Kết quả khám tổng quát và Chẩn đoán toàn hàm trước sơ đồ. Tất cả finding, kể cả Toàn hàm có sẵn, hiển thị trong danh sách răng/vùng chung; nút Thêm vùng khám dùng cho vùng cụ thể hoặc Toàn hàm. Tổng kết và validation focus trỏ dòng chung. Không đổi API/dữ liệu lưu trữ. Web production build đạt; browser dùng API thật xác nhận finding Toàn hàm cũ còn nguyên mô tả/chẩn đoán, hai ô trùng không còn, thêm vùng và quay lại sửa từ Tổng kết focus đúng ô. Kiểm tra trên browser không lưu thay đổi vào hồ sơ demo. Ảnh local: `.artifacts/unified-regions/edit-region.png`.

Ngày kiểm chứng: 26/10/2026. [Đặc tả](../specs/S19-S20-DCMS-112.md). Branch: `feat/dcms-112-examination`. [Draft PR #27](https://github.com/tu2005tktn/DCMS/pull/27).

Tái sử dụng treatment foundation `origin/vinh@fc942b6` qua merge `d162045`; giữ tiếp nhận DCMS-111 `caa01ef`. UI S20 lấy từ `origin/TriNM@1b4d72c` (list/upload/detail/viewer/types), nối API thật và bỏ preview khỏi đường chạy. Không thay backend kế hoạch/actual bằng engine thứ hai. Migration `20261026035110_VisitExaminationsAndClinicalDocuments` bổ sung khám/file/audit/receipt/review vào DbContext chung.

## Mở và chạy

Header bệnh nhân bổ sung ngày 26/10/2026: hiển thị mã/họ tên, tuổi, giới tính và ngày sinh trực tiếp ở đầu S19. Workspace trả thêm `patient { id, patientCode, fullName, birthDate, gender, ageYears }`, lấy đúng Patient của Visit; tuổi tính ở server theo ngày phòng khám UTC+07 và ngày sinh, không dùng tuổi của người đặt hay đồng hồ browser. Thiếu ngày sinh/giới tính hiện “Chưa rõ”; dưới 1 tuổi hiện “Dưới 1 tuổi”. Đây là dữ liệu chỉ đọc, không thêm field yêu cầu bác sĩ nhập hoặc migration.

Kiểm chứng header: backend/web build pass; 49/49 reception/examination API tests và 1/1 snapshot model test pass, gồm ngày sinh khác attendee, sinh nhật hôm qua/hôm nay/ngày mai, thiếu/tương lai và trẻ dưới 1 tuổi; 2/2 Playwright gọi API thật pass (19.4 giây), kiểm mã/họ tên + tuổi + giới tính + ngày sinh và ba giá trị “Chưa rõ” khi thiếu dữ liệu. API 5084 đã được cập nhật với cùng schema test, không seed lại dữ liệu của người dùng.

Cập nhật sơ đồ răng 26/10/2026: tách lựa chọn vĩnh viễn/sữa/hỗn hợp. Mặc định vĩnh viễn khi chưa có finding răng; mở lại chọn nhóm theo finding đã lưu. Hai nhóm được trình bày riêng với hàm trên/dưới. Chuyển cách xem vẫn giữ tất cả findings, cảnh báo khi có kết quả ở nhóm đang ẩn và dùng được ở chế độ chỉ đọc. Đây là thay đổi UI, giữ nguyên API/FDI và không có migration mới.

Kiểm chứng thay đổi sơ đồ: web build pass; 2/2 Playwright Chrome dùng API thật pass (14.4 giây), bao gồm mặc định 32 vị trí, đổi sang 20 vị trí răng sữa, chọn findings ở hai nhóm rồi lưu/mở lại giữ nguyên, tự mở hỗn hợp khi đã có cả hai và đổi nhóm xem trên bản xác nhận vẫn cấm sửa. Kiểm tra trực quan thêm ảnh `.artifacts/tooth-chart/permanent.png` và `primary.png`.

Đăng nhập `dentist.demo` → Tiếp nhận và hàng chờ → Hàng chờ và lượt khám → Mở lượt khám → Bắt đầu khám. Không nhập Visit/Patient/version bằng tay. Trang `/visits/{visitId}` có khám/sơ đồ FDI, ảnh/tài liệu S20, lịch sử, tư vấn và điều trị cũ. Chỉ bác sĩ được phân công mới sửa/xác nhận/review; trợ lý đọc và attach trong chi nhánh đang có quyền. Trang Patient → Hình ảnh & Tệp dẫn tới cùng UI S20.

Nháp cho phép thiếu triệu chứng/chẩn đoán/tình trạng răng/chi tiết tiền sử. Khi xác nhận cần triệu chứng, chẩn đoán, ít nhất một finding có răng/vùng + tình trạng; tiền sử/dị ứng/thuốc phải được bác sĩ đánh dấu đã xác minh. Unknown được giữ là Unknown, không đổi thành None. Bản đã xác nhận chỉ đọc. Lượt mới đọc snapshot tiền sử gần nhất, yêu cầu xác minh lại và không cập nhật ngược lượt cũ.

`CLINICAL_DOCUMENTS_STORAGE_PATH` phải là đường dẫn tuyệt đối ngoài repo/web root; PowerShell import mặc định dùng `%LOCALAPPDATA%\DCMS\clinical-documents`. Không đưa thư mục file vào Git. Đặt `CLAMAV_HOST` và `CLAMAV_PORT` cho daemon nội bộ. Thiếu scanner hoặc timeout → PendingScan, không content/review. Background worker thử lại mỗi 30 giây. Adapter dùng [ClamD INSTREAM](https://docs.clamav.net/manual/Usage/ClamdProtocol.html): chunk length big endian, chỉ phản hồi sạch `stream: OK` được Ready. Cấu hình daemon phải cho phép StreamMaxLength ≥20 MiB và phù hợp MaxFileSize/MaxScanSize; không mở socket TCP ra mạng không tin cậy. Không có endpoint nhân viên tự đổi trạng thái scan.

## Ví dụ contract

IDs/version dưới đây là biến lấy từ queue/workspace/response. Frontend giữ requestKey của cùng command khi retry sau mất phản hồi; đổi payload dùng key mới. Stale 409/MSG111 giữ form và yêu cầu tải phiên bản. Xác nhận trả workspace + allowedActions `handoff`; không tự hoàn tất Visit/Appointment.

```http
POST /api/visits/{visitId}/start
Content-Type: application/json

{"patientId":42,"expectedVersion":1,"requestKey":"11111111-1111-4111-8111-111111111111"}
```

```http
PUT /api/visits/{visitId}/examination
Content-Type: application/json

{
  "patientId":42,"expectedVersion":2,
  "requestKey":"22222222-2222-4222-8222-222222222222",
  "content":{
    "symptoms":"SYNTHETIC đau răng 16",
    "medicalHistory":{"state":"Unknown","detail":null,"verified":true},
    "allergies":{"state":"None","detail":null,"verified":true},
    "medications":{"state":"Present","detail":"SYNTHETIC thuốc đang dùng","verified":true},
    "findings":[{"tooth":16,"region":null,"condition":"SYNTHETIC kết quả khám"}],
    "diagnosis":"SYNTHETIC chẩn đoán", "notes":null
  },"confirm":true
}
```

`GET /api/visits/{id}/clinical-handoff` trả `{visitId,patientId,branchId,version,examination,teeth,documents,source}` cho DCMS-113/114/115. Source cũ giữ ID/version của hồ sơ; bên nhận phải kiểm phiên bản hiện hành. Không tạo hồ sơ khi start/lưu/xác nhận. UI tư vấn mang chẩn đoán/Patient/branch/dentist sang DossierList; mở hồ sơ/điều trị cũ mang visitId tự động. Không yêu cầu actual hoặc khoản tiền để chuyển bước.

Upload S20: `POST /api/clinical-documents`, multipart `file` hoặc `files` (1–10) + `metadata` (string JSON hoặc JSON Blob của UI cũ). Metadata gồm `patientId,visitId,dossierId?,planId?,documentType,source,sourceDate,description,requestKey`. Ngày nguồn không sau ngày phòng khám. Investigation/consentVersion chưa có nguồn xác minh thì từ chối, không chấp nhận ID tùy ý. Context optional dossier/plan phải đúng Patient/branch; Visit bắt buộc. Một file trả `{document,documents:[{document,error}]}`; nhiều file trả `documents`, mỗi file có kết quả riêng. Single file validation thất bại trả 400; batch mixed giữ kết quả từng file. Khóa retry kiểm metadata + tên/MIME/hash nội dung/thứ tự file. Giới hạn request transport 210 MiB, 20 MiB/file, 10 file, mô tả 1000 ký tự. Không trả private storage path.

Document DTO có `documentId,version,fileName,contentType,sizeBytes,documentType,source,sourceDate,description,context,uploaderName,uploadedAt,fileState,rejectReason,allowedActions,reviews[]`. File mới có `reviews:[]` kể cả scanner trả Ready. Content kiểm quyền/nguồn/trạng thái + hash mỗi lần; no-store, nosniff, sandbox, attachment mặc định. `POST /api/clinical-documents/{id}/review` nhận `{patientId,visitId,expectedVersion,requestKey}`; chỉ bác sĩ của Visit đang InProgress, file Ready, cùng Patient/branch. Download/View không tự tạo review. Review có audit/by/time/visit và tăng document version.

## Bằng chứng thực thi

Không dùng database thường để tạo fixture. API integration chạy qua ASP.NET TestServer, JWT/session thật, migrations EF + PostgreSQL schema ngẫu nhiên, private filesystem thật. Antivirus Ready/Rejected dùng `ExaminationTestScanner` có ghi rõ trong test; không coi đó là bằng chứng daemon antivirus thật.

| Kiểm tra | Kết quả |
| --- | --- |
| .NET 10 restore và Release build | Pass. Debug bị API đang chạy giữ DLL; không dừng API của người dùng |
| `npm install`, `npm run build` | Pass |
| Reception + Examination + TreatmentDossier/Progression/Revision + ClinicalWork API | **57/57 pass** sau thay đổi cuối backend |
| Toàn backend | **47/47 unit pass; 228/230 integration pass** |
| Hai lỗi hồi quy ngoài task | `GoogleApiTests.ExistingLocalEmailCreatesSeparateGoogleAccount` và `RegistrationApiTests.RepeatedAndConcurrentRequestsCreateOneAccount`: cùng lỗi 409 tái hiện **2/2 trên baseline d162045** trước thay đổi DCMS-112; các test đang kỳ vọng dùng chung email dù index email duy nhất. Không sửa chính sách auth trong task khám |
| Health API thường 5080 và API riêng 5084 | API/database healthy |
| Playwright Chrome gọi API thật 5084 / web 8084 | **2/2 pass**, 8.1 giây: queue → start → nháp có finding chưa đủ → reopen → S20 upload PendingScan → xác nhận → tư vấn → điều trị cũ đúng Visit/dossier; và concurrent save → giữ form → refresh version → lưu/mở lại. Screenshot đợi kết quả xác nhận tải xong và kiểm input đã khóa |

API coverage: đúng Visit từ check-in; start chuyển trạng thái lịch/lượt; nháp mở lại; xác nhận/lock; handoff có diagnosis/teeth/file; không tự tạo dossier/actual; nhiều Visit giữ kết quả cũ và mở dossier có sẵn; concurrency/replay/stale; sai Patient/role/branch/source; member bị thu hồi chặn replay/content; JPEG giả, 11 file, >20 MiB bị chặn; 10 file hợp lệ; batch mixed; PendingScan/Rejected chặn content/review; Ready tải được nhưng chưa review; review idempotent; scan worker không tự clinical review; hash nội dung thay đổi bị chặn; răng sữa hợp lệ/FDI sai bị chặn; không start/cancelled không ghi được.

Logs local (không commit credential, hồ sơ hay session): `.tools/dcms112-target-tests.log`, `.tools/dcms112-final-full-tests.log`, `.tools/dcms112-baseline-tests.log`. Browser screenshots: `.artifacts/dcms112-browser/`.

Tái chạy browser thật, không `page.route`/mock API:

```powershell
# Terminal riêng tại root, giữ environment sau khi dot-source:
. ./infrastructure/scripts/prepare-clinical-e2e.ps1
& $dotnet run --project backend/src/DCMS.Api -c Release --no-launch-profile
# Web ở terminal khác: VITE_API_BASE_URL=http://localhost:5084, Vite --port 8084
# Terminal test cần E2E_DEMO_PASSWORD từ env fixture, không hardcode:
cd web
$env:PLAYWRIGHT_USE_SYSTEM_CHROME='1'
npx playwright test --config=playwright.clinical.config.ts --reporter=line
```

## Ranh giới bàn giao

### Sửa tích hợp sau khi pull main ngày 26/10/2026

Main `63f8589` đã chứa PR #27/#28 nhưng merge chép lặp declaration của ReceptionFixture và các helper test, import VisitPage, cấu hình VisitModel/snapshot và đăng ký UnavailableReceptionSourceReader sau service thật. Sửa local giữ đúng một declaration/import/model configuration; IReceptionSourceReader vẫn dùng ReceptionTreatmentSourceReader để tìm hồ sơ cũ của đúng Patient/branch. Giữ màn examination S19 và expectation nguồn giả 404/MSG113. Không đổi API/business contract hay tạo migration mới; kiểm build backend/web, reception/examination API và snapshot model. Dữ liệu artifact local được stash và đối chiếu hash khi khôi phục. Các sửa tích hợp này chưa push trong yêu cầu chỉ pull main.

Kết quả sau sửa: .NET 10 restore/Release build và web build pass; 43/43 reception/examination API tests cùng 1/1 snapshot model test pass. `HEAD == origin/main == 63f8589` về commit; phần sửa integration và ghi chú này được giữ dưới dạng thay đổi local chưa commit/push. Stash `before-main-pull-20261026-dcms112` vẫn giữ để dự phòng.

DCMS-112 bàn giao kết quả khám/context và mở được UI tư vấn/điều trị; không tuyên bố post actual/tính phí/hoàn tất lượt đã được nghiệm thu xuyên suốt. Các port consent, charge, Labo, hoàn tất và own-record của DCMS-113/114/115 vẫn phải được module sở hữu nối/xác minh. Nền treatment cũ được giữ nguyên, bao gồm việc fail closed khi nguồn này thiếu. Không có daemon ClamAV thật trong môi trường kiểm chứng này; không đánh dấu pass khả năng phát hiện malware thực tế. Build còn cảnh báo package Pkcs có sẵn và bundle lớn; không thay dependency ngoài task.

## Bản cập nhật Khám riêng theo đặc tả mới — 26/10/2026

Phần này thay thế mô tả UI tư vấn/điều trị nhúng ở S19 trong các bằng chứng trước. Hồ sơ Patient có tab **Khám** với danh sách lượt/phiếu, lọc trạng thái, tìm chẩn đoán/bác sĩ và mở đúng Visit. S19 gồm Khám & sơ đồ răng, Ảnh & tài liệu, Tổng kết khám và Lịch sử khám; không tải dossier hay editor điều trị khi chỉ khám. Tổng kết có kết luận, dặn dò, hướng xử trí, nhu cầu quay lại/nơi chuyển khám. Lưu nháp được phép thiếu; hoàn tất cần nội dung đầy đủ. Bản xác nhận khóa sửa trực tiếp; đính chính bắt buộc lý do, tạo snapshot mới và giữ nội dung cũ để xem lại đầy đủ.

Hoàn tất khám giữ Visit/Appointment InProgress. **Tiếp tục điều trị** mở tab Điều trị & Thanh toán, kiểm Patient/Visit/phiên bản nguồn từ API rồi dùng dossier hiện có hoặc chủ động tạo hồ sơ; không tự sinh dossier, actual hay phí. **Kết thúc lượt khám** là command riêng, yêu cầu bác sĩ kiểm công việc, chặn actual Draft hoặc hướng tiếp tục điều trị chưa có Posted; hoàn tất Visit/Appointment cùng transaction. Start/end có audit thời điểm, không thêm cột thời gian vào Visit. Tạo actual Draft dùng chung lock với đóng lượt để tránh race. Quyền bác sĩ, chi nhánh, Patient, optimistic version và requestKey được kiểm ở server.

Kết quả kiểm chứng bản cập nhật:

| Kiểm tra | Kết quả |
| --- | --- |
| Reception, Examination, TreatmentDossier/Progression/Revision, ClinicalWork API | **69/69 pass** qua HTTP TestServer và PostgreSQL schema cách ly; có race tạo Draft/đóng lượt, stale/replay, đính chính sau đóng lượt và giữ snapshot cũ |
| Race tạo actual Draft/đóng lượt sau dọn cảnh báo analyzer | **1/1 pass**, Release |
| Playwright với API thật 5086 / web 8086, PostgreSQL schema riêng | **3/3 pass**: nháp/documents/confirm/handoff đúng Visit/dossier/tab Khám; giữ form khi stale; đính chính/lịch sử/kết thúc khám tư vấn không có dossier và không gọi API điều trị từ UI khám |
| Web production build; backend build qua integration test | **Pass**; còn cảnh báo Pkcs/SignalR annotation/bundle lớn có sẵn |

Logs local: `.artifacts/examination-redesign-regression-final-v2.log`, `.artifacts/examination-redesign-race.log`, `.artifacts/examination-redesign-browser-final.log`; ảnh tổng kết và sơ đồ trong `.artifacts/dcms112-browser/`. Không dùng mock API ở ba browser test. Antivirus Ready/Rejected trong integration vẫn là scanner stand-in; upload qua browser được giữ PendingScan.

Giới hạn bản này: nhu cầu quay lại lưu trong phiếu, chưa tạo FollowUpInstruction/lịch tự động; chưa nối phí khám/thu tiền hoặc kế toán. Handoff kiểm phiên bản và giữ Visit trong URL khi mở dossier; chưa lưu liên kết phiên bản khám bền vững vào mô hình dossier/plan. Không coi việc mở màn Điều trị là bằng chứng đã thực hiện/post actual hoặc hoàn thành thanh toán. Danh sách giới hạn 100 lượt gần nhất trong chi nhánh có quyền và có thông báo giới hạn. Các thay đổi giữ local, chưa commit/push.

### Đơn giản hóa tiền sử dị ứng và thuốc đang dùng

Theo yêu cầu ngày 26/10/2026, ba mục sức khỏe là ghi chú nhiều dòng tùy chọn. Không dùng select trạng thái hoặc checkbox xác minh; không yêu cầu verified hay thông tin sức khỏe để hoàn tất khám. Có thông tin thì ghi, không có thì để trống; không tự coi trống là None hoặc Verified. Giữ dữ liệu cũ và snapshot đính chính, giữ giới hạn 4000 ký tự. Cập nhật cùng bộ Word gốc và S19-S20-DCMS-112.md.

Kiểm chứng: 28/28 Examination API tests trên schema PostgreSQL riêng, gồm nhập/xóa/mở lại/hoàn tất/đính chính ghi chú không xác minh; 3/3 Playwright API thật, có bỏ trống ghi chú và không có checkbox/select sức khỏe; web build pass. Log local tại `.artifacts/health-simple/`. Không đổi Select hướng xử trí hay xác nhận kết thúc toàn lượt vì chúng thuộc thao tác khác.

### Sửa thông báo thiếu nội dung khi hoàn tất khám

Nguyên nhân: Validate gộp triệu chứng/chẩn đoán/findings thành `Error("MSG02")`, dùng nhãn mặc định Thông tin; phần tổng kết còn gộp nhiều mục vào cùng thông báo. Server nay chỉ rõ từng mục và đường dẫn field, giữ nguyên điều kiện chuyên môn và ba ghi chú sức khỏe tùy chọn. UI kiểm trước, liệt kê mục thiếu và mở đúng tab, đánh dấu lý do/chẩn đoán bắt buộc và hướng dẫn Thêm vùng khám khi khám tổng quát. Không xóa bản nhập hoặc thay phiên bản khi validation thất bại. Nút đính chính có key và tên truy cập riêng để không kế thừa loading từ nút lưu.

Kiểm chứng: **38/38 Examination API tests**, gồm 10 trường hợp thiếu nội dung trả đúng MSG02/field/tên mục và không ghi; **3/3 Playwright với API thật** gồm báo thiếu findings từ tab Tổng kết, chuyển đúng tab và giữ chẩn đoán/kết luận, cùng các luồng lưu/hoàn tất/đính chính/kết thúc đã có. Web build và Release API build pass. Log `.artifacts/examination-validation-tests.log`, `.artifacts/examination-validation-browser-final.log`. Đã cập nhật API 5084 và web 8084 của phiên test hiện tại.

### Odontogram trực quan 2D/3D và nhập kết quả nhanh — 27/10/2026

Hợp đồng bổ sung: [S19-ODONTOGRAM-2D-3D.vi.md](../specs/S19-ODONTOGRAM-2D-3D.vi.md). Mặc định là sơ đồ SVG với số FDI, hình thân/chân răng, bộ răng 32/20/hỗn hợp 52 vị trí và chú giải trạng thái. Chọn nhiều răng rồi tìm/nhập một kết quả để áp dụng; điền dòng trống trước, giữ phát hiện cũ và bỏ qua bản ghi trùng. Danh mục gợi ý giữ các mục người dùng cung cấp, ngoại trừ mục Bi chưa rõ nghĩa. Ghi nhận theo vùng và ô Kết quả khám tổng quát vẫn cho ca không cần chọn răng.

Mô hình WebGL/Three.js được tải khi mở 3D; hỗ trợ xoay, zoom, chọn mesh bằng raycast và đặt lại góc nhìn. Đây là hình học minh họa tạo cục bộ, không phải scan hoặc mô hình giải phẫu cá nhân. Cùng FDI/findings/lựa chọn với 2D, có bộ nút số hỗ trợ bàn phím. Lỗi tải/thiếu WebGL/mất context có đường về 2D. Phiếu xác nhận chỉ xem; điều khiển góc nhìn/bộ răng vẫn dùng được. Gỡ component giải phóng controls, observer, listener, geometry/material và WebGL context.

Finding bổ sung surfaces tùy chọn O/B/L/M/D; backend kiểm whitelist, tối đa 5 giá trị duy nhất và chỉ dùng khi có răng hợp lệ. Lưu trong ContentJson và snapshot hiện có; dữ liệu cũ không có surfaces vẫn đọc được, không cần migration. Lịch sử hiển thị cả bề mặt; không tạo kế hoạch/actual/chi phí từ thao tác chọn răng.

Kiểm chứng bản này: web production build và backend Debug/Release build qua; 43/43 Examination API tests trên schema PostgreSQL riêng. Sau chỉnh chuỗi tiếng Việt của fixture, 5/5 tests surfaces được chạy lại và qua. 5/5 Playwright dùng API/PostgreSQL thật qua, gồm luồng cũ; batch/dedup/nhiều phát hiện; canvas WebGL thực; chọn mesh theo số FDI xuất hiện khi hover rồi đối chiếu dữ liệu API sau lưu/mở lại; xoay/reset; mất context; giả lập thiết bị không có WebGL; ca khám tổng quát không chọn răng và phiếu chỉ xem. Hai trường hợp lỗi GPU được chủ động mô phỏng, không mock API. Antivirus trong fixture giữ ranh giới stand-in như trước.

Ảnh kiểm tra giao diện: `.artifacts/dcms112-browser/examination.integration-vi-fb14c-ion-with-persisted-findings/odontogram-2d.png` và `odontogram-3d.png`. Kết quả API ở `backend/tests/DCMS.IntegrationTests/TestResults/odontogram.trx` (local). Phiên manual giữ schema cũ, API 5084/web 8084; xác nhận `/health` trả API/database healthy và web HTTP 200. Thay đổi giữ local, chưa commit/push. Build vẫn báo cảnh báo dependency Pkcs và kích thước chunk; Three.js được tách chunk và chỉ tải khi mở 3D.


### Chọn nhiều răng trực tiếp — 27/10/2026

Bỏ nút chuyển chế độ Chọn nhiều răng/Chọn từng răng. Click răng để chọn thêm, click lại để bỏ riêng răng đó; giữ nguyên các răng khác, đồng nhất 2D/3D/nút FDI/bàn phím. Chọn/bỏ chọn không tạo finding trống hoặc xóa kết quả cũ; chỉ Áp dụng ghi kết quả. Nút Bỏ chọn vẫn xóa toàn bộ tập chọn. Kiểm chứng bằng build web và các luồng Playwright/API thật trên schema riêng.

Kiểm chứng thay đổi chọn trực tiếp: web build qua; 5/5 Playwright dùng API/PostgreSQL thật qua, có chọn 16 + 26 rồi bỏ riêng 16 và giữ 26, chọn lại bằng Space, không tạo finding trống trước Áp dụng, click mesh 3D lần hai bỏ chọn và lần ba chọn lại, cùng các luồng lưu/mở lại/hoàn tất cũ. Bản web đang phục vụ tại http://localhost:8084; API/database healthy, web HTTP 200.

### Làm lại hình dáng mô hình 3D — 27/10/2026

Thay toàn bộ thân răng sphere/chân cone bằng mesh liên tục tạo riêng theo nhóm răng cửa, nanh, tiền hàm và răng hàm. Thân răng có cổ thu, mặt cắn/múi/rãnh minh họa; nướu ôm cung hàm, có viền lượn và che chân răng. Vị trí tính theo chiều rộng răng, hướng tiếp tuyến cung hàm; răng sữa có cung nhỏ hơn. Dùng vật liệu men răng/nướu, ánh sáng mềm và góc nhìn gần hơn. Raycast chỉ chọn mặt răng nhìn thấy, không xuyên qua nướu. Giữ nguyên FDI, chọn/bỏ chọn trực tiếp, dữ liệu và API hiện có. Đây vẫn là hình học minh họa, không phải scan cá nhân hoặc mô hình dùng để đo đạc lâm sàng. Không dùng asset/dịch vụ bên ngoài.

Đã kiểm tra ảnh render WebGL thực và điều chỉnh hình học, nướu, camera. Một lượt chạy regression có timeout 5 giây lúc danh sách hồ sơ điều trị còn hiện skeleton trong lúc build chạy song song; không thay mã điều trị. Chạy lại sau khi build xong để phân biệt lỗi chức năng với timing của môi trường.

Kết quả bản cuối: web production build qua; 5/5 Playwright/API/PostgreSQL thật qua sau khi build hoàn thành (42,6 giây). Đã xem ảnh `odontogram-3d.png` của bản cuối: thân răng riêng từng nhóm, nướu và góc nhìn gần; kiểm tra lại chọn/bỏ chọn mesh, lưu/mở lại và fallback. API/database manual healthy, web 8084 trả HTTP 200; Vite phục vụ mô hình mới, không đổi schema hay dữ liệu manual.

### Mẫu tham chiếu riêng và kiểm tra từng răng — 27/10/2026

Theo yêu cầu làm kỹ từng răng, dùng 26 mẫu CC BY 4.0 của University of Dundee: 16 loại răng vĩnh viễn và 10 loại răng sữa. Kiểm tra license/source qua metadata từng GLB và API Sketchfab; không nhập asset hàm CC BY-NC. Các vị trí bên đối diện phản chiếu mẫu tương ứng (52 vị trí FDI), không coi là 52 scan riêng. Nguồn, pinned revision, source/derived SHA-256, cỡ file, triangle count và các thay đổi nằm trong `web/public/models/dental/manifest.json`, `ATTRIBUTION.md`; viewer hiển thị attribution. Bổ sung exception gitignore giới hạn vào thư mục public asset này. Script tải/chuẩn hóa ở `infrastructure/scripts/fetch-dental-models.py`, `prepare-dental-models.mjs` và hướng dẫn `DENTAL-MODEL-ASSETS.md`.

Toàn hàm tải LOD thân răng; xem riêng tải mesh chi tiết đầy đủ thân/chân cho đúng mẫu FDI. Có tên răng/hàm/bên, xem chính diện/mặt nhai-cắn/mặt bên, xoay/zoom/reset/trở lại toàn hàm. Xem riêng giữ màu men/chân để thấy bề mặt; tập răng đang chọn vẫn độc lập qua FDI và ghi nhanh. Giữ click chọn/bỏ chọn cả 2D/3D, không tạo finding khi chỉ xem. Fetch/parse có loading/lỗi/AbortController, bỏ kết quả cũ khi đổi view, giải phóng source/clone/material/texture/GPU khi rời component. API và schema không đổi.

Asset tổng cộng khoảng 8,34 MB arch và 40,08 MB detail, detail chỉ tải mẫu đang xem; original khoảng 158 MB chỉ ở artifacts, không phục vụ. Mesh được chuẩn hóa cervical origin, uniformly scaled/oriented, cắt root cho arch, simplification có sai số giới hạn và thay texture gốc bằng vertex color men/chân minh họa. Các LOD giữ normals hình học. Chưa thẩm định giải phẫu của các phép chuẩn hóa/đặt vào cung bởi bác sĩ; không dùng để chẩn đoán, đếm chân/ống tủy hoặc đo chiều dài. Đây là hình học tham chiếu, không phải dữ liệu hình thái bệnh nhân đang khám.

Kiểm tra SHA-256 và GLB header/finite accessor bounds cho 52 file LOD của 26 mẫu: qua. Web production build qua. Một lượt UI đầu phát hiện Vite tối ưu lazy GLTFLoader lần đầu rồi reload trang, mất bản nháp đang nhập; thêm optimizeDeps.include Three/OrbitControls/GLTFLoader theo tài liệu Vite. Sau sửa, 5/5 Playwright/API thật qua, đã xem ảnh riêng 11,14,16,36 và toàn hàm. Lượt cuối bổ sung ảnh mặt nhai 16, răng sữa 55, chuyển bộ 20/52, điều khiển góc nhìn và lỗi tải asset; kết quả ghi tiếp bên dưới khi hoàn tất.

Kết quả lượt cuối: 5/5 Playwright/API/PostgreSQL thật qua (54,0 giây), gồm xem riêng 11/14/16/36 và răng sữa 55, chuyển 20/52 vị trí trong 3D, góc nhìn mặt nhai/cắn và mặt bên, giữ lựa chọn 16+26 khi chỉ đổi răng xem, click mesh chọn/bỏ chọn, lưu/mở lại, lỗi tải asset và thiếu/mất WebGL, cùng luồng khám/đính chính/readonly cũ. Đã xem ảnh toàn hàm và ảnh từng răng, có ảnh mặt nhai 16. Test handoff nay chờ đúng response GET dossiers và kiểm HTTP thành công trước khi kiểm nội dung, tránh timeout skeleton 5 giây khi môi trường lạnh; không thay đổi product treatment. Manual API/database healthy, web và asset địa phương trả HTTP 200. Các thay đổi giữ local, chưa commit/push.

### Nhãn thao tác cạnh răng thay select xem riêng — 27/10/2026

Bỏ select Răng xem chi tiết. Click/chạm mesh hiện nhãn nổi cạnh điểm bấm, gồm FDI/tên răng/nút Xem riêng/nút đóng; nhãn bố trí phía trên hoặc dưới điểm bấm trong viewport. Click vẫn toggle tập chọn khi được phép sửa; Xem riêng không toggle lần nữa. Nút FDI ở 3D gửi yêu cầu mở nhãn tại răng tương ứng; canvas có Tab/Arrow/Enter/Space, Escape đóng nhãn. Click ngoài/bắt đầu xoay/đổi view đóng nhãn. Readonly vẫn mở nhãn và xem riêng bằng mesh/bàn phím, không sửa kết quả; các nút FDI ghi khám tiếp tục khóa. Toolbar góc nhìn/Xem toàn hàm chỉ hiện khi đang xem riêng.

Kiểm chứng: production web build qua; 5/5 Playwright/API/PostgreSQL thật qua (1,0 phút), có nhãn đúng răng qua FDI và raycast, click chọn/bỏ chọn, xem riêng giữ nguyên tập chọn, Escape/nút đóng, xem răng vĩnh viễn/sữa, readonly mở nhãn bằng phím mũi tên rồi xem riêng, lưu/mở lại và các luồng cũ. Đã xem ảnh thực `tooth-context-label.png` trong `.artifacts/dcms112-browser/examination.integration-vi-fb14c-ion-with-persisted-findings/`. Manual API/database healthy, web 8084 HTTP 200. Không đổi API/schema; chưa commit/push.

### Chẩn đoán nhiều loại bệnh là kết luận cuối cùng — 27/10/2026

Theo yêu cầu mới, [S19-MULTIPLE-DIAGNOSES.vi.md](../specs/S19-MULTIPLE-DIAGNOSES.vi.md) thay hai ô Chẩn đoán/Kết luận khám bằng một editor nhiều tag ở Tổng kết khám. Bác sĩ tìm/chọn nhiều loại bệnh từ danh sách đã cung cấp hoặc nhập tên khác; có Chưa phát hiện bất thường cho ca khám tổng quát. Không tự chẩn đoán từ findings/odontogram. Bỏ textarea chẩn đoán ở tab Khám và kết luận riêng; thông báo thiếu chẩn đoán mở tab Tổng kết. Phiếu đã xác nhận khóa editor, đính chính có lý do.

ContentJson/audit bổ sung diagnoses tùy chọn, tối đa 30 mục và tổng chuỗi 4000 ký tự. Danh sách có mặt là authoritative, kể cả []: server trim, kiểm rỗng/trùng không phân biệt hoa thường/độ dài, đồng bộ diagnosis và conclusion thành chuỗi nối cho danh sách/handoff cũ. Xác nhận yêu cầu ít nhất một chẩn đoán, không yêu cầu conclusion riêng; draft cho phép []. Legacy thiếu diagnoses tiếp tục dùng diagnosis; conclusion cũ khác diagnosis hiển thị chỉ đọc và giữ nguyên khi chưa chủ động sửa chẩn đoán. Lịch sử giữ snapshot cũ; không migration. Kết thúc Visit kiểm chẩn đoán và hướng xử trí.

Kiểm chứng: 51/51 Examination API tests trên schema PostgreSQL riêng qua, gồm nhiều chẩn đoán/đồng bộ aliases/draft/reopen/confirm/amend/history/complete, [] không fallback chuỗi cũ, danh sách sai không ghi, legacy không conclusion vẫn hoàn tất. 6/6 Playwright/API thật qua (45,4 giây), gồm chọn hai bệnh, bỏ từng mục/bỏ tất cả, missing diagnosis đúng tab, lưu/mở lại, readonly, đính chính, handoff và ca không bất thường; toàn bộ luồng odontogram 3D/nhãn xem riêng cũ vẫn qua. Web production build và Release API build qua; ảnh `multiple-diagnoses-summary.png` đã được xem. API 5084 cập nhật đúng schema manual cũ, không restart web; health API/database healthy, web 8084 HTTP 200 và OpenAPI live có diagnoses. Kết quả backend ở `backend/tests/DCMS.IntegrationTests/TestResults/multiple-diagnoses.trx`. Chưa commit/push.

### Giữ tab Tổng kết khi chưa đủ nội dung hoàn tất — 27/10/2026

Nguyên nhân điều hướng: command(confirm) gọi setTab(issues[0].tab) khi client validation phát hiện mục thiếu ở Khám; lỗi chỉ hiển thị global trên đầu trang nên thao tác trông như nút Hoàn tất bị quay lại. Nay submit lỗi giữ tab Tổng kết và bản nhập, hiện Alert gần nút Hoàn tất với từng mục thiếu có nút Bổ sung. Chỉ thao tác Bổ sung mới đổi tab và focus/scroll đến ID ô chính xác: lý do, kết quả tổng quát, răng/vùng cụ thể, chẩn đoán, dặn dò hoặc xử trí. Danh sách lỗi tự cập nhật theo nội dung, chỉ scroll/focus thông báo sau submit/navigation, không giật focus khi gõ. Lỗi API ở tab Tổng kết cũng hiện trong vùng này. Khi hoàn tất thành công vẫn ở Tổng kết, clear lỗi và hiển thị trạng thái xác nhận.

Không thay API hoặc các yêu cầu bắt buộc, không tự điền kết quả. Các ô có ID ổn định để dẫn tới đúng mục, dùng đúng ô general cho finding Toàn hàm đầu tiên; các finding vùng khác vẫn trỏ dòng riêng. Quy tắc giữ tab này thay quy tắc cũ tự mở mục thiếu đầu tiên trong S19-S20/README.

Kiểm chứng: web production build qua; 7/7 Playwright/API/PostgreSQL thật qua (45,6 giây), gồm thiếu findings giữ Tổng kết rồi bấm Bổ sung focus ô tổng quát; thiếu kết quả răng 16 giữ Tổng kết/diagnosis/version, nút dẫn đúng textarea, nhập bổ sung rồi xác nhận thành công vẫn Tổng kết; các luồng đa chẩn đoán, concurrency, đính chính, readonly, xem riêng 3D và ca không bất thường vẫn qua. Đã xem ảnh `completion-missing-tooth.png` trong `.artifacts/dcms112-browser/examination.integration-in-d6bf8-y-opening-the-missing-field/`. Manual API/database healthy, web 8084 HTTP 200; không thay schema/dữ liệu manual, chưa commit/push.


### Chỉ định cận lâm sàng trong phần Khám — 27/10/2026

Triển khai [S19-INVESTIGATIONS.vi.md](../specs/S19-INVESTIGATIONS.vi.md): tab Cận lâm sàng cho bác sĩ phụ trách tạo chỉ định X-quang/Xét nghiệm/Khác, răng/vùng tùy chọn, lý do và mức cần kết quả để kết luận. Luồng Chờ kết quả → Có kết quả, chờ bác sĩ đọc → Bác sĩ đã đọc; hủy cần lý do và giữ lịch sử. Trợ lý cùng chi nhánh hoặc bác sĩ phụ trách nhận kết quả; chỉ bác sĩ phụ trách xác nhận đã đọc. Không tự sinh chỉ định theo bệnh, chẩn đoán, điều trị hay khoản thu.

Nhận kết quả cần văn bản hoặc tệp Ready. Tệp gắn đúng chỉ định và chốt snapshot khi nhận; PendingScan/Rejected không tính là bằng chứng đã nhận/đọc. Có văn bản vẫn xử lý được khi tệp riêng đang PendingScan, không tự đánh dấu tệp đó đã đọc. Không tải thêm vào chỉ định sau nhận kết quả. Backend kiểm quyền, patient/visit, trạng thái, phiên bản, idempotency, audit và giao dịch dùng khóa chung với khám/tài liệu. Chỉ định bắt buộc chưa đọc/hủy chặn xác nhận/đính chính; mọi chỉ định chưa đọc/hủy chặn kết thúc toàn Visit. Tiến độ này không thêm trạng thái Visit mới.

Thao tác chỉ định và cập nhật tệp giữ bản khám chưa lưu. Khi tải lại workspace sau lệnh, không tự nâng phiên bản ghi lâm sàng nếu người khác đã sửa; polling tệp chỉ cập nhật danh sách cận lâm sàng. Migration 20261027060259_ClinicalInvestigationOrders chỉ thêm bảng chỉ định, nullable InvestigationId và index/FK; áp dụng vào schema manual cũ, đối chiếu số Visit/Examination/ClinicalDocument trước-sau bằng nhau, không seed lại. API 5084/database healthy, web 8084 HTTP 200, OpenAPI live có đủ bốn đường dẫn chỉ định. Chưa commit/push.

Kiểm chứng: 62/62 Examination API tests qua trên PostgreSQL schema riêng; sau bổ sung nhánh nhận kết quả văn bản với PendingScan/Rejected, chạy lại 3/3 tests bằng chứng tệp và qua. Web production build qua; 8/8 Playwright/API/PostgreSQL thật qua (52,4 giây), gồm giữ bản nháp khi tạo chỉ định, chặn hoàn tất trước nhận/đọc, hủy có lý do, tải PDF PendingScan, nhận văn bản, bác sĩ đọc rồi hoàn tất; các luồng khám/đa chẩn đoán/3D/đính chính cũ vẫn qua. Đã xem ảnh investigation-reviewed.png. Ready/Rejected trong kiểm thử API dùng antivirus stand-in; UI chứng minh upload PendingScan và xử lý kết quả văn bản, không chứng minh tệp đã được máy quét thật duyệt an toàn. Backend build còn cảnh báo dependency Pkcs có sẵn.


### Làm rõ nhóm và tên kỹ thuật cận lâm sàng — 27/10/2026

Theo phản hồi danh mục quá chung, UI hiển thị Chụp phim răng hàm mặt, Chẩn đoán hình ảnh khác, Xét nghiệm, Giải phẫu bệnh / tế bào học, Khác. Gợi ý cụ thể gồm quanh chóp/cánh cắn/mặt nhai/toàn cảnh/sọ nghiêng/sọ thẳng/CBCT; siêu âm/MRI/CT; công thức máu/đường huyết/HbA1c/đông máu/chức năng gan-thận/vi sinh; mô bệnh học và tế bào học. Cho nhập tên ngoài danh mục; đổi nhóm xóa tên để tránh gán nhầm. Không tạo tự động gói xét nghiệm hoặc thủ thuật lấy mẫu. API chấp nhận thêm Imaging/Pathology, không migration; tệp mặc định theo nhóm, dữ liệu cũ giữ nguyên.

Kiểm chứng: web production build và API Release build qua; 2/2 API tests lưu/đọc lại kỹ thuật mới trên PostgreSQL thật qua. Cập nhật API manual cùng schema, không migration mới; đối chiếu số bản ghi Visit/Examination/ClinicalDocument không đổi, API/database healthy và web 8084 HTTP 200. Chưa commit/push.

Playwright luồng cận lâm sàng sau cập nhật danh mục qua 1/1 (20,4 giây): thấy gợi ý phim toàn cảnh, chọn công thức máu, đổi về nhóm chụp phim xóa tên cũ, tạo/nhận/đọc/hoàn tất vẫn giữ nháp khám. Đã sửa callback xóa tên khi đổi nhóm, danh sách gợi ý rộng bằng ô nhập; đã xem ảnh giao diện thực. Web build cuối qua và git diff --check không có lỗi whitespace. Các lần chạy trước phát hiện selector Ant Design virtual option trùng/ô bị label che và callback chưa gắn; đã chỉnh selector và implementation rồi chạy lại thành công.


### Chẩn đoán ghi một lần tại răng/vùng — 27/10/2026

Triển khai phần bổ sung trong S19-MULTIPLE-DIAGNOSES: odontogram có chọn nhiều chẩn đoán áp dụng cho các răng đang chọn, mô tả bổ sung tùy chọn; từng dòng răng/vùng và toàn hàm có thể sửa danh sách tại nguồn. Đổi ô mô tả nhanh sang textarea để không nhầm việc chọn tên bệnh với mô tả tự do. Tổng kết hiển thị bệnh kèm răng/vùng và nút trở lại đúng nguồn; ô bổ sung không gắn răng chỉ nhập khi cần. Không bắt nhập lại bệnh đã ghi. Chỉ có mô tả không tự thành chẩn đoán; bác sĩ chọn Chưa phát hiện bất thường được xác nhận không cần tạo finding/tooth giả. Ca chưa rõ tiếp tục nháp/CLS.

ToothFinding thêm diagnoses, Content thêm unscopedDiagnoses; backend tự tính union vào diagnoses/diagnosis/conclusion, khử trùng tên giữa các nguồn và giữ vị trí riêng. Unscoped là nguồn độc lập, không ghi danh sách tổng hợp ngược vào nguồn để tránh bệnh bị phục hồi sau xóa. Xác nhận cần mô tả hoặc chẩn đoán trong từng finding, thông tin khác giữ quy tắc hiện có. Kiểm giới hạn tại từng nguồn và toàn phiếu; lịch sử/đính chính/handoff/kết thúc dùng cùng dữ liệu. Phiếu cũ vẫn đọc được, không suy luận/tách bệnh từ condition hoặc kết luận cũ; không đổi database schema. Các bản nhập chỉ sửa mô tả của phiếu legacy chưa dùng nguồn mới không tự nâng sang dữ liệu chẩn đoán mới.

Kiểm chứng: 71/71 ExaminationApiTests trên PostgreSQL riêng qua, có cùng bệnh nhiều răng/toàn hàm, chẩn đoán không mô tả, handoff, snapshot đính chính, xóa nguồn không hồi sinh từ aliases, normal không findings và complete, danh sách null/trùng/quá giới hạn. 9/9 Playwright/API/PostgreSQL thật qua (1,2 phút), có tạo nhiều răng một lần, sửa/xóa sau lưu/mở lại, thêm bệnh toàn hàm, không nhập chẩn đoán thứ hai rồi hoàn tất, normal không finding, hồi quy 3D/CLS/readonly/concurrency. Ảnh scoped-diagnoses-summary.png đã xem tại .artifacts/dcms112-browser/examination.integration-sc-2f86d-rough-save-and-finalization/. Antivirus giữ ranh giới stand-in trong test API như trước; UI CLS dùng kết quả văn bản với file PendingScan, không chứng minh scan thật Ready.

API Debug/Release và web production build qua; API manual 5084 cập nhật cùng schema cũ, không migration mới/seed lại, số Visit/Examination/ClinicalDocument trước-sau không đổi. Live API/database healthy, web 8084 HTTP 200, OpenAPI có unscopedDiagnoses. Cảnh báo dependency Pkcs và chunk web có sẵn; chưa commit/push.


### Khôi phục quét an toàn tệp ở phiên manual — 27/10/2026

Chẩn đoán runtime: .env chưa có CLAMAV_HOST hiệu lực, Docker chỉ chạy PostgreSQL/Mailpit. ClamAvClinicalFileScanner chủ động trả PendingScan khi thiếu host; ClinicalScanWorker thử lại mỗi 30 giây nhưng chưa có daemon để quét. Thông báo chặn mở là đúng trạng thái, nguyên nhân vận hành là thiếu dịch vụ quét.

Thay đổi dự kiến: thêm ClamAV image chính thức theo profile clinical trong compose, chỉ publish TCP trên loopback, persistent signature volume; cấu hình CLAMAV_HOST/PORT ở .env local và restart đúng API manual. Không sửa FileState bằng SQL, không dùng scanner mô phỏng hoặc mở tệp khi chưa Ready. Worker quét lại tệp chờ, giữ hash/MIME/quyền và audit hiện có. Dữ liệu lâm sàng và storage root cũ giữ nguyên.

Tham chiếu: https://docs.clamav.net/manual/Installing/Docker.html và https://docs.clamav.net/manual/Usage/ClamdProtocol.html. Kiểm chứng dự kiến: daemon healthy/PONG, INSTREAM tệp thử lành trả OK; worker chuyển tệp hiện có sang Ready/Rejected theo kết quả thật; API tải tệp Ready đúng quyền, không coi 200 HTML đăng nhập là tệp. Không đưa nội dung/tên tệp bệnh nhân vào log nghiệm thu.


Đối chiếu runtime trong lúc xử lý: API manual hiện dùng schema mặc định public, trong khi metadata .tools/dcms112-runtime.json còn giữ schema fixture cũ làm tên thư mục lưu tệp. Nguyên nhân helper dùng cú pháp gán thuộc tính ConnectionString của DbConnectionStringBuilder trong PowerShell (dictionary adapter), làm bỏ qua Search Path khi đọc lại thuộc tính. Các khẳng định trước đó về manual cùng schema riêng cần được hiểu lại theo bằng chứng này; API tests dùng Npgsql và script browser fixture dùng set_ConnectionString nên vẫn có schema riêng.

Không chuyển dữ liệu đang dùng sang schema khác. Sửa helper manual dùng set_ConnectionString/get_ConnectionString, thêm databaseSchema=public trong metadata, giữ schema cũ chỉ để bảo toàn storage root hiện tại; so sánh số bản ghi trên đúng schema active. Không xóa/copy/merge dữ liệu. Năm PendingScan đọc ban đầu thuộc fixture cũ, không phải năm tệp đang dùng của phiên hiện tại. Tệp mới nhất của schema active được scanner thật chuyển Ready, content HTTP 200 image/jpeg và có ScanPassed audit. Không ghi ID/tên/nội dung tệp vào tài liệu.


Kết quả cuối: ClamAV 1.5.4, signature 28166, container healthy; PING/PONG và INSTREAM PDF thử trả stream: OK. Tệp mới nhất của phiên đang dùng có Ready, ScanPassed audit và content HTTP 200 image/jpeg, được kiểm lại sau restart helper đã sửa. Số Visit/Examination/ClinicalDocument trên schema active trước-sau không đổi; storage root giữ nguyên. Compose config --quiet và git diff --check qua. Không cần sửa logic clinical hoặc đánh dấu Ready thủ công. Người dùng cần tải lại trang/đóng mở lại drawer để lấy trạng thái mới.


### Hiện và xử lý chỉ định khi đính chính — 27/10/2026

Nguyên nhân: panel chỉ kiểm allowedActions.save nên mất nút khi phiếu confirmed; create API cũng từ chối mọi confirmed. Nay parent truyền trạng thái đính chính, chỉ hiện tạo bổ sung cho bác sĩ phụ trách của Visit InProgress. Form lấy lý do bổ sung sau xác nhận, dùng lại ở lý do lưu đính chính. Backend kiểm current Visit/examination versions, quyền, lý do; audit ghi sourceExaminationVersion/amendmentReason, không sửa phiếu hay tăng examinationVersion khi mới tạo chỉ định. Chỉ định cần cho kết luận đang chờ chặn amend/handoff; các state guards và idempotency giữ nguyên. Hủy đính chính không tự xóa/hủy chỉ định đã commit, panel có giải thích. Completed vẫn sửa lịch sử được nhưng panel giải thích không lập chỉ định mới vào lượt đã kết thúc.

Không migration. 72/72 Examination API tests qua, thêm confirmed/open bổ sung có lý do, stale, assistant bị từ chối, replay, giữ phiếu gốc, audit và pending handoff/amend; nhận/đọc rồi amend tăng version. 10/10 Playwright/API/PostgreSQL thật qua (1,2 phút), có thiếu lý do, giữ notes nháp, dùng lại lý do, nhận/đọc/lưu và thông báo Completed; hồi quy 3D/scoped diagnoses/CLS cũ qua. Hai lỗi PendingScan ở lần chạy đầu do fixture vô tình thừa hưởng ClamAV thật mới bật; prepare-clinical-e2e nay mặc định giữ nhánh PendingScan bằng không cấu hình host trong tiến trình fixture, -UseRealAntivirus dùng cho proof quét riêng. Không tắt dịch vụ ClamAV hay đổi cấu hình scanner của phiên manual, container vẫn healthy.

API Release và web build qua, manual giữ đúng databaseSchema/public và thư mục tệp hiện tại; số Visit/Examination/ClinicalDocument trước-sau giữ nguyên. Health/database/web HTTP 200, OpenAPI live có hai field mới. Chưa commit/push.


Cập nhật 27/10/2026: đã nối dịch vụ CLS → thực hiện → ghi phí → thu tiền theo [báo cáo bổ sung](INVESTIGATION-BILLING.vi.md). Giới hạn về tài chính ở các mốc kiểm chứng cũ phía trên là lịch sử của từng mốc; phí khám tự thân và tài chính toàn kế hoạch vẫn theo giới hạn báo cáo mới.


Cập nhật chốt 27/10/2026: [khám riêng, điều trị nhiều buổi](EXAMINATION-TREATMENT-SEPARATION.vi.md) thay luồng handoff gắn cùng Visit ở mốc cũ. Không yêu cầu đã điều trị mới kết thúc khám; nguồn khám không phải buổi thực hiện điều trị.
