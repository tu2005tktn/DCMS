# Lưu trữ điều trị trong bốn bảng

Ngày 12/11/2026. Rút 14 bảng điều trị hiện tại về bốn bảng nghiệp vụ theo cấu trúc phiếu gồm kế hoạch và quá trình điều trị.

| Bảng | Dữ liệu |
| --- | --- |
| `TREATMENT_DOSSIER` | Phiếu điều trị của bệnh nhân, thông tin và phiên bản kế hoạch, bản nháp phiếu và bản nháp chỉnh sửa trong JSONB |
| `TREATMENT_PLAN_ITEM` | Hạng mục kế hoạch đã áp dụng cho bệnh nhân, dịch vụ và giá đã thỏa thuận, các bước thực hiện trong JSONB |
| `CLINICAL_ACTIVITY` | Các bản ghi quá trình điều trị, phân loại lần thực hiện, đánh giá, ghi chú và hướng dẫn tái khám |
| `TREATMENT_TEMPLATE` | Kế hoạch cấu hình dùng lại, danh sách hạng mục và phiên bản mẫu cũ trong JSONB |

## Kế hoạch mẫu và kế hoạch trong phiếu

Nha sĩ cấu hình kế hoạch mẫu với nhiều hạng mục dịch vụ. Áp dụng mẫu vào phiếu tạo các hạng mục riêng của bệnh nhân theo thứ tự đã chọn, giữ snapshot dịch vụ, giá, thời lượng, răng và ghi chú. Nha sĩ được chỉnh kế hoạch của bệnh nhân theo hợp đồng hiện tại. Các dòng đã áp dụng có ID riêng để liên kết quá trình thực hiện, labo, vật tư và thanh toán.

Các phiên bản mẫu cũ giữ ID và trạng thái hiện tại trong collection của mẫu. Các bước thuộc hạng mục giữ ID, phiên bản, điều kiện trước, thời lượng và trạng thái. Các bản nháp giữ phạm vi người sửa, hạng mục, phiên bản và request key. Ghi collection nạp bảng cha có tracking, kiểm phiên bản nghiệp vụ và concurrency stamp trên dòng cha.

## Quá trình và lịch sử

Các bản ghi quá trình dùng một bảng phân loại. ID mà API đang dùng được giữ riêng với khóa lưu trữ. Lần thực hiện giữ khóa lưu trữ bằng ID hoạt động hiện có để các chứng từ đang tham chiếu tiếp tục trỏ đúng hoạt động. Đánh giá, ghi chú và tái khám có khóa lưu trữ riêng để bảo toàn cả dữ liệu cũ có ID trùng giữa các bảng nguồn hoặc dữ liệu dùng nguồn lượt khám từ cổng tích hợp.

Thực hiện điều trị tiếp tục kiểm nguồn, bệnh nhân, chi nhánh, nha sĩ, đồng ý điều trị, phiên bản và trạng thái trước khi ghi hoặc tính phí. Đánh giá và ghi chú giữ bản chất nghiệp vụ của từng loại bản ghi. Tái khám giữ cửa sổ ngày, trạng thái và lịch hẹn đã đặt. Các API truy vấn lọc theo đúng loại bản ghi.

`TREATMENT_EVENT` và `TREATMENT_PROGRESSION_EVENT` chuyển vào `AUDIT_LOG` dùng chung. Giữ ID sự kiện, loại, thời gian, nội dung, thứ tự và stable key. Người thực hiện của hai loại sự kiện này lưu ở cột `EventActorId` riêng, không ràng buộc khóa ngoại tài khoản, vì tác vụ hệ thống (quét đến hạn, đồng bộ lịch tái khám) ghi sự kiện với actor `0` như ở bảng nguồn. Các tác vụ phát hiện đến hạn và chăm sóc đọc lại từ loại sự kiện tương ứng. Sự kiện tiến độ cũ thiếu `planVersion` được bỏ qua khi không đủ dữ liệu để xác định bước cần xử lý, để các sự kiện hợp lệ tiếp tục được xử lý trong cùng lượt quét.

Các thay đổi nghiệp vụ, audit và receipt tiếp tục commit trong cùng giao dịch. Giữ khóa và phạm vi chống gửi trùng hiện tại, kiểm quyền trước replay. Khi lỗi hoặc cập nhật đồng thời, trả lỗi nghiệp vụ hiện có và bảo toàn trạng thái đã commit.

## Chuyển dữ liệu và kiểm chứng

Sao lưu `public` trước khi chuyển. Up chuyển dữ liệu trước khi bỏ 10 bảng nguồn. Down phục hồi cấu trúc và dữ liệu với ID, phiên bản và snapshot cũ. Đối chiếu dữ liệu trên bản sao và sau áp dụng vào DB phát triển. Mục tiêu là giảm từ 68 xuống 58 bảng vật lý, model EF từ 67 xuống 57 bảng.

## Bằng chứng chuyển đổi

