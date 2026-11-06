# Khảo sát Thiết lập hệ thống oDental — 27/10/2026

## Nguồn và giới hạn bằng chứng

- [O] Quan sát trực tiếp tab Chrome đang đăng nhập tại [Thiết lập hệ thống oDental](https://quanlyphongnha.vn/#system), phiên bản hiển thị **7.0.0 (11/10/2026)**. Ngày khảo sát theo giờ Việt Nam: 27/10/2026.
- [O] Đã mở đủ 11 nhóm, biểu mẫu thêm của các danh mục và biểu mẫu sửa nhóm quyền/chi nhánh. Chỉ đọc; đóng bằng Đóng/Quay lại, không nhập hoặc lưu, không xóa, không xuất danh sách, không tải logo, không thử giao dịch.
- [O] Bằng chứng là nhãn, trường, trạng thái và thao tác hiện diện trên UI. Không chứng minh validation khi lưu, transaction, enforcement quyền ở server, sinh mã đồng thời, nghiệp vụ in, tồn kho, tích điểm hay tích hợp bên ngoài.
- Không đưa giá trị liên hệ chi nhánh, tài khoản đăng nhập, bản ghi khách hàng hoặc tên dữ liệu thử của tenant vào tài liệu. Không lưu cookie, token hoặc credential.
- [D] Nghĩa là yêu cầu đã có trong bộ DCMS v2.5/quyết định tính năng riêng; [P] nghĩa là chi tiết thiết kế đề xuất trong đặc tả mới. Quan sát [O] không tự trở thành quy tắc DCMS.

## Các nhóm và biểu mẫu đã quan sát

| Mã bằng chứng | Nhóm | Danh sách/biểu mẫu thực tế [O] | Giới hạn |
| --- | --- | --- | --- |
| OS01 | Thiết lập chung — Hệ thống | Thông báo chỉ chủ tài khoản thấy/sửa; checkbox tự đăng xuất khi không sử dụng; chọn 5/10/20/30/60/90/120 phút, giá trị đang hiển thị 20, bộ chọn bị vô hiệu khi checkbox tắt | Không bật/tắt; chưa chứng minh server kết thúc phiên |
| OS02 | Thiết lập chung — Hồ sơ khách hàng | Bắt buộc Ngày lập hồ sơ, Giới tính, Ngày sinh, Địa chỉ, Số điện thoại, Phân nhóm khách hàng, Nguồn; ba radio tự sinh có sửa tay/tự sinh không sửa tay/nhập tay | Không thử lưu hồ sơ hay tính duy nhất của mã |
| OS03 | Thiết lập chung — Phiếu thu | Lưu hành nội bộ; mã phiếu và số bắt đầu; địa chỉ, điện thoại, phương thức; cột bác sĩ/phụ tá; Tổng phí, Giảm giá, Đã trả trước đó, Còn lại; tổng nợ mọi phiếu; chữ ký; tên người trả mặc định khách hàng, người thu mặc định người dùng; ghi chú thanh toán | Không in và không thay số bắt đầu; chưa biết toggle lưu ngay hay lưu theo nhóm |
| OS04 | Chẩn đoán/tên bệnh | Tìm tên, cột Mã bệnh; thêm/sửa/xóa; form Tên và Mã bệnh, gợi ý tham khảo icd.kcb.vn | Danh sách có các tên gần trùng; không coi dữ liệu tenant là bộ seed chuẩn hoặc mã ICD đã kiểm định |
| OS05 | Thủ thuật/dịch vụ/sản phẩm/công việc | Cây nhiều cấp, giá bên cạnh tên, tìm kiếm và Excel; form Thuộc nhóm, Tên, Đơn giá, Thời lượng mặc định (30 phút), Bảo hành | Có dòng giá âm/thu tiền/gọi điện; không sao chép thành dịch vụ DCMS |
| OS06 | Dụng cụ/vật liệu Công ty | Tên, thêm/sửa/xóa; form chỉ có Tên | Chưa chứng minh quản lý công nợ từ danh mục |
| OS07 | Dụng cụ/vật liệu các loại | Tên, Thuộc danh mục, Cty cung cấp mặc định, Đơn giá, tìm tên và Excel; form Tên/loại có gợi ý đơn vị, danh mục và nút quản lý, nhà cung cấp mặc định, giá mặc định, người tạo chỉ đọc | Đã mở quản lý danh mục con: dòng tên, thêm, xóa, quay lại/lưu. Không thêm dòng |
| OS08 | Labo các công ty | Tên, địa chỉ, điện thoại, email; form có bốn trường tương ứng | Không có bằng chứng đồng nhất với bảng vật liệu |
| OS09 | Labo các loại công việc | Tên, công ty, đơn vị tính, giá, bảo hành; form công ty không bắt buộc, đơn vị gợi ý hàm/bán hàm/răng/cái/bộ | Chưa chứng minh điều kiện bảo hành hoặc hạch toán tự động |
| OS10 | Nhóm khách hàng/thẻ thành viên | Danh sách trống có nút thêm; form Tên nhóm bắt buộc; Giá tiền và Giá trị, Quy đổi 1 điểm, Tự động tích điểm, Thời hạn tháng/năm, dịch vụ miễn/giảm và ghi chú | Chỉ thấy form, chưa kiểm chứng mua thẻ, tính điểm, cộng dồn hay ưu đãi tự áp dụng |
| OS11 | Thuốc/dược phẩm/sản phẩm y tế | Tìm tên, Excel; form Tên, Mã thuốc/mã sản phẩm, Hoạt chất/biệt dược, Đơn vị/đóng gói, Đơn giá, Hướng dẫn sử dụng | Không kê đơn, cấp/bán thuốc hoặc gửi liên thông |
| OS12 | Phân quyền truy cập | Chủ tài khoản toàn quyền; bốn nhóm Doctors/Dentists, Assistants, Receptionists, Accountants; số người và nút xem; form tên nhóm + quyền màn hình/thao tác | Mở chi tiết Doctors/Dentists, không sửa checkbox; giá trị hiện tại không đại diện bộ quyền seed chuẩn |
| OS13 | Chi nhánh/cơ sở | Tên, địa chỉ, điện thoại, email, website, Facebook; thêm/sửa; nút xóa chi nhánh đang thấy bị vô hiệu | Không suy nguyên nhân vô hiệu hoặc khả năng xóa chi nhánh khác |
| OS14 | Form chi nhánh | Logo, tên, địa chỉ, điện thoại, email, website, Facebook; thêm trường mã cơ sở, mã liên thông, mật khẩu liên thông đơn thuốc quốc gia | Không nhập/đọc credential; các trường liên thông không thuộc phase 1 DCMS |

## Ma trận quyền hiển thị trong OS12

| Cụm | Các quyền thấy trên form [O] |
| --- | --- |
| Tổng quan, khách hàng | Truy cập; Excel khách hàng; tạo/sửa/xóa; xem mọi chi nhánh; xem điện thoại/email/địa chỉ |
| CSKH, lịch hẹn | Truy cập CSKH; lịch làm việc luôn xem được theo nhãn oDental; xem lịch mọi nhân viên; mở hồ sơ; xem/xóa/sửa chi tiết công việc |
| Nhân viên, vật liệu | Truy cập nhân viên/vật liệu; Excel nhập xuất; sửa/xóa phiếu nhập và xuất |
| Chi tiêu, khuyến mãi, labo | Truy cập; Excel chi tiêu/labo; sửa/xóa phiếu chi và yêu cầu labo |
| Phiếu điều trị | Xóa phiếu; thêm/sửa/xóa dòng; sửa chi phí dòng |
| Thống kê dịch vụ | Truy cập; xem mọi nhân viên; Excel; giới hạn tuần/tháng/3 tháng/bất kỳ |
| Thanh toán/phiếu thu | Truy cập thống kê; xem mọi nhân viên; Excel; mở hồ sơ; sửa/xóa thanh toán; giới hạn kỳ xem |
| Thiết lập | Truy cập thiết lập; quyền từng danh mục chẩn đoán, dịch vụ, công ty/loại vật liệu, công ty/loại labo, nhóm khách hàng, thuốc |

Các checkbox con có thể bị disabled khi quyền cha tắt. Chưa thử bật/tắt để xác nhận quan hệ phụ thuộc khi lưu. Không suy rằng quyền xóa trên UI cho phép hard-delete chứng từ đã ghi sổ trong DCMS.

## Đối chiếu DCMS và các khác biệt cần giữ

1. [D] Bố cục 11 nhóm đã có tại S58, chi tiết S31/S32/S46/S57/S04; UC-51 và GBR-176–186 là nền tảng. Không cần tạo bộ UC/S mới trùng mã.
2. [D] Chỉ Cash/Transfer; nhóm/thẻ không tự mở bán thẻ trả trước hoặc điểm; đơn thuốc mua ngoài không trừ kho. Không dùng giá âm cho giảm giá/hoàn tiền.
3. [D] Lịch hẹn tiếp tục theo DCMS: nha sĩ công bố lịch, bệnh nhân tự đặt Khám, tiếp nhận xác minh đúng người rồi Check-in. OS12 không thay quyền lịch hiện có bằng quy tắc “luôn xem được” của oDental.
4. [D] Seed v2.5 nói 34 chẩn đoán, 14 nhóm dịch vụ, 11 nguồn + Khác, 13 nghề, 9 tiền sử, 4 loại chi và 4 nhóm quyền. Dữ liệu live đã có tên gần trùng và dữ liệu thử; không dùng việc đếm tenant hôm nay để ghi đè seed.
5. [D] AT84 của Word UC ghi 3 phương thức, trái GBR-180 và S58 quy định Cash/Transfer. Đặc tả triển khai dùng **2** phương thức và ghi nhận sai khác, không bật Card/POS.
6. [D] AT90 nói non-owner không mở General; GBR-184 và S58 cho Owner hoặc người được cấp quyền rõ ràng. Hiểu ca bị chặn là non-owner **không có grant**; người được cấp settings grant hợp lệ vẫn có thể vào.
7. [P] DCMS dùng nút lưu rõ ràng, preview tác động, version/requestKey, audit và ngừng dùng/khôi phục; đây là thiết kế triển khai, chưa được kiểm chứng trên backend oDental.

## Tài liệu sử dụng kết quả

- [Đặc tả hệ thống S58/UC-51](../specs/S58-SYSTEM-SETTINGS.vi.md).
- [Kế hoạch triển khai và nghiệm thu](../implementation/S58-SYSTEM-SETTINGS-PLAN.vi.md).
- Nguồn DCMS: [Word màn hình v2.5](../specs/DCMS_Screen_Specifications_v2.5.docx), [Word UC/GBR/MSG v2.5](../specs/DCMS_Use_Cases_Business_Rules_Messages_v2.5.docx).
