# S58 / UC-51 — Thiết lập hệ thống

Cập nhật 30/10/2026: hai module được gộp, `/api/system-settings` là nguồn duy nhất. Bảng đổi route và cách chuyển dữ liệu ở [S58-SETTINGS-MERGE.vi.md](S58-SETTINGS-MERGE.vi.md). Các route `/api/settings/*`, `/api/access-groups`, `/api/reference-options` bên dưới là tên trong bản thiết kế gốc.

Ngày: **27/10/2026**. Trạng thái: **đặc tả để triển khai, chưa phải nghiệm thu phần mềm**. Phạm vi: staff web DCMS; cấu hình dùng chung được backend áp dụng cho các client liên quan. Bản soạn ban đầu chưa triển khai code. Cập nhật local: [kết quả DCMS-119/121/128 và phạm vi bàn giao](../implementation/S58-119-121-128.vi.md); các phần khác của Epic vẫn theo kế hoạch, không suy ra đã nghiệm thu toàn S58.

## 1. Nguồn, ký hiệu và phạm vi

- [D] Yêu cầu nền: [Word màn hình v2.5](DCMS_Screen_Specifications_v2.5.docx), S58/S31/S32/S46/S57/S04/S55; [Word UC/GBR/MSG v2.5](DCMS_Use_Cases_Business_Rules_Messages_v2.5.docx), UC-51/30/27/41/50/31/49, GBR-176–180, GBR-183–186; AT81–85/89/90.
- [O] Bằng chứng giao diện: [khảo sát trực tiếp oDental ngày 27/10](../research/ODENTAL-SYSTEM-SETTINGS-20261027.vi.md), OS01–OS14. [O] chỉ xác nhận UI quan sát được, không xác nhận backend oDental.
- [P] Chi tiết thiết kế dưới đây cụ thể hóa yêu cầu để team code: route, schema, grant, DTO, version, giới hạn bổ sung và thứ tự triển khai. Đây là đề xuất DCMS, không phải quyết định đã được phòng khám xác nhận hay hành vi oDental đã kiểm thử.
- Giữ nguyên các quyết định riêng tại [cấu hình đặt lịch](CLINIC-SLOTS-AND-SERVICE-DURATIONS.vi.md), [mật khẩu](SHARED-PASSWORD-POLICY.md), [chẩn đoán nhiều bệnh](S19-MULTIPLE-DIAGNOSES.vi.md) và [cận lâm sàng](S19-INVESTIGATIONS.vi.md).
- Bổ sung theo yêu cầu giao việc ngày 27/10/2026: nội dung chi tiết đã đưa vào hai Word v2.5 chính, phần **S58 Thiết lập hệ thống chi tiết** và **UC51 Quản lý thiết lập hệ thống chi tiết**. Dùng mã cục bộ `SET-*`, `SET-AT-*`, `SET-W*`; không tự cấp tiếp mã GBR/MSG/AT toàn cục.

Kết quả mong muốn: chủ phòng khám/người được cấp quyền duy trì thông tin phòng khám và danh mục tại một nơi; các form khám, hồ sơ, điều trị, đơn thuốc, kho, labo, in phiếu dùng đúng dữ liệu đó. Không tạo Visit, điều trị, phí, thu tiền, tồn kho hoặc lịch hẹn chỉ vì lưu thiết lập.

Ngoài phạm vi: mua/bán thẻ trả trước, điểm thưởng, Card/POS, hoàn tiền, hóa đơn điện tử pháp lý, gửi đơn quốc gia, cấu hình credential trong form, AI/chatbot và chu trình tiệt khuẩn/bảo trì dụng cụ tái sử dụng. Không bật nghiệp vụ chưa hỗ trợ bằng checkbox. Các cấu hình nhắc lịch/upload/chính sách lâm sàng rộng hơn trong UC-51 chỉ nối module có hợp đồng riêng; không thêm các công tắc chưa có consumer.

## 2. Điều hướng và trạng thái chung [P]

Sidebar có **Thiết lập hệ thống**, mở `/settings`; header hiển thị phòng khám, phạm vi đang xem và mục đang mở. Giữ thứ tự 11 nhóm oDental; có thể dùng Collapse của Ant Design và deep link `/settings/{section}`. Quyền không được cấp thì ẩn nhóm; truy cập trực tiếp API/deep link vẫn kiểm ở server. Có quyền xem nhưng không sửa thì hiển thị chỉ đọc.

| Nhóm | Section | Màn hình/UC liên quan | Bằng chứng |
| --- | --- | --- | --- |
| Thiết lập chung | `general` | S58, UC-51/01/38/20 | OS01–03 |
| Chẩn đoán/tên bệnh | `diagnoses` | S58 → S19/S23 | OS04 |
| Dịch vụ, sản phẩm, công việc | `services` | S31, UC-30 | OS05 |
| Công ty cung cấp vật liệu | `material-suppliers` | S46, UC-41 | OS06 |
| Loại dụng cụ/vật liệu | `materials` | S46, UC-41 | OS07 |
| Công ty labo | `lab-providers` | S46/S44, UC-40/41 | OS08 |
| Loại công việc labo | `lab-work-types` | S46/S45, UC-40/41 | OS09 |
| Nhóm khách hàng | `customer-groups` | S55/S05, UC-49/38 | OS10 |
| Thuốc/dược phẩm | `medicines` | S32/S23, UC-27 | OS11 |
| Phân quyền truy cập | `access-groups` | S04/S52, UC-31 | OS12 |
| Chi nhánh/cơ sở | `branches` | S57, UC-50 | OS13–14 |

