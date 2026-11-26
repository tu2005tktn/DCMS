# Hiện trạng DCMS: thanh toán, kế hoạch điều trị, doanh thu (chỉ đọc repo, 08/11/2026)

Quy ước nguồn: `S/` = `docs/specs/`; `B/` = `backend/src/`. Số sau dấu `:` là số dòng trong file. "[SUY]" = suy luận của người ghi chú, không có trong repo.

## 0. Bản đồ tổng: ba "đường tiền" song song

### Takeaway
DCMS hiện có 3 cơ chế ghi tiền chạy song song, không hợp nhất: (A) ledger chung `FinanceCharge/PaymentReceipt/PaymentAllocation` (khám + CLS theo Visit, và charge dossier); (B) `DossierPayment/DossierPaymentLine` - form "TRẢ TIỀN" trong phiếu điều trị, gắn từng "việc đã làm" (ClinicalActivity Posted); (C) lệnh PayOS (`DossierPayOsOrder`) chỉ là bước trung gian để tạo (B) loại Transfer. "Doanh thu" = tiền ĐÃ THU (A Posted + B), không phải doanh thu theo dịch vụ thực hiện hay theo kế hoạch.

### Cited Findings
- Spec coi form TRẢ TIỀN của phiếu là nguồn riêng, thay "ledger DCMS-101"; module Finance theo lượt khám và `/api/dossiers/{id}/finance`, `/collections` "không đổi và không được dùng bởi form này" - S/TREATMENT-DOSSIER-PAYMENTS.md:5.
- WORK-SERVICE-STATISTICS tuyên bố mục "Nguồn dữ liệu" FinanceCharge/PaymentAllocation "không còn áp dụng", nay dùng DossierPayment/DossierPaymentLine - S/WORK-SERVICE-STATISTICS.md:62-67. Code khớp: B/DCMS.Infrastructure/Features/Treatment/WorkStatisticsService.cs:51-54, 69-72.
- Ledger chung (A): FinanceCharge, PaymentReceipt (State Pending/Posted/Cancelled; Method Cash/Transfer; Number; BankReference), PaymentAllocation(PaymentId, ChargeId, AmountVnd), FinancialAccount(PatientId, BranchId, Version - chỉ là bộ đếm version chống ghi đè, KHÔNG có số dư), BranchReceiptSequence, ProviderPayable, FinanceAudit - B/DCMS.Domain/Features/Finance/FinanceModels.cs:14-116.
- Charge từ điều trị chỉ sinh khi ClinicalActivity được xác nhận thực hiện, `ChargingBasis=Separate`, giá dương, đơn vị khác "gói"; phí = gross - discount snapshot - S/TREATMENT-LEDGER-INTEGRATION.md:5; code B/DCMS.Infrastructure/Features/Finance/ClinicalChargePort.cs:13-38.
- `ITreatmentFinancialSummaryPort`: hiện thực thật `TreatmentFinancialSummaryPort` đọc FinanceCharge + PaymentAllocation của receipt Posted; PlannedVnd, unusedAdvance, pendingFeeCount là hằng số 0 - B/DCMS.Infrastructure/Features/Finance/TreatmentFinancialSummaryPort.cs:10-31. Hiện thực mặc định cũ `UnavailableTreatmentFinancialSummaryPort` trả "unavailable". Spec: "Chưa có cọc/ứng trước hoặc refund trong ledger này" - S/TREATMENT-LEDGER-INTEGRATION.md:13.
- Mâu thuẫn tài liệu về "Đã trả": UI spec 03/11 nói Đã trả của phiếu = `financialSummary.allocatedVnd` (ledger A) - S/PATIENT-TREATMENT-PAYMENT-UI.md:29; nhưng form TRẢ TIỀN ghi vào DossierPayment (B) và cột "Đã trả" từng việc tính từ B (B/.../Treatment/TreatmentDossierPayments.cs:30-58). Spec v2.5 còn ghi "Đã trả chỉ lấy ConfirmedAllocated từ ledger, chưa có nguồn hiện Chưa có dữ liệu" - S/TREATMENT-PAYMENT-V25.md:53 (đã lỗi thời, V25.md:3 có chú thích tích hợp ledger thay thế).

