# Self-booking prefill repair

The name fields previously initialized only when the component mounted, so async session hydration left them empty. Contact was always initialized blank and `/me` did not expose phone/email.

Add current-account nullable contacts to CurrentUser and web/mobile DTOs. Seed booking fields once per authenticated account, with relation-aware attendee handling. Preserve edits on subsequent session reads and booking navigation. Test the API current-account response, restored sessions, phone/email fallback, relative switching and draft preservation. Verify web build and mobile typecheck; mobile runtime is not implied by typechecking.

Validation: backend/web builds and mobile typecheck passed. One isolated PostgreSQL API test verified sign-in and `/me` return only the authenticated account's matching phone/email and anonymous access is rejected. Eleven mocked Playwright checks passed, including delayed session hydration, phone priority, email fallback, preserving manual edits after session refresh/back navigation, relative attendee clearing and switching back to Self. API was restarted for local testing. No database migration was needed.
