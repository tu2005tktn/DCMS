# Ảnh trực tiếp trên phiếu điều trị

## Hành vi
Ô ảnh viền nét đứt dưới HÌNH ẢNH của từng phiếu là nút chọn file JPEG/PNG trên máy. Sau khi chọn, tải trực tiếp vào đúng patientId/dossierId; không điều hướng, không yêu cầu Visit giả. Từng tệp có trạng thái và thử lại với cùng requestKey. Giới hạn 10 ảnh/lần, 20 MB/ảnh. Không làm mất ghi chú hoặc hàng đang nhập.

Ảnh sử dụng ClinicalDocument/storage/quét an toàn hiện có, chỉ lưu một nguồn. Gallery của phiếu lọc dossierId; tab Hình ảnh của hồ sơ hiển thị cùng nguồn cho bệnh nhân. Chỉ Ready được đọc nội dung qua API có xác thực; PendingScan và Rejected hiển thị trạng thái, không mở nội dung. Blob URL được thu hồi khi đổi ảnh hoặc unmount. Gallery phân trang và làm mới khi tải thành công.

## API và dữ liệu
Giữ POST /api/clinical-documents multipart với metadata gồm patientId, dossierId, visitId:null, documentType:Other, source:Clinic, sourceDate (ngày VN), requestKey. VisitId nullable chỉ hợp lệ khi có dossierId; kiểm Patient/chi nhánh theo dossier và quyền ClinicalAccess trước replay. Nếu có Visit giữ kiểm trạng thái và nguồn hiện có. Không tạo Visit, actual hay khoản tiền. GET list bổ sung imagesOnly để lọc JPEG/PNG trước phân trang, bao gồm tài liệu trực tiếp thuộc dossier được phép. Review kiểm cùng chi nhánh với nguồn dossier hoặc Visit.
Migration chỉ cho CLINICAL_DOCUMENT.VisitId nullable và check bắt buộc Visit hoặc dossier. Giữ tài liệu cũ, FK, audit/receipt và quét an toàn. Áp vào development cần duyệt riêng.

## Kiểm tra
Backend: upload trực tiếp không Visit, đúng phiếu/bệnh nhân/chi nhánh, replay, từ chối nguồn sai và nội dung chưa Ready; hồi quy upload theo Visit. Web: bấm đúng ô mở file picker, upload metadata đúng phiếu, không đổi route, gallery phiếu/tab Hình ảnh, retry không trùng, JPEG/PNG/size và trạng thái scan; build backend/Web.
