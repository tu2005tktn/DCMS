# Chạy và kiểm thử Điều trị và Thanh toán v2.5

## Khởi động

Chạy PostgreSQL với compose hiện có. Build backend bằng .NET 10 và `npm run build` từ web. Migration mới `20261027120000_TreatmentPaymentV25` chỉ mở rộng các bảng hiện có: Unit catalog; note/bác sĩ/phụ tá của item; nguồn khám của dossier; người ghi/phụ tá/feeStatus/discount snapshot của actual. Giá trị mặc định của dữ liệu cũ không đánh dấu hoàn tất hoặc đã trả. Chỉ áp vào database development sau khi người dùng duyệt riêng. Dùng `infrastructure/scripts/start-api.ps1` và `npm run dev` để chạy API/Web sau migration.

## Test thủ công theo thứ tự

1. Đăng nhập Dentist có phân công chi nhánh. Mở bệnh nhân → Điều trị & Thanh toán → tạo phiếu. Có thể tạo nhiều phiếu; tổng đầu trang lấy toàn bộ phạm vi được cấp quyền.
2. Nhấn + ở Kế hoạch. Chọn dịch vụ catalog điều trị, nhập `25, 26`, SL/đơn giá, ghi chú, phụ tá khi có. Ngừng nhập không lưu máy chủ. Nút xanh ✓ có tên Đồng ý lưu trực tiếp trên hàng, không mở drawer hay hỏi lại.
3. Quyết định mặc định là Proposed (đề xuất). Nếu bệnh nhân đã đồng ý, chọn Đồng ý trong ô Quyết định của bệnh nhân và nhập tư vấn/người quyết định ngay trên hàng; các lựa chọn Hoãn/Từ chối cũng nhập tại đây. Nhấn ✓ một lần để lưu. Có thể chọn file S20 Ready đúng phiếu nếu áp dụng. Reload kiểm item ID, phiên bản, snapshot và quyết định. Ước lượng nâng cao có thể quản lý tại S21; không cần giờ giữ ghế khi thêm hàng trên phiếu.
4. Agreed → Thực hiện. Nhập răng trong phạm vi đã đồng ý, số lượng thực tế và việc đã làm. Chọn Visit InProgress, bước nếu có, cơ sở tính phí ngay trong ô nhập của hàng rồi nhấn ✓ Đồng ý để lưu trực tiếp. Direct service nhập thỏa thuận tối thiểu trên cùng hàng. Không có Visit thì giữ form và xử lý tiếp nhận trước. Lỗi/version conflict giữ dữ liệu; nút Đọc lại phiên bản không xóa nội dung đang nhập.

5. Partial không hoàn tất item. Chỉ xác nhận hoàn tất lâm sàng khi có việc đã làm đủ điều kiện. Các bước hold/dependency/Labo không đủ nguồn sẽ bị chặn. Mỗi actual giữ Visit; dùng bộ lọc để xem nhiều buổi của cùng phiếu.
6. Separate có giá >0 được Posted cùng yêu cầu phí outbox duy nhất, hiển thị Chưa ghi nhận phí; chưa có charge/payment. Free phải có căn cứ và thỏa thuận phù hợp; Included không lặp lại phí gói. Gói Separate vẫn chờ Q08.
7. Nhãn Hoàn tất mở form đóng hồ sơ. Thử khi còn Agreed chưa xong để thấy MSG243; chọn disposition/lý do khi ngừng phần còn lại. Mở lại cần lý do. Thu tiền và lâm sàng là hai trạng thái riêng.
8. Ghi chú cuối phiếu chỉ lưu khi bấm Lưu ghi chú; giữ người/thời điểm và lịch sử trước/sau. Link ảnh truyền dossierId sang S20. Handoff từ S19 giữ version Visit hiện có và snapshot nguồn đã xác nhận.
9. Ledger chưa nối: phí đã ghi nhận/đã phân bổ/còn nợ ghi chưa có dữ liệu đối soát. Trả tiền/Lịch sử mở drawer unavailable, không xác nhận giả. Form phân bổ, receipt và Pending transfer được kiểm qua Playwright mock cho đến khi Triển nối nguồn thật.

## Handoff tài chính cho Triển

Không gắn các route tiền vào group Dentist-only. Group tài chính kiểm grant riêng và branch. `GET /api/patients/{id}/dossiers/financial-context` hiện chỉ cho active ClinicAssistant có Identity claim `permission=finance.read` và phân công chi nhánh; trả projection tối thiểu, không trả clinical notes/counseling/nháp. Module quản lý quyền chịu trách nhiệm cấp grant; không seed quyền thu tiền cho mọi assistant hoặc Dentist.

