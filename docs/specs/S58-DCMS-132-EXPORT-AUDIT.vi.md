# DCMS-132 — Excel theo quyền, lịch sử cấu hình, restore và kiểm thử tích hợp S58

Owner: Ngô Minh Trí. Ngày chốt: 01/11/2026. Nền: `main` dd50777, sau khi [gộp settings](S58-SETTINGS-MERGE.vi.md). Nguồn: ticket DCMS-132, [S58-SYSTEM-SETTINGS.vi.md](S58-SYSTEM-SETTINGS.vi.md) §2, §4, §9.

## 1. Đã có, tái dùng

| Phần | Nơi |
| --- | --- |
| Lịch sử và khôi phục Thiết lập chung | `GET /general/history`, `POST /general/restore-preview`, `POST /general/restore` (DCMS-119/122) |
| Lịch sử từng mục danh mục | `GET .../catalogs/{kind}/{id}/history`, `GET .../catalogs/{resource}/{id}/history` |
| Export thuốc | `GET .../catalogs/medicines/export`. Bộ ghi `Xlsx` ghi mọi ô dạng chuỗi (`inlineStr`) nên ô bắt đầu bằng `=`, `+`, `-`, `@` không thành công thức |
| Grant, nhóm quyền | `ISettingsAccess`, `SettingsGrants` |

Mọi route bên dưới có tiền tố `/api/system-settings`.

## 2. Shell theo quyền (SET-AT-01)

`GET /settings/capabilities` trả `{ sections: [{ key, canManage }], catalogKinds: string[] }`.

| Section | Điều kiện |
| --- | --- |
| `general` | Owner (giữ như cũ) |
| `catalogs` | Owner, hoặc có ít nhất một grant `catalog.{kind}.read` |
| `supplies`, `materials` | Nha sĩ hiệu lực (giữ như cũ) |
| `audit` | Owner, hoặc có grant `settings.audit.read` |

- `catalogKinds` liệt kê các loại danh mục được xem.
  - Owner được cả 7 loại.
  - Người có grant chỉ được các loại `catalog.{kind}.read` tương ứng.
  - `customer-groups` chỉ dành cho Owner.
- Web chỉ hiện tab và menu loại danh mục theo `catalogKinds`.
- Gọi API của nhóm không được cấp vẫn trả 403 MSG06 (đã có), và không lộ `total`.
- Menu "Thiết lập hệ thống" hiện cho nha sĩ hoặc người có `settings.read`. Mọi grant thiết lập đều có grant cha là `settings.read`. Trước đây chỉ có `settings.general.manage` mới thấy menu này.
- Trong tab danh mục, nút Thêm, Sửa và Ngừng dùng chỉ hiện khi có grant `create`, `update`, `deactivate` tương ứng. Có quyền xem mà không có quyền sửa thì tab chỉ đọc.

## 3. Lịch sử thiết lập (SET-05, SET-AT-31)

`GET /settings/audit?section=&from=&to=&page=&pageSize=` thay route cùng tên đã chuyển sang khi gộp settings. Yêu cầu Owner hoặc grant `settings.audit.read`.

Tham số:
- `section`: `general | catalogs | supplies | materials | access-groups`, bỏ trống là tất cả các section được phép.
- `from`, `to`: ngày theo giờ phòng khám (UTC+07).
- Phân trang 20/50/100. Sắp xếp theo thời gian giảm dần, rồi theo `id`.

| Section | Nguồn | Phạm vi |
| --- | --- | --- |
| `general` | `SYSTEM_CATALOG_CHANGE`, lệnh `General*` và `PatientCodeSequence` | Owner |
| `catalogs` | Lệnh `Reference*` | Owner xem mọi loại; người có grant chỉ xem loại `catalog.{kind}.read` của mình |
| `supplies` | Lệnh `providers:*`, `lab-work-types:*`, `medicines:*` | Nha sĩ hiệu lực hoặc Owner |
| `materials` | Lệnh `Material*`, `Category*` | Nha sĩ hiệu lực hoặc Owner |
| `access-groups` | `SETTINGS_AUDIT` | Như DCMS-128: người không phải Owner chỉ thấy bản ghi thuộc chi nhánh của mình |

