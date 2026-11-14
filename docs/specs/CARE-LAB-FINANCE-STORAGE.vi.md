# Chăm sóc một bảng, labo hai bảng, tạm gỡ tài chính

## Điều chỉnh 13/11/2026: tài chính trên schema rút gọn

### Phạm vi nghiệp vụ đã chốt

- Không có giảm giá, hoàn tiền hoặc chỉ số “tiền thu ròng” trong luồng mới. Dữ liệu lịch sử tương ứng vẫn nằm trong bản lưu để đối chiếu, không bị sửa hoặc xóa.
- Báo cáo gộp “giá dịch vụ” và “tiền thực thu” thành **Tiền thực thu**: tổng các khoản thu đã xác nhận và đã gắn với dịch vụ thực tế đã làm. Khoản thu Pending và tiền nhận trước chưa phân bổ không được tính vào chỉ số này. Ngày báo cáo là ngày xác nhận thu; ngày làm dịch vụ được lưu riêng cho bộ lọc nghiệp vụ.
- **Tiền nhận trước** là khoản đã xác nhận nhận từ bệnh nhân nhưng chưa gắn với dịch vụ đã làm. Nhận trước không đòi hỏi chọn một dịch vụ chưa làm; chỉ gắn bệnh nhân/chi nhánh, có thể gắn phiếu điều trị. Khi có dịch vụ Posted, nhân viên mới phân bổ từ số dư nhận trước vào chi phí dịch vụ. Mỗi đồng chỉ nằm ở một trong hai nơi: số dư nhận trước hoặc tiền thực thu của dịch vụ. Phân bổ không tạo thêm dòng tiền vào phòng khám.
- Phí dịch vụ đã làm lấy từ hoạt động lâm sàng Posted và giá được chốt tại thời điểm làm. Phí phát sinh sau khi bệnh nhân đã trả đủ làm tăng công nợ; các khoản đã thu không được tự ý viết lại. Thu nhiều lần được phép tới mức phí còn phải thu. Tiền nhận trước chưa phân bổ không tự động giảm công nợ.
- API đọc phiếu trả các khoản phí, số đã thu, số dư nhận trước, khoản đã phân bổ và công nợ; API ghi thu tiền mặt hoặc tạo QR PayOS ghi Pending trước, chỉ chuyển Posted sau xác thực PayOS. API phân bổ tiền nhận trước phải kiểm tra số dư và phí chưa thanh toán trong cùng giao dịch, có request key chống lặp. Mọi API lọc bệnh nhân/chi nhánh và quyền trên server.

Đặc tả này thay thế các phần nói về giảm giá, hoàn tiền, nhận trước cho dịch vụ chưa làm và tiền thu ròng trong tài liệu kế hoạch tài chính cũ. Phần nghiệp vụ cũ vẫn được giữ làm lịch sử tham khảo, không phải yêu cầu triển khai hiện hành.

Trạng thái triển khai tại thời điểm rút gọn: database đã áp đủ migration và có đúng 33 bảng vật lý (bao gồm `__EFMigrationsHistory`); 18 bảng tài chính cũ cùng hai loại dòng dùng chung đã được lưu thành 20 snapshot trong `AUDIT_LOG`. API hồ sơ bệnh nhân đọc được các khoản thu lịch sử theo liên kết đã xác minh. Màn thống kê tài chính bên dưới chỉ đọc chứng từ lịch sử đã lưu. Các lệnh ghi thu mới, nhận trước, phân bổ, hoàn tiền, hóa đơn, PayOS và báo cáo doanh thu trên sổ mới **chưa được triển khai**; giao diện thu tiền không được mở lại cho đến khi các lệnh này và công nợ được kiểm thử. Bản sao lưu trước migration nằm ngoài Git tại `.artifacts/database-backups/`.

### Thống kê tài chính tối giản theo yêu cầu 13/11/2026

