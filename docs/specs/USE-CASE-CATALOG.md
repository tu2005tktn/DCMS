# DCMS Use Case Catalog (as built)

Cập nhật 23/11/2026. Bảng này liệt kê **46 use case đang có trong hệ thống**, gộp lại từ bản 58 use case ngày 21/11/2026 (quét endpoint backend `backend/src/DCMS.Api/Features`, màn hình web `web/src/app/App.tsx`, `web/src/features` và mobile `mobile/src/app`). Đây là danh mục mô tả hiện trạng; khác với danh mục thiết kế UC-01–55 trong [Use Cases v2.5](DCMS_Use_Cases_Business_Rules_Messages_v2.5.docx). Bảng đối chiếu với bản 58 use case nằm ở cuối tài liệu.

## Actors

| Actor | Ý nghĩa | Quan hệ |
| --- | --- | --- |
| Registered User | Mọi tài khoản đã đăng ký. | Actor cha của Guest và các vai trò nhân viên. |
| Guest | Tài khoản khách (public account) chưa liên kết hồ sơ bệnh nhân. Vai trò `Guest` có hiệu lực với mọi tài khoản khách và không bao giờ với nhân viên ([Program.cs](../../backend/src/DCMS.Api/Program.cs), policy `PublicAccountPolicy`). | Kế thừa Registered User. |
| Patient | Tài khoản khách đã liên kết hồ sơ bệnh nhân. | Trên sơ đồ kế thừa Registered User; use case dùng chung với Guest ghi ở cả hai actor. (Trong code tài khoản Patient vẫn mang vai trò `Guest`.) |
| Admin | Quản trị tài khoản nhân viên. | Kế thừa Registered User. |
| Clinic Owner | Chủ phòng khám (`ClinicOwner`). | Kế thừa Registered User. |
| Dentist | Bác sĩ. | Kế thừa Registered User. |
| Clinic Assistant | Lễ tân / phụ tá (`ClinicAssistant`). | Kế thừa Registered User. |
| System (Timer) | Tác vụ nền: nhắc lịch trước 24 giờ và 2 giờ (UC-07), đối soát thanh toán mỗi 15 giây (UC-39). | Actor phụ. |
| Google | Nhà cung cấp đăng nhập (UC-02). | Actor phụ bên ngoài. |
| Payment Gateway | Cổng thanh toán trực tuyến (UC-39). | Secondary actor. |
| Notification Service | Dịch vụ gửi thông báo thời gian thực và thông báo đẩy (UC-07). | Secondary actor. |
| Xray/PACS System | Hệ thống X-quang / lưu trữ ảnh chẩn đoán cung cấp ảnh và kết quả chụp (UC-28, UC-29). | Secondary actor. |
| AI Service | `ai-service` phát hiện sâu răng trên X-quang (UC-30). | Actor phụ bên ngoài. |

Cột Secondary Actor ghi hệ thống bên ngoài tham gia use case (Payment Gateway, Notification Service, Xray/PACS System); trên sơ đồ, secondary actor đứng bên phải ranh giới hệ thống.

Quyền thực tế của nhân viên do nhóm quyền động quyết định (UC-06); cột Actor của nhân viên ghi **vai trò mặc định**. Ví dụ UC-05 cho Admin và mọi tài khoản có quyền `accounts.*` (policy `ManageAccounts`).

## Use case table

