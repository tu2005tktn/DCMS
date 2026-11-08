# Đặc tả màn S38 / S39 / S40 — Hóa đơn, Thu tiền & Biên nhận, Báo cáo

- **Task:** đặc tả màn `S38`–`S40` (REQ-32 → REQ-40), bàn giao dạng tài liệu — **không** cam kết triển khai toàn bộ nghiệp vụ thanh toán (REQ-36).
- **Phiên bản tài liệu:** v2.0 — nguồn normative [DCMS_Screen_Specifications_v2.0.docx](DCMS_Screen_Specifications_v2.0.docx) (15/10/2026); quy tắc/luồng lấy từ [DCMS_Use_Cases_Business_Rules_Messages_v2.0.docx](DCMS_Use_Cases_Business_Rules_Messages_v2.0.docx).
- **Mã dùng:** mã màn `Sxx` và mã quy tắc `GBR-*`/`MSG*` thuộc bản v2.0. Không trộn mã của bản v1.0 (`BR-*`, `FN-*`, `AP-*`) và không trộn mã màn `Sxx` vào tài tả use case — đặc tả UC-23/24/25 nằm ở [UC23-24-25-SPEC.md](UC23-24-25-SPEC.md).
- **Không có trong tài liệu này:** mã nguồn backend/web cho ba màn (chưa có trong repo — xem mục 5).

---

## 1. S38 — Patient Bill (Hóa đơn bệnh nhân)

| Hạng mục | Nội dung |
| --- | --- |
| Mã / tên | `S38` — Patient Bill |
| Surface / phase | Staff web · Phase 1 |
| UC liên quan | `UC-19` Prepare and Complete Medical Bill |
| Actor & quyền | Clinic Assistant có quyền hóa đơn (grant `billing`); áp ma trận quyền + phạm vi chi nhánh. Nhân viên có thể lập hóa đơn cho bệnh nhân **không** có tài khoản app; việc bệnh nhân tự đọc đòi Verified self link (màn `S42`) |

**Bố cục:** Danh tính bệnh nhân + chi nhánh đang mở → bộ lọc/nguồn dữ liệu → các trường và dòng liên quan → tổng tiền/trạng thái suy ra → nút lưu/xác nhận/lịch sử tường minh.

### 1.1 Bảng trường (field, validation và hành vi)

| # | Trường / vùng | Validation và hành vi |
| --- | --- | --- |
| 1 | Bệnh nhân và hóa đơn | Bắt buộc có bệnh nhân gốc, hồ sơ (dossier) và chi nhánh đã chọn **trước khi** ghi; không tự tạo âm thanh hồ sơ/hóa đơn |
| 2 | Dòng dịch vụ | Nguồn `TreatmentActivity`, dịch vụ, số lượng, đơn giá và giá đã thỏa thuận; **không** thu tiền cho cả kế hoạch chưa thực hiện |
| 3 | Dòng thuốc | Nguồn từ chứng từ dùng/thuốc thực tế đã phát sinh, kèm mã sự kiện, số lượng và snapshot giá; không liên kết chỉ theo tên thuốc hoặc đơn |
| 4 | Giảm giá và điều chỉnh | Không âm theo quyền; lý do 1–500 ký tự; số dòng không thành âm trong thiết kế cơ sở |
| 5 | Thành tiền và tổng | Gross, một giảm giá, cơ sở chịu thuế, thuế theo cấu hình và net theo `GBR-106`; lấy tổng các dòng đã làm tròn; mọi màn và biên nhận dùng cùng công thức |
| 6 | Khoản phân bổ từ tiền cọc | Không âm, không vượt phần chưa dùng hoặc số tiền đến hạn; tham chiếu nguồn, **không** tính lại tiền cọc như tiền mặt mới |
| 7 | Đã thu và còn nợ | Chỉ đọc, sinh từ thanh toán/phân bổ hợp lệ; tổng tiền đã nhận **không** cho sửa tay |
| 8 | Trạng thái tài chính | `Unpaid` \| `Partially paid` \| `Paid` — `Paid` chỉ khi đã thu đủ; **trạng thái này không quyết định trạng thái Visit** (`GBR-070`) |
| 9 | Hạn thanh toán | Tùy chọn khi có thỏa thuận; phân biệt chưa đến hạn / đã đến hạn / quá hạn; không phải hóa đơn nào cũng có lịch trả góp |
| 10 | Lưu, in, thu tiền | Nút lưu theo quyền, in/xuất hóa đơn; điều hướng sang `S39` để thu tiền; **lưu hóa đơn không trừ tồn kho** |
| 11 | Hóa đơn và hồ sơ | Một hóa đơn thuộc đúng một dossier/chi nhánh; hiển thị version hóa đơn và mã `ChargeEvent` nền; tổng bệnh nhân liên nhiều dossier chỉ là tổng đọc, không tự chuyển nợ |
| 12 | Giảm giá và VAT | Một giảm giá mỗi dòng, không vượt gross, VAT mặc định 0%; net sau giảm giá rồi mới thuế, làm tròn lên whole VND; sửa nguồn đã ghi cần điều chỉnh được phê duyệt |

