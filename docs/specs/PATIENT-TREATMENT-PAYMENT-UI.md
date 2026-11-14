# Hồ sơ khách hàng — Điều trị & Thanh toán (Web)

> **Đã gỡ ngày 13/11/2026.** Phân hệ tài chính đã được xóa khỏi backend, web và database theo [CARE-LAB-FINANCE-STORAGE.vi.md](CARE-LAB-FINANCE-STORAGE.vi.md). Tài liệu này chỉ còn giá trị tham khảo khi đặc tả lại chức năng; không mô tả hành vi hiện tại.

## Baseline v2.5 — 27/10/2026

Điều chỉnh mới nhất: bỏ tự lưu hàng và ghi chú. Nút ✓ trên hàng chỉ mở bước xem lại; chỉ xác nhận cuối cùng mới ghi máy chủ. Ghi chú có nút Lưu ghi chú. Quy định này thay thế các mô tả tự lưu còn giữ bên dưới để tham khảo lịch sử UI.

Phần triển khai mới tuân theo [hợp đồng v2.5](TREATMENT-PAYMENT-V25.md). Hợp đồng này thay thế các mô tả bên dưới về màn hình chỉ xem/điều hướng, cờ hoàn tất tự do, hàng nhập chỉ dùng để xem trước và giới hạn quá trình vào một Visit. Giữ bố cục bảng hiện có, không hiển thị dải chẩn đoán hoặc checkbox VAT theo quyết định người dùng. S21/S22/S43 dùng nguồn thật; S38/S39 chờ ledger DCMS-101, mock chỉ dùng trong preview/test.

## Mục đích và nguồn dữ liệu

Màn hình nhân viên trong hồ sơ bệnh nhân hiển thị mỗi **phiếu điều trị** bằng một treatment dossier hiện có. Phiếu có thể kéo dài nhiều lượt khám. Giao diện này là nơi xem nhanh và điều hướng; S21 vẫn là nơi sửa kế hoạch, S22 là nơi ghi việc đã làm, S43 là chi tiết dossier. Không tạo một thực thể phiếu mới.

Danh sách dùng `GET /api/patients/{patientId}/dossiers` và phân trang theo API hiện có. Khi mở phiếu, tải `GET /api/dossiers/{dossierId}` để đọc item, quyết định, snapshot giá, giảm trừ, ghi chú và financial summary. Nếu URL có `visitId`, bảng quá trình dùng `GET /api/visits/{visitId}/treatment-context`, chỉ hiện activity của dossier đang mở và lượt khám đó. Không có `visitId` thì giải thích rằng chưa có nguồn tổng hợp việc đã làm của toàn đợt. Server tiếp tục quyết định quyền truy cập.

## Bố cục và thao tác

### Đối chiếu giao diện trực tiếp

Đợt chỉnh giao diện ngày 26/10/2026 đối chiếu trang oDental do người dùng cung cấp tại chiều rộng 1913 px. Giữ thứ tự và mật độ như mẫu: thanh tab cao khoảng 55 px; tóm tắt ngay dưới; phiếu trắng toàn chiều rộng; hàng phiếu cao khoảng 65 px; dải xám, hàng tiêu đề và hàng dữ liệu trong hai bảng đều gọn. Cột dịch vụ và ghi chú chiếm phần lớn bề ngang, cột răng/SL/tiền/thao tác hẹp. Bảng kế hoạch kết thúc sát bảng quá trình; nút cộng của bảng quá trình nằm ở chân bảng. Cụm tiền nằm bên phải, vùng hình ảnh bắt đầu bên trái ngang phần dưới cụm tiền, tiếp đến ghi chú và nút tạo phiếu căn giữa. Giữ biểu tượng hành động cỡ nhỏ, nền trắng, đường kẻ mảnh và xanh lá cho nút thêm/lưu. Đây là hướng dẫn trình bày, không thay đổi nguồn dữ liệu hoặc API. Theo chỉ dẫn trước của người dùng, **không hiển thị dải Các chẩn đoán** dù có trong ảnh gốc.

