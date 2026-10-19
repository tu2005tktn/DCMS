# Direct appointment detail from calendar

Date: 19 October 2026.

Calendar appointments open `/appointments/{id}`. Reuse the staff appointment detail rendering and load support requests filtered by that appointment ID on the server, with all persisted statuses and pagination. Reuse RequestDrawer for processing a selected request on the same page. Successful commands reload both appointment details and the scoped list. No request is created automatically. Check-in remains outside the implemented support workflow.

The optional support list filter preserves staff branch authorization. Wait for staff session restoration before redirecting protected routes, so refresh preserves the appointment target. Loading, read failure/retry and empty requests remain distinct. Readers without support permission see appointment details only.

Validation: web build; browser calendar-to-detail, refresh, scoped requests, empty/error and permission states; integration test for appointment filtering and access restrictions.

Results: .NET restore/build passed. SupportNotificationApiTests passed 4/4 against PostgreSQL in an isolated test schema, including appointment filtering before pagination and branch/public-account restrictions. Playwright staff detail checks passed 3/3 with mocked API responses (exact calendar target, refresh, request verification in place, permission gating, read failure and retry/empty state); customer portal regression passed 8/8. RequestDrawer retains its authoritative command response while the parent refreshes appointment and request-list data. Web build passed; API was restarted on 5080 and API/database health checked successfully.