Màn thống kê chỉ trình bày **Tiền thực thu** và **Tiền nhận trước**, có lọc ngày nhận tiền theo múi giờ Asia/Ho_Chi_Minh và chi nhánh. Bỏ các chỉ số/luồng trên màn này: Giá dịch vụ, Giảm giá, Hoàn tiền, Tiền thu ròng và Nhận trước cho dịch vụ chưa làm. Tiền thực thu là phần tiền đã xác nhận và gắn với dịch vụ đã làm; không cộng khoản cọc/nhận trước, kể cả khi khoản đó sau này được phân bổ. Tiền nhận trước là tổng khoản cọc/ứng trước đã xác nhận, chỉ hiển thị trong chỉ số này. Chờ chuyển khoản PayOS không tính ở cả hai chỉ số. API `GET /api/finance/statistics?from=yyyy-MM-dd&to=yyyy-MM-dd&branchId=` chỉ trả các khoản xác định được nguồn và trạng thái từ archive; khi sổ mới triển khai, API phải hợp nhất theo cùng quy tắc và không nhân đôi chứng từ. Quyền đọc dành cho chủ phòng khám; mọi filter chi nhánh áp dụng tại server.

Yêu cầu mới: khôi phục thanh toán và tài chính trên schema rút gọn, **không tạo thêm bảng vật lý**. `AUDIT_LOG` là sổ chứng từ tài chính phân loại bằng `AuditType=FinanceLedger`; mỗi dòng bất biến chứa `PatientId`, `BranchId`, thời điểm, loại nghiệp vụ và payload JSONB. `COMMAND_RECEIPT` lưu request key, fingerprint và kết quả để chống ghi trùng. `TREATMENT_DOSSIER`, `CLINICAL_ACTIVITY`, `VISIT`, `PATIENT_PROFILE` và `SCHEDULING_BRANCH` vẫn là nguồn hồ sơ, dịch vụ đã làm, lượt khám, bệnh nhân và chi nhánh. Các thông tin phương thức/QR PayOS là payload của dòng tài chính, không sinh bảng mới. Mọi query tài chính phải lọc theo bệnh nhân/chi nhánh và quyền ở server; khách hàng chỉ đọc chứng từ của liên kết VERIFIED.

Trước migration `RemoveFinanceStorage`, phải chạy các migration gộp trước đó rồi sao lưu nguyên trạng **tất cả** 18 bảng tài chính cũ vào `AUDIT_LOG` dưới `AuditType=FinanceLegacyArchive`, mỗi bảng một snapshot JSONB với tên bảng, số dòng và dữ liệu. Script dùng khóa định danh cố định để chạy lại không nhân bản; kiểm tra số dòng snapshot bằng số dòng nguồn trước khi áp migration xóa bảng. Đây là lưu trữ lịch sử để đối chiếu, không tự suy hai nguồn thu cũ là cùng giao dịch. Dữ liệu thanh toán cũ được chiếu thành giao dịch có nguồn, ID và trạng thái rõ ràng; chỉ Posted mới cộng vào số đã thu. Bản `pg_dump` bên ngoài Git là phương án khôi phục cuối cùng.

Các lệnh thu tiền mặt, tạo chuyển khoản PayOS, xác nhận webhook/polling, nhận trước, phân bổ, hóa đơn, hoàn tiền và điều chỉnh phải ghi dòng sổ cái bất biến trong giao dịch PostgreSQL với kiểm tra version/idempotency. Pending không tính đã thu; nhận trước chưa phân bổ không tính doanh thu; hoàn tiền hoàn tất trừ tiền thu ròng. Giá trị dịch vụ lấy từ hoạt động đã Posted, không lấy tổng kế hoạch. API/UI cũ chỉ được mở lại khi phép tính và quyền tương ứng đã kiểm thử trên schema mới. Mọi thay đổi cấu trúc chỉ được thêm cột/chỉ mục vào bảng hiện có nếu thật cần thiết; tổng số bảng vật lý không được tăng.

