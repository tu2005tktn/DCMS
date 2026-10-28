# Bộ đặc tả DCMS v2.5

Bổ sung 27/10/2026: [Thiết lập hệ thống S58/UC-51](S58-SYSTEM-SETTINGS.vi.md) cụ thể hóa 11 nhóm thiết lập theo khảo sát oDental, gồm trường dữ liệu, quyền, API, version/audit và ranh giới nghiệp vụ. Nội dung chi tiết đã ghi vào cả hai Word v2.5 chính tại phần S58/UC51 chi tiết; AT84/AT90 đã hiệu chỉnh theo GBR-180/184. Xem [bằng chứng khảo sát](../research/ODENTAL-SYSTEM-SETTINGS-20261027.vi.md), [kế hoạch triển khai/nghiệm thu](../implementation/S58-SYSTEM-SETTINGS-PLAN.vi.md) và [14 việc Jira đã phân công](../implementation/S58-JIRA-HANDOFF.vi.md). Đây là đặc tả và backlog, chưa phải chức năng đã triển khai.

Bổ sung 27/10/2026: [Chỉ định cận lâm sàng trong phần Khám](S19-INVESTIGATIONS.vi.md) mở rộng S19/S20 và thay giới hạn chưa có nguồn InvestigationId.


Bổ sung 27/10/2026: [Chẩn đoán nhiều loại bệnh là kết luận cuối cùng](S19-MULTIPLE-DIAGNOSES.vi.md) thay yêu cầu nhập riêng chẩn đoán và kết luận.


Nguồn đặc tả hiện hành là hai tài liệu Word **v2.5 ngày 26/10/2026** do người dùng cung cấp từ Downloads và yêu cầu cập nhật vào repo. Chỉ cặp Word dưới đây là bộ chính. Việc nhập tài liệu không xác nhận các chức năng mới đã được triển khai hoặc nghiệm thu.

| Tài liệu | Phạm vi |
| --- | --- |
| [Screen Specifications v2.5](DCMS_Screen_Specifications_v2.5.docx) | S01–S63: màn hình, trường, thao tác, điều hướng và trạng thái |
| [Use Cases, Business Rules and Messages v2.5](DCMS_Use_Cases_Business_Rules_Messages_v2.5.docx) | UC-01–55, quy tắc đến GBR-187, thông báo và nghiệm thu đến AT92 |

## Thay đổi của v2.5

- Tiếp tục bố cục Khám riêng S19 và Điều trị & Thanh toán S43: Kế hoạch S21, Quá trình S22, phí/công nợ S38, Trả tiền/Lịch sử S39. Giữ quy tắc GBR-163–175 và AT59–79 của lần sửa v2.4.
- Bổ sung GBR-176–187, MSG257, AT80–92; tài liệu màn hình ghi bằng chứng tham chiếu O07–O30 ở mục 9. Đây là ghi nhận trong tài liệu cung cấp, không phải kiểm chứng runtime mới của repo.
- Sidebar, danh sách, bộ lọc và form: Tổng quan, Khách hàng, CSKH, lịch/tiếp nhận DCMS, Bác sĩ–Nhân viên, Vật liệu, Chi tiêu, Khuyến mãi, Labo, thống kê và thiết lập.
- Hồ sơ bệnh nhân bổ sung ngày lập, hai số điện thoại, người giám hộ, Facebook, nghề nghiệp, người giới thiệu, nguồn, nhóm, lý do khám đầu; ba chế độ sinh mã. Các trường hành chính có thể cấu hình bắt buộc khi lưu mới/sửa, không tự điền dữ liệu cũ.
- Checklist tiền sử tại hồ sơ hành chính là lời khai tiếp nhận, có nguồn/người/thời điểm. Nó không thay ba ghi chú tự do tùy chọn ở S19 và không tạo checkbox xác minh bắt buộc cho hoàn tất khám.
- Danh mục seed là dữ liệu khởi đầu chỉnh sửa được; giá 0 và tên dịch vụ không tự xác nhận giá thật, phạm vi thực hiện hoặc quyền đặt lịch công khai.
- Chỉ Cash/Transfer, kênh Counter/PatientApp; không Card/POS hoặc mã số thuế bệnh nhân. Chuyển khoản chỉ tăng Đã trả sau xác minh, không dựa vào ảnh hoặc lời báo đã chuyển.
- Tổng quan phân biệt giá trị dịch vụ, tiền thực thu, chi phí và dòng tiền. Chênh lệch không được gắn nhãn lợi nhuận kế toán. Danh sách chi tiêu gộp nguồn nhưng không tính trùng lương/nhà cung cấp/Labo.
- Cấu hình in phiếu thu, số phiếu, nhóm quyền, xuất Excel theo quyền và tự đăng xuất khi không hoạt động. Đổi cấu hình không sửa chứng từ đã phát hành.
- Theo GBR-187, nha sĩ công bố lịch, bệnh nhân tự đặt và được xác nhận khi hợp lệ; tự đổi/hủy trước ít nhất 12 giờ, ngoại lệ qua hỗ trợ/trợ lý. Không sao chép lịch hẹn oDental.

## Quyết định Khám tiếp tục có hiệu lực