| ID | Use Case | Feature | Actor | Secondary Actor | Use Case Description |
| --- | --- | --- | --- | --- | --- |
| UC-01 | Sign In / Sign Out | Account and Access | Registered User | — | Authenticate an account, establish a session with the appropriate permissions, renew the session, sign the staff member out automatically after the configured idle time, and end the session on sign-out (web and mobile). |
| UC-02 | Sign In with Google | Account and Access | Guest | — | Authenticate a patient with a Google account when Google sign-in is configured on the server. |
| UC-03 | Register Patient Account | Account and Access | Guest, Patient | — | Create a patient account with a name, a Vietnamese mobile number, an e-mail and a password that follows the password policy (web and mobile). |
| UC-04 | Recover Account | Account and Access | Registered User | — | Let a patient or a staff member who forgot the password verify the identity with a code and set a new password (web and mobile). |
| UC-05 | Manage Staff Accounts | Account and Access | Admin | — | Search, create and edit staff accounts (employee, role, branches), lock or unlock them, and review their change history. |
| UC-06 | Manage Access Groups | Account and Access | Clinic Owner, Admin | — | Create and edit access groups with their screen and action grants, preview the effect, activate or deactivate a group, and assign a staff member to a group and branches. |
| UC-07 | Receive Notifications | Notifications | Guest, Patient | Notification Service | Receive notifications in real time and as push notifications in the mobile app (the web app has no notification screen), including appointment updates and the reminders the system sends 24 hours and 2 hours before a confirmed appointment; list them, see the unread count, mark them as read and open the related screen. |
| UC-08 | Book Appointment Online | Patient Booking | Guest, Patient | — | Book an examination for yourself or a relative: choose the examinee, the branch, the dentist (on web also "any dentist with a free chair") and a free time slot, confirm the contact, review and confirm (web in five steps, mobile in three steps). |
| UC-09 | Reschedule or Cancel My Appointment | Patient Booking | Guest, Patient | — | List and open upcoming and past appointments with their change history; reschedule or cancel at least 12 hours before the appointment, or send a support request when it is closer, and withdraw a pending request. |
| UC-10 | Link Account to Patient Record | Patient Record | Guest | — | Send a request to link the account to an existing patient record, follow its status and withdraw it (web and mobile). |
| UC-11 | View My Medical Record | Patient Record | Patient | — | See the basic information, visit results, treatment sheets and payments, images and documents, and prescriptions once the account is linked (web and mobile). |
| UC-12 | Update My Information | Patient Record | Patient | — | Update the administrative information of the linked patient record (mobile). |
| UC-13 | Manage Family Members | Patient Record | Guest, Patient | — | Add, edit, search and delete the family members saved in the account. |
| UC-14 | View Family Member Record | Patient Record | Patient | — | See the appointments booked for a family member and, when the clinic grants access, the record within the granted scopes (examinations, treatment, prescriptions, documents); request the access or withdraw the request. |
| UC-15 | View Appointment Calendar | Appointments | Clinic Assistant, Dentist, Clinic Owner | — | See the appointments of a branch in a day or week calendar, filter them by dentist and status, open the detail of an appointment and start the allowed actions from it. |
| UC-16 | Book Appointment for a Patient | Appointments | Clinic Assistant, Dentist | — | Create a standard or walk-in appointment for a patient, a new customer or a relative, with its source, choosing the dentist or any dentist with a free chair, with a reason when the lead-time rule is broken. |
| UC-17 | Reschedule Appointment | Appointments | Clinic Assistant, Dentist | — | Move an appointment to another time, dentist or chair from the calendar, the appointment detail or the patient profile. |
| UC-18 | Cancel Appointment or Mark No-show | Appointments | Clinic Assistant, Dentist | — | Cancel an appointment with a reason or record that the patient did not come, and book a walk-in replacement appointment in the freed time. |
| UC-19 | Book Next Visit | Appointments | Clinic Assistant, Dentist | — | Book the next visit of a patient from the patient profile, using the next-visit candidates of the open treatment items, with the patient's consent. |
| UC-20 | Handle Support Request | Appointments | Clinic Assistant | — | Review the change or cancel requests of patients in the support inbox, verify the requester, apply the change, resolve it, or reject it with a reason. |
| UC-21 | Set and Publish Working Schedule | Scheduling | Clinic Owner, Dentist | — | Set a dentist's weekly template or a one-day exception with periods of work, break or block per branch; check the effect on confirmed appointments, preview the bookable slots, and publish. |
| UC-22 | Manage Appointment Settings | Scheduling | Clinic Owner | — | Set, for a branch, the slot interval and the services that can be booked online with their durations. |
| UC-23 | Check In Patient | Reception | Clinic Assistant | — | Find the appointment of the day, confirm the patient (or create the patient record at the desk), record the arrival time, purpose and note, and put the patient in the waiting queue; follow the queue (waiting, being examined, examination confirmed, completed). |
| UC-24 | Manage Patient Records | Patient Records | Clinic Assistant, Dentist | — | Search, create, edit, archive and restore patient records with the duplicate check and required fields, and open the profile (appointments, examinations, treatment and payments, documents, prescriptions). |
| UC-25 | Verify Record Link | Patient Records | Clinic Assistant | — | Check a request to link an account to a record, or a request for access to a family member's record, compare it with the patient record, and approve, reject or revoke it. |
| UC-26 | Handle Care Tasks | Customer Care | Clinic Assistant | — | Create, edit and follow care tasks such as reminders and follow-up calls in a work list, with notes, contact attempts, escalation and export. |
| UC-27 | Conduct Examination | Examination | Dentist | — | Start a visit, record symptoms, history, the odontogram (2D/3D), findings and diagnoses, review earlier visits, save, confirm, amend, complete the visit and hand the patient over to treatment. |
| UC-28 | Order Investigations | Examination | Dentist | Xray/PACS System | Order an investigation (for example an X-ray) for a visit, receive the result, review it and cancel it. |
| UC-29 | Upload and Review Examination Documents | Examination | Dentist, Clinic Assistant, Patient | Xray/PACS System | Upload, view and download the images and documents of a visit or a patient; the dentist reviews them. |
| UC-30 | Analyze X-ray with AI | Examination | Dentist | — | When the AI service is configured on the server, run it on an X-ray image to detect suspected caries, show the boxes and confidence as a suggestion, and keep the result with the document. |
| UC-31 | Plan Treatment | Treatment | Dentist | — | Create a treatment sheet (normal or quick), plan the items with planned dates (also from a plan template), copy an item, and revise the plan and the agreed prices. |
| UC-32 | Record Treatment Work | Treatment | Dentist | — | Record and confirm the work done for a plan item, add notes and images, and print the treatment sheet. |
| UC-33 | Close, Reopen or Cancel Treatment Sheet | Treatment | Dentist, Clinic Owner | — | Close a treatment sheet when the work is finished, reopen it, cancel it with its payment effect, or delete a sheet opened by mistake; see its history. |
| UC-34 | Manage Treatment Plan Templates | Treatment | Dentist, Clinic Owner | — | Create, rename, edit and delete the ready-made treatment plans of a branch and their items. |
| UC-35 | Order Lab Work | Treatment | Dentist, Clinic Assistant | — | Create, edit or remove a lab order for a piece of work on a treatment sheet. |
| UC-36 | Track Lab Work | Treatment | Dentist, Clinic Assistant | — | Follow the lab orders by status and move an order through sent, received, delivered or rework with a history; open the order. |
| UC-37 | Record Materials Used | Treatment | Dentist, Clinic Assistant | — | Record the materials used for a piece of work and take them out of the stock. |
| UC-38 | Prescribe Medicine | Treatment | Dentist | — | Create, view and edit a prescription for a patient with clinic stock medicines or medicines bought outside. |
| UC-39 | Collect Treatment Payment | Payment | Clinic Assistant | Payment Gateway | Record a payment or an advance for a treatment sheet, pay online through a QR order of the payment gateway, allocate an advance to the sheet, and record that the advance of a no-show patient is forfeited; online payments are also confirmed automatically from the payment gateway callback and a reconcile job every 15 seconds. |
| UC-40 | Manage Clinic Catalogs | Administration | Clinic Owner, Dentist | — | Add, edit, search and deactivate or reactivate treatment services and prices, diagnoses, medicines and customer groups, with export where available. |
| UC-41 | Manage Supply Catalog | Administration | Clinic Assistant, Clinic Owner | — | Add, edit, search and deactivate or delete material categories, instruments and materials, suppliers, lab companies and types of lab work. |
| UC-42 | Manage Dental Supplies | Administration | Clinic Assistant, Clinic Owner | — | Record purchases of instruments and materials with the supplier, expiry date and payment; follow the stock and the usage history. |
| UC-43 | Manage Branches and Dental Chairs | Administration | Clinic Owner | — | Create and edit a branch, activate or deactivate it after checking the effect, and add, edit, activate or deactivate its chairs. |
| UC-44 | Manage General Settings | Administration | Admin | — | Set the staff idle sign-out time, the required patient fields, the patient code mode and prefix and the receipt print options, with preview, version history and restore; see who changed which setting. |
| UC-45 | View Finance Statistics | Reports | Clinic Owner | — | See the amount to collect, the money received, the advance, the receivables, the cost of materials and the profit, and open the list behind each figure. |
| UC-46 | View Work and Service Statistics | Reports | Clinic Owner | — | See the work and services of treatment sheets with the cost, the paid and the remaining amount, filtered by branch, period, status and search. |

