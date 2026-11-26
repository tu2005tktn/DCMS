# II. Use Case Specifications

> Reading guide. Flows follow the current code (backend, web, mobile) wherever it was reviewed. Message ids come from the SRS catalogue "System Messages"; an id marked † exists in the SRS catalogue but the current code does not produce it, so the screen shows another text (or none). Where the code returns its own text without an id, the text itself is quoted.

## 1.1 UC-01 Sign In / Sign Out

| Primary Actors | Registered User (Patient, Dentist, Clinic Assistant, Clinic Owner, Administrator) | Secondary Actors | None |
|---|---|---|---|
| Description | Authenticate an existing account, establish a session with the permissions of the account, and end the session on sign-out. | | |
| Preconditions | Sign-in: an active account exists. Sign-out: an authenticated session exists. | | |
| Normal Flow | A. Sign In<br>1. The user opens the Sign-in screen of the customer area or of the staff area and enters an e-mail or user name and a password.<br>2. DCMS validates the credentials and checks the status of the account.<br>3. DCMS establishes a session and shows the functions permitted for the account (the customer home, or the staff menu built from the permissions). The sign-in flow ends.<br>B. Sign Out<br>1. The authenticated user selects Sign Out (this device or all devices).<br>2. DCMS invalidates the session.<br>3. DCMS returns the user to the unauthenticated interface. The sign-out flow ends. | | |
| Alternative Flows | A1 — Account recovery, at Sign In step 1: The user selects "Quên mật khẩu?" and continues with UC-04. UC-01 ends without a session.<br>A2 — Sign in with Google, at Sign In step 1: The user selects the Google button and continues with UC-02.<br>A3 — Idle time, during a staff session: When the user has not acted for the idle time set in the general settings, DCMS ends the session and returns the user to Sign-in. | | |
| Exception Flows | E1 — Invalid credentials or unavailable account, at Sign In step 2: DCMS displays MSG09 without saying which credential or which account condition caused the failure. No session is created.<br>E2 — The service is unavailable, at Sign In step 2 or 3: DCMS displays MSG144 and does not report a successful sign-in.<br>E3 — The session has expired when Sign Out is requested: DCMS displays MSG07 and returns the user to the unauthenticated interface.<br>E4 — A required field is empty, at Sign In step 1: DCMS shows "Vui lòng nhập email hoặc tên đăng nhập." or "Vui lòng nhập mật khẩu." and does not send the request. | | |
| Postconditions | 1. Sign-in succeeded: a session exists.<br>2. Sign-out succeeded: the session is invalid and cannot authorize further requests.<br>3. Sign-in failed: no new session exists. | | |
| Business Rule | GBR-026, GBR-027, GBR-028, GBR-073, GBR-075, GBR-081, GBR-082 | | |

## 1.2 UC-02 Sign In with Google

| Primary Actors | Patient (Guest) | Secondary Actors | Google Identity Service |
|---|---|---|---|
| Description | Authenticate a customer with a Google account. A customer who signs in with Google for the first time gets a new customer account. | | |
| Preconditions | Google sign-in is configured on the server (client ID). The customer has a Google account. | | |
| Normal Flow | 1. The customer opens Customer Sign-in and selects the Google button.<br>2. Google asks the customer to choose an account and returns an identity token to DCMS.<br>3. DCMS verifies the token with Google.<br>4. If the Google identity is already linked to a customer account, DCMS uses that account.<br>5. If it is not linked, DCMS checks that the e-mail of the Google account does not belong to another account, creates a customer account (role Guest, e-mail confirmed) and links the Google identity to it.<br>6. DCMS establishes a session and opens the customer home. | | |
| Alternative Flows | A1 — Google sign-in is not configured, at step 1: The screen shows "Đăng nhập Google chưa được cấu hình trên máy chủ." and the user uses UC-01. | | |
| Exception Flows | E1 — The token is invalid, or the linked account is not a customer account, at step 3 or 4: DCMS displays MSG09 and creates no session.<br>E2 — The e-mail already belongs to another account, at step 5: DCMS refuses and asks the customer to sign in with the method used before, or to choose another Google account. No account is created.<br>E3 — Google cannot be loaded, or returns no credential, at step 2: The screen shows "Không tải được Google. Vui lòng kiểm tra kết nối." or "Google không trả về thông tin đăng nhập.".<br>E4 — The service is unavailable, at step 3 to 6: DCMS displays MSG144. | | |
| Postconditions | 1. Success: a session exists; a new customer account exists only in the case of step 5, with an audit record.<br>2. Failure: no session and no new account. | | |
| Business Rule | GBR-026, GBR-027, GBR-029, GBR-075, GBR-081 | | |

## 1.3 UC-03 Register Customer Account

| Primary Actors | Guest | Secondary Actors | None |
|---|---|---|---|
| Description | Create a customer account with a Vietnamese mobile number, an e-mail and a password that follows the password policy of the system. | | |
| Preconditions | The visitor has no account with the same phone number or e-mail. The password policy is configured. | | |
| Normal Flow | 1. The visitor opens Customer Register from the Access Portal or from Customer Sign-in.<br>2. The visitor enters the full name, the phone number, the e-mail, the password and the password confirmation. DCMS shows the password policy.<br>3. DCMS validates each field and the password against the policy.<br>4. The visitor selects "Đăng ký".<br>5. DCMS creates the customer account (role Guest) and displays MSG11.<br>6. DCMS opens Customer Sign-in. | | |
| Alternative Flows | A1 — The visitor already has an account, at step 1: The visitor selects "Đăng nhập" and continues with UC-01. | | |
| Exception Flows | E1 — A required field is empty or the format is wrong (phone number not a mobile number of 10 digits, e-mail invalid), at step 3: DCMS displays MSG02 or MSG04 under the field.<br>E2 — The input is too long (name over 160 characters, e-mail over 254), at step 3: DCMS displays MSG03.<br>E3 — The password does not follow the policy, or the confirmation differs, at step 3: DCMS displays MSG109 or "Xác nhận mật khẩu không khớp.".<br>E4 — The phone number or e-mail already belongs to an account, at step 5: DCMS displays MSG139 with the links to sign in or to recover the account. No account is created.<br>E5 — The service is unavailable, at step 5: DCMS displays MSG144. | | |
| Postconditions | 1. Success: a customer account with the role Guest exists and can sign in. It does not create a Patient profile and is not linked to one.<br>2. Failure: no account is created. | | |
| Business Rule | GBR-026, GBR-028, GBR-029, GBR-072, GBR-081 | | |

## 1.4 UC-04 Recover Account

| Primary Actors | Registered User (Patient, Staff) | Secondary Actors | None |
|---|---|---|---|
| Description | Restore access to an existing account through the verification and the credential reset of the system. | | |
| Preconditions | The recovery policy is configured. The user knows an identifier of the account. No session is required. | | |
| Normal Flow | 1. The user opens Recover Account (customer or staff) and enters an account identifier.<br>2. DCMS displays the same neutral message MSG12 whether or not an eligible account exists.<br>3. For an eligible account, DCMS sends the recovery instructions through the configured channel.<br>4. The user follows the instructions and submits the verification proof.<br>5. DCMS checks that the proof is valid, not expired and not used.<br>6. The user enters a new password.<br>7. DCMS checks the password policy and the validity of the proof, replaces the password and displays MSG13. | | |
| Alternative Flows | A1 — No eligible account, after step 2: DCMS sends nothing; the public message stays MSG12.<br>A2 — The user cannot use the verification channel, before step 4: The user asks an Administrator for help, who manages the account outside this use case. This does not reset or unlock the account by itself.<br>A3 — The user did not receive the proof, at step 4: The user asks DCMS to send it again; DCMS shows MSG12. | | |
| Exception Flows | E1 — The proof is invalid, expired or already used, at step 5: DCMS displays MSG108 and does not change the password.<br>E2 — The new password violates the policy, at step 7: DCMS displays MSG109. The old password stays.<br>E3 — The instructions cannot be delivered, at step 3: DCMS does not reset the password and still answers with MSG12; the failure is kept for follow-up.<br>E4 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Success: the new password is stored and the recovery is recorded. No session is created.<br>2. Failure or abandonment: the old password stays.<br>3. All outcomes: recovery does not unlock a locked account, change roles or link a patient record. | | |
| Business Rule | GBR-028, GBR-073, GBR-075, GBR-081, GBR-082 | | |

## 1.5 UC-05 Receive Notifications

| Primary Actors | Registered User (Patient, Staff) | Secondary Actors | Push Notification Service |
|---|---|---|---|
| Description | Deliver the notifications of DCMS in real time and as push notifications on the mobile app, and let the user list them, read them and open the related screen. | | |
| Preconditions | The user is authenticated. For push notifications, the mobile app has registered a device. | | |
| Normal Flow | 1. An event occurs (for example an appointment is booked, changed or cancelled, a reminder is due, a support request is answered).<br>2. DCMS creates a notification for each eligible account and sends it in real time (SignalR) to the open app.<br>3. For a registered mobile device, DCMS also sends a push notification through the Push Notification Service. The push content does not contain private clinical details.<br>4. The user opens the notification list and sees the unread count.<br>5. The user opens a notification. DCMS checks that its link still points to an object the user may open and opens that screen.<br>6. DCMS marks the notification as read; the user can also mark all notifications as read. | | |
| Alternative Flows | A1 — The app is closed, at step 2: The notification stays in the list until the user opens the app; the push notification may reach the device.<br>A2 — The user turns push notifications off: DCMS keeps the in-app notification and stops the push for that device. | | |
| Exception Flows | E1 — The list is empty, at step 4: DCMS displays MSG140†.<br>E2 — The linked object no longer exists or is not accessible, at step 5: DCMS displays MSG113 or MSG06 and does not open it.<br>E3 — The push cannot be delivered, at step 3: The in-app notification stays valid; the failure is recorded. DCMS displays MSG94 when no channel works.<br>E4 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Notifications are kept in the list with their read status, separate from the delivery attempts.<br>2. A provider's acceptance of a push does not mean that the user has read it. | | |
| Business Rule | GBR-015, GBR-077, GBR-078 | | |

