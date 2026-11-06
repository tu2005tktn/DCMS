# Quản lý nhập dụng cụ/vật liệu

Ngày: 04/11/2026. Mở rộng [Thiết lập hệ thống: Dụng cụ / Vật liệu](SYSTEM-SETTINGS-MATERIAL-CATALOG.md). Đây là **sổ nhập hàng** của phòng khám (ghi mỗi lần mua/nhận vật liệu và tiền đã trả), không phải module tồn kho: chưa có số dư, số lô, xuất kho hay tiêu hao. Hạn dùng được ghi theo từng dòng nhập (cập nhật 05/11/2026).

## Quy cách nhập và dùng (tìm hiểu 04/11/2026)

Quy cách thay đổi theo hãng nên đơn vị và số lượng phải ghi theo **đơn vị trên chứng từ mua**; đơn giá là giá của một đơn vị đó. Các quy cách phổ biến tìm được trên catalog nhà phân phối (tham khảo, không phải chuẩn):

| Mục | Quy cách thường gặp | Đơn vị nhập gợi ý |
|---|---|---|
| Trụ implant, abutment, trụ lành thương, vít bít, vít chỉnh nha | Từng cái, đóng gói vô trùng riêng, có số lô và hạn dùng trên nhãn | cái |
| Xương ghép | Lọ vô trùng 0,25 g / 0,5 g / 1 g / 2 g (một số hãng 4 g) | lọ |
| Màng ghép | Từng miếng vô trùng, cỡ 15×20, 20×30, 30×40 mm (một số hãng 25×25, 40×50) | miếng |
| Chỉ khâu tiêu / không tiêu | Hộp 12 sợi (một số loại 24 hoặc 36) | hộp |
| Composite | Ống tiêm 4 g, mỗi màu một ống | tuýp |
| Keo dán | Chai 5 mL | chai |
| Glass ionomer | Viên nang, hộp 50 viên; hoặc bộ bột + dung dịch | hộp / bộ |
| Xi măng nhựa | Ống tiêm trộn tự động hoặc viên nang | ống |
| Sealer nội nha, silicone lấy dấu, gel tẩy trắng, keo dán mắc cài | Theo bộ/kit của hãng (số ống, dung tích khác nhau) | bộ |

Nguyên tắc dùng khi nhập: kiểm nguyên vẹn bao bì vô trùng, đối chiếu nhãn (hãng, mã, **số lô, hạn dùng**) với hóa đơn. Với vật liệu cấy ghép (implant, abutment, vít, xương, màng), số lô phải ghi được để truy vết theo bệnh nhân khi có thu hồi: nguồn tham khảo nhấn mạnh việc ghi lô ngay lúc dùng vì nhãn bị mất sau ca. **Lô và hạn dùng chưa nằm trong bản này** (xem Giới hạn).

Nguồn: danh mục nhà phân phối (Dentsply Sirona, SigmaGraft, Henry Schein, DHP Supply, OHI-S) và hướng dẫn truy vết implant; chưa có tài liệu nào của Bộ Y tế Việt Nam được đối chiếu.

## Trường dữ liệu

