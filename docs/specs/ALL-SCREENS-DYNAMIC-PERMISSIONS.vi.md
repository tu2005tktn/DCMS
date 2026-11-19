# Phân quyền động cho toàn bộ màn hình nhân viên

Yêu cầu 02/11/2026: mở rộng phân quyền động cho tất cả màn hình hiện có, gồm phần điều trị. Thay thế các giới hạn phạm vi ở bản đầu S58-DYNAMIC-ACCESS-GROUPS, không chỉ thêm checkbox hoặc ẩn menu.

## Nguyên tắc

- Mỗi màn nghiệp vụ có quyền đọc; thao tác ghi, xuất/in đã triển khai có quyền con. Kiểm cả route/menu/nút trên web và endpoint API. Endpoint nghiệp vụ chưa được ánh xạ phải bị phát hiện bằng kiểm tra inventory; không có đường dẫn trực tiếp vượt kiểm tra.
- Quyền động là điều kiện bổ sung cho vai trò chuyên môn, nhân viên phụ trách, membership chi nhánh, trạng thái dữ liệu, version/requestKey/audit hiện có. Không cấp nghề nha sĩ bằng checkbox, không xóa posted ledger hoặc bỏ các điều kiện hoàn tất lâm sàng.
- Owner luôn có các quyền đúng vai trò hiện có để quản trị; không tự kế thừa Admin/Assistant. Staff chưa gán nhóm giữ hành vi nghiệp vụ hiện hành. Nhân viên đã gán nhóm chỉ có grant được thiết lập; các module trước đây lọt qua kiểm tra role-only nay phải được cấp rõ ràng.
- Phiên xác thực và hồ sơ tài khoản của chính mình luôn dùng được để đăng xuất/cập nhật phiên. Đăng nhập, khôi phục mật khẩu, health và cổng khách hàng giữ hợp đồng xác thực/ownership riêng, không bị nhóm quyền nhân viên chi phối.
- Không tạo checkbox cho màn báo cáo hoặc khả năng xuất dữ liệu chưa tồn tại. Các API chức năng nền đã có (CSKH, thông báo) cũng được kiểm quyền riêng.

## Ma trận màn hình và thao tác

| Màn hình | Quyền đọc | Quyền thao tác |
| --- | --- | --- |
| Lịch hẹn / chi tiết lịch | appointments.read | create, reschedule, cancel, no-show |
| Lịch làm việc / lịch lặp | working-schedules.read | manage (preview/xuất bản) |
| Cài đặt đặt lịch | booking-settings.read | manage |
| Tiếp nhận / check-in | reception.read | check-in |
| Bệnh nhân / hồ sơ | patients.read | create, update, archive; alias manage cũ giữ tương thích |
| Xác minh liên kết | patient-links.read | verify (quyết định/thu hồi) |
| Khám / kết luận / lịch sử | examinations.read | manage; các trạng thái nghiệp vụ không đổi |
| Tiếp nhận kết quả cận lâm sàng | examinations.read | investigations.results.receive; bác sĩ đọc kết quả vẫn cần examinations.manage |
| Hình ảnh/tài liệu | documents.read | upload, review |
| Đơn thuốc theo lượt | prescriptions.read | manage |
| Hồ sơ điều trị | treatment.read | dossiers.create/update/delete; plan.manage; revisions.manage; visits.manage |
| Dòng kế hoạch điều trị | treatment.read | items.create/update/delete/price |
| Quá trình thực hiện | treatment.read | actuals.create/update/delete; progress.manage |
| Kế hoạch điều trị dùng lại / API mẫu cũ | treatment.templates.read | manage |
| Giá điều trị | treatment.prices.read | manage |
| Duyệt thương mại điều trị | treatment.commercial.read | approve |
| Thanh toán / phiếu thu | finance.read | payments.create/confirm/cancel, receipts.print, receipt-sequence.manage; quotes.manage, investigations.perform, costs.read/manage giữ hiện có |
| Danh mục dịch vụ khám/CLS | services.read | manage |
| Hộp thư hỗ trợ lịch hẹn | appointments.support.handle | xử lý theo contract hiện có |
| Công việc CSKH / chuyển bác sĩ | care-tasks.read | manage, escalate |
| Thông báo của chính nhân viên | notifications.read | mark-read |
| Tài khoản nhân viên | accounts.read | create/update/state/audit; alias manage cũ giữ tương thích |
| Thiết lập chung / lịch sử / mã bệnh nhân | settings.general.read | manage; cho phép ủy quyền Admin phù hợp |
| Danh mục hồ sơ/nghiệp vụ, gồm nhóm khách hàng | catalog.{kind}.read | create/update/deactivate |
| Dụng cụ/vật liệu và danh mục | catalog.materials.read | create/update/deactivate |
| Nhà cung cấp, loại công việc labo, thuốc | catalog.{kind}.read | create/update/deactivate; medicines.export cho Excel đã có |
| Phân quyền / gán nhóm / lịch sử | access-groups.read | manage, staff.access.assign; settings.audit.read |

