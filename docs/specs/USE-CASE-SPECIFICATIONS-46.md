# II. Use Case Specifications (46 use cases)

> Message ids follow DCMS_System_Messages (MSG01-MSG116). Business rules follow DCMS_Business_Rules (GBR-001-GBR-077), taken from the column "Applies To". A message without an id is quoted by its text because the message catalog has no id for it.

## UC-01 Sign In / Sign Out

| Primary Actors | Registered User (Patient, Dentist, Clinic Assistant, Clinic Owner, Administrator) | Secondary Actors | None |
|---|---|---|---|
| Description | Authenticate an existing account, establish a session with the permissions of the account, and end the session on sign-out. | | |
| Preconditions | Sign-in: an active account exists. Sign-out: an authenticated session exists. | | |
| Normal Flow | A. Sign In<br>1. The user opens the Sign-in screen of the customer area or of the staff area and enters an e-mail or user name and a password.<br>2. DCMS validates the credentials and checks the status of the account.<br>3. DCMS establishes a session and shows the functions permitted for the account (the customer home, or the staff menu built from the permissions). The sign-in flow ends.<br>B. Sign Out<br>1. The authenticated user selects Sign Out (this device or all devices).<br>2. DCMS invalidates the session.<br>3. DCMS returns the user to the unauthenticated interface. The sign-out flow ends. | | |
| Alternative Flows | A1 — Account recovery, at Sign In step 1: The user selects "Quên mật khẩu?" and continues with UC-04. UC-01 ends without a session.<br>A2 — Sign in with Google, at Sign In step 1: The user selects the Google button and continues with UC-02.<br>A3 — Idle time, during a staff session: When the user has not acted for the idle time set in the general settings, DCMS ends the session and returns the user to Sign-in. | | |
| Exception Flows | E1 — Invalid credentials or unavailable account, at Sign In step 2: DCMS displays MSG07 without saying which credential or which account condition caused the failure. No session is created.<br>E2 — The service is unavailable, at Sign In step 2 or 3: DCMS displays MSG49 and does not report a successful sign-in.<br>E3 — The session has expired when Sign Out is requested: DCMS displays MSG06 and returns the user to the unauthenticated interface.<br>E4 — A required field is empty, at Sign In step 1: DCMS shows "Vui lòng nhập email hoặc tên đăng nhập." or "Vui lòng nhập mật khẩu." and does not send the request. | | |
| Postconditions | 1. Sign-in succeeded: a session exists.<br>2. Sign-out succeeded: the session is invalid and cannot authorize further requests.<br>3. Sign-in failed: no new session exists. | | |
| Business Rule | GBR-001 | | |

## UC-02 Sign In with Google

| Primary Actors | Patient (Guest) | Secondary Actors | Google Identity Service |
|---|---|---|---|
| Description | Authenticate a customer with a Google account. A customer who signs in with Google for the first time gets a new customer account. | | |
| Preconditions | Google sign-in is configured on the server (client ID). The customer has a Google account. | | |
| Normal Flow | 1. The customer opens Customer Sign-in and selects the Google button.<br>2. Google asks the customer to choose an account and returns an identity token to DCMS.<br>3. DCMS verifies the token with Google.<br>4. If the Google identity is already linked to a customer account, DCMS uses that account.<br>5. If it is not linked, DCMS checks that the e-mail of the Google account does not belong to another account, creates a customer account (role Guest, e-mail confirmed) and links the Google identity to it.<br>6. DCMS establishes a session and opens the customer home. | | |
| Alternative Flows | A1 — Google sign-in is not configured, at step 1: The screen shows "Đăng nhập Google chưa được cấu hình trên máy chủ." and the user uses UC-01. | | |
| Exception Flows | E1 — The token is invalid, or the linked account is not a customer account, at step 3 or 4: DCMS displays MSG07 and creates no session.<br>E2 — The e-mail already belongs to another account, at step 5: DCMS refuses and asks the customer to sign in with the method used before, or to choose another Google account. No account is created.<br>E3 — Google cannot be loaded, or returns no credential, at step 2: The screen shows "Không tải được Google. Vui lòng kiểm tra kết nối." or "Google không trả về thông tin đăng nhập.".<br>E4 — The service is unavailable, at step 3 to 6: DCMS displays MSG49. | | |
| Postconditions | 1. Success: a session exists; a new customer account exists only in the case of step 5, with an audit record.<br>2. Failure: no session and no new account. | | |
| Business Rule | GBR-003 | | |

## UC-03 Register Patient Account

| Primary Actors | Guest | Secondary Actors | None |
|---|---|---|---|
| Description | Create a customer account with a Vietnamese mobile number, an e-mail and a password that follows the password policy of the system. | | |
| Preconditions | The visitor has no account with the same phone number or e-mail. The password policy is configured. | | |
| Normal Flow | 1. The visitor opens Customer Register from the Access Portal or from Customer Sign-in.<br>2. The visitor enters the full name, the phone number, the e-mail, the password and the password confirmation. DCMS shows the password policy.<br>3. DCMS validates each field and the password against the policy.<br>4. The visitor selects "Đăng ký".<br>5. DCMS creates the customer account (role Guest) and displays MSG08.<br>6. DCMS opens Customer Sign-in. | | |
| Alternative Flows | A1 — The visitor already has an account, at step 1: The visitor selects "Đăng nhập" and continues with UC-01. | | |
| Exception Flows | E1 — A required field is empty or the format is wrong (phone number not a mobile number of 10 digits, e-mail invalid), at step 3: DCMS displays MSG02 or MSG04 under the field.<br>E2 — The input is too long (name over 160 characters, e-mail over 254), at step 3: DCMS displays MSG03.<br>E3 — The password does not follow the policy, or the confirmation differs, at step 3: DCMS displays MSG39 or "Xác nhận mật khẩu không khớp.".<br>E4 — The phone number or e-mail already belongs to an account, at step 5: DCMS displays MSG46 with the links to sign in or to recover the account. No account is created.<br>E5 — The service is unavailable, at step 5: DCMS displays MSG49. | | |
| Postconditions | 1. Success: a customer account with the role Guest exists and can sign in. It does not create a Patient profile and is not linked to one.<br>2. Failure: no account is created. | | |
| Business Rule | GBR-002 | | |

## UC-04 Recover Account

| Primary Actors | Registered User (Patient, Staff) | Secondary Actors | None |
|---|---|---|---|
| Description | Restore access to an existing account through the verification and the credential reset of the system. | | |
| Preconditions | The recovery policy is configured. The user knows an identifier of the account. No session is required. | | |
| Normal Flow | 1. The user opens Recover Account (customer or staff) and enters an account identifier.<br>2. DCMS displays the same neutral message MSG09 whether or not an eligible account exists.<br>3. For an eligible account, DCMS sends the recovery instructions through the configured channel.<br>4. The user follows the instructions and submits the verification proof.<br>5. DCMS checks that the proof is valid, not expired and not used.<br>6. The user enters a new password.<br>7. DCMS checks the password policy and the validity of the proof, replaces the password and displays MSG10. | | |
| Alternative Flows | A1 — No eligible account, after step 2: DCMS sends nothing; the public message stays MSG09.<br>A2 — The user cannot use the verification channel, before step 4: The user asks an Administrator for help, who manages the account outside this use case. This does not reset or unlock the account by itself.<br>A3 — The user did not receive the proof, at step 4: The user asks DCMS to send it again; DCMS shows MSG09. | | |
| Exception Flows | E1 — The proof is invalid, expired or already used, at step 5: DCMS displays MSG38 and does not change the password.<br>E2 — The new password violates the policy, at step 7: DCMS displays MSG39. The old password stays.<br>E3 — The instructions cannot be delivered, at step 3: DCMS does not reset the password and still answers with MSG09; the failure is kept for follow-up.<br>E4 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the new password is stored and the recovery is recorded. No session is created.<br>2. Failure or abandonment: the old password stays.<br>3. All outcomes: recovery does not unlock a locked account, change roles or link a patient record. | | |
| Business Rule | GBR-004 | | |

## UC-05 Manage Staff Accounts

| Primary Actors | Admin | Secondary Actors | None |
|---|---|---|---|
| Description | Search, create and edit staff accounts, give them roles, lock and unlock them, and start the recovery of a staff account. | | |
| Preconditions | The user is authenticated as Admin (or belongs to an access group with the account permissions). | | |
| Normal Flow | 1. The Admin opens "Quản lý tài khoản" and sees the accounts with their status (Active or Locked) and roles. The Admin may search by name, user name or code and filter by role and status.<br>2. The Admin selects "Tạo tài khoản" and enters the user name, full name, employee code, e-mail, phone number, password and roles.<br>3. The Admin selects "Lưu". DCMS creates the staff account with its employee profile and displays MSG11.<br>4. To change an account, the Admin opens it, edits the data or the roles and saves; DCMS displays MSG11.<br>5. To lock or unlock an account, the Admin selects the action and enters the reason. DCMS changes the status, ends all current sessions of a locked account and displays MSG12 or MSG13. | | |
| Alternative Flows | A1 — The Admin starts the recovery of a staff account who forgot the password: DCMS sends the verification as in UC-04; the lock status is not changed.<br>A2 — The Admin opens the history of the account: DCMS lists every change with the actor, the time and the old and new values. | | |
| Exception Flows | E1 — A required field is empty, too long or not valid, at step 3: DCMS displays MSG02, MSG03 or MSG04 under the field; a password that does not follow the policy displays MSG39.<br>E2 — The user name, e-mail, phone number or employee code already belongs to another account, at step 3 or 4: DCMS displays MSG46 under the field.<br>E3 — The account was changed by someone else, at step 4 or 5: DCMS displays MSG41 and reloads the account.<br>E4 — The reason of a lock or unlock is missing: DCMS displays MSG02.<br>E5 — The user has no permission: DCMS displays MSG05.<br>E6 — The account no longer exists: DCMS displays MSG42.<br>E7 — The service is unavailable: DCMS displays MSG49; nothing is changed. | | |
| Postconditions | 1. Success: the account, its roles and its status are saved and every change is recorded in the account history.<br>2. A locked account cannot sign in and has no active session.<br>3. Failure: nothing is changed. | | |
| Business Rule | GBR-001, GBR-004, GBR-005, GBR-007 | | |

## UC-06 Manage Access Groups

| Primary Actors | Clinic Owner; Admin | Secondary Actors | None |
|---|---|---|---|
| Description | Create and edit access groups (a base role with the screens and actions it may use), assign staff to a group and deactivate a group. | | |
| Preconditions | The user is authenticated as Clinic Owner or Admin, or belongs to a group with the permission to manage access groups. | | |
| Normal Flow | 1. The user opens "Thiết lập hệ thống", section "Phân quyền truy cập", and sees the groups with the code, the name, the base role, the status and the number of staff.<br>2. The user selects "Tạo nhóm" and enters the code, the name and the base role (Admin, Dentist or Clinic Assistant).<br>3. The user ticks the screens and the actions of the group; an action can be ticked only with its parent screen.<br>4. The user selects "Lưu". DCMS saves the group and displays MSG53.<br>5. To assign staff, the user opens the group, selects the staff and the branches and saves with the reason of the change.<br>6. To stop using a group, the user deactivates it with a reason. | | |
| Alternative Flows | A1 — A user who is not the Clinic Owner opens the grant list: DCMS shows only the grants that the user holds.<br>A2 — The user edits a group: DCMS keeps the history of the change with the reason. | | |
| Exception Flows | E1 — A required field is empty or too long, at step 2: DCMS displays MSG02 or MSG03.<br>E2 — The code or the name already exists, at step 4: DCMS displays MSG50.<br>E3 — A grant is not valid, or an action is ticked without its parent screen: DCMS displays MSG68.<br>E4 — The group still has staff, at step 6, or the change would give the user a right they do not hold or change their own group: DCMS refuses and displays MSG05.<br>E5 — The group was changed by someone else: DCMS displays MSG41.<br>E6 — The reason is missing: DCMS displays MSG02.<br>E7 — The service is unavailable: DCMS displays MSG49; nothing is changed. | | |
| Postconditions | 1. Success: the group and its assignments are saved with the reason and the actor; the new rights apply to the staff of the group.<br>2. Failure: nothing is changed. | | |
| Business Rule | GBR-006, GBR-007 | | |

