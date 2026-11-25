# Integration test và test coverage — Backend DCMS

Tài liệu này mô tả cách kiểm thử tích hợp (integration test) cho backend DCMS, công cụ đo độ bao phủ (code coverage), mục tiêu coverage và cách điền báo cáo `Report-5.2_Integration Test.xlsx`.

## 1. Phạm vi

- **Đối tượng:** toàn bộ Web API `backend/src` (DCMS.Api, DCMS.Application, DCMS.Domain, DCMS.Infrastructure).
- **Kiểu test:** mỗi test gọi API thật qua HTTP trong cùng tiến trình (`WebApplicationFactory<Program>`), đi qua đủ routing → middleware (xác thực JWT, `ScreenPermissionMiddleware`, `BusinessErrorMiddleware`) → Application → Infrastructure/EF Core → PostgreSQL thật.
- **Không nằm trong phạm vi:** giao diện web/mobile (E2E), `ai-service` (FastAPI), dịch vụ bên ngoài thật (SMTP, PayOS, Firebase, Google). Các dịch vụ ngoài được thay bằng bản giả trong bộ nhớ (xem mục 3).

## 2. Mục tiêu coverage

| Chỉ số | Mục tiêu |
|---|---|
| Line coverage (4 assembly `src`) | **≥ 70 %** |
| Tỉ lệ test case Passed trong báo cáo | 100 % các case đã chạy |

Loại khỏi phép đo (không phải mã viết tay, không có logic nghiệp vụ):

- `**/Migrations/*.cs` — migration và model snapshot do EF Core sinh ra.
- Mã có `[GeneratedCode]`, `[CompilerGenerated]`, `[ExcludeFromCodeCoverage]`.
- Auto-property (`SkipAutoProps`).

Cấu hình nằm ở `backend/tests/coverage.runsettings`.

## 3. Môi trường test

| Thành phần | Giá trị |
|---|---|
| Server | API chạy trong tiến trình test (`ApiHost` / `ApiFixture`), môi trường `Development` |
| Database | PostgreSQL 17 (`infrastructure/docker/compose.yaml`). Mỗi lần chạy tạo một schema riêng `dcms_it_<guid>` / `dcms_test_<guid>`, chạy migration, seed tài khoản demo; dữ liệu thật không bị đụng tới |
| Tài khoản | Tài khoản demo do `DevelopmentAccountSeeder` tạo (`owner.demo`, `dentist.demo`, `receptionist.demo`, ...) với mật khẩu ngẫu nhiên mỗi lần chạy |
| Dịch vụ ngoài | `IContactDeliveryAdapter` (email/SMS), `IRealtimeNotifier` (SignalR) được thay bằng bản giả ghi lại trong bộ nhớ |
| Trình duyệt | Không cần (test gọi HTTP trực tiếp) |

## 4. Công cụ

| Công cụ | Vai trò | Cài đặt |
|---|---|---|
| xUnit 2.9 | Framework test | `DCMS.IntegrationTests.csproj` |
| Microsoft.AspNetCore.Mvc.Testing | Dựng API thật trong bộ nhớ (`WebApplicationFactory`) | `DCMS.IntegrationTests.csproj` |
| Npgsql + PostgreSQL (Docker) | Database thật cho test | `compose.yaml` |
| **coverlet.collector** | Thu thập coverage khi chạy `dotnet test` (định dạng Cobertura) | `DCMS.IntegrationTests.csproj` |
| **ReportGenerator** | Gộp file Cobertura thành báo cáo HTML / tóm tắt text | công cụ local `dotnet-tools.json` (`dotnet tool restore`) |
| `DCMS_IT_TRACE` | Ghi lại từng case cùng request/response thật ra JSON để điền báo cáo | `Infrastructure/Trace.cs` |

## 5. Cách chạy

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
powershell -ExecutionPolicy Bypass -File backend/tests/run-integration-coverage.ps1 -Name final
```

Script `backend/tests/run-integration-coverage.ps1` nạp `.env`, chạy `dotnet test` với coverlet, ghi kết quả vào `backend/tests/DCMS.IntegrationTests/TestResults/<Name>/`:

| File | Nội dung |
|---|---|
| `**/integration.trx` | Kết quả từng test (Passed/Failed, thời gian, thông báo lỗi) |
| `http-exchanges.jsonl` | Mọi request/response HTTP mà API xử lý trong lúc test (`HttpExchangeLog`); mật khẩu, token, bằng chứng khôi phục (`proof`, `resetGrant`) được che `***`; mã thông báo (`MSG..`) vẫn giữ để đối chiếu |
| `**/coverage.cobertura.xml` | Dữ liệu coverage thô |
| `coverage-report/index.html`, `Summary.txt`, `Summary.json` | Báo cáo coverage của ReportGenerator |

Thêm `-Filter "FullyQualifiedName~PatientLinkApiTests"` để chạy một phần.

**Chạy tuần tự:** `xunit.runner.json` tắt chạy song song giữa các lớp test. Mỗi fixture dựng một API và một schema riêng; chạy song song vượt giới hạn kết nối của PostgreSQL (`53300: too many clients`). Chạy tuần tự còn giúp gắn đúng log HTTP vào từng test.

## 5a. Xuất báo cáo Excel

```powershell
python backend/tests/tools/build_integration_report.py --template "<đường dẫn>\Report-5.2_Integration Test.xlsx" `
  --results backend/tests/DCMS.IntegrationTests/TestResults/final --out "<đường dẫn>\Report-5.2_Integration Test_DCMS.xlsx"
```

