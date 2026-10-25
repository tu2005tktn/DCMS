# Cấu hình khung đặt lịch và thời lượng dịch vụ

Ngày chốt: 19/10/2026. Nha sĩ nhập cấu hình linh hoạt; bệnh nhân chọn dịch vụ trước, sau đó chọn giờ còn đủ thời gian.

## Quyết định triển khai

### Chỉ có dịch vụ Khám cho đợt khám mới (quyết định mới nhất)

- Đặt lần đầu / đợt mới chỉ có **Khám**; bỏ bộ chọn dịch vụ tự do trên web/mobile. Chọn chi nhánh sẽ tự chọn dịch vụ Khám được cấu hình tại chi nhánh đó.
- Dịch vụ Khám có cờ hệ thống `isInitialExamination`, tối đa một bản ghi mỗi chi nhánh. Tên cố định `Khám`; nha sĩ cấu hình thời lượng và trạng thái hoạt động. Các dịch vụ còn lại là dịch vụ điều trị, không thể bật cho bệnh nhân tự đặt khám mới.
- Public options chỉ trả Khám đang hoạt động. Public availability và tạo lịch mới kiểm cờ loại dịch vụ ở server; gửi trực tiếp ID điều trị hoặc đổi tên dịch vụ điều trị thành Khám không vượt qua kiểm tra. `publicBookable` không đủ để biến dịch vụ điều trị thành dịch vụ khám mới.
- Tại quầy, hẹn mới Standard/WalkIn cũng chỉ được gắn Khám; các hẹn FollowUp phải qua chỉ định/phác đồ. API lịch cũ và đổi lịch vẫn đọc/giữ dịch vụ cũ, không ép chuyển lịch sử thành Khám.
- Settings trả `isInitialExamination` để trình bày; client không được đổi loại dịch vụ đã tồn tại. Dịch vụ thêm qua settings luôn là điều trị. Nếu chi nhánh chưa có Khám, lần lưu cấu hình tạo Khám mặc định 30 phút; lần đọc không ghi dữ liệu.
- Migration tạo dịch vụ Khám chính thức cho mỗi chi nhánh cũ, mặc định 30 phút; không xóa/sửa ID hay thời lượng lịch cũ. Các dịch vụ cũ giữ lại cho nội bộ/phác đồ và bị tắt publicBookable. Không suy loại dịch vụ từ tên.
- Các câu mô tả chọn dịch vụ tự do / bật từng dịch vụ cho đợt mới bên dưới là quyết định cũ và được thay thế bởi mục này. Luồng phác đồ chưa có runtime, không coi việc giữ catalog điều trị là đã triển khai đặt theo phác đồ.

### Phân biệt mục đích đặt lịch (bổ sung theo yêu cầu người dùng)

- Luồng chọn dịch vụ tự do chỉ dành cho khám lần đầu hoặc khám cho vấn đề/đợt mới, chưa gắn phác đồ. Một người có phác đồ đang thực hiện vẫn có thể có một vấn đề mới độc lập; không khóa toàn bộ chức năng đặt khám theo tài khoản.
- Tiếp tục đợt điều trị hiện tại phải chọn chỉ định/bước thuộc phác đồ chính thức của đúng bệnh nhân. Nha sĩ quyết định bước, thời lượng, nhân sự và khoảng ngày; bệnh nhân chỉ chọn giờ khả dụng trong các điều kiện đó. Không cho đổi bước thành dịch vụ bất kỳ hoặc tự rút ngắn thời lượng.
- Mỗi dịch vụ có `publicBookable` để phòng khám quyết định có cho chọn trong đặt khám mới không. Hoạt động nội bộ và cho đặt khám mới là hai cờ khác nhau; ngừng cho đặt online không xóa dịch vụ khỏi phác đồ/lịch cũ. Public options và public availability kiểm cờ này ở server.
- Đặt theo phác đồ kiểm tra PlanId/PlanVersion/StepId/InstructionId, PatientId, quyền sở hữu, trạng thái đồng ý/hiệu lực, tiền nhiệm, readiness và khoảng ngày; liên kết chỉ định với Appointment/ResourceReservation trong cùng transaction. Dữ liệu phải lấy từ server, không chỉ tin lựa chọn “đang điều trị” trên giao diện.
- Theo [hợp đồng điều trị nhiều buổi](TREATMENT-PROGRESSION.vi.md), chỉ định NeedsBooking chưa giữ chỗ; chỉ chuyển Booked sau khi kiểm tra nguồn lực và lưu lịch thành công. Đổi/hủy giữ liên kết; hủy chuyển chỉ định về NeedsBooking khi còn hiệu lực.
- Hiện module phác đồ/chỉ định chưa có runtime; adapter FollowUp vẫn trả không khả dụng. Không giả lập một lịch điều trị bằng hẹn Standard và không tuyên bố đã kiểm tra phác đồ khi chưa có dữ liệu. Bản cài đặt slot triển khai cấu hình dùng chung và cổng khám mới; đặt theo phác đồ cần module chuyên môn theo hợp đồng riêng.