Dòng nhập kế hoạch và quá trình cho nhập nhiều số răng, SL riêng và tự lưu nháp khi đủ trường; chưa chốt kế hoạch hoặc post actual. Menu hạng mục đã chốt giữ bốn lựa chọn Thực hiện, Hoàn tất, Sửa, Xóa theo quyền và điều kiện lâm sàng hiện có. “Thực hiện” mở hàng quá trình điền sẵn; muốn ghi việc đã làm thật phải qua S22 với Visit hợp lệ. Các thao tác chưa có command không được báo thành công giả.

Theo ảnh tham chiếu của người dùng, trang hồ sơ dùng toàn bộ chiều rộng: thanh trắng cao gọn với mũi tên và tiêu đề ở trái, năm tab ở giữa, dấu đóng ở phải. Không lặp banner bệnh nhân phía trên tab Điều trị & Thanh toán. Tab Thông tin cơ bản vẫn hiển thị banner và dữ liệu hành chính hiện có; lịch sử thay đổi có đường mở phụ tại đó. Ngay dưới thanh tab của trang điều trị là tên/mã/tuổi bệnh nhân ở trái và ba số liệu xếp ngang ở phải. Ở màn hình hẹp, các thành phần xuống dòng hợp lý và bảng cuộn ngang trong vùng riêng.

**Cập nhật 03/11/2026 (thay thế quy tắc tài chính cũ ngay bên dưới, theo yêu cầu người dùng):**

- Bỏ thanh tóm tắt ở đầu danh sách phiếu (Tổng cộng dự kiến, Đã trả, Còn nợ, số hoạt động chờ ghi phí). Dòng tên/mã/tuổi bệnh nhân giữ nguyên. Không gọi `GET /api/patients/{id}/dossiers/summary` từ danh sách nữa; endpoint và contract API không đổi.
- Trong phiếu bỏ dòng **Phí đã ghi nhận**.
- **Đã trả** = `financialSummary.allocatedVnd` của phiếu khi `financialSummary.available` và giá trị khác null; ngược lại hiển thị `0`. Không dùng khoản dự toán hay mốc thanh toán làm số đã trả.
- **Còn nợ** (sửa 03/11/2026) = tổng **Còn nợ** của các việc đã làm trong QUÁ TRÌNH ĐIỀU TRỊ, tức Σ max(0, Chi phí − Giảm giá − Đã trả) theo `payments/context`. Cách tính cũ "Thành tiền của kế hoạch − Đã trả" bị bỏ vì kế hoạch và việc đã làm có thể có giá khác nhau, làm Còn nợ sai (ví dụ về 0 dù còn việc chưa trả). Việc chỉ nằm trong kế hoạch, chưa làm, chưa phát sinh nợ. Khi chưa tải được dữ liệu thanh toán mới dùng tạm Thành tiền − Đã trả (tối thiểu 0). Đây không phải `outstandingVnd` của ledger nên không gọi là công nợ kế toán.
- Hạng mục hoãn/từ chối không vào Thành tiền theo quy tắc hiện hành của dải Kế hoạch.

### Form Tạo thanh toán mới (03/11/2026)

Nút **TRẢ TIỀN...** của phiếu mở hộp thoại **Tạo thanh toán mới** (Ant Design Modal) theo ảnh tham chiếu; dữ liệu và quy tắc ở [TREATMENT-DOSSIER-PAYMENTS.md](TREATMENT-DOSSIER-PAYMENTS.md). Hộp thoại thay Drawer ledger cũ cho nút này; **LỊCH SỬ** liệt kê các thanh toán đã lưu của phiếu (ngày, tổng tiền, phương thức, người thu, ghi chú, các dòng).

