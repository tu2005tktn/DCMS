# Phiếu labo trong Quá trình điều trị/dịch vụ

Cập nhật phân quyền 07/11/2026: đọc phiếu theo `treatment.read`; tạo/sửa/xóa phiếu cần `treatment.lab.manage`, phụ thuộc `treatment.actuals.update`. API, dịch vụ và form kiểm cùng quyền; thu hồi khi đang mở form khóa thao tác ghi và giữ bản nhập. Vai trò chuyên môn/phạm vi hồ sơ giữ nguyên.

Ngày: 06/11/2026. Liên quan: [S21-S43-TREATMENT-DOSSIER.md](S21-S43-TREATMENT-DOSSIER.md), [SYSTEM-SETTINGS-MATERIAL-CATALOG.md](SYSTEM-SETTINGS-MATERIAL-CATALOG.md), nghiên cứu `docs/research/LABO-NHA-KHOA.vi.md`.

## Mục tiêu

Trên mỗi dòng của bảng **QUÁ TRÌNH ĐIỀU TRỊ/DỊCH VỤ** (phiếu điều trị), trong cột **Labo** có một nút (biểu tượng bình thí nghiệm). Bấm nút mở **form Phiếu labo** để bác sĩ/phụ tá ghi đơn gửi xưởng labo cho dịch vụ đó (mão, cầu, hàm tháo lắp, veneer...). Giai đoạn này chỉ ghi và in phiếu; chưa có trạng thái theo dõi xưởng, tệp đính kèm hay công nợ xưởng.

## Dữ liệu

Mỗi dòng quá trình điều trị có **tối đa một phiếu labo** (lưu lại thì ghi đè phiếu cũ, có kiểm tra phiên bản). Bảng mới `LAB_ORDER`, không sửa bảng cũ.

| Trường | Ghi chú |
|---|---|
| `activityId`, `dossierId`, `patientId`, `branchId` | Lấy từ dòng điều trị, không nhập tay. |
| `code` (Mã số) | Không bắt buộc, ≤ 40 ký tự, duy nhất trong chi nhánh khi có giá trị. |
| `orderedOn` (Ngày) | Bắt buộc, mặc định hôm nay, không trước 2000. |
| `dueOn` (Hẹn), `deliveredOn` (Giao) | Không bắt buộc; Hẹn và Giao không được trước Ngày. |
| `dentistId` (Bác sĩ) | Bắt buộc, là bác sĩ/nhân sự đang hoạt động của chi nhánh phiếu; mặc định người thực hiện dòng điều trị. |
| `labProviderId` (Cty cung cấp) | Không bắt buộc; chỉ công ty loại Lab đang hoạt động ở Thiết lập chung. |
| `labWorkTypeId`, `workName` (Công việc labo) | `workName` bắt buộc ≤ 160 ký tự. Chọn từ loại công việc labo (Thiết lập chung) sẽ điền sẵn công ty, đơn giá, bảo hành; vẫn cho sửa. |
| `marginOuter/Inner/Side` (Đường hoàn tất: mặt ngoài/trong/bên) | Không bắt buộc, một trong: Vai, Vát, Lưỡi dao, Mỏng dần. |
| `bridgeShapes` (Nhịp cầu răng) | Không bắt buộc, nhiều lựa chọn: `saddle` Hình yên ngựa, `semi-saddle` Hình bán yên ngựa, `ovoid` Hình trứng, `t-ridge` Hình gờ T. |
| `isChild` (Trẻ em), `teeth` (Răng) | Răng theo số FDI: người lớn 11–18, 21–28, 31–38, 41–48; trẻ em 51–55, 61–65, 71–75, 81–85. Tối đa 32 răng, không trùng. |
| `shade` (Màu) | 6 vùng răng (1–6), mỗi vùng một mã màu ≤ 20 ký tự (ví dụ A2, B1); vùng trống thì bỏ qua. |
| `note` (Ghi chú) | ≤ 1.000 ký tự. |
| `material` (Chất liệu) | ≤ 100 ký tự, gợi ý sẵn: Sứ kim loại, Titanium, Zirconia, Emax, Nhựa, Khung kim loại. |
| `quantity` (Số lượng) | Số nguyên 1–999, mặc định 1. |
| `totalFeeVnd` (Tổng phí), `paidVnd` (Đã trả) | Số nguyên đồng ≥ 0; Đã trả ≤ Tổng phí. Đây là số liệu tham khảo của phiếu labo, **không** vào Chi phí/Đã trả của phiếu điều trị và không vào Thống kê. |
| `warranty` (Bảo hành) | Văn bản ≤ 100 ký tự (ví dụ "Trọn đời", "7 năm", "24 tháng"). |
| `version`, thời điểm tạo/sửa, người tạo/sửa | Hệ thống. |

