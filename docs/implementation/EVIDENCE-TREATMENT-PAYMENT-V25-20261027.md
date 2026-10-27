# Bằng chứng Điều trị và Thanh toán v2.5 ngày 27/10/2026


## Đợt bổ sung nhập trực tiếp theo phiếu — 27/10/2026

- Backend build: đạt, 0 warning/error. Web build: đạt (Vite vẫn có cảnh báo bundle size/SignalR hiện có).
- 62/62 unit test đạt. 14/14 integration test đạt: TreatmentSheetV25Tests (gồm bốn ca mới), TreatmentModelTests, ClinicalWorkApiTests, TreatmentPaymentMigrationTests. Chạy trong database PostgreSQL tạm riêng `dcms_direct_test_*`, schema synthetic; database tạm được xóa trong finally. Không áp migration hoặc tạo bản ghi test trong dcms_dev.
- Ca mới kiểm nguồn ClinicianEntry không giả consent; actual VisitId null; chuyển Thực hiện nguyên tử/cạnh tranh/retry; adapter phí lỗi rollback actual/flag/receipts; giá/giảm trừ snapshot không đổi khi sửa nguồn; quantity cũ >1 giữ nguyên; xóa mềm vẫn còn actual; phút sai/nguồn khác dossier bị từ chối; assistant không được thao tác. Kiểm migration giữ dữ liệu cũ và cờ mặc định, snapshot EF khớp model.
- 8/8 test Playwright `patient-treatment-ui.spec.ts` đạt với API mock: không Vùng/SL/quyết định/filter, thời gian dự kiến nguyên, single-click, retry MSG111, Thực hiện/gạch ngang/reload, Hoàn tất chỉ đánh dấu, sửa giữ quantity, modal Xóa Đồng ý/Không đồng ý, actual không Visit, Đã trả unavailable, tài chính mock và responsive 390/768/1440 px.
- Sau khi người dùng đồng ý riêng, đã áp migration `20261027180000_DirectTreatmentSheet` vào dcms_dev; kiểm tra lịch sử migration xác nhận đúng ID. Không có migration khác đang chờ trước khi chạy. Không seed hoặc tạo hồ sơ thử trong database dự án. API đã khởi động lại tại localhost:5080 và Web localhost:5173 trả HTTP 200. Không Git/commit/push trong đợt này.
- Nguồn ledger thật vẫn chưa tích hợp; Đã trả không được suy từ giá kế hoạch. Dòng nguồn Dossier giá 0 không giả căn cứ Free; gói chưa có Q08 có trạng thái NeedsRule và không phát sinh yêu cầu phí gói chưa được xác thực.
- Sau bổ sung snapshot lịch sử, chạy lại 9/9 TreatmentSheetV25Tests trên database tạm riêng đạt; gồm ca actual legacy thiếu tên snapshot vẫn giữ tên dịch vụ cũ sau đổi catalog/sửa kế hoạch, không viết lại actual đã post.
- Đã xem ảnh form tại 1440 px và trang tại 390 px sau khi chỉnh lại độ rộng cột: desktop thấy đủ cột, mobile cuộn trong bảng; các ảnh dùng dữ liệu synthetic tại web/test-results/treatment-direct-entry-form.png và treatment-v25-*.png.
- Các kết quả bên dưới là lịch sử của các đợt trước, không thay thế hành vi nhập trực tiếp mới.

## Điều chỉnh xác nhận thủ công

Theo yêu cầu mới nhất, ✓ Đồng ý lưu trực tiếp trên hàng, không mở drawer hoặc bước xác nhận tiếp theo. Không tự lưu khi gõ. Quyết định, tư vấn và dữ liệu actual bắt buộc nằm ngay trên hàng; không giả dữ liệu đồng ý hoặc Visit. Web build và 7/7 Playwright `patient-treatment-ui.spec.ts` đạt; kiểm một lần bấm, không drawer, nhiều răng, Proposed không tự thành Agreed, retry MSG111 không lưu nháp trùng, actual giữ nguồn và ghi chú chỉ lưu bằng nút riêng. Responsive kiểm 390/768/1440 px không tràn viewport. Các kiểm tra backend dưới đây thuộc lượt v2.5 trước đó; lượt sửa UI này không thay backend hoặc database. Chưa đối chiếu ảnh pixel với HTML oDental cho toàn bộ màn hình.

## Kết quả đã chạy