**Danh mục phụ** Nguồn khách hàng, Nghề nghiệp, Tiền sử hành chính, Loại CSKH, Loại chi mở trong Thiết lập chung → Danh mục hồ sơ/nghiệp vụ hoặc từ nút quản lý cạnh select nguồn. Đây là phần DCMS đã nêu ở S58, không phải nhóm thứ 12 quan sát trên oDental. Mẫu điều trị dẫn đến `/treatment-templates` hiện có và giữ quyền nha sĩ.

Luồng chỉnh sửa: mở nhóm → tải dữ liệu và quyền → tìm/lọc → Thêm/Sửa → kiểm tra → xem tác động khi cần → Lưu → đọc lại ID/version/trạng thái từ server. Mỗi form chỉ lưu một phạm vi; không có nút “Lưu tất cả” gồm cả quyền, giá và chi nhánh.

- Danh sách: tìm tối đa 100 ký tự, status Active/Inactive/All, page 20/50/100, total trong phạm vi quyền, sort ổn định có ID cuối cùng. Cây dịch vụ tìm vẫn giữ đường dẫn nhóm cha.
- Form có nhãn, dấu bắt buộc, lỗi cạnh trường, Đóng/Quay lại và Lưu. Cảnh báo bỏ thay đổi khi đổi mục/chi nhánh/route; không tự lưu checkbox. Khi đang lưu khóa submit trùng.
- Tách loading, chưa có dữ liệu, không có kết quả, không có quyền và lỗi tải. Lỗi không hiển thị thành danh sách trống hoặc số 0. Lỗi lưu giữ bản nhập và bỏ loading.
- Ngừng dùng/Khôi phục có tên mục, tác động, lý do và xác nhận. Bản đầu chỉ dùng deactivate/restore, không cung cấp hard-delete. Bản ghi lịch sử vẫn đọc được.
- Excel chỉ ở danh sách có yêu cầu (dịch vụ, vật liệu, thuốc); có read grant và export grant riêng, đúng bộ lọc/cột cho phép. Export không kèm credential, dữ liệu bệnh nhân hoặc chi phí bị ẩn; ô bắt đầu `=`, `+`, `-`, `@` được ghi kiểu text an toàn.

## 3. Quyền và phạm vi [D/P]

Quyền thực thi = tài khoản/phiên hợp lệ + loại tài khoản Staff + role nghiệp vụ đủ điều kiện + action grant + phạm vi phòng khám/chi nhánh. Nhãn nhóm không tự cấp quyền; `settings.read` không suy ra quyền sửa. Patient/Guest không truy cập thiết lập, chỉ đọc projection công khai/tự thân theo hợp đồng riêng.

| Khu vực | Grant đề xuất [P] | Điều kiện bổ sung |
| --- | --- | --- |
| Shell/quyền khả dụng | `settings.read` | Chỉ trả các section được phép; shell không lộ dữ liệu từng mục |
| General, code, bắt buộc, in | `settings.general.read`, `settings.general.manage` | Owner hoặc được cấp rõ ràng; grant ordinary settings không cấp quản trị quyền |
| Danh mục | `catalog.{kind}.read/create/update/deactivate/export` | kind nằm trong allowlist, không cho tạo tên quyền bất kỳ |
| Dịch vụ/giá | `services.read/manage`, `service-prices.manage` | Nha sĩ/người quản lý giá được cấp; tách sửa mô tả và sửa giá |
| Nhà cung cấp/vật liệu/labo | `providers.read/manage`, `materials.read/manage`, `lab-work-types.read/manage` | Giới hạn chi nhánh áp dụng và quyền xem chi phí |
| Thuốc | `medicines.read/manage` | Nhân viên quản lý danh mục; kê đơn vẫn cần quyền kê đơn |
| Nhóm khách hàng | `customer-groups.read/manage` | Không cấp quyền thu tiền, giảm giá hay điểm |
| Nhóm quyền/gán nhân viên | `access-groups.read/manage`, `staff.access.assign` | Owner hoặc admin bảo mật được ủy quyền; không tự nâng quyền/đổi nhóm của mình qua form thường |
| Chi nhánh/ghế | `branches.read/manage`, `chairs.manage` | Owner/người quản lý vận hành được cấp tại phạm vi tương ứng |
| Audit/khôi phục cấu hình | `settings.audit.read`, grant sửa đúng mục | Khôi phục tạo version mới, không xóa audit |

Tên grant là contract đề xuất; khi triển khai phải đăng ký và đối chiếu với quyền sẵn có (`accounts.manage`, `patients.*`, `reception.*`, `appointments.support.handle`). Không chỉ thêm menu mà bỏ kiểm tra API. Không thay đổi mặc nhiên cơ chế Owner kế thừa Dentist; không giả định Owner hiện kế thừa Admin/Assistant. Giữ quyền API cũ đến khi migration phân quyền được đối chiếu và nghiệm thu.

Danh mục hành chính dùng chung trong một phòng khám; dịch vụ/giá/thời lượng đang có identity theo chi nhánh phải giữ ID hiện hành. Tồn, chứng từ, số phiếu và lịch là theo chi nhánh. Quyền xem nhiều chi nhánh là grant phạm vi riêng, không phát sinh từ việc thấy tên chi nhánh trên menu.

## 4. Quy tắc xuyên suốt