## Hợp đồng và kiểm quyền

- Đăng ký đầy đủ grant, nhãn, cha và allowedBaseRoles ở server. Màn checkbox hiển thị toàn bộ danh mục quyền có consumer thật. Quyền cha/con được kiểm ở server và cập nhật nhất quán khi chọn/bỏ trên web.
- Lớp kiểm quyền endpoint tập trung ánh xạ route thực tế + HTTP method sang quyền; đọc lại session hiện hành đã resolve từ DB. Áp dụng cả API staff dùng chung với booking khách hàng, nhưng không chặn khách công khai theo quyền staff.
- Các API options dùng chung cho nhiều màn chấp nhận một trong các quyền màn sử dụng hợp lệ; không vô tình buộc quyền quản lý chỉ để đọc picker.
- Thu ngân có finance.payments.create được chốt phí khám còn chờ bằng đúng giá catalog đã cấu hình; API chốt phí khám chấp nhận quyền này hoặc finance.quotes.manage. Quyền thay giá và yêu cầu lý do thay giá vẫn do dịch vụ phí khám kiểm tra riêng.
- Admin có thể được cấp rõ ràng finance.read để làm quyền cha của chi phí thuê ngoài và thiết lập số phiếu thu. Không tự cấp finance.read cho Admin chưa gán nhóm; các quyền ghi lâm sàng và thu tiền giữ giới hạn vai trò hiện có. Mọi quyền con phải có quyền cha tương thích với cùng vai trò.
- Các kiểm tra chi nhánh/phụ trách trong service giữ nguyên và bổ sung scope assignment cho các module mới. Danh mục hành chính bệnh nhân vẫn dùng chung theo contract; các nghiệp vụ theo Visit/Dossier/branch bị giới hạn theo assignment.
- Sửa đơn giá dòng điều trị yêu cầu thêm items.price; sửa nội dung không thay đơn giá không bị buộc quyền giá. Phiếu đã post tiếp tục dùng correction/reversal theo nghiệp vụ, không hard-delete để mô phỏng checkbox tham chiếu.
- Lập kế hoạch chi tiết yêu cầu quyền sửa hạng mục; thêm/bỏ hạng mục qua lưu cả kế hoạch vẫn phải có items.create/items.delete. Điều chỉnh kế hoạch sau chốt có quyền revisions.manage phụ thuộc items.price. Bản nháp chỉ thuộc tác giả, vẫn kiểm đúng loại dòng khi xóa.
- Middleware đọc lại tối đa 4 MiB JSON metadata để kiểm loại thao tác và chi nhánh, kể cả body chunked; tài liệu multipart tiếp tục dùng giới hạn và kiểm scope riêng của dịch vụ tài liệu.
- Kê đơn/tài liệu tách quyền đọc/ghi khỏi quyền ghi kết luận khám; vẫn kiểm vai trò chuyên môn và phạm vi, không làm đơn thuốc thành bắt buộc.
- API capabilities/allowedActions và UI không quảng bá thao tác mà nhân viên không được cấp. Thu hồi quyền có hiệu lực từ request tiếp theo; UI refresh/focus giữ bản nhập khi chỉ mất quyền ghi.
- Không tự cấp các quyền mới cho nhóm hạn chế hiện có. Không thay role/membership/lịch làm việc hoặc dữ liệu lâm sàng chỉ để gán quyền.
- Tích hợp main PR #39/#40 ngày 03/11: áp kế hoạch dùng lại cần items.create; ngày dự kiến cần items.update; thanh toán theo phiếu cần finance.read/payments.create. Các API người thân giữ Public/ownership. Chi tiết và bằng chứng ở `docs/implementation/VINH-MAIN-INTEGRATION-20261103.vi.md`.

