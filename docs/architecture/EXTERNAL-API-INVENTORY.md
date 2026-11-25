# External API Inventory

Updated 20 November 2026. This document lists every service outside the DCMS API process that DCMS calls or that calls DCMS at runtime. It describes the implemented behavior; the code under `backend/src` is the source of truth. The summary table matches section 5.2 of Report 3 (Software Requirement Specification). Background processes that call these APIs are listed in [Background Job Inventory](BACKGROUND-JOB-INVENTORY.md).

Credentials for all services come from environment variables (`.env` locally, `/opt/dcms/.env` on the server) and never from Git.

## 1. Summary

| # | API Name | Calling Path | Description |
|---|---|---|---|
| 1 | Push Notification API | DCMS → Firebase Cloud Messaging (FCM) | Sends appointment updates, appointment reminders, treatment record updates, and new clinical document notices to registered mobile devices. DCMS obtains an OAuth 2.0 access token from Google with a service account, then submits each message to FCM. Returns submission results and delivery status when supported. Notification content excludes sensitive clinical and financial information. |
| 2 | Google Sign-In API | Web/Mobile → Google Identity Services; DCMS → Google public keys endpoint | Lets customers sign in with a Google account on the web portal and the mobile app. The client receives a Google ID token; DCMS verifies its signature with Google's public keys and checks the issuer and audience (Web Client ID) before creating or linking the account. DCMS does not receive or store the user's Google password. |
| 3 | Payment Gateway API | DCMS → PayOS Merchant API; PayOS → DCMS webhook | Creates payment requests (QR code and checkout link) for treatment payments and advance payments, and queries their status. PayOS sends the payment result to the DCMS webhook; DCMS verifies the HMAC-SHA256 checksum before recording the payment. Only the order code, amount, and payment description are sent; no clinical information is shared. |
| 4 | Email Delivery Service | DCMS → SMTP server (Gmail) | Sends account email confirmation codes and password recovery codes. Connects over SSL/TLS with an application password. Returns whether the message was accepted by the mail server. Email content contains only the verification code and its expiry time. |
| 5 | AI Caries Detection API | DCMS → AI Service | Sends a dental X-ray image to the separately deployed AI service, which detects suspected caries lesions and returns their locations and confidence scores. Requests are authenticated with an API key. Results are suggestions only and must be reviewed by a dentist. |

Classification:

| # | API Name | Provider | Type | Required to run DCMS |
|---|---|---|---|---|
| 1 | Push Notification API | Google Firebase | Third-party, internet | No. Without it, no push is queued and patients receive in-app notifications only. |
| 2 | Google Sign-In API | Google | Third-party, internet | No. Without it, Google sign-in answers `GOOGLE_NOT_CONFIGURED` (503); password sign-in still works. |
| 3 | Payment Gateway API | PayOS (payos.vn) | Third-party, internet | No. Without it, transfer payment by QR is unavailable; other payment methods still work. |
| 4 | Email Delivery Service | Gmail SMTP (any SMTP server) | Third-party, internet | No. Without it, email confirmation reports a delivery error. In Development, recovery codes are only logged. |
| 5 | AI Caries Detection API | DCMS `ai-service` | Self-hosted, private network | No. Without it, X-ray analysis answers `MSG144` (503); other features are unaffected. |

## 2. API details

### 2.1 Push Notification API (Firebase Cloud Messaging)

| Item | Detail |
|---|---|
| Purpose | Deliver notifications created by DCMS to the patient's Android device as system push notifications. |
| Used by | S26 appointment notifications and reminders, treatment record updates, ready clinical documents; mobile app (`expo-notifications`). |
| Calling path | DCMS API (background job) → Google OAuth 2.0 token endpoint → FCM HTTP v1 |
| Endpoints | `POST https://oauth2.googleapis.com/token` (JWT bearer grant, scope `https://www.googleapis.com/auth/firebase.messaging`); `POST https://fcm.googleapis.com/v1/projects/{project_id}/messages:send` |
| Authentication | Service account JSON file. DCMS signs an RS256 JWT with the service account private key, exchanges it for an access token, and sends `Authorization: Bearer <token>` to FCM. |
| Device registration | The mobile app registers its native device token with `PUT /api/notifications/devices` and removes it with `DELETE /api/notifications/devices`. Tokens are stored in `PUSH_DEVICE`. |
| Request data | Device token, notification title and body, Android channel `appointments`, and data fields `notificationId` and `deepLink`. Up to 20 devices per account. |
| Response handling | HTTP 2xx: delivered. HTTP 404 with `UNREGISTERED`: the device token is deleted so it never receives another account's notifications. Any other error: the delivery fails and is retried by JOB-03 in the [Background Job Inventory](BACKGROUND-JOB-INVENTORY.md). |
| Failure and fallback | In-app notifications are the primary record and are never rolled back. After 3 failed attempts the delivery is marked Failed and a staff handling item is created. |
| Data privacy | Title and body contain the appointment code and time only, with no clinical or financial details. |
| Configuration | `FIREBASE_SERVICE_ACCOUNT_PATH` → `Firebase:ServiceAccountPath` (absolute path to the service account JSON outside the repository). |
| Code | `backend/src/DCMS.Infrastructure/Features/Notifications/FirebasePushAdapter.cs`, `mobile/src/features/appointments/push-notifications.tsx` |
| Related specs | [S26 Notifications & Appointment Reminders](../specs/S26-Notifications-Reminders.md), [Notification storage](../specs/NOTIFICATION-STORAGE.vi.md) |