| Mã | Quy tắc |
| --- | --- |
| SET-01 | [D] Danh mục đã tham chiếu chỉ ngừng dùng; sửa không cập nhật tên/giá/đơn vị/bảo hành đã chụp trên hồ sơ, kế hoạch, chứng từ cũ. Mục inactive không chọn cho dòng mới; dòng cũ được hiển thị và giữ nguyên khi sửa trường khác. |
| SET-02 | [D/P] Mọi write kiểm quyền hiện tại, version và requestKey tại server, lưu thay đổi + audit + receipt trong một transaction. Retry cùng actor/key/payload trả kết quả cũ; dùng key cho payload/target khác trả MSG111. Đã mất quyền không được replay đọc kết quả cũ. |
| SET-03 | [D] Tên giữ dấu tiếng Việt; trim và chuẩn hóa hoa/thường cho kiểm trùng, không tự bỏ dấu/gộp bản ghi. Code tối đa 40, tên tối đa 160 trừ thuốc tối đa 200, email 254, URL 255, ghi chú 1000 trừ mô tả dịch vụ 2000. [P] Warranty text tối đa 1000. |
| SET-04 | [D] Giá là số nguyên VND không âm, quantity/chuyển đổi dùng số chính xác; không dùng float cho tiền. NULL = chưa xác định, 0 = giá tham khảo 0, không coi cả hai là xác nhận miễn phí. Giới hạn số phải nằm trong kiểu lưu trữ và tính toán an toàn ở server/client. |
| SET-05 | [D/P] Ghi actor, UTC time, scope, action, entity/version, lý do, before/after theo trường được phép; người có quyền đọc audit đúng phạm vi mới xem. Không ghi password/token/bank secret. Không đưa dữ liệu nháp hồ sơ khám vào audit thiết lập. |
| SET-06 | [D/P] Phiên bản hiệu lực ngay sau commit bằng giờ server; có `effectiveAt` trả về, không cho client tự hồi tố/schedule trong bản đầu. Revert = lấy nội dung version cũ, preview và lưu version mới. Version/audit cũ bất biến. |
| SET-07 | [D] Cập nhật cấu hình phải kiểm lại ở thời điểm lưu nghiệp vụ; không tin chỉ phiên bản form vừa tải. Lịch/clinical/finance có quy tắc riêng tiếp tục áp dụng, không có transaction “lưu settings” nào tự xác nhận nghiệp vụ đó. |
| SET-08 | [P] Preview không giữ chỗ/giữ số và không cấp quyền commit. Write kiểm lại tác động trong transaction; tác động thay đổi thì yêu cầu preview lại, không âm thầm áp bản xem cũ. |

## 5. Thiết lập chung

### 5.1. Tự đăng xuất [D/P], OS01, GBR-184

Trường `staffIdleTimeoutMinutes`: select **5, 10, 20, 30, 60, 90, 120**, mặc định **20**. Bản DCMS này luôn có timeout theo v2.5; không chép checkbox tắt timeout đang thấy trên oDental. Màn hình ghi rõ chỉ áp dụng phiên staff web; Patient app giữ chính sách riêng.

[P] Backend lưu `clientKind` và `lastInteractiveActivityAt`/idle deadline theo session. Không dùng riêng `LastSeenAt` đang được cập nhật trên mọi request: polling, SignalR, refresh token và tải nền không kéo dài hoạt động người dùng. UI báo tương tác thật qua endpoint activity có throttle tối đa một lần/30 giây; dùng thời gian server, không nhận timestamp client. Đây là tín hiệu hoạt động của UI, không phải bằng chứng bảo mật rằng con người có mặt. Refresh/handshake/API đều kiểm idle trước khi gia hạn; sau deadline trả 401/MSG07 và thu hồi refresh của phiên, không tự đăng nhập lại từ token cũ. Các tab cùng session dùng cùng deadline.

Giảm timeout có preview số phiên bị ảnh hưởng; áp chính sách mới cho phiên staff đang chạy ở request tiếp theo theo mốc hoạt động cũ, không reset deadline về “bây giờ”. Tăng timeout không phục hồi phiên đã hết hạn. UI có cảnh báo sắp hết và hướng đăng nhập lại; không tự lưu clinical draft khi hết phiên. Bản nhập chỉ giữ tạm an toàn theo cùng người dùng/ngữ cảnh; không ghi bệnh án vào localStorage chưa mã hóa hoặc hiển thị cho người đăng nhập khác.

### 5.2. Hồ sơ bắt buộc [D/P], OS02, GBR-176

| Khóa DTO [P] | Nhãn | Cách kiểm khi bật |
| --- | --- | --- |
| `profileDate` | Ngày lập hồ sơ | Có ngày hợp lệ, không tương lai theo UTC+07; không đồng nhất với ngày Visit đầu |
| `gender` | Giới tính | Giá trị trong tập giới tính được hỗ trợ; không tự gán |
| `birthDate` | Ngày sinh | Có ngày hợp lệ, không tương lai; không suy từ tuổi giả |
| `address` | Địa chỉ | Chuỗi không trắng trong giới hạn form bệnh nhân |
| `phone` | Số điện thoại | Có Phone 1 hợp lệ; không dùng phone để gộp hồ sơ |
| `customerGroup` | Phân nhóm khách hàng | Chọn ID nhóm được phép; inactive đã gắn được giữ khi sửa trường khác |
| `source` | Nguồn | Chọn ID nguồn; cho Khác khi danh mục có, không tự chọn hộ |

Áp dụng tại lần lưu mới/sửa hành chính tiếp theo ở mọi client; hồ sơ cũ vẫn đọc/khám bình thường, không backfill hoặc chặn riêng lệnh khám vì thiếu trường hành chính. Bật required không tự sửa hàng loạt bệnh nhân. Tắt required không xóa giá trị đã có. Preview hiển thị trường thay đổi và số hồ sơ bị thiếu nếu người thao tác có quyền thống kê phù hợp; nếu không thì chỉ mô tả tác động, không lộ danh sách.

[P] Form lấy projection `patientFormPolicyVersion`. Khi cấu hình đổi giữa lúc nhập và lưu: server kiểm version/policy trong transaction; trả conflict để tải policy mới và giữ bản nhập. Sau đó các trường còn thiếu báo MSG02 cạnh ô; không lặng lẽ điền giá trị. Luồng linked patient tự sửa giữ hợp đồng/quyền hiện có, không tự mở quyền chỉnh hồ sơ từ settings.

