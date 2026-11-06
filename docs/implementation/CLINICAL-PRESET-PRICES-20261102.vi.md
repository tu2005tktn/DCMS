# Giá khám và CLS thiết lập sẵn — 02/11/2026

Theo yêu cầu người dùng, chỉ chỉnh giá khám/cận lâm sàng; **không sửa phần điều trị của Vinh**. Đặc tả được cập nhật trước implementation tại [CLINICAL-PRESET-PRICES.vi.md](../specs/CLINICAL-PRESET-PRICES.vi.md), cùng EXAMINATION-BILLING và INVESTIGATION-BILLING.

## Hành vi

- `/clinical-services`: nhập một giá cụ thể cho dịch vụ khám và từng dịch vụ CLS. Trường giá bắt buộc ở form và hai nhóm API catalog; thiếu/null trả MSG02 `field=priceVnd`, giá âm/vượt giới hạn bị từ chối. Giá 0 được ghi rõ miễn phí theo bảng.
- Các dòng legacy chưa có giá hiển thị “Chưa thiết lập giá”; không tự gán 0 hoặc seed giá thực tế. Không cần migration/backfill.
- Chọn dịch vụ CLS tự điền đơn giá; nếu chỉ có một dịch vụ phù hợp đã thiết lập thì chọn sẵn. Hiện riêng giá bảng, đơn giá áp dụng, đơn vị và thành tiền. Dòng chưa có giá không cho chọn để lập phí; API cũng chặn việc truyền giá trực tiếp để bỏ qua cấu hình.
- Phí khám tự điền giá danh mục khi kết thúc lượt. Legacy null giữ Pending khi kết thúc lâm sàng, không tạo khoản phí 0; muốn chốt phí sau phải thiết lập giá danh mục trước.
- Giá khác bảng theo từng ca vẫn dùng quyền/căn cứ sẵn có, hiển thị rõ; không thay chính sách giảm trừ của điều trị. Miễn phí đúng bảng CLS không buộc nhập lại lý do. Giá/version đã snapshot vào chỉ định/charge/phiếu thu không bị tính lại khi sửa danh mục.

## Kiểm tra cuối

| Kiểm tra | Kết quả |
| --- | --- |
| .NET 10 restore + solution build Release | Đạt, 0 lỗi; còn NU1902/NU1903 Pkcs 9.0.0 đã có |
| Web production build | Đạt; còn warning SignalR annotation và bundle lớn |
| API/PostgreSQL | **53/53 đạt**: ClinicalPresetPriceApiTests, FinanceApiTests, ExaminationBillingApiTests |
| Browser mock | **13/13 đạt**: 4 giá thiết lập sẵn, 6 optional prescription, 3 in phiếu thu |
| Browser API/PostgreSQL thật | **5/5 đạt**: giá khám → ghi phí → thu một phần/chuyển khoản; CLS cấu hình 200.000 → chọn tự điền → thực hiện → thu/đối soát; CLS thuê ngoài/cost privacy; 2 nhánh có/không kê đơn |

API kiểm tra thiếu/null/âm/vượt giới hạn, 0 và giá dương, retry, legacy null không cho chốt bằng giá client, kết thúc khám Pending, snapshot cũ bất biến, scope/cash/transfer. Fixture legacy dùng ghi trực tiếp **chỉ trong schema integration riêng** để dựng dữ liệu cũ null; không dùng API mới để tạo dữ liệu không còn hợp lệ và không bỏ assertion business.

Browser thật chặn lưu form CLS thiếu giá, lưu giá cụ thể qua UI, chọn chỉ định đọc lại đúng đơn giá và kiểm tổng thu/còn lại bằng API. Test dùng schema synthetic `dcms_reception_e2e_84a40bda31554e048d55d2fba6b2419d`, API 5084/web 5173. API chạy bản Release mới; không migrate/seed database development. Một lần khởi động lại cấu hình chưa nhận Search Path đã gây lỗi đăng nhập do schema cũ thiếu cột; đã dừng, sửa bằng `set_ConnectionString/get_ConnectionString`, xác nhận schema trước khi chạy lại và lấy kết quả 5/5 cuối. Không dùng lượt lỗi môi trường đó để kết luận lỗi chức năng.

`git diff --check` đạt. Đã đối chiếu không có thay đổi dưới `backend/src/DCMS.Infrastructure/Features/Treatment` hoặc `web/src/features/treatment`; các thay đổi local có từ trước (kể cả tài liệu demo, bản sửa in phiếu thu và optional prescription) được giữ nguyên. Các bài auth hoặc test cũ ngoài nhóm này không được coi là đã sửa/đạt từ kết quả trên.

Bằng chứng ignored tại `.artifacts/verification-20261102/`: `clinical-prices-api.trx` / `.log`, `clinical-prices-browser.json` / `.log`, `clinical-prices-real.json` / `.log` và output/screenshot tương ứng.
