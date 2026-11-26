# Test bằng Postman theo từng tab của báo cáo Integration Test (5.2)

Có hai cách. **Cách A (import sẵn)** nhanh nhất, nên dùng. **Cách B (thủ công)** để tự gõ từng case, xem ở các mục 1 đến 11.

## Cách A. Import sẵn bộ 85 case (làm theo là chạy)

Thư mục `docs/test/postman/` có hai file:
- `DCMS-Integration-Tests.postman_collection.json`: 96 request (85 case của báo cáo, cộng 5 request đăng nhập và 6 request gửi lần 2 hoặc kiểm tra kết quả cho các case "gửi 2 lần"). Mỗi request đã có sẵn body, vai trò đăng nhập và đoạn kiểm tra tự động (status, mã lỗi, `field`).
- `DCMS-local.postman_environment.json`: Environment `DCMS local` gồm `baseUrl` và `password`.

**Các bước**

1. Bật Docker và API:
   ```powershell
   docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
   ```
   ```powershell
   .\infrastructure\scripts\start-api.ps1
   ```
   Mở `http://localhost:5080/health` thấy phản hồi là API đã chạy.
2. Postman → **Import** → kéo cả hai file `.json` vào.
3. Góc phải trên chọn Environment **DCMS local**. Bấm biểu tượng con mắt → **Edit**, điền giá trị cho `password` (mật khẩu demo, lấy từ `DEV_SEED_PASSWORD` trong `.env`), rồi Save.
4. Chuột phải Collection **DCMS Integration Tests (5.2)** → **Run collection** → **Run**. Postman chạy lần lượt từ thư mục `0. Đăng nhập` tới `11. Material`.
5. Xem kết quả ở tab Results: mỗi request có các dòng PASS/FAIL. Chụp màn hình phần tổng kết làm bằng chứng.

Mỗi request có thêm một dòng test màu xanh tên đúng dòng trạng thái, ví dụ `HTTP 409 Conflict` (hiện trong **Test Results** và trong **Console**), giống trường `StatusLine` của bộ test C#.

Muốn chạy riêng một case: mở request, bấm **Send**, xem phần **Test Results** của phản hồi. Nhớ chạy thư mục `0. Đăng nhập` trước (token hết hạn sau khoảng 15 phút thì chạy lại thư mục này).

**Chạy lại lần hai:** các case tạo mới sẽ báo trùng (409) nếu dùng lại dữ liệu cũ. Vào Collection → tab **Variables**, đổi `run` từ `01` sang `02` (rồi `03`...) và Save. Mọi tên, mã, email, số điện thoại và `requestKey` đổi theo `run`.

**Lưu ý**
- Dữ liệu ghi vào `dcms_dev` và không tự xóa.
- Các id đã điền cứng theo `dcms_dev` hiện tại: chi nhánh A = `1`, chi nhánh B = `3`, nha sĩ = `3`, dịch vụ "Khám" = `3`, dịch vụ "Cạo vôi răng, đánh bóng" = `7`. Nếu bạn dựng lại dữ liệu thì dùng Find and Replace trong Postman để sửa.
- Bộ này đã được kiểm tra cú pháp (JSON và script) nhưng chưa chạy thử trực tiếp với API. Nếu có case FAIL, gửi lại phần Test Results và phản hồi của request đó để chỉnh.

---

## Cách B. Thủ công: body copy là dán theo từng case

Mỗi case có sẵn **method, URL, tài khoản, body, kết quả mong đợi**. Chạy từ trên xuống dưới trong từng mục, vì có case dùng lại dữ liệu của case trước. Trong cách này bạn tự tạo request nên cần thêm bước chuẩn bị dưới đây.

## 0. Chuẩn bị (chỉ cho Cách B)

1. Bật Docker và API: `docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d`, rồi `.\infrastructure\scripts\start-api.ps1` (API ở `http://localhost:5080`).
2. Mọi body bên dưới là **giá trị cố định**, copy dán là chạy. Chỉ cần đăng nhập trước (mục 0.1).
3. Các id có sẵn trong `dcms_dev` đã điền thật: chi nhánh A = `1`, chi nhánh B = `3`, nha sĩ = `3`, dịch vụ "Khám" = `3`, dịch vụ "Cạo vôi răng, đánh bóng" = `7`.
4. Một số URL có chỗ `<ID_...>`: thay bằng `id` lấy từ phản hồi của case tạo ngay trước đó (ví dụ `patient.id` của PAT-01).
5. **Case tạo mới chỉ chạy được một lần** với cùng dữ liệu (lần hai sẽ báo trùng, 409). Muốn chạy lại: đổi `01` thành `02` trong tên, mã, email, số điện thoại, và đổi vài chữ số cuối của `requestKey`.

### 0.1. Đăng nhập (mỗi vai trò một request)