- Trường theo thứ tự: **Ngày thanh toán** (mặc định hôm nay giờ Việt Nam); khung nền vàng nhạt hướng dẫn nhập tiền theo từng dịch vụ/thủ thuật; **Tổng tiền thanh toán (đ)** nhập được trực tiếp (đã chốt 03/11/2026): khi nhập tổng, hệ thống tự chia vào các việc đã làm theo thứ tự cũ nhất trước, mỗi dòng không quá Còn nợ, rồi điền số đã chia vào các ô bên dưới; tổng không được vượt tổng Còn nợ của phiếu. Người dùng vẫn sửa được từng ô, và khi sửa ô thì Tổng tự cộng lại các dòng; để trống ô hạng mục là chấp nhận được. Server vẫn nhận phân bổ theo từng dòng như cũ; bảng `Ngày ĐT/DV | Bác sĩ | Điều trị/dịch vụ | Số tiền TT (đ)` điền sẵn từ các việc đã làm trong QUÁ TRÌNH ĐIỀU TRỊ, ô số tiền mặc định 0; **Thanh toán bằng** (Tiền mặt/Chuyển khoản); **Người thu tiền**; **Ghi chú**; nút **QUAY LẠI** và **LƯU LẠI**.
- Mỗi ô số tiền chỉ nhận số nguyên từ 0 tới Còn nợ của dòng; dòng hết nợ bị khóa và ghi rõ "Đã thanh toán đủ". Dòng không có tên dịch vụ hiển thị "—".
- Không có việc đã làm, hoặc tất cả hết nợ: hiện thông báo tương ứng và tắt LƯU LẠI. Tổng bằng 0 tắt LƯU LẠI.
- Đang lưu thì khóa hộp thoại. Lỗi (kể cả MSG111 vượt Còn nợ) hiển thị ngay trong hộp thoại, giữ dữ liệu đã nhập và nêu bước tiếp theo; sau MSG111 tải lại các dòng. Một `requestKey` cho cùng nội dung, đổi nội dung thì tạo key mới.
- Lưu thành công: đóng hộp thoại, tải lại **Đã trả**, **Còn nợ** và cột **Đã trả** của từng việc trong phiếu.
- Khả năng truy cập: nhãn cho mọi trường và từng ô số tiền (theo tên dịch vụ), focus rõ, thao tác bàn phím, không chỉ dùng màu để báo lỗi. Màn hình hẹp: bảng cuộn ngang trong vùng riêng.
- Quy tắc ledger/"chuyển khoản chỉ tính sau xác minh" ở bên dưới không áp dụng cho form này.

Quy tắc cũ (chỉ còn giá trị lịch sử): các nhãn tài chính trong ảnh là **Tổng cộng**, **Đã trả**, **Còn lại**. Khi chưa có ledger, UI phải ghi rõ **Tổng cộng (dự kiến)** và “Chưa có dữ liệu đối soát” cho hai số sau; không hiển thị 0 đ hoặc tự tính còn lại. Giá kế hoạch và mốc thanh toán không phải tiền đã thu.

Mỗi phiếu là một khung trắng lớn có thể mở/thu, viền nhạt và ít khoảng cách giữa các vùng. Hàng đầu chỉ hiển thị ngày lập, mã phiếu và cụm nhãn màu, xóa, in ở góc phải; **không hiển thị hàng “Các chẩn đoán”** theo yêu cầu giao diện mới. Dữ liệu chẩn đoán của dossier không bị xóa và biểu mẫu tạo dossier hiện có vẫn tuân thủ hợp đồng API. Kế hoạch và quá trình có dải tiêu đề xám nhạt, bảng đầy đủ chiều ngang và các cột như mẫu; nút thêm nhỏ màu xanh nằm ở cuối mỗi bảng. Dưới bảng quá trình là tổng và cụm thanh toán bên phải; không hiển thị ô VAT khi chưa có chính sách thuế. Tiếp theo là vùng Hình ảnh với ô nét đứt rồi vùng Ghi chú; nút xanh “+ TẠO PHIẾU MỚI” nằm giữa dưới danh sách. Các phần thiếu API vẫn được thể hiện đúng vị trí và trạng thái không khả dụng, không biến thành dữ liệu giả.

