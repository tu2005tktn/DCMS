# Labo nha khoa — tài liệu nghiên cứu và hướng làm trong DCMS

Ngày: 06/11/2026. Đây là tài liệu để hiểu nghiệp vụ và quyết định cách làm; **chưa phải đặc tả triển khai**. Khi chốt phạm vi, viết đặc tả vào `docs/specs/` (quy tắc "tài liệu trước, code sau" của dự án) rồi mới sửa code.

## 1. Labo nha khoa là gì

Nhiều việc nha khoa không làm xong ngay trên ghế: răng sứ, mão/cầu răng, veneer, inlay/onlay, hàm giả (tháo lắp, khung), máng nhai, khay tẩy trắng, hàm chỉnh nha, trụ/abutment và mão trên implant. Bác sĩ lấy dấu hoặc quét, gửi cho **xưởng labo** (kỹ thuật viên), xưởng chế tác rồi trả về, bác sĩ thử và gắn cho bệnh nhân. Phòng khám phải theo dõi: gửi gì, cho ai, hạn nào, nhận về chưa, có phải làm lại không, trả xưởng bao nhiêu.

Luồng chuẩn:

1. **Chỉ định** — trong phiếu điều trị, bác sĩ chọn dịch vụ cần labo (ví dụ mão sứ răng 36).
2. **Lấy dấu / quét** — dấu silicone hoặc file quét trong miệng (STL/PLY), kèm màu răng, vật liệu, ghi chú thiết kế.
3. **Tạo phiếu gửi labo** — chọn xưởng, loại công việc, răng, màu, hạn trả, yêu cầu đặc biệt, đính kèm file/ảnh.
4. **Xưởng nhận và chế tác** — có thể có bước thử khung (try-in) giữa chừng.
5. **Nhận về và kiểm tra** — đúng răng, đúng màu, đúng số lượng; không đạt thì trả xưởng làm lại (remake) theo bảo hành.
6. **Lắp cho bệnh nhân** — hẹn bệnh nhân, thử, chỉnh khớp cắn, gắn.
7. **Thanh toán** — hai chiều: bệnh nhân trả phòng khám, phòng khám trả công nợ xưởng.

## 2. Thuật ngữ

| Thuật ngữ | Ý nghĩa |
|---|---|
| Labo / xưởng | Công ty hoặc cơ sở chế tác phục hình |
| Kỹ thuật viên (KTV) | Người làm phục hình tại xưởng |
| Phiếu labo (lab order/prescription) | Phiếu bác sĩ gửi xưởng, ghi đủ yêu cầu |
| Dấu / quét | Mẫu lấy trong miệng (vật lý hoặc số) |
| Try-in | Thử sản phẩm dở dang (khung kim loại, sứ biscuit) trước khi hoàn thiện |
| Remake / làm lại | Xưởng làm lại do lỗi, thường miễn phí trong bảo hành |
| Shade | Màu răng (thang VITA A1–D4, 3D-Master) |
| Bảo hành | Thời hạn xưởng sửa/làm lại miễn phí |

## 3. Dự án đã có gì (đã kiểm tra trong mã nguồn)

- **Danh mục công ty** (`Provider`/`SupplyEntity`, loại `Supplier`/`Lab`/`Both`): màn Thiết lập hệ thống, tab công ty labo. Đặc tả: `docs/specs/S58-SUPPLY-CATALOGS.md`.
- **Loại công việc labo** (`LabWorkType`, `backend/src/DCMS.Domain/Features/SystemSettings/SupplyCatalog.cs`): `code`, `name`, `providerId` (xưởng mặc định), `unit` (`Jaw`, `HalfJaw`, `Tooth`, `Piece`, `Set`), `referencePrice`, `warranty`. API `GET/POST … /api/settings/catalogs/lab-work-types`, phân quyền theo resource `lab-work-types`; Dentist quản lý được, `medicines` thì không.
- **Danh sách lựa chọn**: `GET /api/reference-options?kind=lab-providers` và `kind=lab-work-types`.
- **Trường `LabWorkSnapshot`** (chuỗi) trên hoạt động lâm sàng `ClinicalActivity`/`ActivityDto`, ghi từ dòng phiếu điều trị (`row.LabWork`). Chỉ là **ghi chú văn bản**, chưa có trạng thái hay công nợ.
- Đặc tả S58 đã ghi sẵn: "Yêu cầu labo (S44/S45, chưa có) sẽ chụp giá/đơn vị/bảo hành tại lúc tạo".
- Tài liệu liên quan: `docs/implementation/PROVIDER-MATERIAL-LAB-PROGRESS-20261029.vi.md` (trạng thái dở dang: quy tắc đổi đơn vị mới ở Domain, chưa nối API; còn việc Labo unit mapping/default resolver).

