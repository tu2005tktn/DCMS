# Test thủ công: đặt lịch và xem hồ sơ cho người thân

Phạm vi: web khách hàng và web nhân viên, dữ liệu giả trong môi trường local. Người đặt A có tài khoản; người thân B chỉ có thông tin người thân và hồ sơ bệnh nhân, không tạo tài khoản cho B.

## Luồng mặc định mới — bổ sung 09/11/2026

Thực hiện bước 1–3 bên dưới, rồi A mở **Người thân → Xem hồ sơ**. Hồ sơ chính thức tự hiển thị, mặc định vào **Lịch sử khám**; không cần bước 4–5 xin/duyệt riêng. Có sẵn cả các tab Điều trị/Đơn thuốc/Ảnh và tài liệu, nội dung tùy hồ sơ chính thức tại chi nhánh tiếp nhận. Nếu chỉ mới đặt lịch chưa tiếp nhận, hiển thị **Chưa có hồ sơ liên kết** và vẫn có **Lịch đặt hộ**.

Kiểm thu hồi: trợ lý mở `/link-verification` → **Hồ sơ người thân**, đổi bộ lọc trạng thái sang **Tất cả** để thấy quyền tự cấp, chọn **Đối chiếu**, nhập lý do và **Thu hồi quyền**. A tải lại trạng thái thì hồ sơ biến mất; tải lại nhiều lần không tự cấp lại. Quyền đã bị thu hồi/hết hạn hoặc bị giới hạn thủ công không được tự mở rộng.

Bước 4–5 chỉ là nhánh bổ sung khi cần liên kết hồ sơ đã có từ trước mà không có lịch đặt hộ đã được tiếp nhận phù hợp. Form xin quyền nằm trong mục **Liên kết hồ sơ đã có tại phòng khám**. Không dùng trùng tên/SĐT để tự chọn bệnh nhân.

## Chuẩn bị

- Khách hàng: http://localhost:5173/customer/sign-in. Nếu chưa có A, đăng ký tại `/customer/register` bằng số điện thoại chưa sử dụng, rồi đăng nhập bằng số điện thoại đó.
- Nhân viên: http://localhost:5173/sign-in. Dùng `assistant.demo` cho tiếp nhận/xác minh; `dentist.demo` cho khám. Mật khẩu demo là giá trị `DEV_SEED_PASSWORD` trong `.env` local; không đưa mật khẩu vào báo cáo hoặc Git.
- Mở cửa sổ thường cho A và cửa sổ ẩn danh cho nhân viên để đổi vai thuận tiện. Trong cửa sổ nhân viên, đăng xuất trước khi đổi từ trợ lý sang bác sĩ hoặc ngược lại.
- API: http://localhost:5080/health phải báo API và database healthy.
- Khi muốn đi hết tiếp nhận và khám ngay, chọn lịch còn trống trong hôm nay, đúng ngày Việt Nam. Nếu đã hết giờ, có thể test đặt lịch ngày sau; phần tiếp nhận chờ đúng ngày hẹn. Không đổi đồng hồ hệ thống.

Kiểm tra lúc khởi động ngày 09/11/2026: API/database healthy, các route web trả HTTP 200, hai tài khoản nhân viên demo đăng nhập được và hàng đợi quyền người thân truy cập được. Với dịch vụ `Khám` (60 phút), `Nha sĩ demo` tại `S09 - Chi nhánh thử nghiệm`: ngày 09/11 không còn slot, ngày 10/11 có 33 slot tại thời điểm kiểm tra. Số slot thay đổi khi có người đặt. Chưa tạo lịch, bệnh nhân hoặc quyền truy cập mới trong lượt khởi động này.

## 1. Lưu người thân B

1. A mở http://localhost:5173/customer/family-members.
2. Bấm **Thêm người thân**. Nhập tên giả dễ phân biệt, ví dụ `TEST Người thân 0911`, ngày sinh `12/03/2015`, quan hệ `Con`, giới tính và liên hệ.
3. Bấm **Lưu**.

Kỳ vọng: B xuất hiện trong danh sách, có **Đặt lịch** và **Xem hồ sơ**. Không có bước tạo tài khoản cho B.

## 2. Đặt lịch hộ

1. Bấm **Đặt lịch** trên dòng B.
2. Kiểm tra đúng người thân, bấm **Tiếp tục**.
3. Chọn `S09 - Chi nhánh thử nghiệm`, bác sĩ `Nha sĩ demo`, dịch vụ nếu cần, ngày và khung giờ còn trống.
4. Kiểm tra A là người đặt/liên hệ, B là người khám; tên và ngày sinh B lấy từ người thân đã lưu.
5. Bấm **Xem lại** → **Xác nhận đặt lịch**, ghi mã lịch.
6. Trở lại danh sách B → **Xem hồ sơ** → **Lịch đặt hộ**.