Phiếu hiện mã, ngày tạo, kế hoạch đã chốt, các hàng nháp tự lưu, quá trình điều trị, tài liệu và ghi chú. Nút cộng ở cột thao tác mở hàng nhập tại chỗ. Các hàng đủ thông tin được lưu nháp trên máy chủ, có ID/version và tải lại vẫn còn; không cần nút ✓. Chi phí nháp có thể tham gia tổng dự kiến đang hiển thị nhưng không phải giá đã thỏa thuận hoặc khoản đã thu. Menu hàng nháp cho sửa, xóa có xác nhận và chuyển nội dung sang hàng quá trình; không coi nháp là actual. Không dùng mẫu điều trị trong luồng này. Bản in không bao gồm hàng nháp.

Quá trình điều trị chỉ coi activity Posted từ lượt khám đúng bệnh nhân và phiếu là việc đã làm. Hàng nhập mới có thể tự lưu thành nháp riêng khi có Visit đúng Patient; không tự post actual, không tạo Labo hoặc khoản thu. Không có Visit thì giữ nội dung gõ dở trong trình duyệt và báo cần mở từ lượt khám. Ghi chú cuối phiếu sửa trực tiếp, tự lưu riêng. Tài liệu bệnh nhân mở trang tài liệu hiện có; nút thanh toán vẫn không khả dụng khi chưa có ledger.

## Trạng thái và kiểm thử

Phân biệt đang tải, danh sách trống, phiếu không có item, không có lượt khám, lượt khám không có activity, lỗi mạng, 401/403/404 và lỗi nguồn ledger. Phiếu tải lỗi có thể thử lại; mở phiếu khác vẫn được. Lỗi nhập hiển thị ngay tại ô của hàng nhập; dữ liệu vẫn còn để sửa. Tất cả nút khả dụng có tên hành động rõ và điều hướng bằng bàn phím. Trên màn hình hẹp, bảng có vùng cuộn riêng thay vì làm tràn toàn trang; bản in bỏ điều hướng, điều khiển và hàng nhập chưa xác nhận.

Kiểm bằng Playwright với API giả lập cho nhiều phiếu, mở/thu, tạo phiếu, có/không có `visitId`, thiếu ledger, lỗi tải và in. Kiểm lại S21/S22/S43, tài liệu, tab lịch hẹn, lịch sử và cổng nhân viên. Không tạo dữ liệu thử trong database dự án.







## Nhập nhiều răng, ghi chú và tự lưu bản nháp (26/10/2026)