Tạo Environment `DCMS local` có 2 biến: `password` (mật khẩu demo, lấy từ `DEV_SEED_PASSWORD` trong `.env`) và `token` (để trống). Ở Collection → Authorization chọn **Bearer Token** với giá trị `{{token}}`.

`POST http://localhost:5080/api/auth/sign-in`, chọn **No Auth**. Tab **Scripts** → chọn **After** (tên khác: *Post-response*) → dán:

```js
pm.environment.set("token", pm.response.json().accessToken);
```

Body, đổi `userName` theo vai trò (`admin.demo`, `owner.demo`, `dentist.demo`, `assistant.demo`, `guest.demo`):

```json
{ "userName": "admin.demo", "password": "{{password}}", "deviceLabel": "postman" }
```

Trước mỗi mục, chạy request đăng nhập đúng vai trò. Token sống khoảng 15 phút, hết thì đăng nhập lại. Case ghi "No Auth" thì chọn **No Auth** cho request đó.

Mọi lỗi nghiệp vụ trả về dạng `{ "code": "...", "message": "...", "field": "..." }`.

---
## 1. Staff Account (ACC-01..06) — `POST http://localhost:5080/api/accounts`

Đăng nhập **admin.demo** (trừ ACC-05, ACC-06).

**ACC-01** — Tạo tài khoản hợp lệ → **201**, `MSG14`

```json
{ "userName": "nv.01", "password": "Abcdef1!x", "fullName": "Nguyễn Văn An", "employeeCode": "NV01", "email": "nv01@dcms.vn", "phone": "0700000001", "roles": ["ClinicAssistant"] }
```

**ACC-02** — Trùng tên đăng nhập → **409**, `MSG139`, `field = userName`

```json
{ "userName": "nv.01", "password": "Abcdef1!x", "fullName": "Nguyễn Văn An", "employeeCode": "NVB01", "email": "nvb01@dcms.vn", "phone": "0800000001", "roles": ["ClinicAssistant"] }
```

**ACC-03** — Trùng mã nhân viên → **409**, `MSG139`, `field = employeeCode`

```json
{ "userName": "nv.c01", "password": "Abcdef1!x", "fullName": "Nguyễn Văn An", "employeeCode": "NV01", "email": "nvc01@dcms.vn", "phone": "0500000001", "roles": ["ClinicAssistant"] }
```

**ACC-04** — Tên đăng nhập có khoảng trắng → **400**, `MSG04`

```json
{ "userName": "nv an", "password": "Abcdef1!x", "fullName": "Nguyễn Văn An", "employeeCode": "NVD01", "email": "nvd01@dcms.vn", "phone": "0300000001", "roles": ["ClinicAssistant"] }
```

**ACC-05** — Đăng nhập **assistant.demo**, gửi body ACC-01 đổi `userName` thành `nv.e01` → **403**, `MSG06`

```json
{ "userName": "nv.e01", "password": "Abcdef1!x", "fullName": "Nguyễn Văn An", "employeeCode": "NVE01", "email": "nve01@dcms.vn", "phone": "0700000001", "roles": ["ClinicAssistant"] }
```

**ACC-06** — **No Auth**, cùng body ACC-05 → **401**, `MSG07`

---

## 2. Customer Register (REG-01..09) — `POST http://localhost:5080/api/auth/register` (No Auth)

**REG-01** → **201**, `MSG11`, có `accountId`

```json
{ "fullName": "Lê Văn Nam", "phoneNumber": "0900000001", "email": "nam01@gmail.com", "password": "Abcdef1!x", "confirmPassword": "Abcdef1!x" }
```

**REG-02** — Trùng số điện thoại → **409**, `MSG139`

```json
{ "fullName": "Lê Văn Nam", "phoneNumber": "0900000001", "email": "nam201@gmail.com", "password": "Abcdef1!x", "confirmPassword": "Abcdef1!x" }
```

**REG-03** — Trùng email → **409**, `MSG139`

```json
{ "fullName": "Lê Văn Nam", "phoneNumber": "0800000001", "email": "nam01@gmail.com", "password": "Abcdef1!x", "confirmPassword": "Abcdef1!x" }
```

**REG-04** — Họ tên rỗng → **400**, `MSG02`

```json
{ "fullName": "", "phoneNumber": "0700000001", "email": "nam401@gmail.com", "password": "Abcdef1!x", "confirmPassword": "Abcdef1!x" }
```

**REG-05** — SĐT 5 số → **400**, `MSG04`

```json
{ "fullName": "Lê Văn Nam", "phoneNumber": "12345", "email": "nam501@gmail.com", "password": "Abcdef1!x", "confirmPassword": "Abcdef1!x" }
```

**REG-06** — Email không có @ → **400**, `MSG04`

```json
{ "fullName": "Lê Văn Nam", "phoneNumber": "0500000001", "email": "nam-gmail.com", "password": "Abcdef1!x", "confirmPassword": "Abcdef1!x" }
```