Ngày 13/11/2026. Yêu cầu của chủ dự án sau bước gộp điều trị: phần chăm sóc bệnh nhân chỉ giữ một bảng, phần labo còn hai bảng là phiếu labo và danh mục labo, phần tài chính xóa toàn bộ để thiết kế và thêm lại sau.

## Chăm sóc bệnh nhân

| Bảng | Dữ liệu |
| --- | --- |
| `CARE_TASK` | Công việc chăm sóc, bệnh nhân, chi nhánh, nguồn phát sinh, hạn xử lý, người phụ trách và trạng thái; các lần liên hệ và các lần chuyển cấp trong JSONB |

`CARE_CONTACT_ATTEMPT` và `CARE_ESCALATION` chuyển thành hai collection `ContactAttempts` và `Escalations` trên dòng `CARE_TASK`. Mỗi phần tử giữ ID, người thực hiện, thời điểm và nội dung như dòng nguồn; chuyển cấp giữ thêm trạng thái, phiên bản, thời điểm ghi nhận và xử lý. Route, DTO, quyền, quy tắc retry và lỗi phiên bản của màn hình chăm sóc giữ nguyên.

Ghi lần liên hệ hoặc chuyển cấp nạp công việc có tracking và ghi qua dòng cha. Dòng cha có `StorageStamp` làm concurrency token, nên hai thao tác đồng thời trên cùng công việc không ghi đè nhau kể cả khi phiên bản nghiệp vụ của công việc giữ nguyên. Tra cứu một lần chuyển cấp theo ID đi qua công việc chứa nó.

## Labo

| Bảng | Dữ liệu |
| --- | --- |
| `LAB_ORDER` | Phiếu labo theo hoạt động điều trị, xưởng, loại công việc, răng, màu, ngày gửi/nhận/giao, trạng thái, phí labo; lịch sử trạng thái trong JSONB |
| `LAB_WORK_TYPE` | Danh mục labo: loại công việc theo xưởng, đơn vị, giá tham khảo và bảo hành |

`LAB_ORDER_STATUS_LOG` chuyển thành collection `StatusLogs` trên phiếu. Lịch sử vẫn chỉ thêm, giữ ID, trạng thái cũ và mới, thời điểm, người đổi và ghi chú. Danh sách xưởng labo tiếp tục lấy từ danh mục nhà cung cấp dùng chung `CATALOG_PROVIDER`. `TotalFeeVnd` và `PaidVnd` trên phiếu là thông tin của phiếu labo và được giữ.

## Tài chính

Toàn bộ phân hệ tài chính được gỡ khỏi backend, web và database. Chức năng sẽ được đặc tả lại khi thêm lại; các đặc tả tài chính hiện có chỉ còn giá trị tham khảo.

Bảng bị bỏ (18): `FINANCE_CHARGE`, `EXAMINATION_FEE`, `FINANCE_INVOICE`, `FINANCE_INVOICE_LINE`, `PAYMENT_RECEIPT`, `PAYMENT_ALLOCATION`, `RECEIPT_PRINT_EVENT`, `BRANCH_RECEIPT_SEQUENCE`, `FINANCE_ADVANCE`, `FINANCE_ADVANCE_ALLOCATION`, `FINANCE_REFUND`, `FINANCIAL_ACCOUNT`, `FINANCE_PAYMENT_METHOD`, `TREATMENT_DOSSIER_PAYMENT`, `TREATMENT_DOSSIER_PAYMENT_LINE`, `TREATMENT_PAYOS_ORDER`, `PROVIDER`, `PROVIDER_PAYABLE`. Nhật ký (`FinanceAudit`) và receipt (`FinanceCommandReceipt`) của tài chính trong `AUDIT_LOG` và `COMMAND_RECEIPT` cũng bị xóa, cùng index `IX_AUDIT_LOG_BranchId_At` chỉ phục vụ nhật ký tài chính. Các cột dùng chung của hai kho này giữ nguyên, kể cả độ rộng 64 ký tự của `AUDIT_LOG.Action`; cột `BillingUnit`, `InvestigationKind` của `SCHEDULING_SERVICE` giữ kiểu cũ.