## UC-07 Receive Notifications

| Primary Actors | Registered User (Patient, Staff) | Secondary Actors | Push Notification Service |
|---|---|---|---|
| Description | Deliver the notifications of DCMS in real time and as push notifications on the mobile app, and let the user list them, read them and open the related screen. | | |
| Preconditions | The user is authenticated. For push notifications, the mobile app has registered a device. | | |
| Normal Flow | 1. An event occurs (for example an appointment is booked, changed or cancelled, a reminder is due, a support request is answered).<br>2. DCMS creates a notification for each eligible account and sends it in real time (SignalR) to the open app.<br>3. For a registered mobile device, DCMS also sends a push notification through the Push Notification Service. The push content does not contain private clinical details.<br>4. The user opens the notification list and sees the unread count.<br>5. The user opens a notification. DCMS checks that its link still points to an object the user may open and opens that screen.<br>6. DCMS marks the notification as read; the user can also mark all notifications as read. | | |
| Alternative Flows | A1 — The app is closed, at step 2: The notification stays in the list until the user opens the app; the push notification may reach the device.<br>A2 — The user turns push notifications off: DCMS keeps the in-app notification and stops the push for that device. | | |
| Exception Flows | E1 — The list is empty, at step 4: DCMS displays "Bạn chưa có thông báo nào.".<br>E2 — The linked object no longer exists or is not accessible, at step 5: DCMS displays MSG42 or MSG05 and does not open it.<br>E3 — The push cannot be delivered, at step 3: The in-app notification stays valid; the failure is recorded. DCMS displays "Lịch hẹn đã được xác nhận nhưng thông báo chưa gửi được. Nhân viên sẽ kiểm tra." when no channel works.<br>E4 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Notifications are kept in the list with their read status, separate from the delivery attempts.<br>2. A provider's acceptance of a push does not mean that the user has read it. | | |
| Business Rule | GBR-008, GBR-009 | | |

## UC-08 Book Appointment Online

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | Book a confirmed examination for the person who will attend, in five steps: examinee, schedule, contact, review, completion (web and mobile). It also covers: Book an appointment for a saved family member. The booking does not give the booker access to the relative's medical record. | | |
| Preconditions | The customer is authenticated. At least one branch accepts bookings, with a published working schedule and an examination service. | | |
| Normal Flow | 1. The customer opens "Đặt lịch khám" and chooses who will be examined: "Bản thân" (UC-08) or "Người thân" (UC-08).<br>2. The customer chooses the branch and the dentist ("Bất kỳ bác sĩ (ghế trống)" is allowed) and the date. DCMS shows the free time slots of "Buổi sáng" and "Buổi chiều" for the duration of the examination service and the free chairs.<br>3. The customer chooses a slot and may add a reason for the visit.<br>4. The customer confirms the name and the phone number or e-mail of the booker, and the name of the examinee.<br>5. The customer reviews the branch, the service, the dentist, the time, the booker and the examinee, then selects "Xác nhận đặt lịch".<br>6. DCMS re-checks, at the time of confirmation, that the selected slot is still available.<br>7. DCMS creates a Confirmed appointment (for "Bất kỳ bác sĩ", with the dentist of the chosen slot), displays MSG24 and the appointment code.<br>8. The customer opens the appointment (UC-09) or returns to the home. | | |
| Alternative Flows | A1 — No slot matches, at step 2: DCMS displays "Không tìm thấy khung giờ phù hợp. Vui lòng đổi ngày, bác sĩ hoặc dịch vụ."; the customer chooses another date or dentist.<br>A2 — The same request is sent again after a network error, at step 7: DCMS returns the existing result and displays MSG25; no second appointment is created.<br>Related function — Book for a Relative:<br>1. At step 1 of UC-08 the customer chooses "Người thân" and reads the notice that booking does not give access to the relative's record.<br>2. The customer chooses a family member from "Người thân đã lưu" (search by name). The name and date of birth of the examinee are filled in and cannot be edited.<br>3. The customer continues with steps 2 to 8 of UC-08. The appointment records the booker and the examinee (relationship "Người thân") separately. | | |
| Exception Flows | E1 — Information about the examinee or the booker is missing or wrong, at step 4: DCMS displays MSG18 or MSG02/MSG04 under the field.<br>E2 — Less than 60 minutes before the start, at step 6: DCMS displays MSG19 and creates nothing.<br>E3 — The slot is no longer available (taken by another booking, the schedule changed, or no chair is free), at step 6: DCMS displays MSG20, MSG22 or MSG23, loads the slots again and creates nothing.<br>E4 — The booking data changed meanwhile, at step 6: DCMS displays MSG41 and returns to step 2.<br>E5 — The session has expired, at step 5 or 6: DCMS displays MSG06 and "Đăng nhập".<br>E6 — The service is unavailable: DCMS displays MSG49.<br>Related function — Book for a Relative (exceptions):<br>E1 — The saved family member was deleted, at step 2: DCMS shows "Người thân đã xóa hoặc không còn khả dụng. Hãy chọn lại người thân đã lưu." and the customer chooses again.<br>E2 — The date of birth is invalid or after today: DCMS displays MSG04.<br>E3 — The customer tries to open the clinical record of the relative from the booking: DCMS displays "Bạn chỉ có thể xem thông tin lịch hẹn do mình đặt; hồ sơ khám của người thân cần được xác minh riêng.".<br>E4 — Other exceptions follow UC-08. | | |
| Postconditions | 1. Success: exactly one Confirmed appointment exists with the booker and the examinee recorded separately; its full interval is reserved. No Patient profile, visit or queue entry is created.<br>2. Failure: no appointment or reservation is created.<br>3. A failed notification does not cancel the appointment. | | |
| Business Rule | GBR-008, GBR-010, GBR-011, GBR-012, GBR-013, GBR-014, GBR-015, GBR-016 | | |

## UC-09 Reschedule or Cancel My Appointment

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | Reschedule or cancel a Confirmed appointment, or send a request to the clinic when the appointment is too close to be changed by the customer, and withdraw a pending request. It also covers: List, filter and open the upcoming and the past appointments that the customer booked, including those booked for relatives (web and mobile). | | |
| Preconditions | The customer is authenticated and created the booking (or has the right on it). The appointment is Confirmed and has not started or been checked in. | | |
| Normal Flow | 1. The customer opens the appointment (UC-09) and selects "Đổi lịch" or "Hủy lịch".<br>2. DCMS checks the right of the customer, the status of the appointment and that at least 12 hours remain.<br>3. For a change, the customer chooses a new dentist, date and slot (free slots are loaded) and enters a reason. For a cancellation, the customer enters a reason.<br>4. DCMS shows a review step. For a cancellation it shows "Bạn có chắc muốn hủy lịch hẹn {} lúc {} không?". The customer confirms.<br>5. DCMS checks again the appointment and the new slot, releases the old interval only when the new slot is accepted, and records the change in the history.<br>6. DCMS displays MSG30 (change) or MSG31 (cancellation) and notifies the dentist's calendar and the customer. | | |
| Alternative Flows | A1 — Less than 12 hours remain, at step 2: The buttons become "Yêu cầu đổi lịch" or "Yêu cầu hủy lịch". DCMS creates a support request for the staff (UC-20) and displays MSG28. The original appointment stays in effect.<br>A2 — A request is pending, at step 1: The customer sees "Yêu cầu đang chờ xử lý" and may withdraw it with "Rút yêu cầu". The original appointment stays.<br>A3 — The customer has no account: The customer contacts the clinic with the booking code (MSG27) and the staff act under UC-17 or UC-18.<br>Related function — View My Appointments:<br>1. The customer opens "Lịch hẹn của tôi".<br>2. The customer chooses "Sắp tới" or "Lịch sử" and may filter by status and by date range.<br>3. DCMS returns only the appointments booked by the account, with the permitted booking details.<br>4. The customer opens an appointment. DCMS shows its code, time, branch, service, dentist, booker, examinee, status and the history of changes. | | |
| Exception Flows | E1 — No right or the appointment is no longer eligible, at step 2 or 5: DCMS displays MSG05 or MSG26 and changes nothing.<br>E2 — The new slot is invalid or no longer free, at step 5: DCMS displays MSG29; the original time stays and the customer chooses another slot.<br>E3 — The appointment changed meanwhile, at step 5: DCMS displays MSG41 and asks the customer to reload.<br>E4 — The same action was already done: DCMS displays "Thao tác này đã được ghi nhận. Vui lòng kiểm tra kết quả hiện có để tránh ghi trùng.".<br>E5 — The service is unavailable: DCMS displays MSG49; nothing is changed.<br>Related function — View My Appointments (exceptions):<br>E1 — The date range is wrong ("Đến ngày" before "Từ ngày"), at step 2: DCMS shows "Đến ngày phải từ ngày bắt đầu trở đi." and does not search.<br>E2 — The account has no right on the appointment, at step 4: DCMS displays MSG05 or MSG37 and shows no private detail.<br>E3 — The session has expired: DCMS displays MSG06.<br>E4 — The appointment is no longer available: DCMS displays MSG42 and refreshes the list.<br>E5 — The service is unavailable: DCMS displays MSG49 with the button "Thử lại". | | |
| Postconditions | 1. Change succeeded: the new time is Confirmed, the old interval is released, the history is kept and a linked follow-up stays linked and Booked.<br>2. Cancellation succeeded: the appointment is Cancelled and the history is kept; an active linked follow-up goes back to "Needs booking".<br>3. Failure, abandonment or pending request: the original appointment stays in effect. | | |
| Business Rule | GBR-008, GBR-017, GBR-018, GBR-019, GBR-020 | | |

## UC-10 Link Account to Patient Record

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | Send a request to the clinic to link the customer account to the patient record of the account owner, follow its status and withdraw it. | | |
| Preconditions | The customer is authenticated and has no active self link. | | |
| Normal Flow | 1. The customer opens "Liên kết hồ sơ".<br>2. The customer enters the identifying information that the clinic needs (for example the patient code, if known, and information for matching).<br>3. DCMS validates the request and creates a pending request; it displays MSG15 (or MSG14 when the owner has not yet been checked in).<br>4. The clinic verifies the request in UC-25.<br>5. The customer sees the status of the request: waiting for the clinic, verified (MSG16), rejected or revoked (MSG17). When verified, the medical record can be seen (UC-11). | | |
| Alternative Flows | A1 — The customer withdraws a pending request, after step 3: DCMS marks it as withdrawn.<br>A2 — The request is rejected, after step 4: The customer sees the public note and may send a new request. | | |
| Exception Flows | E1 — A required field is missing or invalid: DCMS displays MSG02 or MSG04.<br>E2 — The account or the patient already has an active self link, at step 3: DCMS displays MSG47.<br>E3 — Ownership cannot be verified: DCMS displays MSG48 (the decision is taken by the staff).<br>E4 — The session has expired: DCMS displays MSG06.<br>E5 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. A request exists with its status; no record is visible before the clinic verifies it.<br>2. At most one active self link exists for an account. | | |
| Business Rule | GBR-002, GBR-022, GBR-023 | | |

## UC-11 View My Medical Record