## 1.6 UC-06 Book Appointment Online

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | Book a confirmed examination for the person who will attend, in five steps: examinee, schedule, contact, review, completion (web and mobile). | | |
| Preconditions | The customer is authenticated. At least one branch accepts bookings, with a published working schedule and an examination service. | | |
| Normal Flow | 1. The customer opens "Đặt lịch khám" and chooses who will be examined: "Bản thân" (UC-06) or "Người thân" (UC-07).<br>2. The customer chooses the branch and the dentist ("Bất kỳ bác sĩ (ghế trống)" is allowed) and the date. DCMS shows the free time slots of "Buổi sáng" and "Buổi chiều" for the duration of the examination service and the free chairs.<br>3. The customer chooses a slot and may add a reason for the visit.<br>4. The customer confirms the name and the phone number or e-mail of the booker, and the name of the examinee.<br>5. The customer reviews the branch, the service, the dentist, the time, the booker and the examinee, then selects "Xác nhận đặt lịch".<br>6. DCMS checks again the latest schedule, the lead time, the full duration, the conflicts of the dentist and of the identified examinee, and the chair capacity.<br>7. DCMS creates a Confirmed appointment (for "Bất kỳ bác sĩ", with the dentist of the chosen slot), displays MSG29 and the appointment code.<br>8. The customer opens the appointment (UC-08) or returns to the home. | | |
| Alternative Flows | A1 — No slot matches, at step 2: DCMS displays MSG22; the customer chooses another date or dentist.<br>A2 — The same request is sent again after a network error, at step 7: DCMS returns the existing result and displays MSG30; no second appointment is created. | | |
| Exception Flows | E1 — Information about the examinee or the booker is missing or wrong, at step 4: DCMS displays MSG23 or MSG02/MSG04 under the field.<br>E2 — Less than 60 minutes before the start, at step 6: DCMS displays MSG24 and creates nothing.<br>E3 — The slot is no longer free, there are not enough consecutive units, or there is a conflict, at step 6: DCMS displays MSG25, MSG26 or MSG27, loads the slots again and returns to step 2.<br>E4 — The chairs cannot cover the interval, at step 6: DCMS displays MSG28.<br>E5 — The booking data changed meanwhile, at step 6: DCMS displays MSG111 and returns to step 2.<br>E6 — The session has expired, at step 5 or 6: DCMS displays MSG07 and "Đăng nhập".<br>E7 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Success: exactly one Confirmed appointment exists with the booker and the examinee recorded separately; its full interval is reserved. No Patient profile, visit or queue entry is created.<br>2. Failure: no appointment or reservation is created.<br>3. A failed notification does not cancel the appointment. | | |
| Business Rule | GBR-002, GBR-003, GBR-004, GBR-005, GBR-006, GBR-008, GBR-009, GBR-010, GBR-011, GBR-015, GBR-030, GBR-076 | | |

## 1.7 UC-07 Book for a Relative

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | Book an appointment for a saved family member. The booking does not give the booker access to the relative's medical record. | | |
| Preconditions | The customer is authenticated. For a saved family member, UC-14 has been done, or the page was opened from "Đặt lịch" of the family member. | | |
| Normal Flow | 1. At step 1 of UC-06 the customer chooses "Người thân" and reads the notice that booking does not give access to the relative's record.<br>2. The customer chooses a family member from "Người thân đã lưu" (search by name). The name and date of birth of the examinee are filled in and cannot be edited.<br>3. The customer continues with steps 2 to 8 of UC-06. The appointment records the booker and the examinee (relationship "Người thân") separately. | | |
| Alternative Flows | A1 — The customer enters the examinee without a saved member, at step 2: The customer enters the name and, optionally, the date of birth. | | |
| Exception Flows | E1 — The saved family member was deleted, at step 2: DCMS shows "Người thân đã xóa hoặc không còn khả dụng. Hãy chọn lại người thân đã lưu." and the customer chooses again.<br>E2 — The date of birth is invalid or after today: DCMS displays MSG04.<br>E3 — The customer tries to open the clinical record of the relative from the booking: DCMS displays MSG21†.<br>E4 — Other exceptions follow UC-06. | | |
| Postconditions | 1. Success: the same as UC-06; the booker has no access to the clinical record of the examinee because of the booking.<br>2. Failure: no appointment is created. | | |
| Business Rule | GBR-030, GBR-031, GBR-075 | | |

## 1.8 UC-08 View My Appointments

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | List, filter and open the upcoming and the past appointments that the customer booked, including those booked for relatives (web and mobile). | | |
| Preconditions | The customer is authenticated. | | |
| Normal Flow | 1. The customer opens "Lịch hẹn của tôi".<br>2. The customer chooses "Sắp tới" or "Lịch sử" and may filter by status and by date range.<br>3. DCMS returns only the appointments booked by the account, with the permitted booking details.<br>4. The customer opens an appointment. DCMS shows its code, time, branch, service, dentist, booker, examinee, status and the history of changes. | | |
| Alternative Flows | A1 — No appointment matches, at step 3: DCMS displays MSG112† with the link to book a new appointment.<br>A2 — The appointment was made for a relative, at step 4: DCMS shows only the details of the booking, not the relative's clinical record. | | |
| Exception Flows | E1 — The date range is wrong ("Đến ngày" before "Từ ngày"), at step 2: DCMS shows "Đến ngày phải từ ngày bắt đầu trở đi." and does not search.<br>E2 — The account has no right on the appointment, at step 4: DCMS displays MSG06 or MSG102 and shows no private detail.<br>E3 — The session has expired: DCMS displays MSG07.<br>E4 — The appointment is no longer available: DCMS displays MSG113 and refreshes the list.<br>E5 — The service is unavailable: DCMS displays MSG144 with the button "Thử lại". | | |
| Postconditions | 1. The customer has seen only the appointments of the account.<br>2. Nothing is created or changed. | | |
| Business Rule | GBR-026, GBR-030, GBR-031, GBR-073, GBR-075 | | |

## 1.9 UC-09 Change or Cancel My Appointment

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | Reschedule or cancel a Confirmed appointment, or send a request to the clinic when the appointment is too close to be changed by the customer, and withdraw a pending request. | | |
| Preconditions | The customer is authenticated and created the booking (or has the right on it). The appointment is Confirmed and has not started or been checked in. | | |
| Normal Flow | 1. The customer opens the appointment (UC-08) and selects "Đổi lịch" or "Hủy lịch".<br>2. DCMS checks the right of the customer, the status of the appointment and that at least 12 hours remain.<br>3. For a change, the customer chooses a new dentist, date and slot (free slots are loaded) and enters a reason. For a cancellation, the customer enters a reason.<br>4. DCMS shows a review step. For a cancellation it shows MSG36†. The customer confirms.<br>5. DCMS checks again the appointment and the new slot, releases the old interval only when the new slot is accepted, and records the change in the history.<br>6. DCMS displays MSG35 (change) or MSG37 (cancellation) and notifies the dentist's calendar and the customer. | | |
| Alternative Flows | A1 — Less than 12 hours remain, at step 2: The buttons become "Yêu cầu đổi lịch" or "Yêu cầu hủy lịch". DCMS creates a support request for the staff (UC-22) and displays MSG33. The original appointment stays in effect.<br>A2 — A request is pending, at step 1: The customer sees "Yêu cầu đang chờ xử lý" and may withdraw it with "Rút yêu cầu". The original appointment stays.<br>A3 — The customer has no account: The customer contacts the clinic with the booking code (MSG32) and the staff act under UC-18 or UC-19. | | |
| Exception Flows | E1 — No right or the appointment is no longer eligible, at step 2 or 5: DCMS displays MSG06 or MSG31 and changes nothing.<br>E2 — The new slot is invalid or no longer free, at step 5: DCMS displays MSG34; the original time stays and the customer chooses another slot.<br>E3 — The appointment changed meanwhile, at step 5: DCMS displays MSG111 and asks the customer to reload.<br>E4 — The same action was already done: DCMS displays MSG124†.<br>E5 — The service is unavailable: DCMS displays MSG144; nothing is changed. | | |
| Postconditions | 1. Change succeeded: the new time is Confirmed, the old interval is released, the history is kept and a linked follow-up stays linked and Booked.<br>2. Cancellation succeeded: the appointment is Cancelled and the history is kept; an active linked follow-up goes back to "Needs booking".<br>3. Failure, abandonment or pending request: the original appointment stays in effect. | | |
| Business Rule | GBR-012, GBR-013, GBR-014, GBR-015, GBR-025, GBR-031, GBR-075, GBR-076, GBR-081 | | |

## 1.10 UC-10 View Customer Home

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | Show the nearest appointment and the shortcuts to the functions of the customer area. | | |
| Preconditions | The customer is authenticated. | | |
| Normal Flow | 1. The customer signs in (UC-01) or selects "Trang chủ".<br>2. DCMS loads the nearest upcoming appointment of the account and the latest one that has already started and was not cancelled.<br>3. DCMS shows the card "Lịch hẹn gần nhất" with their time (Vietnam time), current status and the button "Xem lịch hẹn", the card "Đặt lịch khám" and the "Truy cập nhanh" shortcuts (family members, appointments, prescriptions, record link, medical record).<br>4. The customer selects a card or a shortcut and DCMS opens that function. | | |
| Alternative Flows | A1 — No upcoming appointment, at step 2: DCMS shows "Bạn chưa có lịch hẹn sắp tới." with the button "Đặt lịch ngay". | | |
| Exception Flows | E1 — The appointment cannot be loaded, at step 2: DCMS displays MSG144 with a retry button; the shortcuts still work.<br>E2 — The session has expired: DCMS displays MSG07. | | |
| Postconditions | Nothing is created or changed. | | |
| Business Rule | GBR-026, GBR-030, GBR-075 | | |

## 1.11 UC-11 Link Account to Patient Record

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | Send a request to the clinic to link the customer account to the patient record of the account owner, follow its status and withdraw it. | | |
| Preconditions | The customer is authenticated and has no active self link. | | |
| Normal Flow | 1. The customer opens "Liên kết hồ sơ".<br>2. The customer enters the identifying information that the clinic needs (for example the patient code, if known, and information for matching).<br>3. DCMS validates the request and creates a pending request; it displays MSG18 (or MSG17 when the owner has not yet been checked in).<br>4. The clinic verifies the request in UC-28.<br>5. The customer sees the status of the request: waiting for the clinic, verified (MSG19), rejected or revoked (MSG20). When verified, the medical record can be seen (UC-12). | | |
| Alternative Flows | A1 — The customer withdraws a pending request, after step 3: DCMS marks it as withdrawn.<br>A2 — The request is rejected, after step 4: The customer sees the public note and may send a new request. | | |
| Exception Flows | E1 — A required field is missing or invalid: DCMS displays MSG02 or MSG04.<br>E2 — The account or the patient already has an active self link, at step 3: DCMS displays MSG142.<br>E3 — Ownership cannot be verified: DCMS displays MSG143 (the decision is taken by the staff).<br>E4 — The session has expired: DCMS displays MSG07.<br>E5 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. A request exists with its status; no record is visible before the clinic verifies it.<br>2. At most one active self link exists for an account. | | |
| Business Rule | GBR-072, GBR-073, GBR-074, GBR-075, GBR-081 | | |

## 1.12 UC-12 View My Medical Record

| Primary Actors | Patient | Secondary Actors | None |
|---|---|---|---|
| Description | See the own medical record that the clinic published: basic information, examinations and results, treatment and payments, images and prescriptions (web and mobile). | | |
| Preconditions | The customer is authenticated and has a Verified self link. | | |
| Normal Flow | 1. The customer opens "Hồ sơ bệnh án" (web) or the medical record screen (mobile).<br>2. DCMS checks the account, the self link and the permission of each content.<br>3. DCMS shows the header with the name and the patient code, and the tabs: "Thông tin cơ bản", "Khám", "Điều trị & Thanh toán", "Hình ảnh", "Đơn thuốc".<br>4. The customer opens the result of a visit (symptoms, diagnoses, conclusion, advice, return need, disposition, findings, investigation results).<br>5. The customer opens a published image, or a prescription. | | |
| Alternative Flows | A1 — A tab has no published content, at step 3: DCMS displays MSG101† (for example "Chưa có đơn thuốc"). | | |
| Exception Flows | E1 — The account has no Verified self link: DCMS shows "Hãy xác minh liên kết hồ sơ để xem bệnh án." (MSG102) and no data.<br>E2 — The link was revoked or the account locked, at any step: DCMS stops the reading at once with MSG102.<br>E3 — An image cannot be opened: DCMS shows "Không mở được hình ảnh." (MSG113).<br>E4 — The session has expired: DCMS displays MSG07.<br>E5 — The service is unavailable: DCMS displays MSG144 with "Thử lại". | | |
| Postconditions | 1. The customer has seen only completed and published content of own record.<br>2. Nothing is created or changed. | | |
| Business Rule | GBR-026, GBR-040, GBR-073, GBR-074, GBR-075 | | |

