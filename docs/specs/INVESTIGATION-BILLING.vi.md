# Cận lâm sàng → ghi phí → thanh toán

Chốt theo yêu cầu người dùng 27/10/2026. Đối chiếu UC-20/21 v2.5: chỉ Cash/Transfer, Pending chưa tăng Paid, collection/allocations/receipt commit nguyên tử, lịch sử bất biến. Đây là phần triển khai mới; không suy ra toàn bộ tài chính/điều trị/kho đã hoàn chỉnh.

## Nguồn dữ liệu và phạm vi

Dùng Patient/Visit/Branch, SchedulingService và ReferenceUnitPriceVnd/PriceVersion hiện có. Bổ sung metadata IsInvestigationService/InvestigationKind/BillingUnit trên service hiện tại, không sinh service identity thứ hai. Catalog CLS mới không PublicBookable và không IsInitialExamination; không đổi canonical Khám. Trang /clinical-services cho nhân viên có services.read/manage tạo/sửa dịch vụ đúng chi nhánh, đơn vị và giá tham khảo NULL khác 0; giá là VND nguyên không âm. Không seed giá lâm sàng vào dữ liệu đang dùng. Quản lý provider dùng một Provider catalog chung mới (nguồn hiện chưa có Provider), Kind ClinicalFacility cho đơn vị xét nghiệm/chẩn đoán bên ngoài, không tạo Labo fabrication hoặc kho. Cost chỉ người có quyền xem/quản lý chi phí thấy.

Chưa có ledger/collection trong source đã đọc: tạo FinanceCharge/PaymentReceipt/PaymentAllocation/ProviderPayable, FinancialAccount(patient,branch) version và BranchReceiptSequence làm nguồn chung. Không đặt tổng tiền vào ExaminationContent hay tạo charge riêng trong bảng order. Unique source charge chống tính đôi; IClinicalChargePort có adapter nối cùng FinanceCharge cho actual Separate trong transaction hiện có. Consent/plan financial summary vẫn theo module sở hữu, không giả xác nhận đủ điều trị.

## Luồng

1. Tạo chỉ định không tự ghi phí. Mỗi chỉ định có nút Phí dịch vụ để chọn cách thực hiện và xác nhận báo giá.
2. Bốn BillingMode: Clinic (phòng khám thực hiện/thu), OutsourcedClinic (phòng khám thu và thuê ngoài), ExternalSelfPay (bệnh nhân trả bên ngoài), ExistingResult (phim/kết quả mang sẵn). Hai mode sau không tạo khoản phải thu tại phòng khám; không tự tính thêm phí đọc phim.
3. Clinic/OutsourcedClinic chọn service cùng branch và đúng kind, quantity nguyên 1–100; lấy snapshot tên/đơn vị/giá/version. Giá tham khảo NULL không tự thành 0; cần giá thỏa thuận rõ. Override hoặc miễn phí cần lý do. Xác nhận báo giá là thao tác rõ ràng có actor/time/evidence, chưa phải đã thu. OutsourcedClinic cần provider active và đơn giá chi phí không âm; chỉ người có finance.costs.manage được xác nhận phần thuê ngoài, snapshot provider/cost giữ bất biến sau thực hiện.
4. Bác sĩ phụ trách hoặc trợ lý được cấp investigations.perform xác nhận Đã thực hiện khi Visit đang InProgress, order không Cancelled và đã có báo giá Clinic/OutsourcedClinic. Trong một transaction: đánh dấu performed actor/time, tăng order/Visit versions, tạo một FinanceCharge duy nhất theo order, tăng FinancialAccount version; thuê ngoài tạo thêm một ProviderPayable duy nhất theo charge. Không chờ bác sĩ đọc kết quả để ghi phí. Quantity, price, source và totals server kiểm lại. Nhận/đọc kết quả vẫn là trạng thái lâm sàng riêng.
5. Thanh toán đọc FinanceCharge; thu ngân chọn dòng và số tiền phân bổ từng dòng, người nộp, phương thức. Cash xác nhận tiền thực nhận tạo receipt Posted + allocations + số phiếu branch nguyên tử. Transfer tạo Pending; Paid/Remaining chưa đổi, chưa cấp số phiếu chính thức. Người có finance.payments.confirm xác nhận đã nhận tiền với mã giao dịch và ghi chú đối soát, server kiểm số dư hiện tại và mã giao dịch không dùng lại; mới Post/allocate/cấp số. Có hủy Pending kèm lý do. Không cho sửa/xóa Posted, không hoàn tiền/Card/POS trong phạm vi này.
6. Hủy chỉ định trước thực hiện không tạo phí. Hủy lâm sàng sau thực hiện không tự đảo charge, receipt hoặc payable; UI giải thích điều này. Chưa có quy trình correction/refund thì không cung cấp nút giả. Tiền bệnh nhân trả không tự thanh toán công nợ provider.

Phiếu cũ chưa xác định BillingMode giữ nguyên, không tự hồi tố phí. Đã báo giá billable nhưng chưa xác nhận thực hiện và chưa hủy thì chặn kết thúc Visit để nhân viên xử lý; không bắt thu đủ tiền để hoàn tất khám. Kết thúc khám không xóa công nợ, không khóa thu ngân thu khoản đã ghi. Luồng amendment CLS hiện có giữ nguyên, thao tác tài chính không ghi đè bản khám chưa lưu.

## Tiền, quyền và concurrency

