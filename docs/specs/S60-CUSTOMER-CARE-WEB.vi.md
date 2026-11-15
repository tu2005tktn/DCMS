# S60 — Chăm sóc khách hàng trên web nhân viên

Người phụ trách: Ngô Minh Trí (phần giao diện S25/S60 theo [S22-S63-ACTUAL-REVIEW-CARE.md](S22-S63-ACTUAL-REVIEW-CARE.md)). Ngày 09/11/2026.

Tài liệu tham chiếu:
- Màn "Customer cares" của oDental (ảnh chụp do người dùng cung cấp). Chỉ tham khảo bố cục, không dùng dữ liệu oDental.
- Backend công việc chăm sóc đã có: `CARE_TASK` (từ 13/11/2026 các lần liên hệ và chuyển cấp nằm trong JSONB của dòng, xem [CARE-LAB-FINANCE-STORAGE.vi.md](CARE-LAB-FINANCE-STORAGE.vi.md)), `/api/care-tasks`.
- Danh mục `care-types` "Loại chăm sóc khách hàng" của [S58-REFERENCE-CATALOGS.md](S58-REFERENCE-CATALOGS.md).

## 1. Mục tiêu

Khách đã khám hoặc đang khám thì thông tin đã có trong hệ thống. Trợ lý chọn khách (theo mã, tên hoặc SĐT) và tạo một **phiếu chăm sóc**: hỏi thăm sau điều trị, nhắc tái khám, chúc mừng, v.v.

Sau đó trợ lý:
- ghi nhận từng lần liên hệ;
- chuyển trạng thái tới khi xong.

Phiếu chăm sóc là bản ghi `CARE_TASK` hiện có. Tính năng này không tạo bảng mới và không cần migration.

Ngoài phạm vi:
- gửi tin nhắn/SMS/Zalo tự động;
- chuyển bác sĩ (escalation) trên giao diện. API đã có nhưng màn này chưa hiện.
- chọn lịch hẹn mới khi kết quả là "Đã đặt lại lịch".

## 2. Vị trí và quyền

- **Menu:** mục **Chăm sóc khách hàng**, nhóm **Vận hành**, ngay dưới **Hộp thư hỗ trợ**. Route web là `/customer-care`.
- **Ai thấy menu** (cập nhật 09/11/2026): tài khoản có quyền `care-tasks.read` **và** có vai trò hiệu lực **Trợ lý phòng khám** (`ClinicAssistant`) hoặc **Chủ phòng khám** (`ClinicOwner`).
  - Nha sĩ không có một trong hai vai trò này thì không thấy menu.
  - Tài khoản vừa Trợ lý vừa Nha sĩ thì thấy.
- **Route:** chặn theo cùng điều kiện. Không đủ điều kiện thì hiện thông báo không có quyền, không gọi API.
- **API:** đọc cần `care-tasks.read`, ghi cần `care-tasks.manage`.
  - Theo vai trò, các quyền này thuộc Nha sĩ và Trợ lý. Chủ phòng khám luôn có vai trò hiệu lực Nha sĩ (`AccountRoles.Effective`), nên cũng có các quyền này; không cần cấp thêm.
  - **Ghi** (tạo, sửa, ghi nhận liên hệ) giữ quy tắc chung: Nha sĩ/Trợ lý là **thành viên lịch** của chi nhánh. Vì vậy Chủ phòng khám ghi được ở chi nhánh mình là thành viên, và chỉ xem được ở chi nhánh còn lại. Giao diện chỉ hiện nút TẠO MỚI, Sửa, Đánh dấu đã xong, Lưu liên hệ ở chi nhánh người dùng ghi được.
- **Phạm vi chi nhánh** (theo [S58-BRANCHES-AND-CHAIRS.md](S58-BRANCHES-AND-CHAIRS.md) §4):
  - **Trợ lý / Nha sĩ:** chỉ chi nhánh mình là thành viên lịch đang hoạt động, nằm trong phạm vi chi nhánh được gán. Trợ lý chi nhánh A **không** đọc được phiếu của chi nhánh B, dù truyền thẳng `branchId` hay mã phiếu (403).
  - **Chủ phòng khám:** mọi chi nhánh đang hoạt động, không cần là thành viên lịch.
  - Ghi phiếu vẫn theo quy tắc cũ của `CareTaskService`: Nha sĩ/Trợ lý là thành viên chi nhánh của phiếu.
