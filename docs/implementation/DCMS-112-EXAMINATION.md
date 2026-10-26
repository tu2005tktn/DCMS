# DCMS-112 — Contract và bàn giao khám theo Visit

Ngày kiểm chứng: 26/10/2026. [Đặc tả](../specs/S19-S20-DCMS-112.md). Branch: `feat/dcms-112-examination`.

Tái sử dụng treatment foundation `origin/vinh@fc942b6` qua merge `d162045`; giữ tiếp nhận DCMS-111 `caa01ef`. UI S20 lấy từ `origin/TriNM@1b4d72c` (list/upload/detail/viewer/types), nối API thật và bỏ preview khỏi đường chạy. Không thay backend kế hoạch/actual bằng engine thứ hai. Migration `20261026035110_VisitExaminationsAndClinicalDocuments` bổ sung khám/file/audit/receipt/review vào DbContext chung.

## Mở và chạy

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

DCMS-112 bàn giao kết quả khám/context và mở được UI tư vấn/điều trị; không tuyên bố post actual/tính phí/hoàn tất lượt đã được nghiệm thu xuyên suốt. Các port consent, charge, Labo, hoàn tất và own-record của DCMS-113/114/115 vẫn phải được module sở hữu nối/xác minh. Nền treatment cũ được giữ nguyên, bao gồm việc fail closed khi nguồn này thiếu. Không có daemon ClamAV thật trong môi trường kiểm chứng này; không đánh dấu pass khả năng phát hiện malware thực tế. Build còn cảnh báo package Pkcs có sẵn và bundle lớn; không thay dependency ngoài task.