## Bổ sung chức năng ngày 07/11/2026

Các chức năng mới phải xuất hiện thành lựa chọn trong cây phân quyền, với kiểm tra đồng nhất tại API, dịch vụ và giao diện:

| Chức năng | Quyền mới | Quyền cha |
| --- | --- | --- |
| Doanh thu đã thu | `finance.revenue.read` | `finance.read` |
| Thống kê công việc/dịch vụ | `finance.work-statistics.read` | `finance.read` |
| Danh sách/chi tiết nhập hàng | `materials.purchases.read` | `catalog.materials.read` |
| Thêm, sửa, xóa lần nhập | `materials.purchases.create/update/delete` | `materials.purchases.read` |
| Tồn kho theo lô | `materials.stock.read` | `catalog.materials.read` |
| Lịch sử xuất dùng | `materials.usage.read` | `catalog.materials.read` |
| Tạo QR chuyển khoản PayOS | `finance.payos.create` | `finance.payments.create` |
| Lập, sửa, xóa đơn labo trên quá trình điều trị | `treatment.lab.manage` | `treatment.actuals.update` |

- Giữ giới hạn vai trò hiện có: báo cáo theo vai trò được phép đọc tài chính; kho/nhập hàng theo vai trò được phép dùng danh mục vật liệu; PayOS theo vai trò lập phiếu thu; đơn labo theo vai trò sửa quá trình điều trị. Quyền con không mở rộng quyền nghề nghiệp hoặc chi nhánh.
- Nhân viên chưa gán nhóm tiếp tục nhận quyền theo vai trò hiện hành. Nhóm hạn chế đã gán phải được người quản lý cấp rõ ràng các quyền mới; không tự sửa grant trong database, không có migration dữ liệu. Chọn quyền con tự chọn cha và bỏ cha thu hồi con như cơ chế hiện có.
- Menu và route báo cáo dùng quyền riêng. Route `/materials` nhận một trong ba quyền đọc nhập hàng/tồn kho/xuất dùng; chỉ render tab được cấp, và không gọi API của tab bị ẩn. Sửa danh mục không đồng nghĩa được sửa/xóa lần nhập.
- Picker vật liệu còn tồn trong form điều trị (`material-stock/available`) giữ quyền đọc danh mục hiện hành, không bắt quyền xem toàn bộ tồn kho. Options nhà cung cấp và chi nhánh dùng các quyền cha hiện có.
- Chỉ quyền tạo QR bị tách khỏi thu tiền mặt. Đọc/đối soát lệnh PayOS vẫn dùng `finance.read`; webhook và worker giữ cơ chế xác minh, idempotency hiện hành. Không gọi PayOS thật trong kiểm thử tự động.
- Labo vẫn cho đọc theo `treatment.read`; ghi/xóa cần quyền mới. Giữ kiểm tra tác giả/phụ trách, chi nhánh và trạng thái phiếu hiện có.
- Báo cáo công việc lọc theo chi nhánh được phép của người xem khi không truyền `branchId`, và từ chối chi nhánh ngoài phạm vi khi truyền trực tiếp.
- Kiểm chứng: cấp riêng từng quyền đọc, chặn quyền còn thiếu, thu hồi với cùng token; kiểm các thao tác nhập hàng, labo và PayOS; kiểm cây quyền, route/menu và tab trong browser. Kiểm tra trên dữ liệu synthetic/schema cách ly, không thay nhóm nhân viên đang dùng.