| Primary Actors | Patient | Secondary Actors | None |
|---|---|---|---|
| Description | See the own medical record that the clinic published: basic information, examinations and results, treatment and payments, images and prescriptions (web and mobile). It also covers: List and open the prescriptions that a dentist has finalised for the customer (web and mobile). | | |
| Preconditions | The customer is authenticated and has a Verified self link. | | |
| Normal Flow | 1. The customer opens "Hồ sơ bệnh án" (web) or the medical record screen (mobile).<br>2. DCMS checks the account, the self link and the permission of each content.<br>3. DCMS shows the header with the name and the patient code, and the tabs: "Thông tin cơ bản", "Khám", "Điều trị & Thanh toán", "Hình ảnh", "Đơn thuốc".<br>4. The customer opens the result of a visit (symptoms, diagnoses, conclusion, advice, return need, disposition, findings, investigation results).<br>5. The customer opens a published image, or a prescription. | | |
| Alternative Flows | A1 — A tab has no published content, at step 3: DCMS displays "Chưa có nội dung bệnh án được phép hiển thị." (for example "Chưa có đơn thuốc").<br>Related function — View My Prescriptions:<br>1. The customer opens "Đơn thuốc của tôi".<br>2. DCMS lists the finalised prescriptions of the linked patient, newest first.<br>3. The customer opens a prescription and sees the date, the dentist, the diagnosis, each medicine (name, quantity, unit, dose, days, instructions) and the general advice. | | |
| Exception Flows | E1 — The account has no Verified self link: DCMS shows "Hãy xác minh liên kết hồ sơ để xem bệnh án." (MSG37) and no data.<br>E2 — The link was revoked or the account locked, at any step: DCMS stops the reading at once with MSG37.<br>E3 — An image cannot be opened: DCMS shows "Không mở được hình ảnh." (MSG42).<br>E4 — The session has expired: DCMS displays MSG06.<br>E5 — The service is unavailable: DCMS displays MSG49 with "Thử lại".<br>Related function — View My Prescriptions (exceptions):<br>E1 — No Verified self link or the right was revoked: DCMS displays MSG37.<br>E2 — The prescription is no longer available: DCMS displays MSG42.<br>E3 — The session has expired: DCMS displays MSG06.<br>E4 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. The customer has seen only completed and published content of own record.<br>2. Nothing is created or changed. | | |
| Business Rule | GBR-024 | | |

## UC-12 Update My Information

| Primary Actors | Patient | Secondary Actors | None |
|---|---|---|---|
| Description | Update the administrative information of the own patient record (name, birth date, gender, phone, address and guardian). | | |
| Preconditions | The customer is signed in and the account has a verified link to a patient record (UC-10, UC-25). The record is not archived. | | |
| Normal Flow | 1. The customer opens the own record (UC-11) and selects the action to edit the information.<br>2. DCMS shows the administrative fields of the linked record.<br>3. The customer edits the fields and selects "Lưu".<br>4. DCMS validates the fields, saves the record with a new version and displays MSG44. | | |
| Alternative Flows | A1 — The customer does not change anything: DCMS saves nothing. | | |
| Exception Flows | E1 — A required field is empty, too long or not valid, at step 4: DCMS displays MSG02, MSG03 or MSG04 under the field.<br>E2 — The link is not verified, or it was revoked: DCMS displays MSG17 and the record is not shown.<br>E3 — The record was changed by someone else: DCMS displays MSG41.<br>E4 — The service is unavailable: DCMS displays MSG49; nothing is changed. | | |
| Postconditions | 1. Success: the administrative information is updated; clinical data and internal notes are not changed.<br>2. Failure: the record is unchanged. | | |
| Business Rule | GBR-024, GBR-025 | | |

## UC-13 Manage Family Members

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | Add, edit, search and delete the family members saved in the account, to use them when booking (web and mobile). | | |
| Preconditions | The customer is authenticated. | | |
| Normal Flow | 1. The customer opens "Người thân của tôi" and sees the list (20 per page) with name, relationship, date of birth, gender and contact phone.<br>2. The customer searches by name, or selects "Thêm người thân".<br>3. The customer enters the name, the relationship (Bố, Mẹ, Vợ/Chồng, Con, Anh/Chị/Em, Ông/Bà, Khác), the date of birth (optional), the gender and the contact phone (a mobile number of 10 digits).<br>4. DCMS validates and saves the member.<br>5. To change a member the customer selects "Sửa", edits the fields and saves. To remove a member the customer selects "Xóa" and confirms "Bạn có muốn xóa người thân này khỏi danh sách không?".<br>6. The customer may select "Đặt lịch" (UC-08) or "Xem hồ sơ" (UC-14) for a member. | | |
| Alternative Flows | A1 — No member matches, at step 1: DCMS shows "Chưa có người thân phù hợp. Bạn có thể thêm người thân mới.".<br>A2 — The member was changed elsewhere, at step 5: The customer selects "Xem phiên bản mới" and chooses "Dùng thông tin mới" or "Giữ bản nhập để lưu lại". | | |
| Exception Flows | E1 — A required field is empty or invalid, at step 4: DCMS shows MSG02, MSG03 or MSG04 under the field (name, relationship, gender and phone are required; the date of birth cannot be after today).<br>E2 — The member changed concurrently, at step 4 or 5: DCMS displays MSG41.<br>E3 — The same action is sent again: DCMS displays "Thao tác này đã được ghi nhận. Vui lòng kiểm tra kết quả hiện có để tránh ghi trùng." and does not create a duplicate.<br>E4 — The session has expired: DCMS displays MSG06.<br>E5 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the family member is saved, changed or removed from the list. Appointments already booked keep their recorded examinee.<br>2. Failure: nothing is changed. | | |
| Business Rule | GBR-015, GBR-026 | | |

## UC-14 View Family Member Record

| Primary Actors | Patient | Secondary Actors | None |
|---|---|---|---|
| Description | See the appointments that the customer booked for a family member and, when the clinic has granted access, the record of that person within the granted scopes. The customer can request or end the access. | | |
| Preconditions | The customer is authenticated and the family member is saved in the account. The family member does not need an account. | | |
| Normal Flow | 1. The customer selects "Xem hồ sơ" on a family member (UC-13).<br>2. DCMS shows the access status (Chưa có hồ sơ liên kết, Chờ phòng khám xác minh, Đã được cấp quyền, Bị từ chối, Đã thu hồi, Đã rút yêu cầu, Đã hết hạn) and the tab "Lịch đặt hộ".<br>3. If the family member has an existing record at the clinic, the customer opens "Liên kết hồ sơ đã có tại phòng khám", enters the patient code (if known) and the matching information, and selects "Yêu cầu quyền xem hồ sơ". DCMS creates a pending request.<br>4. The clinic verifies the request in UC-25 and grants a scope (examinations, treatment, prescriptions, documents) with an expiry date.<br>5. DCMS shows the record header and the tabs of the granted scopes, 10 items per page. The customer may download a published document.<br>6. The customer may select "Rút yêu cầu" (pending) or "Chấm dứt quyền truy cập" (granted) after confirming. | | |
| Alternative Flows | A1 — The family member was received at the clinic after an appointment booked by the customer, before step 3: The record is shown by default within the granted scope, with no separate request. | | |
| Exception Flows | E1 — The access expired or was revoked, at any step: DCMS hides the content, loads the status again and displays MSG37.<br>E2 — A scope is not granted: DCMS shows no tab for it; a direct request returns MSG05.<br>E3 — A document cannot be downloaded: DCMS shows "Không thể tải tài liệu. Quyền truy cập có thể đã thay đổi, vui lòng tải lại." (MSG42).<br>E4 — A conflicting request already exists: DCMS displays MSG47.<br>E5 — The session has expired: DCMS displays MSG06.<br>E6 — The service is unavailable: DCMS displays MSG49 with "Thử lại". | | |
| Postconditions | 1. The customer has seen only the scopes granted by the clinic and valid at that time.<br>2. A booking for a relative alone does not give access to the clinical record. | | |
| Business Rule | GBR-027 | | |

## UC-15 View Appointment Calendar

| Primary Actors | Clinic Assistant; Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | See the appointments of a branch in a calendar by day or by week, and open the detail of an appointment. | | |
| Preconditions | The user is authenticated and has the permission to read appointments. A dentist whose account is limited to its own schedule sees only their own column. | | |
| Normal Flow | 1. The user opens "Lịch hẹn".<br>2. The user chooses the branch (when the user can pick a branch), the day or week, and may filter by dentist and status.<br>3. DCMS shows the appointments in the calendar within the scope of the user, with the booker, the examinee, the time and the status.<br>4. The user moves to today, to the previous or to the next day or week.<br>5. The user opens an appointment to see its detail: time, branch, service, dentist, booker, examinee, status, reason and history, with the actions allowed by status and permission (UC-17, UC-18, UC-18). | | |
| Alternative Flows | A1 — No appointment matches, at step 3: DCMS displays "Không có lịch hẹn phù hợp với bộ lọc đã chọn.".<br>A2 — The calendar changes while it is open: DCMS refreshes it in real time, or the user selects "Tải lại". | | |
| Exception Flows | E1 — The user has no permission, at step 3 or 5: DCMS displays MSG05 and shows no appointment outside the scope.<br>E2 — The appointment is no longer available, at step 5: DCMS displays MSG42 and refreshes the calendar.<br>E3 — The session has expired: DCMS displays MSG06.<br>E4 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | The user has seen only the appointments in the scope of the role. Nothing is created or changed. | | |
| Business Rule | GBR-007 | | |

## UC-16 Book Appointment for a Patient

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Book a Confirmed appointment on behalf of a customer, a patient or a relative who contacts the clinic by phone, in person or as a walk-in. | | |
| Preconditions | The assistant is authenticated and has the permission to create appointments. Appointment services and a published working schedule exist. | | |
| Normal Flow | 1. The assistant selects "Đặt lịch" in the calendar (or "Xếp lịch khách vãng lai" in the reception list).<br>2. The assistant chooses or enters the patient or customer, the examinee (Self or Relative) and the booker's contact.<br>3. The assistant chooses the branch, the dentist (or any dentist with a free chair), the service, the date and a free slot.<br>4. DCMS shows the review step "Xem lại trước khi xác nhận".<br>5. The assistant confirms.<br>6. DCMS re-checks, at the time of confirmation, that the selected slot is still available.<br>7. DCMS creates a Confirmed appointment and displays MSG24. | | |
| Alternative Flows | A1 — The start is within 60 minutes, at step 3: The assistant enters the exception reason; DCMS still performs all the checks of step 6.<br>A2 — Walk-in without an appointment, at step 3: The assistant chooses a valid slot of today; DCMS records the source as walk-in. For a slot of a later day DCMS displays "Đã xác nhận lịch hẹn vào {}. Người đến khám sẽ được check-in khi đến đúng ca." and no visit or queue entry is created.<br>A3 — A possible duplicate customer, at step 2: DCMS warns; the assistant compares the identities and does not merge records.<br>A4 — Replacement booking: After a cancelled or no-show appointment the assistant uses "Đặt lịch thay thế"; the new appointment is linked to the old one. | | |
| Exception Flows | E1 — The attendee information is missing, at step 2: DCMS displays MSG18.<br>E2 — No slot matches, at step 3: DCMS displays "Không tìm thấy khung giờ phù hợp. Vui lòng đổi ngày, bác sĩ hoặc dịch vụ." ("Không còn khung giờ phù hợp trong ngày. Vui lòng chọn ngày khác." for a walk-in with no slot today).<br>E3 — The slot is no longer free, not enough consecutive time, a conflict or no chair, at step 6: DCMS displays MSG20, "Không còn khoảng thời gian liên tiếp đủ cho lịch hẹn. Vui lòng chọn giờ khác.", MSG22 or MSG23 and creates nothing.<br>E4 — The data changed meanwhile: DCMS displays MSG41.<br>E5 — The user has no permission: DCMS displays MSG05.<br>E6 — The same request is sent again: DCMS displays MSG25 and returns the existing result.<br>E7 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: one Confirmed appointment exists with its full interval reserved; the dentist's calendar and the customer are notified. No Patient profile, visit or queue entry is created.<br>2. Failure: no appointment is created. | | |
| Business Rule | GBR-010, GBR-011, GBR-012, GBR-013, GBR-014, GBR-016, GBR-028 | | |

