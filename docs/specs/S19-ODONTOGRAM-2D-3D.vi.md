# Odontogram trực quan trong phần Khám

Ngày chốt: 27/10/2026. Bổ sung S19 của bộ v2.5 theo yêu cầu triển khai odontogram 2D trực quan và mô hình 3D, dùng cùng nguồn phiếu khám/Visit hiện có.

## Giao diện và thao tác

Cập nhật 28/10/2026: trong khung răng đang chọn, nhãn và ô Chẩn đoán cho răng đang chọn đặt trước nhãn và ô Mô tả phát hiện / ghi chú bổ sung (nếu có), theo thứ tự chọn chẩn đoán rồi bổ sung mô tả. Không thay hành vi chọn răng, bề mặt, Áp dụng hoặc dữ liệu lưu.

- Mặc định là hình răng SVG 2D có số FDI, tách hàm trên/dưới, răng vĩnh viễn/răng sữa/hỗn hợp. Ghi rõ bên phải/trái của bệnh nhân. Không suy loại bộ răng từ tuổi.
- Chế độ 3D dùng mesh tham chiếu theo từng loại răng, có nguồn/giấy phép rõ ràng, được phục vụ cục bộ bằng Three.js, xoay/zoom/chọn răng và có nút đặt lại góc nhìn. Không phải dữ liệu scan/CBCT hoặc giải phẫu thật của bệnh nhân; không dùng mô hình để tự kết luận tổn thương. Không tải mô hình hay dữ liệu bệnh nhân lên dịch vụ ngoài.
- Hai chế độ dùng cùng số FDI, lựa chọn và findings; đổi góc nhìn/bộ răng không sửa/xóa phiếu hoặc làm dirty. Thiếu WebGL, mất context hoặc lỗi tải 3D phải có thông báo và đường về 2D, không chặn ghi khám.
- Chọn nhiều răng trực tiếp, không có nút chuyển chế độ chọn một/chọn nhiều. Bấm răng chưa chọn để thêm vào tập chọn, bấm lại răng đã chọn để bỏ riêng răng đó; những răng khác giữ nguyên. Cùng hành vi trên SVG 2D, mesh 3D và nút số FDI/bàn phím. Nút Bỏ chọn xóa toàn bộ lựa chọn tạm. Chọn/bỏ chọn chỉ đổi lựa chọn tạm, không tạo dòng finding trống, không xóa kết quả đã ghi và không làm phiếu dirty. Chỉ khi bấm Áp dụng mới ghi cùng nội dung/bề mặt cho các răng đang chọn. Lựa chọn tạm không phải kết quả đã lưu. Cho thêm nhiều phát hiện trên cùng một răng; thao tác áp dụng điền dòng trống trước, không ghi đè phát hiện đã có. Bấm lặp cùng nội dung không tạo dòng trùng.
- Ô ghi nhanh cho tìm/chọn vấn đề thường gặp hoặc nhập nội dung khác, không tự biến triệu chứng thành chẩn đoán. Mục Bi chưa rõ nghĩa không đưa vào lựa chọn mặc định. Tùy chọn bề mặt chỉ mở cho kết quả theo răng.
- Phân biệt bằng cả màu/ký hiệu/chú giải: chưa có ghi nhận, có kết quả, đang chọn. Chưa ghi không có nghĩa bình thường. Đây là lớp kết quả khám, không phải kế hoạch/việc điều trị/phí. Danh sách bên cạnh cho đọc tên răng, kết quả và bề mặt; không chỉ dựa vào màu. Có thao tác bàn phím cho chọn răng và chọn theo số FDI khi xem 3D.
- Phiếu đã xác nhận vẫn xem/đổi bộ răng/xoay 3D, nhưng không áp dụng hoặc sửa finding ngoài chế độ đính chính hợp lệ. Rời route phải hủy animation/listener và giải phóng geometry/material/texture/WebGL context.

## Khám tổng quát