- Chi nhánh có bước chia giờ bắt đầu do nha sĩ nhập, số phút nguyên 1–120, mặc định 15. Bước giờ độc lập với thời lượng dịch vụ; không làm tròn thời lượng thành số ca cố định.
- Thời lượng từng dịch vụ là số phút nguyên 1–480 (ví dụ 20, 35, 45, 90). Nhân viên có thể nhập thời lượng riêng cho hẹn tại quầy trong cùng giới hạn. Bệnh nhân dùng thời lượng dịch vụ, không tự sửa.
- Neo giờ bắt đầu vào đầu mỗi khoảng Work của nha sĩ rồi tăng theo bước cấu hình. Không neo lại sau giờ nghỉ; loại mọi khoảng chạm nghỉ/block, thiếu tài nguyên hoặc vượt giờ kết thúc.
- Nha sĩ/ClinicOwner hoặc thành viên CanManageSchedules sửa cấu hình tại chi nhánh đang tham gia. Cho nhập tên, thời lượng, trạng thái và publicBookable của dịch vụ; ngừng dùng thay vì xóa dịch vụ có lịch sử.
- `GET /api/booking-settings/options` trả chi nhánh được phép sửa. `GET /api/booking-settings/{branchId}` trả `{branchId,slotIntervalMinutes,version,services:[{id,name,durationMinutes,active}]}`. `PUT` nhận `{expectedVersion,requestKey,slotIntervalMinutes,services}`; id=0 thêm dịch vụ, id khác phải thuộc chi nhánh. Payload gồm đủ dịch vụ cũ; dùng active=false để ngừng. Validation 400 MSG04; ngoài phạm vi 403 MSG06; stale/key reuse 409 MSG111. Lưu cấu hình và receipt trong transaction cùng khóa 90403, replay sau kiểm quyền.
- Slot thêm `bookingSettingsVersion`; booking/đổi lịch nhận token tùy chọn cùng tên. Web gửi token đã đọc; token cũ trả 409 MSG111. Client cũ không gửi token vẫn phải qua kiểm tra tài nguyên và cấu hình mới.
- Lịch cũ giữ thời lượng đã đặt. Đổi lịch giữ thời lượng đó dù thời lượng dịch vụ đổi; không tự dời lịch đã xác nhận khi thay cấu hình.
- Quyết định này thay thế giới hạn bước slot 15 phút và dịch vụ online 30/60 trong S09/UC-04/UC-06. Các quy tắc lead time và tài nguyên khác giữ nguyên.

## Yêu cầu đã ghi nhận