UI chờ `GET /api/dossiers/{id}/finance` (available/sourceVersion/asOf/allowedActions/charges/receipts/unusedAdvanceVnd) và `POST /api/dossiers/{id}/collections` theo SheetContracts.cs/DossierFinancialPanel.tsx. `Collect` do server cấp theo grant, không suy từ vai trò Dentist. POST phải kiểm tiền nhận = allocations + acknowledgedAdvance, khóa debt/funds, receipt idempotency, Pending reconciliation và MSG256. Hai route ledger này chưa được cung cấp trong task.

Consumer phí đọc outbox kind `clinical.fee.requested`, stable key `clinical-fee:{activityId}`. Payload ClinicalFeeRequest giữ activity/version, Patient/branch/dossier/item/planVersion, quantity/unit, gross, giá và giảm trừ đã duyệt cùng actor/time/qualifying item. ApprovedDiscountVnd là phần giảm phân bổ cho nguồn này; không tính giảm trừ lần nữa. Ghi charge duy nhất theo sourceKey; cập nhật qua adapter summary có sourceVersion/asOf và line allocations. Task không cung cấp dispatcher/consumer.

## API ví dụ không chứa dữ liệu thật

Lưu hàng: `PUT /api/dossiers/{id}/sheet-drafts/{rowId}` với `{kind:"Plan",visitId:null,row:{serviceId:3,dentistId:7,regions:["25","26"],quantity:2,unitPriceVnd:300000,costVnd:600000,discountType:"VND",discountValue:100000,discountNote:"Giảm trừ theo ca"},expectedVersion:0,requestKey:"UUID"}`.

Ghi nhận: `POST .../sheet-drafts/{rowId}/confirm-plan` với `{expectedVersion:1,expectedDraftVersion:1,decision:{decision:"Agreed",counseling:"Phương án, lợi ích/rủi ro, giá",decisionMaker:"Người khám"},estimate:{minMinutes:20,selectedMinutes:25,maxMinutes:25,sessions:2},requestKey:"UUID"}`. Response TreatmentChange gồm dossier/plan/item IDs/version và notification. Same key/payload trả kết quả cũ; đổi payload hoặc stale trả 409.

Actual: `POST .../sheet-drafts/{rowId}/confirm-actual` nhận expectedVersion/expectedDraftVersion/visitId/expectedVisitVersion/outcome/chargingBasis/chargingBasisEvidence/completeItem/directDecision/requestKey và optional stepId/expectedStepVersion. Response gồm dossier và activity có feeStatus. Không nhập ID kỹ thuật trên UI.

## Kiểm tra tự động

Backend: `TreatmentSheetV25Tests`, `TreatmentModelTests` và hồi quy `TreatmentDossierApiTests`, `ClinicalWorkApiTests`, `TreatmentRevisionApiTests`; unit `TreatmentSheetRulesTests`. Chạy integration trên database PostgreSQL tạm riêng với connection string chỉ có trong môi trường tiến trình; fixture tạo schema synthetic, sau đó dọn toàn bộ database tạm. Không trỏ test vào dcms_dev.

Web: `patient-treatment-ui.spec.ts`, `treatment-dossier.spec.ts`, `treatment-revision.spec.ts`, `treatment-commercial.spec.ts`, `clinical-preview.spec.ts` dùng mock. Không chạy test bệnh nhân gọi API thật trên database đang dùng. Các ảnh responsive chỉ là bằng chứng giao diện; không thay thế nghiệm thu ledger AT63–70/thu nợ AT71.


## Kiểm thử luồng nhập trực tiếp mới