Điều chỉnh ngày 28/10/2026: bỏ hai ô riêng Kết quả khám tổng quát và Chẩn đoán toàn hàm / vùng tổng quát trước sơ đồ. Dùng một danh sách kết quả răng/vùng: bấm Thêm vùng khám, nhập vùng Toàn hàm (hoặc vùng cụ thể), ghi mô tả và/hoặc chọn chẩn đoán tại cùng dòng. Không bắt chọn răng hoặc mở 3D. Finding Toàn hàm đã lưu hiển thị nguyên nội dung trong danh sách này, không bị ẩn, không tự gộp/xóa hay chuyển thành finding cho từng răng. Tổng kết và nút sửa/lỗi cần bổ sung trỏ đúng dòng nguồn; khi chưa có finding, trỏ nút Thêm vùng khám. Để trống không tự điền Bình thường; ca bình thường tiếp tục có thể chủ động chọn Chưa phát hiện bất thường theo quy tắc nhiều chẩn đoán. Không đổi DTO, backend validation, version, audit hoặc dữ liệu lịch sử.

## Hợp đồng và lưu trữ

Giữ API/Visit/requestKey/expectedVersion/phiên bản hiện tại. Finding bổ sung `surfaces?: string[]` với tối đa 5 giá trị duy nhất O/B/L/M/D: Nhai hoặc cắn, Ngoài, Trong, Gần, Xa. Chỉ nhận bề mặt khi có tooth hợp lệ. Dữ liệu cũ thiếu surfaces được đọc như chưa ghi bề mặt. ContentJson và snapshot audit lưu trường này, không tạo schema/engine odontogram riêng và không cần migration. Server kiểm whitelist, giới hạn findings và quyền hiện tại; xóa dữ liệu chỉ qua thao tác người dùng trên đúng phiếu.

## Kiểm chứng

Build web; API thật/schema PostgreSQL riêng kiểm surfaces hợp lệ/sai và đọc lại; trình duyệt kiểm 32/20/52 số FDI, nhiều phát hiện cùng răng, chọn nhiều, đồng bộ 2D/3D, click mesh đúng FDI, xoay/đặt lại, WebGL fallback, lưu/mở lại, phiếu readonly, đổi Visit không giữ lựa chọn cũ và ca tổng quát không chọn răng. Dữ liệu fixture cách ly khỏi phiên người dùng. Chỉ báo hỗ trợ 3D sau khi kiểm canvas WebGL và tương tác thật, không gọi CSS phối cảnh là mô hình 3D.

### Thay hình học 3D ngày 27/10/2026

Thay thân răng dạng cầu và chân răng hình nón bằng mesh liên tục có hình dáng riêng cho răng cửa, răng nanh, răng tiền hàm và răng hàm. Có mặt cắn/múi/rãnh minh họa, thân răng thu ở cổ, nướu hồng ôm cung hàm và che chân răng. Sắp răng theo chiều rộng trên cung hàm, giảm khe hở giả, chỉnh hướng răng theo tiếp tuyến cung. Phối cảnh mặc định thể hiện hai hàm và nướu; ánh sáng mềm để thấy mặt răng. Đây vẫn là mô hình minh họa, không phải scan hoặc hình thái cá nhân; không thay dữ liệu/API hay hành vi click chọn/bỏ chọn. Kiểm tra ảnh render 3D thực tế cùng kiểm thử raycast, toggle và lưu/mở lại.

### Mesh theo từng loại răng và chế độ xem riêng — 27/10/2026

Thay crown mesh tổng quát bằng bộ mẫu răng riêng của University of Dundee, School of Dentistry, kiểm tra license/source từ metadata từng GLB và API Sketchfab. Chỉ nhập các mẫu CC BY 4.0; không nhập mô hình hàm có CC BY-NC. Asset tải và phục vụ cục bộ, ghi nguồn/phiên bản/SHA-256 cùng mô tả thay đổi; có attribution trong giao diện và file đi kèm.

