# Tích hợp main 53d3e2e vào workspace khám

Giữ toàn bộ thay đổi tracked/untracked trong stash `875a89ad3c921f8fae857d7088f86aa6984d59c8`, manifest tại `.git/codex-main-pull-backup.json`. Fast-forward nhánh hiện tại từ 77092e6 tới main 53d3e2e, không push.

Phần thiết lập chung/danh mục mới của TriNM và module S58 phân quyền/danh mục chẩn đoán đang làm có hợp đồng khác nhau nhưng trùng URL. Tích hợp giữ API local `/api/settings` và `/api/reference-options`; API thiết lập mới chuyển sang `/api/system-settings/settings` và `/api/system-settings/reference-options`. Cập nhật các client mới gồm thiết lập chung, danh mục tham chiếu/vật tư và form bệnh nhân. Không hợp nhất ngầm hai kho danh mục khác schema.

Quyền đăng nhập tiếp tục lấy từ resolver S58 hiện tại; quyền settings.general.manage của main chỉ bổ sung cho vai trò ClinicOwner được gán trực tiếp. Giữ các entity mới trong snapshot cùng entity khám/CLS/finance/access group hiện tại. Bản sao lưu stash được giữ lại sau áp dụng.

Hai module cũng trùng tên bảng REFERENCE_ENTRY nhưng khác cấu trúc. Thêm migration trước migration danh mục mới để đổi tên bảng local sang OPERATIONAL_REFERENCE_ENTRY, giữ nguyên dữ liệu/ID và đổi tên khóa/chỉ mục. Không chỉnh sửa migration đã chia sẻ trên main.

## Kiểm chứng hoàn tất

- HEAD = origin/main = 53d3e2e; ancestry đạt, không còn unmerged paths. Không commit hoặc push các thay đổi local.
- Trong 599 đường dẫn ban đầu: 588 giữ nguyên (kể cả chuẩn hóa CRLF), 9 được ghép có chủ đích với main, 2 file v2.4 đã bị xóa từ trước vẫn giữ trạng thái xóa. Không mất file ngoài dự kiến. Stash sao lưu vẫn còn.
- Backend Debug/Release và frontend production build đạt. EF has-pending-model-changes báo không có thay đổi mô hình chưa migration.
- 48/48 API regression (SettingsApiTests, PatientApiTests, ExaminationBillingApiTests) đạt. Kiểm thử tương thích bổ sung 1/1 đạt: cả hai module thiết lập, form-policy, options bệnh nhân có route độc lập; quyền Owner/Dentist được kiểm tra; danh mục chẩn đoán và tiền sử ở bảng riêng.
- Đã cập nhật bản chạy manual bằng migrate-only. Số lượt/phiếu khám/tài liệu không đổi. Toàn bộ dòng danh mục vận hành và sổ phí đối chiếu count + hash trước/sau migration giống nhau. API/database healthy 5084, web HTTP 200 tại 8084.
- Log: `.artifacts/main-53d3e2e-{build,web-build,tests,compatibility,model-check,runtime}.log`. Cảnh báo Pkcs và kích thước bundle tồn tại, không chặn build.