## 1.13 UC-13 View My Prescriptions

| Primary Actors | Patient | Secondary Actors | None |
|---|---|---|---|
| Description | List and open the prescriptions that a dentist has finalised for the customer (web and mobile). | | |
| Preconditions | The customer is authenticated and has a Verified self link. | | |
| Normal Flow | 1. The customer opens "Đơn thuốc của tôi".<br>2. DCMS lists the finalised prescriptions of the linked patient, newest first.<br>3. The customer opens a prescription and sees the date, the dentist, the diagnosis, each medicine (name, quantity, unit, dose, days, instructions) and the general advice. | | |
| Alternative Flows | A1 — There is no prescription, at step 2: DCMS shows "Chưa có đơn thuốc" (MSG101†). | | |
| Exception Flows | E1 — No Verified self link or the right was revoked: DCMS displays MSG102.<br>E2 — The prescription is no longer available: DCMS displays MSG113.<br>E3 — The session has expired: DCMS displays MSG07.<br>E4 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | The customer has seen finalised prescriptions only, as they were at finalisation. Nothing is changed. | | |
| Business Rule | GBR-048, GBR-050, GBR-074, GBR-075 | | |

## 1.14 UC-14 Manage Family Members

| Primary Actors | Patient (Guest) | Secondary Actors | None |
|---|---|---|---|
| Description | Add, edit, search and delete the family members saved in the account, to use them when booking (web and mobile). | | |
| Preconditions | The customer is authenticated. | | |
| Normal Flow | 1. The customer opens "Người thân của tôi" and sees the list (20 per page) with name, relationship, date of birth, gender and contact phone.<br>2. The customer searches by name, or selects "Thêm người thân".<br>3. The customer enters the name, the relationship (Bố, Mẹ, Vợ/Chồng, Con, Anh/Chị/Em, Ông/Bà, Khác), the date of birth (optional), the gender and the contact phone (a mobile number of 10 digits).<br>4. DCMS validates and saves the member.<br>5. To change a member the customer selects "Sửa", edits the fields and saves. To remove a member the customer selects "Xóa" and confirms "Bạn có muốn xóa người thân này khỏi danh sách không?".<br>6. The customer may select "Đặt lịch" (UC-07) or "Xem hồ sơ" (UC-15) for a member. | | |
| Alternative Flows | A1 — No member matches, at step 1: DCMS shows "Chưa có người thân phù hợp. Bạn có thể thêm người thân mới.".<br>A2 — The member was changed elsewhere, at step 5: The customer selects "Xem phiên bản mới" and chooses "Dùng thông tin mới" or "Giữ bản nhập để lưu lại". | | |
| Exception Flows | E1 — A required field is empty or invalid, at step 4: DCMS shows MSG02, MSG03 or MSG04 under the field (name, relationship, gender and phone are required; the date of birth cannot be after today).<br>E2 — The member changed concurrently, at step 4 or 5: DCMS displays MSG111.<br>E3 — The same action is sent again: DCMS displays MSG124† and does not create a duplicate.<br>E4 — The session has expired: DCMS displays MSG07.<br>E5 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Success: the family member is saved, changed or removed from the list. Appointments already booked keep their recorded examinee.<br>2. Failure: nothing is changed. | | |
| Business Rule | GBR-030, GBR-075, GBR-081 | | |

## 1.15 UC-15 View Family Member Record

| Primary Actors | Patient | Secondary Actors | None |
|---|---|---|---|
| Description | See the appointments that the customer booked for a family member and, when the clinic has granted access, the record of that person within the granted scopes. The customer can request or end the access. | | |
| Preconditions | The customer is authenticated and the family member is saved in the account. The family member does not need an account. | | |
| Normal Flow | 1. The customer selects "Xem hồ sơ" on a family member (UC-14).<br>2. DCMS shows the access status (Chưa có hồ sơ liên kết, Chờ phòng khám xác minh, Đã được cấp quyền, Bị từ chối, Đã thu hồi, Đã rút yêu cầu, Đã hết hạn) and the tab "Lịch đặt hộ".<br>3. If the family member has an existing record at the clinic, the customer opens "Liên kết hồ sơ đã có tại phòng khám", enters the patient code (if known) and the matching information, and selects "Yêu cầu quyền xem hồ sơ". DCMS creates a pending request.<br>4. The clinic verifies the request in UC-28 and grants a scope (examinations, treatment, prescriptions, documents) with an expiry date.<br>5. DCMS shows the record header and the tabs of the granted scopes, 10 items per page. The customer may download a published document.<br>6. The customer may select "Rút yêu cầu" (pending) or "Chấm dứt quyền truy cập" (granted) after confirming. | | |
| Alternative Flows | A1 — The family member was received at the clinic after an appointment booked by the customer, before step 3: The record is shown by default within the granted scope, with no separate request. | | |
| Exception Flows | E1 — The access expired or was revoked, at any step: DCMS hides the content, loads the status again and displays MSG102.<br>E2 — A scope is not granted: DCMS shows no tab for it; a direct request returns MSG06.<br>E3 — A document cannot be downloaded: DCMS shows "Không thể tải tài liệu. Quyền truy cập có thể đã thay đổi, vui lòng tải lại." (MSG113).<br>E4 — A conflicting request already exists: DCMS displays MSG142.<br>E5 — The session has expired: DCMS displays MSG07.<br>E6 — The service is unavailable: DCMS displays MSG144 with "Thử lại". | | |
| Postconditions | 1. The customer has seen only the scopes granted by the clinic and valid at that time.<br>2. A booking for a relative alone does not give access to the clinical record. | | |
| Business Rule | GBR-030, GBR-031, GBR-072, GBR-073, GBR-074, GBR-075 | | |


## 1.16 UC-16 Manage Appointment Calendar

| Primary Actors | Clinic Assistant; Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | See the appointments of a branch in a calendar by day or by week, and open the detail of an appointment. | | |
| Preconditions | The user is authenticated and has the permission to read appointments. A dentist whose account is limited to its own schedule sees only their own column. | | |
| Normal Flow | 1. The user opens "Lịch hẹn".<br>2. The user chooses the branch (when the user can pick a branch), the day or week, and may filter by dentist and status.<br>3. DCMS shows the appointments in the calendar within the scope of the user, with the booker, the examinee, the time and the status.<br>4. The user moves to today, to the previous or to the next day or week.<br>5. The user opens an appointment to see its detail: time, branch, service, dentist, booker, examinee, status, reason and history, with the actions allowed by status and permission (UC-18, UC-19, UC-20). | | |
| Alternative Flows | A1 — No appointment matches, at step 3: DCMS displays MSG112†.<br>A2 — The calendar changes while it is open: DCMS refreshes it in real time, or the user selects "Tải lại". | | |
| Exception Flows | E1 — The user has no permission, at step 3 or 5: DCMS displays MSG06 and shows no appointment outside the scope.<br>E2 — The appointment is no longer available, at step 5: DCMS displays MSG113 and refreshes the calendar.<br>E3 — The session has expired: DCMS displays MSG07.<br>E4 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | The user has seen only the appointments in the scope of the role. Nothing is created or changed. | | |
| Business Rule | GBR-026, GBR-075 | | |

## 1.17 UC-17 Book Appointment for a Patient

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Book a Confirmed appointment on behalf of a customer, a patient or a relative who contacts the clinic by phone, in person or as a walk-in. | | |
| Preconditions | The assistant is authenticated and has the permission to create appointments. Appointment services and a published working schedule exist. | | |
| Normal Flow | 1. The assistant selects "Đặt lịch" in the calendar (or "Xếp lịch khách vãng lai" in the reception list).<br>2. The assistant chooses or enters the patient or customer, the examinee (Self or Relative) and the booker's contact.<br>3. The assistant chooses the branch, the dentist (or any dentist with a free chair), the service, the date and a free slot; DCMS takes the duration from the service.<br>4. DCMS shows the review step "Xem lại trước khi xác nhận".<br>5. The assistant confirms.<br>6. DCMS checks again the schedule, the full duration, the conflicts of the dentist and of the identified examinee, and the chair capacity.<br>7. DCMS creates a Confirmed appointment and displays MSG29. | | |
| Alternative Flows | A1 — The start is within 60 minutes, at step 3: The assistant enters the exception reason; DCMS still performs all the checks of step 6.<br>A2 — Walk-in without an appointment, at step 3: The assistant chooses a valid slot of today; DCMS records the source as walk-in. For a slot of a later day DCMS displays MSG41† and no visit or queue entry is created.<br>A3 — A possible duplicate customer, at step 2: DCMS warns; the assistant compares the identities and does not merge records.<br>A4 — Replacement booking: After a cancelled or no-show appointment the assistant uses "Đặt lịch thay thế"; the new appointment is linked to the old one and its duration can be entered, up to the time left in the missed slot. | | |
| Exception Flows | E1 — The attendee information is missing, at step 2: DCMS displays MSG23.<br>E2 — No slot matches, at step 3: DCMS displays MSG22 (MSG40 for a walk-in with no slot today).<br>E3 — The slot is no longer free, not enough consecutive time, a conflict or no chair, at step 6: DCMS displays MSG25, MSG26, MSG27 or MSG28 and creates nothing.<br>E4 — The data changed meanwhile: DCMS displays MSG111.<br>E5 — The user has no permission: DCMS displays MSG06.<br>E6 — The same request is sent again: DCMS displays MSG30 and returns the existing result.<br>E7 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Success: one Confirmed appointment exists with its full interval reserved; the dentist's calendar and the customer are notified. No Patient profile, visit or queue entry is created.<br>2. Failure: no appointment is created. | | |
| Business Rule | GBR-002, GBR-003, GBR-004, GBR-005, GBR-006, GBR-008, GBR-009, GBR-010, GBR-011, GBR-015, GBR-030, GBR-032, GBR-076 | | |