### Inferences
- [SUY] (A) và (B) không chia sẻ dữ liệu; code không chặn việc thu trùng một việc qua `/collections` (ledger) và `/payments` (B), nên có nguy cơ cộng đôi trong màn Doanh thu.

### Gaps
- Chưa đọc frontend (`DossierFinancialPanel.tsx`, `CommercialTreatmentPage.tsx`) để biết web đang gọi route nào cho "Đã trả" header.

## 1. Entity, quy tắc thanh toán, phân bổ, giảm trừ, kế hoạch

### Takeaway
Thanh toán điều trị (B) CHỈ trả cho "việc đã làm" (activity Posted, chưa xóa khỏi phiếu). KHÔNG có cọc/ứng trước, không trả trước khi làm, không hoàn tiền, không hủy payment, không tín dụng chưa phân bổ. Có thu một phần và nhiều lần; phân bổ theo dòng (activity), UI tự chia FIFO.

### Cited Findings
Bảng/thực thể:
- `TREATMENT_DOSSIER_PAYMENT` (Id, DossierId, PatientId, BranchId, PaidOn date, Method, CollectorEmployeeId, Note, TotalVnd, CreatedBy, CreatedAt) và `TREATMENT_DOSSIER_PAYMENT_LINE` (PaymentId, ActivityId, AmountVnd>0; khóa 2 cột) - S/TREATMENT-DOSSIER-PAYMENTS.md:29-30; B/DCMS.Domain/Features/Treatment/DossierPayment.cs:3-24. Idempotency qua `TREATMENT_PROGRESSION_RECEIPT` Command="dossier-payment" (:31).
- `TREATMENT_PAYOS_ORDER` + `_LINE`: State Creating/Pending/Paid/Review/Expired/Cancelled, OrderCode, AmountVnd, ExpiresAt = now+30 phút, PaymentId - B/DCMS.Domain/Features/Treatment/DossierPayOsOrder.cs; B/.../Treatment/PayOsOrderService.cs:89, 134-141, 197-204; S/TREATMENT-TRANSFER-BILL.md:50.
- Không có bảng riêng cho "price snapshot": snapshot nằm trên `ClinicalActivity.PriceSnapshotVnd/DiscountSnapshotVnd/Quantity` và trên TreatmentItem (agreedUnitPriceVnd, referenceUnitPriceVnd, referencePriceVersion do server chụp) - S/S21-S43-TREATMENT-DOSSIER.md:14, 80.
- Kế hoạch dùng lại `TREATMENT_TEMPLATE (ItemPlan)` + `_ITEM` chỉ là mẫu, không tạo appointment/actual/charge/payment - S/TREATMENT-ITEM-PLANS.md:15, 26.
- `PaymentTerms` (milestone, amountVnd, dueOn, evidence) chỉ là thỏa thuận, "không tự ghi charge/payment khi đến hạn" - S/S21-S43-TREATMENT-DOSSIER.md:16, 82. `DepositException*` (Reason/By/At) chỉ ghi lý do không cọc - B/DCMS.Domain/Features/Treatment/TreatmentDossier.cs:32-34; B/.../TreatmentDossierService.cs:860-881.

