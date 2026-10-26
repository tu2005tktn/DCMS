# Luồng phòng khám theo DCMS-110

Ngày cập nhật: **25/10/2026**. Đây là hợp đồng luồng và ranh giới tích hợp để triển khai tiếp, chưa phải báo cáo nghiệm thu.

Nguồn: nội dung người dùng xác nhận trong phiên này; [Epic DCMS-110](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-110); [bộ đặc tả v2.4](README.md). Mô tả task được tổ chức lại ngày 25/10/2026 thay cho các đoạn bổ sung chồng nhau. Trạng thái Done của task cũ là đầu mối tìm phần có thể tái sử dụng, không tự chứng minh luồng tích hợp mới đã đạt.

## Luồng và thông tin tại từng bước

| Bước | Người dùng | Nhập tại bước này | Đầu ra bàn giao |
| --- | --- | --- | --- |
| 1. Tiếp nhận | Lễ tân | Đối chiếu bệnh nhân/lịch, giờ đến, lý do đến; đợt cũ khi biết | Một Visit và một dòng hàng chờ; [DCMS-111 / S17–S18](S17-S18-RECEPTION.md) |
| 2. Khám | Bác sĩ | Triệu chứng, tiền sử cần xác nhận, răng/tình trạng, chẩn đoán, kết quả | Kết quả của Visit hiện tại; [DCMS-112](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-112) |
| 3. Tư vấn và đồng ý | Bác sĩ và bệnh nhân | Hạng mục, răng/vùng, giá thỏa thuận, dự kiến và quyết định | Kế hoạch/hạng mục được đồng ý |
| 4. Điều trị | Bác sĩ | Việc thực tế hôm nay, người làm, kết quả, phần còn lại | Actual và tiến độ; chỉ định quay lại khi cần |
| 5. Hoàn tất chuyên môn | Bác sĩ | Dặn dò, xác nhận tổng kết; không nhập lại việc đã làm | Visit hoàn tất và thông tin bàn giao quầy |
| 6. Bảng kê và thu tiền | Thu ngân | Xem nguồn tính phí; nhập tiền thực nhận và phương thức | Số dư và phiếu thu khi có tiền thực nhận |
| 7. Hẹn tiếp | Bác sĩ chỉ định, lễ tân đặt | Khoảng ngày/nội dung, sau đó chọn giờ/bác sĩ/ghế | `NeedsBooking` hoặc Appointment sau khi đặt thành công |

Khách chưa có lịch đi qua chức năng xếp lịch nhanh hợp lệ rồi check-in. Chỉ tư vấn có thể kết thúc mà không có Actual. Nếu đã đồng ý nhưng làm buổi khác, chuyển sang chỉ định hẹn buổi đầu. Cọc trước điều trị là nhánh theo thỏa thuận, không phải điều kiện mặc định của check-in. Chưa chốt giờ vẫn giữ nhu cầu quay lại; hệ thống không tự tạo một lịch xác nhận.

## Bốn khái niệm nối dữ liệu

| Khái niệm | Ý nghĩa và liên kết |
| --- | --- |
| Patient | Người bệnh dùng cùng hồ sơ định danh; người đặt lịch và người trả tiền có thể là người khác |
| Đợt điều trị và hạng mục | Phạm vi đã tư vấn/thống nhất, theo dõi xuyên nhiều lần đến |
| Visit | Một lần đến; một Appointment hợp lệ tạo tối đa một Visit. Lần đến tiếp theo dùng Appointment và Visit mới |
| Actual | Việc thực tế được ghi trong Visit, nối về hạng mục khi có |

Một Visit có thể làm nhiều hạng mục; một hạng mục có thể qua nhiều Visit. Tiến độ chuyên môn không phải số tiền đã trả. Hoàn tất lượt không tự đóng cả đợt. Lịch đã đặt không phải việc đã thực hiện. Không bắt mỗi step tương ứng một buổi. Xem thêm [điều trị nhiều buổi](TREATMENT-PROGRESSION.vi.md), đồng thời đối chiếu lại các hợp đồng dự kiến cũ với v2.4 trước khi code.

## Form và API dùng chung