### 5.3. Mã bệnh nhân [D/P], OS02, GBR-177

- `AutoWithManualOverride` mặc định: bỏ trống để server sinh; nhập tay thì kiểm mã đã chuẩn hóa/unique.
- `AutoOnly`: client disable ô mã; server bỏ qua input mã và sinh mã mới như AT82. Không đổi mã của bệnh nhân cũ.
- `ManualOnly`: phải nhập mã, thiếu trả MSG02; trùng trả MSG201.
- Prefix cấu hình, sequence tăng nguyên tử tại commit. [P] Prefix 0–12 ký tự chữ ASCII/số/gạch nối, sequence số nguyên dương, code hoàn chỉnh ≤40. Không đoán prefix hiện tại: migration lấy từ thuật toán/sequence đang chạy và bảo toàn mã cũ.
- Mã unique **trong toàn phòng khám**, không chỉ chi nhánh; không tái dùng mã đã archive/hủy giao dịch. Đổi chế độ áp dụng bệnh nhân mới; không tự mở sửa mã cũ. Không lấy “mã tiếp theo” trên UI làm reservation.
- [P] Sửa next sequence chỉ được tiến lên giá trị an toàn sau preview; backend kiểm cả mã thủ công đã tồn tại và sequence đã cấp, retry sinh mã không tạo bệnh nhân thứ hai.

### 5.4. Mẫu in và số phiếu thu [D/P], OS03, GBR-183

Các toggle: `internalUseNote`, `showReceiptNumber`, `patientAddress`, `patientPhone`, `paymentMethod`, `dentistAssistant`, `totalFee`, `discount`, `previouslyPaid`, `remaining`, `patientTotalOutstanding`, `signatureLines`, `defaultPayerToPatient`, `defaultCollectorToActor`, `paymentNote`. Nhãn tiếng Việt theo OS03. [P] Giá trị ban đầu: hiện mã phiếu/phương thức/tổng phí/giảm giá/đã trả/còn lại/ghi chú; các tùy chọn còn lại tắt. Đây là mặc định thiết kế, không suy từ checkbox tenant.

- Template layout và footer theo phòng khám; bộ đếm số phiếu riêng từng chi nhánh. `receiptNumberStart` số nguyên dương, mặc định 1 cho chi nhánh mới. Khi đã có số phát hành: chỉ cho mốc mới lớn hơn mọi số đã cấp; không giảm, reset hoặc tái sử dụng. Tắt hiển thị mã không dừng sinh số.
- Preview dùng dữ liệu giả rõ nhãn, không tạo collection, allocation, receipt hoặc tiêu thụ số. Mọi số tiền lấy từ ledger/snapshot đúng receipt; toggle chỉ ẩn/hiện. Không dùng tổng nợ hiện tại để thay số tiền/remaining tại thời điểm phiếu cũ.
- Khi in lại phiếu cũ có thể dùng tùy chọn trình bày hiện tại như AT89; nội dung tiền, payer/collector thực tế, chi nhánh và số đã cấp giữ snapshot cũ. Tổng công nợ hiện tại nếu được yêu cầu in thêm phải ghi rõ “Công nợ tại thời điểm in” và thời gian, tách số liệu lịch sử của phiếu.
- Payer mặc định bệnh nhân và collector mặc định actor chỉ gợi ý khi tạo chứng từ mới; không ghi đè payer thực tế/người đại diện hoặc collector của phiếu cũ. Lưu print event với receipt ID, settings version và thời điểm in nếu module in hỗ trợ audit.
- Settings không bật hoàn tiền, Card/POS, thu âm, phí khám tự động hoặc xuất hóa đơn pháp lý. Chuyển khoản Pending chưa phải đã thu.

## 6. Danh mục và biểu mẫu

Các mục sau dùng quy tắc SET-01–08; ID stable, `version`, `active` và audit có ở mọi loại. Trường Người tạo/ngày tạo chỉ đọc và do server ghi, không tin actor từ client.