## Use case relationships

Cập nhật 22/11/2026: chỉ ghi quan hệ có thật trên màn hình web — màn hình của use case gốc có nút, drawer hoặc panel mở use case mở rộng cho **cùng đối tượng đang xem** (cùng lịch hẹn, lượt khám, bệnh nhân, dòng phiếu điều trị, người thân). Không vẽ: menu bên trái, liên kết sang trang khác chỉ để xem, quy tắc nghiệp vụ (để ở phần điều kiện trước). Không vẽ «include» từ mọi use case tới UC-01; không nối UC-07 — thông báo là post-condition.

Hướng mũi tên: «extend» vẽ nét đứt từ use case **mở rộng** tới use case **gốc**; generalization vẽ nét liền, tam giác rỗng, từ **con** tới **cha**.

### Generalization

| Con | Cha | Màn hình |
| --- | --- | --- |
| UC-02 Sign In with Google | UC-01 Sign In / Sign Out | Nút Google nằm trên màn đăng nhập khách hàng (`CustomerPages.tsx`, `CustomerGoogleButton`). |

### «extend»

| Use case mở rộng | Use case gốc | Màn hình (điểm mở rộng) |
| --- | --- | --- |
| UC-04 Recover Account | UC-01 | Liên kết "Quên mật khẩu?" trên màn đăng nhập nhân viên và khách hàng (`SignInPage.tsx`, `CustomerPages.tsx`). |
| UC-08 Book Appointment Online | UC-13 | Nút "Đặt lịch" trên dòng người thân, điền sẵn người khám (`FamilyMembersPage.tsx`). |
| UC-14 View Family Member Record | UC-13 | Nút "Xem hồ sơ" trên dòng người thân (`FamilyMembersPage.tsx`). |
| UC-16 Book Appointment for a Patient | UC-15 | Drawer đặt lịch mở từ lịch hẹn (`AppointmentCalendarPage.tsx` → `BookingDrawer`). |
| UC-16 Book Appointment for a Patient | UC-18 | Nút "Đặt lịch thay thế", chỉ hiện sau khi ghi nhận khách không tới và giờ hẹn chưa qua (`StaffAppointmentActions.tsx` → `ReplacementBooking`). |
| UC-16 Book Appointment for a Patient | UC-23 | Nút "Xếp lịch khách vãng lai" trên màn tiếp nhận (`ReceptionPage.tsx` → `BookingDrawer`). |
| UC-17 Reschedule Appointment | UC-15, UC-24 | Nút đổi lịch trong chi tiết lịch hẹn (mở từ lịch) và tab Lịch hẹn của hồ sơ bệnh nhân (`StaffAppointmentActions`). |
| UC-18 Cancel Appointment or Mark No-show | UC-15, UC-24 | Nút "Hủy lịch", "Khách không tới" ở cùng hai chỗ trên. |
| UC-19 Book Next Visit | UC-24 | Panel lịch tái khám trong tab Lịch hẹn của hồ sơ bệnh nhân (`NextVisitPanel` → `NextVisitBookingDrawer`). |
| UC-24 Manage Patient Records (create) | UC-23 | Tạo hồ sơ tại màn check-in khi chưa có hồ sơ (`CheckInPage.tsx` → `PatientFormDrawer`). |
| UC-28 Order Investigations | UC-27 | Panel cận lâm sàng trong phiếu khám (`ExaminationPage` → `InvestigationsPanel`). |
| UC-29 Upload and Review Examination Documents | UC-27, UC-28 | Danh sách tài liệu trong phiếu khám; tải lên/xem kết quả trong panel cận lâm sàng (`DocumentList`, `UploadDrawer`, `DocumentDetailDrawer`). |
| UC-30 Analyze X-ray with AI | UC-29 | Panel AI trong drawer chi tiết tài liệu, chỉ với ảnh X-quang khi AI Service được cấu hình (`DocumentDetailDrawer` → `AiCariesPanel`). |
| UC-31 Plan Treatment | UC-27 | Nút "Lập kế hoạch điều trị" khi phiếu khám cho phép chuyển điều trị; mở tab Điều trị kèm phiếu khám nguồn (`ExaminationPage.tsx`). |
| UC-38 Prescribe Medicine | UC-27 | Nút "Kê đơn trong hồ sơ bệnh nhân" trong phiếu khám (`ExaminationPage.tsx`). |
| UC-35 Order Lab Work | UC-32, UC-36 | Nút labo trên dòng việc đã làm (`TreatmentSheet` → `LabOrderModal`); sửa đơn khi mở từ màn labo (`LabWorkPage` → `LabOrderModal`, cần quyền quản lý). |
| UC-37 Record Materials Used | UC-32 | Nút vật tư trên dòng việc đã làm (`TreatmentSheet` → `MaterialNotePicker`). |
| UC-39 Collect Treatment Payment | UC-32 | Nút "Trả tiền" ở phần việc đã làm của phiếu điều trị (`TreatmentSheet` → `DossierPaymentModal`). |

