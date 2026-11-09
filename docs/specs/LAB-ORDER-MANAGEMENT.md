# Quản lý công việc labo

Ngày: 08/11/2026. Liên quan: [LAB-ORDER.md](LAB-ORDER.md) (phiếu labo lập trong quá trình điều trị), [LAB-CATALOG-SEED.md](LAB-CATALOG-SEED.md), [ALL-SCREENS-DYNAMIC-PERMISSIONS.vi.md](ALL-SCREENS-DYNAMIC-PERMISSIONS.vi.md).

## Mục tiêu

Màn **Quản lý công việc labo** (menu Chuyên môn › Quản lý công việc labo, route `/lab-orders`) cho nhân viên theo dõi mọi phiếu labo đã lập trong Quá trình điều trị/dịch vụ: phiếu nào chưa gửi, đang ở xưởng, đã nhận về, quá hạn hẹn, đã giao bệnh nhân hay phải làm lại. Phiếu vẫn được lập/sửa ở bảng điều trị như cũ (LAB-ORDER.md); màn này chỉ liệt kê, lọc, xem chi tiết và đổi trạng thái.

## Trạng thái và nhật ký

Thêm vào phiếu labo:

| Trường | Ý nghĩa |
|---|---|
| `status` | `NotSent` Chưa gửi (mặc định khi lập phiếu), `Sent` Đã gửi xưởng, `Received` Đã nhận về, `Delivered` Đã giao bệnh nhân, `Rework` Làm lại |
| `sentOn`, `receivedOn` | Ngày gửi / ngày nhận về (đặt tự động khi chuyển trạng thái, giờ Việt Nam). `deliveredOn` đã có sẵn. |

Chuyển trạng thái hợp lệ: `NotSent → Sent`; `Sent → Received | Rework`; `Received → Delivered | Rework`; `Delivered → Rework`; `Rework → Sent`. Chuyển khác → MSG04. Hệ quả: vào `Sent` đặt `sentOn` = hôm nay; vào `Received` đặt `receivedOn`; vào `Delivered` đặt `deliveredOn` (nếu trống); vào `Rework` xóa `receivedOn` và `deliveredOn`. Mỗi lần chuyển tăng `version`.

Bảng nhật ký mới `LAB_ORDER_STATUS_LOG` (không sửa/xóa): phiếu, trạng thái cũ (trống khi tạo), trạng thái mới, thời điểm, người đổi, ghi chú (≤ 300 ký tự, không bắt buộc). Phiếu cũ trước khi có tính năng: `Delivered` nếu đã có ngày giao, ngược lại `NotSent`. Xóa phiếu xóa luôn nhật ký.

**Quá hạn** (tính, không lưu): có ngày hẹn nhỏ hơn hôm nay (giờ Việt Nam) và trạng thái là `NotSent`, `Sent` hoặc `Rework`.

## Quyền

- `treatment.lab.read` — xem màn và danh sách (cha `treatment.read`).
- `treatment.lab.track` — đổi trạng thái (cha `treatment.lab.read`), không giới hạn vai trò, để lễ tân/phụ tá cũng ghi nhận gửi/nhận.
- Mặc định chỉ vai trò Nha sĩ (và Chủ phòng khám qua nhóm quyền) có hai quyền trên; vai trò khác được cấp qua nhóm quyền truy cập.
- Sửa/xóa nội dung phiếu vẫn là `treatment.lab.manage` (Nha sĩ) như LAB-ORDER.md.
- Dữ liệu chỉ gồm chi nhánh được cấp; `branchId` ngoài phạm vi → 403 MSG06.

## API

