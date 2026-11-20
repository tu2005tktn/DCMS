# Lỗi phát hiện khi chạy System test 5.3 — 20/11/2026

Người chạy: Ngô Minh Trí (nhánh TriNM, trên `main` mới nhất `f707ea1`). Chạy bằng trình duyệt thật với API và PostgreSQL thật (`dcms_dev`, dữ liệu demo). Ảnh chụp theo từng bước nằm trong thư mục `5.3 System test FRs\<tab>\<mã test case>` gửi kèm.

| Mã | Test case | Mức độ | Trạng thái |
|---|---|---|---|
| BUG-01 | SYS7-04 | Trung bình | Đã sửa trên TriNM, chờ review |
| BUG-02 | SYS7-07, SYS4-09 | Cao: chặn 2 test case | Đã sửa (cách A) trên TriNM, chạy lại: Đạt |
| BUG-03 | SYS6-04 | Cao: chặn 1 test case | Đã xử lý (hướng 1: cập nhật tài liệu và test case), chạy lại: Đạt |
| BUG-04 | SYS6-09 | Trung bình | Đã sửa trên TriNM, chạy lại: Đạt |
| BUG-05 | SYS7-07 | Thấp | Đã sửa trên TriNM, chạy lại: Đạt |
| Ghi chú 1 | SYS7-01 | Thấp | Đã sửa, SYS7-01 chạy lại: Đạt |
| Ghi chú 2 | — | Trung bình | Mở |

---

## BUG-01 — Giá tham khảo âm bị đổi thành 0 và vẫn lưu (SYS7-04)

**Màn hình:** Thiết lập hệ thống › Loại công việc labo, và Quản lý thuốc (cùng component).

**Cách tái hiện**
1. Đăng nhập `owner.demo`, mở Loại công việc labo, bấm "Thêm mới".
2. Nhập Mã, Tên, rồi nhập Giá tham khảo `-100000`.
3. Rời khỏi ô giá, bấm "Lưu".

**Kết quả thực tế:** ô tự đổi `-100000` thành `0`, bản ghi được lưu với giá 0 ₫, không có thông báo. Người dùng không biết giá mình nhập đã bị thay.

**Kết quả mong đợi:** giá âm bị từ chối kèm thông báo, không tạo bản ghi.

**Nguyên nhân:** ô `InputNumber` có `min={0}`, nên kẹp giá trị về 0 trước khi quy tắc kiểm tra (`priceRule`) chạy. Máy chủ vốn đã chặn giá âm (MSG257) nhưng số âm không bao giờ được gửi lên.

**Đã sửa:** bỏ `min={0}` ở hai ô giá trong `web/src/features/system-settings/SupplyCatalogPanel.tsx`. Giờ ô giữ `-100000` và báo "Giá là số nguyên không âm (VND).", không lưu được. Đặc tả cập nhật trong `docs/specs/S58-SUPPLY-CATALOGS.md`. SYS7-04 chạy lại: **Đạt**.

**Dữ liệu phát sinh khi tái hiện:** loại công việc labo `LB23280` (giá 0 ₫) trong `dcms_dev` là bản ghi bị lưu sai, có thể ngừng dùng.

---

## BUG-02 — Không thể gán chi nhánh nghiệp vụ cho nhân viên mới (chặn SYS7-07, SYS4-09)

**Màn hình:** Thiết lập hệ thống › Phân quyền truy cập › tab "Gán cho nhân viên" › "Gán nhóm quyền".

**Cách tái hiện**
1. Đăng nhập `admin.demo`, tạo một tài khoản nhân viên mới (vai trò Trợ lý phòng khám).
2. Đăng nhập `owner.demo`, mở Phân quyền truy cập › "Gán cho nhân viên". Cột "Chi nhánh nghiệp vụ" của nhân viên mới là "Chưa có".
3. Bấm "Gán nhóm quyền", chọn nhóm "Receptionists", mở ô **Chi nhánh nghiệp vụ**.