Quy tắc form Trả tiền (B) - S/TREATMENT-DOSSIER-PAYMENTS.md:7-26; code B/.../TreatmentDossierPayments.cs:30-58, 92-125:
- Mỗi dòng: Chi phí = round(Quantity x PriceSnapshotVnd); Giảm giá = min(cost, DiscountSnapshotVnd); Còn nợ = max(0, cost - giảm - đã trả); `ChargingBasis=Included` -> nợ 0, không nhập được (code :52).
- Số tiền dòng nguyên VND, 0 < amount <= Còn nợ (vượt -> 409 MSG111); tổng > 0; tối đa 500 dòng; PaidOn không ở tương lai (UTC+7); người thu = nhân viên active của chi nhánh; ghi chú <= 1000.
- Payment bất biến, "Hoàn tiền/hủy là yêu cầu riêng" (S/...PAYMENTS.md:16). Không có endpoint hủy/hoàn DossierPayment: B/DCMS.Api/Features/Treatment/TreatmentEndpoints.cs chỉ có GET payments/context, GET payments, POST payments, POST/GET payos-orders (dòng 58-74).
- Phiếu Đã đóng vẫn thu được nợ còn lại (:17).
- Cash vào Đã trả ngay; code từ chối `Method != "Cash"` ở POST /payments (:94). Khi còn lệnh PayOS Creating/Pending/Review thì chặn thu tiền mặt (MSG111) (:107-109).
- Tự chia tổng tiền vào các việc (FIFO "cũ nhất trước") là hành vi UI; server nhận phân bổ tường minh theo activity - S/PATIENT-TREATMENT-PAYMENT-UI.md:37.
- Overpayment bị CHẶN, không tạo tín dụng: amount > Còn nợ -> 409. PayOS: nếu dư đổi trước khi tiền về, lệnh chuyển `Review`, "không ghi vượt dư", "cần quy trình xử lý riêng" - S/TREATMENT-TRANSFER-BILL.md:36-37; code PayOsOrderService.cs:134-138, 182-190.
- Chuyển khoản pending: Pending không tính Đã trả/doanh thu; chỉ Paid khi PayOS xác nhận đủ tiền (webhook HMAC + poll + worker). Ngày thu transfer = thời điểm xác nhận (UTC+7), không phải thời điểm khách chuyển - TREATMENT-TRANSFER-BILL.md:33-37. Mỗi dossier một lệnh PayOS hoạt động (:29); hết hạn 30 phút.
- Sửa/xóa việc đã làm khi đã thu: sửa giá bị chặn nếu (Chi phí - Giảm giá) < đã trả (409) - B/.../TreatmentSheetActivityService.cs:67; xóa activity có bất kỳ khoản đã trả > 0 bị chặn hoàn toàn (:91). Sửa tiền đặt `FeeStatus=NeedsAdjustment` + outbox "correction request", "chờ ledger, không tự thu/hoàn tiền" - S/TREATMENT-PAYMENT-V25.md:71; code :64, 70, 77, 110-113; cần quyền `treatment.items.price`. V25.md:15 ghi "Không dispatcher/consumer".

Ledger chung (A) cho khám/CLS (theo Visit):
- Charge sinh khi THỰC HIỆN: CLS "Đã thực hiện" tạo FinanceCharge duy nhất (S/INVESTIGATION-BILLING.vi.md:16); khám: khi Complete Visit tạo ExaminationFee/charge một lần (S/EXAMINATION-BILLING.vi.md:10). Giá khác bảng cần `finance.quotes.manage` + lý do (:11). Giá 0 = "Miễn phí", không phiếu thu 0 (:15).
- Thu: Cash -> receipt Posted + allocations + số phiếu chi nhánh, nguyên tử; Transfer -> Pending, chỉ Posted khi người có `finance.payments.confirm` nhập mã giao dịch (unique theo chi nhánh); có hủy Pending kèm lý do; Posted không sửa/xóa; "không hoàn tiền/Card/POS" - INVESTIGATION-BILLING.vi.md:17. Allocation <= RemainingVnd (B/.../Finance/FinanceService.cs:216, 221).
- "chưa có deposit/unused-advance, hoàn/hủy collection Posted, bank webhook hay tax invoice" - INVESTIGATION-BILLING.vi.md:30. Hủy lâm sàng sau thực hiện không tự đảo charge/receipt (:18).
- Collection dossier qua ledger (A): chỉ phân bổ vào charge trực tiếp của dossier, `acknowledgedAdvanceVnd=0`, ngày nhận cách server <= 5 phút, "không giả ứng trước" - S/TREATMENT-LEDGER-INTEGRATION.md:15; code FinanceService.cs:186.