## 1.18 UC-18 Edit Appointment

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Move a Confirmed appointment to another date, dentist or slot on behalf of the customer or because of a change in the clinic. | | |
| Preconditions | The assistant is authenticated and has the permission to edit appointments. The appointment is Confirmed and has not started or been checked in. The assistant has verified the requester. | | |
| Normal Flow | 1. The assistant opens the appointment and selects "Chỉnh sửa lịch".<br>2. The assistant chooses the new date, dentist and slot; DCMS shows the free slots.<br>3. The assistant enters the reason for the change (required, up to 500 characters).<br>4. The assistant selects "Lưu thay đổi".<br>5. DCMS checks again the appointment version, the new slot, the dentist and examinee conflicts and the chair capacity, and releases the old interval only when the new one is accepted.<br>6. DCMS saves the change with the reason, the actor and the time, displays MSG35 and notifies the customer. | | |
| Alternative Flows | A1 — The change comes from a support request, before step 1: The assistant handles it in UC-22.<br>A2 — The assistant abandons the drawer: Nothing is changed. | | |
| Exception Flows | E1 — The reason or the slot is missing: DCMS displays MSG02 and does not save.<br>E2 — The new slot is invalid or no longer free, at step 5: DCMS displays MSG34; the original time stays.<br>E3 — The appointment changed meanwhile, at step 5: DCMS displays MSG111 and the button "Tải lại phiên bản" loads the latest data.<br>E4 — The appointment is not Confirmed or has been checked in: DCMS displays MSG42.<br>E5 — No permission: DCMS displays MSG06.<br>E6 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Success: the new time is Confirmed, the old interval is released, the history and the reason are kept, and a linked follow-up stays Booked.<br>2. Failure: the original appointment stays in effect. | | |
| Business Rule | GBR-004, GBR-005, GBR-006, GBR-008, GBR-013, GBR-015, GBR-076, GBR-081 | | |

## 1.19 UC-19 Cancel Appointment / Mark No-show

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Cancel a Confirmed appointment with a reason, or record that the patient did not come and undo that record when it was a mistake, or move the missed appointment to another time. | | |
| Preconditions | The assistant is authenticated and has the permission. For a cancellation the appointment is Confirmed and has not started. For a no-show the appointment is still Confirmed, has no visit, and its start time has already passed. | | |
| Normal Flow | 1. The assistant opens the appointment and selects "Hủy lịch" or "Khách không tới".<br>2. DCMS shows a dialog and, for a cancellation, MSG36†.<br>3. The assistant enters the reason and confirms.<br>4. DCMS checks again the status and the version, saves the new status (Cancelled or No-show) with the actor, the time and the reason, and releases the interval.<br>5. DCMS displays MSG37 for a cancellation, notifies the customer and updates the calendars. For a no-show DCMS creates one care task to call the customer back after that clinic day is over (UC-29). | | |
| Alternative Flows | A1 — The appointment stays Confirmed after its start time: DCMS does not change it by itself; the staff must record the no-show. Until then DCMS labels it "Trễ hẹn", then "Chờ xử lý" after its end time ("Đã đến giờ hẹn", then "Chờ phòng khám xác nhận" for the customer), and lists the ended ones in the reception tab "Quá giờ chưa xử lý".<br>A2 — A linked follow-up exists: It goes back to "Needs booking" (MSG61†).<br>A3 — The no-show was recorded by mistake, on the same clinic day: The assistant selects "Hoàn tác không tới", enters the reason and confirms. DCMS sets the appointment back to Confirmed (no callback task exists yet on that day), keeps both records in the history and notifies the customer.<br>A4 — The customer arrives late or wants another time on the same day (a late appointment of today, or a no-show recorded today): The assistant selects "Đổi lịch", chooses a free future time (no 60-minute lead time), enters the reason and saves. DCMS confirms the appointment at the new time, cancels any open callback task, keeps the history and notifies the customer. | | |
| Exception Flows | E1 — The reason is missing: DCMS displays MSG02.<br>E2 — The appointment is no longer eligible (already Cancelled, No-show, checked in, or a no-show requested before the start time): DCMS displays MSG31 and changes nothing.<br>E3 — The appointment changed meanwhile: DCMS displays MSG111.<br>E4 — No permission: DCMS displays MSG06.<br>E5 — The service is unavailable: DCMS displays MSG144.<br>E6 — An undo after the day of the record, when another active appointment overlaps the same chair, dentist or patient, or when the treatment sheet was removed: DCMS explains the reason and changes nothing.<br>E7 — A move of a missed appointment when the person already holds another open self-booked examination, or when the treatment sheet was removed: DCMS explains the reason and changes nothing. | | |
| Postconditions | 1. Cancelled: the status, the reason and the history are kept; the interval is available again.<br>2. No-show: the old appointment is preserved; from the next clinic day one callback task exists, unless the no-show was undone or the appointment moved. A later rebooking creates a new linked appointment (UC-17, replacement).<br>3. Failure: the appointment is unchanged. | | |
| Business Rule | GBR-014, GBR-015, GBR-018, GBR-020, GBR-025, GBR-075, GBR-081 | | |

## 1.20 UC-20 Confirm Attendance

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Record whether the patient will attend an appointment (confirmed or withdrawn), for example after a reminder or a call. | | |
| Preconditions | The assistant is authenticated and has the permission. The appointment is Confirmed. | | |
| Normal Flow | 1. The assistant opens the appointment or a "confirm attendance" request in the support inbox.<br>2. The assistant records the answer: will attend, or will not attend.<br>3. DCMS saves the answer on the appointment, increases its version and publishes the change. | | |
| Alternative Flows | A1 — The confirmation comes from a customer request, at step 1: The assistant closes the request in UC-22 after recording it. | | |
| Exception Flows | E1 — The appointment is not Confirmed: DCMS displays MSG42.<br>E2 — The appointment changed meanwhile: DCMS displays MSG111.<br>E3 — No permission: DCMS displays MSG06.<br>E4 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | The confirmation is recorded; it does not change the time, the dentist or the status of the appointment. | | |
| Business Rule | GBR-026, GBR-075, GBR-081 | | |

## 1.21 UC-21 Book Next Visit

| Primary Actors | Clinic Assistant; Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Book the next visit of a patient from the patient profile or from a visit, using the next-visit candidates and the follow-up instruction of the dentist. | | |
| Preconditions | The user is authenticated and has the permission to create appointments. The patient exists. A follow-up instruction may exist. | | |
| Normal Flow | 1. The user opens the "next visit" panel of the patient.<br>2. DCMS shows the candidates (for example a follow-up due date, the duration and the preferred dentist).<br>3. The user chooses a candidate and the drawer of the next visit opens with the patient, the dentist and the duration already filled in.<br>4. The user chooses a free slot and confirms.<br>5. DCMS checks again the slot as in UC-17, creates the Confirmed appointment and displays MSG59† (MSG29 when there is no follow-up).<br>6. A linked follow-up instruction becomes Booked. | | |
| Alternative Flows | A1 — No slot matches the window, at step 4: DCMS displays MSG58†; the user chooses another date or dentist. | | |
| Exception Flows | E1 — The follow-up already has an active appointment: DCMS displays MSG127.<br>E2 — The slot is no longer free or conflicts, at step 5: DCMS displays MSG25, MSG26, MSG27 or MSG28.<br>E3 — The data changed meanwhile: DCMS displays MSG111.<br>E4 — No permission: DCMS displays MSG06.<br>E5 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Success: one new Confirmed appointment exists and a linked follow-up is Booked; at most one active appointment per follow-up.<br>2. Failure: nothing is created and the follow-up status is unchanged. | | |
| Business Rule | GBR-022, GBR-023, GBR-025, GBR-076 | | |

## 1.22 UC-22 Handle Support Request

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Review the requests that customers send about their appointments (change, cancel, confirm attendance, question), verify the requester, apply the change, and resolve or reject the request (web and mobile). | | |
| Preconditions | The assistant is authenticated and has the permission to handle support requests. A request exists in the support inbox. | | |
| Normal Flow | 1. The assistant opens "Hộp thư hỗ trợ" and filters the requests; the list refreshes itself when a request changes.<br>2. The assistant opens a request and reads the appointment, the reason and the proposed slot of the customer.<br>3. The assistant selects "Xác minh người yêu cầu" after checking the requester against the booking data.<br>4. For a change or cancel request the assistant chooses the reason of the change and selects "Đổi lịch" or "Huỷ lịch"; DCMS applies it as in UC-18 or UC-19.<br>5. The assistant selects "Xử lý yêu cầu" and confirms; the request is closed and the customer is notified. | | |
| Alternative Flows | A1 — The request cannot be accepted, at step 5: The assistant selects "Từ chối", enters an internal reason and confirms; the original appointment stays and the customer is notified.<br>A2 — The request is withdrawn by the customer: DCMS closes it and shows its status. | | |
| Exception Flows | E1 — The requester is not verified, at step 4: DCMS does not apply the change until the requester is verified.<br>E2 — The change or cancel request is not applied before closing, at step 5: DCMS does not close the request.<br>E3 — The new slot is invalid, at step 4: DCMS displays MSG34.<br>E4 — The reply cannot be saved: DCMS displays MSG132†.<br>E5 — The request changed meanwhile: DCMS displays MSG111.<br>E6 — No permission: DCMS displays MSG06.<br>E7 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Resolved: the appointment reflects the applied change and the request is closed with the actor and the time.<br>2. Rejected: the appointment is unchanged and the reason is kept.<br>3. Failure: the request stays open. | | |
| Business Rule | GBR-012, GBR-013, GBR-014, GBR-031, GBR-075, GBR-080, GBR-081 | | |

## 1.23 UC-23 Review Schedule Impact

| Primary Actors | Dentist; Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | See on the mobile app the confirmed appointments that a proposed working-schedule change would affect, so that they can be handled before the change is published. | | |
| Preconditions | The user is authenticated on the staff part of the mobile app and has the permission to read appointments. A schedule change with affected appointments exists (UC-41). | | |
| Normal Flow | 1. The user opens "Schedule impact" in the staff part of the mobile app.<br>2. DCMS lists the affected confirmed appointments with the date, the time and the examinee.<br>3. The user opens an appointment to see its detail and handles it (UC-18, UC-19 or UC-22). | | |
| Alternative Flows | A1 — No appointment is affected: DCMS shows an empty list. | | |
| Exception Flows | E1 — No permission: DCMS displays MSG06.<br>E2 — The session has expired: DCMS displays MSG07.<br>E3 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | Nothing is changed by this use case. | | |
| Business Rule | GBR-007, GBR-075 | | |

