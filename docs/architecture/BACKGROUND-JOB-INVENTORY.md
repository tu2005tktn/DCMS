# Background Job Inventory

Updated 20 November 2026. Background jobs of DCMS, matching Report 3 (Software Requirement Specification). The jobs run inside the DCMS API as hosted services without a user request. External services they call are described in the [External API Inventory](EXTERNAL-API-INVENTORY.md).

## Summary

| # | Job Name | Group | Description |
|---|---|---|---|
| 1 | Process appointment events and plan reminders | Event-driven | Reads new appointment events (booked, rescheduled, cancelled, no-show, support request handled) from the outbox every 15 seconds. Sends the matching status notification to linked accounts, cancels pending reminders of the old appointment version, and creates new reminders 24 hours and 2 hours before the start time for Confirmed appointments. |
| 2 | Send due appointment reminders | Scheduled | Checks pending reminders every 15 seconds and sends those whose due time has passed. Skips a reminder and marks it Superseded when the appointment has changed, is no longer Confirmed, or the recipient is no longer a verified linked account. |
| 3 | Dispatch notifications and retry failed deliveries | Scheduled / Queue-based | Sends queued push notifications to registered devices through Firebase Cloud Messaging, about 1 minute after the notification is created. A failed delivery is retried after 5 and then 15 minutes, up to 3 attempts. After the last failure, the notification is marked as failed and a staff handling item is created. |
| 4 | Create missed-appointment care tasks | Scheduled | Runs every hour. For each appointment that staff marked as No-show, creates one Missed appointment care task so staff can contact the patient. Does not create duplicate tasks and does not send messages. |
| 5 | Reconcile PayOS payments | Scheduled | Runs every 15 seconds when PayOS is configured. Queries PayOS for payment orders created in the last 2 hours that are not yet recorded, and records the payment only when PayOS confirms it as paid. Acts as a fallback when the payment webhook is missed. |
| 6 | Finalize pending clinical documents | Scheduled | Runs every 30 seconds and finishes clinical documents still in Pending scan status from before virus scanning was removed. A file whose content still matches its stored SHA-256 becomes Ready; a changed or damaged file is Rejected. |

## 5.1 Process Appointment Events and Plan Reminders (JOB-01)

*Trigger*: Event-driven — after an appointment is booked, rescheduled, cancelled, marked as no-show or checked in, or after a support request is resolved or rejected. Each change writes an event to the outbox in the same transaction; the job reads new events every 15 seconds.

*Purpose*: Informs patients of every appointment change and keeps reminders consistent with the current appointment.

*Processing steps*:

1. Read up to 50 unprocessed appointment events in the order they occurred.
2. For a support request event, notify the requester that the request was resolved or rejected, then mark the event as processed.
3. For an appointment event, mark all pending reminders of the appointment as Superseded.
4. If the appointment is still Confirmed, create reminders 24 hours and 2 hours before the start time for the booking account and every verified linked account. Skip a reminder whose time has already passed.
5. Except for check-in events, create the matching in-app notification (booked, rescheduled, cancelled or no-show) for each recipient, and queue a push notification when the recipient has a registered device.
6. Mark the event as processed.

*Failure behavior*:

| Scenario | System behavior |
|---|---|
| Database unavailable | Logs the failure and stops the run. Unprocessed events stay pending and are processed in the next run 15 seconds later. |
| The same event is processed again after a failure | Does not create duplicate notifications or reminders. Each notification has a unique key per event and account, and each reminder is unique per appointment version, lead time and account. |
| Appointment changes again before its event is processed | Uses the latest appointment data. Reminders of older versions are superseded, so only reminders for the current schedule remain. |
| Appointment no longer exists | Marks the event as processed without sending a notification. |
| Appointment starts in less than 2 hours | Creates no reminder for lead times that have already passed. This is normal and expected. |
| No booking or linked account to notify | Completes without notifications. This is normal and expected. |

## 5.2 Send Due Appointment Reminders (JOB-02)

*Trigger*: Scheduled — every 15 seconds, for reminders whose due time has passed.

*Purpose*: Reminds patients of upcoming appointments 24 hours and 2 hours before the start time, only while the appointment is still valid.

*Processing steps*:

1. Find up to 50 pending reminders whose due time has passed, earliest first.
2. Reload the appointment and check that its version has not changed, its status is still Confirmed, and the recipient is still the booking account or a verified linked account.
3. If any check fails, mark the reminder as Superseded.
4. Otherwise create the in-app reminder with the appointment code and start time (Vietnam time) and a link to the appointment, and queue a push notification when the recipient has a registered device.
5. Mark the reminder as Sent and record the processing time.

*Failure behavior*:

| Scenario | System behavior |
|---|---|
| Database unavailable | Logs the failure. Reminders stay pending and are sent in the next successful run. |
| Appointment rescheduled or cancelled after the reminder was planned | Marks the reminder as Superseded and does not send it. A rescheduled appointment receives new reminders through JOB-01. |
| Recipient's link to the patient revoked | Marks the reminder as Superseded and does not send it. |
| System was stopped when the reminder became due | Sends the reminder in the first run after the system restarts. |
| The same reminder is processed twice | Creates only one notification; each reminder has a unique notification key. |

## 5.3 Dispatch Notifications and Retry Failed Deliveries (JOB-03)

