# Cận lâm sàng, ghi phí và thanh toán — 27/10/2026

Đặc tả áp dụng: [INVESTIGATION-BILLING](../specs/INVESTIGATION-BILLING.vi.md), bổ sung luồng S19 và tài chính của bộ v2.5. Không sửa tệp nguồn trong Downloads hoặc bản đồng bộ `sources/`.

## Đã nối

- Dùng cùng SchedulingService cho danh mục dịch vụ, giá tham chiếu và phiên bản; phân biệt dịch vụ CLS với dịch vụ đặt Khám. Không seed giá thực tế. `/clinical-services` hỗ trợ danh mục và đơn vị thực hiện bên ngoài theo quyền.
- Chỉ định có bốn hình thức: tại phòng khám; thuê ngoài nhưng phòng khám thu; gửi ngoài bệnh nhân tự trả; dùng kết quả đã có. Dịch vụ có số lượng, giá chốt và căn cứ khi đổi giá/miễn phí; thuê ngoài thêm provider và chi phí chốt.
- Lập phí chưa tạo công nợ. Xác nhận đã thực hiện tạo một FinanceCharge có nguồn duy nhất; thuê ngoài tạo một ProviderPayable riêng trong cùng transaction. Nhận kết quả và bác sĩ đọc kết quả giữ luồng độc lập.
- Hủy chỉ định chưa thực hiện không sinh phí. Hủy chỉ định sau thực hiện không tự đảo charge, payable hoặc phiếu thu. Không ghi phí hồi tố cho các chỉ định cũ chưa phân loại.
- Sổ dùng chung FinanceCharge và account version theo Patient/branch. IClinicalChargePort dùng cùng ledger, nhận đúng posting actor, chỉ ghi trong transaction và Visit thực hợp lệ. Những cổng đồng ý điều trị/tóm tắt tài chính toàn kế hoạch còn thiếu vẫn giữ ranh giới hiện tại.
- `/payments` có hàng chờ công nợ, chuyển khoản chờ và các chỉ định đang khám chưa xử lý phí/thực hiện; khoản dự kiến không nằm trong tổng công nợ. `/visits/{visitId}/payments` có phí, đã thu, còn phải thu, phân bổ theo dòng, lịch sử phiếu và drawer dịch vụ.
- Tiền mặt ghi Posted ngay. Chuyển khoản tạo Pending, không tăng Paid, chưa cấp số phiếu; nhân viên đối chiếu mã giao dịch và ghi nội dung xác minh để Posted. Mã ngân hàng xác nhận không dùng lại trong cùng chi nhánh. Thu một phần được hỗ trợ; chặn vượt dư và phân bổ sai lượt/bệnh nhân.
- Pending có thể hủy với lý do; Posted không được sửa/hủy qua API này. Phiếu có người nộp, người lập/xác nhận, thời điểm và lịch sử phân bổ. Không mặc định người nộp bằng bệnh nhân nếu cấu hình S58 chưa bật.
- Cost/provider payable không trả trong DTO cho bác sĩ/lễ tân thiếu quyền xem chi phí. Chủ phòng khám xử lý thuê ngoài từ màn tài chính bằng quyền riêng, không tự nhận quyền sửa bệnh án/lễ tân.
- Ghi sổ có requestKey/fingerprint, optimistic versions, advisory transaction lock chung, audit và replay receipt; database unique constraints chống trùng nguồn và số phiếu. Snapshot không đổi theo danh mục về sau.
- Xác nhận phiếu khám không bắt thu tiền. Kết thúc lượt chặn dịch vụ đã lập phí còn chưa thực hiện; không bắt thanh toán hết để kết thúc lượt.

## Dùng thử

1. Người có quyền dịch vụ vào **Dịch vụ cận lâm sàng**, chọn chi nhánh, tạo kỹ thuật và giá thực tế. Giá trống là chưa cấu hình; giá 0 là miễn phí, cần căn cứ khi lập phí.
2. Bác sĩ trong lượt đang khám tạo chỉ định như trước. Bấm **Dịch vụ và phí**, chọn hình thức, dịch vụ; giá/đơn vị điền từ danh mục, số lượng mặc định 1.
3. Thuê ngoài: chủ phòng khám cấu hình đơn vị và chi phí trong màn tài chính. Chọn hình thức không thu tại phòng khám khi bệnh nhân tự trả bên ngoài hoặc dùng kết quả cũ.
4. Người có quyền **Xác nhận đã thực hiện — ghi phí** sau khi dịch vụ thực sự thực hiện. Không cần đợi bác sĩ đọc kết quả để ghi nhận việc đã làm.
5. Lễ tân/thu ngân vào **Thanh toán**, mở đúng bệnh nhân/lượt. Nhập người nộp, chọn tiền mặt/chuyển khoản, phân bổ số tiền lần này. Chuyển khoản cần thao tác **Xác nhận tiền vào** sau đối chiếu.
6. Bác sĩ nhận/đọc kết quả, hoàn tất khám và kết thúc lượt theo điều kiện lâm sàng. Giá trị đã thực hiện và tiền đã thu được hiển thị riêng.

