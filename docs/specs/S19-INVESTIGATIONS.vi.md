# Chỉ định cận lâm sàng trong phần Khám

Bổ sung S19/S20 theo yêu cầu ngày 27/10/2026. Không tự chỉ định theo chẩn đoán và không tự tạo hoạt động điều trị/chi phí.

## Luồng

Tab Cận lâm sàng trong Visit: bác sĩ phụ trách đã Bắt đầu khám tạo chỉ định gồm nhóm kỹ thuật theo danh mục bên dưới, tên cụ thể, răng/vùng nếu có, lý do và lựa chọn Cần kết quả để kết luận (mặc định có). Có gợi ý tên nhưng cho nhập khác; bác sĩ quyết định nội dung, không chọn sẵn theo bệnh.

Trạng thái: Ordered (Chờ kết quả) → ResultAvailable (Có kết quả, chờ bác sĩ đọc) → Reviewed (Bác sĩ đã đọc). Ordered/ResultAvailable có thể Cancelled với lý do bởi bác sĩ phụ trách. Reviewed giữ bất biến; sai/bổ sung lập chỉ định mới khi phiếu nháp hoặc theo luồng đính chính của lượt đang mở. Không xóa chỉ định khỏi lịch sử.

Lễ tân/trợ lý thuộc chi nhánh hoặc bác sĩ phụ trách nhận kết quả. Có thể tải tệp vào đúng chỉ định khi Ordered. Tệp giữ kiểm tra chữ ký/MIME/antivirus/quyền như S20. Nhận kết quả cần nội dung văn bản hoặc ít nhất một tệp Ready; PendingScan/Rejected không được dùng làm bằng chứng đã nhận/đọc tệp. Khi nhận kết quả, chốt snapshot danh sách tệp Ready hiện có. Nếu có văn bản nhưng tệp còn PendingScan, chỉ xác nhận đọc văn bản và các tệp trong snapshot, không tự ghi đã đọc tệp đang kiểm tra. Sau nhận kết quả không tải thêm vào cùng chỉ định; cần sửa thì bác sĩ hủy có lý do và lập lại theo quyền tạo của phiếu nháp hoặc đính chính của lượt đang mở.

Bác sĩ phụ trách mở kết quả, có thể ghi nhận định (tùy chọn), bấm Xác nhận đã đọc. Kiểm lại quyền/trạng thái/phiên bản và Ready của các tệp snapshot; ghi người đọc/thời điểm và review từng tệp trong snapshot. Người nhận kết quả không được thay bác sĩ ký đọc. Tải tệp lên hoặc đánh dấu đã xem tệp riêng lẻ không tự chuyển chỉ định sang Reviewed.

Nếu có chỉ định cần cho chẩn đoán chưa Reviewed/Cancelled, chặn Hoàn tất khám/đính chính; vẫn lưu nháp và giữ tab Tổng kết với nút đến Cận lâm sàng. Không tạo chẩn đoán tạm giả để vượt kiểm tra. Chỉ định không cần cho chẩn đoán không chặn xác nhận phiếu nhưng mọi chỉ định chưa Reviewed/Cancelled đều chặn Kết thúc toàn lượt. Visit vẫn InProgress; nhãn Chờ kết quả/Chờ bác sĩ đọc là tiến độ khám, không thêm enum Visit mới. Tạo chỉ định khi phiếu chưa xác nhận hoặc theo luồng bổ sung có lý do khi đính chính của Visit InProgress; nhận/đọc/hủy cho lượt InProgress, kể cả phiếu đã xác nhận với chỉ định không chặn chẩn đoán.

## Dữ liệu và API

INVESTIGATION_ORDER lưu UUID, Visit, loại/tên/vùng/lý do, requiredForDiagnosis, state, resultText, snapshot document IDs, interpretation, cancellationReason, người/thời điểm tạo-nhận-đọc-hủy, Version và khóa request tạo. CLINICAL_DOCUMENT thêm InvestigationId nullable/FK. Dùng migration cộng thêm, giữ dữ liệu/tệp cũ. Không bắt dossier/plan để chỉ định từ khám.

