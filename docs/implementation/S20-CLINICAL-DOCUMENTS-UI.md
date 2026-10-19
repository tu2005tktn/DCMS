# S20 — Danh sách và tải lên tài liệu lâm sàng (UI web)

Task UI của Ngô Minh Trí (Epic DCMS-92). Backend lưu trữ private, scan, giới hạn và phân quyền thuộc **DCMS-95 (Tư)**; workspace S19 thuộc **DCMS-94 (Tư)**. Truy vết: S20, tab Images and Results của S19; UC-11/12; AT28, AT38.

> Trạng thái (19/10/2026): **chỉ có UI**. Backend DCMS-95 chưa có trên `main`; hợp đồng dưới đây là **[PROPOSED]** để Tư xác nhận và chỉnh, sau đó UI chỉ đổi trong `web/src/features/clinical-documents/documents.api.ts`. Đặc tả v2.2 (19/10/2026) chưa có trong repo; thông báo MSG cụ thể dùng theo server trả về.

## 1. Mở màn hình

- Từ S06 hồ sơ bệnh nhân → tab **Hình ảnh & Tệp** (danh sách nhúng, cùng component với S20).
- Trang riêng **`/patients/:id/documents`**, có thể kèm `?visitId=` / `?dossierId=` / `?planId=` / `?investigationId=` / `?consentVersion=` để mở đúng ngữ cảnh (S19 truyền vào). UI không tin ngữ cảnh trên URL: server từ chối nếu không thuộc Patient.
- Header chỉ đọc: mã bệnh nhân, họ tên, ngày sinh, lượt khám/hồ sơ/kế hoạch đang chọn. Chỉ chọn ngữ cảnh từ danh sách API trả về; không nhập lại thông tin hồ sơ.

## 2. Hành vi

- **Danh sách:** tên tệp, loại, ngữ cảnh, ngày nguồn, người tải, trạng thái tệp; lọc loại, ngày, ngữ cảnh; phân trang 20/50/100. Đang tải / rỗng / lỗi (mất kết nối, hết phiên, thiếu quyền) hiển thị riêng; lỗi tải không hiện như danh sách rỗng.
- **Trạng thái tệp:** `PendingScan` (đang kiểm tra), `Ready`, `Rejected` (kèm lý do). Chỉ `Ready` mở/tải được; PendingScan/Rejected không có nút xem.
- **Tải lên:** tối đa **10 tệp/lần**, **20 MB/tệp**, **JPEG/PNG/PDF**; loại tài liệu, nguồn, ngày nguồn, mô tả 0–1000 ký tự. Client kiểm tra sớm (tệp thứ 11, quá dung lượng, sai loại) và giữ lựa chọn; server quyết định cuối (chữ ký nội dung, scan).
- **Từng tệp độc lập:** mỗi tệp gửi một request với `requestKey` riêng, thanh tiến trình và kết quả riêng (`Đang tải` → `PendingScan`/`Ready`/`Rejected` hoặc lỗi server). Một tệp lỗi không làm các tệp khác hiện là lỗi; batch có tệp lỗi không hiện "tất cả thành công". Mất mạng: giữ form, nút **Thử lại** gửi lại đúng `requestKey` cũ nên không tạo bản trùng.
- **Theo dõi scan:** sau tải lên, UI đọc lại danh sách định kỳ (5 giây, tối đa 2 phút) cho tới khi tệp hết `PendingScan`; nút Tải lại luôn đọc document ID thật từ server.
- **Xem/tải:** chỉ qua action có trong `allowedActions` của tệp; nội dung lấy bằng request có xác thực thành blob và mở bằng URL tạm (`blob:`), thu hồi khi đóng; không lưu URL công khai. 403/404/hết phiên hiển thị thông báo, không lộ đường dẫn hay nội dung.
- **Chi tiết:** thông tin tệp, ngữ cảnh (kể cả nguồn chỉ định cận lâm sàng / phiên bản đồng thuận khi có), lịch sử chỉ đọc. Không có nút xóa, sửa chẩn đoán, công bố hay duyệt lâm sàng (bác sĩ duyệt ở flow S19).
- **Rời trang khi đang tải lên/chưa lưu:** hỏi xác nhận. Bộ lọc và ngữ cảnh đã chọn được giữ khi quay lại (theo URL).

## 3. Hợp đồng API đề xuất (DCMS-95)

| Method | Đường dẫn | Mô tả |
| --- | --- | --- |
| GET | `/api/patients/{patientId}/document-contexts` | `{patient:{id,patientCode,fullName,birthDate}, visits:[{id,label,date}], dossiers:[{id,label}], plans:[{id,label,dossierId}]}` |
| GET | `/api/clinical-documents?patientId&visitId?&dossierId?&planId?&type?&from?&to?&page&pageSize` | `{items:DocumentItem[], total, page, pageSize, constraints:{maxFiles,maxBytes,contentTypes[],descriptionMax}, allowedActions:["upload"]}` |
| POST | `/api/clinical-documents` (multipart) | phần `file` + phần `metadata` JSON `{patientId, visitId?, dossierId?, planId?, investigationId?, consentVersion?, documentType, source, sourceDate, description, requestKey}` → 201 `{document: DocumentItem}`; cùng `requestKey` → 200 kết quả cũ; lỗi `{code,message}` (400 loại/dung lượng/chữ ký, 403, 409 ngữ cảnh sai) |
| GET | `/api/clinical-documents/{documentId}` | `DocumentItem` + `description` |
| GET | `/api/clinical-documents/{documentId}/history` | `[{action, actorName, occurredAt, note}]` |
| GET | `/api/clinical-documents/{documentId}/content?disposition=inline\|attachment` | nội dung tệp (chỉ khi `Ready` và có quyền) |

`DocumentItem = {documentId, version, fileName, contentType, sizeBytes, documentType, source, sourceDate, description?, context:{visitId?, visitLabel?, dossierId?, dossierLabel?, planId?, planLabel?, investigationId?, investigationLabel?, consentVersion?}, uploaderName, uploadedAt, fileState: PendingScan|Ready|Rejected, rejectReason?, allowedActions:["view","download"]}`.

Loại tài liệu [PROPOSED]: `XRay`, `IntraoralPhoto`, `LabResult`, `ConsentForm`, `Referral`, `Other`. Nguồn: `Clinic`, `External`.

## 4. Chế độ xem trước giao diện

Chỉ để duyệt giao diện khi chưa có API: đặt `VITE_UI_PREVIEW=true` khi chạy web. Màn hình hiện banner **"Xem trước giao diện — dữ liệu minh họa, chưa kết nối API"**; dữ liệu nằm riêng trong `preview.ts` và không bao giờ được dùng khi biến này không bật. Mặc định (và khi bàn giao) UI chỉ gọi API thật.