Báo cáo viết bằng **tiếng Anh**. Mỗi test case theo cách viết test case API phổ biến (ID, mục tiêu, pre-condition, bước có dữ liệu đầu vào cụ thể, expected result cụ thể: mã trạng thái, trường trả về, tác động phụ):

| Cột | Nguồn và nội dung |
|---|---|
| Description | `[Case(...)]` (tiếng Anh) nếu có; nếu không, câu tạo từ tên phương thức test |
| Pre-condition | `[Case(...)]` hoặc mô tả môi trường mặc định; liệt kê các tài khoản demo mà test dùng |
| Procedure | Lấy từ log HTTP thật: mỗi bước là một request, ghi **tài khoản gọi** (hoặc *anonymous*), method, đường dẫn, tham số query và **từng trường của body với giá trị đã gửi** (ví dụ `name = "SYNTHETIC group 0db1…"`). Mật khẩu/token ghi là `(seeded demo password)` / `***`; GUID sinh ngẫu nhiên ghi là `(new GUID)`. Các request giống hệt nhau liên tiếp được gộp, ghi số lần. Test không gọi HTTP ghi bước gọi service/DbContext |
| Expected Result | Theo từng bước: mã HTTP kèm tên (`201 Created`, `409 Conflict`), với lỗi nghiệp vụ là mã `MSGnn` kèm nghĩa tiếng Anh và trường lỗi (`field`), với thành công là các trường chính trong response (`version = 1`, `itemCount = 0`, số bản ghi của danh sách). Sau đó là mục **Verified by the test**: các assertion trong mã test (giá trị DB, version, trạng thái...) diễn đạt bằng tiếng Anh |

Để ghi được tài khoản gọi, `HttpExchangeLog` lưu thêm `sub` (id tài khoản) của request đã xác thực; script ánh xạ id sang tên đăng nhập từ response đăng nhập (`user.id`, `user.userName`). Body request được lưu tới 4000 ký tự.

Kết quả Round 1, ngày test lấy từ file `.trx`; con số coverage lấy từ `Summary.json`. Lỗi của template đã sửa khi xuất: `Number of TCs` chỉ đếm dòng có kết quả (không đếm dòng tiêu đề chức năng), Round 2/3 đếm đúng cột I/L.

## 5b. Kết quả lần chạy 23/11/2026

| Chỉ số | Kết quả |
|---|---|
| Test case | 505 (504 Passed, 0 Failed, 1 N/A – `SystemNfrReportTests.RealHttpFiveMbUploadAndDocumentAccessIsolation` chỉ chạy khi đặt `DCMS_RUN_SYSTEM_NFR=1`) |
| Line coverage | **90.2 %** (5032/5577 dòng), đạt mục tiêu ≥ 70 % |
| Branch coverage | 64.7 % (1753/2706) |
| Method coverage | 84.2 % (621/737) |

Các test đã được cập nhật theo API hiện tại (bỏ API lịch tuần và route chi tiết đơn thuốc theo `docs/specs/UNUSED-API-REMOVAL.vi.md`):

- `SchedulingApiTests`: phiên bản lịch lặp lại (`WorkingScheduleRevision`) tính theo nhân viên trên mọi chi nhánh, mà các test trong lớp dùng chung tài khoản demo. Mỗi test xoá revision của các nhân viên demo trước khi bắt đầu, nên version mong đợi (0, 1, ...) chỉ thuộc về test đó. Kiểm tra sau khi xuất bản đọc `WorkingScheduleRevision` thay cho `WorkingWeek`.
- `TreatmentSheetV25Tests.ImportReusablePlanIsAtomicSnapshotAndIdempotent`: dòng của kế hoạch mẫu lưu bên trong bản ghi kế hoạch, nên sửa một dòng sẽ tăng version của kế hoạch. Test đọc lại version hiện tại để lần import chỉ sai ở dòng răng không hợp lệ (`400`).
- `StorageSimplificationTests`: đọc chi tiết đơn thuốc từ danh sách `GET /api/prescriptions/patients/{patientId}`.

`run-integration-coverage.ps1` xoá thư mục kết quả cùng tên trước khi chạy, vì ReportGenerator gộp mọi file coverage trong thư mục.

## 6. Báo cáo Excel (Report-5.2)

| Sheet | Nội dung |
|---|---|
| Cover | Tên dự án, mã dự án, phiên bản, lịch sử thay đổi |
| Test Cases | Danh sách chức năng → sheet tương ứng, mô tả, pre-condition; mô tả môi trường ở mục 3 |
| Test Statistics | Tổng Passed/Failed/Pending/N/A theo module, test coverage và test successful coverage (công thức sẵn có của template) |
| Một sheet / module | Mỗi dòng là một test case: ID, mô tả, các bước (request HTTP), kết quả mong đợi, pre-condition, kết quả Round 1 (Passed/Failed), ngày test, người test |
| SecurityTest | Các case bảo mật: chưa đăng nhập, sai vai trò, cách ly chi nhánh, thông báo lỗi đăng nhập không lộ thông tin |

Kết quả Round 1 lấy từ file `.trx` của lần chạy thật; không điền tay.

## 7. Module và file test

Danh sách file test theo module được giữ trong `backend/tests/DCMS.IntegrationTests/README.md`.