**Chưa có:** phiếu/yêu cầu labo, theo dõi trạng thái, hạn trả, tệp đính kèm, nhận hàng, làm lại, công nợ xưởng, màn danh sách labo, báo cáo.

## 4. Đề xuất mô hình dữ liệu

Thêm khái niệm **LabOrder** (phiếu gửi labo), tách khỏi danh mục.

`LabOrder`
- `Id`, `Code` (mã phiếu, ví dụ `LB-20261106-0001`), `BranchId`, `PatientId`, `DossierId`, `ActivityId?` (dòng điều trị phát sinh phiếu), `DentistId`.
- `ProviderId` (xưởng), `State` (xem mục 5), `DueOn` (hạn trả), `SentAt?`, `ReceivedAt?`, `FittedAt?`.
- `Note` (yêu cầu chung), `Shade?` (màu), `RemakeOfId?` (nếu là phiếu làm lại).
- Chụp tại lúc tạo (snapshot): giá xưởng, đơn vị, bảo hành, tên xưởng — để đổi danh mục sau này không làm sai phiếu cũ (đúng tinh thần S58).
- `Version`, `CreatedBy/At`, `UpdatedBy/At`, xóa mềm.

`LabOrderItem` (một phiếu có nhiều dòng)
- `LabOrderId`, `LabWorkTypeId`, `ToothRegion` (răng/hàm), `Quantity`, `Unit`, `UnitPriceVnd` (snapshot), `Note`.

`LabOrderEvent` (lịch sử trạng thái, giống `TreatmentEvent`): `LabOrderId`, `From`, `To`, `At`, `By`, `Reason`.

`LabOrderAttachment`: tên tệp, loại (ảnh, STL, PDF), kích thước, đường dẫn lưu **ngoài Git** (quy tắc dự án: hồ sơ bệnh nhân không đưa vào Git).

Công nợ xưởng: `LabPayable` hoặc dùng lại cơ chế khoản thu/chi hiện có. Nên tách khỏi `FinanceCharge` (khoản thu của bệnh nhân) để không lẫn thu với chi. Thanh toán nhà cung cấp cho vật liệu đang được làm ở `MATERIAL-PURCHASES.md`, có thể tham chiếu cách làm.

## 5. Vòng đời trạng thái đề xuất

```
Draft → Sent → InProgress → TryIn? → Received → Fitted
                   ↘ Cancelled
Received → Rejected → (tạo phiếu mới RemakeOfId)
```

| Trạng thái | Ý nghĩa | Ai đổi |
|---|---|---|
| Draft | Nháp, chưa gửi | Bác sĩ/phụ tá |
| Sent | Đã gửi xưởng | Bác sĩ/phụ tá |
| InProgress | Xưởng đang làm (xưởng xác nhận) | Lễ tân/phụ tá |
| TryIn | Đã nhận khung thử | Bác sĩ |
| Received | Nhận về, đã kiểm | Phụ tá/bác sĩ |
| Fitted | Đã lắp cho bệnh nhân | Bác sĩ |
| Rejected | Không đạt, trả làm lại | Bác sĩ |
| Cancelled | Hủy | Bác sĩ (kèm lý do) |

Quy tắc nên có: chỉ đổi theo đường hợp lệ; mọi chuyển trạng thái ghi `LabOrderEvent` kèm lý do khi hủy/từ chối; quá `DueOn` mà chưa `Received` thì đánh dấu **trễ hạn**; gửi lệnh có `requestKey` (idempotent) và `version` (chống ghi đè), giống các lệnh hiện có của dự án.

## 6. Màn hình và API gợi ý

Màn hình:
1. **Trong phiếu điều trị** (`TreatmentSheet`): nút "Gửi labo" trên dòng dịch vụ có loại công việc labo; thay ô văn bản `LabWork` bằng chọn Loại công việc labo + tạo phiếu nhanh.
2. **Danh sách phiếu labo** (menu mới): lọc theo xưởng, trạng thái, trễ hạn, khoảng ngày; hiển thị mã, bệnh nhân, xưởng, hạn trả, trạng thái.
3. **Chi tiết phiếu**: thông tin, các dòng, tệp đính kèm, dòng thời gian trạng thái, nút chuyển trạng thái, tạo phiếu làm lại.
4. **Báo cáo** (sau): số phiếu theo xưởng, tỷ lệ làm lại, chậm hạn, công nợ xưởng.