## 1.24 UC-24 Receive Patient (Check-in)

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Verify the actual attendee, match or create the correct patient record, record the arrival and create exactly one waiting visit for an eligible appointment. | | |
| Preconditions | The assistant is authenticated and has the permission to check patients in. The patient is at the reception. If no Confirmed appointment exists, one is arranged first in UC-17 (walk-in). | | |
| Normal Flow | 1. The assistant opens "Tiếp nhận và hàng chờ", finds the Confirmed appointment of the day and selects it ("Tiếp nhận bệnh nhân").<br>2. The assistant checks that the person present is the attendee recorded on the appointment. The attendee may differ from the booker.<br>3. DCMS checks whether the appointment is linked to a patient record. If linked, the assistant checks that it is the right person. If not, the assistant searches the patient records and reviews the possible duplicates, then selects the existing record or creates a new one (UC-26).<br>4. The assistant chooses the reason for coming (Khám mới, Điều trị tiếp, Kiểm tra sau điều trị / bảo hành, Tái khám định kỳ / vấn đề khác).<br>5. The assistant selects "Xác nhận Check-in".<br>6. DCMS checks again the appointment status and links, changes the appointment to Checked-in, records the arrival time, creates exactly one visit and puts it in the queue, and displays MSG45†. | | |
| Alternative Flows | A1 — The person present differs from the recorded attendee, at step 2: DCMS displays MSG43; the assistant stops until the booking is corrected or a booking for the right person is made.<br>A2 — The linked patient record does not match, at step 3: The assistant stops and investigates; DCMS does not use that record.<br>A3 — The patient arrived earlier than the moment of entry: a user with the permission to backdate may enter the arrival time (not in the future, same clinic day) and must give a reason; without the permission DCMS displays MSG06, without the reason MSG02, and an arrival time in the future or on another day MSG04.<br>A4 — The assistant leaves with unsaved data: DCMS asks "Bỏ thông tin tiếp nhận chưa lưu?". | | |
| Exception Flows | E1 — The appointment is not Confirmed, is not on the current clinic day, or its time has already ended, at step 6: DCMS displays MSG42 (MSG47 when it is No-show). No visit is created.<br>E2 — The appointment is already checked in: DCMS displays MSG114 and returns the existing visit.<br>E3 — A possible duplicate record is found: DCMS displays MSG44.<br>E4 — The appointment or the link changed meanwhile: DCMS displays MSG111.<br>E5 — No permission: DCMS displays MSG06.<br>E6 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Success: the attendee is associated with the right patient record; the appointment is Checked-in with the arrival time; exactly one waiting visit exists.<br>2. Already checked in: the existing visit stays; no duplicate is created.<br>3. Failure: no visit or queue entry is created and no account is linked to a record. | | |
| Business Rule | GBR-016, GBR-017, GBR-021, GBR-030, GBR-032, GBR-033, GBR-034, GBR-035, GBR-081 | | |

## 1.25 UC-25 Manage Waiting Queue

| Primary Actors | Clinic Assistant; Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Follow the patients who are waiting, being examined or done during the day, and open a visit. | | |
| Preconditions | The user is authenticated and has the permission to read the reception. | | |
| Normal Flow | 1. The user opens "Tiếp nhận và hàng chờ" ("Tiếp nhận hôm nay" or a chosen date).<br>2. DCMS shows the appointments of the day and the waiting queue with the patient, the appointment time, the arrival time and the state of the visit.<br>3. The user filters by status, and reloads the list.<br>4. The user opens a visit: a dentist continues with UC-30; the assistant sees the state and the details. | | |
| Alternative Flows | A1 — A patient arrives late, at step 2: The assistant handles the late arrival and the queue order; DCMS does not reopen an elapsed slot automatically.<br>A2 — Past appointments were not processed, at step 2: The user opens the tab "Quá giờ chưa xử lý", which lists the Confirmed appointments of every date whose end time has passed; the assistant records the no-show (UC-19). | | |
| Exception Flows | E1 — The queue is empty: DCMS displays MSG01.<br>E2 — No permission: DCMS displays MSG06.<br>E3 — The visit is no longer available: DCMS displays MSG113.<br>E4 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | The queue shows the current state; nothing is created by this use case. | | |
| Business Rule | GBR-021, GBR-035, GBR-075 | | |

## 1.26 UC-26 Manage Patient Records

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Search, create, correct, archive and restore patient records, with the check of duplicates and the patient code sequence. | | |
| Preconditions | The assistant is authenticated and has the permission to read and manage patients. | | |
| Normal Flow | 1. The assistant opens "Bệnh nhân" and searches by patient code, name or phone number, with filters.<br>2. DCMS shows the matching patients with only the identifying information needed to tell them apart.<br>3. The assistant opens a patient (UC-27) or selects "Thêm hồ sơ".<br>4. The assistant enters the identification, the contact, the date the record is opened and, when needed, the guardian.<br>5. DCMS validates the data and shows the possible duplicates.<br>6. The assistant reviews and saves. DCMS checks the version and the permission, generates the patient code from the configured sequence for a new record, saves the changes with the actor, the time and the changed content, and displays MSG116.<br>7. To stop using a record, or to restore it, the assistant selects "Ngừng sử dụng" or "Khôi phục hồ sơ", enters a reason and confirms. | | |
| Alternative Flows | A1 — No match, at step 2: DCMS displays MSG115; a missing record is not proof that the person has never visited.<br>A2 — A shared phone number, at step 5: The assistant keeps the identities separate. Records are never merged automatically.<br>A3 — Clinical information needs correction, at step 4: The correction is done by the dentist, not here. | | |
| Exception Flows | E1 — A required field is missing or invalid, at step 5: DCMS displays MSG02 or MSG04.<br>E2 — A possible duplicate record, at step 5: DCMS displays MSG44.<br>E3 — The record changed since it was opened, at step 6: DCMS displays MSG111.<br>E4 — No permission: DCMS displays MSG06.<br>E5 — The service is unavailable: DCMS displays MSG144; the saved data stays unchanged. | | |
| Postconditions | 1. Success: the administrative changes and their audit information are saved; an archived record stays in the history and can be restored.<br>2. Failure: nothing is committed.<br>3. The use case does not create, link or merge an account or change a verified link. | | |
| Business Rule | GBR-033, GBR-034, GBR-038, GBR-072, GBR-075, GBR-081 | | |

## 1.27 UC-27 View Patient Profile

| Primary Actors | Clinic Assistant; Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Show one patient in tabs: basic information, examinations, treatment and payments, images and prescriptions, within the permissions of the user. | | |
| Preconditions | The user is authenticated and has the permission to read the patient. | | |
| Normal Flow | 1. The user selects a patient from "Bệnh nhân".<br>2. DCMS checks the permission for each tab and shows only the permitted ones: "Thông tin cơ bản", "Khám", "Điều trị & Thanh toán", "Hình ảnh", "Đơn thuốc".<br>3. The user reads the tabs. In "Thông tin cơ bản" the user may open the history of changes.<br>4. A dentist continues with UC-30 (new examination), UC-35 (treatment sheet), UC-39 (prescription) or UC-32 (images). | | |
| Alternative Flows | A1 — The patient record is archived: DCMS shows it as stopped and offers "Khôi phục hồ sơ" to a user with the permission. | | |
| Exception Flows | E1 — No permission for a tab or for the patient: DCMS displays MSG06 and discloses no content.<br>E2 — The patient is no longer available: DCMS displays MSG113.<br>E3 — The session has expired: DCMS displays MSG07.<br>E4 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | Only the permitted information has been shown. Nothing is created or changed. | | |
| Business Rule | GBR-026, GBR-033, GBR-034, GBR-075 | | |

## 1.28 UC-28 Verify Record Link

| Primary Actors | Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Check a request that links a customer account to a patient record (own record or the record of a relative), compare it with the patient record, and verify, reject or revoke it. | | |
| Preconditions | The assistant is authenticated and has the permission to read and decide link requests. A request exists (UC-11 or UC-15). | | |
| Normal Flow | 1. The assistant opens "Xác minh liên kết hồ sơ" and filters the queue (default "Chờ xác minh").<br>2. The assistant opens a request in the review drawer and compares the account with the declared data and the candidate patient record.<br>3. The assistant verifies the identity separately from the contact channel and chooses the patient record.<br>4. For a link to the own record the assistant selects "Xác minh" and confirms "Xác nhận đúng hồ sơ tự thân?". For a relative the assistant selects "Cấp quyền xem hồ sơ" and chooses the scopes and the expiry date.<br>5. DCMS checks again the version and that the account has no conflicting link, saves the decision and displays MSG19.<br>6. DCMS notifies the customer. | | |
| Alternative Flows | A1 — The request cannot be accepted, at step 4: The assistant selects "Từ chối", enters a reason and confirms; DCMS displays MSG20.<br>A2 — A verified link or a granted access must end: The assistant selects "Thu hồi liên kết" or "Thu hồi quyền" with a reason; DCMS blocks new reads at once (MSG20). | | |
| Exception Flows | E1 — The record does not match the declared data: "Xác minh" and "Cấp quyền" are disabled; the assistant rejects or asks the customer to complete the information (MSG143).<br>E2 — The account or the patient already has an active self link: DCMS displays MSG142.<br>E3 — The request changed meanwhile: DCMS displays MSG111.<br>E4 — No permission: DCMS displays MSG06.<br>E5 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Verified: at most one active self link exists for the account; the customer may read the record within the granted scope.<br>2. Rejected or revoked: no new private read is allowed; pending file requests are cancelled; the reason is kept internally.<br>3. A link is never verified by the contact verification alone. | | |
| Business Rule | GBR-072, GBR-073, GBR-074, GBR-075, GBR-081 | | |

## 1.29 UC-29 Manage Customer Care

| Primary Actors | Clinic Assistant; Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | Create, edit and follow the care tasks of customers, such as callbacks after a no-show, reminders and follow-up calls, with notes and the result of each contact. | | |
| Preconditions | The user is authenticated and has the permission to read or manage care tasks. | | |
| Normal Flow | 1. The user opens "Chăm sóc khách hàng" and filters the tasks by period, owner, type, status and customer.<br>2. The user selects "TẠO MỚI", enters the customer, the type, the owner, the due date and the content, and saves. DCMS saves the task as "Mở".<br>3. The user opens a task, reads it and records a contact (the outcome and a note) with "Lưu contact". The record does not send a message or book an appointment.<br>4. The user changes the status (Mở, Đang xử lý, Đã xong, Đã hủy) with "Sửa" or "Đánh dấu đã xong".<br>5. The user may export the filtered list to Excel. | | |
| Alternative Flows | A1 — A task is created automatically for a no-show (UC-19) on the next clinic day; a customer without a patient record is shown as "Chưa có hồ sơ": The assistant handles it from the list.<br>A2 — The customer wants to book, at step 3: The assistant uses UC-17 or UC-21. | | |
| Exception Flows | E1 — A required field is missing, at step 2: DCMS displays MSG02.<br>E2 — No contact information is usable: DCMS displays MSG131†.<br>E3 — The task changed meanwhile: DCMS displays MSG111.<br>E4 — No permission: DCMS displays MSG06.<br>E5 — The service is unavailable: DCMS displays MSG144.<br>E6 — The user closes the form with typed content: DCMS asks "Bỏ nội dung đang nhập?". | | |
| Postconditions | 1. Success: the task, the contact records and the status are saved with the actor and the time.<br>2. A no-show creates only one task; reprocessing does not create a duplicate. | | |
| Business Rule | GBR-018, GBR-019, GBR-020, GBR-024, GBR-075 | | |

