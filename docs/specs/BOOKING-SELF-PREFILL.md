# Booking contact prefill

Decision: 19 October 2026. Authenticated customers booking for Self should not retype information already available on their account.

- Sign-in user and authenticated `/api/auth/me` add nullable `phoneNumber` and `email` for the current account only. Existing fields/authorization remain unchanged; no other account or patient contacts are exposed.
- Web/mobile prefill booker name and attendee name from current account fullName, and contact from phoneNumber (preferred) or email. Handle session restoration completing after the booking page mounts, not only synchronous initial state.
- Seed each account once, preserve manual edits when moving between booking steps or when `/me` refreshes. If another account becomes active, replace prior account defaults with the new account's values.
- Selecting Relative keeps booker/contact but clears the attendee field for the other person. Switching back to Self fills the account name. Missing values remain editable; anonymous visitors enter details manually. Do not infer contacts or patient identity from username.
- Existing booking validation, verified patient linkage, ownership and confirmation contracts remain authoritative. Editing booking contact fields does not edit the account or link a patient record.