| Nhóm | Trường nhập/hiển thị và validation DCMS | Consumer/tác động |
| --- | --- | --- |
| Chẩn đoán | Tên 1–160 bắt buộc, mã ICD tùy chọn ≤40, active; tên chuẩn hóa unique trong catalog; mã có giá trị cũng unique. Liên kết ICD chỉ tham khảo, không xác nhận mã hợp lệ lâm sàng tự động | S19/S23 chọn từ danh mục; giữ nhiều bệnh tại răng/vùng và chẩn đoán tự do theo đặc tả S19. Snapshot tên/mã/version cho lựa chọn mới; không đổi chuỗi legacy hoặc bắt quy đổi hàng loạt |
| Nhóm dịch vụ | Code/tên, parentId tùy chọn; parent cùng phòng khám, không tạo vòng/có chính nó; có thứ tự hiển thị. Category chỉ phân loại, không có giá để ghi phí | Cây hiển thị đường dẫn; không ngừng dùng cha mà vô tình ẩn con active: phải preview và xử lý con trước |
| Dịch vụ/gói | Code unique, tên, categoryId, loại Service/Package, đơn vị Session/Tooth/Jaw/Package, giá tham khảo, thời lượng, mô tả, bảo hành text, trạng thái, phạm vi OfferedOnSite/ReferralOnly/PendingConfirmation. Gói/Included chỉ cấu hình theo hợp đồng điều trị, không biến mọi node cha thành gói | S21/S22/S38, booking; không dùng giá âm cho voucher/hoàn trả; giá thỏa thuận chụp vào kế hoạch. Tồn tại trong cây không đồng nghĩa được thực hiện tại chỗ/đặt công khai |
| Công ty vật liệu | Code unique và tên bắt buộc; loại Supplier/Lab/Both; địa chỉ ≤1000, phone/email optional hợp lệ, active. Tab này lọc Supplier/Both | Chung Provider với tab labo; đổi loại phải kiểm tra ràng buộc material/lab. Không tạo nợ khi lưu |
| Vật liệu/dụng cụ | Code/tên/category bắt buộc; đơn vị gốc riêng, supplier mặc định optional Supplier/Both; giá tham khảo không âm optional; active. Tên có “(kg)” chỉ là nhãn, không thay trường đơn vị | S48/S49/S50 snapshot unit/cost/conversion; tồn theo ledger. Quản lý danh mục con ngay trong drawer với quyền riêng; lưu danh mục con là commit riêng, hủy form vật liệu không rollback nó |
| Công ty labo | Dùng cùng Provider; tên bắt buộc, code unique, địa chỉ/phone/email optional; lọc Lab/Both | S44/S45/S53; không tự tạo đơn labo/công nợ. Không nhân đôi một công ty Both ở hai bảng |
| Loại công việc labo | Code/tên bắt buộc; provider optional Lab/Both; đơn vị bắt buộc (hàm/bán hàm/răng/cái/bộ hoặc đơn vị danh mục hợp lệ); giá tham khảo không âm optional; warranty text và active | Tạo yêu cầu labo mới lấy default có thể sửa theo quyền; request lưu giá/đơn vị/bảo hành snapshot. Không tự xác nhận được bảo hành hoặc nghiệm thu labo |
| Nhóm khách hàng | Code/tên bắt buộc unique, mô tả/ghi chú optional, active; thành viên/count lấy từ liên kết thật | Gắn S05/S06 và điều kiện ưu đãi S55 khi đã có module. Bản đầu không có giá bán thẻ/giá trị/điểm/quy đổi/thời hạn thẻ. Không tự áp ưu đãi khi chỉ gán nhóm |
| Thuốc | Code unique ≤40, tên 1–200, hoạt chất/biệt dược ≤200, hàm lượng/dạng ≤200 mỗi trường, đơn vị gốc và nhãn đóng gói riêng, hướng dẫn ≤1000, giá tham khảo optional, active; `prescriptionEligible` và `internalStockTracked` độc lập | S23 lấy lời dặn mặc định cho dòng mới, bác sĩ sửa theo bệnh nhân. Đơn mua ngoài không cần tồn và không trừ kho. Nội bộ dùng aggregate, quy đổi ra đơn vị gốc nguyên; không thêm lot/FEFO/bán mang về |
| Danh mục phụ | Code/tên unique trong từng kind, active; mô tả optional. Nguồn/nghề/tiền sử/loại CSKH/loại chi không dùng chung ý nghĩa chỉ vì chung schema | Tiền sử hành chính là lời khai có actor/time, không thay ghi chú optional S19; loại chi Supplier/Lab/Payroll được đánh dấu reserved và không tạo phiếu chi tay trùng nguồn |

**Đơn vị/chuyển đổi [D/P]:** hệ số dương chính xác; vật liệu tối đa 3 số lẻ, thuốc quy ra whole base units. Đã có movement thì không sửa nghĩa đơn vị gốc/hệ số cũ: tạo version/định nghĩa mới cho tương lai, giữ snapshot và yêu cầu xử lý số dư trước khi đổi loại. Catalog không cho sửa trực tiếp số dư hoặc giá vốn lịch sử.

**Dịch vụ và đặt lịch [D]:** giữ `SchedulingService.Id`/BranchId và cờ canonical `isInitialExamination`; tối đa một Khám/chi nhánh. Tên Khám và phân loại là trường hệ thống. Thời lượng 1–480 phút, bước slot 1–120; không tái áp giới hạn 30/60 hoặc bội số 15 trong một số dòng Word cũ. Không bật điều trị cho đặt khám mới chỉ bằng `publicBookable`. Chỉnh thời lượng không đổi Appointment/Reservation cũ; catalog dịch vụ phải dùng chung write path/khóa version với `/api/booking-settings`, không tạo hai nguồn thời lượng. Giá tiếp tục có `priceVersion` riêng và write path `/api/service-prices`.

**Chẩn đoán [P]:** API cung cấp options thay danh sách hard-code hiện có. DTO clinical mới có thể thêm reference ID/version song song snapshot, không bắt ngay migration chuỗi trong ContentJson. “Chưa phát hiện bất thường” là lựa chọn kết quả theo hợp đồng S19, không biến thành mã bệnh tùy tiện. Việc sửa danh mục không thay logic tổng hợp chẩn đoán tại răng/vùng/Tổng kết.

## 7. Nhóm quyền và nhân viên

[D] Nhóm = tên bộ grant; bốn seed: Doctors/Dentists → Dentist, Assistants → ClinicAssistant, Receptionists → ClinicAssistant + Reception profile, Accountants → ClinicAssistant + Collections/report profile. Không tạo bốn Identity role mới có đặc quyền ngầm theo tên tiếng Anh. Profile phải được triển khai thành tập grant hữu hạn theo hành vi đã hỗ trợ.

[P] Form gồm code, tên, mô tả, trạng thái, role nền, bảng **Màn hình / Xem / Thêm / Sửa / Ngừng dùng / Xuất / Phạm vi**, và chi tiết quyền nghiệp vụ khi cần. Quyền con cần quyền xem cha; tắt cha preview các grant sẽ thu hồi, không giữ quyền con ẩn nhưng vẫn có hiệu lực. Bản đầu mỗi nhân viên có tối đa một access group trong phòng khám, giữ các Identity roles được cấp riêng; không tự xóa roles khi đổi group. Quyền hiệu lực vẫn bị ràng buộc bởi role nền và scope, không đơn thuần union mọi grant.

