# GeoScenery

GeoScenery is an Ionic/Capacitor client backed by an ASP.NET Core API and SQL database.

## Local validation

```powershell
dotnet test GeoScenery.Tests
cd ClientApp
npm ci
npm test -- --watch=false --browsers=ChromeHeadless
npm run build -- --configuration production
```

## Roles and permissions

Accounts receive the `Member` role by default. The built-in `Master` account is seeded with the `Admin` role. The `Admin` role grants explicit application permissions through ASP.NET Core authorization policies; role assignments are stored in the database, never accepted from registration data or trusted from client-supplied claims. Admin user management is available through `GET /api/admin/users` and `PUT /api/admin/users/{id}/roles`. The last administrator cannot be demoted.

To provision another trusted administrator safely:

1. Apply the database migration, then create the trusted administrator account through the normal registration flow.
2. Read that account's `userId` from the registration response. Configure `Authorization__BootstrapAdminUserId` with that existing ID in the API environment (or App Service settings).
3. Sign in as that account once. The API grants the `Admin` role and returns a token with role/permission claims. Remove `Authorization__BootstrapAdminUserId` after promotion; the database role assignment remains.

Role changes are checked against current database assignments on protected requests, so revoking Admin access takes effect immediately. Users should sign in again after a role change so their token claims stay current. Never configure the bootstrap ID before the trusted account exists.

Administrators can open the Admin tab to manage paginated user, report, scene, and audit lists (page sizes are limited to 100). The interface supports server-side searches/filters, role assignment, reversible account suspension, reversible scene hiding, report review/resolution, and permanent account/scene deletion. Suspension immediately blocks existing sessions and new logins; suspended users and their scenes are excluded from public/social views. Hidden scenes are excluded from public search/details/visits but remain inspectable in Admin. Report status changes follow `Pending → Reviewed/Dismissed/Actioned` and `Reviewed → Dismissed/Actioned`; terminal statuses cannot be reopened. Changed statuses require a resolution note, and `Actioned` requires action details. Admin mutations and related report resolutions are written to the paginated, read-only audit endpoint at `GET /api/admin/audit`.

Deleting an account removes its relationships, blocks, messages, visits, and ratings; its scenes remain but are detached from the account, and open reports about the removed profile are marked actioned. The last active Admin and the currently signed-in account cannot be deleted or demoted/suspended in a way that removes the final active administrator. Use reversible suspension/hiding for routine moderation; deletion is permanent.

All EF Core migration files, including the baseline, per-migration designer files, and `MyProjectDbContextModelSnapshot.cs`, must be committed together. Validate model parity with `dotnet ef migrations has-pending-model-changes` and review the idempotent SQL Server script before deployment. The optional SQL Server concurrency suite is enabled by `GEOSCENERY_SQLSERVER_TEST_CONNECTION`; it creates and drops a uniquely named disposable database, applies the full migration chain, then checks last-admin and duplicate-report races. Only point it at a SQL Server/LocalDB instance where the test identity may create and drop databases.

## Content reports

Authenticated users can report another user's profile with `POST /api/users/{id}/reports` or a scene with `POST /api/scenes/{id}/reports`. Both requests require a non-empty description (up to 2,000 characters); users cannot report their own profile or scenes. Reports are stored with target and reporter snapshots, limited to five submissions per user per hour, and emailed to every user currently assigned the `Admin` role. Configure the existing `Email` SMTP settings for delivery in production; development logs report notification details instead.

## Account verification and recovery

Registration requires the password and confirmation to match. New accounts cannot sign in until they follow the single-use verification link emailed to the supplied address (expires after 24 hours). Unverified users can request another link from the sign-in screen; resend responses are generic and rate limited. The verification landing page is configured through `Email:ClientVerificationUrl` (production environment variable `Email__ClientVerificationUrl`). Existing accounts remain verified when the migration is applied. The sign-in screen's forgot-password flow sends a one-hour, single-use reset link through `Email:ClientResetUrl`; successfully resetting a password also verifies mailbox ownership.

## Request logging

Each non-health HTTP request writes one structured row to `AppLogEntries`, including correlation ID, method/path (never query string or body), status, duration, user ID when authenticated, outcome, and endpoint. Client errors are warnings; server exceptions are errors and return a generic Problem Details response with a trace ID. Successful health probes are omitted to avoid noisy logs, while failed health checks are recorded. Operation-specific outcomes are added to the same row rather than writing a second audit row. If the database log write itself fails, the request still completes and the logging failure is emitted through the configured application logger.

## Azure deployment

The repository includes:

- `infra/main.bicep` for an HTTPS-only Linux App Service with a system-assigned identity and `/health/ready` health checks.
- `.github/workflows/ci.yml` for backend tests and production frontend builds.
- `.github/workflows/deploy-api.yml` for a manual API deployment using GitHub OIDC.
- `.github/workflows/migrate-production.yml` for a manual EF Core migration run.

Configure the production App Service settings from `GeoScenery.Api/appsettings.Production.template.json`. Store database, JWT, and SMTP secrets in Azure App Service configuration or GitHub environment secrets; do not commit them.

The production client must be built with the deployed API URL in `ClientApp/src/environments/environment.prod.ts`.