Giảm trừ:
- Ở kế hoạch (commercial): `discount{type Fixed|Percent, value, reason, qualifyingItemId?, status Proposed|Approved|Rejected, actor/time, fingerprint}`; mỗi target item một giảm trừ hiệu lực, net không âm; chỉ Approved được trừ trong `plan/preview`; Dentist (chi nhánh được phân công), Admin, ClinicOwner đề xuất và tự duyệt, có audit - S/S21-S43-TREATMENT-DOSSIER.md:80. Đổi cơ sở tính xóa hiệu lực duyệt. Ví dụ 600000+1000000-300000=1300000.
- Ở phiếu thực tế (sheet): `discountType VND|Percent` + `discountValue`, lý do bắt buộc khi >0, làm tròn half-up (S/TREATMENT-PAYMENT-V25.md:11; code TreatmentSheetActivityService.cs:59-65). Không có bước duyệt riêng cho dòng sheet. Màn "Dịch vụ" (06/11) đã gỡ UI duyệt giảm trừ/ngoại lệ cọc, backend giữ nguyên - S/S21-S43...:90.
- Không có giảm giá trả đủ một lần/voucher/khuyến mãi. VAT: "không hiển thị ô VAT khi chưa có chính sách thuế" - S/PATIENT-TREATMENT-PAYMENT-UI.md:47.

Kế hoạch điều trị:
- Dossier -> plan (version), item (decision Proposed/Agreed/Refused/Deferred, giá thỏa thuận snapshot, quantity, răng FDI). Chốt `plan/commit`; sửa phạm vi sau chốt qua S62 - S/S21-S43...:15, 74. Lưu/chốt kế hoạch không tạo Visit/Appointment/charge/payment - V25.md:35.
- Luồng v2.5 nhập trực tiếp (27/10): nguồn `ClinicianEntry` (không phải consent, Decision giữ Proposed); "Thực hiện" tạo ClinicalActivity Posted nguồn Dossier không cần Visit, gạch ngang dòng kế hoạch; "Hoàn tất" chỉ là cờ hiển thị (DisplayCompleted); "Xóa" soft-delete dòng, không chặn dù đã thực hiện/thu phí, giữ actual/tiền - V25.md:47-50. Direct actual không cần kế hoạch, không tăng dự toán (:51).
- Tổng dự toán = tổng dòng ClinicianEntry chưa xóa, kể cả đã gạch ngang; không tính từ actual hay ledger (V25.md:49).
- Item bị hủy/xóa sau khi thanh toán: tiền gắn với ACTIVITY, không gắn kế hoạch, nên xóa dòng kế hoạch không động đến tiền; xóa activity đã thu bị chặn (xem trên).
- Đóng phiếu (`/close` CompletedAsAgreed/CompletedRevisedScope/StoppedIncomplete) cần disposition cho item agreed chưa xong (MSG243); "StoppedIncomplete không xóa actual/debt" - S/S21-S43...:13, 86. Không có logic hoàn/cấn trừ tiền khi dừng.
- Còn nợ phiếu = Σ Còn nợ của việc ĐÃ LÀM (không phải Thành tiền kế hoạch - Đã trả); việc chỉ trong kế hoạch "chưa phát sinh nợ" - S/PATIENT-TREATMENT-PAYMENT-UI.md:30.

### Inferences
- [SUY] Mô hình là "thu theo công việc đã làm" (post-service). Mọi hình thức trả trước (cọc %, trả cả gói, trả góp theo kỳ không gắn việc) không có chỗ lưu vì payment luôn cần ActivityId của việc Posted và số tiền <= nợ hiện tại.
- [SUY] Server không tự phân bổ; auto-allocate chỉ là tiện ích client.
- [SUY] Vì không xóa được activity đã thu và không sửa giá thấp hơn đã trả, hiện không có lối sửa sai/hoàn tiền; cần luồng correction/refund chưa được xây.