- **Nút "…" cạnh ô Loại chăm sóc:**
  - Chỉ hiện khi người dùng là Chủ phòng khám hoặc có quyền `catalog.care-types.read`. Mặc định Trợ lý không có quyền này; Chủ phòng khám cấp được qua "Phân quyền truy cập".
  - Bấm vào mở hộp thoại quản lý danh mục "Loại chăm sóc khách hàng", tức bảng của S58. Thêm, sửa, ngừng dùng theo quyền `catalog.care-types.*`.

## 3. Màn danh sách

Bố cục theo oDental:

| Thành phần | Hành vi |
| --- | --- |
| Chi nhánh | **Chỉ Chủ phòng khám** có ô này, và chỉ khi có từ 2 chi nhánh. Mặc định **Tất cả chi nhánh**; chọn một chi nhánh thì chỉ hiện phiếu của chi nhánh đó. Nhân viên khác không có ô chọn: danh sách gồm phiếu của các chi nhánh mình được gán |
| Xem theo | Ngày / Tuần (thứ Hai–Chủ nhật) / Tháng. Nút ‹ › lùi hoặc tiến một kỳ. Mặc định là hôm nay theo giờ phòng khám (UTC+7). Có thể chọn từ ngày–đến ngày bất kỳ, tối đa 366 ngày |
| Người phụ trách | Lọc theo tài khoản phụ trách. Mặc định tất cả |
| Loại chăm sóc | Lọc theo loại. Gồm loại trong danh mục và loại do hệ thống tự sinh |
| Trạng thái | Tất cả / Cần xử lý (Mở + Đang xử lý) / Mở / Đang xử lý / Đã xong / Đã hủy. Mặc định Tất cả |
| Khách hàng | Ô tìm theo mã, tên hoặc SĐT, tối đa 100 ký tự |
| "Tìm thấy N phiếu." | Tổng số theo bộ lọc |
| [Excel] | Xuất đúng bộ lọc đang chọn, không phân trang. Quá 5.000 dòng thì báo MSG04 yêu cầu thu hẹp bộ lọc, không cắt bớt |
| Phân trang | 20 / 50 / 100 dòng mỗi trang |
| **+ TẠO MỚI** | Mở hộp thoại tạo. Chỉ hiện khi có `care-tasks.manage` |

Cột của bảng:
- Thời gian chăm sóc (`dueAt`);
- Chi nhánh: chỉ hiện khi danh sách gồm nhiều chi nhánh;
- Khách hàng (mã · họ tên) và SĐT;
- Loại chăm sóc;
- Nội dung, hiện **đầy đủ** (xuống dòng, không cắt);
- Người phụ trách;
- Trạng thái;
- Liên hệ gần nhất (kết quả · thời gian).

Sắp xếp theo thời gian chăm sóc tăng dần. Bấm một dòng mở ngăn chi tiết.

Bảng dùng cỡ chữ nhỏ (13px, bảng gọn). Ở màn rộng từ 1280px, mọi cột nằm gọn trong một màn hình, không phải cuộn ngang. Màn hẹp hơn mới cuộn ngang.

Trạng thái hiển thị riêng từng loại: đang tải, lỗi kèm nút Thử lại, rỗng ("Không có phiếu chăm sóc phù hợp"), không có quyền, chưa có chi nhánh được phép.

## 4. Hộp thoại tạo / sửa ("Thêm chăm sóc")

| Trường | Quy tắc |
| --- | --- |
| Chi nhánh | Chỉ hiện khi người tạo thuộc từ 2 chi nhánh. Mặc định chi nhánh đang lọc, nếu không thì chi nhánh đầu tiên. Danh sách khách và người phụ trách theo chi nhánh này |
| Ngày | Bắt buộc. Mặc định hôm nay |
| Giờ | Giờ 00–23, phút theo bước 5. Mặc định giờ hiện tại làm tròn lên 5 phút |
| Khách hàng | Bắt buộc. Gõ mã, tên hoặc SĐT, tối thiểu 1 ký tự. Gợi ý tối đa 20 khách **đã hoặc đang khám tại chi nhánh**, tức có lượt khám `InProgress` hoặc `Completed`. Mỗi dòng gợi ý hiện mã · họ tên · SĐT · ngày khám gần nhất. Khi sửa thì không đổi được khách |
| Người phụ trách | Mặc định người đang đăng nhập. Chọn trong danh sách Nha sĩ / Trợ lý thuộc chi nhánh. Được để trống |
| Loại chăm sóc | Bắt buộc khi tạo. Chọn trong danh mục `care-types` đang dùng, giá trị lưu là **mã** của mục. Chưa có mục nào thì hiện hướng dẫn nhờ Chủ phòng khám thêm ở Thiết lập hệ thống. Khi sửa thì không đổi được loại |
| Nội dung | Bắt buộc, tối đa 4.000 ký tự |
| Trạng thái (chỉ khi sửa) | Mở / Đang xử lý / Đã xong / Đã hủy |