1. Áp migration `20261027180000_DirectTreatmentSheet` vào database development sau khi được người dùng duyệt riêng, rồi khởi động lại API. Không thử API mới trên schema cũ.
2. Mở Patient → Điều trị & Thanh toán → phiếu Open. Nhấn + kế hoạch, chọn dịch vụ, nhập răng `25, 26, 46`, 25 phút, note và chi phí. Không có Vùng/SL/quyết định/Visit; ✓ lưu ngay; chờ khi gõ không gửi lệnh.
3. Menu ⋯ → Thực hiện: một dòng xuất hiện ở quá trình ngay, nguồn gạch ngang, reload vẫn giữ. Bấm lặp không tạo bản thứ hai. Không có Visit giả.
4. Hoàn tất/Bỏ hoàn tất chỉ đổi dấu gạch ngang; không đổi lâm sàng/tiền. Sửa ngay hàng rồi ✓; actual cũ giữ snapshot. Xóa → Không đồng ý giữ nguyên, Đồng ý bỏ dòng khỏi kế hoạch nhưng giữ actual.
5. Thêm việc đã làm ở quá trình: chọn catalog, ngày/răng/text/chi phí và Labo mô tả nếu có; ✓ không cần Visit. Không tự tạo request Labo. Đã trả thiếu ledger hiện Chưa có dữ liệu, không nhập tay.
6. Kiểm lỗi số phút 0/481/25.5, răng sai FDI, mất mạng/MSG111: giữ form, Đọc lại phiên bản rồi thử lại. Bản ghi cũ quantity > 1 giữ nguyên gross khi mở/sửa.


## Cập nhật cuối: sửa/xóa quá trình, chuyển vào chế độ sửa

Phần này thay thế bước Thực hiện lưu ngay trong hướng dẫn phía trên. Không cần migration mới; lưu điều chỉnh bằng bảng event hiện có.

1. Menu kế hoạch → Thực hiện: hàng điền sẵn xuất hiện ở quá trình, có ✓/×. Chưa có write request hoặc dấu gạch ngang; sửa răng/note/ngày/chi phí theo việc thực tế. Ngày mặc định là hôm nay theo giờ Việt Nam.
2. ×: hủy hàng chuyển, kế hoạch không đổi, không có actual/nháp/phí mới. ✓: lưu toàn bộ hàng trong một transaction rồi gạch ngang nguồn; retry không thêm dòng thứ hai.
3. Bảng quá trình không còn Xem nguồn; từng dòng có nút Sửa (bút) và Xóa (×). Sửa mở inline, ✓ lưu, × bỏ chỉnh sửa chưa lưu. Reload giữ bản chỉnh sửa. Bản actual gốc và kế hoạch nguồn giữ trong lịch sử.
4. Xóa: Không đồng ý giữ nguyên, Đồng ý ẩn dòng khỏi bảng sau khi server lưu. Không xóa cứng actual, charge/payment/receipt. Nếu thay giá/dịch vụ/giảm trừ, phí hiện Chờ điều chỉnh phí và outbox gửi đề xuất cho ledger; không giả đã điều chỉnh tiền.
5. Với MSG111/lỗi mạng giữ form, Đọc lại phiên bản rồi thử lại; quyền và expected dossier/activity/sheet versions do server kiểm. Xóa và sửa dùng requestKey chống lặp.

## Bộ chọn vật liệu trong ghi chú — 27/10/2026

1. Mở phiếu → Thêm việc đã làm hoặc Sửa một dòng quá trình. Cạnh ô Ghi chú có icon dụng cụ với tooltip “Chọn vật liệu dùng”. Bảng kế hoạch không có icon này.
2. Bấm icon → chọn tên kèm nhóm, nhập số lượng dương tối đa 3 chữ số thập phân. Thêm vật liệu để chọn nhiều mục; cùng một mục không chọn hai lần. Có tìm kiếm, tải thêm và thử lại khi lỗi danh mục.
3. Hủy/đóng Modal giữ nguyên ghi chú. Lưu Modal chỉ nối đoạn “Vật liệu dùng: …” vào ghi chú đang nhập, chưa gửi command ghi phiếu. Nếu tổng vượt 1.000 ký tự, Modal giữ lựa chọn và báo lỗi.
4. Bấm ✓ của hàng mới lưu lên máy chủ; × bỏ nội dung chưa lưu theo cảnh báo hiện có. Tải lại sau ✓ vẫn giữ tên/nhóm/số lượng dạng chữ, dù danh mục được đổi tên sau đó.

Kiểm chứng đợt này: Web `npm run build` đạt (còn cảnh báo bundle size/SignalR hiện có). Bộ `patient-treatment-ui.spec.ts` có 14 ca: 13 ca đạt trong lượt toàn bộ, sau đó 2 ca bổ sung/cập nhật (bấm Lưu lặp, danh mục trống/lỗi tải đầu) đạt trong lượt riêng. API mock kiểm ghi chú cũ, số lượng lẻ/không hợp lệ, chống chọn trùng, giới hạn ký tự, Cancel/dirty/row ✓, reload, phân trang và retry giữ lựa chọn. Có ảnh Modal tại 390/768/1440 px. Không thêm API, migration hoặc dữ liệu thử vào database dự án; không chạy Git trong đợt này.