### Gaps
- Không tìm thấy trong repo: cọc/ứng trước lưu được, tín dụng chưa phân bổ, hoàn tiền, hủy/đảo payment Posted, lịch trả góp theo dõi quá hạn, giảm giá thanh toán trọn gói, voucher.
- Quy trình xử lý PayOS `Review` chưa có.
- Chưa đọc code `/api/commercial/...` để xác minh ai duyệt giảm trừ; chỉ dựa spec.

## 2. Doanh thu được tính thế nào

### Takeaway
"Doanh thu" = TIỀN ĐÃ THU (cash-basis), theo ngày thực nhận giờ Việt Nam và chi nhánh; không phải dồn tích/theo dịch vụ thực hiện/theo kế hoạch/lợi nhuận. Màn Doanh thu không có lọc bác sĩ/dịch vụ. Màn Thống kê công việc/dịch vụ là màn khác: Chi phí (việc đã làm) / Đã trả / Còn lại, lọc theo ngày TẠO PHIẾU.

### Cited Findings
Màn Doanh thu (`GET /api/finance/revenue`) - S/REVENUE-DASHBOARD.vi.md:5-17; code B/DCMS.Infrastructure/Features/Finance/RevenueService.cs:
- Quyền `finance.revenue.read`; chi nhánh ngoài scope -> 403 (:20-21). Mặc định tháng hiện tại; tối đa 366 ngày (:25); from/to gồm hai đầu UTC+7 (:26-27).
- Nguồn 1: `PaymentReceipt` State=Posted, ngày = `ConfirmedAt` (:30-33). Nguồn 2: `DossierPayment` (cash + transfer PayOS đã xác nhận), ngày = `CreatedAt` - KHÔNG phải `PaidOn` (:34-36; spec :6). Tổng received = cash + transfer (:55-56, 61).
- Pending (receipt Transfer Pending + PayOS order Pending) hiển thị riêng `pendingVnd`; PayOS Review không cộng (spec :7; code :48-54).
- Bộ lọc: from/to, branchId, phân trang. Không lọc bác sĩ, dịch vụ, bệnh nhân, phương thức. Bảng chi tiết là danh sách chứng từ `RevenueItem{source Finance|Treatment, patient, branch, receivedAt, method, amountVnd}` (:42-46); biểu đồ theo ngày cash/transfer (:57-60); tổng tính trên toàn tập lọc, items cắt trang trong bộ nhớ (:62).
- Spec: "tiền đã thu, không phải doanh thu dồn tích/kế toán, lợi nhuận hoặc giá vốn. Vật liệu trên bill không tạo doanh thu riêng" (:8). Không có refund nên không có dòng âm; receipt Cancelled không tính.

Màn Thống kê công việc/dịch vụ (`GET /api/work-statistics`, quyền `finance.work-statistics.read`) - S/WORK-SERVICE-STATISTICS.md; code B/.../Treatment/WorkStatisticsService.cs:
- Đơn vị: ClinicalActivity Posted thuộc dossier, áp bản sửa/xóa trên phiếu (SheetActivityEdited/Deleted) (:31-42); gom theo phiếu; phân trang theo phiếu (pageSize 20/50/100).
- Ngày lọc = ngày TẠO PHIẾU `StartedOn` (:27-28; spec :31), không phải ngày thực hiện hay ngày thu. Lọc: from/to, branchId, q (MSKH/tên KH/tên dịch vụ). Không lọc bác sĩ; chỉ hiện bác sĩ phiếu + phụ tá (:77). Không xuất Excel, không gom theo bác sĩ/dịch vụ (spec :47).
- Công thức dòng: Chi phí = Quantity x PriceSnapshot - min(gross, DiscountSnapshot), `Included` = 0; Đã trả = Σ `DossierPaymentLine` (không lọc theo ngày thu); Còn lại = max(0, cost - paid) (code :69-72; spec :63-67). `totals`, `lineCount` trên toàn tập lọc.
- Ngày thực hiện: ClinicalActivity có `PerformedAt` (mặc định hôm nay UTC+7 khi "Thực hiện" từ kế hoạch, sửa được; TreatmentSheetActivityService.cs:73; V25.md:77), chỉ dùng cho list activity (B/.../TreatmentSheetService.cs:351-352); không có báo cáo doanh thu nào theo `PerformedAt`.