**Kết quả thực tế:** ô Chi nhánh nghiệp vụ **không có lựa chọn nào**. Bấm "Lưu nhóm cho nhân viên" thì báo "Chọn nhóm quyền, chi nhánh và nhập lý do gán nhóm.", không lưu được. Không có màn hình nào khác để đưa nhân viên vào một chi nhánh. `admin.demo` cũng đang ở trạng thái "Chưa có" chi nhánh.

**Kết quả mong đợi:** chủ phòng khám đưa được nhân viên mới vào chi nhánh (ví dụ chi nhánh mới tạo ở SYS7-01) và gán nhóm quyền. Nhân viên đó sau đó chỉ thấy dữ liệu chi nhánh của mình (SYS4-09).

**Nguyên nhân:**
- Web, `AccessGroupsPanel.tsx`: lựa chọn của ô chỉ gồm chi nhánh mà nhân viên đã là thành viên (`membershipBranchIds`).
- Backend, `SettingsService.cs` (gán nhóm cho nhân viên): từ chối mọi `BranchIds` không thuộc danh sách thành viên đang hoạt động (`SchedulingMembers`).
- Thành viên chi nhánh (`SCHEDULING_BRANCH.Members`) **chỉ được tạo bởi `DevelopmentSchedulingSeeder`**. Không có API hay màn hình nào thêm hoặc bỏ thành viên. Nhân viên tạo mới qua "Quản lý tài khoản" vì thế không bao giờ có chi nhánh.

**Ảnh hưởng:** không đưa được nhân viên mới nào vào làm việc qua giao diện. Không làm được lịch làm việc, đặt lịch hay phân quyền cho họ, và không kiểm chứng được phạm vi chi nhánh qua giao diện.

**Ghi chú khi chạy test:** SYS4-09 trong kịch bản dùng `assistant.b.demo`, nhưng tài khoản này chỉ có trong môi trường test tích hợp, không có trong `dcms_dev`. Kể cả khi có tài khoản, BUG-02 vẫn chặn việc đưa nhân viên vào chi nhánh B.

**Hướng sửa đề xuất (chờ nhóm thống nhất):**
- **Cách A (khuyến nghị):** thêm mục **"Nhân sự chi nhánh"** vào màn Chi nhánh & Ghế điều trị: thêm, bỏ hoặc ngừng nhân viên trong chi nhánh, kèm cờ làm việc như nha sĩ (`CanDentist`). Cần API mới, ví dụ `PUT /api/system-settings/branches/{id}/members`, dùng lại `SchedulingMember` sẵn có. Như vậy không thêm bảng hay cột; dữ liệu vẫn nằm trong JSON của `SCHEDULING_BRANCH`.
- **Cách B:** trong hộp thoại "Gán nhóm quyền", cho chọn mọi chi nhánh mà chủ phòng khám quản lý. Khi lưu thì tự thêm nhân viên vào thành viên của chi nhánh đó.

Cách A tách rõ "làm việc ở chi nhánh nào" với "có quyền gì". Cách B nhanh hơn nhưng gộp hai việc vào một hộp thoại.

**Bằng chứng:** `SYS7 Catalogs\SYS7-07 ... - False Test Case (Blocked)\02 - BLOCKED - O Chi nhanh nghiep vu khong co lua chon (0 muc).png` và `03 - BLOCKED - Khong luu duoc vi thieu chi nhanh nghiep vu.png`; ảnh tương tự trong thư mục `SYS4-09 ... - False Test Case (Blocked)`.

---

## BUG-03 — Không còn bước "Chốt kế hoạch" điều trị (chặn SYS6-04)

**Màn hình:** Hồ sơ bệnh nhân › Điều trị & Thanh toán › phiếu điều trị.

**Cách tái hiện**
1. Đăng nhập `dentist.demo`, mở một phiếu điều trị, thêm hạng mục vào bảng KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ.
2. Tìm nút "Chốt kế hoạch".

**Kết quả thực tế:** không có nút "Chốt kế hoạch" hay "Lưu nháp kế hoạch". Hạng mục được lưu ngay khi bấm ✓ và luôn sửa được, không có trạng thái "Đã chốt · phiên bản 1".

