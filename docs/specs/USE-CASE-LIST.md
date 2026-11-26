# Use Case List

Built from the current code of the web app (`web/src`), the mobile app (`mobile/src`) and the API (`backend/src/DCMS.Api/Features`). Actors: Customer, Clinic Assistant, Dentist, Clinic Owner, Admin. "Web" means the web app, "Mobile" the Expo app (customer area and two staff screens). The last column gives the Functional Requirements file; "—" means it is not written yet.

| ID | Use Case | Feature | Use Case Description | FR |
|---|---|---|---|---|
| UC-01 | Sign In / Sign Out | Account and Access | Authenticate an account with an e-mail or user name and a password, establish a session with the permissions of the account, and end the session on sign-out (this device or all devices). | FR-1 |
| UC-02 | Sign In with Google | Account and Access | Authenticate a customer with a Google account when Google sign-in is configured on the server. | FR-1 |
| UC-03 | Register Customer Account | Account and Access | Create a customer account with a name, a Vietnamese mobile number, an e-mail and a password that follows the password policy (web and mobile). | FR-1 |
| UC-04 | Recover Account | Account and Access | Let a customer or a staff member who forgot the password get access to the account again and set a new password (web and mobile). | FR-1 |
| UC-05 | Manage Session Timeout | Account and Access | Sign a staff member out automatically after the idle time set in the general settings, and warn before it happens. | — |
| UC-06 | Choose Access Portal | Account and Access | Let a visitor choose the customer area or the staff area before signing in. | FR-1 |
| UC-07 | Use Staff Workspace | Account and Access | Show the staff menu (Vận hành, Chuyên môn, Quản trị) according to the permissions of the user, and open the screens. | FR-0.1 |
| UC-08 | Receive Notifications | Notifications | Deliver notifications in real time (SignalR) and as push notifications on mobile; list them, see the unread count, mark them as read and open the related screen. | — |
| UC-09 | Book Appointment Online | Customer Booking | Let a customer book an examination in five steps: choose the examinee, the branch, the dentist (or any dentist with a free chair) and a free time slot, confirm the contact, review and confirm (web and mobile). | FR-2 |
| UC-10 | Book for a Relative | Customer Booking | Let a customer book for a saved family member; the booking does not give access to the relative's record. | FR-2 |
| UC-11 | View My Appointments | Customer Booking | List, filter and open the upcoming and past appointments of a customer (web and mobile). | FR-2 |
| UC-12 | Change or Cancel My Appointment | Customer Booking | Let a customer reschedule or cancel an appointment, or send a support request when it is too close to the time, and withdraw a pending request. | FR-2 |
| UC-13 | View Customer Home | Customer Booking | Show the nearest appointment and the shortcuts to the customer functions. | FR-2 |
| UC-14 | Link Account to Patient Record | Customer Record | Let a customer send a request to link the account to an existing patient record, follow its status and withdraw it. | — |
| UC-15 | View My Medical Record | Customer Record | Let a customer see their basic information, examinations and results, treatment and payments, images and prescriptions once the account is linked (web and mobile). | FR-16 |
| UC-16 | View My Prescriptions | Customer Record | List and open the prescriptions issued to the customer (web and mobile). | — |
| UC-17 | Manage Family Members | Customer Record | Add, edit, search and delete the family members saved in the account (web and mobile). | FR-16 |
| UC-18 | View Family Member Record | Customer Record | See the appointments booked for a family member and, when the clinic grants access, the record within the granted scopes (examinations, treatment, prescriptions, documents); request or end the access. | FR-16 |
| UC-19 | Manage Appointment Calendar | Appointments | See the appointments of a branch in a calendar and filter them; open the detail of an appointment. | FR-3 |
| UC-20 | Book Appointment for a Patient | Appointments | Let staff create an appointment for a patient, a new customer or a relative, choosing the dentist or any dentist with a free chair. | FR-3 |
| UC-21 | Edit Appointment | Appointments | Change the time, the dentist or the details of an appointment, and book a replacement appointment. | FR-3 |
| UC-22 | Cancel Appointment / Mark No-show | Appointments | Cancel an appointment with a reason, or record that the patient did not come. | FR-3 |
| UC-23 | Confirm Attendance | Appointments | Record the confirmation that the patient will attend an appointment. | — |
| UC-24 | Book Next Visit | Appointments | Book the next visit of a patient from the profile or from a visit, using the next-visit candidates and the follow-up context. | — |
| UC-25 | Handle Support Request | Appointments | Review the change or cancel requests of customers in the support inbox, verify the requester, apply the change, or reject with a reason (web and mobile). | FR-6 |
| UC-26 | Review Schedule Impact | Appointments | Let staff on mobile see the appointments affected when a working schedule is changed. | — |
| UC-27 | Receive Patient (Check-in) | Reception | Receive a patient who arrives, check the appointment and the record, and put the patient in the queue. | FR-5 |
| UC-28 | Manage Waiting Queue | Reception | Follow the patients waiting, being examined or done, and move them between statuses; show the work list of the day. | FR-5 |
| UC-29 | Manage Patient Records | Patient Records | Search, create, edit, archive and restore patient records, with the duplicate check and the patient code sequence. | FR-4 |
| UC-30 | View Patient Profile | Patient Records | Show the profile of a patient: basic information, examinations, treatment and payments, images and prescriptions. | FR-4, FR-9 |
| UC-31 | Manage Patient Documents | Patient Records | Upload, list, view and delete the images and documents of a patient, in a list or a gallery. | FR-9 |
| UC-32 | Verify Record Link | Patient Records | Check a request to link a record, compare it with the patient record, and approve, reject or revoke it, including the access of a family member. | FR-8 |
| UC-33 | Manage Customer Care | Customer Care | Create, edit and follow care tasks such as reminders and follow-up calls, with notes, contact attempts and escalation. | FR-7 |
| UC-34 | Conduct Examination | Examination | Start a visit, record symptoms, the odontogram, findings and diagnoses, save, amend and complete it. | FR-10 |
| UC-35 | Order Investigations | Examination | Order an investigation (for example an X-ray), receive the result, read it and cancel it. | FR-10 |
| UC-36 | Manage Examination Documents | Examination | Attach and see the images and documents of a visit. | FR-10 |
| UC-37 | Summarise and Close Visit | Examination | Write the conclusion and the advice, choose the next step (follow-up, referral, decide later, continue treatment) and end the visit. | FR-10 |
| UC-38 | View Examination History | Examination | See the earlier visits of a patient and their versions. | FR-9, FR-10 |
| UC-39 | Hand Off to Treatment | Examination | Move from a finished examination to a treatment sheet with the clinical handoff. | FR-10 |
| UC-40 | Manage Treatment Sheet | Treatment | Open the treatment sheets of a patient, plan the items, record the work done, add images and notes; close, reopen, cancel or delete a sheet. | FR-9 |
| UC-41 | Apply Treatment Plan | Treatment | Apply a ready-made treatment plan to a treatment sheet. | FR-9 |
| UC-42 | Order Lab Work for a Treatment | Treatment | Create a lab order for a piece of work on a treatment sheet. | FR-9 |
| UC-43 | Record Materials Used | Treatment | Record the materials used for a piece of work and take them out of the stock. | FR-9 |
| UC-44 | Prescribe Medicine | Treatment | Write, edit, finalise and print a prescription of a visit, with the safety check. | FR-9 |
| UC-45 | Collect Treatment Payment | Treatment | Record a payment or a deposit of a treatment sheet, pay online through PayOS with a QR order, allocate a deposit, and record a deposit exception. | — |
| UC-46 | Close Treatment Sheet | Treatment | Close a treatment sheet with the result of the treatment, resolving each unfinished item first. | FR-9 |
| UC-47 | Manage Treatment Plans | Treatment | Create, rename, edit and delete the ready-made treatment plans of a branch. | FR-13 |
| UC-48 | Manage Lab Work | Treatment | Follow the lab orders by status, move an order through sent, received, delivered or rework with a history, and open the order. | FR-14 |
| UC-49 | Set Service Prices | Treatment | Set the reference price per unit of each treatment service and review the commercial treatment services. | — |
| UC-50 | Manage Working Schedule | Scheduling | Set the weekly template and the exceptions of a working schedule, check the effect on appointments and publish. | FR-11 |
| UC-51 | Preview Availability | Scheduling | Preview the free time slots that a working schedule and the chairs give. | FR-11 |
| UC-52 | Configure Booking Settings | Scheduling | Set, for a branch, the services that can be booked online and their durations. | — |
| UC-53 | Manage Materials Stock | Inventory | Record the purchases of materials and instruments with the supplier, the batch and the payment; follow the stock and the usage history. | — |
| UC-54 | Manage Medicines | Catalogue | Manage the catalogue of medicines used in prescriptions and export it. | — |
| UC-55 | Manage Catalogues | Administration | Maintain the catalogues of the system: diagnoses, customer groups, material suppliers, material types, lab companies and lab work types, with history. | — |
| UC-56 | Manage Branches and Chairs | Administration | Edit the information, the logo and the transfer account of a branch, and manage its treatment chairs. | — |
| UC-57 | Configure General Settings | Administration | Set the patient code sequence, the required patient fields, the receipt template and the idle sign-out time, with the version history and restore. | — |
| UC-58 | Manage Access Groups | Administration | Create access groups with permissions, assign them to staff, and see the effect and the history of the change. | — |
| UC-59 | Manage Accounts | Administration | List, create, edit, lock and unlock the accounts, assign roles, and see the audit of an account. | — |
| UC-60 | View Settings Audit | Administration | Search the history of the changes of the settings by group and date. | — |
| UC-61 | View Work/Service Statistics | Reports | See the work and services of the treatment sheets with the cost, the paid and the remaining amount, by period, branch and search. | FR-12 |
| UC-62 | View Finance Statistics | Reports | See the amount to collect, the money received, the advance, the receivables, the cost of materials and the profit, and open the list behind each figure. | FR-15 |
| UC-63 | Forfeit Deposit of a No-show | Reports | Record that a customer did not come, keep the deposit and hide the treatment sheet. | FR-15 |

Notes:
- The API also has endpoints for plan revisions, treatment steps (hold, release, complete) and item decisions (discount, warranty), but no web or mobile screen uses them now, so they are not listed.
- I did not open every screen in `settings`, `materials` and `accounts`; their descriptions follow the labels and endpoints and should be checked before the FR is written.
