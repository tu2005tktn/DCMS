# Điều trị và Thanh toán v2.5

## Nguồn và quyết định

DCMS_Screen_Specifications_v2.5.docx và DCMS_Use_Cases_Business_Rules_Messages_v2.5.docx (26/10/2026): S21/S22/S38/S39/S43, UC-13/14/19/20/21/23/39, GBR-163–175, AT59–74. Ngày 27/10 người dùng chốt: S21/S22/S43 thật; Triển sở hữu ledger S38/S39; hoàn tất là đóng lâm sàng; không hiện dải chẩn đoán/checkbox VAT.

## Luồng và dữ liệu

Phiếu là dossier hiện có. Nhập dòng chỉ cập nhật form, không tự lưu. Nút ✓ Đồng ý lưu trực tiếp trên hàng, không có drawer xem lại hoặc xác nhận lần hai; quyết định và dữ liệu bắt buộc nhập ngay trong hàng. Giá trị mặc định Proposed không được coi là bệnh nhân đã đồng ý. Ghi chú cuối phiếu dùng nút Lưu ghi chú riêng. Lỗi giữ nội dung; retry giữ requestKey và nháp đã lưu. Nháp máy chủ cũ vẫn đọc/sửa được. Server chuyển nháp thành item ổn định và lưu snapshot phiên bản; thêm item không viết lại actual hoặc giá đã chốt. Sửa phạm vi cũ qua S62; ngày dự kiến/ghi chú có command versioned riêng. Direct treatment cần thỏa thuận tối thiểu và được tạo item nguồn trong cùng transaction. Đơn vị mặc định theo catalog; chi phí là quantity × đơn giá trước giảm trừ. Giảm trừ VND/Percent có lý do, một lần trên mỗi target, số tiền nguyên làm tròn half-up. Răng là danh sách FDI (11–18/21–28/31–38/41–48/51–55/61–65/71–75/81–85), vùng là chuỗi riêng; không ép nhiều răng thành một số.

Soạn actual chưa cần Visit; xác nhận cần Visit InProgress đúng Patient/branch/bác sĩ, agreement/version/readiness/quantity. Actual Posted giữ Visit/item/planVersion, người thực hiện và người ghi riêng. Partial không hoàn tất item; xác nhận Completed chỉ sau actual. Bảng actual đọc toàn dossier, phân trang/lọc Visit/ngày, không lấy chéo dossier. Không dùng giá actual như số đã thu.

Chưa có ledger: actual Separate post cùng outbox yêu cầu phí có stable source key, feeStatus Pending; Included/Free có căn cứ, không tạo yêu cầu phí thêm. Gói chưa có Q08 không tự sinh phí. Không dispatcher/consumer, không charge/payment/receipt tài chính trong task. Lỗi ghi outbox rollback actual. Consent xác thực item/decision/version thật, file S20 chỉ khi có tham chiếu và phải Ready, đúng chủ/nguồn. Không cần file cho mọi đồng ý.

## API và tương thích

Handoff S19 dùng `ClinicalHandoff.Version` hiện có (version của Visit tại kết quả khám đã xác nhận). Tạo dossier nhận SourceVisitId/ExpectedSourceVisitVersion; kiểm đúng Patient/branch và Confirmed, lưu nội dung nguồn bất biến trong TreatmentEvent. Không gọi đó là version mới của một model khám song song. S43 đọc snapshot cũ và có đường mở nguồn hiện hành.

Catalog bổ sung Unit mặc định `lần` cho dữ liệu cũ; API giá tham chiếu hiện có cho phép chọn đơn vị với cùng version/quyền catalog (lần/răng/hàm/gói/g/ml). Đổi catalog chỉ ảnh hưởng item tạo sau, không sửa snapshot đơn vị cũ.