*Trigger*: Scheduled / Queue-based — every 15 seconds, for push deliveries whose next attempt time has passed. A new push delivery is queued when a notification is created for an account with a registered device and is first due about 1 minute later.

*Purpose*: Delivers notifications to patients' phones reliably without delaying or blocking the in-app notification.

*Processing steps*:

1. Find up to 50 pending or retrying push deliveries whose next attempt time has passed, earliest first.
2. Load the notification. If it no longer exists, mark the delivery as Failed.
3. Send the notification through Firebase Cloud Messaging to each registered device of the account (at most 20).
4. Increase the attempt count. If sending succeeds, mark the delivery as Delivered.
5. If sending fails on the first or second attempt, mark the delivery as Retrying and schedule the next attempt 5 minutes, then 15 minutes later.
6. If the third attempt fails, mark the delivery as Failed, flag the notification as having a delivery failure, and create a staff handling item with the attempt count and last error.

*Failure behavior*:

| Scenario | System behavior |
|---|---|
| Push service unavailable or rejects the message | Retries after 5 and then 15 minutes. After 3 failed attempts, creates a staff handling item. The in-app notification is kept. |
| Device is no longer registered with the push service | Removes the device from the account so it never receives another account's notifications. This is not counted as a failure. |
| Account has no registered device | Completes the delivery without sending anything. This is normal and expected. |
| Push service credential file missing on the server | Each attempt fails and ends in a staff handling item after 3 attempts. In-app notifications still work. |
| Database unavailable | Logs the failure. Deliveries stay due and are sent in the next successful run. |

## 5.4 Create Missed-Appointment Care Tasks (JOB-04)

*Trigger*: Scheduled — every hour, and once when the system starts.

*Purpose*: Makes sure every patient who missed an appointment is followed up by customer care staff.

*Processing steps*:

1. Take the scheduling lock so the job does not run at the same time as schedule and appointment changes.
2. Read all no-show events in batches of 500.
3. Skip an appointment that already has a Missed appointment care task.
4. Skip an appointment that is no longer No-show or has no patient record.
5. Create a Missed appointment care task in the appointment's branch, due immediately, asking staff to contact the patient.
6. Save all new tasks together and release the lock.

*Failure behavior*:

| Scenario | System behavior |
|---|---|
| Database unavailable | Logs the failure. No task from the failed run is saved; the next hourly run creates them. |
| Care task already exists for the appointment | Skips the appointment. The database also allows only one Missed appointment task per appointment. |
| Appointment has no patient record | Creates no task. |
| No new no-show appointments | Completes without changes. This is normal and expected. |

## 5.5 Reconcile PayOS Payments (JOB-05)

*Trigger*: Scheduled — every 15 seconds while PayOS is configured.

*Purpose*: Records bank transfer payments even when the payment notification (webhook) from PayOS is delayed or missed.

*Processing steps*:

1. Skip the run if the PayOS keys are not configured.
2. Find up to 20 PayOS payment orders created in the last 2 hours, whose expiry passed less than 30 minutes ago, and that are not yet recorded, newest first.
3. Ask PayOS for the status of each order.
4. If the order is not paid, make no change.
5. Check that the order code, payment link, amount and amount paid match the order, and that the treatment items or lab fees are still due.
6. Record the payment in the treatment ledger in a single transaction while the treatment dossier is locked, unless it has already been recorded.

*Failure behavior*:

| Scenario | System behavior |
|---|---|
| PayOS unavailable | Makes no change and checks the order again in the next run until the 2-hour window ends. Staff can also check the order status manually. |
| Paid amount or order details do not match | Does not record the payment and marks the order for staff review. |
| Webhook and job process the same payment at the same time | Records the payment only once. |
| Treatment items were paid another way in the meantime | Does not record the payment and marks the order for staff review. |
| Payment confirmed after the 2-hour window | Not reconciled by the job; recorded when the PayOS webhook arrives or when staff check the order. |
| Database unavailable | Logs the failure and checks the orders again in the next run. |

## 5.6 Finalize Pending Clinical Documents (JOB-06)

*Trigger*: Scheduled — every 30 seconds.

*Purpose*: Releases clinical documents left in Pending scan status after virus scanning was removed on 20 November 2026, and rejects files whose content has changed. New uploads become Ready immediately after the file type and size checks and do not need this job.

*Processing steps*:

1. Find up to 20 documents in Pending scan status, oldest upload first.
2. Read each file from private document storage.
3. Compare the file's SHA-256 hash with the hash stored at upload.
4. If the hash matches, set the document to Ready and record that the virus scan was skipped. The patient is notified that a new document is available.
5. If the hash does not match, set the document to Rejected with the reason "Tệp không đạt kiểm tra an toàn." and record the rejection.
6. Save the change only if the document is still in Pending scan status.

*Failure behavior*:

| Scenario | System behavior |
|---|---|
| File cannot be read from storage | Skips the document. It stays in Pending scan status and is tried again in the next run. |
| File content changed after upload | Rejects the document; it cannot be opened or reviewed. |
| Document changed by another request during processing | Keeps the other change and does not overwrite it. |
| Database unavailable | Logs the failure and processes the documents in the next run. |
| No pending documents | Completes without changes. This is normal and expected. |