- Không cấp quyền nha sĩ/duyệt clinical template cho assistant bằng checkbox. Không tự nâng giới hạn chi nhánh, xem lương/chi phí, quản lý nhóm hoặc cấp quyền vượt quyền ủy quyền của actor.
- Số người là số tài khoản staff đang gắn nhóm, kể cả tài khoản bị khóa; nút chi tiết tách active/locked, chỉ hiện người nằm trong scope. Count không nhập tay. Không ngừng nhóm còn người dùng: chuyển nhóm hợp lệ trong thao tác riêng trước.
- Preview đổi quyền liệt kê grant tăng/giảm và số người bị ảnh hưởng; gán nhóm ở S04/S52 dùng account version + group version + requestKey. Kiểm lại active group và scope khi commit.
- Group grants mới có hiệu lực từ request tiếp theo qua server permission resolver; đổi quyền không cần cấp lại access token mới để bị chặn. Thay đổi giữa lúc đang nhập: form giữ bản nhập an toàn, API từ chối ghi khi mất quyền.
- Giữ quyền thao tác hiện hành khi nâng cấp; migration phải đối chiếu từng tài khoản, không blanket cấp quyền đầy đủ cho seed group.
- Quyền “sửa/xóa thanh toán/phiếu kho” quan sát trên oDental chuyển thành correction/reversal có phê duyệt độc lập theo DCMS; không hard-delete posted ledger. Clinical finalized dùng amendment; Patient archive khác xóa bệnh án.
- Giới hạn kỳ xem thống kê trên oDental là bằng chứng UI. [P] Chưa thêm bộ lọc giới hạn thời gian quyền trong bản đầu khi module báo cáo chưa có contract; không tuyên bố đã tái tạo đủ ma trận báo cáo chỉ vì hiển thị checkbox.

## 8. Chi nhánh, logo và ghế [D/P]

Form chi nhánh: code ≤40 unique, tên ≤160 bắt buộc; địa chỉ ≤1000, phone, email ≤254, website/Facebook ≤255 optional; timezone `Asia/Ho_Chi_Minh` cho clinic hiện tại; active. Thông tin chuyển khoản công khai là bank code/account/payee được người quản lý xác minh, tách khỏi secret tích hợp và không tự đổi phương thức thu.

[P] URL chỉ HTTPS hợp lệ (môi trường dev theo cấu hình), từ chối script/data URL trong ô liên hệ; không crawl URL hoặc truy xuất nội dung từ backend khi lưu. Logo PNG/JPEG/WebP tối đa 2 MiB, kiểm magic bytes/decode/loại bỏ metadata, không SVG/script, không cho tên/path tùy ý. Lưu ngoài Git/webroot; form dùng asset ID, API chỉ trả asset đã kiểm tra. Upload logo và lưu chi nhánh là hai bước; file chưa gắn được dọn theo policy, không hứa rollback file đã upload khi bấm Hủy.

Ghế nằm trong chi nhánh: code/name unique trong branch, Active, trạng thái/bất khả dụng theo scheduling hiện có. Phòng khám đã xác nhận có **ba ghế vật lý**; không seed ba ghế cho mọi chi nhánh mới và không tạo lại ghế đang có. Mỗi ghế một bệnh nhân tại một thời điểm; không tự giải phóng khi chờ thuốc/kết quả; không đặt thời gian tiệt khuẩn mặc định.

Preview ngừng chi nhánh/ghế phải kiểm lịch tương lai, lượt đang diễn ra, block/reservation, nghĩa vụ kho/tài chính/labo theo module đã nối. Có blocker thì 409 business code và links nguồn trong quyền, không tự chuyển/hủy. Module kiểm nghĩa vụ chưa có adapter thì hiển thị không khả dụng và chặn thao tác đó; không giả kết luận “không có nghĩa vụ”. Thay logo/địa chỉ không đổi snapshot chi nhánh trên chứng từ đã phát hành. Timezone không cho sửa khi đã có giao dịch trong bản đầu để giữ ý nghĩa ngày lịch sử.

Không có ô credential liên thông quốc gia trong phase 1. Không sửa mật khẩu/auth/recovery qua form chi nhánh.

## 9. Dữ liệu, API và tích hợp [P]

Đây là hợp đồng dự kiến; chỉ các endpoint ghi rõ “đã có” được đối chiếu từ code, không hàm ý các endpoint mới tồn tại.

| Resource logic | Nội dung tối thiểu / ràng buộc |
| --- | --- |
| `ClinicSettingsVersion` | ClinicId, version, effectiveAt, nội dung general typed, actor/reason; history bất biến, current pointer |
| `ReferenceEntry` | ClinicId, kind allowlist, id/code/name/normalizedName, active/version, metadata typed theo kind; unique(code), unique(normalizedName) theo clinic/kind |
| Service/category | Mở rộng service hiện có và cây category; không tạo ID dịch vụ thứ hai để mất nguồn booking/treatment; catalogVersion, bookingSettingsVersion, priceVersion có trách nhiệm riêng |
| Provider/Material/LabWorkType/Medicine/CustomerGroup | ID/version/active, FK restrict, trường mục 6; cấu trúc typed theo nghiệp vụ, không một JSON blob chứa cả kho và quyền |
| AccessGroup/AccessGroupGrant/StaffAssignment | ClinicId, group version, allowlisted grant, role/scope, assignment version; count từ quan hệ |
| BranchProfile/Chair | Mở rộng identity branch/chair hiện có; contact/logo/bank public và receipt sequence theo branch, không nhân đôi ID |
| SettingsAudit/CommandReceipt | Actor/scope/target/action/version/requestKey/fingerprint/result; unique key theo actor và namespace command; transaction cùng write |
| AuthSession bổ sung | ClientKind, lastInteractiveActivityAt và idle check; không thay session Patient bằng policy staff |

