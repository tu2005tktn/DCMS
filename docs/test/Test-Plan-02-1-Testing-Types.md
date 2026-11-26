# 2. Test Strategy

## 2.1 Testing Types

| Testing Type | Objective | Technique | Completion Criteria |
|---|---|---|---|
| Functional Testing | Verify that the main flow and alternative flows of each workflow in the eight modules produce the correct result for the right role | Use case testing, equivalence partitioning, state transition testing | Every workflow has at least one passing test case at each of its designated levels |
| Input Validation Testing | Verify that every validated field rejects invalid input and accepts valid input on both client and server | Boundary value analysis, equivalence partitioning, error guessing | Every field with a validation rule has both a valid case and an invalid case covered |
| Background Job Testing | Verify that each background job (online payment status worker, clinical file scanner) produces the correct database state and side effects when executed | Direct invocation of the job with a seeded precondition | Each background job has at least one integration test case confirming the stored result |
| Performance Testing | Verify that response time and payload size targets are met | Timing in the browser and API requests with a fixed number of samples; target values taken from the non-functional requirements document (`SYSTEM-NFR-2026-11-11.vi.md`) | Actual measurements meet the target for every listed performance requirement |
| Security Testing | Verify that authentication, authorization, branch isolation and role-based data visibility rules hold | Role-permission matrix testing across all roles, branch and customer isolation checks, sign-in with invalid credentials | Every security-related business rule and every role-restricted screen or endpoint returns the correct unauthorized or forbidden response when violated |
| UI Testing (End to End) | Verify that the user-visible outcome of a business flow matches what the interface actually renders | Scenario-based execution through a real browser (Playwright) against the real API and database | Every business flow has at least one passing end-to-end scenario on the web application |
