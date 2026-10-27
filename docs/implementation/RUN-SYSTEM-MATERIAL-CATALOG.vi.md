# Chạy và kiểm thử Thiết lập hệ thống — Dụng cụ / Vật liệu

## Khởi động

Dùng cấu hình .env development hiện có, không chép mật khẩu/token vào tài liệu hoặc Git. Migration mới: `20261027200000_SystemMaterialCatalog`; chỉ chạy sau duyệt riêng. `powershell -NoProfile -ExecutionPolicy Bypass -File infrastructure/scripts/start-api.ps1 -Migrate` áp migration rồi thoát; tiếp tục chạy cùng script không có -Migrate để mở API 5080. Web dùng `npm run dev -- --host 127.0.0.1` trong web, port5173. Không cần --seed-demo cho module này.

## Kiểm thử thủ công

1. Đăng nhập tài khoản Staff có Dentist hiệu lực. Mở Thiết lập hệ thống hoặc /system-settings. Trang chỉ có hai tab Dụng cụ / Vật liệu và Danh mục.
2. Nếu chưa có nhóm, tab vật liệu hướng dẫn tạo trước. Sang Danh mục → Thêm mới → tên ví dụ nhóm thực tế của phòng khám → Lưu. Không tự lưu khi gõ.
3. Sang Dụng cụ / Vật liệu → Thêm mới: nhập Tên và chọn Thuộc danh mục bằng dropdown có tìm kiếm. Chỉ hai trường, không đơn vị/giá/tồn kho. Lưu và tải lại trang kiểm tra dữ liệu giữ nguyên.
4. Sửa tên hoặc chuyển nhóm rồi Lưu. Thử tên trùng trong cùng nhóm (không phân biệt hoa/thường và khoảng trắng ngoài) để nhận lỗi, form giữ nội dung. Cùng tên ở nhóm khác được phép.
5. Xóa nhóm còn mục → thông báo đang được sử dụng, nhóm giữ nguyên. Chuyển/xóa các mục rồi xóa nhóm rỗng. Xóa mục/nhóm có Đồng ý/Không đồng ý; xóa mềm, không xóa lịch sử.
6. Hai trình duyệt sửa cùng bản ghi: trình sau nhận MSG111; Đọc lại phiên bản, giữ nội dung rồi chọn lưu lại nếu phù hợp. Mất mạng giữ form, retry cùng nội dung dùng cùng requestKey.
7. Với Dentist ở chi nhánh khác vẫn thấy danh mục chung. Admin/Assistant không có Dentist không thấy menu và URL trực tiếp trả403; API kiểm lại quyền và active account, không dựa vào menu.
8. Kiểm tìm kiếm tên, lọc nhóm, phân trang20/50/100; dropdown tải mọi trang nhóm. Màn nhỏ cuộn trong bảng, trang không tràn ngang.

## Kiểm tra tự động

Backend build: `dotnet build backend/DCMS.sln --no-restore -p:UseSharedCompilation=false`. Integration filter `SystemCatalogApiTests|TreatmentModelTests|TreatmentPaymentMigrationTests`, dùng PostgreSQL database tạm riêng và dọn trong finally, không trỏ test vào dcms_dev. Web build trong web: `npm run build`; Playwright `tests/system-settings.spec.ts` dùng API mock. Không tạo danh mục giả trong database dự án để nghiệm thu.