### 9.1. Các route dự kiến

| Method / route | Contract |
| --- | --- |
| `GET /api/settings/capabilities` | Allowed sections/actions/scopes + policy version; không trả data của section không được xem |
| `GET /api/settings/general` | Current version/effectiveAt, idle, requiredFields, patientCode, receiptPrint; không trả sequence chi nhánh chưa được phép |
| `POST /api/settings/general/preview` | Proposed typed payload + expectedVersion → thay đổi, tác động, cảnh báo/blocker trong quyền; không write |
| `PUT /api/settings/general` | `{expectedVersion,requestKey,reason,settings}` → authoritative DTO/version; atomic trong cùng clinic |
| `GET /api/settings/catalogs/{kind}` | Search/status/page/pageSize/sort; trả paged typed DTO theo kind; kind không hợp lệ 400 |
| `POST /api/settings/catalogs/{kind}` | `{requestKey,entry}`; typed field allowlist, không có actor/clinic tùy ý trong entry |
| `PUT /api/settings/catalogs/{kind}/{id}` | `{expectedVersion,requestKey,reason,entry}`; không cho sửa FK sang clinic khác |
| `POST .../{id}/state-preview` và `POST .../{id}/state` | Preview dependencies; commit `{expectedVersion,requestKey,active,reason}`; không có DELETE trong bản đầu |
| `GET /api/settings/catalogs/{kind}/export` | Chỉ kind được hỗ trợ/export granted; cùng query filter, xlsx download, không mutation |
| `GET /api/settings/audit` | Section/entity/date/page; chỉ trả scope được phép, sanitized before/after |
| `POST /api/settings/general/restore-preview` và `POST /api/settings/general/restore` | Version nguồn + expected current version; restore kiểm điều kiện hiện tại và sinh version mới, không rollback ID/sequence đã cấp |
| `GET/POST /api/access-groups`, `GET/PUT /api/access-groups/{id}` | Danh sách/create/read/update nhóm typed grants, có version/key khi write; state theo quy tắc chung |
| `POST /api/access-groups/{id}/preview` | Diff quyền + số người trong scope; actor không được tự yêu cầu grant vượt delegation |
| `PUT /api/staff/{employeeId}/access-group` | Account/group versions, requestKey, groupId, reason; giữ cơ chế account API hiện có, không tạo hồ sơ staff mới |
| `GET/POST /api/settings/branches`, `GET/PUT /api/settings/branches/{id}` | Branch profile; existing ID; version/key cho write; state-preview/state tương tự |
| `POST /api/settings/branches/{id}/logo` | Multipart kiểm file trước, trả asset ID; grant manage + same branch |
| `PUT /api/settings/branches/{id}/receipt-sequence` | Expected sequence version/requestKey/reason/nextNumber; kiểm max issued, không dùng endpoint general để reset số |
| `GET/POST /api/settings/branches/{id}/chairs`, `PUT .../chairs/{chairId}` | Giữ existing chair ID; update/state kiểm dependency và phiên bản |
| `POST /api/auth/session/activity` | Phiên hiện tại từ token, không nhận account/sessionId tùy ý; chỉ touch staff web chưa idle-expired, trả idle deadline server |
| **Đã có:** `/api/booking-settings/options`, `GET/PUT /api/booking-settings/{branchId}` | Tái dùng, không đổi contract trong tài liệu này; settings shell đặt liên kết/adapter cùng nguồn |
| **Đã có:** `GET /api/service-prices/`, `GET/PUT /api/service-prices/{serviceId}` | Tái dùng giá/version; mở rộng catalog không được bypass quyền/price snapshot |

`{kind}` REST không chứa tất cả resource một cách tùy ý: allowlist v1 gồm diagnoses, patient-sources, occupations, intake-history, care-types, expense-categories, service-categories, services, providers, material-categories, materials, lab-work-types, customer-groups, medicines. Provider tabs dùng filter loại; service write là facade ủy quyền sang nguồn booking/price/catalog tương ứng. Payment methods là enum read-only Cash/Transfer, không tạo động bằng API catalog. Các metadata không thuộc kind bị reject, không mass assignment.

Consumer chọn danh mục dùng projection chuyên biệt có quyền nghiệp vụ của consumer, ví dụ `GET /api/reference-options?kind=diagnoses&context=examination&branchId=...&search=...`. Nha sĩ cần chọn bệnh không phải có quyền quản trị settings; server xác minh context/scope và không trả provider cost/grants. Public booking tiếp tục dùng endpoint public hiện có, không dùng API settings.

### 9.2. Ví dụ command general

```json
{
  "expectedVersion": 3,
  "requestKey": "ba4f4cae-14b1-47c4-97c0-7e196030dca9",
  "reason": "Yêu cầu nguồn và ngày sinh khi lưu hồ sơ",
  "settings": {
    "staffIdleTimeoutMinutes": 20,
    "requiredFields": {
      "profileDate": false, "gender": false, "birthDate": true,
      "address": false, "phone": false, "customerGroup": false, "source": true
    },
    "patientCode": { "mode": "AutoWithManualOverride", "prefix": "BN" },
    "receiptPrint": {
      "internalUseNote": false, "showReceiptNumber": true,
      "patientAddress": false, "patientPhone": false, "paymentMethod": true,
      "dentistAssistant": false, "totalFee": true, "discount": true,
      "previouslyPaid": true, "remaining": true, "patientTotalOutstanding": false,
      "signatureLines": false, "defaultPayerToPatient": false,
      "defaultCollectorToActor": false, "paymentNote": true
    }
  }
}
```