## 1.30 UC-30 Conduct Examination

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Start a checked-in visit, record the reason, the symptoms, the odontogram findings and the diagnoses as a draft, confirm the examination sheet ("Hoàn tất khám"), and correct it afterwards with a reason. | | |
| Preconditions | The dentist is authenticated, has the permission to write examinations and is allowed on the visit. The visit is waiting (Queued) and its appointment is Checked-in. | | |
| Normal Flow | 1. The dentist opens the visit from the queue or the patient profile; DCMS shows the header (patient, appointment code, dentist, branch, arrival time, purpose) and the tabs "Khám & sơ đồ răng", "Cận lâm sàng", "Ảnh & tài liệu", "Tổng kết khám", "Lịch sử khám".<br>2. The dentist selects "Bắt đầu khám"; the visit and the appointment become In progress.<br>3. The dentist reviews the health history, then records the reason and symptoms (up to 4,000 characters), the findings on the odontogram (FDI numbering, up to 100 findings) and the notes (up to 8,000 characters).<br>4. The dentist selects "Lưu nháp"; DCMS saves a new draft version of the examination.<br>5. In the tab "Tổng kết khám" the dentist selects the diagnoses from the diagnosis catalogue, and completes the conclusion, the advice and the next step (UC-33).<br>6. The dentist selects "Hoàn tất khám"; DCMS checks that the symptoms, the findings (or the diagnosis "Chưa phát hiện bất thường"), the diagnoses, the advice and the disposition are present, and confirms the sheet. | | |
| Alternative Flows | A1 — A confirmed sheet needs a correction: The dentist selects "Đính chính kết quả", changes the content and enters the reason (up to 1,000 characters); DCMS saves it as a new version and keeps the old one (UC-34). The dentist can cancel with "Hủy đính chính".<br>A2 — The visit belongs to a treatment: DCMS refuses to create an examination sheet and tells the dentist to use the sessions of the treatment sheet (UC-35).<br>A3 — Required content is missing at step 6: DCMS lists each missing item as a link that moves the focus to the field.<br>A4 — The dentist needs a prescription for medicine bought outside the clinic: after the sheet is confirmed the dentist opens "Đơn thuốc mua ngoài" (UC-39). | | |
| Exception Flows | E1 — The visit changed meanwhile (another save): DCMS displays MSG111; the dentist reloads and reviews.<br>E2 — The visit or the sheet is not in the right state (not Queued when starting, not In progress or already confirmed when saving a draft, not confirmed when amending): DCMS displays MSG42.<br>E3 — The reason of an amendment is empty or longer than 1,000 characters: DCMS displays MSG04 on "Lý do đính chính".<br>E4 — A field is longer than allowed or in the wrong format: DCMS displays MSG04; a required field is empty: MSG02.<br>E5 — A selected diagnosis is no longer active: DCMS displays MSG202; it changed in the catalogue: MSG111; it does not exist: MSG113.<br>E6 — No permission on the visit: DCMS displays MSG06.<br>E7 — The user leaves with unsaved changes: the page asks for confirmation. | | |
| Postconditions | 1. Draft saved: the content is kept as a version of the visit; the visit stays In progress.<br>2. Sheet confirmed: the examination is locked from normal edits; only an amendment with a reason can change it, and each version is kept.<br>3. Failure: nothing newly entered is committed; earlier saved content stays. | | |
| Business Rule | GBR-036, GBR-037, GBR-038, GBR-070, GBR-075, GBR-081 | | |

## 1.31 UC-31 Order Investigations

| Primary Actors | Dentist | Secondary Actors | Clinic Assistant |
|---|---|---|---|
| Description | Order an investigation for the visit (for example a panoramic X-ray), record its result, read it and, when needed, cancel the order. | | |
| Preconditions | The visit is In progress (UC-30). The dentist is the dentist of the visit and has the permission to manage examinations; the assistant may record results when the permission allows. | | |
| Normal Flow | 1. In the tab "Cận lâm sàng" the dentist creates an order: the kind and name of the investigation, the region, the reason, and whether it is required for the diagnosis.<br>2. DCMS saves the order for the visit with the state "Ordered" (at most 100 orders per visit).<br>3. When the result arrives, the dentist or the assistant records the result text (up to 4,000 characters) and the files; the files must be ready after the file check. The state becomes "ResultAvailable".<br>4. The dentist reads the result, writes the interpretation (up to 4,000 characters) and confirms; the order becomes "Reviewed". At least one ready file or a result text must exist.<br>5. The reviewed result shows in the examination of the visit. | | |
| Alternative Flows | A1 — The investigation is no longer needed (state Ordered or ResultAvailable): The dentist cancels the order with a reason (up to 1,000 characters); the order becomes "Cancelled" and stays in the history.<br>A2 — The result is a file: it is added as in UC-32 with the link to the order. | | |
| Exception Flows | E1 — A field is empty, too long or in the wrong format, or the reason of a cancellation is empty or longer than 1,000 characters: DCMS displays MSG04.<br>E2 — The visit is not In progress, or the order is not in the state the action needs (for example reviewing an order that has no result): DCMS displays MSG42.<br>E3 — The visit or the order changed meanwhile: DCMS displays MSG111.<br>E4 — The visit or the order does not belong to the patient or no longer exists: DCMS displays MSG113.<br>E5 — The user is not the dentist of the visit or has no permission: DCMS displays MSG06. | | |
| Postconditions | 1. The order, its result, its interpretation and any cancellation are kept with the actor and the time.<br>2. An order required for the diagnosis that is neither reviewed nor cancelled prevents the visit from being completed (UC-33). | | |
| Business Rule | GBR-037, GBR-039, GBR-040, GBR-075, GBR-081 | | |

## 1.32 UC-32 Manage Examination Documents

| Primary Actors | Dentist; Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Upload, list, open and mark as reviewed the images and documents of a patient, linked to a visit or to a treatment sheet. | | |
| Preconditions | The user is authenticated and has the permission to read clinical documents (and to upload for the upload action). The patient exists and the user has access to the branch of the visit or of the treatment sheet. | | |
| Normal Flow | 1. The user opens the documents of a patient (tab "Ảnh & tài liệu" of a visit, or the documents of the profile) and filters by visit, treatment sheet, plan, type (X-quang, ảnh trong miệng, kết quả xét nghiệm, phiếu đồng ý, giấy chuyển, khác) and dates; the list shows 20, 50 or 100 rows per page and can be limited to images.<br>2. The user selects the upload, chooses up to 10 files (JPEG, PNG or PDF, each at most 20 MB), the type, the source, the source date (not in the future) and a description (up to 1,000 characters), and the context (a visit, or a treatment sheet).<br>3. DCMS checks the permission, the file signature and size, the link to the right patient and visit, and the state of the visit; each file is stored and checked by the file scan, and a file becomes "Ready" only when it passes.<br>4. The user opens a document to see its detail, image or PDF content and history.<br>5. The dentist of the visit can mark a document as reviewed ("review"); DCMS records who and when. | | |
| Alternative Flows | A1 — A result of an investigation: the file is uploaded with the link to the order (UC-31) and the order must still be in the state Ordered.<br>A2 — Some files are invalid: DCMS reports the error of each file and keeps the valid ones. | | |
| Exception Flows | E1 — A field is invalid (type unknown, source date in the future or before 1900, description longer than 1,000 characters, page size not 20, 50 or 100, no target given): DCMS displays MSG04.<br>E2 — A visit is given but is not Queued or In progress, or the order is not Ordered: DCMS displays MSG42.<br>E3 — The patient, the visit, the sheet or the document does not exist or does not match the patient: DCMS displays MSG113.<br>E4 — The version or the request changed meanwhile: DCMS displays MSG111.<br>E5 — The uploader is a dentist who is not the dentist of the visit, or has no permission: DCMS displays MSG06.<br>E6 — The file storage is not configured or cannot be read: DCMS displays MSG144.<br>E7 — A file is not a real JPEG, PNG or PDF, is empty or too large, or its name is longer than 255 characters or contains control characters: DCMS returns the file error and does not store it. | | |
| Postconditions | 1. Success: each valid file is stored with its metadata, the uploader, the time and a hash, and linked to the verified target.<br>2. A document is shown to the patient only through the examination record of the visit (UC-12); the upload alone does not publish it.<br>3. Failure: nothing incomplete is shown. | | |
| Business Rule | GBR-026, GBR-039, GBR-040, GBR-075, GBR-081 | | |

## 1.33 UC-33 Summarise and Close Visit

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Write the conclusion and the advice, choose the next step, and end the visit after the examination sheet is confirmed. | | |
| Preconditions | The dentist is authenticated and the visit is In progress with a confirmed examination sheet (UC-30). | | |
| Normal Flow | 1. In the tab "Tổng kết khám" the dentist enters the diagnoses, the conclusion and the advice, and chooses the disposition (hướng xử trí): "ConsultationOnly" (tư vấn, chưa điều trị), "FollowUp" (theo dõi / quay lại), "Referral" (chuyển khám), "DeferredDecision" (hoãn quyết định) or "ProceedToTreatment" (tiếp tục điều trị).<br>2. For "FollowUp" the dentist enters the return need; for "Referral" the destination or reason.<br>3. After the sheet is confirmed (UC-30), the dentist selects "Kết thúc lượt khám"; a dialog states that untreated items do not block the end of the examination and that the plan and the treatment sessions are followed separately.<br>4. The dentist ticks "Đã kiểm tra công việc và nhu cầu quay lại của lượt này" and selects "Xác nhận kết thúc lượt".<br>5. DCMS checks the conditions, sets the visit and the appointment to Completed.<br>6. For "ProceedToTreatment" the dentist may select "Lập kế hoạch điều trị" to open the treatment tab of the patient with the examination as the source (UC-35). | | |
| Alternative Flows | A1 — The disposition is "FollowUp": the dentist may create a follow-up instruction and later the next visit (UC-21). | | |
| Exception Flows | E1 — The examination sheet is not confirmed, or the work was not marked as reviewed, or the visit is not In progress: DCMS displays MSG239 ("hoàn tất phiếu khám và kiểm tra công việc trước khi kết thúc lượt").<br>E2 — Draft treatment activities remain on the visit: DCMS displays MSG239 ("lượt còn hoạt động điều trị nháp").<br>E3 — An investigation required for the diagnosis is neither reviewed nor cancelled: DCMS displays MSG239 ("còn chỉ định cận lâm sàng chưa được bác sĩ đọc hoặc hủy").<br>E4 — The diagnoses or the disposition are missing: DCMS displays MSG239 ("phiếu khám còn thiếu chẩn đoán hoặc hướng xử trí").<br>E5 — The visit changed meanwhile: DCMS displays MSG111.<br>E6 — No permission: DCMS displays MSG06. | | |
| Postconditions | 1. Success: the visit and the appointment are Completed; the examination content and its versions stay available.<br>2. Completing the visit does not mark any payment, complete a treatment, or change stock.<br>3. Failure: the visit stays In progress. | | |
| Business Rule | GBR-022, GBR-037, GBR-038, GBR-070, GBR-075, GBR-081 | | |

## 1.34 UC-34 View Examination History

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | See the earlier visits of a patient and the versions of their content. | | |
| Preconditions | The dentist is authenticated and has the permission to read the patient's examinations. | | |
| Normal Flow | 1. The dentist opens the tab "Khám" of the patient profile, or "Lịch sử khám" of a visit.<br>2. DCMS lists the earlier visits with the date, the branch, the dentist, the status and the diagnoses.<br>3. The dentist opens a visit to see the "Nội dung khám": symptoms, findings, diagnoses, conclusion, advice and the investigations.<br>4. The dentist opens the versions to compare an amended examination with the earlier content, with the author, the time and the reason. | | |
| Alternative Flows | A1 — No earlier visit: DCMS shows "Chưa có lượt khám". | | |
| Exception Flows | E1 — No permission: DCMS displays MSG06.<br>E2 — The visit is no longer available: DCMS displays MSG113.<br>E3 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | The history has been read only. Amended content never replaces the history. | | |
| Business Rule | GBR-038, GBR-075 | | |