**Kết quả mong đợi (theo test case SYS6-04 và đặc tả):** bác sĩ chốt kế hoạch. Sau khi chốt, hạng mục là căn cứ cho việc đã làm, và muốn đổi phải qua luồng điều chỉnh ("Sửa hạng mục" / "Điều chỉnh kế hoạch").

**Nguyên nhân:** commit `5338722` trên `main` (18/11/2026, "refactor(treatment): simplify TREATMENT_PLAN_ITEM to 18 columns") chủ động gỡ các API `/plan/draft`, `/plan/commit`, `/plan/decision`, `/warranty` và `/steps*` cùng cột trạng thái kế hoạch. Trước đó trang kế hoạch riêng (`PlanPage`, `RevisionPage`) đã bị gỡ ở commit `8bb6eec`.

**Không khớp cần xử lý:** các tài liệu `docs/specs/S21-S43-TREATMENT-DOSSIER.md`, `docs/specs/PATIENT-TREATMENT-PAYMENT-UI.md`, `docs/specs/S41-MOBILE-MEDICAL-RECORDS.md`, `docs/implementation/RUN-CLINICAL-FLOW.vi.md` và test case SYS6-04 vẫn mô tả bước chốt kế hoạch.

**Cần nhóm quyết định:**
1. Nếu **bỏ hẳn** bước chốt là đúng ý đồ: cập nhật các tài liệu trên và sửa SYS6-04 theo luồng mới. Hạng mục kế hoạch luôn sửa được, việc đã làm ghi trực tiếp.
2. Nếu **vẫn cần** chốt kế hoạch: khôi phục trạng thái kế hoạch cho phiếu, chưa cần bảng mới. Thêm lại API chốt và hiển thị "Đã chốt · phiên bản N", khóa sửa hạng mục sau khi chốt. Phần này phải làm cùng người đã rút gọn bảng (commit `5338722`) để không phá giới hạn cột.

**Bằng chứng:** `SYS6 Treatment\SYS6-04 ... - False Test Case\01 - Step 1 - Tim nut Chot ke hoach tren phieu.png`.

---

## BUG-04 — Số tiền thu lớn hơn số còn nợ bị tự giảm, không báo lỗi (SYS6-09)

**Màn hình:** Hồ sơ bệnh nhân › Điều trị & Thanh toán › "Trả tiền" › Thanh toán phiếu điều trị.

**Cách tái hiện**
1. Đăng nhập `owner.demo`, mở phiếu còn nợ (phiếu demo `HSD057C8A6E204`, Phạm Thu Hà), bấm "Trả tiền".
2. Ở bảng "Phí labo cần thu riêng", ô "Thu lần này" của dòng còn nợ 1.200.000 ₫, nhập `99000000`, rồi rời khỏi ô.

**Kết quả thực tế:** ô tự đổi thành `1.200.000` (đúng số còn nợ), không có thông báo nào. Nút "Xác nhận thu tiền mặt" vẫn bấm được, và bấm thì hệ thống ghi khoản thu 1.200.000 ₫.

**Kết quả mong đợi (SYS6-09):** hiện thông báo kiểm tra và không ghi khoản thu nào.

**Nguyên nhân:** `web/src/features/treatment/DossierPaymentModal.tsx`, các ô `InputNumber` dùng `max={line.dueVnd}` (dòng 121) và `max={lab.dueVnd}` (dòng 144). Đây là cùng kiểu lỗi với BUG-01: ô tự kẹp giá trị nên không có quy tắc kiểm tra nào chạy.

**Hướng sửa đề xuất:** bỏ `max`, giữ số người dùng nhập. Nếu số đó lớn hơn số còn nợ thì báo lỗi ngay dưới ô, ví dụ "Số tiền thu không được lớn hơn số còn nợ (1.200.000 ₫).", và khóa nút xác nhận cho đến khi sửa. Cần kiểm tra thêm phía máy chủ có chặn số vượt nợ hay không.

