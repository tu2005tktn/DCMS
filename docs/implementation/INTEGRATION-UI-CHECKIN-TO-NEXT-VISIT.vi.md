# Tích hợp UI — một bệnh nhân từ check-in đến hẹn tiếp

Người thực hiện: Ngô Minh Trí. Lần chạy: 02/11/2026, nhánh `TriNM` (nền `main` dd50777). Trạng thái task: **In Progress**.

Luồng và quy tắc chung: [DCMS-110](../specs/CLINIC-FLOW-DCMS-110.vi.md).

Đây là checklist kiểm tích hợp, không quy định phác đồ hay mốc thu phí gói. Mọi bước "Đạt" dưới đây chạy trên **browser thật gọi API thật** (PostgreSQL, schema riêng), không giả lập API. Không có bước nào được ghi Đạt chỉ từ mock.

## 1. Môi trường và dữ liệu giả

| Mục | Giá trị |
| --- | --- |
| Build | Backend `dotnet build backend/DCMS.sln` 0 lỗi. Web chạy Vite dev |
| API | `http://localhost:5082`, schema PostgreSQL riêng `dcms_test_flowui` (migrate + seed demo). Không dùng schema thường, không đụng dữ liệu oDental |
| Web | `http://127.0.0.1:8081`, `VITE_API_BASE_URL=http://localhost:5082` |
| Tài khoản | Lễ tân/thu ngân `assistant.demo`, bác sĩ `dentist.demo`, chủ phòng khám `owner.demo` (chỉ dùng để nạp fixture). Mật khẩu lấy từ `DEV_SEED_PASSWORD` trong `.env`, không ghi vào tài liệu |
| Lịch làm việc | Seed cho "Nha sĩ demo" làm 08:00–17:00. Do chạy lúc 01:30, ca Thứ Hai 02/11 trong schema thử được mở rộng 00:00–24:00 bằng SQL. Các tuần sau giữ nguyên seed |
| Fixture phí | Chủ phòng khám đặt giá bảng: "Khám" 100.000 ₫; "Điều trị thử nghiệm" 500.000 ₫ (giá tham chiếu S21). Giá trị giả lập, không phải bảng giá thật |
| Fixture thuốc | "Thuốc giả lập kháng sinh" (mã GL-AMOX), kê đơn mua ngoài, không theo dõi tồn kho |
| Bệnh nhân | "Giả Lập Luồng 791316" (BN000001), SĐT giả 0977791316. Người nhà "Giả Lập Người Nhà 791316" (BN000002) dùng chung SĐT cho ca nhầm người |

## 2. Checklist luồng chính

Kết quả: **Đạt**, **Không đạt**, **Bị chặn** (thiếu nguồn/chức năng của task khác) hoặc **Chưa chạy**. Ảnh chụp lưu local, không commit.

