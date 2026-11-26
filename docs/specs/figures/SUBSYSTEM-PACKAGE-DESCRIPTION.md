# Mô tả Sub-system Package Diagram của DCMS

Sơ đồ: [SUBSYSTEM-PACKAGE-DIAGRAM.svg](SUBSYSTEM-PACKAGE-DIAGRAM.svg)

| No | Package | Description |
|---|---|---|
| 1 | Web | React 19, TypeScript, Vite and Ant Design application: staff workspace and customer portal (`/customer`). Calls the backend through REST and SignalR. |
| 2 | Mobile | Expo / React Native application for customers and a few staff screens. Calls the backend through REST and receives push notifications. |
| 3 | Api | Folder `DCMS.Api`. ASP.NET Core minimal-API endpoint groups per business area ({Subject}Endpoints, e.g. AppointmentEndpoints), SignalR hub `/hubs/notifications`, authentication and permission checks, and the PayOS reconcile worker. Depends on Application and, for composition, Infrastructure. |
| 4 | Application | Folder `DCMS.Application`. Use-case contracts per business area: I{Name}Service interfaces, {Subject}Contracts (DTOs), screen and action grants, and Vietnamese system messages (`SystemMessages`, MSGxx). Depends on Domain. |
| 5 | Infrastructure | Folder `DCMS.Infrastructure`. Implementations of the application contracts: {Name}Service, background workers ({Name}Worker) and adapters to external systems (SMTP, Google, Firebase, Dental AI, ClamAV). Depends on Application and Domain. |
| 6 | Persistence | EF Core / Npgsql: `DcmsDbContext`, {Area}Model table mappings and migrations for PostgreSQL. Part of Infrastructure. Depends on Domain. |
| 7 | Domain | Folder `DCMS.Domain`. Entities, status values and business rules per area, e.g. Appointment, Visit, TreatmentDossier. Depends on no other packages. |
| 8 | External systems | PostgreSQL (data), Firebase (push), PayOS (QR payment and webhook), Google Identity (sign-in), SMTP (recovery email), local file storage (clinical documents), Dental AI service (caries detection), ClamAV (optional virus scan). |

Dependency direction: Web / Mobile → Api → Application → Infrastructure → Domain.
