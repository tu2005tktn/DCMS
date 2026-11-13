# Lưu trữ khám trong hai bảng

Ngày 12/11/2026. Yêu cầu gộp phần khám, chỉ định và tài liệu lâm sàng vào hai bảng vật lý `VISIT` và `CLINICAL_DOCUMENT`.

Migration `20261112143714_ConsolidateExaminationStorage` đã áp dụng vào DB phát triển. Phần khám còn hai bảng, schema `public` giảm từ 72 xuống 68 bảng và model EF giảm từ 70 xuống 67 bảng.

| Bảng | Nội dung sau chuyển đổi |
| --- | --- |
| `VISIT` | Tiếp nhận, bệnh nhân, bác sĩ, chi nhánh, trạng thái, nội dung khám hiện hành, người cập nhật, thời điểm xác nhận và danh sách chỉ định JSONB |
| `CLINICAL_DOCUMENT` | Metadata tệp lâm sàng và danh sách các lần bác sĩ đọc tài liệu theo lượt khám trong JSONB |

`VisitExamination` dùng chung dòng `VISIT` qua khóa lượt khám. Lượt chưa có phiếu khám tiếp tục trả examination null. Xóa phiếu khám riêng chỉ xóa nội dung khám. Các chỉ định giữ toàn bộ ID, phiên bản, kết quả, liên kết tài liệu, báo giá, bằng chứng và trạng thái thực hiện trong collection của lượt khám. Các lần đọc tài liệu giữ lượt khám, bác sĩ và thời điểm trong collection của tài liệu.

Các truy vấn và thao tác ghi phải đi qua cấu trúc mới ở cả khám, kê đơn, lịch sử khám của bệnh nhân/người thân, tài chính và bàn giao điều trị. Ghi chỉ định nạp lượt khám có tracking. Ghi review nạp tài liệu có tracking. Concurrency stamp trên lượt khám bảo vệ cập nhật phiếu và collection kể cả khi phiên bản nghiệp vụ chưa tăng. Tài liệu tiếp tục kiểm tra phiên bản khi ghi và review.

Báo giá, xác nhận thực hiện và ghi phí khám phải nạp lại đầy đủ lượt khám cùng các chỉ định sau khi lấy khóa giao dịch, rồi kiểm tra quyền, phiên bản và trạng thái trên dữ liệu vừa nạp. Thay đối tượng có tracking cũ bằng đối tượng mới để giữ collection JSONB và concurrency stamp đồng bộ.

Giữ kiểm quyền và chi nhánh ở server, kiểm bệnh nhân, trạng thái lượt khám, phiên bản, request key và khóa giao dịch hiện có. Tệp vẫn lưu ngoài DB. Chỉ tài liệu sẵn sàng được mở hoặc dùng làm kết quả. Báo giá và thực hiện chỉ định giữ quy trình tính phí hiện tại. Các liên kết tới chỉ định JSONB được kiểm tra trong dịch vụ trước khi ghi.

`CLINICAL_SOURCE_REFERENCE` là bảng tương thích cũ ngoài model EF. Trước khi bỏ bảng, migration ghi snapshot cấu trúc và nội dung vào loại audit chuyển đổi trong `AUDIT_LOG` dùng chung. Down phục hồi cấu trúc và dữ liệu từ snapshot, gồm cả biến thể bảng cũ dùng khi tiếp nhận. Thông tin nguồn đã gắn với lượt khám tiếp tục nằm trong `VISIT.SourceJson`.

Nhật ký khám/tài liệu và kết quả chống gửi trùng dùng các kho `AUDIT_LOG` và `COMMAND_RECEIPT` toàn hệ thống. Phí khám, thu tiền, thuốc và điều trị sử dụng các phân hệ hiện có.

## Chuyển đổi và kiểm chứng