| Trường | Ràng buộc |
|---|---|
| Ngày nhập | Bắt buộc, ngày ghi nhận vào sổ (mặc định hôm nay theo giờ phòng khám UTC+7), không ở tương lai |
| Ngày nhận | Không bắt buộc (trống = chưa nhận hàng), không ở tương lai, không trước Ngày nhập |
| Hạn dùng | Không bắt buộc (dụng cụ không hết hạn để trống), ngày ghi trên nhãn của lô nhập, không trước Ngày nhập (có thể ở tương lai xa). Hiển thị nhãn: "Hết hạn" khi đã qua hôm nay, "Sắp hết hạn" khi còn ≤ 90 ngày; chữ, không chỉ màu. Chỉ là một ngày cho cả dòng nhập, chưa có số lô, chưa theo dõi tồn theo lô |
| Dụng cụ/Vật liệu | Bắt buộc, chọn từ danh mục đang dùng; lưu thêm bản chụp tên và danh mục lúc nhập để lịch sử không đổi khi danh mục bị đổi tên hoặc xóa |
| Số lượng | Bắt buộc, số dương tối đa 3 chữ số thập phân, tối đa 1.000.000 |
| Đơn vị | Bắt buộc, 1–40 ký tự (cái, lọ, hộp…). Không có trong danh sách trường người dùng yêu cầu nhưng cần để số lượng có nghĩa |
| Đơn giá | Bắt buộc, VND nguyên ≥ 0, tối đa 1.000.000.000.000 |
| Tổng | Server tính, không nhập: làm tròn số lượng × đơn giá về VND nguyên (nửa lên) |
| Đã thanh toán | VND nguyên, 0 ≤ giá trị ≤ Tổng; mặc định 0 |
| Còn nợ (hiển thị) | Tổng − Đã thanh toán; nhãn: Chưa thanh toán / Còn nợ / Đã thanh toán đủ |

Sổ dùng chung toàn hệ thống như danh mục (không theo chi nhánh). Không có nhà cung cấp, số hóa đơn hay ghi chú trong bản này.

## Quy đổi đơn vị nhập sang đơn vị dùng (05/11/2026)

Lúc nhập ghi theo đơn vị chứng từ (hộp, tuýp, thùng…) nhưng lúc dùng đếm theo đơn vị nhỏ hơn (sợi, gram, gói…). Danh mục mỗi mục có `usageUnit` (xem [SYSTEM-SETTINGS-MATERIAL-CATALOG.md](SYSTEM-SETTINGS-MATERIAL-CATALOG.md)); mỗi dòng nhập có thêm hệ số quy đổi để suy ra số lượng theo đơn vị dùng, làm dữ liệu cho tồn kho sau này.

| Trường dòng nhập | Ràng buộc |
|---|---|
| `conversionFactor` (Hệ số quy đổi) | Số 1 đơn vị nhập = bao nhiêu đơn vị dùng. Mặc định 1; > 0, tối đa 3 chữ số thập phân, tối đa 1.000.000. Request không gửi = 1 |
| `usageUnit` (đơn vị dùng, bản chụp) | Server chụp từ danh mục lúc lưu (null nếu danh mục chưa khai báo); client không gửi. Giữ nguyên khi danh mục đổi sau này |
| `usageQuantity` | Server tính, không nhập: Số lượng × Hệ số, làm tròn 3 chữ số thập phân (nửa lên); phải ≤ 1.000.000.000 |
| `usageUnitPriceVnd` | Chỉ trong DTO, không lưu: Đơn giá ÷ Hệ số, làm tròn 2 chữ số thập phân (nửa lên), để xem giá vốn mỗi đơn vị dùng |

Hệ số và bản chụp đơn vị chốt theo từng lần nhập; đổi hệ số hay đơn vị dùng trong danh mục không đổi dòng cũ. Tổng, Đã thanh toán và Còn nợ không phụ thuộc hệ số. Lỗi hệ số: MSG04 kèm `field=conversionFactor`. Khi danh mục chưa có đơn vị dùng, form ẩn ô hệ số (gửi 1). Dòng nhập cũ: `conversionFactor=1`, `usageQuantity=Quantity`, `usageUnit=null`.

API: `POST`/`PUT` nhận thêm `conversionFactor` (không bắt buộc); DTO thêm `conversionFactor`, `usageUnit`, `usageQuantity`, `usageUnitPriceVnd`. Cột `ConversionFactor` numeric(12,3) not null default 1, `UsageUnit` varchar(40) null, `UsageQuantity` numeric(18,3) not null trên `MATERIAL_PURCHASE`; migration cộng thêm, dòng cũ nhận mặc định và `UsageQuantity=Quantity`.