## 1.35 UC-35 Manage Treatment Sheet

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Open the treatment sheets of a patient, plan the items, record the work done, add images and notes, and close, reopen, cancel or delete a sheet. | | |
| Preconditions | The dentist (role Dentist) is authenticated and has the permission to read treatment, and to update it for changes. The patient exists. | | |
| Normal Flow | 1. The dentist opens the tab "Điều trị & Thanh toán" of the patient and sees the sheets as cards, with the status "Chưa xong", "Hoàn tất" or "Đã ngừng".<br>2. The dentist selects the branch and "+ TẠO PHIẾU MỚI", or opens an existing sheet (one is open at a time).<br>3. In "KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ" the dentist adds the planned items (service, teeth in FDI numbering, expected minutes, cost, dentist and assistant) one by one or by applying a plan (UC-36). DCMS saves the plan and displays MSG50†.<br>4. In "QUÁ TRÌNH ĐIỀU TRỊ/DỊCH VỤ" the dentist records the work done with "+ Thêm việc đã làm" or by performing a planned item; each row may carry a lab order (UC-37) and the materials used (UC-38). DCMS displays MSG52†.<br>5. The dentist adds images and writes the note (up to 4,000 characters) with "Lưu ghi chú".<br>6. When the treatment ends, the dentist closes the sheet with the result of the treatment; unfinished items must be resolved first. The dentist may reopen a closed sheet. | | |
| Alternative Flows | A1 — The sheet was created by mistake: The dentist cancels or deletes it with a reason; a deleted sheet is hidden from the record.<br>A2 — The sheet has items to resolve, at step 6: The closure dialog shows "Còn n hạng mục chưa hoàn tất" and requires an outcome for each before closing.<br>A3 — The dentist prints the open sheet with "In phiếu đang mở". | | |
| Exception Flows | E1 — A required field of a row is missing or invalid (teeth not FDI, minutes outside 1 to 480, negative cost): DCMS displays MSG02, MSG04 or MSG05† under the field.<br>E2 — The sheet changed meanwhile: DCMS displays MSG111 and loads the latest version.<br>E3 — The reason to reopen a closed sheet is missing: DCMS displays MSG122; closing a sheet that still has unresolved items is refused with MSG243.<br>E4 — A repeated action was already committed: DCMS displays MSG124†.<br>E5 — The sheet is not open for the action, or its plan is still a draft: DCMS displays MSG230 or MSG240; no permission: MSG06.<br>E6 — The service is unavailable: DCMS displays MSG144; the saved content stays. | | |
| Postconditions | 1. Success: the plan, the work done, the images and the notes are kept with the actor and the time; the status of the sheet is updated.<br>2. A closed sheet no longer accepts new work until it is reopened.<br>3. Treatment status, payment and stock stay independent of each other.<br>4. Failure: nothing is committed. | | |
| Business Rule | GBR-045, GBR-046, GBR-070, GBR-075, GBR-081 | | |

## 1.36 UC-36 Apply Treatment Plan

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Apply a ready-made treatment plan (UC-43) to a treatment sheet, to fill the planned items at once. | | |
| Preconditions | The dentist is authenticated and a treatment sheet is open. At least one treatment plan exists in the branch. | | |
| Normal Flow | 1. On the open sheet the dentist selects "Kế hoạch điều trị".<br>2. DCMS shows the plans of the branch with the number of items and the total cost.<br>3. The dentist chooses a plan and confirms.<br>4. DCMS copies the items of the plan to the planned items of the sheet (service, teeth, minutes, note, dentist, assistant, cost) as drafts.<br>5. The dentist reviews and edits the rows as in UC-35, then confirms them; DCMS displays MSG50†. | | |
| Alternative Flows | A1 — The plan has no item, at step 3: DCMS adds nothing. | | |
| Exception Flows | E1 — The plan changed or was deleted meanwhile: DCMS displays MSG111 or MSG113.<br>E2 — The sheet changed meanwhile: DCMS displays MSG111.<br>E3 — No permission: DCMS displays MSG06.<br>E4 — The service is unavailable: DCMS displays MSG144; nothing is added. | | |
| Postconditions | 1. Success: the items are added to the sheet. Changing the plan afterwards does not change items already applied.<br>2. Failure: the sheet is unchanged. | | |
| Business Rule | GBR-045, GBR-046, GBR-075 | | |

## 1.37 UC-37 Order Lab Work for a Treatment

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Create a lab order for a piece of work of a treatment sheet (for example a crown), with the laboratory, the quantity, the material, the teeth and the due date. | | |
| Preconditions | The dentist is authenticated and has the permission to manage lab orders. A work row exists on the sheet. | | |
| Normal Flow | 1. On a work row the dentist selects the lab icon; the window "PHIẾU LABO" opens.<br>2. The dentist chooses the laboratory, the type of work, the quantity, the material, the teeth, the due date, the fee and a note.<br>3. The dentist saves. DCMS creates the lab order with the status "Chưa gửi" and a code, and links it to the work row.<br>4. The dentist may edit or delete the order while it is allowed.<br>5. The order is followed in UC-44. | | |
| Alternative Flows | A1 — The default laboratory of the work type is filled in at step 2 and the dentist may change it. | | |
| Exception Flows | E1 — A required field is missing or invalid: DCMS displays MSG02, MSG04 or MSG05†.<br>E2 — The order changed meanwhile: DCMS displays MSG111.<br>E3 — No permission: DCMS displays MSG06 and the window is read-only.<br>E4 — The service is unavailable: DCMS displays MSG144. | | |
| Postconditions | 1. Success: one lab order exists for the work row with its history.<br>2. The fee of the lab order is kept for the payment of the sheet (UC-40).<br>3. Failure: no order is created. | | |
| Business Rule | GBR-046, GBR-064, GBR-075, GBR-081 | | |

## 1.38 UC-38 Record Materials Used

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Record the materials used for a piece of work on a treatment sheet, and take them out of the material stock. | | |
| Preconditions | The dentist is authenticated and has the permission to update the actual work (treatment.actuals.update). A work row exists on the sheet. Materials exist in the material catalogue and have been received through purchases (material purchases screen). | | |
| Normal Flow | 1. On a work row the dentist selects the material icon; the window of materials opens with the current usage of the row.<br>2. The dentist chooses each material and enters the quantity used (at most 3 decimals, from 0 up to 1,000,000; each material once; at most 30 lines).<br>3. The dentist saves the work row. Under a global lock, DCMS recalculates the stock of each material as received purchases minus the usage of all rows (except the row being saved).<br>4. If the stock is enough, DCMS replaces the usage lines of the row with the new list, with the patient, the branch, the sheet, the time and the actor.<br>5. The usage appears in the stock and usage history of the materials. | | |
| Alternative Flows | A1 — A usage entered by mistake: The dentist removes the line (or all lines) and saves; the removed quantity returns to the stock. Deleting the work row also releases its usage. | | |
| Exception Flows | E1 — The list is invalid (more than 30 lines, a repeated material, a quantity not positive, above 1,000,000 or with more than 3 decimals, an unknown or deactivated material): DCMS displays "Danh sách vật liệu dùng không hợp lệ. Vui lòng kiểm tra vật liệu và số lượng." (MATERIAL_USAGE_INVALID, HTTP 400).<br>E2 — The stock is not enough: DCMS displays "Tồn kho không đủ." with the name, the stock and the quantity needed of each short material (MATERIAL_STOCK_INSUFFICIENT, HTTP 409) and saves nothing.<br>E3 — The row or the sheet changed meanwhile: DCMS displays MSG111.<br>E4 — The sheet is not open: DCMS displays MSG230.<br>E5 — No permission: DCMS displays MSG06. | | |
| Postconditions | 1. Success: the usage lines of the row equal the saved list and the stock is reduced accordingly. Material stock is global across branches.<br>2. A change of a purchase that would make a stock negative is refused (MATERIAL_STOCK_NEGATIVE).<br>3. Failure: the stock and the usage are unchanged. | | |
| Business Rule | GBR-046, GBR-075 | | |

## 1.39 UC-39 Prescribe Medicine

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Write, edit, finalise and print a prescription for a visit, with the care instructions and the safety check. | | |
| Preconditions | The dentist is authenticated and has the permission to prescribe. A visit of the patient exists. Medicines exist in the catalogue. | | |
| Normal Flow | 1. In the tab "Đơn thuốc" of the patient the dentist opens "ĐƠN THUỐC" of a visit.<br>2. The dentist adds medicines from the catalogue, or applies a template, and enters for each: quantity, unit, route, dose for morning, noon, afternoon and evening, meal timing, days and instructions.<br>3. DCMS runs the safety check (for example allergies and duplicates) and shows the warnings.<br>4. The dentist enters the diagnosis and the general advice and saves the draft.<br>5. The dentist finalises the prescription. DCMS keeps a snapshot and displays MSG55†; it does not change stock or any bill.<br>6. The dentist or the assistant prints it. The patient sees it in UC-13. | | |
| Alternative Flows | A1 — A finalised prescription needs a correction: The dentist makes a correction with a reason; the old content is kept in the history (MSG56† if the reason is missing).<br>A2 — The medicine is bought outside the clinic: The prescription states it (MSG74†). | | |
| Exception Flows | E1 — A required field is missing or invalid: DCMS displays MSG02 or MSG05†.<br>E2 — The prescription changed meanwhile: DCMS displays MSG111.<br>E3 — A correction reason is missing: DCMS displays MSG56†.<br>E4 — No permission: DCMS displays MSG06.<br>E5 — The service is unavailable: DCMS displays MSG144; the draft stays. | | |
| Postconditions | 1. Success: the finalised prescription is kept as it was at finalisation; later changes of the catalogue or of templates do not change it.<br>2. Failure: the prescription stays a draft. | | |
| Business Rule | GBR-048, GBR-049, GBR-050, GBR-075, GBR-081 | | |

## 1.40 UC-40 Collect Treatment Payment