Giữ API dossier, /sheet-drafts và /api/activities/{id}/post. Bổ sung GET /api/dossiers/{id}/sheet-context (catalog, nhân sự, Visit đủ điều kiện, allowedActions); GET /activities (filter/pagination); POST /sheet-drafts/{rowId}/confirm-plan; POST /sheet-drafts/{rowId}/confirm-actual; PATCH /plan/items/{itemId}/schedule-note; GET /api/patients/{id}/dossiers/summary. Các command nhận requestKey và expectedVersion của dossier/draft/Visit; chuyển nguồn và tiêu thụ nháp cùng transaction, replay cùng payload trả cùng IDs. Response đọc lại IDs/versions/status, không nhận display labels làm thẩm quyền. Note và assistant IDs có cấu trúc; source examination/version giữ khi truyền vào. Cột cũ nullable/default, DisplayCompleted còn trong DB nhưng UI dùng State; endpoint cờ cũ từ chối thay đổi độc lập. Không suy State từ cờ cũ.

Đóng dùng /close: CompletedAsAgreed/CompletedRevisedScope/StoppedIncomplete, disposition phần chưa làm, MSG243. Mở lại dùng /reopen có lý do. Xóa chỉ dossier không có nguồn nghiệp vụ. Dentist clinical; ClinicAssistant có grant tài chính chỉ đọc phần ledger cần thiết, không đọc/sửa nháp lâm sàng. Quyền hiện ở allowedActions và được server kiểm lại.

## Hợp đồng ledger và UI tài chính

Yêu cầu phí giữ activityId/version, patient/branch/dossier/item/planVersion, charging basis, quantity/unit, agreed unit price, approved discount snapshot, source key. Một source fee, không áp giảm hai lần. Summary có availability/sourceVersion/asOf, gross/discount/tax/net/confirmedAllocated/outstanding/unusedAdvance/pendingFeeCount. Paid = confirmedAllocated. Thiếu nguồn hiện unavailable, không 0. Tổng đầu danh sách theo toàn filter được cấp quyền, không phụ thuộc phiếu đang mở.

S39 drawer: ngày nhận thực tế, từng charge và phân bổ, tiền nhận = phân bổ + ứng được xác nhận, Cash/Transfer, payer, collector từ session, note; history/receipt view giữ các ID/version. Financial adapter mặc định unavailable. Mock chỉ route __preview và Playwright; không fallback mock khi API thật lỗi. Chỉ nối API Triển khi contract tương thích. Pending transfer không tăng paid; khoản đã post dùng correction độc lập, không edit/delete.

## Kiểm thử và giới hạn nghiệm thu

PostgreSQL schema cách ly: chuyển nháp, retry/khác payload/concurrency, quyền và Patient/branch, snapshot, nhiều Visit, outbox một lần, Partial/hold, close/reopen/delete; không tạo Visit/Appointment/charge/payment khi lưu kế hoạch. UI mock: nhiều răng, lưu trực tiếp một lần bấm, quyết định, actual/Visit, lỗi mạng/stale, tổng nhiều phiếu, finance unavailable và drawer mock. Build backend/Web. AT63–70 và thu nợ AT71 chưa nghiệm thu thật trước ledger. Migration dự án phải được duyệt riêng, không seed dữ liệu thử vào database dùng chung.


## Điều chỉnh thao tác theo mẫu oDental — 27/10/2026

Hàng nhập mở ngay trong bảng kế hoạch/quá trình. Nút xanh ✓ có tên “Đồng ý” kiểm tra và lưu trực tiếp; không mở drawer xem lại hoặc hỏi xác nhận thêm. Không tự lưu khi gõ. Quyết định lâm sàng và dữ liệu bắt buộc được nhập trên cùng hàng: lưu đề xuất không đồng nghĩa bệnh nhân đã đồng ý; chọn Agreed phải có tư vấn và người quyết định. Khi ghi actual, chọn Visit hợp lệ, bước khi áp dụng, kết quả và cơ sở tính phí ngay trên hàng. Không tự tạo bằng chứng đồng ý, Visit, kết quả hay căn cứ miễn phí. Các trường tùy chọn/điều kiện không thêm một bước xác nhận riêng. Quy trình quyết định riêng của item đã lưu vẫn có thể mở từ menu.