### 1.2 Thao tác
`Lưu` (theo quyền) · `In` / `Xuất` · `Thu tiền` (chuyển `S39`) · `Xem lịch sử version` · `Xem nguồn charge event`. Không có thao tác xóa/ghi đè chứng từ đã xác nhận.

### 1.3 Validation (client mirror, server vẫn quyết định)
Bắt buộc: bệnh nhân + dossier + chi nhánh; số lượng dòng > 0 cho nguồn đã thực hiện; giảm giá ≥ 0 và ≤ gross; lý do giảm giá 1–500 ký tự. Tổng tiền **luôn** server tính lại trước khi ghi.

### 1.4 Trạng thái quan sát được (REQ-41)
`loading` (đang tải hóa đơn/tổng) · `empty` (chưa có hóa đơn cho hồ sơ này — phân biệt với lỗi tải) · `validation error` (ngay trên trường) · `mất kết nối` (giữ dữ liệu, nút thử lại) · `hết phiên` (về đăng nhập, không mất nháp đang sửa) · `thiếu quyền` (`MSG06`, ẩn nút ghi) · `conflict` (`MSG111` — tải bản mới, giữ nhập liệu). Lỗi tải **không** hiển thị thành “đã thu 0”.

---

## 2. S39 — Payment and Receipt (Thu tiền & Biên nhận)

| Hạng mục | Nội dung |
| --- | --- |
| Mã / tên | `S39` — Payment and Receipt |
| Surface / phase | Staff web · Phase 1 |
| UC liên quan | `UC-20` Pay Medical Bill · `UC-21` Receive Cash · `UC-23` Correct Payment |
| Actor & quyền | Clinic Assistant có quyền thu tiền; **sửa lỗi thu** cần người phê duyệt tài chính độc lập (khác người nộp) |

**Bố cục:** Danh tính + chi nhánh → nguồn/hóa đơn → các trường và dòng phân bổ → tổng suy ra → `Xác nhận thu` / `Phê duyệt sửa` / `Xem lịch sử`.

### 2.1 Bảng trường (field, validation và hành vi)

| # | Trường / vùng | Validation và hành vi |
| --- | --- | --- |
| 1 | Hóa đơn hoặc tiền cọc | Mã hóa đơn khi thu theo hóa đơn; tiền cọc giữ bệnh nhân và mục đích nguồn; **đặt lịch không phải bằng chứng xem bệnh án** |
| 2 | Người trả | Bệnh nhân hoặc người thứ ba; họ tên + kênh liên hệ cho biên nhận; **trả hộ không mở quyền xem hồ sơ của người được trả** |
| 3 | Số tiền và phương thức | VND > 0; `Cash` \| `Transfer/QR` \| `Gateway` theo tích hợp; hai phương thức = hai khoản thu riêng |
| 4 | Tiền mặt thực nhận | Nút xác nhận tiền mặt thực nhận của lễ tân; ghi actor + thời gian; **không giả lập webhook tiền mặt** |
| 5 | Tham chiếu giao dịch | Mã yêu cầu, mã nhà cung cấp, số tiền, tiền tệ, trạng thái; **không** lưu dữ liệu xác thực thẻ; callback phải xác thực phía server |
| 6 | Phân bổ | Danh sách số tiền phân bổ theo hóa đơn; không vượt số tiền khả dụng hoặc số đến hạn; tiền cọc chưa dùng giữ riêng, không tạo khoản thu mới |
| 7 | Trạng thái bản ghi thanh toán | `Confirmed` hoặc `Awaiting reconciliation`; trạng thái nhà cung cấp hiển thị riêng theo giá trị trả về thật; **một callback `SUCCESS` không tự làm hóa đơn thành `Paid`** |
| 8 | Bản gốc và sửa lỗi | Chọn thanh toán đã xác nhận; nhập số tiền đúng và căn cứ cho phần chênh lệch; **không** ghi chuyển tiền ra cho bệnh nhân, **không** tạo refund |
| 9 | Lý do và phê duyệt | Lý do bắt buộc 1–1000 + bằng chứng + mã gốc; người phê duyệt độc lập khác người nộp; duyệt xong tạo bút toán đối ứng/thay thế → biên nhận mới |
| 10 | Biên nhận và lịch sử | Mã biên nhận, số tiền thật, phương thức, phân bổ, thời gian; xem/in/xuất; sửa lỗi giữ **cả** lịch sử gốc và số dư tính lại |
| 11 | Nộp tiền chuyển khoản (bệnh nhân) | Trạng thái `Submitted` / `Pending review`; staff chỉ xác nhận khi có bằng chứng ngân hàng; ảnh/redirect không làm thay đổi số đến hạn |
| 12 | Phê duyệt sửa lỗi | Gốc, đề xuất, lý do/bằng chứng, người phê duyệt độc lập, đối ứng/thay thế, biên nhận hiệu đính — **không bao giờ** sửa/xóa biên nhận đã xác nhận tại chỗ |
| 13 | Credit (tiền cọc) | Tiền cọc tạo credit theo bệnh nhân/chi nhánh; chỉ nhận số tiền thật > 0; phân bổ hóa đơn không vượt còn nợ; **phase 1 không có điều khiển hoàn tiền** |