- Backend solution build: đạt, 0 warning và 0 error ở lần build solution cuối; các dự án production được build lại thành công cùng lần integration test cuối.
- Unit tests: 62/62 đạt, gồm FDI răng vĩnh viễn/răng sữa, đơn vị/số lượng, vùng thực hiện là tập con của vùng đồng ý và quy tắc điều trị hiện có.
- Integration tests: 20/20 đạt. Filter: TreatmentSheetV25Tests, TreatmentModelTests, TreatmentDossierApiTests, ClinicalWorkApiTests, TreatmentRevisionApiTests, TreatmentPaymentMigrationTests.
- Database test là PostgreSQL database tạm riêng `dcms_v25_test_*`, fixture dùng schema synthetic. Mỗi lần chạy tạo và DROP database tạm trong finally; không tạo dữ liệu test hoặc áp migration vào dcms_dev.
- Web `npm run build`: đạt. Vite có cảnh báo chunk size và annotation SignalR hiện có; không có lỗi TypeScript.
- Playwright Chrome: 11/11 đạt trên API mock/preview: patient-treatment-ui (6), treatment-dossier, treatment-commercial, treatment-revision (2), clinical-preview.
- Đã mở ảnh tại 390/768/1440 px; bảng cuộn trong vùng riêng, header tab màn hẹp đọc được. Kiểm thử không tràn viewport và thao tác Enter, reduced motion. Ảnh synthetic ở `web/test-results/treatment-v25-*.png` là artifact kiểm tra, không phải dữ liệu bệnh nhân.

## Ca có bằng chứng

Chuyển nháp thành item với ID ổn định; cùng key/cùng payload replay; khác payload và hai quyết định đồng thời bị conflict; ước tính min20/selected25/max25 không cần reserved; snapshot không đổi khi catalog đổi giá; Agreed và Deferred không có cùng quyền thực hiện; carry-forward bước còn nguyên phạm vi có version/lịch sử.

Hai Visit giữ cùng dossier; Partial rồi hoàn tất item; post/retry có đúng một actual và một fee request cho mỗi nguồn; giảm trừ được chia có giới hạn, không áp lặp; đóng hồ sơ không kết thúc Visit hoặc ghi nhận tiền. Hold chặn post và rollback actual. Direct Free có thỏa thuận; Visit sai Patient/branch bị từ chối. Assistant không có clinical write, projection tài chính cần claim finance.read và không trả clinical notes/nháp.

Handoff S19 kiểm version Visit của hợp đồng hiện hành, lưu snapshot kết quả khám bất biến và không tạo Visit mới. Migration downgrade/upgrade trong schema test giữ giá/đơn vị item, tiến độ và version; cờ DisplayCompleted=true của dữ liệu cũ vẫn có State=Open, không tự đóng chuyên môn. EF snapshot khớp model.

UI giữ nội dung khi MSG111; nhập nhiều răng, tự lưu, quyết định, chọn Visit/xác nhận actual, bảng nhiều buổi, MSG243, unavailable finance, phân bổ MSG256 và receipt Pending trong mock. Bản xem chứng từ không bị mất khi refresh dữ liệu phiếu.

## Những điều chưa nghiệm thu

Không có ledger/consumer/dispatcher DCMS-101 trong task. S38/S39 chỉ là UI và hợp đồng; API finance/collections và các ca tiền thật AT63–70/thu nợ AT71 chưa nghiệm thu tích hợp. Không có xác nhận Labo authoritative thì bước yêu cầu Labo tiếp tục bị chặn. Không xác nhận quy tắc ghi phí gói Q08. Không có kiểm thử gửi thông báo ra ngoài.

Người dùng đã duyệt riêng việc áp migration và chạy API. Migration `20261027120000_TreatmentPaymentV25` đã áp thành công vào dcms_dev; đã đọc lại migration history để xác nhận. API http://127.0.0.1:5080/health và Web http://127.0.0.1:5173/sign-in đều trả HTTP 200. Không seed hoặc tạo dữ liệu thử trong dcms_dev. Chưa thực hiện thao tác Git trong lượt này.

Tệp test ReceptionApiTests có khai báo class/record/method bị lặp sẵn khiến test project không compile. Đã loại bỏ đúng các khai báo trùng; giữ nội dung các ca test và hành vi production của tiếp nhận. Đây là sửa prerequisite build, không phải thay đổi nghiệp vụ reception.
