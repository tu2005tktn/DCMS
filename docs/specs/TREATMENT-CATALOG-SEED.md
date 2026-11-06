# Dữ liệu mẫu: danh mục dịch vụ và kế hoạch điều trị cho phòng khám nhỏ/vừa

Ngày: 06/11/2026. Nguồn: `docs/research/reports/Kế hoạch điều trị nha khoa nhỏ.md` (nghiên cứu cho phòng khám nha khoa cỡ nhỏ và vừa tại Việt Nam, không lấy dịch vụ/ca khó của bệnh viện hay phòng khám lớn).

## Mục tiêu

Nạp một bộ dữ liệu mẫu để màn **Kế hoạch điều trị** (`/treatment-templates`) và danh sách dịch vụ có sẵn nội dung thực tế khi chạy thử, thay vì rỗng. Đây là **dữ liệu mẫu cho môi trường phát triển**, không phải khuyến cáo y khoa và không phải bảng giá chính thức.

## Phạm vi nạp

Chỉ nạp vào chi nhánh demo **"S09 - Chi nhánh thử nghiệm"**, qua lệnh Development riêng, không chạy khi khởi động API và không bao giờ chạy ở môi trường khác Development.

1. **Dịch vụ** (`SchedulingService`): 49 dịch vụ ngoài phạm vi ca khó, mỗi dịch vụ có `Name`, `Unit`/`BillingUnit` (đơn vị tính: răng, hàm, 2 hàm, lần, phim, túi, ca, đơn vị cầu), `PublicDurationMinutes` (thời lượng mỗi buổi), `ReferenceUnitPriceVnd` (giá tham khảo). `PublicBookable = false` (không cho khách tự đặt lịch các dịch vụ này), `Active = true`.
2. **Kế hoạch điều trị mẫu** (`TreatmentTemplate` với `DefinitionKind = "ItemPlan"` và các `TreatmentTemplateItem`): 17 kế hoạch (16 theo báo cáo và 1 kế hoạch mặt dán sứ cho răng cửa thẩm mỹ: sâu răng đơn giản; tủy răng 1 chân kèm mão; tủy răng hàm kèm mão; cạo vôi/viêm lợi; nhổ răng khôn lệch nhẹ; implant đơn lẻ; cầu răng; hàm tháo lắp; tẩy trắng; trẻ em sâu răng sữa; niềng răng ca đơn giản; răng vỡ lớn đã chữa tủy; sâu nhiều răng; đau cấp; nhổ răng rồi phục hình; duy trì định kỳ; mặt dán sứ). Mỗi dòng có dịch vụ, răng (nếu có), thời gian dự kiến (phút), ghi chú (số buổi, lưu ý), chi phí; bác sĩ/phụ tá để trống.

## Quy tắc dữ liệu

- **Giá:** mỗi dịch vụ lưu **một giá tham khảo** chọn trong khoảng giá của báo cáo (gần trung vị, làm tròn đến 50.000). Dịch vụ chưa có nguồn giá để `ReferenceUnitPriceVnd = null`, và dòng kế hoạch có chi phí 0. Phòng khám tự chỉnh giá sau khi nạp.
- **Chi phí dòng kế hoạch** = giá tham khảo × số đơn vị của bước (ví dụ cầu răng 3 đơn vị); dịch vụ không giá → 0.
- **Thời lượng:** theo ước lượng của báo cáo, nằm trong 1–480 phút. Đây là suy luận, cần bác sĩ phòng khám xác nhận.
- **Không nạp** ca/dịch vụ ngoài phạm vi (răng khôn ngầm sát thần kinh, ghép xương lớn/nâng xoang, All-on-4/6, nội nha chữa lại, nha chu cần lật vạt, chỉnh nha phức tạp, khay trong suốt, nha nhi cần gây mê).
- **Dịch vụ khám ban đầu:** chi nhánh demo đã có dịch vụ "Khám" (`IsInitialExamination`, duy nhất trên mỗi chi nhánh); lệnh nạp **không tạo thêm** mà dùng lại làm bước đầu của các kế hoạch.
- **Idempotent, không ghi đè:** dịch vụ khớp theo (`BranchId`, tên); kế hoạch khớp theo (`BranchId`, tên, chưa xóa). Mục đã có thì **bỏ qua**, không sửa giá hay dòng người dùng đã chỉnh. Chạy lại lệnh không nhân đôi dữ liệu.
- Tạo bằng tài khoản `dentist.demo` (`CreatedBy`); kế hoạch nằm trong chi nhánh demo nên bác sĩ demo thấy được ở màn Kế hoạch điều trị.
- Không có thay đổi lược đồ cơ sở dữ liệu, không migration, không sửa API.

## Cách chạy

Từ thư mục gốc, sau khi PostgreSQL và biến môi trường development đã sẵn sàng:

```powershell
. .\infrastructure\scripts\import-development-env.ps1
dotnet run --project backend\src\DCMS.Api --no-build --launch-profile http -- --seed-clinic-catalog
```

Cách ngắn hơn: `& .\infrastructure\scripts\start-api.ps1 -SeedClinicCatalog`. Lệnh cũng nạp luôn danh sách tên dụng cụ/vật liệu (chỉ danh mục, không nhập/xuất) ([MATERIAL-CATALOG-SEED.md](MATERIAL-CATALOG-SEED.md)). Lệnh chỉ nạp rồi thoát, in số dịch vụ và số kế hoạch đã tạo/bỏ qua. Chỉ chạy khi `ASPNETCORE_ENVIRONMENT=Development`, nếu không sẽ báo lỗi.

## Kiểm thử

Test tích hợp trên schema cách ly: sau nạp, chi nhánh demo có đủ 17 kế hoạch, mỗi kế hoạch có ít nhất một dòng; mọi dòng tham chiếu dịch vụ tồn tại, thời gian dự kiến trong 1–480, chi phí ≥ 0; chạy lần hai không tạo thêm; dịch vụ đã đổi giá không bị ghi đè; kế hoạch bị xóa mềm sẽ được tạo lại khi nạp lại (vì chỉ khớp kế hoạch chưa xóa).

## Bỏ ghi chú chuyển tuyến và nạp lại (06/11/2026)

Ghi chú của dòng kế hoạch **không chứa nội dung chuyển tuyến/chuyển viện** (ví dụ "chuyển tuyến nếu ống tủy canxi hóa"). Danh sách ca ngoài phạm vi chỉ nằm ở tài liệu nghiên cứu, không nằm trong dữ liệu mẫu. Các ghi chú dẫn sang kế hoạch khác (ví dụ "sau đó chuyển sang kế hoạch implant") được giữ vì đó là chuyển kế hoạch, không phải chuyển tuyến.

Vì lệnh nạp không ghi đè mục đã có, thêm cờ **`--refresh`** đi kèm `--seed-clinic-catalog`: xóa mềm các kế hoạch **chưa xóa có tên trùng với kế hoạch mẫu** rồi tạo lại theo nội dung mới. Dịch vụ không bị đổi (giữ giá đã sửa). Kế hoạch do người dùng tự tạo (tên khác) không bị đụng; kế hoạch mẫu đã bị người dùng sửa dòng sẽ bị thay bằng bản mới.