**REG-07** — Xác nhận mật khẩu không khớp → **400**, `MSG04`

```json
{ "fullName": "Lê Văn Nam", "phoneNumber": "0300000001", "email": "nam701@gmail.com", "password": "Abcdef1!x", "confirmPassword": "Abcdef1!y" }
```

**REG-08** — Mật khẩu yếu → **400**, `MSG109`

```json
{ "fullName": "Lê Văn Nam", "phoneNumber": "0300000001", "email": "nam801@gmail.com", "password": "abc", "confirmPassword": "abc" }
```

**REG-09** — Đăng nhập bằng tài khoản vừa đăng ký ở REG-01 → **200**, có `accessToken`. `POST http://localhost:5080/api/auth/sign-in`, No Auth:

```json
{ "userName": "0900000001", "password": "Abcdef1!x" }
```

---

## 3. Patient (PAT-01..09) — `POST http://localhost:5080/api/patients`

Đăng nhập **assistant.demo** (trừ PAT-08, PAT-09).

**PAT-01** → **201**, `patientCode` dạng `BN000xxx`, `MSG222`. Ghi lại `patient.id` trong phản hồi (dùng ở mục 6).

```json
{ "fullName": "Trần Thị Mai 01", "birthDate": "1990-05-20", "gender": "FEMALE", "phone": "0900000001", "address": "12 Lê Lợi, Quận 1", "duplicatesReviewed": true, "requestKey": "aaaaaaaa-0000-4000-8000-000000000001" }
```

**PAT-02** — Gửi **2 lần liên tiếp** đúng body này → cả hai **201**, cùng `patientCode`, chỉ có 1 bệnh nhân. Ghi lại `patient.id` (dùng ở mục 6).

```json
{ "fullName": "Phạm Văn Bình 01", "birthDate": "1985-03-12", "gender": "MALE", "phone": "0800000001", "address": "45 Nguyễn Huệ, Quận 1", "duplicatesReviewed": true, "requestKey": "00000000-0000-4000-8000-000200000001" }
```

**PAT-03** — Họ tên rỗng → **400**, `MSG02`

```json
{ "fullName": "", "birthDate": "1990-05-20", "gender": "FEMALE", "phone": "0700000001", "address": "12 Lê Lợi, Quận 1", "duplicatesReviewed": true, "requestKey": "aaaaaaaa-0000-4000-8000-000000000002" }
```

**PAT-04** — Ngày sinh tương lai → **400**, `MSG04`

```json
{ "fullName": "Trần Thị Mai 01", "birthDate": "2099-01-01", "gender": "FEMALE", "phone": "0700000001", "address": "12 Lê Lợi, Quận 1", "duplicatesReviewed": true, "requestKey": "aaaaaaaa-0000-4000-8000-000000000003" }
```

**PAT-05** — SĐT 5 số → **400**, `MSG04`

```json
{ "fullName": "Trần Thị Mai 01", "birthDate": "1990-05-20", "gender": "FEMALE", "phone": "12345", "address": "12 Lê Lợi, Quận 1", "duplicatesReviewed": true, "requestKey": "aaaaaaaa-0000-4000-8000-000000000004" }
```

**PAT-06** — Giới tính ngoài danh sách → **400**, `MSG04`

```json
{ "fullName": "Trần Thị Mai 01", "birthDate": "1990-05-20", "gender": "ROBOT", "phone": "0700000001", "address": "12 Lê Lợi, Quận 1", "duplicatesReviewed": true, "requestKey": "aaaaaaaa-0000-4000-8000-000000000005" }
```

**PAT-07** — Nghi trùng (cùng tên, ngày sinh, SĐT với PAT-01) mà chưa xác nhận → **400**, `MSG02`

```json
{ "fullName": "Trần Thị Mai 01", "birthDate": "1990-05-20", "gender": "FEMALE", "phone": "0900000001", "address": "12 Lê Lợi, Quận 1", "duplicatesReviewed": false, "requestKey": "aaaaaaaa-0000-4000-8000-000000000006" }
```

**PAT-08** — Đăng nhập **guest.demo** → **403**, `MSG06`

```json
{ "fullName": "Trần Thị Mai 01", "birthDate": "1990-05-20", "gender": "FEMALE", "phone": "0500000001", "address": "12 Lê Lợi, Quận 1", "duplicatesReviewed": true, "requestKey": "aaaaaaaa-0000-4000-8000-000000000007" }
```

**PAT-09** — **No Auth**, cùng body PAT-08 → **401**, `MSG07`

---

## 4. Family Member (FAM-01..09) — `POST http://localhost:5080/api/me/family-members`

Đăng nhập **guest.demo** (trừ FAM-06, FAM-07).

**FAM-01** → **201**, `version = 1`

```json
{ "fullName": "Nguyễn Thị Lan 01", "relationship": "Mother", "birthDate": "1965-03-12", "gender": "FEMALE", "contactPhone": "0912345678", "requestKey": "aaaaaaaa-0000-4000-8000-000000000008" }
```