Migration triển khai là `20261113013443_ConsolidateTreatmentStorage`, đã áp dụng vào DB phát triển ngày 13/11/2026. Bản sao lưu `public` trước chuyển đổi nằm tại `.tools/treatment-collapse/before-treatment-collapse-20261113.dump`, được Git ignore. SHA-256 là `07984e16e038b0dc7dadb532501b5d3489022e437ddf7b41d2d10e0a715e7544`.

Trên bản sao phục hồi từ bản sao lưu này, Up, Up rồi Down, và Down rồi Up lại đều khớp số lượng và digest của 24 nguồn với 2.160 dòng: 10 bảng nguồn, `CLINICAL_ACTIVITY`, ba bảng cha, `AUDIT_LOG` và 9 bảng liên quan không đổi (labo, khoản phí, thanh toán phiếu, PayOS, công việc chăm sóc, receipt, lượt khám, lịch hẹn). Thay đổi schema do Up tạo ra khớp thay đổi của model EF; khác biệt duy nhất là tên cũ của bốn khóa ngoại thuộc các bảng nguồn đã bỏ. Up giữ giá trị mặc định `gen_random_uuid()` cho `StorageStamp` và `'[]'` cho các cột collection. Các database bản sao đã được thu hồi.

Trước chuyển đổi DB phát triển có 3 bước, 85 dòng mẫu, 4 hướng dẫn tái khám, 64 sự kiện phiếu, 28 sự kiện tiến độ và 11 hoạt động; các bảng bản nháp, phiên bản mẫu, đánh giá và ghi chú trống. Năm sự kiện tiến độ dùng actor `0`, là lý do có cột `EventActorId`. Sau áp dụng đã đối chiếu trực tiếp cấu trúc mới với dữ liệu trước chuyển đổi: cả 24 nguồn khớp số lượng và digest. `public` giảm từ 68 xuống 58 bảng, model EF từ 67 xuống 57 bảng, mười bảng nguồn đã được bỏ.

Ba kiểm thử `TreatmentStorageTests` đã đạt. Ca rollback giữ bước, bản nháp, dòng và phiên bản mẫu, hoạt động, đánh giá và ghi chú dùng chung một ID, hướng dẫn tái khám, sự kiện phiếu và sự kiện tiến độ actor `0` qua Down rồi Up. Ca đồng thời từ chối ghi đè khi các bước của hạng mục hoặc bản nháp của phiếu đã đổi, kể cả lúc phiên bản nghiệp vụ giữ nguyên. Ca sự kiện cũ bỏ qua sự kiện bước thiếu `planVersion` và vẫn tạo công việc chăm sóc cho sự kiện đến hạn sau nó.

Backend restore/build bằng .NET 10 và web install/build thành công. Model EF khớp migration. Có 102 unit test đạt. Lượt integration toàn bộ gồm 571 test: 289 đạt ở lượt song song, 279 lỗi do PostgreSQL hết kết nối (`too many clients already`) đã đạt khi chạy lại 26 lớp theo lô tuần tự, 2 NFR tùy chọn skip. Tổng hợp theo từng test case đạt 568 integration test. Lượt chạy lại phát hiện `ItemPlanApiTests` còn đọc dòng mẫu như một bảng riêng; test đã chuyển sang đọc qua mẫu và đạt.

Test `ClinicalWorkApiTests.ReviewSaveDoesNotPostActivityAndActivityRetryUsesOneId` vẫn trả 503 theo lỗi đã tái hiện trên HEAD `1febcd7` trước các bước rút gọn.

API trên DB phát triển đã kiểm tra thành công đăng nhập bằng tài khoản nha sĩ demo, đọc phiếu điều trị, ngữ cảnh phiếu, hoạt động, lịch sử kế hoạch, ngữ cảnh thanh toán, phiếu labo, danh sách phiếu của bệnh nhân, hướng dẫn tái khám, mẫu và kế hoạch mẫu. Số hạng mục và số hướng dẫn tái khám từ API khớp DB. Phiên đăng nhập kiểm chứng đã đăng xuất. `GET /health` trả API và database healthy, log runtime không có lỗi; lỗi `KeyNotFoundException` khi đọc `planVersion` ghi nhận ở bước gộp khám không còn xuất hiện.

Trong lúc kiểm chứng, schema `dcms_test_*` còn sót từ các lượt test trước là 1.262 schema (DB khoảng 6,8 GB), làm PostgreSQL mất khoảng bảy phút phục hồi sau lần tắt đột ngột. Các schema này chưa được dọn.

## Phạm vi kiểm tra

Kiểm tra cấu hình mẫu, áp dụng và tùy chỉnh hạng mục, bản nháp và xác nhận kế hoạch, chỉnh sửa kế hoạch, tiến độ, lần điều trị thực tế, đánh giá, ghi chú, tái khám và đặt lịch, bàn giao từ khám, labo, vật tư và thanh toán. Bổ sung kiểm thử rollback, ID trùng giữa các loại quá trình, cập nhật collection đồng thời và sự kiện tiến độ cũ thiếu phiên bản. Chạy backend restore/build, web install/build, kiểm tra model/migration và API health sau chuyển đổi.