UI: trong modal, khi mục được chọn có đơn vị dùng, hiện dòng "1 {Đơn vị} = [Hệ số] {đơn vị dùng}" (ô Hệ số mặc định 1) và dòng xem "Quy ra: {usageQuantity} {đơn vị dùng} · {giá mỗi đơn vị dùng}". Bảng: dưới Số lượng ghi nhỏ "= {usageQuantity} {đơn vị dùng}" khi có đơn vị dùng và hệ số khác 1.

## Công ty cung cấp (05/11/2026)

Mỗi dòng nhập có thể ghi công ty cung cấp, lấy từ danh mục **Công ty cung cấp vật liệu** đã có trong Thiết lập hệ thống (`CATALOG_PROVIDER`, loại Cung cấp vật liệu hoặc Cả hai; xem [S58-SUPPLY-CATALOGS.md](S58-SUPPLY-CATALOGS.md)). Không tạo danh mục mới.

| Trường | Ràng buộc |
|---|---|
| `supplierId` | Không bắt buộc. Nếu có: công ty phải đang dùng và loại ≠ Labo, không thì 400 `PURCHASE_SUPPLIER_INVALID` field=`supplierId` "Công ty cung cấp không còn dùng. Vui lòng chọn công ty khác." |
| `supplierName` | Server chụp tên lúc lưu (null nếu không chọn); đổi tên hay ngừng dùng công ty sau đó không đổi dòng cũ |

API: `POST`/`PUT` nhận thêm `supplierId`; DTO thêm `supplierId`, `supplierName`. Cột `SupplierId` uuid null (FK Restrict tới `CATALOG_PROVIDER`) và `SupplierName` varchar(160) null; migration cộng thêm, dòng cũ để trống. Bộ lọc danh sách chưa có theo công ty.

UI: trong modal có ô chọn "Công ty cung cấp" (không bắt buộc, tìm kiếm, tải từ `GET /api/system-settings/reference-options?kind=suppliers`) kèm nút "+" bên cạnh. Bấm "+" mở hộp nhỏ **Thêm công ty cung cấp** (chỉ có ô Tên công ty, bắt buộc; số điện thoại, email, địa chỉ để trống và bổ sung sau trong Thiết lập hệ thống) và lưu qua API danh mục công ty hiện có (`POST /api/system-settings/settings/catalogs/providers`, loại `Supplier`, mã tự sinh dạng `NCC-XXXXXX`, người dùng đổi được trong Thiết lập hệ thống). Lưu xong, công ty mới được chọn sẵn trong ô. Nút "+" chỉ bật khi có quyền `catalog.providers.create`. Tên trùng báo lỗi tại ô Tên và giữ hộp. Bảng nhập hàng hiển thị công ty dưới tên vật liệu ("NCC: …") khi có.

Số lượng và Hệ số quy đổi trong modal hiển thị số thường (1, 2,5), không ép 3 chữ số thập phân; ràng buộc tối đa 3 chữ số vẫn do server kiểm. Khung quy đổi không đổi màu nền, chỉ gồm "1 {đơn vị nhập} = [hệ số]" (không lặp đơn vị dùng sau ô nhập) và dòng "Quy ra".

## Quyền

Dùng quyền danh mục hiện có (`catalog.materials.read/create/update/deactivate`), tức Dentist hiệu lực như tab Dụng cụ / Vật liệu. Không thêm khóa quyền mới. Ghi chú: người xem được sổ nhập cũng thấy giá và số tiền đã trả; nếu cần tách quyền tài chính thì thêm khóa quyền riêng sau.

## API

Tiền tố `/api/system-settings/material-purchases`, `RequireAuthorization`, mọi lệnh kiểm quyền trên server.

