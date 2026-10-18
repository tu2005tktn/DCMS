# Shared password policy — UC-01, UC-02, UC-31, UC-33

Decision: 18 October 2026. Patient/public accounts and clinic staff use the same password policy.

- New passwords contain 8–256 characters.
- Uppercase letters, lowercase letters, digits and symbols are optional. An eight-character numeric password is accepted.
- Password confirmation must match exactly where requested. Passwords are not trimmed or transformed.
- The policy applies to public registration, Admin-created staff accounts, recovery/reset and development seeding.
- ASP.NET Core Identity enforces the policy. `GET /api/auth/policy` returns `requiredLength: 8`, `maximumLength: 256` and all four composition flags set to `false`. Web and Mobile registration/reset screens render and validate these values.
- Sign-in checks the existing password hash; it does not impose the rules for creating a new password. Changing this policy or `DEV_SEED_PASSWORD` does not replace existing password hashes. Repeated seeding skips existing accounts.
- Default and Development configuration use this baseline. Explicit deployment configuration may override Identity settings, and clients read the effective policy from the API.

This decision replaces the earlier 12-character/composition baseline in GBR-082 and the UC-33/UC-31 implementation notes. HTTP request/response shapes and MSG109 for rejected new passwords are unchanged.

Acceptance: public registration, staff creation and recovery accept an eight-digit password; registration rejects seven characters and more than 256 characters; single-case letters and symbols alone are accepted at eight characters. Confirmation and existing account boundaries continue to apply.
