# Thiết lập hệ thống: Dụng cụ / Vật liệu

## Phạm vi đã chốt 27/10/2026

Danh mục dùng chung toàn hệ thống. Staff active có role hiệu lực Dentist được CRUD; ClinicOwner hiện có Dentist hiệu lực theo AccountRoles.Effective nên được dùng, Admin/Assistant không có Dentist thì không được tự cấp quyền. Không giới hạn branch. Danh mục một cấp; mỗi mục có tên và đúng một nhóm. Không kho, lô, hạn dùng, giá/đơn vị/số lượng, tiêu hao hoặc nối trường Material điều trị.

## UI

Menu Thiết lập hệ thống /system-settings hiện khi có ít nhất một nhóm được phép theo `GET /api/settings/capabilities` (cập nhật 27/10/2026, xem [S58 Thiết lập chung](S58-GENERAL-SETTINGS.md)); hai tab Dụng cụ / Vật liệu và Danh mục chỉ Dentist hiệu lực, API của chúng chặn 403. Hai tab Dụng cụ / Vật liệu và Danh mục. Bảng mục: STT/Tên/Thuộc danh mục/Thao tác, tìm tên + lọc nhóm, phân trang 20/50/100. Bảng nhóm: STT/Tên danh mục/Số mục/Thao tác, tìm tên, phân trang. Modal thêm/sửa: name bắt buộc tối đa160, categoryId bắt buộc với mục; dropdown có tìm kiếm, tải nhóm phân trang từ API (không cắt ở 20). Nhóm trống hướng dẫn chuyển tab tạo nhóm. Chỉ nút Lưu gửi command; đang gửi chặn lặp, validation/mạng/stale giữ form. Đọc lại phiên bản giữ tên/người dùng đang nhập. Xóa có Đồng ý/Không đồng ý. Loading/empty/error/retry/401/403 và responsive theo Ant Design hiện có.

## API

GET /api/system-settings/material-categories?q=&page=1&pageSize=20 -> {items:[{id,name,itemCount,version}],total,page,pageSize}.
GET /api/system-settings/materials?q=&categoryId=&page=1&pageSize=20 -> {items:[{id,name,categoryId,categoryName,version}],total,page,pageSize}.
GET mỗi nhóm/{id} đọc phiên bản mới cho form conflict. POST {name,categoryId? (chỉ mục),requestKey}; PUT /{id} thêm expectedVersion; DELETE /{id} body {expectedVersion,requestKey}. POST/PUT trả DTO đọc lại sau lưu; DELETE trả DTO đã xóa. IDs Guid. GET các ID đã xóa trả404.

Tên Trim và Unicode FormC, so trùng theo UpperInvariant sau Trim, không bỏ dấu. Tên nhóm duy nhất giữa nhóm chưa xóa; tên mục duy nhất trong nhóm chưa xóa. Cùng tên ở nhóm khác hợp lệ. Nhóm không còn hiệu lực không nhận mục mới/chuyển tới. Xóa mềm, nhóm còn mục chưa xóa chặn; mục đã xóa không chặn xóa nhóm. Đổi nhóm giữ item ID. DB FK Restrict và unique filtered indexes giữ toàn vẹn.

Mỗi command kiểm tài khoản/role hiện hành trên server, transaction và khóa advisory riêng của module dùng chung cho mọi mutation: bảo vệ cạnh tranh xóa nhóm/tạo-chuyển mục, check name/version/parent cùng commit. RequestKey bắt buộc; chung key/cùng command+payload+actor replay DTO gốc kể cả sau delete; khác actor/command/payload MSG111. Receipt/audit một bảng chung lưu requestKey, actor, command, resourceId, fingerprint, before/result JSON, time. Không audit PHI hay secret. Read không tạo receipt.

Lỗi kế thừa shape SystemMessage: MSG04 validation + field; MSG06/403 quyền; MSG111/409 stale/reused key; MSG113/404 nguồn không có; MSG144/503 lỗi dịch vụ. CATALOG_NAME_DUPLICATE/409/field=name: Tên đã tồn tại. Vui lòng nhập tên khác. CATALOG_CATEGORY_IN_USE/409: Danh mục đang có dụng cụ/vật liệu. Vui lòng chuyển hoặc xóa các mục trước. Hai code tên riêng được ghi trong đặc tả này, không tự gán số MSG mới.

## Dữ liệu và rollout

Hai entity MaterialCategory và MaterialCatalogItem, ID/name/normalized/version/isDeleted/createdAt/updatedAt; item.categoryId FK. SystemCatalogChange dùng chung receipt/audit. Thêm migration/snapshot, không seed hoặc sửa điều trị hiện có. Migration vào dcms_dev cần người dùng duyệt riêng. Không Git nếu chưa có duyệt đúng từng lệnh.

## Nghiệm thu

Backend PostgreSQL cách ly: CRUD/reload/global across branch/rights active+roles, trim/case duplicate, dài/rỗng, parent deleted/missing, chuyển nhóm, nhóm nonempty, soft delete/unique reuse, stale/concurrency/retry key, race tạo hoặc chuyển mục với xóa nhóm, snapshot model/migration. UI API mock: tabs/menu/direct URL roles, forms/search/filter/pages, đầy đủ nhóm khi >20, no-group guide, confirm/cancel deletion, duplicate/MSG111/network giữ form, submit double lock, mobile 390/768/1440. Build backend/Web. Không tạo dữ liệu thử trong dcms_dev.
