# S03 / UC-02 — Public email password recovery

Decision updated 18 October 2026: the email registered on a Public account is eligible for password recovery without a separate email-confirmation step. This specification narrows S03 to Public accounts on customer Web and Mobile. Staff sign-in and S04 Admin recovery retain their existing behavior. There is no SMS delivery or phone recovery in this scope.

## Account and channel boundary

- S02 registration completes without an OTP. `EmailConfirmed` remains false because registration alone does not prove ownership; Public recovery eligibility is based on the email stored on that account. This applies to existing Public accounts with a registered email as well as new registrations. Do not bulk-update `EmailConfirmed` or Patient links.
- Public S03 accepts an email address. When it belongs to a Public account, a one-time recovery code is sent to that address even if `EmailConfirmed` is false. Correct entry of the delivered code proves control of that inbox for this reset only. Every valid request/resend receives the same MSG12 response regardless of account existence, cooldown or delivery state. The UI must not claim a real message was sent solely from MSG12.
- SMTP is the delivery channel. Development testing uses Mailpit or a configured SMTP server. No SMS adapter or UI is provided. Existing staff recovery routes and development adapter are outside the customer UI.

## Contracts

Anonymous customer recovery uses `/api/customer/recovery/request`, `/resend`, `/verify`, `/reset`. These share the existing recovery DTOs but enforce a Public-only, email-only boundary. `verify` yields an in-memory reset grant; `reset` takes that grant plus matching new password and confirmation. A customer page never sends an account ID or a chosen destination. Invalid/expired/used/over-limit proof uses MSG108; invalid new password uses MSG109; service or delivery failure uses MSG144. Request and resend use neutral MSG12. On success use MSG13 and return to the customer sign-in screen.

## Proof, session and UX rules

- Recovery codes are cryptographically random, stored only as hashes, single-use, time limited and bounded by server-side send/attempt limits. Configuration defaults are [PROPOSED] until approved: 15-minute proof lifetime, 2-minute resend cooldown, 3 resends, 5 incorrect attempts and six digits. The UI countdown is a hint; the server is authoritative.
- Do not put codes, reset grants, passwords or SMTP credentials in URL, logs, audit or Git. Keep an audit of request, confirmation/reset success and failure without secrets.
- Recovery changes only the password. It leaves account lock, roles and Patient links untouched. Revoke all existing sessions and active refresh tokens in the same successful reset transaction; old access and refresh tokens must fail.
- Web entry is `/customer/sign-in` → `/customer/recover` → `/customer/sign-in`. Mobile entry is its Public sign-in screen → recovery → sign-in. The old customer email-verification pages redirect to the account home; their endpoints remain available for compatibility but are not required by recovery. Staff `/sign-in` and `/recover` screens are unchanged.
- Forms distinguish field validation, loading, connection/service failure, invalid proof and success. Disable duplicate submission; retain safe form fields on retry. Do not imply that a nonexistent account received mail. Do not direct customers to a separate email-confirmation screen.

## Acceptance evidence

Use an isolated PostgreSQL test database and Mailpit, not project user data. Cover registration → customer recovery with `EmailConfirmed=false` on Web and Mobile → successful new sign-in; old password and old sessions fail. Cover wrong, expired, used and over-limit codes, resend cooldown, duplicate requests, unknown-email neutral response, locked account, unchanged Patient link/roles, SMTP failure, and absence of SMS. Run backend build/integration tests, Web build/browser tests and Mobile typecheck. Record observed results in `docs/s03-account-recovery-evidence.md`.