**Dữ liệu phát sinh khi test:** lần chạy ngày 20/11 kịch bản đã bấm xác nhận, nên phiếu `HSD057C8A6E204` có thêm một khoản thu labo 1.200.000 ₫ tiền mặt (người thu `owner.demo`). Đây là dữ liệu demo, không phải bệnh nhân thật. Kịch bản đã được sửa để không bấm xác nhận nữa.

**Bằng chứng:** `SYS6 Treatment\SYS6-09 ... - False Test Case\02 - Step 2 - Nhap 99.000.000, o hien 1200000.png`, `03 - Step 2 - Bam xac nhan thu tien.png`, `04 - Ket qua ....png`.

---

## BUG-05 — "Lịch sử phân quyền" hiện người thực hiện là `#undefined` (SYS7-07)

**Màn hình:** Thiết lập hệ thống › Phân quyền truy cập › "Lịch sử phân quyền".

**Kết quả thực tế:** cột "Người thực hiện" hiện `#undefined` thay cho tên chủ phòng khám.

**Nguyên nhân:** `AccessGroupsPanel.tsx` đọc `actorId` để tra tên, nhưng API `/api/system-settings/settings/audit` trả `actorName` và không có `actorId`.

**Đã sửa:** hiển thị `actorName` (dự phòng tra theo `actorId` nếu có). Bằng chứng lỗi: thư mục `SYS7 Catalogs\SYS7-07 - Lich su phan quyen - Nguoi thuc hien undefined - False Test Case`.

---

## Kết quả sửa ngày 20/11/2026 (nhánh TriNM, chưa commit)

Thư mục lỗi được giữ nguyên với hậu tố "False Test Case". Ảnh chạy lại sau khi sửa nằm trong thư mục cùng cấp mang tên gốc của test case.

| Bug | Sửa | File chính | Test case chạy lại |
|---|---|---|---|
| BUG-02 | Thêm mục "Nhân sự chi nhánh" trong ngăn sửa chi nhánh: thêm, ngừng, làm việc lại; bật "Khám như nha sĩ", "Quản lý lịch". API mới `GET/PUT /api/system-settings/branches/{id}/members[/{employeeId}]`. Không thêm bảng hay cột. | `BranchMemberService.cs`, `BranchContracts.cs`, `SettingsEndpoints.cs`, `BranchMembersSection.tsx`, `BranchesPanel.tsx`; đặc tả `S58-BRANCHES-AND-CHAIRS.md` | SYS7-07, SYS4-09: Đạt |
| BUG-03 | Hướng 1: giữ luồng hiện tại của `main` (không có bước chốt). Đã ghi chú vào 4 tài liệu liên quan; SYS6-04 kiểm tra kế hoạch đã lưu, còn sau khi tải lại và là căn cứ cho việc đã làm. **Nhóm cần cập nhật mô tả SYS6-04 trên sheet cho khớp.** | 4 file trong `docs/` | SYS6-04: Đạt |
| BUG-04 | Bỏ `max` ở ô "Thu lần này"; báo "Số tiền thu không được lớn hơn số còn nợ (X ₫)." và khóa nút thu/dùng cọc. | `DossierPaymentModal.tsx`; đặc tả `TREATMENT-DOSSIER-PAYMENTS.md` | SYS6-09: Đạt |
| BUG-05 | Hiện `actorName` trong lịch sử phân quyền. | `AccessGroupsPanel.tsx` | SYS7-07: Đạt |
| Ghi chú 1 | Đặt tên riêng cho form chi nhánh và form ghế (`name="branch"`, `name="chair"`), nên `id` các ô không còn trùng. | `BranchesPanel.tsx` | SYS7-01: Đạt |

Ghi chú 2 (dự án test backend không build) chưa sửa; sẽ xử lý cùng báo cáo 5.2 Integration Test.

---

## Lỗi do kịch bản test (không phải lỗi ứng dụng)

Các ca dưới đây trượt ở lần chạy đầu vì kịch bản tự động viết sai. Đã sửa kịch bản, chạy lại và **đạt**. Ảnh trong thư mục là ảnh của lần chạy lại.

