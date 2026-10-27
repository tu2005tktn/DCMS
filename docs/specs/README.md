# Bộ đặc tả DCMS v2.4

Bộ đặc tả hiện hành là hai tài liệu Word v2.4, sửa ngày **25/10/2026**, do người dùng cung cấp. Các file được nhập nguyên bản, giữ nguyên nội dung và định dạng của nguồn.

| Tài liệu | Phạm vi |
| --- | --- |
| [Screen Specifications v2.4](DCMS_Screen_Specifications_v2.4.docx) | Màn hình, trường dữ liệu, thao tác, điều hướng và trạng thái hiển thị |
| [Use Cases, Business Rules and Messages v2.4](DCMS_Use_Cases_Business_Rules_Messages_v2.4.docx) | Luồng nghiệp vụ, quyền, quy tắc, thông báo, hợp đồng lệnh và kịch bản nghiệm thu |

## Cách sử dụng

Hợp đồng luồng được làm rõ theo yêu cầu ngày 25/10/2026 tại [DCMS-110](CLINIC-FLOW-DCMS-110.vi.md); phần tiếp nhận/check-in và bàn giao Visit cho màn khám tại [DCMS-111 S17/S18](S17-S18-RECEPTION.md). Phần DCMS-111 đã triển khai trên nhánh tính năng, có [bằng chứng riêng](../implementation/DCMS-111-RECEPTION.md); các task khám/điều trị tiếp tục tích hợp trên cùng Visit.

Đọc cả hai tài liệu khi thay đổi chức năng. Tài liệu UC/GBR/MSG xác định hành vi nghiệp vụ; tài liệu màn hình xác định trường, thao tác và điều hướng tương ứng. Dùng mã UC, S, GBR và MSG để tra cứu; số mục của các bản cũ có thể đã thay đổi.

v2.4 thay thế bộ Word v2.0 và các bản Word cũ trong repo. Các bản trùng tại `odental-v2/` đã được gỡ; lịch sử vẫn có trong Git. Chỉ cặp Word ở thư mục này là nguồn đặc tả hiện hành.

Các quyết định riêng đã được người dùng chốt vẫn phải được đối chiếu rõ ràng. Cụ thể, [chính sách mật khẩu ngày 18/10/2026](SHARED-PASSWORD-POLICY.md) quy định 8–256 ký tự, không bắt buộc nhóm ký tự; quyết định này tiếp tục ưu tiên hơn mặc định 12 ký tự kèm yêu cầu thành phần ở GBR-082 trong Word. Không tự đổi chính sách đã triển khai khi nhập tài liệu mới.

Các tài liệu Markdown theo tính năng và báo cáo trong `../implementation/` giữ quyết định, hợp đồng và bằng chứng tại thời điểm được viết. Chúng chưa mặc nhiên được đối chiếu hoặc nghiệm thu lại theo v2.4. Khi triển khai tiếp, cập nhật hợp đồng tính năng theo cặp Word và các quyết định riêng trước khi sửa code; ghi rõ khác biệt chưa giải quyết.

## Nội dung cần đối chiếu khi triển khai

| Nhóm | Nội dung của v2.4 |
| --- | --- |
| Luồng liên tục | Tiếp nhận → khám → tư vấn và thống nhất → ghi nhận điều trị thực tế → hoàn tất lượt khám → thu tiền nếu có → đặt buổi tiếp theo. Nhu cầu quay lại do nha sĩ ghi nhận; chọn lịch cụ thể là thao tác tiếp nhận riêng. |
| Ngữ cảnh và người nhập | Hệ thống mang theo bệnh nhân, lượt khám, đợt điều trị, nguồn liên quan và phiên bản. Trợ lý ghi nhận người đến và lý do ban đầu; nha sĩ xác nhận nội dung chuyên môn. Không yêu cầu nhân viên nhập ID kỹ thuật. |
| Điều trị nhiều buổi | Lượt khám, đợt điều trị, hạng mục, hoạt động thực tế và lịch hẹn có trạng thái riêng. Một hạng mục có thể qua nhiều lượt; hoàn tất một lượt không tự đóng đợt điều trị hoặc phát sinh phí cả gói lần nữa. |
| Màn hình lâm sàng | S19 là không gian lượt khám; S21 là tổng quan kế hoạch; S22 ghi việc đã thực hiện. S62 xử lý thay đổi phạm vi đã thống nhất và đồng thuận; S63 đánh giá hậu phẫu/bảo hành có liên kết nguồn. |
| Đặt buổi tiếp theo | Mẫu điều trị có phiên bản và cần nha sĩ xem xét. Hoàn tất bước tạo/cập nhật `NeedsBooking`; chỉ lệnh đặt lịch thành công mới tạo lịch đã xác nhận. |
| Kho và thuốc | Tồn thuốc tổng theo sản phẩm và chi nhánh; không quản lý lô hoặc FEFO trong bản phát hành này. Phân biệt đơn thuốc mua ngoài, danh mục và biến động tồn thực tế. |
| Thu tiền | Giá thỏa thuận, đặt cọc và thu nhiều đợt có bằng chứng riêng. Hoàn tất chuyên môn, đóng đợt điều trị và thanh toán không phải cùng một trạng thái. |
| Phạm vi | Word ghi nhận ba ghế vật lý, Implant tại phòng khám; không có Email Service trong bản phát hành này. AI và chatbot thuộc phase 2. Đây là phạm vi thiết kế, không phải kết quả kiểm tra triển khai. |

## Ranh giới với code hiện tại

Lần nhập cặp Word v2.4 chỉ tổ chức lại tài liệu. Sau đó người dùng yêu cầu triển khai riêng DCMS-111; phạm vi code, migration và kết quả hiện tại được ghi trong [runbook DCMS-111](../implementation/DCMS-111-RECEPTION.md). Các chức năng khác trong Word chưa mặc nhiên được triển khai theo lần nhập tài liệu.

Các tài liệu [recovery email](S03-PUBLIC-EMAIL-RECOVERY.md), [booking theo cấu hình](CLINIC-SLOTS-AND-SERVICE-DURATIONS.vi.md) và [điều trị nhiều buổi](TREATMENT-PROGRESSION.vi.md) ghi lại các quyết định hoặc phạm vi khác thời điểm. Đặc biệt, hợp đồng recovery email đã có trong repo cần được đối chiếu với phạm vi không có Email Service của v2.4 trước một thay đổi chức năng liên quan; lần nhập nguồn này không tự gỡ chức năng đó.