**FAM-02** — Gửi **2 lần liên tiếp** đúng body này, rồi `GET http://localhost:5080/api/me/family-members?keyword=Bình 01` → `total = 1`

```json
{ "fullName": "Nguyễn Văn Bình 01", "relationship": "Father", "birthDate": "1960-07-01", "gender": "MALE", "contactPhone": "0912345678", "requestKey": "00000000-0000-4000-8000-000400000001" }
```

**FAM-03** — Họ tên rỗng → **400**, `FAMILY_MEMBER_VALIDATION`, `field = fullName`

```json
{ "fullName": "", "relationship": "Mother", "birthDate": "1965-03-12", "gender": "FEMALE", "contactPhone": "0912345678", "requestKey": "aaaaaaaa-0000-4000-8000-000000000009" }
```

**FAM-04** — Quan hệ ngoài danh sách → **400**, `field = relationship`

```json
{ "fullName": "Nguyễn Thị Lan 01", "relationship": "Friend", "birthDate": "1965-03-12", "gender": "FEMALE", "contactPhone": "0912345678", "requestKey": "aaaaaaaa-0000-4000-8000-000000000010" }
```

**FAM-05** — Ngày sinh tương lai → **400**, `field = birthDate`

```json
{ "fullName": "Nguyễn Thị Lan 01", "relationship": "Mother", "birthDate": "2099-01-01", "gender": "FEMALE", "contactPhone": "0912345678", "requestKey": "aaaaaaaa-0000-4000-8000-000000000011" }
```

**FAM-06** — Đăng nhập **dentist.demo** → **403**, `MSG06`

```json
{ "fullName": "Nguyễn Thị Lan 01", "relationship": "Mother", "birthDate": "1965-03-12", "gender": "FEMALE", "contactPhone": "0912345678", "requestKey": "aaaaaaaa-0000-4000-8000-000000000012" }
```

**FAM-07** — **No Auth**, cùng body FAM-06 → **401**, `MSG07`

**FAM-08** — Giới tính rỗng → **400**, `field = gender`

```json
{ "fullName": "Nguyễn Thị Lan 01", "relationship": "Mother", "birthDate": "1965-03-12", "gender": "", "contactPhone": "0912345678", "requestKey": "aaaaaaaa-0000-4000-8000-000000000013" }
```

**FAM-09** — SĐT liên hệ sai định dạng → **400**, `field = contactPhone`

```json
{ "fullName": "Nguyễn Thị Lan 01", "relationship": "Mother", "birthDate": "1965-03-12", "gender": "FEMALE", "contactPhone": "12345", "requestKey": "aaaaaaaa-0000-4000-8000-000000000014" }
```

---

## 5. Appointment (APT-01..10) — `POST http://localhost:5080/api/appointments` (**No Auth**)

Tab này chỉ kiểm tra lỗi nhập form, không đặt lịch thật. Cả 10 case đều → **400**.

**APT-01** — Chưa chọn chi nhánh → `MSG04`

```json
{ "branchId": 0, "booker": { "name": "Lê Văn Nam", "phone": "0987654321", "email": "nam@gmail.com" }, "attendee": { "name": "Lê Văn Nam", "birthDate": "1990-01-01" }, "attendeeRelation": "Self", "kind": "Standard", "source": "App", "serviceId": 3, "reason": "Đau răng", "dentistId": 3, "startAt": "2030-01-15T09:00:00+07:00", "durationMinutes": 30, "requestKey": "aaaaaaaa-0000-4000-8000-000000000015", "scheduleVersion": 1 }
```

**APT-02** — Chưa chọn nha sĩ → `MSG04`

```json
{ "branchId": 1, "booker": { "name": "Lê Văn Nam", "phone": "0987654321", "email": "nam@gmail.com" }, "attendee": { "name": "Lê Văn Nam", "birthDate": "1990-01-01" }, "attendeeRelation": "Self", "kind": "Standard", "source": "App", "serviceId": 3, "reason": "Đau răng", "dentistId": 0, "startAt": "2030-01-15T09:00:00+07:00", "durationMinutes": 30, "requestKey": "aaaaaaaa-0000-4000-8000-000000000016", "scheduleVersion": 1 }
```

**APT-03** — Chưa chọn giờ → `MSG04`

```json
{ "branchId": 1, "booker": { "name": "Lê Văn Nam", "phone": "0987654321", "email": "nam@gmail.com" }, "attendee": { "name": "Lê Văn Nam", "birthDate": "1990-01-01" }, "attendeeRelation": "Self", "kind": "Standard", "source": "App", "serviceId": 3, "reason": "Đau răng", "dentistId": 3, "startAt": "0001-01-01T00:00:00+00:00", "durationMinutes": 30, "requestKey": "aaaaaaaa-0000-4000-8000-000000000017", "scheduleVersion": 1 }
```