### 2.2 Google Sign-In API (Google Identity Services)

| Item | Detail |
|---|---|
| Purpose | Let customers create or sign in to a DCMS account with a Google account. |
| Used by | Customer web portal sign-in, mobile sign-in screen. |
| Calling path | Web: browser → `https://accounts.google.com/gsi/client`. Mobile: app → Google Sign-In SDK (`@react-native-google-signin/google-signin`). Server: DCMS API → `https://www.googleapis.com/oauth2/v3/certs`. |
| DCMS endpoints | `GET /api/auth/google/config` returns the public Web Client ID to the client. `POST /api/auth/google/sign-in` receives the Google ID token. |
| Authentication | Public OAuth Web Client ID only; no client secret is used. |
| Verification | DCMS validates the ID token: RS256 signature against Google's public keys, issuer `accounts.google.com`, audience equal to the Web Client ID, expiry (30 s clock skew), and a verified email claim. |
| Caching | Google's public keys are cached for the `Cache-Control` max-age, at most 1 hour. When a key is not found, DCMS refreshes the keys once and validates again. |
| Data received | Google subject ID (`sub`), email, display name. DCMS never receives the Google password. |
| Failure and fallback | Not configured: `GOOGLE_NOT_CONFIGURED` (503). Invalid or expired token: `GOOGLE_INVALID_TOKEN` (401). Password sign-in is unaffected. |
| Configuration | `GOOGLE_WEB_CLIENT_ID` → `Google:WebClientId` |
| Code | `backend/src/DCMS.Infrastructure/Features/Authentication/GoogleTokenVerifier.cs`, `GoogleAuthenticationService.cs`, `web/src/features/customer-auth/CustomerGoogleButton.tsx`, `mobile/src/features/auth/google/` |
| Related spec | [Google sign-in email](../specs/GOOGLE-SIGN-IN-EMAIL.md) |

### 2.3 Payment Gateway API (PayOS)

| Item | Detail |
|---|---|
| Purpose | Collect treatment payments and advance payments by bank transfer through a VietQR code. |
| Used by | Treatment dossier payment popup (web staff). |
| Calling path | DCMS API → PayOS Merchant API; PayOS → DCMS webhook |
| PayOS endpoints | `POST https://api-merchant.payos.vn/v2/payment-requests` creates a payment request. `GET https://api-merchant.payos.vn/v2/payment-requests/{orderCode}` reads its status. |
| DCMS endpoints | `POST /api/payments/payos/webhook` (anonymous, signature-checked). Staff endpoints under the treatment dossier: `payos-orders`, `advance-payos-orders` and their `active` and `{orderId}` status reads. |
| Authentication | Headers `x-client-id` and `x-api-key`. Create requests are signed with HMAC-SHA256 over `amount`, `cancelUrl`, `description`, `orderCode` and `returnUrl` using the checksum key. Webhook data is verified with HMAC-SHA256 over its sorted fields, compared in constant time. |
| Request data | Order code, amount (VND), description `DCMS {orderCode}`, return/cancel URL (the first configured web origin + `/patients`), and an expiry 30 minutes after creation. |
| Response used | Payment link ID, QR code, bank BIN, account number and account name to render the QR inside DCMS; on status reads, amount, amount paid and status. |
| Settlement rule | A webhook or status read never records money by itself. DCMS queries PayOS and records the ledger entry only when PayOS reports `PAID`, the order code, payment link and amount match, and the amount paid equals the order amount. Mismatches return `REVIEW` for staff. Recording is idempotent per order. |
| Failure and fallback | PayOS unreachable: `PAYOS_STATUS_UNAVAILABLE` (503); the order stays pending and JOB-05 in the [Background Job Inventory](BACKGROUND-JOB-INVENTORY.md) reconciles it. |
| Data privacy | Only order code, amount and description are sent; no patient name or clinical data. |
| Configuration | `PAYOS_CLIENT_ID`, `PAYOS_API_KEY`, `PAYOS_CHECKSUM_KEY` → `PayOs:ClientId`, `PayOs:ApiKey`, `PayOs:ChecksumKey`. All three are required. |
| Code | `backend/src/DCMS.Api/Features/Finance/TreatmentPayOsGateway.cs`, `TreatmentPayOsEndpoints.cs`, `TreatmentPayOsWorker.cs` |
| Related docs | [Treatment dossier payments](../specs/TREATMENT-DOSSIER-PAYMENTS.md), [PayOS transfer bill](../implementation/PAYOS-TRANSFER-BILL.vi.md) |

