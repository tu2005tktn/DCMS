# IV. Non-Functional Requirements

Updated 20 November 2026. Section IV of Report 3 (Software Requirement Specification) for DCMS. External services are detailed in the [External API Inventory](EXTERNAL-API-INVENTORY.md) and background processing in the [Background Job Inventory](BACKGROUND-JOB-INVENTORY.md).

## 1. External Interfaces

| Interface | Description |
|---|---|
| Staff web application | Browser application for clinic owners, dentists, receptionists and other staff: scheduling, reception, examination, treatment, payments, care, inventory, reports and system settings. Designed for desktop screens; menus and actions depend on the user's access group and branch. |
| Customer web portal | Browser portal for patients and family members: registration, sign-in with password or Google, booking, viewing and changing appointments, medical records, prescriptions and notifications. |
| Patient mobile application | Android application providing the customer functions and receiving push notifications. |
| Client devices | Any device with a current web browser (Chrome, Edge, Firefox or Safari); an Android phone for the mobile application. No special hardware is required. |
| Imaging devices | DCMS does not connect to X-ray machines or intraoral cameras directly. Images exported by the device software are uploaded as JPEG, PNG or PDF files. |
| Database | PostgreSQL 17, accessed through Entity Framework Core. |
| Push notification service | Firebase Cloud Messaging delivers notifications to the mobile application. |
| Google sign-in | Google Identity Services authenticates customers who choose Google sign-in. |
| Payment gateway | PayOS creates VietQR bank transfer payments and sends payment results to DCMS through a signed webhook. |
| Email service | An SMTP server sends email confirmation and account recovery codes over SSL/TLS. |
| AI service | A separately deployed service detects suspected caries on dental X-ray images. |
| Communication protocol | Clients call a REST API with JSON over HTTPS. Clinic computers may connect through a WireGuard VPN. Open sessions receive new notifications in real time over WebSocket (SignalR). |

## 2. Quality Attributes

### 2.1 Usability

| Requirement | Description |
|---|---|
| Vietnamese interface | All screens, messages, emails and notifications are in Vietnamese. Dates use dd/MM/yyyy, times use the 24-hour clock in Vietnam time, and amounts are shown in VND. |
| Consistent layout | All screens use the same components, table style, form layout and button placement, so a user who learns one screen can use the others. |
| Responsive customer pages | Customer pages are usable on screens from 390 px to 1440 px wide without horizontal scrolling. |
| Clear feedback | Every action shows a loading state, then a success message or an error message with its message code. Lists show distinct loading, empty, error and access-denied states. |
| Error recovery | When a request fails, entered data is kept and the user can retry without starting again. |
| Keyboard access | Main actions can be reached with the Tab key and activated with Enter. |
| Confirmation of irreversible actions | Cancelling an appointment, marking a no-show, deleting a record and locking an account require confirmation and, where the business rules require it, a reason. |

### 2.2 Performance

| Requirement | Target | Condition |
|---|---|---|
| Page load time | Any page loads within 2 seconds | Up to 100 users active simultaneously |
| Form submission response | System acknowledges within 2 seconds | Up to 100 users active simultaneously |
| Maximum simultaneous users | System remains fully functional for up to 100 users | Single server deployment |
| API response time | 95% of data requests complete within 500 ms | Up to 100 users active simultaneously |
| File upload | A 5 MB clinical document is stored and acknowledged within 2 seconds | Files up to 20 MB, at most 10 files per upload |
| Notification delivery | In-app notification appears immediately; push notification is sent within 2 minutes | Push service available and device registered |
| AI X-ray analysis | Result or an error message returned within 120 seconds | AI service running on the clinic server |

### 2.3 Security

| Requirement | Description |
|---|---|
| Authentication required | Every protected page and action requires authentication; only sign-in, public registration and explicitly public clinic/appointment functions are accessible to Guests. |
| Password protection | Passwords must never be stored in a form that a person or program can read. Passwords have 8 to 256 characters. |
| Account lockout | After a defined number of consecutive failed sign-in attempts, the account is temporarily locked. Threshold and duration defined in TDS §4. Administrators can also lock and unlock accounts with a recorded reason. |
| Clinical document access | Patient clinical documents are private. Validate account, role and patient-link authorization on every download and prohibit publicly guessable file links. |
| Session management | Access tokens expire after 15 minutes and are renewed automatically while the session is valid. Staff sessions end after an idle time set by the clinic owner (5 to 120 minutes, default 20). Users can sign out of one session or all sessions. |
| Role and branch authorization | Each function is available only to roles and access groups that hold its permission. Staff can view and change data only in the branches assigned to them. |
| Account recovery | Recovery and email confirmation use 6-digit one-time codes that expire after 5 minutes and allow at most 5 attempts. Requests are rate-limited per account and per network address. A password reset never unlocks a locked account. |
| Secure transport | All traffic over the Internet uses HTTPS. Only the clinic's own web addresses may call the API from a browser. |
| File upload validation | Only JPEG, PNG and PDF files up to 20 MB are accepted. The server checks the file type, extension and content signature, and downloaded files are never cached by the browser. |
| Third-party data minimization | Push notifications contain no clinical or financial details; payment requests contain only the order code, amount and description; the AI service receives only the image. |
| Payment integrity | A payment is recorded only after the payment gateway's signature is verified and the paid amount is confirmed, and each payment is recorded once. |
| Secret management | Passwords, API keys and service credentials are kept in server configuration, never in source code. |
| Audit trail | Sign-in, sign-out, account and permission changes, clinical document changes, appointment changes and payments are recorded with the actor and time and cannot be edited by users. |