**APT-04** — Tên người đặt rỗng → `MSG04`

```json
{ "branchId": 1, "booker": { "name": "", "phone": "0987654321", "email": "nam@gmail.com" }, "attendee": { "name": "Lê Văn Nam", "birthDate": "1990-01-01" }, "attendeeRelation": "Self", "kind": "Standard", "source": "App", "serviceId": 3, "reason": "Đau răng", "dentistId": 3, "startAt": "2030-01-15T09:00:00+07:00", "durationMinutes": 30, "requestKey": "aaaaaaaa-0000-4000-8000-000000000018", "scheduleVersion": 1 }
```

**APT-05** — Người đặt không có SĐT lẫn email → `MSG04`

```json
{ "branchId": 1, "booker": { "name": "Lê Văn Nam", "phone": "", "email": "" }, "attendee": { "name": "Lê Văn Nam", "birthDate": "1990-01-01" }, "attendeeRelation": "Self", "kind": "Standard", "source": "App", "serviceId": 3, "reason": "Đau răng", "dentistId": 3, "startAt": "2030-01-15T09:00:00+07:00", "durationMinutes": 30, "requestKey": "aaaaaaaa-0000-4000-8000-000000000019", "scheduleVersion": 1 }
```

**APT-06** — SĐT người đặt 5 số → `MSG04`

```json
{ "branchId": 1, "booker": { "name": "Lê Văn Nam", "phone": "12345", "email": "nam@gmail.com" }, "attendee": { "name": "Lê Văn Nam", "birthDate": "1990-01-01" }, "attendeeRelation": "Self", "kind": "Standard", "source": "App", "serviceId": 3, "reason": "Đau răng", "dentistId": 3, "startAt": "2030-01-15T09:00:00+07:00", "durationMinutes": 30, "requestKey": "aaaaaaaa-0000-4000-8000-000000000020", "scheduleVersion": 1 }
```

**APT-07** — Email người đặt không có @ → `MSG04`

```json
{ "branchId": 1, "booker": { "name": "Lê Văn Nam", "phone": "0987654321", "email": "nam-gmail.com" }, "attendee": { "name": "Lê Văn Nam", "birthDate": "1990-01-01" }, "attendeeRelation": "Self", "kind": "Standard", "source": "App", "serviceId": 3, "reason": "Đau răng", "dentistId": 3, "startAt": "2030-01-15T09:00:00+07:00", "durationMinutes": 30, "requestKey": "aaaaaaaa-0000-4000-8000-000000000021", "scheduleVersion": 1 }
```

**APT-08** — Tên người khám rỗng → `MSG23` "Vui lòng nhập thông tin người khám thực tế."

```json
{ "branchId": 1, "booker": { "name": "Lê Văn Nam", "phone": "0987654321", "email": "nam@gmail.com" }, "attendee": { "name": "", "birthDate": "1990-01-01" }, "attendeeRelation": "Self", "kind": "Standard", "source": "App", "serviceId": 3, "reason": "Đau răng", "dentistId": 3, "startAt": "2030-01-15T09:00:00+07:00", "durationMinutes": 30, "requestKey": "aaaaaaaa-0000-4000-8000-000000000022", "scheduleVersion": 1 }
```

**APT-09** — Ngày sinh người khám ở tương lai → `MSG04`

```json
{ "branchId": 1, "booker": { "name": "Lê Văn Nam", "phone": "0987654321", "email": "nam@gmail.com" }, "attendee": { "name": "Lê Văn Nam", "birthDate": "2099-01-01" }, "attendeeRelation": "Self", "kind": "Standard", "source": "App", "serviceId": 3, "reason": "Đau răng", "dentistId": 3, "startAt": "2030-01-15T09:00:00+07:00", "durationMinutes": 30, "requestKey": "aaaaaaaa-0000-4000-8000-000000000023", "scheduleVersion": 1 }
```

**APT-10** — Quan hệ ngoài danh sách → `MSG04`

```json
{ "branchId": 1, "booker": { "name": "Lê Văn Nam", "phone": "0987654321", "email": "nam@gmail.com" }, "attendee": { "name": "Lê Văn Nam", "birthDate": "1990-01-01" }, "attendeeRelation": "Friend", "kind": "Standard", "source": "App", "serviceId": 3, "reason": "Đau răng", "dentistId": 3, "startAt": "2030-01-15T09:00:00+07:00", "durationMinutes": 30, "requestKey": "aaaaaaaa-0000-4000-8000-000000000024", "scheduleVersion": 1 }
```

---

## 6. Treatment Dossier (DOS-01..06) — `POST http://localhost:5080/api/patients/<ID_BN_PAT-01>/dossiers/quick`

Cần đã chạy PAT-01 và PAT-02 (có `pid`, `pid2`). Đăng nhập **dentist.demo** (trừ DOS-05, DOS-06).