- Ô Răng trong hàng kế hoạch và quá trình là chuỗi các số răng ngăn bằng dấu phẩy hoặc khoảng trắng (ví dụ `11, 12, 21`), giữ nguyên từng số đã nhập, không dùng `InputNumber` có giới hạn 99 để biến cả chuỗi thành 99. Khi dòng đủ dữ liệu, lưu mảng răng; khi chưa hợp lệ, giữ nguyên nội dung và báo lỗi tại ô.
- Bỏ dòng chữ “Bản xem trước · Chưa lưu” dưới tên dịch vụ của hàng kế hoạch. Trạng thái lưu, nếu cần, thể hiện riêng và trung thực; không đổi hàng chỉ ở trình duyệt thành hạng mục đã chốt.
- Ghi chú cuối phiếu có thể sửa trực tiếp và tự lưu sau khi ngừng nhập ngắn; API cập nhật riêng `PATCH /api/dossiers/{dossierId}/note` với `{ note, expectedVersion, requestKey }`. Bác sĩ đúng chi nhánh được sửa, ghi chú tối đa 4000 ký tự, trả dossier/version mới và audit event. Lỗi mạng hoặc version cũ giữ nội dung đang nhập, hiển thị trạng thái lỗi và nút thử lại. Cập nhật ghi chú không sửa kế hoạch, actual hoặc trạng thái lâm sàng.
- Hàng kế hoạch và quá trình đang nhập phải giữ nội dung qua tải lại trong lúc chưa đủ điều kiện lưu; khi hợp lệ, tự lưu bằng API tương ứng nếu có ngữ cảnh lâm sàng cần thiết. Không tự post việc điều trị hoặc tạo khoản thu chỉ vì bác sĩ đang gõ. Hàng quá trình không có lượt khám hợp lệ phải báo rõ chưa thể ghi vào hồ sơ.
- Hàng nhập hợp lệ được lưu dưới dạng **nháp phiếu**, tách khỏi `TreatmentItem` và `ClinicalActivity`, qua `GET /api/dossiers/{dossierId}/sheet-drafts` và `PUT/DELETE /api/dossiers/{dossierId}/sheet-drafts/{rowId}`. DTO gồm kind `Plan`/`Actual`, `visitId?`, ngày, các số răng, số lượng, serviceId, ghi chú, dentistId, phụ tá, công việc Labo/công ty, giá và giảm giá; PUT có `expectedVersion` và `requestKey`. Server kiểm bác sĩ/chi nhánh, dossier thuộc Patient, dịch vụ và ngày hợp lệ, và Visit đúng Patient nếu là Actual; không tạo item/actual/charge/booking. Mỗi dòng lưu có ID/version ổn định, tải lại mở được, chỉnh sửa tự lưu lại. Dòng chưa hợp lệ giữ trong `sessionStorage` theo dossier/kind; không gửi payload chưa hợp lệ. Nhãn ở dòng là “Đã lưu nháp” để tránh hiểu là kế hoạch chốt hoặc việc đã làm. Dữ liệu nhập thay đổi được lưu sau khoảng ngừng gõ, không cần nút ✓; × chỉ hủy dòng mới chưa lưu, xóa dòng đã lưu là thao tác riêng có xác nhận.

## Nhãn trạng thái và xóa phiếu (26/10/2026)

- Nhãn ở đầu phiếu là **cờ giao diện độc lập** với trạng thái lâm sàng `State`, chỉ có **Chưa xong** và **Hoàn tất**. `DisplayCompleted` mặc định false cho phiếu hiện có và mới, lưu trong dossier để mở lại trang vẫn giữ lựa chọn. Chọn một giá trị gọi `POST /api/dossiers/{dossierId}/display-status` với `{ completed, expectedVersion, requestKey }`; không gọi close/reopen, không đòi hỏi plan, item, Visit, actual, payment, follow-up hoặc lý do. API chỉ kiểm Dentist được phép xem phiếu/chi nhánh, version và requestKey; ghi audit event, actor, time và tăng dossier version trong cùng transaction. Cùng key/cùng payload trả kết quả cũ, khác payload hoặc version cũ trả MSG111. Chỉ nhãn giao diện đổi sau khi server lưu thành công; không thay đổi `State`, closure, progress, charge hoặc Appointment. Chọn giá trị đang hiển thị không gửi command. Lỗi mạng/conflict giữ nguyên cờ và cho thử lại.
- Nút xóa gọi DELETE /api/dossiers/{dossierId} với JSON body { expectedVersion, requestKey }; API khai báo nguồn body tường minh để Minimal API nhận đúng payload của phương thức DELETE và khởi động được. Chỉ Dentist thuộc chi nhánh được phép. Lệnh xóa mềm (State = Deleted) chỉ thành công khi phiếu Open, PlanState None, PlanVersion 0, không có item, step, instruction, activity, document, revision draft hoặc liên kết Visit; giữ sự kiện tạo/xóa, receipt và mã phiếu để audit. Từ chối phiếu có nghiệp vụ bằng MSG233 (409); version cũ hoặc requestKey cùng payload khác bằng MSG111 (409). Retry cùng key và payload trả cùng kết quả. Phiếu đã xóa không xuất hiện trong list/get hoặc tóm tắt bệnh nhân, không thể mở lại bằng route thường. Không xóa Patient, actual, charge, payment hay dữ liệu liên quan.
- Web yêu cầu xác nhận với mã phiếu, hiển thị loading và lỗi; sau xóa tải lại danh sách và chọn phiếu kế tiếp. Nếu phiếu không đủ điều kiện, nút xóa vẫn cho phép mở hộp thoại giải thích hoặc hiển thị lỗi chặn từ server; không tự ẩn/xóa dữ liệu trên client trước phản hồi thành công.









