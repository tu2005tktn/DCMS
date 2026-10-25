# Database design reference

`DCMS_Logical_ERD_37_Tables.mmd` is the supplied logical ERD: 37 table definitions, relationships and business invariants. It is a design reference, not executable SQL. The UC-01/UC-31 migration currently creates account, role, employee, session and account-audit tables; Patient and other clinical tables remain unimplemented.

ASP.NET Core Identity maps int-key users/roles to `USER_ACCOUNT`, `ROLE`, and `USER_ACCOUNT_ROLE`. Its auxiliary tables and `AUTH_SESSION` are authentication implementation details. See [implementation decisions](../implementation/UC01-UC31.md). Preserve remaining composite/conditional keys and transactional rules when implementing later features; Mermaid annotations alone do not enforce them.