- `GET ?q=&from=&to=&payment=&page=&pageSize=` — `q` tìm theo tên vật liệu (bỏ hoa thường); `from`/`to` lọc theo Ngày nhập (yyyy-MM-dd); `payment` ∈ `unpaid|partial|paid`; pageSize ∈ 20/50/100. Trả `{items,total,page,pageSize,totals:{totalVnd,paidVnd,dueVnd}}` với `totals` tính trên toàn bộ kết quả đã lọc, không chỉ trang hiện tại.
- `GET /{id}` — đọc một dòng (dùng cho "đọc lại phiên bản").
- `POST` `{enteredOn, receivedOn?, expiresOn?, materialId, quantity, unit, unitPriceVnd, paidVnd, requestKey}`.
- `PUT /{id}` thêm `expectedVersion`.
- `DELETE /{id}` `{expectedVersion, requestKey}` (xóa mềm).

DTO: `{id, enteredOn, receivedOn, expiresOn, materialId, materialName, categoryName, quantity, unit, unitPriceVnd, totalVnd, paidVnd, dueVnd, version}`.

Quy tắc chung như danh mục: `requestKey` bắt buộc, chạy trong transaction với khóa advisory của module, replay cùng actor+lệnh+payload trả kết quả gốc, khác → MSG111/409; version lệch → MSG111/409; không tìm thấy hoặc đã xóa → MSG113/404; lỗi dữ liệu → MSG04 kèm `field`. Receipt/audit dùng bảng `SYSTEM_CATALOG_CHANGE` hiện có. Mã lỗi riêng (không gán số MSG mới): `PURCHASE_PAID_EXCEEDS_TOTAL`/400/field=paidVnd "Số đã thanh toán không được lớn hơn Tổng."; `PURCHASE_MATERIAL_INACTIVE`/400/field=materialId "Dụng cụ/vật liệu đã bị xóa. Vui lòng chọn mục khác."; `PURCHASE_DATE_INVALID`/400/field=receivedOn "Ngày nhận không được trước Ngày nhập hoặc ở tương lai." (cùng mã cho enteredOn ở tương lai, field=enteredOn); Hạn dùng trước Ngày nhập → `PURCHASE_DATE_INVALID`/400/field=expiresOn "Hạn dùng không được trước Ngày nhập.".

## Dữ liệu và rollout

Bảng `MATERIAL_PURCHASE`: Id, MaterialId (FK Restrict tới `MATERIAL_CATALOG_ITEM`), MaterialName/CategoryName (bản chụp), EnteredOn (date), ReceivedOn (date, null), ExpiresOn (date, null; cột thêm bằng migration `20261105090000_MaterialPurchaseExpiry`, dòng cũ để trống), Quantity (numeric(12,3)), Unit (40), UnitPriceVnd (bigint), TotalVnd (bigint), PaidVnd (bigint), Version (concurrency token), IsDeleted, CreatedAt/UpdatedAt, CreatedBy/UpdatedBy. Chỉ mục (EnteredOn desc, Id) lọc `NOT IsDeleted`. Migration cộng thêm, không seed, không đổi dữ liệu cũ. Áp migration vào `dcms_dev` cần người dùng duyệt riêng (API chỉ migrate khi chạy với `-Migrate`).

## UI (web)

**Vị trí (cập nhật 04/11/2026):** Dụng cụ - vật liệu là chức năng chính, có mục riêng **Dụng cụ - vật liệu** (nhóm "Chuyên môn") trên thanh chức năng chính, đường dẫn `/materials`, hiện khi có quyền `catalog.materials.read`. Trang chỉ chứa **sổ nhập hàng** (không có tab). Danh mục dụng cụ/vật liệu và Danh mục (nhóm) vẫn nằm trong Thiết lập hệ thống → Dụng cụ / Vật liệu như cũ. API, quyền và dữ liệu giữ nguyên.