| Primary Actors | Clinic Owner | Secondary Actors | Payment Gateway (PayOS) |
|---|---|---|---|
| Description | Record the cash collected for the services and the lab fees of a treatment sheet, receive a deposit (advance) in cash or through PayOS, collect online through a PayOS QR order, and allocate an advance to the lines of the sheet. | | |
| Preconditions | The user has the role Clinic Owner and is authenticated; every payment function of the sheet is refused to other roles (HTTP 403). A treatment sheet with lines exists. For online payment PayOS is configured and the webhook can reach the server. | | |
| Normal Flow | 1. On the treatment sheet the owner selects "Trả tiền"; DCMS shows the payment context: each service line with price, paid and due, each lab fee with total, paid, due, the minimum 30% and the remaining priority amount, and the advance received.<br>2. The owner enters the paid date (not in the future) and the amount collected for each line, or a deposit amount ("Số tiền cọc").<br>3. DCMS checks that every amount is positive, the amounts are not above what is due, a line or lab order appears only once, and the total is at most 1,000,000,000,000 VND.<br>4. DCMS checks the lab priority: at least 30% of each lab fee must be covered before money is collected for other services.<br>5. The owner confirms. DCMS records each collection in the ledger once, with the actor and the time (method "Cash"), and returns the updated context.<br>6. For an online payment the owner creates a PayOS order for the amounts (or for a deposit); DCMS shows the QR and keeps the order pending.<br>7. PayOS sends the result to the webhook; DCMS verifies the signature, records the collection and updates the sheet. | | |
| Alternative Flows | A1 — A deposit is already received, at step 2: The owner allocates the advance to the lines ("advance allocation"); the allocated amount reduces the advance, and each line and lab amount is checked as above.<br>A2 — An unexpired QR order already exists, at step 6: DCMS asks to reopen the current QR instead of creating another.<br>A3 — The sheet needs a deposit exception: The owner records it in the treatment sheet ("deposit exception") with the reason.<br>A4 — The customer did not come and the deposit is kept: The owner records the forfeit in UC-42. | | |
| Exception Flows | E1 — An amount is not positive, a line is repeated, the date is in the future, the request key is empty, or no line is given: DCMS refuses with "Thông tin thu tiền không hợp lệ." (HTTP 400); an amount outside the limit is refused with "Số tiền không hợp lệ." or "Số tiền vượt giới hạn.". A deposit amount that is not valid is refused with "Số tiền cọc không hợp lệ.".<br>E2 — A line or its due amount changed meanwhile: DCMS refuses with "Dịch vụ hoặc số còn phải thu đã thay đổi. Vui lòng tải lại." (HTTP 409).<br>E3 — The 30% lab priority is not met: DCMS refuses with "Cần ưu tiên đủ 30% phí labo trước khi thu cho dịch vụ khác." and records nothing.<br>E4 — The request key was already used for a different collection: DCMS refuses with "Mã yêu cầu đã được dùng cho một khoản thu khác."; the same request repeated returns the existing result without recording it twice.<br>E5 — The PayOS webhook has an invalid signature or data: DCMS rejects it (HTTP 400) and records nothing.<br>E6 — The user is not the owner: the request is refused (HTTP 403).<br>E7 — The server fails: the transaction is rolled back and nothing is recorded. | | |
| Postconditions | 1. Success: each collection is written once in the finance ledger inside a serializable transaction that locks the treatment sheet, so two collections cannot both use the same due amount.<br>2. A repeated request with the same key does not create a second collection.<br>3. Failure: nothing is recorded as paid. | | |
| Business Rule | GBR-064, GBR-065, GBR-066, GBR-067, GBR-069, GBR-070 | | |

## 1.41 UC-41 Manage Working Schedule

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Set the weekly template and the exceptions of the working schedule of a dentist, check the effect on the appointments and publish it. | | |
| Preconditions | The user is authenticated and has the permission to read working schedules, and to manage them for the person edited. A dentist always sees their own schedule. | | |
| Normal Flow | 1. The user opens "Lịch làm việc", chooses the branch, the person and the type of schedule: "Mẫu lặp hằng tuần" or "Ngoại lệ ngày cụ thể", and the start date.<br>2. DCMS shows the status (published or not, version) and the editor of the seven days.<br>3. The user sets for each day the branch and the periods of type Work, Break and Block in steps of 15 minutes. For an exception the user enters the reason.<br>4. The user selects "Kiểm tra ảnh hưởng"; DCMS validates the periods and lists the confirmed appointments that the change would affect.<br>5. If none is affected, the user selects "Lưu và công bố". DCMS publishes the schedule, recalculates the availability and displays MSG39.<br>6. The user may preview the free slots that the schedule gives. | | |
| Alternative Flows | A1 — Confirmed appointments would be affected, at step 4 or 5: DCMS lists them with MSG38 and does not publish. The assistant first handles the appointments (UC-18, UC-19 or UC-22), then the user reloads and publishes again.<br>A2 — The user abandons the editing: "Hủy thay đổi chưa lưu" restores the published version and DCMS asks MSG08† when leaving with changes. | | |
| Exception Flows | E1 — The periods are invalid or overlap: DCMS displays MSG110.<br>E2 — The schedule or the appointments changed after the preview: DCMS displays MSG111 and requires a reload.<br>E3 — The reason of an exception is missing: DCMS displays MSG02.<br>E4 — No permission to manage: the fields and the buttons are disabled; DCMS displays MSG06 on a direct request.<br>E5 — The save fails: DCMS displays MSG144; the published schedule stays in effect. | | |
| Postconditions | 1. Success: the valid schedule is published, its availability is recalculated and no confirmed appointment is invalidated.<br>2. Failure or abandonment: the previous schedule stays in effect. | | |
| Business Rule | GBR-001, GBR-002, GBR-003, GBR-004, GBR-007, GBR-008, GBR-081 | | |

## 1.42 UC-42 View Finance Statistics

| Primary Actors | Clinic Owner | Secondary Actors | None |
|---|---|---|---|
| Description | See the amount to collect, the money received, the advance, the receivables, the cost of materials and the profit of the clinic for a period, open the list behind each figure, and record the forfeit of a deposit when a customer did not come. | | |
| Preconditions | The user has the role Clinic Owner and is authenticated. | | |
| Normal Flow | 1. The owner opens "Thống kê tài chính" and chooses the dates the treatment sheets were opened and, when allowed, the branch.<br>2. DCMS shows six figure cards (Số tiền cần thu dịch vụ, Tiền thực thu, Tiền nhận trước, Tiền còn cần thu, Tiền dụng cụ vật liệu, Lãi), a bar chart, a ring chart, the collection progress and the profit margin.<br>3. The owner opens a figure to see the list behind it: services, payments, advances, outstanding sheets, material purchases or profit entries, 20 rows per page.<br>4. From a row the owner opens the treatment sheet in "Thống kê công việc/dịch vụ".<br>5. In the list of outstanding sheets, for a sheet whose deposit can be forfeited, the owner selects "Khách không đến, bỏ cọc", enters the reason (up to 500 characters) and confirms. DCMS hides the sheet and records the deposit as forfeited. | | |
| Alternative Flows | A1 — No data in the period, at step 2: DCMS shows the figures as 0 and the lists as empty (MSG99†). | | |
| Exception Flows | E1 — The user is not the owner: DCMS displays MSG06 and shows no figure.<br>E2 — The forfeit reason is missing: the button stays disabled.<br>E3 — The sheet changed meanwhile, at step 5: DCMS displays MSG111 and reloads.<br>E4 — The data cannot be loaded: DCMS shows "Không tải được thống kê." (MSG144) with the button "Tải lại". | | |
| Postconditions | 1. The figures only read the data; the cost of materials is the total of the purchases of the whole clinic in the period, and profit is counted only for fully allocated sheets.<br>2. A forfeit records the deposit as a no-show income and hides the sheet; it cannot be used for payment any more. | | |
| Business Rule | GBR-065, GBR-069, GBR-071, GBR-075, GBR-081 | | |

## 1.43 UC-43 Manage Treatment Plans

| Primary Actors | Dentist | Secondary Actors | None |
|---|---|---|---|
| Description | Create, rename, edit and delete the ready-made treatment plans of a branch, which are applied to treatment sheets in UC-36. | | |
| Preconditions | The user has the role Dentist, is authenticated and has the permission to read treatment templates, and to manage them for changes. | | |
| Normal Flow | 1. The dentist opens "Kế hoạch điều trị", chooses the branch and sees the plans with the number of items and the total cost; the dentist may search by name.<br>2. The dentist selects "Tạo kế hoạch", enters the name (up to 160 characters) and saves; the plan opens.<br>3. The dentist selects "+ Thêm hạng mục" and enters the planned date (optional), the teeth (FDI codes), the service, the expected minutes (1 to 480), a note, the dentist and the assistant, and the cost (the reference price of the service is filled in).<br>4. The dentist saves the row. The plan shows the number of items (at most 100) and the total cost.<br>5. The dentist may edit or delete an item, rename the plan ("Sửa tên") or delete the plan after confirming. | | |
| Alternative Flows | A1 — The plan was changed by someone else, at step 4 or 5: DCMS displays MSG111; the dentist selects "Xem phiên bản mới" and keeps the draft or takes the new version. | | |
| Exception Flows | E1 — A required field is missing or invalid (name empty, service not chosen, teeth not FDI, minutes or cost out of range, note over 1,000 characters): DCMS displays MSG02, MSG03, MSG04 or MSG05†.<br>E2 — The user has no permission to manage: the creation and edit buttons are disabled; DCMS displays MSG06 on a direct request.<br>E3 — The same request is repeated: DCMS displays MSG124†.<br>E4 — The service is unavailable: DCMS displays MSG144; the saved plan stays. | | |
| Postconditions | 1. Success: the plan and its items are saved with a new version.<br>2. Deleting a plan does not change treatment sheets that already used it.<br>3. Failure: nothing is committed. | | |
| Business Rule | GBR-045, GBR-063, GBR-075, GBR-081 | | |

## 1.44 UC-44 Manage Lab Work

| Primary Actors | Dentist; Clinic Assistant | Secondary Actors | None |
|---|---|---|---|
| Description | Follow the lab orders of the clinic by status, move an order through sent, received, delivered or rework with a history, and open the order. | | |
| Preconditions | The user is authenticated and has the permission to read lab orders; to change the status the user needs the permission to track them, and to edit the order the permission to manage them. | | |
| Normal Flow | 1. The user opens "Quản lý công việc labo" and sees the tiles with the number of orders: Chưa gửi, Đã gửi xưởng, Đã nhận về, Quá hạn hẹn, Đã giao bệnh nhân, Làm lại.<br>2. The user selects a tile to filter, and may search by code, patient code, patient name or work, and filter by branch, laboratory, dentist and due dates.<br>3. The user opens an order in the panel on the right: patient, work, teeth, laboratory, dentist, dates, fee, paid and note.<br>4. The user enters a note (up to 300 characters) and selects the next status: "Gửi xưởng", "Đã nhận về", "Giao bệnh nhân" or "Yêu cầu làm lại".<br>5. DCMS checks the version, saves the new status with the actor, the time and the note, and updates the history "Nhật ký trạng thái".<br>6. The user selects "Sửa phiếu" (or "Xem phiếu đầy đủ") to open the full order as in UC-37. | | |
| Alternative Flows | A1 — An order is late: DCMS marks it "Quá hạn hẹn" and counts it in the tile. | | |
| Exception Flows | E1 — The order changed meanwhile, at step 5: DCMS displays MSG111 and reloads the list.<br>E2 — The status is not allowed from the current one: DCMS does not offer the button; a direct request returns MSG06.<br>E3 — No permission to track: the status buttons are not shown.<br>E4 — The order is no longer available: DCMS displays MSG113.<br>E5 — The service is unavailable: DCMS displays MSG144 with the button "Thử lại". | | |
| Postconditions | 1. Success: the order has the new status, and every change is in the history with the actor and the time.<br>2. Failure: the status is unchanged. | | |
| Business Rule | GBR-046, GBR-064, GBR-075, GBR-081 | | |
