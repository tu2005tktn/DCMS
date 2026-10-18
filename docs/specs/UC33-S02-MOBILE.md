# UC-33 and S02 Mobile registration

Decision recorded 12 October 2026: public registration runs on Mobile. A visitor enters a full name, a Vietnamese mobile number, an email address, a password, and password confirmation. S02 does not send or verify a code. Staff accounts remain Admin-created through UC-31. This decision replaces the earlier S02 channel and verification workflow. The email field was added to S02 on 12 October 2026.

## Workflow and account boundary

1. The visitor enters the five required fields. Mobile shows field errors and a masked password with a visibility toggle.
2. The API validates the fields and password policy. It accepts a domestic, ten-digit Vietnamese mobile number beginning with 03, 05, 07, 08, or 09; surrounding whitespace may be trimmed, while `+84`, embedded spaces, and other formats are rejected. The stored and sign-in value uses the same `0`-prefixed form. This format check does not prove ownership of the number.
3. One successful request creates one active `Public` account with only the `Guest` role. The mobile number is both `UserName` and `PhoneNumber`. The trimmed email is stored as contact information. `PhoneNumberConfirmed` and `EmailConfirmed` remain false. Password confirmation is not persisted.
4. The result shows MSG11 and navigates to S01. Registration does not create a session; the user signs in with the phone number and password through UC-01.

Registration never creates a Patient record, a verified patient link, a staff role, or clinical access. S07 handles any later patient link separately. S03 recovery is a separate feature and must not treat either unverified contact as proof of ownership. The obsolete S02 verification route and send-code adapter are outside this workflow. S02 does not claim to send a verification email or SMS.

## Validation and uniqueness

- Full name is required after trimming and limited to 160 characters, matching `USER_ACCOUNT.FullName`. It is not evidence of patient identity.
- Email is required, trimmed, at most 254 characters, and must be a valid address. The API stores it in Identity's `Email` and `NormalizedEmail` fields; it is not verified or used as a sign-in identifier in S02. Email is not globally unique under the existing staff account policy, and S02 does not reserve an unverified address. Phone-number uniqueness remains the account rule.
- Existing accounts created before this change may still have no email; the new requirement applies to new public registrations and does not invent an email for earlier accounts.
- Password uses the [shared password policy](SHARED-PASSWORD-POLICY.md): 8–256 characters, with no required uppercase, lowercase, digit or symbol. Identity enforces the effective configuration, which clients read from `/api/auth/policy`.
- Confirmation must exactly equal the password. The server repeats all validation even when Mobile has already checked it.
- Identity's existing unique index on `NormalizedUserName` enforces one account per registration phone, including simultaneous submissions. A duplicate returns 409/MSG139 and does not create another account. Staff `PhoneNumber` remains an optional contact under UC-31 and is not globally unique; changing that staff policy requires a separate decision.
- A request cannot select roles, account type, permissions, patient ID, or verified status. The server assigns Guest explicitly; extra client properties never grant privileges.

## HTTP contract

`POST /api/auth/register` is anonymous and accepts JSON:

```json
{"fullName":"Nguyễn Văn A","phoneNumber":"0912345678","email":"nguyenvana@example.com","password":"...","confirmPassword":"..."}
```

Success: HTTP 201 with `{ "accountId": 123, "phoneNumber": "0912345678", "notification": { "code": "MSG11", "message": "..." } }`. No access token is returned. Validation errors use the existing `{code,message}` response and HTTP 400: MSG02 for missing values, MSG03 for full-name or email length, MSG04 for phone, email, or confirmation format, MSG109 for the configured password policy. Duplicate phone returns HTTP 409/MSG139. Persistence failure returns MSG144 without claiming success. MSG10 and MSG108 do not apply because S02 has no verification proof.

`POST /api/auth/sign-in` retains its existing `{userName,password}` contract; the registered phone number is supplied as `userName`. `GET /api/auth/policy` remains the source of the current password requirements shown on Mobile.

## Acceptance checks

- Register in S02, navigate to S01, then sign in with the new phone through the real API.
- Missing/invalid fields, including email, mismatched passwords, weak passwords, duplicate phones, and repeated or concurrent submissions are rejected without partial accounts.
- The saved email matches the trimmed address supplied by the visitor and remains unverified; no verification message is sent.
- The resulting account has only Guest, no Patient or employee profile, no verified patient link, and no clinical permission. Sending a role field cannot change that.
- Mobile never claims a verification code or real SMS was sent. The existing S01 Mobile placeholder is a separate task; the API sign-in check remains part of the S02 demo.

Vietnamese mobile prefix and ten-digit format reference: [Ministry of Information and Communications](https://mic.gov.vn/bo-tttt-day-manh-dinh-danh-cuoc-goi-de-ngan-ngua-lua-dao-tren-khong-gian-mang-19724111208351436.htm).
