# Dùng luồng điều trị của Vinh

Ngày 28/10/2026, theo yêu cầu bỏ phần điều trị cũ/thêm riêng của workspace và dùng phần điều trị của Vinh. Baseline đối chiếu: commit `f8b2dcd` (vinh), đã có trong HEAD `53d3e2e`; các file điều trị của main không thay đổi sau commit này.

- Giữ phiếu điều trị inline TreatmentSheet trong tab Điều trị của bệnh nhân, direct actual, form kế hoạch/draft/confirmation, điều chỉnh kế hoạch, mẫu, giá/duyệt, ghi chú vật liệu và tài chính của Vinh.
- Bỏ widget Các buổi điều trị thêm riêng, service/API vòng đời buổi `/api/dossiers/{id}/visits`, các màn ActualTreatmentPage/PostoperativeReviewPage và preview lâm sàng riêng. Không giữ đường dẫn giao diện cho những màn đã bỏ.
- Bỏ phần mở rộng nguồn `SourceExaminationVisitId`/`SourceExaminationJson` khỏi contract tạo/đọc điều trị mới; chuyển bàn giao khám sang `SourceVisitId`/`ExpectedSourceVisitVersion` của Vinh. Giữ kiểm tra khám đã kết thúc trước khi chuyển UI; backend nguồn theo validation/version và audit snapshot của Vinh.
- Giữ nguyên storage/migration đã áp dụng cho nguồn khám cũ để không xóa dữ liệu/ID/snapshot lịch sử; không chạy Down, truncate hoặc seed lại. Các trường storage cũ không tiếp tục là luồng tạo mới. Không xóa bệnh nhân/phiếu/lịch sử/công nợ.
- Giữ các tích hợp phân quyền và phí khám/CLS hiện có ngoài module điều trị. Fee outbox của điều trị vẫn theo Vinh, không coi Pending là đã thu hoặc ghi trùng vào sổ khám/CLS.
- Xóa các kiểm thử chỉ dành cho màn/buổi cũ đã loại bỏ, giữ kiểm thử phiếu điều trị của Vinh và nguồn khám native. Kiểm build backend/web; API PostgreSQL schema riêng cho treatment sheet/direct actual/source; browser phiếu inline và bàn giao từ khám. Kiểm kê dữ liệu manual trước/sau không đổi.

## Kết quả kiểm chứng

- .NET restore/build và web production build đạt. Backend giữ API/core phiếu điều trị Vinh và các service chung mà phiếu đó sử dụng; không xóa core chỉ vì bỏ màn cũ.
- 86 ca API/PostgreSQL riêng: lượt đầu 85 đạt, 1 ca kiểm context còn kỳ vọng lệnh chặn của luồng buổi cũ. Cập nhật kỳ vọng theo Vinh: đọc context của Visit đang hoạt động được phép và không tạo dữ liệu điều trị; chạy lại ca đó đạt 1/1. Có kiểm immutable source snapshot theo SourceVisitId/ExpectedSourceVisitVersion của Vinh.
- UI phiếu điều trị của Vinh: 13/14 đạt sau khi cập nhật mock PatientDetail có intakeHistory theo contract S58 và chờ đăng nhập xong. Một lỗi chọn Composite A2 trùng tên khác nhóm tái hiện đúng ghi nhận trước ở MAIN-VINH-INTEGRATION-20261027; không sửa component/ép click để che lỗi, không báo toàn bộ UI đạt.
- Browser/API thật đọc phiếu đã có, mở TreatmentSheet inline; kiểm request tạo từ khám dùng đúng sourceVisitId/expectedSourceVisitVersion (POST được chặn trong kiểm UI, không ghi thêm hồ sơ); route actual/review/preview cũ hiển thị không tìm thấy. Snapshot backend nguồn khám được kiểm bằng bộ API riêng ở trên.
- Số bản ghi public trước/sau giữ nguyên: Visit 15, VisitExamination 13, ClinicalDocument 1, TreatmentDossier 3, ClinicalActivity 0. Không migrate/seed hoặc đổi bản ghi manual. API đã khởi động lại 5080 và /health trả API/database healthy; web tiếp tục 5173.
- Backup code local trước thay đổi nằm tại `.tools/treatment-before-vinh-only-20261028/`. Không commit/push.