**DOS-01** — URL `.../patients/<ID_BN_PAT-01>/dossiers/quick` → **200**, `dossier.state = "Open"`, `MSG222`

```json
{ "branchId": 1, "requestKey": "aaaaaaaa-0000-4000-8000-000000000025" }
```

**DOS-02** — URL `.../patients/<ID_BN_PAT-02>/dossiers/quick`, gửi **2 lần** đúng body này → cả hai **200**, cùng `dossier.id`

```json
{ "branchId": 1, "requestKey": "00000000-0000-4000-8000-000600000001" }
```

**DOS-03** — URL `.../patients/999999/dossiers/quick` → **404**, `MSG113`

```json
{ "branchId": 1, "requestKey": "aaaaaaaa-0000-4000-8000-000000000026" }
```

**DOS-04** — URL `.../patients/<ID_BN_PAT-01>/dossiers/quick`, chi nhánh B → **403**, `MSG06`

```json
{ "branchId": 3, "requestKey": "aaaaaaaa-0000-4000-8000-000000000027" }
```

**DOS-05** — Đăng nhập **assistant.demo**, URL `.../patients/<ID_BN_PAT-01>/dossiers/quick` → **403**, `MSG06`

```json
{ "branchId": 1, "requestKey": "aaaaaaaa-0000-4000-8000-000000000028" }
```

**DOS-06** — **No Auth**, URL `.../patients/1/dossiers/quick` → **401**, `MSG07`

```json
{ "branchId": 1, "requestKey": "aaaaaaaa-0000-4000-8000-000000000029" }
```

---

## 7. Item Plan (PLN-01..07) — `POST http://localhost:5080/api/treatment-templates/item-plans`

Đăng nhập **dentist.demo** (trừ PLN-06, PLN-07).

**PLN-01** → **200**, `version = 1`, `itemCount = 0`. Ghi lại `id` kế hoạch (dùng ở mục 8).

```json
{ "branchId": 1, "name": "Cạo vôi răng 01", "requestKey": "aaaaaaaa-0000-4000-8000-000000000030" }
```

**PLN-02** — Gửi **2 lần** đúng body này → cả hai **200**, cùng `id`. Ghi lại `id` kế hoạch này (dùng ở mục 8).

```json
{ "branchId": 1, "name": "Trám răng 01", "requestKey": "00000000-0000-4000-8000-000700000001" }
```

**PLN-03** — Tên rỗng → **400**, `ITEM_PLAN_VALIDATION`, `field = name`

```json
{ "branchId": 1, "name": "", "requestKey": "aaaaaaaa-0000-4000-8000-000000000031" }
```

**PLN-04** — Tên 161 ký tự → **400**, `field = name`

```json
{ "branchId": 1, "name": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "requestKey": "aaaaaaaa-0000-4000-8000-000000000032" }
```

**PLN-05** — Chi nhánh B (nha sĩ không làm việc ở đó) → **403**, `MSG06`

```json
{ "branchId": 3, "name": "Kế hoạch B 01", "requestKey": "aaaaaaaa-0000-4000-8000-000000000033" }
```

**PLN-06** — Đăng nhập **assistant.demo** → **403**, `MSG06`

```json
{ "branchId": 1, "name": "Kế hoạch 01", "requestKey": "aaaaaaaa-0000-4000-8000-000000000034" }
```

**PLN-07** — **No Auth**, cùng body PLN-06 → **401**, `MSG07`

---

## 8. Plan Item (ROW-01..10) — `POST http://localhost:5080/api/treatment-templates/item-plans/<ID_KH_PLN-01>/items`

Cần đã chạy PLN-01 và PLN-02. Đăng nhập **dentist.demo** (trừ ROW-10). Chạy theo đúng thứ tự vì `expectedVersion` thay đổi.

**ROW-01** — URL `.../item-plans/<ID_KH_PLN-01>/items` → **200**, `version = 2`, `itemCount = 1`, `totalCostVnd = 300000`

```json
{ "teeth": ["25"], "serviceId": 7, "estimatedMinutes": 30, "costVnd": 300000, "expectedVersion": 1, "requestKey": "aaaaaaaa-0000-4000-8000-000000000035" }
```

**ROW-02** — URL `.../item-plans/<ID_KH_PLN-02>/items`, gửi **2 lần** đúng body này → chỉ có 1 hạng mục (`itemCount = 1`)

```json
{ "teeth": ["25"], "serviceId": 7, "estimatedMinutes": 30, "costVnd": 300000, "expectedVersion": 1, "requestKey": "00000000-0000-4000-8000-000800000001" }
```

Các case ROW-03..07 dùng URL `.../item-plans/<ID_KH_PLN-01>/items` (kế hoạch này giờ đang ở version 2), đều → **400**, `ITEM_PLAN_VALIDATION`:

**ROW-03** — Mã răng không tồn tại, `field = teeth`

