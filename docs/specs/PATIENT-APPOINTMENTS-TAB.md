# S06 patient appointments

Decision: 19 October 2026. Replace the patient profile's S11/S12 placeholder with persisted appointment data.

`GET /api/patients/{id:int}/appointments?scope=ALL&page=1&pageSize=20` requires the existing staff patient-read policy. Scope is `ALL`, `UPCOMING` or `HISTORY`; page sizes are 20/50/100. Response is the shared staff AppointmentListPage. Invalid filters return 400 MSG04, missing patient 404 MSG113, and unauthorized staff/public requests are denied by existing policies.

Select only appointments with `Appointment.PatientId == id`. Do not infer a patient match from attendee/booker names, phone numbers or account IDs. Unlinked bookings are not shown. Recompute appointment visibility from active staff branch memberships: assistants/owners/schedule managers see their branches; dentists otherwise see their own appointments. The temporary local dentist role grants continue to operate through these same checks. Only active branches are included. Archived patient profiles retain appointment history.

UPCOMING means EndAt is in the future and status is Confirmed, CheckedIn or InProgress. HISTORY is the complement; ALL includes both. Upcoming sorts by StartAt ascending; other scopes sort descending, with Id as a stable tie-breaker. Apply patient and actor filters before count/pagination. This read never creates or changes a booking.

The tab loads when selected, shows time in UTC+7, code, status, branch/chair, dentist and service, and links each code directly to `/appointments/{id}`. Scope changes reset pagination. Distinguish loading, valid empty result and failed read with retry; support manual refresh. Other unimplemented patient tabs retain their own placeholders.