Mỗi dòng và giao dịch tối đa 1.000.000.000.000 VND; aggregate tối đa 9.000.000.000.000 VND, tính checked/int64 và decimal, không float. Phân bổ >0, không trùng charge, cùng Patient/Visit/branch và không vượt số dư Posted. Transfer Pending không giữ chỗ tiền; nếu khoản nợ đã được trả trước khi đối soát, từ chối xác nhận vượt dư để xử lý rõ, không tự đổi số tiền hay chuyển bệnh nhân.

Quyền mới: finance.read (Dentist/Assistant/Owner), finance.quotes.manage (Dentist/Owner), investigations.perform (Dentist/Assistant/Owner, dentist thường phải là người phụ trách), finance.payments.create/confirm (Assistant/Owner), finance.costs.read/manage (Owner), services.read/manage (Dentist/Admin/Owner). Mỗi API dùng resolver hiện hành và branch memberships; Owner dùng scope operation rõ, không kế thừa Identity Admin/Assistant. Account có group phải được cấp mới rõ, không âm thầm mở grant cho nhóm tùy chỉnh. Giữ fallback legacy role cho account chưa gán group theo cơ chế hiện tại; không tự gán nhóm.

Write có requestKey/fingerprint, expected versions, audit và receipt commit cùng transaction, authorization trước replay. Tái dùng lock nghiệp vụ; quote còn khóa chung với service-price writer để chốt snapshot. Charge unique(sourceKind,sourceId), allocation unique(payment,charge), payable unique(charge), số phiếu branch unique. Confirm transfer cần reference unique tại branch, không tự tích hợp ngân hàng. Giá/provider thay đổi không sửa snapshot cũ.

GET không tạo FinancialAccount/sequence. DTO trả debt/paid/remaining từ ledger, chỉ trả cost/payable khi có quyền. Tệp/đơn thuốc/stock không làm source tiền. Thu tại quầy là phạm vi này; chưa có deposit/unused-advance, hoàn/hủy collection Posted, bank webhook hay tax invoice. API từ chối khoản không có source charge thực hiện; không giả đầy đủ UC-20 ngoài phạm vi.

## API/UI dự kiến

- GET /api/finance/capabilities, /api/finance/services?branchId, POST/PUT /api/finance/services[/id]; GET/POST /api/finance/providers.
- GET /api/visits/{visitId}/investigations/{id}/billing; POST .../quote, .../perform.
- GET /api/finance/queue?branchId; GET /api/finance/visits/{visitId}; POST .../payments; POST /api/finance/payments/{id}/confirm hoặc /cancel.
- /clinical-services: quản lý dịch vụ CLS và đơn vị ngoài khi được cấp cost.manage. /payments: hàng chờ thanh toán; /visits/{id}/payments: dòng phí/thu một phần, Pending/Posted, đối soát và lịch sử phiếu. Từ Khám có link Thanh toán, chỉ định có Phí dịch vụ/Xác nhận thực hiện. Bản nhập giữ khi lỗi/MSG111, không coi mọi 409 là stale; không nhập ID/actor/branch thủ công.

## Kiểm chứng

API/PostgreSQL riêng: bốn mode, quote snapshot/version/override/NULL/0, chưa làm/hủy không phí, perform double/replay/concurrent không đôi, payer/phân bổ/cash/transfer pending/confirm/duplicate ref, partial/overpayment, role/scope/cost privacy, provider payable riêng, actual-port cùng ledger. Browser thật: cấu hình dịch vụ → chỉ định → báo giá → thực hiện → cash/transfer → đối soát; đổi catalog không đổi charge; hồi quy khám/CLS/đính chính. Dữ liệu vận hành hiện có không seed lại; chỉ migration cộng thêm, test dùng schema riêng. Báo rõ scanner fixture và giới hạn tài chính còn lại.

Adapter hoạt động thực tế phải nhận actor đang xác nhận từ service gọi, lưu đúng người ghi sổ; không suy ra actor từ người thực hiện cũ. Không ghi sổ nếu Visit thực không tồn tại hoặc sai bệnh nhân/chi nhánh. Các fixture thay thế Visit chỉ kiểm tra lâm sàng, không giả lập ledger thật.

Màn thanh toán lượt có danh sách chỉ định ở mức thông tin dịch vụ (tên, nhóm, trạng thái, hình thức thu, xác nhận thực hiện), mở cùng drawer Dịch vụ và phí. Chủ phòng khám có thể cấu hình thuê ngoài và người được cấp quyền có thể xác nhận thực hiện từ đây, không cần được cấp quyền sửa bệnh án hay quyền lễ tân. Bill chỉ chiếu thông tin dịch vụ, không trả nội dung kết quả/nhận định/tệp lâm sàng.

Hàng chờ tài chính bao gồm cả lượt đang khám có chỉ định chưa phân loại thu hoặc đã lập phí nhưng chưa thực hiện. Hiển thị số chỉ định chờ xử lý riêng với tổng phí/công nợ; không biến giá dự kiến thành công nợ. Lượt chỉ có ExternalSelfPay/ExistingResult đã phân loại không tự vào hàng chờ thu nếu không còn khoản phí hoặc chuyển khoản chờ.

Màn tài chính dùng bố cục nội dung nghiệp vụ toàn bề rộng workspace, bảng ngang có thể cuộn trên màn nhỏ. Account version dùng nội bộ cho chống ghi đè, không hiển thị thành trường người dùng phải hiểu/nhập.


Phí khám được bổ sung vào cùng ledger theo [EXAMINATION-BILLING.vi.md](EXAMINATION-BILLING.vi.md); không thay đổi phí CLS hoặc phí điều trị pending outbox của Vinh.