- `GET /api/lab-orders?status=&branchId=&labProviderId=&dentistId=&from=&to=&q=&page=&pageSize=` (`treatment.lab.read`).
  - `status` ∈ `NotSent|Sent|Received|Delivered|Rework|Overdue`; `from`/`to` lọc theo ngày hẹn (gồm hai đầu, `from > to` → MSG04); `q` tìm theo mã phiếu, MSKH, tên bệnh nhân, tên công việc (≤ 160 ký tự); `pageSize` ∈ {20, 50, 100}.
  - Sắp xếp: ngày hẹn tăng dần (không có hẹn xếp cuối), rồi ngày lập giảm dần, rồi id.
  - Trả `{ items: [{ id, code, dossierId, dossierCode, activityId, patientId, patientCode, patientName, workName, labProviderId, labName, dentistId, dentistName, orderedOn, dueOn, sentOn, receivedOn, deliveredOn, quantity, material, teeth, note, totalFeeVnd, paidVnd, status, overdue, version }], total, page, pageSize, counts: { notSent, sent, received, delivered, rework, overdue }, labs: [{ id, name }], dentists: [{ id, name }] }`. `counts` tính trên mọi bộ lọc trừ `status`.
- `GET /api/lab-orders/{id}/history` (`treatment.lab.read`) → `[{ fromStatus, toStatus, changedAt, changedByName, note }]` mới nhất trước.
- `PUT /api/lab-orders/{id}/status` (`treatment.lab.track`) body `{ status, expectedVersion, note }` → phiếu sau khi đổi; lệch version → 409 MSG111; phiếu không thấy hoặc ngoài chi nhánh → 404 MSG113.

## Giao diện

- Hàng thẻ đếm: Chưa gửi, Đã gửi xưởng, Đã nhận về, Quá hạn hẹn, Đã giao bệnh nhân, Làm lại; bấm thẻ lọc theo trạng thái đó, bấm lại để bỏ lọc.
- Bộ lọc: ô tìm, công ty labo, bác sĩ, khoảng ngày hẹn, trạng thái (đồng bộ với thẻ), nút Đặt lại.
- Bảng: Mã, Bệnh nhân (MSKH), Công việc (số lượng, chất liệu), Công ty, Hẹn (đỏ khi quá hạn), Phí/Đã trả, Trạng thái; phân trang 20/50/100. Trạng thái: đang tải, trống ("Chưa có phiếu labo nào"), lỗi kèm "Thử lại". Không cuộn ngang; từ 1000px trở xuống mỗi phiếu là một thẻ.
- Bấm một dòng mở ngăn chi tiết bên phải: thông tin phiếu (răng, chất liệu, ghi chú, phí), nhật ký trạng thái, nút chuyển trạng thái hợp lệ (cần `treatment.lab.track`, có ô ghi chú), toàn bộ chi tiết xem ngay tại ngăn này, không chuyển sang hồ sơ bệnh nhân (không có liên kết đến hồ sơ); nút "Sửa phiếu" mở lại form phiếu labo (cần `treatment.lab.manage`; không có thì form chỉ xem).

## Kiểm thử

Backend (tích hợp, schema cách ly): lập phiếu mặc định `NotSent`; chuyển trạng thái hợp lệ ghi ngày và nhật ký; chuyển sai từ chối; version lệch 409; danh sách lọc theo trạng thái/công ty/ngày/từ khóa, quá hạn, `counts`, phân trang, giới hạn chi nhánh; di chuyển phiếu cũ. Web (Playwright, mock API): thẻ đếm lọc, bộ lọc, mở ngăn chi tiết, đổi trạng thái và cập nhật nhật ký, ẩn nút khi thiếu quyền, dạng thẻ ở màn hẹp.

## Ngoài phạm vi

Thông báo nhắc hạn, công nợ xưởng, tệp đính kèm, in danh sách, xuất Excel, tự gửi phiếu cho xưởng.

## Bộ lọc chi nhánh (08/11/2026)

Màn "Quản lý công việc labo" có ô lọc "Chi nhánh" (mặc định "Tất cả chi nhánh") chỉ dành cho chủ phòng khám có từ 2 chi nhánh; chọn là danh sách, các ô đếm trạng thái và danh sách công ty labo/bác sĩ đổi ngay theo chi nhánh (API `GET /api/lab-orders?branchId=`). Danh sách chi nhánh lấy từ `/api/appointments/options`. Nhân viên thường không có ô này và chỉ thấy phiếu chi nhánh mình.