| # | Task | Thao tác | Kỳ vọng | Thực tế | Kết quả |
| --- | --- | --- | --- | --- | --- |
| 1 | DCMS-111 | Lễ tân: Bệnh nhân → Thêm hồ sơ. Tiếp nhận → Xếp lịch khách vãng lai (chọn hồ sơ, Khám, Nha sĩ demo, slot đầu, lý do ngoại lệ). Đối chiếu → Xác nhận Check-in | Một Appointment, một Visit, một dòng "Chờ khám" | BN000001 tạo được. Lịch 08:00 Ghế 1. Check-in tạo Visit, hàng chờ có đúng 1 dòng "Chờ khám", lý do "Khám mới" | Đạt |
| 2 | DCMS-112 | Bác sĩ: hàng chờ → Mở lượt khám → Bắt đầu khám. Nhập triệu chứng, 2 vùng (răng 46, 36) kèm chẩn đoán, Lưu nháp, F5. Tổng kết: dặn dò, hướng xử trí "Tiếp tục điều trị" → Hoàn tất khám | Nháp giữ sau F5; phiếu xác nhận khóa sửa | Nháp giữ sau F5; "Đã hoàn tất khám". Lần nhập thử chọn nhầm chẩn đoán nên đã dùng **Đính chính kết quả** có lý do → phiên bản 3, "Sâu răng" ở răng 46 và 36 | Đạt |
| 3 | DCMS-115 | Tổng kết → Kết thúc lượt khám: xem phí khám, tích đã kiểm tra → Xác nhận | Visit hoàn tất; đợt điều trị (nếu có) vẫn mở | Phí khám hiện "Khám · Giá bảng 100.000 ₫". Lượt "Hoàn tất". Đợt điều trị tạo sau đó vẫn "Đang điều trị" | Đạt |
| 4 | DCMS-113 | Tổng kết → Lập kế hoạch điều trị → + TẠO PHIẾU MỚI → Tạo hồ sơ. Trên phiếu: + thêm 2 hạng mục "Điều trị thử nghiệm" (46, 36), ✓ | Hai hạng mục; ghi được một **đồng ý làm**, một **hoãn** | Tạo được đợt và 2 hạng mục (giá điền sẵn 500.000 ₫). Phiếu điều trị không có ô quyết định của bệnh nhân. S21 hiện cả hai là "Đề xuất" nhưng tính "Tổng các hạng mục đã đồng ý: 1.000.000 ₫" | Bị chặn (L-06) |
| 5 | DCMS-114 | Phiếu: hàng răng 46 → ⋯ → Thực hiện → ghi chú → ✓. Tải lại. S21 → Các bước → Chỉ định thêm buổi | Một việc đã làm cho răng 46; răng 36 giữ lại; lưu chỉ định quay lại | 1 dòng quá trình (ngày 02/11, 500.000 ₫, "Chưa ghi nhận phí"). Hàng 46 gạch ngang, 36 giữ. Tải lại vẫn 1 dòng. Không tạo được chỉ định quay lại: chưa có bước vì không có hạng mục "đã đồng ý" | Đạt một phần, chỉ định quay lại Bị chặn (L-06) |
| 6 | DCMS-117 | Phiếu khám → Chuyển thu ngân → Đơn thuốc mua ngoài. Chọn thuốc, SL 15 → Thêm vào đơn | Lưu dòng đơn; không trừ kho | Lời dặn điền sẵn từ danh mục; 1 dòng "15 viên". Main chưa có bảng kho nên không có biến động kho. Bác sĩ phải đi qua màn thu ngân mới tới đơn (L-04) | Đạt |
| 7 | DCMS-116/102 | Thu ngân: Thanh toán → Mở thanh toán → Thu tiền 50.000 ₫ tiền mặt | Hiện khoản hợp lệ; thu một phần | Bảng kê có phí khám 100.000 ₫. Thu 50.000 ₫ → phiếu PT-1-00000001, còn 50.000 ₫. Phí điều trị răng 46 (500.000 ₫) không lên bảng kê, phiếu điều trị ghi "Chưa ghi nhận phí" | Đạt cho phí khám; phí điều trị Bị chặn (L-08) |
| 8 | DCMS-106 | Lễ tân: Lịch hẹn → Đặt lịch, chọn hồ sơ, Nha sĩ demo, 09/11/2026, slot 09:00 | Lịch với ghế và bác sĩ rảnh | Lịch 09/11 09:00–09:30, Ghế 1, "Đã xác nhận"; tab Lịch hẹn của bệnh nhân có cả lịch cũ "Hoàn thành". Chỉ chọn được dịch vụ "Khám"; loại "Tái khám" bị khóa; lịch không gắn chỉ định quay lại | Đạt một phần (L-09) |
| 9 | DCMS-111 (buổi sau) | Check-in chỉ cho đúng ngày hẹn, nên đặt thêm lịch 10:00 cùng ngày làm buổi sau. Tiếp nhận → lý do "Điều trị tiếp" → chọn đợt cũ → Check-in. Bác sĩ mở lượt | Visit mới; hồ sơ, hạng mục, lịch sử giữ nguyên | Visit mới khác Visit đầu; đợt, 2 hạng mục, phiếu khám, phiếu thu giữ nguyên. Bác sĩ được chuyển sang màn "Đợt điều trị" cũ, không có thao tác bắt đầu/kết thúc buổi; lượt mới đứng ở "Chờ khám" | Đạt phần giữ dữ liệu; lượt buổi sau Bị chặn (L-07) |
| 10 | DCMS-107 | Xem phần chính thức phía bệnh nhân | Hiển thị đúng phần chính thức | Có API `/api/patients/me/treatment-summary` nhưng web/mobile chưa có màn dùng nó | Bị chặn (L-10) |