## Kiểm chứng

1. Inventory mọi route màn nhân viên và mọi endpoint nghiệp vụ: phải ánh xạ hoặc có lý do baseline/public rõ ràng.
2. Mỗi nhóm màn có ca API cấp quyền đọc, từ chối ghi, cấp ghi rồi thu hồi với cùng token; kiểm role/scope và alias cũ.
3. Hồi quy các bộ lịch hẹn/lịch làm việc, điều trị/phiếu điều trị/giá, thiết lập, tài chính, khám/đơn thuốc/tài liệu và hồ sơ bệnh nhân.
4. Browser mock kiểm route/menu/nút theo quyền; browser API thật trên schema riêng kiểm ít nhất lịch/cấu hình, điều trị và thiết lập được ủy quyền, cùng luồng thu hồi.

## Shared permission keys

A permission key may be referenced by more than one screen definition. The effective grant catalog exposed to APIs and role resolution must contain one canonical entry per key, preserving the existing first declaration and its parent/role metadata. Reusing a key must not make sign-in or permission expansion fail, and must not create duplicate grant choices.

## Cập nhật 19/11/2026: Chủ phòng khám gán được mọi quyền

Yêu cầu người dùng: Chủ phòng khám phải gán được **tất cả** quyền cho bất kỳ nhóm quyền nào; không còn dòng bị mờ "Theo vai trò chuyên môn khác". Mục này thay thế các câu ở trên và trong [S58-DYNAMIC-ACCESS-GROUPS](S58-DYNAMIC-ACCESS-GROUPS.vi.md) nói rằng quyền động là "điều kiện bổ sung cho vai trò chuyên môn", "không cấp nghề nha sĩ bằng checkbox", "giữ giới hạn vai trò hiện có" và `allowedBaseRoles` giới hạn lựa chọn.

### Quy tắc quyền hiệu lực

| Loại tài khoản nhân viên | Quyền hiệu lực |
| --- | --- |
| Chủ phòng khám (Owner) | Như hiện hành: mọi quyền phù hợp vai trò, mọi chi nhánh đang hoạt động. |
| Chưa gán nhóm quyền | Như hiện hành: quyền mặc định theo vai trò (Admin / Bác sĩ / Trợ lý), kể cả các kiểm tra vai trò trong dịch vụ. |
| Đã gán nhóm quyền | **Chỉ** các grant đã tích trong nhóm + phạm vi chi nhánh của assignment (giao với membership đang hoạt động). Không kiểm vai trò chuyên môn của người thao tác (Admin/Bác sĩ/Trợ lý) và không kiểm cờ membership `CanDentist`/`CanManageSchedules` ở bất kỳ API/dịch vụ/màn hình nào. |

- `grant-options` trả `allowedBaseRoles` gồm cả ba vai trò nền cho mọi grant (giữ trường để không vỡ hợp đồng). Lưu/xem trước nhóm không còn từ chối grant vì vai trò nền. Server vẫn từ chối grant không tồn tại, grant thiếu quyền cha, và người quản lý không phải Owner cấp grant mà chính họ không có.
- Vai trò nền của nhóm chỉ còn dùng để ghép nhóm với nhân viên khi gán (nhân viên phải có vai trò đó) và mô tả nhóm; không giới hạn grant.
- Ràng buộc dữ liệu **không phải quyền của người thao tác** giữ nguyên: bác sĩ được chọn cho lịch hẹn, hồ sơ điều trị, lượt khám, dòng thực hiện phải là tài khoản Bác sĩ thành viên chi nhánh; trạng thái, version, requestKey, audit, chi nhánh.
- Quy tắc phạm vi theo người phụ trách áp dụng cho tài khoản Bác sĩ: bác sĩ chỉ xem lịch hẹn của mình và chỉ ghi khám trên lượt khám mình phụ trách. Nhân viên đã gán nhóm **không phải Bác sĩ** được cấp quyền tương ứng thì thao tác trên toàn chi nhánh trong phạm vi assignment (ví dụ trợ lý được cấp `examinations.manage` ghi khám thay bác sĩ phụ trách của lượt khám).
- Rủi ro pháp lý hành nghề (kê đơn, ghi khám, điều trị bởi người không phải bác sĩ) thuộc trách nhiệm cấu hình của Chủ phòng khám; hệ thống ghi audit mọi thao tác như hiện có.
- Phiên `/api/auth/me` (`CurrentUser`) bổ sung `accessAssigned` (bool): `true` khi tài khoản nhân viên (không phải Owner) đã được gán nhóm. Web dùng cờ này thay cho kiểm vai trò ở menu/route/nút: đã gán nhóm → chỉ xét quyền; chưa gán → giữ điều kiện vai trò hiện hành.