Một lần bấm gửi lưu nháp và command chuyển nguồn hiện có, có loading và khóa gửi lặp. Retry cùng payload giữ requestKey và version của nháp đã lưu; lỗi giữ hàng đang nhập và cho tải lại phiên bản nguồn mà không xóa form. Ghi chú cuối phiếu vẫn lưu bằng nút riêng. Chỉ hiển thị thành công sau phản hồi server. Không đổi hợp đồng API, backend hay database.


## Luồng nhập trực tiếp theo quyết định người dùng 27/10/2026

Thay luồng quyết định trên hàng phiếu bằng nguồn ClinicianEntry (bác sĩ ghi nhận, không phải consent). Hai bảng chỉ có Răng (nhiều mã FDI), bỏ Vùng và SL. Dòng mới quantity kỹ thuật = 1; dòng cũ giữ quantity và snapshot. Kế hoạch có estimatedMinutes nullable, số nguyên 1–480. Chi phí là gross cả dòng, không nhân số răng. Note chỉ text. ✓ lưu trực tiếp, không autosave hoặc drawer.

Menu kế hoạch gồm Thực hiện, Hoàn tất/Bỏ hoàn tất, Sửa, Xóa. Thực hiện tạo ClinicalActivity Posted nguồn Dossier, VisitId null, snapshot service/name/teeth/unit/quantity/price/discount/note/performer từ kế hoạch và gạch ngang nguồn trong cùng transaction. Retry/requestKey và liên kết source-plan-version chống trùng. Hoàn tất chỉ là DisplayCompleted của item, không đổi Progress/State/tiền. Sửa snapshot tương lai có history, không viết lại actual. Xóa hỏi một lần rồi soft-delete dòng, giữ mọi actual/tiền/nguồn, không chặn vì đã thực hiện hoặc thu phí. Tổng dự toán bao gồm dòng ClinicianEntry chưa xóa dù đã gạch ngang; không tính từ actual hay ledger.

API bổ sung PUT /api/dossiers/{id}/sheet-items/{itemId} (row JSON, expectedVersion, requestKey; cập nhật cùng item), POST .../perform, PATCH .../display-completion, DELETE ... (body expectedVersion/requestKey). Confirm-plan nhận decision nullable; null tạo ClinicianEntry, giữ Decision Proposed và không bịa tư vấn/đồng ý. Confirm-actual nhận VisitId nullable; với nguồn dossier dùng bác sĩ ghi nhận, không giả Visit/consent. Nguồn Visit cũ tiếp tục kiểm các điều kiện cũ. Direct actual không cần kế hoạch trước, tạo item nguồn ẩn khỏi bảng kế hoạch, không làm tăng dự toán. Giữ quyền Patient/branch/dentist và optimistic concurrency. Chỉ nguồn ClinicianEntry được dùng cổng ghi trực tiếp; việc chuyển nguồn cũ phải lưu dấu bác sĩ ghi nhận, không xóa quyết định cũ.

Bảng quá trình bỏ các filter Visit/từ/đến; giữ pagination. Thêm việc đã làm mở hàng trống, chọn catalog/nhân sự, răng/ngày/text/chi phí/giảm trừ nhập trực tiếp, không cần Visit. Không yêu cầu outcome không rỗng nếu note bỏ trống; không bịa kết quả. Labo là mô tả nếu nhập, không phát sinh yêu cầu Labo. Đã trả chỉ lấy ConfirmedAllocated từ ledger, chưa có nguồn hiện Chưa có dữ liệu. Actual giữ riêng nguồn service name và note để sửa kế hoạch không đổi hiển thị actual cũ. Không tạo charge/payment, chỉ outbox fee Pending hiện có cho Separate; giá 0 nguồn dossier ghi NotRequested, không bịa căn cứ miễn phí. Gói chưa có Q08 giữ trạng thái phí chưa xử lý thay vì chặn ghi lâm sàng.

