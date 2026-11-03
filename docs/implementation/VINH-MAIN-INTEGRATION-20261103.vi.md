# Tích hợp main của Vinh — 03/11/2026

## Đích và hành vi cần giữ

Merge `origin/main` tại `aebf19d` (PR #40 và phần PR #39 trước đó) vào nhánh `tư`, trên commit phân quyền/lâm sàng `b962c92`. Giữ thay đổi local có sẵn trong tài liệu demo bằng stash riêng, không ghi đè hoặc đưa nó vào merge commit.

Nguồn nghiệp vụ là các đặc tả mới trên main: `TREATMENT-ITEM-PLANS`, `TREATMENT-SHEET-COPY`, `TREATMENT-SHEET-IMAGES`, `TREATMENT-DOSSIER-PAYMENTS`, `TREATMENT-NEXT-VISIT-BOOKING`, `CUSTOMER-FAMILY-MEMBERS` và các thay đổi khám/tài liệu. Không tự đổi quy tắc kế hoạch, giá, thanh toán hoặc lịch hẹn của Vinh khi xử lý xung đột.

## Hợp đồng tích hợp phân quyền

- Giữ quyền động và scope đã triển khai; endpoint mới phải có ánh xạ rõ ràng, không nới fallback mặc định. Người thân/dashboard khách hàng giữ auth Public và ownership riêng.
- Kế hoạch dùng lại mới tại `/treatment-templates` dùng `treatment.templates.read/manage`; danh mục bước cũ giữ API và hướng chuyển trang của main.
- Sao chép/áp kế hoạch vào phiếu cần `treatment.items.create`; sửa ngày dự kiến cần `treatment.items.update`. Các kiểm version, requestKey, nguồn cùng chi nhánh và snapshot của main vẫn áp dụng.
- Thanh toán theo phiếu cần `finance.read` để đọc và `finance.payments.create` để lập, ngoài policy nha sĩ/chi nhánh của main. Giữ nguyên việc Cash và Transfer đều tính đã trả ngay trong luồng mới; không đồng nhất với xác minh chuyển khoản của Finance theo lượt khám.
- Đọc gợi ý lịch buổi kế tiếp dùng `treatment.read`; tạo/chỉnh chỉ định theo kế hoạch dùng `treatment.progress.manage`, đặt Appointment thực tế vẫn cần quyền lịch hẹn tương ứng. Không buộc màn Lịch hẹn phải tải dữ liệu điều trị khi thiếu quyền.
- Ảnh trực tiếp của phiếu vẫn dùng `documents.read/upload/review`, với ClinicalDocument.VisitId nullable theo migration của main. Quyền lâm sàng, patient/dossier/branch vẫn kiểm phía server.
- Giữ kê đơn tùy chọn, lịch sử khám trong hồ sơ, giá khám/CLS cụ thể và trình bày phiếu thu từ commit local.

## Kiểm chứng dự kiến

Build backend/web; kiểm inventory endpoint, các ca mới family/item-plan và hồi quy phân quyền, khám/tài liệu, phiếu điều trị, thanh toán và lịch hẹn liên quan. Integration dùng schema test riêng. Không áp migration lên database development mặc định trong bước pull này; migration của main yêu cầu thao tác riêng theo đặc tả hiện có.

## Kết quả

- Đã giải quyết 9 file xung đột, giữ UI/contract mới và các điểm kiểm quyền hiện có. `TemplatePage` cũ được thay bằng trang kế hoạch dùng lại theo main; không khôi phục giao diện đã bỏ.
- Backend Debug/Release và web production build: đạt, 0 lỗi. Còn cảnh báo package Pkcs và bundle web lớn như trước.
- API PostgreSQL: 403/403 hồi quy, cộng 24/24 ca phân quyền được chạy lại sau bổ sung kiểm thanh toán độc lập với sửa lâm sàng; tổng **404 ca độc nhất**. Hai nhóm có 23 ca trùng, không cộng thô. Loại Google/Registration khỏi lượt này như kiểm tra phân quyền trước đó.
- Unit test: **85/85**. Browser mock: **94 ca độc nhất đạt** qua lượt tổng và retest. Đã cập nhật fixture quyền cho UI kế hoạch/lịch/điều trị; assertion Đã trả/Còn nợ dùng thanh toán theo phiếu mới; bộ chọn vật liệu chờ response tìm kiếm thay vì bấm vào kết quả đang đổi; test legacy dùng URL cấu hình và mock đầy đủ session/preview/history.
- Runtime Release tại 5084/5080, web 5173: API/database healthy, web trả 200. Đọc kế hoạch mới và gợi ý lịch qua API thật đều trả 200. Sáu migration mới đã áp và đọc lại trong schema synthetic `dcms_reception_e2e_84a40bda31554e048d55d2fba6b2419d`; không migrate/seed `dcms_dev`.
- Bằng chứng ignored: `.artifacts/verification-20261102/vinh-merge-*.log`, `*.trx`, JSON Playwright/aggregate và `vinh-merge-runtime-proof.json`. Không coi browser mock là bằng chứng toàn luồng runtime thật; kiểm tra runtime ở đây là health và API đọc mới.
- Tài liệu demo local được khôi phục từ stash riêng, đối chiếu SHA-256 trước/sau. Không tự push merge lên GitHub trong thao tác pull này.
