# Tích hợp phí điều trị vào ledger chung

> **Đã gỡ ngày 13/11/2026.** Phân hệ tài chính đã được xóa khỏi backend, web và database theo [CARE-LAB-FINANCE-STORAGE.vi.md](CARE-LAB-FINANCE-STORAGE.vi.md). Tài liệu này chỉ còn giá trị tham khảo khi đặc tả lại chức năng; không mô tả hành vi hiện tại.

## Nguồn phí

Chỉ `ClinicalActivity` đã được xác nhận thực hiện, `ChargingBasis=Separate`, giá dương và đơn vị khác `gói` mới ghi một `FinanceCharge`. Phí ròng bằng gross trừ discount snapshot đã được service lâm sàng kiểm tra. Kế hoạch, mẫu, bước, lịch hẹn, đơn thuốc và các giá tham khảo chỉ là dự toán; không tự tạo charge hoặc payment. `Free`, `Included`, giá 0 và gói chưa có quy tắc phân bổ giữ trạng thái lâm sàng hiện hành. Không hồi tố hoạt động cũ một cách ngầm định.

Hoạt động có Visit ghi charge theo Visit; hoạt động trực tiếp gắn charge với Dossier và không giả lập Visit. Nguồn `(ClinicalActivity, activityId)` là duy nhất. Việc post activity, charge, account version và audit dùng chung transaction/lock; lỗi ledger rollback lâm sàng. Actor ghi sổ lấy từ người xác nhận. Retry không nhân đôi phí. Snapshot giá/dịch vụ/giảm trừ đã ghi không bị sửa khi sửa kế hoạch hoặc catalog; yêu cầu điều chỉnh phí ở trạng thái `NeedsAdjustment` chờ quy trình correction riêng.

## Thu tiền và đọc tổng hợp

`GET /api/finance/dossiers/{dossierId}` trả charge phát sinh trực tiếp không có Visit của dossier, phân bổ confirmed, số dư, chuyển khoản Pending và account version. `POST /api/finance/dossiers/{dossierId}/payments` nhận cùng `CollectPayment` như thu theo Visit, chỉ phân bổ charge trực tiếp thuộc đúng dossier/patient/branch. Charge gắn Visit tiếp tục thu ở API Visit và vẫn hiện trong tổng hợp của dossier. Cash tạo receipt Posted; Transfer Pending và chỉ tăng đã trả khi xác nhận qua endpoint hiện có. Receipt có đúng một nguồn Visit hoặc Dossier; số phiếu, quyền, requestKey, account version và kiểm tra phân bổ dùng chung ledger. Các API Visit vẫn chỉ hiển thị/thu phí thuộc Visit.

`TreatmentFinancialSummary` đọc charge theo activity của dossier và `PaymentAllocation` của receipt Posted; `ReceivedVnd`/`AllocatedVnd` chỉ là tiền đã xác nhận phân bổ vào charge này, `OutstandingVnd` là phí đã ghi trừ khoản đó. Dự toán và khoản `Pending` hiển thị riêng. Nguồn thật không có charge trả số 0 với `Available=true`, không suy từ dự toán thành công nợ. Chưa có cọc/ứng trước hoặc refund trong ledger này.

Màn điều trị hiện gọi `GET /api/dossiers/{id}/finance` và `POST /api/dossiers/{id}/collections`: hai endpoint này là projection/adapter của API dossier ledger trên, cùng quyền và requestKey. Snapshot giữ `sourceVersion` bằng account version; charge bất biến có `version=1`; collection chỉ nhận phân bổ đúng phí trực tiếp, tổng phân bổ bằng `receivedVnd`, `acknowledgedAdvanceVnd=0`. Ngày nhận client phải cách thời điểm server không quá 5 phút; chứng từ dùng thời điểm server, không giả lưu ngày nhận quá khứ. Chuyển khoản trả Pending, chưa là Confirmed hay tăng đã trả; xác nhận/hủy dùng API `/api/finance/payments/{id}` hiện có. Không giả ứng trước khi nguồn chưa hỗ trợ.

Màn lịch sử của dossier cho người có quyền xác nhận/hủy một khoản Transfer Pending; hai thao tác gọi endpoint payment hiện có với account version mới nhất, mã ngân hàng/nội dung đối soát hoặc lý do hủy. Sau thành công tải lại snapshot và summary. Màn không cho in chứng từ chính thức của khoản Pending.
Form thu tiền gửi thời điểm hiện tại của máy khách để kiểm tra độ lệch, không cho nhập ngày quá khứ hoặc khoản ứng trước mà ledger chưa lưu được.

Migration chỉ thêm `DossierId` nullable và làm `VisitId` nullable trên charge/receipt, với check đúng một nguồn; dữ liệu Visit cũ giữ nguyên. Test kiểm tra direct/Visit post, giá ròng, retry, rollback, scope, thu một phần Cash/Transfer, và summary.