## UC-17 Reschedule Appointment

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Move a Confirmed appointment to another date, dentist or slot on behalf of the customer or because of a change in the clinic. | | |
| Preconditions | The assistant is authenticated and has the permission to edit appointments. The appointment is Confirmed and has not started or been checked in. The assistant has verified the requester. | | |
| Normal Flow | 1. The assistant opens the appointment and selects "Chỉnh sửa lịch".<br>2. The assistant chooses the new date, dentist and slot; DCMS shows the free slots.<br>3. The assistant enters the reason for the change (required, up to 500 characters).<br>4. The assistant selects "Lưu thay đổi".<br>5. DCMS checks again the appointment version, the new slot, the dentist and examinee conflicts and the chair capacity, and releases the old interval only when the new one is accepted.<br>6. DCMS saves the change with the reason, the actor and the time, displays MSG30 and notifies the customer. | | |
| Alternative Flows | A1 — The change comes from a support request, before step 1: The assistant handles it in UC-20.<br>A2 — The assistant abandons the drawer: Nothing is changed. | | |
| Exception Flows | E1 — The reason or the slot is missing: DCMS displays MSG02 and does not save.<br>E2 — The new slot is invalid or no longer free, at step 5: DCMS displays MSG29; the original time stays.<br>E3 — The appointment changed meanwhile, at step 5: DCMS displays MSG41 and the button "Tải lại phiên bản" loads the latest data.<br>E4 — The appointment is not Confirmed or has been checked in: DCMS displays MSG34.<br>E5 — No permission: DCMS displays MSG05.<br>E6 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the new time is Confirmed, the old interval is released, the history and the reason are kept, and a linked follow-up stays Booked.<br>2. Failure: the original appointment stays in effect. | | |
| Business Rule | GBR-008, GBR-012, GBR-013, GBR-018 | | |

## UC-18 Cancel Appointment or Mark No-show

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Cancel a Confirmed appointment with a reason, or record that the patient did not come. | | |
| Preconditions | The assistant is authenticated and has the permission. For a cancellation the appointment is Confirmed and has not started. For a no-show the appointment is still Confirmed, has no visit, and its start time has already passed. | | |
| Normal Flow | 1. The assistant opens the appointment and selects "Hủy lịch" or "Khách không tới".<br>2. DCMS shows a dialog and, for a cancellation, "Bạn có chắc muốn hủy lịch hẹn {} lúc {} không?".<br>3. The assistant enters the reason and confirms.<br>4. DCMS checks again the status and the version, saves the new status (Cancelled or No-show) with the actor, the time and the reason, and releases the interval.<br>5. DCMS displays MSG31 for a cancellation, notifies the customer and updates the calendars. For a no-show DCMS creates one care task to call the patient back (UC-26). | | |
| Alternative Flows | A1 — The appointment stays Confirmed after its start time: DCMS does not change it by itself; the staff must record the no-show.<br>A2 — A linked follow-up exists: It goes back to "Needs booking" ("Lịch tái khám không còn hiệu lực. Chỉ định đã chuyển về Cần đặt lịch và tiếp tục được theo dõi."). | | |
| Exception Flows | E1 — The reason is missing: DCMS displays MSG02.<br>E2 — The appointment is no longer eligible (already Cancelled, No-show, checked in, or a no-show requested before the start time): DCMS displays MSG26 and changes nothing.<br>E3 — The appointment changed meanwhile: DCMS displays MSG41.<br>E4 — No permission: DCMS displays MSG05.<br>E5 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Cancelled: the status, the reason and the history are kept; the interval is available again.<br>2. No-show: one callback task exists; the old appointment is preserved. A later rebooking creates a new linked appointment (UC-16, replacement).<br>3. Failure: the appointment is unchanged. | | |
| Business Rule | GBR-019, GBR-029, GBR-030, GBR-032, GBR-066 | | |

## UC-19 Book Next Visit

| Primary Actors | Clinic Assistant; Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Book the next visit of a patient from the patient profile or from a visit, using the next-visit candidates and the follow-up instruction of the dentist. | | |
| Preconditions | The user is authenticated and has the permission to create appointments. The patient exists. A follow-up instruction may exist. | | |
| Normal Flow | 1. The user opens the "next visit" panel of the patient.<br>2. DCMS shows the candidates (for example a follow-up due date, the duration and the preferred dentist).<br>3. The user chooses a candidate and the drawer of the next visit opens with the patient, the dentist and the duration already filled in.<br>4. The user chooses a free slot and confirms.<br>5. DCMS checks again the slot as in UC-16, creates the Confirmed appointment and displays "Đã đặt lịch tái khám vào {}." (MSG24 when there is no follow-up).<br>6. A linked follow-up instruction becomes Booked. | | |
| Alternative Flows | A1 — No slot matches the window, at step 4: DCMS displays "Chưa có khung giờ tái khám phù hợp. Chỉ định vẫn được giữ để phòng khám tiếp tục theo dõi."; the user chooses another date or dentist. | | |
| Exception Flows | E1 — The follow-up already has an active appointment: DCMS displays "Chỉ định quay lại đã có lịch hẹn đang hiệu lực. Vui lòng kiểm tra lịch hiện tại.".<br>E2 — The slot is no longer free or conflicts, at step 5: DCMS displays MSG20, "Không còn khoảng thời gian liên tiếp đủ cho lịch hẹn. Vui lòng chọn giờ khác.", MSG22 or MSG23.<br>E3 — The data changed meanwhile: DCMS displays MSG41.<br>E4 — No permission: DCMS displays MSG05.<br>E5 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: one new Confirmed appointment exists and a linked follow-up is Booked; at most one active appointment per follow-up.<br>2. Failure: nothing is created and the follow-up status is unchanged. | | |
| Business Rule | GBR-028, GBR-031 | | |

## UC-20 Handle Support Request

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Review the requests that customers send about their appointments (change, cancel, confirm attendance, question), verify the requester, apply the change, and resolve or reject the request (web and mobile). | | |
| Preconditions | The assistant is authenticated and has the permission to handle support requests. A request exists in the support inbox. | | |
| Normal Flow | 1. The assistant opens "Hộp thư hỗ trợ" and filters the requests; the list refreshes itself when a request changes.<br>2. The assistant opens a request and reads the appointment, the reason and the proposed slot of the customer.<br>3. The assistant selects "Xác minh người yêu cầu" after checking the requester against the booking data.<br>4. For a change or cancel request the assistant chooses the reason of the change and selects "Đổi lịch" or "Huỷ lịch"; DCMS applies it as in UC-17 or UC-18.<br>5. The assistant selects "Xử lý yêu cầu" and confirms; the request is closed and the customer is notified. | | |
| Alternative Flows | A1 — The request cannot be accepted, at step 5: The assistant selects "Từ chối", enters an internal reason and confirms; the original appointment stays and the customer is notified.<br>A2 — The request is withdrawn by the customer: DCMS closes it and shows its status. | | |
| Exception Flows | E1 — The requester is not verified, at step 4: DCMS does not apply the change until the requester is verified.<br>E2 — The change or cancel request is not applied before closing, at step 5: DCMS does not close the request.<br>E3 — The new slot is invalid, at step 4: DCMS displays MSG29.<br>E4 — The reply cannot be saved: DCMS displays "Chưa gửi được phản hồi. Vui lòng kiểm tra kết nối và thử lại.".<br>E5 — The request changed meanwhile: DCMS displays MSG41.<br>E6 — No permission: DCMS displays MSG05.<br>E7 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Resolved: the appointment reflects the applied change and the request is closed with the actor and the time.<br>2. Rejected: the appointment is unchanged and the reason is kept.<br>3. Failure: the request stays open. | | |
| Business Rule | GBR-018, GBR-020, GBR-021 | | |

## UC-21 Set and Publish Working Schedule

| Primary Actors | Dentist; Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | Set the weekly working schedule of a dentist (work periods, breaks and blocked time) and a one-day exception, then publish it. | | |
| Preconditions | The user is authenticated and has the permission to manage working schedules. A dentist always sees their own schedule. | | |
| Normal Flow | 1. The user opens "Lịch làm việc", chooses the branch, the dentist and the type: the weekly template or a one-day exception.<br>2. DCMS shows the current schedule of the seven days.<br>3. The user sets, for each day, the work periods, the breaks and the blocked time in steps of 15 minutes. For an exception the user enters the reason.<br>4. The user selects "Lưu và công bố".<br>5. DCMS checks the periods and the confirmed appointments, publishes the schedule, recalculates the available slots and displays MSG33. | | |
| Alternative Flows | A1 — The user selects "Kiểm tra ảnh hưởng" before publishing: DCMS lists the confirmed appointments that the change would affect.<br>A2 — The user abandons the editing: the published schedule stays in effect. | | |
| Exception Flows | E1 — The periods are invalid or overlap, at step 5: DCMS displays MSG40 and does not publish.<br>E2 — The change would leave confirmed appointments outside working time, at step 5: DCMS displays MSG32 and lists them; the assistant first moves or cancels them (UC-17, UC-18 or UC-20), then the schedule can be published.<br>E3 — The reason of an exception is missing: DCMS displays MSG02.<br>E4 — The schedule changed meanwhile: DCMS displays MSG41 and requires a reload.<br>E5 — The user has no permission: DCMS displays MSG05.<br>E6 — The service is unavailable: DCMS displays MSG49; the published schedule stays in effect. | | |
| Postconditions | 1. Success: the schedule is published and no confirmed appointment is left outside working time.<br>2. Failure or abandonment: the previous schedule stays in effect. | | |
| Business Rule | GBR-012, GBR-034, GBR-035, GBR-036 | | |

## UC-22 Manage Appointment Settings

| Primary Actors | Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | Set, for a branch, the slot interval of the online booking and the services that patients can book, with their duration. | | |
| Preconditions | The Clinic Owner is authenticated and has the permission to manage the appointment settings. | | |
| Normal Flow | 1. The Clinic Owner opens "Cài đặt đặt lịch" and chooses the branch.<br>2. DCMS shows the slot interval in minutes and the services of the branch ("Dịch vụ khám / điều trị") with the name, the duration in minutes and the status.<br>3. The Clinic Owner changes the slot interval (1 to 120 minutes; 15 by default) and the services (add, rename, set the duration from 1 to 480 minutes, choose whether it can be booked online, deactivate).<br>4. The Clinic Owner selects "Lưu". DCMS saves the settings with a new version; the availability is calculated with the new interval and durations. | | |
| Alternative Flows | A1 — The branch has no service yet: DCMS creates the initial examination service (30 minutes by default). | | |
| Exception Flows | E1 — A value is empty, out of range or too long, at step 4: DCMS displays MSG02, MSG03 or MSG04.<br>E2 — The change would remove the initial examination service of the branch: DCMS displays MSG68 and does not save.<br>E3 — The settings were changed by someone else: DCMS displays MSG41 and reloads them.<br>E4 — The user has no permission: DCMS displays MSG05.<br>E5 — The service is unavailable: DCMS displays MSG49; nothing is changed. | | |
| Postconditions | 1. Success: the settings are saved; existing appointments are not changed.<br>2. Failure: the previous settings stay in effect. | | |
| Business Rule | GBR-011, GBR-037 | | |

