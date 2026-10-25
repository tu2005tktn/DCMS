# Database design reference

`DCMS_Logical_ERD_37_Tables.mmd` is the supplied logical ERD: 37 table definitions, relationships and business invariants. It is a design reference, not executable SQL. The UC-01/UC-31 migration currently creates account, role, employee, session and account-audit tables; Patient and other clinical tables remain unimplemented.

## Use case view — Administrator

`DCMS_UseCase_Administrator_View.puml` is the editable source of the Administrator actor view; the rendered images are `DCMS_UseCase_Administrator_View.svg` and `.png`.

Conventions used (task rules): only human actors — no `Backend`/`App`/`Timer` actors; no `<<include>>`/`<<extend>>` relationships; Administrator links only `UC-01` (Sign In / Sign Out) and `UC-02` (Recover Account Access), so the view carries no default clinical, stock, collection or report permission. Sign-in/recovery sub-scenarios are not split into separate use cases. Use case IDs come from `DCMS_Use_Cases_Business_Rules_Messages_v2.0.docx` (15 October 2026).

Re-render after editing the source: `java -jar plantuml.jar -tsvg docs/architecture/DCMS_UseCase_Administrator_View.puml`.

ASP.NET Core Identity maps int-key users/roles to `USER_ACCOUNT`, `ROLE`, and `USER_ACCOUNT_ROLE`. Its auxiliary tables and `AUTH_SESSION` are authentication implementation details. See [implementation decisions](../implementation/UC01-UC31.md). Preserve remaining composite/conditional keys and transactional rules when implementing later features; Mermaid annotations alone do not enforce them.
