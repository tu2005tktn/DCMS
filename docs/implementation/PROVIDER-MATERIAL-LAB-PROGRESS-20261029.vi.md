# Trạng thái triển khai Provider/vật liệu/Labo — 29/10/2026

Chưa hoàn thành toàn bộ kế hoạch.

Đã sửa: quyền theo resource ở SupplyCatalogPermissions; Dentist quản lý providers/lab-work-types, không được medicines; capabilities mở supplies cho Dentist, Web ẩn thuốc với tài khoản không Owner. Thêm UnitConversionRules trong Domain, dùng BigInteger/scaled decimal để nhân chính xác, nhận chuỗi số dương, không exponent, kết quả vật liệu tối đa 3 số lẻ/thuốc nguyên; quy tắc chưa được nối vào API catalog.

Bằng chứng: dotnet build backend/DCMS.sln --no-restore đạt 0 warning/error; dotnet test DCMS.UnitTests filter SupplyCatalogRulesTests --no-build đạt 17/17. Không chạy test PostgreSQL/API/browser mới; chưa nghiệm thu SET-AT-15/16/17.

Còn cần triển khai: mở rộng MaterialCatalogItem/DTO/schema, Unit/conversion versioned persistence, migration/snapshot, typed material/category/unit API và facade legacy/state/history/receipt namespace, guards Provider hai chiều và dependency ports, Labo unit mapping/default resolver, UI vật liệu + category manager, tests PostgreSQL cách ly và browser mock/thật. Không được coi các phần này đã tồn tại chỉ từ đặc tả hoặc quy tắc Domain.

Không chạy Git hoặc migration development; không seed/tạo dữ liệu thử. Các thay đổi cục bộ trước task không được sửa.