```json
{ "teeth": ["99"], "serviceId": 7, "estimatedMinutes": 30, "costVnd": 300000, "expectedVersion": 2, "requestKey": "aaaaaaaa-0000-4000-8000-000000000036" }
```

**ROW-04** — 481 phút, `field = estimatedMinutes`

```json
{ "teeth": ["25"], "serviceId": 7, "estimatedMinutes": 481, "costVnd": 300000, "expectedVersion": 2, "requestKey": "aaaaaaaa-0000-4000-8000-000000000037" }
```

**ROW-05** — Chi phí âm, `field = costVnd`

```json
{ "teeth": ["25"], "serviceId": 7, "estimatedMinutes": 30, "costVnd": -1, "expectedVersion": 2, "requestKey": "aaaaaaaa-0000-4000-8000-000000000038" }
```

**ROW-06** — Ghi chú 1001 ký tự, `field = note`

```json
{ "teeth": ["25"], "serviceId": 7, "estimatedMinutes": 30, "costVnd": 300000, "note": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "expectedVersion": 2, "requestKey": "aaaaaaaa-0000-4000-8000-000000000039" }
```

**ROW-07** — Chưa chọn dịch vụ, `field = serviceId`

```json
{ "teeth": ["25"], "serviceId": 0, "estimatedMinutes": 30, "costVnd": 300000, "expectedVersion": 2, "requestKey": "aaaaaaaa-0000-4000-8000-000000000040" }
```

**ROW-08** — Gửi version cũ (kế hoạch `<ID_KH_PLN-01>` đã lên version 2) → **409**, `MSG111`

```json
{ "teeth": ["26"], "serviceId": 7, "estimatedMinutes": 30, "costVnd": 300000, "expectedVersion": 1, "requestKey": "aaaaaaaa-0000-4000-8000-000000000041" }
```

**ROW-09** — URL `.../item-plans/00000000-0000-0000-0000-000000000001/items` (kế hoạch không tồn tại) → **404**, `MSG113`

```json
{ "teeth": ["25"], "serviceId": 7, "estimatedMinutes": 30, "costVnd": 300000, "expectedVersion": 1, "requestKey": "aaaaaaaa-0000-4000-8000-000000000042" }
```

**ROW-10** — Đăng nhập **assistant.demo**, URL `.../item-plans/<ID_KH_PLN-01>/items` → **403**, `MSG06`

```json
{ "teeth": ["25"], "serviceId": 7, "estimatedMinutes": 30, "costVnd": 300000, "expectedVersion": 2, "requestKey": "aaaaaaaa-0000-4000-8000-000000000043" }
```

---

## 9. Lab Work Type (LWT-01..08) — `POST http://localhost:5080/api/system-settings/settings/catalogs/lab-work-types`

Đăng nhập **owner.demo** (trừ LWT-08). Dữ liệu nằm trong `entry`.

**LWT-01** → **201**, `active = true`, `version = 1`

```json
{ "requestKey": "aaaaaaaa-0000-4000-8000-000000000044", "entry": { "code": "LW-01", "name": "Mão răng sứ 01", "unit": "Tooth", "referencePrice": 1500000 } }
```

**LWT-02** — Mã rỗng → **400**, `MSG02`, `field = code`

```json
{ "requestKey": "aaaaaaaa-0000-4000-8000-000000000045", "entry": { "code": "", "name": "Mão răng sứ 01", "unit": "Tooth", "referencePrice": 1500000 } }
```

**LWT-03** — Mã có khoảng trắng → **400**, `MSG04`, `field = code`

```json
{ "requestKey": "aaaaaaaa-0000-4000-8000-000000000046", "entry": { "code": "LW 01", "name": "Mão răng sứ 01", "unit": "Tooth", "referencePrice": 1500000 } }
```

**LWT-04** — Tên rỗng → **400**, `MSG02`, `field = name`

```json
{ "requestKey": "aaaaaaaa-0000-4000-8000-000000000047", "entry": { "code": "LW-A01", "name": "", "unit": "Tooth", "referencePrice": 1500000 } }
```

**LWT-05** — Chưa chọn đơn vị → **400**, `MSG04`, `field = unit`

```json
{ "requestKey": "aaaaaaaa-0000-4000-8000-000000000048", "entry": { "code": "LW-B01", "name": "Mão răng sứ B 01", "unit": "", "referencePrice": 1500000 } }
```

**LWT-06** — Giá tham chiếu âm → **400**, `MSG257`, `field = referencePrice`

```json
{ "requestKey": "aaaaaaaa-0000-4000-8000-000000000049", "entry": { "code": "LW-C01", "name": "Mão răng sứ C 01", "unit": "Tooth", "referencePrice": -1 } }
```

**LWT-07** — Trùng mã (viết thường, sau LWT-01) → **409**, `MSG201`, `field = code`