### Inferences
- [SUY] Không có doanh thu hoãn lại: tiền cho việc chưa làm không nhập được nên không cần ghi nhận hoãn, nhưng cũng không dùng được cho trả trước.
- [SUY] Doanh thu theo bác sĩ/dịch vụ chưa trích được từ API; có thể dựng từ `DossierPaymentLine -> ActivityId -> PerformerId/ServiceId` nhưng chưa hiện thực.
- [SUY] Dùng `CreatedAt` thay `PaidOn` làm ngày doanh thu có thể lệch kỳ khi nhập ngày thu quá khứ (PaidOn cho phép <= hôm nay).
- [SUY] Revenue (theo ngày thu) và WorkStatistics (theo ngày tạo phiếu) không đối chiếu được trực tiếp theo cùng kỳ.

### Gaps
- Không thấy code/spec hoa hồng bác sĩ (không có entity/endpoint `commission`).
- Chưa đọc `RevenuePage.tsx`/`WorkStatisticsPage.tsx`.

## 3. Khoảng trống / mơ hồ / hạn chế so với quy trình thực tế

### Takeaway
Mô hình phục vụ tốt "làm xong - thu tiền phần đã làm", nhưng thiếu công cụ cho điều trị dài hạn trả trước (niềng, implant): không cọc, không trả theo gói, không trả góp có lịch, không hoàn tiền, không số dư chưa phân bổ, không doanh thu hoãn lại, không hoa hồng.