### Đã kiểm tra và không vẽ

| Quan hệ cũ / ứng viên | Lý do |
| --- | --- |
| UC-17, UC-18 «extend» UC-20 | Hộp thư hỗ trợ áp dụng thay đổi bằng form riêng (`RequestDrawer`, `POST /api/support-requests/{id}/change-appointment`); đây là một phần của UC-20, không mở màn đổi/hủy lịch. |
| UC-17, UC-18 «extend» UC-21 | Màn lịch làm việc chỉ liệt kê lịch bị ảnh hưởng và liên kết sang trang chi tiết lịch hẹn; việc chặn công bố là quy tắc nghiệp vụ. |
| UC-19 «extend» UC-31, UC-32 | Phiếu điều trị không có nút đặt tái khám; chỉ có ở hồ sơ bệnh nhân. |
| UC-19 → UC-16 (generalization) | Dùng drawer và luồng riêng (`NextVisitBookingDrawer`). |
| UC-34 «extend» UC-31 | Nút "Kế hoạch điều trị" trên phiếu chỉ áp dụng mẫu (`ApplyItemPlanModal`) — thuộc UC-31; UC-34 là màn quản lý mẫu riêng (`/treatment-templates`). |
| UC-27 từ UC-23 | Hàng chờ chỉ có liên kết "Mở lượt khám" sang trang phiếu khám (điều hướng). |
| UC-31, UC-32, UC-33, UC-38 từ UC-24 | Là các tab của hồ sơ bệnh nhân (điều hướng), không phải bước tùy chọn của quản lý hồ sơ. |
| UC-33 với UC-31 | Đóng, mở lại, hủy phiếu là thao tác riêng trên danh sách phiếu, không chèn vào luồng lập kế hoạch. |
| UC-03 từ UC-01, UC-09 sau UC-08 | Chỉ là liên kết điều hướng ("Tạo tài khoản", "Xem và quản lý lịch hẹn"). |