Account đã gán nhóm tùy chỉnh cần được cấp các grant mới trong Nhóm quyền. Không tự mở thêm quyền cho nhóm cũ. Tài khoản chưa gán nhóm giữ fallback vai trò hiện hành.

## Migration và dữ liệu

Migration cộng thêm `20261027120927_ClinicalServiceBilling`: cột phân loại/snapshot và ledger/account/phiếu thu/phân bổ/payable/provider/audit/receipt sequence. Up không xóa bảng, không sửa số tiền cũ, không tạo phí tự động, không seed giá hoặc chứng từ. Giữ tệp và lượt khám đang có.

API/UI proof dùng schema PostgreSQL riêng và dữ liệu SYNTHETIC. Giá test là dữ liệu kiểm thử, không đưa vào dữ liệu người dùng. Antivirus trong bộ kiểm thử khám là fixture; API chạy tay vẫn dùng ClamAV thật theo cấu hình trước đó.

## Kiểm chứng

- Backend build và web TypeScript/Vite build đạt. Các cảnh báo NU1902/NU1903 về Pkcs 9.0.0 và cảnh báo bundle Vite là cảnh báo nền hiện có, không được coi là đã sửa trong thay đổi này.
- API/PostgreSQL thật: **171/171** đạt, gồm FinanceApiTests (23), ClinicalWorkApiTests và hồi quy khám, tiếp nhận, thiết lập/quyền, tài khoản và thông báo/yêu cầu hỗ trợ. Có kiểm tra concurrent perform, replay đổi payload, branch/group revocation, NULL/0/đổi giá, source snapshot, thu một phần, chuyển khoản chờ, stale balance, mã ngân hàng trùng, vượt dư và cost privacy.
- Browser/API/PostgreSQL thật: **10/10 luồng khám** đạt và **2/2 luồng tài chính** đạt. Luồng xét nghiệm tạo danh mục trên UI → chỉ định → phí → xác nhận thực hiện → tiền mặt 50.000 + chuyển khoản chờ 150.000 → đối chiếu, tổng Paid 200.000. Luồng chủ phòng khám vào hàng chờ tài chính → thuê ngoài phim 250.000 → payable 70.000 riêng, bác sĩ/lễ tân không nhận DTO chi phí.
- Dữ liệu manual: migration trên **public** hiện dùng, giữ schema lưu file cũ theo runtime metadata; xác nhận số Visit/VisitExamination/ClinicalDocument trước và sau không đổi. API 5084 `/health` báo API/database healthy, web 8084 HTTP 200. Không seed lại dữ liệu vận hành.
- Docker Desktop từng dừng giữa lượt kiểm thử; các lần lỗi kết nối/khôi phục PostgreSQL đã chạy lại sau khi database accepting connections. Không dùng các lượt bị gián đoạn làm bằng chứng đạt.
- Bằng chứng local: `.artifacts/finance-final-regression.log`, `.artifacts/finance-examination-browser.log`, `.artifacts/finance-browser-proof.log`, `.artifacts/finance-manual-api.log`, `.artifacts/finance-web-build.log`; ảnh `posted-payments.png`, `outsourced-service.png` trong `.artifacts/finance-browser/`. Không đưa artifact/credentials/dữ liệu bệnh nhân vào commit.

Bản chạy để thử: `http://localhost:8084`. F5 để tải menu và quyền mới.

## Phạm vi còn lại

Chưa cung cấp hoàn/đảo phiếu Posted, thu cọc/ứng trước không phân bổ, thanh toán nhà cung cấp, cổng ngân hàng tự động, in phiếu đầy đủ theo mọi tùy chọn S58 hoặc hóa đơn điện tử pháp lý. Khoản phải trả đơn vị chỉ là sổ phải trả, không phải bằng chứng đã chi. Tóm tắt tài chính toàn kế hoạch/gói điều trị chưa được thay bằng projection CLS đơn thuần. Phí khám tự thân chưa tự sinh từ việc bấm hoàn tất khám.
