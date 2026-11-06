# Hướng dẫn test luồng phòng khám hiện tại

Phiên chạy ngày 26/10/2026, code main `63f8589` kèm sửa merge local. Đây là hướng dẫn nghiệm thu theo trạng thái implementation hiện có, không xác nhận đã hoàn chỉnh toàn bộ luồng phòng khám.

## Môi trường và tài khoản

- Web: <http://localhost:8084/sign-in>; API health: <http://localhost:5084/health>.
- Dữ liệu tổng hợp nằm trong PostgreSQL schema `dcms_reception_e2e_*` riêng; file private ngoài repo. Không dùng schema thường để tạo fixture.
- Lễ tân: `assistant.demo`; bác sĩ được phân công cho hai ca hướng dẫn: `dentist.demo`.
- Mật khẩu là giá trị `DEV_SEED_PASSWORD` trong `.env` local. Không ghi mật khẩu vào tài liệu, screenshot hoặc Git.
- Chi nhánh: **S09 - Chi nhánh thử nghiệm**; bác sĩ **Nha sĩ demo**; ngày tiếp nhận **26/10/2026**.
- Mã Patient, link tiếp nhận, lịch và hồ sơ của phiên chạy nằm trong `.tools/dcms-manual-flow-fixture.json`. Ca `newExamination` và `continuation` chưa được check-in để người dùng tự thao tác. Dữ liệu của ca `proof` đã dùng kiểm chứng và không dùng thay hai ca này.
- Các lịch của fixture chỉ dùng trong ngày; sau giờ kết thúc lịch phải xếp lịch hợp lệ mới. Nếu mở link bị chuyển về đăng nhập, đăng nhập rồi mở lại link.

## Ca A — Từ lịch hẹn tới khám mới

### 1. Tiếp nhận bằng tài khoản lễ tân

1. Đăng nhập `assistant.demo`.
2. Mở **Tiếp nhận và hàng chờ**, chọn đúng chi nhánh/ngày.
3. Tìm mã Patient của `newExamination`, mở **Tiếp nhận**. Có thể mở trực tiếp `newExamination.checkInUrl` từ fixture.
4. Đối chiếu mã/tên/ngày sinh của Patient. Người đặt có thể khác người khám.
5. Chọn lý do **Khám mới**, ghi `TEST lý do khám`, tích **Đã đối chiếu đúng người đến khám với lịch và hồ sơ đã chọn**.
6. Bấm **Xác nhận Check-in**, mở **Xem hàng chờ** → tab **Hàng chờ và lượt khám**.

Kỳ vọng: có một hàng cho Patient, trạng thái **Chờ khám**; Appointment đã CheckedIn. Mở lại tiếp nhận trả cùng Visit; không tạo Patient hoặc Visit thứ hai. Không sửa ID/version thủ công.

Nếu muốn test từ bước tạo dữ liệu: tạo Patient ở **Bệnh nhân** trước, sau đó **Xếp lịch khách vãng lai** tại tiếp nhận; chọn Patient đó, dịch vụ **Khám**, Nha sĩ demo, ngày hiện tại, thời lượng phù hợp và slot hợp lệ. Với giờ bắt đầu trong 60 phút, nhập lý do ngoại lệ. Lịch thành công mới đi tiếp Check-in.

### 2. Bác sĩ bắt đầu và lưu nháp

1. Đăng xuất lễ tân, đăng nhập `dentist.demo`.
2. Vào hàng chờ, tìm đúng Patient, bấm **Mở lượt khám**.
3. Kiểm tra header mã/họ tên, **tuổi, giới tính, ngày sinh**, lịch/bác sĩ/chi nhánh; bấm **Bắt đầu khám**. Tuổi tính số năm tròn theo ngày phòng khám; thiếu ngày sinh/giới tính hiện **Chưa rõ**, không suy từ người đặt. Nếu cần bổ sung danh tính, sửa qua chức năng hồ sơ bệnh nhân rồi tải lại phiên bản ở màn khám.
4. Nhập **Lý do khám / triệu chứng**: `TEST đau răng 16`.
5. Sơ đồ mặc định **Răng vĩnh viễn** (32 vị trí). Chọn **Răng 16**, ghi tình trạng `TEST kết quả quan sát buổi này`. Có thể chuyển **Răng sữa** (20 vị trí) hoặc **Răng hỗn hợp** để xem hai nhóm riêng; chuyển nhóm không mất kết quả đã ghi, kể cả răng đang ẩn.
6. Nhập chẩn đoán `TEST chẩn đoán`; bấm **Lưu nháp** rồi F5.