- **Lưu:**
  - gửi `requestKey` cố định cho lần bấm đó, nên bấm lại hoặc mạng chập chờn không tạo trùng;
  - sửa gửi kèm `expectedVersion`; phiếu đã bị người khác sửa thì báo MSG111 và giữ nguyên nội dung đang nhập.
- **Hủy:** đóng hộp thoại. Nếu đã nhập dữ liệu thì hỏi xác nhận trước khi bỏ.
- **Phiếu Đã xong hoặc Đã hủy** chỉ xem được, không sửa, theo `allowedActions`.

Ô gợi ý khách chỉ là cách chọn. API tạo phiếu giữ quy tắc hiện có: bệnh nhân tồn tại, chưa lưu trữ, cùng chi nhánh được phép. Lý do là công việc tự sinh từ chỉ định tái khám hoặc vắng hẹn vẫn đi qua cùng dữ liệu.

## 5. Ngăn chi tiết

- **Thông tin phiếu:** khách (mã, tên, SĐT), loại, nội dung, người phụ trách, thời gian chăm sóc, trạng thái, phiên bản.
- **Thao tác:** **Sửa** (mở hộp thoại ở mục 4) và **Đánh dấu đã xong**.
- **Ghi nhận liên hệ:**
  - Kênh: Điện thoại / Trực tiếp / Khác.
  - Kết quả: Đã liên lạc được / Không nghe máy / Khách từ chối / Khác.
  - Bước tiếp theo: tùy chọn, tối đa 1.000 ký tự.
  - Mỗi lần lưu là một bản ghi mới; bấm lại cùng một lần lưu không nhân đôi.
- **Lịch sử liên hệ:** mới nhất trước, có kênh, kết quả, bước tiếp theo, người ghi và thời gian.

Ghi nhận liên hệ không gửi tin nhắn, không tạo lịch hẹn và không hoàn tất chỉ định chuyên môn (theo S22-S63).

## 6. Hợp đồng API

Mọi route nằm trong `/api/care-tasks`, JSON dạng camelCase. Lỗi trả theo bộ thông điệp chung:
- 400 MSG04: dữ liệu không hợp lệ;
- 403 MSG06: không có quyền hoặc khác chi nhánh;
- 404 MSG113: không tìm thấy;
- 409 MSG111: phiên bản cũ hoặc trùng `requestKey` khác nội dung.

Route **mới**:

| Route | Hợp đồng |
| --- | --- |
| `GET /api/care-tasks/options` | `{ clinicDate, actorAccountId, allBranches, branches: [{ id, name, canCreate, staff: [{ accountId, name, roles }] }], types: [{ code, name, system }] }`<br>`branches` là các chi nhánh người dùng **đọc** được (§2). `canCreate` = người dùng là Nha sĩ/Trợ lý thành viên chi nhánh đó. `allBranches` = true với Chủ phòng khám<br>`types` gồm mục `care-types` đang dùng (`system: false`) và các loại hệ thống (`system: true`, chỉ để lọc). Mục đã ngừng dùng không có trong `types` nhưng vẫn hiện đúng tên ở dòng cũ |
| `GET /api/care-tasks/patient-options?branchId=&search=` | `search` 1–100 ký tự<br>Tối đa 20 dòng `[{ patientId, patientCode, fullName, phone, lastVisitAt }]`<br>Chỉ bệnh nhân chưa lưu trữ, có lượt khám `InProgress`/`Completed` ở chi nhánh; sắp theo lượt gần nhất |
| `GET /api/care-tasks/worklist?branchId=&from=&to=&ownerAccountId=&type=&status=&search=&page=&pageSize=` | `branchId` **tùy chọn** (09/11/2026): bỏ trống thì gồm mọi chi nhánh người dùng đọc được; truyền chi nhánh ngoài phạm vi thì 403<br>`from`/`to` là ngày (UTC+7), `from ≤ to`, cách nhau tối đa 366 ngày<br>`status` ∈ `Open, InProgress, Done, Cancelled, Pending` (`Pending` = Mở + Đang xử lý)<br>`pageSize` ∈ 20/50/100<br>Trả `{ items: [{ task: CareTaskDto, branchName, patientCode, patientName, phone, typeName, ownerName, lastContactResult, lastContactAt }], total, page, pageSize }` |
| `GET /api/care-tasks/export?` (cùng bộ lọc, bỏ `page`/`pageSize`) | Tệp `.xlsx`, tên `cham-soc-khach-hang-yyyyMMdd.xlsx`, header `Cache-Control: no-store`<br>Quá 5.000 dòng: 400 MSG04<br>Cột: Thời gian, Chi nhánh, Mã KH, Khách hàng, SĐT, Loại chăm sóc, Nội dung, Người phụ trách, Trạng thái, Liên hệ gần nhất |