## Điều chỉnh thao tác theo mẫu oDental — 27/10/2026

Hàng nhập mở ngay trong bảng kế hoạch/quá trình. Nút xanh ✓ có tên “Đồng ý” kiểm tra và lưu trực tiếp; không mở drawer xem lại hoặc hỏi xác nhận thêm. Không tự lưu khi gõ. Quyết định lâm sàng và dữ liệu bắt buộc được nhập trên cùng hàng: lưu đề xuất không đồng nghĩa bệnh nhân đã đồng ý; chọn Agreed phải có tư vấn và người quyết định. Khi ghi actual, chọn Visit hợp lệ, bước khi áp dụng, kết quả và cơ sở tính phí ngay trên hàng. Không tự tạo bằng chứng đồng ý, Visit, kết quả hay căn cứ miễn phí. Các trường tùy chọn/điều kiện không thêm một bước xác nhận riêng. Quy trình quyết định riêng của item đã lưu vẫn có thể mở từ menu.

Một lần bấm gửi lưu nháp và command chuyển nguồn hiện có, có loading và khóa gửi lặp. Retry cùng payload giữ requestKey và version của nháp đã lưu; lỗi giữ hàng đang nhập và cho tải lại phiên bản nguồn mà không xóa form. Ghi chú cuối phiếu vẫn lưu bằng nút riêng. Chỉ hiển thị thành công sau phản hồi server. Không đổi hợp đồng API, backend hay database.


## Luồng nhập trực tiếp mới — 27/10/2026 (thay các hướng dẫn nhập cũ)

Theo yêu cầu đã chốt, form nhập chỉ còn Răng, bỏ Vùng/SL; kế hoạch thêm số phút dự kiến nullable 1–480. Ghi chú chỉ text, không chọn quyết định/tư vấn. ✓ lưu trực tiếp, không tự lưu. Nguồn ClinicianEntry không phải Agreed/consent. Thực hiện trong menu lưu ngay actual Dossier không Visit và gạch ngang nguồn; Hoàn tất/Bỏ hoàn tất chỉ đổi DisplayCompleted item; Sửa mở inline; Xóa hiện Đồng ý/Không đồng ý rồi soft-delete, không cascade dữ liệu đã làm/tiền. Không có filter Visit/ngày ở quá trình. Thêm việc đã làm cho nhập trực tiếp từ catalog không cần item/Visit trước. Đã trả là số ConfirmedAllocated từ ledger, không có dữ liệu hiện Chưa có dữ liệu. Snapshot giá/tên dịch vụ/răng/note actual giữ độc lập khỏi sửa kế hoạch. Chi tiết API/schema/test xem TREATMENT-PAYMENT-V25.md, mục Luồng nhập trực tiếp.


## Bỏ nút in trên thẻ phiếu điều trị (08/11/2026)

Trong danh sách phiếu điều trị của hồ sơ bệnh nhân, thẻ mỗi phiếu không còn nút in ("In phiếu đang mở"). Các nút Xóa phiếu, trạng thái và thu/mở phiếu giữ nguyên.

Hồ sơ khách hàng (14/11/2026): vùng nội dung các tab dùng gần hết bề rộng màn hình, lề trong thu còn 12px (trước 30px) để các thẻ thông tin dàn kín; màn hình hẹp giữ lề riêng.

Phiếu điều trị: nút "Kế hoạch điều trị" (chọn kế hoạch mẫu) nằm ngay cạnh tiêu đề "KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ" ở bên trái; các số tổng dự toán, giảm giá, thành tiền giữ bên phải.