API (cùng phong cách hiện tại, tiền tố gợi ý `/api/lab-orders`):
- `GET /api/lab-orders?providerId=&state=&overdue=&from=&to=&q=&page=&pageSize=`
- `GET /api/lab-orders/{id}`
- `POST /api/lab-orders` (tạo, nhận `requestKey`)
- `PUT /api/lab-orders/{id}` (sửa khi còn Draft, kèm `version`)
- `POST /api/lab-orders/{id}/transition` (`to`, `reason?`, `version`, `requestKey`)
- `POST /api/lab-orders/{id}/attachments` (tải tệp lên)

Quyền: thêm nhóm quyền riêng (ví dụ `lab-orders.read`, `lab-orders.manage`) vào hệ thống quyền động (`ScreenEndpointPermissions.cs`, `S58-DYNAMIC-ACCESS-GROUPS`), không gắn cứng theo vai trò.

## 7. Lộ trình làm từng bước (đề xuất)

1. **Giai đoạn 1 — Phiếu labo cơ bản**: `LabOrder` + `LabOrderItem` + `LabOrderEvent`, tạo từ phiếu điều trị, danh sách và chi tiết, chuyển trạng thái, hạn trả và cảnh báo trễ. Migration viết tay như các migration hiện có.
2. **Giai đoạn 2 — Tệp đính kèm** (ảnh, STL), lưu ngoài Git, giới hạn kích thước và loại tệp.
3. **Giai đoạn 3 — Làm lại và bảo hành**: `RemakeOfId`, tính bảo hành theo snapshot `warranty`, thống kê tỷ lệ làm lại.
4. **Giai đoạn 4 — Công nợ xưởng**: ghi nhận phải trả, thanh toán, báo cáo theo xưởng, nối vào màn Thanh toán/Thống kê.
5. **Giai đoạn 5 — Nâng cao**: thông báo (nhắc hạn, nhận hàng) qua cơ chế thông báo S26, liên kết lịch hẹn lắp, cổng cho xưởng nếu cần.

Mỗi giai đoạn: đặc tả trước, rồi backend (Domain → Application → Infrastructure → Api), test tích hợp trên schema cách ly, rồi giao diện web và test Playwright.

## 8. Rủi ro và điểm cần quyết định

- **Một dòng điều trị có thể cần nhiều phiếu** (làm lại), hoặc một phiếu gộp nhiều răng: nên cho phiếu nhiều dòng, liên kết `ActivityId` tùy chọn.
- **Dữ liệu cũ**: `LabWorkSnapshot` hiện là văn bản tự do; quyết định giữ nguyên làm ghi chú hay di chuyển sang `LabOrder`. Đề xuất giữ, không tự chuyển.
- **Giá**: giá bán cho bệnh nhân (dịch vụ) và giá trả xưởng là hai giá khác nhau; đừng gộp.
- **Tệp lớn** (STL hàng chục MB): cần giới hạn, quét loại tệp, lưu ngoài Git và ngoài cơ sở dữ liệu.
- **Quyền riêng tư**: tệp và phiếu chứa dữ liệu bệnh nhân; không đưa mẫu thật vào repo hay test.
- **Phạm vi**: xưởng có dùng hệ thống không? Nếu không, nhân viên phòng khám cập nhật trạng thái hộ xưởng (đề xuất cho giai đoạn đầu).

## 9. Câu hỏi cần bạn trả lời trước khi viết đặc tả

1. Phòng khám có xưởng nội bộ hay chỉ gửi xưởng ngoài? (ảnh hưởng đến công nợ xưởng)
2. Cần cho xưởng đăng nhập cập nhật trạng thái, hay nhân viên phòng khám tự cập nhật?
3. Có cần đính kèm file quét/ảnh ngay giai đoạn đầu không?
4. Công nợ xưởng làm cùng đợt hay để sau?
5. Có bước thử khung (try-in) bắt buộc không, hay chỉ nhận về rồi lắp?

## 10. Tài liệu nên đọc thêm

- Trong dự án: `docs/specs/S58-SUPPLY-CATALOGS.md`, `docs/specs/S21-S43-TREATMENT-DOSSIER.md`, `docs/specs/TREATMENT-PAYMENT-V25.md`, `docs/specs/MATERIAL-PURCHASES.md` (mẫu công nợ nhà cung cấp), `docs/specs/S26-Notifications-Reminders.md`.
- Ngoài dự án: tìm các từ khóa "dental laboratory prescription form", "dental lab case management workflow", "VITA shade guide", "digital impression STL workflow" để xem mẫu phiếu và quy trình thực tế.