```json
{ "requestKey": "aaaaaaaa-0000-4000-8000-000000000050", "entry": { "code": "lw-01", "name": "Tên khác 01", "unit": "Tooth", "referencePrice": 1500000 } }
```

**LWT-08** — Đăng nhập **assistant.demo** → **403**, `MSG06`

```json
{ "requestKey": "aaaaaaaa-0000-4000-8000-000000000051", "entry": { "code": "LW-D01", "name": "Mão răng sứ D 01", "unit": "Tooth", "referencePrice": 1500000 } }
```

---

## 10. Material Group (CAT-01..05) — `POST http://localhost:5080/api/system-settings/material-categories`

Đăng nhập **owner.demo** (trừ CAT-05).

**CAT-01** → **201**, `itemCount = 0`, `version = 1`. Ghi lại `id` nhóm (dùng ở mục 11).

```json
{ "name": "Vật liệu trám 01", "requestKey": "aaaaaaaa-0000-4000-8000-000000000052" }
```

**CAT-02** — Tên rỗng → **400**, `MSG04`, `field = name`

```json
{ "name": "", "requestKey": "aaaaaaaa-0000-4000-8000-000000000053" }
```

**CAT-03** — Tên 161 ký tự → **400**, `MSG04`

```json
{ "name": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "requestKey": "aaaaaaaa-0000-4000-8000-000000000054" }
```

**CAT-04** — Trùng tên (viết thường, sau CAT-01) → **409**, `CATALOG_NAME_DUPLICATE`

```json
{ "name": "vật liệu trám 01", "requestKey": "aaaaaaaa-0000-4000-8000-000000000055" }
```

**CAT-05** — Đăng nhập **assistant.demo** → **403**, `MSG06`

```json
{ "name": "Vật liệu khác 01", "requestKey": "aaaaaaaa-0000-4000-8000-000000000056" }
```

---

## 11. Material (MAT-01..06) — `POST http://localhost:5080/api/system-settings/materials`

Cần đã chạy CAT-01 (có `catId`). Đăng nhập **owner.demo** (trừ MAT-06).

**MAT-01** → **201**, có `categoryName`, `usageUnit = "g"`

```json
{ "name": "Composite 01", "categoryId": "<ID_NHOM_CAT-01>", "usageUnit": "g", "requestKey": "aaaaaaaa-0000-4000-8000-000000000057" }
```

**MAT-02** — Tên rỗng → **400**, `MSG04`, `field = name`

```json
{ "name": "", "categoryId": "<ID_NHOM_CAT-01>", "usageUnit": "g", "requestKey": "aaaaaaaa-0000-4000-8000-000000000058" }
```

**MAT-03** — Đơn vị 41 ký tự → **400**, `MSG04`, `field = usageUnit`

```json
{ "name": "Composite B 01", "categoryId": "<ID_NHOM_CAT-01>", "usageUnit": "ggggggggggggggggggggggggggggggggggggggggg", "requestKey": "aaaaaaaa-0000-4000-8000-000000000059" }
```

**MAT-04** — Nhóm không tồn tại → **404**, `MSG113`

```json
{ "name": "Composite C 01", "categoryId": "00000000-0000-0000-0000-000000000001", "usageUnit": "g", "requestKey": "aaaaaaaa-0000-4000-8000-000000000060" }
```

**MAT-05** — Trùng tên trong cùng nhóm (viết thường, sau MAT-01) → **409**, `CATALOG_NAME_DUPLICATE`

```json
{ "name": "composite 01", "categoryId": "<ID_NHOM_CAT-01>", "usageUnit": "g", "requestKey": "aaaaaaaa-0000-4000-8000-000000000061" }
```

**MAT-06** — Đăng nhập **assistant.demo** → **403**, `MSG06`

```json
{ "name": "Composite D 01", "categoryId": "<ID_NHOM_CAT-01>", "usageUnit": "g", "requestKey": "aaaaaaaa-0000-4000-8000-000000000062" }
```

---

## Script kiểm tra mẫu (tab Scripts → **After** (Post-response) của từng request)

```js
pm.test("Status 201", () => pm.response.to.have.status(201));
pm.test("MSG14", () => pm.expect(pm.response.json().notification.code).to.eql("MSG14"));
```

Với case lỗi: `pm.test("MSG139", () => pm.expect(pm.response.json().code).to.eql("MSG139"));`

## Lưu ý

- Dữ liệu ghi vào `dcms_dev` và không tự xóa.
- Nếu một case báo 403 trong khi bạn đang đăng nhập đúng vai trò, kiểm tra `token` hiện tại là của tài khoản nào.
- Nếu case tạo thất bại giữa chừng (ví dụ PAT-01 không chạy được) thì các case sau dùng `<ID_BN_PAT-01>`, `<ID_KH_PLN-01>`, `<ID_NHOM_CAT-01>` sẽ lỗi; chạy lại case tạo trước.