### 2.2 Thao tác
`Xác nhận thu tiền` · `Xác nhận tiền mặt thực nhận` · `Nộp/ghi nhận chuyển khoản` · `Phê duyệt` / `Từ chối` sửa lỗi (actor phê duyệt) · `Xem` / `In` / `Xuất` biên nhận · `Xem lịch sử` · chuyển sang `S38`.

### 2.3 Validation
`amount > 0` (VND nguyên); phân bổ ≤ số khả dụng và ≤ còn nợ; lý do sửa 1–1000 ký tự + căn cứ bắt buộc; `requestKey` UUID cho mỗi lần ghi để chống ghi trùng; `expectedVersion` theo contract.

### 2.4 Trạng thái quan sát được (REQ-41, REQ-42)
Các trạng thái chung §1.4, thêm: `conflict` khi version lệch (`MSG111`); đề xuất chờ phê duyệt hiển thị chưa áp dụng; kết quả ghi thành công **luôn** đọc lại từ server (mã biên nhận/version/tổng) trước khi báo thành công.

### 2.5 Ranh giới tích hợp (REQ-35)
Chỉ mô hình **Payment Gateway** khi thực sự có tích hợp API/webhook trực tiếp với nhà cung cấp; callback xác thực phía server, không mô phỏng thành công khi chưa có tích hợp. Tiền mặt và đối soát ngân hàng bắt buộc chạy được mà **không** cần gateway.

---

## 3. S40 — Reports Dashboard and Viewer (Bảng điều khiển báo cáo)

| Hạng mục | Nội dung |
| --- | --- |
| Mã / tên | `S40` — Reports Dashboard and Viewer |
| Surface / phase | Staff web · Phase 1 |
| UC liên quan | `UC-32` View Clinic Reports · `UC-54` (báo cáo/xuất tài chính) |
| Actor & quyền | Admin hoặc Owner; người đọc/xuất báo cáo được cấp riêng. Quyền báo cáo **không** kèm quyền ghi bệnh án/kho/thanh toán |

### 3.1 Bảng trường