Phí dùng fixture ở mục 1. Nhánh phí gói nhiều buổi chưa chốt chính sách nên **không kiểm**: theo DCMS-110, không tự chia phí theo buổi.

## 3. Ca bổ sung

| Ca | Thao tác | Thực tế | Kết quả |
| --- | --- | --- | --- |
| Nhầm người chung SĐT | Tạo hồ sơ người nhà cùng SĐT; tìm theo SĐT khi xếp lịch | Form báo "Có hồ sơ nghi trùng", khóa Lưu đến khi tích đã xem; tạo BN000002 riêng. Tìm theo SĐT trả cả hai, lễ tân phải tự chọn | Đạt |
| Không có slot | Xếp lịch Chủ nhật 08/11 (không có lịch làm việc) | "Không còn khung giờ đủ tài nguyên…", 0 slot, nút Xem lại bị khóa | Đạt |
| Quyền sai | Bác sĩ mở thanh toán; trợ lý mở phiếu khám và đơn thuốc | Bác sĩ không có nút Thu tiền. Trợ lý xem phiếu khám chỉ đọc (không Lưu nháp/Đính chính) nhưng thấy lỗi tải danh mục chẩn đoán (L-03). Trợ lý mở đơn thuốc: "Bạn không có quyền thực hiện thao tác này" | Đạt, kèm lỗi hiển thị |
| Bấm đôi / retry | Check-in mở lại; tải lại sau Thực hiện; gửi 2 lệnh thu tiền đồng thời cùng `requestKey` (API) | Vẫn 1 Visit, 1 dòng quá trình. Thu tiền: cả hai trả 200, chỉ 1 phiếu thu mới (đã thu 50.000 → 60.000 ₫) | Đạt |
| Lỗi mạng | — | Chưa chạy | Chưa chạy |
| Tư vấn-only; đồng ý làm buổi khác | — | Chưa chạy; nhánh "đồng ý" đang bị chặn bởi L-06 | Chưa chạy |
| Nguồn Labo | — | Không dùng nhánh Labo trong lần chạy này | Chưa chạy |
| S20/DCMS-104, chăm sóc DCMS-105 | — | Chưa kiểm điều hướng giữ ngữ cảnh | Chưa chạy |

## 4. Lỗi và điểm chặn