## UC-23 Check In Patient

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Receive a patient who comes for a Confirmed appointment of the day: check who the patient is, link the right patient record, record the arrival and put the patient in the waiting queue. | | |
| Preconditions | The assistant is authenticated and has the permission to check patients in. The patient is at the reception and has a Confirmed appointment of the current day (a walk-in is booked first in UC-16). | | |
| Normal Flow | 1. The assistant opens "Tiếp nhận và hàng chờ", finds the appointment of the day and selects "Tiếp nhận bệnh nhân".<br>2. The assistant checks that the person present is the attendee of the appointment.<br>3. The assistant selects the patient record: the record already linked to the appointment, or an existing record found by search, or a new record (UC-24).<br>4. The assistant chooses the reason for coming and selects "Xác nhận Check-in".<br>5. DCMS records the arrival time, changes the appointment to Checked-in, creates one visit and puts it in the waiting queue. | | |
| Alternative Flows | A1 — The patient arrived earlier than the moment of entry: a user with the permission may enter the earlier arrival time with a reason. | | |
| Exception Flows | E1 — The person present does not match the appointment or the record, at step 2 or 3: DCMS displays MSG35 and creates nothing.<br>E2 — The appointment is not Confirmed, is not of today or has ended, at step 5: DCMS displays MSG34.<br>E3 — The appointment is already checked in: DCMS shows the existing visit and creates no new one.<br>E4 — A possible duplicate record is found, at step 3: DCMS asks the assistant to review it before creating a new record.<br>E5 — The appointment changed meanwhile: DCMS displays MSG41.<br>E6 — The user has no permission: DCMS displays MSG05.<br>E7 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the appointment is Checked-in with the arrival time, the patient record is linked and exactly one waiting visit exists.<br>2. Failure: no visit or queue entry is created. | | |
| Business Rule | GBR-007, GBR-038, GBR-039, GBR-041, GBR-043 | | |

## UC-24 Manage Patient Records

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Search, create, edit, archive and restore patient records, with a check of possible duplicates, and open a patient to see the information in tabs. | | |
| Preconditions | The assistant is authenticated and has the permission to read and manage patients. | | |
| Normal Flow | 1. The assistant opens "Bệnh nhân" and searches by patient code, name or phone number.<br>2. DCMS shows the matching patients.<br>3. The assistant opens a patient, or selects "Thêm hồ sơ" to create one.<br>4. The assistant enters the identification, the contact and, when needed, the guardian.<br>5. DCMS shows the possible duplicates; the assistant reviews them and saves.<br>6. DCMS generates the patient code for a new record, saves the data with the actor and the time, and displays MSG44.<br>7. To stop using or to restore a record, the assistant selects "Ngừng sử dụng" or "Khôi phục hồ sơ", enters a reason and confirms. | | |
| Alternative Flows | A1 — No match, at step 2: DCMS displays MSG43.<br>A2 — The assistant opens a patient: DCMS shows the tabs "Thông tin cơ bản", "Khám", "Điều trị & Thanh toán", "Hình ảnh" and "Đơn thuốc", only those that the user is allowed to see. | | |
| Exception Flows | E1 — A required field is missing or not valid, at step 5: DCMS displays MSG02 or MSG04.<br>E2 — The record changed since it was opened, at step 6: DCMS displays MSG41.<br>E3 — The user has no permission: DCMS displays MSG05.<br>E4 — The service is unavailable: DCMS displays MSG49; the saved data stays unchanged. | | |
| Postconditions | 1. Success: the changes are saved with their audit information; an archived record stays in the history and can be restored.<br>2. Records are never merged automatically.<br>3. Failure: nothing is changed. | | |
| Business Rule | GBR-007, GBR-040, GBR-041, GBR-042, GBR-043 | | |