Migration giữ dữ liệu cũ, thêm source/display/delete/snapshot, VisitId nullable cho actual; không fake Visit hoặc chuyển consent cũ. Test PostgreSQL cách ly: chuyển nguyên tử/idempotency, source rights, no Visit, snapshots, legacy quantity, completion flag, soft deletion không cascade, fee outbox rollback. UI mock: cột mới, single click, menu, strike-through, no filter/decision, error giữ form, paid unavailable, responsive. Database dự án và Git cần duyệt riêng.


### Ví dụ hợp đồng nhập trực tiếp

- PUT sheet-drafts giữ `{kind:"Plan"|"Actual", visitId:null, row:{serviceId,dentistId,assistantId?,regions:["25","26"],quantity:1,unitPriceVnd:300000,costVnd:300000,estimatedMinutes:25,note:"...",discountType:"VND",discountValue:0,labWork?},expectedVersion:0,requestKey}`; response có draft ID/version. Với Actual, estimatedMinutes không bắt buộc và labWork chỉ là mô tả.
- POST confirm-plan dùng `{expectedVersion,expectedDraftVersion,decision:null,requestKey}`. POST confirm-actual dùng cùng version/key, `visitId:null`, `expectedVisitVersion:0`, `directDecision:null`; server ghi nguồn Dossier. `decision:null` không đồng nghĩa Agreed.
- PUT sheet-items/{itemId}: `{row,expectedVersion,requestKey}`; sửa tại chỗ, giữ item ID/quantity cũ và snapshot actual. PATCH display-completion: `{completed:true|false,expectedVersion,requestKey}`. DELETE sheet-items/{itemId} nhận body `{expectedVersion,requestKey}`. POST perform dùng cùng body; response `{dossier,activity}` gồm item.DisplayCompleted=true, activity.VisitId=null, SourceKind=Dossier và ServiceNameSnapshot.
- Chỉ server quyết allowedActions, IDs nguồn, actor/time và feeStatus. Lỗi quyền 403, sai dossier/item 404, version/requestKey conflict MSG111/409; lỗi không xóa nháp. Transfer retry cùng key/payload trả receipt cũ; key khác với nguồn đã chuyển trả activity cũ, không thêm actual/phí lần hai.


## Sửa/xóa hàng quá trình và chuyển vào chế độ nhập — 27/10/2026

Bảng quá trình bỏ Xem nguồn, thay bằng hai nút Sửa/Xóa trên từng dòng. Sửa mở hàng nhập tại chỗ điền sẵn; ✓ lưu, × bỏ chỉnh sửa chưa lưu. Xóa hỏi Đồng ý/Không đồng ý, ẩn dòng sau server xác nhận và giữ lịch sử nguồn. Actual Posted và tài chính đã ghi nhận không bị ghi đè/xóa cứng: lưu bản điều chỉnh phiếu có version trong TreatmentEvent hiện có, projection bảng quá trình đọc bản điều chỉnh mới nhất. Không thêm bảng/migration cho điều chỉnh hiển thị. Các API clinical khác vẫn đọc nguồn actual gốc.

PUT /api/dossiers/{id}/sheet-activities/{activityId}: {row, expectedVersion (dossier), expectedActivityVersion, expectedSheetVersion, requestKey}. DELETE cùng đường dẫn: body gồm versions/requestKey. Mỗi command kiểm nguồn Patient/branch/dossier và Dentist được giao, lưu before/after, actor/time, chống lặp và concurrency; response {dossier,activity}. GET activities trả thêm sheetVersion, được lọc các dòng đã xóa mềm trước phân trang. Sửa giá/dịch vụ/giảm trừ không sửa charge/payment/receipt cũ; đánh dấu NeedsAdjustment và ghi outbox correction request với source key theo activity/correction version, chờ ledger, không tự thu/hoàn tiền. Sửa chỉ text không tạo yêu cầu phí. Snapshot nguồn kế hoạch không thay đổi.