Prefix `BN` trong ví dụ chỉ là minh họa. Sequence update là lệnh riêng có version hiện tại; không đưa vào payload mẫu và không thay sequence khi chỉ lưu checkbox. Response trả version mới/effectiveAt và đầy đủ giá trị được server chấp nhận. Nếu preview và commit dùng payload khác thì preview cũ hết giá trị.

### 9.3. Lỗi và xử lý client

| HTTP / code | Trường hợp | Client |
| --- | --- | --- |
| 400 MSG02/MSG04 | Thiếu trường, sai enum/range/FK loại, URL/file không hỗ trợ | Giữ dữ liệu, đánh dấu field; không chuyển sang xử lý stale |
| 400 MSG257 | Giá dịch vụ âm theo v2.5 | Giải thích dùng giảm trừ đúng nghiệp vụ; mã cần bổ sung vào message catalog khi triển khai |
| 401 MSG07 | Phiên/idle hết hạn | Dừng lệnh, đăng nhập lại; không replay tự động bằng tài khoản khác |
| 403 MSG06 | Không đủ action/scope/role | Không cho lưu; không coi là danh sách trống |
| 404 MSG113 | Resource không còn khả dụng theo hợp đồng read | Hiển thị không tìm thấy, không tạo lại bản ghi cùng ID |
| 409 MSG201 | Code hoặc tên unique bị trùng | Trả `field`; sửa giá trị, không yêu cầu tải lại vô điều kiện |
| 409 MSG205 | Mục/đơn vị/nhóm/branch còn tham chiếu hoặc nghĩa vụ ngăn thao tác | Trả blocker type/count/links trong scope; xử lý dependency trước |
| 409 MSG111 | Stale version, requestKey khác fingerprint | Giữ bản nhập, cho đọc bản mới/so sánh và xác nhận lưu lại bằng key mới |
| 503 MSG144 | Consumer kiểm nghĩa vụ/storage/module cần thiết không sẵn sàng | Không ghi một phần; cho retry cùng key khi outcome chưa rõ |

Lỗi dạng `{code,message,field?,currentVersion?,blockers?}`. Mã MSG205/257 đã có trong Word nhưng chưa có đủ mapping trong code hiện tại; phải đăng ký theo đúng trigger trước khi nối UI. Không xử lý mọi 409 thành MSG111; message thành công chỉ xuất hiện sau commit authoritative, không sau preview.

## 10. Seed, chuyển đổi và phần cần quyết định

[D] Bộ seed mục tiêu v2.5: 34 chẩn đoán có mã optional; 14 nhóm dịch vụ và tên dịch vụ tham khảo giá 0; 11 nguồn + Khác; 13 nghề nghiệp; 9 mục tiền sử hành chính; 4 loại chi; **2 phương thức Cash/Transfer**; 4 access group. Không nhập tên dữ liệu thử, tên gần trùng hoặc giá âm từ tenant đang khảo sát. Các nhóm nghiệp vụ reserved vẫn phân biệt với loại chi tay.

[P] Seed có stable key/version và manifest nguồn đã rà soát; chạy lại không overwrite dữ liệu phòng khám chỉnh, không tạo trùng hoặc tự kích hoạt quyền. Seed service mới ở PendingConfirmation và không public-bookable; không seed/đổi canonical Khám theo tên. Mã ICD bỏ trống nếu chưa xác nhận; không tự sáng tác. Thiếu manifest tên cụ thể là hạng mục cần hoàn tất trước migration, không dùng tổng số làm bằng chứng đúng nghiệp vụ.

Migration giữ tất cả ID/sequence và snapshot hiện hành, không thay patient code cũ, không tự tạo thêm branch/chair, không suy nguồn/nhóm/ngày sinh cho patient. Bổ sung field nullable/default tương thích rồi mới bật validation cấu hình; backfill cấu hình theo trạng thái hiện tại đã đối chiếu. Không bật required trước khi form/API consumer đã đọc được policy. Không dùng GET để seed hoặc sửa dữ liệu.

Điểm v2.5 đã hiệu chỉnh trong Word UC ngày 27/10/2026: AT84 dùng 2 phương thức Cash/Transfer theo GBR-180/S58; AT90 chặn non-owner **không có explicit settings grant**. [Ghi chép khảo sát](../research/ODENTAL-SYSTEM-SETTINGS-20261027.vi.md) giữ bằng chứng về khác biệt trước hiệu chỉnh; ghi chú nhập tài liệu ghi lịch sử cập nhật, không đồng nhất với nghiệm thu code.

Q01 còn mở: danh mục thực tế, giá, phạm vi dịch vụ được làm tại chỗ, seed names đầy đủ và đơn vị thuốc/vật liệu cần phòng khám rà soát trước dùng thật. Q08 về ghi phí gói/Included và đổi/dừng gói theo [bộ đặc tả chính](README.md) tiếp tục có hiệu lực. Quy tắc bảo hành/ưu đãi không tự suy từ ô text hoặc thành viên nhóm. Các điểm này không ngăn xây CRUD, version, audit và quyền theo phạm vi ở đây; không dùng dữ liệu minh họa làm cấu hình vận hành thật.

## 11. Nghiệm thu và giao việc

Tiêu chí chi tiết, thứ tự công việc, khoảng trống code đã đối chiếu và ranh giới mock/API/database tại [kế hoạch triển khai S58](../implementation/S58-SYSTEM-SETTINGS-PLAN.vi.md). Đạt UI danh mục không đồng nghĩa đã đạt consumer khám/booking/in/kho hoặc kiểm quyền ở server. Chỉ báo hoàn thành sau khi các consumer thuộc phạm vi của đợt đó dùng cùng dữ liệu và kiểm thử có bằng chứng.
