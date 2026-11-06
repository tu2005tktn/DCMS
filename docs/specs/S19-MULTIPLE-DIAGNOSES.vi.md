# Chẩn đoán nhiều loại bệnh là kết luận cuối cùng của phiếu khám

Chốt theo yêu cầu ngày 27/10/2026, bổ sung S19/v2.5.

## Nghiệp vụ và giao diện

- Chẩn đoán chính là kết luận cuối cùng về tình trạng/bệnh được bác sĩ ghi nhận, không có một ô Kết luận khám bắt nhập lại cùng ý. Một phiếu có thể chọn nhiều chẩn đoán; chưa yêu cầu phân loại chính/phụ hoặc tự xếp ưu tiên.
- Tab Tổng kết khám có một mục Chẩn đoán / Kết luận cuối cùng, tìm/chọn nhiều loại bệnh dưới dạng tag. Gợi ý lấy từ danh sách vấn đề người dùng cung cấp; không tự coi triệu chứng/kết quả odontogram là chẩn đoán. Có thể nhập tên khác rồi Enter để bao quát trường hợp ngoài danh mục. Không tự gán mã ICD hoặc suy đoán bệnh.
- Có lựa chọn Chưa phát hiện bất thường cho ca khám tổng quát; bác sĩ chủ động chọn, không tự điền khi để trống. Không bắt chọn răng, chọn điều trị hay nhập tiền sử nếu không có thông tin. Kết quả khám răng/vùng, lý do khám, dặn dò và hướng xử trí vẫn theo quy tắc hiện tại.
- Chỉ một nơi sửa chẩn đoán cuối cùng, không còn textarea Chẩn đoán ở tab Khám và textarea Kết luận khám riêng. Phiếu đã xác nhận chỉ đọc, sửa qua đính chính có lý do; lịch sử giữ chẩn đoán của từng phiên bản. Thông báo hoàn tất thiếu chẩn đoán mở đúng tab Tổng kết.

## API và tương thích

`ExaminationContent` bổ sung `diagnoses?: string[] | null`, tối đa 30 mục không rỗng/không trùng sau trim (so sánh không phân biệt hoa thường), tổng độ dài chuỗi nối bằng dấu chấm phẩy và khoảng trắng tối đa 4000 ký tự. Mỗi mục tối đa 4000 ký tự để giữ nguyên một chẩn đoán tự do dài của phiếu cũ. Lưu nháp cho phép danh sách rỗng, xác nhận bắt ít nhất một mục.

Khi `diagnoses` có mặt (kể cả []), đây là nguồn có thẩm quyền: server kiểm tra và chuẩn hóa; `diagnosis` và `conclusion` được đồng bộ thành chuỗi nối để các màn danh sách/handoff cũ vẫn hoạt động. [] xóa chẩn đoán hiện hành, không fallback lại chuỗi cũ. Không có diagnoses thì tiếp tục nhận diagnosis legacy; không bắt conclusion, nếu conclusion trống thì dùng diagnosis để tương thích. Không dùng conclusion đơn lẻ để tự suy ra tên bệnh khi ghi phiếu mới.

Đọc phiếu/snapshot cũ thiếu diagnoses vẫn giữ nguyên diagnosis/conclusion, không tách chuỗi theo dấu phẩy/chấm phẩy vì có thể là nội dung tự do. UI coi diagnosis cũ là một mục, hiển thị conclusion cũ khác diagnosis như ghi chú chỉ đọc; không xóa dữ liệu/snapshot khi chỉ mở hoặc lưu các mục khác. Khi bác sĩ chủ động sửa danh sách chẩn đoán, phiếu hiện hành dùng danh sách mới, nội dung trước đó giữ trong lịch sử phiên bản. Không migration: dữ liệu lưu trong ContentJson và audit JSON hiện có. Điều kiện kết thúc Visit kiểm có chẩn đoán và hướng xử trí, không yêu cầu hai trường cùng ý.

## Kiểm chứng

API/PostgreSQL riêng: nhiều chẩn đoán lưu/mở lại/xác nhận/đính chính/lịch sử; kết luận tương thích được đồng bộ; [] không phục hồi chuỗi cũ; giá trị rỗng/trùng/quá giới hạn bị từ chối không tăng version; legacy diagnosis không có conclusion vẫn hoàn tất; snapshot cũ giữ nguyên. Browser/API thật: chọn nhiều bệnh từ danh mục, tìm/nhập mục khác, bỏ từng tag, một editor duy nhất, mở lại/readonly/đính chính/handoff, ca không bất thường và thông báo thiếu chẩn đoán đúng tab. Không thay thế kiểm định chuyên môn bởi kiểm thử phần mềm.

### Phản hồi khi hoàn tất chưa đủ nội dung — 27/10/2026

