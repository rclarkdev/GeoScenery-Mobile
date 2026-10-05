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

Accounts receive the `Member` role by default. No user account or password is seeded into a fresh database. The `Admin` role grants explicit application permissions through ASP.NET Core authorization policies; role assignments are stored in the database, never accepted from registration data or trusted from client-supplied claims. Admin user management is available through `GET /api/admin/users` and `PUT /api/admin/users/{id}/roles`. The last administrator cannot be demoted.

To provision the first trusted administrator safely:

1. Apply the database migrations, then create the trusted administrator account through the normal registration flow.
2. Read that account's `userId` from the registration response. Configure `Authorization__BootstrapAdminUserId` with that existing ID in the API environment (or App Service settings).
3. Sign in as that account once. The API grants the `Admin` role and returns a token with role/permission claims. Remove `Authorization__BootstrapAdminUserId` after promotion; the database role assignment remains.

Role changes are checked against current database assignments on protected requests, so revoking Admin access takes effect immediately. Users should sign in again after a role change so their token claims stay current. Never configure the bootstrap ID before the trusted account exists.

Administrators can open the Admin tab to manage paginated user, report, scene, and audit lists (page sizes are limited to 100). The interface supports server-side searches/filters, role assignment, reversible account suspension, reversible scene hiding, report review/resolution, and permanent account/scene deletion. Suspension immediately blocks existing sessions and new logins; suspended users and their scenes are excluded from public/social views. Hidden scenes are excluded from public search/details/visits but remain inspectable in Admin. Report status changes follow `Pending → Reviewed/Dismissed/Actioned` and `Reviewed → Dismissed/Actioned`; terminal statuses cannot be reopened. Changed statuses require a resolution note, and `Actioned` requires action details. Admin mutations and related report resolutions are written to the paginated, read-only audit endpoint at `GET /api/admin/audit`.

Deleting an account removes its relationships, blocks, messages, visits, and ratings; its scenes remain but are detached from the account, and open reports about the removed profile are marked actioned. The last active Admin and the currently signed-in account cannot be deleted or demoted/suspended in a way that removes the final active administrator. Use reversible suspension/hiding for routine moderation; deletion is permanent.

All EF Core migration files, including the baseline, per-migration designer files, and `MyProjectDbContextModelSnapshot.cs`, must be committed together. Validate model parity with `dotnet ef migrations has-pending-model-changes` and review the idempotent SQL Server script before deployment. The optional SQL Server concurrency suite is enabled by `GEOSCENERY_SQLSERVER_TEST_CONNECTION`; it creates and drops a uniquely named disposable database, applies the full migration chain, then checks last-admin and duplicate-report races. Only point it at a SQL Server/LocalDB instance where the test identity may create and drop databases.

## Content reports

Authenticated users can report another user's profile with `POST /api/users/{id}/reports` or a scene with `POST /api/scenes/{id}/reports`. Both requests require a non-empty description (up to 2,000 characters); users cannot report their own profile or scenes. Reports are stored with target and reporter snapshots, limited to five submissions per user per hour, and emailed to every user currently assigned the `Admin` role. Email uses authenticated SMTP through MailKit. Configure `EmailSettings:SmtpClient`, `EmailSettings:SmtpPort`, and `EmailSettings:NetworkCredentials:Username/Password`. Supply credentials through environment variables or a secret store, not committed configuration. Delivery is skipped in Development when settings are incomplete.

### Local SMTP email setup

The API uses the same MailKit SMTP approach as CollectionsOfMineLive. For Gmail, use an account with 2-Step Verification and create an app password for SMTP; no Entra app registration is required. From the repository root, set the SMTP account and app password as .NET user secrets:

```powershell
$project = ".\GeoScenery.Api\GeoScenery.Api.csproj"
dotnet user-secrets init --project $project
dotnet user-secrets set "EmailSettings:NetworkCredentials:Username" "your-smtp-account@gmail.com" --project $project
dotnet user-secrets set "EmailSettings:NetworkCredentials:Password" "your-gmail-app-password" --project $project
```

The SMTP host defaults to `smtp.gmail.com` on port 587. Keep the app password out of source control and chat. Restart the API after setting these values, then use **Resend verification email** for an account created before email was configured. For another SMTP provider, override `EmailSettings:SmtpClient` and `EmailSettings:SmtpPort` as needed. Delivery failures are logged by the API and are not reported to the client as successful sends.

## Account verification and recovery

Registration requires the password and confirmation to match. New accounts cannot sign in until they follow the single-use verification link emailed to the supplied address (expires after 24 hours). Verification and password-reset tokens are never returned by the API or displayed in the app. The registration screen reports when SMTP rejects the email, or when delivery is skipped because SMTP is not configured in Development; a successful response means the SMTP server accepted the message, not that it reached the inbox. Configure the SMTP settings above and check spam folders if needed. Unverified users can request another link from the sign-in screen; resend responses remain generic when no unverified account is found and are rate limited. The verification landing page is configured through `Email:ClientVerificationUrl` (production environment variable `Email__ClientVerificationUrl`). Existing accounts remain verified when the migration is applied. The sign-in screen's forgot-password flow sends a one-hour, single-use reset link through `Email:ClientResetUrl`; successfully resetting a password also verifies mailbox ownership.

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