Phần đầu form (Khách hàng, MSKH, tuổi, giới tính) chỉ đọc, lấy từ bệnh nhân của phiếu.

## API

- `GET /api/dossiers/{dossierId}/lab-orders` — danh sách tóm tắt (`activityId`, `workName`, `dueOn`, `deliveredOn`) cho cột Labo của bảng. Cần `treatment.read`.
- `GET /api/dossiers/{dossierId}/sheet-activities/{activityId}/lab-order` — trả `{ order | null, patient, options: { labs, workTypes, dentists, defaultDentistId } }`. Cần `treatment.read`.
- `PUT` cùng đường dẫn — tạo hoặc ghi đè phiếu. Có `expectedVersion` khi sửa phiếu đã có (lệch → 409 MSG111 hoặc mã xung đột hiện có). Cần `treatment.actuals.update`. Dữ liệu sai → MSG04 kèm `field`.
- `DELETE` cùng đường dẫn — xóa phiếu. Cần `treatment.actuals.update`.
- Dòng điều trị phải thuộc phiếu `dossierId` và người dùng phải có quyền truy cập phiếu (cùng quy tắc với các thao tác khác trên phiếu điều trị); không thì lỗi như truy cập phiếu không hợp lệ.
- Xóa dòng điều trị xóa luôn phiếu labo của dòng đó.

## Giao diện

- Nút trong ô cột Labo (cạnh tên công việc, không nằm ở ô Ghi chú): `aria-label="Phiếu labo cho {tên dịch vụ}"`, tooltip "Phiếu labo", bị khóa khi người dùng thiếu `treatment.actuals.update` hoặc đang sửa dòng khác. Người chỉ có quyền đọc vẫn xem được phiếu (form ở chế độ chỉ đọc).
- Cột **Labo** của bảng hiện tên công việc và hạn ("Hẹn dd/mm/yyyy") của phiếu; chưa có phiếu thì hiện nội dung labo cũ hoặc "—".
- Form (Modal rộng, bố cục như mẫu): tiêu đề "PHIẾU LABO"; hàng Mã số, Ngày, Hẹn, Giao; Khách hàng (chỉ đọc); Bác sĩ và Cty cung cấp; Công việc labo; Đường hoàn tất (3 ô chọn); Nhịp cầu răng (4 ô tick); Màu (sơ đồ 6 vùng bấm để nhập mã màu) và Răng (hai hàng răng FDI bấm chọn, ô Trẻ em đổi sang răng sữa); Ghi chú; Chất liệu, Số lượng, Tổng phí, Đã trả; Bảo hành. Nút **IN** (in riêng phiếu), **LƯU LẠI**, **✕** đóng (hỏi lại nếu có thay đổi chưa lưu). Lỗi kiểm tra ở trình duyệt (thiếu Công việc labo/Bác sĩ/Ngày, Hẹn hoặc Giao trước Ngày, Đã trả lớn hơn Tổng phí) hiện ngay dưới ô và chưa gửi; lỗi từ máy chủ hiện thành thông báo ở đầu form.
- Trên màn hẹp (≤ 700px) form xếp một cột, không cuộn ngang; hai hàng răng tự xuống dòng.

## Kiểm thử

- Backend (tích hợp, schema cách ly): tạo/sửa/xóa phiếu; ghi đè đúng phiên bản và 409 khi lệch; từ chối răng không hợp lệ/trùng, Giao trước Ngày, Đã trả > Tổng phí, bác sĩ ngoài chi nhánh, công ty không phải Lab; dòng điều trị của phiếu khác; danh sách tóm tắt; xóa dòng điều trị xóa phiếu.
- Web (Playwright, mock API): bấm nút mở form, chọn công việc điền sẵn công ty/giá/bảo hành, chọn răng và màu, lưu gửi đúng nội dung, lỗi 400 hiện dưới ô, cột Labo cập nhật.

## Ngoài phạm vi (làm sau)

Trạng thái theo dõi xưởng (gửi, nhận, làm lại), tệp đính kèm, công nợ xưởng, báo cáo, danh sách phiếu labo riêng, thông báo nhắc hạn.


Cập nhật 08/11/2026: danh sách phiếu labo riêng và theo dõi trạng thái xưởng (gửi, nhận, làm lại) đã chuyển sang [LAB-ORDER-MANAGEMENT.md](LAB-ORDER-MANAGEMENT.md). Phiếu mới có trạng thái mặc định `NotSent`.