## UC-25 Verify Record Link

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Check a request that links a customer account to a patient record (own record or a relative's record), and verify, reject or revoke it. | | |
| Preconditions | The assistant is authenticated and has the permission to decide link requests. A request exists (UC-10 or UC-14). | | |
| Normal Flow | 1. The assistant opens "Xác minh liên kết hồ sơ" and sees the requests waiting for verification.<br>2. The assistant opens a request and compares the account with the declared data and the patient record.<br>3. The assistant selects "Xác minh" (own record) or "Cấp quyền xem hồ sơ" (a relative, with the scopes and the expiry date) and confirms.<br>4. DCMS saves the decision, displays MSG16 and notifies the customer. | | |
| Alternative Flows | A1 — The request cannot be accepted, at step 3: the assistant selects "Từ chối" and enters a reason; DCMS displays MSG17.<br>A2 — A verified link must end: the assistant selects "Thu hồi liên kết" or "Thu hồi quyền" with a reason; the customer can no longer read the record. | | |
| Exception Flows | E1 — The record does not match the declared data, at step 3: DCMS does not allow the verification and displays MSG48.<br>E2 — The account or the patient already has a verified own link: DCMS displays MSG47.<br>E3 — The request changed meanwhile: DCMS displays MSG41.<br>E4 — The user has no permission: DCMS displays MSG05.<br>E5 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Verified: the customer can read the record within the granted scope; at most one own link exists for the account.<br>2. Rejected or revoked: the customer cannot read the record and the reason is kept.<br>3. Failure: nothing is changed. | | |
| Business Rule | GBR-023, GBR-027 | | |

## UC-26 Handle Care Tasks

| Primary Actors | Clinic Assistant; Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | Create, edit and follow the care tasks of customers, such as callbacks after a no-show, reminders and follow-up calls, with notes and the result of each contact. | | |
| Preconditions | The user is authenticated and has the permission to read or manage care tasks. | | |
| Normal Flow | 1. The user opens "Chăm sóc khách hàng" and filters the tasks by period, owner, type, status and customer.<br>2. The user selects "TẠO MỚI", enters the customer, the type, the owner, the due date and the content, and saves. DCMS saves the task as "Mở".<br>3. The user opens a task, reads it and records a contact (the outcome and a note) with "Lưu contact". The record does not send a message or book an appointment.<br>4. The user changes the status (Mở, Đang xử lý, Đã xong, Đã hủy) with "Sửa" or "Đánh dấu đã xong".<br>5. The user may export the filtered list to Excel. | | |
| Alternative Flows | A1 — A task is created automatically for a no-show (UC-18): The assistant handles it from the list.<br>A2 — The customer wants to book, at step 3: The assistant uses UC-16 or UC-19. | | |
| Exception Flows | E1 — A required field is missing, at step 2: DCMS displays MSG02.<br>E2 — No contact information is usable: DCMS displays "Chưa có thông tin liên hệ phù hợp. Vui lòng kiểm tra hồ sơ và ghi nhận hướng xử lý.".<br>E3 — The task changed meanwhile: DCMS displays MSG41.<br>E4 — No permission: DCMS displays MSG05.<br>E5 — The service is unavailable: DCMS displays MSG49.<br>E6 — The user closes the form with typed content: DCMS asks "Bỏ nội dung đang nhập?". | | |
| Postconditions | 1. Success: the task, the contact records and the status are saved with the actor and the time.<br>2. A no-show creates only one task; reprocessing does not create a duplicate. | | |
| Business Rule | GBR-032, GBR-033 | | |

## UC-27 Conduct Examination

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Examine a checked-in patient: record the symptoms, the findings and the diagnoses, confirm the examination, and end the visit. | | |
| Preconditions | The dentist is authenticated and has the permission to write examinations. The visit is waiting in the queue and its appointment is Checked-in. | | |
| Normal Flow | 1. The dentist opens the visit from the queue and selects "Bắt đầu khám"; the visit becomes In progress.<br>2. The dentist reviews the health history, then records the reason, the symptoms, the findings on the tooth chart and the notes, and selects "Lưu nháp".<br>3. In the tab "Tổng kết khám" the dentist selects the diagnoses, writes the conclusion and the advice, and chooses the next step (consultation only, follow-up, referral, decision later or proceed to treatment).<br>4. The dentist selects "Hoàn tất khám"; DCMS checks that the required content is present and confirms the examination.<br>5. The dentist selects "Kết thúc lượt khám" and confirms that the work was reviewed; DCMS sets the visit and the appointment to Completed. | | |
| Alternative Flows | A1 — A confirmed examination needs a correction: the dentist selects "Đính chính kết quả" and enters the reason; DCMS saves a new version and keeps the old one.<br>A2 — The next step is "proceed to treatment": the dentist can open the treatment tab of the patient (UC-31).<br>A3 — The patient needs a prescription: the dentist opens the prescription after the examination is confirmed (UC-38). | | |
| Exception Flows | E1 — A required field is empty or not valid, at step 4: DCMS displays MSG02 or MSG04.<br>E2 — The visit is not in the right state: DCMS displays MSG34.<br>E3 — A selected diagnosis is no longer in use: DCMS displays MSG51.<br>E4 — The examination is not confirmed, or an investigation is neither read nor cancelled, at step 5: DCMS displays MSG56 and does not end the visit.<br>E5 — The visit changed meanwhile: DCMS displays MSG41.<br>E6 — The user has no permission: DCMS displays MSG05.<br>E7 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the examination is saved as a version; once confirmed it can be changed only by an amendment with a reason, and every version is kept.<br>2. Ending the visit sets the visit and the appointment to Completed.<br>3. Failure: nothing newly entered is saved. | | |
| Business Rule | GBR-044, GBR-045, GBR-046, GBR-047 | | |

## UC-28 Order Investigations

| Primary Actors | Dentist | Secondary Actors | Clinic Assistant |
|---|---|---|---|
| Description | Order an investigation for a visit (for example a panoramic X-ray), record its result, read it, or cancel the order. | | |
| Preconditions | The visit is In progress (UC-27). The dentist is the dentist of the visit and has the permission to manage examinations. | | |
| Normal Flow | 1. In the tab "Cận lâm sàng" the dentist creates an order: the investigation, the region, the reason, and whether it is needed for the diagnosis.<br>2. DCMS saves the order with the state "Ordered".<br>3. When the result arrives, the dentist or the assistant records the result text or the files; the state becomes "ResultAvailable".<br>4. The dentist reads the result, writes the interpretation and confirms; the order becomes "Reviewed" and appears in the examination. | | |
| Alternative Flows | A1 — The investigation is no longer needed: the dentist cancels the order with a reason; the order becomes "Cancelled" and stays in the history.<br>A2 — The result is an image or a file: it is added as in UC-29. | | |
| Exception Flows | E1 — A field is empty or not valid, or the reason of a cancellation is missing: DCMS displays MSG04.<br>E2 — The visit is not In progress, or the order is not in the right state (for example reviewing an order with no result): DCMS displays MSG34.<br>E3 — The order changed meanwhile: DCMS displays MSG41.<br>E4 — The visit or the order no longer exists: DCMS displays MSG42.<br>E5 — The user is not the dentist of the visit or has no permission: DCMS displays MSG05. | | |
| Postconditions | 1. The order, its result, its interpretation and any cancellation are kept with the actor and the time.<br>2. An order needed for the diagnosis that is neither reviewed nor cancelled prevents the visit from being ended (UC-27). | | |
| Business Rule | GBR-048, GBR-049 | | |

## UC-29 Upload and Review Examination Documents

| Primary Actors | Dentist; Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Upload, view and mark as reviewed the images and documents of a patient, linked to a visit or to a treatment sheet. | | |
| Preconditions | The user is authenticated and has the permission to read clinical documents, and to upload them for the upload action. The patient exists. | | |
| Normal Flow | 1. The user opens the documents of a patient (tab "Ảnh & tài liệu") and may filter them by visit, type and date.<br>2. The user selects the upload, chooses the files (JPEG, PNG or PDF), the type, the source and a description, and the visit or the treatment sheet.<br>3. DCMS checks the permission and the files, stores each valid file and links it to the right patient and visit.<br>4. The user opens a document to see its content and history.<br>5. The dentist of the visit marks a document as reviewed; DCMS records who and when. | | |
| Alternative Flows | A1 — The file is the result of an investigation: it is uploaded with the link to the order (UC-28).<br>A2 — Some files are not valid: DCMS reports the error of each file and keeps the valid ones. | | |
| Exception Flows | E1 — A field is not valid (unknown type, date in the future, description too long), at step 2: DCMS displays MSG04.<br>E2 — The visit is not waiting or in progress, or the order is not in the state "Ordered": DCMS displays MSG34.<br>E3 — The patient, the visit or the document does not exist or does not match: DCMS displays MSG42.<br>E4 — The data changed meanwhile: DCMS displays MSG41.<br>E5 — The user has no permission, or is a dentist who is not the dentist of the visit: DCMS displays MSG05.<br>E6 — The file storage cannot be used: DCMS displays MSG49.<br>E7 — A file is not a real JPEG, PNG or PDF, is empty or too large: DCMS returns the error of the file and does not store it. | | |
| Postconditions | 1. Success: each valid file is stored with its details, the uploader and the time, and linked to the patient and to the visit or the sheet.<br>2. A document is shown to the patient only through the examination record of the visit (UC-11); the upload alone does not publish it.<br>3. Failure: nothing incomplete is shown. | | |
| Business Rule | GBR-049 | | |

## UC-30 Analyze X-ray with AI

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | When the clinic has the AI service, analyze an X-ray image of a visit and show the suggested findings to the dentist, who decides the diagnosis. | | |
| Preconditions | The AI service is configured. The dentist is authenticated, a visit is open and an X-ray document (JPEG or PNG) of the patient is ready. | | |
| Normal Flow | 1. The dentist opens the X-ray document in the examination (UC-29) and selects "Phân tích AI".<br>2. DCMS sends the image to the AI service and waits for the result.<br>3. DCMS shows the image with the suggested findings and their confidence, and keeps the result with the document.<br>4. The dentist reads the suggestion, then records the own conclusion in the examination (UC-27). The suggestion never creates a diagnosis, a plan, a prescription or a charge. | | |
| Alternative Flows | A1 — The AI service is not configured: the button is not shown and the dentist continues without AI.<br>A2 — The dentist runs the analysis again on the same image: DCMS keeps each result, up to 20 analyses per document. | | |
| Exception Flows | E1 — The document is not an X-ray, is not ready, or the limit of 20 analyses is reached, at step 1: DCMS refuses the request.<br>E2 — The AI service fails or does not answer, at step 2: DCMS displays MSG49 with the reason and saves no result; the dentist can retry or continue manually.<br>E3 — The user has no permission: DCMS displays MSG05. | | |
| Postconditions | 1. Success: the result is kept with the document as a suggestion for the dentist.<br>2. Failure: no result is saved and the examination can be completed without AI. | | |
| Business Rule | GBR-050 | | |

## UC-31 Plan Treatment

| Primary Actors | Dentist; Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | Open the treatment sheets of a patient, create a sheet and plan its items (service, teeth, expected minutes, cost, dentist and assistant). | | |
| Preconditions | The user is authenticated and has the permission to read treatment and to update it for changes. The patient exists and is not archived. | | |
| Normal Flow | 1. The user opens the tab "Điều trị & Thanh toán" of the patient and sees the sheets as cards with the status "Chưa xong", "Hoàn tất" or "Đã ngừng".<br>2. The user selects the branch and "+ TẠO PHIẾU MỚI", or opens an existing open sheet (one is open at a time). A new sheet can follow a confirmed examination that proceeds to treatment.<br>3. In "KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ" the user adds the planned items one by one, or applies a ready-made plan (UC-34): the service, the teeth in FDI numbering, the expected minutes, the cost, the dentist and the assistant.<br>4. The user saves the plan. DCMS keeps the planned items with the actor and the time and displays MSG53. | | |
| Alternative Flows | A1 — The patient does not agree to an item: the user records the decision and the item is not performed.<br>A2 — The user prints the open sheet with "In phiếu đang mở". | | |
| Exception Flows | E1 — A required field of a row is missing or invalid (teeth not FDI, minutes outside 1 to 480, negative cost), at step 4: DCMS displays MSG02, MSG04 or MSG75 under the field; a negative price displays MSG60.<br>E2 — The sheet changed meanwhile: DCMS displays MSG41 and loads the latest version.<br>E3 — The sheet is not open for the action: DCMS displays MSG54.<br>E4 — A repeated action was already committed: DCMS displays MSG113.<br>E5 — The user has no permission: DCMS displays MSG05.<br>E6 — The service is unavailable: DCMS displays MSG49; the saved content stays. | | |
| Postconditions | 1. Success: the sheet and its planned items are saved.<br>2. Treatment status, payment and stock stay independent of each other.<br>3. Failure: nothing is committed. | | |
| Business Rule | GBR-031, GBR-046, GBR-051, GBR-052, GBR-053, GBR-057 | | |

## UC-32 Record Treatment Work

| Primary Actors | Dentist; Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | Record the work done on an open treatment sheet, with the images, the notes, the lab order and the materials used, and confirm the agreed price and discount. | | |
| Preconditions | The user is authenticated and has the permission to update treatment. The treatment sheet is open. | | |
| Normal Flow | 1. In "QUÁ TRÌNH ĐIỀU TRỊ/DỊCH VỤ" the user records the work done with "+ Thêm việc đã làm" or by performing a planned item: the service, the teeth, the quantity, the agreed price and the discount (with its reason).<br>2. For a row, the user may add a lab order (UC-35) and the materials used (UC-37).<br>3. The user adds images and writes the note (up to 4,000 characters) with "Lưu ghi chú".<br>4. DCMS saves the work with the actor and the time and displays MSG53. | | |
| Alternative Flows | A1 — The work is outside the agreed plan: the user records the change of scope with its cost, and a new decision of the patient.<br>A2 — The warranty or free work is recorded: the user enters the basis and the item that it covers. | | |
| Exception Flows | E1 — A required field is missing or invalid, at step 1: DCMS displays MSG02, MSG04 or MSG59 under the field.<br>E2 — The materials list is not valid: DCMS displays MSG78; the stock would become negative: DCMS displays MSG79.<br>E3 — The consent does not match the current option: DCMS displays MSG57.<br>E4 — The sheet changed meanwhile: DCMS displays MSG41.<br>E5 — The sheet is not open for the action: DCMS displays MSG54.<br>E6 — A repeated action was already committed: DCMS displays MSG113.<br>E7 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the work, the images, the notes, the materials and the lab orders are saved with the actor and the time.<br>2. Failure: nothing is committed. | | |
| Business Rule | GBR-051, GBR-052, GBR-053 | | |

## UC-33 Close, Reopen or Cancel Treatment Sheet

| Primary Actors | Dentist; Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | Close a treatment sheet with its outcome, reopen it, cancel it or delete a sheet that was opened by mistake. | | |
| Preconditions | The user is authenticated and has the permission to update treatment. The sheet exists. | | |
| Normal Flow | 1. When the treatment ends, the user selects "Hoàn tất" on the sheet.<br>2. The user chooses the outcome: completed as agreed, completed with a revised plan, or stopped before completion, and enters the reason when it is required.<br>3. DCMS checks that the upcoming follow-up appointments of the sheet were handled and closes the sheet.<br>4. To work on a closed sheet again, the user selects "Mở lại phiếu điều trị" and enters the reason; DCMS reopens it. | | |
| Alternative Flows | A1 — The sheet has items that are not finished, at step 2: the closure dialog shows "Còn n hạng mục chưa hoàn tất" and asks for the result and the reason of each.<br>A2 — The sheet was opened by mistake and has nothing recorded: the user deletes it; a deleted sheet is hidden from the record.<br>A3 — The user cancels the sheet with "Hủy phiếu" and a reason; the money already collected is kept in the finance ledger. | | |
| Exception Flows | E1 — The reason to reopen is missing, at step 4: DCMS displays MSG45.<br>E2 — The sheet still has unresolved items: DCMS displays MSG58 and does not close it.<br>E3 — The reason to cancel is missing: DCMS displays MSG99.<br>E4 — The sheet cannot be cancelled while a transfer QR is not reconciled: DCMS displays MSG111.<br>E5 — The sheet changed meanwhile: DCMS displays MSG41.<br>E6 — The user has no permission: DCMS displays MSG05.<br>E7 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the sheet has its new status and every change is kept with the actor, the time and the reason.<br>2. A closed sheet no longer accepts new work until it is reopened; a cancelled sheet cannot be reopened.<br>3. Failure: the sheet is unchanged. | | |
| Business Rule | GBR-054, GBR-055, GBR-056 | | |

## UC-34 Manage Treatment Plan Templates

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Create, rename, edit and delete the ready-made treatment plans of a branch, which are applied to treatment sheets in UC-34. It also covers: Apply a ready-made treatment plan (UC-34) to a treatment sheet, to fill the planned items at once. | | |
| Preconditions | The user has the role Dentist, is authenticated and has the permission to read treatment templates, and to manage them for changes. | | |
| Normal Flow | 1. The dentist opens "Kế hoạch điều trị", chooses the branch and sees the plans with the number of items and the total cost; the dentist may search by name.<br>2. The dentist selects "Tạo kế hoạch", enters the name (up to 160 characters) and saves; the plan opens.<br>3. The dentist selects "+ Thêm hạng mục" and enters the planned date (optional), the teeth (FDI codes), the service, the expected minutes (1 to 480), a note, the dentist and the assistant, and the cost (the reference price of the service is filled in).<br>4. The dentist saves the row. The plan shows the number of items (at most 100) and the total cost.<br>5. The dentist may edit or delete an item, rename the plan ("Sửa tên") or delete the plan after confirming. | | |
| Alternative Flows | A1 — The plan was changed by someone else, at step 4 or 5: DCMS displays MSG41; the dentist selects "Xem phiên bản mới" and keeps the draft or takes the new version.<br>Related function — Apply Treatment Plan:<br>1. On the open sheet the dentist selects "Kế hoạch điều trị".<br>2. DCMS shows the plans of the branch with the number of items and the total cost.<br>3. The dentist chooses a plan and confirms.<br>4. DCMS copies the items of the plan to the planned items of the sheet (service, teeth, minutes, note, dentist, assistant, cost) as drafts.<br>5. The dentist reviews and edits the rows as in UC-31 to UC-33, then confirms them; DCMS displays "Đã lưu kế hoạch điều trị.". | | |
| Exception Flows | E1 — A required field is missing or invalid (name empty, service not chosen, teeth not FDI, minutes or cost out of range, note over 1,000 characters): DCMS displays MSG02, MSG03, MSG04 or "{} phải lớn hơn 0.".<br>E2 — The user has no permission to manage: the creation and edit buttons are disabled; DCMS displays MSG05 on a direct request.<br>E3 — The same request is repeated: DCMS displays "Thao tác này đã được ghi nhận. Vui lòng kiểm tra kết quả hiện có để tránh ghi trùng.".<br>E4 — The service is unavailable: DCMS displays MSG49; the saved plan stays.<br>Related function — Apply Treatment Plan (exceptions):<br>E1 — The plan changed or was deleted meanwhile: DCMS displays MSG41 or MSG42.<br>E2 — The sheet changed meanwhile: DCMS displays MSG41.<br>E3 — No permission: DCMS displays MSG05.<br>E4 — The service is unavailable: DCMS displays MSG49; nothing is added. | | |
| Postconditions | 1. Success: the plan and its items are saved with a new version.<br>2. Deleting a plan does not change treatment sheets that already used it.<br>3. Failure: nothing is committed. | | |
| Business Rule | GBR-052, GBR-057 | | |

## UC-35 Order Lab Work

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Create a lab order for a piece of work of a treatment sheet (for example a crown), with the laboratory, the quantity, the material, the teeth and the due date. | | |
| Preconditions | The dentist is authenticated and has the permission to manage lab orders. A work row exists on the sheet. | | |
| Normal Flow | 1. On a work row the dentist selects the lab icon; the window "PHIẾU LABO" opens.<br>2. The dentist chooses the laboratory, the type of work, the quantity, the material, the teeth, the due date, the fee and a note.<br>3. The dentist saves. DCMS creates the lab order with the status "Chưa gửi" and a code, and links it to the work row.<br>4. The dentist may edit or delete the order while it is allowed.<br>5. The order is followed in UC-36. | | |
| Alternative Flows | A1 — The default laboratory of the work type is filled in at step 2 and the dentist may change it. | | |
| Exception Flows | E1 — A required field is missing or invalid: DCMS displays MSG02, MSG04 or "{} phải lớn hơn 0.".<br>E2 — The order changed meanwhile: DCMS displays MSG41.<br>E3 — No permission: DCMS displays MSG05 and the window is read-only.<br>E4 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: one lab order exists for the work row with its history.<br>2. The fee of the lab order is kept for the payment of the sheet (UC-39).<br>3. Failure: no order is created. | | |
| Business Rule | GBR-051, GBR-058, GBR-064 | | |

## UC-36 Track Lab Work

| Primary Actors | Dentist; Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Follow the lab orders of the clinic by status, move an order through sent, received, delivered or rework with a history, and open the order. | | |
| Preconditions | The user is authenticated and has the permission to read lab orders; to change the status the user needs the permission to track them, and to edit the order the permission to manage them. | | |
| Normal Flow | 1. The user opens "Quản lý công việc labo" and sees the tiles with the number of orders: Chưa gửi, Đã gửi xưởng, Đã nhận về, Quá hạn hẹn, Đã giao bệnh nhân, Làm lại.<br>2. The user selects a tile to filter, and may search by code, patient code, patient name or work, and filter by branch, laboratory, dentist and due dates.<br>3. The user opens an order in the panel on the right: patient, work, teeth, laboratory, dentist, dates, fee, paid and note.<br>4. The user enters a note (up to 300 characters) and selects the next status: "Gửi xưởng", "Đã nhận về", "Giao bệnh nhân" or "Yêu cầu làm lại".<br>5. DCMS checks the version, saves the new status with the actor, the time and the note, and updates the history "Nhật ký trạng thái".<br>6. The user selects "Sửa phiếu" (or "Xem phiếu đầy đủ") to open the full order as in UC-35. | | |
| Alternative Flows | A1 — An order is late: DCMS marks it "Quá hạn hẹn" and counts it in the tile. | | |
| Exception Flows | E1 — The order changed meanwhile, at step 5: DCMS displays MSG41 and reloads the list.<br>E2 — The status is not allowed from the current one: DCMS does not offer the button; a direct request returns MSG05.<br>E3 — No permission to track: the status buttons are not shown.<br>E4 — The order is no longer available: DCMS displays MSG42.<br>E5 — The service is unavailable: DCMS displays MSG49 with the button "Thử lại". | | |
| Postconditions | 1. Success: the order has the new status, and every change is in the history with the actor and the time.<br>2. Failure: the status is unchanged. | | |
| Business Rule | GBR-059 | | |

## UC-37 Record Materials Used

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Record the materials used for a piece of work on a treatment sheet, and take them out of the material stock. | | |
| Preconditions | The dentist is authenticated and has the permission to update the actual work (treatment.actuals.update). A work row exists on the sheet. Materials exist in the material catalogue and have been received through purchases (material purchases screen). | | |
| Normal Flow | 1. On a work row the dentist selects the material icon; the window of materials opens with the current usage of the row.<br>2. The dentist chooses each material and enters the quantity used (at most 3 decimals, from 0 up to 1,000,000; each material once; at most 30 lines).<br>3. The dentist saves the work row. Under a global lock, DCMS recalculates the stock of each material as received purchases minus the usage of all rows (except the row being saved).<br>4. If the stock is enough, DCMS replaces the usage lines of the row with the new list, with the patient, the branch, the sheet, the time and the actor.<br>5. The usage appears in the stock and usage history of the materials. | | |
| Alternative Flows | A1 — A usage entered by mistake: The dentist removes the line (or all lines) and saves; the removed quantity returns to the stock. Deleting the work row also releases its usage. | | |
| Exception Flows | E1 — The list is invalid (more than 30 lines, a repeated material, a quantity not positive, above 1,000,000 or with more than 3 decimals, an unknown or deactivated material): DCMS displays "Danh sách vật liệu dùng không hợp lệ. Vui lòng kiểm tra vật liệu và số lượng." (MATERIAL_USAGE_INVALID, HTTP 400).<br>E2 — The stock is not enough: DCMS displays "Tồn kho không đủ." with the name, the stock and the quantity needed of each short material (MATERIAL_STOCK_INSUFFICIENT, HTTP 409) and saves nothing.<br>E3 — The row or the sheet changed meanwhile: DCMS displays MSG41.<br>E4 — The sheet is not open: DCMS displays MSG54.<br>E5 — No permission: DCMS displays MSG05. | | |
| Postconditions | 1. Success: the usage lines of the row equal the saved list and the stock is reduced accordingly. Material stock is global across branches.<br>2. A change of a purchase that would make a stock negative is refused (MATERIAL_STOCK_NEGATIVE).<br>3. Failure: the stock and the usage are unchanged. | | |
| Business Rule | GBR-060, GBR-070, GBR-072 | | |

## UC-38 Prescribe Medicine

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Write, edit, finalise and print a prescription for a visit, with the care instructions and the safety check. | | |
| Preconditions | The dentist is authenticated and has the permission to prescribe. A visit of the patient exists. Medicines exist in the catalogue. | | |
| Normal Flow | 1. In the tab "Đơn thuốc" of the patient the dentist opens "ĐƠN THUỐC" of a visit.<br>2. The dentist adds medicines from the catalogue, or applies a template, and enters for each: quantity, unit, route, dose for morning, noon, afternoon and evening, meal timing, days and instructions.<br>3. DCMS runs the safety check (for example allergies and duplicates) and shows the warnings.<br>4. The dentist enters the diagnosis and the general advice and saves the draft.<br>5. The dentist finalises the prescription. DCMS keeps a snapshot and displays "Đã hoàn tất đơn thuốc. Tồn kho chỉ thay đổi khi ghi nhận cấp hoặc sử dụng thuốc thực tế."; it does not change stock or any bill.<br>6. The dentist or the assistant prints it. The patient sees it in UC-11. | | |
| Alternative Flows | A1 — A finalised prescription needs a correction: The dentist makes a correction with a reason; the old content is kept in the history ("Vui lòng nhập lý do điều chỉnh đơn thuốc. Nội dung đơn trước đó sẽ được lưu lại." if the reason is missing).<br>A2 — The medicine is bought outside the clinic: The prescription states it ("Đơn thuốc này không phát sinh xuất kho hoặc khoản thu thuốc tại phòng khám."). | | |
| Exception Flows | E1 — A required field is missing or invalid: DCMS displays MSG02 or "{} phải lớn hơn 0.".<br>E2 — The prescription changed meanwhile: DCMS displays MSG41.<br>E3 — A correction reason is missing: DCMS displays "Vui lòng nhập lý do điều chỉnh đơn thuốc. Nội dung đơn trước đó sẽ được lưu lại.".<br>E4 — No permission: DCMS displays MSG05.<br>E5 — The service is unavailable: DCMS displays MSG49; the draft stays. | | |
| Postconditions | 1. Success: the finalised prescription is kept as it was at finalisation; later changes of the catalogue or of templates do not change it.<br>2. Failure: the prescription stays a draft. | | |
| Business Rule | GBR-043, GBR-061, GBR-070 | | |

## UC-39 Collect Treatment Payment

| Primary Actors | Clinic Owner | Secondary Actors | Payment Gateway (PayOS) |
|---|---|---|---|
| Description | Record the cash collected for the services and the lab fees of a treatment sheet, receive a deposit (advance) in cash or through PayOS, collect online through a PayOS QR order, and allocate an advance to the lines of the sheet. | | |
| Preconditions | The user has the role Clinic Owner and is authenticated; every payment function of the sheet is refused to other roles (HTTP 403). A treatment sheet with lines exists. For online payment PayOS is configured and the webhook can reach the server. | | |
| Normal Flow | 1. On the treatment sheet the owner selects "Trả tiền"; DCMS shows the payment context: each service line with price, paid and due, each lab fee with total, paid, due, the minimum 30% and the remaining priority amount, and the advance received.<br>2. The owner enters the paid date (not in the future) and the amount collected for each line, or a deposit amount ("Số tiền cọc").<br>3. DCMS checks that every amount is positive, the amounts are not above what is due, a line or lab order appears only once, and the total is at most 1,000,000,000,000 VND.<br>4. DCMS checks the lab priority: at least 30% of each lab fee must be covered before money is collected for other services.<br>5. The owner confirms. DCMS records each collection in the ledger once, with the actor and the time (method "Cash"), and returns the updated context.<br>6. For an online payment the owner creates a PayOS order for the amounts (or for a deposit); DCMS shows the QR and keeps the order pending.<br>7. PayOS sends the result to the webhook; DCMS verifies the signature, records the collection and updates the sheet. | | |
| Alternative Flows | A1 — A deposit is already received, at step 2: The owner allocates the advance to the lines ("advance allocation"); the allocated amount reduces the advance, and each line and lab amount is checked as above.<br>A2 — An unexpired QR order already exists, at step 6: DCMS asks to reopen the current QR instead of creating another.<br>A3 — The sheet needs a deposit exception: The owner records it in the treatment sheet ("deposit exception") with the reason.<br>A4 — The customer did not come and the deposit is kept: The owner records the forfeit in UC-45. | | |
| Exception Flows | E1 — An amount is not positive, a line is repeated, the date is in the future, the request key is empty, or no line is given: DCMS refuses with "Thông tin thu tiền không hợp lệ." (HTTP 400); an amount outside the limit is refused with "Số tiền không hợp lệ." or "Số tiền vượt giới hạn.". A deposit amount that is not valid is refused with "Số tiền cọc không hợp lệ.".<br>E2 — A line or its due amount changed meanwhile: DCMS refuses with "Dịch vụ hoặc số còn phải thu đã thay đổi. Vui lòng tải lại." (HTTP 409).<br>E3 — The 30% lab priority is not met: DCMS refuses with "Cần ưu tiên đủ 30% phí labo trước khi thu cho dịch vụ khác." and records nothing.<br>E4 — The request key was already used for a different collection: DCMS refuses with "Mã yêu cầu đã được dùng cho một khoản thu khác."; the same request repeated returns the existing result without recording it twice.<br>E5 — The PayOS webhook has an invalid signature or data: DCMS rejects it (HTTP 400) and records nothing.<br>E6 — The user is not the owner: the request is refused (HTTP 403).<br>E7 — The server fails: the transaction is rolled back and nothing is recorded. | | |
| Postconditions | 1. Success: each collection is written once in the finance ledger inside a serializable transaction that locks the treatment sheet, so two collections cannot both use the same due amount.<br>2. A repeated request with the same key does not create a second collection.<br>3. Failure: nothing is recorded as paid. | | |
| Business Rule | GBR-056, GBR-058, GBR-062, GBR-063, GBR-064, GBR-065, GBR-066, GBR-067 | | |

## UC-40 Manage Clinic Catalogs

| Primary Actors | Clinic Owner; Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Add, edit, search and deactivate the clinic catalogs: services with their prices, diagnoses, customer groups, lab companies and lab work types. | | |
| Preconditions | The user is authenticated and has the permission to read the catalog, and to manage it for changes (the Dentist maintains the services and their prices). | | |
| Normal Flow | 1. The user opens "Thiết lập hệ thống" and chooses the section ("Dịch vụ điều trị", "Chẩn đoán / Tên bệnh", "Nhóm khách hàng", "Công ty labo" or "Loại công việc labo"), then sees the entries with the code, the name, the status and, for a service, the reference price and the billing basis. The user may search by code or name.<br>2. The user adds an entry and enters the code, the name, the description and, for a service, the price and the billing basis (per visit, tooth, jaw or package).<br>3. The user selects "Lưu". DCMS saves the entry and displays MSG53.<br>4. To change an entry, the user edits it and saves. A new price applies to new decisions only; agreed prices are not changed.<br>5. To stop using an entry, the user deactivates it with a reason; a deactivated entry can be reactivated. | | |
| Alternative Flows | A1 — The entry was already used: DCMS does not delete it but offers to deactivate it (MSG52). | | |
| Exception Flows | E1 — A required field is empty or too long, at step 3: DCMS displays MSG02 or MSG03; a negative price displays MSG60.<br>E2 — The code or the name already exists: DCMS displays MSG50.<br>E3 — The entry is inactive and chosen for a new transaction: DCMS displays MSG51.<br>E4 — The entry is used by the system for linked records and cannot be deactivated: DCMS displays MSG87.<br>E5 — The entry was changed by someone else: DCMS displays MSG41.<br>E6 — The user has no permission: DCMS displays MSG05.<br>E7 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the entry is saved; past records keep the catalog data that they used.<br>2. Failure: nothing is changed. | | |
| Business Rule | GBR-045, GBR-053, GBR-068, GBR-069 | | |

## UC-41 Manage Supply Catalog

| Primary Actors | Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | Add, edit, search and deactivate the supplies of the clinic: material categories, materials, medicines, suppliers and their units. | | |
| Preconditions | The Clinic Owner is authenticated and has the permission to manage the supply catalog. | | |
| Normal Flow | 1. The Clinic Owner opens "Thiết lập hệ thống" and chooses the section: "Loại dụng cụ / Vật liệu", "Công ty cung cấp vật liệu" or "Thuốc / Dược phẩm".<br>2. DCMS lists the entries with the code, the name, the unit and the status; the user may search.<br>3. The Clinic Owner adds an entry and enters the code, the name, the unit and the conversion to the base unit (materials allow up to 3 decimals, medicines whole units); for a supplier the type (supplier or lab company).<br>4. The Clinic Owner saves. DCMS saves the entry and displays MSG53.<br>5. To stop using an entry, the Clinic Owner deactivates it with a reason. | | |
| Alternative Flows | A1 — The entry is already used: DCMS deactivates it instead of deleting it. | | |
| Exception Flows | E1 — A required field is empty or too long, at step 4: DCMS displays MSG02 or MSG03; a conversion that is not exact displays MSG04.<br>E2 — The code or the name already exists: DCMS displays MSG50 or MSG85.<br>E3 — The category still has materials, at deletion: DCMS displays MSG86.<br>E4 — The entry is used by linked records: DCMS displays MSG87.<br>E5 — The entry was changed by someone else: DCMS displays MSG41.<br>E6 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the entry is saved and can be chosen in purchases, treatment and prescriptions.<br>2. Failure: nothing is changed. | | |
| Business Rule | GBR-068, GBR-069, GBR-070 | | |

## UC-42 Manage Dental Supplies

| Primary Actors | Clinic Owner; Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Record the purchases of materials and see the stock on hand of each material. | | |
| Preconditions | The user is authenticated and has the permission to manage dental supplies. The material and the supplier exist and are active. | | |
| Normal Flow | 1. The user opens "Dụng cụ - vật liệu" and sees the purchases and the stock of each material.<br>2. The user adds a purchase and enters the material, the supplier, the quantity, the entry date, the received date, the expiry date, the total and the amount paid.<br>3. The user saves. DCMS adds the quantity to the stock and displays MSG53.<br>4. The user may edit a purchase; DCMS recalculates the stock. | | |
| Alternative Flows | A1 — The user opens the stock list: DCMS shows the quantity received, used and on hand of each material. | | |
| Exception Flows | E1 — A required field is empty, at step 3: DCMS displays MSG02.<br>E2 — A date is not valid (received before the entry date or in the future, expiry before the entry date): DCMS displays MSG81.<br>E3 — The paid amount is higher than the total: DCMS displays MSG82.<br>E4 — The material or the supplier is deleted or inactive: DCMS displays MSG83 or MSG84.<br>E5 — The change would make the stock negative: DCMS displays MSG80.<br>E6 — The purchase was changed by someone else: DCMS displays MSG41.<br>E7 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the purchase is saved and the stock on hand equals the quantity received minus the quantity used.<br>2. Failure: nothing is changed. | | |
| Business Rule | GBR-060, GBR-070, GBR-071, GBR-072 | | |

## UC-43 Manage Branches and Dental Chairs

| Primary Actors | Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | Create and edit the branches and the dental chairs of the clinic, and deactivate them after a check of their open obligations. | | |
| Preconditions | The Clinic Owner is authenticated and has the permission to manage branches. | | |
| Normal Flow | 1. The Clinic Owner opens "Thiết lập hệ thống", section "Chi nhánh & Ghế điều trị", and sees the branches with their chairs and status.<br>2. The Clinic Owner selects "Thêm chi nhánh" and enters the code and the name, then adds the chairs with their code and name.<br>3. The Clinic Owner saves. DCMS saves the data and displays MSG53.<br>4. To deactivate a branch or a chair, the Clinic Owner opens the impact preview, which shows the future appointments, the visits in progress, the open sheets and the lab orders, enters the reason and confirms. | | |
| Alternative Flows | A1 — The Clinic Owner activates a chair: DCMS allows it only when its branch is active. | | |
| Exception Flows | E1 — A required field is empty or too long, at step 3: DCMS displays MSG02 or MSG03.<br>E2 — The code already exists: DCMS displays MSG50.<br>E3 — The branch or the chair still has open obligations, at step 4: DCMS shows them and does not deactivate; nothing is cancelled automatically.<br>E4 — The data was changed by someone else: DCMS displays MSG41.<br>E5 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the branch or the chair is saved with its new status and the reason.<br>2. Failure: nothing is changed. | | |
| Business Rule | GBR-073, GBR-074 | | |

## UC-44 Manage General Settings

| Primary Actors | Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | Set the general settings of the clinic: the idle sign-out time of staff, the required patient fields, the patient code and the print data, with a version history. | | |
| Preconditions | The Clinic Owner is authenticated and has the permission to manage the system settings. | | |
| Normal Flow | 1. The Clinic Owner opens "Thiết lập hệ thống", section "Thiết lập chung", and sees the current settings and their version.<br>2. The Clinic Owner changes the staff idle sign-out time (5, 10, 20, 30, 60, 90 or 120 minutes), the patient fields that are required, the patient code mode and prefix, or the print data.<br>3. The Clinic Owner enters the reason and selects "Lưu".<br>4. DCMS saves the settings as a new version; the change applies to new records and to the next printout. | | |
| Alternative Flows | A1 — The Clinic Owner opens "Lịch sử thiết lập" and restores an older version: DCMS saves it as a new version.<br>A2 — The Clinic Owner previews the number of staff sessions that a shorter idle time would end before saving. | | |
| Exception Flows | E1 — Nothing was changed, at step 3: DCMS displays MSG88 and does not save.<br>E2 — A required patient field does not exist in the patient record: DCMS displays MSG89.<br>E3 — A value is empty, too long or not valid (for example the code prefix): DCMS displays MSG02, MSG03 or MSG04.<br>E4 — The settings were changed by someone else: DCMS displays MSG41.<br>E5 — The reason is missing: DCMS displays MSG02.<br>E6 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: a new version of the settings exists with the reason and the actor.<br>2. Failure: the current settings stay in effect. | | |
| Business Rule | GBR-040, GBR-042, GBR-075 | | |

## UC-45 View Finance Statistics

| Primary Actors | Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | See the amount to collect, the money received, the advance, the receivables, the cost of materials and the profit of the clinic for a period, open the list behind each figure, and record the forfeit of a deposit when a customer did not come. | | |
| Preconditions | The user has the role Clinic Owner and is authenticated. | | |
| Normal Flow | 1. The owner opens "Thống kê tài chính" and chooses the dates the treatment sheets were opened and, when allowed, the branch.<br>2. DCMS shows six figure cards (Số tiền cần thu dịch vụ, Tiền thực thu, Tiền nhận trước, Tiền còn cần thu, Tiền dụng cụ vật liệu, Lãi), a bar chart, a ring chart, the collection progress and the profit margin.<br>3. The owner opens a figure to see the list behind it: services, payments, advances, outstanding sheets, material purchases or profit entries, 20 rows per page.<br>4. From a row the owner opens the treatment sheet in "Thống kê công việc/dịch vụ".<br>5. In the list of outstanding sheets, for a sheet whose deposit can be forfeited, the owner selects "Khách không đến, bỏ cọc", enters the reason (up to 500 characters) and confirms. DCMS hides the sheet and records the deposit as forfeited. | | |
| Alternative Flows | A1 — No data in the period, at step 2: DCMS shows the figures as 0 and the lists as empty ("Không có dữ liệu báo cáo phù hợp với bộ lọc đã chọn."). | | |
| Exception Flows | E1 — The user is not the owner: DCMS displays MSG05 and shows no figure.<br>E2 — The forfeit reason is missing: the button stays disabled.<br>E3 — The sheet changed meanwhile, at step 5: DCMS displays MSG41 and reloads.<br>E4 — The data cannot be loaded: DCMS shows "Không tải được thống kê." (MSG49) with the button "Tải lại". | | |
| Postconditions | 1. The figures only read the data; the cost of materials is the total of the purchases of the whole clinic in the period, and profit is counted only for fully allocated sheets.<br>2. A forfeit records the deposit as a no-show income and hides the sheet; it cannot be used for payment any more. | | |
| Business Rule | GBR-062, GBR-076 | | |

## UC-46 View Work and Service Statistics

| Primary Actors | Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | See the work and the services of the clinic by period, branch and dentist: the value of the work, the amount paid and the amount remaining. | | |
| Preconditions | The Clinic Owner is authenticated and has the permission to read the statistics. | | |
| Normal Flow | 1. The Clinic Owner opens "Thống kê công việc/dịch vụ" and chooses the period, the branch and, if needed, the dentist.<br>2. DCMS shows the totals and the list of work lines: quantity, price, discount, amount, paid and remaining.<br>3. The Clinic Owner searches, sorts and pages the list (20, 50 or 100 per page) and opens the treatment sheet of a line. | | |
| Alternative Flows | A1 — There is no data in the period: DCMS shows MSG01. | | |
| Exception Flows | E1 — The period or the branch is not valid, at step 1: DCMS displays MSG101.<br>E2 — The user has no permission: DCMS displays MSG05.<br>E3 — The service is unavailable: DCMS displays MSG49. | | |
| Postconditions | 1. Success: the figures match the treatment sheets; deleted sheets and deleted work are not counted.<br>2. Failure: nothing is shown. | | |
| Business Rule | GBR-077 | | |