### Điều kiện trước (precondition / dependency)

Các phụ thuộc sau không vẽ trong sơ đồ use case, nhưng phải ghi trong phần precondition của đặc tả:

| Use case | Phụ thuộc |
| --- | --- |
| UC-11, UC-12 | UC-10 gửi yêu cầu liên kết và UC-25 duyệt; sau đó tài khoản trở thành Patient. |
| UC-14 (xem hồ sơ người thân) | UC-13 có người thân; yêu cầu quyền xem được duyệt ở UC-25. |
| UC-08, UC-16 | UC-43 (chi nhánh, ghế), UC-22 (slot, dịch vụ đặt online), UC-21 (lịch làm việc đã công bố). |
| UC-20 | UC-09 tạo yêu cầu hỗ trợ khi còn dưới 12 giờ trước lịch hẹn. |
| UC-27 | UC-23 đã check-in bệnh nhân. |
| UC-32, UC-33, UC-39 | UC-31 đã có phiếu điều trị. |
| UC-36 | UC-35 đã có đơn labo. |
| UC-39 (ghi mất cọc) | UC-18 đã ghi nhận bệnh nhân không tới. |
| UC-33 (hủy phiếu) | Ảnh hưởng thanh toán tính trên các khoản đã thu ở UC-39. |
| UC-37 | UC-42 có tồn kho; vật tư lấy từ UC-41. |
| UC-27, UC-31, UC-38, UC-24 | UC-40 cung cấp chẩn đoán, dịch vụ và giá, thuốc, nhóm khách hàng. |
| UC-35, UC-36, UC-42 | UC-41 cung cấp công ty labo, loại công việc labo, nhà cung cấp. |
| UC-01, UC-24, UC-39 | UC-44 cấp thời gian tự đăng xuất, trường bắt buộc và mã bệnh nhân, tùy chọn in phiếu thu. |
| UC-45, UC-46 | Dữ liệu từ UC-32, UC-37, UC-39, UC-42. |
| UC-07 | Được kích hoạt bởi UC-08, UC-09, UC-16, UC-17, UC-18, UC-20, UC-25 và nhắc lịch của System. |

