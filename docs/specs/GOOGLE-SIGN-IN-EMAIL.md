# Google sign-in — email uniqueness

Applies to `POST /api/auth/google/sign-in` (Public accounts, `Guest` role).

- A Google `sub` that is already linked to an account signs in to that account. The linked account must be `Public`, otherwise the API returns 403/MSG06. The check does not look at the account's current email, so an account whose email was later cleared can still sign in.
- A new Google `sub` creates an account only when no account has the same normalized email, compared case-insensitively. Otherwise the API returns **409 `GOOGLE_EMAIL_EXISTS`** and creates nothing. A matching email never links or signs in to another account.
- The new account is `Public` with the `Guest` role, `EmailConfirmed = true` because Google verified it, and a `GoogleRegister` audit row.
- This matches password registration ([UC33-S02-MOBILE.md](UC33-S02-MOBILE.md)), where a duplicate email returns 409/MSG139. Normalized email is therefore unique across Public accounts.
- Any other Identity failure while creating the account returns 409 `GOOGLE_CONFLICT`.
- Web and Mobile show the returned message. They suggest signing in with the original method or choosing another Google account, and do not reveal which account owns the email.