GET `/api/visits/{visitId}/investigations` trả VisitId/VisitVersion/CanOrder/Items với quyền thao tác và tài liệu từng chỉ định. POST cùng đường dẫn tạo mới; POST `/{id}/result`, `/{id}/review`, `/{id}/cancel` nhận kết quả/đọc/hủy. Lệnh có patientId/expectedVisitVersion/requestKey; lệnh với chỉ định có thêm expectedVersion. Trả danh sách mới và VisitVersion để cập nhật ngữ cảnh mà không ghi đè bản khám chưa lưu. GET examination workspace kèm investigations.

Kiểm quyền staff đang hoạt động/chi nhánh/đúng bệnh nhân/Visit và bác sĩ phụ trách ở backend. Sai tài nguyên trả 404, sai quyền 403, stale/reuse key khác payload MSG111/409. Receipt tái dùng EXAMINATION_RECEIPT, fingerprint gồm action/Visit/order/payload; replay không lặp dữ liệu/audit. Transaction + advisory lock chung với khám/tài liệu đảm bảo nhận/đọc/xác nhận không đua trạng thái. Mỗi lệnh đổi Order.Version/Visit.Version và ghi VisitAudit, không tăng examinationVersion. File upload liên kết đổi phiên bản khi có file thành công; receipt replay không tăng lại.

Tên tối đa 160, vùng 200, lý do 1000, kết quả/nhận định 4000, lý do hủy 1000 ký tự; tối đa 100 chỉ định mỗi Visit. Nhận định tùy chọn; kết quả phải có văn bản hoặc tệp Ready. Không nhận InvestigationId tùy ý trên upload: phải tồn tại, cùng Patient/Visit và còn Ordered, người tải có quyền nhận kết quả. Hủy/đã đọc/đã nhận không nhận file mới vào cùng chỉ định. ConsentVersion chưa có nguồn vẫn từ chối như trước.

## Giao diện và kiểm chứng

Chỉ định hiển thị trạng thái/lý do/vùng, bác sĩ và mốc thời gian; kết quả có văn bản, snapshot tệp, nhận định. Liên kết tệp dùng lại viewer S20, không hiển thị nội dung PendingScan/Rejected. Có Tải lại khi dữ liệu thay đổi; lỗi không xóa bản nhập. Tạo/nhận/đọc/hủy cập nhật VisitVersion ở trang cha và giữ triệu chứng/findings/chẩn đoán chưa lưu. Điều hướng khi đang xử lý theo cảnh báo hiện có.

Kiểm thử API/PostgreSQL riêng: quyền/Patient/Visit, trạng thái, stale/idempotency, văn bản/Ready/PendingScan/Rejected, snapshot/review tệp, chặn confirm/complete, hủy có lý do và dữ liệu cũ. Browser API thật: tạo từ khám có bản nhập chưa lưu, chặn hoàn tất, nhận tệp/nội dung, bác sĩ đọc, xác nhận khám, hủy chỉ định nhầm và giữ luồng cũ. Scanner Ready/Rejected trong API tests là stand-in; phải nêu rõ trong báo cáo.


## Danh mục kỹ thuật cụ thể — bổ sung 27/10/2026

Thay cách hiển thị chỉ có X-quang/Xét nghiệm/Khác bằng năm nhóm: Chụp phim răng hàm mặt (XRay), Chẩn đoán hình ảnh khác (Imaging), Xét nghiệm (Lab), Giải phẫu bệnh / tế bào học (Pathology), Khác (Other). Đây là nhóm phân loại; tên kỹ thuật cụ thể bắt buộc, có gợi ý và cho nhập ngoài danh mục. Đổi nhóm xóa tên đang chọn để tránh gán nhầm nhóm; không đổi chỉ định đã lập.

- Chụp phim: quanh chóp (cận chóp), cánh cắn, mặt nhai, toàn cảnh Panorama/OPG, sọ nghiêng Cephalometric, sọ thẳng, CBCT vùng răng/hàm.
- Hình ảnh khác: siêu âm tuyến nước bọt/vùng hàm mặt, MRI khớp thái dương hàm, CT hàm mặt.
- Xét nghiệm: công thức máu, đường huyết, HbA1c, PT/INR, aPTT, chức năng gan (AST/ALT), chức năng thận (creatinin/eGFR), vi sinh nuôi cấy và kháng sinh đồ. Cho phép tách từng xét nghiệm theo nhu cầu thực tế, không tạo sẵn gói xét nghiệm.
- Giải phẫu bệnh / tế bào học: mô bệnh học mẫu sinh thiết, tế bào học tổn thương miệng. Đây là yêu cầu phân tích mẫu; không tự tạo thủ thuật lấy mẫu hay chi phí.
- Khác: nhập rõ tên kỹ thuật ngoài danh mục.