Mỗi task phải chỉ rõ dữ liệu nhân viên nhập, dữ liệu hệ thống lấy, thao tác commit và đầu ra cho bước sau. Patient/Visit/item/source IDs và version đi theo ngữ cảnh hoặc lựa chọn có nhãn; không xuất hiện thành trường nhập ID thủ công. Giá trị do client mang theo vẫn phải được server kiểm lại.

Dùng codebase .NET 10/EF Core/PostgreSQL, staff React/TypeScript và app React Native/Expo hiện có. Domain giữ quy tắc, Application giữ contract, Infrastructure thực thi persistence và Api ghép các lớp. Mở rộng mô hình và `DcmsDbContext` chung khi thiếu; không dựng schema nghiệp vụ hoặc engine song song chỉ để khớp tên task.

Server kiểm actor/quyền, Patient, chi nhánh, trạng thái và nguồn. Các command thay đổi dữ liệu áp dụng `requestKey`/`expectedVersion` theo đối tượng cần bảo vệ. Retry, bấm đôi và thao tác đồng thời không nhân Visit, Actual, biến động kho, charge, khoản thu hoặc booking. Xác thực/quyền hiện tại phải được kiểm lại cả khi trả kết quả retry.

DCMS-111 tạo lượt; DCMS-112 bắt đầu và ghi khám trên **chính Visit đó**. Bước sau đọc dữ liệu bằng ID đã commit, không tìm “lượt mới nhất của bệnh nhân” để thay thế ID được bàn giao. Các status Appointment, Visit, điều trị và thanh toán giữ riêng.

## Phạm vi đã xác nhận

| Nhóm | Quy tắc |
| --- | --- |
| Nguồn lực | Ba ghế vật lý; kiểm ghế và toàn bộ nhân sự được phân công khi xếp lịch. Không suy ra ba nha sĩ. Chờ thuốc/chuẩn bị không tự giải phóng reservation |
| Điều trị | Implant tại phòng khám; nội dung niềng răng khảo sát đã được người dùng xác nhận. Bác sĩ điều chỉnh kế hoạch/số buổi theo từng Patient |
| Thuốc | Đơn mua ngoài; thuốc thực dùng nội bộ theo tồn tổng sản phẩm/chi nhánh. Không quản lý lô, BatchId, FEFO hoặc bán thuốc mang về |
| Tiền | Cọc, trả nhiều đợt và giảm giá theo thỏa thuận/quyền; không tỷ lệ cứng hoặc phiếu thu 0 đồng. Lưu plan hoặc đến hạn trả không tự ghi nhận tiền |
| Bảo hành | Theo hạng mục/bộ phận/điều khoản; tách tuổi thọ dự kiến và bảo hành Labo |
| App | Chỉ đọc nguồn chính thức của chính Patient đã xác minh; check-in không cấp quyền hồ sơ |
| Ngoài backlog này | Không thêm Email Service, chatbot/AI, hệ thống kho mới hoặc thanh toán nhà cung cấp |

## Điểm cần quyết định trước khi bật chức năng liên quan

Thời điểm ghi nhận phí gói nhiều buổi chưa đủ dữ liệu nghiệp vụ: ghi toàn gói hay từng phần theo sự kiện nào, và xử lý khi dừng/đổi kế hoạch ra sao. Task bảng kê phải ghi rõ nhánh chưa được phép bật trước khi có xác nhận. Không tự chia đều phí theo số buổi, không tự thu toàn bộ lúc lưu plan. Nguồn dịch vụ một lần có quy tắc rõ vẫn có thể triển khai độc lập. Việc này không chặn check-in.

## Chuẩn bàn giao và bằng chứng

Mỗi task bàn giao PR, contract/ví dụ request–response, danh sách phần tái sử dụng/phần bổ sung và bằng chứng phù hợp. Luồng có ghi dữ liệu cần bằng chứng API/PostgreSQL và thao tác giao diện thật; mock hỗ trợ kiểm giao diện nhưng không đủ để đánh dấu tích hợp pass.

Kiểm một Patient xuyên các bước, đọc lại nguồn đã commit, đồng thời kiểm retry, concurrency, quyền và dữ liệu sai nguồn. Số liệu test, commit, môi trường và giới hạn chưa kiểm phải được ghi rõ. Lần cập nhật tài liệu này không tạo PR code và không ghi nhận ca nghiệp vụ nào đã pass.