## Đối chiếu với bản 58 use case (21/11/2026)

| UC bản 58 | UC mới | Thay đổi |
| --- | --- | --- |
| UC-01, 02, 03 | UC-01, 02, 03 | Đổi "Customer" thành Guest/Patient. |
| UC-04 | UC-04 | Giữ nguyên; không có luồng Admin hỗ trợ khôi phục vì `/api/admin/recovery/*` đã gỡ ([UNUSED-API-REMOVAL.vi.md](UNUSED-API-REMOVAL.vi.md)). |
| UC-05, 06, 07 | UC-05, 06, 07 | UC-05 đổi kích hoạt/vô hiệu thành khóa/mở khóa (trạng thái `Active`/`Locked`); UC-06 thêm gán chi nhánh; UC-07 ghi rõ web không có màn thông báo và mốc nhắc 24 giờ, 2 giờ. |
| UC-08, 09 | UC-08 | **Gộp**: đặt cho người thân là một lựa chọn người khám trong cùng luồng. |
| UC-10, 11 | UC-09 | **Gộp**: xem lịch hẹn của tôi và đổi/hủy lịch. |
| UC-12 | UC-10 | Giữ nguyên. |
| UC-13, 15 | UC-11 | **Gộp**: đơn thuốc là một phần hồ sơ của tôi. |
| UC-14, 16, 17 | UC-12, 13, 14 | Giữ nguyên. |
| UC-18, 19 | UC-15 | **Gộp**: chi tiết lịch hẹn mở từ lịch. |
| UC-20, 21, 22, 23, 24 | UC-16, 17, 18, 19, 20 | Giữ nguyên. |
| UC-25, 26 | UC-21, 22 | UC-21 bỏ phần xem lịch hẹn của bác sĩ theo tuần. |
| UC-27, 28 | UC-23 | **Gộp**: hàng chờ là bước tiếp theo của tiếp nhận. |
| UC-29, 30 | UC-24 | **Gộp**: xem hồ sơ bệnh nhân là một phần quản lý hồ sơ. |
| UC-31, 32 | UC-25, 26 | Giữ nguyên. |
| UC-33, 37 | UC-27 | **Gộp**: xem lần khám trước trong lúc khám. |
| UC-34, 35, 36 | UC-28, 29, 30 | UC-29 bỏ khỏi mô tả phần xóa mềm, lịch sử và thư viện ảnh. |
| UC-38, 39 | UC-31, 32, 33 | **Tách** UC-38 thành lập kế hoạch, ghi nhận công việc, đóng/mở lại/hủy phiếu; áp dụng mẫu (UC-39) gộp vào UC-31. |
| UC-40, 41, 42, 43, 44 | UC-34, 35, 36, 37, 38 | Giữ nguyên. |
| UC-45 | UC-39, UC-33 | Hủy phiếu chuyển sang UC-33; UC-39 thêm ghi nhận mất cọc, webhook và đối soát PayOS. |
| UC-50, 51, 52, 53 | UC-40 | **Gộp**: danh mục chẩn đoán, thuốc, dịch vụ và giá, nhóm khách hàng. |
| UC-46, 47, 48 | UC-41 | **Gộp**: danh mục vật tư, nhà cung cấp, labo. |
| UC-49 | UC-42 | Giữ nguyên. |
| UC-54 | UC-43 | Giữ nguyên. |
| UC-55, 56 | UC-44 | **Gộp**: lịch sử thiết lập. |
| UC-57, 58 | UC-45, 46 | Ghi nhận mất cọc chuyển sang UC-39. |