Kỳ vọng: Visit và Appointment InProgress, nội dung không mất sau F5. Nháp có thể thiếu thông tin; không tự có actual/khoản thu. Patient/lịch không bị thay đổi.

### 3. Xác minh tiền sử và xác nhận kết quả

1. Tiền sử: chọn **Chưa biết** và đánh dấu **Đã xác minh tiền sử** nếu chưa lấy được thông tin.
2. Dị ứng: chọn **Không có**, đánh dấu đã xác minh. Thuốc: chọn **Không có**, đánh dấu đã xác minh. Đây chỉ là dữ liệu test.
3. Có thể thử bỏ trống chẩn đoán hoặc chưa đánh dấu xác minh rồi **Xác nhận kết quả khám**: phải báo lỗi và giữ form.
4. Bổ sung đủ thông tin, bấm **Xác nhận kết quả khám**.

Kỳ vọng: hiện **Đã xác nhận kết quả khám**, nội dung khóa sửa; nút **Chuyển sang tư vấn** và **Mở điều trị cũ** dùng được. Unknown vẫn khác None. Đây chưa phải hoàn tất Visit.

### 4. Ảnh và tài liệu S20

1. Chọn tab **Ảnh & tài liệu** → **Tải tài liệu lên**.
2. Chọn JPEG/PNG/PDF thực, ngày nguồn không sau hôm nay; chọn loại/nguồn và upload.
3. Thử file giả đuôi, file >20 MiB hoặc quá 10 file: phải bị từ chối.

Kỳ vọng trong môi trường hiện tại: file hợp lệ nhận **Đang kiểm tra an toàn/PendingScan** vì chưa có daemon ClamAV; không xem/tải/review được. Không đổi trực tiếp database sang Ready để đánh dấu pass. Upload không tự có dấu bác sĩ đã review.

### 5. Tư vấn và kế hoạch

1. Từ kết quả đã xác nhận, bấm **Chuyển sang tư vấn** → **Tạo đợt điều trị**.
2. Kiểm tra Patient, chi nhánh, bác sĩ và chẩn đoán được mang sang; bấm **Tạo hồ sơ**.
3. Chọn **Mở kế hoạch S21** → **Thêm hạng mục**.

**Điểm chặn UI hiện có:** PlanPage đang gọi `/api/appointments/options`, chỉ trả dịch vụ Khám (`IsInitialExamination`). Vì vậy không chọn được đầy đủ dịch vụ điều trị. Ghi nhận bước này là chưa đạt tích hợp danh mục; không đổi nhãn Khám thành dịch vụ điều trị để coi luồng đã pass.

Backend kế hoạch đã được kiểm tra độc lập bằng API thật: tạo hồ sơ → lưu nháp có hạng mục → ghi Agreed với nội dung tư vấn/người quyết định → chốt kế hoạch Committed. Dữ liệu mẫu của ca B được chuẩn bị theo đường này. UI S21 còn bỏ `visitId` khi chuyển trang; sau khi xem kế hoạch nên quay lại Visit từ hàng chờ để tiếp tục đúng lượt.

## Ca B — Đến lại và mở hồ sơ cũ