- Phòng khám cấu hình cách chia ca/slot để bệnh nhân chọn giờ từ lịch làm việc đã công bố.
- Các loại khám và điều trị có thời lượng khác nhau, do phòng khám cấu hình; không áp một thời lượng chung cho tất cả.
- Slot khả dụng phải xét giờ làm thực tế, nghỉ/block, nha sĩ, ghế, phụ tá nếu có và lịch đã được đặt. Toàn bộ thời gian dự kiến của cuộc hẹn phải nằm trong khoảng trống liên tục.
- Thời lượng dự kiến khi đặt hẹn và thời gian thực tế thực hiện khám/điều trị là hai dữ liệu khác nhau. Không tự thay đổi lịch hoặc chiếm thời gian của lịch kế tiếp khi ca thực tế kéo dài.

## Hiện trạng trước thay đổi (mốc khảo sát)

- S09 đã có lịch làm lặp, ngoại lệ theo ngày và tra cứu giờ trống từ tài nguyên.
- `WorkingScheduleService` đang duyệt các giờ bắt đầu cách nhau 15 phút.
- `SchedulingService.PublicDurationMinutes` đã lưu thời lượng theo dịch vụ, nhưng ràng buộc database/domain cho đặt online chỉ cho phép 30 hoặc 60 phút. Web bệnh nhân gửi thời lượng theo dịch vụ đã chọn.
- Chưa có màn cài đặt bước chia slot và thời lượng từng loại dịch vụ cho phòng khám.
- Booking và đổi lịch phải tiếp tục dùng cùng kiểm tra tài nguyên trong transaction; tra cứu slot không giữ chỗ.

## Câu hỏi ban đầu (đã chốt ở trên)

1. **Ý nghĩa của ca/slot**: bước chia giờ bắt đầu độc lập với thời lượng dịch vụ, hay ca cố định mà dịch vụ phải chiếm một số nguyên ca liên tiếp? Hai cách dẫn đến quy tắc căn giờ và làm tròn thời lượng khác nhau.
2. **Luồng bệnh nhân**: chọn dịch vụ trước rồi chọn giờ còn đủ thời lượng, hay chỉ đặt ca khám ban đầu; các lần điều trị được nha sĩ chỉ định và đặt sau?

Ví dụ để thảo luận, không phải thời lượng lâm sàng mặc định: giờ làm 08:00–12:00, bước giờ 15 phút, dịch vụ A dự kiến 30 phút và dịch vụ B dự kiến 45 phút. Nếu chọn mô hình bước giờ độc lập, cả hai có thể bắt đầu 08:00 nhưng lần lượt kết thúc 08:30 và 08:45. Một giờ bắt đầu chỉ được trả về nếu đủ cả khoảng liên tục; dịch vụ 45 phút không được nhận lúc 11:30. Nếu đã có hẹn 09:00–09:30, dịch vụ 45 phút không được bắt đầu lúc 08:30.

## Phạm vi triển khai sau khi chốt

- Bổ sung cấu hình tại chi nhánh, quyền sửa theo cơ chế staff hiện hành và kiểm tra phiên bản.
- Cấu hình thời lượng theo dịch vụ/loại khám, điều trị; xác định dịch vụ nào bệnh nhân được tự đặt.
- Dùng cấu hình chung trong tra cứu, tạo lịch và đổi lịch; server xác minh thời lượng, giờ bắt đầu và tài nguyên, không tin dữ liệu client.
- Chốt đơn vị phút, khoảng giá trị, cách neo slot vào đầu ca và cách xử lý giờ nghỉ trước khi tạo migration.
- Lưu thời lượng đã đặt trong Appointment/ResourceReservation; sửa cấu hình dịch vụ không đổi thời lượng hoặc tự dời lịch đã xác nhận.
- Kiểm tra lại cấu hình cùng lịch làm và reservation khi commit; nếu cấu hình thay đổi sau lúc tra cứu thì trả xung đột và yêu cầu chọn lại. Không âm thầm ghi lịch theo dữ liệu cũ.
- Web và mobile hiển thị khoảng bắt đầu–kết thúc và thời lượng nhất quán; phân biệt tra cứu không còn slot với lỗi kết nối.

Quyết định ở đầu tài liệu là hợp đồng triển khai; các câu hỏi ở trên lưu bối cảnh lựa chọn ban đầu.