Kỳ vọng: có lịch mới của B. Đặt lịch chưa tự cấp quyền xem bệnh án. Nếu không có khung giờ, nhân viên kiểm tra lịch đã công bố ở `/working-schedules` và cấu hình đặt lịch trước khi thử ngày/bác sĩ khác.

## 3. Tiếp nhận và tạo nội dung khám

1. `assistant.demo` mở http://localhost:5173/reception, chọn đúng chi nhánh/ngày, tìm mã lịch và bấm **Tiếp nhận**.
2. Chọn hồ sơ bệnh nhân của B. Nếu chưa có, bấm **Tạo hồ sơ qua chức năng hồ sơ bệnh nhân**; họ tên và ngày sinh phải giống B. Ghi mã bệnh nhân.
3. Đối chiếu danh tính, chọn lý do đến khám lần đầu và bấm **Xác nhận Check-in**.
4. Đổi sang `dentist.demo`, mở `/reception` → **Hàng chờ và lượt khám** → **Mở lượt khám** của B.
5. Bấm **Bắt đầu khám**, nhập triệu chứng, chẩn đoán và lời dặn giả; chọn hướng xử trí phù hợp rồi **Hoàn tất khám**.

Kỳ vọng: có phiếu khám chính thức. Phiếu chỉ lưu nháp chưa xuất hiện trong lịch sử khám được chia sẻ. Không tạo tài khoản đăng nhập cho B trong các bước này.

## 4. A xin quyền xem hồ sơ

1. A mở B → **Xem hồ sơ**.
2. Mở **Liên kết hồ sơ đã có tại phòng khám**, nhập mã hồ sơ B và thông tin giúp phòng khám đối chiếu.
3. Bấm **Yêu cầu quyền xem hồ sơ**.

Kỳ vọng: **Chờ phòng khám xác minh**; A vẫn xem được lịch đặt hộ nhưng chưa có tab bệnh án.

## 5. Trợ lý xác minh

1. Đăng nhập lại bằng `assistant.demo`, mở http://localhost:5173/link-verification.
2. Chọn tab **Hồ sơ người thân**, tìm yêu cầu B và bấm **Đối chiếu**.
3. Bấm **Tra cứu hồ sơ**, chọn đúng mã bệnh nhân; kiểm tra họ tên/ngày sinh khớp và chi nhánh có lượt khám của B.
4. Chọn chi nhánh chia sẻ, tích **Lịch sử khám**, đặt ngày hết hạn trong tương lai.
5. Chọn căn cứ chia sẻ phù hợp dữ liệu giả; nhập kết quả đối chiếu và tích xác nhận đã kiểm tra danh tính/sự đồng ý hoặc căn cứ đại diện.
6. Bấm **Cấp quyền xem hồ sơ**.

Kỳ vọng: **Đã được cấp quyền**. Không thể cấp khi danh tính không khớp, chưa chọn phạm vi, thiếu căn cứ/xác nhận hoặc thời hạn không hợp lệ.

## 6. Kiểm tra đọc và thu hồi

1. A bấm **Tải lại trạng thái**, mở **Lịch sử khám**, mở phiếu và đọc lời dặn.
2. Kiểm tra chỉ hiện phạm vi đã cấp; ghi chú nội bộ và phiếu nháp không xuất hiện.
3. Trợ lý mở lại yêu cầu, nhập lý do và bấm **Thu hồi quyền**.
4. A bấm **Tải lại trạng thái**, hoặc chuyển ra rồi quay lại cửa sổ A.

Kỳ vọng: trạng thái **Đã thu hồi**, tab/nội dung bệnh án biến mất; **Lịch đặt hộ** vẫn có. Các lần đọc API tiếp theo bị chặn dù A chưa đăng xuất.

## Các nhánh nên thử thêm

- A rút yêu cầu khi đang chờ: yêu cầu kết thúc, chưa có quyền đọc.
- Trợ lý từ chối có lý do: A thấy trạng thái từ chối và nội dung công khai nếu có.
- Sửa họ tên/ngày sinh hoặc xóa B sau khi có quyền: quyền cũ kết thúc, không xóa hồ sơ bệnh nhân và lịch đã lưu.
- Đăng nhập khách hàng khác: không thấy người thân hoặc hồ sơ của A.
- Hồ sơ được cấp quyền nhưng chưa có nội dung chính thức trong phạm vi/chi nhánh đó: hiển thị danh sách rỗng, không nhầm với trạng thái chưa cấp quyền.

Luồng và API được mô tả tại `../specs/FAMILY-DELEGATED-RECORD-ACCESS.vi.md`. Kết quả kiểm chứng tự động trước đó nằm trong `FAMILY-RECORD-ACCESS-20261109.vi.md`.