1. Đăng nhập `assistant.demo`, mở lịch của `continuation`.
2. Đối chiếu Patient, chọn lý do **Điều trị tiếp**; chọn **Đợt điều trị cũ** do server trả về.
3. Xác nhận Check-in, ghi nhận mã Visit mới từ hàng chờ.
4. Đăng xuất, đăng nhập `dentist.demo`; mở Visit vừa tạo, **Bắt đầu khám**.
5. Ghi nhận đánh giá của buổi hiện tại, xác minh tiền sử/dị ứng/thuốc, lưu và xác nhận như ca A.
6. Bấm **Mở điều trị cũ** → chọn đúng hồ sơ đã có → **Điều trị trong lượt này**.

Kỳ vọng: giữ nguyên dossier/item/plan cũ, không bắt lập lại toàn bộ kế hoạch hoặc đồng ý lại chỉ vì có lượt mới. Visit hiện tại vẫn đúng Patient/lịch được tiếp nhận.

### S22 — Lưu nháp việc làm và kiểm tra điểm bị chặn

1. Tại **Ghi việc đã làm**, chọn **Hạng mục đã đồng ý**: dịch vụ điều trị thử nghiệm, răng 16.
2. Chưa chọn bước chi tiết nếu ca không cần; số lượng `1`, đơn vị `răng`, thời điểm nhập theo giờ Việt Nam hiện tại.

Lưu ý lỗi hiển thị hiện có: giá trị mặc định của **Thời điểm thực hiện** đang lấy từ UTC qua `toISOString()` rồi đưa vào ô local datetime, có thể lệch 7 giờ. Kiểm tra và nhập lại theo đồng hồ Việt Nam trước khi lưu; không coi giá trị mặc định này đã được nghiệm thu đúng múi giờ.

3. Ghi **Kết quả thực hiện**: `TEST nội dung thực hiện`; chọn cách tính phí **Đã gồm trong giá thỏa thuận** (Included).
4. Bấm **Lưu nháp**.
5. Bấm **Xác nhận đã làm**.

Kỳ vọng đã kiểm chứng: **Lưu nháp thành công**, chưa phải công việc Posted và chưa có khoản thu. **Xác nhận đã làm bị chặn 409/MSG240**, UI báo cần kiểm tra đồng ý theo phiên bản hiện tại vì consent adapter chưa kết nối. Không coi nháp là đã điều trị. Nhánh Separate còn phụ thuộc nguồn charge sau khi consent được nối.

## Các bước cuối cần nghiệm thu sau khi tích hợp thêm

| Bước | Trạng thái trong code hiện tại |
| --- | --- |
| Xác nhận actual → tiến độ | Bị chặn ở consent; Labo/capability/nguồn Visit-activity còn adapter unavailable ở các nhánh phụ thuộc |
| Hoàn tất chuyên môn, dặn dò, bàn giao quầy | Chưa có command hoàn tất Visit/Appointment của DCMS-115 |
| Bảng kê, công nợ, thu tiền, đơn thuốc | Chưa nối vào luồng hiện tại; số tiền kế hoạch không phải khoản đã thu |
| Hẹn tiếp từ instruction | Có backend chỉ định; form đặt lịch hiện gửi `followUpId: null`, chưa nghiệm thu luồng liên kết hoàn chỉnh |
| App đọc hồ sơ chính thức | Chỉ có treatment-summary một phần; chưa tổng hợp toàn bộ Visit/actual/file/dặn dò |

Vì các điểm này, môi trường hiện tại cho test luồng đầy đủ **đến từng điểm chặn thực tế**; chưa thể đánh dấu pass ca check-in → điều trị chính thức → hoàn tất → thu tiền → hẹn tiếp.

## Bằng chứng phiên chạy

- PostgreSQL Docker healthy; API 5084 health trả API/database healthy; web 8084/sign-in HTTP 200.
- Playwright Chrome dùng API thật: 2/2 pass (17.9 giây), queue/start/nháp/reopen/S20/confirm/mở điều trị cũ và giữ bản nhập khi xung đột.
- API thật trong schema riêng: plan Committed, actual Draft, post actual 409/MSG240, tài liệu PendingScan và content 409, handoff cùng Visit. Không thay adapter consent/scan để giả lập luồng thành công.
- Kiểm thêm bằng Chrome thật tại S22: chọn hạng mục đã đồng ý → nhập kết quả → Lưu nháp thành công → Xác nhận đã làm trả 409/MSG240, UI hiển thị cần kiểm tra đồng ý theo phiên bản hiện tại. Hai ca dành cho người dùng vẫn chưa check-in.
- Dữ liệu runtime và kết quả lưu trong `.tools/dcms-manual-flow-fixture.json`; screenshot test ở `.artifacts/manual-clinical-flow/` và `.artifacts/dcms112-browser/`.


