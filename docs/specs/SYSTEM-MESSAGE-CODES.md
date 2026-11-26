# System message codes (22 November 2026)

Decision: the codes in [DCMS_System_Messages.docx](DCMS_System_Messages.docx) are the only message codes the system uses. Every message the backend returns, web and mobile show, and tests check uses these codes, numbered MSG01 to MSG116 in table order.

## Rules

- The backend catalog `backend/src/DCMS.Application/Common/SystemMessages.cs` holds MSG01–MSG60 and MSG93–MSG116 with the exact wording of the document. MSG43 (no matching patient) is shown only by the web patient search.
- MSG61–MSG92 replace the earlier text codes (`GOOGLE_*`, `FAMILY_*`, `PURCHASE_*`, `PAYOS_*` and the others). Their wording, HTTP status and `field` stay the same; only `code` changes.
- MSG93–MSG116 are the payment and PayOS messages that used to be returned as plain text. The payment endpoints now return them as a system message body `{ "code": "MSGnn", "message": "..." }` with the same HTTP status (400 or 409). Web shows `message`, so payment errors are now shown with their real wording instead of the generic connection message.
- Catalog entries that no code returned (old MSG22, 40, 44, 94, 114, 127, 203, 229, 231, 234–237, 241, 244, 245, 249, 255, 256, 258) are removed. Their old numbers are reused by the new numbering, so keeping them would make one code mean two things.
- Web and mobile compare codes (for example the "data changed, reload" code, now MSG41, and the connection-failure code, now MSG49). They use the new codes. The support inbox no longer handles old MSG36 and MSG94, which the backend never sent.
- Results saved before this change (replayed command receipts) may still contain an old code in their stored message. A replay returns that stored body unchanged.

- MSG117–MSG121 were added after the Word table and are not in it yet: MSG117 not enough chairs for a working-schedule interval, MSG118 one open booking per person, MSG119–MSG121 refusals of "undo no-show" (another day, interval booked again, treatment sheet removed; MSG121 and MSG118 also refuse moving a missed appointment).

## Old codes

Older specifications, implementation notes, evidence files and the v2.5 Word documents use the old numbering. [SYSTEM-MESSAGE-CODE-MAP.md](SYSTEM-MESSAGE-CODE-MAP.md) maps each new code to its old code. Those older documents are not rewritten.

## Acceptance

- The backend, web and mobile contain no message code outside MSG01–MSG121 (test mocks that invent their own codes excepted).
- Each backend catalog entry from MSG01 to MSG116 has the same number and wording as the Word table.
- A payment request that breaks a rule (for example paying other services before 30% of a lab fee) returns its MSG code and wording, and the payment dialog shows that wording.
- The backend builds and its tests pass; the web builds.