Có 16 mẫu răng vĩnh viễn (8 hàm trên/8 hàm dưới) và 10 mẫu răng sữa (5 hàm trên/5 hàm dưới), phản chiếu có kiểm soát sang bên đối diện để ánh xạ 52 số FDI. Không tuyên bố đây là 52 scan riêng, hình thái bệnh nhân thật hoặc mô hình đã được bác sĩ thẩm định. Giữ riêng mẫu răng cửa giữa/bên, nanh, tiền hàm thứ nhất/thứ hai, răng hàm thứ nhất/thứ hai/thứ ba và răng sữa; không chỉ thu nhỏ một mẫu chung. Bản toàn hàm tối ưu dung lượng/mesh, bản xem riêng giữ chi tiết cao hơn.

Ngoài xem toàn hàm, có điều khiển Xem riêng răng chọn theo số FDI/tên răng, xoay/phóng to từng răng và trở lại toàn hàm. Xem riêng hiện đầy đủ thân/chân của mẫu, giữ nguyên hình học gốc sau chuẩn hóa và thể hiện nguồn minh họa. Thao tác chọn răng để xem riêng độc lập với tập răng đang chọn ghi kết quả: không làm dirty, không tự thêm/xóa finding. Cả hai chế độ tiếp tục click để chọn thêm/click lại để bỏ chọn, cùng dữ liệu 2D, bộ FDI và quyền readonly. Không tự gán số chân/ống tủy hay đo đạc lâm sàng từ mẫu.

Tải bất đồng bộ có trạng thái tiến trình/lỗi và đường về 2D; bỏ qua kết quả tải cũ khi đổi bộ răng hoặc rời màn. Giải phóng geometry/material/texture/renderer khi bỏ view; không gửi dữ liệu bệnh nhân ra ngoài. Kiểm tra trực quan toàn hàm và từng răng từ nhiều góc, các số FDI, tải lỗi, WebGL fallback, chọn/bỏ chọn/lưu/mở lại và ca tổng quát cũ.

Môi trường Vite development phải khai báo trước Three.js/OrbitControls/GLTFLoader trong optimizeDeps.include. Mở 3D lần đầu không được kích hoạt tối ưu dependency mới rồi reload toàn trang, vì sẽ làm mất bản nhập khám chưa lưu. Production tiếp tục tải chunk 3D khi cần.

Ở chế độ xem riêng, giữ màu men/chân răng để nhìn chi tiết, không phủ màu kết quả lên toàn bộ mẫu; trạng thái chọn vẫn hiển thị qua số FDI và tập đang chọn. Có góc nhìn nhanh chính diện, mặt nhai/cắn và mặt bên cho răng đang xem; các góc này không thay đổi dữ liệu khám hoặc lựa chọn.

### Nhãn thao tác ngay tại răng — 27/10/2026

Bỏ select Răng xem chi tiết. Bấm/chạm mesh 3D hiện nhãn nổi cạnh vị trí răng: số FDI, tên răng, nút Xem riêng và nút đóng. Bấm răng vẫn chọn thêm/bỏ chọn như trước nếu phiếu được sửa; bấm Xem riêng chỉ đổi chế độ xem, không toggle lựa chọn lần nữa hoặc tạo finding. Phiếu readonly vẫn bấm mesh để xem nhãn và xem riêng, không sửa tập chọn/kết quả.

Nút FDI khi đang xem 3D mở cùng nhãn tại răng tương ứng; không có danh sách select thay thế. Canvas hỗ trợ Tab/Arrow để xem nhãn theo răng và Enter/Space theo thao tác chọn; readonly vẫn mở nhãn bằng bàn phím. Nhãn nằm trong viewport, hiển thị bên trên/dưới để không che điểm vừa bấm. Đóng bằng nút, Escape, bấm ngoài, bắt đầu xoay hoặc đổi bộ răng/view; Xem toàn hàm và các góc nhìn riêng giữ nguyên. Đổi răng/bỏ chọn không xóa kết quả đã ghi. Kiểm tra click mesh/FDI, nhãn đúng răng, chọn nhiều, hành động xem riêng không đổi lựa chọn, readonly, đóng nhãn và lưu/mở lại trên API thật.