## Bổ sung 27/10/2026 — Test chỉ định cận lâm sàng

Dùng lượt khám đang InProgress, bác sĩ phụ trách, phiếu chưa xác nhận. Giữ nguyên bản nháp khám nếu đang nhập.

1. Mở tab **Cận lâm sàng** → tạo chỉ định, chọn loại, nhập tên/lý do; răng hoặc vùng chỉ ghi khi có. Bác sĩ quyết định có cần kết quả để kết luận hay không.
2. Với chỉ định cần kết quả: sang Tổng kết, bấm Hoàn tất khám. Kỳ vọng giữ Tổng kết, báo chỉ định chưa xử lý và có nút đến Cận lâm sàng.
3. Trong chỉ định, tải tệp nếu có. Chờ tệp Ready trước khi dùng tệp làm kết quả; PendingScan/Rejected không được tính là đã nhận hoặc đã đọc. Có thể nhập kết quả văn bản độc lập, không xác nhận thay cho tệp đang kiểm tra.
4. Bấm nhận kết quả bằng bác sĩ phụ trách hoặc trợ lý cùng chi nhánh. Trạng thái chuyển sang chờ bác sĩ đọc; vẫn chưa hoàn tất khám được nếu đây là chỉ định bắt buộc.
5. Bác sĩ phụ trách xem kết quả, ghi nhận định khi cần và **Xác nhận đã đọc**. Trợ lý không có quyền ký đọc thay bác sĩ.
6. Trở lại Tổng kết, chọn các chẩn đoán cuối cùng, ghi hướng xử trí/dặn dò theo ca rồi Hoàn tất khám. Nội dung chưa lưu trước lúc tạo chỉ định phải còn nguyên.
7. Chỉ định tạo nhầm được bác sĩ hủy kèm lý do, vẫn có lịch sử. Chỉ định không cần cho kết luận có thể chưa xong khi xác nhận phiếu, nhưng phải đọc hoặc hủy trước khi kết thúc toàn lượt.

Ví dụ nội dung nhập chỉ là dữ liệu test, không phải gợi ý chỉ định theo bệnh. Ca không cần cận lâm sàng có thể bỏ qua tab này; không tạo chỉ định chỉ để hoàn tất khám.


## Bổ sung 27/10/2026 — Test ghi chẩn đoán một lần

Trong lượt đang khám, chọn răng 16 và 26 → chọn Sâu răng ở Chẩn đoán cho răng đang chọn → Áp dụng. Không cần gõ mô tả nếu không có thông tin bổ sung. Tổng kết phải hiển thị Sâu răng với từng răng, ô chẩn đoán bổ sung có thể trống. Lưu nháp, mở lại; sửa chẩn đoán tại nguồn phải đổi Tổng kết, xóa ở một răng vẫn giữ ở răng còn lại. Xóa ở tất cả nguồn phải bỏ khỏi kết luận, không phục hồi chuỗi trước đó. Có thể chọn bệnh toàn hàm tại Chẩn đoán toàn hàm; không bắt nhập kết quả tổng quát khi đã đủ ghi nhận khác.

Điền lý do khám, dặn dò và hướng xử trí theo ca rồi Hoàn tất; các chỉ định CLS cần kết quả vẫn phải xử lý. Ca bình thường chọn Chưa phát hiện bất thường trong Tổng kết, không chọn răng/không tạo kết quả giả. Nếu mới ghi phát hiện và chưa đủ dữ kiện kết luận thì lưu nháp, không chọn bệnh để vượt kiểm tra. Dữ liệu test không thay quyết định chuyên môn của bác sĩ.