Chức năng bị gỡ:

- Thu ngân: hàng chờ thu, hóa đơn, phiếu thu, in phiếu, số phiếu thu theo chi nhánh, phương thức thanh toán, ứng trước, hoàn tiền, công nợ phải thu và phải trả nhà cung cấp.
- Phí khám và tính phí chỉ định cận lâm sàng trên màn hình khám, cùng màn hình quản lý giá dịch vụ khám và dịch vụ cận lâm sàng (quyền `services.*`, `investigations.perform`). Hoàn tất khám không còn chặn vì chỉ định đã lập phí chưa xác nhận thực hiện.
- Thanh toán, ứng trước, chuyển khoản PayOS và phiếu chuyển khoản trên phiếu điều trị; tổng hợp đã thu/còn nợ của phiếu và màn hình hồ sơ tài chính của bệnh nhân (`/api/patients/{id}/dossiers/financial-context`).
- Báo cáo doanh thu và các số liệu tiền đã thu trong thống kê.
- Các quyền `finance.*` và mục menu, màn hình, nút tương ứng trên web.

Phần được giữ vì không thuộc sổ tài chính: giá tham chiếu và giá đã thỏa thuận của dịch vụ, hạng mục kế hoạch và hoạt động điều trị; giá vật tư và lần nhập vật tư; phí trên phiếu labo; các dòng dịch vụ khám và cận lâm sàng trong `SCHEDULING_SERVICE` (chỉ không còn màn hình định giá). Thống kê công việc/dịch vụ giữ chi phí theo phiếu, số đã thu và còn nợ bằng 0. Ghi nhận hoạt động điều trị, hoàn tất khám và chỉ định không còn tạo khoản phí. Hoạt động tính phí riêng chỉ ghi sự kiện tiến độ `clinical.fee.requested` (khóa `clinical-fee:{activityId}`) và có `FeeStatus` = `Pending`, để phân hệ tài chính mới tiêu thụ sau. Tổng hợp tài chính của phiếu trả `available = false`, chỉ còn số tiền dự toán của kế hoạch. Xóa phiếu labo dùng một lệnh xóa trực tiếp có kiểm `StorageStamp`, vì phiếu luôn có lịch sử trạng thái trong JSONB.

## Chuyển dữ liệu

Hai migration nối tiếp sau `ConsolidateTreatmentStorage`:

1. Gộp chăm sóc và labo. Up chuyển từng dòng nguồn vào collection của dòng cha trước khi bỏ ba bảng nguồn, kiểm số lượng trong cùng giao dịch. Down dựng lại ba bảng và phục hồi dữ liệu với ID, thứ tự và nội dung cũ.
2. Gỡ tài chính. Up xóa 18 bảng cùng dữ liệu, các dòng audit và receipt của tài chính. Down chỉ dựng lại cấu trúc rỗng. Dữ liệu tài chính của DB phát triển chỉ còn trong bản sao lưu `public` lấy trước khi áp dụng, theo quyết định của chủ dự án.

Mục tiêu: model EF từ 57 xuống 36 bảng, `public` từ 58 xuống 37 bảng.

## Kiểm chứng

- Up, Down và Up lại của migration gộp chăm sóc/labo trên bản sao, đối chiếu số lượng và digest từng nguồn; đối chiếu trực tiếp sau khi áp dụng.
- Sau migration gỡ tài chính: các bảng còn lại giữ nguyên số lượng và digest; không còn bảng, cột, audit hay receipt tài chính.
- Kiểm thử lưu trữ: giữ lần liên hệ, chuyển cấp và lịch sử trạng thái qua rollback; từ chối ghi đè khi collection đã đổi.
- Backend restore/build bằng .NET 10, bộ kiểm thử còn lại, web install/build, model khớp migration, API health và đọc màn hình chăm sóc, labo, phiếu điều trị, khám trên DB phát triển.