## Điểm còn cần xác nhận

Cột Actor cập nhật 22/11/2026 theo lựa chọn của nhóm. Các chỗ khác với quyền mặc định của vai trò trong code (`SettingsGrants.RoleAllows`, `ScreenGrants.Allows`); nhóm quyền (UC-06) vẫn cấp thêm được:

| UC | Actor đã chọn | Quyền mặc định trong code |
| --- | --- | --- |
| UC-29 | có Patient | Ứng dụng khách hàng không có chức năng tải lên tài liệu; khách chỉ xem ảnh, tài liệu trong UC-11. |
| UC-41 | Clinic Assistant | `catalog.*` (vật tư, nhà cung cấp, labo) mặc định cho Clinic Owner, Admin, Dentist; không cho Clinic Assistant. |
| UC-42 | Clinic Assistant | `materials.*` mặc định cho Clinic Owner, Admin, Dentist; không cho Clinic Assistant. |
| UC-44 | Admin | `settings.general.*` mặc định cho cả Clinic Owner và Admin. |
| UC-28 | Dentist | `investigations.results.receive` mặc định cho cả Dentist và Clinic Assistant. |
| UC-26 | Clinic Assistant | `care-tasks.*` mặc định cho cả Dentist và Clinic Assistant. |
| UC-35, UC-36 | Dentist, Clinic Assistant | Lập/sửa đơn labo (`treatment.lab.manage`) mặc định chỉ cho Dentist; Clinic Owner có mọi quyền mặc định. |
| UC-24 | Clinic Assistant, Dentist | Tạo/sửa/lưu trữ hồ sơ (`patients.create`, `.update`, `.archive`) mặc định chỉ cho Clinic Assistant; Dentist chỉ xem. |
| UC-07 | Guest, Patient | Nhân viên cũng nhận thông báo (`notifications.*` mặc định cho mọi vai trò nhân viên). |
| UC-03 | Guest, Patient | Người tự đăng ký chưa có tài khoản; Guest trong hệ thống là tài khoản đã đăng ký. |

## Ghi chú hiện trạng

- Màn nhân viên trên mobile "Hộp thư hỗ trợ" (`mobile/src/app/(home)/staff/requests`) gọi API thật `/api/support-requests` (25/11/2026). "Lịch bị ảnh hưởng" chỉ có trên web (xem trước khi sửa lịch làm việc), mobile không có màn này.
- Lượt khám đã check-in không hủy được: trạng thái `Cancelled` của Visit có khai báo nhưng chưa có thao tác nào đặt trạng thái này.
- Các API không có màn hình nào gọi (xác minh e-mail, quản trị viên đặt lại mật khẩu nhân viên, danh sách phiên, đăng xuất mọi thiết bị và một số API khác) đã được gỡ ngày 21/11/2026, xem [UNUSED-API-REMOVAL.vi.md](UNUSED-API-REMOVAL.vi.md). Các danh mục nguồn khách, nghề nghiệp, tiền sử, loại chăm sóc, loại chi phí trong `/catalogs/{kind}` vẫn có API nhưng chưa có tab riêng nên không đưa vào bảng.
- Không tính các endpoint kiểm thử/phát triển: `/api/test/realtime`, `/api/dev/recovery`, `/health`.