| Test case | Kịch bản sai ở đâu | Sửa |
|---|---|---|
| SYS4-08 | Tìm hộp thoại dạng ngăn kéo (`Drawer`), nhưng "Xếp lịch khách vãng lai" mở hộp thoại `Modal` "Đặt lịch tại quầy (S11)". Chưa điền người đặt, dịch vụ, nha sĩ. | Tìm theo tên hộp thoại, điền đủ ô bắt buộc, chọn khung giờ, "Xem lại", "Xác nhận đặt lịch". |
| SYS5-01 | Tìm "Các lần khám trước" ở tab đầu, nhưng mục này nằm ở tab "Lịch sử khám". | Bấm tab "Lịch sử khám" trước. Giữ ảnh 01–03 của lần bắt đầu khám, thêm ảnh 04. |
| SYS5-02 | Bấm nút sai tên (nút đúng là "Áp dụng cho 1 răng"). Chọn "Mô hình 3D" bằng vai trò `radio` nên chờ đến hết giờ. | Bấm "Áp dụng cho 1 răng", chuyển 2D/3D bằng chữ trên nút. |
| SYS5-03 | Kiểm tra chẩn đoán ở sai phần tử. Ở lần chạy lại, gõ "Sâu răng" rồi Enter làm **bỏ chọn** chẩn đoán đã lưu. | Chỉ chọn khi chưa có; kiểm tra thẻ "Sâu răng" trong ô chẩn đoán. |
| SYS5-05 | Trượt dây chuyền: SYS5-02 chưa lưu kết quả răng nên "Hoàn tất khám" báo thiếu mục (đúng nghiệp vụ). | Chạy lại sau khi SYS5-02, SYS5-03 đạt. |
| SYS5-06 | Không điền ô bắt buộc "Đơn vị thuốc" nên form báo "Nhập đơn vị thuốc" (đúng nghiệp vụ). | Điền đơn vị "viên". |
| SYS6-05 | Đăng nhập lần hai khi đang đăng nhập, trang đăng nhập không còn ô mật khẩu. | Đăng xuất trước khi mở phiếu. |
| SYS6-07 | Phiếu labo đã lưu và có trong "Quản lý công việc labo", nhưng kịch bản tìm theo mã phiếu điều trị, mà danh sách không hiện mã này. | Tìm theo tên bệnh nhân. |
| SYS8-04 | Tìm nút "Đổi lịch" có chữ hoa; nút thật là "Yêu cầu đổi lịch". | Tìm đúng tên nút. |

Kết quả cuối sau khi chạy lại: các ca trên **đạt**. Ca **false** còn lại: SYS6-04 (BUG-03), SYS6-09 (BUG-04), SYS7-07 và SYS4-09 (bị chặn bởi BUG-02). Thư mục của 4 ca này đã đổi tên có hậu tố "False Test Case".

---

## Ghi chú 1 — Nhãn ô của form ghế điều trị trỏ nhầm sang form chi nhánh (SYS7-01)

Trong ngăn sửa chi nhánh, hộp thoại "Thêm ghế điều trị" dùng chung `id` (`code`, `name`) với form chi nhánh phía sau. Nhãn "Mã ghế"/"Tên ghế" vì vậy gắn vào ô của chi nhánh. Người dùng bấm chuột vẫn nhập đúng, nhưng trình đọc màn hình và kiểm thử tự động chọn nhầm ô. Sửa: đặt `name` riêng cho form ghế (`<Form name="chair">`), hoặc đổi `id` của hai ô. File `web/src/features/system-settings/BranchesPanel.tsx`.

## Ghi chú 2 — Dự án test backend trên `main` không build được

`backend/tests/DCMS.IntegrationTests` (khoảng 30 file) vẫn dùng lớp `ApiFixture`, mà lớp này đã bị xóa khi chuyển sang `Infrastructure/ApiHost.cs`. `backend/tests/DCMS.UnitTests/Create/AppointmentCreateTests.cs` gọi `CreateAppointmentRequest` theo chữ ký cũ. Mã ứng dụng (`DCMS.Api`) vẫn build bình thường. Hệ quả là chưa chạy được test backend tự động trên `main`.