## Kết quả triển khai ngày 13/11/2026

Hai migration `20261113022238_ConsolidateCareAndLabStorage` và `20261113072340_RemoveFinanceStorage` đã áp dụng vào DB phát triển. Model EF còn 36 bảng; `public` còn 37 bảng gồm `__EFMigrationsHistory`, khớp [danh sách bảng](../architecture/DATABASE-TABLE-INVENTORY.vi.md). Bản sao lưu `public` trước khi áp dụng nằm tại `.tools/finance-removal/before-finance-removal-20261113.dump`, được Git ignore, SHA-256 `7d94c748ba2a0ba6eee1f1a3ba7eadd76c22a2d2d3b0a77a18ada22d1408e3d0`. Dữ liệu tài chính đã xóa chỉ còn trong bản sao lưu này.

Trên bản sao phục hồi từ DB phát triển, migration EF thật chạy Up, Down về `ConsolidateTreatmentStorage` rồi Up lại. Cả 39 nguồn được giữ với 2.743 dòng, gồm ba bảng nguồn chăm sóc/labo, khớp số lượng và digest ở cả ba bước. Up xóa 72 dòng `FinanceAudit` và 46 receipt `FinanceCommandReceipt`; Down dựng lại 18 bảng tài chính rỗng. Thay đổi schema của bảng giữ lại chỉ là bỏ index nhật ký tài chính. Sau khi áp dụng vào DB phát triển, đối chiếu trực tiếp cả 39 nguồn đều khớp, không còn bảng hay nhật ký tài chính. Trước chuyển đổi có 5 công việc chăm sóc với 1 lần liên hệ, 0 lần chuyển cấp, 4 phiếu labo với 10 dòng lịch sử trạng thái.

Backend restore/build bằng .NET 10 và web build thành công; model EF khớp migration. Có 100 unit test đạt (hai test cổng PayOS đã gỡ cùng tài chính). Lượt integration toàn bộ chạy tuần tự gồm 507 test: 504 đạt, 2 NFR tùy chọn skip, 1 lỗi ở bước xóa phiếu labo. EF Core 10 không xóa được dòng đang theo dõi khi collection JSONB còn phần tử, nên việc xóa phiếu labo chuyển sang lệnh xóa trực tiếp có kiểm `StorageStamp`; chạy lại test labo, hai test lưu trữ chăm sóc/labo mới, test lưu trữ điều trị và test công việc lâm sàng đều đạt 10/10. Test `ClinicalWorkApiTests.ReviewSaveDoesNotPostActivityAndActivityRetryUsesOneId`, trước đây trả 503 do cổng ghi phí kiểm tra Visit trong PostgreSQL, nay đạt vì hoạt động chỉ ghi sự kiện yêu cầu phí.

Đã gỡ các lớp test thuần tài chính (`FinanceApiTests`, `ExaminationBillingApiTests`, `ClinicalPresetPriceApiTests`, `PayOsGatewayTests`) và sửa các test còn lại theo hành vi mới: phí là `Pending`, tổng hợp tài chính `available = false`, thống kê công việc có số đã trả và còn nợ bằng 0.

API trên DB phát triển: `GET /health` trả API và database healthy. Đăng nhập nha sĩ demo, đọc chi tiết công việc chăm sóc, lịch sử và danh sách phiếu labo, phiếu điều trị, ngữ cảnh phiếu, hoạt động, phiếu khám, chỉ định và thống kê công việc đều thành công; số lần liên hệ, chuyển cấp và dòng lịch sử labo từ API khớp DB. Các route `/api/finance/*` và `/api/dossiers/{id}/payments/*` trả 404. Phiên kiểm chứng đã đăng xuất, log API không có lỗi. Giao diện web chưa được bấm thử trên trình duyệt; bản build TypeScript/Vite đạt.