Thực hiện từ kế hoạch mở hàng quá trình điền sẵn trong trạng thái sửa, có ✓/×, không mở drawer; quy tắc hủy chuyển và thời điểm commit sẽ ghi theo lựa chọn người dùng trong mục tiếp theo. Lỗi giữ form; không tự lưu khi gõ. Giữ Răng, không Vùng/SL/quyết định/Visit; Đã trả lấy ledger thật. Test mock và PostgreSQL cách ly cho sửa/xóa/version/idempotency, giữ actual/tiền nguồn, chuyển/sửa/hủy, reload và lỗi.


### Lựa chọn đã chốt: hủy chuyển bằng ×

Người dùng chọn: Thực hiện chỉ mở hàng Actual điền sẵn, chưa gửi write request hoặc gạch ngang kế hoạch. ✓ gọi POST sheet-items/{itemId}/perform với {row,expectedVersion,requestKey} trong một transaction, không cần lưu nháp trung gian; server ghi actual từ dữ liệu đã chỉnh, gắn nguồn kế hoạch và gạch ngang nguồn trong cùng transaction. × bỏ hàng đang chuyển, không tạo actual/draft/fee hoặc thay đổi kế hoạch. Ngày thực hiện mặc định ngày hiện tại theo giờ Việt Nam, không dùng ngày dự kiến như ngày đã làm. Sửa actual hiện có: × giữ nguyên dòng đã lưu, ✓ gửi API điều chỉnh. Giá/dịch vụ/răng/note actual được sửa độc lập, không cập nhật kế hoạch nguồn. Giữ một transfer gốc cho mỗi item; retry không nhân dòng.


## Bộ chọn vật liệu vào ghi chú quá trình — 27/10/2026

Chỉ hàng Actual đang thêm/sửa có icon nhỏ (tooltip/aria-label Chọn vật liệu dùng) cạnh ô ghi chú. Mở Modal Vật liệu dùng, mỗi lần mở bắt đầu một dòng quantity=1. Có thêm/xóa dòng, chọn danh mục dụng cụ/vật liệu qua GET /api/system-settings/materials?q=&page=&pageSize=20, tìm kiếm debounce250ms, tải thêm trang và giữ snapshot lựa chọn; hiển thị name (categoryName) để phân biệt tên trùng. Không API ghi danh mục, không API/migration mới, không kho/tiêu hao/đơn vị/giá.

Số lượng là số hữu hạn dương, tối đa3 chữ số thập phân; không tự làm tròn. Mỗi materialId chỉ có một dòng, thiếu/âm/0/thập phân quá3/chọn trùng bị chặn. Modal có loading/empty/error/retry, lựa chọn không mất khi lỗi. Hủy/đóng không đổi note. Lưu nối một đoạn plain text, ví dụ Vật liệu dùng: Composite A2 (Vật liệu trám) × 0,5; Gương khám (Dụng cụ khám) × 1. Giữ nguyên note cũ, ngăn bằng hai xuống dòng nếu note cũ có nội dung. Không parse ghi chú cũ; mỗi lần mở mới là danh sách trống. Tên/nhóm/quantity được chụp thành text, không tự đổi theo catalog về sau.

Đo note kết quả <=1000 ký tự trước khi áp dụng; vượt thì giữ nguyên note và toàn bộ lựa chọn trong Modal, không cắt chữ. Lưu Modal không gửi command máy chủ, chỉ set note vào form và dirty=true; ✓ hàng mới lưu theo pipeline hiện có, × hàng bỏ thay đổi và giữ quy tắc cảnh báo dirty. Chặn Lưu lặp/đóng do repeat để không append hai lần. Modal edit nội bộ không dùng nested HTML form trong form hàng. Test API mock kiểm Cancel, append/quantity/duplicates/overflow, remote search pagination+retry giữ selection, note persist qua row ✓ và reload, network/stale giữ dữ liệu, bàn phím/responsive390/768/1440. Web build và test hàng quá trình hồi quy; không Git hoặc DB mutation.