| # | Trường / vùng | Validation và hành vi |
| --- | --- | --- |
| 1 | Loại báo cáo | Vận hành · Tiền thực thu · Công nợ · Điều trị · Tái khám · Tồn kho thuốc; liệt kê **theo quyền** |
| 2 | Khoảng thời gian | Ngày bắt đầu không được sau ngày kết thúc; mặc định tháng hiện tại; múi giờ phòng khám; báo cáo tồn kho cần mốc cắt (cutoff) |
| 3 | Nha sĩ / dịch vụ / thuốc | Lọc theo loại báo cáo; chỉ hiện giá trị được quyền; **bộ lọc không mở rộng quyền đọc** |
| 4 | Thẻ chỉ số | Số lượt hẹn, số lượt khám, bệnh nhân mới, tiền thực thu hoặc công nợ tùy báo cáo — có đơn vị và định nghĩa rõ |
| 5 | Biểu đồ | Theo ngày/nha sĩ/phương thức/nhóm phù hợp; không trộn VND với số lượt hoặc số lượng thuốc trên cùng thang chưa giải thích |
| 6 | Bảng báo cáo | Chi tiết + tổng; hạn chế thông tin nhận diện theo phạm vi báo cáo; drilldown không được lộ dữ liệu ngoài quyền |
| 7 | Tồn kho | Đầu kỳ, nhập, phát/ghi nhận dùng, hoàn, điều chỉnh và cuối kỳ theo thuốc, lô, đơn vị; tách phân phối được và cách ly |
| 8 | Định nghĩa và cắt dữ liệu | Thời điểm cập nhật, công thức, dữ liệu còn thiếu; **không báo 0 khi báo cáo tải lỗi** |
| 9 | Xem / xuất | Nút chỉ đọc; xuất theo quyền và kỳ lọc; **không** có nút sửa Bệnh nhân/Thanh toán/Lô |
| 10 | Trạng thái không dữ liệu | Giải thích rõ kỳ rỗng hoặc thiếu quyền xem; **lỗi nguồn dữ liệu tách khỏi dữ liệu bằng 0** |
| 11 | Cơ sở báo cáo | Chọn theo ngày thực hiện / ngày thực thu hoặc công nợ / số dư tại mốc; hiển thị bộ lọc, chi nhánh, múi giờ, thời điểm sinh; **số tiền mặt thừa không được gọi là lợi nhuận kế toán** |

### 3.2 Thao tác
`Chọn loại báo cáo` · `Lọc` · `Xuất` (theo quyền) · `Xem chi tiết` — không có thao tác ghi/chỉnh sửa dữ liệu nghiệp vụ.

### 3.3 Validation
`from <= to`; lọc chỉ nhận giá trị thuộc phạm vi quyền; xuất giới hạn theo kỳ đã lọc.

### 3.4 Trạng thái quan sát được (REQ-41)
`loading` · `empty` (kỳ rỗng — có giải thích) · `error` (lỗi tải báo cáo — **không** hiển thị thành số 0) · `mất kết nối` · `hết phiên` · `thiếu quyền` (giải thích rõ thiếu quyền xem) · `conflict`.

---

## 4. Điều kiện chung cho cả ba màn (REQ-17, REQ-41 → REQ-43)

- **Phân quyền ở server từ session**: đổi filter/URL không mở rộng quyền; scope chi nhánh và grant đọc trên server quyết định dữ liệu trả về.
- **Trạng thái phân biệt** loading / empty / validation error / mất kết nối / hết phiên / thiếu quyền / conflict; lỗi tải không được hiện như “không có dữ liệu”.
- **Kết quả lưu và trạng thái lấy từ server**: dùng `allowedActions`, `requestKey`, `expectedVersion` ở các API theo contract; đọc lại sau ghi thành công.
- **API thật**: mock chỉ dùng khi phát triển; không báo “đã lưu/đã gửi” khi server chưa xác nhận.
- Số tiền là VND nguyên; công thức `GBR-106` tính lại phía server trước khi ghi.

---

## 5. Trạng thái triển khai và traceability (REQ-40)

| Màn | UC | Tài liệu nguồn (phiên bản) | Trạng thái trong repo |
| --- | --- | --- | --- |
| `S38` Patient Bill | `UC-19` | Screen Spec **v2.0** 15/10/2026 | **Chưa có code** — chưa có bảng/entity Bill, ChargeEvent |
| `S39` Payment and Receipt | `UC-20`, `UC-21`, `UC-23` | Screen Spec **v2.0** | **Chưa có code** — xem [UC23-24-25-SPEC.md](UC23-24-25-SPEC.md) đặc tả UC-23 |
| `S40` Reports | `UC-32`, `UC-54` | Screen Spec **v2.0** | **Chưa có code** |

- Các màn `S01`–`S37`, `S41`+ hiện có trong repo dùng mã màn của **cùng** bộ v2.0; đừng gán mã màn cũ từ bản v1.0 (05/10/2026) vào các màn mới.
- Nội dung liên quan thuốc (dòng thuốc ở `S38`, tồn kho ở `S40`) cần đối chiếu **phiên bản hiện hành** của quy tắc thuốc trước khi chuyển thành yêu cầu code (REQ-36).