Gợi ý không phải chỉ định thường quy theo bệnh; bác sĩ chọn theo tình trạng và khả năng thực hiện/chuyển gửi. Chụp phim là cách gọi các kỹ thuật X-quang/CBCT, không tạo nhóm trùng tên X-quang. Danh mục này là gợi ý nhập liệu ban đầu, chưa phải danh mục dịch vụ có giá hay kết nối máy xét nghiệm/PACS. API chấp nhận thêm Imaging/Pathology; không đổi bản ghi cũ hoặc migration. Tệp Lab/Pathology mặc định LabResult, Imaging/Other mặc định Other, XRay mặc định XRay.

Tham khảo tên kỹ thuật phim: [ADA dental radiographs](https://www.ada.org/resources/ada-library/oral-health-topics/x-rays-radiographs), [ARPANSA dental imaging](https://www.arpansa.gov.au/understanding-radiation/sources-radiation/dental-imaging). Danh mục phần mềm không thay hướng dẫn chỉ định chuyên môn.


## Chỉ định bổ sung khi đính chính — 27/10/2026

Thay giới hạn chỉ tạo trước xác nhận bằng: phiếu nháp tạo như cũ; phiếu đã xác nhận, Visit còn InProgress, bác sĩ phụ trách bấm Đính chính thì được Thêm chỉ định. UI truyền chế độ đính chính xuống Cận lâm sàng, không chỉ dựa allowedActions.save. Form yêu cầu lý do bổ sung sau xác nhận và dùng lại lý do đó ở phần Lưu đính chính để tránh nhập hai lần. Backend không tin boolean UI: cần đúng bác sĩ/quyền manage, Visit InProgress, expectedVisitVersion và expectedExaminationVersion hiện tại, lý do không trắng tối đa 1000. Dòng chỉ định mới lưu ngay với audit nguồn phiên bản khám/lý do; không sửa nội dung hay snapshot phiếu đã xác nhận. Retry giữ idempotency, stale/mất quyền vẫn bị chặn.

Chỉ định cần cho kết luận chưa đọc/hủy chặn lưu đính chính và handoff điều trị; kết thúc Visit vẫn chặn mọi chỉ định chưa xử lý. Hủy chế độ đính chính chỉ bỏ bản nhập phiếu, không tự xóa/hủy chỉ định đã được tạo; UI nhắc điều này. Sau khi nhận/đọc hoặc hủy hợp lệ có thể lưu đính chính có lý do như cũ. Tạo chỉ định không tự tăng examinationVersion hay tự xác nhận lại nội dung lâm sàng.

Visit Completed vẫn cho đính chính lịch sử theo hợp đồng cũ nhưng không được tạo chỉ định mới; panel phải giải thích cần lượt khám mới, không chỉ để trống nút. Nếu phiếu đã xác nhận và chưa vào đính chính, hiển thị hướng dẫn bấm Đính chính kết quả. Tạo trên phiếu nháp và các quyền/trạng thái nhận-đọc-hủy cũ giữ nguyên.

API CreateInvestigation bổ sung expectedExaminationVersion?: int và amendmentReason?: string. Audit CreateInvestigation giữ trường order hiện có, thêm sourceExaminationVersion/amendmentReason khi tạo sau xác nhận. Không cần migration. Kiểm thử cần ca confirmed/open/amending, không lý do, stale version, sai bác sĩ/quyền, Completed, replay; browser xác nhận → Đính chính → tạo → nhận/đọc → Lưu đính chính, giữ draft và lý do.

Kiểm chứng PendingScan trên fixture browser riêng cần chế độ antivirus có kiểm soát: prepare-clinical-e2e mặc định không nối ClamAV để giữ nhánh chờ; -UseRealAntivirus giữ cấu hình thật khi kiểm riêng luồng quét. Không đổi scanner hoặc trạng thái tệp của phiên manual.


Bổ sung giá và thực hiện/thanh toán ngày 27/10: [INVESTIGATION-BILLING.vi.md](INVESTIGATION-BILLING.vi.md). Chỉ định/nhận/đọc không tự tạo phí; xác nhận dịch vụ đã thực hiện theo báo giá mới tạo source charge duy nhất.
