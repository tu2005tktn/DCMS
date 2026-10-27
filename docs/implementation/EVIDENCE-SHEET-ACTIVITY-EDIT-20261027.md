# Sửa/xóa quá trình và chuyển hàng vào chế độ sửa — 27/10/2026

- Backend solution build đạt (0 warning/error); Web build đạt (cảnh báo bundle size/SignalR hiện có).
- 9/9 Playwright `patient-treatment-ui.spec.ts` đạt với API giả lập. Kiểm Thực hiện mở inline mà chưa write/gạch ngang; × hủy và ✓ lưu, giữ dữ liệu đã sửa; actual có nút sửa/xóa và không có Xem nguồn; hủy sửa không ghi, sửa/reload giữ dữ liệu, xóa hỏi Đồng ý/Không đồng ý; các ca lỗi/nhãn/finance mock/responsive 390/768/1440 hiện có tiếp tục đạt.
- 16/16 integration test đạt: TreatmentSheetV25Tests (11), ClinicalWorkApiTests (3), TreatmentModelTests (1), TreatmentPaymentMigrationTests (1). Chạy trên database PostgreSQL tạm riêng `dcms_sheet_edit_test_*`, được xóa trong finally; không tạo dữ liệu thử trong dcms_dev.
- Ca API mới: transfer từ dữ liệu đã sửa giữ kế hoạch gốc, giá/giảm trừ thực tế và outbox đúng, retry một activity, không cần draft Actual trung gian; sửa/xóa có expected dossier/activity/sheet versions, retry chống trùng, stale bị chặn, wrong dossier/assistant bị từ chối. Projection sửa/xóa tồn tại qua GET nhưng actual Posted/giá/Version gốc giữ nguyên; đề xuất chỉnh phí có stable source key, không ghi charge/payment/receipt.
- Không có migration mới. Bản điều chỉnh lưu TreatmentEvent; chỉ mở rộng DTO/command và projection phiếu. Ledger consumer chưa được triển khai trong đợt này, các thay đổi phí chờ xử lý.
- Không chạy Git, commit hoặc push trong đợt này. Tài liệu và các tệp khác có sẵn trong workspace chưa được kiểm tra bằng Git theo quy định duyệt từng lệnh.