| Mã | Bước | Mô tả | Tái hiện | Thuộc | Xử lý |
| --- | --- | --- | --- | --- | --- |
| L-01 | Nhiều màn | Nút và thông báo mặc định của Ant Design hiện tiếng Anh: "Cancel", "OK", "No data", "Please enter Người nộp tiền", "20 / page" | Mở modal Tạo đợt điều trị; bảng trống; bấm thu tiền khi bỏ trống người nộp | Web chung | Sửa trong đợt này (U1) |
| L-02 | 2–3 | "Lập kế hoạch điều trị" và "Chuyển thu ngân" hiển thị như link thường, không phải nút | Kết thúc lượt khám, xem Tổng kết | DCMS-112 UI | Sửa (U2) |
| L-03 | Ca quyền | Trợ lý xem phiếu khám chỉ đọc vẫn tải danh mục chẩn đoán → API 403, báo "Không tải được danh mục chẩn đoán" | `assistant.demo` mở một phiếu khám | DCMS-112 UI | Sửa (U3) |
| L-04 | 6 | Phiếu khám không có đường tới Đơn thuốc mua ngoài; bác sĩ phải qua "Chuyển thu ngân". Tab "Đơn thuốc" trong hồ sơ bệnh nhân vẫn ghi "Chức năng chưa triển khai" | Mở phiếu khám đã hoàn tất; mở tab Đơn thuốc của bệnh nhân | DCMS-117 (Triển) | Thêm nút trên phiếu khám (U2). Tab hồ sơ cần API theo bệnh nhân, chuyển chủ task |
| L-05 | 4 | Tab Điều trị mở từ phiếu khám ghi "Bệnh nhân (MSKH: #4, chưa rõ tuổi)" (ID kỹ thuật) và không điền sẵn bác sĩ của lượt khám | Tổng kết → Lập kế hoạch điều trị → + TẠO PHIẾU MỚI | DCMS-113 UI | Sửa tên/mã/tuổi (U4). Điền sẵn bác sĩ cần `clinical-handoff` trả bác sĩ, chuyển chủ task |
| L-06 | 4–5 | Phiếu điều trị không ghi quyết định đồng ý/hoãn. Hạng mục từ phiếu luôn "Đề xuất", nhưng S21 vẫn cộng vào "Tổng các hạng mục đã đồng ý". "Thực hiện" trên phiếu chỉ gạch ngang hàng, tiến độ hạng mục ở S21 vẫn "Chưa thực hiện". Không có hạng mục đồng ý thì không thêm bước và chỉ định quay lại | Thêm 2 hạng mục trên phiếu → mở S21 → Các bước | DCMS-113/114 (Vinh), chỉ định quay lại S24 (Tuấn) | Chuyển chủ task |
| L-07 | 9 | Lượt "Điều trị tiếp" chuyển sang màn Đợt điều trị cũ, không có thao tác bắt đầu/kết thúc buổi; lượt mới đứng mãi "Chờ khám". Màn này còn hiện "Hồ sơ #4", "Chi nhánh 1", "Bác sĩ phụ trách 2", "NotStarted" và mã sự kiện tiếng Anh | Check-in "Điều trị tiếp" có chọn đợt → bác sĩ mở lượt | Vòng đời buổi: DCMS-114/115 (Vinh/Tư). Nhãn: UI | Sửa nhãn và tên (U5). Vòng đời buổi chuyển chủ task |
| L-08 | 7 | Việc đã làm có phí "Chưa ghi nhận phí": yêu cầu phí nằm ở outbox, chưa có consumer ghi vào sổ thu nên không lên bảng kê | Thực hiện hạng mục → mở thanh toán của lượt | DCMS-116/102 (Triển) | Chuyển chủ task |
| L-09 | 8 | Đặt lịch chỉ có dịch vụ "Khám"; loại "Tái khám" khóa ("cần module hướng dẫn tái khám"); lịch không gắn chỉ định quay lại | Lịch hẹn → Đặt lịch | DCMS-106; danh mục dịch vụ đặt lịch DCMS-123 (Vinh) | Chuyển chủ task |
| L-10 | 10 | Chưa có màn bệnh nhân đọc phần chính thức | — | DCMS-107 | Chuyển chủ task |
| L-11 | 7 | Ô "Người nộp tiền" để trống (server không gợi ý); thu tiền phải gõ lại tên | Thu tiền | DCMS-116 (Triển) | Chuyển chủ task |
| L-12 | 1 | Mã lịch vãng lai dài 32 ký tự hex (`AP-90C0…12EF6`), khó đọc với lễ tân | Xếp lịch vãng lai | DCMS-111 | Ghi nhận |
| L-13 | 5 | S21 ghi "Dịch vụ #2" thay vì tên dịch vụ khi dịch vụ không có trong danh sách đặt lịch | Mở S21 của đợt | DCMS-113 UI | Sửa (U6) |

## 5. Sửa giao diện trong đợt này

Chỉ sửa nhãn, hiển thị và điều hướng đơn giản theo contract hiện có. Không đổi API, engine lịch, kê đơn, tính tiền hay tiến trình.

- **U1.** Ant Design dùng ngôn ngữ tiếng Việt (`vi_VN`) cho toàn web nhân viên và khách: nút mặc định, bảng trống, thông báo kiểm tra form, phân trang.
- **U2.** Tổng kết phiếu khám:
  - "Lập kế hoạch điều trị" và "Chuyển thu ngân" là nút.
  - Thêm nút **Đơn thuốc mua ngoài** khi phiếu đã xác nhận và người dùng có quyền `examinations.manage`. Nút mở màn `/visits/{visitId}/prescription` có sẵn.