### 2.4 Email Delivery Service (SMTP)

| Item | Detail |
|---|---|
| Purpose | Send one-time codes by email. |
| Used by | Public account email confirmation; S03 account recovery. |
| Calling path | DCMS API → SMTP server (Gmail `smtp.gmail.com` in production; Mailpit for local testing) |
| Protocol | SMTP through MailKit. Port 465 uses implicit SSL; other ports use STARTTLS when SSL is enabled. |
| Authentication | SMTP username and application password (a Google App Password for Gmail, never the account password). |
| Messages | "Xác minh email tài khoản DCMS" (email confirmation) and "Mã khôi phục tài khoản DCMS" (recovery). Plain text containing the code, its lifetime in minutes, and a note to ignore unexpected email. |
| Response handling | The adapter returns success when the server accepts the message. SMTP, network and configuration errors return failure; the caller shows a delivery error. |
| Failure and fallback | SMTP not configured: email confirmation reports a delivery error. In Development only, recovery codes are logged instead of sent. |
| Configuration | `SMTP_HOST`, `SMTP_PORT`, `SMTP_ENABLE_SSL`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS`, `SMTP_FROM_NAME` → `Smtp:*` |
| Code | `backend/src/DCMS.Infrastructure/Features/Authentication/SmtpContactDeliveryAdapter.cs` |
| Related spec | [S03 public email recovery](../specs/S03-PUBLIC-EMAIL-RECOVERY.md) |

### 2.5 AI Caries Detection API (ai-service)

| Item | Detail |
|---|---|
| Purpose | Suggest suspected caries lesions on dental X-ray images. |
| Used by | S19/S20 clinical document viewer. |
| Calling path | DCMS API → `ai-service` (FastAPI + YOLO, deployed as image `dcms-ai`, not exposed to the internet) |
| Endpoint | `POST {DENTAL_AI_BASE_URL}/v1/xray/analyze`, multipart field `file` (`image/jpeg` or `image/png`) |
| Authentication | Header `X-Api-Key` when an API key is configured; it must equal `AI_SERVICE_API_KEY` of the service. |
| Request data | The X-ray image bytes read from private document storage after the SHA-256 check. |
| Response used | Schema version, model name and SHA-256, image size, device, elapsed time, and findings (ID, kind, label, confidence 0–1, bounding box). Findings outside the image are dropped. |
| Timeout | `DENTAL_AI_TIMEOUT_SECONDS`, default 120, limited to 5–600 seconds. |
| Failure and fallback | Not configured, unreachable, timeout or invalid response: `MSG144` (503), nothing is saved, audit `AiAnalysisFailed`. |
| Data privacy | Only the image is sent; no patient identity. Model weights stay outside Git. |
| Configuration | `DENTAL_AI_BASE_URL`, `DENTAL_AI_API_KEY`, `DENTAL_AI_TIMEOUT_SECONDS` → `DentalAi:*` |
| Code | `backend/src/DCMS.Infrastructure/Features/Examinations/HttpDentalAiClient.cs`, `ai-service/` |
| Related spec | [AI caries detection](../specs/AI-CARIES-DETECTION.vi.md) |

## 3. Not runtime external APIs

These sources are reached only by developers or are links, not calls made by DCMS at runtime:

| Source | Use |
|---|---|
| `raw.githubusercontent.com`, `api.sketchfab.com` | `infrastructure/scripts/fetch-dental-models.py` downloads the 3D tooth models and checks their CC BY 4.0 licence once. The web app serves the models from DCMS itself. |
| `sketchfab.com` links | Credit links for the 3D tooth models shown in the odontogram. |
| ClamAV | Removed on 20 November 2026; uploaded files are no longer virus scanned. See [S19/S20 DCMS-112](../specs/S19-S20-DCMS-112.md). |

## 4. Known gaps

- The root `.env.example` does not list `PAYOS_*` and `FIREBASE_SERVICE_ACCOUNT_PATH`, although `infrastructure/scripts/import-development-env.ps1` reads them. It still lists `CLAMAV_HOST` and `CLAMAV_PORT`, which are no longer used.