### Cited Findings
1. Cọc/prepayment %: không lưu được. `acknowledgedAdvanceVnd` bắt buộc 0 (S/TREATMENT-LEDGER-INTEGRATION.md:15; B/.../Finance/FinanceService.cs:186); `unusedAdvance` luôn 0 (TreatmentFinancialSummaryPort.cs:28). PaymentTerms chỉ là ghi chú (S/S21-S43...:16, 82); deposit-exception chỉ ghi lý do bỏ qua cọc.
2. Trả cả kế hoạch một lần/trả trước: không thể; POST /payments yêu cầu activity Posted và amount <= Còn nợ (TreatmentDossierPayments.cs:111-116); việc chưa làm không có nợ (PATIENT-TREATMENT-PAYMENT-UI.md:30).
3. Trả góp: không có lịch kỳ, nhắc nợ, quá hạn; chỉ trả từng phần cho việc đã làm.
4. Chiết khấu trả đủ/khuyến mãi/voucher: không có.
5. Hai cơ chế giảm giá không đồng bộ: kế hoạch có Proposed/Approved nhưng UI duyệt đã gỡ (S/S21-S43...:90); sheet giảm giá tự nhập, không duyệt (TreatmentSheetActivityService.cs:65, 77) [SUY về hệ quả].
6. Hoàn tiền/hủy payment/đảo charge: không có (TREATMENT-DOSSIER-PAYMENTS.md:16; INVESTIGATION-BILLING.vi.md:17-18, 30). Sửa giá thấp hơn đã trả và xóa việc đã thu bị chặn 409 (TreatmentSheetActivityService.cs:67, 91) nên bệnh nhân dừng điều trị (StoppedIncomplete) không có lối hoàn tiền.
7. Quá trả/tiền chưa phân bổ: bị từ chối thay vì giữ tín dụng; PayOS `Review` chưa có quy trình (TREATMENT-TRANSFER-BILL.md:36-37).
8. Hai nguồn tiền không hợp nhất, tài liệu mâu thuẫn về "Đã trả" (PATIENT-TREATMENT-PAYMENT-UI.md:29 vs TREATMENT-DOSSIER-PAYMENTS.md:5-18 vs TREATMENT-PAYMENT-V25.md:53); rủi ro trùng/lệch.
9. Ngày doanh thu dùng `CreatedAt`/`ConfirmedAt`, không `PaidOn`; ledger không cho ngày nhận quá khứ (>5 phút lệch) (RevenueService.cs:34-36; TREATMENT-LEDGER-INTEGRATION.md:15).
10. Doanh thu chỉ cash-basis, không tách bác sĩ/dịch vụ; không Excel; WorkStatistics lọc theo ngày tạo phiếu (WORK-SERVICE-STATISTICS.md:31, 47).
11. Hoa hồng bác sĩ/phụ tá: không có model; chỉ có `PerformerId`, `AssistantId` trên activity làm dữ liệu thô [SUY].
12. Công nợ kế toán/doanh thu hoãn lại: spec khẳng định "không phải outstandingVnd của ledger nên không gọi là công nợ kế toán" (PATIENT-TREATMENT-PAYMENT-UI.md:30); `FinancialAccount` không lưu số dư.
13. Gói (`Unit="gói"`/`Included`): không sinh charge tự động ("gói chưa có Q08 giữ trạng thái phí chưa xử lý", LEDGER-INTEGRATION.md:5; V25.md:53); `Included` có Còn nợ 0 và không nhập được (PAYMENTS.md:11) nên không có cách phân bổ tiền gói vào các buổi.
14. Giá 0 nguồn dossier ghi `NotRequested`, không bịa căn cứ miễn phí (V25.md:53).
15. Thuế VAT, hóa đơn điện tử: chưa có (INVESTIGATION-BILLING.vi.md:30; PATIENT-TREATMENT-PAYMENT-UI.md:47).
16. Sửa giá việc đã làm không sửa charge/payment cũ, chỉ `NeedsAdjustment` + outbox, chưa có consumer (V25.md:15, 71).
17. Quyền: tạo payment dossier đòi policy `TreatmentDentist` (nha sĩ của chi nhánh) cộng quyền tài chính (PAYMENTS.md:21) - thu ngân/lễ tân có thể không dùng được form này [SUY, cần kiểm vai trò thực].
18. Quy mô: WorkStatistics gom trong bộ nhớ (WORK-SERVICE-STATISTICS.md:47); Revenue tải toàn bộ chứng từ rồi cắt trang trong bộ nhớ (RevenueService.cs:30-47, 62).

### Inferences
- [SUY] Ca thực tế sẽ không khớp: (a) niềng đóng 30-50% trước; (b) implant cọc rồi trả theo giai đoạn; (c) trả đủ được giảm; (d) bỏ dở cần hoàn phần chưa làm; (e) chuyển khoản dư/thiếu; (f) hoa hồng theo thực thu hoặc theo việc làm.
- [SUY] Cần tối thiểu: thực thể khoản thu chưa phân bổ (deposit/advance) gắn bệnh nhân/phiếu; phân bổ sau; refund/void có audit; thống nhất ngày doanh thu (PaidOn vs CreatedAt); báo cáo theo bác sĩ/dịch vụ; một nguồn duy nhất cho "Đã trả".

### Gaps
- Chưa chạy hệ thống; chỉ đọc 9 spec yêu cầu + TREATMENT-LEDGER-INTEGRATION.md, TREATMENT-TRANSFER-BILL.md và code backend chính (DossierPayment, TreatmentDossierPayments, PayOsOrderService, RevenueService, WorkStatisticsService, TreatmentFinancialSummaryPort, ClinicalChargePort, một phần FinanceService, TreatmentSheetActivityService). Chưa đọc toàn bộ FinanceService, frontend, ScreenGrants.cs.
- Chưa mở: S/S62-TREATMENT-PLAN-REVISION.md (sửa kế hoạch đã chốt/giá), S/CLINICAL-PRESET-PRICES.vi.md, S/MATERIAL-PURCHASES.md.
