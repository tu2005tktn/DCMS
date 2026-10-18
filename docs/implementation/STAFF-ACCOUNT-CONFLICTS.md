# Staff account conflicts — 18 October 2026

Contract: [S04 / UC-31](../specs/S04-UC31.md).

The reported POST `/api/accounts` HTTP 409 carries MSG139. Existing code collapses duplicate username, employee code, normalized email and normalized phone into a generic message that suggests account recovery. The browser cannot identify the field from that response.

Add optional `field` to system messages, omitted when absent. Staff administration maps known duplicate fields to a specific message. Application checks and database unique-constraint handling use the same field mapping. Web preserves the form draft and attaches the error to the named input. Unknown conflicts keep a general alert. No schema migration or change to contact uniqueness is required.

Verify distinct-field conflicts, normalized values, database constraint mapping and successful creation after correcting a duplicate through the browser. Preserve transaction rollback and existing audit/role/account boundaries.

The operator subsequently confirmed the reported submission reused an existing username. HTTP 409 was appropriate; the repair makes that reason actionable on the form.

Validation: 30/30 account API integration tests and 40/40 unit tests passed. The real API/database Playwright case for duplicate contact identifies `email`, marks the input invalid, preserves the draft, and succeeds after correction while suppressing double-submit (1/1 passed). Four new integration cases cover username, employee code, email and phone collisions, including normalization and unchanged account counts. Backend/Web builds passed; pre-existing package and bundle-size warnings remain. The restarted API continues to select the original `public` schema.