Route **giữ nguyên**, màn này dùng lại:
- `POST /api/care-tasks`: tạo; `type` là mã `care-types`.
- `GET /api/care-tasks/{id}`: chi tiết và lịch sử liên hệ.
- `PATCH /api/care-tasks/{id}`: sửa và đổi trạng thái.
- `POST /api/care-tasks/{id}/contact-attempts`: ghi nhận liên hệ.

`GET /api/care-tasks?branchId=` cũ cũng giữ nguyên cho consumer hiện có.

Sửa lỗi ở route có sẵn:
- `POST /api/care-tasks` và `PATCH /api/care-tasks/{id}` chuẩn hóa `dueAt` về UTC trước khi lưu.
- Trước đây, gửi `dueAt` kèm offset, ví dụ `2026-11-09T09:00:00+07:00`, làm PostgreSQL từ chối ghi và API trả 500 MSG144.
- Giá trị thời điểm không đổi, chỉ đổi cách biểu diễn.

Quyền cho route mới: là GET nên cần `care-tasks.read`, cùng kiểm phạm vi chi nhánh theo §2.

`GET /api/care-tasks/{id}` (chi tiết) đọc theo phạm vi đọc ở §2, nên Chủ phòng khám mở được phiếu mọi chi nhánh. Các lệnh ghi giữ quy tắc cũ.

Xuất Excel chứa SĐT của bệnh nhân, nên dùng cùng quyền đọc danh sách. Không thêm quyền `*.export` riêng, vì danh sách trên màn hình đã hiện đủ các cột này.

## 7. Nghiệm thu

1. Tài khoản chỉ có vai trò Nha sĩ không thấy menu và bị chặn ở route. Trợ lý và Chủ phòng khám thấy menu ngay dưới "Hộp thư hỗ trợ".
1a. Trợ lý chi nhánh A chỉ thấy phiếu chi nhánh A. Truyền `branchId` của chi nhánh B, hoặc mở chi tiết phiếu chi nhánh B, đều trả 403.
1b. Chủ phòng khám thấy phiếu của mọi chi nhánh, chọn được một chi nhánh để lọc. Ở chi nhánh không phải thành viên lịch thì không tạo, sửa hay ghi nhận liên hệ được (403 ở API, không có nút trên giao diện).
2. Ô chọn khách chỉ gợi ý khách đã/đang khám tại chi nhánh. Khách chỉ có lịch hẹn hoặc chỉ đang chờ khám (`Queued`) thì không hiện.
3. Tạo phiếu có đủ ngày giờ, khách, người phụ trách, loại, nội dung. Bấm lại cùng một lần lưu không tạo trùng. Phiếu hiện trong danh sách với tên loại lấy từ danh mục.
4. Lọc theo khoảng ngày, người phụ trách, loại, trạng thái, khách (mã/tên/SĐT) đều đúng. Phân trang và tổng số đúng.
5. Excel có đúng các dòng theo bộ lọc. Chi nhánh khác: 403.
6. Ghi nhận liên hệ tạo lịch sử, tăng phiên bản phiếu. Sửa với phiên bản cũ: 409 MSG111. Đã xong thì không sửa được.
7. Bằng chứng tách riêng: API trên PostgreSQL thật (schema test riêng) và trình duyệt chạy với API thật. Không tính đạt chỉ từ mock.

## Điều chỉnh bố cục (14/11/2026)

Trang Chăm sóc khách hàng không còn dùng kiểu căn giữa toàn màn hình của lớp `.page` chung (gây khoảng trống lớn giữa tiêu đề, bộ lọc và bảng). Nay bố cục xếp dọc liền mạch: tiêu đề và nút "Tạo mới", thẻ bộ lọc, dòng "Tìm thấy n phiếu" kèm [Excel] / Tải lại, bảng dàn hết bề rộng.

Thẻ bộ lọc không còn khung trắng và lề trong, các ô lọc dàn trực tiếp trên nền trang theo hết bề rộng để nội dung chiếm đủ màn hình.