### Chức năng bổ sung vào cây phân quyền (rà soát toàn bộ màn hình 19/11/2026)

Rà toàn bộ route web nhân viên, menu, endpoint và các chỗ kiểm vai trò cứng. Các chức năng trước đây chỉ mở theo vai trò, chưa có checkbox hoặc checkbox không có hiệu lực:

| Chức năng | Trước | Sau |
| --- | --- | --- |
| Màn **Thống kê tài chính** (`/finance/statistics`, `GET /api/finance/statistics`) | Chỉ Owner, không có grant | Grant mới `finance.statistics.read` "Xem thống kê tài chính", cha `finance.read`. Mặc định theo vai trò: chỉ Owner. |
| Nút **Trả tiền**, đặt cọc, áp tạm ứng, mất cọc không đến, QR PayOS trên phiếu điều trị | Chỉ Owner; grant `finance.payments.create` không có hiệu lực ở đây | Owner, hoặc nhân viên đã gán nhóm có `finance.payments.create` (đọc ngữ cảnh thanh toán cần `finance.read`). Nhân viên chưa gán nhóm giữ hành vi cũ. |
| Màn **Chăm sóc khách hàng** | Chỉ Trợ lý/Owner có `care-tasks.read` | Nhân viên đã gán nhóm có `care-tasks.read` mở được bất kể vai trò. |
| Danh sách / phiếu **hồ sơ điều trị**, **Kế hoạch điều trị dùng lại** | Chỉ Bác sĩ/Owner | Nhân viên đã gán nhóm có `treatment.read` / `treatment.templates.read`. |
| Tab **Công ty cung cấp vật liệu** trong Thiết lập | Kiểm khóa không tồn tại `catalog.material-suppliers.read` nên chỉ Owner thấy | Dùng đúng grant `catalog.providers.read`. |

Không thêm checkbox cho chức năng chưa tồn tại.

### Chọn quyền trong form

- Tích một quyền (thường là quyền màn hình) tự tích **toàn bộ quyền con, cháu** của nó (kể cả đang bị bộ lọc tìm kiếm che) và các quyền cha còn thiếu. Người dùng bỏ tích từng quyền con không mong muốn; bỏ quyền con không bỏ quyền cha.
- Bỏ tích quyền cha vẫn bỏ toàn bộ quyền con như hiện hành.
- Với người quản lý không phải Owner, tự tích con chỉ chọn những quyền họ được phép cấp.
- Không còn checkbox bị khóa vì vai trò nền; chỉ khóa khi người xem không có quyền sửa nhóm hoặc (không phải Owner) không có chính quyền đó.

### Kiểm chứng

- Unit: grant mặc định theo vai trò không đổi cho Owner và nhân viên chưa gán nhóm; nhóm vai trò nền bất kỳ lưu được mọi grant; nhóm đã gán trả đúng grant đã tích không lọc vai trò; `toggleGrant` tự tích con/cha.
- API: Owner tạo nhóm nền Bác sĩ có `patients.create`, nền Trợ lý có `prescriptions.manage`/`treatment.read`; nhân viên được gán thao tác được API tương ứng, bị chặn khi thu hồi.