- **U3.** Phiếu khám chỉ tải danh mục chẩn đoán khi người dùng được sửa hoặc đính chính phiếu. Người chỉ xem không gọi API này và không thấy lỗi.
- **U4.** Tab Điều trị mở từ phiếu khám hiện họ tên, mã bệnh nhân và tuổi như tab Điều trị thường.
- **U5.** Màn Đợt điều trị:
  - Bệnh nhân hiện "Mở hồ sơ bệnh nhân".
  - Chi nhánh và bác sĩ hiện tên, lấy từ `/api/appointments/options` như S21; nếu không tải được thì ghi "Không tải được tên".
  - Tiến độ, kết quả đóng và các sự kiện trên dòng thời gian (gồm sự kiện của phiếu điều trị) hiện nhãn tiếng Việt; loại lạ vẫn hiện mã gốc.
- **U6.** S21 dùng tên dịch vụ đã lưu trên hạng mục khi danh sách tùy chọn không có dịch vụ đó. Tiến độ hiện nhãn tiếng Việt.

### Kiểm lại sau khi sửa

Kiểm trên browser thật, cùng API và dữ liệu ở mục 1, không có lỗi JS hay API 4xx/5xx:

| Sửa | Kết quả |
| --- | --- |
| U1 | Modal hiện "Hủy". Form báo "Hãy nhập thông tin cho trường Người nộp tiền". Bảng trống hiện "Trống" |
| U2 | Ba nút hiển thị đúng. "Đơn thuốc mua ngoài" mở đúng đơn của lượt |
| U3 | Trợ lý xem phiếu khám không còn gọi API chẩn đoán và không còn báo lỗi |
| U4 | Tab Điều trị ghi "Giả Lập Luồng 791316 (MSKH: BN000001, 36 tuổi)" |
| U5 | Màn đợt điều trị hiện "Mở hồ sơ bệnh nhân", "S09 - Chi nhánh thử nghiệm", "Nha sĩ demo", "Chưa thực hiện". Dòng thời gian: "Ghi nhận nguồn từ phiếu khám", "Thực hiện hạng mục" |
| U6 | S21 ghi "Điều trị thử nghiệm" thay cho "Dịch vụ #2" |

Kiểm tra tự động:
- `npm run build` đạt.
- Playwright mock (mọi spec trừ `*.integration` và `staff-idle`): 73 đạt, 15 lỗi.
- Chạy lại 15 ca lỗi trên code chưa sửa (stash) cho đúng 15 lỗi đó, nên không do lần sửa này. Ít nhất 3 ca (`accounts`, `patients`, `customer-appointments`) cần API thật. Các ca còn lại chưa điều tra.

## 6. Chạy lại luồng

Không cần nhập tay ID hay tạo lại dữ liệu ở mỗi màn: mỗi bước mở màn kế tiếp bằng nút, link hoặc hàng chờ.

1. Tạo schema riêng và nạp seed:
   - Tạo schema `dcms_test_<tên>`.
   - Chạy API với `--migrate --seed-demo` trên schema đó.
2. Thêm lịch làm việc giả cho ngày chạy, chỉ trong schema thử.
3. Nạp fixture:
   - Đăng nhập `owner.demo`, đặt giá Khám và giá tham chiếu dịch vụ điều trị.
   - Tạo một thuốc kê đơn mua ngoài ở Thiết lập hệ thống → Vật tư, labo & thuốc.
4. Làm bước 1–9 ở mục 2 theo thứ tự, đổi tài khoản như cột Thao tác. Các nút cần bấm:
   - Hàng chờ → **Mở lượt khám**.
   - Tổng kết → **Lập kế hoạch điều trị**, **Chuyển thu ngân**, **Đơn thuốc mua ngoài**.
   - Thanh toán → **Mở thanh toán**.
5. Dọn dẹp: dừng API và web, rồi xóa schema thử.