Sao lưu `public` trước chuyển đổi. Migration chuyển dữ liệu trước khi bỏ `VISIT_EXAMINATION`, `INVESTIGATION_ORDER`, `DOCUMENT_REVIEW` và bảng tham chiếu cũ. Kiểm tra Up/Down trên bản sao và đối chiếu số lượng, nội dung, ID và phiên bản. Mục tiêu trên DB phát triển hiện tại là giảm từ 72 xuống 68 bảng vật lý, còn 67 bảng trong model EF.

Kiểm tra lưu nháp, xác nhận, đính chính, nhận/đọc/hủy chỉ định, báo giá/thực hiện/tính phí, quét và đọc tài liệu, kê đơn, lịch sử bệnh nhân/người thân, bàn giao điều trị, tiếp nhận và chống ghi đè khi cập nhật đồng thời. Kiểm tra rollback với dữ liệu bảng tham chiếu cũ có bản ghi. Chạy backend restore/build, web install/build, kiểm tra model/migration và API health sau áp dụng.

## Bằng chứng chuyển đổi

Migration triển khai là `20261112143714_ConsolidateExaminationStorage`. Bản sao lưu trước chuyển đổi nằm tại `.tools/examination-collapse/before-examination-collapse.dump`, được Git ignore. SHA-256 là `847b073c1d8068b6aaaf96d5b3158fc04065cb052f3909d22a9dd2f91a400e7e`.

Up và Down trên bản sao đã đối chiếu 1.294 bản ghi của 9 nguồn, gồm sáu bảng khám cùng `FINANCE_CHARGE`, `PRESCRIPTION` và `AUDIT_LOG`. Số lượng và digest khớp cả sau Up và sau rollback. Trước chuyển đổi có 34 lượt khám, 24 phiếu khám, 11 chỉ định, 3 tài liệu, 2 lần đọc tài liệu và bảng tham chiếu cũ trống.

Bốn kiểm thử lưu trữ bổ sung đã đạt. Hai ca kiểm tra dữ liệu có bản ghi với bảng tham chiếu dạng đầy đủ và dạng cũ rút gọn, đồng thời giữ phiếu khám, chỉ định và review qua rollback. Hai ca kiểm tra cập nhật chỉ định và phiếu khám từ dữ liệu cũ bị từ chối khi lượt khám đã thay đổi, kể cả lúc phiên bản nghiệp vụ giữ nguyên.

Sau áp dụng đã đối chiếu trực tiếp cấu trúc mới với dữ liệu trước chuyển đổi. Cả 9 nguồn khớp số lượng và digest. Bốn bảng nguồn đã được bỏ và các database bản sao kiểm chứng đã được thu hồi.

Backend restore/build và web install/build thành công. Model EF khớp migration. Có 102 unit test đạt. Lượt integration liên quan ban đầu đạt 174/201 test và phát hiện lỗi nạp lại collection chỉ định ở luồng tài chính. Sau khi sửa, cả 51 test thuộc tài chính, phí khám và lưu trữ khám đạt. Tổng hợp theo từng test case đạt 201 integration test liên quan.

Test `ClinicalWorkApiTests.ReviewSaveDoesNotPostActivityAndActivityRetryUsesOneId` được loại khỏi lượt hồi quy theo lỗi 503 đã tái hiện trên HEAD `1febcd7` trước các bước rút gọn. Fixture giả lập Visit trong cổng clinical, còn cổng ghi phí kiểm tra Visit trong PostgreSQL.

API trên DB phát triển đã kiểm tra thành công đăng nhập, đọc phiếu khám, chỉ định, danh sách và chi tiết tài liệu cùng các lần bác sĩ đọc tài liệu. Số chỉ định từ API khớp DB. Phiên đăng nhập kiểm chứng đã đăng xuất. `GET /health` trả API và database healthy.

Log runtime còn ghi lỗi `KeyNotFoundException` tại `CareTaskIngestionService.cs` dòng 55 khi đọc `planVersion` từ sự kiện tiến độ điều trị. Cùng lỗi và vị trí đã có trong log runtime của bước gộp lịch trước bước gộp khám. Kiểm tra health API/DB và các API khám nêu trên đã đạt trong trạng thái này.