Bổ sung triển khai theo yêu cầu ngày 27/10/2026: [Odontogram 2D/3D](S19-ODONTOGRAM-2D-3D.vi.md), gồm hình răng trực quan, ghi nhanh nhiều răng, bề mặt tùy chọn và ô kết quả tổng quát. Phần dưới giữ bối cảnh nhập v2.5; trạng thái code và bằng chứng mới được cập nhật trong báo cáo triển khai.

Khám có phiếu và lịch sử riêng. Tiền sử, Dị ứng, Thuốc đang dùng là ghi chú tùy chọn: có thì ghi, không có thì để trống; không bắt chọn None/Present/Unknown hoặc tích xác minh. Hoàn tất phiếu khám không kết thúc Visit/Appointment. Tiếp tục điều trị là handoff rõ ràng, giữ cùng Visit cho cùng lần đến; kết thúc lượt là lệnh riêng. Khám tư vấn/bình thường không cần dossier/actual hay phiếu thu 0 đồng.

Đã hiệu chỉnh hai ngoại lệ cũ tại UC-10 và UC-15 của bản nhập để không mâu thuẫn với S19, GBR-036 và GBR-138. Không thay nội dung ngoài hai đoạn đó trong Word UC; Word màn hình giữ nguyên như tệp cung cấp. Chi tiết tại [ghi chú nhập](V2.5-IMPORT-NOTES.vi.md).

[Hợp đồng Khám S19/S20](S19-S20-DCMS-112.md) giữ quy tắc lỗi hoàn tất: chỉ rõ mục thiếu ngay cạnh nút hoàn tất, giữ bản nhập/tab Tổng kết và chỉ mở tab khác khi bấm Bổ sung. Khám không có bất thường vẫn ghi kết quả răng/vùng/toàn hàm theo thực tế. Ô Kết quả khám tổng quát lưu kết quả vùng Toàn hàm theo [bổ sung odontogram 2D/3D](S19-ODONTOGRAM-2D-3D.vi.md).

## Cách sử dụng và thứ tự đối chiếu

Đọc cặp Word cùng nhau: UC/GBR/MSG quy định hành vi, Screen quy định trường/thao tác. Tra theo mã thay vì số trang. Các quyết định riêng đã chốt tiếp tục phải được đối chiếu rõ ràng:

- [Chính sách mật khẩu](SHARED-PASSWORD-POLICY.md): 8–256 ký tự, không bắt buộc nhóm ký tự; không tự đổi code theo mặc định khác ở GBR-082.
- [Đặt khám mới và cấu hình thời lượng](CLINIC-SLOTS-AND-SERVICE-DURATIONS.vi.md), [tự điền thông tin bản thân](BOOKING-SELF-PREFILL.md): giữ quyết định đã triển khai; những mốc thời lượng cố định trong Word cần đối chiếu trước khi thay cấu hình/runtime.
- [Luồng DCMS-110](CLINIC-FLOW-DCMS-110.vi.md), [tiếp nhận S17/S18](S17-S18-RECEPTION.md), [Khám S19/S20](S19-S20-DCMS-112.md), [điều trị nhiều buổi](TREATMENT-PROGRESSION.vi.md) là hợp đồng theo tính năng, phải cập nhật khi triển khai theo v2.5.

Báo cáo trong `../implementation/` giữ mốc kiểm chứng cũ; đổi liên kết nguồn không biến chúng thành bằng chứng đạt toàn bộ v2.5. Xem [báo cáo Khám hiện có](../implementation/DCMS-112-EXAMINATION.md) để phân biệt code, API thật và các điểm chưa nối.

## Phạm vi và điểm chưa chốt

Giữ Q01 cho danh mục/phạm vi/giá thực tế và Q08 cho thời điểm ghi phí gói, phần việc Included và cách xử lý đổi/dừng gói; không tự suy chính sách từ ví dụ hay giao diện oDental. Không tạo dịch vụ giá âm; không xóa đè chứng từ đã ghi sổ. Hoàn tiền, thẻ/POS, hóa đơn điện tử pháp lý, email phiếu thu, điểm/thẻ thành viên trả trước và liên thông quốc gia không tự được bật. AI/chatbot thuộc phase 2.

Word không đưa Email Service vào phạm vi này; [recovery email đã có trong repo](S03-PUBLIC-EMAIL-RECOVERY.md) là khác biệt cần đối chiếu riêng, không tự gỡ trong lần nhập tài liệu.

## Lịch sử phiên bản

[Bộ v2.4](archive/v2.4/README.md) được giữ nguyên trong `archive/v2.4/`, gồm các sửa trước đây về điều trị/thanh toán, tách khám và ghi chú sức khỏe. Các phiên bản cũ không còn là nguồn hiện hành. Tệp gốc người dùng trong Downloads được giữ nguyên.


Cập nhật triển khai S58 ngày 27/10: [DCMS-119/121/128 — nền tảng, danh mục chẩn đoán và nhóm quyền](../implementation/S58-119-121-128.vi.md). Chỉ phạm vi ba task đã nối API/UI và kiểm chứng; không phải nghiệm thu toàn Epic.


[Cận lâm sàng và thanh toán](INVESTIGATION-BILLING.vi.md): hình thức thực hiện, chốt phí, charge, thu tiền và khoản phải trả đơn vị; [báo cáo triển khai](../implementation/INVESTIGATION-BILLING.vi.md).


[Phí khám và thanh toán sau khám](EXAMINATION-BILLING.vi.md): chốt phí hoặc chờ xác định, miễn phí, thu một phần và còn nợ.