- Bố cục phẳng, gọn như sổ nhập/mua hiện dùng: không đóng trong thẻ/hộp. Hàng lọc ở trên cùng (Từ ngày, Đến ngày, ô tìm tên, lọc thanh toán, nút làm mới), dòng tóm tắt "Tổng cộng / Đã trả / Còn lại" theo bộ lọc, rồi bảng viền mỏng; nút "+" xanh ở đầu cột Thao tác để thêm mới, mỗi hàng có biểu tượng sửa/xóa; chân bảng ghi "Có N kết quả.". Các mô tả dưới đây về cột và modal giữ nguyên (không còn hàng tổng cộng trong bảng).
- Bảng: STT, Ngày nhập, Ngày nhận (trống hiện "Chưa nhận"), Hạn dùng (trống hiện "—", kèm nhãn Hết hạn/Sắp hết hạn), Tên dụng cụ/vật liệu (kèm danh mục nhỏ bên dưới), Số lượng (kèm đơn vị), Đơn giá, Tổng, Đã thanh toán, Còn nợ (kèm nhãn), Thao tác Sửa/Xóa. Số tiền căn phải, định dạng VND; hàng tổng cộng phía dưới (Tổng, Đã thanh toán, Còn nợ) theo bộ lọc.
- Thanh công cụ: tìm theo tên, khoảng Ngày nhập, lọc thanh toán, nút Thêm mới.
- Modal thêm/sửa: Ngày nhập, Ngày nhận, Hạn dùng (không bắt buộc), Dụng cụ/Vật liệu (chọn có tìm kiếm, nhóm theo danh mục), Số lượng, Đơn vị (ô nhập có gợi ý theo bảng quy cách trên), Đơn giá, Đã thanh toán; Tổng và Còn nợ tính tại chỗ chỉ để xem. Nút Lưu chặn gửi lặp; lỗi validation/mạng/stale giữ nguyên nội dung kèm "Đọc lại phiên bản, giữ nội dung". Xóa hỏi Đồng ý/Không đồng ý.
- Loading/empty/error/retry và responsive 390/768/1440 theo Ant Design hiện có; trạng thái thanh toán không chỉ dựa vào màu.

## Nghiệm thu

Backend (PostgreSQL cách ly): tạo/đọc/sửa/xóa, tính Tổng làm tròn, Đã thanh toán > Tổng bị chặn, ngày nhận trước ngày nhập hoặc ở tương lai, số lượng 0/âm/>3 số thập phân, vật liệu đã xóa, bản chụp tên giữ nguyên khi danh mục đổi tên, lọc/tổng theo bộ lọc, requestKey replay/khác payload, version lệch, quyền. Web (API mock): tab, bảng và hàng tổng, form tính Tổng/Còn nợ, lỗi giữ form, xóa, bộ lọc, responsive. Build backend/web.

## Giới hạn

Tồn kho và xuất dùng theo `MATERIAL-STOCK.md`: sửa/xóa dòng nhập làm tồn âm bị chặn (`MATERIAL_STOCK_NEGATIVE`). Chưa có: số lô, số hóa đơn, ghi chú, nhiều dòng trong một phiếu, theo chi nhánh, tách quyền tài chính. Lô/hạn dùng được khuyến nghị làm tiếp cho nhóm vật liệu cấy ghép vì cần truy vết.

## Số lô (06/11/2026)

Mỗi dòng nhập có thêm **Số lô** (`lotNumber`): không bắt buộc, ghi theo nhãn trên bao bì, trim, tối đa 60 ký tự, lỗi → MSG04 `field=lotNumber`. Cột "Số lô" nằm cạnh Hạn dùng trong bảng nhập hàng; ô "Số lô" nằm cạnh Hạn dùng trong modal. DTO, `POST` và `PUT` thêm `lotNumber` (null = để trống). Thẻ Tồn kho (`MATERIAL-STOCK.md`) hiển thị số lô của từng lần nhập. Số lô không có ràng buộc duy nhất; xuất dùng vẫn chưa gắn với lô cụ thể.