Hoàn tất khám có lỗi kiểm tra không được tự bật tab Khám & sơ đồ răng. Giữ tab Tổng kết và bản nhập, hiển thị thông báo Chưa thể hoàn tất khám ngay gần nút Hoàn tất cùng danh sách chính xác mục thiếu. Mỗi mục có thao tác Bổ sung để bác sĩ chủ động mở đúng tab, cuộn và focus đúng ô (lý do, kết quả tổng quát, vùng/răng cụ thể, chẩn đoán, dặn dò/hướng xử trí/chi tiết chuyển khám-quay lại). Khi bổ sung, danh sách lỗi cập nhật theo nội dung đang nhập; không tự chuyển tab hoặc kéo focus theo mỗi ký tự. Lỗi API khi hoàn tất cũng hiện trong tab Tổng kết, không bị khuất ở đầu trang. Xác nhận thành công giữ tab Tổng kết và trạng thái Đã hoàn tất khám.

Giữ các điều kiện bắt buộc hiện có, không tự điền kết quả hay kết luận bình thường và không tự lưu khi chỉ bấm mục lỗi. Dữ liệu thiếu bị chặn trước request, backend tiếp tục kiểm độc lập. Quy tắc này thay quy tắc cũ tự mở tab chứa mục thiếu đầu tiên.


## Ghi một lần tại răng/vùng, tập hợp kết luận — 27/10/2026

Cập nhật UI 28/10/2026: kết quả/chẩn đoán Toàn hàm dùng cùng dòng vùng khám được tạo qua Thêm vùng khám, không còn hai ô toàn hàm riêng phía trên sơ đồ. Tất cả finding cũ vẫn hiển thị và sửa tại dòng nguồn. Liên kết sửa từ Tổng kết và focus lỗi dùng ID dòng chung, không trỏ các ô toàn hàm đã bỏ.

Thay quy tắc chỉ sửa chẩn đoán tại Tổng kết bằng các chẩn đoán có vị trí. Bác sĩ chọn một/nhiều chẩn đoán ngay khi ghi nhận răng, vùng hoặc toàn hàm; mô tả bổ sung tùy chọn nếu đã có chẩn đoán rõ. Ghi nhanh trên odontogram có cả ô mô tả phát hiện và chọn nhiều chẩn đoán; áp dụng tới các răng đang chọn, không suy luận từ văn bản. Nội dung mô tả cũ và tình trạng có sẵn không tự biến thành chẩn đoán.

Tổng kết hiển thị đầy đủ chẩn đoán và răng/vùng từ dữ liệu nguồn, cho quay lại đúng dòng để sửa/xóa; ô Chẩn đoán bổ sung dùng cho kết luận không gắn vị trí và dữ liệu cũ. Không bắt chọn lại bệnh đã ghi tại răng/vùng. Xóa/sửa chẩn đoán tại nguồn cập nhật Tổng kết ngay, không phục hồi từ chuỗi kết luận trước. Cùng bệnh ở nhiều răng được giữ vị trí từng răng, danh sách kết luận chung khử trùng tên. Khám tổng quát không bắt nhập nếu đã đủ ghi nhận răng/vùng. Ca bình thường chủ động chọn Chưa phát hiện bất thường; không bắt tạo finding giả hoặc chọn răng. Ca chưa đủ dữ kiện giữ phát hiện và lưu nháp, có thể tạo cận lâm sàng; không ép chọn bệnh giả để xác nhận. Các điều kiện CLS và hướng xử trí giữ nguyên.

API ToothFinding thêm diagnoses?: string[]. ExaminationContent thêm unscopedDiagnoses?: string[] để lưu nguồn kết luận không gắn răng; diagnoses hiện có là danh sách tổng hợp đầu ra. Nếu unscopedDiagnoses có mặt (kể cả []), đây là nguồn thay cho diagnoses/diagnosis ở cấp phiếu; tổng hợp union với diagnoses của các finding. Nếu vắng mặt, giữ tương thích diagnoses/diagnosis hiện hành. Không lấy condition làm bệnh. Server chuẩn hóa trim/khử trùng giữa các nguồn, tính lại diagnoses, diagnosis và conclusion từ các nguồn có cấu trúc. Không ghi danh sách tổng hợp ngược vào unscopedDiagnoses hoặc từng finding. Phiếu cũ không có trường mới giữ nội dung và lịch sử khi mở; không tách văn bản tự do, không migration.

Mỗi danh sách tối đa 30 tên, không rỗng/trùng không phân biệt hoa thường, mỗi tên và chuỗi tổng hợp tối đa 4000 ký tự; toàn phiếu tối đa 30 tên duy nhất. Finding cần răng/vùng hợp lệ và có mô tả hoặc chẩn đoán khi xác nhận. Không có finding chỉ được xác nhận khi kết luận duy nhất là Chưa phát hiện bất thường. Draft được phép chưa có chẩn đoán/kết quả; xác nhận vẫn cần nhận định của bác sĩ, không tự xác nhận bình thường. Xác nhận/đính chính/kết thúc/handoff và lịch sử dùng cùng tổng hợp, kiểm độc lập ở backend. Không đổi phân quyền, version, idempotency hoặc audit.

Kiểm chứng cần có: ca chỉ chọn bệnh ở răng, bệnh toàn hàm, nhiều răng cùng bệnh, sửa/xóa nguồn không hồi sinh, văn bản phát hiện không tự thành bệnh, ca bình thường không finding, danh sách sai giới hạn/null, legacy; lưu/mở lại/readonly/lịch sử/đính chính và handoff giữ vị trí. Browser trên schema riêng với API thật, bao gồm hồi quy odontogram/CLS.