Mỗi dòng: `{ id, section, target, action, actorName, at, reason, before, after }`.
- `before` và `after` đã được làm sạch: bỏ mọi khóa có chứa `password`, `token`, `secret`, `bank` (không phân biệt hoa thường), ở mọi cấp.
- Bỏ khóa có chứa `cost` nếu người đọc không có `finance.costs.read`.
- Bản ghi lịch sử bất biến; API này chỉ đọc.

Web: thêm tab "Lịch sử thiết lập" gồm bộ lọc section và khoảng ngày, phân trang, bảng có thể mở rộng để so sánh trước/sau. Lỗi tải hiển thị khác với trạng thái "chưa có lịch sử".

Khôi phục (SET-AT-31) dùng restore đã có của Thiết lập chung: tạo version mới, kiểm lại theo ràng buộc hiện hành, không lùi sequence hay mã đã cấp. Danh mục khôi phục bằng ngừng dùng/khôi phục (state), không xóa cứng.

## 4. Excel theo quyền (SET-AT-30)

Grant export theo quy ước `catalog.{loại}.export` của lớp kiểm quyền endpoint (`ScreenEndpointPermissions`) trên `main` (Owner có sẵn):
- `catalog.services.export`: grant mới, cha là `settings.read`.
- `catalog.materials.export`: grant mới, cha là `catalog.materials.read`.
- `catalog.medicines.export`: đã có trên `main`.

Vai trò Nha sĩ có sẵn các grant `catalog.{materials|providers|lab-work-types|medicines}.*` theo `ScreenGrants`, gồm cả grant export. Nhân viên khác cần được giao grant export qua nhóm quyền.

Cập nhật 08/11/2026 khi merge `main`: tên cũ `settings.export.*` của bản đầu DCMS-132 được bỏ, không giữ hai bộ grant song song. Route `GET /materials/export` được khai báo riêng trong `ScreenEndpointPermissions` (cần `catalog.materials.export`), không rơi vào quyền đọc chung của `/material*`.

| Route | Quyền xem danh sách | Bộ lọc | Cột |
| --- | --- | --- | --- |
| `GET /settings/catalogs/services/export` | Owner xem mọi chi nhánh; nhân viên chỉ xem chi nhánh mình đang là thành viên | `q`, `status` (Active/Inactive/All) | Chi nhánh, tên, loại (khám đầu/cận lâm sàng/điều trị), đơn vị, thời lượng đặt lịch, cho đặt online, trạng thái. Thêm giá tham khảo và phiên bản giá **chỉ khi** có `finance.read` |
| `GET /materials/export` | Nha sĩ hiệu lực (như danh sách) | `q`, `categoryId` | Nhóm, tên |
| `GET /settings/catalogs/medicines/export` (đã có) | Nha sĩ hiệu lực | `q`, `status` | Như cũ |

- Mỗi route đòi **cả** quyền xem danh sách **và** grant export tương ứng. Thiếu một trong hai thì trả 403 MSG06, không trả file rỗng.
- File tối đa 10.000 dòng. Dùng cùng bộ lọc với danh sách. API không ghi dữ liệu.
- File không chứa thông tin đăng nhập, dữ liệu bệnh nhân hay chi phí nhà cung cấp.
- Mọi ô được ghi dạng chuỗi. Tên file có ngày theo UTC.
- Web: nút "Xuất Excel" ở danh sách thuốc và danh sách vật liệu, tải bằng cùng bộ lọc đang chọn, chỉ hiện khi có grant export (`/api/auth/me` permissions). Dịch vụ chưa có danh sách trong shell (thuộc S31), nên tạm chỉ có API.

## 5. Kiểm thử tích hợp 32 SET-AT

Kết quả ghi ở [S58-DCMS-132.vi.md](../implementation/S58-DCMS-132.vi.md). Mỗi AC ghi một trong các trạng thái sau, kèm bằng chứng:

| Trạng thái | Ý nghĩa |
| --- | --- |
| **Pass API** | Đã chạy với API và PostgreSQL thật |
| **Pass browser** | Đã chạy trên browser thật, API thật |
| **Mock** | Chỉ chạy với UI giả lập |
| **Blocked** | Chưa có tính năng hoặc API; ghi rõ chủ sở hữu |

Không ghi pass cho phần chỉ chạy với mock hoặc chỉ có tài liệu.
